param(
    [ValidateSet('Both', 'Day', 'Night')]
    [string]$Period = 'Both',
    [string]$LauncherPath = 'dist\launcher\launcher.exe'
)

$ErrorActionPreference = 'Stop'

$expectedRepoRoot = 'D:\Dokumente\ChatGPT\Pokemon reborn\reborn-update'
$repoRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
if (-not $repoRoot.Equals($expectedRepoRoot, [StringComparison]::OrdinalIgnoreCase)) {
    throw "Dieses Vorschauwerkzeug darf nur im D:-Repository ausgeführt werden: $expectedRepoRoot"
}

$launcherCandidate = if ([IO.Path]::IsPathRooted($LauncherPath)) { $LauncherPath } else { Join-Path $repoRoot $LauncherPath }
$launcher = [IO.Path]::GetFullPath($launcherCandidate)
$distRoot = [IO.Path]::GetFullPath((Join-Path $repoRoot 'dist'))
$distPrefix = $distRoot.TrimEnd('\') + '\'
if (-not $launcher.StartsWith($distPrefix, [StringComparison]::OrdinalIgnoreCase)) {
    throw "Die Launcher-EXE muss innerhalb des D:-Repo-Unterordners dist liegen: $launcher"
}
if (-not (Test-Path -LiteralPath $launcher -PathType Leaf)) {
    throw "Launcher-EXE fehlt. Zuerst tools\build.ps1 ausführen oder -LauncherPath auf einen lokalen dist-Build setzen: $launcher"
}
$relativePath = $launcher.Substring($distPrefix.Length)
$currentPath = $distRoot
$distItem = Get-Item -LiteralPath $distRoot -ErrorAction SilentlyContinue
if ($distItem -and ($distItem.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
    throw "Der dist-Ordner darf nicht über Junctions oder symbolische Links auf einen anderen Ort zeigen: $distRoot"
}
foreach ($part in $relativePath.Split('\')) {
    $currentPath = Join-Path $currentPath $part
    $item = Get-Item -LiteralPath $currentPath
    if (($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
        throw "Die Launcher-EXE darf nicht über Junctions oder symbolische Links auf einen anderen Ort zeigen: $currentPath"
    }
}

$themes = @('standard', 'halloween', 'winter', 'spring', 'summer', 'harvest', 'newyear', 'arceus')
$periods = switch ($Period) {
    'Day' { @(@{ Name = 'day'; Argument = '--day' }) }
    'Night' { @(@{ Name = 'night'; Argument = '--night' }) }
    default { @(@{ Name = 'day'; Argument = '--day' }, @{ Name = 'night'; Argument = '--night' }) }
}
$previousSmokeOutput = $env:REBORN_LAUNCHER_SMOKE_OUTPUT
Remove-Item Env:REBORN_LAUNCHER_SMOKE_OUTPUT -ErrorAction SilentlyContinue
try {
    # All windows share one runtime and the decoded artwork cache.
    $previewArguments = @('--preview-all')
    if ($Period -eq 'Day') { $previewArguments += '--day' }
    elseif ($Period -eq 'Night') { $previewArguments += '--night' }
    $processes = @(Start-Process -FilePath $launcher -ArgumentList $previewArguments -PassThru)
}
finally {
    if ($null -ne $previousSmokeOutput) { $env:REBORN_LAUNCHER_SMOKE_OUTPUT = $previousSmokeOutput }
}

Write-Output "$($themes.Count * @($periods).Count) Theme-Vorschauen ($Period) in einem gemeinsamen Prozess gestartet aus $launcher"
$processes | Select-Object Id, ProcessName
