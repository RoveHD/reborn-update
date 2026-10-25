using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Windows.Media;
using System.Windows.Threading;
using PokemonRebornLauncher.Services;

namespace PokemonRebornLauncher.UI;

/// <summary>One quiet ambient voice, owned by the window's WPF dispatcher.</summary>
public sealed class HalloweenAmbientAudio : IDisposable
{
    private sealed record Voice(MediaPlayer Player, HalloweenAmbientCue Cue, TimeSpan Started);

    private readonly Dispatcher dispatcher = Dispatcher.CurrentDispatcher;
    private readonly HalloweenAmbientSchedule schedule = new();
    private readonly DispatcherTimer timer;
    private readonly Stopwatch clock = Stopwatch.StartNew();
    private readonly Action<string>? diagnostic;
    private readonly Func<MediaPlayer> jumpscarePlayerFactory;
    private readonly HashSet<string> reportedFailures = new(StringComparer.Ordinal);
    private readonly Random jumpscareRandom = new();
    private TimeSpan previousTick;
    private Voice? voice;
    private MediaPlayer? jumpscareVoice;
    private bool jumpscareEnabled;
    private double jumpscareVolume = HalloweenAmbientSchedule.DefaultEventVolume;
    private bool disposed;

    public HalloweenAmbientAudio(Action<string>? diagnostic = null, Func<MediaPlayer>? jumpscarePlayerFactory = null)
    {
        this.diagnostic = diagnostic;
        this.jumpscarePlayerFactory = jumpscarePlayerFactory ?? (() => new MediaPlayer());
        timer = new DispatcherTimer(DispatcherPriority.Background, dispatcher) { Interval = TimeSpan.FromSeconds(1) };
        timer.Tick += OnTick;
    }

    /// <param name="theme">Resolved active theme, e.g. "halloween", rather than the stored "auto" setting.</param>
    /// <param name="paused">True when minimized, a game is running, or the window should otherwise be silent.</param>
    public void Configure(string? theme, bool ambientEnabled, bool eventSoundsEnabled, double eventVolume,
        bool paused, string? frequency = "normal")
    {
        VerifyAccess();
        if (disposed) return;
        bool wasActive = schedule.IsActive;
        schedule.Configure(theme, ambientEnabled, eventSoundsEnabled, eventVolume, paused, frequency);
        if (!schedule.IsActive)
        {
            timer.Stop();
            StopVoice();
            return;
        }
        if (!wasActive) previousTick = clock.Elapsed;
        if (voice is { } current)
        {
            try { current.Player.Volume = schedule.EventVolume * current.Cue.RelativeVolume; }
            catch (Exception error) { Report(current.Cue.Name, error.Message); StopVoice(); }
        }
        timer.Start();
    }

    /// <summary>Configures the visual Gengar cue's laugh separately from the ambient scheduler.</summary>
    public void ConfigureJumpscareLaugh(bool enabled, double eventVolume, bool paused)
    {
        VerifyAccess();
        if (disposed) return;
        jumpscareEnabled = enabled && !paused;
        jumpscareVolume = HalloweenAmbientSchedule.NormalizeVolume(eventVolume);
        if (!jumpscareEnabled || jumpscareVolume <= 0) StopJumpscareVoice();
        else if (jumpscareVoice is { } current) current.Volume = jumpscareVolume * .65;
    }

