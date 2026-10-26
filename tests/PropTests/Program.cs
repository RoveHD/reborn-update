using PokemonRebornLauncher.Services;

var tests = new List<(string, Action)>();
void Test(string name, Action test) => tests.Add((name, test));
void Check(bool ok, string message = "Assertion failed") { if (!ok) throw new Exception(message); }
void Advance(EventPropSimulation sim, double seconds, bool reduced = false)
{
    for (int i = 0; i < (int)Math.Ceiling(seconds / .025); i++) sim.Step(.025, reduced);
}
void Bounds(EventPropSimulation sim)
{
    Check(sim.Particles.Count <= 320);
    foreach (var p in sim.Particles)
    {
        Check(double.IsFinite(p.X + p.Y + p.Vx + p.Vy + p.Rotation + p.Life));
        Check(p.X >= 0 && p.X <= 1000 && p.Y >= 0 && p.Y <= 400, "Particle escaped world");
        Check(p.Life > 0 && p.Life <= p.MaxLife && p.Size > 0);
    }
    foreach (var p in sim.Props)
        Check(double.IsFinite(p.X + p.Y + p.Rotation + p.Age) && p.X is >= 0 and <= 1000 && p.Y is >= 0 and <= 400);
}

Test("Themes include every prop; Christmas is winter-only", () =>
{
    var sim = new EventPropSimulation();
    var seen = new HashSet<PropKind>();
    foreach (string theme in new[] { "standard", "winter", "spring", "halloween", "newyear", "summer", "harvest", "arceus" })
    {
        sim.Reset(theme, true);
        foreach (var p in sim.Props) seen.Add(p.Kind);
        if (theme != "winter") Check(!sim.Props.Any(p => p.Kind is PropKind.Gift or PropKind.Ornament or PropKind.Bell or PropKind.SnowGlobe or PropKind.CandyCane));
        foreach (var p in sim.Props.ToArray()) sim.Activate(p.Id);
        Advance(sim, .6);
        foreach (var p in sim.Props) seen.Add(p.Kind);
    }
    Check(seen.Count == Enum.GetValues<PropKind>().Length);
    sim.Reset("winter");
    Check(sim.Props.Count == 6);
    sim.Reset("winter", true);
    Check(sim.Props.Count == 12);
    sim.Reset("unknown");
    Check(sim.Props.All(p => p.Kind is PropKind.Star or PropKind.LightCrystal));
});

Test("Authored themes have distinct compositions with safe margins and clear prop spacing", () =>
{
    var sim = new EventPropSimulation();
    var starts = new HashSet<(double X, double Y)>();
    foreach (var (theme, expectedCount) in new[] { ("standard", 4), ("winter", 6), ("spring", 8), ("halloween", 10), ("newyear", 7), ("summer", 6), ("harvest", 6), ("arceus", 6) })
    {
        sim.Reset(theme);
        Check(sim.Props.Count == expectedCount, theme + " count");
        Check(sim.Props.Select(p => p.X).Distinct().Count() == expectedCount, theme + " aligned columns");
        Check(sim.Props.Select(p => p.Y).Distinct().Count() >= 4, theme + " lacks vertical composition");
        Check(sim.Props.Any(p => p.X is > 200 and < 800), theme + " still uses edge-only grid");
        foreach (var p in sim.Props)
            Check(starts.Add((p.X, p.Y)), theme + " reuses another theme's placement");
        CheckLayout(theme);
    }
    sim.Reset("winter", true);
    Check(sim.Props.Count == 12);
    CheckLayout("Christmas");

    void CheckLayout(string theme)
    {
        foreach (var p in sim.Props)
        {
            double radius = 30 * p.Scale;
            Check(p.X - radius >= 40 && p.X + radius <= 960 && p.Y - radius >= 40 && p.Y + radius <= 360,
                $"{theme} {p.Kind} violates 40-unit margin including body radius");
            foreach (var other in sim.Props.Where(q => q.Id > p.Id))
            {
                double dx = p.X - other.X, dy = p.Y - other.Y;
                Check(Math.Sqrt(dx * dx + dy * dy) >= radius + 30 * other.Scale + 12,
                    $"{theme} {p.Kind}/{other.Kind} overlap");
            }
        }
    }
});

