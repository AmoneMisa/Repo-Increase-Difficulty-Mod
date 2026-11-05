"""python tools/build_damage_upgrade_atlases.py --root .

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


def melee_pictogram():
    img = layer()
    d = ImageDraw.Draw(img)
    # Impact burst behind the bat's head.
    star(d, 118, 62, 50, 28, 9, rot=-80)
    # Baseball bat, diagonal, tan wood with a dark grip tape.
    wood = (204, 158, 96, 255)
    grip = (70, 58, 52, 255)
    angle = -52
    # Barrel (thick end) towards the impact, handle towards the bottom left.
    cx, cy = 74, 118
    a = math.radians(angle)
    ux, uy = math.cos(a), math.sin(a)       # along the bat
    nx, ny = -uy, ux                         # across the bat
    def along(t, half):
        return [(cx + ux * t + nx * half, cy + uy * t + ny * half), (cx + ux * t - nx * half, cy + uy * t - ny * half)]
    profile = [(-78, 6.5), (-70, 7), (-30, 8), (0, 12), (40, 19), (72, 20)]
    left = [along(t, h)[0] for t, h in profile]
    right = [along(t, h)[1] for t, h in reversed(profile)]
    outlined_poly(d, left + right, wood, 3.6)
    # Rounded barrel end cap.
    ex, ey = cx + ux * 72, cy + uy * 72
    r = 20
    d.ellipse([P(ex - r, ey - r), P(ex + r, ey + r)], fill=INK)
    d.ellipse([P(ex - r + 3.6, ey - r + 3.6), P(ex + r - 3.6, ey + r - 3.6)], fill=wood)
    # Grip tape and knob.
    grip_pts = [along(-74, 7.5)[0], along(-38, 8.5)[0], along(-38, 8.5)[1], along(-74, 7.5)[1]]
    outlined_poly(d, grip_pts, grip, 3)
    kx, ky = cx + ux * -80, cy + uy * -80
    d.ellipse([P(kx - 11, ky - 11), P(kx + 11, ky + 11)], fill=INK)
    d.ellipse([P(kx - 7.5, ky - 7.5), P(kx + 7.5, ky + 7.5)], fill=grip)
    # Wood grain highlight lines.
    for off, t0, t1 in ((5, -20, 50), (-6, 0, 62)):
        p0 = (cx + ux * t0 + nx * off, cy + uy * t0 + ny * off)
        p1 = (cx + ux * t1 + nx * off * 1.4, cy + uy * t1 + ny * off * 1.4)
        d.line([P(*p0), P(*p1)], fill=(236, 205, 150, 255), width=int(2.4 * S))
    up_badge(d, 146, 176)
    return img


def grenade_pictogram():
    img = layer()
    d = ImageDraw.Draw(img)
    burst(d, 128, 56, 26, 44, 7, width=5, rot=-100)
    star(d, 128, 56, 24, 13, 7, rot=-90)
    body = (128, 136, 82, 255)
    dark = (88, 96, 58, 255)
    cx, cy, rx, ry = 76, 128, 46, 54
    d.ellipse([P(cx - rx, cy - ry), P(cx + rx, cy + ry)], fill=INK)
    d.ellipse([P(cx - rx + 3.8, cy - ry + 3.8), P(cx + rx - 3.8, cy + ry - 3.8)], fill=body)
    # Pineapple segments.
    for k in (-1, 0, 1):
        x = cx + k * 21
        half = math.sqrt(max(0, 1 - ((x - cx) / rx) ** 2)) * ry - 6
        d.line([P(x, cy - half), P(x, cy + half)], fill=dark, width=int(4 * S))
    for k in (-1, 0, 1):
        y = cy + k * 24
        half = math.sqrt(max(0, 1 - ((y - cy) / ry) ** 2)) * rx - 6
        d.line([P(cx - half, y), P(cx + half, y)], fill=dark, width=int(4 * S))
    # Highlight.
    d.ellipse([P(cx - 30, cy - 38), P(cx - 16, cy - 18)], fill=(176, 184, 124, 255))
    # Fuse head, spoon/lever and pin ring.
    thick_rect(d, (cx - 17, cy - ry - 18, cx + 17, cy - ry + 6), (170, 170, 160, 255), 3.4, 3)
    lever = [(cx + 8, cy - ry - 14), (cx + 40, cy - ry - 6), (cx + 48, cy - ry + 30), (cx + 40, cy - ry + 32),
             (cx + 32, cy - ry + 2), (cx + 6, cy - ry - 4)]
    outlined_poly(d, lever, (190, 190, 178, 255), 3.4)
    rr = 13
    rx0, ry0 = cx - 26, cy - ry - 22
    d.ellipse([P(rx0 - rr, ry0 - rr), P(rx0 + rr, ry0 + rr)], outline=INK, width=int(7.4 * S))
    d.ellipse([P(rx0 - rr + 2, ry0 - rr + 2), P(rx0 + rr - 2, ry0 + rr - 2)], outline=CREAM, width=int(3.4 * S))
    up_badge(d, 146, 176)
    return img


def armor_pictogram():
    """Riveted steel shield with a raised centre plate: 'take less damage'."""
    img = layer()
    d = ImageDraw.Draw(img)
    steel = (150, 158, 168, 255)
    plate = (196, 204, 214, 255)
    cx, top, bottom, half = 84, 22, 186, 64
    shield = [(cx - half, top + 14), (cx, top), (cx + half, top + 14), (cx + half - 2, top + 80),
              (cx + half - 18, top + 124), (cx, bottom - top), (cx - half + 18, top + 124), (cx - half + 2, top + 80)]
    outlined_poly(d, shield, steel, 4.2)
    inner = [(cx - 38, top + 30), (cx, top + 20), (cx + 38, top + 30), (cx + 36, top + 82), (cx + 24, top + 112),
             (cx, top + 130), (cx - 24, top + 112), (cx - 36, top + 82)]
    outlined_poly(d, inner, plate, 3.2)
    d.line([P(cx, top + 26), P(cx, top + 124)], fill=INK, width=int(3 * S))          # centre ridge
    d.line([P(cx - 30, top + 66), P(cx + 30, top + 66)], fill=INK, width=int(3 * S))  # cross band
    for rx, ry in ((cx - 50, top + 26), (cx + 50, top + 26), (cx - 48, top + 84), (cx + 48, top + 84), (cx, top + 142)):
        d.ellipse([P(rx - 5, ry - 5), P(rx + 5, ry + 5)], fill=INK)
        d.ellipse([P(rx - 2.6, ry - 2.6), P(rx + 2.6, ry + 2.6)], fill=CREAM)
    # Glancing-blow chips on the rim.
    for x0, y0, x1, y1 in ((cx + 40, top + 6, cx + 62, top - 6), (cx - 66, top + 100, cx - 84, top + 92)):
        d.line([P(x0, y0), P(x1, y1)], fill=INK, width=int(6 * S)); d.line([P(x0, y0), P(x1, y1)], fill=CREAM, width=int(3 * S))
    up_badge(d, 146, 176)
    return img


def overcharge_pictogram():
    """Grabber claw holding a crackling bolt: 'hold monsters longer'."""
    img = layer()
    d = ImageDraw.Draw(img)
    claw = (120, 126, 136, 255)
    bolt = (255, 214, 70, 255)
    # Lightning bolt held in the claw.
    outlined_poly(d, [(96, 18), (56, 98), (86, 98), (66, 168), (124, 80), (92, 80), (118, 18)], bolt, 4)
    # Two claw fingers closing around it.
    for side in (-1, 1):
        x = 84 + side * 46
        outlined_poly(d, [(x - 10, 128), (x + 10, 128), (x + 14 - side * 4, 82), (x - side * 20, 62), (x - side * 30, 70),
                          (x - side * 6, 90), (x - 10, 128)], claw, 3.6)
    thick_rect(d, (34, 126, 134, 150), claw, 3.6, 6)          # claw palm
    thick_rect(d, (70, 150, 98, 196), (90, 96, 104, 255), 3.4, 4)  # wrist
    # Charge sparks.
    for a in (-150, -30, 200, -95):
        r = math.radians(a)
        x, y = 84 + math.cos(r) * 70, 96 + math.sin(r) * 70
        d.line([P(x, y), P(x + math.cos(r) * 14, y + math.sin(r) * 14)], fill=INK, width=int(7 * S))
        d.line([P(x, y), P(x + math.cos(r) * 14, y + math.sin(r) * 14)], fill=bolt, width=int(3.6 * S))
    up_badge(d, 146, 176)
    return img


def tumble_pictogram():
    """Curled-up tumbling robot with speed lines slamming into an impact star: 'tumble launch damage'."""
    img = layer()
    d = ImageDraw.Draw(img)
    star(d, 132, 64, 40, 22, 9, rot=-70)
    body = (214, 128, 64, 255)
    # Speed lines trailing up-left.
    for i, (x0, y0, length) in enumerate(((10, 70, 40), (14, 96, 52), (24, 122, 36))):
        d.line([P(x0, y0), P(x0 + length, y0 + 10)], fill=INK, width=int(7 * S))
        d.line([P(x0, y0), P(x0 + length, y0 + 10)], fill=CREAM, width=int(3.4 * S))
    # Round tumbling body with a visor, tucked limbs, spin arrow.
    cx, cy, r = 88, 108, 40
    d.ellipse([P(cx - r, cy - r), P(cx + r, cy + r)], fill=INK)
    d.ellipse([P(cx - r + 4, cy - r + 4), P(cx + r - 4, cy + r - 4)], fill=body)
    outlined_poly(d, [(cx - 6, cy - 30), (cx + 24, cy - 22), (cx + 26, cy - 6), (cx - 4, cy - 10)], (60, 70, 80, 255), 3)
    for ax, ay in ((cx - 30, cy + 26), (cx + 20, cy + 34)):
        d.ellipse([P(ax - 11, ay - 11), P(ax + 11, ay + 11)], fill=INK)
        d.ellipse([P(ax - 7, ay - 7), P(ax + 7, ay + 7)], fill=body)
    d.arc([P(cx - r - 14, cy - r - 14), P(cx + r + 14, cy + r + 14)], start=200, end=320, fill=INK, width=int(7 * S))
    d.arc([P(cx - r - 14, cy - r - 14), P(cx + r + 14, cy + r + 14)], start=202, end=318, fill=CREAM, width=int(3.4 * S))
    up_badge(d, 146, 176)
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


compose('Upgrade_Melee-Damage.png', 352, 0.85, 0.82, melee_pictogram)
compose('Upgrade_Grenade-Damage.png', 80, 0.48, 0.62, grenade_pictogram)
compose('Upgrade_Armor.png', 215, 0.55, 0.7, armor_pictogram)
compose('Upgrade_Overcharge.png', 48, 0.9, 0.95, overcharge_pictogram)
compose('Upgrade_Tumble-Launch.png', 18, 0.95, 0.9, tumble_pictogram)