"""Reference-inspired original valuables and Spritz mesh assets."""
import sys,math,json,bpy
from pathlib import Path
sys.path.insert(0,str(Path(__file__).resolve().parent))
from valuable_meshkit import *
from mathutils import Matrix

def centrifuge():
    body=lathe('Centrifuge body',[(.025,.205),(.045,.232),(.18,.235),(.205,.217),(.205,.193),(.125,.152),(.107,.038)],'white',32)
    body.data.materials.append(mat('steel'))
    for p in body.data.polygons:
        if 4*32<=p.index<6*32:p.material_index=1
    # Annular rim surrounds the visible rotor rather than covering it with a solid lid.
    torus('Thick rubber opening',(0,0,.207),.209,.020,'dark',32)
    cyl('Rotor hub',(0,0,.135),.028,.06,'steel');cyl('Hub fastener',(0,0,.176),.010,.018,'dark',8)
    for i in range(12):
        a=i*2*pi/12;x,y=.151*cos(a),.151*sin(a)
        o=cyl('Sample tube %02d'%i,(x,y,.183),.013,.095,'purple',10)
        o.rotation_euler=Vector((.25*cos(a),.25*sin(a),1)).to_track_quat('Z','Y').to_euler()
        cyl('Tube collar %02d'%i,(x+.010*cos(a),y+.010*sin(a),.229),.016,.015,'white',10)
        cyl('Orange stopper %02d'%i,(x+.011*cos(a),y+.011*sin(a),.243),.012,.015,'red' if i%3 else 'orange',10)
    box('Control module',(0,-.223,.10),(.112,.024,.085),'white',.008)
    box('Display',(0,-.237,.118),(.064,.006,.023),'dark',.001)
    for i in range(4):box('Display segments',(-.024+i*.016,-.241,.119),(.009,.002,.011),'cyan')
    for x in [-.032,0,.032]:cyl('Panel button',(x,-.24,.077),.009,.005,'purple',10,'Y')
    # Hinged lid, opened 105 degrees relative to its closed pose.
    lidparts=[];before=set(bpy.data.objects)
    torus('Lid outer frame',(0,-.205,0),.215,.016,'dark',32)
    glass=mat('glass');p=glass.node_tree.nodes.get('Principled BSDF');p.inputs['Transmission Weight'].default_value=.75;p.inputs['Roughness'].default_value=.23;p.inputs['Metallic'].default_value=0
    cyl('Lid smoky window',(0,-.205,0),.20,.010,glass,32)
    torus('Lid raised ring',(0,-.205,.007),.172,.004,'white',28)
    cyl('Lid central fitting',(0,-.205,.013),.036,.015,'dark',16)
    box('Lid handle',(0,-.408,.018),(.095,.038,.029),'dark',.007)
    T=Matrix.Translation(Vector((0,.205,.222)))@Matrix.Rotation(math.radians(-108),4,'X')
    for o in set(bpy.data.objects)-before:o.matrix_world=T@o.matrix_world
    for x in [-.075,.075]:bar('Lid hinge',(x,.181,.207),(x,.216,.24),.009,'dark')
    for x in [-.14,.14]:
        for y in [-.12,.12]:cyl('Rubber foot',(x,y,.013),.020,.025,'dark',10)

def spritz():
    cyl('Spritz barrel',(0,-.025,0),.026,.17,'steel',16,'Y')
    for y in [-.108,.058]:cyl('Spritz collar',(0,y,0),.031,.016,'gold',16,'Y')
    cyl('Spritz nozzle',(0,.075,0),.011,.025,'steel',12,'Y')
    needle=lathe('Spritz_Needle',[(0,.0035),(.177,.0035),(.185,.0008)],'steel',8);needle.rotation_euler[0]=-pi/2;needle.location.y=.085
    cyl('Plunger shaft',(0,-.158,0),.006,.098,'steel',10,'Y')
    torus('Thumb ring',(0,-.226,0),.025,.005,'steel',20)
    for x in [-.047,.047]:
        torus('Finger ring',(x,-.095,0),.022,.0045,'steel',20)
        bar('Ring mount',(x*.46,-.097,0),(x*.77,-.095,0),.005,'gold')
    box('Dose window',(0,-.025,.026),(.012,.09,.0025),'dark')
    for i in range(5):box('Graduation tick',(.010,-.061+i*.016,.026),(.009,.002,.002),'bone')

