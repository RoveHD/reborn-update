# Eigene Zusatz- und Fixmods mit RE-Namen

Zwölf installierte Mods tragen jetzt verständliche Namen im Stil `RE - Funktion.rb`. Die Ruby-Inhalte sind bytegleich geblieben; ursprüngliche Autorenhinweise sind erhalten. RE kennzeichnet hier deine angepasste Installation, auch wenn eine Datei auf fremdem Code aufbaut.

| Bisher | Neuer Dateiname |
|---|---|
| `mitnahme.rb` | `RE_Mitnahme-Overworld.rb` |
| `randomwildmusic.rb` | `RE_Zufallsmusik-Wildkaempfe.rb` |
| `Battle_Online.rb` | `RE_Zufallsmusik-Onlinekaempfe.rb` |
| `Bag.rb` | `RE_Beutel-Sortierung-und-TM-Infos.rb` |
| `TimeWeather.rb` | `RE_Zeit-und-Wetter-Fixes.rb` |
| `PulseDex.rb` | `RE_PulseDex-Versteckte-Bilder.rb` |
| `smartrepel.rb` | `RE_Begegnungen-Neue-Pokemon.rb` |
| `PokemonFormItems.rb` | `RE_Begleiter-Formen.rb` |
| `Languages.rb` | `RE_Deutsch.rb` |
| `Retired Mega Stone Compatibility.rb` | `RE_Alte-Megasteine-Kompatibilitaet.rb` |
| `zzLocalGermanText.rb` | `RE_Deutsch.rb` |
| `zzPokedexFrontMenu.rb` | `RE_Pokedex-Ansichtsauswahl.rb` |

Die Dateien liegen in `working-game/patch/Mods` und `Ergebnis-0.9.9/patch/Mods`.

## Ladereihenfolge und Installation

`Scripts/ScriptLoader.rb` ordnet die RE-Dateien anhand ihrer bisherigen Dateinamen ein. Dadurch bleiben sämtliche Überschreibungen und Aliase in derselben Reihenfolge; insbesondere werden die deutschen Zusatztexte weiter zuletzt geladen. Der alte und der neue Name derselben Mod dürfen nicht gleichzeitig installiert sein. Der Lader meldet solche Duplikate vor dem Laden der Mods.

Das Ergebnis-Paket enthält den angepassten Lader. Bei manueller Installation den bisherigen Patch-Ordner wie in der Paket-Anleitung vollständig ersetzen und auch `Scripts/ScriptLoader.rb` übernehmen. Nicht nur die neuen RE-Dateien über die alten Mods kopieren. Für Launcher-Releases muss die bestehende Paketierung mit `--previous-game` verwendet werden, damit die alten Dateinamen mit ihren Prüfsummen als entfernt erfasst werden. Die Liste steht zusätzlich in `re-mod-removed-files.json`. Es wurde kein Online-Release veröffentlicht.

## Bestehende Namen und Anpassungen im Hauptcode

SWM-Mods und M44Followers behalten ihre Herkunftsnamen. Offizielle AllGen-Dateien werden nicht allein wegen einzelner eigener Änderungen als eigenständige RE-Mod umbenannt: `Battle_Scene.rb` und `Party.rb` enthalten die UI-Abstände, `Debug.rb` eigene Debug-Anpassungen, `Illegal Mon System.rb` die angepasste Zulassung, `RebornPokegear.rb` die deutsche UI-Integration und `AGMusicPack.rb` eigene Musikzuordnungen. Diese Änderungen bleiben eingebaut. Pokémon-Typen, Attackenlisten, Übersetzungsdaten und Sprites sind ebenfalls keine eigenständigen Ruby-Moddateien.

## Prüfung und Rückweg

Die Spielengine hat alle zwölf umbenannten Mods geladen. Die gesamte effektive Modreihenfolge entspricht exakt dem bisherigen Stand. Acht Kampfbox- und zwei Teamansichten wurden erneut erfolgreich gerendert. Ein separater Ladertest prüft Reihenfolge, späte Text-Overrides, Duplikaterkennung und die Unterstützung ausschließlich alter Dateinamen. Die Modinhalte und die Dateien des Ergebnis-Pakets wurden per SHA-256 verglichen. Live-Spielstände blieben unverändert. Kein vollständiger Gameplay- oder Online-Durchlauf.

Sicherung vor der Umbenennung: `../backups/re-mod-names-20261025`. Für einen Rückweg bei geschlossenem Spiel die zwölf Dateien zurückbenennen und den gesicherten Lader gemeinsam wiederherstellen. Eine weitere Referenz liegt außerhalb der Spielordner unter `../custom-update-reference/re-mod-names-20261025`.

Ein RE-Präfix schützt nicht automatisch vor Updates: der angepasste Lader und die zusammengehörigen Mods müssen in künftige Pakete übernommen werden. Historische Merge-/Einmal-Publish-Skripte und ältere Einzelmod-Archive dokumentieren den damaligen Dateistand; sie nicht ungeprüft erneut auf diese Installation anwenden.


Alle deutschen Textkorrekturen werden in RE_Deutsch.rb gepflegt. Neue Korrekturen dort eintragen.