    /// <summary>Plays one random existing laugh variant without advancing or pausing ambient scheduling.</summary>
    public void PlayJumpscareLaugh()
    {
        VerifyAccess();
        if (disposed || !jumpscareEnabled || jumpscareVolume <= 0 || jumpscareVoice is not null) return;
        var laughs = HalloweenAmbientSchedule.Catalog
            .Where(item => item.Kind == HalloweenAmbientKind.Laugh)
            .ToArray();
        if (laughs.Length == 0) return;
        string cueName = laughs[jumpscareRandom.Next(laughs.Length)].Name;
        try
        {
            var player = jumpscarePlayerFactory();
            player.Volume = jumpscareVolume * .65;
            jumpscareVoice = player;
            player.MediaOpened += OnJumpscareOpened;
            player.MediaEnded += OnJumpscareEnded;
            player.MediaFailed += OnJumpscareFailed;
            player.Open(new Uri(EnsureCache(cueName), UriKind.Absolute));
        }
        catch (Exception error)
        {
            Report(cueName, error.Message);
            StopJumpscareVoice();
        }
    }

    private void OnJumpscareOpened(object? sender, EventArgs e)
    {
        if (jumpscareVoice is not { } current || !ReferenceEquals(sender, current)) return;
        if (disposed || !jumpscareEnabled || jumpscareVolume <= 0)
        {
            StopJumpscareVoice();
            return;
        }
        try { current.Play(); }
        catch { StopJumpscareVoice(); }
    }

    private void OnJumpscareEnded(object? sender, EventArgs e)
    {
        if (jumpscareVoice is { } current && ReferenceEquals(sender, current)) StopJumpscareVoice();
    }

    private void OnJumpscareFailed(object? sender, ExceptionEventArgs e)
    {
        if (jumpscareVoice is not { } current || !ReferenceEquals(sender, current)) return;
        StopJumpscareVoice();
    }

    private void StopJumpscareVoice()
    {
        var current = jumpscareVoice;
        jumpscareVoice = null;
        if (current is null) return;
        current.MediaOpened -= OnJumpscareOpened;
        current.MediaEnded -= OnJumpscareEnded;
        current.MediaFailed -= OnJumpscareFailed;
        try { current.Stop(); } catch { }
        try { current.Close(); } catch { }
    }

    private void OnTick(object? sender, EventArgs e)
    {
        if (disposed || !schedule.IsActive) return;
        var now = clock.Elapsed;
        var elapsed = now - previousTick;
        previousTick = now;
        // A codec/device that never sends MediaOpened/MediaEnded cannot keep a voice alive indefinitely.
        if (voice is { } current && now - current.Started > TimeSpan.FromSeconds(30))
        {
            Report(current.Cue.Name, "Audiozeitlimit überschritten.");
            StopVoice();
        }
        if (elapsed.TotalSeconds > HalloweenAmbientSchedule.MaximumTickGapSeconds) StopVoice();
        var cue = schedule.Step(elapsed.TotalSeconds, voice is not null);
        if (cue is not null) PlayCue(cue);
    }

    // Integration verification only: still respects every enable/theme/pause/volume gate.
    // A true result means Open was requested, never that a person heard the sound.
    internal bool PreviewForVerification(string cueName)
    {
        VerifyAccess();
        var cue = HalloweenAmbientSchedule.Catalog.FirstOrDefault(item => item.Name == cueName);
        return cue is not null && PlayCue(cue);
    }

    internal static async Task<Dictionary<string, double>> VerifyMutedResourcesAsync()
    {
        var durations = new Dictionary<string, double>();
        foreach (var cue in HalloweenAmbientSchedule.Catalog)
        {
            var player = new MediaPlayer { Volume = 0 };
            var opened = new TaskCompletionSource<double>(TaskCreationOptions.RunContinuationsAsynchronously);
            player.MediaOpened += (_, _) =>
            {
                if (player.NaturalDuration.HasTimeSpan && player.NaturalDuration.TimeSpan > TimeSpan.Zero)
                    opened.TrySetResult(player.NaturalDuration.TimeSpan.TotalSeconds);
                else opened.TrySetException(new InvalidDataException($"Ungültige Audiodauer: {cue.Name}"));
            };
            player.MediaFailed += (_, args) => opened.TrySetException(args.ErrorException);
            try
            {
                player.Open(new Uri(EnsureCache(cue.Name), UriKind.Absolute));
                durations[cue.Name] = await opened.Task.WaitAsync(TimeSpan.FromSeconds(10));
            }
            finally { player.Close(); }
        }
        return durations;
    }

