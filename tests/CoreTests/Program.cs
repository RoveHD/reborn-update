using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using PokemonRebornLauncher.Services;

if (args.Length == 2 && args[0] == "--reject-reparse")
{
    try { SafePaths.Under(args[1], "Data/escape.dat"); Environment.ExitCode = 1; }
    catch (InvalidDataException) { Console.WriteLine("PASS native Windows junction rejected"); }
    return;
}
if (args.Length == 3 && args[0] == "--interrupt-game")
{
    var installer = new GameInstaller(args[1]);
    using var installLock = installer.AcquireLock();
    installer.Install(args[2], Path.Combine(args[1], "version.txt"), "7.0.0", [], new Progress<UpdateProgress>(), CancellationToken.None, i => { if (i == 0) Environment.Exit(91); });
    return;
}
if (args.Length == 2 && args[0] == "--interrupt-switch")
{
    var installer = new GameInstaller(args[1]);
    using var installLock = installer.AcquireLock();
    installer.SwitchVersion(Path.Combine(args[1], "version.txt"), new Progress<UpdateProgress>(), CancellationToken.None,
        i => { if (i == 0) Environment.Exit(92); });
    return;
}
var tests = new List<(string Name, Func<Task> Run)>();
void Add(string name, Action action) => tests.Add((name, () => { action(); return Task.CompletedTask; }));
void Async(string name, Func<Task> action) => tests.Add((name, action));
void Check(bool condition, string message = "Assertion failed") { if (!condition) throw new Exception(message); }
void Throws<T>(Action action) where T : Exception { try { action(); } catch (T) { return; } throw new Exception("Expected " + typeof(T).Name); }
async Task ThrowsAsync<T>(Func<Task> action) where T : Exception { try { await action(); } catch (T) { return; } throw new Exception("Expected " + typeof(T).Name); }
var progress = new Progress<UpdateProgress>();

