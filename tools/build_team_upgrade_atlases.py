"""python tools/build_team_upgrade_atlases.py --root .

Builds the six team shop upgrade atlases (src/Textures/Upgrade_Team_*.png) the same way as the Skill Point atlas
(tools/build_skill_point_atlas.py): the old bullet-gun wrapper in art/upgrade-reference is recoloured per upgrade,
its front panel is cleared and repainted with a flat cream-ink pictogram with chunky outlines. Side labels, barcode
and UV islands are untouched. Then render the Icon_ PNGs:
blender --background --factory-startup --python tools/build_upgrade_art.py -- --root . --atlas Upgrade_Team_Haggling.png
(once per atlas).
"""
import argparse
import colorsys
import math
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFilter

parser = argparse.ArgumentParser()
parser.add_argument('--root', type=Path, required=True)
args = parser.parse_args()
textures = args.root.resolve() / 'src' / 'Textures'
source = Image.open(args.root.resolve() / 'art' / 'upgrade-reference' / 'Upgrade_Gun-Bullet-Count.png').convert('RGB')

INK = (27, 24, 22, 255)
CREAM = (239, 230, 207, 255)
S = 4
X0, Y0, X1, Y1 = 259, 296, 436, 504
CLEAN = (259, 238, 436, 296)


def recolour(img, hue_deg, sat_scale, val_scale):
    a = np.asarray(img).astype(np.float32) / 255.0
    flat = a.reshape(-1, 3)
    out = flat.copy()
    for i, (r, g, b) in enumerate(flat):
        h, s, v = colorsys.rgb_to_hsv(r, g, b)
        deg = h * 360
        if s > 0.35 and (deg < 45 or deg > 350):
            out[i] = colorsys.hsv_to_rgb(hue_deg / 360, min(1, s * sat_scale), min(1, v * val_scale))
    return Image.fromarray((out.reshape(a.shape) * 255).clip(0, 255).astype(np.uint8), 'RGB')


def clear_front(img):
    patch = img.crop(CLEAN)
    flipped = patch.transpose(Image.FLIP_TOP_BOTTOM)
    y, k = Y0, 0
    while y < Y1:
        tile = patch if k % 2 == 0 else flipped
        img.paste(tile.crop((0, 0, X1 - X0, min(tile.height, Y1 - y))), (X0, y))
        y += tile.height
        k += 1
    return img


def layer():
    return Image.new('RGBA', ((X1 - X0) * S, (Y1 - Y0) * S), (0, 0, 0, 0))


def P(x, y):
    return (x * S, y * S)


def outlined_poly(d, pts, fill, width=3.4):
    pp = [P(x, y) for x, y in pts]
    d.polygon(pp, fill=fill)
    d.line(pp + [pp[0]], fill=INK, width=int(width * S), joint='curve')
    for p in pp:
        r = width * S / 2
        d.ellipse([p[0] - r, p[1] - r, p[0] + r, p[1] + r], fill=INK)


def thick_rect(d, box, fill, width=3.2, radius=4):
    x0, y0, x1, y1 = box
    d.rounded_rectangle([P(x0, y0), P(x1, y1)], radius=radius * S, fill=INK)
    w = width
    d.rounded_rectangle([P(x0 + w, y0 + w), P(x1 - w, y1 - w)], radius=max(1, radius - w) * S, fill=fill)


def disc(d, cx, cy, r, fill, width=3.4):
    d.ellipse([P(cx - r, cy - r), P(cx + r, cy + r)], fill=INK)
    d.ellipse([P(cx - r + width, cy - r + width), P(cx + r - width, cy + r - width)], fill=fill)


def bar(d, x0, y0, x1, y1, width=6):
    d.line([P(x0, y0), P(x1, y1)], fill=INK, width=int(width * S))


def cream_bar(d, x0, y0, x1, y1, width=5):
    d.line([P(x0, y0), P(x1, y1)], fill=INK, width=int((width + 4) * S))
    d.line([P(x0, y0), P(x1, y1)], fill=CREAM, width=int(width * S))


