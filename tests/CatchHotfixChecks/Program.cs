using System.Net;
using System.Text.Json;
using PokemonRebornLauncher.Services;

if (args.Length is < 2 or > 3) throw new ArgumentException("CatchHotfixChecks <release-directory> <output-directory> [--public]");
string release = Path.GetFullPath(args[0]), output = Path.GetFullPath(args[1]);
bool publicDownload = args.Length == 3 && args[2] == "--public";
var manifest = UpdateManifest.Parse(File.ReadAllText(Path.Combine(release, "update.json")));
var hotfix = manifest.GameHotfixes.Single();
string repo = Path.GetFullPath(Path.Combine(release, "../.."));
string fixture = Path.Combine(output, "delta-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(fixture);
void Write(string relative, string text)
{
    string file = SafePaths.Under(fixture, relative);
    Directory.CreateDirectory(Path.GetDirectoryName(file)!); File.WriteAllText(file, text);
}
void Check(bool value, string message) { if (!value) throw new Exception(message); }
Write("Game.exe", "isolated game executable sentinel - never launched");
Write("Game.ini", "isolated game ini"); Write("mkxp.json", "friend display settings");
Write("settings.json", "friend launcher settings"); Write("Save Data/Game_3.rxdata", "synthetic friend save - not real player data");
Write("patch/Mods/FriendsOwnMod.rb", "friend mod sentinel"); Write("version.txt", hotfix.FromVersion);
string relativeMod = hotfix.Files.Single().Path;
string baseline = SafePaths.Under(repo, $"game-hotfixes/{hotfix.FromVersion}/{relativeMod}");
string mod = SafePaths.Under(fixture, relativeMod);
Directory.CreateDirectory(Path.GetDirectoryName(mod)!); File.Copy(baseline, mod);
Check(hotfix.MatchesBase(fixture, Path.Combine(fixture, "version.txt"), CancellationToken.None), "Verified baseline is not eligible for tiny hotfix.");
byte[] original = File.ReadAllBytes(mod); File.AppendAllText(mod, "\n# local friend change");
Check(!hotfix.MatchesBase(fixture, Path.Combine(fixture, "version.txt"), CancellationToken.None), "Changed friend file was accepted as pristine hotfix base.");
File.WriteAllBytes(mod, original);
var protectedFiles = new[] { "Game.exe", "Game.ini", "mkxp.json", "settings.json", "Save Data/Game_3.rxdata", "patch/Mods/FriendsOwnMod.rb" }
    .ToDictionary(path => path, path => VerifiedDownload.Hash(SafePaths.Under(fixture, path)));
// Verify the immutable published release snapshot rather than GitHub's briefly
// cached raw/main response or a later, unrelated launcher publication.
string feed = publicDownload ? new Uri(new Uri(manifest.Game.Url), "update.json").AbsoluteUri : "https://isolated.hotfix.test/update.json";
var settings = new LauncherSettings { InstallDirectory = fixture, ManifestUrl = feed };
using var transport = new CheckedTransport(feed, hotfix.Url, release, publicDownload);
using var downloader = new VerifiedDownload(transport);
using var service = new UpdateService(downloader);
var progress = new Reporter();
await service.DownloadAndInstallAsync(settings, manifest.Game.Version, progress, CancellationToken.None);
Check(transport.Requests.SequenceEqual(new[] { feed, hotfix.Url }), "Updater did not select exactly the small hotfix asset.");
Check(VerifiedDownload.Hash(mod).Equals(hotfix.Files[0].Sha256, StringComparison.OrdinalIgnoreCase), "Installed mod hash differs.");
Check(File.ReadAllText(Path.Combine(fixture, "version.txt")) == manifest.Game.Version, "Target version not committed.");
Check(progress.Messages.Any(text => text.StartsWith("Hotfix: ", StringComparison.Ordinal)), "Hotfix progress missing.");
var selection = service.GetVersionSelection(settings);
Check(selection.ActiveVersion == manifest.Game.Version && selection.OtherVersion == hotfix.FromVersion && !selection.IsPreviousActive, "Previous version was not preserved.");
await service.SwitchVersionAsync(settings, progress, CancellationToken.None);
Check(File.ReadAllBytes(mod).SequenceEqual(original) && File.ReadAllText(Path.Combine(fixture, "version.txt")) == hotfix.FromVersion, "Rollback did not restore original mod/version.");
await service.SwitchVersionAsync(settings, progress, CancellationToken.None);
Check(VerifiedDownload.Hash(mod).Equals(hotfix.Files[0].Sha256, StringComparison.OrdinalIgnoreCase), "Switching forward did not restore patched mod.");
foreach (var file in protectedFiles)
    Check(VerifiedDownload.Hash(SafePaths.Under(fixture, file.Key)) == file.Value, "Protected fixture changed: " + file.Key);
var result = new { success = true, version = manifest.Game.Version, fromVersion = hotfix.FromVersion,
    realPublicGithubDownload = publicDownload, manifestUrl = feed, downloadedHotfixBytes = hotfix.Size, fullPackageDownloaded = false,
    hotfixSha256 = hotfix.Sha256, modSha256 = hotfix.Files[0].Sha256, modifiedBaseRejected = true,
    previousAndCurrentVersionSwitchVerified = true, personalFilesUnchanged = protectedFiles.Keys, isolatedInstall = fixture };
Directory.CreateDirectory(output);
string report = Path.Combine(output, publicDownload ? "public-hotfix-check.json" : "hotfix-integration.json");
File.WriteAllText(report, JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine(JsonSerializer.Serialize(result));

sealed class Reporter : IProgress<UpdateProgress>
{
    public List<string> Messages { get; } = [];
    public void Report(UpdateProgress value) { lock (Messages) Messages.Add(value.Message); }
}
sealed class CheckedTransport(string feed, string asset, string release, bool real) : DelegatingHandler(new HttpClientHandler())
{
    public List<string> Requests { get; } = [];
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        string url = request.RequestUri!.AbsoluteUri;
        if (url != feed && url != asset) throw new InvalidDataException("Unexpected/fallback request refused: " + url);
        Requests.Add(url);
        if (real) return await base.SendAsync(request, cancellationToken);
        string path = Path.Combine(release, url == feed ? "update.json" : Path.GetFileName(new Uri(asset).AbsolutePath));
        return new HttpResponseMessage(HttpStatusCode.OK) { RequestMessage = request, Content = new StreamContent(File.OpenRead(path)) };
    }
}