Add("SemVer ordering, prerelease, metadata, downgrade guard", () =>
{
    Check(UpdateService.IsDifferentVersion("6.9.9", "7.0.0"));
    Check(UpdateService.IsDifferentVersion("7.0.0-rc.2", "7.0.0-rc.10"));
    Check(UpdateService.IsDifferentVersion("7.0.0-rc.10", "7.0.0"));
    Check(!UpdateService.IsDifferentVersion("7.0.0", "6.99.0"));
    Check(!UpdateService.IsDifferentVersion("v7.0.0+build1", "7.0.0+build2"));
    Throws<InvalidDataException>(() => ReleaseVersion.Parse("latest"));
});
Add("Snake case migration, modern keys win, unknown settings retained", () =>
{
    var old = LauncherSettings.Parse("""{"auto_update":false,"install_directory":"D:/Reborn","favorite_theme":"purple"}""");
    Check(!old.AutoUpdate && old.InstallDirectory == "D:/Reborn");
    Check(old.AdditionalSettings!["favorite_theme"].GetString() == "purple");
    Check(!old.AdditionalSettings.ContainsKey("auto_update"));
    Check(LauncherSettings.Parse("""{"auto_update":false,"autoUpdate":true}""").AutoUpdate);
});
Add("Windows paths block traversal, ADS, reserved names and ambiguity", () =>
{
    foreach (var path in new[] { "../escape", "C:/file", "/root", "folder/file:stream", "a/../b", "a//b", "a./b", "a /b", "CON.txt", "foo/LPT1", "foo/./bar", "a\0b" })
        Throws<InvalidDataException>(() => SafePaths.ValidateRelative(path));
    Check(SafePaths.ValidateRelative("Data\\german.dat") == "Data/german.dat");
});
foreach (var entries in new[] { new[] { "../escape" }, new[] { "Data/a", "data/A" }, new[] { "Data", "Data/a" }, new[] { "Data/a:stream" }, new[] { "Data/a." } })
{
    var captured = entries;
    Add("ZIP rejects " + string.Join(",", captured), () =>
    {
        using var f = new Fixture();
        Throws<InvalidDataException>(() => SafePaths.Extract(f.Zip(captured), Path.Combine(f.Root, "unpack"), progress, CancellationToken.None));
        Check(!Directory.Exists(Path.Combine(f.Root, "unpack")), "Validate entire archive before writing");
    });
}
Add("ZIP rejects Unix symbolic links", () =>
{
    using var f = new Fixture(); string path = Path.Combine(f.Root, "bad.zip");
    using (var zip = ZipFile.Open(path, ZipArchiveMode.Create)) { var entry = zip.CreateEntry("link"); entry.ExternalAttributes = unchecked((int)0xA1FF0000); }
    Throws<InvalidDataException>(() => SafePaths.Extract(path, Path.Combine(f.Root, "out"), progress, CancellationToken.None));
});
Add("Saves and configs protected, engine ini installable", () =>
{
    foreach (string path in new[] { "Save Data/Game_3.rxdata", "save data/a.txt", "Saves/a.sav", "settings.json", "mkxp.json", "mkxp.conf", "Game_3.rxdata", ".reborn-updater/journal", "launcher.exe.previous" }) Check(SafePaths.IsProtected(path), path);
    Check(!SafePaths.IsProtected("Data/Map001.rxdata")); Check(!SafePaths.IsProtected("Game.ini"));
});
Add("Install preserves unknown files/configs and commits version last", () =>
{
    using var f = new Fixture(); string root = f.Dir("game"), payload = f.Dir("payload");
    f.Write(root, "Data/a.dat", "old"); f.Write(root, "Save Data/Game_3.rxdata", "save"); f.Write(root, "mkxp.json", "my config"); f.Write(root, "my_mod.rb", "user"); f.Write(root, "version.txt", "6.1.0");
    f.Write(payload, "Data/a.dat", "new"); f.Write(payload, "Data/new.dat", "new"); f.Write(payload, "Save Data/Game_3.rxdata", "foreign save"); f.Write(payload, "mkxp.json", "foreign config"); f.Write(payload, "version.txt", "wrong");
    var installer = new GameInstaller(root); using var guard = installer.AcquireLock();
    installer.Install(payload, Path.Combine(root, "version.txt"), "7.0.0", [], progress, CancellationToken.None, i => Check(f.Read(root, "version.txt") == (i == 2 ? "7.0.0" : "6.1.0")));
    Check(f.Read(root, "Data/a.dat") == "new" && f.Read(root, "version.txt") == "7.0.0");
    Check(f.Read(root, "Save Data/Game_3.rxdata") == "save" && f.Read(root, "mkxp.json") == "my config" && f.Read(root, "my_mod.rb") == "user");
});
Add("Repair skips unchanged readonly files and preserves their timestamp while repairing modified bytes", () =>
{
    using var f = new Fixture(); string root = f.Dir("game"), payload = f.Dir("payload");
    string unchanged = f.Write(root, "same.dat", "same bytes"); f.Write(payload, "same.dat", "same bytes");
    f.Write(root, "changed.dat", "bad bytes"); f.Write(payload, "changed.dat", "new bytes");
    string versionPath = f.Write(root, "version.txt", "7.0.0");
    var timestamp = new DateTime(2001, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    File.SetLastWriteTimeUtc(unchanged, timestamp); File.SetLastWriteTimeUtc(versionPath, timestamp);
    File.SetAttributes(unchanged, FileAttributes.ReadOnly);
    try
    {
        var installer = new GameInstaller(root); using var guard = installer.AcquireLock(); int writes = 0;
        var stages = new List<UpdateProgress>();
        installer.Install(payload, versionPath, "7.0.0", [], new InlineProgress(p => stages.Add(p)), CancellationToken.None, _ => writes++);
        Check(writes == 1, "Only modified content should be written");
        Check(f.Read(root, "changed.dat") == "new bytes");
        Check(File.GetLastWriteTimeUtc(unchanged) == timestamp && File.GetLastWriteTimeUtc(versionPath) == timestamp);
        Check((File.GetAttributes(unchanged) & FileAttributes.ReadOnly) != 0);
        Check(stages.Any(p => p.Stage == UpdateStage.Checking && p.Percent == 100));
    }
    finally { File.SetAttributes(unchanged, FileAttributes.Normal); }
});
Add("Fully unchanged repair succeeds without mutations or an empty-payload error", () =>
{
    using var f = new Fixture(); string root = f.Dir("game"), payload = f.Dir("payload");
    f.Write(root, "Data/a.dat", "identical"); f.Write(payload, "Data/a.dat", "identical"); f.Write(root, "version.txt", "7.0.0");
    var installer = new GameInstaller(root); using var guard = installer.AcquireLock(); int writes = 0;
    installer.Install(payload, Path.Combine(root, "version.txt"), "7.0.0", [], progress, CancellationToken.None, _ => writes++);
    Check(writes == 0 && Directory.GetDirectories(Path.Combine(root, ".reborn-updater"), "game-*").Length == 0);
    Check(!File.Exists(Path.Combine(root, ".reborn-updater/game-transaction.json")));
});
Add("Repair refuses to downgrade a newer installed version before any mutation", () =>
{
    using var f = new Fixture(); string root = f.Dir("game"), payload = f.Dir("payload");
    f.Write(root, "a.dat", "newer build"); f.Write(payload, "a.dat", "older build"); f.Write(root, "version.txt", "8.0.0");
    var installer = new GameInstaller(root); using var guard = installer.AcquireLock();
    Throws<InvalidOperationException>(() => installer.Install(payload, Path.Combine(root, "version.txt"), "7.0.0", [], progress, CancellationToken.None));
    Check(f.Read(root, "a.dat") == "newer build" && f.Read(root, "version.txt") == "8.0.0");
});
Add("Write failure restores replaced/deleted files and removes additions", () =>
{
    using var f = new Fixture(); string root = f.Dir("game"), payload = f.Dir("payload");
    f.Write(root, "a.dat", "original"); f.Write(root, "version.txt", "6.1.0"); f.Write(root, "retired.dat", "retired");
    f.Write(payload, "a.dat", "new"); f.Write(payload, "b.dat", "added");
    var installer = new GameInstaller(root); using var guard = installer.AcquireLock();
    Throws<IOException>(() => installer.Install(payload, Path.Combine(root, "version.txt"), "7.0.0", [new() { Path = "retired.dat", Sha256 = VerifiedDownload.Hash(Path.Combine(root, "retired.dat")), ReplacedBy = "b.dat" }], progress, CancellationToken.None, i => { if (i == 2) throw new IOException("simulated disk failure"); }));
    Check(f.Read(root, "a.dat") == "original" && !File.Exists(Path.Combine(root, "b.dat")) && f.Read(root, "retired.dat") == "retired" && f.Read(root, "version.txt") == "6.1.0");
});
Add("Historical removal hashes migrate legacy mod names and survive repeated version switches", () =>
{
    foreach (string historical in new[] { "historical mod v1", "historical mod v2" })
    {
        using var f = new Fixture(); string root = f.Dir("game"), payload = f.Dir("payload"), history = f.Dir("history");
        string legacy = "patch/Mods/Bag.rb", renamed = "patch/Mods/RE - Beutel-Sortierung-und-TM-Infos.rb";
        f.Write(root, legacy, historical); f.Write(root, "version.txt", "6.1.0");
        f.Write(root, "patch/Mods/FriendsOwnMod.rb", "custom mod");
        f.Write(root, "patch/Mods/zzLocalGermanText.rb", "customized legacy mod");
        f.Write(payload, renamed, "new bag mod");
        f.Write(payload, "patch/Mods/RE - Deutsche-Zusatztexte.rb", "new language mod");
        f.Write(history, "v1", "historical mod v1"); f.Write(history, "v2", "historical mod v2");
        var removals = new List<RemovedFile>
        {
            new() { Path = legacy, Sha256 = VerifiedDownload.Hash(Path.Combine(history, "v1")), ReplacedBy = renamed },
            new() { Path = "PATCH\\Mods\\BAG.rb", Sha256 = VerifiedDownload.Hash(Path.Combine(history, "v2")).ToUpperInvariant(), ReplacedBy = renamed },
            new() { Path = legacy, Sha256 = VerifiedDownload.Hash(Path.Combine(history, "v1")), ReplacedBy = renamed },
            new() { Path = "patch/Mods/zzLocalGermanText.rb", Sha256 = new string('0', 64) }
        };
        var installer = new GameInstaller(root); using var guard = installer.AcquireLock();
        installer.Install(payload, Path.Combine(root, "version.txt"), "7.0.0", removals, progress, CancellationToken.None);
        Check(!File.Exists(Path.Combine(root, legacy)) && f.Read(root, renamed) == "new bag mod");
        for (int i = 0; i < 4; i++)
        {
            installer.SwitchVersion(Path.Combine(root, "version.txt"), progress, CancellationToken.None);
            bool previous = i % 2 == 0;
            Check(File.Exists(Path.Combine(root, legacy)) == previous && File.Exists(Path.Combine(root, renamed)) != previous);
            if (previous) Check(f.Read(root, legacy) == historical);
            Check(f.Read(root, "version.txt") == (previous ? "6.1.0" : "7.0.0"));
            Check(f.Read(root, "patch/Mods/FriendsOwnMod.rb") == "custom mod");
            Check(f.Read(root, "patch/Mods/zzLocalGermanText.rb") == "customized legacy mod");
        }
        Check(removals[1].Path == "PATCH\\Mods\\BAG.rb", "Installer must not mutate manifest paths");
        var pair = System.Text.Json.JsonSerializer.Deserialize<GameInstaller.VersionState>(f.Read(root, ".reborn-updater/versions.json"))!;
        Check(pair.Entries.Count(e => e.Path.Equals(legacy, StringComparison.OrdinalIgnoreCase)) == 1);
    }
});
Add("Unknown renamed legacy mods stop migration before any game mutation", () =>
{
    using var f = new Fixture(); string root = f.Dir("game"), payload = f.Dir("payload");
    string legacy = "patch/Mods/Bag.rb", renamed = "patch/Mods/RE - Beutel-Sortierung-und-TM-Infos.rb";
    f.Write(root, legacy, "my own modified bag mod"); f.Write(root, "Data/a.dat", "old game");
    f.Write(root, "version.txt", "6.1.0"); f.Write(payload, renamed, "new bag mod"); f.Write(payload, "Data/a.dat", "new game");
    var installer = new GameInstaller(root); using var guard = installer.AcquireLock(); int writes = 0;
    try
    {
        installer.Install(payload, Path.Combine(root, "version.txt"), "7.0.0",
            [new() { Path = legacy, Sha256 = new string('0', 64), ReplacedBy = renamed },
             new() { Path = legacy, Sha256 = new string('1', 64), ReplacedBy = renamed }], progress, CancellationToken.None, _ => writes++);
        throw new Exception("Expected conflicting modified legacy mod to stop migration");
    }
    catch (InvalidDataException ex) { Check(ex.Message.Contains(legacy) && ex.Message.Contains(renamed)); }
    Check(writes == 0 && f.Read(root, legacy) == "my own modified bag mod" && f.Read(root, "Data/a.dat") == "old game");
    Check(f.Read(root, "version.txt") == "6.1.0" && !File.Exists(Path.Combine(root, renamed)));
    Check(!File.Exists(Path.Combine(root, ".reborn-updater/game-transaction.json")) && Directory.GetDirectories(Path.Combine(root, ".reborn-updater"), "game-*").Length == 0);
});
Add("Replacement metadata rejects unsafe missing and inconsistent replacement paths before mutation", () =>
{
    foreach (string? replacement in new string?[] { "../outside.rb", "Save Data/Game_3.rxdata", "patch/Mods/missing.rb", null })
    {
        using var f = new Fixture(); string root = f.Dir("game"), payload = f.Dir("payload");
        string legacy = "patch/Mods/Bag.rb", renamed = "patch/Mods/RE - Bag.rb";
        f.Write(root, legacy, "old bag mod"); f.Write(root, "version.txt", "6.1.0"); f.Write(payload, renamed, "new bag mod");
        var installer = new GameInstaller(root); using var guard = installer.AcquireLock(); int writes = 0;
        string hash = VerifiedDownload.Hash(Path.Combine(root, legacy));
        Throws<InvalidDataException>(() => installer.Install(payload, Path.Combine(root, "version.txt"), "7.0.0",
            [new() { Path = legacy, Sha256 = hash, ReplacedBy = renamed },
             new() { Path = legacy, Sha256 = hash, ReplacedBy = replacement }], progress, CancellationToken.None, _ => writes++));
        Check(writes == 0 && f.Read(root, legacy) == "old bag mod" && f.Read(root, "version.txt") == "6.1.0" && !File.Exists(Path.Combine(root, renamed)));
    }
    foreach (string replacement in new[] { "../outside.rb", "Save Data/Game_3.rxdata", "patch/Mods/Bag.rb" })
    {
        var asset = new UpdateAsset { Version = "7.0.0", Url = "https://example.test/game.zip", Sha256 = new string('0', 64), Size = 1,
            RemovedFiles = [new() { Path = "patch/Mods/Bag.rb", Sha256 = new string('0', 64), ReplacedBy = replacement }] };
        Throws<InvalidDataException>(asset.Validate);
    }
});
Add("Changes to obsolete mods during preflight are preserved and stop before mutation", () =>
{
    foreach (bool changeDuringBackup in new[] { true, false })
    {
        using var f = new Fixture(); string root = f.Dir("game"), payload = f.Dir("payload");
        string legacy = "patch/Mods/Bag.rb", renamed = "patch/Mods/RE - Bag.rb";
        f.Write(root, legacy, "known bag mod"); f.Write(root, "Data/a.dat", "old game");
        f.Write(root, "version.txt", "6.1.0"); f.Write(payload, renamed, "new bag mod"); f.Write(payload, "Data/a.dat", "new game");
        string oldHash = VerifiedDownload.Hash(Path.Combine(root, legacy));
        var installer = new GameInstaller(root); using var guard = installer.AcquireLock(); int writes = 0; bool changed = false;
        void Change() { f.Write(root, legacy, "concurrent custom edit"); changed = true; }
        var observedProgress = new InlineProgress(p => { if (changeDuringBackup && !changed && p.Stage == UpdateStage.Installing) Change(); });
        Throws<IOException>(() => installer.Install(payload, Path.Combine(root, "version.txt"), "7.0.0",
            [new() { Path = legacy, Sha256 = oldHash, ReplacedBy = renamed }], observedProgress, CancellationToken.None,
            _ => writes++, beforeMutation: () => { if (!changeDuringBackup) Change(); }));
        Check(changed && writes == 0 && f.Read(root, legacy) == "concurrent custom edit" && f.Read(root, "Data/a.dat") == "old game");
        Check(f.Read(root, "version.txt") == "6.1.0" && !File.Exists(Path.Combine(root, renamed)) && !File.Exists(Path.Combine(root, ".reborn-updater/game-transaction.json")));
    }
});
Add("Invalid removal conflicts fail before mutation even after valid historical hashes", () =>
{
    foreach (string conflict in new[] { "data/a.dat", "version.txt", "Save Data/Game_3.rxdata" })
    {
        using var f = new Fixture(); string root = f.Dir("game"), payload = f.Dir("payload");
        f.Write(root, "Data/a.dat", "old"); f.Write(root, "retired.dat", "known obsolete");
        f.Write(root, "version.txt", "6.1.0"); f.Write(payload, "Data/a.dat", "new");
        var installer = new GameInstaller(root); using var guard = installer.AcquireLock(); int writes = 0;
        Throws<InvalidDataException>(() => installer.Install(payload, Path.Combine(root, "version.txt"), "7.0.0",
            [new() { Path = "retired.dat", Sha256 = VerifiedDownload.Hash(Path.Combine(root, "retired.dat")) },
             new() { Path = conflict, Sha256 = new string('0', 64) }], progress, CancellationToken.None, _ => writes++));
        Check(writes == 0 && f.Read(root, "Data/a.dat") == "old" && f.Read(root, "retired.dat") == "known obsolete" && f.Read(root, "version.txt") == "6.1.0");
        Check(!File.Exists(Path.Combine(root, ".reborn-updater/game-transaction.json")));
    }
});
Add("Cancellation mid-install rolls back earlier writes", () =>
{
    using var f = new Fixture(); string root = f.Dir("game"), payload = f.Dir("payload"); f.Write(root, "a", "old"); f.Write(payload, "a", "new"); f.Write(payload, "b", "new");
    var installer = new GameInstaller(root); using var guard = installer.AcquireLock(); using var cancel = new CancellationTokenSource();
    Throws<OperationCanceledException>(() => installer.Install(payload, Path.Combine(root, "version.txt"), "7.0.0", [], progress, cancel.Token, i => cancel.Cancel()));
    Check(f.Read(root, "a") == "old" && !File.Exists(Path.Combine(root, "b")));
});
Add("Failure after backup and before mutation preserves game and removes unused backups", () =>
{
    using var f = new Fixture(); string root = f.Dir("game"), payload = f.Dir("payload"); f.Write(root, "a", "old"); f.Write(payload, "a", "new");
    var installer = new GameInstaller(root); using var guard = installer.AcquireLock();
    Throws<InvalidOperationException>(() => installer.Install(payload, Path.Combine(root, "version.txt"), "7.0.0", [], progress, CancellationToken.None, beforeMutation: () => throw new InvalidOperationException("game started during backup")));
    Check(f.Read(root, "a") == "old" && Directory.GetDirectories(Path.Combine(root, ".reborn-updater"), "game-*").Length == 0);
});
Add("Failed rollback retains durable journal and can be retried after file unlock", () =>
{
    using var f = new Fixture(); string root = f.Dir("game"), payload = f.Dir("payload"); f.Write(root, "a", "old"); f.Write(payload, "a", "new");
    var installer = new GameInstaller(root); using var guard = installer.AcquireLock(); FileStream? blocked = null;
    try
    {
        Throws<IOException>(() => installer.Install(payload, Path.Combine(root, "version.txt"), "7.0.0", [], progress, CancellationToken.None, i =>
        {
            blocked = new FileStream(Path.Combine(root, "a"), FileMode.Open, FileAccess.Read, FileShare.None);
            throw new IOException("write failed with target locked");
        }));
        Check(File.Exists(Path.Combine(root, ".reborn-updater/game-transaction.json")));
    }
    finally { blocked?.Dispose(); }
    installer.Recover(); Check(f.Read(root, "a") == "old");
});
Add("Explicit removal requires old hash and cannot touch saves", () =>
{
    using var f = new Fixture(); string root = f.Dir("game"), payload = f.Dir("payload"); f.Write(payload, "a", "new"); f.Write(root, "old", "modified");
    var installer = new GameInstaller(root); using var guard = installer.AcquireLock();
    installer.Install(payload, Path.Combine(root, "version.txt"), "7.0.0", [new() { Path = "old", Sha256 = new string('0', 64) }], progress, CancellationToken.None);
    Check(f.Read(root, "old") == "modified");
    Throws<InvalidDataException>(() => installer.Install(payload, Path.Combine(root, "version.txt"), "7.0.1", [new() { Path = "Save Data/Game.rxdata", Sha256 = new string('0', 64) }], progress, CancellationToken.None));
});
Add("Process interruption recovers using durable journal", () =>
{
    using var f = new Fixture(); string root = f.Dir("game"), payload = f.Dir("payload"); f.Write(root, "a", "old"); f.Write(root, "version.txt", "6.1.0"); f.Write(payload, "a", "new"); f.Write(payload, "b", "new");
    var info = new System.Diagnostics.ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = false };
    if (Path.GetFileNameWithoutExtension(Environment.ProcessPath!).Equals("dotnet", StringComparison.OrdinalIgnoreCase)) info.ArgumentList.Add(typeof(Fixture).Assembly.Location);
    info.ArgumentList.Add("--interrupt-game"); info.ArgumentList.Add(root); info.ArgumentList.Add(payload);
    using (var child = System.Diagnostics.Process.Start(info)!) { child.WaitForExit(); Check(child.ExitCode == 91); }
    Check(f.Read(root, "a") == "new" && File.Exists(Path.Combine(root, ".reborn-updater/game-transaction.json")));
    var fresh = new GameInstaller(root); using var guard = fresh.AcquireLock(); fresh.Recover(); fresh.Recover();
    Check(f.Read(root, "a") == "old" && f.Read(root, "version.txt") == "6.1.0" && !File.Exists(Path.Combine(root, "b")));
});
Add("Committed interrupted cleanup preserves new version", () =>
{
    using var f = new Fixture(); string root = f.Dir("game"); f.Write(root, "a", "new");
    var installer = new GameInstaller(root); string stage = installer.CreateStage();
    GameInstaller.SaveJournal(Path.Combine(root, ".reborn-updater/game-transaction.json"), new GameInstaller.Journal { Transaction = Path.GetFileName(stage), Committed = true, Operations = [new() { Path = "a", Ready = true }] });
    installer.Recover(); Check(f.Read(root, "a") == "new");
});
Add("Journal traversal cannot escape install folder", () =>
{
    using var f = new Fixture(); string root = f.Dir("game"); var installer = new GameInstaller(root);
    GameInstaller.SaveJournal(Path.Combine(root, ".reborn-updater/game-transaction.json"), new GameInstaller.Journal { Transaction = "../other" });
    Throws<InvalidDataException>(() => installer.Recover());
});
Async("Downloads verify SHA/size; bad downloads removed before install", async () =>
{
    using var f = new Fixture(); byte[] bytes = Encoding.UTF8.GetBytes("package bytes");
    var asset = new UpdateAsset { Version = "7.0.0", Url = "https://example.test/game.zip", Size = bytes.Length, Sha256 = Convert.ToHexString(SHA256.HashData(bytes)) };
    using var downloader = new VerifiedDownload(new StubHandler(bytes));
    string path = Path.Combine(f.Root, "good"); await downloader.DownloadAsync(asset, path, progress, CancellationToken.None); Check(File.ReadAllBytes(path).SequenceEqual(bytes));
    asset.Sha256 = new string('0', 64); string bad = Path.Combine(f.Root, "bad");
    await ThrowsAsync<InvalidDataException>(() => downloader.DownloadAsync(asset, bad, progress, CancellationToken.None)); Check(!File.Exists(bad));
    asset.Size++; await ThrowsAsync<InvalidDataException>(() => downloader.DownloadAsync(asset, bad, progress, CancellationToken.None));
    Throws<InvalidDataException>(() => VerifiedDownload.ValidateUrl("http://example.test/package"));
    Throws<InvalidDataException>(() => VerifiedDownload.ValidateUrl("https://user:password@example.test/package"));
});
Async("404 fails clearly without legacy fallback", async () =>
{
    using var downloader = new VerifiedDownload(new StubHandler([], HttpStatusCode.NotFound));
    await ThrowsAsync<InvalidOperationException>(() => downloader.TextAsync("https://example.test/update.json", CancellationToken.None));
});
Async("Service rejects downgrade under install lock before downloading the package", async () =>
{
    using var f = new Fixture(); string root = f.Dir("game");
    foreach (string file in new[] { "Game.exe", "Game.ini", "mkxp.json" }) f.Write(root, file, "existing");
    f.Write(root, "version.txt", "8.0.0");
    var asset = new { version = "7.0.0", url = "https://example.test/package", sha256 = new string('0', 64), size = 1 };
    string json = System.Text.Json.JsonSerializer.Serialize(new { schemaVersion = 1, launcher = asset, game = asset, patchNotes = "" });
    var handler = new StubHandler(Encoding.UTF8.GetBytes(json));
    using var service = new UpdateService(new VerifiedDownload(handler));
    await ThrowsAsync<InvalidOperationException>(() => service.DownloadAndInstallAsync(new LauncherSettings { InstallDirectory = root, ManifestUrl = "https://example.test/update.json" }, "7.0.0", progress, CancellationToken.None));
    Check(handler.Requests == 1 && f.Read(root, "version.txt") == "8.0.0", "Only manifest should be requested");
});
Add("Self-update atomic replacement and previous-version rollback", () =>
{
    using var f = new Fixture(); string target = f.Write(f.Root, "launcher.exe", "MZold"), candidate = f.Write(f.Root, "candidate.exe", "MZnew");
    var request = new SelfUpdateService.Request { Target = target, Candidate = candidate, PreviousSha256 = VerifiedDownload.Hash(target), Sha256 = VerifiedDownload.Hash(candidate), Size = new FileInfo(candidate).Length };
    SelfUpdateService.ReplaceLauncher(request, Path.Combine(f.Root, "request.json"));
    Check(File.ReadAllText(target) == "MZnew" && File.ReadAllText(target + ".previous") == "MZold" && request.Phase == "installed");
    SelfUpdateService.RestoreLauncher(request); Check(File.ReadAllText(target) == "MZold");
});
Add("Self-update rejects bad hash and concurrently changed target", () =>
{
    using var f = new Fixture(); string target = f.Write(f.Root, "launcher.exe", "old"), candidate = f.Write(f.Root, "candidate.exe", "new");
    var request = new SelfUpdateService.Request { Target = target, Candidate = candidate, PreviousSha256 = VerifiedDownload.Hash(target), Sha256 = new string('0', 64), Size = 3 };
    Throws<InvalidDataException>(() => SelfUpdateService.ReplaceLauncher(request, Path.Combine(f.Root, "request.json"))); Check(File.ReadAllText(target) == "old");
    request.Sha256 = VerifiedDownload.Hash(candidate); File.WriteAllText(target, "changed");
    Throws<IOException>(() => SelfUpdateService.ReplaceLauncher(request, Path.Combine(f.Root, "request.json"))); Check(File.ReadAllText(target) == "changed");
});
Add("Install lock excludes simultaneous launchers", () =>
{
    using var f = new Fixture(); var installer = new GameInstaller(f.Dir("game")); using var guard = installer.AcquireLock(); Throws<IOException>(() => installer.AcquireLock());
});
Add("Large install keeps the durable journal unchanged between first write and commit", () =>
{
    using var f = new Fixture(); string root = f.Dir("game"), payload = f.Dir("payload");
    for (int i = 0; i < 2000; i++) f.Write(payload, $"Data/{i:D5}.dat", "content");
    var installer = new GameInstaller(root); using var guard = installer.AcquireLock();
    string journalPath = Path.Combine(root, ".reborn-updater/game-transaction.json");
    DateTime firstWrite = default;
    var watch = System.Diagnostics.Stopwatch.StartNew();
    installer.Install(payload, Path.Combine(root, "version.txt"), "7.0.0", [], progress, CancellationToken.None, i =>
    {
        if (i == 0) firstWrite = File.GetLastWriteTimeUtc(journalPath);
        Check(File.GetLastWriteTimeUtc(journalPath) == firstWrite, "Journal was rewritten for an individual file");
    });
    Check(Directory.GetFiles(Path.Combine(root, "Data")).Length == 2000);
    Console.WriteLine($"Large install: 2000 files in {watch.Elapsed.TotalSeconds:0.00}s");
});

