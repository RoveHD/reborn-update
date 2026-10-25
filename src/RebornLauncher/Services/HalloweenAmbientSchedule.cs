namespace PokemonRebornLauncher.Services;

public enum HalloweenAmbientFrequency { Low, Normal, High }
public enum HalloweenAmbientKind { Laugh, Giggle, Whisper, Wind, Creak, Special }

public sealed record HalloweenAmbientCue(string Name, HalloweenAmbientKind Kind, double RelativeVolume);

/// <summary>
/// UI-independent, active-time scheduling. Step receives elapsed monotonic seconds, never wall-clock time.
/// Pausing discards the pending delay; re-enabling starts a complete new quiet interval.
/// </summary>
public sealed class HalloweenAmbientSchedule
{
    public const double DefaultEventVolume = .12;
    public const double VariantCooldownSeconds = 15 * 60;
    public const double MaximumTickGapSeconds = 30;

    private static readonly IReadOnlyList<HalloweenAmbientCue> catalog = Array.AsReadOnly(new[]
    {
        new HalloweenAmbientCue("ambient-laugh-01", HalloweenAmbientKind.Laugh, .65),
        new HalloweenAmbientCue("ambient-laugh-02", HalloweenAmbientKind.Laugh, .65),
        new HalloweenAmbientCue("ambient-laugh-03", HalloweenAmbientKind.Laugh, .65),
        new HalloweenAmbientCue("ambient-laugh-04", HalloweenAmbientKind.Laugh, .65),
        new HalloweenAmbientCue("ambient-giggle-01", HalloweenAmbientKind.Giggle, .5),
        new HalloweenAmbientCue("ambient-giggle-02", HalloweenAmbientKind.Giggle, .5),
        new HalloweenAmbientCue("ambient-whisper-01", HalloweenAmbientKind.Whisper, .55),
        new HalloweenAmbientCue("ambient-whisper-02", HalloweenAmbientKind.Whisper, .55),
        new HalloweenAmbientCue("ambient-wind-01", HalloweenAmbientKind.Wind, .6),
        new HalloweenAmbientCue("ambient-wind-02", HalloweenAmbientKind.Wind, .6),
        new HalloweenAmbientCue("ambient-creak-01", HalloweenAmbientKind.Creak, .45),
        new HalloweenAmbientCue("ambient-special-01", HalloweenAmbientKind.Special, .55)
    });
    private static readonly (HalloweenAmbientKind Kind, int Weight)[] weights =
    [
        (HalloweenAmbientKind.Laugh, 55), (HalloweenAmbientKind.Giggle, 15),
        (HalloweenAmbientKind.Whisper, 15), (HalloweenAmbientKind.Wind, 10),
        (HalloweenAmbientKind.Creak, 4), (HalloweenAmbientKind.Special, 1)
    ];

    private readonly Random random;
    private readonly Dictionary<string, double> lastPlayed = new(StringComparer.Ordinal);
    private string? previousCue;
    private double activeSeconds, remainingSeconds;

    public HalloweenAmbientSchedule(Random? random = null) => this.random = random ?? new Random();

    public static IReadOnlyList<HalloweenAmbientCue> Catalog => catalog;
    public bool IsActive { get; private set; }
    public double EventVolume { get; private set; } = DefaultEventVolume;
    public HalloweenAmbientFrequency Frequency { get; private set; } = HalloweenAmbientFrequency.Normal;
    public TimeSpan? TimeUntilNextSound => IsActive ? TimeSpan.FromSeconds(Math.Max(0, remainingSeconds)) : null;

    public static double NormalizeVolume(double value) => double.IsFinite(value) ? Math.Clamp(value, 0, 1) : DefaultEventVolume;

    public static HalloweenAmbientFrequency ParseFrequency(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        "low" => HalloweenAmbientFrequency.Low,
        "high" => HalloweenAmbientFrequency.High,
        _ => HalloweenAmbientFrequency.Normal
    };

    public static (TimeSpan Minimum, TimeSpan Maximum) DelayBounds(HalloweenAmbientFrequency frequency) => frequency switch
    {
        HalloweenAmbientFrequency.Low => (TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(10)),
        HalloweenAmbientFrequency.High => (TimeSpan.FromMinutes(2), TimeSpan.FromMinutes(5)),
        _ => (TimeSpan.FromMinutes(3), TimeSpan.FromMinutes(7))
    };

    public void Configure(string? theme, bool ambientEnabled, bool eventSoundsEnabled, double eventVolume,
        bool paused, string? frequency = "normal")
    {
        EventVolume = NormalizeVolume(eventVolume);
        var newFrequency = ParseFrequency(frequency);
        bool active = string.Equals(theme?.Trim(), "halloween", StringComparison.OrdinalIgnoreCase)
            && ambientEnabled && eventSoundsEnabled && EventVolume > 0 && !paused;
        bool restart = active != IsActive || newFrequency != Frequency;
        IsActive = active;
        Frequency = newFrequency;
        if (restart) remainingSeconds = active ? NextDelay() : 0;
    }

    /// <summary>
    /// Emits at most one cue. A long dispatcher/suspend gap starts a fresh pause, with no catch-up sounds.
    /// Attempts consume their slot even when no variant is eligible or an audio device later fails.
    /// </summary>
    public HalloweenAmbientCue? Step(double elapsedSeconds, bool voiceBusy = false)
    {
        if (!IsActive || !double.IsFinite(elapsedSeconds) || elapsedSeconds <= 0) return null;
        if (elapsedSeconds > MaximumTickGapSeconds)
        {
            remainingSeconds = NextDelay();
            return null;
        }
        activeSeconds += elapsedSeconds;
        remainingSeconds -= elapsedSeconds;
        if (remainingSeconds > 0) return null;

        remainingSeconds = NextDelay();
        if (voiceBusy) return null;
        var cue = ChooseEligibleCue();
        if (cue is null) return null;
        previousCue = cue.Name;
        lastPlayed[cue.Name] = activeSeconds;
        return cue;
    }

    private double NextDelay()
    {
        var bounds = DelayBounds(Frequency);
        return bounds.Minimum.TotalSeconds + random.NextDouble() * (bounds.Maximum - bounds.Minimum).TotalSeconds;
    }

    private HalloweenAmbientCue? ChooseEligibleCue()
    {
        var eligible = catalog.Where(cue => cue.Name != previousCue
            && (!lastPlayed.TryGetValue(cue.Name, out double played) || activeSeconds - played >= VariantCooldownSeconds)).ToArray();
        var groups = weights.Where(group => eligible.Any(cue => cue.Kind == group.Kind)).ToArray();
        if (groups.Length == 0) return null;

        // Weight categories, then choose a variant: four laughs do not quadruple the laugh weighting.
        double draw = random.NextDouble() * groups.Sum(group => group.Weight);
        var kind = groups[^1].Kind;
        foreach (var group in groups)
        {
            draw -= group.Weight;
            if (draw < 0) { kind = group.Kind; break; }
        }
        var variants = eligible.Where(cue => cue.Kind == kind).ToArray();
        return variants[random.Next(variants.Length)];
    }
}
