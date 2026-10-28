# Combined German Mod Runtime Comparison

Vergleich der sieben bisherigen Mods in ihrer Loader-Reihenfolge gegen `RE_Deutsch.rb`, jeweils in einem separaten Spielordner aus der unveränderten ZIP-Baseline. Ausgeführt mit der gebündelten Ruby-Runtime über `ruby_runner.py`; weder Game.exe noch Saves wurden geöffnet.

Baseline ZIP SHA-256: `4fea005a1a17f3ee0fb9f6d0cf0e5644c6a72d60dd119498fd491472107dbf1a`  
Combined payload SHA-256: `5218be68bf8fbee078c7c9246e760818264a5845f3e68f2e454eb86dc46c76bc`

| Ausgabe | Einträge | SHA-256 | Gleich |
|---|---:|---|:---:|
| `german.lookups` | 83025 | `5a81f20747accb29a3522543a5057d99a4560ea3c187600899189d822666494e` | Ja |
| `german.items` | 1157 | `74a1e5becd1c6c0f0ac04b4d299047432097510925daf35f98f6f6651a63e70d` | Ja |
| `german.bttrainers` | 351 | `68b8ab9e255e52593c5b311cbec090f87d39c50b77165c04c5b3d308a9cd9e8c` | Ja |
| `english.lookups` | 81929 | `c5384aa759b05ddf4cee63aa55b8e1f266cc47eb1f85b145c9be8deae84aac5c` | Ja |
| `english.items` | 1157 | `4263def4634b5cd113c19c420f8ed403804c4c9cf177828b28c556febf3d3ab7` | Ja |
| `english.bttrainers` | 351 | `68b8ab9e255e52593c5b311cbec090f87d39c50b77165c04c5b3d308a9cd9e8c` | Ja |
| `german_after_switch.lookups` | 83025 | `5a81f20747accb29a3522543a5057d99a4560ea3c187600899189d822666494e` | Ja |
| `german_after_switch.items` | 1157 | `74a1e5becd1c6c0f0ac04b4d299047432097510925daf35f98f6f6651a63e70d` | Ja |
| `german_after_switch.bttrainers` | 351 | `68b8ab9e255e52593c5b311cbec090f87d39c50b77165c04c5b3d308a9cd9e8c` | Ja |

Unterschiede: **0**.

Die Lookup-Snapshots umfassen alle Schlüssel in den geladenen Message-Tabellen und deren tatsächliche Runtime-Lookupwerte; Items umfassen jeden Itemnamen und jede Itembeschreibung; Battle Tower umfasst alle 351 Datensätze. Der Sprachwechsel German → English → German wurde in beiden Läufen ausgeführt. Die zurückgeschalteten deutschen Ausgaben stimmen wieder bytegenau mit der Baseline überein.

Artefakte: `baseline-snapshot.json`, `combined-snapshot.json`, `comparison-report.json`, `differences.json`, `inputs.json`.
