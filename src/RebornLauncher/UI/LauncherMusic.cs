using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Windows.Threading;
using System.Windows.Media;

namespace PokemonRebornLauncher.UI;

/// <summary>Optional launcher theme playback backed by the Windows WPF media codec.</summary>
public sealed class LauncherMusic : IDisposable
{
    public const double DefaultVolume = 0.22;
    private static readonly TimeSpan FadeDuration = TimeSpan.FromMilliseconds(450);
    private static readonly IReadOnlyDictionary<string, (string Title, string Isrc)> ThemeTracks =
        new Dictionary<string, (string, string)>(StringComparer.OrdinalIgnoreCase)
        {
            ["standard"] = ("Pokémon Reborn: Reborn Theme", ""),
            ["halloween"] = ("Come Play with Me", "USUAN1400042"),
            ["winter"] = ("We Wish you a Merry Christmas", "USUAN1100369"),
            ["spring"] = ("Whimsy Groove", "USUAN1100011"),
            ["summer"] = ("Sunshine (version 2)", "USUAN1100628"),
            ["harvest"] = ("Lone Harvest", "USUAN1100409"),
            ["newyear"] = ("Beachfront Celebration", "USUAN1200022"),
            ["arceus"] = ("Phoenix", "")
        };

    private readonly Dispatcher dispatcher;
    private MediaPlayer player = null!;
    private readonly DispatcherTimer fadeTimer;
    private string currentThemeId = "standard";
    private string? cachedAudioPath;
    private string? pendingThemeId;
    private double requestedVolume = DefaultVolume;
    private double fadeStartVolume;
    private double fadeTargetVolume;
    private DateTime fadeStarted;
    private bool stopAfterFade;
    private bool enabled;
    private bool suspended;
    private bool opened;
    private bool disposed;
    private bool failed;

    private readonly bool verificationMuted;
    public LauncherMusic() : this(false) { }
    private LauncherMusic(bool verificationMuted)
    {
        this.verificationMuted = verificationMuted;
        dispatcher = Dispatcher.CurrentDispatcher;
        fadeTimer = new DispatcherTimer(DispatcherPriority.Background, dispatcher)
        {
            Interval = TimeSpan.FromMilliseconds(30)
        };
        fadeTimer.Tick += OnFadeTick;
        TryLoadTheme("standard");
    }

    public event Action<string>? StatusChanged;

    public string Status { get; private set; } = "Musik wird geladen";
    public bool Enabled => enabled;
    public double Volume => requestedVolume;
    public bool Suspended => suspended;
    public string ThemeId => currentThemeId;

    /// <summary>German visible music credit for the selected built-in theme.</summary>
    public static string TrackCredit(string themeId)
    {
        var id = NormalizeThemeId(themeId);
        var track = ThemeTracks[id];
        if (id == "standard")
            return "„Reborn Theme“ von O Colosso (2017), aus der vorhandenen Pokémon-Reborn-Spielkopie. " +
                "Die Lizenzangaben der Spielmusik sind nicht Teil dieser Launcher-Credits.";

        if (id == "arceus")
            return "„Phoenix“ von Scott Buckley (scottbuckley.com.au), veröffentlicht unter " +
                "Creative Commons Namensnennung 4.0 International (CC BY 4.0). " +
                "Quelle: https://www.scottbuckley.com.au/library/phoenix/ | " +
                "Lizenz: https://creativecommons.org/licenses/by/4.0/ | " +
                "Bearbeitet: auf 92 Sekunden gekürzt, mit 5 Sekunden Ausblendung versehen und als MP3 mit 128 kbit/s kodiert.";

        if (id == "summer")
            return "„Sunshine (version 2)“ von Kevin MacLeod (incompetech.com), veröffentlicht unter " +
                "Creative Commons Namensnennung 4.0 (CC BY 4.0). " +
                "Quelle: https://incompetech.com/music/royalty-free/index.html?isrc=USUAN1100628 | " +
                "Lizenz: https://creativecommons.org/licenses/by/4.0/ | " +
                "Bearbeitet: auf 92 Sekunden gekürzt, mit 5 Sekunden Ausblendung versehen und als MP3 mit 128 kbit/s kodiert.";

        return $"„{track.Title}“ von Kevin MacLeod (incompetech.com), " +
            $"Creative Commons Namensnennung 4.0 (CC BY 4.0). Track: " +
            $"https://incompetech.com/music/royalty-free/index.html?isrc={track.Isrc} | " +
            "Lizenz: https://creativecommons.org/licenses/by/4.0/ " +
            "Bearbeitet: für den Launcher gekürzt, am Ende ausgeblendet und als MP3 komprimiert.";
    }

