# Interaktive Jahreszeiten

Die Erweiterung verwendet das vorhandene WPF-Theme-System. Der Kalender bestimmt automatisch das saisonale Design. Die Beleuchtung startet mit Nacht und lässt sich dauerhaft auf Tag, Nacht oder Auto stellen. Nur bei Auto gilt die lokale Uhrzeit: Tag von 07:00 bis 19:00 Uhr, sonst Nacht. Das seltene Arceus-Theme bleibt eine eigene Tagesbegegnung. Musik, Mausspur und Effekttöne sind voneinander unabhängig.

## Bedienung

Anklickbare Objekte stehen auf der Pokémon-Bühne. Leere Bereiche nehmen keine Mausklicks entgegen; Spielstart, Versionswechsel und Texte bleiben frei. Jedes Theme hat eigene bevorzugte Positionen. Die tatsächliche Fenstergröße und die Pokémon-Flächen bestimmen freie Ruheplätze. Dieselben Modellkoordinaten werden für Darstellung, Trefferflächen, Partikel und schlüpfende Figuren verwendet. Flugbahnen laufen hinter den Pokémonbildern; neue Figuren verschieben keine bereits platzierten Objekte. Die Objekte können außerdem als eine Gruppe mit Tab fokussiert werden: Pfeiltasten wählen ein Objekt, Eingabe oder Leertaste aktiviert es. Eine leichte Bewegung beim Darüberfahren und ein kleiner Punkt bei Tastaturauswahl zeigen das gewählte Objekt; es gibt keine Auswahlrahmen.

Der Schalter **Effekte** öffnet Dekorationen, Interaktionen, Partikelqualität, Animationsintensität, Event-Geräusche und Lautstärke. Dort befinden sich auch die zusätzlichen Halloween-Ambient-Einstellungen. Event-Geräusche starten standardmäßig ausgeschaltet; es gibt keinen lauten Sound beim Öffnen des Launchers. Vorschaufenster speichern keine Spielordner oder Einstellungen und führen keine Spiel- oder Updateaktionen aus. Musik kann dort einzeln eingeschaltet werden.

## Erweiterte Themes

| Theme | Beispiele der Objekte und Aktionen |
|---|---|
| Winter | Schneemann zerfällt in drei physikalisch bewegte Schneekugeln; Schneeball fliegt und zerplatzt beim Aufprall; Eiszapfen fällt und zersplittert; Schneehaufen wirbelt auf und wächst begrenzt nach; Tanne und Kristall reagieren. |
| Weihnachten im Winter-Theme | Zusätzliche Geschenke mit wechselnden Überraschungen, verzögert brechende Kugeln, schwingende Glocken, Schneekugel, Zuckerstange und reagierende Lichterkette. |
| Silvester / Neujahr | Mehrere Raketen fliegen gleichzeitig, explodieren in unterschiedlichen Mustern und zünden eine zweite Stufe. Funken, Konfetti und ein begrenzter Lichtschein bleiben innerhalb der Bühne. |
| Frühling / Ostern | Eier öffnen sich und lassen temporäre Küken oder Hasen erscheinen; Fundzähler pro Ei; Schokohase, Nest, Blume und Schmetterling reagieren. |
| Halloween | Kürbisse, springende Vogelscheuche, wackelnder Grabstein mit Skeletthand, absteigende Spinne, fliegende Fledermaus, zeitweise sichtbarer Geist, blubbernder Kessel und Kerze. |
| Sommer | Platzende Wasserballons und Seifenblasen, Wasserpartikel und bewegte Glühwürmchen. |
| Herbst | Laubhaufen, Eicheln und Pilze mit Blattpartikeln und wiederkehrenden Objekten. |
| Standard / Arceus | Anklickbare Sterne und Lichtkristalle, seltene Bonusvarianten und passende Lichtpartikel. |

Zerstörte Objekte kehren nach etwa 8–15 Sekunden zurück. Eier werden dabei nicht mehrfach als neuer Fund gezählt. Der Fundzähler gilt für die aktuelle Szene; ein Theme-Wechsel oder Neustart beginnt eine neue Sammlung. Temporäre Figuren verschwinden nach ihrer kurzen Animation. Themenwechsel entfernen alte Partikel und ausstehende Folgeexplosionen.

## Assets, Grenzen und Ressourcen

### Silvesterfeuerwerk

