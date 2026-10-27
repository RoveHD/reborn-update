namespace PokemonRebornLauncher.Services;

public enum FireworkBurstKind
{
    Chrysanthemum, Ring, GoldWillow, Crackle, Palm, Peony, DoubleRing,
    Heart, Star, Saturn, MultiBreak, Brocade, Crossette, Strobe
}
public readonly record struct FireworkTrailPoint(double X, double Y);
public readonly record struct FireworkBurstEvent(double X, double Y, uint Color, FireworkBurstKind Kind, bool IsSecondary);

public sealed class FireworkParticle
{
    public double X { get; internal set; }
    public double Y { get; internal set; }
    public double PreviousX { get; internal set; }
    public double PreviousY { get; internal set; }
    public double Vx { get; internal set; }
    public double Vy { get; internal set; }
    public double Age { get; internal set; }
    public double Lifetime { get; internal set; }
    public double Size { get; internal set; }
    public double Drag { get; internal set; }
    public double Gravity { get; internal set; }
    public uint Color { get; internal set; }
    public uint EndColor { get; internal set; }
    public bool Twinkles { get; internal set; }
    public bool IsTrail { get; internal set; }
    public bool LeavesTrail { get; internal set; }
    public IReadOnlyList<FireworkTrailPoint> TrailPoints => _trailPoints;
    public double ColorProgress => Math.Clamp((Age / Math.Max(.001, Lifetime) - .34) / .46, 0, 1);
    public double Opacity
    {
        get
        {
            double life = Age / Math.Max(.001, Lifetime);
            double fade = Math.Clamp((1 - life) / .48, 0, 1);
            // Independent phases avoid a screen-wide synchronized strobe.
            return fade * (Twinkles && life > .38 ? .25 + .75 * Math.Pow(.5 + .5 * Math.Sin(Age * 24 + FlickerPhase), 3) : 1);
        }
    }
    internal readonly List<FireworkTrailPoint> _trailPoints = new(14);
    internal double TrailClock, FlickerPhase;
}

public sealed class FireworkRocket
{
    public double X { get; internal set; }
    public double Y { get; internal set; }
    public double Vx { get; internal set; }
    public double Vy { get; internal set; }
    public double TargetY { get; internal set; }
    public double Age { get; internal set; }
    public double Lifetime { get; internal set; }
    public uint Color { get; internal set; }
    public IReadOnlyList<FireworkTrailPoint> TrailPoints => _trailPoints;
    internal readonly List<FireworkTrailPoint> _trailPoints = new(14);
    internal double Gravity, TrailClock;
    internal FireworkBurstKind Kind;
}

public sealed class FireworkSecondaryShell
{
    public double X { get; internal set; }
    public double Y { get; internal set; }
    public double Vx { get; internal set; }
    public double Vy { get; internal set; }
    public uint Color { get; internal set; }
    public IReadOnlyList<FireworkTrailPoint> TrailPoints => _trailPoints;
    internal readonly List<FireworkTrailPoint> _trailPoints = new(14);
    internal double Delay, Drag, Gravity, TrailClock;
    internal uint EndColor;
    internal int Stars, Children, ChildStars;
    internal FireworkBurstKind Kind;
    internal int Reserved => Stars + Children * ChildStars;
}

internal sealed class FireworkScheduledLaunch
{
    public double Delay, StartX, TargetX, TargetY;
}

