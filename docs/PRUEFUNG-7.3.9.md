# Spielhotfix 7.3.9: Glitzerball bei legendären Shinys

Der Hotfix senkt ausschließlich den Glitzerball-Bonus für bereits schillernde Pokémon aus der vorhandenen Legendären-Klassifikation von ×8 auf ×4. Gewöhnliche Shinys behalten ×8; Nicht-Shinys und der OnCatch-Effekt bleiben unverändert. Die Klassifikation umfasst wie bisher auch mysteriöse Pokémon.

Für das bestehende kleine Hotfix-Protokoll wird der neue Ball-Handler am Ende von `patch/Mods/zzLegendaryCatchDifficulty.rb` registriert. Der gesamte veröffentlichte Inhalt aus 7.3.5 bleibt als bytegleicher Präfix erhalten. Der Hotfix ersetzt nur diese Datei; Originalskripte/PBS, persönliche Grafiken/Audio und andere Mods gehören nicht zu seinem Delta. Die Originaldateien bereits veröffentlichter Releases werden nicht verändert.

## Ruby-Prüfung

`python tools/check_glitter_hotfix.py` verwendet das mit dem Spiel gebündelte Ruby 3.1 und die tatsächlichen Battle-, Ball-, Item- und SWM-Methoden in isolierten Testobjekten. Vier Ladevarianten bestehen: original 284, SWM zuerst 285, Fang-Mod zuerst 285 und Script-Neuladen 286 Prüfungen (insgesamt 1.140).

Die neuen Prüfungen vergleichen den tatsächlichen ursprünglichen Glitzerball-Handler mit dem neuen Handler, einschließlich Nicht-Shinys, gewöhnlicher Pokémon, legendärer/mysteriöser Pokémon, Ultra-Beast-Sonderbehandlung und wiederholtem Laden. Zusätzlich wird beim tatsächlichen Ballwurf der Gen-3-Schwellwert mit ×4 geprüft. Hyperball und OnCatch bleiben erhalten. Die vorhandenen Reborn-Kugel-Sperren (20/30), Zielauswahl, Inventar und Fangstufen bestehen weiterhin.

Die Tests starten kein Spiel und verwenden keine echten Spielstände. Eine manuelle Kampfsitzung ist damit nicht nachgewiesen.

Mod-SHA-256: `82f918bdd7855ec8e3fb57a5af054ff86308514d8b35e3f197954b21d0b8ddf8`.

Paketbau, Updaterintegration und Veröffentlichung werden separat geprüft; ihre Ergebnisse werden nach Abschluss hier ergänzt.
