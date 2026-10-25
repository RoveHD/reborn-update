using System.Text.Json;
using PokemonRebornLauncher.Services;

if (args.Length is < 2 or > 5) throw new ArgumentException("ReleaseChecks <release-directory> <test-output-directory> [--repair|--switch|--migration <previous-archive> [baseline-directory]]");
string release=Path.GetFullPath(args[0]), output=Path.GetFullPath(args[1]);
Directory.CreateDirectory(output);
if (args.Length >= 4 && args[2] == "--migration") { MigrationCheck.Run(release, output, Path.GetFullPath(args[3]), args.Length == 5 ? Path.GetFullPath(args[4]) : null); return; }
if (args.Length >= 4) throw new ArgumentException("Unknown release check mode");
if (args.Length == 3 && args[2] == "--switch") { await VersionSwitchCheck.Run(release, output); return; }
bool repair=args.Length==3 && args[2]=="--repair";
string root=Path.Combine(output,"game-"+Guid.NewGuid().ToString("N"));
if(repair) {
    string report=Path.Combine(output,"release-migration.json");
    if(!File.Exists(report))report=Path.Combine(output,"release-install.json");
    var earlier=JsonDocument.Parse(File.ReadAllText(report));
    root=Path.GetFullPath(earlier.RootElement.GetProperty("isolatedInstall").GetString()!);
    if(!root.StartsWith(output+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase) || !Path.GetFileName(root).StartsWith("game-"))throw new Exception("Refuse to repair outside generated fixture");
    if(File.ReadAllText(Path.Combine(root,"Save Data/Game_3.rxdata")) is not ("unique friend save sentinel" or "unique friend save migration sentinel"))throw new Exception("Not an isolated test fixture");
}
Directory.CreateDirectory(root);
var manifest=UpdateManifest.Parse(File.ReadAllText(Path.Combine(release,"update.json")));
string archive=SafePaths.Under(release,Path.GetFileName(new Uri(manifest.Game.Url).AbsolutePath));
if (!VerifiedDownload.Hash(archive).Equals(manifest.Game.Sha256,StringComparison.OrdinalIgnoreCase)) throw new Exception("Wrong release archive hash");
var timer=System.Diagnostics.Stopwatch.StartNew();
var progress=new Reporter();
var installer=new GameInstaller(root);
using var updateLock=installer.AcquireLock();
void Write(string rel,string text) { string path=Path.Combine(root,rel);Directory.CreateDirectory(Path.GetDirectoryName(path)!);File.WriteAllText(path,text); }
if(!repair) {
    Write("Game.exe","old game executable");
    Write("Game.ini","old game ini");
    Write("mkxp.json","friend display settings");
    Write("settings.json","friend launcher settings");
    Write("version.txt","6.1.0");
    Write("Save Data/Game_3.rxdata","unique friend save sentinel");
    Write("patch/Mods/FriendsOwnMod.rb","custom mod retained");
}
Write("Scripts/Summary.rb","old summary code");
DateTime unchangedTime=File.GetLastWriteTimeUtc(Path.Combine(root,"Game.exe"));
string friendMod=File.Exists(Path.Combine(root,"patch/Mods/FriendsOwnMigrationMod.rb"))?"patch/Mods/FriendsOwnMigrationMod.rb":"patch/Mods/FriendsOwnMod.rb";
var protectedFiles=new[]{"mkxp.json","settings.json","Save Data/Game_3.rxdata",friendMod}.ToDictionary(p=>p,p=>VerifiedDownload.Hash(Path.Combine(root,p)));
string stage=installer.CreateStage();
string payload=SafePaths.Extract(archive,Path.Combine(stage,"payload"),progress,CancellationToken.None);
installer.Install(payload,Path.Combine(root,"version.txt"),manifest.Game.Version,manifest.Game.RemovedFiles,progress,CancellationToken.None);
var expected=JsonDocument.Parse(File.ReadAllText(Path.Combine(release,"game-files.json")));
int verified=0;
foreach (var file in expected.RootElement.EnumerateObject()) {
    string path=SafePaths.Under(root,file.Name);
    if (!File.Exists(path) || new FileInfo(path).Length!=file.Value.GetProperty("size").GetInt64() ||
        !VerifiedDownload.Hash(path).Equals(file.Value.GetProperty("sha256").GetString(),StringComparison.OrdinalIgnoreCase)) throw new Exception("Installed file differs: "+file.Name);
    verified++;
}
foreach(var pair in protectedFiles) if(VerifiedDownload.Hash(Path.Combine(root,pair.Key))!=pair.Value)throw new Exception("Friend data changed: "+pair.Key);
if(File.ReadAllText(Path.Combine(root,"version.txt"))!=manifest.Game.Version)throw new Exception("Version commit missing");
if(File.Exists(Path.Combine(root,".reborn-updater/game-transaction.json")))throw new Exception("Uncommitted journal remains");
if(repair && File.GetLastWriteTimeUtc(Path.Combine(root,"Game.exe"))!=unchangedTime)throw new Exception("Unchanged executable was unnecessarily rewritten");
GameInstaller.TryClean(stage);
var result=new{success=true,installedFilesVerified=verified,personalFilesUnchanged=protectedFiles.Keys,version=manifest.Game.Version,archiveSha256=manifest.Game.Sha256,durationSeconds=timer.Elapsed.TotalSeconds,isolatedInstall=root,repair,unchangedFileTimestampPreserved=repair};
File.WriteAllText(Path.Combine(output,repair?"release-repair.json":"release-install.json"),JsonSerializer.Serialize(result,new JsonSerializerOptions{WriteIndented=true}));
Console.WriteLine(JsonSerializer.Serialize(result));

sealed class Reporter:IProgress<UpdateProgress> {
    DateTime last=DateTime.MinValue;
    public void Report(UpdateProgress p) {if((DateTime.UtcNow-last).TotalSeconds>10){Console.WriteLine($"{p.Stage}: {p.Percent:0}% {p.Message}");last=DateTime.UtcNow;}}
}
