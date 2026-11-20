"""Draws the three Fable Realm loading-screen layers in the style of the game's own level cards.

The game shows a level as three 2252 x 1128 sprites on top of each other (LoadingUI.loadingGraphic01-03) and
slides them in before the truck crashes into the front one. Vanilla keeps one colour scheme for all of them:
white-to-grey buildings with heavy black outlines in front, near-black scenery in the middle, a glowing moon
and dark scenery behind. These layers keep that scheme with Fable Realm's own motifs:

  01 front   a castle and a gingerbread cottage standing on a giant open storybook, the beanstalk growing
             out of its spine (the truck crashes into the cottage, where it hits the vanilla houses)
  02 middle  a storybook forest: round-crowned trees and toadstools
  03 back    a sleeping crescent moon in a nightcap, stars, and the giant's castle on a cloud

Run:  python tools/fable_realm/build_loading_art.py [preview.png]
Writes src/FableRealm/Loading/fable_loading_0{1,2,3}.png (embedded in the DLL) and, if given, a preview of the
three layers composed on the loading screen's black.
"""
import math
import random
import sys
from pathlib import Path

import numpy as np
from PIL import Image, ImageChops, ImageDraw

REPO = Path(__file__).resolve().parents[2]
OUT = REPO / 'src' / 'FableRealm' / 'Loading'
W, H = 2252, 1128          # vanilla sprite size; the sprite fills the same rect as the level it replaces
S = 3                      # supersampling: drawn at 3x, reduced with a Lanczos filter
LINE = 7                   # outline width in final pixels (vanilla: about 6-8)

WHITE, LIGHT, MID, DARK, INK = (255, 255, 255), (203, 204, 203), (101, 102, 101), (58, 58, 58), (0, 0, 0)
SCENERY = (16, 17, 16)     # vanilla tree colour


# ---- drawing helpers (all coordinates in final pixels) ---------------------------------------------------
class Layer:
    def __init__(self):
        self.img = Image.new('RGBA', (W*S, H*S), (0, 0, 0, 0))

    def mask(self, points=None, ellipse=None):
        m = Image.new('L', self.img.size, 0)
        d = ImageDraw.Draw(m)
        if points: d.polygon([(x*S, y*S) for x, y in points], fill=255)
        if ellipse: d.ellipse([c*S for c in ellipse], fill=255)
        return m

    def fill(self, m, top, bottom=None, horizontal=False):
        """Paints a gradient through a mask. Vertical (top to bottom) by default, or left to right."""
        box = m.getbbox()
        if box is None: return
        bottom = bottom or top
        x0, y0, x1, y1 = box
        size = (x1-x0, y1-y0)
        ramp = Image.linear_gradient('L')          # 0 at the top, 255 at the bottom
        if horizontal: ramp = ramp.rotate(90)      # 0 on the left
        a = Image.new('RGBA', size, top+(255,)); b = Image.new('RGBA', size, bottom+(255,))
        grad = Image.composite(b, a, ramp.resize(size, Image.BILINEAR))
        layer = Image.new('RGBA', self.img.size, (0, 0, 0, 0))
        layer.paste(grad, (x0, y0))
        self.img = Image.composite(layer, self.img, m)

    def poly(self, points, top, bottom=None, horizontal=False, outline=LINE, color=INK):
        self.fill(self.mask(points), top, bottom, horizontal)
        if outline: self.line(points + [points[0]], outline, color)

    def ellipse(self, box, top, bottom=None, horizontal=False, outline=LINE, color=INK):
        self.fill(self.mask(ellipse=box), top, bottom, horizontal)
        if outline:
            ImageDraw.Draw(self.img).ellipse([c*S for c in box], outline=color+(255,), width=int(outline*S))

    def line(self, points, width, color=INK, alpha=255):
        d = ImageDraw.Draw(self.img)
        pts = [(x*S, y*S) for x, y in points]
        d.line(pts, fill=color+(alpha,), width=int(width*S), joint='curve')
        r = width*S/2
        for x, y in (pts[0], pts[-1]): d.ellipse([x-r, y-r, x+r, y+r], fill=color+(alpha,))

    def glow(self, cx, cy, inner, outer, color=(206, 190, 226), strength=.85):
        """Soft radial halo like the vanilla moon's."""
        size = int(outer*2*S)
        c = (size-1)/2
        yy, xx = np.mgrid[0:size, 0:size]
        d = np.hypot(xx-c, yy-c)/(size/2)                       # 0 at the centre, 1 at the outer radius
        t = np.clip((1-d)/(1-inner/outer), 0, 1)
        alpha = Image.fromarray((255*strength*t**1.6).astype(np.uint8), 'L')
        glow = Image.new('RGBA', (size, size), color+(0,)); glow.putalpha(alpha)
        layer = Image.new('RGBA', self.img.size, (0, 0, 0, 0))
        layer.paste(glow, (int(cx*S-c), int(cy*S-c)))
        self.img = Image.alpha_composite(self.img, layer)

    def erase(self, points=None, ellipse=None):
        m = self.mask(points, ellipse)
        alpha = ImageChops.subtract(self.img.getchannel('A'), m)
        self.img.putalpha(alpha)

    def save(self, path):
        out = self.img.resize((W, H), Image.LANCZOS)
        # Grey plus alpha is all the art uses; it keeps the embedded files small.
        r, g, b, a = out.split()
        out = Image.merge('LA', (out.convert('L'), a))
        out.save(path, optimize=True)
        return out


