# Feste Pokémon-Besetzung im Launcher

Die Pokémon sind fester Bestandteil der saisonalen Szenenillustrationen. Sie sind direkt in die Bildkomposition eingezeichnet; es gibt keinen Pokémon-Picker und keine pro Theme auswählbaren Begleiter. Die frühere Beschreibung eines durchklickbaren Haupt-Pokémon und gespeicherter Auswahl-Overrides ist veraltet.

Die aktuell bestätigte Besetzung und Komposition ist in [BEWOHNTE-ANIME-SZENEN.md](BEWOHNTE-ANIME-SZENEN.md) dokumentiert. Das Dokument nennt die Pokédex-Größen samt PokéWiki-Quellen sowie grobe visuelle Anteile der Bildhöhe. Diese Anteile sind illustrative Schätzungen aus den Szenenbildern; es gibt keine feste Umrechnung von Metern zu Pixeln.

## Szenen-Assets und Darstellung

Die integrierten saisonalen Landschaften liegen in `src/RebornLauncher/assets/scenery/inhabited/`. Die Anime-Landschaften und transparenten Vordergrundebenen liegen getrennt in `src/RebornLauncher/assets/scenery/anime/`; `AtmosphericBackdrop` zeichnet Landschaft, Atmosphäre und Vordergrund auf unabhängigen Ebenen. Saisonale Dekorationen und Partikel werden als weitere Darstellungsebenen darübergelegt. So entsteht ein leichter 2.5D-Eindruck, während Startschaltfläche, Statusinformationen und Navigation auf ihren stabilen UI-Flächen bleiben.

Die Pokémon in den Szenen sind Bildbestandteil. Es wird keine individuelle Pokémon-Animation behauptet; Bewegung entsteht durch Atmosphäre, Parallax und saisonale Effekte, soweit diese aktiviert sind.

## Quellen

Die offiziellen Körpergrößen und Formangaben stehen in [POKEMON-GROESSEN-QUELLEN.json](POKEMON-GROESSEN-QUELLEN.json). Die Ermittlungsmethode und dokumentierten Formfälle sind in [POKEMON-GROESSEN-QUELLEN.md](POKEMON-GROESSEN-QUELLEN.md) erläutert. Die Höhen sind Pokédex-Angaben, keine Vorgabe für die dargestellte Pixelgröße.
