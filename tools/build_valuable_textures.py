"""Original coarse, painted texture tiles. No reference photos are copied into textures."""
from pathlib import Path
from PIL import Image,ImageDraw
import random
ROOT=Path(__file__).resolve().parents[1];OUT=ROOT/'art/valuables/medical-tech-pack-06/textures';OUT.mkdir(parents=True,exist_ok=True)
PALETTE={'olive':(89,91,48),'yellow':(199,157,38),'red':(145,43,43),'dark':(34,40,36),
'steel':(119,132,125),'bone':(194,184,145),'white':(176,185,168),'gold':(164,122,44),
'pink':(158,61,103),'purple':(91,69,126),'cyan':(69,143,141),'green':(50,94,64),'orange':(182,99,44),'glass':(175,192,181)}
for k,(name,base) in enumerate(PALETTE.items()):
    rng=random.Random(620+k);im=Image.new('RGB',(128,128));px=im.load()
    for y in range(128):
        for x in range(128):
            shade=rng.choice([.94,.97,1,1.015])
            px[x,y]=tuple(min(255,int(v*shade)) for v in base)
    d=ImageDraw.Draw(im)
    for i in range(47):
        x,y=rng.randrange(128),rng.randrange(128);w,h=rng.randrange(2,9),rng.randrange(1,4)
        c=tuple(int(v*.62) for v in base) if i%3 else (78,66,45)
        d.rectangle((x,y,x+w,y+h),fill=c)
        if i%3==0:d.line((x,y,x+w//2,y),fill=tuple(min(255,v+37) for v in base),width=1)
    for i in range(15):
        x,y=rng.randrange(128),rng.randrange(128)
        d.line((x,y,x+rng.randrange(3,11),y+1),fill=tuple(min(255,v+26) for v in base),width=1)
    im.save(OUT/f'loot_{name}.png')

# Original stylized fairy labels for the pink bath set, with no copied brand artwork or text.
for i,c in enumerate([(189,91,124),(136,91,160),(192,124,63)]):
    im=Image.new('RGB',(128,160),c);d=ImageDraw.Draw(im)
    for y in range(160):
        t=y/160;d.line((0,y,127,y),fill=tuple(int(v*(.8+.3*t)) for v in c))
    for x,y in [(12,22),(100,32),(20,120),(108,137),(65,12)]:
        d.polygon([(x,y-5),(x+2,y-1),(x+6,y),(x+2,y+2),(x,y+6),(x-2,y+2),(x-6,y),(x-2,y-1)],fill=(232,204,140))
    d.ellipse((21,39,66,89),fill=(151,201,178),outline=(231,221,174),width=2)
    d.ellipse((63,39,108,89),fill=(173,184,220),outline=(231,221,174),width=2)
    d.ellipse((31,73,68,113),fill=(183,150,192),outline=(231,221,174),width=2)
    d.ellipse((61,73,98,113),fill=(141,201,199),outline=(231,221,174),width=2)
    d.ellipse((49,32,78,65),fill=(179,115,55));d.ellipse((54,39,73,60),fill=(235,196,139))
    d.polygon([(58,63),(70,63),(77,109),(52,109)],fill=(226,156,124))
    d.line((61,108,56,139),fill=(235,196,139),width=4);d.line((69,108,78,139),fill=(235,196,139),width=4)
    d.rectangle((10,145,118,153),fill=(223,204,174));im.save(OUT/f'loot_fairy_{i}.png')
print('VALUABLE_TEXTURES_CREATED',OUT)