def bezier(p0, p1, p2, p3, n=40):
    pts = []
    for i in range(n+1):
        t = i/n; u = 1-t
        pts.append((u*u*u*p0[0]+3*u*u*t*p1[0]+3*u*t*t*p2[0]+t*t*t*p3[0],
                    u*u*u*p0[1]+3*u*u*t*p1[1]+3*u*t*t*p2[1]+t*t*t*p3[1]))
    return pts


def chain(*segments):
    pts = []
    for seg in segments: pts += bezier(*seg)[1 if pts else 0:]
    return pts


def offset(path, widths):
    """Left and right edges of a stroke along `path` with per-point widths."""
    left, right = [], []
    for i, (x, y) in enumerate(path):
        a = path[max(i-1, 0)]; b = path[min(i+1, len(path)-1)]
        dx, dy = b[0]-a[0], b[1]-a[1]; n = math.hypot(dx, dy) or 1
        nx, ny = -dy/n, dx/n; w = widths[i]/2
        left.append((x+nx*w, y+ny*w)); right.append((x-nx*w, y-ny*w))
    return left, right


def arch(x0, x1, y_top, y_bottom, n=16):
    """Window or door with a round top."""
    r = (x1-x0)/2; cx = (x0+x1)/2
    pts = [(x0, y_bottom), (x0, y_top+r)]
    pts += [(cx-r*math.cos(math.pi*i/n), y_top+r-r*math.sin(math.pi*i/n)) for i in range(1, n)]
    return pts + [(x1, y_top+r), (x1, y_bottom)]


def leaf(x, y, angle, length, width):
    """Bean leaf: a pointed teardrop from (x, y) along `angle` (degrees, 0 = right)."""
    a = math.radians(angle); ca, sa = math.cos(a), math.sin(a)
    side = []
    for i in range(21):
        u = i/20; w = width*math.sin(math.pi*u)**.75*(1-.35*u)
        side.append((u*length, w))
    pts = [(px, py) for px, py in side] + [(px, -py) for px, py in reversed(side)]
    return [(x+px*ca-py*sa, y+px*sa+py*ca) for px, py in pts]


