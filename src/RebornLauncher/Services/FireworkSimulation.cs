namespace PokemonRebornLauncher.Services;

public enum FireworkBurstKind { Chrysanthemum, Ring, GoldWillow, Crackle, Palm }
public readonly record struct FireworkTrailPoint(double X, double Y);

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
    public bool IsTrail { get; internal set; }
    public bool LeavesTrail { get; internal set; }
    public IReadOnlyList<FireworkTrailPoint> TrailPoints => _trailPoints;
    public double Opacity => Math.Clamp(1 - Age / Math.Max(.001, Lifetime), 0, 1);
    internal readonly List<FireworkTrailPoint> _trailPoints = new(8);
    internal double TrailClock;
}

public sealed class FireworkRocket
{
    public double X { get; internal set; }
    public double Y { get; internal set; }
    public double Vx { get; internal set; }
    public double Vy { get; internal set; }
    public double TargetY { get; internal set; }
    public uint Color { get; internal set; }
    internal double TrailClock;
}

internal sealed class FireworkScheduledPop
{
    public double X, Y, Delay;
    public uint Color;
}

/// <summary>Bounded, deterministic-when-seeded firework effects; coordinates are WPF DIPs.</summary>
public sealed class FireworkSimulation
{
    private static readonly uint[] Palette = [0xFFFFC857, 0xFFFF5D73, 0xFF62D6FF, 0xFFB889FF, 0xFF8CE99A, 0xFFFFF0A5, 0xFFFF8A45];
    private readonly Random _random;
    private readonly List<FireworkParticle> _particles = [];
    private readonly List<FireworkRocket> _rockets = [];
    private readonly List<FireworkScheduledPop> _delayedPops = [];
    private double _spawnClock;
    private double _width = 1, _height = 1, _intensity = 1;
    private int _particleLimit = 160;
    private bool _enabled = true, _reduceMotion, _active = true;
    public IReadOnlyList<FireworkParticle> Particles => _particles;
    public IReadOnlyList<FireworkRocket> Rockets => _rockets;
    public int ActiveParticleCount => _particles.Count + _rockets.Count + _delayedPops.Count * 9;
    public int PendingCracklePopCount => _delayedPops.Count;
    public bool IsAnimating => _rockets.Count > 0 || _particles.Count > 0 || _delayedPops.Count > 0;
    public bool IsRunning => _enabled && !_reduceMotion && _active && _particleLimit > 0 && _intensity > 0;
    public int BurstCount { get; private set; }
    public FireworkBurstKind? LastBurstKind { get; private set; }
    public event Action<string>? SoundRequested;

    public FireworkSimulation() : this(Random.Shared.Next()) { }
    public FireworkSimulation(int seed) => _random = new Random(seed);

    public void Configure(bool enabled, int particleLimit, double intensity, bool reduceMotion, bool active)
    {
        _enabled = enabled;
        _particleLimit = Math.Clamp(particleLimit, 0, 320);
        _intensity = double.IsFinite(intensity) ? Math.Clamp(intensity, 0, 2) : 1;
        _reduceMotion = reduceMotion;
        _active = active;
        if (!IsRunning || _intensity <= 0 || _particleLimit == 0) Clear();
        else TrimToBudget();
    }

    public void Resize(double width, double height)
    {
        width = double.IsFinite(width) ? Math.Max(1, width) : 1;
        height = double.IsFinite(height) ? Math.Max(1, height) : 1;
        double sx = width / _width, sy = height / _height;
        foreach (var p in _particles)
        {
            p.X *= sx; p.Y *= sy; p.PreviousX *= sx; p.PreviousY *= sy; p.Vx *= sx; p.Vy *= sy;
            for (int i = 0; i < p._trailPoints.Count; i++)
            {
                var point = p._trailPoints[i];
                p._trailPoints[i] = new FireworkTrailPoint(point.X * sx, point.Y * sy);
            }
        }
        foreach (var r in _rockets) { r.X *= sx; r.Y *= sy; r.Vx *= sx; r.Vy *= sy; r.TargetY *= sy; }
        foreach (var pop in _delayedPops) { pop.X *= sx; pop.Y *= sy; }
        _width = width; _height = height;
    }

