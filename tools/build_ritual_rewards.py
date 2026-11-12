"""Original ritual rewards. Blender -b --python tools/build_ritual_rewards.py -- [key ...]."""
import sys, math, json
from pathlib import Path
import bpy
from mathutils import Vector
sys.path.insert(0, str(Path(__file__).resolve().parent))
import rework_valuables08 as export
from valuable_meshkit import reset, box, ico, torus, bar, cyl, mesh

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / 'art/valuables/ritual-rewards'

def material(name, rgb, metal=0, glow=0, glass=0):
    return export.solid(name, rgb, .12 if glass else .32, metal, glass, glow)

def ring(name, center, radius, width, mat, rotation=None, group=None):
    ob = torus(name, center, radius, width, mat, n=32, rot=rotation)
    if group: ob['group'] = group
    return ob

def quantum():
    dark = material('Quantum graphite', (.025,.045,.065), .65)
    steel = material('Brushed titanium', (.29,.39,.44), .8)
    gold = material('Cryostat gold', (.72,.43,.11), .8)
    cyan = material('Quantum circuits', (.06,.65,.95), glow=1.7)
    violet = material('Entangled qubits', (.51,.13,.9), glow=1.4)
    screen = material('Large dark display', (.012,.025,.045), .15)
    glass = material('Cryostat crystal window', (.38,.77,.93), glass=.82)
    box('Wide foot', (0,0,.055), (.84,.61,.11), dark, .025)
    box('Integrated computer chassis', (0,.08,.29), (.67,.49,.45), dark, .035)
    for x in [-.28,.28]:
        box('Side rail', (x,.08,.30), (.035,.50,.43), steel, .008)
    for z in [.18,.25,.32,.39]:
        box('Vent', (0,-.177,z), (.36,.018,.014), steel, .004)
    cyl('Cryostat chamber', (0,-.05,.61), .22,.27,glass,n=24)
    for z,r in [(.50,.23),(.61,.18),(.74,.13)]:
        cyl('Cryogenic stage', (0,-.05,z), r,.025,gold,n=24)
    for i in range(8):
        a=2*math.pi*i/8
        bar('Cooling line', (.16*math.cos(a),-.05+.16*math.sin(a),.49),
            (.085*math.cos(a),-.05+.085*math.sin(a),.73), .009, gold)
    ico('Qubit core', (0,-.05,.65), (.045,.045,.065), cyan, sub=2)
    box('Screen support', (0,.21,.74), (.10,.09,.75), steel, .01)
    box('Huge display housing', (0,.20,1.13), (1.38,.115,.86), dark, .035)
    box('Screen bevel', (0,.132,1.13), (1.30,.022,.78), steel, .012)
    box('Display glass', (0,.114,1.13), (1.24,.018,.72), screen, .006)
    # Baked circuitry and entanglement diagrams rather than screen-font substitutes.
    for row in range(4):
        z=.89+row*.15
        for i in range(7):
            x=-.51+i*.17
            ico('Display qubit', (x,.098,z), (.019,.009,.019), cyan if (row+i)%2 else violet)
            if i<6: bar('Entanglement link', (x,.098,z), (x+.17,.098,z+(.05 if i%2 else -.05)), .004, cyan)
    ring('Quantum orbit', (0,-.05,.65), .10,.007, violet, (math.pi/2,.35,0), 'quantum_orbit')

