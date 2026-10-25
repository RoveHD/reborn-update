# Launcher 7.3.0 – Prüfung

Stand: 25. Oktober 2026. Launcher-only-Update mit interaktiven Jahreszeiten, eigener Szenenkomposition pro Theme und seltenen Halloween-Ambient-Sounds.

## Nachweise

- 52/52 Core-Prüfungen bestanden, einschließlich Speicherung und Normalisierung der dauerhaften Tag-/Nacht-/Auto-Auswahl.
- 18/18 Ambient-Prüfungen bestanden: Themen- und Soundfreigabe, Pause bei Minimierung, variable Wartezeiten, Wiederholsperre, keine nachgeholten Sounds nach langen Pausen und gültige WAV-Dateien.
- 19/19 Modellprüfungen für interaktive Props bestanden. Ruheplätze, Bewegung und Effektursprung verwenden gemeinsame Koordinaten; temporäre Figuren verschieben keine bestehenden Objekte.
- 24/24 WPF-Geometrieprüfungen bestanden: acht Themes in drei Bühnengrößen, einschließlich Größenwechsel, aktiver Bewegung, temporärer Figuren und Respawn.
- 8/8 Python-Paketierungsprüfungen bestanden.
- 9/9 Feuerwerksprüfungen bestanden: fünf Explosionsmuster, Partikelgrenzen, fallende Schweife, verzögerte Folgeexplosionen, Größenwechsel und Pause ohne nachgeholte Salven. Der separate WPF-Test prüft fensterweite Darstellung, durchlässige Trefferprüfung, automatische Explosionen, Einstellungen, Themewechsel, kompaktes Fenster und Stoppen beim Ausblenden.
- Vollständige WPF-Prüfung der installierten Release-EXE bestanden: acht Themes in Tag und Nacht, acht kompakte und acht maximierte Ansichten, Hauptansicht ohne Scrollen, sichtbare Bedienelemente und Pokémon, begrenzte Bühnenhöhe und kompakte Buttons. Boden liegt unter den Props, Pokémon darüber; Wintergeschenke bleiben sichtbar. Gerenderte Bilder visuell kontrolliert.
- Alle acht Musikstücke, 50 Event-Sounds und zwölf Halloween-Ambient-Sounds über den Windows-Mediencodec stumm geöffnet. Kalenderwechsel, Mausspur und Musikwechsel einschließlich schneller Folgewechsel geprüft.
- Morph-Prüfung auf exakt derselben Release-EXE bestanden: acht gedämpfte Tagespaletten, Textkontrast, sichtbare Farbzwischenstände, Richtungswechsel während der Animation, fertige Pokémon-Überblendung und reduzierte Bewegung.
- Echte isolierte Installation, erneute Installation, Ablehnung eines Spielordners, Selbstupdate der installierten EXE mit Neustart und alter EXE-Sicherung sowie Deinstallation bestanden. Einstellungen, synthetische Spiel-/Spielstanddateien und vorhandene globale Benutzereinträge unverändert.

Geprüfte Artefakte:

- Launcher SHA-256: `1549976203ce800165447470c924d158ebc03f4726655ad0e18e86581e1edca6`
- Installer SHA-256: `bd3a1928b044f53f3d87d6741a4bb52659a77d53faa248ff2fc4f9329feb9f61`
- Installerbericht: `test-results/installer.json`; vollständiger UI-Bericht: `test-results/installer-70f4987fb468430e85a9f554f6d0bf05/installed-self-update.json`.
- Beleuchtungsbericht mit EXE-Prüfsumme: `test-results/lighting-release.json`.

## Reproduktion

```powershell
dotnet run --project tests/CoreTests -c Release
dotnet run --project tests/AmbientTests -c Release
dotnet run --project tests/PropTests -c Release
dotnet run --project tests/PropLayoutTests -c Release
dotnet run --project tests/FireworkTests -c Release
dotnet run --project tests/FireworkUiTests -c Release -- test-results/fireworks
./tools/build.ps1
./tools/build-installer.ps1
./installer/test-installer.ps1
./tools/test-lighting.ps1
python tools/prepare_launcher_release.py --version 7.3.0
./tools/test-github-update.ps1 -ExpectedVersion 7.3.0
```

## Grenzen

Die Audioprüfung öffnet Ressourcen stumm über den Windows-Mediencodec; sie ersetzt keinen manuellen Hörtest. Kein echter Spielstart und keine Änderungen am persönlichen Spielordner, an Mods oder Spielständen. Der isolierte Installertest deaktiviert globale Startmenü-/Deinstallationseinträge und kontrolliert vorhandene Einträge auf Unverändertheit. Kein eigenes Code-Signing-Zertifikat. Das Verhalten auf anderen PCs und bei anderen Windows-/DPI-Konfigurationen ist damit nicht vollständig geprüft.

Der öffentliche Update-Nachweis wird nach Veröffentlichung ergänzt.
