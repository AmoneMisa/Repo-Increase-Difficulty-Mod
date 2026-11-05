"""Original label textures and synthesized sounds for the plush corgi and the whiskey bottle.

python tools/build_corgi_whiskey_assets.py
Writes src/Art/v11_whiskey_label.png, src/Art/v11_whiskey_neck.png and the cues
src/Audio/valuable_dog_bark.wav, valuable_dog_arf.wav, valuable_dog_whine.wav, valuable_dog_yip.wav,
valuable_plush.wav. The brand is invented ("OLD TRUCK No. 9"); no real whiskey trademark is reproduced.
Everything is generated here: no recorded or third-party audio is used.
"""
import math
import wave
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont

ROOT = Path(__file__).resolve().parents[1]
ART = ROOT / 'src' / 'Art'
AUDIO = ROOT / 'src' / 'Audio'
FONTS = Path('C:/Windows/Fonts')
RATE = 44100


def font(name, size):
    for candidate in (name, 'georgiab.ttf', 'timesbd.ttf'):
        path = FONTS / candidate
        if path.exists():
            return ImageFont.truetype(str(path), size)
    return ImageFont.load_default()


def centered(draw, y, text, face, fill, width):
    box = draw.textbbox((0, 0), text, font=face)
    draw.text(((width - (box[2] - box[0])) / 2 - box[0], y), text, font=face, fill=fill)
    return y + (box[3] - box[1])