def painting():
    gold=material('Antique carved gold',(.50,.30,.075),.78)
    light=material('Gold moulding highlights',(.72,.48,.16),.75)
    recess=material('Oxidised frame recess',(.09,.053,.022),.35)
    wood=material('Dark walnut backing',(.045,.027,.018))
    box('Gallery backing',(0,.025,.80),(1.16,.10,1.54),wood,.012)
    # Layered moulding gives a proper deep picture frame instead of four flat rails.
    for width,height,rail,y,depth,mat in [
        (1.24,1.60,.08,-.015,.16,gold),
        (1.17,1.53,.026,-.105,.025,light),
        (1.125,1.485,.024,-.092,.025,recess),
        (1.08,1.44,.025,-.102,.035,gold),
        (1.04,1.40,.012,-.098,.02,light)]:
        for sign in [-1,1]:
            box('Vertical carved moulding',(sign*(width-rail)/2,y,.80),(rail,depth,height),mat,.005)
            box('Horizontal carved moulding',(0,y,.80+sign*(height-rail)/2),(width,depth,rail),mat,.005)
    # Small acanthus-like leaves along the outer gold; no bulky corner buttons.
    for sign in [-1,1]:
        for z in [.19+i*.105 for i in range(12)]:
            leaf=ico('Gilt leaf',(sign*.587,-.105,z),(.022,.012,.038),light,sub=1)
            leaf.rotation_euler[1]=sign*.5
        for x in [-.47+i*.105 for i in range(10)]:
            ico('Gilt top and bottom leaves',(x,-.105,.80+sign*.762),(.032,.012,.017),light,sub=1)
    canvas=material('Oil on aged linen',(1,1,1))
    bsdf=canvas.node_tree.nodes['Principled BSDF'];bsdf.inputs['Roughness'].default_value=.9
    tex=canvas.node_tree.nodes.new('ShaderNodeTexImage')
    tex.image=bpy.data.images.load(str(ROOT/'src/Art/ritual_living_portrait.png'),check_existing=True)
    canvas.node_tree.links.new(tex.outputs['Color'],bsdf.inputs['Base Color'])
    # A flat textured canvas. Fine UV rows exist only around the eyes, so the living
    # gaze can move within the painting without floating eyeball geometry.
    xs=sorted(set([0,.38]+[round(.39+i*.006,6) for i in range(43)]+[.66,1]))
    ys=sorted(set([0,.72]+[round(.725+i*.003,6) for i in range(27)]+[.81,1]))
    vs=[((u-.5)*1.02,-.081,.12+v*1.36) for v in ys for u in xs]
    nx=len(xs);faces=[]
    for j in range(len(ys)-1):
        for i in range(nx-1):
            n=j*nx+i;faces.append((n,n+1,n+nx+1,n+nx))
    ob=mesh('Painted canvas',vs,faces,canvas);ob['group']='portrait_canvas'
    uv=ob.data.uv_layers.new(name='UVMap')
    for poly in ob.data.polygons:
        for li in poly.loop_indices:
            index=ob.data.loops[li].vertex_index
            uv.data[li].uv=(xs[index%nx],ys[index//nx])

def sun():
    gold=material('Solar filigree gold',(.79,.46,.08),.86)
    stone=material('Resort obsidian',(.035,.045,.055),.35)
    core=material('Eternal amber',(.98,.33,.025),glow=2.1)
    crystal=material('Solar crystal shell',(.95,.66,.16),glass=.7)
    cyl('Display pedestal',(0,0,.06),.30,.12,stone,n=20)
    cyl('Gold plinth',(0,0,.13),.25,.035,gold,n=20)
    for sign in [-1,1]:
        bar('Solar cradle',(sign*.18,0,.13),(sign*.29,0,.48),.035,gold)
    ico('Sun heart',(0,0,.63),(.22,.22,.22),core,sub=2)['group']='sun_core'
    ico('Faceted crystal',(0,0,.63),(.29,.29,.29),crystal,sub=2)
    ring('Celestial ring',(0,0,.63),.36,.018,gold,(math.pi/2,0,.3))
    ring('Orbital ring',(0,0,.63),.32,.012,gold,(.5,.7,0))
    for i in range(12):
        a=i*math.pi/6
        p=Vector((math.cos(a),0,math.sin(a)))
        mesh('Sun ray',[tuple(Vector((0,0,.63))+p*.34+Vector((0,-.018,0))),
                        tuple(Vector((0,0,.63))+p*.49),
                        tuple(Vector((0,0,.63))+p*.34+Vector((0,.018,0))),
                        tuple(Vector((0,0,.63))+p*.34+Vector((-math.sin(a),0,math.cos(a)))*.035)],
             [(0,1,3),(3,1,2),(2,1,0),(0,3,2)],gold)

def coffin(variant):
    gold=material('Coffin silver gilt',(.44,.55,.61),.86)
    crystal=material('Transparent faceted crystal',(.49,.78,.91),glass=.88)
    velvet=material('Coffin velvet',(.055,.035,.085))
    skin=material('Sleeping ivory face',(.69,.55,.44))
    dark=material('Closed eyes and boots',(.035,.025,.028))
    outfits=[(.11,.20,.10),(.33,.035,.10),(.035,.045,.065),(.10,.12,.29),(.27,.15,.055)]
    cloth=material('Character outfit '+str(variant),outfits[variant])
    hair=material('Character hair '+str(variant),[(.20,.10,.045),(.44,.31,.16),(.13,.09,.06),(.49,.49,.44),(.10,.07,.04)][variant])
    # Wide shoulders, tapered feet; character and its skeleton use the same limb layout.
    outline=[(-.29,-.88),(.29,-.88),(.46,.35),(.33,.88),(-.33,.88),(-.46,.35)]
    vs=[(x,y,z) for z in [.09,.46] for x,y in outline]
    faces=[tuple(reversed(range(6))),tuple(range(6,12))]+[(i,(i+1)%6,(i+1)%6+6,i+6) for i in range(6)]
    mesh('Faceted crystal shell',vs,faces,crystal)
    box('Velvet resting bed',(0,0,.13),(.69,1.54,.07),velvet,.025)
    for z in [.10,.46]:
        for i,(x,y) in enumerate(outline):
            nx,ny=outline[(i+1)%6];bar('Crystal edge',(x,y,z),(nx,ny,z),.014,gold)
    for x,y in outline:bar('Corner post',(x,y,.10),(x,y,.46),.012,gold)
    for y in [-.5,.15]:
        for sign in [-1,1]:
            ring('Carry handle',(sign*.45,y,.25),.045,.01,gold,(0,math.pi/2,0))
    def P(x,y,z=.26):return Vector((x*.72,y*.85,z))
    ico('Coat torso',P(0,.15),(.145,.19,.08),cloth,sub=2)
    ico('Character head',P(0,.535,.29),(.105,.125,.075),skin,sub=2)
    ico('Hair',P(0,.565,.31),(.110,.11,.065),hair,sub=2)
    # Expose the face above the hair so the occupant reads as a person, not bones.
    ico('Face',P(0,.50,.355),(.085,.09,.025),skin,sub=2)
    for sign in [-1,1]:
        side=0 if sign==-1 else 1
        shoulder=P(sign*.21,.31)
        elbow=P(sign*(.37+(.09 if variant==2 else 0)),.18)
        raised=variant==3 or variant==0 and side==0 or variant==1 and side==1
        hand=P(sign*(.32 if raised else .4), .52 if raised else -.10)
        bar('Sleeve upper',shoulder,elbow,.045,cloth)
        bar('Sleeve lower',elbow,hand,.036,cloth)
        ico('Hand',hand,(.035,.044,.027),skin,sub=2)
        knee_x=.35 if variant==4 and side==1 else sign*(.29 if variant==2 else .16)
        knee=P(knee_x,-.4);foot=P(.05 if variant==4 and side==1 else knee_x+sign*.045,-.76)
        bar('Trouser leg',P(sign*.12,-.12),knee,.048,cloth)
        bar('Boot',knee,foot,.041,dark)
        box('Closed eyelid',P(sign*.037,.52,.380),(.038,.008,.004),dark,.001)
    if variant==0:box('Hunter belt',P(0,-.04,.34),(.27,.045,.025),gold,.003)
    if variant==1:
        for i in range(5):ico('Countess crown jewel',P((i-2)*.033,.66,.37),(.014,.015,.025),gold)
    if variant==2:
        for y in [.05,.16,.25]:ico('Butler silver button',P(0,y,.35),(.009,.009,.006),gold)
    if variant==3:
        for sign in [-1,1]:ring('Scholar spectacles',P(sign*.04,.52,.39),.025,.004,gold)
    if variant==4:box('Ranger sash',P(0,.20,.35),(.24,.04,.025),gold,.003)

def render(key, parts, folder):
    scene=bpy.context.scene
    points=[o.matrix_world@v.co for o in parts for v in o.data.vertices]
    lo=Vector([min(p[i] for p in points) for i in range(3)]);hi=Vector([max(p[i] for p in points) for i in range(3)])
    center=(lo+hi)/2;size=max(hi-lo)
    floor=material('Preview floor',(.045,.055,.068))
    box('Preview_ground',(0,0,-.035),(200,200,.035),floor)
    scene.render.engine='CYCLES';scene.cycles.samples=12;scene.cycles.use_denoising=True;scene.cycles.transmission_bounces=8
    scene.render.resolution_x=768;scene.render.resolution_y=768;scene.render.resolution_percentage=100
    scene.world.use_nodes=True;scene.world.node_tree.nodes['Background'].inputs['Strength'].default_value=.35
    for p,power,color in [((-2,-3,4),500,(.75,.85,1)),((3,1,3),650,(1,.73,.45))]:
        bpy.ops.object.light_add(type='AREA',location=center+Vector(p)*size)
        light=bpy.context.object;light.data.energy=power*size*size;light.data.size=size*3;light.data.color=color
        light.rotation_euler=(center-light.location).to_track_quat('-Z','Y').to_euler()
    bpy.ops.object.camera_add(location=center+Vector((1.9,-3.4,2.8 if 'coffin' in key else 1.9))*size)
    cam=bpy.context.object;cam.rotation_euler=(center-cam.location).to_track_quat('-Z','Y').to_euler()
    cam.data.type='ORTHO';cam.data.ortho_scale=size*1.45;scene.camera=cam
    scene.render.filepath=str(folder/(key+'.png'));bpy.ops.render.render(write_still=True)

def build(key):
    reset();folder=OUT/key;folder.mkdir(parents=True,exist_ok=True)
    if key.endswith('quantum'):quantum()
    elif key.endswith('painting'):painting()
    elif key.endswith('sun'):sun()
    else:coffin(int(key[-1]))
    parts=export.merge_static(export.prepare_parts(),key)
    runtime=export.runtime_export(key,parts,folder)
    for mat in runtime['materials']:
        mat['metallic']=float(bpy.data.materials[mat['name']].node_tree.nodes['Principled BSDF'].inputs['Metallic'].default_value)
    path=ROOT/'src/Art'/('v11_'+key+'.mesh.json')
    path.write_text(json.dumps(runtime,separators=(',',':')),encoding='utf8')
    render(key,parts,folder)
    print('RITUAL_REWARD',key,'parts',len(parts),flush=True)

keys=sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else []
for key in keys or ['ritual_reward_quantum','ritual_reward_painting','ritual_reward_sun']+[f'ritual_reward_coffin_{i}' for i in range(5)]:build(key)
