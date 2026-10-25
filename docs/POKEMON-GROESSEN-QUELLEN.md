# Pokémon-Körpergrößen: Quellen und Prüfmethode

Für alle 40 deduplizierten IDs aus `MainPokemonSelection.cs` wurden PokéWiki-Artikelseiten direkt per HTTP abgerufen (Status 200). Die Roh-HTML-Dateien liegen unter `test-results/pokewiki-cache/<id>.html`; Höhe wurde aus dem ersten Hauptinfobox-Feld `Erscheinung → Größe` entnommen. Sekundärformen in derselben Infobox wurden getrennt behandelt.

`docs/POKEMON-GROESSEN-QUELLEN.json` enthält pro ID Name, ausgewählte Form, Körpergröße in Metern, direkte PokéWiki-URL, Verifizierungsstatus und die Rohangabe/Anmerkung. Alle 40 Seiten wurden abgerufen und das entsprechende Größenfeld im HTML geprüft.

**Formfälle:** Giratina ist als Wandelform (4,5 m) geführt; Shaymin als Landform (0,2 m); Alola-Vulpix hat in der umschaltbaren Infobox ebenfalls 0,6 m. Irrbis ist als Mittelgroße Sorte (0,4 m) eingetragen: Die Artworkzuordnung `official-artwork/710.png` ist in `DESIGN-QUELLEN.md` dokumentiert; eine Live-Abfrage von PokeAPI `/pokemon/710` lieferte `pumpkaboo-average` mit Höhe 4 dm. PokéWiki belegt die Staffel: Kleine Sorte 0,3 m, Mittelgroße Sorte 0,4 m, Große Sorte 0,5 m und Extragroße Sorte 0,8 m.

**Validierung:** JSON erfolgreich eingelesen; 40 Zeilen und 40 eindeutige IDs; keine Größenwerte sind null.
