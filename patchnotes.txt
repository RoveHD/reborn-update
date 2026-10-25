# Spielhotfix 7.3.2 - Ballwurf-Absturz

Behebt den Absturz `super: no superclass method pbThrowPokeBall` beim gemeinsamen Einsatz der legendaeren Fangschwierigkeit und SWM Reusable Premier Balls, insbesondere nach erneutem Laden der Spielskripte.

Die Fang-Mod greift jetzt an den Kampfklassen ein, damit SWM weiterhin die Original-Fangroutine speichert. Fangchancen, wiederverwendbare Premierbaelle und Arceus' Reborn-Kugel-Sperre bleiben unveraendert.

Alle Spielkorrekturen aus 7.3.1 sind enthalten. Genau eine Spiel-Datei wurde gegenueber 7.3.1 geaendert. Der Launcher bleibt auf Version 7.3.0.

Spiel vor dem Update vollstaendig schliessen und danach neu starten. Kein Kompilieren erforderlich.

Pruefung: Absturz mit vorheriger Version reproduziert; 132 isolierte Ruby-Pruefungen bestanden (Originalroutine, beide Mod-Ladereihenfolgen, erneutes Laden, Safari, Fangbalance und Ballverbrauch). Kein Test auf dem Rechner des Empfaengers.
