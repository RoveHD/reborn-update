using System.Text.Json;
using PokemonRebornLauncher.Services;

internal static class MigrationCheck
{
    public static void Run(string release, string output, string previousArchive, string? baseline = null)
    {
        foreach (string path in new[] { release, output, previousArchive }.Concat(baseline is null ? [] : new[] { baseline }))
            if (!string.Equals(Path.GetPathRoot(path), "D:\\", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Migration checks only read and write D: test copies.");
        var timer = System.Diagnostics.Stopwatch.StartNew();
        var progress = new Reporter();
        var manifest = UpdateManifest.Parse(File.ReadAllText(Path.Combine(release, "update.json")));
        string archive = SafePaths.Under(release, Path.GetFileName(new Uri(manifest.Game.Url).AbsolutePath));
        if (!VerifiedDownload.Hash(archive).Equals(manifest.Game.Sha256, StringComparison.OrdinalIgnoreCase))
            throw new Exception("Wrong new release archive hash");
        string previousArchiveSha256 = VerifiedDownload.Hash(previousArchive);
        string fixture = Path.Combine(output, "game-" + Guid.NewGuid().ToString("N"));
        string root = SafePaths.Extract(previousArchive, fixture, progress, CancellationToken.None);
        if (!root.StartsWith(output + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new Exception("Refuse migration outside generated test fixture");
        int baselineFilesAdded = 0;
        if (baseline is not null)
        {
            // Published old releases may be patch ZIPs. Fill missing base-game files
            // from the authorized D: backup; archived patch bytes always win.
            var pendingDirectories = new Stack<string>();
            pendingDirectories.Push(baseline);
            while (pendingDirectories.TryPop(out string? directory))
            {
                SafePaths.NoLinks(directory);
                foreach (string source in Directory.EnumerateFiles(directory))
                {
                    string relative = SafePaths.ValidateRelative(Path.GetRelativePath(baseline, source));
                    if (SafePaths.IsProtected(relative)) continue;
                    SafePaths.NoLinks(source);
                    string target = SafePaths.Under(root, relative);
                    if (File.Exists(target)) continue;
                    Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                    File.Copy(source, target);
                    baselineFilesAdded++;
                }
                foreach (string child in Directory.EnumerateDirectories(directory))
                {
                    string relative = SafePaths.ValidateRelative(Path.GetRelativePath(baseline, child));
                    if (!SafePaths.IsProtected(relative)) pendingDirectories.Push(child);
                }
            }
        }
        if (!File.Exists(SafePaths.Under(root, "Game.exe")))
            throw new Exception("The old archive is a patch without Game.exe; provide an authorized D: baseline directory.");
        void Write(string relative, string text)
        {
            string path = SafePaths.Under(root, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, text);
        }
        Write("version.txt", "6.1.0");
        Write("mkxp.json", "friend display settings migration sentinel");
        Write("settings.json", "friend launcher settings migration sentinel");
        Write("Save Data/Game_3.rxdata", "unique friend save migration sentinel");
        const string unknownMod = "patch/Mods/FriendsOwnMigrationMod.rb";
        if (File.Exists(SafePaths.Under(root, unknownMod))) throw new Exception("Sentinel mod name already exists");
        Write(unknownMod, "# unknown friend mod migration sentinel");
        var personal = new[] { "mkxp.json", "settings.json", "Save Data/Game_3.rxdata", unknownMod }
            .ToDictionary(p => p, p => VerifiedDownload.Hash(SafePaths.Under(root, p)), StringComparer.OrdinalIgnoreCase);
        bool Include(string relative) => !SafePaths.IsProtected(relative) && !relative.Equals("version.txt", StringComparison.OrdinalIgnoreCase);
        Dictionary<string, string> HashOld() => Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Select(p => (Path: SafePaths.ValidateRelative(Path.GetRelativePath(root, p)), Source: p))
            .Where(p => Include(p.Path)).ToDictionary(p => p.Path, p => VerifiedDownload.Hash(p.Source), StringComparer.OrdinalIgnoreCase);
        var old = HashOld();
        using var expected = JsonDocument.Parse(File.ReadAllText(Path.Combine(release, "game-files.json")));
        var current = expected.RootElement.EnumerateObject().ToDictionary(p => p.Name,
            p => p.Value.GetProperty("sha256").GetString()!, StringComparer.OrdinalIgnoreCase);
        var retired = manifest.Game.RemovedFiles.GroupBy(p => SafePaths.ValidateRelative(p.Path), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Select(p => p.Sha256).ToHashSet(StringComparer.OrdinalIgnoreCase), StringComparer.OrdinalIgnoreCase);
        int expectedRemoved = old.Count(p => retired.TryGetValue(p.Key, out var hashes) && hashes.Contains(p.Value));
        int renamed = manifest.Game.RemovedFiles.Where(p => p.ReplacedBy is not null).Select(p => p.Path).Distinct(StringComparer.OrdinalIgnoreCase).Count();
        void VerifyPersonal()
        {
            foreach (var file in personal)
                if (VerifiedDownload.Hash(SafePaths.Under(root, file.Key)) != file.Value) throw new Exception("Personal file changed: " + file.Key);
        }
        void VerifyCurrent()
        {
            foreach (var file in current)
            {
                string path = SafePaths.Under(root, file.Key);
                if (!File.Exists(path) || !VerifiedDownload.Hash(path).Equals(file.Value, StringComparison.OrdinalIgnoreCase))
                    throw new Exception("New release file differs: " + file.Key);
            }
            foreach (var file in old.Where(p => !current.ContainsKey(p.Key)))
            {
                string path = SafePaths.Under(root, file.Key);
                if (retired.TryGetValue(file.Key, out var hashes) && hashes.Contains(file.Value))
                {
                    if (File.Exists(path)) throw new Exception("Known obsolete file remains: " + file.Key);
                }
                else if (!File.Exists(path) || VerifiedDownload.Hash(path) != file.Value)
                    throw new Exception("Unknown old file changed or deleted: " + file.Key);
            }
            if (File.ReadAllText(Path.Combine(root, "version.txt")) != manifest.Game.Version) throw new Exception("Wrong new active version");
            VerifyPersonal();
        }
        void VerifyOld()
        {
            var actual = HashOld();
            if (actual.Count != old.Count) throw new Exception("Old release file set differs after switch");
            foreach (var file in old)
                if (!actual.TryGetValue(file.Key, out string? hash) || hash != file.Value) throw new Exception("Restored old file differs: " + file.Key);
            if (File.ReadAllText(Path.Combine(root, "version.txt")) != "6.1.0") throw new Exception("Wrong old active version");
            VerifyPersonal();
        }
        var installer = new GameInstaller(root);
        using var updateLock = installer.AcquireLock();
        string stage = installer.CreateStage();
        try
        {
            string payload = SafePaths.Extract(archive, Path.Combine(stage, "payload"), progress, CancellationToken.None);
            installer.Install(payload, Path.Combine(root, "version.txt"), manifest.Game.Version, manifest.Game.RemovedFiles, progress, CancellationToken.None);
            VerifyCurrent();
            for (int i = 0; i < 4; i++)
            {
                installer.SwitchVersion(Path.Combine(root, "version.txt"), progress, CancellationToken.None);
                if (i % 2 == 0) VerifyOld(); else VerifyCurrent();
            }
            var result = new { success = true, oldFilesVerified = old.Count, newFilesVerified = current.Count,
                removedHistoricalFilesVerified = expectedRemoved, renamedPathsInManifest = renamed, switches = 4,
                personalFilesUnchanged = personal.Keys, previousArchiveSha256, archiveSha256 = manifest.Game.Sha256,
                isolatedInstall = root, baselineSource = baseline, baselineFilesAdded,
                gameExecutableBytesVerified = old.ContainsKey("Game.exe"), activeVersion = manifest.Game.Version, durationSeconds = timer.Elapsed.TotalSeconds,
                gameStarted = false };
            string json = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(Path.Combine(output, "release-migration.json"), json);
            Console.WriteLine(json);
        }
        finally { GameInstaller.TryClean(stage); }
    }
}