/// <summary>Bounded, seeded firework choreography in viewport DIPs, including moving multi-stage shells.</summary>
public sealed class FireworkSimulation
{
    public const int MaxParticleBudget = 1200;
    private static readonly uint[] Palette =
        [0xFFFFC857, 0xFFFF4166, 0xFF48CEFF, 0xFFC28BFF, 0xFF63F7A2, 0xFFFFF2CD, 0xFFFF823F, 0xFFFF79DE, 0xFF6294FF];
    private const uint Gold = 0xFFFFC567, Amber = 0xFFFF8B35;
    private readonly Random _random;
    private readonly List<FireworkParticle> _particles = [];
    private readonly List<FireworkRocket> _rockets = [];
    private readonly List<FireworkSecondaryShell> _delayedPops = [];
    private readonly List<FireworkScheduledLaunch> _launches = [];
    private readonly Queue<FireworkBurstKind> _familyBag = [];
    private double _spawnClock, _finaleCooldown = 22;
    private double _width = 1, _height = 1, _intensity = 1;
    private int _particleLimit = 480;
    private bool _enabled = true, _reduceMotion, _active = true;
    public IReadOnlyList<FireworkParticle> Particles => _particles;
    public IReadOnlyList<FireworkRocket> Rockets => _rockets;
    public IReadOnlyList<FireworkSecondaryShell> SecondaryShells => _delayedPops;
    // Includes the ENTIRE reserved payload of every future daughter explosion and launch.
    public int ActiveParticleCount => _particles.Count + _rockets.Count + _launches.Count + ReservedSecondaryParticleCount;
    public int ReservedSecondaryParticleCount => _delayedPops.Sum(pop => pop.Reserved);
    public int PendingCracklePopCount => _delayedPops.Count;
    public int PendingLaunchCount => _launches.Count;
    public bool IsAnimating => _rockets.Count > 0 || _particles.Count > 0 || _delayedPops.Count > 0 || _launches.Count > 0;
    public bool IsRunning => _enabled && !_reduceMotion && _active && _particleLimit > 0 && _intensity > 0;
    public int BurstCount { get; private set; }
    public int SecondaryBurstCount { get; private set; }
    public FireworkBurstKind? LastBurstKind { get; private set; }
    public event Action<string>? SoundRequested;
    public event Action<FireworkBurstEvent>? BurstCreated;

    public FireworkSimulation() : this(Random.Shared.Next()) { }
    public FireworkSimulation(int seed) => _random = new Random(seed);

    public void Configure(bool enabled, int particleLimit, double intensity, bool reduceMotion, bool active)
    {
        _enabled = enabled;
        _particleLimit = Math.Clamp(particleLimit, 0, MaxParticleBudget);
        _intensity = double.IsFinite(intensity) ? Math.Clamp(intensity, 0, 2) : 1;
        _reduceMotion = reduceMotion;
        _active = active;
        if (!IsRunning) Clear();
        else TrimToBudget();
    }

    public void Resize(double width, double height)
    {
        width = double.IsFinite(width) ? Math.Clamp(width, 1, 32768) : 1;
        height = double.IsFinite(height) ? Math.Clamp(height, 1, 32768) : 1;
        double sx = width / _width, sy = height / _height;
        foreach (var p in _particles)
        {
            p.X *= sx; p.Y *= sy; p.PreviousX *= sx; p.PreviousY *= sy; p.Vx *= sx; p.Vy *= sy; p.Gravity *= sy;
            ScaleTrail(p._trailPoints, sx, sy);
        }
        foreach (var r in _rockets)
        {
            r.X *= sx; r.Y *= sy; r.Vx *= sx; r.Vy *= sy; r.TargetY *= sy; r.Gravity *= sy;
            ScaleTrail(r._trailPoints, sx, sy);
        }
        foreach (var pop in _delayedPops)
        {
            pop.X *= sx; pop.Y *= sy; pop.Vx *= sx; pop.Vy *= sy; pop.Gravity *= sy;
            ScaleTrail(pop._trailPoints, sx, sy);
        }
        _width = width; _height = height;
    }

    public void Clear()
    {
        _particles.Clear(); _rockets.Clear(); _delayedPops.Clear(); _launches.Clear(); _familyBag.Clear();
        _spawnClock = 0; _finaleCooldown = 22; BurstCount = 0; SecondaryBurstCount = 0; LastBurstKind = null;
    }