Test("Theme placements respect seasonal roles and are authored independently of random seed", () =>
{
    var a = new EventPropSimulation(2); var b = new EventPropSimulation(791);
    foreach (string theme in new[] { "standard", "winter", "spring", "halloween", "newyear", "summer", "harvest", "arceus" })
    {
        a.Reset(theme, true); b.Reset(theme, true);
        Check(a.Props.Select(p => (p.Kind, p.X, p.Y, p.Scale)).SequenceEqual(b.Props.Select(p => (p.Kind, p.X, p.Y, p.Scale))));
    }
    a.Reset("winter", true);
    Check(a.Props.Single(p => p.Kind == PropKind.Icicle).Y < 110);
    Check(a.Props.Single(p => p.Kind == PropKind.LightString).Y < 110);
    var tree = a.Props.Single(p => p.Kind == PropKind.PineTree);
    foreach (var p in a.Props.Where(p => p.Kind is PropKind.Bell or PropKind.Ornament))
        Check(Math.Sqrt(Math.Pow(p.X - tree.X, 2) + Math.Pow(p.Y - tree.Y, 2)) < 150);
    Check(tree.Y >= 290 && a.Props.Single(p => p.Kind == PropKind.Snowman).Y > tree.Y);
    a.Reset("newyear");
    Check(a.Props.Where(p => p.Kind == PropKind.Rocket).All(p => p.Y >= 315));
    Check(a.Props.Where(p => p.Kind == PropKind.Rocket).Select(p => p.X).Max() - a.Props.Where(p => p.Kind == PropKind.Rocket).Select(p => p.X).Min() > 500);
    Check(a.Props.Single(p => p.Kind == PropKind.Sparkler).Y < 120);
    a.Reset("spring");
    Check(a.Props.Single(p => p.Kind == PropKind.Butterfly).Y < 140);
    Check(a.Props.Where(p => p.Kind is PropKind.Egg or PropKind.Nest).All(p => p.Y >= 300));
    a.Reset("summer");
    Check(a.Props.Where(p => p.Kind == PropKind.WaterBalloon).All(p => p.Y >= 315));
    Check(a.Props.Where(p => p.Kind == PropKind.Bubble).Max(p => p.Y) - a.Props.Where(p => p.Kind == PropKind.Bubble).Min(p => p.Y) > 120);
    a.Reset("halloween");
    Check(a.Props.Single(p => p.Kind == PropKind.Spider).Y < 110);
    Check(a.Props.Single(p => p.Kind == PropKind.Bat).Y < 150);
    a.Reset("harvest");
    Check(a.Props.Where(p => p.Kind == PropKind.LeafPile).All(p => p.Y >= 300));
    Check(a.Props.Where(p => p.Kind == PropKind.Mushroom).Select(p => p.Y).Distinct().Count() == 2);
});

