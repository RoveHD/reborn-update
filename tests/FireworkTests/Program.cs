using PokemonRebornLauncher.Services;

var tests = new List<(string Name, Action Run)>();
void Test(string name, Action run) => tests.Add((name, run));
void Check(bool value, string message = "Assertion failed") { if (!value) throw new InvalidOperationException(message); }
void Advance(FireworkSimulation simulation, double seconds)
{
    double remaining = seconds;
    while (remaining > 0)
    {
        double dt = Math.Min(1d / 60, remaining);
        simulation.Step(dt);
        remaining -= dt;
    }
}

Test("Seeded launches and burst stream are deterministic", () =>
{
    var a = new FireworkSimulation(331); var b = new FireworkSimulation(331);
    var sequenceA = new List<(string Cue, int Count, FireworkBurstKind? Kind)>();
    var sequenceB = new List<(string Cue, int Count, FireworkBurstKind? Kind)>();
    a.SoundRequested += cue => sequenceA.Add((cue, a.BurstCount, a.LastBurstKind));
    b.SoundRequested += cue => sequenceB.Add((cue, b.BurstCount, b.LastBurstKind));
    for (int i = 0; i < 6000; i++)
    {
        a.Step(1d / 60); b.Step(1d / 60);
        Check(a.ActiveParticleCount == b.ActiveParticleCount);
        Check(a.BurstCount == b.BurstCount && a.LastBurstKind == b.LastBurstKind);
        if (i % 30 == 0)
        {
            Check(a.Rockets.Count == b.Rockets.Count);
            if (a.Rockets.Count > 0) Check(a.Rockets[0].X == b.Rockets[0].X && a.Rockets[0].TargetY == b.Rockets[0].TargetY);
        }
    }
    Check(a.BurstCount >= 20 && sequenceA.SequenceEqual(sequenceB));
    Check(sequenceA.Any(item => item.Cue == "rocket-launch") && sequenceA.Any(item => item.Cue == "firework-boom"));
});

Test("Five visibly different burst families occur across ordinary seeded shows", () =>
{
    var kinds = new HashSet<FireworkBurstKind>();
    for (int seed = 0; seed < 8; seed++)
    {
        var simulation = new FireworkSimulation(seed);
        int previousCount = 0;
        Advance(simulation, 75);
        Check(simulation.BurstCount > 0);
        // Last kind plus deterministic seed sweeps cover the product's five-form selector.
        kinds.Add(simulation.LastBurstKind!.Value);
        for (int i = 0; i < 75 * 60; i++)
        {
            simulation.Step(1d / 60);
            if (simulation.BurstCount != previousCount) { kinds.Add(simulation.LastBurstKind!.Value); previousCount = simulation.BurstCount; }
        }
    }
    Check(kinds.SetEquals(Enum.GetValues<FireworkBurstKind>()), "Not all authored burst styles appeared.");
});

Test("Particle budgets remain bounded at 60, 80, 160 and 320", () =>
{
    foreach (int budget in new[] { 60, 80, 160, 320 })
    {
        var simulation = new FireworkSimulation(981);
        simulation.Resize(1000, 700);
        simulation.Configure(true, budget, 1.5, false, true);
        for (int i = 0; i < 24000; i++)
        {
            simulation.Step(i % 997 == 0 ? 4 : 1d / 60);
            Check(simulation.ActiveParticleCount <= budget, $"Exceeded {budget} with {simulation.ActiveParticleCount} (particles={simulation.Particles.Count}, rockets={simulation.Rockets.Count}, pops={simulation.PendingCracklePopCount}, burst={simulation.LastBurstKind}) at frame {i}.");
            Check(simulation.Rockets.All(rocket => rocket.X >= 0 && rocket.X <= 1000), "Rocket/burst center left the horizontal viewport.");
        }
        Check(simulation.BurstCount > 0);
    }
});

Test("Gold Willow and Palm leave a bounded trail that scales on resize", () =>
{
    FireworkSimulation? matching = null;
    for (int seed = 0; seed < 16 && matching is null; seed++)
    {
        var simulation = new FireworkSimulation(seed);
        simulation.Resize(900, 600);
        int lastBurst = 0;
        for (int frame = 0; frame < 90 * 60; frame++)
        {
            simulation.Step(1d / 60);
            if (simulation.BurstCount != lastBurst)
            {
                lastBurst = simulation.BurstCount;
                if (simulation.LastBurstKind is FireworkBurstKind.GoldWillow or FireworkBurstKind.Palm)
                { matching = simulation; break; }
            }
        }
    }
    Check(matching is not null, "Seed sweep did not produce a Willow or Palm.");
    Advance(matching!, .24);
    var particle = matching!.Particles.FirstOrDefault(p => p.LeavesTrail && p.TrailPoints.Count >= 2);
    Check(particle is not null, "Long-trail effect did not retain its path.");
    double x = particle!.X, previousX = particle.PreviousX, pointX = particle.TrailPoints[0].X;
    matching.Resize(1800, 1200);
    Check(Math.Abs(particle.X - x * 2) < 1e-8 && Math.Abs(particle.PreviousX - previousX * 2) < 1e-8 && Math.Abs(particle.TrailPoints[0].X - pointX * 2) < 1e-8);
    Advance(matching, 1);
    Check(particle.TrailPoints.Count <= 7);
});

