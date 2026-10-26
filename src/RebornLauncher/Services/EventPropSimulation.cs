namespace PokemonRebornLauncher.Services;

public enum PropKind
{
    Snowman, Snowball, SnowPile, Icicle, PineTree, IceCrystal, Rocket, Sparkler,
    Confetti, PartyHat, Egg, ChocolateBunny, Nest, Flower, Butterfly, Pumpkin,
    Scarecrow, Gravestone, Spider, Cauldron, Candle, Gift, Ornament, Bell,
    SnowGlobe, CandyCane, Bubble, WaterBalloon, Firefly, LeafPile, Acorn,
    Mushroom, Star, LightCrystal, Ghost, Bat, SkeletonHand, Chick, Rabbit, LightString, Spellbook, Potion
}

public enum PropState { Idle, Active, Broken, Cooldown }
public enum BatFlightPhase { Perched, Anticipation, Flying, Away, Returning }

/// <summary>Coordinates describe the prop's centre in a 1000 by 400 world, Y downwards.</summary>
public sealed class EventProp
{
    public int Id { get; internal set; }
    public PropKind Kind { get; internal set; }
    public double X { get; internal set; }
    public double Y { get; internal set; }
    public double Scale { get; internal set; } = 1;
    public double Rotation { get; internal set; }
    public PropState State { get; internal set; }
    public BatFlightPhase BatPhase { get; internal set; }
    public double Age { get; internal set; }
    public double Opacity { get; internal set; } = 1;
    public bool IsRare { get; internal set; }
    /// <summary>Rocket explosion: 0 radial, 1 ring, 2 fountain, 3 heart.</summary>
    public int Variant { get; internal set; }
    internal double HomeX, HomeY, HomeScale, StateAge, RespawnAfter, SinceActivation, Vx, Vy;
    internal double PendingBurst = -1, EmitClock, Lifetime, ElapsedLifetime;
    internal bool EggCollected, HasSpawned, IsTemporary;
    internal int Activations;
}

public sealed class PropParticle
{
    public double X { get; internal set; }
    public double Y { get; internal set; }
    public double Vx { get; internal set; }
    public double Vy { get; internal set; }
    public double Size { get; internal set; }
    public double Rotation { get; internal set; }
    /// <summary>Remaining lifetime in seconds; opacity can use Life / MaxLife.</summary>
    public double Life { get; internal set; }
    public double MaxLife { get; internal set; }
    public string Kind { get; internal set; } = "spark";
    public uint Color { get; internal set; }
    internal double Gravity, Bounce, Spin;
}

/// <summary>Seeded, UI-independent seasonal interactions. Call Step from the UI clock.</summary>
public sealed class EventPropSimulation
{
    public const double WorldWidth = 1000, WorldHeight = 400;
    private readonly Random _random;
    private readonly List<EventProp> _props = [];
    private readonly List<PropParticle> _particles = [];
    private readonly List<EventProp> _pendingSpawns = [];
    private readonly IReadOnlyList<EventProp> _propView;
    private readonly IReadOnlyList<PropParticle> _particleView;
    private bool _particlesEnabled = true;
    private int _maxParticles = 160, _nextId;
    private double _intensity = 1;
    public IReadOnlyList<EventProp> Props => _propView;
    public IReadOnlyList<PropParticle> Particles => _particleView;
    public int FoundEggs { get; private set; }
    /// <summary>Short firework glow, 0..1. Suppressed when particles or motion are disabled.</summary>
    public double Flash { get; private set; }
    public event Action<string>? SoundRequested;

    public EventPropSimulation(int seed = 7321)
    {
        _random = new Random(seed);
        _propView = _props.AsReadOnly();
        _particleView = _particles.AsReadOnly();
    }

    public void Configure(bool particlesEnabled, int maxParticles, double intensity)
    {
        _particlesEnabled = particlesEnabled;
        _maxParticles = Math.Clamp(maxParticles, 0, 320);
        _intensity = double.IsFinite(intensity) ? Math.Clamp(intensity, 0, 2) : 1;
        int keep = particlesEnabled ? _maxParticles : 0;
        if (_particles.Count > keep) _particles.RemoveRange(keep, _particles.Count - keep);
        if (!particlesEnabled || _intensity == 0) Flash = 0;
    }

