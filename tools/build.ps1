param([string]$Configuration = 'Release')
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repoRoot 'src/RebornLauncher/PokemonRebornLauncher.csproj'
$output = Join-Path $repoRoot 'dist/launcher'
& dotnet publish $project -c $Configuration -r win-x64 --self-contained true -o $output
if ($LASTEXITCODE -ne 0) { throw 'Launcher-Build fehlgeschlagen.' }
Copy-Item -LiteralPath (Join-Path $repoRoot 'src/RebornLauncher/assets/reborn.ico') -Destination (Join-Path $output 'reborn.ico') -Force
Write-Output "Fertiger Launcher: $(Join-Path $output 'launcher.exe')"