def label():
    w, h = 512, 576
    img = Image.new('RGB', (w, h), (14, 13, 12))
    d = ImageDraw.Draw(img)
    cream = (232, 226, 210)
    # Double frame with notched corners and a little filigree.
    for inset, width in ((14, 4), (26, 2)):
        d.rectangle((inset, inset, w - inset, h - inset), outline=cream, width=width)
    for cx, cy in ((26, 26), (w - 26, 26), (26, h - 26), (w - 26, h - 26)):
        d.ellipse((cx - 10, cy - 10, cx + 10, cy + 10), fill=(14, 13, 12), outline=cream, width=2)
        d.ellipse((cx - 3, cy - 3, cx + 3, cy + 3), fill=cream)
    y = centered(d, 52, 'OLD TRUCK', font('georgiab.ttf', 60), cream, w) + 18
    d.line((70, y, w - 70, y), fill=cream, width=2)
    y = centered(d, y + 10, 'TENNESSEE STYLE', font('georgia.ttf', 30), cream, w) + 18
    # Big ornate number with a ring.
    d.ellipse((w / 2 - 100, y, w / 2 + 100, y + 200), outline=cream, width=3)
    d.ellipse((w / 2 - 89, y + 11, w / 2 + 89, y + 189), outline=cream, width=1)
    ny = centered(d, y + 22, 'No.', font('georgiai.ttf', 34), cream, w)
    centered(d, ny + 4, '9', font('georgiab.ttf', 124), cream, w)
    y += 214
    y = centered(d, y, 'SOUR MASH', font('georgiab.ttf', 36), cream, w) + 10
    y = centered(d, y, 'WHISKEY', font('georgiab.ttf', 52), cream, w) + 18
    d.line((110, y, w - 110, y), fill=cream, width=1)
    centered(d, y + 10, '40% ALC./VOL.  ·  DISTILLED & AGED', font('georgia.ttf', 18), cream, w)
    # Slight print softness and paper wear so it does not look like a screenshot.
    img = img.filter(ImageFilter.GaussianBlur(.6))
    rng = np.random.default_rng(9)
    a = np.asarray(img).astype(np.float32)
    a *= 1 - .05 * np.asarray(Image.fromarray((rng.random((h // 8, w // 8)) * 255).astype(np.uint8)).resize((w, h), Image.BILINEAR), np.float32)[..., None] / 255
    Image.fromarray(np.clip(a, 0, 255).astype(np.uint8)).save(ART / 'v11_whiskey_label.png')

    # Neck band: repeating "OLD TRUCK No.9" on black.
    nb = Image.new('RGB', (512, 96), (14, 13, 12))
    nd = ImageDraw.Draw(nb)
    nd.rectangle((0, 8, 511, 87), outline=cream, width=2)
    centered(nd, 26, 'OLD TRUCK  No. 9', font('georgiab.ttf', 38), cream, 512)
    nb.filter(ImageFilter.GaussianBlur(.5)).save(ART / 'v11_whiskey_neck.png')


# ------------------------------------------------------------------ audio

def save(name, signal):
    signal = np.asarray(signal, dtype=np.float64)
    peak = np.max(np.abs(signal)) or 1
    pcm = (signal / peak * .89 * 32767).astype('<i2')
    with wave.open(str(AUDIO / (name + '.wav')), 'wb') as f:
        f.setnchannels(1); f.setsampwidth(2); f.setframerate(RATE); f.writeframes(pcm.tobytes())


def envelope(n, attack, release):
    t = np.arange(n) / RATE
    dur = n / RATE
    return np.minimum(1, t / attack) * np.clip((dur - t) / release, 0, 1)


def formant(x, freq, q):
    """Two-pole resonator (band-pass) over a signal."""
    w = 2 * math.pi * freq / RATE
    r = math.exp(-w / (2 * q))
    a1, a2 = -2 * r * math.cos(w), r * r
    y = np.zeros_like(x)
    y1 = y2 = 0.0
    g = 1 - r
    for i, v in enumerate(x):
        y0 = g * v - a1 * y1 - a2 * y2
        y[i] = y0; y2 = y1; y1 = y0
    return y


def voiced(f0, rng, breath=.18):
    """Glottal-like pulse train following the pitch curve f0 (per-sample array)."""
    phase = np.cumsum(f0 / RATE)
    # Sawtooth-ish rich source, slight jitter.
    phase += .004 * np.cumsum(rng.normal(0, 1, len(f0))) / RATE * 50
    src = 2 * (phase % 1) - 1
    src = src - np.convolve(src, np.ones(3) / 3, 'same') * .3
    return src + breath * rng.normal(0, 1, len(f0))


def bark(duration, start, peak, end, formants, rng, rough=.35):
    n = int(duration * RATE)
    t = np.linspace(0, 1, n)
    # Fast rise to the peak pitch then a fall, the typical "woof" contour.
    f0 = np.where(t < .25, start + (peak - start) * np.clip(t / .25, 0, 1) ** .6, peak + (end - peak) * np.clip((t - .25) / .75, 0, 1) ** 1.3)
    src = voiced(f0, rng, rough)
    out = sum(gain * formant(src, f, q) for f, q, gain in formants)
    # Throat growl: amplitude modulation at ~45 Hz.
    out *= 1 + .35 * np.sin(2 * math.pi * 45 * np.arange(n) / RATE)
    return out * envelope(n, .006, duration * .55)


def plush_box(signal, rng):
    """Toy voice-box colour: small speaker band-limit plus muffled fabric."""
    lo = formant(signal, 1400, .7)
    return .6 * lo + .4 * formant(signal, 2600, 1.2) + .01 * rng.normal(0, 1, len(signal))


def silence(seconds):
    return np.zeros(int(seconds * RATE))


def sounds():
    rng = np.random.default_rng(7)
    F = [(700, 4, 1.0), (1500, 5, .7), (2700, 6, .35)]
    woof = bark(.22, 420, 560, 300, F, rng)
    save('valuable_dog_bark', plush_box(np.concatenate([woof, silence(.08)]), rng))
    a1 = bark(.13, 600, 760, 480, [(900, 4, 1), (1900, 5, .7), (3000, 6, .3)], rng)
    a2 = bark(.15, 560, 720, 420, [(850, 4, 1), (1800, 5, .7), (3000, 6, .3)], rng)
    save('valuable_dog_arf', plush_box(np.concatenate([a1, silence(.07), a2, silence(.06)]), rng))
    # Whine: thin, nasal pitch glide.
    n = int(.75 * RATE)
    t = np.linspace(0, 1, n)
    f0 = 900 + 420 * np.sin(math.pi * t) ** 1.5 - 180 * t + 25 * np.sin(2 * math.pi * 6 * t)
    whine = sum(g * formant(voiced(f0, rng, .04), f, q) for f, q, g in [(1200, 6, 1), (2400, 8, .4)])
    save('valuable_dog_whine', plush_box(whine * envelope(n, .05, .3), rng))
    yip = bark(.09, 900, 1150, 700, [(1200, 4, 1), (2300, 5, .6)], rng, .2)
    save('valuable_dog_yip', plush_box(np.concatenate([yip, silence(.05)]), rng))
    # Soft fabric thump for impacts.
    n = int(.16 * RATE)
    thump = formant(rng.normal(0, 1, n), 180, .8) + .3 * formant(rng.normal(0, 1, n), 700, .6)
    save('valuable_plush', thump * envelope(n, .003, .12) ** 2)


if __name__ == '__main__':
    label()
    sounds()
    print('ASSETS_COMPLETE')
