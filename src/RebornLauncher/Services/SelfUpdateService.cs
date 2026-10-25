using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text.Json;

namespace PokemonRebornLauncher.Services;

public sealed class SelfUpdateService
{
    public const string HelperArgument = "--apply-launcher-update";
    public static bool ContinueAfterSelfUpdate => Environment.GetCommandLineArgs().Contains("--continue-update");
    public static string? StartupFailure { get; private set; }
    public sealed class Request
    {
        public string Target { get; set; } = "";
        public string Candidate { get; set; } = "";
        public string Sha256 { get; set; } = "";
        public long Size { get; set; }
        public string PreviousSha256 { get; set; } = "";
        public int HelperPid { get; set; }
        public int ParentPid { get; set; }
        public long ParentStartedUtcTicks { get; set; }
        public string Phase { get; set; } = "prepared";
    }
    public static string CurrentVersion => Assembly.GetExecutingAssembly().GetName().Version is { } version ? $"{version.Major}.{version.Minor}.{version.Build}" : "7.0.0";
    public async Task<bool> CheckAndApplyAsync(LauncherSettings settings, IProgress<UpdateProgress> progress, CancellationToken ct)
    {
        if (StartupFailure is not null) throw new InvalidOperationException(StartupFailure);
        using var client = new VerifiedDownload();
        progress.Report(new(UpdateStage.Checking, null, 0, null, 0, "Launcher wird auf Updates geprüft …"));
        var manifest = UpdateManifest.Parse(await client.TextAsync(settings.ManifestUrl, ct));
        if (ReleaseVersion.Parse(manifest.Launcher.Version).CompareTo(ReleaseVersion.Parse(CurrentVersion)) <= 0) return false;
        string target = Environment.ProcessPath ?? throw new InvalidOperationException("Der Launcherpfad konnte nicht ermittelt werden.");
        if (!Path.GetExtension(target).Equals(".exe", StringComparison.OrdinalIgnoreCase) || Path.GetFileName(target).Equals("dotnet.exe", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Ein Launcher-Update ist nur mit der veröffentlichten launcher.exe möglich.");
        SafePaths.NoLinks(target);
        string root = Path.GetDirectoryName(target)!;
        string state = SafePaths.Under(root, ".reborn-updater");
        Directory.CreateDirectory(state);
        using var updateLock = new FileStream(Path.Combine(state, "launcher.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        string transaction = SafePaths.Under(state, "self-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(transaction);
        string candidate = Path.Combine(transaction, "launcher.exe");
        bool launched = false;
        try
        {
            await client.DownloadAsync(manifest.Launcher, candidate, progress, ct);
            // A packaged Windows executable is required; never execute an HTML error page or ZIP.
            using (var stream = File.OpenRead(candidate))
                if (stream.ReadByte() != 'M' || stream.ReadByte() != 'Z') throw new InvalidDataException("Das Launcher-Update ist keine Windows-EXE.");
            using var current = Process.GetCurrentProcess();
            var request = new Request { Target = target, Candidate = candidate, Sha256 = manifest.Launcher.Sha256, Size = manifest.Launcher.Size, PreviousSha256 = VerifiedDownload.Hash(target), ParentPid = current.Id, ParentStartedUtcTicks = current.StartTime.ToUniversalTime().Ticks };
            string requestPath = Path.Combine(transaction, "request.json");
            GameInstaller.SaveJournal(requestPath, request);
            ct.ThrowIfCancellationRequested();
            var info = new ProcessStartInfo(candidate) { UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = transaction, WindowStyle = ProcessWindowStyle.Hidden };
            info.ArgumentList.Add(HelperArgument); info.ArgumentList.Add(requestPath);
            var helper = Process.Start(info) ?? throw new IOException("Der Updatehelfer konnte nicht gestartet werden.");
            helper.Dispose();
            launched = true;
            progress.Report(new(UpdateStage.Installing, null, 0, null, 0, "Der Launcher wird neu gestartet und aktualisiert danach Reborn …"));
            return true;
        }
        finally { if (!launched) GameInstaller.TryClean(transaction); }
    }
    public static async Task<bool> TryHandleArgumentsAsync(string[] args)
    {
        if (args.Length == 2 && args[0] == HelperArgument)
        {
            await RunHelperAsync(args[1]);
            return true;
        }
        if (args.Contains(HelperArgument)) throw new InvalidOperationException("Unvollständiger Aufruf des Updatehelfers.");
        if (args.Contains("--self-update-failed")) StartupFailure = "Das automatische Launcher-Update konnte nicht abgeschlossen werden. Die vorherige Version wurde wiederhergestellt. Details stehen in .reborn-updater/self-update-error.txt.";
        if (args.Length == 3 && args[0] == "--continue-update" && args[1] == "--self-update-complete")
        {
            // Only a restart of the installed target may acknowledge the pending handoff.
            string requestPath = Path.GetFullPath(args[2]);
            var request = ReadRequest(requestPath, requireHelper: false);
            if (!string.Equals(request.Target, Environment.ProcessPath, StringComparison.OrdinalIgnoreCase) || !VerifiedDownload.Hash(request.Target).Equals(request.Sha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Der Neustart des aktualisierten Launchers konnte nicht bestätigt werden.");
            request.Phase = "acknowledged";
            GameInstaller.SaveJournal(requestPath, request);
        }
        RecoverPendingUpdates();
        return false;
    }
    private static void RecoverPendingUpdates()
    {
        string target = Environment.ProcessPath ?? "";
        if (string.IsNullOrEmpty(target)) return;
        string state = Path.Combine(Path.GetDirectoryName(target)!, ".reborn-updater");
        SafePaths.NoLinks(state);
        if (!Directory.Exists(state)) return;
        foreach (string directory in Directory.EnumerateDirectories(state, "self-*"))
        {
            string path = Path.Combine(directory, "request.json");
            if (!File.Exists(path)) continue;
            var request = ReadRequest(path, requireHelper: false);
            if (!request.Target.Equals(target, StringComparison.OrdinalIgnoreCase)) continue;
            if (request.Phase is "acknowledged" or "restored" or "cancelled")
            {
                if (!ProcessIsAlive(request.HelperPid)) GameInstaller.TryClean(directory);
                continue;
            }
            if (request.Phase == "prepared" && ProcessIsAlive(request.ParentPid, request.ParentStartedUtcTicks)) continue;
            string hash = VerifiedDownload.Hash(target);
            if (request.Phase is "installed" or "replacing")
            {
                if (hash.Equals(request.Sha256, StringComparison.OrdinalIgnoreCase))
                {
                    // Successfully running the installed EXE also recovers a lost restart/acknowledgement.
                    request.Phase = "acknowledged";
                    GameInstaller.SaveJournal(path, request);
                }
                else if (hash.Equals(request.PreviousSha256, StringComparison.OrdinalIgnoreCase))
                {
                    request.Phase = "restored";
                    GameInstaller.SaveJournal(path, request);
                }
                else throw new InvalidDataException("Ein unterbrochenes Launcher-Update konnte nicht zugeordnet werden. Die Sicherung launcher.exe.previous bleibt erhalten.");
            }
            else if (request.Phase == "prepared")
            {
                request.Phase = "cancelled";
                GameInstaller.SaveJournal(path, request);
                if (!ProcessIsAlive(request.HelperPid)) GameInstaller.TryClean(directory);
            }
        }
    }
    private static bool ProcessIsAlive(int pid, long startedTicks = 0)
    {
        if (pid <= 0) return false;
        try { using var process = Process.GetProcessById(pid); return !process.HasExited && (startedTicks == 0 || process.StartTime.ToUniversalTime().Ticks == startedTicks); }
        catch (ArgumentException) { return false; }
    }
    private static Request ReadRequest(string path, bool requireHelper)
    {
        SafePaths.NoLinks(path);
        var request = JsonSerializer.Deserialize<Request>(File.ReadAllText(path)) ?? throw new InvalidDataException("Ungültiger Launcher-Updateauftrag.");
        string transaction = Path.GetDirectoryName(Path.GetFullPath(path))!;
        string state = Path.GetDirectoryName(transaction)!;
        string root = Path.GetDirectoryName(state)!;
        if (!Path.GetFileName(transaction).StartsWith("self-", StringComparison.Ordinal) || Path.GetFileName(state) != ".reborn-updater" ||
            !Path.GetFullPath(request.Target).Equals(SafePaths.Under(root, Path.GetFileName(request.Target)), StringComparison.OrdinalIgnoreCase) ||
            !Path.GetExtension(request.Target).Equals(".exe", StringComparison.OrdinalIgnoreCase) ||
            !Path.GetFullPath(request.Candidate).Equals(Path.Combine(transaction, "launcher.exe"), StringComparison.OrdinalIgnoreCase) ||
            (requireHelper && !Path.GetFullPath(Environment.ProcessPath!).Equals(Path.GetFullPath(request.Candidate), StringComparison.OrdinalIgnoreCase)))
            throw new InvalidDataException("Der Launcher-Updateauftrag liegt außerhalb des vorgesehenen Ordners.");
        SafePaths.NoLinks(request.Target); SafePaths.NoLinks(request.Candidate);
        return request;
    }
    private static async Task RunHelperAsync(string path)
    {
        Request? request = null;
        Process? restartedProcess = null;
        try
        {
            request = ReadRequest(path, requireHelper: true);
            request.HelperPid = Environment.ProcessId;
            GameInstaller.SaveJournal(path, request);
            try
            {
                using var parent = Process.GetProcessById(request.ParentPid);
                if (parent.StartTime.ToUniversalTime().Ticks != request.ParentStartedUtcTicks || !string.Equals(parent.MainModule?.FileName, request.Target, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("Der ursprüngliche Launcherprozess stimmt nicht überein.");
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(90));
                await parent.WaitForExitAsync(timeout.Token);
            }
            catch (ArgumentException) { /* Parent already exited before helper startup. */ }
            string state = Path.GetDirectoryName(Path.GetDirectoryName(path))!;
            using var updateLock = new FileStream(Path.Combine(state, "launcher.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            ReplaceLauncher(request, path);
            updateLock.Dispose();
            var restart = new ProcessStartInfo(request.Target) { UseShellExecute = false, WorkingDirectory = Path.GetDirectoryName(request.Target)! };
            restart.ArgumentList.Add("--continue-update"); restart.ArgumentList.Add("--self-update-complete"); restart.ArgumentList.Add(Path.GetFullPath(path));
            var restarted = restartedProcess = Process.Start(restart) ?? throw new IOException("Der aktualisierte Launcher konnte nicht gestartet werden.");
            var deadline = Stopwatch.StartNew();
            bool acknowledged = false;
            while (deadline.Elapsed < TimeSpan.FromSeconds(30))
            {
                var saved = JsonSerializer.Deserialize<Request>(await File.ReadAllTextAsync(path));
                if (saved?.Phase == "acknowledged") { acknowledged = true; break; }
                if (restarted.HasExited) throw new IOException("Der neue Launcher wurde vor der Startbestätigung beendet.");
                await Task.Delay(150);
            }
            if (!acknowledged)
            {
                // Do not overwrite a new executable while it is still running. Next successful startup acknowledges it.
                string message = "Der neue Launcher läuft, hat seinen Start aber noch nicht bestätigt. Bitte das Fenster prüfen. Die vorherige EXE bleibt als launcher.exe.previous erhalten.";
                File.WriteAllText(Path.Combine(state, "self-update-error.txt"), message);
            }
        }
        catch (Exception ex)
        {
            if (request is not null)
            {
                try
                {
                    if (request.Phase is "replacing" or "installed")
                    {
                        // A live replacement may still be initializing; never restore over its mapped executable.
                        if (restartedProcess is not null && !restartedProcess.HasExited)
                        {
                            File.WriteAllText(Path.Combine(Path.GetDirectoryName(request.Target)!, ".reborn-updater", "self-update-error.txt"), ex.ToString());
                            return;
                        }
                        RestoreLauncher(request);
                    }
                    string state = Path.Combine(Path.GetDirectoryName(request.Target)!, ".reborn-updater");
                    File.WriteAllText(Path.Combine(state, "self-update-error.txt"), DateTimeOffset.Now + Environment.NewLine + ex);
                    if (File.Exists(request.Target))
                    {
                        var restart = new ProcessStartInfo(request.Target) { UseShellExecute = false, WorkingDirectory = Path.GetDirectoryName(request.Target)! };
                        restart.ArgumentList.Add("--self-update-failed"); Process.Start(restart)?.Dispose();
                    }
                }
                catch { /* Durable request and previous executable remain for manual recovery. */ }
            }
        }
        finally { restartedProcess?.Dispose(); }
    }
    // Kept independent of Process APIs so replacement/rollback can be tested against disposable files.
    public static void ReplaceLauncher(Request request, string requestPath)
    {
        if (new FileInfo(request.Candidate).Length != request.Size || !VerifiedDownload.Hash(request.Candidate).Equals(request.Sha256, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Launcher-Prüfsumme stimmt nicht.");
        if (!VerifiedDownload.Hash(request.Target).Equals(request.PreviousSha256, StringComparison.OrdinalIgnoreCase)) throw new IOException("Die vorhandene launcher.exe wurde inzwischen geändert.");
        string previous = request.Target + ".previous";
        string pending = request.Target + ".new";
        SafePaths.NoLinks(previous); SafePaths.NoLinks(pending); SafePaths.NoLinks(request.Target);
        // Keep the original executable at its real name until the atomic overwrite.
        CopyFlushed(request.Target, previous);
        CopyFlushed(request.Candidate, pending);
        request.Phase = "replacing"; GameInstaller.SaveJournal(requestPath, request);
        try
        {
            File.Move(pending, request.Target, true);
            request.Phase = "installed"; GameInstaller.SaveJournal(requestPath, request);
        }
        catch { RestoreLauncher(request); throw; }
    }
    public static void RestoreLauncher(Request request)
    {
        string previous = request.Target + ".previous";
        SafePaths.NoLinks(previous); SafePaths.NoLinks(request.Target);
        if (!VerifiedDownload.Hash(previous).Equals(request.PreviousSha256, StringComparison.OrdinalIgnoreCase)) throw new IOException("Die vorherige Launcherversion konnte nicht geprüft werden.");
        string pending = request.Target + ".restore";
        SafePaths.NoLinks(pending);
        CopyFlushed(previous, pending); File.Move(pending, request.Target, true);
        request.Phase = "restored";
    }
    private static void CopyFlushed(string source, string destination)
    {
        using var input = File.OpenRead(source);
        using var output = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None);
        input.CopyTo(output); output.Flush(true);
    }
}