    /// <summary>Applies the user's music toggle, normalized volume, transient suspension, and seasonal track.</summary>
    public void Apply(bool enabled, double volume, bool suspended, string themeId = "standard")
    {
        VerifyAccess();
        if (disposed) return;

        this.enabled = enabled;
        requestedVolume = double.IsFinite(volume) ? Math.Clamp(volume, 0d, 1d) : DefaultVolume;
        this.suspended = suspended;

        var nextThemeId = NormalizeThemeId(themeId);
        if (pendingThemeId is not null && string.Equals(nextThemeId, currentThemeId, StringComparison.Ordinal))
            pendingThemeId = null;
        if (!string.Equals(nextThemeId, currentThemeId, StringComparison.Ordinal))
        {
            // If audible, fade the current track first. Reuse exactly one MediaPlayer and
            // open the replacement only after the outgoing player has stopped.
            if (opened && enabled && !suspended && player.Volume > 0.001)
            {
                pendingThemeId = nextThemeId;
                FadeTo(0, stopWhenSilent: true);
                SetStatus("Theme-Musik wechselt");
                return;
            }

            if (!TryLoadTheme(nextThemeId)) return;
        }

        ApplyPlaybackState();
    }

    /// <summary>
    /// Opens the selected embedded MP3 muted and verifies a successful MediaOpened event and positive duration.
    /// Returns the duration; throws on decode failure, invalid duration, cancellation, or timeout.
    /// Call on the WPF UI dispatcher.
    /// </summary>
    public static async Task<TimeSpan> RunMutedCodecSmokeCheckAsync(
        TimeSpan timeout,
        CancellationToken cancellationToken = default,
        string themeId = "standard")
    {
        if (timeout <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(timeout));

        var normalizedThemeId = NormalizeThemeId(themeId);
        var player = new MediaPlayer { Volume = 0 };
        var opened = new TaskCompletionSource<TimeSpan>(TaskCreationOptions.RunContinuationsAsynchronously);
        var failed = new TaskCompletionSource<Exception>(TaskCreationOptions.RunContinuationsAsynchronously);
        player.MediaOpened += (_, _) =>
        {
            if (!player.NaturalDuration.HasTimeSpan || player.NaturalDuration.TimeSpan <= TimeSpan.Zero)
                opened.TrySetException(new InvalidDataException("MP3 wurde geöffnet, hat aber keine positive Dauer."));
            else
                opened.TrySetResult(player.NaturalDuration.TimeSpan);
        };
        player.MediaFailed += (_, args) => failed.TrySetResult(
            args.ErrorException ?? new InvalidDataException("WPF MediaPlayer meldete einen unbekannten Codecfehler."));

        try
        {
            var path = EnsureAudioCache(normalizedThemeId);
            player.Open(new Uri(path, UriKind.Absolute));
            var timeoutTask = Task.Delay(timeout, cancellationToken);
            var completed = await Task.WhenAny(opened.Task, failed.Task, timeoutTask);
            if (completed == opened.Task) return await opened.Task;
            if (completed == failed.Task) throw new InvalidDataException($"WPF konnte die Musik für Theme '{normalizedThemeId}' nicht dekodieren.", await failed.Task);
            cancellationToken.ThrowIfCancellationRequested();
            throw new TimeoutException($"WPF MediaPlayer öffnete die Musik für Theme '{normalizedThemeId}' nicht innerhalb von {timeout}.");
        }
        finally
        {
            player.Stop();
            player.Close();
        }
    }

