# Halloween: getrennte Tiefenebenen

Die Szene verwendet fünf unabhängige Bildebenen. Von hinten nach vorne:

| Ebene | Inhalt | Mausbewegung X/Y in DIP |
|---|---|---|
| Ferne | Himmel, Mond, Berge und Wald | 4 / 2,5 |
| Schloss | Freigestelltes Schloss mit beleuchteten Fenstern | 7 / 4 |
| Gengar | Vorhandenes transparentes Pokémon-Artwork | 11 / 6 |
| Gelände | Steinmauern, Weg, niedrige Pflanzen, Irrbis und Mimigma | 15 / 8,5 |
| Nahrahmen | Bäume links/rechts und unscharfe Büsche unten | 24 / 14 |

Alle vier flächigen PNGs liegen registriert auf demselben 1672×941-Rahmen. Ein gemeinsamer Bildausschnitt mit genügend Überstand verhindert unterschiedliche Skalierung und freie Ränder bei Mausbewegungen. Bäume und Büsche bilden eine einzige vorderste Ebene ohne ausblendende Verlaufsmaske. Der Nahrahmen liegt vor dem Gelände und hinter der festen Oberfläche. Die frühere harte Schnittkante bei 64 DIP entfällt; die deckende Titelleiste steht unabhängig von der Kamera.

Die bestehende Gengar-Grafik `assets/artwork/gengar.png` wird wiederverwendet. Seine Fluganimation blendet ausschließlich diese Ebene aus und wieder ein. Die Spinne bleibt am projizierten Ast befestigt; Bodenobjekte folgen der Geländeebene.

## Assets und Bildbriefings

Die neuen Dateien liegen unter `src/RebornLauncher/assets/scenery/parallax/`. Die vorhandenen Originale bleiben erhalten. Vier Bilder wurden mit dem eingebauten Bildwerkzeug nach dem Skill `imagegen` bearbeitet, jeweils anhand der bisherigen Halloween-Illustration, beim Nahrahmen zusätzlich anhand des bestehenden transparenten Baumrahmens.

- `halloween-forest.png`: Undurchsichtige ferne Bildebene; nächtliche Palette, Mondposition und Blickwinkel erhalten. Bäume im Vordergrund, Gelände, Pokémon und Schloss entfernen und die frei werdenden Bereiche mit fernem Wald ergänzen.
- `halloween-castle.png`: Nur das Schloss mit seinen Fenstern und Türmen freistellen, transparenten Hintergrund verwenden. Untere verborgene Mauern ergänzen, keine Pokémon oder Gelände übernehmen.
- `halloween-terrain.png`: Steinweg, niedrige Mauern, niedrige Pflanzen, Kürbisse, Laternen, Irrbis und Mimigma erhalten. Gengar, große Bäume, unscharfe vorderste Büsche sowie Schloss und Hintergrund entfernen; natürliche Stein-/Blattkonturen freistellen.
- `halloween-near.png`: Nur die großen orangefarbenen Randbäume und unscharfe dunkle Büsche mit orangefarbenen Blättern in den unteren Ecken. Tatsächlicher Alphakanal, freie Bildmitte, keine Steine, Pokémon oder Hintergrundflächen. Astposition wie beim bisherigen Baumrahmen.

## Prüfung

`tests/ParallaxUiTests` prüft fünf Kamerapositionen bei fünf Fensterformaten: getrennte Bewegung und Verdeckungsreihenfolge, gemeinsame Bildregistrierung, Abdeckung der Ränder, feste Bedienelemente, erreichbaren Startknopf, erhaltene untere Büsche, reduzierte Bewegung und Themenwechsel. Es rendert Ansichten unter `test-results/parallax`.

Die vorhandenen Gengar- und Spinnentests prüfen zusätzlich die unveränderten Pixel außerhalb Gengars, seine Wiederkehr und die Befestigung des Spinnenfadens bei Kamera- und Größenänderungen. Alle Prüfungen verwenden isolierte Launcher-Vorschauen, ohne Spielstart oder Zugriff auf den aktiven Spielordner.

Ausgeführt am 26.10.2026: `ParallaxUiTests` bestanden mit 25 Kamera-/Fensterfällen und zehn gerenderten Ansichten. `GengarUiTests` bestanden einschließlich echter Animationstimer, Pixelvergleich außerhalb der Figur und exakter Wiederherstellung. Berichte: `test-results/parallax/checks.json` und `test-results/gengar-parallax/result.json`.

Die integrierte Spinnen-/Deko-Prüfung bestand ebenfalls mit 25 Kamera-/Fensterfällen (`test-results/bat-flight-final/result.json`). Die Trankflasche wurde für den größeren Kameraweg leicht nach innen versetzt; die Fledermaus verwendet die Projektion der Schloss-Ebene. Das vollständige Release-Build wurde lokal gebaut; der öffentliche Updatekanal wurde für diese Änderung nicht angefasst.

Abschließend bestand die erweiterte Prüfung einschließlich besonders hohem Fenster mit 30 Fällen (`test-results/bat-flight-tall-regression-final/result.json`). Dabei wurde auch der Grabstein im hohen Fenster nach innen versetzt. Der kombinierte Menü-/Hauptansichtstest bestand einschließlich acht Themes (`test-results/menu-navigation-combined/result.json`). Die lokale Vorschau liegt unter `dist/parallax-preview/Parallax ansehen.cmd`; der Starter öffnet ausschließlich die Halloween-Vorschau. Die integrierte EXE hat SHA-256 `4e4032c3f71f3779bbcd7794b4434dd98cfaf6e9fb566a0c8f044ab170c80bba`.

Auch `ReferenceLayoutTests` wurde an die neuen Halloween-Assets und Bewegungsweiten angepasst und vollständig ausgeführt: acht Themes × zwei Lichtzustände × vier Fenstergrößen sowie echte Kameratimer, Randabdeckung, feste Oberfläche und Menüs bestanden (`test-results/reference-parallax-final/result.json`).