Test("Committed relocation moves new effects and children, preserves motion, and resets to authored homes", () =>
{
    var sim = new EventPropSimulation(12); sim.Reset("winter");
    var snowman = sim.Props.Single(p => p.Kind == PropKind.Snowman);
    double authoredX = snowman.X, authoredY = snowman.Y;
    sim.RelocateHome(snowman.Id, 300, 280);
    Check(snowman.X == 300 && snowman.Y == 280);
    sim.Activate(snowman.Id);
    var balls = sim.Particles.Where(p => p.Kind == "snowball").ToArray();
    Check(balls.Length == 3 && balls.All(p => p.X == 300 && Math.Abs(p.Y - 280) <= 29));
    double oldParticleX = balls[0].X, oldParticleY = balls[0].Y;
    sim.RelocateHome(snowman.Id, 450, 260);
    Check(balls[0].X == oldParticleX && balls[0].Y == oldParticleY, "Relocation dragged a free particle");
    sim.RelocateHome(snowman.Id, double.NaN, 40);
    sim.RelocateHome(snowman.Id, 50, double.PositiveInfinity);
    Check(snowman.X == 450 && snowman.Y == 260);
    sim.RelocateHome(-1, 1, 1);
    sim.Reset("winter");
    snowman = sim.Props.Single(p => p.Kind == PropKind.Snowman);
    Check(snowman.X == authoredX && snowman.Y == authoredY);

    sim.Reset("spring");
    var egg = sim.Props.First(p => p.Kind == PropKind.Egg);
    sim.RelocateHome(egg.Id, 402, 250); sim.Activate(egg.Id); Advance(sim, .35);
    var child = sim.Props.Single(p => p.State == PropState.Active && p.Kind is PropKind.Chick or PropKind.Rabbit);
    Check(Math.Abs(child.X - 402) < 45 && Math.Abs(child.Y - 250) < 45);
    double childX = child.X, childY = child.Y;
    sim.RelocateHome(child.Id, 700, 100);
    Check(child.X == childX && child.Y == childY, "Temporary child was repacked");

    sim.Reset("summer");
    var bubble = sim.Props.First(p => p.Kind == PropKind.Bubble);
    double homeX = bubble.X, homeY = bubble.Y;
    Advance(sim, .3);
    double offsetX = bubble.X - homeX, offsetY = bubble.Y - homeY;
    sim.RelocateHome(bubble.Id, 500, 160);
    Check(Math.Abs(bubble.X - 500 - offsetX) < .000001 && Math.Abs(bubble.Y - 160 - offsetY) < .000001);
    sim.RelocateHome(bubble.Id, -50, 450);
    Check(bubble.X is >= 0 and <= 1000 && bubble.Y is >= 0 and <= 400);

    sim.Reset("newyear");
    var rocket = sim.Props.First(p => p.Kind == PropKind.Rocket);
    sim.RelocateHome(rocket.Id, 440, 320); sim.Activate(rocket.Id); sim.Step(.025);
    Check(rocket.X > 438 && rocket.X < 442 && rocket.Y < 320);
    Check(sim.Particles.Any(p => p.Kind == "spark" && Math.Abs(p.X - 440) < 3 && p.Y > 320 && p.Y < 340));
});

Test("Seeded simulation and click results are deterministic", () =>
{
    var a = new EventPropSimulation(54); var b = new EventPropSimulation(54);
    a.Reset("newyear"); b.Reset("newyear");
    foreach (var p in a.Props) Check(a.Activate(p.Id));
    foreach (var p in b.Props) Check(b.Activate(p.Id));
    Advance(a, 2); Advance(b, 2);
    Check(a.Props.Select(p => (p.Kind, p.X, p.Y, p.State, p.IsRare, p.Variant)).SequenceEqual(b.Props.Select(p => (p.Kind, p.X, p.Y, p.State, p.IsRare, p.Variant))));
    Check(a.Particles.Select(p => (p.X, p.Y, p.Vx, p.Vy, p.Kind)).SequenceEqual(b.Particles.Select(p => (p.X, p.Y, p.Vx, p.Vy, p.Kind))));
    Check(!a.Activate(-1));
});

Test("Schneemann has three ballistic body balls with gravity and ground bounce", () =>
{
    var sim = new EventPropSimulation(81); sim.Reset("winter");
    var snowman = sim.Props.Single(p => p.Kind == PropKind.Snowman);
    Check(sim.Activate(snowman.Id));
    Check(!sim.Activate(snowman.Id));
    var balls = sim.Particles.Where(p => p.Kind == "snowball").ToArray();
    Check(balls.Length == 3 && balls.Select(p => p.Size).Distinct().Count() == 3);
    double vy = balls[0].Vy, vx = Math.Abs(balls[0].Vx), lastVy = vy;
    sim.Step(.025);
    Check(balls[0].Vy > vy && Math.Abs(balls[0].Vx) < vx, "Gravity and drag must change velocity");
    bool bounced = false;
    for (int i = 0; i < 140; i++)
    {
        sim.Step(.025);
        if (lastVy > 0 && balls[0].Vy < 0) bounced = true;
        lastVy = balls[0].Vy;
        Bounds(sim);
    }
    Check(bounced, "Body ball did not bounce on floor");
});

