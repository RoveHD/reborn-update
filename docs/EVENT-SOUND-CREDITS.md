# Event-Sound-Credits

Alle hier aufgeführten Event-Sounds wurden eigens für den Pokémon-Reborn-Launcher
mit `tools/generate-event-sounds.py` erzeugt. Das Skript verwendet ausschließlich
die Python-Standardbibliothek und synthetisiert mono PCM-WAV-Dateien; es verwendet
weder Aufnahmen noch Samples, Musik, externe Dienste oder fremde Sounddateien.
Es bestehen daher keine Drittanbieter-Credits oder zusätzlichen Lizenzbedingungen
für diese Soundeffekte.

Je Cue liegen zwei deterministisch erzeugte Varianten in
`src/RebornLauncher/assets/audio/events/`. Beide Varianten wechseln bei wiederholter
Wiedergabe ab. Dauer und Klangfarbe sind im Generator festgelegt; Dateien sind
auf maximal 48 % PCM-Peak begrenzt. Diese Cues reagieren auf Interaktionen;
die getrennten Halloween-Ambient-Sounds sind in [HALLOWEEN-AMBIENT-AUDIO.md](HALLOWEEN-AMBIENT-AUDIO.md) dokumentiert.
Die Event-Geräusche sind optional und standardmäßig deaktiviert.

Enthaltene Cues: `snow-crunch`, `snowball-hit`, `snow-poof`, `ice-break`,
`rocket-launch`, `firework-boom`, `sparkle`, `confetti`, `egg-crack`,
`bird-chirp`, `rustle`, `chocolate-snap`, `ghost`, `wood-creak`, `pumpkin-pop`,
`bat-flap`, `cauldron-bubble`, `bell`, `gift-rip`, `glass-break`, `water-pop`,
`leaf-rustle`, `magic`, `thunder`, `wind`.
