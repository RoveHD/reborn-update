# Halloween ambient audio assets

## Playback controls

Enable Event-Geräusche in „Lebendige Jahreszeiten“ and leave Halloween-Ambient-Sounds enabled to hear the atmosphere during Halloween. Both switches and the common event volume are saved. Default event volume is 12%; ambient cues use a further 45–65% multiplier. The master event switch defaults to off.

Frequency intervals are random: low 5–10 minutes, normal 3–7 minutes, high 2–5 minutes. Each variant has a 15-active-minute cooldown; immediate repeats are excluded. Laughter is the most likely category, special cues the least likely. Only one ambient cue plays at once. Theme changes, minimization, game start and disabling either switch stop the voice. Resuming starts a fresh full interval; sleeping or blocking the UI does not queue a burst of sounds.

## Asset provenance

This folder contains twelve original, locally synthesized background-effect cues for the Halloween theme. They are short PCM WAV files at 22,050 Hz, mono, 16-bit. Vocal-like effects are generated from synthesized harmonics, soft formants, and breath/noise envelopes. They are not speech recordings, licensed recordings, or imitations of a named person or character; the whisper effects contain no intelligible words.

The generator uses only Python's standard library and lives at `tools/generate-halloween-ambient.py`. Running it recreates the files under `src/RebornLauncher/assets/audio/events/` deterministically.

| Cue names | Count | Character |
|---|---:|---|
| `ambient-laugh-01.wav`–`ambient-laugh-04.wav` | 4 | Distinct, gently voiced, uneven ghost-laugh cadences |
| `ambient-giggle-01.wav`–`ambient-giggle-02.wav` | 2 | Quieter, quicker, higher-pitched chuckles |
| `ambient-whisper-01.wav`–`ambient-whisper-02.wav` | 2 | Airy, nonverbal distant whisper textures |
| `ambient-wind-01.wav`–`ambient-wind-02.wav` | 2 | Slow, soft, differently shaped wind swells |
| `ambient-creak-01.wav` | 1 | A restrained, bending wooden groan |
| `ambient-special-01.wav` | 1 | A soft hollow, layered eerie tone |

Each cue lasts 2.7–5.8 seconds. Peak sample levels are capped per sound family from 0.145 to 0.285 full scale, below the requested 0.35 ceiling. Every file has a gradual attack/release and was checked for duration, sample format, peak level, non-silence, quiet start/end edges, and a unique content fingerprint. This validates the audio files themselves; playback cadence, launcher focus/minimize handling, volume controls, and event-sound opt-out are implemented and checked separately by the launcher integration.