    public void Clear()
    {
        _particles.Clear(); _rockets.Clear(); _delayedPops.Clear(); _spawnClock = 0; BurstCount = 0; LastBurstKind = null;
    }

    public void Step(double deltaSeconds)
    {
        if (!IsRunning || !double.IsFinite(deltaSeconds) || deltaSeconds <= 0 || _particleLimit == 0 || _intensity <= 0) return;
        // A suspended dispatcher advances one ordinary frame only; it never catches up with a volley.
        double dt = Math.Min(deltaSeconds, 1d / 30d);
        _spawnClock -= dt;
        if (_spawnClock <= 0)
        {
            LaunchRocket();
            _spawnClock = Next(1.25, 2.65) / Math.Clamp(_intensity, .35, 1.5);
        }
        for (int i = _rockets.Count - 1; i >= 0; i--)
        {
            var r = _rockets[i];
            r.TrailClock += dt;
            if (r.TrailClock >= .045)
            {
                r.TrailClock = 0;
                AddParticle(new FireworkParticle { X = r.X, Y = r.Y, Vx = -r.Vx * .08 + Next(-10, 10), Vy = -r.Vy * .08 + Next(-10, 10), Age = 0, Lifetime = Next(.18, .35), Size = Next(1.1, 2.2), Drag = 2.2, Gravity = 22, Color = r.Color, IsTrail = true });
            }
            r.X = Math.Clamp(r.X + r.Vx * dt, 2, Math.Max(2, _width - 2));
            r.Y += r.Vy * dt; r.Vy += 30 * dt;
            if (r.Y <= r.TargetY || r.Vy >= -8)
            {
                _rockets.RemoveAt(i); Burst(r.X, r.Y, r.Color);
                SoundRequested?.Invoke("firework-boom");
            }
        }
        StepDelayedPops(dt);
        for (int i = _particles.Count - 1; i >= 0; i--)
        {
            var p = _particles[i];
            p.PreviousX = p.X; p.PreviousY = p.Y;
            p.Age += dt;
            if (p.Age >= p.Lifetime) { _particles.RemoveAt(i); continue; }
            p.Vx *= Math.Exp(-p.Drag * dt); p.Vy = p.Vy * Math.Exp(-p.Drag * dt) + p.Gravity * dt;
            p.X += p.Vx * dt; p.Y += p.Vy * dt;
            if (p.LeavesTrail)
            {
                p.TrailClock += dt;
                if (p.TrailClock >= .055)
                {
                    p.TrailClock -= .055;
                    p._trailPoints.Add(new FireworkTrailPoint(p.X, p.Y));
                    if (p._trailPoints.Count > 7) p._trailPoints.RemoveAt(0);
                }
            }
        }
    }

    private void LaunchRocket()
    {
        if (ActiveParticleCount >= _particleLimit) return;
        // Random heights deliberately reach into the lower half as well as the skyline.
        double targetY = Next(_height * .18, _height * .76);
        double startY = _height + 4;
        double travel = Math.Max(1, startY - targetY);
        double vy = -Math.Sqrt(2 * 30 * travel);
        double flightSeconds = Math.Sqrt(2 * travel / 30);
        double x = Next(_width * .04, _width * .96);
        double targetX = Next(_width * .06, _width * .94);
        double vx = (targetX - x) / flightSeconds;
        _rockets.Add(new FireworkRocket { X = x, Y = startY, Vx = vx, Vy = vy, TargetY = targetY, Color = Palette[_random.Next(Palette.Length)] });
        SoundRequested?.Invoke("rocket-launch");
    }

