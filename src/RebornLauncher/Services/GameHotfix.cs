using System.IO;
using System.IO.Compression;
using System.Text.RegularExpressions;

namespace PokemonRebornLauncher.Services;

public sealed class GameHotfix
{
    public string FromVersion { get; set; } = "";
    public string Version { get; set; } = "";
    public string Url { get; set; } = "";
    public string Sha256 { get; set; } = "";
    public long Size { get; set; }
    public List<GameHotfixFile> Files { get; set; } = [];

    [System.Text.Json.Serialization.JsonIgnore]
    public UpdateAsset Asset => new() { Version = Version, Url = Url, Sha256 = Sha256, Size = Size };

    public void Validate(string targetVersion)
    {
        Asset.Validate();
        if (!Version.Equals(targetVersion, StringComparison.Ordinal) ||
            ReleaseVersion.Parse(FromVersion).CompareTo(ReleaseVersion.Parse(Version)) >= 0)
            throw new InvalidDataException("Ungültige Hotfix-Ausgangs- oder Zielversion.");
        if (Files is null || Files.Count == 0 || Files.Count > 200000)
            throw new InvalidDataException("Ungültige Hotfix-Dateiliste.");
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        long expanded = 0;
        foreach (var file in Files)
        {
            if (file is null) throw new InvalidDataException("Leere Hotfix-Datei.");
            string path = SafePaths.ValidateRelative(file.Path);
            if (SafePaths.IsProtected(path) || !seen.Add(path) || file.Size < 0 ||
                (file.BeforeExists ? !IsHash(file.BeforeSha256) : file.BeforeSha256 != "") ||
                (file.Delete ? !file.BeforeExists || file.Sha256 != "" || file.Size != 0 : !IsHash(file.Sha256)))
                throw new InvalidDataException("Ungültige oder geschützte Hotfix-Datei.");
            expanded = checked(expanded + file.Size);
            if (expanded > 30L * 1024 * 1024 * 1024) throw new InvalidDataException("Entpackter Hotfix ist zu groß.");
        }
        foreach (string path in seen)
        {
            string ancestor = path;
            while (ancestor.Contains('/'))
            {
                ancestor = ancestor[..ancestor.LastIndexOf('/')];
                if (seen.Contains(ancestor)) throw new InvalidDataException("Hotfix-Datei kollidiert mit einem Ordner.");
            }
        }
    }

    public bool MatchesBase(string directory, string versionPath, CancellationToken ct)
    {
        SafePaths.NoLinks(versionPath);
        if (!File.Exists(versionPath) || !File.ReadAllText(versionPath).Trim().Equals(FromVersion, StringComparison.Ordinal)) return false;
        string versionRelative = SafePaths.ValidateRelative(Path.GetRelativePath(directory, versionPath));
        foreach (var file in Files)
        {
            ct.ThrowIfCancellationRequested();
            if (SafePaths.ValidateRelative(file.Path).Equals(versionRelative, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Ein Hotfix darf die Versionsdatei nicht als Spieldatei ersetzen.");
            string path = SafePaths.Under(directory, file.Path);
            if (Directory.Exists(path) || File.Exists(path) != file.BeforeExists ||
                (file.BeforeExists && !VerifiedDownload.Hash(path, ct).Equals(file.BeforeSha256, StringComparison.OrdinalIgnoreCase))) return false;
            // A file occupying a new path's parent must not trigger a full-package fallback.
            for (string? parent = Path.GetDirectoryName(path); parent is not null && !parent.Equals(Path.GetFullPath(directory), StringComparison.OrdinalIgnoreCase); parent = Path.GetDirectoryName(parent))
                if (File.Exists(parent)) return false;
        }
        return true;
    }

    public long RequiredSpace(string directory) => checked(Size + Files.Sum(f => f.Size) * 2 +
        Files.Sum(f => { var info = new FileInfo(SafePaths.Under(directory, f.Path)); return info.Exists ? info.Length : 0; }) + 1024 * 1024);

    private static bool IsHash(string? value) => value is not null && Regex.IsMatch(value, "^[a-fA-F0-9]{64}$");

    [System.Text.Json.Serialization.JsonIgnore]
    public IEnumerable<RemovedFile> Removals => Files.Where(f => f.Delete)
        .Select(f => new RemovedFile { Path = f.Path, Sha256 = f.BeforeSha256 });

    public string ExtractVerified(string archive, string destination, IProgress<UpdateProgress> progress, CancellationToken ct)
    {
        var expected = Files.Where(f => !f.Delete).ToDictionary(f => SafePaths.ValidateRelative(f.Path), StringComparer.OrdinalIgnoreCase);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using (var zip = ZipFile.OpenRead(archive))
        {
            foreach (var entry in zip.Entries)
            {
                ct.ThrowIfCancellationRequested();
                bool directory = entry.FullName.EndsWith('/') || entry.FullName.EndsWith('\\');
                string path = SafePaths.ValidateRelative(directory ? entry.FullName[..^1] : entry.FullName);
                if (SafePaths.IsProtected(path) || !seen.Add(path)) throw new InvalidDataException("Geschützter oder doppelter Hotfix-Archivpfad.");
                if (directory)
                {
                    if (!expected.Keys.Any(key => key.StartsWith(path + "/", StringComparison.OrdinalIgnoreCase)))
                        throw new InvalidDataException("Nicht deklarierter Hotfix-Ordner.");
                }
                else if (!expected.TryGetValue(path, out var file) || file.Size != entry.Length)
                    throw new InvalidDataException("Nicht deklarierte oder falsch dimensionierte Hotfix-Datei.");
            }
        }
        // SafePaths checks links, traversal, collisions and extraction bounds.
        // Ignore its optional single-wrapper return: hotfix paths are always root-relative.
        // A deletion-only hotfix deliberately carries an empty ZIP.
        if (expected.Count == 0) Directory.CreateDirectory(destination);
        else SafePaths.Extract(archive, destination, progress, ct);
        var actual = Directory.EnumerateFiles(destination, "*", SearchOption.AllDirectories).ToArray();
        if (actual.Length != expected.Count) throw new InvalidDataException("Hotfix-Dateimenge ist unvollständig.");
        foreach (string path in actual)
        {
            ct.ThrowIfCancellationRequested();
            string relative = SafePaths.ValidateRelative(Path.GetRelativePath(destination, path));
            if (!expected.TryGetValue(relative, out var file) || new FileInfo(path).Length != file.Size ||
                !VerifiedDownload.Hash(path, ct).Equals(file.Sha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Hotfix-Dateiprüfung fehlgeschlagen. Es wurden keine Spieldateien geändert.");
        }
        return destination;
    }
}

public sealed class GameHotfixFile
{
    public string Path { get; set; } = "";
    // Defaults preserve existing replacement-only manifests. Absence is explicit,
    // never inferred from a missing hash or a missing local file.
    public bool BeforeExists { get; set; } = true;
    public bool Delete { get; set; }
    public string BeforeSha256 { get; set; } = "";
    public string Sha256 { get; set; } = "";
    public long Size { get; set; }
}
