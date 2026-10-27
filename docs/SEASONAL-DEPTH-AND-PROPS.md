# Saisonwelten: gemalte Deko und 2,5D

Alle Themes außer Halloween verwenden mindestens drei unabhängige gemalte Bildebenen: Ferne, Gelände und Nähe. Halloween bleibt auf seinem bisherigen Bild-, Bewegungs- und Interaktionspfad.

## Aufbau

- Standard, Frühling, Sommer, Herbst, Neujahr, Arceus und Winter: eigene ferne Kulisse, Geländeebene mit den dazugehörigen Pokémon und transparente Nah-Ebene. Alle drei Ebenen nutzen unabhängig 4 / 15 / 24 DIP horizontal und 2,5 / 8,5 / 14 DIP vertikal. Die feste Bedienoberfläche bewegt sich nicht mit.
- Sommer: Wasserlinie und schwimmende Pokémon liegen gemeinsam in der Geländeebene. Sie bewegen sich zusammen mit der Geländeparallaxe (15 / 8,5 DIP); die ferne Kulisse und der Nahrahmen behalten ihre eigene Tiefe.
- Winter: Die ferne Ebene verwendet die bestehende Landschaft. Das transparente Winter-Vordergrundbild bildet zwei getrennte Ebenen: obere Äste bis y=390/941 und untere Büsche ab dort. Der Schnitt liegt im transparenten Band des Bildes (y=363 bis 423); dadurch wird keine sichtbare Bildkante durchschnitten. Die Deko bleibt einzeln verankert: Bodenprops folgen der Landschaft, der Januar-Eiszapfen dem oberen Ast.
- Geländeobjekte werden aus Bildkoordinaten in die tatsächliche Ansicht projiziert. Wiederholte Objekttypen erhalten eigene Anker. Der Eiszapfen folgt dem Ast der nahen Winterebene.
- Die neue Deko wird aus sieben transparenten Atlanten geladen und eingefroren zwischengespeichert. Abweichende Atlasabstände sind als explizite Ausschnitte beschrieben; Blätter und Blumen werden dadurch vollständig angezeigt.
- Alle saisonalen Landschaftsebenen teilen eine normalisierte Fläche von 1672×941. Zwei Geländebilder (Herbst und Neujahr) wurden vom Bildwerkzeug mit 1671×941 ausgegeben und werden auf dieselbe Fläche abgebildet. Die übrigen Quellen sind 1672×941.
- Reduzierte Bewegung setzt die Kamera sofort zurück; inaktive Ansichten stoppen die Bewegung. Bestehende Lichtübergänge bleiben erhalten.

## Grafiken und Herkunft

Erstellt mit dem integrierten Imagegen-Werkzeug. Ausgangsbilder wurden erhalten. Die finalen Prompts, Ursprungsdateien und Zielpfade stehen in [SEASONAL-ARTWORK-PROMPTS.json](SEASONAL-ARTWORK-PROMPTS.json). Saisonale Ebenen liegen in `src/RebornLauncher/assets/scenery/parallax/`; Winter teilt die vorhandene transparente Grafik unter `assets/scenery/anime/winter-foreground.png` in zwei nicht überlappende Ausschnitte. Die Atlanten liegen in `src/RebornLauncher/assets/scenery/props/*-props.png`.

## Prüfung des ersten Ausbaus

- `SeasonalDepthTests`: 175 Kamera-/Fensterfälle in sieben Themes, Ebenenregistrierung, Alpha, Terrainprojektion, feste Bedienelemente, echte Kamerabewegung, reduzierte Bewegung und Wechsel zurück zu Halloween.
- `ParallaxUiTests`: 25 Halloween-Kamera-/Fensterfälle.
- Separater Vergleich der ursprünglichen und neuen AtmosphericBackdrop-Source mit identischen Halloween-Ressourcen: 25 Renderfälle mit identischen BGRA-Pixelhashes.
- `ReferenceLayoutTests`: 64 Kombinationen aus acht Themes, Tag/Nacht und vier Fenstergrößen; Ebenen, Randabdeckung, feste UI, Kamera und Menüs erfolgreich geprüft.
- `CenteredLayoutTests`: unveränderte Layout- und Interaktionsprüfungen erfolgreich, einschließlich echter Fenster-Klickziele, dauerhafter Props, Fledermaus-Rückkehr, Navigation und Downloadabbruch.
- Der zusätzliche Januar-Test bestätigt, dass der Eiszapfen bei Kamerabewegung der nahen Winterebene folgt. Bodendeko verwendet je Bildformat eigens gesetzte Kontaktpunkte auf sichtbaren Flächen.
- Alle 25 neuen Asset-Dateien stimmen mit `SEASONAL-ASSET-HASHES.json` überein.
- Zusätzliche MainWindow-Ankerdiagnose: 40 Fälle mit fünf Formaten einschließlich Januar-Winter ohne Layoutfehler; 16 Ansichten bei 1280×800 und 960×1000 gerendert.
- `SettingsUiTests` im gemeinsamen Arbeitsstand: 32 Kombinationen erfolgreich, einschließlich Popup-Pixeln, Lichtauswahl, Dialogscrollen und Übernehmen/Abbrechen. `StatusUiTests` bestätigt Fortschrittsphasen, Downloadangaben und das Entfernen alter Anzeigen.
- Die veröffentlichungsfrei gebaute Vorschau-EXE besteht die Lichtprüfung für acht Paletten, kontinuierliche Farb-/Bildübergänge, Richtungswechsel und reduzierte Bewegung.

