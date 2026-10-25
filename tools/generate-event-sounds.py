#!/usr/bin/env python3
"""Create original, compact PCM event effects using only Python's standard library."""
import math
import random
import hashlib
import struct
import wave
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1] / "src" / "RebornLauncher" / "assets" / "audio" / "events"
RATE = 22050
CUES = {
    "snow-crunch": ("noise", .28), "snowball-hit": ("thud", .24), "snow-poof": ("softnoise", .38),
    "ice-break": ("glass", .44), "rocket-launch": ("whistle", .75), "firework-boom": ("boom", .60),
    "sparkle": ("sparkle", .42), "confetti": ("confetti", .48), "egg-crack": ("crack", .25),
    "bird-chirp": ("chirp", .32), "rustle": ("rustle", .42), "chocolate-snap": ("snap", .18),
    "ghost": ("ghost", .64), "wood-creak": ("creak", .60), "pumpkin-pop": ("pop", .27),
    "bat-flap": ("flap", .32), "cauldron-bubble": ("bubble", .68), "bell": ("bell", .72),
    "gift-rip": ("rip", .48), "glass-break": ("glass", .40), "water-pop": ("water", .28),
    "leaf-rustle": ("leaf", .46), "magic": ("magic", .68), "thunder": ("thunder", .82), "wind": ("wind", .72),
}

def render(kind, duration, seed):
    rng = random.Random(seed)
    count = round(duration * RATE)
    phases = [rng.random() * math.tau for _ in range(5)]
    freqs = [rng.uniform(210, 650) for _ in range(5)]
    out = []
    for i in range(count):
        t = i / RATE
        x = t / duration
        env = min(1, t * 35) * min(1, (1-x) * 8)
        noise = rng.uniform(-1, 1)
        val = 0.0
        def tone(f, a=1, ph=0): return a * math.sin(math.tau*f*t + ph)
        if kind in ("noise", "softnoise", "rustle", "leaf", "wind", "thunder", "boom", "confetti", "flap", "rip"):
            filt = math.sin(math.tau*(95 if kind in ("boom", "thunder") else 500)*t + phases[0])
            amp = (1-x) ** (2 if kind in ("boom", "thunder") else .7)
            val = noise * (0.55 if kind in ("noise", "softnoise", "rustle", "leaf", "wind", "confetti", "flap", "rip") else .22) * amp
            val += filt * amp * (.40 if kind in ("boom", "thunder") else .20)
            if kind in ("rustle", "leaf", "wind"): val *= .35 + .65*abs(math.sin(t*15))
        elif kind in ("glass", "crack", "snap"):
            decay = math.exp(-t*(7 if kind == "glass" else 20))
            val = decay * (tone(freqs[0]*2.8,.35,phases[0]) + tone(freqs[1]*3.2,.23,phases[1]) + noise*.18)
        elif kind == "whistle":
            f = 440 + 1100*x - 800*x*x
            val = .55*math.sin(math.tau*f*t) * env
        elif kind == "chirp":
            f = 1500 + 700*math.sin(math.tau*5*x)
            val = .48*math.sin(math.tau*f*t) * (math.sin(math.pi*x)**2)
        elif kind == "ghost":
            f = 260 - 90*x + 65*math.sin(math.tau*2*x)
            val = .48*math.sin(math.tau*f*t) * env + noise*.08*env
        elif kind == "creak":
            f = 160 + 55*math.sin(math.tau*2*x) + 100*x
            val = .45*math.sin(math.tau*f*t) * env + noise*.08*env
        elif kind == "bell":
            val = sum(a*math.sin(math.tau*freq*t+phases[j])*math.exp(-t*d) for j,(freq,a,d) in enumerate(((880,.30,4),(1320,.25,5),(1760,.18,6),(2210,.08,8))))
        elif kind in ("sparkle", "magic"):
            f = 900 + 1500*x
            val = .38*math.sin(math.tau*f*t)*env + .16*math.sin(math.tau*f*1.5*t + phases[1])*env
        elif kind in ("bubble", "water", "pop", "thud"):
            f = (180 if kind in ("thud", "pop") else 480) * math.exp(-t*8) + 80
            val = .5*math.sin(math.tau*f*t)*env + noise*(.18 if kind != "thud" else .1)*env
        else:
            val = noise*.25*env
        out.append(max(-1, min(1, val*env)))
    peak = max(abs(v) for v in out) or 1
    scale = min(.48/peak, 1.0)
    return [int(v*scale*32767) for v in out]

def main():
    ROOT.mkdir(parents=True, exist_ok=True)
    for cue, (kind, duration) in CUES.items():
        for variant in (1, 2):
            seed = int.from_bytes(hashlib.sha256(f"{cue}:{variant}".encode()).digest()[:4], "little")
            samples = render(kind, duration * (1 if variant == 1 else .92), seed)
            path = ROOT / f"{cue}-{variant:02}.wav"
            with wave.open(str(path), "wb") as wav:
                wav.setnchannels(1); wav.setsampwidth(2); wav.setframerate(RATE)
                wav.writeframes(struct.pack("<" + "h"*len(samples), *samples))
            print(f"{path.name}: {len(samples)} samples")

if __name__ == "__main__": main()
