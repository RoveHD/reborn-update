# Eigene Legendär-Themen im Fangkampf

Diese additive Mod lässt 70 legendäre und mysteriöse Arten im wilden Fangkampf dieselbe Musik verwenden, die Reborn/AllGen für ihren Bosskampf auswählt. Beispiele: Dialga/Palkia, Groudon/Kyogre/Rayquaza, Giratina, die Regis, die Schutzpatrone, Zacian/Zamazenta, Koraidon/Miraidon und Arceus.

Die Auswahl wird direkt an `pbGetTrainerBattleBGM` delegiert. Dadurch bleibt AllGens vorhandene Musikeinstellung (Schalter 3074) maßgeblich: Mit Musikpaket etwa `CLBattle- Dialga-Palkia`, ohne Musikpaket das entsprechende ursprüngliche Boss-Thema. Trainerkämpfe werden durch diese Mod nicht geändert. Formen derselben Art verwenden ihre gemeinsame Musik; die zugrunde liegende Wildkampf-Funktion erhält nur die Art.

Regieleki/Regidrago verwenden das vorhandene Regis-Thema, Enamorus das Thema der Naturkräfte. Die tatsächlichen AllGen-Bossgruppen werden auch für Zacian/Zamazenta/Zarude, die Schätze des Unheils und die Gefährten übernommen. Es werden keine Audiodateien heruntergeladen oder dupliziert.

Ein explizit gesetztes `nextBattleBGM` hat weiterhin Vorrang, einschließlich Lautstärke und Tonhöhe. Fehlt eine Zuordnung, die Trainerdefinition oder die Audiodatei, greift die ursprüngliche Wildmusik. Beispielsweise behalten Arktos, Zapdos und Lavados mangels eigener Zuordnung ihre bisherige Musik. Die Custom-Evoli-Entwicklungen bekommen keine neue Legendär-Zuordnung.

## Einsetzen und Entfernen

Spiel schließen und den enthaltenen Ordner `patch` in den gewünschten Spielordner kopieren. Hinzu kommt ausschließlich `patch/Mods/zzLegendaryWildMusic.rb`. Die Datei muss zusammen mit den anderen `.rb`-Mods in `patch/Mods` liegen. Eine bereits vorhandene gleichnamige Datei vorher außerhalb des Spielordners sichern. Keine zweite Kopie mit anderem Dateinamen im Mods-Ordner ablegen.

Zum Entfernen bei geschlossenem Spiel nur `zzLegendaryWildMusic.rb` entfernen. Spielstände, Begegnungen und Audiodateien werden nicht verändert. Nach einem AllGen-Update, das den gesamten Mods-Ordner ersetzt, muss diese Zusatzdatei erneut eingesetzt werden.

Der lokale Entwicklungsstand liegt unter `custom-mods/legendary-music`. Die Mod wurde in die eigene D:-Arbeitskopie und das Ergebnis-Paket übernommen und ist Bestandteil des Spielhotfixes 7.3.12. Keine Installation im persönlichen Spielordner auf Z:.

## Prüfung am 27.10.2026

- **447 Prüfungen bestanden**, mit den tatsächlichen Wild-/Trainer-Musikfunktionen, Audio-Parsern, Trainerdaten und vorhandenen Musikdateien aus AllGen 0.9.9.
- Alle 70 Zuordnungen mit Musikpaket an und aus; konkrete Referenzthemen für Dialga, Groudon, Giratina, Arceus und Zacian.
- Normale Wildkämpfe, explizite Eventmusik samt Lautstärke/Tonhöhe, fehlende Audiodateien, fehlende Trainerdaten, F12-Neuladen und spätere Neudefinition des Basiscodes geprüft.
- 915 effektive Karten aus Basisdaten und Patch-Overlays gelesen. Die direkten Dialga-/Giratina-Fangaufrufe nutzen `pbGetWildBattleBGM` ohne eigenen Musiksetter auf derselben Ereignisseite. Der Kartenaudit ist eine statische Prüfung, keine vollständige Ausführung aller Eventverzweigungen.
- Keine laufende Spielinstanz gestartet; tatsächliche hörbare Wiedergabe wurde nicht geprüft.

Reproduzierbarer Test aus `reborn-update`:

```powershell
python ../update-0.9.9-20261025/tools/run_ruby.py tests/legendary_wild_music.rb ../update-0.9.9-20261025/working-game custom-mods/legendary-music/patch/Mods/zzLegendaryWildMusic.rb custom-mods/legendary-music/PRUEFUNG.json
```

Die technischen Ergebnisse und der Quellenhash stehen in `PRUEFUNG.json`; die reine Kartenanalyse steht in `EVENT-AUDIT.json`.
