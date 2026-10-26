# Spielhotfix 7.3.9: Glitzerball bei legendären Shinys

Der Hotfix senkt ausschließlich den Glitzerball-Bonus für bereits schillernde Pokémon aus der vorhandenen Legendären-Klassifikation von ×8 auf ×4. Gewöhnliche Shinys behalten ×8; Nicht-Shinys und der OnCatch-Effekt bleiben unverändert. Die Klassifikation umfasst wie bisher auch mysteriöse Pokémon.

Für das bestehende kleine Hotfix-Protokoll wird der neue Ball-Handler am Ende von `patch/Mods/zzLegendaryCatchDifficulty.rb` registriert. Der gesamte veröffentlichte Inhalt aus 7.3.5 bleibt als bytegleicher Präfix erhalten. Der Hotfix ersetzt nur diese Datei; Originalskripte/PBS, persönliche Grafiken/Audio und andere Mods gehören nicht zu seinem Delta. Die Originaldateien bereits veröffentlichter Releases werden nicht verändert.

## Ruby-Prüfung

`python tools/check_glitter_hotfix.py` verwendet das mit dem Spiel gebündelte Ruby 3.1 und die tatsächlichen Battle-, Ball-, Item- und SWM-Methoden in isolierten Testobjekten. Vier Ladevarianten bestehen: original 284, SWM zuerst 285, Fang-Mod zuerst 285 und Script-Neuladen 286 Prüfungen (insgesamt 1.140).

Die neuen Prüfungen vergleichen den tatsächlichen ursprünglichen Glitzerball-Handler mit dem neuen Handler, einschließlich Nicht-Shinys, gewöhnlicher Pokémon, legendärer/mysteriöser Pokémon, Ultra-Beast-Sonderbehandlung und wiederholtem Laden. Zusätzlich wird beim tatsächlichen Ballwurf der Gen-3-Schwellwert mit ×4 geprüft. Hyperball und OnCatch bleiben erhalten. Die vorhandenen Reborn-Kugel-Sperren (20/30), Zielauswahl, Inventar und Fangstufen bestehen weiterhin.

Die Tests starten kein Spiel und verwenden keine echten Spielstände. Eine manuelle Kampfsitzung ist damit nicht nachgewiesen.

Mod-SHA-256: `82f918bdd7855ec8e3fb57a5af054ff86308514d8b35e3f197954b21d0b8ddf8`.

## Paket und Updater

Das vollständige Paket enthält 18.864 Dateien; der Paketbau hat sämtliche Archivdateien gegen das Inventar geprüft und genau einen geänderten Pfad bestätigt. Der lokale Vollinstallationstest hat zusätzlich alle 18.864 installierten Dateien per SHA-256 geprüft und persönliche synthetische Dateien erhalten.

`CatchHotfixChecks` hat mit dem echten `UpdateService` ausschließlich das 4.441 Byte große Hotfix-ZIP geladen. Basishashes, Ablehnung einer veränderten Ausgangs-Mod, Versionsmarker und Rück-/Vorwechsel bestehen; Spielstand-, Konfigurations- und Fremdmod-Sentinels bleiben erhalten.

Der [GitHub-Build 40352550976](https://github.com/RoveHD/reborn-update/actions/runs/40352550976) hat das vollständige Paket bytegleich reproduziert und ausschließlich in den Release-Entwurf hochgeladen.

| Artefakt | SHA-256 |
|---|---|
| Hotfix 7.3.5 → 7.3.9, 4.441 Byte | `eb858735221a7e2ea714ca56adf674979d4fa94ee8c0b666bb07f8a4b26842af` |
| Vollständiges Paket, 1.476.961.210 Byte | `0d9bfe384cac9ffd34f8fbf48bbeb27c7dafbe2a730bb65112a5c23fcc98df0f` |

Die Reparaturprüfung besteht ebenfalls mit 18.864 geprüften Dateien und erhaltenem Zeitstempel der unveränderten Game.exe. Alle lokalen Tests verwenden isolierte D:-Fixtures; keine Z:-Spielinstallation wird angefasst.

## Veröffentlichung

[Spielhotfix 7.3.9](https://github.com/RoveHD/reborn-update/releases/tag/v7.3.9) ist veröffentlicht und der Hauptkanal liefert Spiel 7.3.9 bei unverändertem Launcher 7.3.8. Alle zehn Release-Assets sind mit den von GitHub gemeldeten SHA-256-Digests und Dateigrößen abgeglichen. Die bisherigen Releases bleiben unverändert.

Der öffentliche Test hat mit dem echten UpdateService das unveränderliche veröffentlichte Manifest sowie ausschließlich das 4.441-Byte-ZIP über HTTPS geladen und installiert. Modifizierte Basisdateien werden abgelehnt; Rückwechsel, erneuter Vorwechsel und geschützte persönliche Testdateien bestehen. Auch `raw/main/update.json` wurde mit Spielversion 7.3.9 geprüft. Kompakter Nachweis: `docs/glitter-7.3.9-public-checks.json`; lokale Installation/Reparatur: `game-hotfixes/7.3.9/updater-checks.json`.
