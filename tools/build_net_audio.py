"""Original synthesized cues for the Net Launcher (mono PCM16 22050 Hz, no recordings).
net_shot: pneumatic thump + rope whoosh. net_hit: soft weighted thud + rope rustle.
python tools/build_net_audio.py"""
import math, random, struct, wave
from pathlib import Path

OUT = Path(__file__).resolve().parents[1] / 'src' / 'Audio'
RATE = 22050


def write(name, samples):
    peak = max(1e-6, max(abs(s) for s in samples))
    with wave.open(str(OUT / f'{name}.wav'), 'wb') as w:
        w.setnchannels(1); w.setsampwidth(2); w.setframerate(RATE)
        w.writeframes(b''.join(struct.pack('<h', int(max(-1, min(1, s / peak * .9)) * 32767)) for s in samples))


def shot():
    rng = random.Random('net_shot'); out = []; low = band = 0.0
    for i in range(int(.55 * RATE)):
        t = i / RATE; n = rng.uniform(-1, 1)
        low = low * .93 + n * .07; band = band * .6 + (n - low) * .4
        thump = math.sin(2 * math.pi * (85 * t + 40 * (1 - math.exp(-t * 30)))) * math.exp(-t * 22)
        puff = low * math.exp(-t * 18) * 2.2
        whoosh = band * (1 - math.exp(-t * 25)) * math.exp(-max(0, t - .06) * 7) * .55
        out.append(.75 * thump + .45 * puff + whoosh)
    return out


def hit():
    rng = random.Random('net_hit'); out = []; low = 0.0
    for i in range(int(.5 * RATE)):
        t = i / RATE; n = rng.uniform(-1, 1); low = low * .9 + n * .1
        thud = math.sin(2 * math.pi * 62 * t) * math.exp(-t * 16) + low * math.exp(-t * 20) * 1.6
        rustle = (n - low) * (sum(max(0, math.sin(t * f)) ** 14 for f in (91, 137, 173)) * .5) * math.exp(-t * 5)
        out.append(.8 * thud + .5 * rustle)
    return out


write('net_shot', shot())
write('net_hit', hit())
print('IHM_NET_AUDIO_OK')