    /// <summary>Commits a renderer's placement to the model while preserving ongoing movement.
    /// Existing particles remain independent; newly emitted effects use the relocated position.</summary>
    public void RelocateHome(int id, double x, double y)
    {
        if (!double.IsFinite(x) || !double.IsFinite(y)) return;
        EventProp? prop = _props.Find(p => p.Id == id && !p.IsTemporary);
        if (prop is null) return;
        double nextX = Math.Clamp(x, 0, WorldWidth), nextY = Math.Clamp(y, 0, WorldHeight);
        prop.X = Math.Clamp(prop.X + nextX - prop.HomeX, 0, WorldWidth);
        prop.Y = Math.Clamp(prop.Y + nextY - prop.HomeY, 0, WorldHeight);
        prop.HomeX = nextX;
        prop.HomeY = nextY;
    }

    public void Reset(string theme, bool christmas = false)
    {
        _props.Clear();
        _particles.Clear();
        _pendingSpawns.Clear();
        FoundEggs = 0;
        Flash = 0;
        // Never recycle IDs: a queued click from the previous scene cannot activate a new prop.
        // Authored compositions are world-space preferences. The renderer can pack these around
        // the current artwork and text exclusions by committing the resolved home positions.
        (PropKind Kind, double X, double Y, double Scale)[] placements = theme?.ToLowerInvariant() switch
        {
            "winter" =>
            [
                (PropKind.Snowman, 124, 328, 1.04), (PropKind.Snowball, 270, 333, .82),
                (PropKind.SnowPile, 421, 336, .65), (PropKind.Icicle, 183, 83, .92),
                (PropKind.PineTree, 884, 303, 1.08), (PropKind.IceCrystal, 763, 227, .86)
            ],
            "spring" =>
            [
                (PropKind.Egg, 154, 326, .87), (PropKind.ChocolateBunny, 847, 329, 1.03),
                (PropKind.Nest, 304, 316, .96), (PropKind.Flower, 82, 247, .95),
                (PropKind.Butterfly, 736, 100, .90), (PropKind.Egg, 434, 334, .72),
                (PropKind.Egg, 621, 302, .79), (PropKind.Rabbit, 933, 252, .90)
            ],
            "halloween" =>
            [
                (PropKind.Pumpkin, 190, 327, 1.04), (PropKind.Scarecrow, 863, 313, 1.03),
                (PropKind.Gravestone, 716, 332, .91), (PropKind.Spider, 100, 86, .84),
                (PropKind.Cauldron, 348, 327, 1.04), (PropKind.Candle, 586, 294, .79),
                (PropKind.Ghost, 825, 119, .95), (PropKind.Bat, 282, 110, .86),
                (PropKind.Spellbook, 470, 330, .96), (PropKind.Potion, 792, 244, .85)
            ],
            "newyear" =>
            [
                (PropKind.Rocket, 115, 331, .94), (PropKind.Sparkler, 764, 97, .96),
                (PropKind.Confetti, 319, 315, .84), (PropKind.PartyHat, 906, 326, .99),
                (PropKind.Rocket, 522, 334, .83), (PropKind.Star, 177, 146, .77),
                (PropKind.Rocket, 729, 321, .98)
            ],
            "summer" =>
            [
                (PropKind.Bubble, 106, 106, .88), (PropKind.WaterBalloon, 843, 328, 1.00),
                (PropKind.Firefly, 659, 178, .81), (PropKind.Bubble, 315, 245, 1.05),
                (PropKind.WaterBalloon, 199, 329, .85), (PropKind.Firefly, 900, 83, .74)
            ],
            "harvest" =>
            [
                (PropKind.LeafPile, 201, 329, .98), (PropKind.Acorn, 381, 302, .80),
                (PropKind.Mushroom, 858, 327, .94), (PropKind.LeafPile, 686, 316, .92),
                (PropKind.Acorn, 107, 226, .77), (PropKind.Mushroom, 781, 250, .82)
            ],
            "arceus" =>
            [
                (PropKind.LightCrystal, 142, 286, 1.12), (PropKind.Star, 272, 112, .86),
                (PropKind.LightCrystal, 856, 307, 1.04), (PropKind.Star, 716, 85, .76),
                (PropKind.LightCrystal, 622, 259, .91), (PropKind.Star, 381, 334, .78)
            ],
            _ =>
            [
                (PropKind.Star, 122, 154, .86), (PropKind.LightCrystal, 839, 320, 1.05),
                (PropKind.Star, 701, 111, .75), (PropKind.LightCrystal, 276, 331, .88)
            ]
        };
        if (theme?.Equals("winter", StringComparison.OrdinalIgnoreCase) == true && christmas)
            placements =
            [
                .. placements,
                (PropKind.Gift, 792, 332, .91), (PropKind.Ornament, 852, 192, .76),
                (PropKind.Bell, 934, 241, .78), (PropKind.SnowGlobe, 580, 328, .86),
                (PropKind.CandyCane, 691, 315, .83), (PropKind.LightString, 508, 82, 1.04)
            ];
        for (int i = 0; i < placements.Length; i++)
        {
            var placement = placements[i];
            var prop = new EventProp
            {
                Id = ++_nextId, Kind = placement.Kind, HomeX = placement.X, HomeY = placement.Y,
                X = placement.X, Y = placement.Y, Scale = placement.Scale, Variant = _random.Next(4),
                IsRare = theme == "arceus" && i == 1 || _random.Next(20) == 0
            };
            prop.HomeScale = prop.Scale;
            _props.Add(prop);
        }
    }

