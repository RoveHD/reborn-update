using PokemonRebornLauncher.Services;

var tests = new List<(string Name, Action Run)>();
void Test(string name, Action run) => tests.Add((name, run));
void Check(bool value, string message = "Assertion failed") { if (!value) throw new InvalidOperationException(message); }
double Distance(double x, double y) => Math.Sqrt(x * x + y * y);
void Advance(FireworkSimulation simulation, double seconds)
{
    for (int i = 0; i < (int)Math.Ceiling(seconds * 60); i++) simulation.Step(1d / 60);
}
FireworkSimulation Create(int seed, int budget = 1200)
{
    var simulation = new FireworkSimulation(seed);
    simulation.Resize(1100, 760);
    simulation.Configure(true, budget, 1.2, false, true);
    return simulation;
}
void CheckFinite(FireworkSimulation simulation)
{
    foreach (var p in simulation.Particles)
    {
        Check(new[] { p.X, p.Y, p.PreviousX, p.PreviousY, p.Vx, p.Vy, p.Age, p.Lifetime, p.Size, p.Opacity, p.ColorProgress }
            .All(double.IsFinite), "A particle has non-finite state.");
        Check(p.Opacity is >= 0 and <= 1 && p.ColorProgress is >= 0 and <= 1 && p.TrailPoints.Count <= 14);
        Check(p.TrailPoints.All(t => double.IsFinite(t.X) && double.IsFinite(t.Y)));
    }
    foreach (var r in simulation.Rockets)
    {
        Check(new[] { r.X, r.Y, r.Vx, r.Vy, r.TargetY, r.Age, r.Lifetime }.All(double.IsFinite));
        Check(r.TrailPoints.Count <= 14 && r.TrailPoints.All(t => double.IsFinite(t.X) && double.IsFinite(t.Y)));
    }
    foreach (var shell in simulation.SecondaryShells)
    {
        Check(new[] { shell.X, shell.Y, shell.Vx, shell.Vy }.All(double.IsFinite));
        Check(shell.TrailPoints.Count <= 14 && shell.TrailPoints.All(t => double.IsFinite(t.X) && double.IsFinite(t.Y)));
    }
}

Test("Seeded choreography, secondary explosions and particle state are deterministic", () =>
{
    var a = Create(331); var b = Create(331);
    var eventsA = new List<FireworkBurstEvent>(); var eventsB = new List<FireworkBurstEvent>();
    var cuesA = new List<string>(); var cuesB = new List<string>();
    a.BurstCreated += eventsA.Add; b.BurstCreated += eventsB.Add;
    a.SoundRequested += cuesA.Add; b.SoundRequested += cuesB.Add;
    for (int i = 0; i < 8000; i++)
    {
        a.Step(1d / 60); b.Step(1d / 60);
        Check(a.ActiveParticleCount == b.ActiveParticleCount && a.BurstCount == b.BurstCount);
        Check(a.LastBurstKind == b.LastBurstKind && a.SecondaryBurstCount == b.SecondaryBurstCount);
        if (i % 30 != 0) continue;
        Check(a.Rockets.Select(r => (r.X, r.Y, r.TargetY)).SequenceEqual(b.Rockets.Select(r => (r.X, r.Y, r.TargetY))));
        Check(a.Particles.Select(p => (p.X, p.Y, p.Opacity, p.ColorProgress)).SequenceEqual(b.Particles.Select(p => (p.X, p.Y, p.Opacity, p.ColorProgress))));
    }
    Check(eventsA.Count > 80 && eventsA.SequenceEqual(eventsB) && cuesA.SequenceEqual(cuesB));
    Check(cuesA.Contains("rocket-launch") && cuesA.Contains("firework-boom") && cuesA.Contains("sparkle"));
    Check(eventsA.Count(e => e.IsSecondary) == a.SecondaryBurstCount);
});

Test("All fourteen authored families occur naturally without immediate test explosions", () =>
{
    var simulation = Create(27);
    var families = new HashSet<FireworkBurstKind>();
    simulation.BurstCreated += e => { if (!e.IsSecondary) families.Add(e.Kind); };
    Advance(simulation, 90);
    Check(families.Count == 14 && families.SetEquals(Enum.GetValues<FireworkBurstKind>()), "A family did not appear in ordinary choreography.");
});

