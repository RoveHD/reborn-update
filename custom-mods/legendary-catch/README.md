# Fangbalance für Legendäre und Mysteriöse

Die geprüfte Quelle liegt unter `patch/Mods/zzLegendaryCatchDifficulty.rb` und wird unverändert im Spielhotfix 7.3.10 ausgeliefert. Bei zukünftigen Fangbalance-Releases diese Quelle weiterführen; frühere Release-Dateien bleiben unverändert.

Alle 94 legendären und mysteriösen Arten einschließlich ihrer Stufen stehen in [FANGSTUFEN.md](FANGSTUFEN.md) und maschinenlesbar in [fangstufen.json](fangstufen.json). Abysara und Divinara sind Evoli-Entwicklungen und ausdrücklich ausgenommen, obwohl AllGen sie in seiner `LEGENDARYLIST` aufführt.

`python tools/check_event_balance.py` prüft die Balance, den tatsächlichen Ballwurf, Inventarverbrauch, Glitzerball, SWM-Ladereihenfolgen und F12-Neuladen mit dem gebündelten Ruby gegen die lokalen Spieldaten. `PRUEFUNG.json` bindet die Ergebnisse an die konkreten Datei-Prüfsummen.

Die Installation erfolgt mit dem Hotfix über den Launcher. Für eine manuelle Installation das Spiel schließen, die bisherige gleichnamige Mod außerhalb des Spielordners sichern und nur diese Datei ersetzen. Keine zweite Ruby-Kopie im Mods-Ordner anlegen. Spielstände und Pokémon-Daten werden nicht verändert.
