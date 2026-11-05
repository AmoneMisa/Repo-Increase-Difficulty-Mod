"""Original label textures and synthesized sounds for the plush corgi and the whiskey bottle.

python tools/build_corgi_whiskey_assets.py
Writes src/Art/v11_whiskey_label.png, src/Art/v11_whiskey_neck.png, src/Art/v11_corgi_bandana.png,
src/Art/v11_corgi_felt_orange.png, src/Art/v11_corgi_felt_cream.png and the cues
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


def arc_text(img, text, face, fill, cx, cy, radius, span):
    """Text along an upward arch (centre cx,cy, radius, total angle span in degrees)."""
    d = ImageDraw.Draw(img)
    widths = [d.textlength(ch, font=face) for ch in text]
    total = sum(widths); angle = -span / 2
    for ch, wch in zip(text, widths):
        step = span * wch / total; a = math.radians(angle + step / 2)
        size = int(face.size * 1.6)
        tile = Image.new('RGBA', (size, size), (0, 0, 0, 0))
        ImageDraw.Draw(tile).text((size / 2, size / 2), ch, font=face, fill=fill, anchor='mm')
        tile = tile.rotate(-math.degrees(a), resample=Image.BICUBIC)
        x = cx + radius * math.sin(a); y = cy - radius * math.cos(a)
        img.paste(tile, (int(x - size / 2), int(y - size / 2)), tile)
        angle += step


def filigree(d, box, cream):
    """Scroll-work border: a row of small curls between two rules."""
    x0, y0, x1, y1 = box
    d.rectangle(box, outline=cream, width=3)
    d.rectangle((x0 + 14, y0 + 14, x1 - 14, y1 - 14), outline=cream, width=1)
    step = 18
    for x in range(x0 + 16, x1 - 26, step):
        for top in (True, False):
            y = y0 + 7 if top else y1 - 7
            d.arc((x, y - 5, x + 10, y + 5), 200 if top else 20, 340 if top else 160, fill=cream, width=1)
    for y in range(y0 + 16, y1 - 26, step):
        for left in (True, False):
            x = x0 + 7 if left else x1 - 7
            d.arc((x - 5, y, x + 5, y + 10), 110 if left else 290, 250 if left else 70, fill=cream, width=1)


def label():
    # Mapped onto an arched label: the top centre of the image is the arch peak.
    w, h = 512, 640
    ink = (12, 11, 10); cream = (236, 232, 220)
    img = Image.new('RGB', (w, h), ink)
    d = ImageDraw.Draw(img)
    filigree(d, (10, 10, w - 11, h - 11), cream)
    arc_text(img, 'OLD TRUCK', font('georgiab.ttf', 64), cream, w / 2, 330, 250, 92)
    d = ImageDraw.Draw(img)
    y = centered(d, 150, 'TENNESSEE STYLE', font('georgia.ttf', 26), cream, w) + 18
    # Central medallion.
    d.ellipse((w / 2 - 92, y, w / 2 + 92, y + 184), outline=cream, width=3)
    d.ellipse((w / 2 - 82, y + 10, w / 2 + 82, y + 174), outline=cream, width=1)
    for k in range(24):
        a = 2 * math.pi * k / 24; r0, r1 = 96, 104
        d.line((w / 2 + r0 * math.cos(a), y + 92 + r0 * math.sin(a), w / 2 + r1 * math.cos(a), y + 92 + r1 * math.sin(a)), fill=cream, width=2)
    ny = centered(d, y + 26, 'Old No.', font('georgiai.ttf', 30), cream, w)
    centered(d, ny + 2, '9', font('georgiab.ttf', 104), cream, w)
    y += 206
    y = centered(d, y, 'BRAND', font('georgia.ttf', 24), cream, w) + 14
    d.line((90, y, w - 90, y), fill=cream, width=2); y += 12
    y = centered(d, y, 'TENNESSEE', font('georgiab.ttf', 42), cream, w) + 10
    y = centered(d, y, 'SOUR MASH WHISKEY', font('georgiab.ttf', 32), cream, w) + 16
    d.line((130, y, w - 130, y), fill=cream, width=1)
    centered(d, y + 8, '40% ALC./VOL.  ·  DISTILLED & AGED', font('georgia.ttf', 17), cream, w)
    img = img.filter(ImageFilter.GaussianBlur(.55))
    rng = np.random.default_rng(9)
    a = np.asarray(img).astype(np.float32)
    a *= 1 - .05 * np.asarray(Image.fromarray((rng.random((h // 8, w // 8)) * 255).astype(np.uint8)).resize((w, h), Image.BILINEAR), np.float32)[..., None] / 255
    Image.fromarray(np.clip(a, 0, 255).astype(np.uint8)).save(ART / 'v11_whiskey_label.png')

    # Shoulder sleeve: wraps once around (u from +X, counter-clockwise), v from shoulder (image bottom) to neck top.
    nw, nh = 1024, 256
    nb = Image.new('RGB', (nw, nh), ink); nd = ImageDraw.Draw(nb)
    nd.line((0, 14, nw, 14), fill=cream, width=3); nd.line((0, 24, nw, 24), fill=cream, width=1)
    nd.line((0, nh - 30, nw, nh - 30), fill=cream, width=2)
    # Front (Blender -Y) is u = 0.75; back (+Y) is u = 0.25.
    for cx in (nw * .75, nw * .25):
        for text, face, y in (('OLD TRUCK', font('georgiab.ttf', 40), nh - 100), ('Old No. 9', font('georgiai.ttf', 28), nh - 150)):
            box = nd.textbbox((0, 0), text, font=face)
            nd.text((cx - (box[2] - box[0]) / 2 - box[0], y), text, font=face, fill=cream)
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



def bandana():
    """Red felt bandana flap (UV: u across, v from the tip up) with a white paw print and a stitched hem."""
    S = 512; rng = np.random.default_rng(7)
    base = np.array([150, 30, 26], float)
    a = np.ones((S, S, 3)) * base
    a += rng.normal(0, 7, (S, S, 1))                           # felt fibre noise
    img = Image.fromarray(np.clip(a, 0, 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(.8))
    d = ImageDraw.Draw(img)
    # Flap outline in UV space: a downward triangle, top edge at v=1 (image top), tip at v=0 (bottom).
    left, right, top, tip = (14, 14), (S - 14, 14), None, (S // 2, S - 10)
    for k in range(0, 40):                                     # dashed hem stitch along both slanted edges
        for (x0, y0), (x1, y1) in ((left, tip), (right, tip)):
            t0, t1 = k / 40, (k + .55) / 40
            inset = 26 if x0 < S // 2 else -26
            d.line([(x0 + (x1 - x0) * t0 + inset * (1 - t0), y0 + 14 + (y1 - y0 - 30) * t0),
                    (x0 + (x1 - x0) * t1 + inset * (1 - t1), y0 + 14 + (y1 - y0 - 30) * t1)], fill=(205, 120, 105), width=4)
    cream = (238, 228, 210)
    cx, cy = S // 2, int(S * .56)                              # paw print, below the hanging tongue
    d.ellipse([cx - 52, cy + 2, cx + 52, cy + 84], fill=cream)
    d.ellipse([cx - 70, cy + 30, cx - 26, cy + 86], fill=cream); d.ellipse([cx + 26, cy + 30, cx + 70, cy + 86], fill=cream)
    for dx, dy, r in ((-62, -22, 21), (-24, -50, 23), (24, -50, 23), (62, -22, 21)):
        d.ellipse([cx + dx - r, cy + dy - r * 1.15, cx + dx + r, cy + dy + r * 1.15], fill=cream)
    img.filter(ImageFilter.GaussianBlur(.6)).save(ART / 'v11_corgi_bandana.png')



def felt(name, rgb, seed):
    """Seamless felt: periodic multi-scale fibre noise (FFT-filtered, so it tiles) around a base colour."""
    S = 512; rng = np.random.default_rng(seed)
    fy, fx = np.meshgrid(np.fft.fftfreq(S), np.fft.fftfreq(S), indexing='ij')
    f = np.hypot(fx, fy) + 1e-6
    def band(lo, hi, aniso=1.):
        spec = np.fft.fft2(rng.normal(size=(S, S)))
        g = np.hypot(fx * aniso, fy) + 1e-6
        mask = np.exp(-((np.log(g) - np.log((lo + hi) / 2)) ** 2) / .5)
        n = np.real(np.fft.ifft2(spec * mask)); return n / n.std()
    fuzz = .6 * band(.18, .35) + .22 * band(.06, .12, 1.6) + .07 * band(.015, .03)
    shade = 1 + .075 * fuzz
    a = np.clip(np.array(rgb, float)[None, None, :] * shade[..., None], 0, 255)
    Image.fromarray(a.astype(np.uint8)).save(ART / name)


if __name__ == '__main__':
    label()
    bandana()
    felt('v11_corgi_felt_orange.png', (203, 112, 24), 3)
    felt('v11_corgi_felt_cream.png', (232, 210, 172), 4)
    sounds()
    print('ASSETS_COMPLETE')
