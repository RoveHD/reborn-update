# Einstellungen und Lichtmenü – 26.10.2026

## Änderung

- Die fest eingebauten blauen Ressourcen im Navigationspanel sind entfernt. Panel, Kopf, Fuß und Trennlinien folgen den Saisonfarben und den bestehenden weichen Tag-/Nacht-Übergängen.
- Das Lichtmenü verwendet eine vollständige eigene Vorlage ohne den weißen Windows-Systemstreifen. Alle drei Optionen haben 16-Pixel-Schrift und mindestens 44 Pixel Bedienhöhe; die aktive Option ist mit einem Häkchen markiert.
- Einheitliche 16-Pixel-Schrift für Einstellungsbeschriftungen, Schalter und Aktionsbuttons; kompakte, thematisch gefärbte Buttons. Regler und Scrollleisten verwenden ebenfalls die Saisonfarben.
- „Lebendige Jahreszeiten“ ist breiter und übersichtlicher. Der Inhalt scrollt; Titel, Schließen, Abbrechen und Übernehmen bleiben sichtbar. Abbrechen verändert die Einstellungen weiterhin nicht.

## Ausgeführte Prüfungen

- `SettingsUiTests`: 32 Kombinationen aus 8 Themes, Tag/Nacht und 2 Fenstergrößen erfolgreich. Farben, Schrift-/Bediengrößen, Scrollen und die drei Lichtoptionen geprüft. Die gerenderten Popup-Pixel enthalten keinen weißen Systemstreifen. Dialog bei 540/850 Pixel Höhe sowie Übernehmen/Abbrechen geprüft. Bericht: `test-results/settings-ui-final/result.json`.
- `CenteredLayoutTests`: Hauptansicht bei vier Größen, alle acht Themes, Props als Fenster-Klickziele, Fledermaus-Rückkehr, permanente Dekoration, Navigation, Scrollposition, Escape, Unterdialog und Downloadabbruch erfolgreich. Bericht: `test-results/settings-navigation-final/result.json`.
- Finale Vorschau-EXE: acht Lichtpaletten, kontinuierliche Übergänge, Richtungswechsel, reduzierte Bewegung und Artwork-Blende erfolgreich. Bericht: `test-results/settings-lighting-final/result.json`.

Die finalen Prüfungen und der Vorschau-Build verwenden einen isolierten Snapshot aus dem letzten Git-Stand plus ausschließlich den vier Einstellungsdateien und dem neuen Settings-Test. So enthalten sie nicht die zeitgleich in einem anderen Chat bearbeiteten sieben Landschafts-Themes. Der vollständige gemeinsame Theme-Umbau wird dadurch nicht als geprüft ausgewiesen.

Lokale Vorschau: `dist/settings-preview/Vorschau-Halloween.cmd` startet den vorhandenen Vorschau-Modus; anschließend „Einstellungen“ wählen. Keine Installation, kein Release, keine Spieländerung.

SHA-256 des ursprünglich mit der Lichtprüfung geprüften Settings-Builds: `b4da4705c5c83513fc333b0355701db025257100c85774452dd283fb4a2444a2`.

Die lokale Vorschau wurde anschließend um den separat geprüften Status-Fix ergänzt: konkrete Versions-/Prüfmeldungen, Entfernen alter Fortschrittsanzeigen und Ignorieren verspäteter Dispatcher-Berichte nach Operationsende. `StatusUiTests` und die vollständige `CenteredLayoutTests`-Prüfung wurden dafür erfolgreich ausgeführt (`test-results/status-ui/result.json`, `test-results/status-navigation/result.json`). SHA-256 der aktualisierten Vorschau-EXE: `6b3f3989d22a6c2ecc38164acef9e558c4f0ec8abcbaef005a0856d1e7c65431`.
