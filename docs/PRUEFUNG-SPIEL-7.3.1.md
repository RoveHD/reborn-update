# Prüfung des Spielhotfixes 7.3.1

## Geprüftes Paket

- Spielpaket: AllGen 0.9.9, Updateversion 7.3.1.
- Datei: `Reborn-AllGen-0.9.9.zip`, 1,476,583,417 Bytes.
- SHA-256: `1bc86c047f6c2b7a96e12f5478811e6bf714e6ad3780c14db3f0306850365c97`.
- 18.864 Archivdateien vollständig gelesen und gegen SHA-256-Inventar geprüft.
- Genau neun geänderte Pfade gegenüber der veröffentlichten Basis 7.0.0; keine zusätzliche aktuelle Datei entfernt.
- Der Launcher-Manifestabschnitt bleibt unverändert bei 7.3.0. Laufende Launcher-UI-Arbeiten sind nicht enthalten.

## Funktions- und Installationstests

- Teammenü: 148 isolierte Ruby-Prüfungen auch auf dem finalen Party-Code.
- Street-Namen: 195 Prüfungen; 24 gezielte Korrekturen in den kompilierten Daten, alle anderen kompilierten Texte und Schlüssel unverändert. 182 Street-Abfragen geprüft.
- Battle Sound Effects v1.1: gemeldeten ArgumentError mit der lokal vorhandenen alten Methode reproduziert; 13 Prüfungen für Adapter, Sounds, Schaden, Delegation und Fehlerweitergabe bestanden.
- Geschlechtssymbole: zwölf Kampf- und zwei Teamvarianten im echten isolierten Engine-Renderer geprüft; Grenzen, Überlappungen, Illusion und geschlechtslose Pokémon berücksichtigt.
- Bestehende Engine-Renderprüfungen der Markierungen, Pokédex-Labels und TM-Infos: bestanden, getestete Dateien unverändert übernommen.
- Vollständige Neuinstallation des vorherigen, noch unveröffentlichten Paketstands ohne Geschlechtssymbol-Zusatz: bestanden, 18.864 Dateien geprüft; Archiv-SHA `a7de84301fcaafaac4cf0bbf246fda92da2ca3857ef6363ccf971ae29a69bb48`.
- Anschließendes Update und Reparatur genau des oben genannten endgültigen Archivs auf dieser isolierten Installation: bestanden, erneut alle 18.864 Dateien geprüft (65.2 Sekunden). Eine absichtlich beschädigte Datei wurde repariert; die unveränderte Game.exe wurde nicht neu geschrieben.
- Testspielstand, Launcher-/Anzeigeeinstellungen und eigener Zusatzmod blieben unverändert.

Berichte: `dist/release-game-7.3.1-release/package-checks.json`, `hotfix-checks.json`, `release-repair.json`; lokale Funktionsberichte im übergeordneten Spiel-Arbeitsbereich. Der erste Neuinstallationstest und der nachfolgende Update-/Reparaturtest haben absichtlich unterschiedliche Archiv-Hashes, da der Geschlechtssymbol-Fix anschließend hinzukam. Eine zweite vollständige Neuinstallation des finalen Archivs wurde nicht durchgeführt.

## Grenzen

Keine vollständige Spiel-Durchspielprüfung und kein Kampf auf dem Rechner des Empfängers. Der Sound-Adapter behebt den konkreten Signaturkonflikt; er portiert nicht alle alten Kampfmethoden der Sound-Mod. Zusätzliche fremde Dateien bleiben erhalten, vorhandene abweichende Paketdateien werden nach dem bestehenden Updaterverfahren gesichert und ersetzt.

## Veröffentlichung

Am 25.10.2026 veröffentlicht: https://github.com/RoveHD/reborn-update/releases/tag/v7.3.1

Alle acht Release-Assets wurden vor Freischaltung anhand der von GitHub gemeldeten Größe und SHA-256-Prüfsumme gegen die lokalen Dateien geprüft. Der Release ist öffentlich, kein Entwurf und kein Prerelease. Die Aktivierung erfolgte mit Commit `d04fd3d` erst nach erfolgreicher Assetprüfung. Anschließend wurde auch der öffentliche Raw-Updatekanal direkt abgerufen: Spielversion 7.3.1 mit exakt der oben genannten Prüfsumme, Launcher unverändert 7.3.0. Der Legacy-Kanal blieb unverändert. Der Hotfix ist absichtlich nicht als neuester Launcher-Download markiert.