Test("Broken props respawn after 8 to 15 seconds; reusable decorations remain visible", () =>
{
    foreach (bool reduced in new[] { false, true })
    {
        var sim = new EventPropSimulation(14); sim.Reset("winter", true);
        foreach (var p in sim.Props) Check(sim.Activate(p.Id));
        Advance(sim, 7.9, reduced);
        Check(sim.Props.Where(p => p.Kind is PropKind.Snowman or PropKind.Snowball or PropKind.Icicle or PropKind.IceCrystal or PropKind.SnowGlobe or PropKind.CandyCane or PropKind.Gift or PropKind.Ornament).All(p => p.State != PropState.Idle));
        Check(sim.Props.Where(p => p.Kind is PropKind.SnowPile or PropKind.PineTree or PropKind.Bell or PropKind.LightString).All(p => p.State == PropState.Idle && p.Opacity == 1));
        Advance(sim, 7.2, reduced);
        Check(sim.Props.All(p => p.State == PropState.Idle && p.Opacity == 1));
        foreach (var p in sim.Props) Check(sim.Activate(p.Id));
    }
});

Test("Three rockets launch concurrently, explode aloft, and stay within particle budget", () =>
{
    var sim = new EventPropSimulation(102); sim.Configure(true, 80, 2); sim.Reset("newyear");
    int launch = 0, boom = 0;
    sim.SoundRequested += sound =>
    {
        if (sound == "rocket-launch") launch++;
        if (sound == "firework-boom")
        {
            boom++;
            Check(sim.Props.Any(p => p.Kind == PropKind.Rocket && p.Y < 110));
        }
    };
    var rockets = sim.Props.Where(p => p.Kind == PropKind.Rocket).ToArray();
    foreach (var p in rockets) Check(sim.Activate(p.Id));
    Check(launch == 3 && rockets.All(p => p.State == PropState.Active));
    for (int i = 0; i < 100; i++) { sim.Step(.025); Check(sim.Particles.Count <= 80); Bounds(sim); }
    Check(boom == 6 && rockets.All(p => p.State is PropState.Broken or PropState.Cooldown));
    Advance(sim, 4);
    Check(sim.Particles.Count == 0);
});

Test("Reset cancels flights, pending explosions, particles and stale clicks", () =>
{
    var sim = new EventPropSimulation(); sim.Reset("newyear");
    var rocket = sim.Props.First(p => p.Kind == PropKind.Rocket);
    int boom = 0; sim.SoundRequested += s => { if (s == "firework-boom") boom++; };
    sim.Activate(rocket.Id); Advance(sim, .4);
    Check(sim.Particles.Count > 0);
    sim.Reset("spring");
    Check(!sim.Activate(rocket.Id) && sim.Particles.Count == 0 && sim.FoundEggs == 0);
    Advance(sim, 16);
    Check(boom == 0 && sim.Props.All(p => p.State == PropState.Idle));
});

Test("Rare spring egg is counted once across respawns; Arceus has a rare discovery", () =>
{
    EventPropSimulation? sim = null; EventProp? egg = null;
    for (int seed = 0; seed < 500 && egg is null; seed++)
    {
        sim = new EventPropSimulation(seed); sim.Reset("spring");
        egg = sim.Props.FirstOrDefault(p => p.Kind == PropKind.Egg && p.IsRare);
    }
    Check(egg is not null && sim is not null);
    var scene = sim!; var rare = egg!;
    var sounds = new List<string>(); scene.SoundRequested += sounds.Add;
    Check(scene.Activate(rare.Id) && scene.FoundEggs == 1 && sounds.Contains("egg-crack"));
    Check(!scene.Activate(rare.Id) && scene.FoundEggs == 1);
    Advance(scene, 15.1);
    Check(scene.Activate(rare.Id) && scene.FoundEggs == 1);
    scene.Reset("arceus");
    Check(scene.FoundEggs == 0 && scene.Props.Any(p => p.IsRare && p.Kind == PropKind.Star));
});