Prüfberichte: `test-results/seasonal-depth-final/checks.json`, `test-results/seasonal-reference-final/result.json`, `test-results/seasonal-centered-final/result.json`, `test-results/anchor-diagnostics-final/layout.json`, `test-results/seasonal-settings-final/result.json`, `test-results/seasonal-status-final/result.json` und `test-results/seasonal-lighting-final/result.json`.

Erster Vorschau-Build (vor der nachfolgenden Kompositionskorrektur): `dotnet publish src/RebornLauncher/PokemonRebornLauncher.csproj -c Release -r win-x64 --self-contained true -o dist/seasonal-preview` erfolgreich. EXE: 180.154.101 Byte; SHA-256 `6874efee0cb3972cd0b92577d978e62cce3063d035a3b1e6fe31c10868b9a496`.

Die lokale Vorschau liegt unter `dist/seasonal-preview/`; die sieben `Vorschau-*.cmd` starten die Themes einzeln im isolierten Vorschau-Modus. Es wurde kein neuer Release veröffentlicht und kein Updatekanal geändert. Spielinstallation und Spielstände werden in diesem Modus nicht verändert.

## Kompositionskorrektur und Wasserbindung

Alle sieben saisonalen Szenen haben jetzt eigene Größenhierarchien und Bildanker für die Fundstücke. Kleine Eier, Eicheln und Sterne wechseln sich mit größeren Blickfängen ab. Die Platzierung nutzt Blumenbeete, Äste, Schnee, Mooskanten und Steinvorsprünge statt einer einheitlichen Reihe am unteren Bildrand. Nest/Ei und Baum/Geschenk bilden zusammengehörige Gruppen. Schmale und breite Fenster verwenden passende Ausschnitte und Kontaktpunkte. Halloween bleibt unverändert.

Jedes saisonale Theme hat drei unabhängig transformierte Ebenen. Im Sommer sind Wasserlinie und Schwimmer gemeinsam im Gelände gemalt und an dessen Transform gebunden (15 / 8,5 DIP); die ferne Ebene bewegt sich mit 4 / 2,5 DIP und die Nähe mit 24 / 14 DIP. Im Winter bilden Landschaft, oberer Astbereich und untere Büsche drei Tiefen mit denselben Bewegungsstufen. Die beiden transparenten Winterausschnitte teilen dieselbe Quelle, aber ihre Clipflächen überschneiden sich nicht.

Am finalen Quellstand geprüft:

- `test-results/seasonal-compositions-final/layout.json`: 40 Fälle, keine Layoutfehler; 21 gerenderte Ansichten in Normal-, Breit- und Hochformat.
- `test-results/seasonal-compositions-depth/checks.json`: 175 Kamera-/Fensterfälle, darunter 25 Sommerfälle mit unveränderter Wasserbindung, feste UI, reduzierte Bewegung, Januar-Eiszapfen und unveränderte Halloween-Pixel nach Themewechseln.
- `test-results/seasonal-compositions-reference/result.json`: 64 Kombinationen aus acht Themes, zwei Lichtzuständen und vier Größen; Randabdeckung, Parallax und Menüs erfolgreich.
- `test-results/seasonal-compositions-centered/result.json`: Layout, dauerhafte Props, echte Klickziele, Fledermaus-Rückkehr, Navigation und Downloadabbruch erfolgreich.
- Das abgestimmte optimierte Silvester-Feuerwerk ist enthalten; dessen separate native Prüfung steht in `test-results/fireworks-optimized-ui/result.json` und `FIREWORK-PERFORMANCE.md`.

Aktuelle lokale Vorschau: `dist/seasonal-composition-preview/`. Dort starten die sieben `Vorschau-*.cmd` das jeweilige Theme isoliert. Release-Publish erfolgreich, EXE 180.170.997 Byte, SHA-256 `3A471502E8009EFDA76703225964B07B77596A497D7BA3158E9CBD399AA3CD15`. Keine Veröffentlichung und keine Änderung des Updatekanals.
