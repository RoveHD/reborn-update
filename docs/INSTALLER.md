# Installierbarer Pokémon Reborn Launcher

Das Setup installiert den Launcher für das angemeldete Windows-Konto nach
`%LOCALAPPDATA%\Programs\RebornLauncher`. Administratorrechte sind nicht nötig.
Es richtet eine Startmenü-Verknüpfung und eine Deinstallation in Windows ein.
Eine zusätzliche Desktop-Verknüpfung ist optional. Der Launcher startet am Ende
nur, wenn die entsprechende Auswahl ausdrücklich aktiviert wird.

Der Installer enthält die eigenständig ausführbare Launcher-EXE, das Reborn-Icon,
Hinweise und Credits. Er enthält keine Spielinstallation und akzeptiert keinen
Ordner mit `Game.exe`, `Game.ini` oder `mkxp.json` als Installationsziel.

## GitHub-Selbstupdate

Die installierte Anwendung nutzt dieselbe geprüfte Selbstaktualisierung wie die
portable C#-Version. `SelfUpdateService.CheckAndApplyAsync` liest beim Start und
vor manuellen Updates das Manifest aus
`https://raw.githubusercontent.com/RoveHD/reborn-update/main/update.json`.
Nur eine höhere `launcher.version` löst einen Download aus. Die feste
Release-Adresse, Dateigröße und SHA-256-Prüfsumme werden geprüft. Ein separater
Helfer wartet auf das Ende der alten EXE, sichert sie als `launcher.exe.previous`,
ersetzt sie und startet die neue Anwendung. Die neue Anwendung bestätigt den
Start und setzt anschließend die Spielaktualisierung fort. Die Verknüpfungen
bleiben gültig, weil sich der Installationspfad nicht ändert.

Bei der Diagnose am 25. Oktober 2026 enthielt das öffentliche GitHub-Manifest
noch Launcher 7.0.0. Die lokal verteilte Designfassung 7.1.0 konnte davon kein
neueres Update erhalten. Die portable Verpackung ist im vorliegenden C#-Code
keine Sperre für Selbstupdates. Release 7.2.0 veröffentlicht das neue Setup
und die dazugehörige Launcher-EXE; die Launcher-Sektion des Manifests wird erst
nach dem vollständigen Upload auf diese Version gesetzt. Das Spielmanifest
bleibt unverändert bei AllGen 0.9.9 / Paketversion 7.0.0.

Die Installation unter LocalAppData stellt einen beschreibbaren, dauerhaften
Launcher-Ordner bereit. Dadurch hängt der Austausch nicht von Schreibrechten
in einem fremden Spiel- oder Programmordner ab. Das Setup selbst wird nicht bei
jedem Update neu ausgeführt; die Anwendung aktualisiert ihre einzelne EXE.

## Einstellungen und Deinstallation

Eine vorhandene `settings.json` im Installationsziel wird nie vom Setup
überschrieben. Ein erster Wechsel von der portablen Anwendung übernimmt keinen
geratenen Spielpfad: Den gewünschten Spielordner einmal im Launcher auswählen.
Andere portable Ordner und Spiele werden vom Installer nicht durchsucht.

Die Deinstallation entfernt ausschließlich registrierte Programmdateien.
Persönliche Einstellungen und vom Launcher angelegte Sicherungen bleiben
erhalten. Ein späteres Setup kann die Einstellungen wiederverwenden. Die
Spielinstallation und ihre Spielstände werden vom Installer und Deinstaller
nicht geändert.

## Bauen und prüfen

```powershell
./tools/build.ps1
./tools/build-installer.ps1 -BootstrapCompiler
./installer/test-installer.ps1
```

`-BootstrapCompiler` lädt bei fehlendem Compiler das offizielle, versionsfeste
Inno Setup 7.1.0 x64. Vor dem Start werden die feste SHA-256-Prüfsumme und die
gültige Authenticode-Signatur von Pyrsys B.V. geprüft. Das Toolkit wird mit dem
vom Hersteller unterstützten portablen Modus ausschließlich nach
`artifacts/installer-toolkit/inno-7.1.0` entpackt. Es wird kein globales
Entwicklerwerkzeug installiert. Alternativ kann `-CompilerPath <ISCC.exe>`
übergeben werden. Inno Setup wird ausschließlich als Buildwerkzeug benötigt;
Mitspieler brauchen weder Inno Setup noch eine separate .NET-Installation.

Der Build liest die tatsächliche Dateiversion der geprüften
`dist/launcher/launcher.exe` und erzeugt
`dist/installer/Reborn-Launcher-<Version>-Setup.exe`. Ein danebenliegender
JSON-Nachweis enthält die SHA-256-Prüfsummen von Setup und Launcher.

Der Installertest verwendet einen neuen Ordner unter `test-results` auf D:.
Er installiert dort die tatsächliche Setup-EXE, installiert erneut, prüft
unveränderte Einstellungen, lehnt einen synthetischen Spielordner ab,
aktualisiert die installierte EXE mit dem echten Selbstupdate-Helfer, prüft den
bestätigten Neustart und die vorherige EXE-Sicherung und deinstalliert genau
diese Testinstallation. Synthetische Spiel-/Spielstanddateien müssen identisch
bleiben. Die Anwendung läuft dabei im vorhandenen Offline-Smoke-Modus; kein
Spiel wird gestartet. Der Bericht steht in `test-results/installer.json`.

Der Testparameter `/ISOLATEDTEST=1` deaktiviert ausschließlich Verknüpfungen und
die Windows-Deinstallationsregistrierung für diesen isolierten Test. Er
deaktiviert weder die Dateiinhalte noch die Deinstallation. Der Test vergleicht
die tatsächlichen Benutzerregistrierungs- und Verknüpfungseinträge vor und nach
dem Lauf. Die normale Installation legt die vorgesehenen Einträge an.

Launcher und Setup sind nicht mit einem eigenen Code-Signing-Zertifikat
signiert. Ein erfolgreicher lokaler Test bestätigt keinen späteren Download auf
anderen Rechnern; der veröffentlichte GitHub-Stand wird separat geprüft.

Quellen: [Offizieller Inno-Setup-Download](https://jrsoftware.org/isdl.php),
[portabler Toolkit-Modus](https://jrsoftware.org/ishelp/topic_technotes.htm),
[Installation ohne Administratorrechte](https://jrsoftware.org/ishelp/topic_setup_privilegesrequired.htm).