    public void Step(double deltaSeconds)
    {
        if (!IsRunning || !double.IsFinite(deltaSeconds) || deltaSeconds <= 0) return;
        // No scheduler catch-up after minimization, a blocked dispatcher or a long pause.
        double dt = Math.Min(deltaSeconds, 1d / 30d);
        _spawnClock -= dt;
        _finaleCooldown -= dt;
        if (_spawnClock <= 0)
        {
            SchedulePhrase();
            _spawnClock = Next(1.75, 3.1) / Math.Clamp(_intensity, .35, 1.5);
        }
        StepLaunches(dt);
        // Children already in flight advance before new parent bursts are created below.
        StepDelayedPops(dt);
        for (int i = _rockets.Count - 1; i >= 0; i--)
        {
            var r = _rockets[i];
            r.Age += dt;
            r.TrailClock += dt;
            if (r.TrailClock >= .035)
            {
                r.TrailClock = 0;
                RecordTrail(r._trailPoints, r.X, r.Y);
                AddParticle(new FireworkParticle
                {
                    X = r.X, Y = r.Y, PreviousX = r.X, PreviousY = r.Y,
                    Vx = Next(-9, 9), Vy = Next(8, 28), Lifetime = Next(.22, .46), Size = Next(.85, 1.6),
                    Drag = 1.9, Gravity = 20, Color = Gold, EndColor = Amber, IsTrail = true
                });
            }
            r.X = Math.Clamp(r.X + r.Vx * dt, 1, Math.Max(1, _width - 1));
            r.Y += r.Vy * dt + .5 * r.Gravity * dt * dt;
            r.Vy += r.Gravity * dt;
            if (r.Age >= r.Lifetime || r.Y <= r.TargetY)
            {
                _rockets.RemoveAt(i);
                Burst(r.X, r.Y, r.Color, r.Kind);
            }
        }
        for (int i = _particles.Count - 1; i >= 0; i--)
        {
            var p = _particles[i];
            p.PreviousX = p.X; p.PreviousY = p.Y;
            p.Age += dt;
            if (p.Age >= p.Lifetime) { _particles.RemoveAt(i); continue; }
            double drag = Math.Exp(-p.Drag * dt);
            p.Vx *= drag; p.Vy = p.Vy * drag + p.Gravity * dt;
            p.X += p.Vx * dt; p.Y += p.Vy * dt;
            if (p.LeavesTrail)
            {
                p.TrailClock += dt;
                if (p.TrailClock >= .04)
                {
                    p.TrailClock -= .04;
                    RecordTrail(p._trailPoints, p.X, p.Y);
                }
            }
        }
    }

    private void SchedulePhrase()
    {
        // Start with one rocket; later phrases add paired launches, fans and rare short finales.
        bool first = BurstCount == 0 && _rockets.Count == 0;
        bool finale = !first && _finaleCooldown <= 0 && _particleLimit >= 480 && _random.NextDouble() < .24;
        int count = first ? 1 : finale ? 5 : _random.NextDouble() < .35 ? 3 : _random.NextDouble() < .45 ? 2 : 1;
        if (finale) _finaleCooldown = Next(28, 46);
        double center = Next(.18, .82);
        for (int i = 0; i < count && _launches.Count < 6 && ActiveParticleCount < _particleLimit; i++)
        {
            double fan = count == 1 ? 0 : (i - (count - 1) / 2d) * .14;
            _launches.Add(new FireworkScheduledLaunch
            {
                Delay = i * (finale ? .24 : .19),
                StartX = Math.Clamp(center + fan * .20, .04, .96),
                TargetX = Math.Clamp(center + fan + Next(-.06, .06), .06, .94),
                TargetY = _random.NextDouble() < .22 ? Next(.48, .72) : Next(.16, .46)
            });
        }
    }

    private void StepLaunches(double dt)
    {
        for (int i = _launches.Count - 1; i >= 0; i--)
        {
            var launch = _launches[i];
            launch.Delay -= dt;
            if (launch.Delay > 0) continue;
            _launches.RemoveAt(i); // Releases the one slot reserved for this rocket.
            double startY = _height + 4, targetY = _height * launch.TargetY;
            double travel = startY - targetY, flight = Next(1.35, 2.45);
            double gravity = 2 * travel / (flight * flight);
            _rockets.Add(new FireworkRocket
            {
                X = _width * launch.StartX, Y = startY, Vx = _width * (launch.TargetX - launch.StartX) / flight,
                Vy = -gravity * flight, Gravity = gravity, TargetY = targetY, Lifetime = flight,
                Color = Palette[_random.Next(Palette.Length)], Kind = NextFamily()
            });
            SoundRequested?.Invoke("rocket-launch");
        }
    }

