using System.IO;
using System.Reflection;
using System.Windows.Media;
using PokemonRebornLauncher.Services;
using PokemonRebornLauncher.UI;

internal static class Program
{
    private static readonly List<(string Name, Action Run)> tests = [];

    [STAThread]
    private static int Main(string[] args)
    {
        Test("Catalog has twelve quiet variants, including four different laughs", () =>
        {
            Check(HalloweenAmbientSchedule.Catalog.Count == 12);
            Check(HalloweenAmbientSchedule.Catalog.Select(c => c.Name).Distinct().Count() == 12);
            Check(HalloweenAmbientSchedule.Catalog.Count(c => c.Kind == HalloweenAmbientKind.Laugh) == 4);
            Check(HalloweenAmbientSchedule.Catalog.All(c => c.RelativeVolume is > 0 and <= .65));
            var schedule = new HalloweenAmbientSchedule(new Random(1));
            Check(!schedule.IsActive && schedule.TimeUntilNextSound is null);
            Check(schedule.Step(30) is null);
        });

        Test("Each frequency chooses a full random initial pause inside its stated bounds", () =>
        {
            foreach (var frequency in new[] { "low", "normal", "high" })
            {
                var bounds = HalloweenAmbientSchedule.DelayBounds(HalloweenAmbientSchedule.ParseFrequency(frequency));
                var delays = new HashSet<double>();
                for (int seed = 0; seed < 100; seed++)
                {
                    var schedule = Active(seed, frequency);
                    double delay = schedule.TimeUntilNextSound!.Value.TotalSeconds;
                    Check(delay >= bounds.Minimum.TotalSeconds && delay < bounds.Maximum.TotalSeconds);
                    delays.Add(delay);
                }
                Check(delays.Count > 90, "Startup delays should not be fixed.");
            }
        });

        Test("No startup sound, and normal cannot emit before three active minutes", () =>
        {
            var schedule = Active(new FixedRandom(0));
            for (int second = 0; second < 179; second++) Check(schedule.Step(1) is null);
            Check(schedule.Step(1) is not null);
            Check(schedule.TimeUntilNextSound == TimeSpan.FromMinutes(3));
        });

        Test("Theme, both switches, pause, and zero volume independently gate all sounds", () =>
        {
            var cases = new (string? Theme, bool Ambient, bool Events, double Volume, bool Paused)[]
            {
                ("standard", true, true, .12, false), (null, true, true, .12, false),
                ("auto", true, true, .12, false), ("halloween", false, true, .12, false),
                ("halloween", true, false, .12, false), ("halloween", true, true, .12, true),
                ("halloween", true, true, 0, false), ("halloween", true, true, -3, false)
            };
            foreach (var config in cases)
            {
                var schedule = new HalloweenAmbientSchedule(new FixedRandom(0));
                schedule.Configure(config.Theme, config.Ambient, config.Events, config.Volume, config.Paused);
                Check(!schedule.IsActive);
                for (int tick = 0; tick < 1000; tick++) Check(schedule.Step(30) is null);
            }
        });

        Test("Changing only volume or reapplying the same settings preserves the delay", () =>
        {
            var schedule = Active(new FixedRandom(0));
            schedule.Step(30);
            var remaining = schedule.TimeUntilNextSound;
            schedule.Configure(" HALLOWEEN ", true, true, .6, false, " NORMAL ");
            Check(schedule.TimeUntilNextSound == remaining && schedule.EventVolume == .6);
            schedule.Configure("halloween", true, true, .6, false, "high");
            Check(schedule.TimeUntilNextSound == TimeSpan.FromMinutes(2));
        });

        Test("Minimizing discards pending work; resuming needs a complete new quiet interval", () =>
        {
            var schedule = Active(new FixedRandom(0));
            for (int i = 0; i < 179; i++) Check(schedule.Step(1) is null);
            schedule.Configure("halloween", true, true, .12, true);
            for (int i = 0; i < 1000; i++) Check(schedule.Step(30) is null);
            schedule.Configure("halloween", true, true, .12, false);
            Check(schedule.TimeUntilNextSound == TimeSpan.FromMinutes(3));
            for (int i = 0; i < 179; i++) Check(schedule.Step(1) is null);
            Check(schedule.Step(1) is not null);
        });

        Test("A long blocked-dispatcher/suspend gap never catches up with an immediate cue", () =>
        {
            var schedule = Active(new FixedRandom(0));
            for (int i = 0; i < 179; i++) schedule.Step(1);
            Check(schedule.Step(3600) is null);
            Check(schedule.TimeUntilNextSound == TimeSpan.FromMinutes(3));
            Check(schedule.Step(1) is null && schedule.TimeUntilNextSound == TimeSpan.FromSeconds(179));
        });

        Test("A busy voice consumes its due slot and starts another full pause", () =>
        {
            var schedule = Active(new FixedRandom(0), "high");
            for (int i = 0; i < 4; i++) Check(schedule.Step(30, voiceBusy: true) is null);
            Check(schedule.TimeUntilNextSound == TimeSpan.FromMinutes(2));
            for (int i = 0; i < 119; i++) Check(schedule.Step(1) is null);
            Check(schedule.Step(1) is not null);
        });

        Test("Every variant has fifteen active minutes cooldown and never repeats immediately", () =>
        {
            var schedule = Active(4567, "high");
            var seen = new Dictionary<string, int>();
            string? previous = null;
            int emitted = 0;
            for (int seconds = 1; seconds <= 300000; seconds++)
            {
                var cue = schedule.Step(1);
                if (cue is null) continue;
                Check(cue.Name != previous, "Immediate variant repetition.");
                if (seen.TryGetValue(cue.Name, out int when)) Check(seconds - when >= 900, "Variant cooldown violated.");
                seen[cue.Name] = seconds;
                previous = cue.Name;
                emitted++;
            }
            Check(emitted > 1000 && seen.Count == 12);
        });

        Test("Category boundaries match 55/15/15/10/4/1, independent of variant count", () =>
        {
            var samples = new (double Draw, HalloweenAmbientKind Expected)[]
            {
                (0, HalloweenAmbientKind.Laugh), (.549, HalloweenAmbientKind.Laugh),
                (.55, HalloweenAmbientKind.Giggle), (.699, HalloweenAmbientKind.Giggle),
                (.70, HalloweenAmbientKind.Whisper), (.849, HalloweenAmbientKind.Whisper),
                (.85, HalloweenAmbientKind.Wind), (.949, HalloweenAmbientKind.Wind),
                (.95, HalloweenAmbientKind.Creak), (.989, HalloweenAmbientKind.Creak),
                (.99, HalloweenAmbientKind.Special), (.999, HalloweenAmbientKind.Special)
            };
            foreach (var sample in samples)
            {
                var schedule = Active(new SequenceRandom(0, 0, sample.Draw), "high");
                Check(NextCue(schedule).Kind == sample.Expected, $"Wrong category at draw {sample.Draw}.");
            }
        });

        Test("Cooling variants are excluded and remaining category weights are renormalized", () =>
        {
            var schedule = Active(new SequenceRandom(0, 0, .999, 0, .999, 0, .999), "high");
            Check(NextCue(schedule).Kind == HalloweenAmbientKind.Special);
            Check(NextCue(schedule).Kind == HalloweenAmbientKind.Creak);
            Check(NextCue(schedule).Kind == HalloweenAmbientKind.Wind);
        });

        Test("A long inactive period cannot expire a variant's active-time cooldown", () =>
        {
            var schedule = Active(new SequenceRandom(0, 0, .999, 0, .999), "high");
            Check(NextCue(schedule).Kind == HalloweenAmbientKind.Special);
            schedule.Configure("standard", true, true, .12, false, "high");
            for (int i = 0; i < 1000; i++) schedule.Step(30);
            schedule.Configure("halloween", true, true, .12, false, "high");
            Check(NextCue(schedule).Kind != HalloweenAmbientKind.Special);
        });

        Test("Seeded schedule remains deterministic across reconfigurations", () =>
        {
            var a = Active(123); var b = Active(123);
            for (int i = 0; i < 10000; i++)
            {
                if (i % 2000 == 0)
                {
                    bool paused = i % 4000 == 0;
                    a.Configure("halloween", true, true, .2, paused, "low");
                    b.Configure("halloween", true, true, .2, paused, "low");
                }
                Check(a.Step(1)?.Name == b.Step(1)?.Name);
                Check(a.TimeUntilNextSound == b.TimeUntilNextSound);
            }
        });

        Test("Invalid settings and clock deltas cannot create rapid sounds", () =>
        {
            var schedule = new HalloweenAmbientSchedule(new FixedRandom(0));
            schedule.Configure("halloween", true, true, double.NaN, false, "broken");
            Check(schedule.IsActive && schedule.Frequency == HalloweenAmbientFrequency.Normal);
            Check(schedule.EventVolume == HalloweenAmbientSchedule.DefaultEventVolume);
            var remaining = schedule.TimeUntilNextSound;
            foreach (double delta in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity, -3, 0 })
                Check(schedule.Step(delta) is null && schedule.TimeUntilNextSound == remaining);
            Check(HalloweenAmbientSchedule.NormalizeVolume(4) == 1);
            Check(HalloweenAmbientSchedule.NormalizeVolume(-4) == 0);
            Check(HalloweenAmbientSchedule.ParseFrequency(null) == HalloweenAmbientFrequency.Normal);
            Check(HalloweenAmbientSchedule.DelayBounds((HalloweenAmbientFrequency)999).Minimum == TimeSpan.FromMinutes(3));
        });

