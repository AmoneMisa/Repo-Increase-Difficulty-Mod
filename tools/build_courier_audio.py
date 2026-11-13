"""Courier pet sounds: original synthesised cues, mono PCM16 22050 Hz, no recordings.

For each animal: <animal>_happy (petted) and <animal>_call (takes a job / arrives). Shared: step, poof, find.
Writes src/Audio/courier_*.wav.
"""
import math, random, struct, wave
from pathlib import Path

OUT = Path(__file__).resolve().parents[1] / 'src' / 'Audio'
RATE = 22050


def write(name, samples, level=.7):
    peak = max(1e-6, max(abs(x) for x in samples))
    with wave.open(str(OUT / f'courier_{name}.wav'), 'wb') as w:
        w.setnchannels(1); w.setsampwidth(2); w.setframerate(RATE)
        w.writeframes(b''.join(struct.pack('<h', int(max(-1, min(1, x / peak * level)) * 32767)) for x in samples))


def env(t, length, attack=.01, release=.05):
    return max(0.0, min(1.0, t / attack, (length - t) / release))


class Resonator:
    """Two-pole resonance (a formant): unity gain at its centre."""
    def __init__(self):
        self.y1 = self.y2 = 0.0

    def run(self, x, fc, bw):
        fc = min(fc, RATE * .45)
        r = math.exp(-math.pi * bw / RATE)
        a1 = 2 * r * math.cos(2 * math.pi * fc / RATE); a2 = -r * r
        y = (1 - r) * x + a1 * self.y1 + a2 * self.y2
        self.y2 = self.y1; self.y1 = y
        return y * (1 + r)


def swell(t, length, attack, release):
    """Loudness of one call: a smooth rise, a sagging middle and a tail that dies away (no straight ramps)."""
    if t < attack: return .5 - .5 * math.cos(math.pi * t / attack)
    left = length - t
    if left < release: return (.5 - .5 * math.cos(math.pi * left / release)) ** 1.5
    return 1.0


def voice(length, pitch, harmonics, formants=(), noise=0.0, vibrato=(0, 0), seed=1, rough=0.0, mouth=.25):
    """One animal call, built the way a throat makes it.

    A harmonic source whose pitch wanders a little at random (no two cycles alike), whose upper harmonics grow
    with effort (loud = bright, the start and the tail are dull), through formants that open and close with the
    call (mouth), plus breath that passes through the same formants and pulses with the voice. rough adds period
    doubling: the rasp of a bray, a squawk or a growl.
    """
    rng = random.Random(seed); out = []; phase = 0.0; low = 0.0; drift = 0.0; wobble = 0.0
    breath = [Resonator() for _ in formants] or [Resonator()]
    attack = min(.03, length * .22); release = min(.14, length * .5)
    for i in range(int(length * RATE)):
        t = i / RATE
        n = rng.uniform(-1, 1); low = low * .7 + n * .3
        drift += (rng.uniform(-1, 1) - drift) * .002       # pitch: slow random wander
        wobble += (rng.uniform(-1, 1) - wobble) * .004     # loudness: slow random wander
        effort = swell(t, length, attack, release)
        f = pitch(t) * (1 + .9 * drift + vibrato[1] * math.sin(2 * math.pi * vibrato[0] * t)) * (.97 + .03 * effort)
        phase += 2 * math.pi * f / RATE
        opening = 1 + mouth * (effort - .6)
        v = 0.0
        for k, a in enumerate(harmonics, 1):
            if k * f > RATE * .45: break
            boost = 1.0
            for fc, bw, g in formants:
                boost += g * math.exp(-((k * f - fc * opening) / bw) ** 2)
            v += a * effort ** (.35 * (k - 1)) * boost * math.sin(k * phase + .13 * k * k)
        if rough: v *= 1 + rough * math.sin(phase * .5)
        air = low + .18 * n
        if formants:
            shaped = sum(r.run(air, fc * opening, bw * 1.5) * (.6 + .4 * g) for r, (fc, bw, g) in zip(breath, formants)) / len(formants)
            air = .3 * air + 1.2 * shaped
        air *= .7 + .3 * math.sin(phase)                   # breath pulses with the voice
        out.append((v * (1 + 1.6 * wobble) + noise * air) * effort)
    return out


def concat(*parts, gap=.04):
    out = []
    for p in parts:
        out.extend(p); out.extend([0.0] * int(gap * RATE))
    return out


def variants(name, makers):
    for k, make in enumerate(makers, 1):
        write(f'{name}_happy{k}', make())


