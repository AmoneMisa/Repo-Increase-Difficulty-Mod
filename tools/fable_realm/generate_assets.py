"""Original, deterministic modelling source. Python standard library only.

Run from any directory. Writes only its own named OBJ/MTL/JSON outputs.
These are static environment assets, not registered REPO modules.
"""
import json
import math
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
ART = ROOT / 'unity/FableRealm/Assets/FableRealm'
MODELS = ART / 'Models'
TAU = math.tau
PALETTE = {
    'Bark': ('503950', 0.8), 'Leaves': ('527f71', 0.9),
    'Moss': ('6f8a68', 0.9), 'Ivory': ('e4d5b4', 0.7),
    'Stone': ('9396ac', 0.9), 'Biscuit': ('b67343', 0.9),
    'Icing': ('f7d5cf', 0.6), 'Roof': ('944b6b', 0.8),
    'Gold': ('c99452', 0.3), 'Glass': ('edb86e', 0.3),
    'Mushroom': ('af709e', 0.8), 'Glow': ('8be5cf', 0.4),
    'Rose': ('b64c75', 0.7), 'Earth': ('686b65', 1.0),
}


class Mesh:
    def __init__(self):
        self.vertices, self.faces = [], []

    def part(self, vertices, faces, material):
        offset = len(self.vertices) + 1
        self.vertices.extend(vertices)
        self.faces.extend((material, tuple(offset + i for i in face)) for face in faces)

    def box(self, center, size, material):
        x, y, z = center
        a, b, c = (v / 2 for v in size)
        self.part([(x+dx*a, y+dy*b, z+dz*c) for dx, dy, dz in
                   [(-1,-1,-1),(1,-1,-1),(1,1,-1),(-1,1,-1),
                    (-1,-1,1),(1,-1,1),(1,1,1),(-1,1,1)]],
                  [(0,3,2,1),(4,5,6,7),(0,4,7,3),(1,2,6,5),(0,1,5,4),(3,7,6,2)], material)

    def lathe(self, profile, material, center=(0,0,0), segments=20):
        # Profile is (radius, height), ordered bottom to top, or closed around a cross-section.
        verts = [(center[0]+r*math.cos(TAU*j/segments), center[1]+y,
                  center[2]+r*math.sin(TAU*j/segments))
                 for r, y in profile for j in range(segments)]
        faces = []
        for row in range(len(profile)-1):
            for j in range(segments):
                a=row*segments+j; b=row*segments+(j+1)%segments
                faces.append((a, a+segments, b+segments, b))
        self.part(verts, faces, material)

    def tube(self, points, radii, material, sides=9):
        verts=[]
        for i, p in enumerate(points):
            prev=points[max(0,i-1)]; nxt=points[min(len(points)-1,i+1)]
            tangent=unit(sub(nxt,prev))
            axis=(0,1,0) if abs(tangent[1]) < .95 else (1,0,0)
            u=unit(cross(tangent,axis)); v=cross(tangent,u)
            for j in range(sides):
                a=TAU*j/sides
                verts.append(tuple(p[k]+radii[i]*(u[k]*math.cos(a)+v[k]*math.sin(a)) for k in range(3)))
        faces=[]
        for i in range(len(points)-1):
            for j in range(sides):
                a=i*sides+j; b=i*sides+(j+1)%sides
                faces.append((a,b,b+sides,a+sides))
        faces += [tuple(reversed(range(sides))), tuple((len(points)-1)*sides+j for j in range(sides))]
        self.part(verts,faces,material)

    def save(self, name):
        assert self.vertices and self.faces
        lines=['# Original Fable Realm environment mesh; units: metres; Y up.', 'mtllib FablePalette.mtl', 'o '+name]
        lines += ['v %.6f %.6f %.6f' % p for p in self.vertices]
        # Planar UVs provide an editable baseline; painted hero assets still need dedicated UVs.
        lines += ['vt %.6f %.6f' % (p[0]*.25,p[2]*.25) for p in self.vertices]
        current=None
        for mat, face in self.faces:
            if mat != current:
                lines.append('usemtl '+mat); current=mat
            lines.append('f '+' '.join(f'{i}/{i}' for i in face))
        (MODELS/(name+'.obj')).write_text('\n'.join(lines)+'\n',encoding='utf-8')
        return {'name':name,'vertices':len(self.vertices),'polygons':len(self.faces)}