Test("Runtime limits, particle disable and malformed clock input are bounded", () =>
{
    var sim = new EventPropSimulation(); sim.Reset("winter");
    var p = sim.Props[0];
    sim.Step(double.NaN); sim.Step(double.PositiveInfinity); sim.Step(-1);
    Check(p.Age == 0);
    sim.Step(1000); Check(p.Age == .05);
    sim.Activate(p.Id); Check(sim.Particles.Count > 0);
    sim.Configure(true, 3, double.NaN); Check(sim.Particles.Count == 3);
    sim.Configure(false, 160, 1); Check(sim.Particles.Count == 0);
    sim.Reset("newyear");
    foreach (var prop in sim.Props) sim.Activate(prop.Id);
    Advance(sim, 2); Check(sim.Particles.Count == 0);
    sim.Configure(true, int.MaxValue, double.PositiveInfinity);
    foreach (string theme in new[] { "newyear", "winter", "spring", "summer", "halloween", "harvest", "arceus" })
    {
        sim.Reset(theme, true);
        for (int i = 0; i < 1600; i++)
        {
            if (i % 20 == 0) foreach (var prop in sim.Props) sim.Activate(prop.Id);
            sim.Step(.05, i % 2 == 0); Bounds(sim);
        }
    }
});

Test("Icicle falls before shattering and snowball follows a parabola before impact", () =>
{
    var sim = new EventPropSimulation(62); sim.Reset("winter");
    var sounds = new List<string>(); sim.SoundRequested += sounds.Add;
    var ice = sim.Props.Single(p => p.Kind == PropKind.Icicle);
    double startY = ice.Y;
    sim.Activate(ice.Id); Advance(sim, .35);
    Check(ice.Y > startY + 20 && ice.State == PropState.Active && ice.Opacity == 1);
    Check(!sim.Particles.Any() && !sounds.Contains("ice-break"));
    Advance(sim, 1.1);
    Check(ice.State is PropState.Broken or PropState.Cooldown && ice.Y == 374);
    Check(sounds.Count(s => s == "ice-break") == 1 && sim.Particles.Any(p => p.Kind == "shard"));

    sim.Reset("winter"); sounds.Clear();
    var ball = sim.Props.Single(p => p.Kind == PropKind.Snowball);
    double x = ball.X, y = ball.Y;
    sim.Activate(ball.Id); Advance(sim, .2);
    Check(ball.State == PropState.Active && ball.Y < y && Math.Abs(ball.X - x) > 20);
    Check(sim.Particles.Count == 0 && !sounds.Contains("snowball-hit"));
    Advance(sim, 1.3);
    Check(ball.Y == 384 && ball.State is PropState.Broken or PropState.Cooldown);
    Check(sounds.Count(s => s == "snowball-hit") == 1 && sim.Particles.Any(p => p.Kind == "snow"));
});

Test("Bell wobbles without destruction; ornament swings then shatters; snowpile growth is capped", () =>
{
    var sim = new EventPropSimulation(9); sim.Reset("winter", true);
    var bell = sim.Props.Single(p => p.Kind == PropKind.Bell);
    int variant = bell.Variant;
    sim.Activate(bell.Id); Advance(sim, .2);
    Check(Math.Abs(bell.Rotation) > 1 && bell.Opacity == 1 && bell.State == PropState.Active);
    Advance(sim, 1.3);
    Check(bell.State == PropState.Idle && bell.Opacity == 1 && bell.Rotation == 0);
    sim.Activate(bell.Id);
    Check(bell.Variant == (variant + 1) % 4);
    var ornament = sim.Props.Single(p => p.Kind == PropKind.Ornament);
    sim.Activate(ornament.Id); Advance(sim, .3);
    Check(ornament.State == PropState.Active && ornament.Opacity == 1 && Math.Abs(ornament.Rotation) > 1);
    Check(sim.Particles.Count == 0);
    Advance(sim, .7);
    Check(ornament.State == PropState.Broken && sim.Particles.Any(p => p.Kind == "shard"));
    var pile = sim.Props.Single(p => p.Kind == PropKind.SnowPile);
    double firstScale = pile.Scale;
    for (int cycle = 0; cycle < 12; cycle++)
    {
        Check(sim.Activate(pile.Id));
        for (int i = 0; i < 52; i++) { sim.Step(.025); Check(pile.Scale <= 1); }
    }
    Check(pile.Scale > firstScale && pile.Scale == 1);
});

