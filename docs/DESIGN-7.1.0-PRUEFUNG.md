# Launcher 7.1.0 – lokale Designfassung

Stand: 25.10.2026. Das Design ist lokal gebaut, nicht veröffentlicht.

## Ergebnis

Eigene WPF-Titelleiste, dunkle violette Bühne mit bewegten Ringen, Barlow-Schriften,
Nachtara-/Absol-Artwork mit gespeicherter Auswahl, animierte Schalter, eigener
Bestätigungsdialog, Musikregler und reduzierte Bewegung. Musik und Grafiken sind
lokal eingebettet. Die Musik pausiert bei minimiertem Fenster und während eines
über den Launcher gestarteten Spiels. Menü und Versionswechsel bleiben erhalten.

## Tatsächlich geprüft

- Self-contained Windows-x64-Build erfolgreich.
- 46/46 Core-Prüfungen, einschließlich Speicherung der neuen Darstellungsoptionen.
- WPF-Fenster bei 1280×800 und 960×640 gerendert und visuell kontrolliert.
- Versionsauswahl, Musikschalter, Lautstärke, reduzierte Animationen und Motivwechsel automatisiert geprüft.
- Downloadzustand, Abbruchknopf, lange Versionshinweise und Dialogabbruch geprüft.
- Eingebettete MP3 vom Windows-Mediencodec geöffnet: 129,593 Sekunden; stumm getestet.
- Tatsächlicher EXE-Austausch von 7.0.0 zu 7.1.0 mit Neustartbestätigung, bytegenauer Sicherung der alten EXE und unveränderten isolierten Test-Spielständen/Einstellungen.
- Quellen und Schriftlizenz im portablen ZIP enthalten.

Berichte: test-results/design-7.1.0/result.json und test-results/self-update.json.
EXE-SHA256: 00dcf0d37db4353615634a77f494f5fb027a67b1adae3a917055064055271c14

## Grenzen

Kein manueller Hörtest, kein Spielstart und kein vollständiger Spieltest.
Keine Änderungen am persönlichen Spielordner auf Z:.
Der Downloadzustand wurde mit Testdaten geprüft, kein vollständiges Spielpaket erneut geladen.

Separat ist der minimale Manifest-Hotfix für drei nachgewiesene alte deutsche Mods
bereits im bestehenden Updatekanal veröffentlicht (cc2c96d). Siehe MIGRATION-7.0.0-HOTFIX.md.