Die Silvesterraketen sind Papier-Feuerwerkskörper mit Kappe, Holzstab und Zündschnur. Zusätzlich steigen automatisch Raketen aus wechselnden Positionen auf und explodieren über der gesamten Launcherfläche, auch in der unteren Fensterhälfte. Fünf Muster wechseln zufällig: Chrysantheme, Ring, Goldweide, Palm und Crackle mit zeitversetzten Folgeexplosionen. Funken bremsen ab, fallen durch Schwerkraft und verblassen; Goldweide und Palm hinterlassen kurze Verlaufsspuren.

Die transparente Feuerwerksebene fängt keine Klicks ab. Sie erscheint nur im Neujahrsthema und folgt den vorhandenen Einstellungen für Dekorationen, Partikelqualität, Intensität und Effektgeräusche. Bei reduzierter Bewegung, ausgeschalteten Partikeln, ausgeblendetem/inaktivem Launcher oder gestartetem Spiel stoppt sie. Drei Viertel des Partikelbudgets stehen dem automatischen Feuerwerk zur Verfügung, der Rest den anklickbaren Saisonobjekten. Es gibt keine Vollbildblitze. `tests/FireworkTests` prüft die Simulation; `tests/FireworkUiTests` prüft die Einbindung im tatsächlichen WPF-Fenster einschließlich Klickdurchleitung, Größenwechsel, Einstellungen und Ausblenden.

- 40 vorhandene und ergänzte Pokémon-Artworks aus dem dokumentierten PokeAPI-Repository. Pokémon bleiben deckend; Größen und Platzierung richten sich nach Figur und Fensterfläche. Die Interaktionsobjekte sind lokal in einem einheitlichen Vektorstil gezeichnet, ohne Emoji-Ersatzbilder.
- 50 selbst erzeugte WAV-Dateien für 25 Interaktionsgeräusche in je zwei Varianten; maximal vier Stimmen gleichzeitig. Credits und reproduzierbarer Generator stehen in `EVENT-SOUND-CREDITS.md` und `tools/generate-event-sounds.py`.
- Zusätzlich zwölf separat geprüfte synthetische Halloween-Ambient-Sounds aus dem dazugehörigen Generierungsskript; Planung, Pausen und Wiederholungsschutz sind in `HALLOWEEN-AMBIENT-AUDIO.md` beschrieben.
- Partikel sind auf 0 / 80 / 160 / 320 begrenzt, temporäre Props auf 48. Inaktive oder minimierte Fenster und laufende Spiele pausieren die interaktiven Szenen. Reduzierte Bewegung hält die Objekte ruhig; laufende Wiederaufbauphasen werden sparsam weitergeführt.
- Die Simulation ersetzt keine vollständige Spielphysik. Es gibt keine kollidierende Schneedecke über allen Launcher-Bedienelementen und keine dauerhaft über Neustarts gespeicherte Eier-Sammlung. Die Geräusche sind synthetische Effekte, keine Naturaufnahmen oder gesprochenen Geisterstimmen.

## Prüfung

`tests/PropTests` enthält 19 bestandene Prüfungen für Flugbahnen, Aufprall, Wiederaufbau, Mehrfachraketen, temporäre Figuren, Sammlungszähler, Limits, Theme-Reset und die Übereinstimmung versetzter Objekt- und Effektpositionen. `tests/PropLayoutTests` prüft alle acht Themes in drei Bühnenformaten: Ruheplätze ohne Überdeckung oder abgeschnittene Trefferflächen, Resize-Reflow, übereinstimmende Modell- und Renderpositionen während der Interaktion, Wiederaufbau und neue Figuren ohne Verschieben vorhandener Objekte.

Der gemeinsame WPF-Smoke prüft Tag-/Nachtansichten, kleine und maximierte Fenster, Mausspur und Musikwechsel sowie die tatsächliche stumme WPF-Dekodierung von 50 Effekt-WAVs, zwölf Ambient-WAVs und acht Musikstücken. Native Maximierungsgrenzen wurden zusätzlich auf zwei Monitoren geprüft. Ein manueller Hörtest und ein Wechsel zwischen Monitoren mit unterschiedlichen DPI-Stufen sind damit nicht ersetzt.

Spielstände und persönliche Spieldateien werden von diesen Komponenten nicht verwendet. Sämtliche Entwicklungs- und Vorschautests liegen im Arbeitsordner auf D:.