Test("Crackle secondary pops are time-delayed and bounded", () =>
{
    bool found = false;
    for (int seed = 0; seed < 32 && !found; seed++)
    {
        var simulation = new FireworkSimulation(seed);
        simulation.Resize(900, 600);
        int lastBurst = 0;
        for (int frame = 0; frame < 120 * 60; frame++)
        {
            simulation.Step(1d / 60);
            if (simulation.BurstCount != lastBurst)
            {
                lastBurst = simulation.BurstCount;
                if (simulation.LastBurstKind != FireworkBurstKind.Crackle) continue;
                Check(simulation.PendingCracklePopCount is > 0 and <= 2);
                int count = simulation.ActiveParticleCount;
                Advance(simulation, .58);
                Check(simulation.PendingCracklePopCount == 0);
                Check(simulation.ActiveParticleCount <= 160 && simulation.BurstCount >= lastBurst);
                found = count > 0;
                break;
            }
        }
    }
    Check(found, "Seed sweep did not produce a bounded delayed Crackle.");
});

Test("Disabled, reduced-motion, inactive and unloaded-equivalent states clear and stay quiet", () =>
{
    foreach (var settings in new[] { (false, 160, 1d, false, true), (true, 160, 1d, true, true), (true, 160, 1d, false, false), (true, 0, 1d, false, true) })
    {
        var simulation = new FireworkSimulation(22);
        Advance(simulation, 2);
        simulation.Configure(settings.Item1, settings.Item2, settings.Item3, settings.Item4, settings.Item5);
        Check(simulation.ActiveParticleCount == 0 && !simulation.IsAnimating && !simulation.IsRunning);
        int bursts = simulation.BurstCount;
        Advance(simulation, 20);
        Check(simulation.BurstCount == bursts && simulation.ActiveParticleCount == 0);
    }
});

Test("Large or invalid frame deltas never create catch-up volleys", () =>
{
    var simulation = new FireworkSimulation(8);
    simulation.Step(3600);
    Check(simulation.Rockets.Count == 1 && simulation.BurstCount == 0);
    for (int i = 0; i < 10; i++) simulation.Step(double.NaN);
    Check(simulation.Rockets.Count == 1 && simulation.BurstCount == 0);
});

Test("Viewport resize scales active DIPs without losing rockets", () =>
{
    var simulation = new FireworkSimulation(912);
    simulation.Resize(400, 300);
    simulation.Step(1d / 60);
    var before = simulation.Rockets.Single();
    double x = before.X, y = before.Y, vx = before.Vx, targetY = before.TargetY;
    simulation.Resize(800, 600);
    var after = simulation.Rockets.Single();
    Check(Math.Abs(after.X - x * 2) < 1e-9 && Math.Abs(after.Y - y * 2) < 1e-9 && Math.Abs(after.Vx - vx * 2) < 1e-9 && Math.Abs(after.TargetY - targetY * 2) < 1e-9);
});

Test("Intensities and viewport positions produce launches below mid-screen too", () =>
{
    var simulation = new FireworkSimulation(472);
    simulation.Resize(1100, 700);
    simulation.Configure(true, 160, 1, false, true);
    var targets = new List<double>();
    for (int i = 0; i < 8000; i++)
    {
        simulation.Step(1d / 60);
        if (simulation.Rockets.Count > 0 && !targets.Contains(simulation.Rockets[0].TargetY)) targets.Add(simulation.Rockets[0].TargetY);
    }
    Check(targets.Count > 5 && targets.Any(y => y > 350) && targets.All(y => y >= 126 && y <= 532));
});

int failed = 0;
foreach (var (name, run) in tests)
{
    try { run(); Console.WriteLine($"PASS {name}"); }
    catch (Exception error) { failed++; Console.WriteLine($"FAIL {name}: {error.Message}"); }
}
Console.WriteLine($"{tests.Count - failed}/{tests.Count} firework tests passed.");
return failed == 0 ? 0 : 1;
