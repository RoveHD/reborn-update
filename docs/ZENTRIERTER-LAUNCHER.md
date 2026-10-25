# Zentrierte Launcher-Oberfläche

Logo und Startbutton liegen auf derselben horizontalen Fenstermitte. Die
Versionsauswahl steht in einer eigenen Zeile unter dem Startbutton. Die
Saisonbühne nutzt die verfügbare Fensterbreite mit Pokémon links und rechts;
Logo und Texte werden bei der Platzierung anklickbarer Dekorationen ausgespart.
Kleine Fenster behalten sämtliche Start- und Updateaktionen ohne Hauptscrollbar.
Die diagonalen Pfeile im Startbutton und in den Linkbeschriftungen entfallen.

„Was ist neu?“ öffnet ein eigenes, vergrößerbares Fenster im aktuellen Theme.
Lange Versionshinweise sind dort scrollbar. Aktualisieren und „Release öffnen“
bleiben verfügbar. Das Fenster verwendet dieselben geladenen Textkontrollen,
damit neue Hinweise und Offline-Fehler sofort erscheinen. Nach dem Schließen
und beim erneuten Öffnen bleibt die Hauptansicht unverändert bedienbar.

## Halloween-Gengar

Bei aktivem Halloween-Theme erscheint nach zufällig 42–100 aktiven Sekunden ein
kurzer Gengar-Flug: zufällige Seite und Höhe, eine volle Drehung, ein Lacher,
anschließend verschwindet Gengar. Die etwa 2,45 Sekunden lange Einlage fängt
keine Eingaben ab. Die bestehenden Einstellungen für Event-Dekorationen,
reduzierte Bewegung, Event-Geräusche und Effektlautstärke gelten auch hier.
Inaktive, versteckte oder minimierte Launcher und laufende Spiele stoppen den
Effekt. Nach einer Pause werden keine verpassten Flüge nachgeholt.

Das Lachen verwendet vier vorhandene Halloween-Ambient-WAVs über eine separate
Stimme, ohne den Zeitplan der seltenen Hintergrundgeräusche zu verändern.

## Gezielte Prüfungen

- `tests/CenteredLayoutTests`: acht Themes in vier Fenstergrößen einschließlich
  960×640 und breitem 1920×640; Mittelpunkt von Logo und Startbutton, freie
  Bedienung, Szenenbegrenzungen und Dekorationsflächen; Changelog öffnen,
  schließen, erneut öffnen, lange Texte scrollen und Live-/Offline-Text ändern.
- `tests/GengarUiTests`: echter Dispatcher-Timer, Flug und einmaliger Lacher,
  Klickdurchreichung, Stoppen bei Themewechsel/reduzierter Bewegung/Inaktivität
  und Verstecken, Schutz vor veralteten Audiocallbacks.
- `tests/AmbientTests`: bestehender Ambient-Zeitplan sowie separate Gengar-
  Soundfreigabe, Lautstärke und Stoppen.
- `tests/BloodEasterEggTests`: Auslösen erst mit fünf Klicks innerhalb von vier
  Sekunden, Zeitfenster-Reset, Ersetzen statt Stapeln, wachsende Tropfen,
  selbstständiges Verblassen, statische reduzierte Variante und Lebenszyklus.
- `tests/PokemonSelectionTests`: feste eingezeichnete Besetzung je Theme,
  ausgeblendete frühere Sprite-Ebenen, passende Beschriftung und weiterhin
  lesbare alte Einstellungen. Der Pokémon-Wechsel wurde auf Nutzerwunsch entfernt.

Die Prüfungen verwenden Vorschaufenster und isolierte Testdaten. Ein manueller
Hörtest und ein Wechsel zwischen Monitoren mit unterschiedlichen DPI-Skalierungen
sind damit nicht abgedeckt. Veröffentlichungen erfolgen separat.

## Logo-Easter-Egg

Fünf schnelle Klicks auf das Reborn-Logo lösen einen vorübergehenden Bluteffekt
aus: Spritzer verteilen sich über die Fensterfläche, Kleckse hinterlassen
herablaufende Tropfenspuren. Das Overlay fängt keine Eingaben ab und verändert
weder Einstellungen noch Spieldateien. Wiederholtes Auslösen ersetzt den
laufenden Effekt. Verstecken, Deaktivieren und Spielstart räumen ihn auf.
Bei reduzierter Bewegung wird eine kurze statische Variante verwendet.