def sub(a,b): return tuple(x-y for x,y in zip(a,b))
def cross(a,b): return (a[1]*b[2]-a[2]*b[1],a[2]*b[0]-a[0]*b[2],a[0]*b[1]-a[1]*b[0])
def unit(a):
    n=math.sqrt(sum(v*v for v in a))
    assert n > 0
    return tuple(v/n for v in a)


def tree():
    m=Mesh()
    m.tube([(0,0,0),(.1,1,0),(-.2,2,.1),(.2,3,.15),(.45,4,.1),(.3,5,.3)], [.7,.5,.4,.32,.2,.06],'Bark',12)
    for i in range(7):
        a=i*TAU/7
        c,s=math.cos(a),math.sin(a)
        m.tube([(c*1.9,.06,s*1.9),(c*1.1,.2,s*1.1),(0,.7,0)],[.08,.2,.45],'Bark')
        m.tube([(0,2.4+i*.24,0),(c*.9,3.4+i*.2,s*.9),(c*1.7,4+i*.2,s*1.7),(c*1.55,4.7+i*.2,s*1.55)],[.22,.16,.09,.02],'Bark')
        m.lathe([(0,0),(.7,.12),(1.15,.45),(.85,.9),(0,1.2)],'Leaves',(c*1.55,4.1+i*.2,s*1.55),12)
    return m


def mushroom():
    m=Mesh()
    m.lathe([(0,0),(.3,0),(.22,.4),(.16,1.3),(.3,1.5),(0,1.5)],'Ivory')
    m.lathe([(0,1.35),(.65,1.35),(1,1.5),(.9,1.75),(.65,2.0),(.1,2.15),(0,2.15)],'Mushroom')
    m.lathe([(.2,1.4),(.8,1.4),(.9,1.45)],'Glow')
    for i in range(9):
        a=i*TAU/9
        m.lathe([(0,0),(.08,.02),(0,.07)],'Icing',(.66*math.cos(a),1.98,.66*math.sin(a)),8)
    return m


def arch():
    m=Mesh()
    for x in (-2,2):
        m.box((x,1.5,0),(.65,3,.75),'Stone')
        for y in (.15,1,2,2.9): m.box((x,y,0),(.8,.18,.9),'Ivory')
    points=[(2*math.cos(i*math.pi/18),3+1.4*math.sin(i*math.pi/18),0) for i in range(19)]
    m.tube(points,[.36]*19,'Ivory',6)
    m.tube([(-2.2,0,.45),(-1.8,1,.48),(-2.2,2,.45),(-1.7,3,.45),(-.9,4.15,.45)],[.09]*5,'Moss')
    return m


