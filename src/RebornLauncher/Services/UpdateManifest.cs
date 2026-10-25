using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace PokemonRebornLauncher.Services;

public sealed class UpdateAsset
{
    public string Version { get; set; } = "";
    public string Url { get; set; } = "";
    public string Sha256 { get; set; } = "";
    public long Size { get; set; }
    public List<RemovedFile> RemovedFiles { get; set; } = [];
    public void Validate()
    {
        ReleaseVersion.Parse(Version);
        VerifiedDownload.ValidateUrl(Url);
        if (!Regex.IsMatch(Sha256, "^[a-fA-F0-9]{64}$") || Size <= 0)
            throw new InvalidDataException("Das Update enthält keine gültige Prüfsumme oder Dateigröße.");
        foreach (var file in RemovedFiles)
        {
            string relative = SafePaths.ValidateRelative(file.Path);
            if (!Regex.IsMatch(file.Sha256, "^[a-fA-F0-9]{64}$"))
                throw new InvalidDataException("Ungültige Prüfsumme einer entfernten Datei.");
            if (file.ReplacedBy is not null)
            {
                string replacement = SafePaths.ValidateRelative(file.ReplacedBy);
                if (SafePaths.IsProtected(replacement) || replacement.Equals(relative, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("Ungültiger Ersatzpfad einer entfernten Datei.");
            }
        }
    }
}
public sealed class RemovedFile
{
    public string Path { get; set; } = "";
    public string Sha256 { get; set; } = "";
    public string? ReplacedBy { get; set; }
}
public sealed class UpdateManifest
{
    public int SchemaVersion { get; set; }
    public UpdateAsset Launcher { get; set; } = new();
    public UpdateAsset Game { get; set; } = new();
    public List<GameHotfix> GameHotfixes { get; set; } = [];
    public string PatchNotes { get; set; } = "";
    public static UpdateManifest Parse(string json)
    {
        var manifest = JsonSerializer.Deserialize<UpdateManifest>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidDataException("Leeres Update-Manifest.");
        if (manifest.SchemaVersion != 1) throw new InvalidDataException("Dieses Update-Format wird nicht unterstützt.");
        manifest.Launcher.Validate();
        manifest.Game.Validate();
        if (manifest.GameHotfixes is null) throw new InvalidDataException("Ungültige Hotfix-Liste.");
        var bases = new HashSet<string>(StringComparer.Ordinal);
        foreach (var hotfix in manifest.GameHotfixes)
        {
            if (hotfix is null) throw new InvalidDataException("Leerer Hotfix-Eintrag.");
            hotfix.Validate(manifest.Game.Version);
            if (!bases.Add(hotfix.FromVersion)) throw new InvalidDataException("Doppelte Hotfix-Ausgangsversion.");
        }
        return manifest;
    }
}

public sealed class ReleaseVersion : IComparable<ReleaseVersion>
{
    private readonly int[] numbers;
    private readonly string[] prerelease;
    private ReleaseVersion(int[] n, string[] p) { numbers = n; prerelease = p; }
    public static ReleaseVersion Parse(string value)
    {
        var match = Regex.Match(value.Trim().TrimStart('v', 'V'), @"^(\d+)\.(\d+)\.(\d+)(?:\.(\d+))?(?:-([0-9A-Za-z.-]+))?(?:\+[0-9A-Za-z.-]+)?$");
        if (!match.Success) throw new InvalidDataException($"Ungültige Versionsnummer: {value}");
        var n = Enumerable.Range(1, 4).Select(i => match.Groups[i].Success ? int.Parse(match.Groups[i].Value) : 0).ToArray();
        var p = match.Groups[5].Success ? match.Groups[5].Value.Split('.') : [];
        if (p.Any(s => s.Length == 0)) throw new InvalidDataException("Ungültige Vorabversion.");
        return new ReleaseVersion(n, p);
    }
    public int CompareTo(ReleaseVersion? other)
    {
        if (other is null) return 1;
        for (int i = 0; i < 4; i++) { int c = numbers[i].CompareTo(other.numbers[i]); if (c != 0) return c; }
        if (prerelease.Length == 0 || other.prerelease.Length == 0)
            return (prerelease.Length == 0 ? 1 : 0).CompareTo(other.prerelease.Length == 0 ? 1 : 0);
        for (int i = 0; i < Math.Min(prerelease.Length, other.prerelease.Length); i++)
        {
            bool a = long.TryParse(prerelease[i], out long an), b = long.TryParse(other.prerelease[i], out long bn);
            int c = a && b ? an.CompareTo(bn) : a != b ? (a ? -1 : 1) : string.CompareOrdinal(prerelease[i], other.prerelease[i]);
            if (c != 0) return c;
        }
        return prerelease.Length.CompareTo(other.prerelease.Length);
    }
}
