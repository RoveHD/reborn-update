param(
    [string]$SourceDirectory = '',
    [string]$OutputDirectory = '',
    [string]$CompilerPath = '',
    [switch]$BootstrapCompiler
)
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
if (-not $SourceDirectory) { $SourceDirectory = Join-Path $repoRoot 'dist/launcher' }
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $repoRoot 'dist/installer' }
$SourceDirectory = [IO.Path]::GetFullPath($SourceDirectory)
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
foreach ($required in @('launcher.exe', 'reborn.ico', 'credits/DESIGN-QUELLEN.md', 'credits/MUSIC-CREDITS.txt', 'credits/Barlow-OFL.txt')) {
    if (-not (Test-Path -LiteralPath (Join-Path $SourceDirectory $required) -PathType Leaf)) { throw "Installer-Eingabe fehlt: $required. Zuerst tools/build.ps1 ausführen." }
}
$versionInfo = [Diagnostics.FileVersionInfo]::GetVersionInfo((Join-Path $SourceDirectory 'launcher.exe'))
$appVersion = '{0}.{1}.{2}' -f $versionInfo.FileMajorPart, $versionInfo.FileMinorPart, $versionInfo.FileBuildPart
if ($appVersion -eq '0.0.0') { throw 'Die Launcher-EXE hat keine gültige Produktversion.' }
$toolkit = Join-Path $repoRoot 'artifacts/installer-toolkit'
if (-not $CompilerPath) { $CompilerPath = Join-Path $toolkit 'inno-7.1.0/ISCC.exe' }
if (-not (Test-Path -LiteralPath $CompilerPath -PathType Leaf) -and $BootstrapCompiler) {
    New-Item -ItemType Directory -Path $toolkit -Force | Out-Null
    $download = Join-Path $toolkit 'innosetup-7.1.0-x64.exe'
    $expectedHash = '0362a383ed217d4c4239b5933866dd96d3eb2102737da92f80f6057a4b40df2f'
    if (-not (Test-Path -LiteralPath $download)) {
        Invoke-WebRequest -Uri 'https://github.com/jrsoftware/issrc/releases/download/is-7_1_0/innosetup-7.1.0-x64.exe' -OutFile $download
    }
    if ((Get-FileHash -LiteralPath $download -Algorithm SHA256).Hash -ine $expectedHash) { throw 'Die Inno-Setup-Prüfsumme stimmt nicht.' }
    $signature = Get-AuthenticodeSignature -LiteralPath $download
    if ($signature.Status -ne 'Valid' -or $signature.SignerCertificate.Subject -notmatch '(^|, )CN=Pyrsys B\.V\.(,|$)') { throw 'Die Inno-Setup-Signatur konnte nicht bestätigt werden.' }
    $toolDirectory = Join-Path $toolkit 'inno-7.1.0'
    $toolArguments = @('/PORTABLE=1', '/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', '/CURRENTUSER', '/NOICONS', '/TASKS=', ('/DIR="' + $toolDirectory + '"'), ('/LOG="' + (Join-Path $toolkit 'install.log') + '"'))
    $toolProcess = Start-Process -FilePath $download -ArgumentList $toolArguments -WindowStyle Hidden -Wait -PassThru
    if ($toolProcess.ExitCode -ne 0) { throw "Inno-Setup-Toolkit konnte nicht entpackt werden: $($toolProcess.ExitCode)" }
}
if (-not (Test-Path -LiteralPath $CompilerPath -PathType Leaf)) { throw 'Inno Setup fehlt. -BootstrapCompiler oder -CompilerPath <ISCC.exe> verwenden.' }
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
& $CompilerPath ('/DSourceDirectory=' + $SourceDirectory) ('/DOutputDirectory=' + $OutputDirectory) ('/DAppVersion=' + $appVersion) (Join-Path $repoRoot 'installer/RebornLauncher.iss')
if ($LASTEXITCODE -ne 0) { throw 'Installer-Build fehlgeschlagen.' }
$setupPath = Join-Path $OutputDirectory "Reborn-Launcher-$appVersion-Setup.exe"
if (-not (Test-Path -LiteralPath $setupPath)) { throw 'Der Compiler hat keine Setup-EXE erzeugt.' }
$buildReport = [ordered]@{ version = $appVersion; compiler = 'Inno Setup 7.1.0'; setup = $setupPath; setupSha256 = (Get-FileHash -LiteralPath $setupPath -Algorithm SHA256).Hash.ToLowerInvariant(); launcherSha256 = (Get-FileHash -LiteralPath (Join-Path $SourceDirectory 'launcher.exe') -Algorithm SHA256).Hash.ToLowerInvariant(); perUser = $true; administratorRequired = $false; codeSigned = $false }
$buildReport | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $OutputDirectory "Reborn-Launcher-$appVersion-Setup.json") -Encoding utf8
$buildReport | ConvertTo-Json
