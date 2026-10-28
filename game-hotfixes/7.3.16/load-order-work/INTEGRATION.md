# Ladereihenfolge 7.3.16

Nur `Payload/Scripts/ScriptLoader.rb` in das Spielpayload uebernehmen. Der Abschnitt
`BEGIN RE MOD NAMES` bis `END RE MOD NAMES` erweitert den bereits aktiven Mechanismus;
alles ausserhalb dieses Abschnitts ist unveraendert. Keine neue Shim-Infrastruktur.

Die 15 einzeln umbenannten Mods behalten ihren bisherigen virtuellen Originalnamen.
`RE_Box-Markierungen.rb` erhaelt explizit den bisherigen Namen `RE - Box-Markierungen.rb`
als Sortierschluessel, da diese Datei vorher keine Sonderzuordnung hatte.
`zzzzByteonEvolutionHotfix.rb` liegt durch Ruby-Stringsortierung VOR
`zzzz_RE_BattleSound_099_Compatibility.rb` (Grossbuchstabe B vor Unterstrich).
Diese Reihenfolge bleibt erhalten.

`RE_Deutsch.rb` laedt an Position des ehemaligen `zzzzzzGermanItemReview.rb`.
Die sieben Komponenten muessen darin in folgender Reihenfolge stehen:

1. Sprachauswahl (`LANGUAGES`)
2. Deutsche-Zusatztexte (`LocalGermanText`)
3. LocalGermanVisible
4. GermanGrammarAndClub (`ReviewedGermanText`)
5. EnglishStreetNames
6. GermanFloorLabels
7. GermanItemReview

Die gemeinsame spaetere Ladeposition ist eine bewusste Folge der Konsolidierung.
`LANGUAGES` wird in System.rb/Load.rb/Compiler.rb erst in Methoden gelesen.
Der einzige externe LocalGermanVisible-Verweis liegt in der Box-Markierungsanzeige
ebenfalls innerhalb einer Methode. TimeWeather, Pokedex und SWM Glamazon sind
zum Installieren der Deutsch-Prepend-Hooks bereits vollstaendig geladen.
Die sieben Komponenten behalten innerhalb der Sammeldatei ihre alte Hook-Reihenfolge.

Doppelte alte/neue Aliasdateien und ein gleichzeitiges RE_Deutsch mit einem seiner
neun historischen physischen Dateinamen werden vor dem ersten Mod abgefangen.
Kein unbekannter oder modifizierter Altbestand wird hier geloescht oder still uebersprungen.
Die Root-Migration muss historische Dateien anhand bekannter Hashes entfernen.
Beachten: package_release.py erkennt Originalnamen aus den Mappingwerten; die
frueheren `RE - ...` Namen und alle sieben Deutschteile brauchen fuer ihre neuen
Ersatzpfade zusaetzlich die vom Root gepflegten retiredFiles-Eintraege.

## Pruefung

Vom Repository `reborn-update` aus:

```
python ../update-0.9.9-20261025/tools/run_ruby.py -c game-hotfixes/7.3.16/load-order-work/Payload/Scripts/ScriptLoader.rb
python ../update-0.9.9-20261025/tools/run_ruby.py game-hotfixes/7.3.16/load-order-work/check_load_order.rb
```

Ergebnis: `Syntax OK`; `RE_LOAD_ORDER_OK 116 checks; 46 duplicate cases`.
`load-order-checks.json` enthaelt Hashes und die vollstaendige gemessene Reihenfolge.
Die alte aktive Loaderdatei und Namensliste liegen als unveraenderte Baseline hier,
damit die Pruefung nach der Migration weiter reproduzierbar bleibt.
Die vollstaendige neue Loaderdatei wurde mit echten Basis-/Mod-Alias-Fixtures zweimal
ausgefuehrt; Fehlerprotokoll und Alias-Ergebnis wurden geprueft.
Das Spiel und seine grafische Oberflaeche wurden hier nicht gestartet.

Im aktiven Spiel belegt `Scripts/ScriptLoader.rb:101` die Basisphase,
`:108-136` den bisherigen Mapping-/Sortiermechanismus. Die neue Variante liegt hier
mit Mapping bei Zeile 109, Konfliktpruefung bei 142/150 und Sortierung bei 162.
Keine aktiven Spiel-/Base-Dateien wurden in diesem Work Order veraendert.