    internal static async Task VerifyMutedThemeTransitionsAsync()
    {
        using var music = new LauncherMusic(verificationMuted: true);
        async Task Until(Func<bool> ready)
        {
            var elapsed = System.Diagnostics.Stopwatch.StartNew();
            while (!ready())
            {
                if (music.failed) throw new InvalidDataException(music.Status);
                if (elapsed.Elapsed > TimeSpan.FromSeconds(10)) throw new TimeoutException("Musikwechsel wurde nicht abgeschlossen.");
                await Task.Delay(20);
            }
        }
        music.Apply(true, 0.3, false, "halloween");
        await Until(() => music.opened && !music.fadeTimer.IsEnabled && music.player.Volume > 0.29);
        music.Apply(true, 0.37, false, "winter");
        music.Apply(true, 0.37, false, "summer");
        await Until(() => music.ThemeId == "summer" && music.opened && !music.fadeTimer.IsEnabled && music.player.Volume > 0.36);
        music.Apply(false, 0.37, false, "summer");
        await Until(() => !music.fadeTimer.IsEnabled && music.player.Volume == 0);
        music.Apply(false, 0.37, false, "spring");
        await Until(() => music.ThemeId == "spring" && music.opened);
        if (music.Enabled || music.Volume != 0.37 || music.player.Volume != 0) throw new InvalidOperationException("Stummschaltung ging beim Musikwechsel verloren.");
        music.Apply(true, 0.37, true, "arceus");
        await Until(() => music.ThemeId == "arceus" && music.opened);
        if (!music.Suspended || music.player.Volume != 0) throw new InvalidOperationException("Musikpause ging beim Wechsel verloren.");
        music.Apply(true, 0.37, false, "arceus");
        await Until(() => !music.fadeTimer.IsEnabled && music.player.Volume > 0.36);
    }

    private static string NormalizeThemeId(string? themeId)
    {
        var id = string.IsNullOrWhiteSpace(themeId) ? "standard" : themeId.Trim();
        return ThemeTracks.ContainsKey(id) ? id.ToLowerInvariant() : "standard";
    }

    private void LoadTheme(string themeId)
    {
        fadeTimer.Stop();
        pendingThemeId = null;
        if (player is not null)
        {
            player.MediaOpened -= OnMediaOpened;
            player.MediaFailed -= OnMediaFailed;
            player.MediaEnded -= OnMediaEnded;
            player.Stop();
            player.Close();
        }

        currentThemeId = themeId;
        opened = false;
        failed = false;
        player = new MediaPlayer { Volume = 0, IsMuted = verificationMuted };
        player.MediaOpened += OnMediaOpened;
        player.MediaFailed += OnMediaFailed;
        player.MediaEnded += OnMediaEnded;
        cachedAudioPath = EnsureAudioCache(themeId);
        player.Open(new Uri(cachedAudioPath, UriKind.Absolute));
        SetStatus("Musik wird geladen");
    }

    private bool TryLoadTheme(string themeId)
    {
        try
        {
            LoadTheme(themeId);
            return true;
        }
        catch (Exception ex)
        {
            failed = true;
            SetStatus($"Musik für Theme „{themeId}“ konnte nicht geladen werden: {ex.Message}");
            return false;
        }
    }

    private static string EnsureAudioCache(string themeId)
    {
        var resourceName = themeId == "standard"
            ? "PokemonRebornLauncher.reborn-theme.mp3"
            : $"PokemonRebornLauncher.music-{themeId}.mp3";
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName)
            ?? throw new FileNotFoundException($"Eingebettete Musikressource '{resourceName}' fehlt.");
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        var payload = buffer.ToArray();
        var expectedHash = SHA256.HashData(payload);
        var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PokemonRebornLauncher", "audio");
        Directory.CreateDirectory(directory);
        var target = Path.Combine(directory, themeId == "standard" ? "reborn-theme.mp3" : $"music-{themeId}.mp3");