    public bool Activate(int propId)
    {
        EventProp? prop = _props.Find(p => p.Id == propId);
        if (prop is null || prop.State != PropState.Idle) return false;
        prop.State = PropState.Active;
        prop.StateAge = prop.SinceActivation = 0;
        prop.RespawnAfter = Next(8, 15);
        prop.Rotation = 0;
        prop.HasSpawned = false;
        prop.EmitClock = 0;
        if (prop.Activations++ > 0) prop.Variant = (prop.Variant + 1) % 4;
        switch (prop.Kind)
        {
            case PropKind.Bat:
                prop.BatPhase = BatFlightPhase.Anticipation;
                prop.RespawnAfter = 3.2 + Next(8, 15);
                SoundRequested?.Invoke("bat-flap");
                return true;
            case PropKind.Rocket:
                prop.Vx = Next(-20, 20); prop.Vy = -Next(180, 230);
                SoundRequested?.Invoke("rocket-launch");
                return true;
            case PropKind.Icicle:
                prop.Vy = 0;
                SoundRequested?.Invoke("wind");
                return true;
            case PropKind.Snowball:
                prop.Vx = prop.X < 500 ? Next(170, 220) : -Next(170, 220);
                prop.Vy = -Next(210, 260);
                SoundRequested?.Invoke("snow-crunch");
                return true;
            case PropKind.Scarecrow:
                prop.Vy = -250;
                SoundRequested?.Invoke("wood-creak");
                return true;
            case PropKind.SnowPile:
                prop.HomeScale = Math.Min(1, prop.HomeScale + .075);
                prop.Scale = prop.HomeScale;
                break;
        }
        if (prop.Kind == PropKind.Egg && !prop.EggCollected)
        {
            FoundEggs++;
            prop.EggCollected = true;
        }
        if (prop.Kind is not (PropKind.Ornament or PropKind.Gravestone or PropKind.Bell or PropKind.Cauldron
            or PropKind.Spider or PropKind.LightString or PropKind.Gift or PropKind.Spellbook or PropKind.Potion or PropKind.Bat)) EmitInteraction(prop);
        SoundRequested?.Invoke(prop.Kind switch
        {
            PropKind.Bell => "bell",
            PropKind.Egg or PropKind.Nest => "egg-crack",
            PropKind.ChocolateBunny or PropKind.CandyCane => "chocolate-snap",
            PropKind.Bubble or PropKind.WaterBalloon => "water-pop",
            PropKind.IceCrystal => "ice-break",
            PropKind.SnowGlobe => "glass-break",
            PropKind.Ornament => "bell",
            PropKind.Cauldron => "cauldron-bubble",
            PropKind.Ghost or PropKind.Spider => "ghost",
            PropKind.Bat => "bat-flap",
            PropKind.Gravestone => "wood-creak",
            PropKind.Pumpkin => "pumpkin-pop",
            PropKind.Spellbook or PropKind.Potion => "magic",
            PropKind.Gift => "gift-rip",
            PropKind.Star or PropKind.LightCrystal or PropKind.LightString => "magic",
            PropKind.Sparkler or PropKind.Firefly => "sparkle",
            PropKind.Confetti or PropKind.PartyHat => "confetti",
            PropKind.Snowman => "snow-poof",
            PropKind.SnowPile => "snow-crunch",
            PropKind.LeafPile or PropKind.Acorn or PropKind.Mushroom => "leaf-rustle",
            PropKind.Chick => "bird-chirp",
            _ => "rustle"
        });
        return true;
    }

