# Wetter über dem Launcher

Die transparente Wetterschicht folgt dem vorhandenen jährlichen Saisonkalender und der Tag-/Nachtwahl. Sie liegt über Landschaft und Oberfläche; Maus- und Tastatureingaben gehen durch sie hindurch. Rund um Logo, Startknopf, Status und geöffnete Inhaltsbereiche werden Partikel deutlich abgeschwächt. Navigation und untere Statusleiste bleiben frei.

| Theme | Bewegte Atmosphäre |
|---|---|
| Winter | Schneeflocken in verschiedenen Größen, einzelne drehende Kristalle |
| Frühling / Ostern | Rosa und helle Blütenblätter, frisches grünes Laub |
| Sommer | Seifenblasen, glitzernde Reflexe, Wasserringe; tags warme Sonnenstrahlen, nachts kühle Lichtreflexe |
| Herbst | Kupferrotes, goldenes und rotes Laub mit wechselnder Drehung und Windbewegung |
| Halloween | Aufsteigende Glut, violette Funken und langsamer Nebel an den Rändern |
| Neujahr | Bewegtes Konfetti zusätzlich zum vorhandenen Feuerwerk |
| Arceus | Goldene Lichtstrahlen und pulsierende helle Lichtfunken |
| Standard | Schwebende Lichtpunkte und kleine Sterne |

Die vorhandenen Einstellungen für Event-Dekorationen, Partikelqualität, Intensität und reduzierte Bewegung gelten auch für das Wetter. Ausgeblendete, inaktive oder minimierte Fenster pausieren die Wetteranimation; beim Spielen pausiert sie ebenfalls. Der Timer stoppt beim Schließen. Die Partikelzahl bleibt unabhängig von der Fenstergröße begrenzt.

Alle acht Themes besitzen bereits eigene Musik. Passende Geräusche der Saisonobjekte sind separat einschaltbar und unabhängig von der Musik geregelt.

Prüfung: `tests/WeatherOverlayTests` rendert alle acht Themes bei Tag und Nacht und vergleicht die tatsächliche Bildänderung. Zusätzlich prüft es die Integration bei 960 × 640 und 1280 × 800, den echten Eingabetreffer auf dem Startknopf, freie Navigation/Statusleiste und das Stoppen bei deaktivierten oder ruhigen Effekten. Die acht MP3-Zuordnungen und 50 normalen Effekt-WAVs werden über den stummen WPF-Decoder geöffnet. Ergebnis: `test-results/weather-overlay/result.json`.

Lokale Vorschau: `dist/weather-preview/launcher.exe`. Die acht Nachtansichten lassen sich mit `tools/open-theme-previews.ps1 -Period Night -LauncherPath dist/weather-preview/launcher.exe` starten. Diese Entwicklungsänderung veröffentlicht selbst keinen GitHub-Release und verwendet keine Spieldateien.
