# Historische deutsche Zusatztexte: Manifest-Hotfix für 7.0.0

Stand: 25. Oktober 2026. Lokal umgesetzt und geprüft; diese Änderung veröffentlicht selbst nichts.

Der Installer meldete einen Konflikt zwischen `patch/Mods/zzLocalGermanText.rb` und
`patch/Mods/RE - Deutsche-Zusatztexte.rb`, wenn eine eigene historische Zwischenfassung
der Übersetzung installiert war. Im Manifest waren nur zwei andere bekannte Fassungen
freigegeben. Aus dem gemeldeten Screenshot geht der tatsächlich betroffene Dateihash
nicht hervor; eine Übereinstimmung mit einer der folgenden Fassungen ist deshalb
nicht als am Nutzer-PC bestätigt anzusehen.

## Änderung und Provenienz

`update.json` erhält ausschließlich drei zusätzliche `game.removedFiles`-Einträge,
jeweils mit obigem Altpfad und `replacedBy` auf die umbenannte Zusatzdatei. Alle übrigen
Manifestwerte, insbesondere Launcher-/Spielversion 7.0.0, Download-URLs, Dateigrößen,
Assetprüfsummen und Patchnotes, bleiben unverändert.

Alle folgenden Pfade beziehen sich auf den übergeordneten Arbeitsordner
`D:\Dokumente\ChatGPT\Pokemon reborn`:

| SHA-256 | Größe | Nachgewiesene Quelle |
| --- | ---: | --- |
| `a920efdcb90e4d9a5fa9db96f67a84ac40869e04429dddb51e264db42d941cb3` | 43.653 Bytes | `Deutsch-Ergaenzung/Sicherungen/20261025-124122-9816/zzLocalGermanText.rb`; identisch im vorläufigen `reborn-update/dist/release-7.0.0/Reborn-19.5.0.zip`, Eintrag `patch/Mods/zzLocalGermanText.rb`, und in dessen damaliger vollständiger Testinstallation |
| `2354f69c75352cea944da88f2dcfa1f409b74bd3647d57b877ce66260e6cf888` | 43.664 Bytes | `Deutsch-Ergaenzung/Sicherungen/20261025-102537-9692/zzLocalGermanText.rb` |
| `402d0f35c75bf4019cfbcfd1c3749e81913162a3023f51a92a8350a9fe556f0c` | 372.349 Bytes | `Deutsch-Ergaenzung/Sicherungen/20261025-140146-4469/zzLocalGermanText.rb`; identisch in `layout-before-20261025/zzLocalGermanText.rb` und dessen `Deutsch-Ergaenzung-v2.zip` |

Das vorläufige Spielarchiv wurde erneut als
`ae70767ce951b251ef03c10fc9c0762d5d3d8405183e8a4771a35fbe16bae885` verifiziert.
Dies stimmt mit `test-results/full-package/release-install.json` überein. Das separat
geprüfte veröffentlichte 6.1.0-Archiv unter `artifacts/previous-6.1.0/` hat SHA-256
`b73375fd22f874982fd092e823a63aed39858e553c9c48347be4112b7b1a9bde` und enthält
keine `zzLocalGermanText.rb`; es allein deckt diese Zwischenfassungen nicht ab.

Der Inhaltsvergleich belegt die gemeinsame Herkunft als `LocalGermanText`:
`2354…` und `a920…` unterscheiden sich nur durch zwei Menüübersetzungen. Gegenüber
dem bereits akzeptierten `010a86…` enthält `a920…` zusätzlich die beiden Einträge
für `HT` und `WT`. Gegenüber `402d…` ergänzt die bereits akzeptierte aktuelle
Fassung `a3ecfc…` ausschließlich Texte innerhalb von `MAP_REPAIRS`.

`tools/known-retired-files.json` hält exakt diese drei geprüften Hashes einschließlich
Quellpfaden fest. `tools/package_release.py` berücksichtigt sie bei jedem neuen
Paketbau, auch wenn historische lokale Kopien nicht mehr vorhanden sind. Pfade und
SHA-256 werden geprüft; Selbstersatz und geschützte Pfade werden abgewiesen. Ein
Eintrag wird nur übernommen, wenn der Ersatzpfad im neuen Inventar vorhanden ist
und der Altpfad dort fehlt. Doppelte historische Hashes werden zusammengeführt.
Provenienzpfade sind Dokumentation und werden beim Paketbau nicht geöffnet.

Der Schutz für unbekannte Bytes bleibt bestehen. Der Installer wurde nicht verändert:
Er akzeptiert weiterhin nur exakte bekannte Hashes, sichert die Altdatei vor dem
Entfernen und prüft sie vor dem Schreiben erneut. Es gibt keine Freigabe anhand
von Dateiname, Kommentar oder vermeintlich ähnlichem Inhalt.

## Ausgeführte Prüfungen

- Acht Python-Pakettests erfolgreich: bisherige Paket-/Privatdatenprüfungen plus
  Registerübernahme ohne historische Quellen, Ersatz vorhanden/fehlend, weiterhin
  enthaltenem Altpfad, Duplikaten, ungültigen Pfaden/Hashes und Selbstersatz.
- Fokusintegration mit dem tatsächlichen unveränderten `GameInstaller` und den drei
  oben genannten Originaldateien aus D:. Neue deutsche Mod aus der freigegebenen
  Spielquelle; deren Hash wurde gegen `dist/release-7.0.0/game-files.json` geprüft.
- Für jede der drei Fassungen: Altdatei entfernt, neue Mod bytegleich installiert,
  dauerhafte Sicherung bytegleich geprüft, anschließend vier Versionswechsel mit
  bytegleicher Wiederherstellung der jeweiligen Altdatei und der neuen Mod.
- Zusätzliche unbekannte Fassung durch Änderung einer Testkopie: Konflikt vor jeder
  Spieldateiänderung, null Installationsschreiboperationen, Originalbytes und alte
  Version erhalten; kein verbliebenes Transaktionsprotokoll oder temporärer Stage.
- Test-Spielstand, Launcher-/Anzeigeeinstellungen und eine zusätzliche eigene Mod
  in allen Szenarien unverändert. Historische Quelldateien unverändert.
- Manifest semantisch gegen Git-HEAD verglichen: ausschließlich drei ergänzte
  Löschfreigaben; alle übrigen Werte identisch.

Befehle aus dem Repository:

```powershell
python -m unittest discover -s tests -p test_package.py -v
dotnet run --project artifacts/german-migration-hotfix/Integration.csproj --configuration Release -- "D:\Dokumente\ChatGPT\Pokemon reborn\reborn-update" "D:\Dokumente\ChatGPT\Pokemon reborn\reborn-update\test-results\german-migration-hotfix-20261025"
```

Der Integrationslauf erwartet ein neues Ausgabeverzeichnis; bei Wiederholung ist
ein neuer Verzeichnisname zu wählen. Harness und Testkopien liegen ausschließlich
in den ignorierten Ordnern `artifacts/` beziehungsweise `test-results/`.
Das konkrete Ergebnis steht in
`test-results/german-migration-hotfix-20261025/german-migration-hotfix.json`.

Die Tests starten das Spiel nicht. Kein vollständiger Paketneubau und kein erneuter
Test aller Spieldateien; geprüft wurde gezielt die durch die drei Freigaben geänderte
Migration. Kein Zugriff auf Z: oder eine aktuelle C:-Spielinstallation. Eine tatsächlich
unbekannte eigene Mod wird weiterhin blockiert und muss vor einem Update bewusst
außerhalb des Spielordners gesichert werden.
