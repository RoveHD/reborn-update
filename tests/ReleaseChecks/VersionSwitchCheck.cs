using System.Text.Json;
using PokemonRebornLauncher.Services;

internal static class VersionSwitchCheck
{
    public static async Task Run(string release, string output)
    {
        string report = Path.Combine(output, "release-migration.json");
        if (!File.Exists(report)) report = Path.Combine(output, "release-install.json");
        using var earlier = JsonDocument.Parse(File.ReadAllText(report));
        string root = Path.GetFullPath(earlier.RootElement.GetProperty("isolatedInstall").GetString()!);
        if (!root.StartsWith(output + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
            !Path.GetFileName(root).StartsWith("game-") || File.ReadAllText(Path.Combine(root, "Save Data/Game_3.rxdata")) is not ("unique friend save sentinel" or "unique friend save migration sentinel"))
            throw new Exception("Refuse to mutate outside isolated integration fixture");
        var manifest = UpdateManifest.Parse(File.ReadAllText(Path.Combine(release, "update.json")));
        var settings = new LauncherSettings { InstallDirectory = root };
        using var service = new UpdateService();
        var progress = new Reporter();
        string version = await service.ReadLocalVersionAsync(settings, CancellationToken.None);
        if (version != "7.0.0") throw new Exception("Expected untouched 7.0.0 test fixture");
        string friendMod = File.Exists(Path.Combine(root, "patch/Mods/FriendsOwnMigrationMod.rb")) ? "patch/Mods/FriendsOwnMigrationMod.rb" : "patch/Mods/FriendsOwnMod.rb";
        var protectedFiles = new[] { "mkxp.json", "settings.json", "Save Data/Game_3.rxdata", friendMod }
            .ToDictionary(p => p, p => VerifiedDownload.Hash(Path.Combine(root, p)));
        string changed = "Scripts/Summary.rb", added = "Scripts/VersionSwitchFixture.rb", removed = "Scripts/AnimEditor.rb";
        string oldChanged = VerifiedDownload.Hash(Path.Combine(root, changed));
        string oldRemoved = VerifiedDownload.Hash(Path.Combine(root, removed));
        var installer = new GameInstaller(root);
        string stage = installer.CreateStage();
        try
        {
            string payload = Path.Combine(stage, "payload");
            Directory.CreateDirectory(Path.Combine(payload, "Scripts"));
            File.WriteAllText(Path.Combine(payload, changed), "# synthetic next release; never launched");
            File.WriteAllText(Path.Combine(payload, added), "# new synthetic file");
            if (service.GetVersionSelection(settings) == new VersionSelection("7.0.0", "7.0.1", true))
                await service.SwitchVersionAsync(settings, progress, CancellationToken.None);
            else
            {
                using (installer.AcquireLock())
                    installer.Install(payload, settings.ResolveLocalVersionPath(), "7.0.1", [new RemovedFile { Path = removed, Sha256 = oldRemoved }], progress, CancellationToken.None);
            }
            if (service.GetVersionSelection(settings) != new VersionSelection("7.0.1", "7.0.0", false)) throw new Exception("Wrong pair after update");
            await service.SwitchVersionAsync(settings, progress, CancellationToken.None);
            if (service.GetVersionSelection(settings) != new VersionSelection("7.0.0", "7.0.1", true)) throw new Exception("Wrong previous selection");
            int verified = 0;
            using var expected = JsonDocument.Parse(File.ReadAllText(Path.Combine(release, "game-files.json")));
            foreach (var file in expected.RootElement.EnumerateObject())
            {
                if (!VerifiedDownload.Hash(SafePaths.Under(root, file.Name)).Equals(file.Value.GetProperty("sha256").GetString(), StringComparison.OrdinalIgnoreCase))
                    throw new Exception("Restored file differs: " + file.Name);
                verified++;
            }
            if (File.Exists(Path.Combine(root, added))) throw new Exception("New-only file remains on old release");
            await service.SwitchVersionAsync(settings, progress, CancellationToken.None);
            if (File.Exists(Path.Combine(root, removed)) || !File.Exists(Path.Combine(root, added))) throw new Exception("New release not restored");
            await service.SwitchVersionAsync(settings, progress, CancellationToken.None);
            if (VerifiedDownload.Hash(Path.Combine(root, changed)) != oldChanged || VerifiedDownload.Hash(Path.Combine(root, removed)) != oldRemoved)
                throw new Exception("Repeated switch failed");
            foreach (var file in protectedFiles)
                if (VerifiedDownload.Hash(Path.Combine(root, file.Key)) != file.Value) throw new Exception("Personal file changed: " + file.Key);
            var result = new { success = true, restoredFilesVerified = verified, switches = 3, addedAndRemovedFilesVerified = true,
                personalFilesUnchanged = protectedFiles.Keys, archiveSha256 = manifest.Game.Sha256, isolatedInstall = root,
                activeVersion = service.GetVersionSelection(settings).ActiveVersion, syntheticNextVersion = "7.0.1" };
            File.WriteAllText(Path.Combine(output, "release-switch.json"), JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
            Console.WriteLine(JsonSerializer.Serialize(result));
        }
        finally { GameInstaller.TryClean(stage); }
    }
}