    public void Step(double deltaSeconds, bool reduceMotion = false)
    {
        if (!double.IsFinite(deltaSeconds) || deltaSeconds <= 0) return;
        double dt = Math.Min(deltaSeconds, .05);
        Flash = reduceMotion ? 0 : Math.Max(0, Flash - dt * 2.5);
        foreach (EventProp prop in _props)
        {
            prop.Age += dt;
            prop.ElapsedLifetime += dt;
            if (prop.IsTemporary) { StepTemporary(prop, dt, reduceMotion); continue; }
            if (prop.PendingBurst >= 0)
            {
                prop.PendingBurst -= dt;
                if (prop.PendingBurst <= 0)
                {
                    prop.PendingBurst = -1;
                    Explode(prop, reduceMotion, true);
                    SoundRequested?.Invoke("firework-boom");
                }
            }
            if (prop.State == PropState.Idle)
            {
                IdleMotion(prop, reduceMotion);
                continue;
            }
            prop.StateAge += dt;
            prop.SinceActivation += dt;
            if (prop.Kind == PropKind.Bat)
            {
                StepBatFlight(prop, reduceMotion);
                continue;
            }
            if (prop.SinceActivation >= prop.RespawnAfter)
            {
                ReturnIdle(prop);
                continue;
            }
            if (prop.State == PropState.Active)
            {
                StepActive(prop, dt, reduceMotion);
            }
            else if (prop.State == PropState.Broken && prop.StateAge >= .6)
            {
                prop.State = PropState.Cooldown;
                prop.StateAge = 0;
            }
        }
        // Child props are queued while enumerating; no timers or async callbacks survive Reset.
        _props.RemoveAll(p => p.IsTemporary && p.ElapsedLifetime >= p.Lifetime);
        _props.AddRange(_pendingSpawns);
        _pendingSpawns.Clear();
        StepParticles(dt, reduceMotion);
    }

    public void CelebrateHalloweenSecret(int sourceId, bool reducedMotion)
    {
        if (reducedMotion || !_particlesEnabled || _intensity <= 0) return;
        var source = _props.Find(p => p.Id == sourceId);
        if (source is null) return;
        for (int i = 0; i < 18; i++)
            Emit(source.X, source.Y - 12, "spark", 0xFFFFD677, Next(2, 5), Next(-45, 45), -Next(25, 70), 1.3, 12, 0);
        SpawnChild(source, PropKind.Ghost, 1.6);
    }

    public void CancelBatFlights()
    {
        foreach (var prop in _props.Where(p => p.Kind == PropKind.Bat && !p.IsTemporary)) ReturnIdle(prop);
    }

    private static void StepBatFlight(EventProp p, bool reduced)
    {
        double t = p.SinceActivation;
        if (t >= p.RespawnAfter)
        {
            p.BatPhase = BatFlightPhase.Returning; p.State = PropState.Cooldown;
            p.X = p.HomeX; p.Y = p.HomeY; p.Scale = p.HomeScale; p.Rotation = 0;
            double fade = Math.Clamp((t - p.RespawnAfter) / .55, 0, 1);
            p.Opacity = fade * fade * (3 - 2 * fade);
            if (fade >= 1) ReturnIdle(p);
            return;
        }
        if (reduced)
        {
            p.X = p.HomeX; p.Y = p.HomeY; p.Scale = p.HomeScale; p.Rotation = 0;
            p.Opacity = 1 - Math.Clamp(t / .35, 0, 1);
            p.BatPhase = t < .35 ? BatFlightPhase.Anticipation : BatFlightPhase.Away;
            if (t >= .35) p.State = PropState.Cooldown;
            return;
        }
        if (t <= .2)
        {
            p.BatPhase = BatFlightPhase.Anticipation;
            return;
        }
        if (t >= 3.2)
        {
            p.BatPhase = BatFlightPhase.Away; p.State = PropState.Cooldown; p.Opacity = 0;
            return;
        }
        p.BatPhase = BatFlightPhase.Flying;
        double u = Math.Clamp((t - .2) / 3, 0, 1), s = u * u * (2 - u), r = 1 - s;
        double endX = Math.Min(960, p.HomeX + 120), endY = Math.Max(35, p.HomeY - 140);
        p.X = r*r*r*p.HomeX + 3*r*r*s*Math.Min(960,p.HomeX+14) + 3*r*s*s*Math.Min(960,p.HomeX+80) + s*s*s*endX;
        p.Y = Math.Max(30, r*r*r*p.HomeY + 3*r*r*s*Math.Max(30,p.HomeY-65) +
            3*r*s*s*Math.Max(30,p.HomeY-120) + s*s*s*endY + Math.Sin(t*42)*1.5*(1-u));
        p.Scale = p.HomeScale * (1 - .55*s);
        p.Rotation = -14*Math.Sin(s*Math.PI) + Math.Sin(t*42)*1.5;
        double fadeOut = Math.Clamp((u - .72) / .28, 0, 1);
        p.Opacity = 1 - fadeOut*fadeOut*(3-2*fadeOut);
    }

