param([string]$Setup = '', [string]$Launcher = '')
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
if (-not $Launcher) { $Launcher = Join-Path $repoRoot 'dist/launcher/launcher.exe' }
$Launcher = (Resolve-Path -LiteralPath $Launcher).Path
$versionInfo = [Diagnostics.FileVersionInfo]::GetVersionInfo($Launcher)
$version = '{0}.{1}.{2}' -f $versionInfo.FileMajorPart, $versionInfo.FileMinorPart, $versionInfo.FileBuildPart
if (-not $Setup) { $Setup = Join-Path $repoRoot "dist/installer/Reborn-Launcher-$version-Setup.exe" }
$Setup = (Resolve-Path -LiteralPath $Setup).Path
$testRoot = Join-Path $repoRoot ('test-results/installer-' + [guid]::NewGuid().ToString('N'))
$target = [IO.Path]::GetFullPath((Join-Path $testRoot 'installed-launcher'))
$expectedPrefix = [IO.Path]::GetFullPath((Join-Path $repoRoot 'test-results')) + [IO.Path]::DirectorySeparatorChar
if (-not $target.StartsWith($expectedPrefix, [StringComparison]::OrdinalIgnoreCase) -or [IO.Path]::GetPathRoot($target) -ine 'D:\') { throw 'Der Test benötigt einen isolierten Ordner innerhalb des D:-Workspace.' }
New-Item -ItemType Directory -Path $target -Force | Out-Null
$uninstallKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\{19E65ED2-29A4-4C1C-BB37-8F8C099160B0}_is1'
function Get-RegistrationSnapshot {
    if (Test-Path -LiteralPath $uninstallKey) { return (Get-ItemProperty -LiteralPath $uninstallKey | ConvertTo-Json -Compress) }
    return '<absent>'
}
function Get-ShortcutSnapshot {
    $paths = @((Join-Path ([Environment]::GetFolderPath('Programs')) 'Pokémon Reborn/Pokémon Reborn Launcher.lnk'), (Join-Path ([Environment]::GetFolderPath('Desktop')) 'Pokémon Reborn Launcher.lnk'))
    return (($paths | ForEach-Object { if (Test-Path -LiteralPath $_) { $_ + '=' + (Get-FileHash -LiteralPath $_).Hash } else { $_ + '=<absent>' } }) -join '|')
}
function Run-Installer([string]$Destination, [string]$LogName) {
    $arguments = @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART','/NOICONS','/TASKS=','/ISOLATEDTEST=1',('/DIR="'+$Destination+'"'),('/LOG="'+(Join-Path $testRoot $LogName)+'"'))
    $process = Start-Process -FilePath $Setup -ArgumentList $arguments -WindowStyle Hidden -PassThru
    if (-not $process.WaitForExit(45000)) { $process.Kill(); throw 'Setup-Test hat das Zeitlimit überschritten.' }
    return $process.ExitCode
}
$registrationBefore = Get-RegistrationSnapshot
$shortcutsBefore = Get-ShortcutSnapshot
$settings = Join-Path $target 'settings.json'
[IO.File]::WriteAllText($settings, '{"autoUpdate":false,"musicVolume":0.37,"friendSetting":"keep"}')
$settingsHash = (Get-FileHash -LiteralPath $settings).Hash
$game = Join-Path $testRoot 'separate-fake-game'
New-Item -ItemType Directory -Path $game | Out-Null
foreach ($name in @('Game.exe','Game.ini','mkxp.json','Game.rxdata')) { [IO.File]::WriteAllText((Join-Path $game $name), 'isolated-sentinel-' + $name) }
$gameBefore = Get-ChildItem -LiteralPath $game -File | Get-FileHash | Select-Object Path,Hash | ConvertTo-Json -Compress
if ((Run-Installer $target 'install.log') -ne 0) { throw 'Die isolierte Installation ist fehlgeschlagen.' }
$installedExe = Join-Path $target 'launcher.exe'
$uninstaller = Join-Path $target 'unins000.exe'
foreach ($required in @('launcher.exe','reborn.ico','HINWEISE.txt','credits/Barlow-OFL.txt','credits/DESIGN-QUELLEN.md','credits/MUSIC-CREDITS.txt','unins000.exe','unins000.dat')) {
    if (-not (Test-Path -LiteralPath (Join-Path $target $required))) { throw "Installierte Datei fehlt: $required" }
}
$newHash = (Get-FileHash -LiteralPath $Launcher).Hash
if ((Get-FileHash -LiteralPath $installedExe).Hash -ne $newHash) { throw 'Die installierte EXE stimmt nicht mit dem geprüften Build überein.' }
if ((Run-Installer $target 'reinstall.log') -ne 0) { throw 'Die erneute Installation ist fehlgeschlagen.' }
if ((Get-FileHash -LiteralPath $settings).Hash -ne $settingsHash) { throw 'Die Installation hat vorhandene Einstellungen überschrieben.' }
if ((Run-Installer $game 'rejected-game-folder.log') -eq 0) { throw 'Der Installer hat einen Spielordner als Ziel akzeptiert.' }

# Exercise the real update helper inside the installed directory, with an old
# released EXE as target. The restarted app inherits smoke mode: no game or
# user settings are opened, and no network/game update is started.
$legacy = Join-Path $repoRoot 'launcher.exe'
Copy-Item -LiteralPath $legacy -Destination $installedExe -Force
$oldHash = (Get-FileHash -LiteralPath $installedExe).Hash
$transaction = Join-Path $target '.reborn-updater/self-installer-test'
New-Item -ItemType Directory -Path $transaction -Force | Out-Null
$candidate = Join-Path $transaction 'launcher.exe'
Copy-Item -LiteralPath $Launcher -Destination $candidate
$requestPath = Join-Path $transaction 'request.json'
$request = @{Target=$installedExe; Candidate=$candidate; Sha256=$newHash; Size=(Get-Item -LiteralPath $candidate).Length; PreviousSha256=$oldHash; ParentPid=2147483000; ParentStartedUtcTicks=0; Phase='prepared'}
[IO.File]::WriteAllText($requestPath, ($request | ConvertTo-Json), [Text.UTF8Encoding]::new($false))
$smokeResult = Join-Path $testRoot 'installed-self-update.json'
$start = [Diagnostics.ProcessStartInfo]::new($candidate)
$start.UseShellExecute = $false
$start.CreateNoWindow = $true
$start.WindowStyle = [Diagnostics.ProcessWindowStyle]::Hidden
$start.WorkingDirectory = $transaction
$start.ArgumentList.Add('--apply-launcher-update')
$start.ArgumentList.Add($requestPath)
$start.Environment['REBORN_LAUNCHER_SMOKE_OUTPUT'] = $smokeResult
$helper = [Diagnostics.Process]::Start($start)
if (-not $helper.WaitForExit(60000)) { $helper.Kill(); throw 'Der installierte Updatehelfer hat das Zeitlimit überschritten.' }
if ($helper.ExitCode -ne 0) { throw "Updatehelfer beendet: $($helper.ExitCode)" }
$deadline = [DateTime]::UtcNow.AddSeconds(20)
while (-not (Test-Path -LiteralPath $smokeResult) -and [DateTime]::UtcNow -lt $deadline) { Start-Sleep -Milliseconds 100 }
$smoke = Get-Content -LiteralPath $smokeResult -Raw | ConvertFrom-Json
$request = Get-Content -LiteralPath $requestPath -Raw | ConvertFrom-Json
if (-not $smoke.success -or $smoke.processPath -ine $installedExe -or $request.Phase -ne 'acknowledged') { throw 'Der installierte Launcher hat den Neustart nicht erfolgreich bestätigt.' }
if ((Get-FileHash -LiteralPath $installedExe).Hash -ne $newHash -or (Get-FileHash -LiteralPath ($installedExe+'.previous')).Hash -ne $oldHash) { throw 'Austausch oder Sicherung der installierten EXE stimmt nicht.' }
try {
    $restarted = [Diagnostics.Process]::GetProcessById([int]$smoke.processId)
    if (-not $restarted.WaitForExit(15000)) { throw 'Der installierte Smoke-Launcher läuft unerwartet weiter.' }
} catch [ArgumentException] { }

# Only invoke the uninstaller that was just created in the verified test target.
if (-not $uninstaller.StartsWith($expectedPrefix, [StringComparison]::OrdinalIgnoreCase)) { throw 'Unsicherer Deinstallationspfad.' }
$remove = Start-Process -FilePath $uninstaller -ArgumentList @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART',('/LOG="'+(Join-Path $testRoot 'uninstall.log')+'"')) -WindowStyle Hidden -PassThru
if (-not $remove.WaitForExit(30000) -or $remove.ExitCode -ne 0) { throw 'Die isolierte Deinstallation ist fehlgeschlagen.' }
if (Test-Path -LiteralPath $installedExe) { throw 'Die Deinstallation hat die Launcher-EXE nicht entfernt.' }
if ((Get-FileHash -LiteralPath $settings).Hash -ne $settingsHash) { throw 'Die Deinstallation hat Einstellungen verändert.' }
$gameAfter = Get-ChildItem -LiteralPath $game -File | Get-FileHash | Select-Object Path,Hash | ConvertTo-Json -Compress
if ($gameAfter -ne $gameBefore) { throw 'Die isolierten Spiel-/Spielstand-Sentinels wurden verändert.' }
if ((Get-RegistrationSnapshot) -ne $registrationBefore -or (Get-ShortcutSnapshot) -ne $shortcutsBefore) { throw 'Der isolierte Test hat globale Benutzereinträge verändert.' }
$report = [ordered]@{success=$true; launcherVersion=$version; setupSha256=(Get-FileHash -LiteralPath $Setup).Hash.ToLowerInvariant(); launcherSha256=$newHash.ToLowerInvariant(); installationVerified=$true; reinstallPreservedSettings=$true; gameFolderRejected=$true; installedExecutableSelfUpdateVerified=$true; restartAcknowledged=$true; oldExecutableBackupVerified=$true; uninstallVerified=$true; settingsAndGameSentinelsUnchanged=$true; globalRegistrationAndShortcutsUnchanged=$true; testDirectory=$testRoot}
$report | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $repoRoot 'test-results/installer.json') -Encoding utf8
$report | ConvertTo-Json
