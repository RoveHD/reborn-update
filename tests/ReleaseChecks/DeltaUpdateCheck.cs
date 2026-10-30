using System.Collections.Concurrent;
using System.Net;
using System.Runtime.InteropServices;
using System.Text.Json;
using PokemonRebornLauncher.Services;

internal static class DeltaUpdateCheck
{
    private const string ManifestUrl = "https://raw.githubusercontent.com/RoveHD/reborn-update/main/update.json";

    public static async Task Run(string release, string output, string previousRelease, string baselineRoot, bool publicDownload)
    {
        release = Path.GetFullPath(release).TrimEnd(Path.DirectorySeparatorChar);
        output = Path.GetFullPath(output).TrimEnd(Path.DirectorySeparatorChar);
        previousRelease = Path.GetFullPath(previousRelease).TrimEnd(Path.DirectorySeparatorChar);
        baselineRoot = Path.GetFullPath(baselineRoot).TrimEnd(Path.DirectorySeparatorChar);
        foreach (string path in new[] { release, output, previousRelease, baselineRoot })
            if (!string.Equals(Path.GetPathRoot(path), "D:\\", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Delta update checks may only use D: test copies.");

        string manifestPath = Path.Combine(release, "update.json");
        string inventoryPath = Path.Combine(release, "game-files.json");
        string previousInventoryPath = Path.Combine(previousRelease, "game-files.json");
        if (!File.Exists(manifestPath) || !File.Exists(inventoryPath) || !File.Exists(previousInventoryPath))
            throw new FileNotFoundException("Current and previous release proofs are required.");

        var targetManifest = UpdateManifest.Parse(File.ReadAllText(manifestPath));
        string version = targetManifest.Game.Version;
        string immutableRoot = $"https://github.com/RoveHD/reborn-update/releases/download/v{version}/";
        var previousManifest = UpdateManifest.Parse(File.ReadAllText(Path.Combine(previousRelease, "update.json")));
        string previousVersion = previousManifest.Game.Version;
        var delta = targetManifest.GameHotfixesV2.Single(h => h.FromVersion == previousVersion && h.Version == version);
        string publicDelta = delta.Url;
        if (delta.Version != version || !delta.Url.StartsWith(immutableRoot, StringComparison.Ordinal))
            throw new Exception("The candidate manifest does not contain the expected immutable public delta.");
        if (File.ReadAllText(Path.Combine(baselineRoot, "version.txt")).Trim() != previousVersion)
            throw new Exception("The baseline fixture is not the exact delta base version.");

        string mode = publicDownload ? "public" : "local";
        string reportName = $"release-update-delta-{mode}-from-{previousVersion}.json";
        foreach (string reportRoot in new[] { output, release }.Distinct(StringComparer.OrdinalIgnoreCase))
            File.Delete(SafePaths.Under(reportRoot, reportName)); // A failed rerun cannot retain a prior success.
        string candidateManifestHash = VerifiedDownload.Hash(manifestPath);
        string targetInventoryHash = VerifiedDownload.Hash(inventoryPath);
        string previousInventoryHash = VerifiedDownload.Hash(previousInventoryPath);
        string deltaPath = SafePaths.Under(release, Path.GetFileName(new Uri(publicDelta).AbsolutePath));
        if (!publicDownload && (!File.Exists(deltaPath) || new FileInfo(deltaPath).Length != delta.Size ||
            !VerifiedDownload.Hash(deltaPath).Equals(delta.Sha256, StringComparison.OrdinalIgnoreCase)))
            throw new Exception("Local candidate delta does not match its manifest.");

        string previousInstallReport = Path.Combine(previousRelease, "release-install.json");
        if (!File.Exists(previousInstallReport))
            previousInstallReport = Path.Combine(Path.GetDirectoryName(baselineRoot)!, "release-install.json");
        if (!File.Exists(previousInstallReport))
            previousInstallReport = Path.Combine(Path.GetDirectoryName(previousRelease)!, $"release-game-{previousVersion}-checks", "release-install.json");
        if (!File.Exists(previousInstallReport)) throw new FileNotFoundException("The baseline install report is missing.", previousInstallReport);
        using var baselineReport = JsonDocument.Parse(File.ReadAllText(previousInstallReport));
        var baselineProof = baselineReport.RootElement;
        if (!baselineProof.GetProperty("success").GetBoolean() || baselineProof.GetProperty("version").GetString() != previousVersion ||
            !string.Equals(baselineProof.GetProperty("archiveSha256").GetString(), previousManifest.Game.Sha256, StringComparison.OrdinalIgnoreCase))
            throw new Exception("The baseline install report does not match its release manifest.");
        string reportedBaseline = baselineProof.GetProperty("isolatedInstall").GetString()!;
        if (!Path.GetFullPath(reportedBaseline).Equals(Path.GetFullPath(baselineRoot), StringComparison.OrdinalIgnoreCase))
            throw new Exception("The supplied baseline is not the isolated install recorded by its release proof.");
        VerifyInventory(baselineRoot, previousInventoryPath, $"installed {previousVersion} baseline");

        string fixture = Path.Combine(output, "game-delta-update-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(fixture);
        if (!fixture.StartsWith(output + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new Exception("Refuse to create the delta-update fixture outside the requested test-output directory.");
        CloneWithHardLinks(baselineRoot, fixture, previousInventoryPath);
        string gameMod = "patch/Mods/FriendsOwnMod.rb";
        var runId = Path.GetFileName(fixture);
        var sentinels = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["mkxp.json"] = $"unique {mode} delta display settings " + runId,
            ["settings.json"] = $"unique {mode} delta launcher settings " + runId,
            ["Save Data/Game_3.rxdata"] = $"unique {mode} delta save sentinel " + runId,
            [gameMod] = $"# unique {mode} delta friend mod " + runId
        };
        foreach (var item in sentinels)
        {
            string path = SafePaths.Under(fixture, item.Key);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            if (File.Exists(path)) File.Delete(path); // Break any link before writing test-only user data.
            File.WriteAllText(path, item.Value);
        }

        var settings = new LauncherSettings { InstallDirectory = fixture, ManifestUrl = ManifestUrl };
        var requested = new ConcurrentQueue<string>();
        using var handler = new CandidateChannelHandler(release, ManifestUrl, publicDelta, publicDownload, requested);
        using var service = new UpdateService(new VerifiedDownload(handler));
        var progress = new Reporter();
        var personalHashes = sentinels.Keys.ToDictionary(p => p, p => VerifiedDownload.Hash(SafePaths.Under(fixture, p)), StringComparer.OrdinalIgnoreCase);
        var selection = service.GetVersionSelection(settings);
        if (selection.ActiveVersion != previousVersion || selection.IsPreviousActive)
            throw new Exception("Public delta fixture did not begin at its declared base version.");

        var remoteManifest = await service.GetManifestAsync(settings, CancellationToken.None);
        if (remoteManifest.Game.Version != version || !remoteManifest.Game.Sha256.Equals(targetManifest.Game.Sha256, StringComparison.OrdinalIgnoreCase))
            throw new Exception("The update channel does not match the candidate full-package proof.");
        if (remoteManifest.Game.Url != targetManifest.Game.Url || remoteManifest.Game.Size != targetManifest.Game.Size)
            throw new Exception("The update channel full-package URL or size differs from the candidate.");
        var publicHotfix = remoteManifest.GameHotfixesV2.Single(h => h.FromVersion == previousVersion && h.Version == version);
        if (JsonSerializer.Serialize(publicHotfix) != JsonSerializer.Serialize(delta))
            throw new Exception("The published delta does not match the candidate's verified delta proof.");

        await service.DownloadAndInstallAsync(settings, version, progress, CancellationToken.None);
        if (!requested.Contains(ManifestUrl) || !requested.Contains(publicDelta))
            throw new Exception("The real UpdateService did not request both the public channel manifest and its immutable delta asset.");
        if (requested.Contains(remoteManifest.Game.Url))
            throw new Exception("UpdateService fell back to downloading the full game package instead of using the exact-base delta.");
        selection = service.GetVersionSelection(settings);
        if (selection.ActiveVersion != version || selection.OtherVersion != previousVersion || selection.IsPreviousActive)
            throw new Exception("The installed delta did not select the target version with the prior version available for switching.");
        int targetFiles = VerifyInventory(fixture, inventoryPath, $"installed {version} delta");
        VerifyPersonal();
        foreach (var file in delta.Files.Where(f => f.Delete))
            if (File.Exists(SafePaths.Under(fixture, file.Path))) throw new Exception("Delta removal remains installed: " + file.Path);

        await service.SwitchVersionAsync(settings, progress, CancellationToken.None);
        selection = service.GetVersionSelection(settings);
        if (selection.ActiveVersion != previousVersion || !selection.IsPreviousActive)
            throw new Exception("Switching back did not select the pre-update release.");
        VerifyInventory(fixture, previousInventoryPath, $"switched-back {previousVersion}");
        foreach (var file in delta.Files.Where(f => !f.BeforeExists))
            if (File.Exists(SafePaths.Under(fixture, file.Path))) throw new Exception("Target-only path remains after switching back: " + file.Path);
        VerifyPersonal();
        await service.SwitchVersionAsync(settings, progress, CancellationToken.None);
        selection = service.GetVersionSelection(settings);
        if (selection.ActiveVersion != version || selection.IsPreviousActive)
            throw new Exception("Switching forward did not restore the target release.");
        VerifyInventory(fixture, inventoryPath, $"switched-forward {version}");
        foreach (var file in delta.Files.Where(f => f.Delete))
            if (File.Exists(SafePaths.Under(fixture, file.Path))) throw new Exception("Deleted path returned after switching forward: " + file.Path);
        VerifyPersonal();

        if (File.Exists(Path.Combine(fixture, ".reborn-updater/game-transaction.json")))
            throw new Exception("An unfinished update transaction remains.");
        // Hard-linked baseline files must remain byte-for-byte unchanged by the test install.
        VerifyInventory(baselineRoot, previousInventoryPath, $"unchanged source {previousVersion} baseline");
        if (VerifiedDownload.Hash(manifestPath) != candidateManifestHash || VerifiedDownload.Hash(inventoryPath) != targetInventoryHash ||
            VerifiedDownload.Hash(previousInventoryPath) != previousInventoryHash)
            throw new Exception("Candidate or baseline metadata changed during the check.");
        var result = new
        {
            success = true,
            proofMode = mode,
            realUpdateServiceUsed = true,
            updateServiceUsedPublicChannel = publicDownload && requested.Contains(ManifestUrl),
            publicManifestUrl = publicDownload ? ManifestUrl : null,
            publicDeltaUrl = publicDownload ? publicDelta : null,
            candidateManifestUrl = ManifestUrl,
            candidateDeltaUrl = publicDelta,
            candidateManifestSha256 = candidateManifestHash,
            targetInventorySha256 = targetInventoryHash,
            previousInventorySha256 = previousInventoryHash,
            archiveSha256 = targetManifest.Game.Sha256,
            archiveSize = targetManifest.Game.Size,
            requestedUrls = requested.ToArray(),
            fullPackageDownloaded = false,
            deltaSha256 = delta.Sha256,
            deltaSize = delta.Size,
            version,
            previousVersion,
            installedTargetFilesVerified = targetFiles,
            previousFilesVerifiedAfterSwitch = JsonDocument.Parse(File.ReadAllText(previousInventoryPath)).RootElement.EnumerateObject().Count(),
            personalSentinelsPreserved = personalHashes.Keys,
            deletedPathsVerified = delta.Files.Count(f => f.Delete),
            addedPathsVerifiedAfterSwitchBack = delta.Files.Count(f => !f.BeforeExists),
            baselineFixtureUnchanged = true,
            switchesAfterDownload = 2,
            previousReleaseDirectory = previousRelease,
            baselineInstallRoot = baselineRoot,
            isolatedInstall = fixture,
            gameStarted = false
        };
        string resultPath = Path.Combine(output, reportName);
        string resultJson = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(resultPath, resultJson);
        File.WriteAllText(Path.Combine(release, reportName), resultJson);
        Console.WriteLine(JsonSerializer.Serialize(result));

        void VerifyPersonal()
        {
            foreach (var item in personalHashes)
                if (!VerifiedDownload.Hash(SafePaths.Under(fixture, item.Key)).Equals(item.Value, StringComparison.OrdinalIgnoreCase))
                    throw new Exception("Personal sentinel changed during delta update or version switch: " + item.Key);
        }
    }

    private static int VerifyInventory(string root, string inventoryPath, string label)
    {
        using var inventory = JsonDocument.Parse(File.ReadAllText(inventoryPath));
        int count = 0;
        foreach (var file in inventory.RootElement.EnumerateObject())
        {
            string path = SafePaths.Under(root, file.Name);
            long size = file.Value.GetProperty("size").GetInt64();
            string hash = file.Value.GetProperty("sha256").GetString()!;
            if (!File.Exists(path) || new FileInfo(path).Length != size ||
                !VerifiedDownload.Hash(path).Equals(hash, StringComparison.OrdinalIgnoreCase))
                throw new Exception($"{label} inventory mismatch: {file.Name}");
            count++;
        }
        return count;
    }

    private static void CloneWithHardLinks(string source, string destination, string inventoryPath)
    {
        using var inventory = JsonDocument.Parse(File.ReadAllText(inventoryPath));
        var managed = inventory.RootElement.EnumerateObject().Select(file => file.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var file in inventory.RootElement.EnumerateObject())
        {
            string relative = SafePaths.ValidateRelative(file.Name);
            string from = SafePaths.Under(source, relative);
            string to = SafePaths.Under(destination, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(to)!);
            if (!File.Exists(from) || new FileInfo(from).Length != file.Value.GetProperty("size").GetInt64() ||
                !VerifiedDownload.Hash(from).Equals(file.Value.GetProperty("sha256").GetString(), StringComparison.OrdinalIgnoreCase))
                throw new Exception("Baseline file changed while cloning: " + relative);
            if (!NativeHardLink.Create(to, from, IntPtr.Zero)) throw new IOException("Could not create isolated hard link for " + relative, new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error()));
        }
        foreach (string relative in new[] { "version.txt", "mkxp.json", "settings.json", "Save Data/Game_3.rxdata", "patch/Mods/FriendsOwnMod.rb" })
        {
            string from = SafePaths.Under(source, relative);
            if (!File.Exists(from) || managed.Contains(relative)) continue;
            string to = SafePaths.Under(destination, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(to)!);
            File.Copy(from, to);
        }
    }

    private sealed class CandidateChannelHandler(string release, string manifestUrl, string deltaUrl, bool publicDownload, ConcurrentQueue<string> requested)
        : DelegatingHandler(new HttpClientHandler { AutomaticDecompression = DecompressionMethods.None })
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            string url = request.RequestUri?.AbsoluteUri ?? throw new InvalidOperationException("Request URL is missing.");
            if (url != manifestUrl && url != deltaUrl) throw new InvalidOperationException("Blocked unexpected update URL: " + url);
            requested.Enqueue(url);
            if (publicDownload) return await base.SendAsync(request, cancellationToken);
            // Only HTTP delivery is replaced. Download hashing, delta validation, installation and switching use production services.
            string path = url == manifestUrl ? Path.Combine(release, "update.json") :
                SafePaths.Under(release, Path.GetFileName(new Uri(deltaUrl).AbsolutePath));
            var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 131072, FileOptions.Asynchronous | FileOptions.SequentialScan);
            var response = new HttpResponseMessage(HttpStatusCode.OK) { RequestMessage = request, Content = new StreamContent(stream) };
            response.Content.Headers.ContentLength = stream.Length;
            return response;
        }
    }

    private sealed class Reporter : IProgress<UpdateProgress>
    {
        private DateTime last = DateTime.MinValue;
        public void Report(UpdateProgress value)
        {
            if ((DateTime.UtcNow - last).TotalSeconds > 5)
            {
                Console.WriteLine($"{value.Stage}: {value.Percent:0}% {value.Message}");
                last = DateTime.UtcNow;
            }
        }
    }

    private static class NativeHardLink
    {
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true, EntryPoint = "CreateHardLinkW")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool Create(string newFileName, string existingFileName, IntPtr securityAttributes);
    }
}
