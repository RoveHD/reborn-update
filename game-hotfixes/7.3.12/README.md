# Spielhotfix 7.3.12

Basis: unverändertes öffentliches Paket 7.3.11 / AllGen 0.9.9. Nur die drei Dateien in `approved-files.json` sind für die neue Ausgabe vorgesehen:

- `Graphics/Characters/Followers/722.png`: fertiger überarbeiteter Bauz-Begleiter.
- `Graphics/Characters/Followers/887s.png`: fehlender Shiny-Katapultdra-Begleiter.
- `patch/Mods/zzLegendaryWildMusic.rb`: additive Auswahl der vorhandenen Boss-Musik in wilden Legendärkämpfen.

## Grafikabgleich

Ausschließlich `Characters/Finished` wurde als Quelle zugelassen. 59 Bilder geprüft, 57 bereits enthalten. Die alten Quelldateien 1036/1036s gehören zu Abysara (AllGen 1037), 1037/1037s zu Divinara (AllGen 1038); sie wurden anhand der aktuellen Artendaten und bytegleicher vorhandener Sprites zugeordnet. Sie werden nicht unter den alten Nummern installiert. Byteon 1036 wird dadurch nicht überschrieben.

Die acht vorhandenen Grafikdateien zu Maxax einschließlich Mega-/Shiny-Battlern bleiben unverändert. Sämtliche sonstigen Grafiken und Spielskripte aus 7.3.11 bleiben erhalten. Das unfertige Altaria außerhalb von Finished und alle Krita-Dateien sind ausgeschlossen.

`allgen-form-preservation.json` bestätigt außerdem den pixelgleichen Erhalt der neuen Mega-/Shiny-Zeilen in Maxax' Kampfgrafik und Icon gegenüber dem originalen AllGen-0.9.9-Patch. Die bereits angepassten Normalform-Zeilen bleiben bestehen.

`finished-graphics-audit.json` dokumentiert jeden Vergleich. `follower-selection-checks.json` prüft die echten Begleiter-Auswahlmethoden mit dem Paketinventar als Ersatz für die Bitmap-Auflösung der Engine. Die Auswahl von Bauz, Shiny-Katapultdra, Abysara, Divinara und Maxax einschließlich Mega-Form wurde geprüft. Kein Spielstart oder visueller Ingame-Test.

## Build und Installation

`tools/prepare_music_graphics_hotfix.py` prüft die eingefrorene öffentliche ZIP-Basis und ihr Inventar, ergänzt ausschließlich freigegebene Dateien und prüft jede Datei des neuen Archivs erneut. Der aktuelle Launcher und die historischen hashgesicherten Dateientfernungen bleiben gleich. Ein neuer Ersatzdatei-Deltaeintrag wird nicht angeboten, weil Launcher 7.3.8 diesen Mechanismus nur für bereits vorhandene Dateien unterstützt.

Der Launcher verwendet daher den vollständigen Paketdownload. Das kleine Zusatz-ZIP ist alternativ für die manuelle Ergänzung einer gesicherten 7.3.11-Installation vorgesehen; es setzt keine Paketversion und erzeugt keine Launcher-Rückwechselsicherung.

Integrationstests laufen ausschließlich in eigens erzeugten D:-Testordnern mit künstlichen Spielständen, Einstellungen und einer fremden Beispiel-Mod. Der aktive persönliche Spielordner auf Z: wird weder getestet noch verändert.

## Veröffentlichung und Abschlussprüfung

[Release 7.3.12](https://github.com/RoveHD/reborn-update/releases/tag/v7.3.12) ist veröffentlicht. Der normale Updatekanal liefert Spiel 7.3.12 und unverändert Launcher 7.3.8.

- 447 Musikprüfungen für 70 Arten, 11 Prüfungen der tatsächlichen Begleiter-Auswahlmethoden.
- Alle 18.866 Paketdateien geprüft; nur die drei freigegebenen Pfade unterscheiden sich von 7.3.11.
- Migration mit vier Versionswechseln, Reparatur einschließlich Erhalt unveränderter Dateizeitstempel und vollständiger Updater-Test mit lokalen Paketdaten bestanden.
- Abschließend das öffentliche GitHub-Manifest und komplette Paket mit dem echten Updater heruntergeladen, installiert, vollständig gehasht und zweimal zwischen den Versionen gewechselt. Neue Dateien verschwinden beim Rückwechsel und kehren beim Vorwechsel zurück. Persönliche Testdateien bleiben erhalten.
- Der GitHub-Build reproduziert exakt das lokal geprüfte ZIP: `46d25df0aed1bf8c7c30773e753cdff6b97fe4e7dd6554052d1e8bd4e3d0d955`.

Die Nachweise stehen unter `Pruefprotokolle`. Keine Spielinstanz wurde gestartet; hörbare Musikwiedergabe und Darstellung im laufenden Spiel wurden nicht manuell geprüft.