        Test("WPF preview obeys all gates, and disposed audio cannot be restarted", () =>
        {
            using var audio = new HalloweenAmbientAudio();
            Check(!audio.PreviewForVerification("ambient-laugh-01"));
            foreach (var gate in new[] { "theme", "ambient", "events", "volume", "paused" })
            {
                audio.Configure(gate == "theme" ? "winter" : "halloween", gate != "ambient", gate != "events",
                    gate == "volume" ? 0 : .12, gate == "paused");
                Check(!audio.PreviewForVerification("ambient-laugh-01"));
            }
            audio.Dispose();
            audio.Configure("halloween", true, true, .12, false);
            Check(!audio.PreviewForVerification("ambient-laugh-01"));
        });

        Test("WPF audio rejects use from another dispatcher", () =>
        {
            using var audio = new HalloweenAmbientAudio();
            var error = Task.Run(() =>
            {
                try { audio.Configure("halloween", true, true, .12, false); return false; }
                catch (InvalidOperationException) { return true; }
            }).GetAwaiter().GetResult();
            Check(error);
        });

        if (args.Contains("--audio")) AddAudioIntegrationTests();
        int failed = 0;
        foreach (var (name, run) in tests)
        {
            try { run(); Console.WriteLine($"PASS {name}"); }
            catch (Exception error) { failed++; Console.WriteLine($"FAIL {name}: {error}"); }
        }
        Console.WriteLine($"{tests.Count - failed}/{tests.Count} ambient tests passed. Audio audibility was not measured.");
        return failed == 0 ? 0 : 1;
    }

    private static void AddAudioIntegrationTests()
    {
        Test("All catalog resources exist and are valid nonempty PCM WAVs", () =>
        {
            foreach (var cue in HalloweenAmbientSchedule.Catalog)
            {
                using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream($"PokemonRebornLauncher.events.{cue.Name}.wav");
                Check(stream is not null, $"Missing {cue.Name}");
                using var reader = new BinaryReader(stream!);
                Check(new string(reader.ReadChars(4)) == "RIFF");
                Check(reader.ReadUInt32() + 8 == stream!.Length);
                Check(new string(reader.ReadChars(4)) == "WAVE");
                bool pcm = false, data = false;
                while (stream.Position + 8 <= stream.Length)
                {
                    string chunk = new(reader.ReadChars(4));
                    uint length = reader.ReadUInt32();
                    long next = stream.Position + length + (length & 1);
                    Check(next <= stream.Length, "Truncated WAV chunk.");
                    if (chunk == "fmt ")
                    {
                        Check(length >= 16 && reader.ReadUInt16() == 1, "Expected PCM format.");
                        Check(reader.ReadUInt16() is 1 or 2);
                        Check(reader.ReadUInt32() is >= 22050 and <= 48000);
                        reader.ReadUInt32(); reader.ReadUInt16();
                        Check(reader.ReadUInt16() == 16);
                        pcm = true;
                    }
                    if (chunk == "data") data = length > 1000;
                    stream.Position = next;
                }
                Check(pcm && data, "Missing PCM format or sound data.");
            }
        });
        Test("One voice, volume updates, immediate stop, and stale MediaOpened are safe", () =>
        {
            using var audio = new HalloweenAmbientAudio();
            audio.Configure("halloween", true, true, .2, false);
            Check(audio.PreviewForVerification("ambient-laugh-01"));
            object oldVoice = GetVoice(audio)!;
            var oldPlayer = (MediaPlayer)oldVoice.GetType().GetProperty("Player")!.GetValue(oldVoice)!;
            Check(Math.Abs(oldPlayer.Volume - .13) < .000001);
            Check(!audio.PreviewForVerification("ambient-laugh-02"), "Ambient voices overlapped.");
            audio.Configure("halloween", true, true, .4, false);
            Check(Math.Abs(oldPlayer.Volume - .26) < .000001);
            audio.Configure("halloween", true, true, .4, true);
            Check(GetVoice(audio) is null);
            InvokeOpened(audio, oldPlayer);
            Check(GetVoice(audio) is null, "Stale callback restarted a stopped voice.");
            audio.Configure("halloween", true, true, .4, false);
            Check(audio.PreviewForVerification("ambient-giggle-01"));
            object current = GetVoice(audio)!;
            InvokeOpened(audio, oldPlayer);
            Check(ReferenceEquals(current, GetVoice(audio)));
            audio.Configure("standard", true, true, .4, false);
            Check(GetVoice(audio) is null);
            Check(!audio.PreviewForVerification("ambient-giggle-01"));
        });
    }

    private static object? GetVoice(HalloweenAmbientAudio audio) => typeof(HalloweenAmbientAudio)
        .GetField("voice", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(audio);
    private static void InvokeOpened(HalloweenAmbientAudio audio, MediaPlayer player) => typeof(HalloweenAmbientAudio)
        .GetMethod("OnMediaOpened", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(audio, [player, EventArgs.Empty]);
    private static void Test(string name, Action test) => tests.Add((name, test));
    private static void Check(bool condition, string message = "Assertion failed") { if (!condition) throw new Exception(message); }
    private static HalloweenAmbientSchedule Active(int seed, string frequency = "normal") => Active(new Random(seed), frequency);
    private static HalloweenAmbientSchedule Active(Random random, string frequency = "normal")
    {
        var schedule = new HalloweenAmbientSchedule(random);
        schedule.Configure("halloween", true, true, .12, false, frequency);
        return schedule;
    }
    private static HalloweenAmbientCue NextCue(HalloweenAmbientSchedule schedule)
    {
        for (int i = 0; i < 100; i++) if (schedule.Step(30) is { } cue) return cue;
        throw new Exception("No cue in fifty active minutes.");
    }
    private sealed class FixedRandom(double value) : Random
    {
        public override double NextDouble() => value;
        public override int Next(int maxValue) => 0;
    }
    private sealed class SequenceRandom(params double[] values) : Random
    {
        private int offset;
        public override double NextDouble() => offset < values.Length ? values[offset++] : 0;
        public override int Next(int maxValue) => 0;
    }
}