def cottage():
    m=Mesh()
    # Open front doorway: this mesh can be entered once fitted with gameplay colliders.
    m.box((-1.65,1.5,0),(1.3,3,.35),'Biscuit'); m.box((1.65,1.5,0),(1.3,3,.35),'Biscuit')
    m.box((0,2.8,0),(2,.4,.35),'Biscuit')
    for x in (-2.3,2.3): m.box((x,1.5,1.8),(.35,3,3.6),'Biscuit')
    m.box((0,1.5,3.6),(4.6,3,.35),'Biscuit')
    m.part([(-2.7,3,-.4),(2.7,3,-.4),(0,4.8,-.4),(-2.7,3,4),(2.7,3,4),(0,4.8,4)],
           [(0,2,1),(3,4,5),(0,3,5,2),(2,5,4,1),(0,1,4,3)],'Roof')
    for z in (-.43,4.03):
        m.tube([(-2.7,3,z),(0,4.8,z),(2.7,3,z)],[.15]*3,'Icing')
    for x in (-1.65,1.65):
        m.box((x,1.7,-.2),(.65,.9,.1),'Glass')
        for dx in (-.4,.4): m.box((x+dx,1.7,-.29),(.1,1.2,.13),'Icing')
        for y in (1.15,2.25): m.box((x,y,-.29),(.9,.1,.13),'Icing')
    for i in range(17):
        x=-2.6+i*.325
        m.tube([(x,3.02,-.45),(x,2.7-.15*math.sin(i),-.45)],[.11,.04],'Icing')
    for x in (-1.05,1.05):
        m.tube([(x,0,-.3),(x,2.6,-.3)],[.12,.12],'Icing')
    m.box((1.3,4,2.7),(.6,1.7,.6),'Biscuit')
    m.box((1.3,4.85,2.7),(.8,.2,.8),'Icing')
    return m


def cup():
    m=Mesh()
    m.lathe([(0,.1),(1,.1),(1.2,.4),(1.6,2),(1.55,2.1),(1.42,2.05),(1.05,.45),(0,.35)],'Ivory',segments=32)
    m.lathe([(0,0),(2.1,0),(2.3,.15),(2.1,.23),(0,.23)],'Ivory',segments=32)
    for y in (.5,1.8):
        r=1.2+(y-.4)*.25
        m.lathe([(r,y),(r+.025,y+.08),(r,y+.12)],'Gold',segments=32)
    pts=[(1.45+.95*math.sin(i*math.pi/20),1.15+.75*math.cos(i*math.pi/20),0) for i in range(21)]
    m.tube(pts,[.14]*len(pts),'Gold')
    return m


def rose():
    m=Mesh()
    m.tube([(0,0,0),(.12,.7,0),(0,1.5,0)],[.06,.05,.03],'Moss')
    for i in range(7):
        a=i*TAU/7
        m.lathe([(0,0),(.25,.05),(.3,.18),(.12,.35),(0,.32)],'Rose',(.2*math.cos(a),1.4,.2*math.sin(a)),8)
    m.lathe([(0,0),(.2,.05),(.12,.35),(0,.4)],'Rose',(0,1.48,0),9)
    return m


def lantern():
    m=Mesh()
    m.box((0,.9,0),(.42,.7,.42),'Glass')
    for x in (-.25,.25):
        for z in (-.25,.25): m.box((x,.9,z),(.07,.85,.07),'Gold')
    m.lathe([(0,0),(.4,0),(.4,.1),(.12,.3),(.1,.5)],'Gold',segments=4)
    m.lathe([(.4,1.3),(.4,1.4),(.07,1.7),(0,1.7)],'Gold',segments=4)
    m.tube([(0,1.7,0),(0,1.95,0),(.15,2,0),(.25,1.9,0)],[.04]*4,'Gold')
    return m


def tower():
    m=Mesh()
    m.lathe([(0,0),(1.4,0),(1.3,.3),(1, .5),(1,5),(1.3,5.2),(1.3,5.4),(0,5.4)],'Ivory')
    m.lathe([(0,5.4),(1.7,5.4),(1.65,5.6),(0,8)],'Roof')
    for y in (.5,2.5,4.8): m.lathe([(1.01,y),(1.08,y+.04),(1.08,y+.16),(1.01,y+.2)],'Gold')
    for i in range(4):
        a=TAU*i/4
        m.box((math.cos(a)*1.01,3.7,math.sin(a)*1.01),(.28,1,.28),'Glass')
    return m


