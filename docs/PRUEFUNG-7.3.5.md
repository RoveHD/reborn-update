# Spielhotfix 7.3.5: Reborn-Kugel und mächtige Aura

Der Hotfix ersetzt ausschließlich `patch/Mods/zzLegendaryCatchDifficulty.rb` im geprüften Spielpaket 7.3.2. Das veröffentlichte Launcher-Asset 7.3.4 wird unverändert übernommen; aktuelle UI-Arbeiten sind nicht Bestandteil dieses Spielhotfixes.

## Verhalten

- Die 25 Arten aus `BOX_LEGENDS`, `STRONG_THIRDS` sowie Dialga/Palkia benötigen 20 andere gültige Ballwürfe auf dasselbe Pokémon im selben Kampf.
- Arceus behält 30 Würfe. Gewöhnliche Gruppen wie Arktos/Zapdos/Lavados erhalten keine neue Sperre.
- Die Meldung nennt das richtige Pokémon, seine mächtige Aura, die nötige Erschöpfung und den jeweiligen Zähler.
- Ein abgewiesener Reborn-Ball wird nicht verbraucht und erhöht keinen Zähler. Ein freigeschalteter Fang verbraucht genau einen.
- Normale Fangformel, Wahrscheinlichkeiten, Originalroutine-Prüfsumme und SWM-Hooks bleiben unverändert.

## Nachweise

`game-hotfixes/7.3.5/catch-checks.json` protokolliert 264 erfolgreiche Prüfungen mit dem tatsächlich gebündelten Ruby 3.1 sowie den originalen Battle-, Ball-, Item- und SWM-Methoden aus der D:-Arbeitskopie. Die vier Ladevarianten bestehen mit 65/66/66/67 Prüfungen. Die Szene, Pokémonobjekte und Inventardaten sind isolierte Testobjekte; es wird kein echtes Spiel gestartet und kein echter Spielstand geladen.

`tests/CatchHotfixChecks` verwendet den tatsächlichen `UpdateService` mit dem gebauten Hotfix. Es prüft die Auswahl genau des kleinen ZIPs, Basishashes, Installation, Versionsmarker, den Wechsel zur vorherigen und wieder zur aktuellen Version sowie unveränderte synthetische Spielstände, Einstellungen, Game.exe und fremde Mods. Änderungen an der Ausgangs-Mod werden als ungeeignete Hotfix-Basis erkannt. Bericht: `test-results/catch-hotfix-7.3.5/hotfix-integration.json`. Mit `--public` wird derselbe Test über den echten GitHub-Download ausgeführt; dessen Bericht heißt `public-hotfix-check.json`.

Das vollständige ZIP enthält genau 18.864 Dateien. Der Paketbau prüft jede fertige Archivdatei per SHA-256 gegen das Inventar und bestätigt exakt einen geänderten Pfad. Kleine Hotfix-Datei und vollständiges Paket enthalten identische neue Mod-Bytes. Eine manuelle Kampf- oder gerenderte Textprüfung ist damit nicht ersetzt.

| Artefakt | SHA-256 |
|---|---|
| Fang-Mod | `2adf338088066a3b67eeb1d17b9d1077acbffac7b03cf23395430ffc7a619dd3` |
| Hotfix 7.3.2 → 7.3.5, 4.234 Byte | `143593e4e25d405a3f5be578482bca0646105c3dc24c5ff257bf19cffd487562` |
| Vollständiges Spielpaket | `9e1fa3b885e051db90ef1b0973d17a56024c687ee25094231b6842c0a9110b54` |

Alle Tests liegen unter D:. Der aktuelle Spielordner auf Z: wird nicht verwendet. Der vorhandene Versionswechsel stellt nach dem Update weiterhin die unmittelbar vorherige Spielversion bereit.
