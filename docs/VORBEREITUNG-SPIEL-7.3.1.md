# Spielhotfix 7.3.1

Das Paket basiert auf dem veröffentlichten Spielstand 7.0.0 / AllGen 0.9.9. Der Launcher bleibt unverändert bei 7.3.0. Die laufende Neugestaltung des Launchers gehört nicht zu diesem Release.

## Änderungen gegenüber 7.0.0

- `patch/Mods/Party.rb`: „Erneut erlernen“ im Teammenü wiederhergestellt; Freischaltung, Eier und alte boolesche Speicherwerte berücksichtigt.
- `patch/Mods/RE - Box-Markierungen.rb`: schriftunabhängige Markierungen in Box und Info-Seite; Menü mit normaler Spielschrift und passender Breite.
- `patch/Mods/zzLocalGermanVisible.rb`: Pokédex-Labels mit normaler Schrift und angepassten Zellen.
- `patch/Mods/RE - Beutel-Sortierung-und-TM-Infos.rb`: TM-Werte im vorhandenen Beschreibungsfenster.
- `intl.txt`, `Data/german.dat`, `patch/Mods/RE - Deutsche-Zusatztexte.rb`: englische Street-Eigennamen erhalten; normale deutsche Dialogwörter bleiben bestehen. Die Mod schützt die Namen auch bei älteren oder neu kompilierten Sprachdateien.
- `patch/Mods/zzzz_RE_BattleSound_099_Compatibility.rb`: gezielter Adapter für den Drei-Argumente-Override aus Battle Sound Effects v1.1. Moderne Implementierungen erhalten weiterhin die Treffernummer. Die übrigen alten Kampfmethoden der Sound-Mod werden nicht portiert.

Ältere bereits veröffentlichte Pokédex-, Megastein- und Fangmechanik-Fixes bleiben Bestandteil der geprüften Basis. Keine Datei der aktuellen Basis wird entfernt. Historische, anhand exakter Prüfsummen abgesicherte Migrationen des bisherigen Kanals bleiben erhalten.

## Paket und Prüfung

`tools/prepare_game_hotfix.py` prüft das veröffentlichte Basisarchiv und kopiert dessen anhand des Inventars geprüfte Dateien in `artifacts/game-source-7.3.1-final`. Nur die acht oben aufgeführten Pfade werden ergänzt beziehungsweise ersetzt. Die älteren Vorbereitungen werden nicht überschrieben. Das finale Paket liegt in `dist/release-game-7.3.1-final`.

Nach dem Packen werden sämtliche ZIP-Dateien erneut gelesen und mit SHA-256 gegen das Quellinventar geprüft. `hotfix-checks.json` bestätigt den begrenzten Änderungsumfang und den unveränderten Launcher. Die Funktionsprüfungen umfassen 148 Menüprüfungen, 195 Street-Prüfungen und 13 Sound-Kompatibilitätsprüfungen. Die vorherigen echten Engine-Renderprüfungen der unveränderten Menü-/Schrift-Fixes liegen unter `update-0.9.9-20261025/runtime-test` im übergeordneten Arbeitsbereich.

Installation und Reparatur des finalen Archivs werden mit dem echten Updater-Installationscode auf isolierten Daten unter `test-results/game-7.3.1-final` geprüft. Testspielstand, Einstellungen und zusätzlicher eigener Mod müssen erhalten bleiben. Für die Kampf-Sound-Mod liegt keine vollständige Durchspielprüfung aller ihrer alten Überschreibungen vor.

## Veröffentlichung und Rückweg

`tools/publish-game-hotfix.ps1` verlangt passende Paket-, Installations- und Reparaturberichte. Zuerst werden die Assets als Entwurf hochgeladen und ihre von GitHub gemeldeten Größen und SHA-256-Werte geprüft. Erst danach wird der Release veröffentlicht und der Kanal auf `main` atomar umgestellt. Der öffentliche Launcher-Abschnitt muss gegenüber dem bisherigen Kanal unverändert sein. Der Legacy-Kanal `version.txt` bleibt unverändert. Der Release erhält nicht die Markierung „Latest“, damit bestehende Launcher-Downloadlinks weiterhin auf den Launcher-Release zeigen.

Der Updater sichert ersetzte Dateien und bietet die vorherige Spielversion als Rückweg an. Der neue Sound-Adapter lässt sich bei geschlossenem Spiel auch einzeln wieder entfernen. Nach Installation des Hotfixes muss das Spiel vollständig neu gestartet werden.