    private FireworkBurstKind NextFamily()
    {
        if (_familyBag.Count == 0)
        {
            var bag = Enum.GetValues<FireworkBurstKind>();
            for (int i = bag.Length - 1; i > 0; i--)
            {
                int j = _random.Next(i + 1);
                (bag[i], bag[j]) = (bag[j], bag[i]);
            }
            foreach (var kind in bag) _familyBag.Enqueue(kind);
        }
        return _familyBag.Dequeue();
    }

    private void Burst(double x, double y, uint baseColor, FireworkBurstKind kind)
    {
        int available = _particleLimit - ActiveParticleCount;
        if (available <= 0) return;
        uint secondary = Palette[_random.Next(Palette.Length)];
        double scale = Math.Clamp(Math.Min(_width, _height) / 760, .55, 1.3);
        int desired = kind switch
        {
            FireworkBurstKind.Palm => 22,
            FireworkBurstKind.MultiBreak or FireworkBurstKind.Crossette => 44,
            FireworkBurstKind.Crackle => 54,
            FireworkBurstKind.Ring => 72,
            FireworkBurstKind.Peony or FireworkBurstKind.Brocade or FireworkBurstKind.GoldWillow => 112,
            _ => 96
        };
        int daughterStars = kind == FireworkBurstKind.MultiBreak ? 20 : kind == FireworkBurstKind.Crossette ? 12 : 9;
        int grandChildren = kind == FireworkBurstKind.MultiBreak ? 3 : 0;
        int reservedPerDaughter = daughterStars + grandChildren * 7;
        int desiredDaughters = kind == FireworkBurstKind.MultiBreak ? 4 : kind is FireworkBurstKind.Crossette or FireworkBurstKind.Crackle ? 6 : 0;
        // At most 60% of free capacity goes to the future; low quality still has a readable primary shell.
        int daughterCount = Math.Min(desiredDaughters, (int)(available * .60) / reservedPerDaughter);
        int count = Math.Min(desired, available - daughterCount * reservedPerDaughter);
        if (count < 6) return;
        for (int child = 0; child < daughterCount; child++)
        {
            double angle = child * Math.Tau / daughterCount + Next(-.12, .12);
            double speed = Next(108, 166) * scale;
            _delayedPops.Add(new FireworkSecondaryShell
            {
                X = x, Y = y, Vx = Math.Cos(angle) * speed, Vy = Math.Sin(angle) * speed,
                Delay = kind == FireworkBurstKind.Crackle ? Next(.42, .8) : Next(.65, 1.05),
                Gravity = 24 * scale, Drag = .22, Color = child % 2 == 0 ? baseColor : secondary,
                EndColor = secondary, Kind = kind, Stars = daughterStars, Children = grandChildren, ChildStars = 7
            });
        }
        double rotation = Next(-Math.PI, Math.PI);
        for (int i = 0; i < count; i++)
        {
            double angle = Math.Tau * i / count;
            double life = Next(2.0, 2.7), drag = .4, gravity = 35, size = Next(1.25, 2.1), speed = Next(125, 185);
            uint color = baseColor, endColor = secondary;
            bool twinkles = false, trail = true;
            switch (kind)
            {
                case FireworkBurstKind.Ring:
                    speed = 146; drag = .10; gravity = 7; life = 2.4;
                    break;
                case FireworkBurstKind.DoubleRing:
                    speed = i % 2 == 0 ? 170 : 95; color = i % 2 == 0 ? baseColor : secondary;
                    endColor = color; drag = .10; gravity = 7; life = 2.5;
                    angle = Math.Tau * (i / 2) / Math.Ceiling(count / 2d);
                    break;
                case FireworkBurstKind.GoldWillow:
                    speed = Next(112, 160); color = Gold; endColor = Amber;
                    gravity = 56; drag = .19; life = Next(3.6, 4.6); size = Next(.8, 1.5); twinkles = i % 3 == 0;
                    break;
                case FireworkBurstKind.Brocade:
                    speed = Next(60, 187); color = i % 5 == 0 ? 0xFFFFF2CD : Gold; endColor = Amber;
                    gravity = 34; drag = .27; life = Next(3.0, 4.2); size = Next(.7, 1.25); twinkles = true;
                    break;
                case FireworkBurstKind.Palm:
                    angle = -Math.PI + Math.PI * i / Math.Max(1, count - 1);
                    speed = Next(153, 190); color = Gold; endColor = i % 3 == 0 ? secondary : Amber;
                    gravity = 69; drag = .24; life = Next(2.9, 3.5); size = Next(1.9, 2.6);
                    break;
                case FireworkBurstKind.Peony:
                    speed = 190 * Math.Sqrt(_random.NextDouble()); drag = .65; trail = false;
                    color = i % 7 == 0 ? secondary : baseColor; life = Next(1.6, 2.2);
                    break;
                case FireworkBurstKind.Strobe:
                    speed = Next(95, 180); twinkles = true; trail = i % 4 == 0;
                    life = Next(2.8, 3.6); color = i % 3 == 0 ? 0xFFFFF2CD : baseColor;
                    break;
                case FireworkBurstKind.Crackle:
                    speed = Next(90, 160); color = Gold; endColor = Amber; twinkles = true; life = Next(1.6, 2.3);
                    break;
                case FireworkBurstKind.MultiBreak:
                    speed = Next(55, 105); life = Next(1.4, 2.1); color = Gold;
                    break;
                case FireworkBurstKind.Crossette:
                    speed = Next(86, 150); life = Next(1.2, 1.7);
                    break;
            }
            double vx = Math.Cos(angle) * speed, vy = Math.Sin(angle) * speed;
            if (kind == FireworkBurstKind.Heart)
            {
                vx = 16 * Math.Pow(Math.Sin(angle), 3) * 10;
                vy = -(13 * Math.Cos(angle) - 5 * Math.Cos(2 * angle) - 2 * Math.Cos(3 * angle) - Math.Cos(4 * angle)) * 10;
                drag = .10; gravity = 8; life = 2.5; endColor = baseColor;
            }
            else if (kind == FireworkBurstKind.Star)
            {
                // Straight edges of a five-point silhouette rather than a wavy circle.
                double vertex = i * 10d / count;
                int a = (int)vertex; double t = vertex - a;
                double a1 = -Math.PI / 2 + a * Math.PI / 5, a2 = a1 + Math.PI / 5;
                double r1 = a % 2 == 0 ? 180 : 76, r2 = a % 2 == 0 ? 76 : 180;
                vx = Math.Cos(a1) * r1 * (1 - t) + Math.Cos(a2) * r2 * t;
                vy = Math.Sin(a1) * r1 * (1 - t) + Math.Sin(a2) * r2 * t;
                drag = .1; gravity = 8; life = 2.5; endColor = baseColor;
            }
            else if (kind == FireworkBurstKind.Saturn)
            {
                if (i < count * .58)
                {
                    double ringAngle = Math.Tau * i / Math.Ceiling(count * .58);
                    double ex = Math.Cos(ringAngle) * 193, ey = Math.Sin(ringAngle) * 48;
                    vx = ex * Math.Cos(-.38) - ey * Math.Sin(-.38);
                    vy = ex * Math.Sin(-.38) + ey * Math.Cos(-.38);
                    color = Gold; endColor = Amber;
                }
                else { vx *= .47; vy *= .47; }
                gravity = 8; drag = .1; life = 2.6;
            }
            else if (kind == FireworkBurstKind.Chrysanthemum)
            {
                vx = Math.Cos(angle + rotation) * speed; vy = Math.Sin(angle + rotation) * speed;
                life = Next(2.3, 3); drag = .48;
                if (i % 8 == 0) color = 0xFFFFF2CD;
            }
            AddParticle(new FireworkParticle
            {
                X = x, Y = y, PreviousX = x, PreviousY = y, Vx = vx * scale, Vy = vy * scale,
                Lifetime = life, Size = size, Drag = drag, Gravity = gravity * scale,
                Color = color, EndColor = endColor, LeavesTrail = trail, Twinkles = twinkles, FlickerPhase = Next(0, Math.Tau)
            });
        }
        EmitBurst(x, y, baseColor, kind, false);
    }

