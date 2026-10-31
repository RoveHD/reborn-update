using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using PokemonRebornLauncher.Services;

if (args.Length == 3 && args[0] == "--release-check")
{
    await ReleaseCheck.Run(args[1], args[2]);
    return;
}

var tests = new List<(string, Func<Task>)>();
void Add(string name, Func<Fixture, Task> test) => tests.Add((name, async () => { using var fixture = new Fixture(); await test(fixture); }));
void Check(bool condition, string message = "Assertion failed") { if (!condition) throw new Exception(message); }
async Task Reject<T>(Func<Task> action) where T : Exception
{ try { await action(); } catch (T) { return; } throw new Exception("Expected " + typeof(T).Name); }
void Unchanged(Fixture f) { Check(f.Read(Fixture.Mod) == "old mod" && f.Read("version.txt") == "7.3.1"); }

Add("actual UpdateService selects small exact-base delta, preserves user extras and skips full removals", async f =>
{
    await f.Install();
    Check(f.Handler.Requests.SequenceEqual(new[] { "/manifest", "/delta" }));
    Check(f.Read(Fixture.Mod) == "fixed mod" && f.Read("version.txt") == "7.3.2");
    Check(f.Read("retired.txt") == "retired" && f.Read("Graphics/custom.png") == "custom shiny");
    Check(f.Read("Save Data/Game.rxdata") == "save" && f.Read("mkxp.json") == "custom settings");
    Check(f.Progress.Any(p => p.Stage == UpdateStage.Downloading && p.TotalBytes == f.Delta.Length && p.Message.Contains("Hotfix")));
    Check(f.DeltaManifest.RequiredSpace(f.Root) < f.Manifest.Game.Size * 3);
});
Add("delta update supports version switching in both directions", async f =>
{
    await f.Install();
    using var service = f.Service();
    await service.SwitchVersionAsync(f.Settings, new InlineProgress(_ => { }), CancellationToken.None);
    Check(f.Read(Fixture.Mod) == "old mod" && f.Read("version.txt") == "7.3.1");
    Check(service.GetVersionSelection(f.Settings).IsPreviousActive);
    await service.SwitchVersionAsync(f.Settings, new InlineProgress(_ => { }), CancellationToken.None);
    Check(f.Read(Fixture.Mod) == "fixed mod" && f.Read("version.txt") == "7.3.2");
});
Add("hotfix updates directly from selected older version and replaces withdrawn snapshot", async f =>
{
    await f.Install();
    using var service = f.Service();
    await service.SwitchVersionAsync(f.Settings, new InlineProgress(_ => { }), CancellationToken.None);
    string withdrawnSnapshot = JsonSerializer.Deserialize<GameInstaller.VersionState>(
        File.ReadAllText(Path.Combine(f.Root, ".reborn-updater/versions.json")))!.Snapshot;
    Check(service.GetVersionSelection(f.Settings) == new VersionSelection("7.3.1", "7.3.2", true));

    f.Handler.Requests.Clear();
    await f.Install();

    Check(f.Handler.Requests.SequenceEqual(new[] { "/manifest", "/delta" }));
    Check(f.Read(Fixture.Mod) == "fixed mod" && f.Read("version.txt") == "7.3.2");
    Check(service.GetVersionSelection(f.Settings) == new VersionSelection("7.3.2", "7.3.1", false));
    Check(!Directory.Exists(Path.Combine(f.Root, ".reborn-updater", withdrawnSnapshot)));
    Check(Directory.GetDirectories(Path.Combine(f.Root, ".reborn-updater"), "game-*").Length == 1);
    Check(f.Read("Save Data/Game.rxdata") == "save" && f.Read("mkxp.json") == "custom settings");
});
Add("full-package update works from selected older version", async f =>
{
    await f.Install();
    using var service = f.Service();
    await service.SwitchVersionAsync(f.Settings, new InlineProgress(_ => { }), CancellationToken.None);
    Check(service.GetVersionSelection(f.Settings).IsPreviousActive);

    f.Manifest.GameHotfixes.Clear();
    f.Manifest.GameHotfixesV2.Clear();
    f.Handler.Requests.Clear();
    await f.Install();

    Check(f.Handler.Requests.SequenceEqual(new[] { "/manifest", "/full" }));
    Check(f.Read(Fixture.Mod) == "fixed mod" && f.Read("version.txt") == "7.3.2");
    Check(service.GetVersionSelection(f.Settings) == new VersionSelection("7.3.2", "7.3.1", false));
    Check(f.Read("Graphics/custom.png") == "custom shiny");
    Check(f.Read("Save Data/Game.rxdata") == "save" && f.Read("mkxp.json") == "custom settings");
});
Add("custom base hash stops without overwriting personal bytes through full fallback", async f =>
{
    f.Write(Fixture.Mod, "custom mod"); await Reject<InvalidOperationException>(() => f.Install());
    Check(f.Handler.Requests.SequenceEqual(new[] { "/manifest" }));
    Check(f.Read(Fixture.Mod) == "custom mod" && f.Read("version.txt") == "7.3.1" && f.Read("retired.txt") == "retired");
});
Add("missing replacement base stops without full fallback", async f =>
{
    File.Delete(Path.Combine(f.Root, Fixture.Mod)); await Reject<InvalidOperationException>(() => f.Install());
    Check(f.Handler.Requests.SequenceEqual(new[] { "/manifest" }));
    Check(!File.Exists(Path.Combine(f.Root, Fixture.Mod)) && f.Read("version.txt") == "7.3.1");
});
Add("another version selects full package", async f =>
{
    f.Write("version.txt", "7.3.0"); await f.Install();
    Check(f.Handler.Requests.Contains("/full") && !f.Handler.Requests.Contains("/delta"));
});
Add("same version repair selects full package", async f =>
{
    f.Write("version.txt", "7.3.2"); await f.Install();
    Check(f.Handler.Requests.Contains("/full") && !f.Handler.Requests.Contains("/delta"));
});
Add("legacy manifest without hotfix array retains full install", async f =>
{
    f.Manifest.GameHotfixes.Clear(); f.Handler.OmitHotfixes = true; await f.Install();
    Check(f.Handler.Requests.Contains("/full"));
});
Add("archive hash failure stops without silent full download", async f =>
{
    f.DeltaManifest.Sha256 = new string('0', 64);
    await Reject<InvalidDataException>(() => f.Install()); Unchanged(f);
    Check(!f.Handler.Requests.Contains("/full"));
});
Add("HTTP failure stops without silent full download", async f =>
{
    f.Handler.DeltaStatus = HttpStatusCode.ServiceUnavailable;
    await Reject<HttpRequestException>(() => f.Install()); Unchanged(f);
    Check(!f.Handler.Requests.Contains("/full"));
});
Add("valid archive hash with corrupt payload hash is rejected", async f =>
{
    f.SetDelta(Fixture.Zip((Fixture.Mod, "wrong mod")));
    await Reject<InvalidDataException>(() => f.Install()); Unchanged(f);
});
Add("undeclared extra file is rejected before any game write", async f =>
{
    f.SetDelta(Fixture.Zip((Fixture.Mod, "fixed mod"), ("extra.rb", "extra")));
    await Reject<InvalidDataException>(() => f.Install()); Unchanged(f);
    Check(!File.Exists(Path.Combine(f.Root, "extra.rb")));
});
Add("missing declared file is rejected", async f =>
{
    f.SetDelta(Fixture.Zip(("patch/", "")));
    await Reject<InvalidDataException>(() => f.Install()); Unchanged(f);
});
Add("duplicate archive paths are rejected case-insensitively", async f =>
{
    f.SetDelta(Fixture.Zip((Fixture.Mod, "fixed mod"), (Fixture.Mod.ToUpperInvariant(), "fixed mod")));
    await Reject<InvalidDataException>(() => f.Install()); Unchanged(f);
});
Add("traversal archive paths are rejected", async f =>
{
    f.SetDelta(Fixture.Zip((Fixture.Mod, "fixed mod"), ("../escape", "bad")));
    await Reject<InvalidDataException>(() => f.Install()); Unchanged(f);
});
Add("protected archive paths are rejected", async f =>
{
    f.SetDelta(Fixture.Zip((Fixture.Mod, "fixed mod"), ("Save Data/Game.rxdata", "bad")));
    await Reject<InvalidDataException>(() => f.Install()); Unchanged(f);
});
Add("wrapper directory is not accepted as a substitute for declared root", async f =>
{
    f.SetDelta(Fixture.Zip(("wrapper/" + Fixture.Mod, "fixed mod")));
    await Reject<InvalidDataException>(() => f.Install()); Unchanged(f);
});
Add("link archive entry rejected", async f =>
{
    using var stream = new MemoryStream();
    using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, true))
    {
        var entry = zip.CreateEntry(Fixture.Mod); entry.ExternalAttributes = unchecked((int)0xA0000000);
        using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false)); writer.Write("fixed mod");
    }
    f.SetDelta(stream.ToArray()); await Reject<InvalidDataException>(() => f.Install()); Unchanged(f);
});
Add("base hash is rechecked immediately before mutation", async f =>
{
    f.Callback = p => { if (p.Stage == UpdateStage.Installing && p.Message.Contains("gesichert")) f.Write(Fixture.Mod, "changed during backup"); };
    await Reject<InvalidOperationException>(() => f.Install());
    Check(f.Read(Fixture.Mod) == "changed during backup" && f.Read("version.txt") == "7.3.1");
});
Add("base version is rechecked before mutation", async f =>
{
    f.Handler.BeforeDelta = () => f.Write("version.txt", "7.3.0");
    await Reject<InvalidOperationException>(() => f.Install());
    Check(f.Read(Fixture.Mod) == "old mod" && f.Read("version.txt") == "7.3.0");
});
Add("installer rolls back delta bytes and version after write failure", async f =>
{
    f.Callback = p => { if (p.Message.Contains("Installiere patch/Mods")) throw new IOException("injected write failure"); };
    await Reject<IOException>(() => f.Install()); Unchanged(f);
    Check(!File.Exists(Path.Combine(f.Root, ".reborn-updater/game-transaction.json")));
});
Add("invalid hotfix declarations reject before network package request", async f =>
{
    foreach (Action<GameHotfix> corrupt in new Action<GameHotfix>[]
    {
        h => h.Version = "7.3.3", h => h.FromVersion = h.Version,
        h => h.Files[0].Path = "../outside", h => h.Files[0].Path = "mkxp.json",
        h => h.Files[0].BeforeSha256 = "invalid", h => h.Files[0].Sha256 = "invalid",
        h => h.Files[0].Size = -1, h => h.Files.Add(h.Files[0]), h => h.Files.Clear()
    })
    {
        var h = Fixture.Hotfix(f.Delta); corrupt(h); f.Manifest.GameHotfixes = [h];
        await Reject<InvalidDataException>(() => f.Install()); Unchanged(f);
    }
    Check(f.Handler.Requests.All(p => p == "/manifest"));
});
Add("versions file cannot be declared as a hotfix payload", async f =>
{
    f.DeltaManifest.Files[0].Path = "version.txt";
    await Reject<InvalidDataException>(() => f.Install()); Unchanged(f);
});
Add("cancelled delta leaves game and local version unchanged", async f =>
{
    using var cancellation = new CancellationTokenSource();
    f.Handler.BeforeDelta = cancellation.Cancel;
    await Reject<OperationCanceledException>(() => f.Install(cancellation.Token)); Unchanged(f);
});

