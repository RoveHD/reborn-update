param(
    [string]$ExpectedVersion = '7.2.0',
    [ValidatePattern('^\d+\.\d+\.\d+$')][string]$FromVersion = '7.0.0',
    [ValidatePattern('^[a-fA-F0-9]{64}$')][string]$OldSha256 = '0cce75b7f5ae28b28ffd447622fb17edcf2f0c3c2dd1b729922f2e3e4d87073a',
    [string]$ManifestUrl = 'https://raw.githubusercontent.com/RoveHD/reborn-update/main/update.json',
    [ValidateRange(60,7200)][int]$TimeoutSeconds = 600
)
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$legacy = Join-Path $repoRoot "artifacts/released-launcher-$FromVersion.exe"
if (-not (Test-Path -LiteralPath $legacy)) {
    New-Item -ItemType Directory -Path (Split-Path -Parent $legacy) -Force | Out-Null
    Invoke-WebRequest -Uri "https://github.com/RoveHD/reborn-update/releases/download/v$FromVersion/launcher.exe" -OutFile $legacy
}
if ((Get-FileHash -LiteralPath $legacy -Algorithm SHA256).Hash -ine $OldSha256) { throw "Nicht die bekannte veröffentlichte Launcher-$FromVersion-EXE." }
$manifestUrl = $ManifestUrl
$manifest = Invoke-RestMethod -Uri $manifestUrl
if ($manifest.launcher.version -ne $ExpectedVersion) { throw 'Die neue Version ist im öffentlichen Kanal noch nicht sichtbar.' }
$testRoot = [IO.Path]::GetFullPath((Join-Path $repoRoot ('test-results/github-update-' + [guid]::NewGuid().ToString('N'))))
$prefix = [IO.Path]::GetFullPath((Join-Path $repoRoot 'test-results')) + [IO.Path]::DirectorySeparatorChar
if (-not $testRoot.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase) -or [IO.Path]::GetPathRoot($testRoot) -ine 'D:\') { throw 'Unsicherer Testpfad.' }
New-Item -ItemType Directory -Path $testRoot | Out-Null
$target = Join-Path $testRoot 'launcher.exe'
Copy-Item -LiteralPath $legacy -Destination $target
$settingsPath = Join-Path $testRoot 'settings.json'
$settings = @{autoUpdate=$false; musicEnabled=$false; installDirectory=$testRoot; manifestUrl=$manifestUrl; friendSetting='keep-live-test'}
[IO.File]::WriteAllText($settingsPath, ($settings | ConvertTo-Json), [Text.UTF8Encoding]::new($false))
$info = [Diagnostics.ProcessStartInfo]::new($target)
$info.UseShellExecute = $false
$info.CreateNoWindow = $true
$info.WindowStyle = [Diagnostics.ProcessWindowStyle]::Hidden
$info.WorkingDirectory = $testRoot
$info.Environment.Remove('REBORN_LAUNCHER_SMOKE_OUTPUT') | Out-Null
$oldProcess = [Diagnostics.Process]::Start($info)
try {
    $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    $acknowledged = $false
    while ([DateTime]::UtcNow -lt $deadline) {
        $state = Join-Path $testRoot '.reborn-updater'
        if (Test-Path -LiteralPath $state) {
            foreach ($transaction in Get-ChildItem -LiteralPath $state -Directory -Filter 'self-*') {
                $requestPath = Join-Path $transaction.FullName 'request.json'
                if (-not (Test-Path -LiteralPath $requestPath)) { continue }
                try { $request = Get-Content -LiteralPath $requestPath -Raw | ConvertFrom-Json } catch { continue }
                if ($request.Phase -eq 'acknowledged') { $acknowledged = $true; break }
            }
        }
        if ($acknowledged) { break }
        Start-Sleep -Milliseconds 200
    }
    if (-not $acknowledged) { throw 'Der echte GitHub-Selbstupdate-Neustart wurde nicht bestätigt.' }
    if ((Get-FileHash -LiteralPath $target).Hash -ine $manifest.launcher.sha256) { throw 'Die installierte EXE stimmt nicht mit dem öffentlichen Manifest überein.' }
    if ((Get-FileHash -LiteralPath ($target+'.previous')).Hash -ine $OldSha256) { throw 'Alte EXE nicht gesichert.' }
    $after = Get-Content -LiteralPath $settingsPath -Raw | ConvertFrom-Json
    if ($after.autoUpdate -ne $false -or $after.musicEnabled -ne $false -or $after.friendSetting -ne 'keep-live-test' -or $after.installDirectory -ne $testRoot) { throw 'Benutzereinstellungen nicht erhalten.' }
    $report = @{success=$true; from=$FromVersion; to=$ExpectedVersion; realPublicGithubDownload=$true; manifestUrl=$manifestUrl; restartAcknowledged=$true; oldExeBackupVerified=$true; settingsPreserved=$true; launcherSha256=$manifest.launcher.sha256; testDirectory=$testRoot}
    $report | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $repoRoot 'test-results/github-update.json') -Encoding utf8
    $report | ConvertTo-Json
}
finally {
    # Close only the executable at this exact newly-created isolated test path.
    foreach ($process in Get-Process -Name launcher -ErrorAction SilentlyContinue) {
        try {
            if ($process.MainModule.FileName -ine $target) { continue }
            $process.CloseMainWindow() | Out-Null
            if (-not $process.WaitForExit(5000)) { $process.Kill() }
        } catch [InvalidOperationException] { }
    }
    $oldProcess.Dispose()
}
