# Bewohnte Anime-Szenen

Die Launcher-Bühne zeigt je nach Theme eine feste, direkt in die Illustration eingezeichnete Pokémon-Besetzung. Es gibt keinen Picker für die Szenen-Pokémon. Die Figuren bilden gemeinsam mit Landschaft, Vordergrund und jahreszeitlichen Details eine einzelne Komposition; es gibt keine separaten individuellen Pokémon-Animationen.

## Bildaufbau

Die eingebetteten bewohnten Landschaften liegen unter `src/RebornLauncher/assets/scenery/inhabited/`; ergänzende Landschafts- und transparente Vordergrundebenen unter `src/RebornLauncher/assets/scenery/anime/`. `AtmosphericBackdrop` behandelt Landschaft, Atmosphäre und Vordergrund als getrennte Ebenen. Wetter, Licht, Partikel und saisonale Dekorationen ergänzen die Bilder als dezente Bewegung und Tiefe. Navigation, Logo, Startaktion und Statusinformationen bleiben auf stabilen UI-Flächen über der Szene.

Die Pokémon sind Teil der Bildillustrationen. Die Tabelle beschreibt Posen, Platzierung und Perspektive; sie behauptet keine individuellen Pokémon-Animationen. Sichtbare Höhenanteile sind grobe visuelle Schätzungen aus der 1280×800-Kompositionsprüfung vor der abschließenden Randerweiterung. Durch das zusätzliche Outpainting und den fensterabhängigen Ausschnitt können sie abweichen. Die Entfernungsangabe ist qualitativ (nah, mittel, fern). Keine der Schätzungen ist eine Messung aus Code oder eine feste Umrechnung von Pokédex-Metern in Pixel. Die Randerweiterung ist in [SZENEN-OUTPAINTING.md](SZENEN-OUTPAINTING.md) dokumentiert.

## Besetzung und Größen

Die offiziellen Pokédex-Maße stammen aus den PokéWiki-Hauptinfoboxen; direkte Links und Verifikationsnotizen stehen in [POKEMON-GROESSEN-QUELLEN.json](POKEMON-GROESSEN-QUELLEN.json). Saisonale und regionale Formen sind ausgewiesen.