Add("mixed delta adds replaces and removes with repeated bidirectional switching", async f =>
{
    f.MixedDelta();
    Check(f.DeltaManifest.RequiredSpace(f.Root) < f.Manifest.Game.Size * 3);
    await f.Install();
    Check(f.Handler.Requests.SequenceEqual(new[] { "/manifest", "/delta" }));
    using var service = f.Service();
    for (int i = 0; i < 5; i++)
    {
        bool current = i % 2 == 0;
        Check(f.Read(Fixture.Mod) == (current ? "fixed mod" : "old mod"));
        Check(f.Read("version.txt") == (current ? "7.3.2" : "7.3.1"));
        Check(File.Exists(Path.Combine(f.Root, Fixture.Added)) == current);
        if (current) Check(f.Read(Fixture.Added) == "new mod");
        Check(File.Exists(Path.Combine(f.Root, "retired.txt")) != current);
        if (!current) Check(f.Read("retired.txt") == "retired");
        Check(f.Read("Graphics/custom.png") == "custom shiny" && f.Read("Save Data/Game.rxdata") == "save" && f.Read("mkxp.json") == "custom settings");
        if (i < 4) await service.SwitchVersionAsync(f.Settings, new InlineProgress(_ => { }), CancellationToken.None);
    }
});
Add("addition-only delta uses small package and restores absence", async f =>
{
    f.MixedDelta();
    f.DeltaManifest.Files.RemoveAll(file => file.Path != Fixture.Added);
    f.SetDelta(Fixture.Zip((Fixture.Added, "new mod")));
    await f.Install();
    Check(f.Read(Fixture.Added) == "new mod" && f.Read(Fixture.Mod) == "old mod" && f.Read("retired.txt") == "retired");
    using var service = f.Service();
    await service.SwitchVersionAsync(f.Settings, new InlineProgress(_ => { }), CancellationToken.None);
    Check(!File.Exists(Path.Combine(f.Root, Fixture.Added)) && f.Read("version.txt") == "7.3.1");
});
Add("deletion-only delta accepts empty ZIP and restores removed bytes", async f =>
{
    f.MixedDelta(); f.DeltaManifest.Files.RemoveAll(file => !file.Delete); f.SetDelta(Fixture.Zip());
    await f.Install();
    Check(!File.Exists(Path.Combine(f.Root, "retired.txt")) && f.Read(Fixture.Mod) == "old mod");
    using var service = f.Service();
    await service.SwitchVersionAsync(f.Settings, new InlineProgress(_ => { }), CancellationToken.None);
    Check(f.Read("retired.txt") == "retired" && f.Read("version.txt") == "7.3.1");
    await service.SwitchVersionAsync(f.Settings, new InlineProgress(_ => { }), CancellationToken.None);
    Check(!File.Exists(Path.Combine(f.Root, "retired.txt")) && f.Read("version.txt") == "7.3.2");
});
Add("new path occupied by personal or identical bytes is never overwritten", async f =>
{
    f.MixedDelta();
    foreach (string bytes in new[] { "personal mod", "new mod" })
    {
        f.Write(Fixture.Added, bytes); await Reject<InvalidOperationException>(() => f.Install());
        Check(f.Read(Fixture.Added) == bytes); Unchanged(f);
    }
    Check(f.Handler.Requests.All(path => path == "/manifest"));
});
Add("new path occupied by directory or parent file is rejected", async f =>
{
    f.MixedDelta(); Directory.CreateDirectory(Path.Combine(f.Root, Fixture.Added));
    await Reject<InvalidOperationException>(() => f.Install()); Unchanged(f);
    f.DeltaManifest.Files.Single(file => !file.BeforeExists).Path = "personal-parent/new.rb";
    f.Write("personal-parent", "personal parent file");
    await Reject<InvalidOperationException>(() => f.Install()); Unchanged(f);
    Check(f.Read("retired.txt") == "retired" && f.Handler.Requests.All(path => path == "/manifest"));
});
Add("changed or missing removal base is rejected without full download", async f =>
{
    f.MixedDelta(); f.Write("retired.txt", "personal bytes");
    await Reject<InvalidOperationException>(() => f.Install()); Unchanged(f);
    Check(f.Read("retired.txt") == "personal bytes");
    File.Delete(Path.Combine(f.Root, "retired.txt"));
    await Reject<InvalidOperationException>(() => f.Install()); Unchanged(f);
    Check(f.Handler.Requests.All(path => path == "/manifest"));
});
Add("delete paths cannot carry archive payload", async f =>
{
    f.MixedDelta(); f.SetDelta(Fixture.Zip((Fixture.Mod, "fixed mod"), (Fixture.Added, "new mod"), ("retired.txt", "retired")));
    await Reject<InvalidDataException>(() => f.Install()); Unchanged(f);
    Check(f.Read("retired.txt") == "retired" && !File.Exists(Path.Combine(f.Root, Fixture.Added)));
});
Add("contradictory add remove declarations fail validation", async f =>
{
    foreach (Action<GameHotfix> corrupt in new Action<GameHotfix>[]
    {
        h => h.Files[0].BeforeExists = false,
        h => h.Files[0].Delete = true,
        h => { h.Files[0].Delete = true; h.Files[0].Sha256 = ""; h.Files[0].Size = 1; },
        h => { h.Files[0].BeforeExists = false; h.Files[0].BeforeSha256 = ""; h.Files[0].Delete = true; h.Files[0].Sha256 = ""; h.Files[0].Size = 0; },
        h => h.Files[0].BeforeSha256 = null!, h => h.Files[0].Sha256 = null!
    })
    {
        var hotfix = Fixture.Hotfix(f.Delta); corrupt(hotfix); f.Manifest.GameHotfixes = [hotfix];
        await Reject<InvalidDataException>(() => f.Install()); Unchanged(f);
    }
    Check(f.Handler.Requests.All(path => path == "/manifest"));
});
Add("addition absence is rechecked after download and backup", async f =>
{
    f.MixedDelta();
    f.Callback = p => { if (p.Message.Contains("gesichert")) f.Write(Fixture.Added, "concurrent personal file"); };
    await Reject<InvalidOperationException>(() => f.Install()); Unchanged(f);
    Check(f.Read(Fixture.Added) == "concurrent personal file" && f.Read("retired.txt") == "retired");
});
Add("removal hash is rechecked after backup", async f =>
{
    f.MixedDelta();
    f.Callback = p => { if (p.Message.Contains("gesichert")) f.Write("retired.txt", "concurrent personal file"); };
    await Reject<IOException>(() => f.Install()); Unchanged(f);
    Check(f.Read("retired.txt") == "concurrent personal file" && !File.Exists(Path.Combine(f.Root, Fixture.Added)));
});
Add("failed mixed delta rolls back additions removals replacements and version", async f =>
{
    f.MixedDelta();
    f.Callback = p => { if (p.Message.Contains("Installiere version.txt")) throw new IOException("after every write"); };
    await Reject<IOException>(() => f.Install()); Unchanged(f);
    Check(f.Read("retired.txt") == "retired" && !File.Exists(Path.Combine(f.Root, Fixture.Added)));
    Check(!File.Exists(Path.Combine(f.Root, ".reborn-updater/game-transaction.json")));
});
Add("switch rejects modified replacement or addition and unexpected restored removal", async f =>
{
    f.MixedDelta(); await f.Install(); using var service = f.Service();
    foreach (var (path, original) in new[] { (Fixture.Mod, "fixed mod"), (Fixture.Added, "new mod"), ("retired.txt", (string?)null) })
    {
        f.Write(path, "personal bytes");
        await Reject<InvalidOperationException>(() => service.SwitchVersionAsync(f.Settings, new InlineProgress(_ => { }), CancellationToken.None));
        Check(f.Read(path) == "personal bytes" && f.Read("version.txt") == "7.3.2");
        if (original is null) File.Delete(Path.Combine(f.Root, path)); else f.Write(path, original);
    }
    await service.SwitchVersionAsync(f.Settings, new InlineProgress(_ => { }), CancellationToken.None);
    f.Write(Fixture.Added, "personal bytes on previous version");
    await Reject<InvalidOperationException>(() => service.SwitchVersionAsync(f.Settings, new InlineProgress(_ => { }), CancellationToken.None));
    Check(f.Read(Fixture.Added) == "personal bytes on previous version" && f.Read("version.txt") == "7.3.1" && f.Read("retired.txt") == "retired");
});
Add("switch rechecks all active states immediately before writes", async f =>
{
    f.MixedDelta(); await f.Install(); var installer = new GameInstaller(f.Root);
    foreach (string path in new[] { Fixture.Mod, Fixture.Added, "retired.txt" })
    {
        int writes = 0;
        await Reject<IOException>(() => Task.Run(() => installer.SwitchVersion(f.Settings.ResolveLocalVersionPath(), new InlineProgress(_ => { }), CancellationToken.None,
            afterWrite: _ => writes++, beforeMutation: () => f.Write(path, "changed during backup"))));
        Check(writes == 0 && f.Read(path) == "changed during backup" && f.Read("version.txt") == "7.3.2");
        if (path == "retired.txt") File.Delete(Path.Combine(f.Root, path)); else f.Write(path, path == Fixture.Mod ? "fixed mod" : "new mod");
    }
});
Add("failed switch restores all three operation kinds and remains retryable", async f =>
{
    f.MixedDelta(); await f.Install(); var installer = new GameInstaller(f.Root);
    await Reject<IOException>(() => Task.Run(() => installer.SwitchVersion(f.Settings.ResolveLocalVersionPath(), new InlineProgress(_ => { }), CancellationToken.None,
        afterWrite: index => { if (index == 3) throw new IOException("after version write"); })));
    Check(f.Read(Fixture.Mod) == "fixed mod" && f.Read(Fixture.Added) == "new mod" && !File.Exists(Path.Combine(f.Root, "retired.txt")) && f.Read("version.txt") == "7.3.2");
    installer.SwitchVersion(f.Settings.ResolveLocalVersionPath(), new InlineProgress(_ => { }), CancellationToken.None);
    Unchanged(f); Check(f.Read("retired.txt") == "retired" && !File.Exists(Path.Combine(f.Root, Fixture.Added)));
});
Add("V2 is preferred over legacy delta for the same base", async f =>
{
    f.MixedDelta(); f.Manifest.GameHotfixes.Add(Fixture.Hotfix(Fixture.Zip((Fixture.Mod, "fixed mod"))));
    await f.Install();
    Check(f.Read(Fixture.Added) == "new mod" && !File.Exists(Path.Combine(f.Root, "retired.txt")));
});
Add("legacy reader can ignore V2 and still parse the full-package manifest", f =>
{
    f.MixedDelta();
    var json = System.Text.Json.Nodes.JsonNode.Parse(JsonSerializer.Serialize(f.Manifest))!;
    json.AsObject().Remove("GameHotfixesV2");
    var legacy = UpdateManifest.Parse(json.ToJsonString());
    Check(legacy.GameHotfixes.Count == 0 && legacy.GameHotfixesV2.Count == 0 && legacy.Game.Url == f.Manifest.Game.Url);
    return Task.CompletedTask;
});
Add("extended operations are rejected in legacy list before any package request", async f =>
{
    f.MixedDelta(); f.Manifest.GameHotfixes = f.Manifest.GameHotfixesV2; f.Manifest.GameHotfixesV2 = [];
    await Reject<InvalidDataException>(() => f.Install()); Unchanged(f);
    Check(f.Handler.Requests.SequenceEqual(new[] { "/manifest" }));
});
Add("duplicate V2 bases and null V2 lists are rejected", async f =>
{
    f.MixedDelta(); f.Manifest.GameHotfixesV2.Add(f.DeltaManifest);
    await Reject<InvalidDataException>(() => f.Install()); Unchanged(f);
    f.Manifest.GameHotfixesV2 = null!;
    await Reject<InvalidDataException>(() => f.Install()); Unchanged(f);
});
Add("older snapshots without installed hashes remain switchable", async f =>
{
    f.MixedDelta(); await f.Install();
    string path = Path.Combine(f.Root, ".reborn-updater/versions.json");
    var json = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(path))!;
    foreach (var entry in json["Entries"]!.AsArray()) entry!.AsObject().Remove("InstalledSha256");
    File.WriteAllText(path, json.ToJsonString());
    using var service = f.Service();
    await service.SwitchVersionAsync(f.Settings, new InlineProgress(_ => { }), CancellationToken.None);
    Unchanged(f); Check(f.Read("retired.txt") == "retired" && !File.Exists(Path.Combine(f.Root, Fixture.Added)));
    await service.SwitchVersionAsync(f.Settings, new InlineProgress(_ => { }), CancellationToken.None);
    Check(f.Read(Fixture.Added) == "new mod" && !File.Exists(Path.Combine(f.Root, "retired.txt")));
});