def gpu():
    box('GPU PCB',(0,0,.015),(.322,.128,.014),'green',.003)
    box('Triple fan shroud',(0,0,.043),(.334,.139,.047),'dark',.010)
    box('Backplate',(0,0,.009),(.330,.134,.009),'steel',.002)
    for x in [-.106,0,.106]:
        cyl('Fan recess',(x,0,.069),.047,.004,'dark',24)
        torus('Fan frame',(x,0,.073),.046,.0025,'steel',24)
        for j in range(8):
            a=j*2*pi/8;outline=[(.014,a),(.041,a+.32),(.040,a+.68),(.018,a+.44)]
            vs=[(x+r*cos(t),r*sin(t),z) for z in [.072,.076] for r,t in outline]
            mesh('Swept fan blade',vs,[(3,2,1,0),(4,5,6,7),(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7)],'dark')
        cyl('Fan hub',(x,0,.078),.013,.011,'steel',16)
    for i in range(28):box('Heatsink fin',(-.15+i*.011,.071,.042),(.004,.013,.038),'steel')
    for i in range(6):
        for y in [-.062,.062]:box('Muted RGB trim',(-.135+i*.054,y,.070),(.046,.006,.006),['purple','purple','cyan','green','cyan','purple'][i])
    box('PCIe contacts',(-.03,-.077,.014),(.21,.018,.004),'gold')
    for i in range(26):box('Contact separation',(-.131+i*.008,-.082,.017),(.002,.012,.001),'dark')
    box('Mounting bracket',(-.178,0,.037),(.011,.145,.069),'steel',.002)
    for y in [-.045,-.012,.026]:box('Display socket',(-.185,y,.04),(.004,.023,.014),'dark',.002)
    box('Power socket',(.108,.073,.042),(.038,.015,.016),'dark',.002)
    for x in [-.156,.156]:
        for y in [-.053,.053]:cyl('Shroud screw',(x,y,.071),.0035,.004,'gold',8)

def ram():
    for y in [-.022,.022]:
        box('RAM PCB',(0,y,.027),(.142,.004,.038),'green',.001)
        box('Faceted heatspreader',(0,y-.004,.032),(.144,.007,.035),'dark',.003)
        for i in range(24):box('Gold contact',(-.067+i*.0056,y,.005),(.0038,.0045,.009),'gold')
        for i in range(8):box('RGB diffuser',(-.062+i*.0175,y,.051),(.018,.008,.005),['red','orange','yellow','green','cyan','cyan','purple','purple'][i])
        for x in [-.055,.055]:
            cyl('Heatspreader fastener',(x,y-.009,.042),.003,.002,'steel',8,'Y')
        for x in [-.035,0,.035]:
            o=box('Heatspreader slash',(x,y-.008,.029),(.004,.002,.022),'steel');o.rotation_euler[1]=-.5

def vacuum():
    lathe('Rounded vacuum housing',[(.015,.150),(.025,.173),(.068,.174),(.084,.163),(.089,.145)],'white',40)
    cyl('Dark bumper',(0,0,.037),.175,.028,'dark',40)
    cyl('Top cover',(0,0,.084),.164,.012,'white',40)
    torus('Cover seam',(0,0,.091),.160,.0018,'dark',40)
    for key,z,r,h in [('red',.102,.040,.016),('white',.118,.043,.018)]:cyl('Lidar turret',(0,.043,z),r,h,key,24)
    # Curved front sensor window is a closed shallow cylindrical patch.
    angles=[-2.00+i*.95/12 for i in range(13)];vs=[(r*cos(a),r*sin(a),z) for r,z in [(.176,.032),(.176,.061),(.173,.032),(.173,.061)] for a in angles];n=len(angles)
    fs=[]
    for k in range(n-1):fs.extend([(k,k+1,k+1+n,k+n),(k+2*n,k+3*n,k+1+3*n,k+1+2*n),(k,k+2*n,k+1+2*n,k+1),(k+n,k+1+n,k+1+3*n,k+3*n)])
    fs.extend([(0,n,3*n,2*n),(n-1,3*n-1,4*n-1,2*n-1)]);mesh('Front sensor window',vs,fs,'dark')
    for y in [-.03,-.055]:cyl('Control button',(.067,y,.095),.008,.004,'dark',12)
    for x in [-.114,.114]:cyl('Drive wheel',(x,0,.018),.029,.023,'dark',12,'X')
    for a in [0,2*pi/3,4*pi/3]:bar('Side brush',(.11,-.10,.010),(.11+.055*cos(a),-.10+.055*sin(a),.01),.003,'dark',6)