Test("Christmas lights brighten and dim without breaking, and reduced motion suppresses flashing", () =>
{
    var sim = new EventPropSimulation(23); sim.Reset("winter", true);
    var lights = sim.Props.Single(p => p.Kind == PropKind.LightString);
    sim.Activate(lights.Id);
    var samples = new List<double>();
    for (int i = 0; i < 20; i++) { sim.Step(.05); samples.Add(lights.Opacity); }
    Check(lights.State == PropState.Active && samples.Max() - samples.Min() > .5);
    Advance(sim, 1); Check(lights.State == PropState.Idle && lights.Opacity == 1);
    sim.Activate(lights.Id); Advance(sim, .5, true);
    Check(lights.State == PropState.Active && lights.Opacity == 1);
});

Test("Egg hatches one temporary child, removes it, and cannot farm the discovery count", () =>
{
    var sim = new EventPropSimulation(17); sim.Reset("spring");
    var egg = sim.Props.First(p => p.Kind == PropKind.Egg);
    Check(sim.Activate(egg.Id)); Advance(sim, .35);
    var child = sim.Props.Single(p => p.Kind is PropKind.Chick || p.Kind == PropKind.Rabbit && p.State == PropState.Active);
    int childId = child.Id;
    Check(sim.FoundEggs == 1 && child.State == PropState.Active && !sim.Activate(childId));
    Advance(sim, 1);
    Check(sim.Props.Count(p => p.Id == childId) == 1);
    Check(sim.Props.Count(p => p.Kind == PropKind.Chick || p.Kind == PropKind.Rabbit && p.State == PropState.Active) == 1);
    Advance(sim, 4);
    Check(sim.Props.All(p => p.Id != childId));
    Advance(sim, 10);
    Check(sim.Activate(egg.Id)); Advance(sim, .35);
    Check(sim.FoundEggs == 1 && sim.Props.Any(p => p.Id != childId && p.Kind is PropKind.Chick or PropKind.Rabbit && p.State == PropState.Active));
});

Test("Gravestone raises a temporary hand, cauldron bubbles, spider returns and scarecrow jumps", () =>
{
    var sim = new EventPropSimulation(2); sim.Reset("halloween");
    var grave = sim.Props.Single(p => p.Kind == PropKind.Gravestone);
    var cauldron = sim.Props.Single(p => p.Kind == PropKind.Cauldron);
    var spider = sim.Props.Single(p => p.Kind == PropKind.Spider);
    var scarecrow = sim.Props.Single(p => p.Kind == PropKind.Scarecrow);
    double spiderY = spider.Y, scarecrowY = scarecrow.Y;
    sim.Activate(grave.Id); sim.Activate(cauldron.Id); sim.Activate(spider.Id); sim.Activate(scarecrow.Id);
    Advance(sim, .5);
    Check(grave.Rotation < 0 && sim.Props.Count(p => p.Kind == PropKind.SkeletonHand) == 1);
    var hand = sim.Props.Single(p => p.Kind == PropKind.SkeletonHand);
    double handY = hand.Y;
    Check(cauldron.State == PropState.Active && cauldron.Opacity == 1 && sim.Particles.Any(p => p.Kind == "bubble"));
    Check(spider.Y > spiderY + 50 && scarecrow.Y < scarecrowY - 25);
    Advance(sim, .2); Check(hand.Y < handY);
    Advance(sim, 3);
    Check(!sim.Props.Any(p => p.Kind == PropKind.SkeletonHand));
    Check(new[] { grave, cauldron, spider, scarecrow }.All(p => p.State == PropState.Idle && p.Opacity == 1));
    Check(Math.Abs(spider.Y - spiderY) <= 12 && scarecrow.Y == scarecrowY);
});