int failures = 0;
foreach (var (name, test) in tests)
{
    try { await test(); Console.WriteLine("PASS " + name); }
    catch (Exception ex) { failures++; Console.WriteLine("FAIL " + name + "\n" + ex); }
}
Console.WriteLine($"{tests.Count - failures}/{tests.Count} hotfix checks passed");
Environment.ExitCode = failures == 0 ? 0 : 1;

sealed class Fixture : IDisposable
{
    public const string Mod = "patch/Mods/zzLegendaryCatchDifficulty.rb";
    public const string Added = "patch/Mods/zzAddedHotfix.rb";
    public string Root { get; } = Path.Combine(Path.GetTempPath(), "RebornHotfixTests", Guid.NewGuid().ToString("N"));
    public LauncherSettings Settings { get; }
    public UpdateManifest Manifest { get; }
    public GameHotfix DeltaManifest => Manifest.GameHotfixesV2.Concat(Manifest.GameHotfixes).First();
    public byte[] Delta { get; private set; }
    public byte[] Full { get; }
    public FakeHandler Handler { get; }
    public List<UpdateProgress> Progress { get; } = [];
    public Action<UpdateProgress>? Callback { get; set; }
    public Fixture()
    {
        Directory.CreateDirectory(Root);
        foreach (var (path, text) in new[] { ("Game.exe", "game"), ("Game.ini", "ini"), ("mkxp.json", "custom settings"),
            ("version.txt", "7.3.1"), (Mod, "old mod"), ("retired.txt", "retired"), ("Graphics/custom.png", "custom shiny"), ("Save Data/Game.rxdata", "save") }) Write(path, text);
        Settings = new() { InstallDirectory = Root, ManifestUrl = "https://test.invalid/manifest" };
        Delta = Zip((Mod, "fixed mod"));
        // An incompressible-enough fixture makes full-vs-delta size observable.
        Full = Zip(("Game.exe", "new game"), (Mod, "fixed mod"), ("Data/new.dat", Convert.ToBase64String(RandomNumberGenerator.GetBytes(512 * 1024))));
        Manifest = new() { SchemaVersion = 1, Game = new() { Version = "7.3.2", Url = "https://test.invalid/full", Size = Full.Length, Sha256 = Hash(Full),
            RemovedFiles = [new() { Path = "retired.txt", Sha256 = Hash("retired") }] },
            Launcher = new() { Version = "7.3.0", Url = "https://test.invalid/launcher", Size = 1, Sha256 = new string('a', 64) }, GameHotfixes = [Hotfix(Delta)] };
        Handler = new(this);
    }
    public static GameHotfix Hotfix(byte[] zip) => new() { FromVersion = "7.3.1", Version = "7.3.2", Url = "https://test.invalid/delta", Size = zip.Length, Sha256 = Hash(zip),
        Files = [new() { Path = Mod, BeforeSha256 = Hash("old mod"), Sha256 = Hash("fixed mod"), Size = Encoding.UTF8.GetByteCount("fixed mod") }] };
    public UpdateService Service() => new(new VerifiedDownload(Handler));
    public async Task Install(CancellationToken ct = default)
    {
        using var service = Service();
        await service.DownloadAndInstallAsync(Settings, "7.3.2", new InlineProgress(p => { Progress.Add(p); Callback?.Invoke(p); }), ct);
    }
    public void SetDelta(byte[] bytes) { Delta = bytes; DeltaManifest.Size = bytes.Length; DeltaManifest.Sha256 = Hash(bytes); }
    public void MixedDelta()
    {
        DeltaManifest.Files.Add(new() { Path = Added, BeforeExists = false, Sha256 = Hash("new mod"), Size = Encoding.UTF8.GetByteCount("new mod") });
        DeltaManifest.Files.Add(new() { Path = "retired.txt", Delete = true, BeforeSha256 = Hash("retired") });
        Manifest.GameHotfixesV2.Add(DeltaManifest); Manifest.GameHotfixes.Clear();
        SetDelta(Zip((Mod, "fixed mod"), (Added, "new mod")));
    }
    public void Write(string path, string text) { string target = Path.Combine(Root, path); Directory.CreateDirectory(Path.GetDirectoryName(target)!); File.WriteAllText(target, text); }
    public string Read(string path) => File.ReadAllText(Path.Combine(Root, path));
    public static string Hash(string value) => Hash(Encoding.UTF8.GetBytes(value));
    public static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
    public static byte[] Zip(params (string Path, string Text)[] entries)
    {
        using var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, true))
            foreach (var (path, text) in entries)
            { using var writer = new StreamWriter(zip.CreateEntry(path).Open(), new UTF8Encoding(false)); writer.Write(text); }
        return stream.ToArray();
    }
    public void Dispose() { if (Directory.Exists(Root)) Directory.Delete(Root, true); }
}
sealed class FakeHandler(Fixture fixture) : HttpMessageHandler
{
    public List<string> Requests { get; } = [];
    public HttpStatusCode DeltaStatus { get; set; } = HttpStatusCode.OK;
    public Action? BeforeDelta { get; set; }
    public bool OmitHotfixes { get; set; }
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        string path = request.RequestUri!.AbsolutePath; Requests.Add(path);
        if (path == "/delta") BeforeDelta?.Invoke();
        ct.ThrowIfCancellationRequested();
        byte[] bytes;
        if (path == "/manifest")
        {
            var node = System.Text.Json.Nodes.JsonNode.Parse(JsonSerializer.Serialize(fixture.Manifest))!;
            if (OmitHotfixes) node.AsObject().Remove("GameHotfixes");
            bytes = Encoding.UTF8.GetBytes(node.ToJsonString());
        }
        else bytes = path == "/delta" ? fixture.Delta : fixture.Full;
        return Task.FromResult(new HttpResponseMessage(path == "/delta" ? DeltaStatus : HttpStatusCode.OK) { Content = new ByteArrayContent(bytes), RequestMessage = request });
    }
    protected override void Dispose(bool disposing) { /* fixture reuses handler across update + version selection */ }
}
sealed class InlineProgress(Action<UpdateProgress> callback) : IProgress<UpdateProgress>
{ public void Report(UpdateProgress value) => callback(value); }

