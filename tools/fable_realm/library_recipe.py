"""Authoring recipe for the library showcase and a minimal reusable room example.

Outputs JSON only; the Unity LevelAuthoring compiler owns geometry and validation.
"""
import json
import math
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2] / 'unity/FableRealm/Assets/FableRealm/RoomDefinitions'


def v(x=0,y=0,z=0): return dict(x=x,y=y,z=z)


def navigation_ready(recipe):
    recipe['navigation']=dict(radius=.35,height=1.8,climb=.25,slope=35,voxel=.08,sampleDistance=.25,maxVerticalSnap=.15)
    for e in recipe['elements']:
        if not e.get('collider',True): area='Ignore'
        elif e['kind']=='stairs' or e['id'].startswith('floor') or e['id'] in ('bridge','west-gallery','rear-gallery','east-gallery'): area='Walkable'
        else: area='Obstacle'
        e.setdefault('navigation',area)
    return recipe


def library():
    elements=[]
    def element(name,kind,pos=(0,0,0),size=(1,1,1),mat='Oak',rot=(0,0,0),collider=True,**kwargs):
        elements.append(dict(id=name.replace('.','_'),kind=kind,position=v(*pos),size=v(*size),material=mat,
                             rotation=v(*rot),collider=collider,**kwargs))
    def box(name,pos,size,mat='Oak',collider=True,rot=(0,0,0)):
        element(name,'box',pos,size,mat,rot,collider)
    def tube(name,points,radius,mat='Bark',collider=False):
        element(name,'tube',mat=mat,collider=collider,points=[v(*p) for p in points],radius=radius)
    palette=['Brass','BookWine','BookTeal','BookOchre','BookInk','BookCream']
    def shelf(name,x,y,z,yaw=0,width=2.2,height=2.65):
        element(name,'shelf',(x,y,z),(width,height,.72),'OakDark',(0,yaw,0),count=4,palette=palette)
    def rail(name,x,y,z,length,yaw=0):
        element(name,'rail',(x,y,z),(length,.95,.15),'Brass',(0,yaw,0),count=max(2,int(length/.35)))
    def lamp(name,x,y,z,mat='WarmGlass',intensity=1.8,reach=7):
        box(name+'-mount',(x,y-.3,z),(.45,.12,.45),'Brass',False)
        box(name+'-glass',(x,y,z),(.24,.5,.24),mat,False)
        box(name+'-cap',(x,y+.3,z),(.4,.1,.4),'Brass',False)
        element(name+'-light','light',(x,y,z),mat=mat,collider=False,intensity=intensity,range=reach)

    # Real floor opening. Surface ornament has no collision and never bridges the hole.
    slabs=[(-7.5,-2.5,-7.5,7.5),(2.5,7.5,-7.5,7.5),(-2.5,2.5,-7.5,-.5),(-2.5,2.5,3.5,7.5)]
    for i,(x0,x1,z0,z1) in enumerate(slabs):
        box(f'floor-{i}',((x0+x1)/2,-.16,(z0+z1)/2),(x1-x0,.32,z1-z0))
        for j in range(math.ceil((z1-z0)/.36)):
            depth=min(.34,z1-z0-j*.36)
            if depth>0:
                box(f'plank-{i}-{j}',((x0+x1)/2,.006,z0+j*.36+depth/2),(x1-x0-.02,.012,depth),'Oak' if j%3 else 'OakLight',False)
    box('pit-bottom',(0,-3.36,1.5),(5,.32,4),'Stone')
    for x in (-2.6,2.6): box('pit-side-'+str(x).replace('.','_'),(x,-1.76,1.5),(.2,2.88,4),'Bark')
    for i,z in enumerate((-.6,3.6)): box(f'pit-end-{i}',(0,-1.76,z),(5,2.88,.2),'Bark')
    # A narrow optional bridge; a wide level route remains around the east side.
    box('bridge',(0,-.1,1.5),(5.3,.2,1.8),'OakDark')
    for i in range(16): box(f'bridge-board-{i}',(-2.5+i/3,.015,1.5),(.3,.03,1.8),'OakLight',False)
    rail('bridge-north',0,0,2.45,5.2)
    rail('bridge-south',0,0,.55,5.2)
    rail('pit-front',0,0,-.6,5.1)
    rail('pit-back',0,0,3.6,5.1)
    for side in (-1,1):
        for i,z in enumerate((-.05,3.05)): rail(f'pit-side-rail-{side}-{i}',side*2.6,0,z,.8,90)

    # Full room envelope. The doorway is an actual opening, with a lintel above it.
    box('rear-wall',(0,4,7.35),(14.8,8,.3),'Bark')
    for side in (-1,1):
        box(f'side-wall-{side}',(side*7.35,4,0),(.3,8,14.8),'Bark')
        box(f'front-wall-{side}',(side*4.8,4,-7.35),(5.1,8,.3),'Bark')
    box('door-lintel',(0,5.9,-7.35),(4.5,4.2,.3),'Bark')
    box('ceiling',(0,8.1,0),(14.8,.3,14.8),'OakDark')
    element('entrance-arch','arch',(0,0,-7),(4.2,4.1,.4),'OakDark',radius=.22)
    for side in (-1,1):
        for j in range(18):
            box(f'wall-rib-{side}-{j}',(side*7.13,4,-6.8+j*.8),(.12,8,.05),'OakDark',False)
        for z in (-5,0,5):
            tube(f'trunk-{side}-{z}',[(side*6.9,0,z),(side*6.8,2,z+.2),(side*6.65,4.5,z),
                 (side*6.2,6.4,z),(side*4.5,7.6,z),(side*1.5,7.95,z)],.25)
            tube(f'root-{side}-{z}',[(side*6.9,1,z),(side*6.3,.3,z+.3),(side*5.8,.05,z+.6)],.15)
    for z in (-3.7,1.5,5.6):
        element('ceiling-arch-'+str(z).replace('.','_'),'arch',(0,3.4,z),(12.8,4.5,.3),'OakDark',collider=False,radius=.16)
    # Galleries, with a continuous loop across the rear and an accessible staircase.
    box('west-gallery',(-5.25,2.88,3.2),(3.5,.24,6.8),'OakDark')
    box('rear-gallery',(0,2.88,5.3),(7,.24,2.6),'OakDark')
    box('east-gallery',(5.25,2.88,3.2),(3.5,.24,6.8),'OakDark')
    element('west-stair','stairs',(-5.25,0,-3.2),(3.5,3,6),'Oak',count=20)
    for side in (-1,1):
        rail(f'gallery-inner-{side}',side*3.55,3,1.9,4.2,90)
        for z in (1.0,5.9):
            box(f'column-{side}-{z}',(side*3.7,1.4,z),(.27,2.8,.27),'OakDark')
            box(f'capital-{side}-{z}',(side*3.7,2.65,z),(.5,.25,.5),'Brass',False)
    rail('rear-balustrade',0,3,4,7)
    # Keep the west landing open; east gallery is reached from the rear crossover.
    rail('east-end',5.25,3,-.2,3.5)
    for side in (-1,1):
        tube(f'stair-handrail-{side}',[(-5.25+side*1.62,1,-6.2),(-5.25+side*1.62,4,-.2)],.055,'Brass')
    for i,x in enumerate((-5.4,-2.7,0,2.7,5.4)): shelf(f'back-lower-{i}',x,0,6.72)
    for i,z in enumerate((-4.8,-1.8,1.2,4.2)): shelf(f'east-lower-{i}',6.72,0,z,90)
    for i,z in enumerate((1.4,4.4)): shelf(f'west-lower-{i}',-6.72,0,z,-90)
    for side in (-1,1):
        for i,z in enumerate((1.4,4.4)): shelf(f'upper-side-{side}-{i}',side*6.72,3,z,90*side)
    for i,x in enumerate((-5.1,5.1)): shelf(f'upper-rear-{i}',x,3,6.72,width=2.8)
    # Reading alcove, a real shelter, open toward the entrance and backed by opaque books.
    shelf('reading-niche-back',3.5,0,-4.6,width=2.5,height=2.7)
    box('reading-niche-side',(4.8,1.3,-5.3),(.18,2.6,1.9),'OakDark')
    box('reading-niche-roof',(3.5,2.75,-5.3),(2.8,.18,1.9),'OakDark')
    box('reading-bench',(4.35,.35,-5.35),(.55,.7,1.4),'BookWine')
    # Glowing rose window, framed by concentric carved wood and brass rings.
    element('window-glass','disc',(0,5.5,7.12),(3.45,3.45,.06),'MoonGlass',collider=False)
    for r,mat in ((1.95,'OakDark'),(1.72,'Brass'),(.48,'Brass')):
        tube('window-ring-'+str(r).replace('.','_'),[(r*math.cos(i*math.tau/48),5.5+r*math.sin(i*math.tau/48),7.02) for i in range(49)],.08,mat)
    for i in range(8):
        a=i*math.tau/8
        tube(f'window-ray-{i}',[(.48*math.cos(a),5.5+.48*math.sin(a),7),(1.7*math.cos(a),5.5+1.7*math.sin(a),7)],.035,'Brass')
    # Suspended lantern chandelier; supporting chains stay above all walkways.
    tube('chandelier-chain',[(0,8,1.5),(0,5.5,1.5)],.025,'Brass')
    tube('chandelier-ring',[(1.55*math.cos(i*math.tau/36),5.5,1.5+1.55*math.sin(i*math.tau/36)) for i in range(37)],.06,'Brass')
    for i in range(6):
        a=i*math.tau/6
        lamp(f'chandelier-{i}',1.55*math.cos(a),5.2,1.5+1.55*math.sin(a),intensity=1.1,reach=8)
    for i,(x,y,z) in enumerate(((-2.8,2.2,-6),(2.5,2.2,-6),(-3.8,4.8,4),(3.8,4.8,4),(-5.7,1.8,3),(5.7,1.8,3))):
        lamp(f'wall-lamp-{i}',x,y,z,intensity=1.7,reach=6)
    element('moonlight','light',(0,6,5.8),mat='MoonGlass',collider=False,intensity=2.3,range=10,shadows=True)
    element('pit-glow','light',(0,-2,1.5),mat='MossGlow',collider=False,intensity=1.8,range=5)
    for i in range(9):
        x=math.sin(i*4.1)*1.8; z=1.5+math.cos(i*2.7)*1.3
        box(f'fallen-tome-{i}',(x,-3.05,z),(.55,.24,.8),palette[1+i%5],rot=(0,i*37,0))

    nodes={
        'entry':(0,0,-6.5),'hide-entry':(3.5,0,-6.2),'hide-inside':(3.5,0,-5.4),'front':(0,0,-3.7),'west-foot':(-5.25,0,-6.2),
        'west-mid':(-5.25,1.5,-3.2),'west-top':(-5.25,3,-.2),
        'upper-west':(-5.25,3,5.2),'upper-rear':(0,3,5.2),'upper-east':(5.25,3,5.2),
        'upper-east-end':(5.25,3,.5),'east-front':(1.8,0,-3.7),'east-turn':(4.8,0,-2.6),
        'east-bridge':(4.8,0,1.5),'east-back':(4.8,0,4.8),'back':(0,0,4.8),
        'west-back':(-4.8,0,4.8),'west-bridge':(-4.8,0,1.5),'bridge':(0,0,1.5),
    }
    edges=[('entry','hide-entry'),('hide-entry','hide-inside'),('entry','front'),('entry','west-foot'),('west-foot','west-mid'),('west-mid','west-top'),
           ('west-top','upper-west'),('upper-west','upper-rear'),('upper-rear','upper-east'),('upper-east','upper-east-end'),
           ('front','east-front'),('east-front','east-turn'),('east-turn','east-bridge'),('east-bridge','east-back'),
           ('east-back','back'),('back','west-back'),('west-back','west-bridge'),('west-bridge','bridge'),('bridge','east-bridge')]
    anchors=[dict(id=name,kind='nav',position=v(*p),connectsTo=[b if a==name else a for a,b in edges if name in (a,b)]) for name,p in nodes.items()]
    anchors += [dict(id='south',kind='portal',position=v(0,0,-7.5),rotation=v(0,180,0),size=v(4.2,3.8,.4)),
                dict(id='reading-hide',kind='hide',position=v(3.5,1,-5.4)),
                dict(id='open-pit',kind='pit',position=v(0,0,0),size=v(5,3.2,4)),
                dict(id='room-volume',kind='volume',position=v(0,2.3,0),size=v(14.6,11,14.6)),
                dict(id='upper-loot',kind='loot',position=v(5.8,3.5,4.4),size=v(.5,.5,.5))]
    surfaces=[('Oak','#896047',.26),('OakLight','#a77d57',.22),('OakDark','#48322c',.3),('Bark','#625047',.35),
              ('Stone','#343b40',.1),('Brass','#c59b58',.12),('BookWine','#743f4d',.1),('BookTeal','#366364',.1),
              ('BookOchre','#bd8a49',.1),('BookInk','#394351',.1),('BookCream','#c5b398',.1),
              ('WarmGlass','#ffd294',0),('MoonGlass','#69afbe',0),('MossGlow','#7db98f',0)]
    materials=[dict(id=name,color=color,grain=grain,smoothness=.25,metallic=.55 if name=='Brass' else 0,
                    emission=1.4 if name.endswith('Glass') else .4 if name=='MossGlow' else 0) for name,color,grain in surfaces]
    return dict(schemaVersion=1,id='HollowLibrary',title='Библиотека забытых сказок',seed=73,bounds=v(15,12,15),
                materials=materials,elements=elements,anchors=anchors,views=[
                    dict(id='entrance',position=v(0,1.65,-6.5),target=v(0,3.1,3),fieldOfView=72),
                    dict(id='gallery',position=v(-5.15,4.65,1),target=v(1.2,1.9,3.5),fieldOfView=75),
                    dict(id='reading-niche',position=v(1.8,1.65,-6.5),target=v(4,1.4,-4.4),fieldOfView=65)])


def example():
    return dict(schemaVersion=1,id='ExampleRoom',title='Minimal room template',seed=1,bounds=v(15,5,15),
                materials=[dict(id='Stone',color='#737b86')],elements=[
                    dict(id='floor',kind='box',material='Stone',position=v(0,-.2,0),size=v(15,.4,15),collider=True),
                    dict(id='back-wall',kind='box',material='Stone',position=v(0,2,7.2),size=v(15,4,.4),collider=True)],
                anchors=[dict(id='entry',kind='nav',position=v(0,0,-6),connectsTo=['middle']),
                         dict(id='middle',kind='nav',position=v(),connectsTo=['entry']),
                         dict(id='south',kind='portal',position=v(0,0,-7.5),rotation=v(0,180,0))],
                views=[dict(id='entrance',position=v(0,1.65,-6.5),target=v(0,1.65,3),fieldOfView=65)])


if __name__=='__main__':
    ROOT.mkdir(parents=True,exist_ok=True)
    for recipe in (navigation_ready(library()),navigation_ready(example())):
        path=ROOT/(recipe['id']+'.room.json')
        path.write_text(json.dumps(recipe,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
        print(path.name,len(recipe['elements']),'elements')