Test("Circular, double-ring, heart, five-point star and tilted Saturn silhouettes have distinct geometry", () =>
{
    var simulation = Create(723);
    var snapshots = new Dictionary<FireworkBurstKind, FireworkParticle[]>();
    simulation.BurstCreated += e =>
    {
        if (e.IsSecondary || snapshots.ContainsKey(e.Kind)) return;
        snapshots[e.Kind] = simulation.Particles.Where(p => p.Age == 0 && !p.IsTrail)
            .Select(p => new FireworkParticle { Vx = p.Vx, Vy = p.Vy, Lifetime = p.Lifetime, LeavesTrail = p.LeavesTrail }).ToArray();
    };
    Advance(simulation, 100);
    double Radius(FireworkParticle p) => Math.Sqrt(p.Vx * p.Vx + p.Vy * p.Vy);
    var ring = snapshots[FireworkBurstKind.Ring];
    Check(ring.Length >= 60 && ring.Max(Radius) - ring.Min(Radius) < .001, "Ring shell is not circular in viewport DIPs.");
    var doubled = snapshots[FireworkBurstKind.DoubleRing];
    Check(doubled.Select(p => Math.Round(Radius(p), 3)).Distinct().Count() == 2, "Double ring must have two discrete radii.");
    var heart = snapshots[FireworkBurstKind.Heart];
    Check(Math.Abs(heart.Average(p => p.Vx)) < 1 && heart.Max(p => p.Vy) > 155 && heart.Min(p => p.Vy) < -100);
    Check(heart.Where(p => Math.Abs(p.Vx) < 1).Min(p => p.Vy) > -60, "The heart's upper cleft is missing.");
    var star = snapshots[FireworkBurstKind.Star];
    Check(star.Count(p => Radius(p) > 165) >= 5 && star.Count(p => Radius(p) < 82) >= 5, "Star needs five outer tips and deep inner corners.");
    var saturn = snapshots[FireworkBurstKind.Saturn];
    var outer = saturn.Where(p => Radius(p) > 140).ToArray();
    Check(outer.Length > 12 && outer.All(p => Math.Abs(p.Vx * Math.Sin(.38) + p.Vy * Math.Cos(.38)) < 50), "Saturn's ring should be a thin tilted ellipse.");
    var peony = snapshots[FireworkBurstKind.Peony];
    Check(peony.All(p => !p.LeavesTrail) && peony.Min(Radius) < 50 && peony.Max(Radius) > 160);
});

Test("Gold willow and brocade rain longer than peonies and retain bounded trails", () =>
{
    var simulation = Create(45);
    var lifetimes = new Dictionary<FireworkBurstKind, double>();
    FireworkParticle? willow = null;
    simulation.BurstCreated += e =>
    {
        if (e.IsSecondary) return;
        var fresh = simulation.Particles.Where(p => p.Age == 0 && !p.IsTrail).ToArray();
        if (fresh.Length == 0) return;
        lifetimes[e.Kind] = fresh.Average(p => p.Lifetime);
        if (e.Kind == FireworkBurstKind.GoldWillow) willow = fresh[0];
    };
    Advance(simulation, 90);
    Check(lifetimes[FireworkBurstKind.GoldWillow] > 3.5 && lifetimes[FireworkBurstKind.Brocade] > 3);
    Check(lifetimes[FireworkBurstKind.GoldWillow] > lifetimes[FireworkBurstKind.Peony] + 1.5);
    Check(willow is not null && willow.LeavesTrail && willow.EndColor != willow.Color);
    Check(willow!.TrailPoints.Count <= 14 && willow.TrailPoints.Count > 2);
});