def penguin():
    # Penguin: nasal brays (strong low harmonics, buzzy formant), a happy trumpeting run.
    bray = lambda f0, L, s, up=0.0: voice(L, lambda t: f0 * (1 + up * t / L - .2 * (t / L) ** 2), [1, .95, .8, .7, .55, .4, .3, .2],
                                          [(700, 220, 3.0), (1500, 380, 1.6)], .35, vibrato=(28, .03), seed=s, rough=.45)
    variants('penguin', [
        lambda: concat(bray(300, .12, 1, .3), bray(340, .12, 2, .3), bray(380, .3, 3, .2), gap=.05),
        lambda: concat(bray(360, .2, 4, .4), bray(300, .26, 5, -.1), gap=.06),
        lambda: concat(*[bray(320 + 25 * k, .09, 10 + k, .2) for k in range(5)], gap=.035),
    ])
    write('penguin_call', concat(bray(280, .18, 6), bray(250, .24, 7)))


def chicken():
    bok = lambda f0, L, s: voice(L, lambda t: f0 * (1.25 - .55 * t / L), [1, .6, .5, .35, .25, .15], [(1100, 280, 2.2), (2400, 500, 1.0)], .4, seed=s)
    squawk = lambda L, s: voice(L, lambda t: 650 + 600 * math.sin(math.pi * min(1, t / L)), [1, .7, .55, .4, .3], [(1500, 400, 1.8)], .45, vibrato=(14, .04), seed=s, rough=.3)
    variants('chicken', [
        lambda: concat(bok(520, .07, 1), bok(540, .07, 2), bok(560, .08, 3), squawk(.34, 4), gap=.06),
        lambda: concat(*[bok(500 + 20 * (k % 2), .06, 20 + k) for k in range(6)], gap=.05),
        lambda: concat(bok(480, .08, 30), squawk(.42, 31), gap=.05),
    ])
    write('chicken_call', concat(bok(480, .08, 5), bok(500, .08, 6), gap=.08))


def cat():
    def meow(L, a, b, c, s):
        return voice(L, lambda t: a + (b - a) * math.sin(math.pi * min(1, t / (L * .7))) if t < L * .7 else b + (c - b) * (t - L * .7) / (L * .3),
                     [1, .75, .55, .4, .25, .12], [(750, 240, 1.6), (2000, 500, 1.1)], .16, vibrato=(6, .012), seed=s, mouth=.7)
    def purr(L, s):
        rng = random.Random(s); out = []; low = 0.0; chest = Resonator(); throat = Resonator(); phase = 0.0; drift = 0.0
        for i in range(int(L * RATE)):
            t = i / RATE; n = rng.uniform(-1, 1); low = low * .92 + n * .08
            drift += (rng.uniform(-1, 1) - drift) * .002
            breath = math.sin(2 * math.pi * t / .9)                      # out ... in ... out
            phase += (24 + 3 * breath) * (1 + 2 * drift) / RATE
            p = phase % 1.0
            pulse = math.exp(-p * 9) * (1 + .25 * rng.uniform(-1, 1))     # each pulse a soft knock, never identical
            strength = .75 + .25 * breath
            body = chest.run(pulse, 110, 60) + .5 * throat.run(pulse, 290, 120)
            out.append((body * 1.4 + low * 1.3 * (.4 + .6 * pulse)) * strength * swell(t, L, .12, .3))
        return out
    variants('cat', [
        lambda: concat(purr(.8, 1), meow(.4, 520, 780, 560, 2), gap=.02),
        lambda: concat(meow(.25, 700, 900, 760, 3), meow(.35, 560, 820, 600, 4), gap=.08),
        lambda: purr(1.2, 5),
    ])
    write('cat_call', meow(.36, 600, 840, 620, 6))


def owl():
    hoo = lambda L, f0, s, bend=.06: voice(L, lambda t: f0 * (1 + bend * math.sin(math.pi * t / L)), [1, .22, .07], [(420, 200, .6)], .14, seed=s)
    trill = lambda L, f0, s: voice(L, lambda t: f0 * (1 + .04 * math.sin(2 * math.pi * 30 * t)), [1, .25, .08], [], .05, seed=s)
    variants('owl', [
        lambda: concat(hoo(.22, 400, 1), hoo(.16, 430, 2), hoo(.38, 380, 3), gap=.07),
        lambda: concat(trill(.5, 420, 4), hoo(.3, 380, 5), gap=.08),
        lambda: concat(hoo(.14, 460, 6, .15), hoo(.14, 480, 7, .15), hoo(.14, 500, 8, .15), gap=.05),
    ])
    write('owl_call', concat(hoo(.2, 360, 9), hoo(.32, 340, 10), gap=.1))


