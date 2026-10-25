using System.Windows;
using System.Windows.Media;
using System.Reflection;
using PokemonRebornLauncher.UI;
using PokemonRebornLauncher.Services;

internal static class Program
{
    private sealed record ActorProfile(double CenterX, double FootY, double Size);
    private sealed record ThemeProfile(string Name, ActorProfile[] Actors);
    private static readonly List<string> Failures = [];
    private static readonly HashSet<string> FailureSet = new(StringComparer.Ordinal);
    private static readonly Size[] Stages = [new(688, 202), new(972, 362), new(1800, 650)];
    private static readonly ThemeProfile[] Themes =
    [
        new("standard", [new(.24,.94,.83),new(.76,.86,.50),new(.52,.56,.32)]),
        new("winter", [new(.20,.94,.86),new(.81,.88,.49),new(.55,.72,.36)]),
        new("halloween", [new(.51,.95,.78),new(.16,.65,.45),new(.85,.87,.38)]),
        new("spring", [new(.79,.93,.85),new(.24,.90,.53),new(.48,.53,.34)]),
        new("summer", [new(.27,.96,.73),new(.73,.91,.66),new(.50,.42,.28)]),
        new("harvest", [new(.43,.94,.85),new(.84,.72,.42),new(.13,.90,.36)]),
        new("newyear", [new(.72,.68,.60),new(.18,.96,.63),new(.46,.94,.36)]),
        new("arceus", [new(.49,.91,.88),new(.13,.63,.40),new(.85,.85,.40)])
    ];

    [STAThread]
    private static int Main()
    {
        try
        {
            int cases = 0;
            foreach (Size stage in Stages)
            foreach (ThemeProfile theme in Themes)
            {
                try { RunCase(stage, theme); }
                catch (Exception ex) { Record($"theme={theme.Name} stage={stage.Width:0}x{stage.Height:0} phase=unhandled: {ex}"); }
                cases++;
            }
            if (Failures.Count > 0)
            {
                Console.Error.WriteLine($"PROP_LAYOUT_FAILED cases={cases} failures={Failures.Count}");
                foreach (string failure in Failures.Take(100)) Console.Error.WriteLine(failure);
                if (Failures.Count > 100) Console.Error.WriteLine($"... {Failures.Count - 100} additional distinct failures");
                return 1;
            }
            Console.WriteLine($"PROP_LAYOUT_OK cases={cases} stages={Stages.Length} themes={Themes.Length}");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return 1;
        }
    }