# ---- layer 01: castle and cottage on an open storybook --------------------------------------------------
def front():
    L = Layer()
    rnd = random.Random(14)
    spine_x, ground = 1280, 978

    # Beanstalk out of the spine, behind everything else on this layer.
    stalk = chain(((spine_x, 950), (1215, 790), (1345, 700), (1300, 585)),
                  ((1300, 585), (1255, 470), (1195, 395), (1255, 295)),
                  ((1255, 295), (1310, 205), (1345, 150), (1295, 52)))
    widths = [44 - 26*i/(len(stalk)-1) for i in range(len(stalk))]
    lft, rgt = offset(stalk, widths)
    for i, (t, side, size) in enumerate(((.22, 1, 1.0), (.33, -1, .9), (.47, 1, .85), (.58, -1, .8), (.7, 1, .7),
                                         (.8, -1, .62), (.9, 1, .5))):
        k = int(t*(len(stalk)-1)); x, y = stalk[k]
        base = 200 if side < 0 else -20
        pts = leaf(x, y, base + side*rnd.uniform(-15, 5) - (25 if side > 0 else -25), 120*size, 44*size)
        L.poly(pts, WHITE, MID, horizontal=True)
        tip = pts[20]
        L.line([(x, y), ((x*2+tip[0])/3, (y*2+tip[1])/3 - 6), tip], 3.5)
    L.poly(lft + list(reversed(rgt)), WHITE, MID, horizontal=True)
    # Curling tendrils.
    for (x, y, d) in ((1300, 585, 1), (1255, 295, -1), (1240, 780, -1)):
        curl = [(x + d*(18 + 34*math.cos(a))*(1 - a/9), y - 30 + 34*math.sin(a)*(1 - a/9)) for a in [i*.25 for i in range(26)]]
        L.line([(x, y)] + curl, 4)

    # Castle on the right page.
    base = 920
    L.poly([(1365, 690), (1585, 690), (1585, base), (1365, base)], WHITE, MID, horizontal=True)
    for i in range(6):
        x = 1365 + i*40
        L.poly([(x, 664), (x+24, 664), (x+24, 692), (x, 692)], WHITE, LIGHT, horizontal=True, outline=6)
    for (x0, x1, top, roof, peak) in ((1318, 1394, 600, 470, 1356), (1558, 1632, 640, 520, 1595)):
        L.poly([(x0, top), (x1, top), (x1, base), (x0, base)], WHITE, MID, horizontal=True)
        L.poly([(x0-14, top+4), (peak, roof), (x1+14, top+4)], (24, 24, 24), (110, 110, 110), horizontal=True)
        L.line([(x0-6, top-2), (peak-4, roof+12)], 3, WHITE)
        for y in (top+48, top+150):
            L.poly(arch(peak-13, peak+13, y, y+52), DARK, INK, outline=5)
    L.poly([(1428, 520), (1517, 520), (1517, 692), (1428, 692)], WHITE, MID, horizontal=True)
    L.poly([(1414, 524), (1472, 352), (1531, 524)], (24, 24, 24), (110, 110, 110), horizontal=True)
    L.line([(1422, 518), (1468, 366)], 3, WHITE)
    L.line([(1472, 352), (1472, 300)], 5)
    L.poly([(1475, 300), (1530, 314), (1475, 330)], WHITE, LIGHT, outline=5)
    L.ellipse((1447, 560, 1497, 610), DARK, INK, outline=6)
    L.line([(1472, 566), (1472, 604)], 4, WHITE); L.line([(1453, 585), (1491, 585)], 4, WHITE)
    for x in (1390+12, 1530):
        L.poly(arch(x, x+30, 730, 790), DARK, INK, outline=5)
    # Gate with a portcullis.
    L.poly(arch(1440, 1510, 822, base), (70, 70, 70), (30, 30, 30))
    for x in (1452, 1466, 1480, 1494): L.line([(x, 842), (x, base)], 3)
    for y in (858, 884): L.line([(1444, y), (1506, y)], 3)
    for _ in range(14):
        x, y = rnd.uniform(1375, 1570), rnd.uniform(705, 900)
        if 1430 < x < 1520 and y > 810: continue
        L.poly([(x, y), (x+22, y), (x+22, y+11), (x, y+11)], LIGHT, None, outline=0)

    # Gingerbread cottage on the left page.
    base = 912
    L.poly([(1052, 690), (1088, 690), (1088, 760), (1052, 760)], WHITE, MID, horizontal=True)
    L.poly([(1046, 676), (1094, 676), (1094, 694), (1046, 694)], WHITE, LIGHT, horizontal=True, outline=6)
    L.poly([(905, 790), (1105, 790), (1105, base), (905, base)], WHITE, MID, horizontal=True)
    L.poly([(872, 808), (1005, 652), (1138, 808)], (24, 24, 24), (110, 110, 110), horizontal=True)
    # Icing along the eaves: a white band with drips.
    eave = [(872, 808), (1005, 652), (1138, 808)]
    L.line(eave, 16); L.line(eave, 7, WHITE)
    drips = Layer()
    for i in range(9):
        x = 884 + i*29
        drips.poly([(x-10, 808), (x+10, 808), (x+7, 822+(i % 3)*7), (x, 830+(i % 3)*7), (x-7, 822+(i % 3)*7)], WHITE, outline=0)
    L.img = Image.alpha_composite(L.img, drips.img)
    L.line([(872, 808), (1138, 808)], 6)
    for x, y in ((960, 735), (1005, 700), (1050, 735)):
        L.ellipse((x-13, y-13, x+13, y+13), WHITE, LIGHT, outline=5)
    L.ellipse((930, 820, 978, 868), DARK, INK, outline=6)
    L.line([(954, 824), (954, 864)], 4, WHITE); L.line([(934, 844), (974, 844)], 4, WHITE)
    L.poly(arch(990, 1040, 838, base), (80, 80, 80), (30, 30, 30))
    L.ellipse((1027, 872, 1035, 880), WHITE, outline=0)
    L.poly([(1058, 824), (1092, 824), (1092, 860), (1058, 860)], DARK, INK, outline=6)
    L.line([(1075, 826), (1075, 858)], 4, WHITE)
    for y in (878, 896): L.line([(907, y), (985, y)], 3, MID); L.line([(1045, y), (1103, y)], 3, MID)
    # Lollipop and candy cane.
    L.line([(868, base), (868, 846)], 12); L.line([(868, base), (868, 846)], 5, WHITE)
    L.ellipse((838, 790, 898, 850), WHITE, LIGHT)
    spiral = [(868 + r*math.cos(a), 820 + r*math.sin(a)) for a, r in ((i*.3, 3 + i*.95) for i in range(28))]
    L.line(spiral, 4)
    cane = [(1150, base)] + [(1150, 840)] + [(1170 - 20*math.cos(a), 840 - 20*math.sin(a)) for a in [i*math.pi/12 for i in range(13)]]
    L.line(cane, 20); L.line(cane, 11, WHITE)
    for y in (900, 878, 856): L.line([(1144, y), (1156, y-9)], 5, MID)

    # The open book: two page blocks bending into the spine, on a dark cover.
    cover = [(792, 952), (spine_x, 968), (1768, 952), (1768, 972), (spine_x, ground), (792, 972)]
    L.poly(cover, (70, 70, 70), (30, 30, 30))
    for side in (-1, 1):
        top = bezier((spine_x, 930), (spine_x + side*190, 878), (spine_x + side*330, 878), (spine_x + side*470, 900))
        bottom = bezier((spine_x + side*470, 954), (spine_x + side*300, 960), (spine_x + side*120, 966), (spine_x, 968))
        L.poly(top + bottom, WHITE, LIGHT)
        for k in (.3, .55, .78):
            mid = [(a[0], a[1]*(1-k) + b[1]*k) for a, b in zip(top, reversed(bottom))]
            L.line(mid[2:-2], 2.5, MID)
        L.line(top, LINE)
    L.line([(spine_x, 930), (spine_x, 968)], 5)
    return L