static class ReleaseCheck
{
    // Uses real release bytes, but only an isolated installation with the delta's
    // declared old files. Never executes Game.exe or touches the source install.
    public static async Task Run(string releaseDirectory, string baseDirectory)
    {
        string json = await File.ReadAllTextAsync(Path.Combine(releaseDirectory, "update.json"));
        var manifest = UpdateManifest.Parse(json);
        var hotfix = manifest.GameHotfixesV2.Concat(manifest.GameHotfixes).First();
        string zip = Path.Combine(releaseDirectory, Uri.UnescapeDataString(Path.GetFileName(new Uri(hotfix.Url).AbsolutePath)));
        if (new FileInfo(zip).Length != hotfix.Size || !VerifiedDownload.Hash(zip).Equals(hotfix.Sha256, StringComparison.OrdinalIgnoreCase))
            throw new Exception("Release delta ZIP mismatch");
        using var fixture = new Fixture();
        foreach (var file in hotfix.Files)
        {
            if (!file.BeforeExists) continue;
            string source = SafePaths.Under(Path.GetFullPath(baseDirectory), file.Path);
            if (!VerifiedDownload.Hash(source).Equals(file.BeforeSha256, StringComparison.OrdinalIgnoreCase)) throw new Exception("Release base mismatch: " + file.Path);
            string target = SafePaths.Under(fixture.Root, file.Path);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!); File.Copy(source, target, true);
        }
        fixture.Write("version.txt", hotfix.FromVersion);
        using var handler = new ReleaseHandler(json, hotfix.Url, zip);
        using var service = new UpdateService(new VerifiedDownload(handler));
        var messages = new List<UpdateProgress>();
        var progress = new InlineProgress(messages.Add);
        await service.DownloadAndInstallAsync(fixture.Settings, manifest.Game.Version, progress, CancellationToken.None);
        Verify(hotfix.Version, false);
        if (handler.PackageRequests != 1 || !messages.Any(p => p.Message.Contains("Hotfix") && p.TotalBytes == hotfix.Size))
            throw new Exception("Release delta selection/progress mismatch");
        await service.SwitchVersionAsync(fixture.Settings, progress, CancellationToken.None);
        Verify(hotfix.FromVersion, true);
        await service.SwitchVersionAsync(fixture.Settings, progress, CancellationToken.None);
        Verify(hotfix.Version, false);
        Console.WriteLine($"RELEASE_HOTFIX_OK {hotfix.FromVersion} -> {hotfix.Version}; {hotfix.Files.Count} files; {hotfix.Size} download bytes; install + switch back + switch forward; user files preserved");
        void Verify(string version, bool before)
        {
            if (fixture.Read("version.txt") != version) throw new Exception("Release version mismatch");
            foreach (var file in hotfix.Files)
            {
                string path = SafePaths.Under(fixture.Root, file.Path);
                bool expectedExists = before ? file.BeforeExists : !file.Delete;
                if (File.Exists(path) != expectedExists || (expectedExists && !VerifiedDownload.Hash(path).Equals(before ? file.BeforeSha256 : file.Sha256, StringComparison.OrdinalIgnoreCase)))
                    throw new Exception("Release installed file mismatch: " + file.Path);
            }
            if (fixture.Read("Graphics/custom.png") != "custom shiny" || fixture.Read("Save Data/Game.rxdata") != "save" || fixture.Read("mkxp.json") != "custom settings")
                throw new Exception("Release user data changed");
        }
    }
    private sealed class ReleaseHandler(string json, string deltaUrl, string zip) : HttpMessageHandler
    {
        public int PackageRequests { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            HttpContent content;
            if (request.RequestUri!.AbsoluteUri == deltaUrl) { PackageRequests++; content = new StreamContent(File.OpenRead(zip)); }
            else if (request.RequestUri.AbsolutePath == "/manifest") content = new StringContent(json);
            else throw new Exception("Full download was unexpectedly selected: " + request.RequestUri);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content, RequestMessage = request });
        }
    }
}
