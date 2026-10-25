param([string]$Launcher = '')
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
if (-not $Launcher) { $Launcher = Join-Path $repoRoot 'dist/launcher/launcher.exe' }
$Launcher = (Resolve-Path -LiteralPath $Launcher).Path
$expectedHash = (Get-FileHash -LiteralPath $Launcher).Hash.ToLowerInvariant()
$output = Join-Path $repoRoot ('test-results/lighting-' + [guid]::NewGuid().ToString('N'))
$process = Start-Process -FilePath $Launcher -ArgumentList @('--lighting-smoke-test', ('"' + $output + '"')) -WindowStyle Hidden -PassThru
if (-not $process.WaitForExit(60000)) {
    $process.Kill()
    throw 'Die isolierte Beleuchtungsprüfung hat das Zeitlimit überschritten.'
}
if ($process.ExitCode -ne 0) { throw "Beleuchtungsprüfung fehlgeschlagen; Bericht unter $output" }
$report = Get-Content -Raw -LiteralPath (Join-Path $output 'result.json') | ConvertFrom-Json
if (-not $report.success -or (Get-FileHash -LiteralPath $Launcher).Hash.ToLowerInvariant() -ne $expectedHash) {
    throw 'Beleuchtungsprüfung oder Bindung an die getestete EXE fehlgeschlagen.'
}
$report | Add-Member -NotePropertyName launcherSha256 -NotePropertyValue $expectedHash
$report | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $repoRoot 'test-results/lighting-release.json') -Encoding utf8
$report | ConvertTo-Json