    private bool PlayCue(HalloweenAmbientCue cue)
    {
        if (disposed || !schedule.IsActive || voice is not null) return false;
        try
        {
            string path = EnsureCache(cue.Name);
            var player = new MediaPlayer { Volume = schedule.EventVolume * cue.RelativeVolume };
            voice = new Voice(player, cue, clock.Elapsed);
            player.MediaOpened += OnMediaOpened;
            player.MediaEnded += OnMediaEnded;
            player.MediaFailed += OnMediaFailed;
            player.Open(new Uri(path, UriKind.Absolute));
            return true;
        }
        catch (Exception error)
        {
            StopVoice();
            Report(cue.Name, error.Message);
            return false;
        }
    }

    private void OnMediaOpened(object? sender, EventArgs e)
    {
        // A pending MediaOpened callback may have been queued before disable/minimize/Dispose.
        if (disposed || !schedule.IsActive || voice is not { } current || !ReferenceEquals(sender, current.Player)) return;
        try { current.Player.Play(); }
        catch (Exception error) { Report(current.Cue.Name, error.Message); StopVoice(); }
    }

    private void OnMediaEnded(object? sender, EventArgs e)
    {
        if (voice is { } current && ReferenceEquals(sender, current.Player)) StopVoice();
    }

    private void OnMediaFailed(object? sender, ExceptionEventArgs e)
    {
        if (voice is not { } current || !ReferenceEquals(sender, current.Player)) return;
        Report(current.Cue.Name, e.ErrorException.Message);
        StopVoice();
    }

    private void StopVoice()
    {
        var current = voice;
        voice = null;
        if (current is null) return;
        current.Player.MediaOpened -= OnMediaOpened;
        current.Player.MediaEnded -= OnMediaEnded;
        current.Player.MediaFailed -= OnMediaFailed;
        try { current.Player.Stop(); } catch { }
        try { current.Player.Close(); } catch { }
    }

    private static string EnsureCache(string cueName)
    {
        string resourceName = $"PokemonRebornLauncher.events.{cueName}.wav";
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName)
            ?? throw new FileNotFoundException($"Halloween-Sound-Ressource '{resourceName}' fehlt.");
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        byte[] payload = buffer.ToArray();
        string digest = Convert.ToHexString(SHA256.HashData(payload)).ToLowerInvariant();
        string directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PokemonRebornLauncher", "audio", "ambient");
        Directory.CreateDirectory(directory);
        string target = Path.Combine(directory, $"{cueName}-{digest[..16]}.wav");
        if (File.Exists(target) && new FileInfo(target).Length == payload.Length
            && SHA256.HashData(File.ReadAllBytes(target)).AsSpan().SequenceEqual(SHA256.HashData(payload))) return target;
        string temporary = Path.Combine(directory, $".{Guid.NewGuid():N}.tmp");
        try { File.WriteAllBytes(temporary, payload); File.Move(temporary, target, overwrite: true); }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
        return target;
    }

    private void Report(string cueName, string message)
    {
        if (!reportedFailures.Add(cueName)) return;
        string text = $"Halloween-Ambient ({cueName}): {message}";
        try { Trace.TraceWarning(text); } catch { }
        try { diagnostic?.Invoke(text); } catch { }
    }

    private void VerifyAccess()
    {
        if (!dispatcher.CheckAccess()) throw new InvalidOperationException("HalloweenAmbientAudio muss auf seinem WPF-Dispatcher verwendet werden.");
    }

    public void Dispose()
    {
        VerifyAccess();
        if (disposed) return;
        disposed = true;
        timer.Stop();
        timer.Tick -= OnTick;
        StopVoice();
        StopJumpscareVoice();
        clock.Stop();
    }
}
