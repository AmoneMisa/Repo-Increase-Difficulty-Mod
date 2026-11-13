"""Original deterministic synth/Foley cues for the 0.10.15 gear, mono PCM16 22050 Hz. No external recordings.
Same format as tools/build_item_audio.py (ItemAudio.Load accepts only canonical PCM16 mono RIFF)."""
import math, random, struct, wave
from pathlib import Path

OUT = Path(__file__).resolve().parents[1] / 'src/Audio'; OUT.mkdir(parents=True, exist_ok=True)
RATE = 22050


def write(name, samples, level=.7):
    peak = max(abs(x) for x in samples) or 1
    data = [int(x / peak * level * 32767) for x in samples]
    with wave.open(str(OUT / (name + '.wav')), 'wb') as out:
        out.setparams((1, 2, RATE, len(data), 'NONE', 'not compressed'))
        out.writeframes(struct.pack('<' + 'h' * len(data), *data))


def make(name, seconds, voice, level=.7):
    rng = random.Random(name); low = 0.0; band = 0.0; samples = []
    n = int(seconds * RATE)
    for i in range(n):
        t = i / RATE; noise = rng.uniform(-1, 1)
        low = low * .86 + noise * .14; band = band * .6 + (noise - low) * .4
        value = voice(t, noise, low, band, rng)
        fade = min(1, t / .003, (seconds - t) / .03)
        samples.append(value * max(0, fade))
    write(name, samples, level)


def clink(t, f=2400): return math.sin(2 * math.pi * f * t) * math.exp(-t * 60)


# Grenades: arming click, then the burst at detonation.
make('gear_arm', .22, lambda t, n, lo, b, r: .5 * clink(t, 2100) + .4 * clink(max(0, t - .07), 3100) * (t > .07) + .2 * b * math.exp(-t * 40), .45)
make('smoke_burst', 1.7, lambda t, n, lo, b, r:
     (.7 * b + .3 * lo) * (1 - math.exp(-t * 30)) * math.exp(-t * 1.2) + .5 * lo * math.exp(-t * 18))
make('cryo_burst', 1.0, lambda t, n, lo, b, r:
     .8 * (n - lo) * math.exp(-t * 26) + .25 * math.sin(2 * math.pi * (1800 * t - 500 * t * t)) * math.exp(-t * 5)
     + .25 * b * (max(0, math.sin(t * 95)) ** 8) * math.exp(-t * 3))
make('glue_burst', .7, lambda t, n, lo, b, r:
     .7 * lo * math.exp(-t * 14) + .35 * math.sin(2 * math.pi * (180 * t + 60 * math.sin(t * 40))) * math.exp(-t * 9)
     + .3 * b * math.exp(-t * 30))

# Consumables: played on use for everyone nearby.
make('antidote_use', .8, lambda t, n, lo, b, r:
     .5 * clink(t, 1600) + .45 * b * math.exp(-((t - .18) / .1) ** 2) + .3 * math.sin(2 * math.pi * (300 * t + 140 * t * t)) * math.exp(-((t - .5) / .15) ** 2), .5)
make('adrenaline_use', .9, lambda t, n, lo, b, r:
     .45 * clink(t, 2600) + .4 * b * math.exp(-((t - .15) / .08) ** 2)
     + .7 * math.sin(2 * math.pi * 55 * t) * (math.exp(-((t - .45) / .03) ** 2) + .8 * math.exp(-((t - .65) / .03) ** 2)), .6)
make('repair_use', 1.0, lambda t, n, lo, b, r:
     sum(clink(max(0, t - k * .11), 1500 + 200 * (k % 2)) * (t > k * .11) for k in range(8)) * .35 + .15 * lo * math.exp(-t * 3), .5)

# Boomerang Sickle: a spinning whoosh while it flies.
make('sickle_throw', .5, lambda t, n, lo, b, r:
     (.7 * b + .3 * lo) * math.sin(math.pi * t / .5) ** 2 * (.6 + .4 * math.sin(2 * math.pi * 14 * t)), .45)
print('Created 9 original gear cues')