Test("MultiBreak daughters travel, explode after a real delay, then launch moving grandchildren", () =>
{
    var simulation = Create(192);
    FireworkBurstEvent parent = default;
    bool found = false;
    var secondary = new List<(double Time, FireworkBurstEvent Event)>();
    double time = 0, parentTime = 0;
    simulation.BurstCreated += e =>
    {
        if (!e.IsSecondary && e.Kind == FireworkBurstKind.MultiBreak && !found)
        { parent = e; parentTime = time; found = true; }
        if (found && e.IsSecondary) secondary.Add((time, e));
    };
    for (int i = 0; i < 6000 && !found; i++) { time += 1d / 60; simulation.Step(1d / 60); }
    Check(found && simulation.PendingCracklePopCount == 4 && simulation.ReservedSecondaryParticleCount == 164);
    var daughter = simulation.SecondaryShells[0];
    double startX = daughter.X, startY = daughter.Y;
    for (int i = 0; i < 18; i++) { time += 1d / 60; simulation.Step(1d / 60); }
    Check(simulation.SecondaryShells.Contains(daughter) && Distance(daughter.X - startX, daughter.Y - startY) > 25);
    Check(secondary.Count == 0 && daughter.TrailPoints.Count > 2, "Daughter shells exploded before their travelling fuse elapsed.");
    for (int i = 0; i < 100; i++) { time += 1d / 60; simulation.Step(1d / 60); }
    var children = secondary.Where(e => e.Event.Kind == FireworkBurstKind.MultiBreak).ToArray();
    var grandchildren = secondary.Where(e => e.Event.Kind == FireworkBurstKind.Crackle).ToArray();
    Check(children.Length == 4 && grandchildren.Length == 12, "Expected four daughter breaks plus twelve granddaughter breaks.");
    Check(children.All(e => e.Time - parentTime >= .6 && Distance(e.Event.X - parent.X, e.Event.Y - parent.Y) > 45));
    Check(grandchildren.All(g => children.Any(c => g.Time - c.Time >= .3 && g.Time - c.Time <= .6 && Distance(g.Event.X - c.Event.X, g.Event.Y - c.Event.Y) > 15)));
});

Test("Crossettes split into four branches and crackle is spatially separated and delayed", () =>
{
    var simulation = Create(116);
    bool crossette = false, crackle = false;
    double time = 0;
    var parents = new List<(double Time, FireworkBurstEvent Event)>();
    simulation.BurstCreated += e =>
    {
        if (!e.IsSecondary) { parents.Add((time, e)); return; }
        if (e.Kind is not (FireworkBurstKind.Crossette or FireworkBurstKind.Crackle)) return;
        var possibleParents = parents.Where(p => p.Event.Kind == e.Kind && time - p.Time is > .35 and < 1.2).ToArray();
        if (possibleParents.Length == 0) return;
        Check(possibleParents.Any(p => Distance(p.Event.X - e.X, p.Event.Y - e.Y) > 20));
        if (e.Kind == FireworkBurstKind.Crossette)
        {
            var fresh = simulation.Particles.Where(p => p.Age == 0 && !p.IsTrail).TakeLast(12).ToArray();
            Check(fresh.Length >= 12);
            double inheritedX = fresh.OrderBy(p => p.Vx).ElementAt(6).Vx, inheritedY = fresh.OrderBy(p => p.Vy).ElementAt(6).Vy;
            Check(fresh.All(p => Math.Min(Math.Abs(p.Vx - inheritedX), Math.Abs(p.Vy - inheritedY)) < .001), "Crossette split lost its four separate cardinal branches.");
            crossette = true;
        }
        else crackle = true;
    };
    for (int i = 0; i < 7000; i++) { time += 1d / 60; simulation.Step(1d / 60); }
    Check(crossette && crackle);
});

Test("Hard budgets include all planned child stars and scheduled launches from one slot through maximum", () =>
{
    foreach (int budget in new[] { 1, 6, 24, 60, 80, 160, 240, 480, 960, 1200 })
    {
        var simulation = Create(981, budget);
        for (int i = 0; i < 12000; i++)
        {
            simulation.Step(i % 997 == 0 ? 4 : 1d / 60);
            Check(simulation.ActiveParticleCount <= budget, $"Budget {budget} exceeded with {simulation.ActiveParticleCount} at frame {i}.");
            Check(simulation.ActiveParticleCount == simulation.Particles.Count + simulation.Rockets.Count + simulation.PendingLaunchCount + simulation.ReservedSecondaryParticleCount);
            Check(simulation.Rockets.All(r => r.X >= 0 && r.X <= 1100));
            if (i % 100 == 0) CheckFinite(simulation);
        }
        if (budget >= 24) Check(simulation.BurstCount > 0, $"Low budget {budget} produced no bursts.");
    }
    var clamped = Create(4, int.MaxValue);
    for (int i = 0; i < 6000; i++) { clamped.Step(1d / 60); Check(clamped.ActiveParticleCount <= FireworkSimulation.MaxParticleBudget); }
});