    private void StepActive(EventProp p, double dt, bool reduced)
    {
        double t = p.StateAge, motion = reduced ? .25 : 1;
        switch (p.Kind)
        {
            case PropKind.Rocket:
                p.X = Math.Clamp(p.X + p.Vx * dt, 25, 975);
                p.Y = Math.Max(42, p.Y + p.Vy * dt);
                p.Rotation = p.Vx * .12;
                Emit(p.X, p.Y + 17, "spark", 0xFFFFBA5A, 2, Next(-10, 10), 40, .35, 0, 0);
                if (p.Y <= 85 || t >= 1.65)
                {
                    Explode(p, reduced);
                    SoundRequested?.Invoke("firework-boom");
                    p.PendingBurst = .25;
                    Break(p);
                }
                return;
            case PropKind.Icicle:
                p.Vy += 560 * dt;
                p.Y = Math.Min(374, p.Y + p.Vy * dt);
                p.Rotation = Math.Min(12, t * 8) * motion;
                if (p.Y >= 374)
                {
                    EmitInteraction(p); SoundRequested?.Invoke("ice-break"); Break(p);
                }
                return;
            case PropKind.Snowball:
                p.Vy += 450 * dt;
                p.X = Math.Clamp(p.X + p.Vx * dt, 16, 984);
                p.Y = Math.Min(384, p.Y + p.Vy * dt);
                p.Rotation += dt * p.Vx;
                if (p.Y >= 384)
                {
                    EmitInteraction(p); SoundRequested?.Invoke("snowball-hit"); Break(p);
                }
                return;
            case PropKind.Scarecrow:
                p.Vy += 530 * dt;
                p.Y = Math.Min(p.HomeY, p.Y + p.Vy * dt * motion);
                p.Rotation = (t < .8 ? t * 420 : Math.Sin(t * 28) * 13) * motion;
                if (!p.HasSpawned && t >= .28)
                {
                    p.HasSpawned = true;
                    SpawnChild(p, PropKind.Ghost, 1.6);
                    SoundRequested?.Invoke("ghost");
                }
                if (t >= 1.35) ReturnIdle(p);
                return;
            case PropKind.Ornament:
                p.Rotation = Math.Sin(t * 17) * 36 * motion;
                p.X = p.HomeX + Math.Sin(t * 17) * 12 * motion;
                if (t >= .9)
                {
                    EmitInteraction(p); SoundRequested?.Invoke("glass-break"); Break(p);
                }
                return;
            case PropKind.Bell:
                p.Rotation = Math.Sin(t * 24) * Math.Exp(-t * 1.6) * 38 * motion;
                if (t >= 1.4) ReturnIdle(p);
                return;
            case PropKind.Gift:
                p.Rotation = Math.Sin(t * 24) * 8 * motion;
                p.Scale = p.HomeScale * (1 + Math.Min(t, .35) * .35);
                if (!p.HasSpawned && t >= .4)
                {
                    p.HasSpawned = true;
                    if (p.Variant % 2 == 0) SpawnChild(p, PropKind.Flower, 4);
                    else EmitInteraction(p);
                }
                if (t >= .6) Break(p);
                return;
            case PropKind.Egg:
            case PropKind.Nest:
                p.Rotation = Math.Sin(t * 34) * 14 * motion;
                if (!p.HasSpawned && t >= .32)
                {
                    p.HasSpawned = true;
                    SpawnChild(p, p.Variant % 3 == 0 ? PropKind.Rabbit : PropKind.Chick, 4.5);
                    SoundRequested?.Invoke("bird-chirp");
                }
                if (t >= .5) Break(p);
                return;
            case PropKind.Gravestone:
                p.Rotation = -Math.Min(1, t * 3) * 24 * motion;
                if (!p.HasSpawned && t >= .35)
                {
                    p.HasSpawned = true;
                    SpawnChild(p, PropKind.SkeletonHand, 2.6);
                    SoundRequested?.Invoke("ghost");
                }
                if (t >= 3) ReturnIdle(p);
                return;
            case PropKind.Spellbook:
                p.Rotation = Math.Sin(t * 8) * 3 * motion;
                if (t >= 1.4) ReturnIdle(p);
                return;
            case PropKind.Potion:
                p.Rotation = Math.Sin(t * 10) * 9 * motion;
                if (t >= 1) ReturnIdle(p);
                return;
            case PropKind.Cauldron:
                p.Rotation = Math.Sin(t * 12) * 3 * motion;
                p.EmitClock += dt;
                while (p.EmitClock >= .12)
                {
                    p.EmitClock -= .12;
                    Emit(p.X + Next(-13, 13), p.Y - 12, "bubble", Palette((int)(t * 10)), Next(3, 8), Next(-12, 12), -Next(35, 70), 1.5, -15, 0);
                }
                if (t >= 2.6) ReturnIdle(p);
                return;
            case PropKind.Spider:
                p.Y = p.HomeY + (t < .8 ? t / .8 : Math.Max(0, 1 - (t - .8) / 1.1)) * 155 * motion;
                p.Rotation = Math.Sin(t * 13) * 12 * motion;
                if (t >= 1.9) ReturnIdle(p);
                return;
            case PropKind.Butterfly:
            case PropKind.Rabbit:
                p.X = Math.Clamp(p.HomeX + (p.HomeX < 500 ? 1 : -1) * t * 105 * motion, 20, 980);
                p.Y = p.Kind == PropKind.Rabbit ? p.HomeY - Math.Abs(Math.Sin(t * 9)) * 38 * motion
                    : Math.Max(25, p.HomeY - t * 85 * motion + Math.Sin(t * 14) * 10 * motion);
                p.Rotation = Math.Sin(t * 19) * 14 * motion;
                p.Opacity = Math.Clamp(1.7 - t, 0, 1);
                if (t >= 1.7) Break(p);
                return;
            case PropKind.Ghost:
                p.Y = Math.Max(30, p.HomeY - t * 65 * motion);
                p.X = Math.Clamp(p.HomeX + Math.Sin(t * 8) * 22 * motion, 20, 980);
                p.Opacity = Math.Max(0, 1 - t / 1.4);
                if (t >= 1.4) Break(p);
                return;
            case PropKind.LightString:
                p.Opacity = reduced ? 1 : .35 + .65 * (.5 + .5 * Math.Sin(t * 14));
                if (t >= 1.8) ReturnIdle(p);
                return;
            case PropKind.Sparkler:
            case PropKind.LightCrystal:
                p.EmitClock += dt;
                while (p.EmitClock >= .08)
                {
                    p.EmitClock -= .08;
                    double a = t * 9;
                    Emit(p.X + Math.Cos(a) * 14, p.Y - 10 + Math.Sin(a) * 14, "spark", p.IsRare ? 0xFFFFD970 : Palette((int)(t * 10)), 3, Math.Cos(a) * 60, Math.Sin(a) * 60 - 25, .9, 30, .1);
                }
                p.Scale = p.HomeScale * (1 + Math.Sin(t * 9) * .08 * motion);
                if (t >= 1.8) ReturnIdle(p);
                return;
            case PropKind.SnowPile:
            case PropKind.PineTree:
            case PropKind.Flower:
            case PropKind.Candle:
            case PropKind.Mushroom:
            case PropKind.Star:
                p.Rotation = Math.Sin(t * 14) * Math.Exp(-t) * 10 * motion;
                p.Scale = p.HomeScale * (1 + Math.Sin(Math.Min(t, 1) * Math.PI) * .12 * motion);
                if (p.Kind == PropKind.SnowPile) p.Scale = Math.Min(1, p.Scale);
                if (t >= 1.2) ReturnIdle(p);
                return;
            default:
                p.Rotation = Math.Sin(t * 22) * (reduced ? 2 : 12);
                p.Opacity = Math.Max(0, 1 - t / .4);
                if (t >= .4) Break(p);
                return;
        }
    }

