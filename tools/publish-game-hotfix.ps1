param(
    [Parameter(Mandatory = $true)][string]$ReleaseDirectory,
    [switch]$Publish,
    [switch]$VerifyPublic
)
$ErrorActionPreference = 'Stop'
if ($Publish -and $VerifyPublic) { throw '-Publish und -VerifyPublic sind getrennte Modi.' }
$releaseRoot = (Resolve-Path -LiteralPath $ReleaseDirectory).Path
$repository = 'RoveHD/reborn-update'
$manifestPath = Join-Path $releaseRoot 'update.json'
$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
$parityProof = Join-Path $releaseRoot 'source-parity.json'
if (-not (Test-Path -LiteralPath $parityProof)) { throw 'Pflichtnachweis fehlt: source-parity.json. Der Hotfix muss gegen die vollständige lokale Spielquelle neu gebaut werden.' }
& python (Join-Path $PSScriptRoot 'verify_release_parity.py') --release-dir $releaseRoot
if ($LASTEXITCODE -ne 0) { throw 'Lokale Spielquelle, Hotfix-Fallbackarchiv und Dateiinventar stimmen nicht überein.' }
$version = $manifest.game.version
$tag = "v$version"
$gameAsset = [IO.Path]::GetFileName(([Uri]$manifest.game.url).AbsolutePath)
if ($gameAsset -ne 'Reborn-AllGen-0.9.9.zip') { throw 'Unerwarteter Spielpaketname. Legacy-Clients dürfen dieses Paket nicht installieren.' }
$launcherVersion = [Version]::new()
$gameVersion = [Version]::new()
if ($manifest.schemaVersion -ne 1 -or
    $manifest.launcher.version -notmatch '^\d+\.\d+\.\d+$' -or $version -notmatch '^\d+\.\d+\.\d+$' -or
    -not [Version]::TryParse($manifest.launcher.version, [ref]$launcherVersion) -or
    -not [Version]::TryParse($version, [ref]$gameVersion)) { throw 'Unerwartete Hotfix-Version.' }
foreach ($entry in ,@($gameAsset, $manifest.game)) {
    $path = Join-Path $releaseRoot $entry[0]
    $hash = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash
    if ($hash -ne $entry[1].sha256 -or (Get-Item -LiteralPath $path).Length -ne $entry[1].size) { throw "Datei stimmt nicht mit dem geprüften Manifest überein: $path" }
    $expected = "https://github.com/$repository/releases/download/$tag/$($entry[0])"
    if ($entry[1].url -ne $expected) { throw "Falsche Release-Adresse: $($entry[1].url)" }
}
foreach ($name in @('package-checks.json','hotfix-checks.json','release-install.json','release-repair.json')) {
    $checks = Get-Content -LiteralPath (Join-Path $releaseRoot $name) -Raw | ConvertFrom-Json
    if ($checks.success -ne $true) { throw "Prüfung nicht erfolgreich: $name" }
}
$hotfixChecks = Get-Content -LiteralPath (Join-Path $releaseRoot 'hotfix-checks.json') -Raw | ConvertFrom-Json
if (-not $hotfixChecks.launcherUnchanged -or $hotfixChecks.archiveSha256 -ne $manifest.game.sha256) { throw 'Hotfix-Nachweis passt nicht.' }
$installChecks = Get-Content -LiteralPath (Join-Path $releaseRoot 'release-install.json') -Raw | ConvertFrom-Json
$targetInventory = Get-Content -LiteralPath (Join-Path $releaseRoot 'game-files.json') -Raw | ConvertFrom-Json -AsHashtable
$managedFiles = $targetInventory.Count
$installedFiles = if ($null -ne $installChecks.installedFilesVerified) { $installChecks.installedFilesVerified } else { $installChecks.newFilesVerified }
if ($installChecks.version -ne $version -or $installChecks.repair -ne $false -or
    $installChecks.archiveSha256 -ne $manifest.game.sha256 -or $managedFiles -le 0 -or $installedFiles -ne $managedFiles -or
    @($installChecks.personalFilesUnchanged).Count -lt 4) { throw 'Die isolierte Hotfix-Installation hat nicht jede verwaltete Paketdatei und die Benutzerdateien verifiziert.' }