# ---- layer 02: storybook forest ---------------------------------------------------------------------------
def middle():
    L = Layer()
    rnd = random.Random(7)
    top_shade, foot = (28, 29, 28), (8, 8, 8)
    xs = [40, 170, 290, 420, 560, 700, 1820, 1950, 2080, 2200, 860, 1700, 1010, 1560]
    for i, x in enumerate(xs):
        x += rnd.uniform(-25, 25); ground = 1010 + rnd.uniform(-20, 15)
        if i % 4 == 3:
            # Toadstool: domed cap with pale spots, on a fat stem.
            h, r = rnd.uniform(170, 240), rnd.uniform(70, 100)
            L.poly([(x-r*.22, ground), (x-r*.17, ground-h), (x+r*.17, ground-h), (x+r*.22, ground)], top_shade, foot, outline=0)
            cap = [(x + r*math.cos(a), ground - h + 8 - r*.85*math.sin(a)) for a in [math.pi*j/30 for j in range(31)]]
            L.poly(cap, top_shade, SCENERY, outline=0)
            for _ in range(4):
                sx, sy = x + rnd.uniform(-.6, .6)*r, ground - h - rnd.uniform(.15, .6)*r*.85
                L.ellipse((sx-9, sy-6, sx+9, sy+6), (40, 41, 40), outline=0)
            continue
        h = rnd.uniform(260, 470); trunk = rnd.uniform(16, 24)
        bend = rnd.uniform(-30, 30)
        L.poly([(x-trunk, ground), (x-trunk*.6+bend, ground-h*.7), (x+trunk*.6+bend, ground-h*.7), (x+trunk, ground)], top_shade, foot, outline=0)
        cx, cy, r = x+bend, ground-h*.72, rnd.uniform(85, 130)
        for k in range(6):
            a = k/6*math.tau + rnd.uniform(-.3, .3)
            rr = r*rnd.uniform(.55, .75)
            ox, oy = cx + math.cos(a)*r*.55, cy + math.sin(a)*r*.45
            L.ellipse((ox-rr, oy-rr, ox+rr, oy+rr), top_shade, SCENERY, outline=0)
        L.ellipse((cx-r*.7, cy-r*.7, cx+r*.7, cy+r*.7), top_shade, SCENERY, outline=0)
    return L


