# Silvester-Feuerwerk: Darstellung und Leistungsprüfung

Lokaler Entwicklungsstand, 26.10.2026. Keine Veröffentlichung und keine Änderung an einer Spielinstallation.

Die Show enthält 14 Formen, sichtbare Raketenflüge, Farbwechsel, räumlich versetzte Tochterladungen und weitere Folgeexplosionen. Das harte Partikelbudget zählt auch noch nicht gezündete Ladungen. Verdeckte, inaktive oder minimierte Fenster sowie reduzierte Bewegung und ausgeschaltete Dekoration stoppen die Show und leeren ihre Zustände. Der Himmel liegt hinter Vordergrundzweigen, Text und Bedienelementen und fängt keine Eingaben ab.

## Ursache des Hängens

Der erste Renderer erzeugte pro Stern mehrere `PushOpacity`-Zeichenebenen, einen radialen Verlauf und bis zu drei neu gebaute Spur-Geometrien. Das war im WPF-Software-Rasterpfad extrem teuer. Ein begrenzter isolierter Vorher-Test erreichte bei 1280×800 und maximal 432 sichtbaren Sternen einen Median von **4691,9 ms** und P95 von **7884,1 ms** pro Bild (24 Bilder). Der anschließende größere Vorher-Fall wurde abgebrochen; dafür liegt kein Ergebnis vor.

Zusätzlich exportierte der erste Launcher-UI-Test 180 ganze PNG-Bilder synchron auf dem UI-Thread. Dieser Test wurde beendet und umgebaut: nur zwei Screenshots, begrenzte Eingabe-Callbacks und keine PNG-Schleife pro Bild.

## Korrektur

- Transparenz steckt in eingefrorenen, wiederverwendeten Pinseln statt in einzelnen Opacity-Ebenen.
- Sternleuchten verwenden leichte transparente Farbkreise, aufwendige Verläufe nur die begrenzten Explosionslichter.
- Eine Spur-Geometrie pro Stern erhält die Formen von Weiden, Palmen und Folgeladungen.
- Der 30-Hz-Effekttimer läuft mit Dispatcher-Priorität `Background`, damit Eingaben Vorrang haben.
- Die Teilchenzahl wurde zur Behebung nicht herabgesetzt; normale Qualität erlaubt 480, hohe 960 einschließlich Reservierungen.

## Tatsächlich ausgeführte Prüfungen

`dotnet run --project tests/FireworkTests/FireworkTests.csproj`: **15/15 bestanden**. Dazu gehören alle Formen, zwei Folgestufen, harte Budgets, lange Pausen, Größenänderung und Abschaltzustände.

`dotnet run --project tests/FireworkRenderTests/FireworkRenderTests.csproj -- test-results/firework-render-optimized`: **360 Software-Rasterbilder**, keine nativen Fenster, kein Live-Timer, pro Fall 36 simulierte Sekunden. Ergebnisse in `test-results/firework-render-optimized/result.json`:

| Budget | Bildgröße | Sichtbare Sterne max. | Median | P95 |
| --- | --- | --- | --- | --- |
| 480 | 1280×800 | 472 | 10,18 ms | 15,22 ms |
| 480 | 2560×1600 | 472 | 14,88 ms | 21,99 ms |
| 960 | 1280×800 | 722 | 9,98 ms | 16,14 ms |
| 960 | 2560×1600 | 722 | 15,72 ms | 22,87 ms |

Diese Werte messen den isolierten Feuerwerksrenderer auf diesem Rechner. Sie sind keine zugesicherte Bildrate für den gesamten Launcher oder andere Rechner.

Der native `FireworkUiTests`-Lauf mit dem gemeinsamen aktuellen Launcher-Source endete nach **4,4 Sekunden erfolgreich**. Er zeigte bis zu 635 sichtbare Sterne, 74 Explosionen und 50 Folgeexplosionen. Alle 12 Dispatcher-Eingabe-Callbacks wurden in höchstens **1,56 ms** bearbeitet. Zusätzlich bestanden Klickdurchleitung zum Startknopf, Ebenenreihenfolge, Abschalten per Einstellungen, Themewechsel, kompakte Tagesansicht und vollständiges Stoppen beim Verbergen. Beleg: `test-results/fireworks-optimized-ui/result.json`. Das Testfenster wurde geschlossen; es bleibt kein Prüfprozess offen.

Alle 14 Formen wurden nach der Optimierung noch einmal ohne natives Fenster gerendert: `test-results/fireworks-optimized-showcase/showcase.json` (28 Folgeexplosionen).

## Lokale Vorschau

`Vorschau-Silvester.cmd` startet ausschließlich die lokale EXE `dist/fireworks-optimized-preview/launcher.exe` mit Silvester/Nacht-Vorschaumodus. Der Release-Publish war erfolgreich. SHA256 der EXE: `58E7B2BBA053A1C1DE030E7EE8DE38BF316B18E4CA4CDBFFD2BEC514528D0CE4`. Die installierte Launcher-Version und der GitHub-Updatekanal wurden nicht verändert.
