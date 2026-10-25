# Prüfung von Launcher 7.0.0 und AllGen 0.9.9

Stand: 25. Oktober 2026. Prüfnachweise für die Freigabe dieses Releases.

- Windows-x64-Build mit eigener .NET-Laufzeit erfolgreich.
- 45 Core-Tests erfolgreich: geprüfte Downloads, Pfadschutz, Abbruch und Wiederherstellung, gesperrte Dateien, unveränderte Dateien, wiederholter Versionswechsel, Rotation auf zwei Versionen und Reparatur mit erhaltenem Rückweg. Auch mehrere historische Prüfsummen je Löschpfad, widersprüchliche Ersatzpfade, unbekannte modifizierte Mods und während der Sicherung geänderte Löschkandidaten sind abgedeckt.
- 5 Python-Pakettests erfolgreich, einschließlich mehrerer Altstände und alter Patch-ZIPs ohne Game.exe.
- Echter Selbstupdate-Helfer tauschte eine alte Launcher-EXE aus. Neustartbestätigung, Sicherung der alten EXE und unveränderte Test-Spielstände/Einstellungen geprüft. Der Test verwendet einen bereits beendeten Elternprozess.
- Feste Spielquelle mit 18.862 Dateien paketiert; jede ZIP-Datei anschließend gegen die Quelle per SHA-256 verifiziert. Keine persönlichen Dateien enthalten. Archivgröße: 1.476.579.974 Bytes.
- Tatsächlich veröffentlichtes GitHub-Paket 6.1.0 heruntergeladen und per SHA-256 geprüft. Dieses alte Archiv ist ein Patch; 1.027 fehlende Basisdateien wurden aus der separaten alten D:-Spielkopie ergänzt, ohne Dateien des alten Archivs zu überschreiben.
- Migration in einer neuen isolierten Testinstallation erfolgreich: alle 18.862 neuen Dateien geprüft, 26 vorhandene bekannte Altdateien entfernt und unbekannte zusätzliche Dateien erhalten. Vier Versionswechsel prüften wiederholt alle 18.790 alten und 18.862 neuen Dateien. Dauer: rund 340 Sekunden. Test-Spielstand, persönliche Einstellungen und eigene zusätzliche Mod blieben erhalten. Die Game.exe wurde als Datei geprüft, das Spiel nicht gestartet.
- In derselben erfolgreich migrierten Testinstallation eine Datei absichtlich beschädigt und repariert: erneut alle 18.862 Dateien geprüft. Der Zeitstempel einer unveränderten Spieldatei blieb erhalten. Dauer: rund 57 Sekunden.
- Anschließend synthetische Folgeversion mit geändertem, neuem und entferntem Skript installiert. Drei weitere Versionswechsel über den Service erfolgreich; alle 18.862 wiederhergestellten Paketdateien und vier persönliche Testdateien geprüft. Das synthetische Spiel wurde nicht gestartet.
- Eine ältere, wiederverwendete Testkopie wurde zunächst korrekt abgewiesen: zuerst wegen ausgewählter Vorgängerversion, anschließend wegen einer unbekannten Zwischenfassung der umbenannten deutschen Mod. Der abschließende vollständige Prüfablauf verwendete deshalb die frisch migrierte Testinstallation. Keine Schutzprüfung wurde abgeschwächt.
- Tatsächliche WPF-Oberfläche bei großer und minimaler Fenstergröße gerendert; die aktuelle Mindestgröße visuell kontrolliert. Original-Reborn-Logo und Spiel-Icon eingebunden.
- Veröffentlichungsskript vorab ohne Veröffentlichung ausgeführt: Paketgrößen, Prüfsummen und zugehörige Prüfnachweise erfolgreich kontrolliert. Beim Upload prüft es zusätzlich die Größen und SHA-256-Digests aller sechs GitHub-Assets, bevor Release und neues Update-Manifest freigegeben werden.

Launcher-SHA-256: `0cce75b7f5ae28b28ffd447622fb17edcf2f0c3c2dd1b729922f2e3e4d87073a`

Spielpaket-SHA-256: `727f1580fd7faa30ea73deb32e223d9c829fe8b69f179322090f12c345f9f0ec`

Altes GitHub-Paket 6.1.0: `b73375fd22f874982fd092e823a63aed39858e553c9c48347be4112b7b1a9bde`

Die vollständigen lokalen Protokolle liegen unter `test-results/`; die paketgebundenen Nachweise unter `dist/release-7.0.0/`. Diese Ordner werden nicht eingecheckt. `SHA256SUMS.txt` wird als Release-Asset bereitgestellt.

Nicht geprüft: Download und Installation durch Mitspieler auf deren PCs, vollständiger Spieldurchlauf und jede mögliche fremde Mod-Kombination. Die EXE ist nicht codesigniert. Der erste Wechsel vom alten Python-Launcher erfordert einmaliges Ersetzen der geschlossenen EXE. Das alte `version.txt` bleibt auf 6.1.0; der neue Launcher verwendet `update.json`.

Bei Installationsfehlern bleibt das Updateprotokoll mit den nötigen Sicherungen für die Wiederherstellung erhalten. Beim Selbstupdate bleibt die vorherige EXE als Rückweg verfügbar. Details stehen in der README. Sämtliche Tests dieses Releases liefen ausschließlich mit Kopien unter D:.