Add("Two versions switch repeatedly with added/deleted files, shared bytes and untouched saves", () =>
{
    using var f = new Fixture(); string root = f.Dir("game"), payload = f.Dir("v2");
    f.Write(root, "a.dat", "v1"); f.Write(root, "retired.dat", "v1 only"); f.Write(root, "version.txt", "1.0.0");
    string shared = f.Write(root, "same.dat", "shared"); f.Write(payload, "same.dat", "shared");
    f.Write(payload, "a.dat", "v2"); f.Write(payload, "added.dat", "v2 only");
    f.Write(root, "Save Data/Game_3.rxdata", "save"); f.Write(root, "mkxp.json", "settings"); f.Write(root, "patch/Mods/my.rb", "own mod");
    DateTime time = new(2001, 1, 1, 0, 0, 0, DateTimeKind.Utc); File.SetLastWriteTimeUtc(shared, time);
    var installer = new GameInstaller(root); using var guard = installer.AcquireLock();
    installer.Install(payload, Path.Combine(root, "version.txt"), "2.0.0", [new() { Path = "retired.dat", Sha256 = VerifiedDownload.Hash(Path.Combine(root, "retired.dat")) }], progress, CancellationToken.None);
    for (int i = 0; i < 8; i++)
    {
        installer.SwitchVersion(Path.Combine(root, "version.txt"), progress, CancellationToken.None);
        var pair = installer.GetVersionSelection(Path.Combine(root, "version.txt"));
        bool previous = i % 2 == 0;
        Check(pair.ActiveVersion == (previous ? "1.0.0" : "2.0.0") && pair.OtherVersion == (previous ? "2.0.0" : "1.0.0") && pair.IsPreviousActive == previous);
        Check(f.Read(root, "a.dat") == (previous ? "v1" : "v2"));
        Check(File.Exists(Path.Combine(root, "retired.dat")) == previous && File.Exists(Path.Combine(root, "added.dat")) != previous);
        Check(f.Read(root, "Save Data/Game_3.rxdata") == "save" && f.Read(root, "mkxp.json") == "settings" && f.Read(root, "patch/Mods/my.rb") == "own mod");
        Check(File.GetLastWriteTimeUtc(shared) == time);
        var pairState = System.Text.Json.JsonSerializer.Deserialize<GameInstaller.VersionState>(f.Read(root, ".reborn-updater/versions.json"))!;
        Check(!pairState.Entries.Any(e => e.Path == "same.dat"));
        Check(Directory.GetDirectories(Path.Combine(root, ".reborn-updater"), "game-*").Length == 1, "Only one inactive snapshot should remain");
    }
});
Add("Third release rotates the pair and discards only the oldest snapshot", () =>
{
    using var f = new Fixture(); string root = f.Dir("game"), v2 = f.Dir("v2"), v3 = f.Dir("v3");
    f.Write(root, "a", "v1"); f.Write(root, "version.txt", "1.0.0"); f.Write(v2, "a", "v2"); f.Write(v3, "a", "v3");
    var installer = new GameInstaller(root); using var guard = installer.AcquireLock();
    installer.Install(v2, Path.Combine(root, "version.txt"), "2.0.0", [], progress, CancellationToken.None);
    string oldSnapshot = Directory.GetDirectories(Path.Combine(root, ".reborn-updater"), "game-*").Single();
    installer.Install(v3, Path.Combine(root, "version.txt"), "3.0.0", [], progress, CancellationToken.None);
    Check(!Directory.Exists(oldSnapshot));
    Check(installer.GetVersionSelection(Path.Combine(root, "version.txt")) == new VersionSelection("3.0.0", "2.0.0", false));
    installer.SwitchVersion(Path.Combine(root, "version.txt"), progress, CancellationToken.None);
    Check(f.Read(root, "a") == "v2" && installer.GetVersionSelection(Path.Combine(root, "version.txt")) == new VersionSelection("2.0.0", "3.0.0", true));
    installer.SwitchVersion(Path.Combine(root, "version.txt"), progress, CancellationToken.None); Check(f.Read(root, "a") == "v3");
});
Add("Failed and cancelled new release retain both existing versions", () =>
{
    using var f = new Fixture(); string root = f.Dir("game"), v2 = f.Dir("v2"), v3 = f.Dir("v3");
    f.Write(root, "a", "v1"); f.Write(root, "version.txt", "1.0.0"); f.Write(v2, "a", "v2"); f.Write(v3, "a", "v3");
    var installer = new GameInstaller(root); using var guard = installer.AcquireLock();
    installer.Install(v2, Path.Combine(root, "version.txt"), "2.0.0", [], progress, CancellationToken.None);
    string metadata = f.Read(root, ".reborn-updater/versions.json");
    Throws<IOException>(() => installer.Install(v3, Path.Combine(root, "version.txt"), "3.0.0", [], progress, CancellationToken.None, _ => throw new IOException("disk fault")));
    using var cancel = new CancellationTokenSource();
    Throws<OperationCanceledException>(() => installer.Install(v3, Path.Combine(root, "version.txt"), "3.0.0", [], progress, cancel.Token, _ => cancel.Cancel()));
    Check(f.Read(root, "a") == "v2" && metadata == f.Read(root, ".reborn-updater/versions.json"));
    installer.SwitchVersion(Path.Combine(root, "version.txt"), progress, CancellationToken.None); Check(f.Read(root, "a") == "v1");
    installer.SwitchVersion(Path.Combine(root, "version.txt"), progress, CancellationToken.None); Check(f.Read(root, "a") == "v2");
});
Add("Repair preserves previous release and repairs bytes before switching back and forth", () =>
{
    using var f = new Fixture(); string root = f.Dir("game"), v2 = f.Dir("v2");
    f.Write(root, "a", "v1"); f.Write(root, "version.txt", "1.0.0"); f.Write(v2, "a", "v2");
    var installer = new GameInstaller(root); using var guard = installer.AcquireLock();
    installer.Install(v2, Path.Combine(root, "version.txt"), "2.0.0", [], progress, CancellationToken.None);
    string metadata = f.Read(root, ".reborn-updater/versions.json");
    f.Write(root, "a", "corrupt");
    installer.Install(v2, Path.Combine(root, "version.txt"), "2.0.0", [], progress, CancellationToken.None);
    Check(metadata == f.Read(root, ".reborn-updater/versions.json"));
    installer.SwitchVersion(Path.Combine(root, "version.txt"), progress, CancellationToken.None); Check(f.Read(root, "a") == "v1");
    Throws<InvalidOperationException>(() => installer.Install(v2, Path.Combine(root, "version.txt"), "2.0.0", [], progress, CancellationToken.None));
    installer.SwitchVersion(Path.Combine(root, "version.txt"), progress, CancellationToken.None); Check(f.Read(root, "a") == "v2");
});
Add("Damaged snapshot fails before any active mutation and keeps the selected release playable", () =>
{
    using var f = new Fixture(); string root = f.Dir("game"), v2 = f.Dir("v2");
    f.Write(root, "a", "v1"); f.Write(root, "z", "old z"); f.Write(root, "version.txt", "1.0.0"); f.Write(v2, "a", "v2"); f.Write(v2, "z", "new z");
    var installer = new GameInstaller(root); using var guard = installer.AcquireLock();
    installer.Install(v2, Path.Combine(root, "version.txt"), "2.0.0", [], progress, CancellationToken.None);
    var pair = System.Text.Json.JsonSerializer.Deserialize<GameInstaller.VersionState>(f.Read(root, ".reborn-updater/versions.json"))!;
    f.Write(root, $".reborn-updater/{pair.Snapshot}/backup/z", "corrupt backup");
    int writes = 0;
    Throws<InvalidDataException>(() => installer.SwitchVersion(Path.Combine(root, "version.txt"), progress, CancellationToken.None, _ => writes++));
    Check(writes == 0 && f.Read(root, "a") == "v2" && f.Read(root, "z") == "new z" && f.Read(root, "version.txt") == "2.0.0");
});
Add("Failed and cancelled switches preserve the pair and can be retried", () =>
{
    using var f = new Fixture(); string root = f.Dir("game"), v2 = f.Dir("v2");
    f.Write(root, "a", "v1"); f.Write(root, "version.txt", "1.0.0"); f.Write(v2, "a", "v2"); f.Write(v2, "new", "v2 only");
    var installer = new GameInstaller(root); using var guard = installer.AcquireLock();
    installer.Install(v2, Path.Combine(root, "version.txt"), "2.0.0", [], progress, CancellationToken.None);
    string metadata = f.Read(root, ".reborn-updater/versions.json");
    Throws<IOException>(() => installer.SwitchVersion(Path.Combine(root, "version.txt"), progress, CancellationToken.None, _ => throw new IOException("disk fault")));
    using var cancel = new CancellationTokenSource();
    Throws<OperationCanceledException>(() => installer.SwitchVersion(Path.Combine(root, "version.txt"), progress, cancel.Token, _ => cancel.Cancel()));
    Check(f.Read(root, "a") == "v2" && f.Read(root, "new") == "v2 only" && metadata == f.Read(root, ".reborn-updater/versions.json"));
    installer.SwitchVersion(Path.Combine(root, "version.txt"), progress, CancellationToken.None); Check(f.Read(root, "a") == "v1" && !File.Exists(Path.Combine(root, "new")));
});
Add("Process interruption during switch restores active files and retains both releases", () =>
{
    using var f = new Fixture(); string root = f.Dir("game"), v2 = f.Dir("v2");
    f.Write(root, "a", "v1"); f.Write(root, "version.txt", "1.0.0"); f.Write(v2, "a", "v2"); f.Write(v2, "new", "v2 only");
    var installer = new GameInstaller(root);
    using (installer.AcquireLock()) installer.Install(v2, Path.Combine(root, "version.txt"), "2.0.0", [], progress, CancellationToken.None);
    string metadata = f.Read(root, ".reborn-updater/versions.json");
    var info = new System.Diagnostics.ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = false };
    if (Path.GetFileNameWithoutExtension(Environment.ProcessPath!).Equals("dotnet", StringComparison.OrdinalIgnoreCase)) info.ArgumentList.Add(typeof(Fixture).Assembly.Location);
    info.ArgumentList.Add("--interrupt-switch"); info.ArgumentList.Add(root);
    using (var child = System.Diagnostics.Process.Start(info)!) { child.WaitForExit(); Check(child.ExitCode == 92); }
    Check(f.Read(root, "a") == "v1" && File.Exists(Path.Combine(root, ".reborn-updater/game-transaction.json")));
    var fresh = new GameInstaller(root); using var guard = fresh.AcquireLock(); fresh.Recover(); fresh.Recover();
    Check(f.Read(root, "a") == "v2" && f.Read(root, "new") == "v2 only" && f.Read(root, "version.txt") == "2.0.0" && metadata == f.Read(root, ".reborn-updater/versions.json"));
    fresh.SwitchVersion(Path.Combine(root, "version.txt"), progress, CancellationToken.None); Check(f.Read(root, "a") == "v1");
    fresh.SwitchVersion(Path.Combine(root, "version.txt"), progress, CancellationToken.None); Check(f.Read(root, "a") == "v2");
});
Async("Service blocks update and repair of previous selection before package request", async () =>
{
    using var f = new Fixture(); string root = f.Dir("game"), v2 = f.Dir("v2");
    foreach (string file in new[] { "Game.exe", "Game.ini", "mkxp.json" }) f.Write(root, file, "existing");
    f.Write(root, "a", "v1"); f.Write(root, "version.txt", "1.0.0"); f.Write(v2, "a", "v2");
    var installer = new GameInstaller(root);
    using (installer.AcquireLock()) { installer.Install(v2, Path.Combine(root, "version.txt"), "2.0.0", [], progress, CancellationToken.None); installer.SwitchVersion(Path.Combine(root, "version.txt"), progress, CancellationToken.None); }
    var asset = new { version = "2.0.0", url = "https://example.test/package", sha256 = new string('0', 64), size = 1 };
    string json = System.Text.Json.JsonSerializer.Serialize(new { schemaVersion = 1, launcher = asset, game = asset, patchNotes = "" });
    var handler = new StubHandler(Encoding.UTF8.GetBytes(json)); using var service = new UpdateService(new VerifiedDownload(handler));
    var settings = new LauncherSettings { InstallDirectory = root, ManifestUrl = "https://example.test/update.json" };
    Check(service.GetVersionSelection(settings) == new VersionSelection("1.0.0", "2.0.0", true));
    await ThrowsAsync<InvalidOperationException>(() => service.DownloadAndInstallAsync(settings, "2.0.0", progress, CancellationToken.None));
    Check(handler.Requests == 1 && f.Read(root, "a") == "v1");
    await service.SwitchVersionAsync(settings, progress, CancellationToken.None); Check(service.GetVersionSelection(settings) == new VersionSelection("2.0.0", "1.0.0", false));
});

