# Launcher 7.3.7 – Release-Prüfung

Geprüft am 26.10.2026. Dieses Release betrifft ausschließlich den Launcher.
Das Spielpaket 7.3.5 / AllGen 0.9.9, alle Spielhotfixes und die übrigen
Update-Kanalfelder bleiben unverändert. Tests verwenden isolierte Ordner auf D:.

## Fertige Dateien

- `launcher.exe`: SHA-256 `7ebcc0ae50cadf7b88e62a926f03f1bd1bb2baee859cebfdf226f29613aae289`
- `Reborn-Launcher-7.3.7-Setup.exe`: SHA-256 `4d76a028b7125e6c40f185f17c01b1f68b509c936a4897d19c481f00a58c366c`
- Build und Inno-Setup-Build erfolgreich. Das Setup installiert pro Benutzer ohne Administratorrechte. Die Dateien sind nicht codesigniert.

## Ausgeführte Prüfungen

- `installer/test-installer.ps1`: Installation, Neuinstallation mit erhaltenen Einstellungen, Ablehnung eines Spielordners als Installationsziel, echter EXE-Austausch und bestätigter Neustart, Sicherung der alten EXE und Deinstallation erfolgreich. Spiel-/Einstellungs-Sentinels sowie globale Registrierung und Verknüpfungen bleiben unverändert. Bericht: `test-results/installer.json`.
- Die dabei gestartete finale EXE prüft 8 Musikstücke, 50 Event-Sounds, 12 Halloween-Ambient-Sounds, 8 Prop-Interaktionen, 16 Tag-/Nacht-Ansichten sowie kompakte und maximierte Layouts. Kalender, Maus-Effekte und Musikwechsel erfolgreich.
- `tools/test-lighting.ps1`: 8 Paletten, kontinuierliche Übergänge, Richtungswechsel, reduzierte Bewegung und Artwork-Blende erfolgreich, an den obigen EXE-Hash gebunden. Bericht: `test-results/lighting-release.json`.
- Prop-Bodenprüfung: 30 Fenster-/Kamerafälle einschließlich der letzten Bodenanker-Korrekturen. Bericht: `test-results/grounded-props-final/result.json`.
- Navigation: Props bleiben bei Changelog, Extras und Einstellungen sichtbar und behalten Instanzen und Positionen. Alle 8 Themes und kleine, breite und hohe Fenster geprüft; Scrollen, Escape, Unterdialoge und Updateabbruch erfolgreich. Bericht: `test-results/props-persistent-navigation/result.json`.
- Parallax: 25 Fälle mit 5 unabhängigen Ebenen. Bericht: `test-results/parallax/checks.json`.
- Gengar-Flug: echte Timer, Pixel-Isolation und Wiederherstellung der Szene erfolgreich. Bericht: `test-results/gengar-parallax/result.json`.
- Referenzlayouts: 8 Themes × 2 Lichtmodi × 4 Größen, Randabdeckung und Navigation erfolgreich. Bericht: `test-results/reference-parallax-final/result.json`.
- Feuerwerk: vorhandene erfolgreiche Prüfung für volle Fensterfläche, durchlässige Mausklicks, Einstellungen, Theme-Wechsel, kompakte Ansicht und gestoppten Timer bei unsichtbarer Szene übernommen. Bericht: `test-results/fireworks-verified/result.json`.
- Publisher: 6 Sicherheitstests erfolgreich. Änderungen am Spielpaket, entfernte Hotfixes, Änderungen sonstiger Kanalwerte und Überschreiben veröffentlichter Releases werden abgewiesen; der Prüfmodus schreibt nichts. Neue Entwürfe werden direkt anhand der Erstellungsantwort identifiziert, unabhängig von einer kurzzeitig verzögerten GitHub-Release-Liste.
- Release-Staging prüft Installer-/EXE-Hashes und UI-Nachweise. Der anschließende Prüfmodus vergleicht alle erhaltenen Kanalfelder mit dem aktuellen GitHub-Stand.

## Öffentliche Freigabe und Selbstupdate

[Release v7.3.7](https://github.com/RoveHD/reborn-update/releases/tag/v7.3.7) ist seit 26.10.2026, 13:50:04 UTC öffentlich. Alle 7 Assets besitzen die geprüften Größen und SHA-256-Digests. Quellen: `fbd2f2359a5d5e68617936ff85c9e5ec9b9082f6`; Aktivierung: `5201cfe9073e4ffcc6ce15c3c2188b787ab54e63`.

`tools/test-github-update.ps1 -ExpectedVersion 7.3.7 -FromVersion 7.3.6 -OldSha256 48d3215b6923aa3546a557d08d59e6df3dbb872a477ad0b35fab2f5dcfeb0dc9` erfolgreich:

- Die originale öffentliche 7.3.6-EXE erkennt 7.3.7 über den normalen Kanal `https://raw.githubusercontent.com/RoveHD/reborn-update/main/update.json`.
- Echter öffentlicher GitHub-Download, Austausch der EXE und Neustart bestätigt.
- Neue EXE stimmt mit der veröffentlichten Prüfsumme überein; alte EXE ist unter `.previous` gesichert.
- Einstellungen bleiben erhalten. Automatische Spielupdates sind ausschließlich in dieser isolierten Testinstallation ausgeschaltet.
- Alle Kanalfelder außer `launcher` und `patchNotes` stimmen mit dem Stand vor Veröffentlichung überein, einschließlich Spielpaket, Löschmigrationen und kleinen Hotfixes.

Maschinenlesbarer Nachweis: [launcher-7.3.7-public-checks.json](launcher-7.3.7-public-checks.json). Automatisierte UI- und Audioprüfungen ersetzen keine vollständige manuelle Prüfung aller Animationen und Lautsprecherwiedergabe.
