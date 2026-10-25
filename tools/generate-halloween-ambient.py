#!/usr/bin/env python3
"""Synthesize gentle, original Halloween ambience as small PCM WAV cues.

Only Python's standard library is used. The vocal-like sounds are synthesized;
they are not recordings or imitations of a named person or character.
"""
from __future__ import annotations

import hashlib
import math
import random
import struct
import wave
from pathlib import Path

ROOT = (Path(__file__).resolve().parents[1] / "src" / "RebornLauncher" /
        "assets" / "audio" / "events")
RATE = 22050
TAU = math.tau

# Cue, duration, synthesis family, seed. Separate seeds and family parameters
# make each laugh/whisper/wind cue audibly distinct instead of simple pitch shifts.
CUES = (
    ("ambient-laugh-01", 4.8, "laugh", 101),
    ("ambient-laugh-02", 5.2, "laugh", 207),
    ("ambient-laugh-03", 4.4, "laugh", 319),
    ("ambient-laugh-04", 5.6, "laugh", 431),
    ("ambient-giggle-01", 3.1, "giggle", 541),
    ("ambient-giggle-02", 3.7, "giggle", 653),
    ("ambient-whisper-01", 4.5, "whisper", 761),
    ("ambient-whisper-02", 5.1, "whisper", 877),
    ("ambient-wind-01", 5.8, "wind", 983),
    ("ambient-wind-02", 4.9, "wind", 1091),
    ("ambient-creak-01", 2.7, "creak", 1201),
    ("ambient-special-01", 3.4, "special", 1303),
)


def smoothstep(x: float) -> float:
    x = max(0.0, min(1.0, x))
    return x * x * (3.0 - 2.0 * x)


def envelope(t: float, duration: float, attack: float = 0.38,
             release: float = 0.72) -> float:
    return smoothstep(t / attack) * smoothstep((duration - t) / release)


def pulse_train(t: float, centers: list[float], width: float) -> float:
    """Soft syllable-like ha pulses, with no sharp transient."""
    value = 0.0
    for center in centers:
        phase = (t - center) / width
        if abs(phase) < 4:
            value += math.exp(-phase * phase * 1.15)
    return min(1.0, value)


def vocal(t: float, family: str, variant: int, phase: float) -> float:
    # Use modulated harmonics plus broad formant bands to suggest a soft voice.
    # No intelligible words, sampled speech, or abrupt attack is present.
    if family == "laugh":
        base = (172, 214, 151, 238)[variant] + 12 * math.sin(TAU * (0.13 + variant*.017) * t)
        cadence = ((.50,.82,1.18,1.56,2.06,2.51,3.02,3.55,4.08),
                   (.42,.70,1.03,1.44,1.88,2.38,2.92,3.46,4.07,4.62),
                   (.58,.97,1.34,1.92,2.31,2.88,3.47),
                   (.45,.78,1.16,1.65,2.12,2.59,3.16,3.72,4.33,4.96))[variant]
        width = (.16,.12,.19,.14)[variant]
        rhythm = pulse_train(t, list(cadence), width)
        trem = 0.70 + 0.30 * math.sin(TAU * (4.2 + variant*.71) * t)
        # Slightly changing pitch and vowel-like resonance create voiced texture.
        f0 = base + 10 * math.sin(TAU * (1.7 + variant*.19) * t)
        source = (math.sin(TAU*f0*t + phase) + .43*math.sin(TAU*f0*2.03*t + phase*.4)
                  + .21*math.sin(TAU*f0*3.01*t + 1.2))
        formants = (.58*math.sin(TAU*(520+variant*37)*t + .2)
                    + .36*math.sin(TAU*(920-variant*29)*t + 1.4)
                    + .18*math.sin(TAU*(1510+variant*41)*t + .9))
        return rhythm * trem * (0.67*source + 0.33*formants)
    if family == "giggle":
        cadence = ((.47,.79,1.15,1.61,2.02,2.42,2.79),
                   (.36,.70,1.06,1.49,1.99,2.53,3.02,3.42))[variant]
        rhythm = pulse_train(t, list(cadence), (.10,.14)[variant])
        f0 = (263, 292)[variant] + 15*math.sin(TAU*(2.2+variant*.4)*t)
        return rhythm * (math.sin(TAU*f0*t + phase) + .31*math.sin(TAU*f0*2.7*t + phase*.6 + 1.1)
                         + .18*math.sin(TAU*1080*t + .7))
    return 0.0