def bath_set():
    # Open-front cardboard carrier, three gold-capped bottles, raised carry handle.
    box('Cardboard bottom',(0,0,.012),(.275,.095,.024),'pink',.004)
    box('Printed back panel',(0,.048,.116),(.275,.007,.224),'pink',.004)
    for x in [-.134,.134]:box('Carrier side',(x,0,.095),(.009,.095,.18),'pink',.003)
    box('Front bottom lip',(0,-.047,.037),(.27,.009,.05),'pink',.002)
    box('Header strip',(0,-.047,.176),(.27,.009,.028),'pink',.002)
    handle=box('Carry handle',(0,.048,.246),(.148,.008,.065),'purple',.007)
    cutter=box('temporary handle hole',(0,.048,.244),(.090,.04,.026),'dark',.006)
    bpy.context.view_layer.objects.active=handle;mod=handle.modifiers.new('Handle opening','BOOLEAN');mod.object=cutter;mod.operation='DIFFERENCE';bpy.ops.object.modifier_apply(modifier=mod.name);bpy.data.objects.remove(cutter,do_unlink=True)
    for i,x in enumerate([-.086,0,.086]):
        o=lathe('Bath bottle',[(.025,.029),(.036,.034),(.145,.034),(.160,.027),(.173,.021)],['red','purple','orange'][i],20);o.location.x=x
        cyl('Gold bottle cap',(x,0,.186),.024,.035,'gold',16)
        torus('Cap rim',(x,0,.171),.024,.0018,'yellow',16)
        label('Original fairy label',(x,-.034,.094),(.058,.002,.092),'fairy_'+str(i))
        for a in [-.7,.7]:
            o=box('Cap facet',(x+.019*sin(a),-.019*cos(a),.187),(.007,.003,.023),'yellow',.001);o.rotation_euler[2]=a