| Theme | Pokémon und offizielle Größe | Platzierung und Pose | Perspektive | Grobe Höhe vor Randerweiterung |
|---|---|---|---|---:|
| Standard | Nachtara, Normalform — 1,0 m ([PokéWiki](https://www.pokewiki.de/Nachtara)) | Links auf niedriger Felskante, liegend und in die Szene blickend; Absol gegenüber. | nah | ca. 27 % |
| Standard | Absol, Normalform — 1,2 m ([PokéWiki](https://www.pokewiki.de/Absol)) | Rechts auf erhöhtem Felsvorsprung, aufrecht und zur Bildmitte gewandt. | nah | ca. 25 % |
| Standard | Evoli, Normalform — 0,3 m ([PokéWiki](https://www.pokewiki.de/Evoli)) | Unten rechts am Wegesrand, zusammengerollt schlafend. | nah | ca. 15 % |
| Halloween | Gengar, Normalform — 1,5 m ([PokéWiki](https://www.pokewiki.de/Gengar)) | Links hinter der Steinmauer hervorlugend. | nah | ca. 25 % |
| Halloween | Irrbis, gewählte Größenreferenz Mittelgroße Sorte — 0,4 m ([PokéWiki](https://www.pokewiki.de/Irrbis)) | Rechts auf der Mauer nahe dem Schloss, schwebend. | mittel | ca. 20 % |
| Halloween | Mimigma, Normalform — 0,2 m ([PokéWiki](https://www.pokewiki.de/Mimigma)) | Unten rechts am Weg, klein und halb hinter der Steinböschung hervorschauend. | nah | ca. 12 % |
| Winter | Glaziola, Normalform — 0,8 m ([PokéWiki](https://www.pokewiki.de/Glaziola)) | Links auf dem verschneiten Felsvorsprung, liegend und nach rechts blickend. | nah | ca. 25 % |
| Winter | Vulpix, Alola-Form — 0,6 m ([PokéWiki](https://www.pokewiki.de/Vulpix)) | Rechts neben einem kleinen Schneemann, sitzend. | nah | ca. 20 % |
| Frühling | Feelinara, Normalform — 1,0 m ([PokéWiki](https://www.pokewiki.de/Feelinara)) | Groß rechts auf der blühenden Steinplattform, sitzend und nach links blickend. | nah | ca. 48 % |
| Frühling | Haspiror, Normalform — 0,4 m ([PokéWiki](https://www.pokewiki.de/Haspiror)) | Unten rechts an den Blüten, sitzend und zur Mitte gewandt. | nah | ca. 14 % |
| Frühling | Shaymin, Landform — 0,2 m ([PokéWiki](https://www.pokewiki.de/Shaymin)) | Links unten zwischen Blumen und Steinen, klein und bodennah. | nah | ca. 11 % |
| Sommer | Schiggy, Normalform — 0,5 m ([PokéWiki](https://www.pokewiki.de/Schiggy)) | Links vorne am Wasserrand auf einem Felsen, sitzend und dem Betrachter zugewandt. | nah | ca. 20 % |
| Sommer | Lapras, Normalform — 2,5 m ([PokéWiki](https://www.pokewiki.de/Lapras)) | Rechts der Mitte im Meer, schwimmend; die Wasserlinie verdeckt einen Teil des Körpers. | mittel | ca. 24 % |
| Sommer | Mantirps, Normalform — 1,0 m ([PokéWiki](https://www.pokewiki.de/Mantirps)) | Weit hinten rechts im Wasser; nur ein kleiner Teil des Körpers ist über der Oberfläche sichtbar. | fern | ca. 11 % |
| Erntezeit | Folipurba, Normalform — 1,0 m ([PokéWiki](https://www.pokewiki.de/Folipurba)) | Links auf dem unteren Steinweg, geduckt und nach rechts blickend. | nah | ca. 31 % |
| Erntezeit | Kronjuwild, Frühlingsform — 1,9 m ([PokéWiki](https://www.pokewiki.de/Kronjuwild)) | Rechts auf der erhöhten Mauer, ruhig neben Bauz. | mittel | ca. 16 % |
| Erntezeit | Bauz, Normalform — 0,3 m ([PokéWiki](https://www.pokewiki.de/Bauz)) | Neben Kronjuwild auf der Mauer, sitzend und nach vorne gerichtet. | mittel | ca. 7 % |
| Neujahr | Pikachu, Normalform — 0,4 m ([PokéWiki](https://www.pokewiki.de/Pikachu)) | Unten links am Geländer, aufrecht und zur Bildmitte schauend. | nah | ca. 20 % |
| Neujahr | Piepi, Normalform — 0,6 m ([PokéWiki](https://www.pokewiki.de/Piepi)) | Links auf erhöhter Steinplattform, sitzend und nach rechts blickend. | nah | ca. 15 % |
| Neujahr | Jirachi, Normalform — 0,3 m ([PokéWiki](https://www.pokewiki.de/Jirachi)) | Rechts über dem Geländer schwebend. | mittel | ca. 20 % |
| Arceus | Arceus, Normalform — 3,2 m ([PokéWiki](https://www.pokewiki.de/Arceus)) | Links auf schwebender Plattform, groß und majestätisch aufgerichtet. | nah | ca. 48 % |
| Arceus | Palkia, Normalform — 4,2 m ([PokéWiki](https://www.pokewiki.de/Palkia)) | Rechts auf zweiter Plattform, kampfbereit in Richtung Arceus. | nah | ca. 34 % |
| Arceus | Giratina, Wandelform — 4,5 m ([PokéWiki](https://www.pokewiki.de/Giratina)) | Hoch rechts im Hintergrund über der Plattform schwebend. | fern | ca. 20 % |

## Quellenhinweise zu Formen

- **Irrbis:** PokéWiki listet Kleine Sorte 0,3 m, Mittelgroße Sorte 0,4 m, Große Sorte 0,5 m und Extragroße Sorte 0,8 m. Für die Komposition ist die Mittelgroße Sorte als Größenreferenz gewählt. PokeAPI ordnet Pokémon 710 `pumpkaboo-average` zu und nennt eine Höhe von 4 dm; das stützt die Referenzwahl. Das finale generierte Szenenbild ist dadurch nicht als direkt aus Artwork-ID 710 abgeleitet oder visuell exakt dieser Sorte belegt.
- **Vulpix:** Die Szene verwendet die Alola-Form. Beide umschaltbaren PokéWiki-Infoboxen führen 0,6 m.
- **Shaymin:** Die Szene verwendet die Landform (0,2 m), nicht die Zenitform (0,4 m).
- **Giratina:** Die Szene verwendet die Wandelform (4,5 m), nicht die Urform (6,9 m).
- **Kronjuwild:** Die Szene zeigt die Frühlingsform; PokéWiki führt für die Formfamilie dieselbe Spezieshöhe von 1,9 m.