    private void StepDelayedPops(double dt)
    {
        // Only old entries advance; grandchildren added by a burst start moving next frame.
        for (int i = _delayedPops.Count - 1; i >= 0; i--)
        {
            var pop = _delayedPops[i];
            pop.Delay -= dt;
            double drag = Math.Exp(-pop.Drag * dt);
            pop.Vx *= drag; pop.Vy = pop.Vy * drag + pop.Gravity * dt;
            pop.X += pop.Vx * dt; pop.Y += pop.Vy * dt;
            pop.TrailClock += dt;
            if (pop.TrailClock >= .04) { pop.TrailClock -= .04; RecordTrail(pop._trailPoints, pop.X, pop.Y); }
            if (pop.Delay > 0) continue;
            _delayedPops.RemoveAt(i); // Release this payload's reservation before materializing it.
            double scale = Math.Clamp(Math.Min(_width, _height) / 760, .55, 1.3);
            for (int j = 0; j < pop.Stars; j++)
            {
                bool cross = pop.Kind == FireworkBurstKind.Crossette;
                double angle = cross ? Math.PI * .5 * (j % 4) : Math.Tau * j / pop.Stars;
                double speed = (cross ? Next(60, 95) : Next(38, 94)) * scale;
                AddParticle(new FireworkParticle
                {
                    X = pop.X, Y = pop.Y, PreviousX = pop.X, PreviousY = pop.Y,
                    Vx = Math.Cos(angle) * speed + pop.Vx * .15, Vy = Math.Sin(angle) * speed + pop.Vy * .15,
                    Lifetime = Next(.7, 1.4), Size = Next(.8, 1.5), Drag = .45, Gravity = 34 * scale,
                    Color = pop.Color, EndColor = pop.EndColor, LeavesTrail = true, Twinkles = true, FlickerPhase = Next(0, Math.Tau)
                });
            }
            for (int child = 0; child < pop.Children; child++)
            {
                double angle = Math.Tau * child / pop.Children;
                _delayedPops.Add(new FireworkSecondaryShell
                {
                    X = pop.X, Y = pop.Y, Vx = Math.Cos(angle) * 75 * scale, Vy = Math.Sin(angle) * 75 * scale,
                    Delay = Next(.35, .56), Drag = .2, Gravity = 24 * scale, Color = Gold, EndColor = Amber,
                    Stars = pop.ChildStars, Kind = FireworkBurstKind.Crackle
                });
            }
            EmitBurst(pop.X, pop.Y, pop.Color, pop.Kind, true);
        }
    }

