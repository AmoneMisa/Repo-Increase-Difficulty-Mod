"""Original flat ceramic decoration for the reference-14 approval sculpt.

The supplied photograph is used as a visual reference, never pasted on the mesh.
Run with system Python (Pillow); then build_maneki14.py with Blender.
"""
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont
import math

OUT = Path(__file__).resolve().parents[1] / 'art/valuables/maneki-reference-14/textures'
OUT.mkdir(parents=True, exist_ok=True)
N = 2048

def texture(name, box, strokes, polygons=()):
    im = Image.new('RGB', (N, N), (247, 245, 234))
    d = ImageDraw.Draw(im)
    x0, y0, x1, y1 = box
    def p(pt): return ((pt[0]-x0)/(x1-x0)*N, (pt[1]-y0)/(y1-y0)*N)
    for pts, color in polygons:
        d.polygon([p(q) for q in pts], fill=color)
    for pts, width, color in strokes:
        points = [p(q) for q in pts]
        w = round(width/(x1-x0)*N)
        d.line(points, fill=color, width=w, joint='curve')
        for x, y in [points[0],points[-1]]:
            d.ellipse((x-w/2,y-w/2,x+w/2,y+w/2),fill=color)
    im.save(OUT/name)

def bez(a,b,c,e,n=60):
    return [tuple((1-t)**3*a[k]+3*(1-t)**2*t*b[k]+3*(1-t)*t*t*c[k]+t**3*e[k] for k in (0,1)) for t in [i/n for i in range(n+1)]]

ink=(26,23,19); rose=(192,91,94); red=(183,14,16)
strokes=[]
for q in [((86,82),(91,69),(107,67),(117,79)), ((151,74),(158,60),(175,60),(182,72))]:
    strokes.append((bez(*q),2.65,ink))
for q in [((66,97),(72,99),(77,100),(83,99)),((66,108),(73,110),(79,110),(85,109)),((69,118),(75,119),(80,119),(85,117)),((188,88),(194,87),(201,86),(205,83)),((188,97),(194,98),(201,96),(205,95)),((188,106),(194,108),(200,108),(205,106))]:
    strokes.append((bez(*q),1.55,ink))
for a,b in [((84,57),(87,63)),((94,52),(95,58)),((168,41),(168,49)),((180,43),(177,50))]:
    strokes.append(([a,b],1.65,ink))
strokes += [(bez((135,90),(134,95),(136,98),(135,99)),1.9,rose),
            (bez((135,98),(131,104),(127,104),(124,101)),2,rose),
            (bez((135,98),(138,103),(143,103),(147,99)),2,rose)]
nose=bez((129,85),(132,82),(140,82),(141,86))+bez((141,86),(138,91),(136,94),(134,93))+bez((134,93),(131,91),(128,88),(129,85))
left=bez((69,30),(67,35),(68,45),(73,52))+bez((73,52),(77,52),(87,42),(88,39))+bez((88,39),(84,34),(76,28),(69,30))
right=bez((166,27),(170,19),(176,11),(181,12))+bez((181,12),(188,14),(190,28),(190,34))+bez((190,34),(183,39),(172,31),(166,27))
texture('head_painted.png',(48,0,220,168),strokes,[(nose,rose),(left,red),(right,red)])
texture('paw_painted.png',(208,58,250,103),[([a,b],1.6,ink) for a,b in [((220,74),(220,82)),((229,73),(229,82)),((238,75),(237,83))]])

# The medallion is a gold relief, not a HUD label. Rasterize a local CJK glyph;
# no system font file is copied or distributed with the asset.
im=Image.new('RGB',(1024,1024),(188,127,27))
d=ImageDraw.Draw(im)
d.ellipse((30,30,994,994), fill=(227,175,57),outline=(151,92,15),width=23)
d.ellipse((75,75,949,949),outline=(244,206,103),width=8)
font=ImageFont.truetype('C:/Windows/Fonts/simsun.ttc',650)
d.text((512,496),'福',font=font,anchor='mm',fill=(133,82,13),stroke_width=0)
im.save(OUT/'medallion_gold.png')
print(OUT)
