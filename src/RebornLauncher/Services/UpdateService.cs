using System.IO;

namespace PokemonRebornLauncher.Services;

public sealed class UpdateService : IDisposable
{
    private readonly VerifiedDownload download;
    public UpdateService() : this(new VerifiedDownload()) { }
    public UpdateService(VerifiedDownload download) => this.download = download;
    public async Task<UpdateManifest> GetManifestAsync(LauncherSettings settings, CancellationToken cancellationToken) => UpdateManifest.Parse(await download.TextAsync(settings.ManifestUrl, cancellationToken));
    public async Task<string> ReadLocalVersionAsync(LauncherSettings settings, CancellationToken cancellationToken)
    {
        string directory = ValidateInstallDirectory(settings);
        var installer = new GameInstaller(directory);
        using (installer.AcquireLock())
        {
            if (File.Exists(Path.Combine(directory, ".reborn-updater", "game-transaction.json")))
            {
                GameInstaller.EnsureGameStopped(directory, settings.GameExecutable);
            }
            await Task.Run(installer.Recover, cancellationToken);
        }
        string path = settings.ResolveLocalVersionPath();
        if (!File.Exists(path)) return "0.0.0";
        string text = (await File.ReadAllTextAsync(path, cancellationToken)).Trim().TrimStart('v', 'V');
        ReleaseVersion.Parse(text);
        return text;
    }
    public VersionSelection GetVersionSelection(LauncherSettings settings)
    {
        string directory = ValidateInstallDirectory(settings);
        var installer = new GameInstaller(directory);
        using var installLock = installer.AcquireLock();
        if (File.Exists(Path.Combine(directory, ".reborn-updater", "game-transaction.json")))
            throw new InvalidOperationException("Bitte zuerst die unterbrochene Installation prüfen oder den Launcher neu öffnen.");
        return installer.GetVersionSelection(settings.ResolveLocalVersionPath());
    }
    public async Task SwitchVersionAsync(LauncherSettings settings, IProgress<UpdateProgress> progress, CancellationToken cancellationToken)
    {
        string directory = ValidateInstallDirectory(settings);
        RequireExistingGame(directory, settings);
        var installer = new GameInstaller(directory);
        using var installLock = installer.AcquireLock();
        GameInstaller.EnsureGameStopped(directory, settings.GameExecutable);
        await Task.Run(installer.Recover, cancellationToken);
        await Task.Run(() => installer.SwitchVersion(settings.ResolveLocalVersionPath(), progress, cancellationToken,
            beforeMutation: () => GameInstaller.EnsureGameStopped(directory, settings.GameExecutable)), cancellationToken);
        var selection = installer.GetVersionSelection(settings.ResolveLocalVersionPath());
        progress.Report(new(UpdateStage.Complete, 100, 0, null, 0, $"Reborn {selection.ActiveVersion} ist ausgewählt."));
    }
    public async Task<string> GetRemoteVersionAsync(LauncherSettings settings, CancellationToken cancellationToken) => (await GetManifestAsync(settings, cancellationToken)).Game.Version;
    public async Task<string> GetPatchNotesAsync(LauncherSettings settings, CancellationToken cancellationToken) => (await GetManifestAsync(settings, cancellationToken)).PatchNotes.Trim();
    public async Task DownloadAndInstallAsync(LauncherSettings settings, string remoteVersion, IProgress<UpdateProgress> progress, CancellationToken cancellationToken)
    {
        var manifest = await GetManifestAsync(settings, cancellationToken);
        if (ReleaseVersion.Parse(remoteVersion).CompareTo(ReleaseVersion.Parse(manifest.Game.Version)) != 0)
            throw new InvalidOperationException("Das Update wurde inzwischen geändert. Bitte erneut prüfen.");
        string directory = ValidateInstallDirectory(settings);
        RequireExistingGame(directory, settings);
        var installer = new GameInstaller(directory);
        using var installLock = installer.AcquireLock();
        GameInstaller.EnsureGameStopped(directory, settings.GameExecutable);
        await Task.Run(installer.Recover, cancellationToken);
        string versionPath = settings.ResolveLocalVersionPath();
        string localVersion = File.Exists(versionPath) ? await File.ReadAllTextAsync(versionPath, cancellationToken) : "0.0.0";
        if (ReleaseVersion.Parse(localVersion).CompareTo(ReleaseVersion.Parse(manifest.Game.Version)) > 0)
            throw new InvalidOperationException("Eine neuere Reborn-Version ist bereits installiert. Eine Rückstufung wird nicht durchgeführt.");
        GameHotfix? hotfix = null;
        // Same-release repair always uses the full package. A delta is safe only
        // for its exact declared base, including presence/absence of every path.
        if (ReleaseVersion.Parse(localVersion).CompareTo(ReleaseVersion.Parse(manifest.Game.Version)) < 0)
        {
            var candidate = manifest.GameHotfixesV2.Concat(manifest.GameHotfixes)
                .FirstOrDefault(h => h.FromVersion.Equals(localVersion.Trim(), StringComparison.Ordinal));
            if (candidate is not null)
            {
                if (!await Task.Run(() => candidate.MatchesBase(directory, versionPath, cancellationToken), cancellationToken))
                    throw new InvalidOperationException("Die Spieldateien passen nicht zur Hotfix-Ausgangsversion. Eine Datei wurde verändert, fehlt oder belegt einen neuen Hotfix-Pfad. Bitte eigene Dateien außerhalb des Spielordners sichern und den Konflikt prüfen. Es wurden keine Spieldateien geändert.");
                hotfix = candidate;
            }
        }
        UpdateAsset asset = hotfix?.Asset ?? manifest.Game;
        IProgress<UpdateProgress> selectedProgress = hotfix is null ? progress : new HotfixProgress(progress);
        string stage = installer.CreateStage();
        try
        {
            selectedProgress.Report(new(UpdateStage.Checking, null, 0, null, 0, "Freier Speicher für Download und Sicherung wird geprüft …"));
            var drive = new DriveInfo(Path.GetPathRoot(directory)!);
            long requiredSpace = hotfix?.RequiredSpace(directory) ?? checked(asset.Size * 3);
            if (drive.AvailableFreeSpace < requiredSpace) throw new IOException("Zu wenig freier Speicher für Download und Sicherung.");
            string archive = Path.Combine(stage, "game.zip");
            await download.DownloadAsync(asset, archive, selectedProgress, cancellationToken);
            string payload = await Task.Run(() => hotfix is null
                ? SafePaths.Extract(archive, Path.Combine(stage, "payload"), selectedProgress, cancellationToken)
                : hotfix.ExtractVerified(archive, Path.Combine(stage, "payload"), selectedProgress, cancellationToken), cancellationToken);
            if (hotfix is null && !File.Exists(SafePaths.Under(payload, settings.GameExecutable))) throw new InvalidDataException("Das Update enthält keine Game.exe im Spielordner.");
            GameInstaller.EnsureGameStopped(directory, settings.GameExecutable);
            await Task.Run(() => installer.Install(payload, versionPath, manifest.Game.Version,
                hotfix is null ? manifest.Game.RemovedFiles : hotfix.Removals, selectedProgress, cancellationToken, beforeMutation: () =>
                {
                    GameInstaller.EnsureGameStopped(directory, settings.GameExecutable);
                    if (hotfix is not null && !hotfix.MatchesBase(directory, versionPath, cancellationToken))
                        throw new InvalidOperationException("Die Hotfix-Ausgangsdateien wurden während des Updates geändert. Bitte erneut prüfen.");
                }), cancellationToken);
            selectedProgress.Report(new(UpdateStage.Complete, 100, 0, null, 0, $"Reborn {manifest.Game.Version} ist installiert."));
        }
        finally { await Task.Run(() => GameInstaller.TryClean(stage)); }
    }
    public static void EnsureSafeToLaunch(LauncherSettings settings)
    {
        string directory = ValidateInstallDirectory(settings);
        var installer = new GameInstaller(directory);
        using var installLock = installer.AcquireLock();
        if (File.Exists(Path.Combine(directory, ".reborn-updater", "game-transaction.json")))
        {
            GameInstaller.EnsureGameStopped(directory, settings.GameExecutable);
            installer.Recover();
        }
    }
    private static void RequireExistingGame(string directory, LauncherSettings settings)
    {
        foreach (var file in new[] { settings.GameExecutable, "Game.ini", "mkxp.json" })
            if (!File.Exists(SafePaths.Under(directory, file)))
                throw new InvalidOperationException("Bitte den bestehenden Reborn-Spielordner auswählen. Dort müssen Game.exe, Game.ini und mkxp.json vorhanden sein. Der Updater ergänzt keine Erstinstallation.");
    }
    public static string ValidateInstallDirectory(LauncherSettings settings)
    {
        string directory = settings.ResolveInstallDirectory();
        SafePaths.NoLinks(directory);
        SafePaths.ValidateRelative(settings.GameExecutable);
        if (!Path.GetFileName(settings.GameExecutable).Equals("Game.exe", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Als Spielprogramm muss Game.exe ausgewählt sein.");
        SafePaths.Under(directory, settings.LocalVersionFile);
        if (Directory.Exists(directory) && Directory.EnumerateFileSystemEntries(directory).Any() &&
            !File.Exists(SafePaths.Under(directory, settings.GameExecutable)) &&
            !File.Exists(Path.Combine(directory, "launcher.exe")) &&
            !File.Exists(Path.Combine(directory, "Pokemon Reborn Launcher.exe")) &&
            !directory.Equals(Path.GetFullPath(AppContext.BaseDirectory).TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Der ausgewählte Ordner enthält keine Reborn-Installation. Bitte den vorhandenen Spielordner auswählen.");
        return directory;
    }
    public static bool IsDifferentVersion(string localVersion, string remoteVersion) => ReleaseVersion.Parse(remoteVersion).CompareTo(ReleaseVersion.Parse(localVersion)) > 0;
    public void Dispose() => download.Dispose();
    private sealed class HotfixProgress(IProgress<UpdateProgress> target) : IProgress<UpdateProgress>
    {
        public void Report(UpdateProgress value) => target.Report(value with { Message = "Hotfix: " + value.Message });
    }
}
