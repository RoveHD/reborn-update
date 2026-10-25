using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace PokemonRebornLauncher.Services;

public sealed record VersionSelection(string ActiveVersion, string? OtherVersion, bool IsPreviousActive);

public sealed partial class GameInstaller
{
    // Only the inactive bytes of paths that differ between the two releases are stored.
    // A new immutable snapshot is prepared before changing any game file. The existing
    // pair remains authoritative until the transaction's committed journal is durable.
    public sealed class VersionState
    {
        public int Schema { get; set; } = 1;
        public string ActiveVersion { get; set; } = "";
        public string OtherVersion { get; set; } = "";
        public string LatestVersion { get; set; } = "";
        public string VersionPath { get; set; } = "version.txt";
        public string Snapshot { get; set; } = "";
        public List<Operation> Entries { get; set; } = [];
        public bool IsPreviousActive => ReleaseVersion.Parse(ActiveVersion).CompareTo(ReleaseVersion.Parse(LatestVersion)) < 0;
    }
    private string SelectionPath => SafePaths.Under(state, "versions.json");

    public VersionSelection GetVersionSelection(string versionPath)
    {
        var pair = LoadSelection(versionPath);
        return pair is null
            ? new(ReadVersion(versionPath), null, false)
            : new(pair.ActiveVersion.Trim(), pair.OtherVersion.Trim(), pair.IsPreviousActive);
    }
    private static string ReadVersion(string path)
    {
        SafePaths.NoLinks(path);
        string version = File.Exists(path) ? File.ReadAllText(path).Trim() : "0.0.0";
        ReleaseVersion.Parse(version);
        return version;
    }
    private VersionState? LoadSelection(string versionPath)
    {
        if (!File.Exists(SelectionPath)) return null;
        var pair = JsonSerializer.Deserialize<VersionState>(File.ReadAllText(SelectionPath)) ?? throw new InvalidDataException("Die Versionsauswahl ist beschädigt.");
        ValidateSelection(pair);
        string relative = SafePaths.ValidateRelative(Path.GetRelativePath(root, versionPath));
        if (!relative.Equals(pair.VersionPath, StringComparison.OrdinalIgnoreCase) ||
            ReleaseVersion.Parse(ReadVersion(versionPath)).CompareTo(ReleaseVersion.Parse(pair.ActiveVersion)) != 0)
            throw new InvalidDataException("Die aktive Spielversion passt nicht zur gesicherten Versionsauswahl. Bitte die Versionsdatei nicht von Hand ändern.");
        return pair;
    }
    private void ValidateSelection(VersionState pair)
    {
        if (pair.Schema != 1 || pair.Entries is null || pair.Entries.Count == 0 || !Regex.IsMatch(pair.Snapshot, "^game-[0-9a-f]{32}$"))
            throw new InvalidDataException("Die Versionssicherung ist ungültig.");
        var active = ReleaseVersion.Parse(pair.ActiveVersion);
        var other = ReleaseVersion.Parse(pair.OtherVersion);
        var latest = ReleaseVersion.Parse(pair.LatestVersion);
        if (active.CompareTo(other) == 0 || (active.CompareTo(latest) != 0 && other.CompareTo(latest) != 0) || active.CompareTo(latest) > 0 || other.CompareTo(latest) > 0)
            throw new InvalidDataException("Die Versionssicherung enthält kein gültiges Versionspaar.");
        string version = SafePaths.ValidateRelative(pair.VersionPath);
        ValidateOperations(pair.Entries);
        if (!pair.Entries.Any(o => o.Path.Equals(version, StringComparison.OrdinalIgnoreCase))) throw new InvalidDataException("Die Versionssicherung enthält keine Versionsdatei.");
        foreach (var entry in pair.Entries)
            if (!entry.Ready || (entry.Existed && (entry.Sha256 is null || !Regex.IsMatch(entry.Sha256, "^[a-fA-F0-9]{64}$"))))
                throw new InvalidDataException("Die Versionssicherung enthält keine gültige Prüfsumme.");
        SafePaths.Under(state, pair.Snapshot);
    }
    private static void ValidateOperations(IEnumerable<Operation> operations)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var operation in operations)
        {
            string path = SafePaths.ValidateRelative(operation.Path);
            if (SafePaths.IsProtected(path) || !seen.Add(path)) throw new InvalidDataException("Die Sicherung enthält einen geschützten oder doppelten Pfad.");
        }
    }
    private static void ValidateBackups(string transaction, IEnumerable<Operation> operations, CancellationToken ct)
    {
        foreach (var operation in operations.Where(o => o.Ready && o.Existed))
        {
            ct.ThrowIfCancellationRequested();
            string backup = SafePaths.Under(Path.Combine(transaction, "backup"), operation.Path);
            if (!File.Exists(backup) || (operation.Sha256 is not null && !VerifiedDownload.Hash(backup, ct).Equals(operation.Sha256, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidDataException("Eine Versionssicherung fehlt oder ist beschädigt: " + operation.Path);
        }
    }
    private static VersionState MakeSelection(Journal journal, string versionPath, string active, string other, string latest) => new()
    {
        ActiveVersion = active.Trim(), OtherVersion = other.Trim(), LatestVersion = latest.Trim(), VersionPath = versionPath,
        Snapshot = journal.Transaction,
        Entries = journal.Operations.Select(o => new Operation { Path = o.Path, Existed = o.Existed, Ready = true, Sha256 = o.Sha256 }).ToList()
    };
    private void FinishCommit(Journal journal)
    {
        if (journal.NextSelection is null) return;
        if (journal.NextSelection.Snapshot != journal.Transaction) throw new InvalidDataException("Die Versionssicherung passt nicht zum Updateprotokoll.");
        ValidateSelection(journal.NextSelection);
        ValidateBackups(SafePaths.Under(state, journal.NextSelection.Snapshot), journal.NextSelection.Entries, CancellationToken.None);
        SaveJournal(SelectionPath, journal.NextSelection);
        // The previous pair is retired only after both the game and the new metadata commit.
        if (journal.PreviousSnapshot is not null && journal.PreviousSnapshot != journal.NextSelection.Snapshot)
        {
            if (!Regex.IsMatch(journal.PreviousSnapshot, "^game-[0-9a-f]{32}$")) throw new InvalidDataException("Ungültiger alter Sicherungspfad.");
            TryClean(SafePaths.Under(state, journal.PreviousSnapshot));
        }
    }
    private void CleanIfUnretained(string transaction)
    {
        if (File.Exists(SelectionPath))
        {
            var pair = JsonSerializer.Deserialize<VersionState>(File.ReadAllText(SelectionPath)) ?? throw new InvalidDataException("Die Versionsauswahl ist beschädigt.");
            ValidateSelection(pair);
            if (Path.GetFileName(transaction).Equals(pair.Snapshot, StringComparison.OrdinalIgnoreCase)) return;
        }
        TryClean(transaction);
    }
    private void CleanOrphanStages()
    {
        string? retained = null;
        if (File.Exists(SelectionPath))
        {
            var pair = JsonSerializer.Deserialize<VersionState>(File.ReadAllText(SelectionPath)) ?? throw new InvalidDataException("Die Versionsauswahl ist beschädigt.");
            ValidateSelection(pair);
            retained = pair.Snapshot;
        }
        // These uniquely named folders are only created under the reserved updater
        // directory. A killed preflight has no journal and never changed the game.
        foreach (string folder in Directory.EnumerateDirectories(state))
        {
            string name = Path.GetFileName(folder);
            if (Regex.IsMatch(name, "^game-[0-9a-f]{32}$") && !name.Equals(retained, StringComparison.OrdinalIgnoreCase))
                TryClean(SafePaths.Under(state, name));
        }
    }
    public void SwitchVersion(string versionPath, IProgress<UpdateProgress> progress, CancellationToken ct, Action<int>? afterWrite = null, Action? beforeMutation = null)
    {
        string journalPath = SafePaths.Under(state, "game-transaction.json");
        if (File.Exists(journalPath)) throw new InvalidOperationException("Ein unterbrochener Vorgang muss zuerst wiederhergestellt werden.");
        var pair = LoadSelection(versionPath) ?? throw new InvalidOperationException("Es ist noch keine vorherige Version gespeichert. Sie wird beim nächsten Update automatisch gesichert.");
        string incoming = SafePaths.Under(state, pair.Snapshot);
        ValidateBackups(incoming, pair.Entries, ct); // Validate every source before any active file changes.
        string transaction = CreateStage();
        var journal = new Journal { Transaction = Path.GetFileName(transaction), PreviousSnapshot = pair.Snapshot };
        try
        {
            var sources = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in pair.Entries.OrderBy(o => o.Path.Equals(pair.VersionPath, StringComparison.OrdinalIgnoreCase)))
            {
                ct.ThrowIfCancellationRequested();
                string target = SafePaths.Under(root, entry.Path);
                if (Directory.Exists(target)) throw new IOException("Am Dateipfad befindet sich ein Ordner: " + entry.Path);
                var operation = new Operation { Path = entry.Path, Delete = !entry.Existed, Existed = File.Exists(target), Ready = true };
                if (operation.Existed)
                {
                    string backup = SafePaths.Under(Path.Combine(transaction, "backup"), entry.Path);
                    Directory.CreateDirectory(Path.GetDirectoryName(backup)!);
                    CopyDurable(target, backup, ct);
                    operation.Sha256 = VerifiedDownload.Hash(backup, ct);
                }
                if (entry.Existed) sources[entry.Path] = SafePaths.Under(Path.Combine(incoming, "backup"), entry.Path);
                journal.Operations.Add(operation);
            }
            journal.NextSelection = MakeSelection(journal, pair.VersionPath, pair.OtherVersion, pair.ActiveVersion, pair.LatestVersion);
            ct.ThrowIfCancellationRequested();
            beforeMutation?.Invoke();
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
                        string pending = target + ".reborn-update-new";
                        SafePaths.NoLinks(pending);
                        if (File.Exists(pending)) throw new IOException("Eine temporäre Updatedatei existiert bereits: " + pending);
                        try { CopyDurable(sources[operation.Path], pending, ct); File.Move(pending, target, true); }
                        finally { if (File.Exists(pending)) File.Delete(pending); }
                    }
                    afterWrite?.Invoke(i);
                    progress.Report(new(UpdateStage.Installing, (i + 1) * 100d / journal.Operations.Count, 0, null, 0, "Wechsel zu Reborn " + pair.OtherVersion + " …"));
                }
                ct.ThrowIfCancellationRequested();
                journal.Committed = true;
                SaveJournal(journalPath, journal);
            }
            catch (Exception failure)
            {
                try { Rollback(journal, transaction); }
                catch (Exception recoveryFailure) { throw new IOException("Der Versionswechsel konnte noch nicht vollständig zurückgesetzt werden. Bitte Programme mit Zugriff auf den Spielordner schließen und den Launcher erneut öffnen. Sicherungen bleiben erhalten.", new AggregateException(failure, recoveryFailure)); }
                File.Delete(journalPath);
                throw;
            }
            FinishCommit(journal);
            File.Delete(journalPath);
        }
        finally { if (!File.Exists(journalPath)) CleanIfUnretained(transaction); }
    }
}