if ($gameVersion -le [Version]::Parse($hotfixChecks.baselineGameVersion)) { throw 'Der Hotfix muss neuer als seine Ausgangsversion sein.' }
foreach ($name in @('release-repair.json')) {
    $checks = Get-Content -LiteralPath (Join-Path $releaseRoot $name) -Raw | ConvertFrom-Json
    if ($checks.archiveSha256 -ne $manifest.game.sha256 -or $checks.version -ne $version -or
        $checks.repair -ne $true -or $checks.installedFilesVerified -ne $managedFiles -or
        $checks.unchangedFileTimestampPreserved -ne $true -or @($checks.personalFilesUnchanged).Count -lt 4) { throw "Reparaturpruefung fehlt oder gehoert zu anderem Paket: $name" }
}
$candidateManifestHash = (Get-FileHash -LiteralPath $manifestPath -Algorithm SHA256).Hash
$inventoryHash = (Get-FileHash -LiteralPath (Join-Path $releaseRoot 'game-files.json') -Algorithm SHA256).Hash
$hotfixes = @(@($manifest.gameHotfixes) + @($manifest.gameHotfixesV2) | Where-Object { $null -ne $_ })
$deltas = @($manifest.gameHotfixesV2)
if ($deltas.Count -eq 0 -or @($deltas | Where-Object { $_.fromVersion -eq $hotfixChecks.baselineGameVersion }).Count -ne 1) {
    throw 'Der Hotfix braucht ein V2-Delta fuer seine deklarierte Ausgangsversion.'
}
if (@($deltas.fromVersion | Select-Object -Unique).Count -ne $deltas.Count) { throw 'Doppelte Delta-Ausgangsversion.' }
foreach ($hotfix in $hotfixes) {
    $name = [IO.Path]::GetFileName(([Uri]$hotfix.url).AbsolutePath)
    if ($hotfix.fromVersion -notmatch '^\d+\.\d+\.\d+$' -or $hotfix.version -ne $version -or
        $hotfix.url -ne "https://github.com/$repository/releases/download/$tag/$name") { throw 'Falsche Hotfix-Adresse oder Version.' }
    $path = Join-Path $releaseRoot $name
    if ((Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -ne $hotfix.sha256 -or
        (Get-Item -LiteralPath $path).Length -ne $hotfix.size) { throw 'Hotfix-Pruefsumme stimmt nicht.' }
    if (@($deltas | Where-Object { $_.fromVersion -eq $hotfix.fromVersion -and $_.sha256 -eq $hotfix.sha256 }).Count -ne 1) {
        throw 'Jeder angebotene Legacy-Hotfix muss durch dasselbe gepruefte V2-Delta abgedeckt sein.'
    }
}

function Assert-DeltaProof($Delta, [string]$Mode) {
    $name = "release-update-delta-$Mode-from-$($Delta.fromVersion).json"
    $proof = Get-Content -LiteralPath (Join-Path $releaseRoot $name) -Raw | ConvertFrom-Json
    $expectedPublic = $Mode -eq 'public'
    if ($proof.success -ne $true -or $proof.proofMode -ne $Mode -or $proof.realUpdateServiceUsed -ne $true -or
        $proof.version -ne $version -or $proof.previousVersion -ne $Delta.fromVersion -or
        $proof.candidateManifestSha256 -ne $candidateManifestHash -or $proof.targetInventorySha256 -ne $inventoryHash -or
        $proof.archiveSha256 -ne $manifest.game.sha256 -or $proof.archiveSize -ne $manifest.game.size -or
        $proof.deltaSha256 -ne $Delta.sha256 -or $proof.deltaSize -ne $Delta.size -or $proof.candidateDeltaUrl -ne $Delta.url -or
        $proof.candidateManifestUrl -ne 'https://raw.githubusercontent.com/RoveHD/reborn-update/main/update.json' -or
        $proof.fullPackageDownloaded -ne $false -or $proof.installedTargetFilesVerified -ne $managedFiles -or
        $proof.baselineFixtureUnchanged -ne $true -or $proof.switchesAfterDownload -ne 2 -or
        $proof.deletedPathsVerified -ne @($Delta.files | Where-Object { $_.delete -eq $true }).Count -or
        $proof.addedPathsVerifiedAfterSwitchBack -ne @($Delta.files | Where-Object { $_.beforeExists -eq $false }).Count -or
        @($proof.personalSentinelsPreserved).Count -lt 4 -or $proof.updateServiceUsedPublicChannel -ne $expectedPublic) {
        throw "Der echte Launcher-Delta-Nachweis fehlt oder passt nicht zum Release: $name"
    }
    if ($expectedPublic) {
        if ($proof.publicManifestUrl -ne $proof.candidateManifestUrl -or $proof.publicDeltaUrl -ne $Delta.url) { throw "Oeffentlicher Download nicht belegt: $name" }
    } elseif ($null -ne $proof.publicManifestUrl -or $null -ne $proof.publicDeltaUrl) { throw "Lokaler Nachweis behauptet oeffentlichen Download: $name" }
    $previousManifest = Get-Content -LiteralPath (Join-Path $proof.previousReleaseDirectory 'update.json') -Raw | ConvertFrom-Json
    $previousInventory = Join-Path $proof.previousReleaseDirectory 'game-files.json'
    if ($previousManifest.game.version -ne $Delta.fromVersion -or
        (Get-FileHash -LiteralPath $previousInventory -Algorithm SHA256).Hash -ne $proof.previousInventorySha256 -or
        (Get-Content -LiteralPath $previousInventory -Raw | ConvertFrom-Json -AsHashtable).Count -ne $proof.previousFilesVerifiedAfterSwitch -or
        -not (Test-Path -LiteralPath $proof.baselineInstallRoot -PathType Container)) { throw "Ausgangsversion des Nachweises passt nicht: $name" }
    return $proof
}
$localProofs = @{}
foreach ($delta in $deltas) { $localProofs[$delta.fromVersion] = Assert-DeltaProof $delta 'local' }

# Derive translation changes from the verified inventories, never from optional changedPaths metadata.
$translationChanged = $false
foreach ($local in $localProofs.Values) {
    $previousInventory = Get-Content -LiteralPath (Join-Path $local.previousReleaseDirectory 'game-files.json') -Raw | ConvertFrom-Json -AsHashtable
    foreach ($relative in @('intl.txt', 'Data/german.dat')) {
        $before = $previousInventory[$relative]
        $after = $targetInventory[$relative]
        if (($null -eq $before) -ne ($null -eq $after) -or
            ($null -ne $before -and ($before.sha256 -ne $after.sha256 -or $before.size -ne $after.size))) { $translationChanged = $true }
    }
}
$germanProofPath = Join-Path $releaseRoot 'german-hotfix-checks.json'
if ($translationChanged -and -not (Test-Path -LiteralPath $germanProofPath -PathType Leaf)) {
    throw 'Geaenderte Uebersetzungsdateien erfordern german-hotfix-checks.json mit installiertem Runtime- und UI-Nachweis.'
}
if ($translationChanged -or (Test-Path -LiteralPath $germanProofPath -PathType Leaf)) {
    $german = Get-Content -LiteralPath $germanProofPath -Raw | ConvertFrom-Json
    $runtime = $german.installedRuntimeProof
    if ($null -eq $targetInventory['intl.txt'] -or $null -eq $targetInventory['Data/german.dat'] -or
        $german.success -ne $true -or $german.version -ne $version -or $german.archiveSha256 -ne $manifest.game.sha256 -or
        $german.sourceIntlSha256 -ne $targetInventory['intl.txt'].sha256 -or
        $german.compiledSemanticChanges -le 0 -or $german.runtimeTranslationLookups -le 0 -or
        $german.loadedItems -le 0 -or $german.bagSelectionsRendered -ne $german.loadedItems -or
        $null -eq $german.PSObject.Properties['itemErrors'] -or @($german.itemErrors).Count -ne 0 -or
        $null -eq $german.PSObject.Properties['bagErrors'] -or @($german.bagErrors).Count -ne 0 -or $german.overflowItems -ne 0 -or
        $null -eq $runtime -or $runtime.success -ne $true -or
        $runtime.installedFilesVerifiedBeforeInstrumentation -ne $managedFiles -or
        $runtime.loadedItems -le 0 -or $runtime.loadedItems -ne $german.loadedItems -or
        $runtime.bagSelectionsRendered -ne $runtime.loadedItems -or
        $runtime.itemErrors -ne 0 -or $runtime.bagErrors -ne 0 -or $runtime.overflowItems -ne 0 -or
        $runtime.runtimeTranslationLookups -le 0 -or $runtime.runtimeTranslationLookups -ne $german.runtimeTranslationLookups -or
        $runtime.engineExitCode -ne 0) {
        throw 'Uebersetzungs-/UI-Nachweis fehlt, enthaelt Fehler oder passt nicht zum Paket: german-hotfix-checks.json'
    }
    # Bind the rendered scene observations to a complete installed copy of this exact candidate.
    function Read-LocalProof([string]$Path, [string]$ExpectedSha256) {
        if ([string]::IsNullOrWhiteSpace($Path)) { throw 'Dateipfad eines installierten Runtime-Nachweises fehlt.' }
        if (-not [IO.Path]::IsPathRooted($Path)) { $Path = Join-Path (Join-Path $PSScriptRoot '..') $Path }
        if ($ExpectedSha256 -notmatch '^[a-fA-F0-9]{64}$' -or
            (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash -ne $ExpectedSha256) {
            throw "Installierter Runtime-Dateinachweis ist veraltet oder unvollstaendig (SHA-256): $Path"
        }
        return (Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json)
    }
    $inputProof = Read-LocalProof $runtime.inputProof $runtime.inputProofSha256
    $sceneProof = Read-LocalProof $runtime.runtimeReport $runtime.runtimeReportSha256
    if ($inputProof.success -ne $true -or $inputProof.version -ne $version -or
        $inputProof.archiveSha256 -ne $manifest.game.sha256 -or $inputProof.manifestSha256 -ne $candidateManifestHash -or
        $inputProof.installedFilesVerifiedBeforeInstrumentation -ne $managedFiles -or
        $null -eq $sceneProof.PSObject.Properties['errors'] -or @($sceneProof.errors).Count -ne 0 -or
        $null -eq $sceneProof.PSObject.Properties['bag_errors'] -or @($sceneProof.bag_errors).Count -ne 0 -or
        @($sceneProof.items).Count -ne $runtime.loadedItems -or @($sceneProof.bag_selections).Count -ne $runtime.loadedItems -or
        @($sceneProof.bag_selections | Select-Object -Unique).Count -ne $runtime.loadedItems -or
        @($sceneProof.translations).Count -ne $runtime.runtimeTranslationLookups) {
        throw 'Installierter Uebersetzungs-/UI-Eingabe- oder Szenennachweis ist veraltet oder unvollstaendig.'
    }
    $itemIds = @($sceneProof.items | ForEach-Object { $_.id })
    if (@($itemIds | Select-Object -Unique).Count -ne $runtime.loadedItems -or
        @(Compare-Object $itemIds @($sceneProof.bag_selections)).Count -ne 0) { throw 'Gerenderte Beutelauswahl deckt nicht jedes geladene Item genau einmal ab.' }
    foreach ($item in $sceneProof.items) {
        foreach ($context in @('bag', 'pc', 'mart')) {
            $layout = $item.layouts.$context
            if ($null -eq $layout -or $layout.overflow -ne $false -or $layout.width -le 0 -or $layout.height -le 0 -or
                $null -eq $layout.text_width -or $null -eq $layout.text_height -or
                $layout.text_width -gt $layout.width -or $layout.text_height -gt $layout.height) {
                throw "Itembeschreibung nicht vollstaendig dargestellt: $($item.id) / $context"
            }
        }
    }
}

# Build before any external mutation so a missing/broken test runner cannot strand publication halfway through.
$checkProject = Join-Path $PSScriptRoot '../tests/ReleaseChecks/ReleaseChecks.csproj'
if ($Publish -or $VerifyPublic) {
    & dotnet build $checkProject --configuration Release --nologo
    if ($LASTEXITCODE -ne 0) { throw 'ReleaseChecks konnte nicht gebaut werden.' }
}
function Test-PublicInstallation {
    $publicOutput = Join-Path (Split-Path $releaseRoot -Parent) ((Split-Path $releaseRoot -Leaf) + '-public-checks')
    foreach ($delta in $deltas) {
        $local = $localProofs[$delta.fromVersion]
        & dotnet run --project $checkProject --configuration Release --no-build -- $releaseRoot $publicOutput --delta-update-public $local.previousReleaseDirectory $local.baselineInstallRoot
        if ($LASTEXITCODE -ne 0) { throw "Oeffentliche Launcher-Installation fehlgeschlagen: $($delta.fromVersion) -> $version. Der Kanal kann bereits veroeffentlicht sein; keine Erfolgsmeldung." }
        $null = Assert-DeltaProof $delta 'public'
    }
}
if ($VerifyPublic) {
    Test-PublicInstallation
    Write-Output "Oeffentlicher Launcher-Download und Installation geprueft: $tag. Keine Veroeffentlichung durch diesen Aufruf."
    exit 0
}
if (-not $Publish) {
    Write-Output "Lokal geprueft: $tag, alle $($deltas.Count) Delta-Ausgangsversionen. Kein oeffentlicher Download belegt. Zum Veroeffentlichen erneut mit -Publish aufrufen; danach erfolgt die oeffentliche Launcher-Pruefung."
    exit 0
}

function Invoke-GhJson([string[]]$Arguments, $Body = $null) {
    if ($null -eq $Body) {
        $result = & gh @Arguments
    } else {
        $file = Join-Path $releaseRoot ('gh-request-' + [guid]::NewGuid().ToString('N') + '.json')
        try {
            [IO.File]::WriteAllText($file, ($Body | ConvertTo-Json -Depth 20), [Text.UTF8Encoding]::new($false))
            $result = & gh @Arguments --input $file
        } finally { Remove-Item -LiteralPath $file -Force -ErrorAction SilentlyContinue }
    }
    if ($LASTEXITCODE -ne 0) { throw "GitHub-Aufruf fehlgeschlagen: $($Arguments -join ' ')" }
    return ($result | ConvertFrom-Json)
}

# A draft never affects existing clients. Re-running can finish a failed upload.
$releases = Invoke-GhJson @('api', "repos/$repository/releases?per_page=100")
$existing = $releases | Where-Object { $_.tag_name -eq $tag } | Select-Object -First 1
if ($existing -and -not $existing.draft) { throw "$tag ist bereits veröffentlicht. Veröffentlichte Pakete nicht überschreiben; neue Version bauen." }
if (-not $existing) {
    $repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
    $sourceCommit = (& git -C $repositoryRoot rev-parse HEAD).Trim()
    if ($LASTEXITCODE -ne 0) { throw 'Quellcode-Commit konnte nicht bestimmt werden.' }
    & gh release create $tag --repo $repository --target $sourceCommit --draft --title "Spielhotfix $version" --notes-file (Join-Path $releaseRoot 'patchnotes.txt')
    if ($LASTEXITCODE -ne 0) { throw 'Release-Entwurf konnte nicht erstellt werden.' }
}
$assets = @($gameAsset,'update.json','game-files.json','source-parity.json','SHA256SUMS.txt','patchnotes.txt','hotfix-checks.json','package-checks.json','release-install.json','release-repair.json')
foreach ($delta in $deltas) { $assets += "release-update-delta-local-from-$($delta.fromVersion).json" }
$addonName = "Reborn-$version-Zusatzdateien.zip"
if (Test-Path -LiteralPath (Join-Path $releaseRoot $addonName)) { $assets += $addonName }
if (Test-Path -LiteralPath (Join-Path $releaseRoot 'catch-checks.json')) { $assets += 'catch-checks.json' }
if (Test-Path -LiteralPath (Join-Path $releaseRoot 'german-hotfix-checks.json')) { $assets += 'german-hotfix-checks.json' }
if (Test-Path -LiteralPath (Join-Path $releaseRoot 're-mods-checks.json')) { $assets += 're-mods-checks.json' }
foreach ($name in @('TRANSLATION-CHANGELOG.md', 'translation-changes.json', "item-regression-checks-$version.zip")) {
    if (Test-Path -LiteralPath (Join-Path $releaseRoot $name) -PathType Leaf) { $assets += $name }
}
foreach ($hotfix in $hotfixes) {
    $name = [IO.Path]::GetFileName(([Uri]$hotfix.url).AbsolutePath)
    if ($hotfix.version -ne $version -or $hotfix.url -ne "https://github.com/$repository/releases/download/$tag/$name") { throw 'Falsche Hotfix-Adresse oder Zielversion.' }
    $path = Join-Path $releaseRoot $name
    if ((Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -ne $hotfix.sha256 -or (Get-Item -LiteralPath $path).Length -ne $hotfix.size) { throw 'Hotfix-Pruefsumme stimmt nicht.' }
    $assets += $name
}
foreach ($name in $assets) {
    $path = Join-Path $releaseRoot $name
    $alreadyUploaded = $existing.assets | Where-Object { $_.name -eq $name } | Select-Object -First 1
    $digest = 'sha256:' + (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($alreadyUploaded -and $alreadyUploaded.state -eq 'uploaded' -and
        $alreadyUploaded.size -eq (Get-Item -LiteralPath $path).Length -and $alreadyUploaded.digest -eq $digest) {
        Write-Output "Bereits vollständig hochgeladen und geprüft: $name"
        continue
    }
    & gh release upload $tag (Join-Path $releaseRoot $name) --repo $repository --clobber
    if ($LASTEXITCODE -ne 0) { throw "Upload fehlgeschlagen: $name. Entwurf bleibt unveröffentlicht." }
}
# GitHub's by-tag endpoint does not expose an unpublished draft reliably.
$release = (Invoke-GhJson @('api', "repos/$repository/releases?per_page=100")) | Where-Object { $_.tag_name -eq $tag } | Select-Object -First 1
if (-not $release -or -not $release.draft) { throw 'Der erwartete Release-Entwurf fehlt.' }
foreach ($name in $assets) {
    $asset = $release.assets | Where-Object { $_.name -eq $name } | Select-Object -First 1
    $path = Join-Path $releaseRoot $name
    if (-not $asset -or $asset.size -ne (Get-Item -LiteralPath $path).Length -or $asset.state -ne 'uploaded') { throw "Unvollständiger Upload: $name" }
    $digest = 'sha256:' + (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()
    if (-not $asset.digest -or $asset.digest -ne $digest) { throw "GitHub-Prüfsumme stimmt nicht: $name" }
}

# Prepare one atomic metadata commit. Existing clients only discover it AFTER all assets are public.
$reference = Invoke-GhJson @('api', "repos/$repository/git/ref/heads/main")
$head = $reference.object.sha
$currentFile = Invoke-GhJson @('api',"repos/$repository/contents/update.json?ref=$head")
$current = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($currentFile.content)) | ConvertFrom-Json
foreach ($property in @('version','url','sha256','size')) {
    if ($current.launcher.$property -ne $manifest.launcher.$property) { throw 'Der Launcher-Kanal wurde inzwischen geaendert.' }
}
if ($current.game.version -ne $hotfixChecks.baselineGameVersion) { throw 'Der Spiel-Kanal wurde inzwischen geaendert.' }

$commit = Invoke-GhJson @('api', "repos/$repository/git/commits/$head")
$entries = @()
# Keep the old version.txt feed unchanged: the legacy updater cannot apply the
# hash-guarded removal/rename migration. Its old asset name is not republished.
foreach ($name in @('update.json','patchnotes.txt')) {
    $content = [IO.File]::ReadAllText((Join-Path $releaseRoot $name))
    $entries += @{ path=$name; mode='100644'; type='blob'; content=$content }
}
$tree = Invoke-GhJson @('api','--method','POST',"repos/$repository/git/trees") @{ base_tree=$commit.tree.sha; tree=$entries }
$newCommit = Invoke-GhJson @('api','--method','POST',"repos/$repository/git/commits") @{ message="Publish verified Reborn update $version"; tree=$tree.sha; parents=@($head) }
& gh release edit $tag --repo $repository --draft=false --latest=false
if ($LASTEXITCODE -ne 0) { throw 'Release konnte nicht veröffentlicht werden; Manifest ist unverändert.' }
Invoke-GhJson @('api','--method','PATCH',"repos/$repository/git/refs/heads/main") @{ sha=$newCommit.sha; force=$false } | Out-Null
$verification = Invoke-GhJson @('api',"repos/$repository/contents/update.json?ref=main")
$published = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($verification.content)) | ConvertFrom-Json
if ($published.game.sha256 -ne $manifest.game.sha256 -or $published.launcher.sha256 -ne $manifest.launcher.sha256) { throw 'Veröffentlichte Metadaten stimmen nicht überein.' }
Test-PublicInstallation
foreach ($delta in $deltas) {
    $name = "release-update-delta-public-from-$($delta.fromVersion).json"
    & gh release upload $tag (Join-Path $releaseRoot $name) --repo $repository --clobber
    if ($LASTEXITCODE -ne 0) { throw "Oeffentliche Installation erfolgreich, aber Nachweis-Upload fehlgeschlagen: $name" }
}
Write-Output "Veröffentlicht und durch oeffentliche Launcher-Installation geprueft: https://github.com/$repository/releases/tag/$tag"