    private static void ReturnIdle(EventProp p)
    {
        p.State = PropState.Idle;
        p.StateAge = p.Age = p.SinceActivation = 0;
        p.X = p.HomeX; p.Y = p.HomeY; p.Scale = p.HomeScale;
        p.Rotation = 0; p.Opacity = 1; p.Vx = p.Vy = 0;
        p.BatPhase = BatFlightPhase.Perched;
        // EggCollected intentionally survives respawn: the discovery counter cannot be farmed.
    }

    private void SpawnChild(EventProp parent, PropKind kind, double lifetime)
    {
        if (_props.Count + _pendingSpawns.Count >= 48) return;
        double x = Math.Clamp(parent.X + (parent.X < 500 ? 18 : -18), 24, 976);
        double y = Math.Clamp(parent.Y - 5, 35, 370);
        _pendingSpawns.Add(new EventProp
        {
            Id = ++_nextId, Kind = kind, X = x, Y = y, HomeX = x, HomeY = y,
            Scale = .7, HomeScale = .7, State = PropState.Active, IsTemporary = true,
            Lifetime = lifetime, IsRare = parent.IsRare, Variant = parent.Variant
        });
    }

    private static void StepTemporary(EventProp p, double dt, bool reduced)
    {
        p.StateAge += dt;
        double t = p.StateAge, motion = reduced ? .25 : 1;
        p.Opacity = Math.Clamp((p.Lifetime - p.ElapsedLifetime) / .7, 0, 1);
        if (p.Kind == PropKind.SkeletonHand)
            p.Y = p.HomeY + 20 - Math.Min(1, t * 3) * 45 * motion;
        else if (p.Kind == PropKind.Ghost)
        {
            p.Y = Math.Max(25, p.HomeY - t * 50 * motion);
            p.X = Math.Clamp(p.HomeX + Math.Sin(t * 5) * 18 * motion, 20, 980);
        }
        else if (p.Kind is PropKind.Chick or PropKind.Rabbit)
        {
            p.X = Math.Clamp(p.HomeX + (p.HomeX < 500 ? 1 : -1) * t * 25 * motion, 20, 980);
            p.Y = p.HomeY - Math.Abs(Math.Sin(t * (p.Kind == PropKind.Rabbit ? 8 : 5))) * 18 * motion;
        }
        else if (p.Kind == PropKind.Flower)
            p.Scale = p.HomeScale * Math.Min(1, t * 4);
    }

