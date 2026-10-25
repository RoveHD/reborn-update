#ifndef SourceDirectory
  #error SourceDirectory fehlt. tools/build-installer.ps1 verwenden.
#endif
#ifndef OutputDirectory
  #error OutputDirectory fehlt.
#endif
#ifndef AppVersion
  #error AppVersion fehlt.
#endif

[Setup]
AppId={{19E65ED2-29A4-4C1C-BB37-8F8C099160B0}
AppName=Pokémon Reborn Launcher
AppVersion={#AppVersion}
AppVerName=Pokémon Reborn Launcher
AppPublisher=RoveHD
AppPublisherURL=https://github.com/RoveHD/reborn-update
AppSupportURL=https://github.com/RoveHD/reborn-update/issues
AppUpdatesURL=https://github.com/RoveHD/reborn-update/releases
DefaultDirName={localappdata}\Programs\RebornLauncher
DefaultGroupName=Pokémon Reborn
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763
DisableProgramGroupPage=yes
AllowNoIcons=yes
UsePreviousAppDir=no
UsePreviousGroup=no
UsePreviousTasks=no
UsePreviousLanguage=no
CreateUninstallRegKey=not IsIsolatedTest
UninstallDisplayIcon={app}\launcher.exe
UninstallDisplayName=Pokémon Reborn Launcher
SetupIconFile={#SourceDirectory}\reborn.ico
WizardStyle=modern dark
WizardSizePercent=110
Compression=lzma2/fast
SolidCompression=yes
OutputDir={#OutputDirectory}
OutputBaseFilename=Reborn-Launcher-{#AppVersion}-Setup
VersionInfoVersion={#AppVersion}
CloseApplications=no
RestartApplications=no
SetupMutex=RoveHD.RebornLauncher.Setup
UninstallLogMode=append

[Languages]
Name: german; MessagesFile: compiler:Languages\German.isl

[Tasks]
Name: desktopicon; Description: Desktop-Verknüpfung erstellen; Flags: unchecked; Check: not IsIsolatedTest

[Files]
Source: {#SourceDirectory}\launcher.exe; DestDir: {app}; Flags: ignoreversion
Source: {#SourceDirectory}\reborn.ico; DestDir: {app}; Flags: ignoreversion
Source: {#SourceDirectory}\credits\*; DestDir: {app}\credits; Flags: ignoreversion recursesubdirs createallsubdirs
Source: {#SourcePath}\HINWEISE.txt; DestDir: {app}; Flags: ignoreversion

[Icons]
Name: {group}\Pokémon Reborn Launcher; Filename: {app}\launcher.exe; WorkingDir: {app}; Check: not IsIsolatedTest
Name: {autodesktop}\Pokémon Reborn Launcher; Filename: {app}\launcher.exe; WorkingDir: {app}; Tasks: desktopicon; Check: not IsIsolatedTest

[Run]
Filename: {app}\launcher.exe; Description: Pokémon Reborn Launcher starten; WorkingDir: {app}; Flags: nowait postinstall skipifsilent unchecked

[Code]
function IsIsolatedTest: Boolean;
begin
  Result := ExpandConstant('{param:ISOLATEDTEST|0}') = '1';
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  Result := '';
  if FileExists(ExpandConstant('{app}\Game.exe')) or
     FileExists(ExpandConstant('{app}\Game.ini')) or
     FileExists(ExpandConstant('{app}\mkxp.json')) then
    Result := 'Bitte einen eigenen Launcher-Ordner verwenden. Der Installer darf nicht im Reborn-Spielordner installiert werden.';
end;
