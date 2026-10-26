# Feste Boden-Props und durchgehende Szene

Die Kontaktpunkte von Buch, Kerze, Flasche, Kürbis, Kessel und Grabstein liegen auf Steinflächen der Halloween-Szene. Die Flasche steht auf dem Pflaster statt vor Irrbis/Mimigma oder vor der Laternenflamme. Für schmale und hohe Fenster gibt es eigene Standpunkte. Die Props folgen weiterhin der Geländeebene; Bodenobjekte schweben und kippen im Ruhezustand nicht. Ihre Interaktionsanimationen bleiben erhalten.

Die dauerhafte `PokemonStage` liegt jetzt direkt im `ViewHost`, neben dem Inhalt der Startseite. Der Wechsel zu Changelog, Extras oder Einstellungen blendet ausschließlich die Bedienelemente der Startseite aus. Die Szene bleibt im Hintergrund erhalten. Der verborgene Startseiteninhalt behält seine Layoutgröße, damit beim Vergrößern eines Menüs die Schutzflächen für die Deko stabil bleiben. Die Menüseiten liegen weiterhin vor der Szene und erhalten die Eingaben.

Geprüft am 26.10.2026:

- `SpiderAttachmentTests`: 30 Fenster-/Kamerafälle mit Boden-Kontaktpunkten, Idle-Stabilität, Spinne, Fledermaus, Interaktionen und Klickflächen. Bericht: `test-results/grounded-props-final/result.json`.
- `CenteredLayoutTests`: Hauptansicht in acht Themes; neue Prüfung für alle acht Themes sowie kleine, breite und hohe Fenster. Beim Wechsel durch alle Menüseiten bleiben Props sichtbar, werden nicht entladen oder neu erstellt und behalten ihre Positionen. Auch Menübedienung, Scrollposition, Escape, Dialoge und Downloadabbruch werden geprüft. Bericht: `test-results/props-persistent-navigation/result.json`.

Die Kontaktpunkte wurden zusätzlich in gerenderten Ansichten bei 1280×800 und 960×1000 kontrolliert. Die unveränderten Originalbilder bleiben erhalten. Die Änderung ist lokal; der öffentliche Updatekanal und aktive Spielordner werden nicht angefasst.
