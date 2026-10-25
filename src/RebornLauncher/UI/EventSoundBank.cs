using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Threading;

namespace PokemonRebornLauncher.UI;

/// <summary>Small, opt-in pool for locally cached seasonal interaction effects.</summary>
public sealed class EventSoundBank : IDisposable
{
    public const double DefaultVolume = 0.12;
    private const int MaxVoices = 4;
    private static readonly string[] Cues =
    [
        "snow-crunch", "snowball-hit", "snow-poof", "ice-break", "rocket-launch", "firework-boom", "sparkle", "confetti",
        "egg-crack", "bird-chirp", "rustle", "chocolate-snap", "ghost", "wood-creak", "pumpkin-pop", "bat-flap",
        "cauldron-bubble", "bell", "gift-rip", "glass-break", "water-pop", "leaf-rustle", "magic", "thunder", "wind"
    ];

    private sealed class Voice(MediaPlayer player, DateTime started)
    {
        public MediaPlayer Player { get; } = player;
        public DateTime Started { get; } = started;
    }

    private readonly Dispatcher dispatcher = Dispatcher.CurrentDispatcher;
    private readonly List<Voice> voices = [];
    private readonly Dictionary<string, DateTime> lastPlayed = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string[]> variants = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> nextVariant = new(StringComparer.Ordinal);
    private bool enabled;
    private bool paused;
    private bool disposed;
    private double volume = DefaultVolume;

    public EventSoundBank() { }

    /// <summary>Configures event effects independently from launcher music.</summary>
    public void Configure(bool enabled, double volume, bool paused)
    {
        VerifyAccess();
        if (disposed) return;
        this.enabled = enabled;
        this.volume = double.IsFinite(volume) ? Math.Clamp(volume, 0d, 1d) : DefaultVolume;
        this.paused = paused;
        foreach (var voice in voices.ToArray())
        {
            if (!enabled || paused) StopAndRemove(voice);
            else voice.Player.Volume = this.volume;
        }
    }

    /// <summary>Plays a known effect if enabled. Unknown cues and rapid repeats are ignored.</summary>
    public void Play(string cue)
    {
        VerifyAccess();
        if (disposed || !enabled || paused || string.IsNullOrWhiteSpace(cue)) return;
        cue = cue.Trim().ToLowerInvariant();
        if (!Cues.Contains(cue, StringComparer.Ordinal)) return;
        var now = DateTime.UtcNow;
        if (lastPlayed.TryGetValue(cue, out var previous) && now - previous < TimeSpan.FromMilliseconds(80)) return;
        lastPlayed[cue] = now;

        Voice? voice = null;
        try
        {
            if (!variants.TryGetValue(cue, out var files)) variants[cue] = files = LoadVariants(cue);
            if (files.Length == 0) return;
            var index = nextVariant.GetValueOrDefault(cue) % files.Length;
            nextVariant[cue] = index + 1;
            while (voices.Count >= MaxVoices) StopAndRemove(voices.MinBy(v => v.Started)!);

            var player = new MediaPlayer { Volume = volume };
            voice = new Voice(player, now);
            player.MediaEnded += OnMediaEnded;
            player.MediaFailed += OnMediaFailed;
            player.MediaOpened += OnMediaOpened;
            voices.Add(voice);
            player.Open(new Uri(EnsureCache(files[index]), UriKind.Absolute));
        }
        catch
        {
            if (voice is not null) StopAndRemove(voice);
            // A missing/unsupported optional effect must never affect the launcher's UI.
        }
    }