Test("Budget reduction while daughters and fan launches are scheduled cannot oversubscribe later", () =>
{
    var simulation = Create(19);
    for (int i = 0; i < 6000 && (simulation.PendingCracklePopCount == 0 || simulation.ActiveParticleCount < 180); i++) simulation.Step(1d / 60);
    Check(simulation.PendingCracklePopCount > 0);
    simulation.Configure(true, 24, 2, false, true);
    for (int i = 0; i < 4000; i++) { simulation.Step(1d / 60); Check(simulation.ActiveParticleCount <= 24); }
});

Test("Rocket flight is 1.35 to 2.45 seconds, visible across the viewport and near apex at burst", () =>
{
    var simulation = Create(472);
    var rockets = new HashSet<FireworkRocket>();
    var targets = new List<double>();
    for (int i = 0; i < 6000; i++)
    {
        simulation.Step(1d / 60);
        foreach (var rocket in simulation.Rockets)
        {
            if (rockets.Add(rocket)) targets.Add(rocket.TargetY);
            Check(rocket.Lifetime is >= 1.35 and <= 2.45 && rocket.Age <= rocket.Lifetime, "Rocket exceeded its authored flight time.");
            Check(rocket.Y >= rocket.TargetY - 1 && rocket.Vy <= 0, "Rocket flew below its target or started falling before bursting.");
        }
    }
    Check(rockets.Count > 40 && targets.Any(y => y > 380) && targets.Any(y => y < 200), "Flight height or launch count coverage is insufficient.");
    Check(rockets.Where(r => !simulation.Rockets.Contains(r)).All(r => r.Age >= r.Lifetime || Math.Abs(r.Y - r.TargetY) < .01), "A completed rocket burst before reaching its target/apex.");
});

Test("Choreography includes pairs, fans, short finale salvos and breathing intervals", () =>
{
    var simulation = Create(377);
    int maxPending = 0;
    var times = new List<double>();
    double time = 0;
    simulation.SoundRequested += cue => { if (cue == "rocket-launch") times.Add(time); };
    for (int i = 0; i < 18000; i++)
    {
        time += 1d / 60; simulation.Step(1d / 60);
        maxPending = Math.Max(maxPending, simulation.PendingLaunchCount);
    }
    Check(maxPending >= 4, "No rare five-shot finale was observed.");
    var gaps = times.Zip(times.Skip(1), (a, b) => b - a).ToArray();
    Check(gaps.Any(g => g is > .15 and < .3) && gaps.Any(g => g > 1.1));
    Check(times.Count < 450, "Choreography should leave pauses instead of continuous saturated salvos.");
});

Test("Color transitions stay bounded and strobes have independent glints with normal fade", () =>
{
    var simulation = Create(83);
    bool colors = false, glitter = false, fades = false;
    for (int i = 0; i < 6000; i++)
    {
        simulation.Step(1d / 60);
        colors |= simulation.Particles.Any(p => p.Color != p.EndColor && p.ColorProgress is > .1 and < .9);
        glitter |= simulation.Particles.Count(p => p.Twinkles && p.Opacity is > .25 and < .9) > 6;
        fades |= simulation.Particles.Any(p => !p.Twinkles && p.Opacity is > 0 and < .2);
        if (i % 100 == 0) CheckFinite(simulation);
    }
    Check(colors && glitter && fades);
});

