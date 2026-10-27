using System.Diagnostics;
using System.IO;
using System.Text.Json;

namespace PokemonRebornLauncher.Services;

public sealed partial class GameInstaller
{
    public sealed class Operation
    {
        public string Path { get; set; } = "";
        public bool Existed { get; set; }
        public bool Ready { get; set; }
        public bool Delete { get; set; }
        public string? Sha256 { get; set; }
        // Expected active bytes for version switching; empty means absent.
        // Null supports snapshots made by launchers that did not record this state.
        public string? InstalledSha256 { get; set; }
    }
    public sealed class Journal
    {
        public string Transaction { get; set; } = "";
        public bool Committed { get; set; }
        public List<Operation> Operations { get; set; } = [];
        public VersionState? NextSelection { get; set; }
        public string? PreviousSnapshot { get; set; }
    }
    private readonly string root;
    private readonly string state;
    public GameInstaller(string installDirectory)
    {
        root = Path.GetFullPath(installDirectory);
        if (root.TrimEnd('\\', '/').Equals(Path.GetPathRoot(root)?.TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Bitte einen eigenen Spielordner auswählen.");
        SafePaths.NoLinks(root);
        Directory.CreateDirectory(root);
        state = SafePaths.Under(root, ".reborn-updater");
        Directory.CreateDirectory(state);
    }
    public IDisposable AcquireLock() => new FileStream(Path.Combine(state, "update.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
    public string CreateStage()
    {
        string path = SafePaths.Under(state, "game-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path); return path;
    }
    public static void EnsureGameStopped(string installDirectory, string executable)
    {
        string expected = SafePaths.Under(installDirectory, executable);
        foreach (var process in Process.GetProcessesByName(Path.GetFileNameWithoutExtension(executable)))
        {
            using (process)
            {
                try
                {
                    if (string.Equals(process.MainModule?.FileName, expected, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Pokémon Reborn läuft noch. Bitte das Spiel vor dem Update schließen.");
                }
                catch (System.ComponentModel.Win32Exception) { throw new InvalidOperationException("Ein laufender Reborn-Prozess konnte nicht geprüft werden. Bitte alle Reborn-Fenster schließen."); }
            }
        }
    }
    public void Recover()
    {
        string path = Path.Combine(state, "game-transaction.json");
        SafePaths.NoLinks(path);
        if (!File.Exists(path)) { CleanOrphanStages(); return; }
        var journal = JsonSerializer.Deserialize<Journal>(File.ReadAllText(path)) ?? throw new InvalidDataException("Das Updateprotokoll ist beschädigt.");
        string transaction = SafePaths.Under(state, journal.Transaction);
        if (!journal.Transaction.StartsWith("game-", StringComparison.Ordinal) || journal.Transaction.Contains('/') || journal.Transaction.Contains('\\')) throw new InvalidDataException("Ungültiger Wiederherstellungspfad.");
        if (!journal.Committed) Rollback(journal, transaction);
        else FinishCommit(journal);
        File.Delete(path);
        CleanIfUnretained(transaction);
        CleanOrphanStages();
    }
    public void Install(string payload, string versionPath, string version, IEnumerable<RemovedFile> removals, IProgress<UpdateProgress> progress, CancellationToken ct, Action<int>? afterWrite = null, Action? beforeMutation = null)
    {
        string journalPath = Path.Combine(state, "game-transaction.json");
        if (File.Exists(journalPath)) throw new InvalidOperationException("Ein vorheriges Update muss zuerst wiederhergestellt werden.");
        var selection = LoadSelection(versionPath);
        if (selection?.IsPreviousActive == true) throw new InvalidOperationException("Bitte zuerst zur aktuellen Version wechseln, bevor du ein Update oder eine Reparatur startest.");
        string transaction = CreateStage();
        try
        {
            string backup = Path.Combine(transaction, "backup");
            Directory.CreateDirectory(backup);
            string versionRelative = SafePaths.ValidateRelative(Path.GetRelativePath(root, versionPath));
            if (SafePaths.IsProtected(versionRelative)) throw new InvalidOperationException("Die Versionsdatei darf keine Benutzerdaten überschreiben.");
            string safeVersionPath = SafePaths.Under(root, versionRelative);
            string? installedVersion = File.Exists(safeVersionPath) ? File.ReadAllText(safeVersionPath) : null;
            if (installedVersion is not null && ReleaseVersion.Parse(installedVersion).CompareTo(ReleaseVersion.Parse(version)) > 0)
                throw new InvalidOperationException("Eine neuere Reborn-Version ist bereits installiert. Das Update würde die Installation zurückstufen.");
            var files = Directory.EnumerateFiles(payload, "*", SearchOption.AllDirectories)
                .Select(path => (Source: path, Relative: SafePaths.ValidateRelative(Path.GetRelativePath(payload, path))))
                .Where(file => !SafePaths.IsProtected(file.Relative) && !file.Relative.Equals(versionRelative, StringComparison.OrdinalIgnoreCase)).ToList();
            var journal = new Journal { Transaction = Path.GetFileName(transaction), PreviousSnapshot = selection?.Snapshot };
            var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            int checkedFiles = 0;
            foreach (var file in files)
            {
                ct.ThrowIfCancellationRequested();
                SafePaths.NoLinks(file.Source);
                if (!used.Add(file.Relative)) throw new InvalidDataException("Doppelte Spieldatei.");
                string target = SafePaths.Under(root, file.Relative);
                if (Directory.Exists(target)) throw new IOException($"Am Dateipfad befindet sich ein Ordner: {file.Relative}");
                progress.Report(new(UpdateStage.Checking, ++checkedFiles * 100d / files.Count, 0, null, 0, $"Spieldateien werden geprüft ({checkedFiles} von {files.Count}) …"));
                bool unchanged = File.Exists(target) && new FileInfo(file.Source).Length == new FileInfo(target).Length &&
                    VerifiedDownload.Hash(file.Source, ct).Equals(VerifiedDownload.Hash(target, ct), StringComparison.OrdinalIgnoreCase);
                if (!unchanged) journal.Operations.Add(new() { Path = file.Relative });
            }
            // A retired path may have different known bytes in several supported
            // releases. Match any historical hash, but mutate and back up it once.
            var removalHashes = new Dictionary<string, (string? Replacement, HashSet<string> Hashes)>(StringComparer.OrdinalIgnoreCase);
            foreach (var removal in removals)
            {
                ct.ThrowIfCancellationRequested();
                string relative = SafePaths.ValidateRelative(removal.Path);
                if (relative.Equals(versionRelative, StringComparison.OrdinalIgnoreCase) || SafePaths.IsProtected(relative) || used.Contains(relative)) throw new InvalidDataException("Ungültige Löschanweisung im Manifest.");
                if (!System.Text.RegularExpressions.Regex.IsMatch(removal.Sha256, "^[a-fA-F0-9]{64}$")) throw new InvalidDataException("Ungültige Prüfsumme einer entfernten Datei.");
                string? replacement = removal.ReplacedBy is null ? null : SafePaths.ValidateRelative(removal.ReplacedBy);
                if (replacement is not null && (SafePaths.IsProtected(replacement) || !used.Contains(replacement)))
                    throw new InvalidDataException("Der Ersatz einer entfernten Datei fehlt im Spielpaket: " + relative);
                if (!removalHashes.TryGetValue(relative, out var group))
                    removalHashes.Add(relative, group = (replacement, new(StringComparer.OrdinalIgnoreCase)));
                else if (!string.Equals(group.Replacement, replacement, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("Widersprüchliche Ersatzpfade im Löschmanifest: " + relative);
                group.Hashes.Add(removal.Sha256);
            }
            if (files.Count == 0 && removalHashes.Count == 0) throw new InvalidDataException("Das Paket enthält keine installierbaren Spieldateien.");
            foreach (var removal in removalHashes)
            {
                ct.ThrowIfCancellationRequested();
                string target = SafePaths.Under(root, removal.Key);
                if (!File.Exists(target)) continue;
                if (removal.Value.Hashes.Contains(VerifiedDownload.Hash(target, ct))) journal.Operations.Add(new() { Path = removal.Key, Delete = true });
                else if (removal.Value.Replacement is not null)
                    throw new InvalidDataException($"Die veränderte Moddatei '{removal.Key}' kollidiert mit '{removal.Value.Replacement}'. Bitte die eigene Mod vor dem Update außerhalb des Spielordners sichern und entfernen. Es wurden keine Spieldateien geändert.");
            }
            if (!string.Equals(installedVersion, version, StringComparison.Ordinal)) journal.Operations.Add(new() { Path = versionRelative });
            ct.ThrowIfCancellationRequested();
            if (journal.Operations.Count == 0) return;
            string versionSource = Path.Combine(transaction, "version.txt");
            File.WriteAllText(versionSource, version);
            // Preflight and back up every original before the first game-file mutation.
            // One complete durable journal avoids rewriting an O(n) JSON document n times.
            int backedUp = 0;
            foreach (var operation in journal.Operations)
            {
                ct.ThrowIfCancellationRequested();
                string target = SafePaths.Under(root, operation.Path);
                if (Directory.Exists(target)) throw new IOException($"Am Dateipfad befindet sich ein Ordner: {operation.Path}");
                operation.Existed = File.Exists(target);
                if (operation.Existed)
                {
                    string previous = SafePaths.Under(backup, operation.Path);
                    Directory.CreateDirectory(Path.GetDirectoryName(previous)!);
                    CopyDurable(target, previous, ct);
                    operation.Sha256 = VerifiedDownload.Hash(previous, ct);
                    if (operation.Delete && !removalHashes[operation.Path].Hashes.Contains(operation.Sha256))
                        throw new IOException("Eine zu entfernende Datei wurde während der Prüfung geändert. Bitte das Update erneut starten: " + operation.Path);
                }
                operation.Ready = true;
                progress.Report(new(UpdateStage.Installing, ++backedUp * 35d / journal.Operations.Count, 0, null, 0, "Vorherige Spieldateien werden gesichert …"));
            }
            var sources = files.ToDictionary(f => f.Relative, f => f.Source, StringComparer.OrdinalIgnoreCase);
            sources[versionRelative] = versionSource;
            foreach (var operation in journal.Operations)
                operation.InstalledSha256 = operation.Delete ? "" : VerifiedDownload.Hash(sources[operation.Path], ct);
            // A repair of the same release keeps the previous release unchanged.
            if (ReleaseVersion.Parse(installedVersion ?? "0.0.0").CompareTo(ReleaseVersion.Parse(version)) < 0)
                journal.NextSelection = MakeSelection(journal, versionRelative, version, installedVersion ?? "0.0.0", version);
            ct.ThrowIfCancellationRequested();
            beforeMutation?.Invoke();
            ValidateActiveFiles(journal.Operations, ct);
            SaveJournal(journalPath, journal);
            try
            {
                for (int i = 0; i < journal.Operations.Count; i++)
                {
                    ct.ThrowIfCancellationRequested();
                    var operation = journal.Operations[i];
                    string target = SafePaths.Under(root, operation.Path);
                    if (operation.Delete) File.Delete(target);
                    else
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                        string source = sources[operation.Path];
                        string pending = target + ".reborn-update-new";
                        SafePaths.NoLinks(pending);
                        if (File.Exists(pending)) throw new IOException("Eine temporäre Updatedatei existiert bereits: " + pending);
                        try { CopyDurable(source, pending, ct); File.Move(pending, target, true); }
                        finally { if (File.Exists(pending)) File.Delete(pending); }
                    }
                    afterWrite?.Invoke(i);
                    progress.Report(new(UpdateStage.Installing, 35 + (i + 1) * 65d / journal.Operations.Count, 0, null, 0, $"Installiere {operation.Path}"));
                }
                ct.ThrowIfCancellationRequested();
                journal.Committed = true;
                SaveJournal(journalPath, journal);
            }
            catch (Exception failure)
            {
                try { Rollback(journal, transaction); }
                catch (Exception recoveryFailure)
                {
                    throw new IOException("Das Update wurde unterbrochen und konnte noch nicht vollständig zurückgesetzt werden. Bitte Reborn und andere Programme mit Zugriff auf den Spielordner schließen und den Launcher erneut öffnen. Sicherungen und Wiederherstellungsprotokoll bleiben erhalten.", new AggregateException(failure, recoveryFailure));
                }
                File.Delete(journalPath);
                TryClean(transaction);
                throw;
            }
            // A committed journal is harmless if cleanup is interrupted; recovery only cleans it up.
            FinishCommit(journal);
            try { File.Delete(journalPath); CleanIfUnretained(transaction); } catch (IOException) { }
        }
        finally
        {
            // Failed/cancelled preflight never modified the game and needs no retained backup.
            // A rollback failure keeps its durable journal and all backup files for the next launch.
            if (!File.Exists(journalPath)) CleanIfUnretained(transaction);
        }
    }
    private void Rollback(Journal journal, string transaction)
    {
        ValidateOperations(journal.Operations);
        ValidateBackups(transaction, journal.Operations, CancellationToken.None);
        foreach (var operation in journal.Operations.AsEnumerable().Reverse().Where(o => o.Ready))
        {
            if (SafePaths.IsProtected(operation.Path)) throw new InvalidDataException("Wiederherstellung betrifft einen geschützten Pfad.");
            string target = SafePaths.Under(root, operation.Path);
            string pending = target + ".reborn-update-new";
            SafePaths.NoLinks(pending);
            if (File.Exists(pending)) File.Delete(pending);
            if (operation.Existed)
            {
                string previous = SafePaths.Under(Path.Combine(transaction, "backup"), operation.Path);
                if (!File.Exists(previous)) throw new IOException("Die Sicherung fehlt: " + operation.Path);
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                // Preserve the backup until the complete rollback succeeds, so an interrupted rollback is repeatable.
                string restore = target + ".reborn-update-restore";
                SafePaths.NoLinks(restore);
                CopyDurable(previous, restore);
                File.Move(restore, target, true);
            }
            else if (File.Exists(target)) File.Delete(target);
        }
    }
    private void ValidateActiveFiles(IEnumerable<Operation> operations, CancellationToken ct)
    {
        foreach (var operation in operations)
        {
            ct.ThrowIfCancellationRequested();
            string target = SafePaths.Under(root, operation.Path);
            if (Directory.Exists(target) || File.Exists(target) != operation.Existed ||
                (operation.Existed && !VerifiedDownload.Hash(target, ct).Equals(operation.Sha256, StringComparison.OrdinalIgnoreCase)))
                throw new IOException("Eine Spieldatei wurde während der Sicherung geändert. Bitte den Vorgang erneut starten: " + operation.Path);
        }
    }
    private static void CopyDurable(string source, string target, CancellationToken ct = default)
    {
        using var input = File.OpenRead(source);
        using var output = new FileStream(target, FileMode.Create, FileAccess.Write, FileShare.None);
        byte[] buffer = System.Buffers.ArrayPool<byte>.Shared.Rent(131072);
        try
        {
            while (true)
            {
                ct.ThrowIfCancellationRequested();
                int read = input.Read(buffer, 0, buffer.Length);
                if (read == 0) break;
                output.Write(buffer, 0, read);
            }
            output.Flush(true);
        }
        finally { System.Buffers.ArrayPool<byte>.Shared.Return(buffer); }
    }
    internal static void SaveJournal<T>(string path, T journal)
    {
        string pending = path + ".new";
        SafePaths.NoLinks(path); SafePaths.NoLinks(pending);
        using (var stream = new FileStream(pending, FileMode.Create, FileAccess.Write, FileShare.None))
        { JsonSerializer.Serialize(stream, journal); stream.Flush(true); }
        File.Move(pending, path, true);
    }
    internal static void TryClean(string path)
    {
        try { SafePaths.NoLinks(path); if (Directory.Exists(path)) Directory.Delete(path, true); } catch { }
    }
}
