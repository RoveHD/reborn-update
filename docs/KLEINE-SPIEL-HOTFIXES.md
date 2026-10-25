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
