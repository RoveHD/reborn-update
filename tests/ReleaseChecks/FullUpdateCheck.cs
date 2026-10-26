using System.Collections.Concurrent;
using System.Net;
using System.Text.Json;
using PokemonRebornLauncher.Services;

internal static class FullUpdateCheck
{
    private const string Repository = "RoveHD/reborn-update";
    private const string Version = "7.3.12";
    private const string PreviousVersion = "7.3.11";
    private const string SaveSentinel = "unique full update friend save sentinel";

    public static async Task Run(string release, string output, string previousRelease, bool publicDownload)
    {
        foreach (string path in new[] { release, output, previousRelease })
            if (!string.Equals(Path.GetPathRoot(path), "D:\\", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Full update checks may only use D: test copies.");

        string manifestPath = Path.Combine(release, "update.json");
        string inventoryPath = Path.Combine(release, "game-files.json");
        string oldInventoryPath = Path.Combine(previousRelease, "game-files.json");
        string previousManifestPath = Path.Combine(previousRelease, "update.json");
        if (!File.Exists(manifestPath) || !File.Exists(inventoryPath) || !File.Exists(oldInventoryPath) || !File.Exists(previousManifestPath))
            throw new FileNotFoundException("Current and previous release inventories and update.json are required.");
        var localManifest = UpdateManifest.Parse(File.ReadAllText(manifestPath));
        var previousManifest = UpdateManifest.Parse(File.ReadAllText(previousManifestPath));
        string immutableRoot = $"https://github.com/{Repository}/releases/download/v{Version}/";
        string immutableManifestUrl = immutableRoot + "update.json";
        string expectedGameUrl = immutableRoot + "Reborn-AllGen-0.9.9.zip";
        if (localManifest.Game.Version != Version || localManifest.Game.Url != expectedGameUrl)
            throw new Exception("The release manifest is not the immutable 7.3.12 full-package manifest.");
        if (previousManifest.Game.Version != PreviousVersion)
            throw new Exception("The previous release manifest must describe 7.3.11.");
        string previousArchive = SafePaths.Under(previousRelease, Path.GetFileName(new Uri(previousManifest.Game.Url).AbsolutePath));
        if (!VerifiedDownload.Hash(previousArchive).Equals(previousManifest.Game.Sha256, StringComparison.OrdinalIgnoreCase))
            throw new Exception("Previous 7.3.11 archive differs from its old manifest.");

        string fixture = Path.Combine(output, "game-full-update-" + Guid.NewGuid().ToString("N"));
        string root = Path.GetFullPath(SafePaths.Extract(previousArchive, fixture, new Reporter(), CancellationToken.None));
        if (!fixture.StartsWith(output + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
            !System.Text.RegularExpressions.Regex.IsMatch(Path.GetFileName(fixture), "^game-full-update-[0-9a-f]{32}$", System.Text.RegularExpressions.RegexOptions.IgnoreCase) ||
            (!root.Equals(fixture, StringComparison.OrdinalIgnoreCase) && !root.StartsWith(fixture + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)))
            throw new Exception("Refuse to update outside the unique isolated full-update fixture.");
        if (!File.Exists(Path.Combine(root, "Game.exe")) || !File.Exists(Path.Combine(root, "Game.ini")))
            throw new Exception("Extracted previous release is not a complete Reborn game.");
        File.WriteAllText(Path.Combine(root, "version.txt"), PreviousVersion);
        string gameMod = "patch/Mods/FriendsOwnFullUpdateMod.rb";
        var runId = Path.GetFileName(fixture);
        var sentinels = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["mkxp.json"] = "unique display settings " + runId,
            ["settings.json"] = "unique launcher settings " + runId,
            ["Save Data/Game_3.rxdata"] = SaveSentinel + " " + runId,
            [gameMod] = "# unique friend mod " + runId
        };
        foreach (var item in sentinels)
        {
            string path = SafePaths.Under(root, item.Key);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, item.Value);
        }

        var settings = new LauncherSettings { InstallDirectory = root, ManifestUrl = immutableManifestUrl };
        var urls = new ConcurrentQueue<string>();
        using var handler = new ExactReleaseHandler(release, immutableManifestUrl, expectedGameUrl, publicDownload, urls);
        using var service = new UpdateService(new VerifiedDownload(handler));
        var progress = new Reporter();
        var personalPaths = new[] { "mkxp.json", "settings.json", "Save Data/Game_3.rxdata", gameMod };
        var personalHashes = personalPaths.ToDictionary(p => p, p => VerifiedDownload.Hash(SafePaths.Under(root, p)), StringComparer.OrdinalIgnoreCase);
        if (!File.Exists(SafePaths.Under(root, gameMod))) throw new Exception("Generated full-update friend-mod sentinel is missing.");
        if (!Directory.Exists(Path.Combine(root, "Save Data"))) throw new Exception("Generated full-update save sentinel is missing.");

        var selection = service.GetVersionSelection(settings);
        if (selection.ActiveVersion != PreviousVersion || selection.IsPreviousActive)
            throw new Exception("Fresh full-update fixture did not start at active 7.3.11.");
        int oldFiles = VerifyInventory(root, oldInventoryPath, "previous 7.3.11");
        VerifyPersonal();

        await service.DownloadAndInstallAsync(settings, Version, progress, CancellationToken.None);
        if (!urls.Contains(immutableManifestUrl) || !urls.Contains(expectedGameUrl))
            throw new Exception("UpdateService did not request both immutable manifest and full game package URLs.");
        if (handler.GameRequests != 1) throw new Exception("The update did not download exactly one full game package.");
        selection = service.GetVersionSelection(settings);
        if (selection.ActiveVersion != Version || selection.OtherVersion != PreviousVersion || selection.IsPreviousActive)
            throw new Exception("Update did not select 7.3.12 with 7.3.11 as its previous version.");
        VerifyManifest(await service.GetManifestAsync(settings, CancellationToken.None));
        int currentFiles = VerifyInventory(root, inventoryPath, "downloaded full 7.3.12 package");
        VerifyPersonal();
        var newMusicPaths = GetRequiredNewMusicPaths(inventoryPath, oldInventoryPath);

        await service.SwitchVersionAsync(settings, progress, CancellationToken.None);
        selection = service.GetVersionSelection(settings);
        if (selection.ActiveVersion != PreviousVersion || !selection.IsPreviousActive)
            throw new Exception("Post-download switch did not restore previous 7.3.11.");
        VerifyInventory(root, oldInventoryPath, "post-download 7.3.11");
        foreach (string path in newMusicPaths)
            if (File.Exists(SafePaths.Under(root, path))) throw new Exception("New music asset remains after switching to 7.3.11: " + path);
        VerifyPersonal();
        await service.SwitchVersionAsync(settings, progress, CancellationToken.None);
        selection = service.GetVersionSelection(settings);
        if (selection.ActiveVersion != Version || selection.IsPreviousActive)
            throw new Exception("Post-download switch did not restore current 7.3.12.");
        VerifyInventory(root, inventoryPath, "post-download 7.3.12");
        VerifyPersonal();

        var result = new
        {
            success = true,
            realPublicGithubDownload = publicDownload,
            requestedURLs = urls.ToArray(),
            fullPackageDownloaded = handler.GameRequests == 1,
            archiveSha256 = localManifest.Game.Sha256,
            archiveSize = localManifest.Game.Size,
            version = Version,
            previousVersion = PreviousVersion,
            previousInventoryFilesVerified = oldFiles,
            currentInventoryFilesVerified = currentFiles,
            personalSentinelsPreserved = personalHashes.Keys,
            switchesBeforeDownload = 0,
            switchesAfterDownload = 2,
            isolatedInstall = root,
            gameStarted = false
        };
        string resultPath = Path.Combine(output, publicDownload ? "release-update-public.json" : "release-update-local.json");
        File.WriteAllText(resultPath, JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine(JsonSerializer.Serialize(result));

        void VerifyPersonal()
        {
            foreach (var item in personalHashes)
                if (VerifiedDownload.Hash(SafePaths.Under(root, item.Key)) != item.Value)
                    throw new Exception("Personal sentinel changed: " + item.Key);
        }

        void VerifyManifest(UpdateManifest actual)
        {
            if (actual.Game.Version != localManifest.Game.Version || actual.Game.Url != localManifest.Game.Url ||
                actual.Game.Size != localManifest.Game.Size || !actual.Game.Sha256.Equals(localManifest.Game.Sha256, StringComparison.OrdinalIgnoreCase))
                throw new Exception("Immutable manifest response differs from the local release proof.");
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

    private static string[] GetRequiredNewMusicPaths(string currentInventoryPath, string previousInventoryPath)
    {
        using var current = JsonDocument.Parse(File.ReadAllText(currentInventoryPath));
        using var previous = JsonDocument.Parse(File.ReadAllText(previousInventoryPath));
        var previousPaths = previous.RootElement.EnumerateObject().Select(p => p.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var additions = current.RootElement.EnumerateObject().Select(p => p.Name)
            .Where(p => !previousPaths.Contains(p)).ToArray();
        var requiredNames = new[] { "887s.png", "zzLegendaryWildMusic.rb" };
        var result = requiredNames.Select(name => additions.SingleOrDefault(p =>
            Path.GetFileName(p).Equals(name, StringComparison.OrdinalIgnoreCase)) ??
            throw new Exception("Expected new music file is absent from the 7.3.12 inventory: " + name)).ToArray();
        return result;
    }

    private sealed class ExactReleaseHandler : DelegatingHandler
    {
        private readonly string release;
        private readonly string manifestUrl;
        private readonly string gameUrl;
        private readonly bool usePublicNetwork;
        private readonly ConcurrentQueue<string> requested;
        private int gameRequests;
        public int GameRequests => Volatile.Read(ref gameRequests);

        public ExactReleaseHandler(string release, string manifestUrl, string gameUrl, bool usePublicNetwork, ConcurrentQueue<string> requested)
            : base(new HttpClientHandler { AutomaticDecompression = DecompressionMethods.None })
        {
            this.release = release;
            this.manifestUrl = manifestUrl;
            this.gameUrl = gameUrl;
            this.usePublicNetwork = usePublicNetwork;
            this.requested = requested;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            string url = request.RequestUri?.AbsoluteUri ?? throw new InvalidOperationException("Request URL is missing.");
            if (url != manifestUrl && url != gameUrl) throw new InvalidOperationException("Blocked unexpected update URL: " + url);
            requested.Enqueue(url);
            if (url == gameUrl) Interlocked.Increment(ref gameRequests);
            if (usePublicNetwork) return await base.SendAsync(request, cancellationToken);

            string file = url == manifestUrl ? Path.Combine(release, "update.json") : Path.Combine(release, "Reborn-AllGen-0.9.9.zip");
            var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read, 131072, FileOptions.Asynchronous | FileOptions.SequentialScan);
            var response = new HttpResponseMessage(HttpStatusCode.OK) { RequestMessage = request, Content = new StreamContent(stream) };
            response.Content.Headers.ContentLength = stream.Length;
            if (url == manifestUrl) response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
            return response;
        }

    }

    private sealed class Reporter : IProgress<UpdateProgress>
    {
        private DateTime last = DateTime.MinValue;
        public void Report(UpdateProgress value)
        {
            if ((DateTime.UtcNow - last).TotalSeconds > 10)
            {
                Console.WriteLine($"{value.Stage}: {value.Percent:0}% {value.Message}");
                last = DateTime.UtcNow;
            }
        }
    }
}
