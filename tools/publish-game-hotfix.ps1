param(
    [Parameter(Mandatory = $true)][string]$ReleaseDirectory,
    [switch]$Publish
)
$ErrorActionPreference = 'Stop'
$releaseRoot = (Resolve-Path -LiteralPath $ReleaseDirectory).Path
$repository = 'RoveHD/reborn-update'
$manifestPath = Join-Path $releaseRoot 'update.json'
$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
$version = $manifest.game.version
$tag = "v$version"
$gameAsset = [IO.Path]::GetFileName(([Uri]$manifest.game.url).AbsolutePath)
if ($gameAsset -ne 'Reborn-AllGen-0.9.9.zip') { throw 'Unerwarteter Spielpaketname. Legacy-Clients dürfen dieses Paket nicht installieren.' }
if ($manifest.schemaVersion -ne 1 -or $manifest.launcher.version -ne '7.3.0' -or $version -ne '7.3.1') { throw 'Unerwartete Hotfix-Version.' }
foreach ($entry in ,@($gameAsset, $manifest.game)) {
    $path = Join-Path $releaseRoot $entry[0]
    $hash = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash
    if ($hash -ne $entry[1].sha256 -or (Get-Item -LiteralPath $path).Length -ne $entry[1].size) { throw "Datei stimmt nicht mit dem geprüften Manifest überein: $path" }
    $expected = "https://github.com/$repository/releases/download/$tag/$($entry[0])"
    if ($entry[1].url -ne $expected) { throw "Falsche Release-Adresse: $($entry[1].url)" }
}
foreach ($name in @('package-checks.json','hotfix-checks.json','release-install.json','release-repair.json')) {
    $checks = Get-Content -LiteralPath (Join-Path $releaseRoot $name) -Raw | ConvertFrom-Json
    if (-not $checks.success) { throw "Prüfung nicht erfolgreich: $name" }
}
$hotfixChecks = Get-Content -LiteralPath (Join-Path $releaseRoot 'hotfix-checks.json') -Raw | ConvertFrom-Json
if (-not $hotfixChecks.launcherUnchanged -or $hotfixChecks.archiveSha256 -ne $manifest.game.sha256) { throw 'Hotfix-Nachweis passt nicht.' }
foreach ($name in @('release-install.json','release-repair.json')) {
    $checks = Get-Content -LiteralPath (Join-Path $releaseRoot $name) -Raw | ConvertFrom-Json
    if ($checks.archiveSha256 -ne $manifest.game.sha256 -or $checks.version -ne $version) { throw "Pruefung gehoert zu anderem Paket: $name" }
}
if (-not $Publish) {
    Write-Output "Geprüft: $tag. Keine Veröffentlichung. Zum Veröffentlichen erneut mit -Publish aufrufen."
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
    $sourceCommit = (& git rev-parse HEAD).Trim()
    if ($LASTEXITCODE -ne 0) { throw 'Quellcode-Commit konnte nicht bestimmt werden.' }
    & gh release create $tag --repo $repository --target $sourceCommit --draft --title "Spielhotfix $version · Menüs, Street-Namen und Kampf-Sounds" --notes-file (Join-Path $releaseRoot 'patchnotes.txt')
    if ($LASTEXITCODE -ne 0) { throw 'Release-Entwurf konnte nicht erstellt werden.' }
}
$assets = @($gameAsset,'update.json','game-files.json','SHA256SUMS.txt','patchnotes.txt','hotfix-checks.json','release-install.json','release-repair.json')
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
Write-Output "Veröffentlicht und geprüft: https://github.com/$repository/releases/tag/$tag"
