# Launcher 7.2.0 – Prüfung

Stand: 25. Oktober 2026. Installierbare Windows-x64-Anwendung mit automatischem GitHub-Selbstupdate und kalenderabhängigen Designs.

## Nachweise

- 49/49 Core-Prüfungen bestanden. Kalendergrenzen für mehrere Jahre, Schaltjahr, Gregorianischer Ostertermin 2000–2200, kulturunabhängige und über Neustarts stabile geheime Tagesbegegnung.
- WPF-Fenster bei 1280×800 und 960×640 gerendert und visuell kontrolliert. Alle acht Designs, Versionswechsel, Ersteinrichtung, Downloadzustand, Dialogabbruch und bedienbare Hauptaktionen geprüft.
- Alle acht MP3-Ressourcen vom Windows-Mediencodec geöffnet. Dauer 60–130 Sekunden; stummer Test.
- Tatsächlicher stummer Wechsel zwischen mehreren Musikstücken einschließlich schneller Folgewechsel, Ausblenden, Stummschaltung, pausiertem Zustand und Fortsetzen geprüft.
- Saisonfarbige native CUR-Mauszeiger erzeugt. Leuchtspur gezeichnet, Partikelgrenze und Stoppen bei reduzierten Effekten geprüft.
- Echte isolierte Installation, erneute Installation, Ablehnung eines Spielordners, Selbstupdate mit Prozessneustart und bytegenauer alter EXE-Sicherung, danach Deinstallation. Einstellungen und synthetische Spiel-/Spielstanddateien unverändert.
- Nach Veröffentlichung zusätzlich die unveränderte offizielle 7.0.0-EXE mit dem öffentlichen GitHub-Manifest gestartet: 7.2.0 heruntergeladen, SHA-256 geprüft, EXE ersetzt und Neustart bestätigt. Einstellungen und alte EXE-Sicherung geprüft (`test-results/github-update.json`). Der erste Versuch wurde durch die zu kurze 180-Sekunden-Grenze des Testskripts bei etwa 72 von 79 MB beendet; mit 600 Sekunden Testlimit lief der gesamte Ablauf erfolgreich durch.
- Code, Grafiken, Schriften und Musik in eigenständig ausführbarer EXE; vollständige Schriftlizenz und Musik-Credits zusätzlich eingebettet. Installer enthält die Quellenhinweise.

Reproduzierbare Befehle:

```powershell
dotnet run --project tests/CoreTests -c Release
./tools/build.ps1
./tools/build-installer.ps1
./installer/test-installer.ps1
./tools/test-github-update.ps1
```

Der letzte Installertest bindet seine Setup- und EXE-Prüfsummen in `test-results/installer.json`. Sein Unterordner enthält `installed-self-update.json` mit den tatsächlich gerenderten Bildern, geöffneten Audiotracks und überprüften UI-Zuständen. Diese Berichte entstehen ausschließlich unter D:.

## Grenzen

Kein manueller Hörtest, kein echter Spielstart, kein Zugriff auf den persönlichen Spielordner auf Z:. Die Startmenü- und Windows-Deinstallationseinträge sind im Installer konfiguriert; der isolierte Test deaktiviert diese globalen Benutzereinträge und prüft, dass bestehende Einträge unverändert bleiben. Kein eigenes Code-Signing-Zertifikat vorhanden. Der zukünftige Updateablauf auf den Rechnern der Mitspieler kann lokal nicht vollständig vorweggenommen werden; die veröffentlichte EXE, der Installer und das öffentliche Manifest werden zusätzlich geprüft.