        if (File.Exists(target))
        {
            using var existing = File.OpenRead(target);
            if (CryptographicOperations.FixedTimeEquals(SHA256.HashData(existing), expectedHash)) return target;
        }

        var temporary = Path.Combine(directory, $".{themeId}-{Guid.NewGuid():N}.tmp");
        try
        {
            File.WriteAllBytes(temporary, payload);
            File.Move(temporary, target, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
        return target;
    }

    private void ApplyPlaybackState()
    {
        if (failed) return;
        if (!enabled)
        {
            if (suspended) StopImmediately();
            else FadeTo(0, stopWhenSilent: true);
            SetStatus("Musik aus");
            return;
        }

        if (suspended)
        {
            fadeTimer.Stop();
            player.Volume = 0;
            player.Pause();
            SetStatus("Musik pausiert");
            return;
        }

        if (!opened) return;
        if (player.NaturalDuration.HasTimeSpan && player.Position >= player.NaturalDuration.TimeSpan)
            player.Position = TimeSpan.Zero;
        player.Play();
        FadeTo(requestedVolume, stopWhenSilent: false);
        SetStatus("Musik läuft");
    }

    private void FadeTo(double target, bool stopWhenSilent)
    {
        fadeTimer.Stop();
        if (Math.Abs(player.Volume - target) < 0.001)
        {
            player.Volume = target;
            if (stopWhenSilent) StopImmediately();
            return;
        }
        fadeStartVolume = player.Volume;
        fadeTargetVolume = target;
        fadeStarted = DateTime.UtcNow;
        stopAfterFade = stopWhenSilent;
        fadeTimer.Start();
    }

    private void OnFadeTick(object? sender, EventArgs e)
    {
        var progress = Math.Clamp((DateTime.UtcNow - fadeStarted).TotalMilliseconds / FadeDuration.TotalMilliseconds, 0d, 1d);
        player.Volume = fadeStartVolume + (fadeTargetVolume - fadeStartVolume) * progress;
        if (progress < 1) return;
        fadeTimer.Stop();
        if (!stopAfterFade) return;
        StopImmediately();
        if (pendingThemeId is { } nextThemeId)
        {
            if (TryLoadTheme(nextThemeId)) ApplyPlaybackState();
        }
    }

    private void OnMediaOpened(object? sender, EventArgs e)
    {
        if (!ReferenceEquals(sender, player) || disposed) return;
        opened = true;
        ApplyPlaybackState();
    }

    private void OnMediaFailed(object? sender, ExceptionEventArgs e)
    {
        if (!ReferenceEquals(sender, player) || disposed) return;
        failed = true;
        fadeTimer.Stop();
        SetStatus($"Musik konnte nicht dekodiert werden: {e.ErrorException?.Message ?? "Unbekannter Fehler"}");
    }

    private void OnMediaEnded(object? sender, EventArgs e)
    {
        if (!ReferenceEquals(sender, player) || disposed || !enabled || suspended) return;
        player.Position = TimeSpan.Zero;
        player.Play();
    }

    private void StopImmediately()
    {
        fadeTimer.Stop();
        player.Stop();
        player.Volume = 0;
    }

    private void SetStatus(string status)
    {
        Status = status;
        var handlers = StatusChanged;
        if (handlers is null) return;
        foreach (Action<string> handler in handlers.GetInvocationList())
        {
            try { handler(status); }
            catch { /* A UI status observer must not break playback or launcher startup. */ }
        }
    }

    private void VerifyAccess()
    {
        if (!dispatcher.CheckAccess()) throw new InvalidOperationException("LauncherMusic muss auf seinem WPF-Dispatcher verwendet werden.");
    }

    public void Dispose()
    {
        VerifyAccess();
        if (disposed) return;
        disposed = true;
        fadeTimer.Stop();
        player.MediaOpened -= OnMediaOpened;
        player.MediaFailed -= OnMediaFailed;
        player.MediaEnded -= OnMediaEnded;
        player.Stop();
        player.Close();
        player.Volume = 0;
        StatusChanged = null;
    }
}
