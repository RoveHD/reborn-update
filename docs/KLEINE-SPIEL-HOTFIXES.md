# Kleine automatische Spiel-Hotfixes

Ab Launcher 7.3.3 kann das bestehende Schema 1 optional `gameHotfixes` enthalten. `game` bleibt das vollstaendige Zielpaket fuer bisherige Clients und Reparaturen. Ein Hotfix deklariert `fromVersion`, `version`, `url`, `sha256`, `size` sowie `files` mit `path`, `beforeSha256`, `sha256` und `size`.

Der erste Protokollstand ersetzt ausschliesslich vorhandene Dateien. Neue oder entfernte Pfade brauchen weiterhin das Vollpaket. Das ZIP enthaelt die deklarierten Pfade direkt an der Wurzel. Versionsdatei, Spielstaende, Einstellungen und Updater-interne Dateien sind keine zulaessigen Hotfix-Ziele.

Die Auswahl verlangt die exakte Ausgangsversion und die passenden Hashes aller betroffenen Dateien. Eine Reparatur derselben Version verwendet immer das Vollpaket. Nach dem Download werden ZIP-Inhalt, Dateimenge, Groessen und Hashes geprueft; unmittelbar vor der Installation werden die Ausgangsdateien nochmals kontrolliert. Fehler beim Download oder bei der Archivpruefung brechen ab. Installation, Sicherung und Rollback verwenden den bestehenden GameInstaller.

## Paketbau

`python tools/build_game_hotfix.py --baseline dist/release-game-7.3.1-release --target dist/release-game-7.3.2`

Das Werkzeug vergleicht die beiden Dateiinventare, liest die neuen Dateien aus dem geprueften Zielarchiv und ergaenzt dessen Manifest. Das optionale kleine ZIP muss zusammen mit dem vollstaendigen Zielpaket veroeffentlicht sein, bevor der Live-Kanal umgestellt wird.

Der vollständige Fang-Hotfix 7.3.2 wurde im manuell gestarteten GitHub-Workflow gebaut, um einen erneuten lokalen 1,5-GB-Upload zu vermeiden. Das Workflow-Ergebnis bleibt bis zur lokalen Installerpruefung ein Entwurf. Der kleine Hotfix umfasst nur `patch/Mods/zzLegendaryCatchDifficulty.rb` (ZIP: 4.190 Bytes).

## Isolierter Launcher-Build

Die veroeffentlichte UI-Basis ist Commit `b3f5512` (Launcher 7.3.0). Fuer 7.3.3 werden nur `Services/UpdateManifest.cs`, `Services/UpdateService.cs` und `Services/GameHotfix.cs` ergaenzt und mit `dotnet publish -c Release -r win-x64 --self-contained true -p:Version=7.3.3 -p:AssemblyVersion=7.3.3.0 -p:FileVersion=7.3.3.0` gebaut. Unveroeffentlichte UI-Arbeiten im Arbeitsordner werden nicht eingebaut.

## Pruefung und Veroeffentlichung am 25.10.2026

- 23/23 Hotfix-Tests und 52/52 bestehende CoreTests bestanden.
- Das echte 4.190-Byte-Paket wurde ueber UpdateService in einer isolierten Installation angewendet und zurueck/vorwaerts gewechselt. Ein Vollpaketabruf waere in diesem Test ein Fehler; eigene Grafik, Einstellungen und Spielstand blieben erhalten.
- Das vollstaendige 7.3.2-Archiv wurde mit dem bisherigen Windows-Installer installiert/repariert; alle 18.864 Dateien stimmten. Unveraenderte Game.exe, Testspielstand, Einstellungen und eigene Zusatzmod blieben erhalten.
- Reale Launcher-Selbstaktualisierung von 7.3.0 auf 7.3.3 mit gesicherter alter EXE, bestaetigtem Neustart und erfolgreichem EXE-Smoke bestanden. Keine vollstaendige Spielrunde beim Empfaenger getestet.
- Beide GitHub-Releases sind oeffentlich. Assetgroessen und GitHub-SHA-256 stimmen mit den lokalen Dateien ueberein. Der kleine Hotfix wurde zusaetzlich ueber seine oeffentliche Downloadadresse geladen und geprueft.
- Live-Kanal auf Launcher 7.3.3 / Spiel 7.3.2 atomar aktualisiert; GitHub-API und Git-Stand bestaetigt. Raw-CDN lieferte unmittelbar danach noch den vorherigen Stand mit `Cache-Control: max-age=300`; dadurch kann die Erkennung bis zu einige Minuten verzoegert sein.

Launcher-SHA-256: `29ac03add15acbde27b27f82b570c16908fb63cfca6332dc8334c359356a7dfe`.
Vollpaket-SHA-256: `a7016240d9d75df4a2c37ec779bfea581911d8fa234bab68e2b9574ab2c8d87b`.
Hotfix-SHA-256: `1ed868f76b2805794d523cf2cf153bf1b1c598151e221445ffa4f7f679323a97`.

Die Projektdatei der parallel bearbeiteten UI bleibt lokal unangetastet. Der naechste Design-Release muss eine Version neuer als 7.3.3 erhalten und die neuen Services beibehalten. Der vorhandene Setup-Download kann sich ueber den aktualisierten Launcher-Kanal selbst aktualisieren.