    private void EmitBurst(double x, double y, uint color, FireworkBurstKind kind, bool secondary)
    {
        LastBurstKind = kind; BurstCount++;
        if (secondary) SecondaryBurstCount++;
        BurstCreated?.Invoke(new FireworkBurstEvent(x, y, color, kind, secondary));
        SoundRequested?.Invoke(secondary ? "sparkle" : "firework-boom");
    }

    private void AddParticle(FireworkParticle particle)
    {
        if (ActiveParticleCount < _particleLimit) _particles.Add(particle);
    }

    private void TrimToBudget()
    {
        while (ActiveParticleCount > _particleLimit)
        {
            if (_particles.Count > 0) _particles.RemoveAt(0);
            else if (_delayedPops.Count > 0) _delayedPops.RemoveAt(0);
            else if (_launches.Count > 0) _launches.RemoveAt(0);
            else if (_rockets.Count > 0) _rockets.RemoveAt(0);
        }
    }

    private static void RecordTrail(List<FireworkTrailPoint> trail, double x, double y)
    {
        trail.Add(new FireworkTrailPoint(x, y));
        if (trail.Count > 14) trail.RemoveAt(0);
    }
    private static void ScaleTrail(List<FireworkTrailPoint> trail, double sx, double sy)
    {
        for (int i = 0; i < trail.Count; i++) trail[i] = new FireworkTrailPoint(trail[i].X * sx, trail[i].Y * sy);
    }
    private double Next(double min, double max) => min + _random.NextDouble() * (max - min);
}