def badge(d, cx, cy, text):
    """Corner tag with a short label such as +1 or +2, drawn as strokes (no font dependency)."""
    thick_rect(d, (cx - 26, cy - 22, cx + 26, cy + 22), CREAM, 3.4, 7)
    # plus sign
    bar(d, cx - 18, cy, cx - 4, cy, 5)
    bar(d, cx - 11, cy - 7, cx - 11, cy + 7, 5)
    digit(d, cx + 10, cy, text)


def digit(d, cx, cy, ch):
    w, h = 6, 10
    segs = {
        '1': [((0, -h), (0, h))],
        '2': [((-w, -h), (w, -h)), ((w, -h), (w, 0)), ((w, 0), (-w, 0)), ((-w, 0), (-w, h)), ((-w, h), (w, h))],
        '%': [((-w, h), (w, -h))],
    }[ch]
    for (a, b) in segs:
        bar(d, cx + a[0], cy + a[1], cx + b[0], cy + b[1], 4.5)
    if ch == '%':
        for ox, oy in ((-w + 1, -h + 3), (w - 1, h - 3)):
            d.ellipse([P(cx + ox - 3, cy + oy - 3), P(cx + ox + 3, cy + oy + 3)], fill=INK)


# ---------------------------------------------------------------- pictograms (panel 177 x 208)

def haggling():
    img = layer(); d = ImageDraw.Draw(img)
    # Robot head
    thick_rect(d, (34, 54, 142, 138), CREAM, 4.2, 14)
    disc(d, 64, 92, 12, INK, 3); disc(d, 112, 92, 12, INK, 3)
    d.ellipse([P(60, 88), P(68, 96)], fill=CREAM); d.ellipse([P(108, 88), P(116, 96)], fill=CREAM)
    bar(d, 66, 118, 110, 118, 5)
    bar(d, 88, 54, 88, 36, 5); disc(d, 88, 32, 7, CREAM, 3)
    # Price tag with a % hanging off the side
    tag = [(96, 140), (150, 140), (166, 160), (150, 180), (96, 180)]
    outlined_poly(d, tag, CREAM, 3.6)
    d.ellipse([P(150, 156), P(158, 164)], fill=INK)
    digit(d, 122, 160, '%')
    bar(d, 34, 160, 70, 160, 6); outlined_poly(d, [(28, 160), (46, 148), (46, 172)], INK, 2)  # down arrow pointing at price
    return img


def crystal(d, cx, cy, s):
    pts = [(cx, cy - 34 * s), (cx + 15 * s, cy - 14 * s), (cx + 11 * s, cy + 26 * s), (cx - 11 * s, cy + 26 * s), (cx - 15 * s, cy - 14 * s)]
    outlined_poly(d, pts, CREAM, 3.6)
    bar(d, cx, cy - 34 * s, cx, cy + 26 * s, 2.6)
    bar(d, cx - 15 * s, cy - 14 * s, cx + 15 * s, cy - 14 * s, 2.6)


def crystal_stock():
    img = layer(); d = ImageDraw.Draw(img)
    crystal(d, 56, 122, 1.0); crystal(d, 120, 122, 1.0); crystal(d, 88, 112, 1.25)
    # Shelf under them
    thick_rect(d, (20, 152, 158, 166), CREAM, 3.2, 3)
    # Up arrow (more stock)
    outlined_poly(d, [(146, 24), (168, 50), (156, 50), (156, 78), (136, 78), (136, 50), (124, 50)], CREAM, 3.4)
    return img


def spare_gun():
    img = layer(); d = ImageDraw.Draw(img)
    gun = [(24, 92), (128, 92), (128, 84), (146, 84), (146, 110), (88, 110), (80, 150), (54, 150), (60, 110), (24, 110)]
    outlined_poly(d, gun, CREAM, 4)
    bar(d, 92, 110, 100, 126, 4); bar(d, 100, 126, 82, 126, 4)  # trigger guard
    badge(d, 136, 40, '1')
    return img