    private static string[] LoadVariants(string cue)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var prefix = $"PokemonRebornLauncher.events.{cue}-";
        return assembly.GetManifestResourceNames()
            .Where(name => name.StartsWith(prefix, StringComparison.Ordinal) && name.EndsWith(".wav", StringComparison.OrdinalIgnoreCase))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
    }

    private static string EnsureCache(string variant)
    {
        var resourceName = variant;
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName)
            ?? throw new FileNotFoundException($"Event-Sound-Ressource '{resourceName}' fehlt.");
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        var payload = buffer.ToArray();
        var digest = Convert.ToHexString(SHA256.HashData(payload)).ToLowerInvariant();
        var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PokemonRebornLauncher", "audio", "events");
        Directory.CreateDirectory(directory);
        var fileName = Path.GetFileNameWithoutExtension(variant).Replace('.', '-');
        var target = Path.Combine(directory, $"{fileName}-{digest[..16]}.wav");
        if (File.Exists(target))
        {
            using var existing = File.OpenRead(target);
            if (CryptographicOperations.FixedTimeEquals(SHA256.HashData(existing), SHA256.HashData(payload))) return target;
        }
        var temporary = Path.Combine(directory, $".{Guid.NewGuid():N}.tmp");
        try { File.WriteAllBytes(temporary, payload); File.Move(temporary, target, overwrite: true); }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
        return target;
    }

    /// <summary>Opens every embedded effect through WPF's decoder while muted, without playing it.</summary>
    internal static async Task<int> VerifyMutedCodecsAsync()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resources = assembly.GetManifestResourceNames()
            .Where(name => Cues.Any(cue => name.StartsWith($"PokemonRebornLauncher.events.{cue}-", StringComparison.Ordinal)) && name.EndsWith(".wav", StringComparison.OrdinalIgnoreCase))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        if (resources.Length != Cues.Length * 2)
            throw new InvalidDataException($"Erwartet wurden {Cues.Length * 2} eingebettete Event-WAVs, gefunden wurden {resources.Length}.");

        foreach (var resource in resources)
        {
            var player = new MediaPlayer { Volume = 0, IsMuted = true };
            var opened = new TaskCompletionSource<TimeSpan>(TaskCreationOptions.RunContinuationsAsynchronously);
            EventHandler openedHandler = (_, _) =>
            {
                if (!player.NaturalDuration.HasTimeSpan || player.NaturalDuration.TimeSpan <= TimeSpan.Zero)
                    opened.TrySetException(new InvalidDataException($"Event-WAV '{resource}' hat keine positive Dauer."));
                else opened.TrySetResult(player.NaturalDuration.TimeSpan);
            };
            EventHandler<ExceptionEventArgs> failedHandler = (_, args) => opened.TrySetException(
                args.ErrorException ?? new InvalidDataException($"WPF meldete einen Codecfehler bei '{resource}'."));
            player.MediaOpened += openedHandler;
            player.MediaFailed += failedHandler;
            try
            {
                player.Open(new Uri(EnsureCache(resource), UriKind.Absolute));
                var completed = await Task.WhenAny(opened.Task, Task.Delay(TimeSpan.FromSeconds(15)));
                if (completed != opened.Task) throw new TimeoutException($"WPF öffnete '{resource}' nicht innerhalb von 15 Sekunden.");
                _ = await opened.Task;
            }
            finally
            {
                player.MediaOpened -= openedHandler;
                player.MediaFailed -= failedHandler;
                player.Stop();
                player.Close();
            }
        }
        return resources.Length;
    }

    private void Remove(Voice voice)
    {
        voices.Remove(voice);
        voice.Player.MediaEnded -= OnMediaEnded;
        voice.Player.MediaFailed -= OnMediaFailed;
        voice.Player.MediaOpened -= OnMediaOpened;
        try { voice.Player.Stop(); voice.Player.Close(); } catch { }
    }

    private void StopAndRemove(Voice voice)
    {
        try { voice.Player.Stop(); } catch { }
        Remove(voice);
    }

    private void OnMediaEnded(object? sender, EventArgs e)
    {
        var voice = voices.FirstOrDefault(item => ReferenceEquals(item.Player, sender));
        if (voice is not null) Remove(voice);
    }

    private void OnMediaFailed(object? sender, ExceptionEventArgs e) => OnMediaEnded(sender, e);

    private void OnMediaOpened(object? sender, EventArgs e)
    {
        if (!enabled || paused || disposed) return;
        if (sender is MediaPlayer player && voices.Any(item => ReferenceEquals(item.Player, player))) player.Play();
    }

    private void VerifyAccess()
    {
        if (!dispatcher.CheckAccess()) throw new InvalidOperationException("EventSoundBank muss auf seinem WPF-Dispatcher verwendet werden.");
    }

    public void Dispose()
    {
        VerifyAccess();
        if (disposed) return;
        disposed = true;
        foreach (var voice in voices.ToArray()) StopAndRemove(voice);
        lastPlayed.Clear(); variants.Clear(); nextVariant.Clear();
    }
}
