# Lokale Prüfung der bewohnten Anime-Szenen

Stand 25.10.2026. Lokale Vorschau: `dist/inhabited-anime/Vorschau.cmd`; ausführbare Datei `dist/inhabited-anime/launcher.exe`. Diese erste Prüfung war lokal. Die Release-Prüfung für 7.3.4 ist unten dokumentiert.

- Acht erweiterte Anime-Landschaften mit festen Pokémon eingebunden; Originale erhalten. Separate transparente Vordergründe und Wetterebenen erzeugen 2,5D-Parallaxe.
- ReferenceLayoutTests: 8 Themes × 2 Lichtzustände × 4 Fenstergrößen (960×640, 1280×800, 1920×640, 1920×1080). Alle vier Kameraecken prüfen echte Bildbounds und WPF-Layout-Clips; Logo und Startbutton bleiben unbewegt. Dialoge, Downloadabbruch und Motion-Gates bestehen. Ergebnis: `test-results/outpaint-final/result.json`.
- Tatsächliche WPF-Darstellung aller acht Themes und des Kamera-Eckzustands visuell kontrolliert. Die Canvas-Hosts verhindern die automatische Beschneidung übergroßer Bilder vor dem RenderTransform. Kein unscharfer Seitenfüller.
- Veröffentlichungsfähige lokale Single-File-EXE gebaut; ihr kompletter `--smoke-test` bestand inklusive kompakter und maximierter Szenen, Start-/Versions-/Downloadsteuerung, Dialogen, Eventinteraktionen und stummen Audiocodec-Prüfungen. Ergebnis: `test-results/outpaint-exe-final/result.json`.
- Weitere bestandene Prüfungen während der Integration: CoreTests (52), CenteredLayoutTests, PokemonSelectionTests (feste Szenen), GengarUiTests, BloodEasterEggTests, WeatherOverlayTests, SceneDecorationTests. Lichtkontrast und kontinuierlicher Tag-/Nachtwechsel: `test-results/inhabited-lighting-final/result.json` (vor reiner Randerweiterung und Canvas-Anordnung).

Grenzen: Gemalte Pokémon haben keine eigenständige Blink-/Pose-Animation. Extrem breite Fenster verwenden einen vertikalen Ausschnitt der Landschaft. Kein manueller Hörtest, kein Mehrmonitor-DPI-Wechsel durchgeführt. Alte Vorschauen und Originalbilder bleiben erhalten.


## Release-Prüfung 7.3.4 am 26.10.2026

Die Release-EXE und ihr Installer bestanden Installation, erneute Installation, Selbstupdate mit Neustart und Deinstallation in einem isolierten Testverzeichnis. Einstellungen und Spiel-Sentinels blieben unverändert. Dazu bestanden 52 CoreTests und 23 HotfixTests. ReferenceLayoutTests prüften erneut alle acht Themes bei zwei Lichtzuständen und vier Fenstergrößen, einschließlich der vier Kameraecken und stabiler UI. FireworkUiTests sowie Lichtwechsel und reduzierte Bewegung bestanden.

EXE SHA256: `d9207936bd589663e158e76acd1fbf5d74d67dcb9269f93dc0409f7e9bd96c80`. Setup SHA256: `ec0e378f0ee288ac598c4c397e328f5425f8b0b0986f07a4bcbdee87b10e30ec`. Prüfergebnisse werden als `checks.json` im Release mitgeliefert.