Test("Gift alternates flowers and confetti, ghost fades idly and roof bat flies away then fades back after cooldown", () =>
{
    var sim = new EventPropSimulation(19); sim.Reset("winter", true);
    var gift = sim.Props.Single(p => p.Kind == PropKind.Gift);
    for (int cycle = 0; cycle < 2; cycle++)
    {
        sim.Activate(gift.Id); Advance(sim, .5);
        if (gift.Variant % 2 == 0) Check(sim.Props.Any(p => p.Kind == PropKind.Flower));
        else Check(sim.Particles.Any(p => p.Kind == "confetti"));
        Advance(sim, 15);
    }
    sim.Reset("halloween");
    var ghost = sim.Props.Single(p => p.Kind == PropKind.Ghost);
    var bat = sim.Props.Single(p => p.Kind == PropKind.Bat);
    var opacitySamples = new List<double>();
    for (int i = 0; i < 25; i++) { Advance(sim, .4); opacitySamples.Add(ghost.Opacity); }
    Check(opacitySamples.Max() - opacitySamples.Min() > .7);
    double batX = bat.X, batY = bat.Y;
    sim.Activate(bat.Id); Advance(sim, .5);
    Check(bat.BatPhase == BatFlightPhase.Flying && bat.Y < batY && !sim.Activate(bat.Id));
    Advance(sim, 3);
    Check(bat.BatPhase == BatFlightPhase.Away && bat.State == PropState.Cooldown && bat.Opacity == 0);
    Advance(sim, 16);
    Check(bat.State == PropState.Idle && bat.Opacity == 1 && Math.Abs(bat.X-batX) < .001 && Math.Abs(bat.Y-batY) < .001,
        "Perched bat must return exactly to its authored ledge, fully visible");
    Bounds(sim);
});

Test("Rocket second stage is delayed, flashes fade, and resetting cancels pending bursts", () =>
{
    var sim = new EventPropSimulation(3); sim.Reset("newyear");
    var rocket = sim.Props.First(p => p.Kind == PropKind.Rocket);
    int boom = 0; sim.SoundRequested += s => { if (s == "firework-boom") boom++; };
    sim.Activate(rocket.Id);
    for (int i = 0; i < 100 && boom == 0; i++) sim.Step(.025);
    Check(boom == 1 && sim.Flash > 0 && sim.Flash <= 1);
    Advance(sim, .15); Check(boom == 1);
    sim.Reset("standard"); Advance(sim, 1);
    Check(boom == 1 && sim.Flash == 0 && sim.Particles.Count == 0);
    sim.Reset("newyear"); rocket = sim.Props.First(p => p.Kind == PropKind.Rocket);
    sim.Activate(rocket.Id); Advance(sim, 2.2);
    Check(boom == 3);
    Advance(sim, 1); Check(sim.Flash == 0);
    sim.Reset("newyear"); rocket = sim.Props.First(p => p.Kind == PropKind.Rocket);
    sim.Activate(rocket.Id); Advance(sim, 2.2, true); Check(sim.Flash == 0);
});

Test("Every interaction requests only supported sound keys", () =>
{
    var allowed = new HashSet<string>("snow-crunch snowball-hit snow-poof ice-break rocket-launch firework-boom sparkle confetti egg-crack bird-chirp rustle chocolate-snap ghost wood-creak pumpkin-pop bat-flap cauldron-bubble bell gift-rip glass-break water-pop leaf-rustle magic thunder wind".Split(' '));
    var sim = new EventPropSimulation(41);
    sim.SoundRequested += sound => Check(allowed.Contains(sound), "Unknown sound " + sound);
    foreach (string theme in new[] { "winter", "spring", "halloween", "newyear", "summer", "harvest", "arceus", "standard" })
    {
        sim.Reset(theme, true);
        foreach (var prop in sim.Props.ToArray()) sim.Activate(prop.Id);
        Advance(sim, 4);
    }
});

int failures = 0;
foreach (var (name, run) in tests)
{
    try { run(); Console.WriteLine("PASS " + name); }
    catch (Exception ex) { failures++; Console.Error.WriteLine("FAIL " + name + ": " + ex); }
}
Console.WriteLine($"{tests.Count - failures}/{tests.Count} passed");
Environment.ExitCode = failures == 0 ? 0 : 1;