    private static void Break(EventProp prop)
    {
        prop.State = PropState.Broken; prop.StateAge = 0; prop.Opacity = 0;
    }

    private static void IdleMotion(EventProp p, bool reduceMotion)
    {
        p.X = p.HomeX; p.Y = p.HomeY; p.Rotation = 0; p.Opacity = 1;
        if (reduceMotion) return;
        double phase = p.Age * 1.8 + p.Id;
        switch (p.Kind)
        {
            case PropKind.Butterfly:
            case PropKind.Firefly:
            case PropKind.Bubble:
                p.X += Math.Sin(phase * .6) * 12;
                p.Y += Math.Cos(phase) * 14;
                p.Rotation = Math.Sin(phase) * 8;
                break;
            case PropKind.ChocolateBunny:
            case PropKind.Rabbit:
                p.Y -= Math.Max(0, Math.Sin(p.Age * 2.1)) * 9;
                break;
            case PropKind.Ghost:
                p.X = Math.Clamp(p.HomeX + Math.Sin(phase * .4) * 14, 20, 980);
                p.Y += Math.Sin(phase * .6) * 15;
                p.Opacity = .08 + .92 * Math.Pow(.5 + .5 * Math.Sin(phase * .5), 2);
                break;
            case PropKind.Spider:
                p.Y += Math.Sin(phase * .4) * 12;
                break;
        }
    }

    private void EmitInteraction(EventProp p)
    {
        (string kind, uint color, int count) = p.Kind switch
        {
            PropKind.Snowman or PropKind.Snowball or PropKind.SnowPile or PropKind.PineTree => ("snow", 0xFFE5F7FF, 22),
            PropKind.Icicle or PropKind.IceCrystal or PropKind.SnowGlobe or PropKind.Ornament => ("shard", 0xFFACECFF, 16),
            PropKind.LeafPile or PropKind.Acorn or PropKind.Mushroom => ("leaf", 0xFFD28A38, 23),
            PropKind.Confetti or PropKind.PartyHat or PropKind.Gift => ("confetti", 0xFFFFCC69, 28),
            PropKind.Cauldron or PropKind.Gravestone or PropKind.Candle or PropKind.Ghost => ("smoke", 0xFFAF8BC9, 18),
            PropKind.Bubble or PropKind.WaterBalloon => ("water", 0xFF79D7FF, 22),
            PropKind.Egg or PropKind.Nest => ("shard", p.IsRare ? 0xFFFFD65A : 0xFFFFE5B1, 12),
            PropKind.ChocolateBunny => ("shard", 0xFF995B38, 18),
            PropKind.Flower or PropKind.Butterfly => ("petal", 0xFFFFA9DE, 18),
            PropKind.Pumpkin or PropKind.Scarecrow or PropKind.Spider => ("leaf", 0xFFF09840, 18),
            _ => ("spark", p.IsRare ? 0xFFFFDB6C : 0xFFBCDCFF, 24)
        };
        if (p.Kind == PropKind.Snowman)
        {
            // Three recognisable body balls, followed by smaller powder.
            for (int i = 0; i < 3; i++)
                Emit(p.X, p.Y + (i == 0 ? 12 : i == 1 ? -10 : -29), "snowball", 0xFFF0FAFF, (18 - i * 4) * p.Scale,
                    (i - 1) * 75, -Next(110, 190), 4.5, 280, .48);
        }
        int n = (int)Math.Round(count * _intensity);
        for (int i = 0; i < n; i++)
        {
            double vx = Next(-130, 130), vy = -Next(70, 230);
            bool smoke = kind == "smoke";
            Emit(p.X, p.Y, kind, kind == "confetti" ? Palette(i) : color,
                Next(2, smoke ? 10 : 6), vx, smoke ? -Next(20, 65) : vy,
                Next(1.2, 3.4), smoke ? -12 : 260, kind is "snow" or "shard" or "water" ? .4 : .22);
        }
    }

