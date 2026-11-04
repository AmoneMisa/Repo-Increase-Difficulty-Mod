"""python tools/build_flame_audio.py --root .

Synthesises the Flamethrower C.A.R.T. sounds (original, procedural): a seamless roaring
burner loop and a short ignition whoomp. Canonical PCM16 mono RIFF, as ItemAudio expects.
"""
import argparse
import wave
from pathlib import Path

import numpy as np

parser = argparse.ArgumentParser(); parser.add_argument('--root', type=Path, required=True)
OUT = parser.parse_args().root.resolve() / 'src' / 'Audio'
RATE = 44100
rng = np.random.default_rng(31)


def lowpass(x, cutoff):
    a = np.exp(-2 * np.pi * cutoff / RATE); y = np.empty_like(x); acc = 0.0
    for i, v in enumerate(x):
        acc = a * acc + (1 - a) * v; y[i] = acc
    return y


def save(name, x):
    x = x / max(1e-6, np.max(np.abs(x))) * .85
    with wave.open(str(OUT / name), 'wb') as w:
        w.setnchannels(1); w.setsampwidth(2); w.setframerate(RATE)
        w.writeframes((x * 32767).astype('<i2').tobytes())
    print('wrote', OUT / name)


# Loop: low roar + crackle, made seamless by crossfading the tail into the head.
seconds, fade = 2.4, .3
n, f = int(RATE * seconds), int(RATE * fade)
noise = rng.standard_normal(n + f)
roar = lowpass(noise, 380) * 2.2 + lowpass(noise, 1400) * .55 - lowpass(noise, 90) * 1.3
t = np.arange(n + f) / RATE
roar *= 1 + .18 * np.sin(2 * np.pi * 5.3 * t) * np.sin(2 * np.pi * .7 * t)
crackle = np.zeros(n + f)
for i in rng.integers(0, n + f - 400, 180):
    crackle[i:i + 300] += rng.standard_normal(300) * np.exp(-np.arange(300) / 40) * rng.uniform(.2, .8)
x = roar + lowpass(crackle, 5000) * .9
loop = x[:n].copy(); w = np.linspace(0, 1, f)
loop[:f] = loop[:f] * w + x[n:n + f] * (1 - w)
save('flame_loop.wav', loop)

# Ignition: pressure pop and a rising whoosh that settles.
n = int(RATE * .9); t = np.arange(n) / RATE
burst = lowpass(rng.standard_normal(n), 900) * np.exp(-t * 4) * 2.5
thump = np.sin(2 * np.pi * (70 - 30 * t) * t) * np.exp(-t * 9) * 1.4
click = np.zeros(n); click[:600] = rng.standard_normal(600) * np.exp(-np.arange(600) / 70)
save('flame_ignite.wav', burst + thump + click * .6)
