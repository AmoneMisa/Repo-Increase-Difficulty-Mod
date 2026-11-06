"""python tools/build_skill_point_atlas.py --root .

Skill Point upgrade atlas, same recipe as build_damage_upgrade_atlases.py: teal packaging and a
skill-tree node pictogram (hexagon with a star and a plus badge).
Then render its Icon_ PNG: blender --background --factory-startup --python tools/build_upgrade_art.py -- --root . --atlas Upgrade_Skill-Point.png

(Original notes of the shared recipe:)

Builds the Melee Damage and Grenade Damage upgrade atlases from the checked-in
Upgrade_Gun-Bullet-Count.png wrapper: the packaging is recoloured, the bullet/battery
illustration is cleared from the front panel and replaced by a flat pictogram in the
same cream-ink / chunky-outline style. Only the front panel (x256-438, y296-500 of the
512 px atlas) is repainted; side labels, barcode and UV islands are untouched.
Afterwards run tools/build_upgrade_art.py --atlas <file> to render the Icon_ PNG.
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
source = Image.open(textures / 'Upgrade_Gun-Bullet-Count.png').convert('RGB')

INK = (27, 24, 22, 255)
CREAM = (239, 230, 207, 255)
S = 4  # supersampling factor for the pictogram layer
# Front-panel region repainted (inside the panel's printed border lines).
X0, Y0, X1, Y1 = 259, 296, 436, 504
CLEAN = (259, 238, 436, 296)  # plain packaging print above the old illustration


def recolour(img, hue_deg, sat_scale, val_scale):
    a = np.asarray(img).astype(np.float32) / 255.0
    flat = a.reshape(-1, 3)
    out = flat.copy()
    for i, (r, g, b) in enumerate(flat):
        h, s, v = colorsys.rgb_to_hsv(r, g, b)
        deg = h * 360
        # Packaging orange / its dark-brown flap; leaves cream labels, ink and gold-free areas alone.
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
    """Panel-local coordinates (0..177, 0..208) to supersampled layer pixels."""
    return (x * S, y * S)


def poly(d, pts, fill, outline=6):
    pts = [P(x, y) for x, y in pts]
    d.polygon(pts, fill=INK)
    # Draw an inset fill to produce a chunky even outline.
    cx = sum(p[0] for p in pts) / len(pts)
    cy = sum(p[1] for p in pts) / len(pts)
    inset = []
    for x, y in pts:
        dx, dy = x - cx, y - cy
        L = math.hypot(dx, dy) or 1
        inset.append((x - dx / L * outline * S * .5, y - dy / L * outline * S * .5))
    d.polygon(inset, fill=fill)


def thick_rect(d, box, fill, width=3.2, radius=4):
    x0, y0, x1, y1 = box
    d.rounded_rectangle([P(x0, y0), P(x1, y1)], radius=radius * S, fill=INK)
    w = width
    d.rounded_rectangle([P(x0 + w, y0 + w), P(x1 - w, y1 - w)], radius=max(1, radius - w) * S, fill=fill)


def rotated_rect(cx, cy, w, h, deg):
    a = math.radians(deg)
    ca, sa = math.cos(a), math.sin(a)
    pts = []
    for x, y in ((-w / 2, -h / 2), (w / 2, -h / 2), (w / 2, h / 2), (-w / 2, h / 2)):
        pts.append((cx + x * ca - y * sa, cy + x * sa + y * ca))
    return pts


def outlined_poly(d, pts, fill, width=3.4):
    pp = [P(x, y) for x, y in pts]
    d.polygon(pp, fill=fill)
    d.line(pp + [pp[0]], fill=INK, width=int(width * S), joint='curve')
    for p in pp:
        r = width * S / 2
        d.ellipse([p[0] - r, p[1] - r, p[0] + r, p[1] + r], fill=INK)


def burst(d, cx, cy, r0, r1, n, width=5, rot=0):
    for i in range(n):
        a = math.radians(rot + i * 360 / n)
        p0 = P(cx + math.cos(a) * r0, cy + math.sin(a) * r0)
        p1 = P(cx + math.cos(a) * r1, cy + math.sin(a) * r1)
        d.line([p0, p1], fill=INK, width=int((width + 3) * S))
        d.line([p0, p1], fill=CREAM, width=int(width * S))
        for p, rr in ((p0, (width + 3) / 2), (p1, (width + 3) / 2)):
            pass


def star(d, cx, cy, r_out, r_in, n=8, rot=-90, fill=CREAM):
    pts = []
    for i in range(n * 2):
        r = r_out if i % 2 == 0 else r_in
        a = math.radians(rot + i * 180 / n)
        pts.append((cx + math.cos(a) * r, cy + math.sin(a) * r))
    outlined_poly(d, pts, fill, 3.6)


def up_badge(d, cx, cy):
    """Small cream tag with a dark double chevron: 'more damage'."""
    thick_rect(d, (cx - 22, cy - 22, cx + 22, cy + 22), CREAM, 3.4, 6)
    for dy in (-7, 7):
        pts = [P(cx - 12, cy + dy + 6), P(cx, cy + dy - 6), P(cx + 12, cy + dy + 6)]
        d.line(pts, fill=INK, width=int(5 * S), joint='curve')


def skill_point_pictogram():
    img = layer()
    d = ImageDraw.Draw(img)
    cx, cy = 88, 100
    # Tree node: a chunky hexagon.
    hexagon = [(cx + math.cos(math.radians(90 + i * 60)) * 66, cy + math.sin(math.radians(90 + i * 60)) * 66) for i in range(6)]
    outlined_poly(d, hexagon, CREAM, 6)
    inner = [(cx + math.cos(math.radians(90 + i * 60)) * 50, cy + math.sin(math.radians(90 + i * 60)) * 50) for i in range(6)]
    d.line([P(x, y) for x, y in inner] + [P(*inner[0])], fill=INK, width=int(3 * S), joint='curve')
    star(d, cx, cy + 2, 34, 15, n=5, rot=-90, fill=CREAM)
    # "+1 point" badge in the corner.
    bx, by = 146, 34
    thick_rect(d, (bx - 24, by - 24, bx + 24, by + 24), CREAM, 3.4, 7)
    d.line([P(bx - 12, by), P(bx + 12, by)], fill=INK, width=int(6 * S))
    d.line([P(bx, by - 12), P(bx, by + 12)], fill=INK, width=int(6 * S))
    # Branch lines leaving the node, like the tree's connectors.
    for x0, y0, x1, y1 in ((cx - 66, cy + 70, cx - 30, cy + 48), (cx + 66, cy + 70, cx + 30, cy + 48)):
        d.line([P(x0, y0), P(x1, y1)], fill=INK, width=int(9 * S))
        d.line([P(x0, y0), P(x1, y1)], fill=CREAM, width=int(4 * S))
    return img


def compose(name, hue, sat, val, picto):
    img = recolour(source, hue, sat, val)
    img = clear_front(img)
    art = picto()
    # Drop shadow like the original print.
    shadow = Image.new('RGBA', art.size, (0, 0, 0, 0))
    alpha = art.split()[3].filter(ImageFilter.GaussianBlur(3 * S))
    shadow.putalpha(alpha.point(lambda a: int(a * .35)))
    small_shadow = shadow.resize((X1 - X0, Y1 - Y0), Image.LANCZOS)
    small = art.resize((X1 - X0, Y1 - Y0), Image.LANCZOS)
    # Distressed print: sprinkle a little of the packaging grain back over the ink.
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


compose('Upgrade_Skill-Point.png', 182, 0.8, 0.78, skill_point_pictogram)
