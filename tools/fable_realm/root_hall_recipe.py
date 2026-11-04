"""Second authored room: root hall, stream chasm, side routes and an upper landing."""
import json
import math
from library_recipe import ROOT, v, library, navigation_ready


def root_hall():
    elements=[]
    def add(name,kind,pos=(0,0,0),size=(1,1,1),mat='Bark',rot=(0,0,0),collider=True,**kw):
        elements.append(dict(id=name,kind=kind,position=v(*pos),size=v(*size),material=mat,rotation=v(*rot),collider=collider,**kw))
    def box(name,pos,size,mat='Bark',walk=False,collider=True,rot=(0,0,0)):
        add(name,'box',pos,size,mat,rot,collider,navigation='Walkable' if walk else 'Obstacle' if collider else 'Ignore')
    def tube(name,points,radius,mat='Bark',collider=False):
        add(name,'tube',mat=mat,collider=collider,points=[v(*p) for p in points],radius=radius)
    def rail(name,pos,length,yaw=0):
        add(name,'rail',pos,(length,.9,.15),'OakDark',(0,yaw,0),count=max(2,int(length/.45)))
    def lamp(name,x,y,z):
        add(name+'-light','light',(x,y,z),mat='MossGlow',collider=False,intensity=2,range=7)
        for i in range(5):
            a=i*math.tau/5
            tube(name+f'-crystal-{i}',[(x+.25*math.cos(a),y-.3,z+.25*math.sin(a)),(x+.12*math.cos(a),y+.2+i*.07,z+.12*math.sin(a))],.09,'MossGlow')
    # A real transverse crevice. Both perimeter bypasses remain wide enough for foot navigation.
    for i,(x0,x1,z0,z1) in enumerate(((-7.5,-4.5,-7.5,7.5),(4.5,7.5,-7.5,7.5),(-4.5,4.5,-7.5,-1.5),(-4.5,4.5,1.5,7.5))):
        box(f'floor-{i}',((x0+x1)/2,-.2,(z0+z1)/2),(x1-x0,.4,z1-z0),'Moss',True)
    box('crevice-bottom',(0,-3.2,0),(9,.4,3),'Stone')
    for i,z in enumerate((-1.6,1.6)): box(f'crevice-wall-{i}',(0,-1.7,z),(9,2.6,.2),'Bark')
    for i,x in enumerate((-4.6,4.6)): box(f'crevice-end-{i}',(x,-1.7,0),(.2,2.6,3),'Bark')
    box('bridge',(0,-.12,0),(3.2,.24,3.4),'Oak',True)
    for i in range(11): box(f'bridge-board-{i}',(0,.01,-1.55+i*.31),(3.2,.02,.28),'OakLight',collider=False)
    rail('bridge-west',(-1.7,0,0),3.3,90)
    rail('bridge-east',(1.7,0,0),3.3,90)
    for side in (-1,1):
        rail(f'ledge-left-{side}',(-3.15,0,side*1.6),2.6)
        rail(f'ledge-right-{side}',(3.15,0,side*1.6),2.6)
    # All four doors align to the existing 15-metre module grid.
    for side in range(4):
        angle=side*math.pi/2
        def p(x,y,z): return (x*math.cos(angle)+z*math.sin(angle),y,-x*math.sin(angle)+z*math.cos(angle))
        for half in (-1,1):
            box(f'wall-{side}-{half}',p(half*4.8,4,7.35),(5.1,8,.3),'Bark',rot=(0,side*90,0))
        box(f'lintel-{side}',p(0,5.9,7.35),(4.5,4.2,.3),'Bark',rot=(0,side*90,0))
        add(f'door-arch-{side}','arch',p(0,0,7.05),(4.2,4.1,.4),'OakDark',(0,side*90,0),radius=.23)
    # Ceiling opening and branching ribs make this a hollow tree chamber rather than a box.
    for side in (-1,1):
        box(f'ceiling-side-{side}',(side*5.75,8.1,0),(3.5,.25,14.8),'Bark')
        box(f'ceiling-end-{side}',(0,8.1,side*5.75),(8,.25,3.5),'Bark')
    tube('skylight-rim',[(3.8*math.cos(i*math.tau/48),8.05,3.8*math.sin(i*math.tau/48)) for i in range(49)],.23,'OakDark')
    add('skylight','disc',(0,8.3,0),(7.5,7.5,.05),'MoonGlass',(90,0,0),False)
    add('skylight-source','light',(0,6.5,0),mat='MoonGlass',collider=False,intensity=3,range=12,shadows=True)
    for sx in (-1,1):
        for sz in (-1,1):
            rear_offset=1.8 if sz>0 else 0
            tube(f'trunk-{sx}-{sz}',[(sx*6,0,sz*4.8+rear_offset),(sx*5.9,2,sz*4.7+rear_offset),(sx*6.3,4,sz*4.8+rear_offset),
                 (sx*5.2,6.3,sz*4.1),(sx*2.8,7.8,sz*2.7)],.38,collider=True)
            tube(f'trunk-twist-{sx}-{sz}',[(sx*6.4,0,sz*4.7),(sx*5.5,1.9,sz*4.6),(sx*6.1,4.2,sz*4.5),(sx*4.8,6.5,sz*3.9)],.16)
            tube(f'root-to-floor-{sx}-{sz}',[(sx*6,1,sz*4.8),(sx*5.3,.3,sz*4.8),(sx*4.2,.08,sz*5.7)],.2)
    # Upper route: stairs run along the rear wall, clear of east/west ground portals.
    add('root-stair','stairs',(-3.25,0,5.2),(2.6,3,6.5),'Oak',(0,90,0),count=22)
    box('upper-landing',(3.25,2.88,5.2),(6.5,.24,2.6),'Oak',True)
    box('upper-east',(5.2,2.88,2.7),(2.6,.24,2.4),'Oak',True)
    rail('landing-front',(1.95,3,3.85),3.9)
    rail('east-balcony-inner',(3.85,3,2.7),2.4,90)
    rail('east-balcony-end',(5.2,3,1.5),2.6)
    for i,x in enumerate((1.4,5.9)):
        box(f'landing-support-{i}',(x,1.38,5.7),(.3,2.76,.3),'OakDark')
    tube('stair-handhold',[(-6.5,1,3.85),(0,4,3.85)],.06,'Brass')
    # Ground refuge facing south, built into an oversized root buttress.
    box('refuge-back',(3.6,1.25,-3.7),(2.8,2.5,.2),'Bark')
    box('refuge-east',(4.9,1.25,-4.6),(.2,2.5,2),'Bark')
    box('refuge-west',(2.3,1.25,-4.6),(.2,2.5,2),'Bark')
    box('refuge-roof',(3.6,2.6,-4.6),(2.8,.2,2),'Moss')
    add('refuge-arch','arch',(3.6,0,-5.55),(2.5,2.5,.2),'OakDark',radius=.12)
    for i,(x,y,z) in enumerate(((-3.5,1.3,-3.2),(3.5,1.3,2.8),(-5.9,2,1.6),(5.9,2,-1.6),(4.8,4.2,5.7))): lamp(f'glow-{i}',x,y,z)
    add('crevice-light','light',(0,-1.7,0),mat='MossGlow',collider=False,intensity=2,range=6)
    for i in range(15):
        x=math.sin(i*2.4)*3.9;z=math.cos(i*3.2)*.9
        tube(f'fallen-root-{i}',[(x,-3,z),(x+.25,-2.7,z+.2),(x+.4,-2.95,z+.4)],.15,'OakDark')
    nodes={'south':(0,0,-6.5),'hide-entry':(3.6,0,-6.2),'hide-inside':(3.6,0,-4.6),'front':(0,0,-3),'bridge-south':(0,0,-1.8),'bridge':(0,0,0),
           'bridge-north':(0,0,1.8),'back':(0,0,3),'north':(0,0,6.5),
           'east':(6.5,0,0),'east-south':(5.8,0,-2.6),'east-north':(5.8,0,2.7),
           'west':(-6.5,0,0),'west-south':(-5.8,0,-2.7),'west-north':(-5.8,0,2.7),
           'stair-foot':(-6.5,0,5.2),'stair-mid':(-3.25,1.5,5.2),'stair-top':(0,3,5.2),
           'landing':(5.2,3,5.2),'balcony':(5.2,3,2.7)}
    edges=[('south','hide-entry'),('hide-entry','hide-inside'),('south','front'),('front','bridge-south'),('bridge-south','bridge'),('bridge','bridge-north'),
           ('bridge-north','back'),('back','north'),('front','east-south'),('east-south','east'),('east','east-north'),
           ('east-north','back'),('front','west-south'),('west-south','west'),('west','west-north'),('west-north','back'),
           ('west-north','stair-foot'),('stair-foot','stair-mid'),('stair-mid','stair-top'),('stair-top','landing'),('landing','balcony')]
    anchors=[dict(id=name,kind='nav',position=v(*p),connectsTo=[b if a==name else a for a,b in edges if name in (a,b)]) for name,p in nodes.items()]
    for name,p,angle in [('door-north',(0,0,7.5),0),('door-east',(7.5,0,0),90),('door-south',(0,0,-7.5),180),('door-west',(-7.5,0,0),270)]:
        anchors.append(dict(id=name,kind='portal',position=v(*p),rotation=v(0,angle,0),size=v(4.2,3.8,.4)))
    anchors += [dict(id='crevice',kind='pit',position=v(3,0,0),size=v(9,3,3)),
                dict(id='refuge',kind='hide',position=v(3.6,1,-4.6)),
                dict(id='room',kind='volume',position=v(0,2.5,0),size=v(14.8,11,14.8))]
    materials=library()['materials']+[dict(id='Moss',color='#53634a',grain=.18,smoothness=.1)]
    return navigation_ready(dict(schemaVersion=1,id='RootHall',title='Зал переплетённых корней',seed=91,bounds=v(15,12,15),
        materials=materials,elements=elements,anchors=anchors,views=[
            dict(id='entrance',position=v(0,1.65,-6.5),target=v(0,2.8,2.5),fieldOfView=72),
            dict(id='balcony',position=v(5.2,4.65,3),target=v(-1,1,0),fieldOfView=78)]))


if __name__=='__main__':
    recipe=root_hall(); ROOT.mkdir(parents=True,exist_ok=True)
    (ROOT/'RootHall.room.json').write_text(json.dumps(recipe,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    print(recipe['id'],len(recipe['elements']),'elements')