Add("Committed switch journal finalizes metadata after interrupted commit and retires the old snapshot", () =>
{
    using var f = new Fixture(); string root = f.Dir("game"), v2 = f.Dir("v2");
    f.Write(root, "a", "v1"); f.Write(root, "version.txt", "1.0.0"); f.Write(v2, "a", "v2");
    var installer = new GameInstaller(root); using var guard = installer.AcquireLock();
    installer.Install(v2, Path.Combine(root, "version.txt"), "2.0.0", [], progress, CancellationToken.None);
    string oldMetadata = f.Read(root, ".reborn-updater/versions.json");
    var oldPair = System.Text.Json.JsonSerializer.Deserialize<GameInstaller.VersionState>(oldMetadata)!;
    string oldSnapshot = Path.Combine(root, ".reborn-updater", oldPair.Snapshot);
    var oldBytes = Directory.EnumerateFiles(oldSnapshot, "*", SearchOption.AllDirectories)
        .ToDictionary(p => Path.GetRelativePath(oldSnapshot, p), File.ReadAllBytes);
    GameInstaller.Journal? pending = null;
    installer.SwitchVersion(Path.Combine(root, "version.txt"), progress, CancellationToken.None,
        _ => pending = System.Text.Json.JsonSerializer.Deserialize<GameInstaller.Journal>(f.Read(root, ".reborn-updater/game-transaction.json")));
    // Recreate the exact durable state at the crash boundary: files and commit
    // marker have changed, but versions.json still references the former pair.
    foreach (var file in oldBytes) { string target = Path.Combine(oldSnapshot, file.Key); Directory.CreateDirectory(Path.GetDirectoryName(target)!); File.WriteAllBytes(target, file.Value); }
    f.Write(root, ".reborn-updater/versions.json", oldMetadata);
    pending!.Committed = true;
    GameInstaller.SaveJournal(Path.Combine(root, ".reborn-updater/game-transaction.json"), pending);
    installer.Recover(); installer.Recover();
    Check(installer.GetVersionSelection(Path.Combine(root, "version.txt")) == new VersionSelection("1.0.0", "2.0.0", true));
    Check(f.Read(root, "a") == "v1" && !Directory.Exists(oldSnapshot));
    installer.SwitchVersion(Path.Combine(root, "version.txt"), progress, CancellationToken.None); Check(f.Read(root, "a") == "v2");
});
Add("Recovery removes abandoned preflight stages while retaining the inactive release", () =>
{
    using var f = new Fixture(); string root = f.Dir("game"), v2 = f.Dir("v2");
    f.Write(root, "a", "v1"); f.Write(root, "version.txt", "1.0.0"); f.Write(v2, "a", "v2");
    var installer = new GameInstaller(root); using var guard = installer.AcquireLock();
    installer.Install(v2, Path.Combine(root, "version.txt"), "2.0.0", [], progress, CancellationToken.None);
    string abandoned = installer.CreateStage(); f.Write(abandoned, "backup/a", "uncommitted");
    installer.Recover();
    Check(!Directory.Exists(abandoned) && Directory.GetDirectories(Path.Combine(root, ".reborn-updater"), "game-*").Length == 1);
    installer.SwitchVersion(Path.Combine(root, "version.txt"), progress, CancellationToken.None); Check(f.Read(root, "a") == "v1");
});
Add("Snapshot metadata rejects traversal, protected paths and missing hashes before writes", () =>
{
    using var f = new Fixture(); string root = f.Dir("game"), v2 = f.Dir("v2");
    f.Write(root, "a", "v1"); f.Write(root, "version.txt", "1.0.0"); f.Write(v2, "a", "v2");
    var installer = new GameInstaller(root); using var guard = installer.AcquireLock();
    installer.Install(v2, Path.Combine(root, "version.txt"), "2.0.0", [], progress, CancellationToken.None);
    string metadata = f.Read(root, ".reborn-updater/versions.json");
    foreach (Action<GameInstaller.VersionState> corrupt in new Action<GameInstaller.VersionState>[]
    {
        p => p.Snapshot = "../outside",
        p => p.Entries[0].Path = "Save Data/Game.rxdata",
        p => p.Entries[0].Sha256 = null,
        p => p.Entries.Add(p.Entries[0]),
    })
    {
        var pair = System.Text.Json.JsonSerializer.Deserialize<GameInstaller.VersionState>(metadata)!; corrupt(pair);
        GameInstaller.SaveJournal(Path.Combine(root, ".reborn-updater/versions.json"), pair);
        Throws<InvalidDataException>(() => installer.SwitchVersion(Path.Combine(root, "version.txt"), progress, CancellationToken.None));
        Check(f.Read(root, "a") == "v2" && f.Read(root, "version.txt") == "2.0.0");
    }
    f.Write(root, ".reborn-updater/versions.json", metadata);
    installer.SwitchVersion(Path.Combine(root, "version.txt"), progress, CancellationToken.None); Check(f.Read(root, "a") == "v1");
});

