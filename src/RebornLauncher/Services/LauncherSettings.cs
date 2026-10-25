using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PokemonRebornLauncher.Services;

public sealed class LauncherSettings
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true };
    private static readonly SemaphoreSlim SaveLock = new(1, 1);
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalSettings { get; set; }
    public bool AutoUpdate { get; set; } = true;
    public bool Fullscreen { get; set; }
    public bool CloseLauncherAfterGameStart { get; set; }
    public string GameExecutable { get; set; } = "Game.exe";
    public string InstallDirectory { get; set; } = ".";
    public string LocalVersionFile { get; set; } = "version.txt";
    public string BackgroundImage { get; set; } = "background.png";
    public string LogoImage { get; set; } = "logo.png";
    public string ManifestUrl { get; set; } = "https://raw.githubusercontent.com/RoveHD/reborn-update/main/update.json";
    // Retained for migration from the old settings format. Downloads always use the verified manifest.
    public string VersionFileUrl { get; set; } = "https://raw.githubusercontent.com/RoveHD/reborn-update/main/version.txt";
    public string PatchNotesUrl { get; set; } = "https://raw.githubusercontent.com/RoveHD/reborn-update/main/patchnotes.txt";
    public string DownloadUrl { get; set; } = "https://github.com/RoveHD/reborn-update/releases/latest/download/Reborn-19.5.0.zip";
    public string ReleasePageUrl { get; set; } = "https://github.com/RoveHD/reborn-update/releases/latest";
    public static string SettingsPath => Path.Combine(AppContext.BaseDirectory, "settings.json");
    public static async Task<LauncherSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(SettingsPath)) return new LauncherSettings();
        try
        {
            await using var stream = File.OpenRead(SettingsPath);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            return Parse(document.RootElement.GetRawText());
        }
        catch (JsonException ex) { throw new InvalidDataException("settings.json ist beschädigt. Bitte die Datei prüfen oder umbenennen; deine Spielstände sind davon unabhängig.", ex); }
    }
    public static LauncherSettings Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        var settings = JsonSerializer.Deserialize<LauncherSettings>(json, JsonOptions) ?? new LauncherSettings();
        var aliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["auto_update"] = "AutoUpdate", ["fullscreen"] = "Fullscreen",
            ["close_launcher_after_game_start"] = "CloseLauncherAfterGameStart",
            ["game_executable"] = "GameExecutable", ["install_directory"] = "InstallDirectory",
            ["local_version_file"] = "LocalVersionFile", ["manifest_url"] = "ManifestUrl",
            ["version_url"] = "VersionFileUrl", ["download_url"] = "DownloadUrl",
            ["patchnotes_url"] = "PatchNotesUrl", ["background_image"] = "BackgroundImage", ["logo_image"] = "LogoImage"
        };
        foreach (var alias in aliases)
        {
            if (!document.RootElement.TryGetProperty(alias.Key, out var value)) continue;
            var property = typeof(LauncherSettings).GetProperty(alias.Value)!;
            bool hasModern = document.RootElement.EnumerateObject().Any(p => p.Name.Equals(alias.Value, StringComparison.OrdinalIgnoreCase));
            if (!hasModern) property.SetValue(settings, JsonSerializer.Deserialize(value.GetRawText(), property.PropertyType));
            settings.AdditionalSettings?.Remove(alias.Key);
        }
        return settings;
    }
    public async Task SaveAsync(CancellationToken cancellationToken = default)
    {
        await SaveLock.WaitAsync(cancellationToken);
        string temporary = SettingsPath + ".new";
        try
        {
            SafePaths.NoLinks(SettingsPath); SafePaths.NoLinks(temporary);
            await using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
            { await JsonSerializer.SerializeAsync(stream, this, JsonOptions, cancellationToken); stream.Flush(true); }
            File.Move(temporary, SettingsPath, true);
        }
        finally { SaveLock.Release(); }
    }
    public string ResolveInstallDirectory() => Path.TrimEndingDirectorySeparator(ResolvePath(InstallDirectory));
    public string ResolveLocalVersionPath() => SafePaths.Under(ResolveInstallDirectory(), LocalVersionFile);
    public string ResolvePath(string path) => string.IsNullOrWhiteSpace(path) ? Path.GetFullPath(AppContext.BaseDirectory) : Path.GetFullPath(Path.IsPathRooted(path) ? path : Path.Combine(AppContext.BaseDirectory, path));
}
