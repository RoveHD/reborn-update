# Pokedex X/Y Form Cycling Regression

Die umbenannte Payload-Datei wurde mit der echten eingebetteten Ruby-Runtime über `ruby_runner.py` gegen die aktuellen `PokedexScene.rb`- und `NestAndForm.rb`-Quellen headless geprüft. Keine Game.exe und keine Spielstände wurden verwendet.

Ergebnis: **20 Assertions bestanden**; separater Missing-API-Ladecheck ebenfalls bestanden.
Aktive lokale Datei und umbenannte Payload sind byteidentisch (SHA-256 `d722e7dcee16f7caaa84baaf96bcb7154935219eaf476d8beb65d4662ccdd640`).

Der Harness prüft Formwechsel mit X/Y, Wraparound vorwärts und rückwärts, Beschränkung auf bekannte Formen, sowie unveränderte Daten bei ungesehenem Pokémon, Einzelform und `cancelledgenders`-Ausnahme. Bestehende C-Menü-, B-Abbruch-, Hauptlisten-Navigation-, Busy-Guard- und Fehlerbereinigungsprüfungen laufen ebenfalls mit.

Ergebnisdateien: `dex-cycle-runtime.txt`, `dex-cycle-report.json`; Harness: `dex_cycle_unit.rb`. Kopien der Engine-Skripte liegen daneben.

Grenze: Rendering und physische Tasteneingabe wurden nicht im grafischen Spiel geprüft.