    private static void RunCase(Size size, ThemeProfile theme)
    {
        var scene = new EventPropsScene();
        scene.Measure(size);
        scene.Arrange(new Rect(new Point(0, 0), size));
        scene.Configure(theme.Name, theme.Name == "winter", enabled: true, interactive: true,
            particleLimit: 0, intensity: 1, reduceMotion: false, active: false);
        Rect[] actorZones = MakeActorZones(size, theme);
        scene.SetExclusionZones(actorZones);

        Verify(scene, size, theme.Name, actorZones, "initial layout");
        IReadOnlyList<(int Id, PropKind Kind, Rect Bounds)> initial = scene.GetPlacementBounds();
        if (initial.Count == 0)
            RecordFailure(theme.Name, size, "none", Rect.Empty, "no permanent props were placed");
        if (scene.IsAnimating)
            RecordFailure(theme.Name, size, "scene", Rect.Empty, "active=false still started the animation timer");

        foreach (var item in initial)
        {
            VerifyBounds(item.Kind, item.Bounds, actorZones, size, theme.Name, "initial bounds");
            Point center = new(item.Bounds.X + item.Bounds.Width / 2, item.Bounds.Y + item.Bounds.Height / 2);
            HitTestResult? hit = VisualTreeHelper.HitTest(scene, center);
            if (hit?.VisualHit != scene)
                RecordFailure(theme.Name, size, item.Kind.ToString(), item.Bounds, $"center hit-test returned {hit?.VisualHit?.GetType().Name ?? "null"}");
        }

        // Advance an idle scene in bounded increments; placement must remain safe throughout.
        for (int i = 0; i < 30; i++)
        {
            scene.AdvanceForTest(.1);
            Verify(scene, size, theme.Name, actorZones, $"idle advance {i + 1}/30");
            if (scene.IsAnimating)
                RecordFailure(theme.Name, size, "scene", Rect.Empty, "active=false started animation during idle advance");
        }
        var settledA = scene.GetPlacementBounds();
        Verify(scene, size, theme.Name, actorZones, "stable repeated layout");
        var settledB = scene.GetPlacementBounds();
        if (!SameBounds(settledA, settledB))
            RecordFailure(theme.Name, size, "scene", Rect.Empty, "repeated VerifyLayout changed unchanged placements");

        // Resize away and back: homes must be recomputed from authored positions,
        // not accumulated from the previous packed layout.
        var boundsBeforeResize = scene.GetPlacementBounds().ToDictionary(x => x.Id, x => x.Bounds);
        Size intermediate = new(size.Width * 1.17, size.Height * 1.11);
        scene.Measure(intermediate);
        scene.Arrange(new Rect(new Point(0, 0), intermediate));
        var intermediateActors = MakeActorZones(intermediate, theme);
        scene.SetExclusionZones(intermediateActors);
        Verify(scene, intermediate, theme.Name, intermediateActors, "resize layout");
        scene.Measure(size);
        scene.Arrange(new Rect(new Point(0, 0), size));
        scene.SetExclusionZones(actorZones);
        Verify(scene, size, theme.Name, actorZones, "resize return layout");
        var boundsAfterResize = scene.GetPlacementBounds();
        if (!ApproximatelySameBounds(boundsBeforeResize.Select(x => (x.Key, scene.Props.First(p => p.Id == x.Key).Kind, x.Value)).ToArray(), boundsAfterResize))
        {
            var changed = boundsAfterResize.FirstOrDefault(x => !boundsBeforeResize.TryGetValue(x.Id, out Rect before) || before != x.Bounds);
            RecordFailure(theme.Name, size, changed.Kind.ToString(), changed.Bounds,
                $"resize away and back changed packed bounds from {boundsBeforeResize.GetValueOrDefault(changed.Id)}");
        }

        EventProp[] permanent = scene.Props.Where(p => !p.IsTemporary).ToArray();
        foreach (EventProp prop in permanent)
            if (!scene.ActivateForTest(prop.Id))
                RecordFailure(theme.Name, size, prop.Kind.ToString(), Rect.Empty, $"permanent prop id {prop.Id} did not activate from idle");

        // During effects props may leave the stage and cross actor artwork. The
        // rendered anchor must still be the model's physical coordinate.
        for (int i = 0; i < 60; i++)
        {
            scene.AdvanceForTest(.05);
            VerifyRenderAnchors(scene, size, theme.Name, $"interaction advance {(i + 1) * .05:0.00}s");
        }
        scene.AdvanceForTest(18);
        Verify(scene, size, theme.Name, actorZones, "post-respawn layout");
        if (scene.Props.Any(p => !p.IsTemporary && p.State != PropState.Idle))
            RecordFailure(theme.Name, size, "scene", Rect.Empty, "permanent prop did not respawn to idle after 18 seconds");
        if (scene.Props.Any(p => p.IsTemporary))
            RecordFailure(theme.Name, size, "temporary", Rect.Empty, "temporary child remained after 18 seconds");
        // Spawn an egg's temporary hatchling and confirm that adding it does not
        // cause permanent homes to be packed again.
        EventProp? egg = scene.Props.FirstOrDefault(p => !p.IsTemporary && p.Kind is PropKind.Egg or PropKind.Nest);
        if (egg is not null)
        {
            permanent = scene.Props.Where(p => !p.IsTemporary).ToArray();
            var homes = permanent.ToDictionary(p => p.Id, p => (p.HomeX, p.HomeY));
            scene.ActivateForTest(egg.Id);
            for (int i = 0; i < 8; i++) scene.AdvanceForTest(.05);
            if (!scene.Props.Any(p => p.IsTemporary))
                RecordFailure(theme.Name, size, egg.Kind.ToString(), Rect.Empty, "egg/nest interaction did not create temporary child");
            foreach (EventProp p in permanent)
                if (homes[p.Id] != (p.HomeX, p.HomeY))
                    RecordFailure(theme.Name, size, p.Kind.ToString(), Rect.Empty, "temporary child creation reflowed a permanent home");
        }
    }

    private static void VerifyRenderAnchors(EventPropsScene scene, Size stage, string theme, string phase)
    {
        MethodInfo? method = typeof(EventPropsScene).GetMethod("GetPropLayout", BindingFlags.Instance | BindingFlags.NonPublic);
        if (method is null) { RecordFailure(theme, stage, "scene", Rect.Empty, "GetPropLayout diagnostic method is missing"); return; }
        foreach (EventProp prop in scene.Props.Where(p => p.State != PropState.Idle))
        {
            object? layout = method.Invoke(scene, [prop]);
            if (layout is null) continue;
            Point center = (Point)layout.GetType().GetProperty("Center")!.GetValue(layout)!;
            Point expected = new(prop.X, prop.Y);
            if ((center - expected).Length > .001)
                RecordFailure(theme, stage, prop.Kind.ToString(), new Rect(center, new Size(0, 0)),
                    $"{phase}: render anchor {center} differs from model coordinate {expected}");
        }
    }