def beaver():
    def chit(L, f0, s):
        return voice(L, lambda t: f0 * (1 + .35 * math.sin(2 * math.pi * 20 * t)), [1, .55, .35, .2], [(2600, 600, 1.6)], .3, seed=s)
    def chatter(L, s):  # teeth chattering: fast clicks
        rng = random.Random(s); out = []
        for i in range(int(L * RATE)):
            t = i / RATE; click = math.exp(-((t * 34) % 1) * 30)
            out.append(click * (rng.uniform(-1, 1) * .7 + .5 * math.sin(2 * math.pi * 2200 * t)) * env(t, L))
        return out
    variants('beaver', [
        lambda: concat(*[chit(.05, 900 + 60 * k, k) for k in range(6)], gap=.025),
        lambda: concat(chatter(.4, 20), chit(.12, 980, 21), gap=.03),
        lambda: concat(chit(.09, 820, 30), chit(.09, 900, 31), chatter(.25, 32), gap=.04),
    ])
    write('beaver_call', concat(chit(.08, 800, 7), chit(.1, 760, 8), gap=.05))


def extra_voices():
    def note(length, start, crest, end, harmonics, formants, breath, seed, flutter=(6, .018), rough=0.0):
        def contour(t):
            p = t / length
            return start * (1 - p) + end * p + (crest - (start + end) * .5) * math.sin(math.pi * p)
        return voice(length, contour, harmonics, formants, breath, flutter, seed, rough)

    bleat = lambda length, f, s: note(length, f, f * 1.2, f * .82,
        [1, .75, .52, .34, .23, .12], [(850, 240, 2.4), (1600, 450, 1)], .24, s, (18, .044))
    moo = lambda length, f, s: note(length, f, f * 1.19, f * .7,
        [1, .78, .64, .4, .23, .15, .09], [(430, 150, 2.5), (980, 240, 1.5)], .18, s, (6, .035))
    yip = lambda length, f, s: note(length, f, f * 1.7, f * .7,
        [1, .8, .61, .45, .34, .22, .15], [(1050, 450, 1.6), (2100, 650, 1.2)], .45, s, rough=.25)
    peep = lambda length, f, s: note(length, f, f * 1.4, f * .91,
        [1, .19, .035], [(2200, 400, .8)], .08, s, (20, .023))
    chuckle = lambda length, f, s: note(length, f, f * 1.45, f * .66,
        [1, .65, .44, .35, .18], [(930, 200, 1.7), (1780, 300, 1.25)], .28, s, (26, .055))
    coo = lambda length, f, s: note(length, f, f * 1.26, f * .88,
        [1, .39, .19, .08], [(1350, 240, .8)], .21, s, (7, .025))
    chirr = lambda length, f, s: note(length, f, f * 1.28, f * .65,
        [1, .81, .58, .33, .26, .15], [(660, 190, 2), (1800, 500, 1.3)], .52, s, (31, .075), rough=.4)

    profiles = {
        'sheep': (bleat, .3, 420), 'cow': (moo, .46, 230), 'dog': (yip, .13, 490),
        'fairy': (peep, .14, 1330), 'devil': (chuckle, .17, 550), 'angel': (coo, .24, 710),
        'dragon': (chirr, .21, 360),
        'mermaid': (coo, .2, 930), 'skeleton': (chirr, .1, 560), 'elemental': (coo, .32, 250), 'phoenix': (yip, .17, 840),
    }
    for name, (animal, length, f) in profiles.items():
        write(name + '_call', animal(length, f, 11), .53)
        write(name + '_happy1', concat(animal(length * .67, f * 1.13, 21), animal(length, f * 1.28, 22)), .58)
        write(name + '_happy2', animal(length * 1.4, f * 1.09, 31), .58)
        write(name + '_happy3', concat(animal(length * .75, f, 41), animal(length * .6, f * 1.21, 42),
            animal(length * .72, f * 1.35, 43), gap=.055), .58)


def shared():
    rng = random.Random(3); step = []; low = 0; pad = Resonator()
    for i in range(int(.09 * RATE)):
        t = i / RATE; n = rng.uniform(-1, 1); low = low * .6 + n * .4
        thump = math.sin(2 * math.pi * (120 - 500 * t) * t) * math.exp(-t * 55)
        step.append((thump * .8 + pad.run(n, 900, 700) * .5 * math.exp(-t * 45) + low * .15 * math.exp(-t * 30)) * min(1, t / .003))
    write('step', step, .35)
    poof = []; low = 0
    for i in range(int(.45 * RATE)):
        t = i / RATE; n = rng.uniform(-1, 1); low = low * .9 + n * .1
        poof.append((low * 4 + .2 * math.sin(2 * math.pi * (300 - 260 * t) * t)) * math.exp(-t * 7) * env(t, .45, .005, .1))
    write('poof', poof, .55)
    find = []
    for i in range(int(.9 * RATE)):
        t = i / RATE; v = 0
        for k, f in enumerate((660, 880, 1320)):
            st = k * .12
            if t >= st: v += math.sin(2 * math.pi * f * (t - st)) * math.exp(-(t - st) * 4)
        find.append(v * env(t, .9, .005, .2))
    write('find', find, .5)


for build in (penguin, chicken, cat, owl, beaver, extra_voices, shared):
    build()
print('IHM_COURIER_AUDIO_OK', sorted(p.name for p in OUT.glob('courier_*.wav')))