    private void Explode(EventProp p, bool reduced, bool secondary = false)
    {
        int count = (int)Math.Round((p.IsRare ? 96 : 64) * _intensity * (secondary ? .6 : 1));
        int variant = (p.Variant + (secondary ? 1 : 0)) % 4;
        // At a low budget, each simultaneous rocket still gets a visible share of the burst.
        int reserve = Math.Min(count, Math.Max(1, _maxParticles / 3));
        int replace = Math.Min(_particles.Count, Math.Max(0, _particles.Count + reserve - _maxParticles));
        if (_particlesEnabled && replace > 0) _particles.RemoveRange(0, replace);
        if (_particlesEnabled && _intensity > 0 && !reduced)
            Flash = Math.Max(Flash, secondary ? .2 : p.IsRare ? .65 : .4);
        for (int i = 0; i < count; i++)
        {
            double angle = i * Math.PI * 2 / Math.Max(1, count);
            double speed = Next(60, 170) * (reduced ? .35 : 1);
            double vx = Math.Cos(angle) * speed, vy = Math.Sin(angle) * speed;
            switch (variant)
            {
                case 1: vx = Math.Cos(angle) * 125; vy = Math.Sin(angle) * 125; break;
                case 2: vx = Next(-75, 75); vy = -Next(60, 210); break;
                case 3:
                    vx = 8 * Math.Pow(Math.Sin(angle), 3) * 10;
                    vy = -(6.5 * Math.Cos(angle) - 2.5 * Math.Cos(2 * angle) - Math.Cos(3 * angle) - .5 * Math.Cos(4 * angle)) * 10;
                    break;
            }
            if (reduced && variant != 0) { vx *= .35; vy *= .35; }
            if (p.IsRare) { vx *= 1.2; vy *= 1.2; }
            Emit(p.X, p.Y, "spark", p.IsRare ? 0xFFFFD970 : Palette(i + variant), Next(2, p.IsRare ? 5.5 : 4.5), vx, vy, Next(1.4, 2.6), 60, .15);
        }
    }

    private void Emit(double x, double y, string kind, uint color, double size, double vx, double vy,
        double life, double gravity, double bounce)
    {
        if (!_particlesEnabled || _intensity <= 0 || _particles.Count >= _maxParticles) return;
        _particles.Add(new PropParticle
        {
            X = x, Y = y, Vx = vx, Vy = vy, Size = size, Life = life, MaxLife = life,
            Kind = kind, Color = color, Gravity = gravity, Bounce = bounce, Spin = Next(-140, 140)
        });
    }

    private void StepParticles(double dt, bool reduced)
    {
        double motion = reduced ? .35 : 1;
        for (int i = _particles.Count - 1; i >= 0; i--)
        {
            var p = _particles[i];
            p.Life -= dt;
            if (p.Life <= 0) { _particles.RemoveAt(i); continue; }
            p.Vy += p.Gravity * dt;
            p.X += p.Vx * dt * motion;
            p.Y += p.Vy * dt * motion;
            p.Rotation = (p.Rotation + p.Spin * dt * motion) % 360;
            p.Vx *= Math.Exp(-.65 * dt);
            if (p.X < p.Size) { p.X = p.Size; p.Vx = Math.Abs(p.Vx) * p.Bounce; }
            if (p.X > WorldWidth - p.Size) { p.X = WorldWidth - p.Size; p.Vx = -Math.Abs(p.Vx) * p.Bounce; }
            if (p.Y < p.Size) { p.Y = p.Size; p.Vy = Math.Abs(p.Vy) * p.Bounce; }
            if (p.Y > WorldHeight - p.Size)
            {
                p.Y = WorldHeight - p.Size;
                p.Vy = -Math.Abs(p.Vy) * p.Bounce;
                p.Vx *= .78;
                if (Math.Abs(p.Vy) < 12) p.Vy = 0;
            }
        }
    }

    private double Next(double min, double max) => min + _random.NextDouble() * (max - min);
    private static uint Palette(int i) => (i % 6) switch
    {
        0 => 0xFFFF739C, 1 => 0xFFFFD970, 2 => 0xFF8FFFC2,
        3 => 0xFF91CDFF, 4 => 0xFFD6A5FF, _ => 0xFFFFF1C4
    };
}
