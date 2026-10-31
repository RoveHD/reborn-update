param([string]$Configuration = 'Release')
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repoRoot 'src/RebornLauncher/PokemonRebornLauncher.csproj'
$output = Join-Path $repoRoot 'dist/launcher'
& dotnet restore $project -r win-x64
if ($LASTEXITCODE -ne 0) { throw 'Launcher-Restore fehlgeschlagen.' }
& dotnet clean $project -c $Configuration -r win-x64
if ($LASTEXITCODE -ne 0) { throw 'Launcher-Clean fehlgeschlagen.' }
& dotnet publish $project -c $Configuration -r win-x64 --no-restore --self-contained true -o $output
if ($LASTEXITCODE -ne 0) { throw 'Launcher-Build fehlgeschlagen.' }
Copy-Item -LiteralPath (Join-Path $repoRoot 'src/RebornLauncher/assets/reborn.ico') -Destination (Join-Path $output 'reborn.ico') -Force
$credits = Join-Path $output 'credits'
New-Item -ItemType Directory -Path $credits -Force | Out-Null
foreach ($file in @(
    @{ Source = 'docs/DESIGN-QUELLEN.md'; Name = 'DESIGN-QUELLEN.md' },
    @{ Source = 'docs/MUSIC-CREDITS.txt'; Name = 'MUSIC-CREDITS.txt' },
    @{ Source = 'docs/SEASONAL-MUSIC-SOURCES.md'; Name = 'SEASONAL-MUSIC-SOURCES.md' },
    @{ Source = 'docs/EVENT-SOUND-CREDITS.md'; Name = 'EVENT-SOUND-CREDITS.md' },
    @{ Source = 'docs/HALLOWEEN-AMBIENT-AUDIO.md'; Name = 'HALLOWEEN-AMBIENT-AUDIO.md' },
    @{ Source = 'docs/HALLOWEEN-BAT-FLIGHT.md'; Name = 'HALLOWEEN-BAT-FLIGHT.md' },
    @{ Source = 'docs/HALLOWEEN-PARALLAX.md'; Name = 'HALLOWEEN-PARALLAX.md' },
    @{ Source = 'docs/HALLOWEEN-PROPS-GROUNDING.md'; Name = 'HALLOWEEN-PROPS-GROUNDING.md' },
    @{ Source = 'src/RebornLauncher/assets/fonts/OFL.txt'; Name = 'Barlow-OFL.txt' }
)) {
    $sourcePath = Join-Path $repoRoot $file.Source
    if (-not (Test-Path -LiteralPath $sourcePath -PathType Leaf)) { throw "Launcher-Credits fehlen: $($file.Source)" }
    Copy-Item -LiteralPath $sourcePath -Destination (Join-Path $credits $file.Name) -Force
}
Write-Output "Fertiger Launcher: $(Join-Path $output 'launcher.exe')"
