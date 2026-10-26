# Launcher 7.3.8 – Prop-Eingaben

26.10.2026. Reiner Launcher-Hotfix; Spielpaket 7.3.5 / AllGen 0.9.9 und alle Spielhotfixes bleiben erhalten. Tests verwenden isolierte D:-Ordner.

## Ursache und Fix

Seit der permanenten Props-Szene aus 7.3.7 lag der vollflächige `HeroScroller` als Geschwisterelement vor `PokemonStage`. Obwohl seine Fläche durchsichtig war, lieferte der WPF-Eingabetest dort den ScrollViewer statt des sichtbaren Props zurück. Der neue Regressionstest scheiterte vor dem Fix mit `Prop clicks intercepted: standard/Star by ScrollViewer`.

`PokemonStage` erhält `Panel.ZIndex=1`. Die Props-Komponente nimmt bereits ausschließlich ihre eigenen aktiven Objektflächen als Klickziele an; freie Bereiche bleiben für die Startansicht durchlässig. Die Navigation behält ihre bisherige Eingabe- und Sichtbarkeitssteuerung. Die finale EXE prüft jetzt den Treffer über das ganze Fenster, statt nur innerhalb der Props-Komponente.

## Ausgeführt

- `dotnet run --project tests/CenteredLayoutTests/CenteredLayoutTests.csproj -- test-results/prop-input-final`: erfolgreich. Alle acht Themes, vier Größen für die Hauptansicht, zusätzliche schmale/breite/hohe Prop-Ansichten; Klickziele über `Window.InputHitTest`, Startbutton, alle Menüs, Persistenz der Props, Scrollen, Escape, Unterdialog und Downloadabbruch geprüft.
- Fledermaus: Dachposition über den Fenster-Eingabetest erreichbar; während Flug und Abwesenheit kein Klickziel; nach simuliertem Ablauf der Rückkehr wieder erreichbar und erneut aktivierbar.
- Build und Windows-Setup erfolgreich. `installer/test-installer.ps1`: Installation, Neuinstallation, EXE-Austausch und bestätigter Neustart, Sicherung der alten EXE sowie Deinstallation erfolgreich; Einstellungen, Spiel-Sentinels, globale Registrierung und Verknüpfungen erhalten.
- Die installierte finale EXE besteht die UI-/Medienprüfung einschließlich des neuen Fenster-Eingabetests, 8 Musikstücken, 50 Event-Sounds, 12 Halloween-Ambient-Sounds und Tag-/Nacht-/Fensterlayouts.
- `tools/test-lighting.ps1`: acht Paletten, kontinuierlicher Übergang, Richtungswechsel, reduzierte Bewegung und Bildblende erfolgreich.
- Publisher: sechs Sicherheitstests erfolgreich; Release-Staging verlangt jetzt zusätzlich den Prop-Eingabe-Regressionsnachweis.

Die Eingabeprüfung verwendet WPF-Hit-Tests des gesamten Fensters und die bestehende deterministische Aktivierung für den Fledermauszyklus. Physische Mausklicks wurden damit nicht automatisiert nachgewiesen. Ein experimenteller Test mit dem nativen Mausgerät lieferte in dieser Testumgebung keine gültigen Mauskoordinaten und wurde nicht als erfolgreicher Nachweis übernommen.

## Prüfsummen

- `launcher.exe`: `56ae36bab431cea27105fa6b5d63528c79d6cb0a6030c61f83bd900a39a6aa0d`
- `Reborn-Launcher-7.3.8-Setup.exe`: `c6080d04163a6b0cbc8404a84d00cfa3a009463a50c224c07fdd0875040e9cfb`

Das Setup installiert pro Benutzer ohne Administratorrechte. Die Dateien sind nicht codesigniert.

## Öffentlicher Selbstupdate-Nachweis

[Release v7.3.8](https://github.com/RoveHD/reborn-update/releases/tag/v7.3.8) ist öffentlich und als aktuelles Release markiert. Alle sieben Assets wurden anhand der auf GitHub gespeicherten SHA-256-Digests bestätigt. Quellen: `02f9a8183a6b9b38ebd2e0e7e6b2bc445a83493b`; Kanalaktivierung: `277f7f186fe5e4a0afbc23244a34788ba026611f`.

`tools/test-github-update.ps1 -ExpectedVersion 7.3.8 -FromVersion 7.3.7 -OldSha256 7ebcc0ae50cadf7b88e62a926f03f1bd1bb2baee859cebfdf226f29613aae289` erfolgreich: Die bisherige EXE erkennt 7.3.8 über den normalen öffentlichen raw/main-Kanal, lädt die veröffentlichte EXE, ersetzt sich und bestätigt den Neustart. Neue Prüfsumme, Sicherung der alten EXE und Erhalt der Einstellungen bestätigt.

Alle Kanalfelder außer `launcher` und `patchNotes` stimmen mit dem Stand vor Veröffentlichung überein; Spielpaket, Löschmigrationen und Spielhotfixes sind erhalten. Das veröffentlichte Release 7.3.7 wurde nicht verändert. Maschinenlesbarer Nachweis: [launcher-7.3.8-public-checks.json](launcher-7.3.8-public-checks.json).
