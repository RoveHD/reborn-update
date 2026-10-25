param([string]$Executable = '', [string]$OldExecutable = '')
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
if (-not $Executable) { $Executable = Join-Path $repoRoot 'dist/launcher/launcher.exe' }
if (-not $OldExecutable) { $OldExecutable = Join-Path $repoRoot 'launcher.exe' }
$testRoot = Join-Path $repoRoot ('test-results/self-update-' + [guid]::NewGuid().ToString('N'))
$transaction = Join-Path $testRoot '.reborn-updater/self-integration'
New-Item -ItemType Directory -Path $transaction -Force | Out-Null
$target = Join-Path $testRoot 'launcher.exe'
$candidate = Join-Path $transaction 'launcher.exe'
Copy-Item -LiteralPath $OldExecutable -Destination $target
Copy-Item -LiteralPath $Executable -Destination $candidate
$originalHash = (Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash
$newHash = (Get-FileHash -LiteralPath $candidate -Algorithm SHA256).Hash
$settings = Join-Path $testRoot 'settings.json'
[IO.File]::WriteAllText($settings, '{"auto_update":false,"friendSetting":"keep"}')
$saveFolder = Join-Path $testRoot 'Save Data'
New-Item -ItemType Directory -Path $saveFolder | Out-Null
$save = Join-Path $saveFolder 'Game.rxdata'
[IO.File]::WriteAllText($save, 'test-save-not-a-real-game-save')
$settingsHash = (Get-FileHash -LiteralPath $settings).Hash
$saveHash = (Get-FileHash -LiteralPath $save).Hash
$requestPath = Join-Path $transaction 'request.json'
$request = @{ Target=$target; Candidate=$candidate; Sha256=$newHash; Size=(Get-Item -LiteralPath $candidate).Length; PreviousSha256=$originalHash; ParentPid=2147483000; ParentStartedUtcTicks=0; Phase='prepared' }
[IO.File]::WriteAllText($requestPath, ($request | ConvertTo-Json), [Text.UTF8Encoding]::new($false))
$smokeResult = Join-Path $testRoot 'restarted.json'
$start = [Diagnostics.ProcessStartInfo]::new($candidate)
$start.UseShellExecute = $false
$start.CreateNoWindow = $true
$start.WindowStyle = [Diagnostics.ProcessWindowStyle]::Hidden
$start.WorkingDirectory = $transaction
$start.ArgumentList.Add('--apply-launcher-update')
$start.ArgumentList.Add($requestPath)
$start.Environment['REBORN_LAUNCHER_SMOKE_OUTPUT'] = $smokeResult
$process = [Diagnostics.Process]::Start($start)
if (-not $process.WaitForExit(55000)) { $process.Kill(); throw 'Isolierter Testhelfer hat Zeitlimit überschritten.' }
if ($process.ExitCode -ne 0) { throw "Testhelfer fehlgeschlagen: $($process.ExitCode)" }
# The restarted smoke verifies 62 effect codecs and every day/night scene.
$deadline = [DateTime]::UtcNow.AddSeconds(180)
while (-not (Test-Path -LiteralPath $smokeResult) -and [DateTime]::UtcNow -lt $deadline) { Start-Sleep -Milliseconds 100 }
$result = Get-Content -LiteralPath $smokeResult -Raw | ConvertFrom-Json
$request = Get-Content -LiteralPath $requestPath -Raw | ConvertFrom-Json
if (-not $result.success -or $request.Phase -ne 'acknowledged' -or $result.processPath -ne $target) { throw 'Der Neustart wurde nicht bestätigt.' }
if ((Get-FileHash -LiteralPath $target).Hash -ne $newHash) { throw 'Neue EXE wurde nicht installiert.' }
if ((Get-FileHash -LiteralPath ($target+'.previous')).Hash -ne $originalHash) { throw 'Alte EXE-Sicherung stimmt nicht.' }
if ((Get-FileHash -LiteralPath $settings).Hash -ne $settingsHash -or (Get-FileHash -LiteralPath $save).Hash -ne $saveHash) { throw 'Benutzerdaten verändert.' }
$report = @{ success=$true; realExecutableReplacement=$true; restartAcknowledged=$true; originalBackupVerified=$true; savesAndSettingsUnchanged=$true; launcherSha256=$newHash.ToLowerInvariant(); testDirectory=$testRoot; processId=$result.processId }
$reportPath = Join-Path $repoRoot 'test-results/self-update.json'
[IO.File]::WriteAllText($reportPath, ($report | ConvertTo-Json), [Text.UTF8Encoding]::new($false))
$report | ConvertTo-Json