Test("Disabled, reduced, inactive, zero budget and zero intensity clear every scheduler and stay silent", () =>
{
    foreach (var settings in new[] { (false, 480, 1d, false, true), (true, 480, 1d, true, true), (true, 480, 1d, false, false), (true, 0, 1d, false, true), (true, 480, 0d, false, true) })
    {
        var simulation = Create(22);
        Advance(simulation, 20);
        simulation.Configure(settings.Item1, settings.Item2, settings.Item3, settings.Item4, settings.Item5);
        int cues = 0; simulation.SoundRequested += _ => cues++;
        Check(simulation.ActiveParticleCount == 0 && !simulation.IsAnimating && !simulation.IsRunning);
        Check(simulation.PendingLaunchCount == 0 && simulation.PendingCracklePopCount == 0 && simulation.SecondaryShells.Count == 0);
        Advance(simulation, 20);
        Check(simulation.BurstCount == 0 && simulation.SecondaryBurstCount == 0 && cues == 0);
        simulation.Configure(true, 480, 1, false, true);
        simulation.Step(3600);
        Check(simulation.Rockets.Count == 1 && simulation.PendingLaunchCount == 0 && simulation.BurstCount == 0, "Resume produced a catch-up salvo.");
    }
});

Test("Invalid deltas and arbitrarily long pauses never catch up choreography", () =>
{
    var simulation = Create(8);
    simulation.Step(3600);
    Check(simulation.Rockets.Count == 1 && simulation.BurstCount == 0);
    var before = simulation.Rockets[0];
    double age = before.Age;
    foreach (double delta in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity, 0d, -5d }) simulation.Step(delta);
    Check(before.Age == age && simulation.Rockets.Count == 1);
    simulation.Step(3600);
    Check(before.Age - age <= 1d / 30 + .00001 && simulation.Rockets.Count == 1);
});

Test("Resize scales active stars, rocket and child trajectories plus every retained trail", () =>
{
    var simulation = Create(102);
    for (int i = 0; i < 6000 && (simulation.SecondaryShells.Count == 0 || simulation.Particles.Count == 0 || simulation.Rockets.Count == 0); i++) simulation.Step(1d / 60);
    Check(simulation.SecondaryShells.Count > 0 && simulation.Particles.Count > 0 && simulation.Rockets.Count > 0);
    Advance(simulation, .1);
    var p = simulation.Particles.First(); var shell = simulation.SecondaryShells.First(); var rocket = simulation.Rockets.First();
    double px = p.X, py = p.PreviousY, svx = shell.Vx, sx = shell.X, rx = rocket.X, target = rocket.TargetY;
    var trail = shell.TrailPoints.ToArray();
    simulation.Resize(2200, 1520);
    Check(Math.Abs(p.X - px * 2) < 1e-8 && Math.Abs(p.PreviousY - py * 2) < 1e-8);
    Check(Math.Abs(shell.X - sx * 2) < 1e-8 && Math.Abs(shell.Vx - svx * 2) < 1e-8);
    Check(Math.Abs(rocket.X - rx * 2) < 1e-8 && Math.Abs(rocket.TargetY - target * 2) < 1e-8);
    Check(trail.Zip(shell.TrailPoints).All(pair => Math.Abs(pair.First.X * 2 - pair.Second.X) < 1e-8 && Math.Abs(pair.First.Y * 2 - pair.Second.Y) < 1e-8));
    foreach (var size in new[] { (double.NaN, double.PositiveInfinity), (double.MaxValue, double.MaxValue), (-1d, 0d), (1100d, 760d) })
    { simulation.Resize(size.Item1, size.Item2); Advance(simulation, .1); CheckFinite(simulation); }
});

Test("Explicit Clear removes reserved payloads and starts a single fresh show", () =>
{
    var simulation = Create(19);
    Advance(simulation, 35);
    Check(simulation.BurstCount > 0);
    simulation.Clear();
    Check(simulation.ActiveParticleCount == 0 && !simulation.IsAnimating && simulation.BurstCount == 0 && simulation.LastBurstKind is null);
    simulation.Step(1d / 60);
    Check(simulation.Rockets.Count == 1 && simulation.PendingLaunchCount == 0 && simulation.BurstCount == 0);
});

int failed = 0;
foreach (var (name, run) in tests)
{
    try { run(); Console.WriteLine($"PASS {name}"); }
    catch (Exception error) { failed++; Console.WriteLine($"FAIL {name}: {error.Message}"); }
}
Console.WriteLine($"{tests.Count - failed}/{tests.Count} firework tests passed.");
return failed == 0 ? 0 : 1;