def make_samples(name: str, duration: float, family: str, seed: int) -> list[int]:
    rng = random.Random(seed)
    n = round(duration * RATE)
    phases = [rng.random()*TAU for _ in range(8)]
    # Noise filters provide airy texture without sharp broadband attacks.
    low, mid, high = 0.0, 0.0, 0.0
    raw: list[float] = []
    for i in range(n):
        t = i / RATE
        x = t / duration
        env = envelope(t, duration)
        noise = rng.uniform(-1, 1)
        low += .010 * (noise - low)
        mid += .055 * (noise - mid)
        high = noise - mid

        if family in ("laugh", "giggle"):
            variant = int(name[-2:]) - 1
            val = vocal(t, family, variant, phases[5])
            # Quiet breath/noise rounds the synthetic voice and avoids a tonal beep.
            breath = (noise*.13 + low*.14 + high*.018)
            val = val * .69 + breath * (.28 + .22*math.sin(TAU*.21*t + phases[0]))
            # A slow stereo-like motion is encoded in timbre (mono compatible).
            val *= .84 + .16*math.sin(TAU*.17*t + phases[1])
        elif family == "whisper":
            variant = int(name[-2:]) - 1
            # Nonverbal breathy whisper: filtered air shaped into slowly moving phrases.
            phrase_rate = (1.15, .82)[variant]
            phrase = .24 + .76*abs(math.sin(TAU*phrase_rate*t + phases[0]))
            breath_gaps = .54 + .46*abs(math.sin(TAU*(.31+variant*.08)*t + phases[1]))
            formant = (.22*math.sin(TAU*(430+variant*75)*t + phases[2])
                       + .12*math.sin(TAU*(710-variant*48)*t + phases[3]))
            val = phrase * breath_gaps * (low*.48 + mid*.15 + high*.055 + formant*.17)
        elif family == "wind":
            variant = int(name[-2:]) - 1
            swell = .37 + .63*(.5 + .5*math.sin(TAU*(.16 if variant == 0 else .23)*t + phases[0]))
            gust = .60 + .40*abs(math.sin(TAU*(.43 if variant == 0 else .31)*t + phases[1]))
            f = (118 if variant == 0 else 93) + 20*math.sin(TAU*.21*t + phases[2])
            howl = .34*math.sin(TAU*f*t + 2.1*math.sin(TAU*.12*t + phases[3]))
            howl += .12*math.sin(TAU*(f*1.51)*t + phases[4])
            val = swell*gust*(low*.82 + mid*.10 + howl)
        elif family == "creak":
            # One soft, irregular timber groan, slowly bending in pitch.
            bend = 0.5 + .5*math.sin(TAU*.37*t + phases[0])
            f = 128 + 43*math.sin(TAU*.48*t + phases[1]) + 17*math.sin(TAU*.91*t)
            groan = math.sin(TAU*f*t + 1.6*math.sin(TAU*.22*t))
            val = (.46*groan + .15*math.sin(TAU*(f*1.47)*t + phases[2])
                   + .16*low + .025*high) * (.42 + .58*bend)
        else:  # special: restrained hollow, soft, layered chime-like moan.
            swell = .35 + .65*smoothstep((t-.25)/(duration*.54))
            decay = 1.0 - .32*smoothstep((t-duration*.46)/(duration*.50))
            f = 236 + 23*math.sin(TAU*.19*t + phases[0])
            hollow = (.34*math.sin(TAU*f*t + phases[1])
                      + .19*math.sin(TAU*(f*1.49)*t + phases[2])
                      + .12*math.sin(TAU*(f*2.04)*t + phases[3]))
            airy = low*.42 + mid*.12 + high*.016
            val = swell * decay * (hollow + airy)

        raw.append(val * env)

    peak = max(abs(v) for v in raw) or 1.0
    # Target peaks are deliberately quiet; per-cue target remains under 0.35 FS.
    target = {"laugh": .285, "giggle": .245, "whisper": .145,
              "wind": .205, "creak": .19, "special": .22}[family]
    scale = target / peak
    return [max(-32768, min(32767, round(v * scale * 32767))) for v in raw]


def write_cue(name: str, duration: float, family: str, seed: int) -> Path:
    path = ROOT / f"{name}.wav"
    samples = make_samples(name, duration, family, seed)
    with wave.open(str(path), "wb") as wav:
        wav.setnchannels(1)
        wav.setsampwidth(2)
        wav.setframerate(RATE)
        wav.writeframes(struct.pack("<" + "h"*len(samples), *samples))
    return path


def main() -> None:
    ROOT.mkdir(parents=True, exist_ok=True)
    for name, duration, family, seed in CUES:
        stable_seed = int.from_bytes(hashlib.sha256(f"{name}:{seed}".encode()).digest()[:4], "little")
        path = write_cue(name, duration, family, stable_seed)
        print(f"{path.name}: {duration:.1f}s")


if __name__ == "__main__":
    main()