# ---- layer 03: sleeping moon, stars and the giant's castle on a cloud ------------------------------------
def back():
    L = Layer()
    rnd = random.Random(3)
    mx, my, r = 1680, 300, 104
    L.glow(mx, my, r*1.05, r*2.75)
    moon = Layer()
    moon.ellipse((mx-r, my-r, mx+r, my+r), WHITE, (236, 236, 236), outline=0)
    moon.erase(ellipse=(mx-r*1.38, my-r*1.12, mx+r*.42, my+r*.68))
    L.img = Image.alpha_composite(L.img, moon.img)
    # Closed eye and a small smile on the inner edge, a nightcap on the upper horn.
    L.line([(mx+54, my-14), (mx+66, my-6), (mx+80, my-12)], 5)
    L.line([(mx+30, my+48), (mx+44, my+56), (mx+58, my+50)], 4)
    cap = [(mx-30, my-92), (mx+40, my-118), (mx-40, my-196)]
    L.poly(cap, (70, 70, 70), (24, 24, 24), outline=6)
    L.line([(mx-34, my-90), (mx+44, my-120)], 12, WHITE)
    L.ellipse((mx-58, my-216, mx-26, my-184), WHITE, LIGHT, outline=5)
    # Stars: four-pointed sparkles.
    for x, y, s in ((210, 120, 16), (520, 70, 10), (860, 150, 14), (1080, 60, 9), (1350, 200, 11), (1990, 110, 15),
                    (2140, 260, 9), (1500, 90, 8), (720, 300, 8), (1930, 420, 10), (120, 380, 9), (1180, 330, 7)):
        L.glow(x, y, s*.4, s*2.4, (220, 220, 230), .45)
        star = [(x, y-s*1.6), (x+s*.32, y-s*.32), (x+s*1.6, y), (x+s*.32, y+s*.32), (x, y+s*1.6), (x-s*.32, y+s*.32),
                (x-s*1.6, y), (x-s*.32, y-s*.32)]
        L.poly(star, WHITE, outline=0)
    # The giant's castle on its cloud, far behind on the left.
    c = (24, 24, 24)
    for x0, x1, top, peak in ((300, 360, 260, 190), (520, 575, 280, 215)):
        L.poly([(x0, top), (x1, top), (x1, 400), (x0, 400)], c, SCENERY, outline=0)
        L.poly([(x0-10, top+2), ((x0+x1)/2, peak), (x1+10, top+2)], c, SCENERY, outline=0)
    L.poly([(350, 300), (530, 300), (530, 400), (350, 400)], c, SCENERY, outline=0)
    for i in range(5): L.poly([(352+i*38, 284), (374+i*38, 284), (374+i*38, 302), (352+i*38, 302)], c, outline=0)
    L.poly([(410, 230), (470, 230), (470, 300), (410, 300)], c, SCENERY, outline=0)
    L.poly([(402, 232), (440, 150), (478, 232)], c, SCENERY, outline=0)
    for (x, y) in ((328, 300), (546, 320), (440, 255)):
        L.poly(arch(x-7, x+7, y, y+26), (210, 210, 210), (150, 150, 150), outline=0)
    def cloud(cx, cy, scale):
        puffs = ((-1.0, .15, .42), (-.55, -.12, .55), (0, -.28, .7), (.55, -.1, .55), (1.0, .15, .4))
        rim, body = Layer(), Layer()
        for dx, dy, rr in puffs:
            x, y, rr = cx+dx*scale, cy+dy*scale, rr*scale
            rim.ellipse((x-rr, y-rr, x+rr, y+rr), WHITE, outline=0)
            body.ellipse((x-rr+3, y-rr+9, x+rr-3, y+rr+9), INK, outline=0)
        flat = [(cx-1.45*scale, cy+.35*scale), (cx+1.45*scale, cy+.35*scale), (cx+1.45*scale, cy+.6*scale), (cx-1.45*scale, cy+.6*scale)]
        rim.erase(flat); body.erase(flat)
        return Image.alpha_composite(rim.img, body.img)
    for cx, cy, sc in ((440, 410, 150), (1060, 245, 70), (2010, 520, 80), (160, 560, 60)):
        L.img = Image.alpha_composite(L.img, cloud(cx, cy, sc))
    return L


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    layers = {}
    for n, build in (('03', back), ('02', middle), ('01', front)):
        layers[n] = build().save(OUT / f'fable_loading_{n}.png')
        print(f'fable_loading_{n}.png  {(OUT / f"fable_loading_{n}.png").stat().st_size // 1024} KB')
    if len(sys.argv) > 1:
        preview = Image.new('RGBA', (W, H), (0, 0, 0, 255))
        for n in ('03', '02', '01'): preview.alpha_composite(layers[n].convert('RGBA'))
        preview.save(sys.argv[1])


if __name__ == '__main__':
    main()
