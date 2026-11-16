"""Original synthesized Fable Realm sounds: the magic mirror (waking chime, whisper, falling asleep) and the
pumpkin spell. PCM16 mono."""
import math, random, struct, wave
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
RATE = 22050


def bell(t, onset, freq, gain, decay, shimmer=0.0):
    u = t - onset
    if u < 0:
        return 0.0
    wobble = 1.0 + shimmer * math.sin(math.tau * 5.5 * u)
    partials = ((1.0, 1.0), (2.76, .32), (5.4, .12))
    return gain * wobble * math.exp(-decay * u) * sum(a * math.sin(math.tau * freq * k * u) for k, a in partials)


def wake(t, rng, state):
    # A rising glass arpeggio over a soft swell: the face opening its eyes.
    notes = [(0.00, 523.25), (0.11, 659.25), (0.22, 783.99), (0.34, 1046.5), (0.50, 1318.5)]
    v = sum(bell(t, onset, f, .22, 3.2, .08) for onset, f in notes)
    swell = math.sin(math.pi * min(1.0, t / 1.5)) ** 2
    return v + .08 * swell * (math.sin(math.tau * 261.63 * t) + math.sin(math.tau * 392.0 * t))


def sleep(t, rng, state):
    # The same notes falling and fading: the eyes closing.
    notes = [(0.00, 1046.5), (0.13, 783.99), (0.27, 659.25), (0.43, 523.25)]
    return sum(bell(t, onset, f, .2, 4.0, .05) for onset, f in notes) + .06 * math.exp(-2.5 * t) * math.sin(math.tau * 196.0 * t)


def whisper(t, rng, state):
    # Breath shaped like three murmured syllables, with a faint glass ring under it. No words.
    noise = rng.uniform(-1, 1)
    syllables = [(0.05, .30, 620.0, 1500.0), (0.42, .34, 480.0, 1900.0), (0.86, .44, 700.0, 1250.0)]
    v = 0.0
    for index, (onset, length, f1, f2) in enumerate(syllables):
        u = t - onset
        if u < 0 or u > length:
            continue
        envelope = max(0.0, math.sin(math.pi * u / length)) ** 1.5
        for band, (freq, q) in enumerate(((f1, .93), (f2, .95))):
            # Two-pole resonator per formant, state kept per syllable and band.
            y1, y2 = state.get((index, band), (0.0, 0.0))
            w = math.tau * freq / RATE
            y = noise * (1 - q) + 2 * q * math.cos(w) * y1 - q * q * y2
            state[(index, band)] = (y, y1)
            v += envelope * y * (1.8 if band == 0 else 1.2)
    return v + bell(t, 0.02, 880.0, .05, 2.2, .1) + bell(t, 0.8, 1174.7, .035, 2.6, .1)


def spell(t, rng, state):
    # A quick run of small bells climbing two octaves while a shimmer swells, then one bright chord: the wand's touch.
    scale = [523.25, 587.33, 659.25, 783.99, 880.0]
    v = 0.0
    for n in range(16):
        v += bell(t, .1 * n, scale[n % 5] * 2 ** (n // 5), .13, 7.0, .06)
    swell = min(1.0, t / 1.9) ** 2 * (1.0 if t < 1.9 else math.exp(-5.0 * (t - 1.9)))
    low = state.get('low', 0.0)
    noise = rng.uniform(-1, 1)
    state['low'] = low = .6 * low + .4 * noise
    v += .1 * swell * (noise - low)
    for freq in (1046.5, 1318.5, 1568.0, 2093.0):
        v += bell(t, 1.9, freq, .2, 3.4, .1)
    return v


def write(name, duration, voice, level):
    rng = random.Random(name)
    state = {}
    values = []
    for i in range(round(duration * RATE)):
        t = i / RATE
        values.append(voice(t, rng, state) * min(1.0, t / .004, (duration - t) / .08))
    peak = max(map(abs, values))
    samples = [round(v / peak * level * 32767) for v in values]
    path = ROOT / 'src/Audio' / ('valuable_' + name + '.wav')
    with wave.open(str(path), 'wb') as f:
        f.setparams((1, 2, RATE, len(samples), 'NONE', 'not compressed'))
        f.writeframes(struct.pack('<' + 'h' * len(samples), *samples))
    rms = math.sqrt(sum(s * s for s in samples) / len(samples))
    assert 300 < rms < 12000 and max(map(abs, samples)) < 30000 and abs(samples[0]) < 10 and abs(samples[-1]) < 100, (name, rms)
    print(f'{path.name}: {duration:.2f} s, peak {max(map(abs, samples))}, rms {rms:.0f}')


write('mirror_wake', 1.7, wake, .62)
write('mirror_whisper', 1.45, whisper, .6)
write('mirror_sleep', 1.5, sleep, .58)
write('spell', 2.6, spell, .62)