    private void Burst(double x, double y, uint baseColor)
    {
        if (ActiveParticleCount >= _particleLimit) return;
        var kind = (FireworkBurstKind)_random.Next(5);
        LastBurstKind = kind; BurstCount++;
        uint secondary = Palette[_random.Next(Palette.Length)];
        int reservedForDelayedPops = kind == FireworkBurstKind.Crackle ? 18 : 0;
        int count = kind switch { FireworkBurstKind.Ring => 34, FireworkBurstKind.GoldWillow => 40, FireworkBurstKind.Palm => 10, FireworkBurstKind.Crackle => 28, _ => 38 };
        count = Math.Min(count, Math.Max(0, _particleLimit - ActiveParticleCount - reservedForDelayedPops));
        for (int i = 0; i < count; i++)
        {
            double angle = Math.PI * 2 * i / count + Next(-.045, .045);
            double speed = kind switch
            {
                // A fixed shell speed makes the ring read as a circle, including at wide resolutions.
                FireworkBurstKind.Ring => 86,
                FireworkBurstKind.GoldWillow => Next(60, 82),
                FireworkBurstKind.Palm => Next(130, 172),
                FireworkBurstKind.Crackle => Next(120, 176),
                _ => Next(122, 168)
            };
            uint color = kind == FireworkBurstKind.GoldWillow ? 0xFFFFD17A : kind == FireworkBurstKind.Crackle ? secondary : i % 5 == 0 ? secondary : baseColor;
            AddParticle(new FireworkParticle
            {
                X = x, Y = y, PreviousX = x, PreviousY = y, Vx = Math.Cos(angle) * speed, Vy = Math.Sin(angle) * speed,
                Age = 0, Lifetime = kind switch
                {
                    FireworkBurstKind.GoldWillow => Next(2.5, 3.1),
                    FireworkBurstKind.Ring => Next(1.9, 2.2),
                    FireworkBurstKind.Palm => Next(2.1, 2.6),
                    FireworkBurstKind.Crackle => Next(1.45, 1.9),
                    _ => Next(1.9, 2.4)
                },
                Size = kind == FireworkBurstKind.Palm ? Next(1.8, 2.5) : kind == FireworkBurstKind.GoldWillow ? Next(1.25, 1.9) : Next(1.45, 2.25),
                Drag = kind switch
                {
                    FireworkBurstKind.GoldWillow => .22,
                    FireworkBurstKind.Ring => .08,
                    FireworkBurstKind.Palm => .5,
                    FireworkBurstKind.Crackle => .6,
                    _ => .6
                },
                Gravity = kind switch
                {
                    FireworkBurstKind.GoldWillow => 82,
                    FireworkBurstKind.Ring => 0,
                    FireworkBurstKind.Palm => 33,
                    FireworkBurstKind.Crackle => 52,
                    _ => 36
                },
                Color = color, LeavesTrail = true
            });
        }
        if (kind == FireworkBurstKind.Crackle)
        {
            // Two compact delayed pops create a crackle without an intense screen flash.
            for (int pop = 0; pop < 2 && ActiveParticleCount + 9 <= _particleLimit; pop++)
            {
                double ox = Next(-24, 24), oy = Next(-24, 24);
                _delayedPops.Add(new FireworkScheduledPop { X = x + ox, Y = y + oy, Delay = Next(.20, .48), Color = secondary });
            }
        }
    }

    private void StepDelayedPops(double dt)
    {
        for (int i = _delayedPops.Count - 1; i >= 0; i--)
        {
            var pop = _delayedPops[i];
            pop.Delay -= dt;
            if (pop.Delay > 0) continue;
            _delayedPops.RemoveAt(i);
            for (int j = 0; j < 9 && ActiveParticleCount < _particleLimit; j++)
            {
                double angle = Math.PI * 2 * j / 9;
                double speed = Next(25, 52);
                AddParticle(new FireworkParticle
                {
                    X = pop.X, Y = pop.Y, PreviousX = pop.X, PreviousY = pop.Y,
                    Vx = Math.Cos(angle) * speed, Vy = Math.Sin(angle) * speed,
                    Age = 0, Lifetime = Next(.35, .65), Size = 1.2, Drag = 1.1, Gravity = 42, Color = pop.Color
                });
            }
        }
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
            else if (_rockets.Count > 0) _rockets.RemoveAt(0);
        }
    }
    private double Next(double min, double max) => min + _random.NextDouble() * (max - min);
}