ROOMS = [
    ('01','BookGate','Врата раскрытой книги','Forest','Start','Arch'),
    ('02','RootHall','Зал переплетённых корней','Forest','Normal','Tree'),
    ('03','MushroomChoir','Грибной хор','Forest','Normal','Mushroom'),
    ('04','FireflyBrook','Ручей светлячков','Forest','Passage','Lantern'),
    ('05','LostPicnic','Забытый пикник','Forest','Normal','GiantCup'),
    ('06','HollowLibrary','Библиотека в дупле','Forest','DeadEnd','Tree'),
    ('07','SugarGate','Сахарная застава','Village','Passage','Arch'),
    ('08','GingerbreadSquare','Пряничная площадь','Village','Normal','Cottage'),
    ('09','BakersWorkshop','Мастерская пекаря','Village','Normal','Cottage'),
    ('10','CandyMarket','Конфетный рынок','Village','Normal','Lantern'),
    ('11','Clockmaker','Дом часовщика','Village','DeadEnd','Cottage'),
    ('12','MarzipanVault','Марципановая кладовая','Village','Extraction','Arch'),
    ('13','BeanstalkCourt','Двор бобового стебля','Giant','Normal','Tree'),
    ('14','GiantTea','Чаепитие великана','Giant','Normal','GiantCup'),
    ('15','PorcelainWalk','Фарфоровая галерея','Giant','Passage','GiantCup'),
    ('16','RoseMaze','Розовый лабиринт','Giant','Normal','Rose'),
    ('17','ForgottenToy','Забытая игрушечная','Giant','DeadEnd','Tower'),
    ('18','GardenStore','Садовая сокровищница','Giant','Extraction','GiantCup'),
    ('19','ThornGate','Терновые ворота','Castle','Passage','Arch'),
    ('20','MirrorCourt','Зеркальный двор','Castle','Normal','Tower'),
    ('21','SilentBallroom','Безмолвный бальный зал','Castle','Normal','Lantern'),
    ('22','SleepingObservatory','Спящая обсерватория','Castle','Normal','Tower'),
    ('23','StoryArchive','Архив недописанных сказок','Castle','DeadEnd','Rose'),
    ('24','RoyalTreasury','Королевская сокровищница','Castle','Extraction','Arch'),
]


def main():
    MODELS.mkdir(parents=True,exist_ok=True)
    materials=[]; mtl=[]
    for name,(hex_color,roughness) in PALETTE.items():
        rgb=[int(hex_color[i:i+2],16)/255 for i in (0,2,4)]
        mtl += ['newmtl '+name,'Kd '+' '.join(map(str,rgb)),'Ns '+str((1-roughness)*100),'']
        materials.append({'name':name,'color':'#'+hex_color,'smoothness':1-roughness})
    (MODELS/'FablePalette.mtl').write_text('\n'.join(mtl),encoding='utf-8')
    meshes=[fn().save(name) for name,fn in [('Tree',tree),('Mushroom',mushroom),('Arch',arch),('Cottage',cottage),('GiantCup',cup),('Rose',rose),('Lantern',lantern),('Tower',tower)]]
    rooms=[{'id':i,'key':key,'title':title,'biome':biome,'role':role,'hero':hero,
            'width':15,'depth':15,'doorClearance':3.2,'status':'authored-layout-gameplay-pending',
            'pitDepth':4 if int(i) in (3,4,8,13,15,19,22) else 0,
            'upperRoute':int(i) in (6,9,11,13,14,17,20,21,22),
            'shelterType':'under-table' if int(i) in (5,9) else 'alcove',
            'layoutSource':'Editor/FableRealmLayouts.cs'}
           for i,key,title,biome,role,hero in ROOMS]
    (ART/'room-catalog.json').write_text(json.dumps({'rooms':rooms},ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    (ART/'asset-manifest.json').write_text(json.dumps({'materials':materials,'meshes':meshes},indent=2)+'\n',encoding='utf-8')
    print(f'Generated {len(meshes)} original OBJ meshes, {len(materials)} materials, {len(rooms)} room briefs.')


if __name__ == '__main__': main()