int failures = 0;
foreach (var test in tests)
{
    try { await test.Run(); Console.WriteLine("PASS " + test.Name); }
    catch (Exception ex) { failures++; Console.WriteLine("FAIL " + test.Name + Environment.NewLine + ex); }
}
Console.WriteLine($"{tests.Count - failures}/{tests.Count} checks passed");
Environment.ExitCode = failures == 0 ? 0 : 1;

sealed class StubHandler(byte[] data, HttpStatusCode status = HttpStatusCode.OK) : HttpMessageHandler
{
    public int Requests { get; private set; }
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests++;
        return Task.FromResult(new HttpResponseMessage(status) { Content = new ByteArrayContent(data), RequestMessage = request });
    }
}
sealed class InlineProgress(Action<UpdateProgress> callback) : IProgress<UpdateProgress>
{
    public void Report(UpdateProgress value) => callback(value);
}
sealed class Fixture : IDisposable
{
    public string Root { get; } = Path.Combine(Path.GetTempPath(), "RebornUpdaterTests", Guid.NewGuid().ToString("N"));
    public Fixture() => Directory.CreateDirectory(Root);
    public string Dir(string path) { string target = Path.Combine(Root, path); Directory.CreateDirectory(target); return target; }
    public string Write(string root, string path, string value) { string target = Path.Combine(root, path); Directory.CreateDirectory(Path.GetDirectoryName(target)!); File.WriteAllText(target, value); return target; }
    public string Read(string root, string path) => File.ReadAllText(Path.Combine(root, path));
    public string Zip(string[] entries)
    {
        string path = Path.Combine(Root, "archive.zip");
        using (var zip = ZipFile.Open(path, ZipArchiveMode.Create)) foreach (string name in entries) { using var writer = new StreamWriter(zip.CreateEntry(name).Open()); writer.Write("data"); }
        return path;
    }
    public void Dispose() { if (Directory.Exists(Root)) Directory.Delete(Root, true); }
}