def skeleton():
    # Reuse the approved original skull, converted to weathered teaching-model bone.
    source=ROOT/'art/references/valuable-skull-approved.blend'
    with bpy.data.libraries.load(str(source),link=False) as (src,dst):dst.objects=[n for n in src.objects if n.startswith('Skull_')]
    for ob in dst.objects:
        if not ob or ob.type!='MESH':continue
        bpy.context.collection.objects.link(ob);ob.parent=None;ob.location=ob.location*.65+Vector((0,0,1.49));ob.scale*=.65
        ob.data.materials.clear();ob.data.materials.append(mat('bone'))
        if len(ob.data.polygons)>200:
            bpy.context.view_layer.objects.active=ob;dec=ob.modifiers.new('Teaching skull simplification','DECIMATE');dec.ratio=.5;bpy.ops.object.modifier_apply(modifier=dec.name)
    # Rolling stand.
    bar('Stand pole',(0,.11,.08),(0,.11,1.48),.009,'steel')
    for i in range(5):
        a=2*pi*i/5;p=(.25*cos(a),.11+.25*sin(a),.055)
        bar('Stand spoke',(0,.11,.09),p,.014,'dark');cyl('Caster wheel',(p[0],p[1],.025),.025,.018,'dark',10,'X')
    for side in [-1,1]:
        x=side*.095
        bone('Femur',(x,0,1.02),(x,-.009,.68),.023);ico('Knee',(x,-.013,.68),(.025,.023,.024),'orange')
        bone('Tibia',(x,-.004,.65),(x,-.012,.28),.016);bone('Fibula',(x+side*.025,.014,.65),(x+side*.014,.014,.28),.008)
        ico('Heel',(x,-.005,.255),(.023,.033,.025),'bone')
        for i in range(5):
            xx=x+(i-2)*.010;bone('Metatarsal',(xx,-.005,.254),(xx,-.073,.24),.006)
            bone('Toe',(xx,-.073,.24),(xx,-.105+.004*i,.237),.004)
        ico('Pelvic wing',(side*.062,.014,1.045),(.070,.040,.075),'bone',2)
        torus('Hip socket',(side*.08,-.019,1.012),.021,.008,'orange',12,(pi/2,0,0))
        tube('Pubic arch',[(side*.07,-.02,1.02),(side*.044,-.05,.986),(0,-.053,.978)],.010,'bone')
        ico('Shoulder blade',(side*.115,.057,1.36),(.055,.013,.075),'bone')
        bone('Humerus',(side*.19,0,1.395),(side*.235,-.01,1.105),.016)
        ico('Elbow',(side*.235,-.01,1.10),(.019,.020,.021),'orange')
        bone('Radius',(side*.235,-.018,1.09),(side*.25,-.026,.86),.009)
        bone('Ulna',(side*.25,.005,1.09),(side*.27,-.014,.86),.008)
        ico('Wrist',(side*.26,-.02,.85),(.021,.018,.018),'bone')
        for i in range(5):
            xx=side*.26+(i-2)*.008
            bone('Metacarpal',(xx,-.02,.846),(xx,-.026,.803),.0045)
            bone('Finger phalanx',(xx,-.026,.803),(xx,-.032,.775-.005*(2-abs(i-2))),.0038)
            bone('Finger tip',(xx,-.032,.775-.005*(2-abs(i-2))),(xx,-.038,.752-.005*(2-abs(i-2))),.003)
        tube('Clavicle',[(0,-.055,1.43),(side*.07,-.061,1.445),(side*.15,-.028,1.43),(side*.19,0,1.395)],.009,'bone')
        ico('Shoulder joint',(side*.19,0,1.395),(.021,.023,.022),'orange')
        for i in range(7):
            z=1.407-i*.033;w=[.08,.115,.14,.15,.147,.132,.11][i];dep=.079
            end=pi if i<5 else .82*pi
            path=[(side*(.014+w*sin(j*end/12)),.010+dep*cos(j*end/12),z-.026*sin(j*end/12)-.014*j/12) for j in range(13)]
            tube('Rib %d'%i,path,.0065,'bone',5)
    box('Sternum',(0,-.077,1.321),(.021,.018,.164),'bone',.005)
    for i in range(24):
        z=.99+i*.021;y=.052+.010*sin(i*.20)
        cyl('Vertebra',(0,y,z),.014,.014,'bone',8)
        if i%3==0:cyl('Intervertebral disc',(0,y,z-.009),.012,.003,'orange',8)
        bar('Spinal process',(0,y,z),(0,y+.021,z-.004),.005,'bone',6)

def bomb():
    path=ROOT/'art/valuables/heavy-bomb-reference-05/Heavy_Bomb_Reference_05.blend'
    with bpy.data.libraries.load(str(path),link=False) as (src,dst):dst.objects=[n for n in src.objects if n.startswith('Bomb_')]
    lookup={'Olive painted casing':'olive','Yellow painted bands':'yellow','Red paint blocks':'red','Dark circumferential seams':'dark','Olive tail fins':'olive','Ochre nose cap':'yellow','Nose rim and rivets':'gold'}
    for ob in dst.objects:
        if not ob or ob.type!='MESH':continue
        bpy.context.collection.objects.link(ob);ob.parent=None
        for i,m in enumerate(ob.data.materials):ob.data.materials[i]=mat(lookup.get(m.name.split('.')[0],'olive'))
        for p in ob.data.polygons:p.use_smooth=False

BUILDERS=[('bomb','valuable',bomb),('centrifuge','valuable',centrifuge),('spritz','shop-consumable',spritz),('skeleton','valuable',skeleton),('graphics_card','valuable',gpu),('ram_pair','valuable',ram),('robot_vacuum','valuable',vacuum),('bath_gift_set','valuable',bath_set)]
if __name__=='__main__':
    only=sys.argv[sys.argv.index('--')+1] if '--' in sys.argv else None
    reports=[]
    if only and (OUT/'manifest.json').exists():reports=[r for r in json.loads((OUT/'manifest.json').read_text()) if r['asset']!=only]
    for name,role,builder in BUILDERS:
        if only and name!=only:continue
        reset();builder();reports.append(export(name,role))
        (OUT/'manifest.json').write_text(json.dumps(reports,indent=2))
    print('MEDICAL_TECH_PACK_BUILT',flush=True)