    private static Rect[] MakeActorZones(Size stage, ThemeProfile theme)
    {
        var zones = new List<Rect>();
        foreach (ActorProfile actor in theme.Actors)
        {
            double side = Math.Min(stage.Height * actor.Size, stage.Width * .32);
            var zone = new Rect(actor.CenterX * stage.Width - side / 2,
                actor.FootY * stage.Height - side, side, side);
            zone.Inflate(6, 8); // Actor contract padding; renderer adds its own 6-DIP margin.
            zones.Add(zone);
        }
        return zones.ToArray();
    }

    private static void Verify(EventPropsScene scene, Size stage, string theme, Rect[] actorZones, string phase)
    {
        try { scene.VerifyLayout(); }
        catch (Exception ex)
        {
            var boxes = scene.GetPlacementBounds();
            bool foundPair = false;
            for (int i = 0; i < boxes.Count; i++)
            for (int j = i + 1; j < boxes.Count; j++)
            {
                if (!boxes[i].Bounds.IntersectsWith(boxes[j].Bounds)) continue;
                foundPair = true;
                Record($"theme={theme} stage={stage.Width:0}x{stage.Height:0} phase={phase} prop={boxes[i].Kind}/{boxes[j].Kind} bounds={boxes[i].Bounds}/{boxes[j].Bounds}: {ex.Message}");
            }
            if (ex.Message.Contains("Pokémon oder Text", StringComparison.Ordinal))
            {
                foreach (var item in boxes)
                foreach (Rect actorZone in actorZones)
                {
                    actorZone.Inflate(6, 6); // Match EventPropsScene.SetExclusionZones' renderer margin.
                    if (actorZone.IntersectsWith(item.Bounds))
                    {
                        foundPair = true;
                        Record($"theme={theme} stage={stage.Width:0}x{stage.Height:0} phase={phase} prop={item.Kind} bounds={item.Bounds} exclusion={actorZone}: {ex.Message}");
                    }
                }
            }
            if (!foundPair) Record($"theme={theme} stage={stage.Width:0}x{stage.Height:0} phase={phase}: {ex.Message}");
        }
    }

    private static void VerifyBounds(PropKind kind, Rect bounds, Rect[] actorZones, Size stage, string theme, string phase)
    {
        Rect available = new(0, 0, stage.Width, stage.Height);
        if (!available.Contains(bounds))
            RecordFailure(theme, stage, kind.ToString(), bounds, $"{phase}: bounds extend outside stage");
        foreach (Rect zone in actorZones)
        {
            zone.Inflate(6, 6); // Match the additional margin applied by SetExclusionZones.
            if (zone.IntersectsWith(bounds))
                RecordFailure(theme, stage, kind.ToString(), bounds, $"{phase}: intersects actor exclusion {zone}");
        }
    }

    private static bool SameBounds(IReadOnlyList<(int Id, PropKind Kind, Rect Bounds)> a,
        IReadOnlyList<(int Id, PropKind Kind, Rect Bounds)> b)
    {
        if (a.Count != b.Count) return false;
        for (int i = 0; i < a.Count; i++)
            if (a[i].Id != b[i].Id || a[i].Kind != b[i].Kind || a[i].Bounds != b[i].Bounds) return false;
        return true;
    }

    private static bool ApproximatelySameBounds(IReadOnlyList<(int Id, PropKind Kind, Rect Bounds)> a,
        IReadOnlyList<(int Id, PropKind Kind, Rect Bounds)> b)
    {
        if (a.Count != b.Count) return false;
        for (int i = 0; i < a.Count; i++)
        {
            Rect x = a[i].Bounds, y = b[i].Bounds;
            if (a[i].Id != b[i].Id || a[i].Kind != b[i].Kind || Math.Abs(x.X-y.X) > 1e-7 ||
                Math.Abs(x.Y-y.Y) > 1e-7 || Math.Abs(x.Width-y.Width) > 1e-7 || Math.Abs(x.Height-y.Height) > 1e-7) return false;
        }
        return true;
    }

    private static void RecordFailure(string theme, Size stage, string kind, Rect bounds, string details) =>
        Record($"theme={theme} stage={stage.Width:0}x{stage.Height:0} prop={kind} bounds={bounds}: {details}");

    private static void Record(string failure)
    {
        if (FailureSet.Add(failure)) Failures.Add(failure);
    }
}