def spare_melee():
    img = layer(); d = ImageDraw.Draw(img)
    # A bat across the panel
    a = math.radians(-34)
    cx, cy = 74, 128
    def rot(x, y):
        return (cx + x * math.cos(a) - y * math.sin(a), cy + x * math.sin(a) + y * math.cos(a))
    body = [rot(-64, -6), rot(-8, -9), rot(56, -15), rot(62, 0), rot(56, 15), rot(-8, 9), rot(-64, 6)]
    outlined_poly(d, body, CREAM, 4)
    knob = rot(-68, 0)
    disc(d, knob[0], knob[1], 9, CREAM, 3.4)
    for t in (-48, -36, -24):  # grip tape
        p0, p1 = rot(t, -6), rot(t + 4, 6)
        bar(d, p0[0], p0[1], p1[0], p1[1], 2.6)
    badge(d, 136, 40, '1')
    return img


def spare_drone():
    img = layer(); d = ImageDraw.Draw(img)
    cx, cy = 84, 118
    for ox, oy in ((-46, -30), (46, -30), (-46, 30), (46, 30)):
        cream_bar(d, cx, cy, cx + ox, cy + oy, 6)
    for ox, oy in ((-46, -30), (46, -30), (-46, 30), (46, 30)):
        d.ellipse([P(cx + ox - 22, cy + oy - 6), P(cx + ox + 22, cy + oy + 6)], fill=INK)
        d.ellipse([P(cx + ox - 19, cy + oy - 3), P(cx + ox + 19, cy + oy + 3)], fill=CREAM)
    disc(d, cx, cy, 24, CREAM, 4)
    disc(d, cx, cy, 9, INK, 3)
    badge(d, 136, 40, '1')
    return img


def upgrade_shelf():
    img = layer(); d = ImageDraw.Draw(img)
    # Rack: two shelves with little upgrade boxes
    thick_rect(d, (20, 60, 30, 176), CREAM, 3, 2)
    thick_rect(d, (148, 60, 158, 176), CREAM, 3, 2)
    for y in (110, 168):
        thick_rect(d, (16, y, 162, y + 10), CREAM, 3, 2)
    for i, x in enumerate((38, 72, 106)):
        thick_rect(d, (x, 80, x + 26, 110), CREAM, 3.2, 3)
        bar(d, x + 5, 90, x + 21, 90, 3)
        thick_rect(d, (x, 138, x + 26, 168), CREAM, 3.2, 3)
        bar(d, x + 5, 148, x + 21, 148, 3)
    badge(d, 136, 34, '2')
    return img


def compose(name, hue, sat, val, picto):
    img = recolour(source, hue, sat, val)
    img = clear_front(img)
    art = picto()
    shadow = Image.new('RGBA', art.size, (0, 0, 0, 0))
    alpha = art.split()[3].filter(ImageFilter.GaussianBlur(3 * S))
    shadow.putalpha(alpha.point(lambda a: int(a * .35)))
    small_shadow = shadow.resize((X1 - X0, Y1 - Y0), Image.LANCZOS)
    small = art.resize((X1 - X0, Y1 - Y0), Image.LANCZOS)
    rng = np.random.default_rng(7)
    a = np.asarray(small).copy()
    mask = rng.random(a.shape[:2]) < 0.05
    a[..., 3] = np.where(mask, (a[..., 3] * 0.55).astype(np.uint8), a[..., 3])
    small = Image.fromarray(a, 'RGBA')
    base = img.convert('RGBA')
    base.alpha_composite(small_shadow, (X0 + 2, Y0 + 3))
    base.alpha_composite(small, (X0, Y0))
    base.convert('RGB').save(textures / name)
    print('wrote', textures / name)


compose('Upgrade_Team_Haggling.png', 285, 0.75, 0.80, haggling)          # plum
compose('Upgrade_Team_Crystal-Stock.png', 215, 0.85, 0.82, crystal_stock)  # cobalt
compose('Upgrade_Team_Spare-Gun.png', 75, 0.70, 0.72, spare_gun)          # olive
compose('Upgrade_Team_Spare-Melee.png', 355, 0.90, 0.75, spare_melee)     # crimson
compose('Upgrade_Team_Spare-Drone.png', 165, 0.75, 0.70, spare_drone)     # teal-mint
compose('Upgrade_Team_Upgrade-Shelf.png', 320, 0.70, 0.80, upgrade_shelf) # rose
