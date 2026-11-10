"""Nine modular REPO costumes, in request order. Blender --background --python ... -- --root . --preview.
Original procedural geometry; native robot slot frames, greyscale recolour textures, no game assets exported.
"""
import argparse
import math
import random
import sys
from pathlib import Path

import bpy
import bmesh
import numpy as np
from mathutils import Vector

sys.path.insert(0, str(Path(__file__).resolve().parent))
import cosmetic_kit as K
import costume_parts as P

parser = argparse.ArgumentParser()
parser.add_argument('--root', type=Path, required=True)
parser.add_argument('--preview', action='store_true')
parser.add_argument('--sets', nargs='*')
args = parser.parse_args(sys.argv[sys.argv.index('--') + 1:])
args.root = args.root.resolve()
K.configure(args.root)
TAU = math.tau

PALETTES = {
    'musketeer': ((.8, .31, .055), (.91, .75, .47), (.25, .085, .045)),
    'bombbird': ((.055, .05, .055), (.23, .21, .20), (.85, .13, .035)),
    'talkingdog': ((.56, .36, .17), (.78, .60, .34), (.28, .17, .085)),
    'rabbid': ((.86, .81, .68), (.64, .39, .34), (.59, .17, .09)),
    'jackal': ((.07, .085, .095), (.66, .54, .34), (.92, .61, .12)),
    'panda': ((.08, .075, .07), (.87, .82, .67), (.76, .39, .09)),
    'penguin': ((.045, .065, .08), (.9, .89, .78), (.98, .51, .065)),
    'kitty': ((.9, .87, .78), (.92, .37, .53), (.82, .035, .30)),
    'chipmunk': ((.53, .27, .085), (.86, .65, .35), (.69, .035, .035)),
}


def mats(kind):
    main, light, accent = PALETTES[kind]
    n = 256
    y, x = np.mgrid[0:n, 0:n]
    rng = random.Random(108)
    noise = K.periodic_noise(n, 5, rng)
    # Fine weave and directional short fibres; all neutral for true slot recolouring.
    K.save_png('mascot_canvas.png', .78 + .12 * noise + .055 * np.sin(x * math.pi / 2) * np.cos(y * math.pi / 3))
    K.save_png('mascot_fur.png', .76 + .13 * noise + .085 * np.sin(x * 2.7 + np.sin(y * .26)))
    K.save_png('mascot_leather.png', .73 + .19 * noise + .025 * np.sin(x * 2 + y * 3))
    # Keep tile detail instead of baking it away; matte relief is in the geometry.
    K.KEEP_PATTERN.update({'mascot_canvas.png', 'mascot_fur.png', 'mascot_leather.png'})
    fur = 'mascot_fur.png' if kind not in ('jackal', 'penguin') else 'mascot_canvas.png'
    return {
        'main': K.material(kind + ' body', main, .91, image=fur, tint=True),
        'light': K.material(kind + ' lining', light, .91, image=fur),
        'accent': K.material(kind + ' accent', accent, .84, image='mascot_canvas.png', tint=True),
        'leather': K.material(kind + ' leather', (.12, .075, .045), .82, image='mascot_leather.png'),
        'metal': K.material(kind + ' buckle', (.49, .45, .34), .48, .65, image='mascot_leather.png'),
        'gold': K.material(kind + ' gold', (.98, .61, .10), .58, .25, image='mascot_canvas.png'),
        'white': K.material(kind + ' teeth', (.94, .91, .79), .68, image='mascot_canvas.png'),
        'dark': K.material(kind + ' dark', (.018, .012, .012), .88, image='mascot_leather.png'),
        'pink': K.material(kind + ' inner ear', (.62, .31, .28), .91, image='mascot_canvas.png'),
    }


def E(name, pos, size, mat, group='static'):
    return K.ellipsoid(name, pos, size, mat, (20, 12), group=group)


def box(name, pos, size, mat, group='static'):
    x, y, z = pos; a, b, c = (v / 2 for v in size)
    verts = [K.B(x + i*a, y + j*b, z + k*c) for i,j,k in
             [(-1,-1,-1),(1,-1,-1),(1,1,-1),(-1,1,-1),(-1,-1,1),(1,-1,1),(1,1,1),(-1,1,1)]]
    return K.mesh_object(name, verts, [(0,3,2,1),(4,5,6,7),(0,1,5,4),(3,7,6,2),(0,4,7,3),(1,2,6,5)], mat, group, smooth=False)


def tag(objects, group):
    for o in objects: o['group'] = group
    return objects


def fur_fringe(parts, centre, radius, height, mat, group='static', count=30):
    # Overlapping tapered tufts break the silhouette without thousands of hair cards.
    for k in range(count):
        a = TAU * k / count
        p = Vector(centre) + Vector((radius * math.sin(a), .008 * math.sin(k * 4.2), radius * math.cos(a)))
        parts.append(K.tube('Layered fibre tuft', [p, p + Vector((.012*math.sin(a), -height*.5, .012*math.cos(a))),
                         p + Vector((.022*math.sin(a), -height, .022*math.cos(a)))],
                         lambda t: .022 * (1 - t) + .001, mat, group, segs=5))


def hood(M, kind):
    parts, pivots = [], {}
    outer = M['light'] if kind in ('panda', 'kitty') else M['main']
    obs, point, pt = P.hood('Open mascot hood', outer, M['leather'], P.HEAD_C, Vector((.29,.40,.29)),
                           lambda a: -8 + 70 * P.front(a)**1.6, peak=.008, brim=.025)
    parts += obs
    if kind=='musketeer':
        # The hood ends under the hat, instead of its crown penetrating the brim and crown.
        for ob in obs:
            bm=bmesh.new();bm.from_mesh(ob.data)
            bmesh.ops.bisect_plane(bm,geom=list(bm.verts)+list(bm.edges)+list(bm.faces),
                                  plane_co=(0,0,.295),plane_no=(0,0,1),clear_outer=True,clear_inner=False,dist=.00001)
            bm.to_mesh(ob.data);bm.free();ob.data.update()
    # Rear seams and repair stitches make the shell read as sewn fabric.
    for lat in (-10, 5, 20, 35, 50):
        p = point(math.pi, lat, .004)
        parts += P.cross_stitch('Back seam stitch', p, Vector((0,0,-1)), .014, M['light'])
    if kind in ('jackal','kitty'):
        for s in (-1,1):
            parts.append(K.tube('Pointed ear', [(s*.18,.27,.23),(s*.23,.48 if kind!='jackal' else .68,.23)],
                                lambda t: .075*(1-t)+.002, outer, segs=4))
            parts.append(K.tube('Inner ear', [(s*.18,.30,.275),(s*.225,.46 if kind!='jackal' else .64,.25)],
                                lambda t: .043*(1-t)+.001, M['pink'] if kind!='jackal' else M['gold'], segs=3))
    if kind in ('talkingdog','rabbid'):
        for s, side in ((1,'r'),(-1,'l')):
            group = 'ear_' + side; pivot = (s*.24,.28,.22)
            pivots[group] = K.B(*pivot)
            centre = (s*.31, .1, .22) if kind=='talkingdog' else (s*.2,.58,.24)
            size = (.105,.25,.065) if kind=='talkingdog' else (.085,.30,.065)
            parts.append(E('Flexible ear', centre, size, M['main'], group))
            parts.append(E('Ear lining', (centre[0],centre[1],centre[2]+.047), (size[0]*.65,size[1]*.78,.024), M['pink'], group))
    if kind in ('panda','chipmunk'):
        for s in (-1,1):
            parts.append(E('Round ear', (s*.235,.32,.24), (.09,.10,.055), M['main']))
            parts.append(E('Ear lining', (s*.235,.32,.28), (.055,.065,.025), M['pink']))
    if kind=='musketeer':
        for side,suffix in ((1,'r'),(-1,'l')):
            group='ear_'+suffix;pivots[group]=K.B(side*.265,.35,.25)
            vertices=[K.B(side*x,y,z) for x,y,z in [(.18,.32,.29),(.35,.34,.29),(.30,.66,.28),
                                                    (.18,.32,.22),(.35,.34,.22),(.30,.66,.22)]]
            parts.append(K.mesh_object('Visible cat ear',vertices,[(0,1,2),(5,4,3),(0,3,4,1),(1,4,5,2),(2,5,3,0)],M['main'],group,smooth=False))
            parts.append(K.mesh_object('Pink ear inset',[K.B(side*x,y,z) for x,y,z in
                                [(.217,.375,.296),(.325,.385,.296),(.296,.609,.287)]],[(0,1,2)],M['pink'],group,smooth=False))
        def brim(t,s,offset=0):
            a=TAU*s;r=.19+.28*t
            return Vector((r*math.sin(a),.30+.075*t*t*max(0,math.cos(3*a))**3+offset,.24+r*.74*math.cos(a)))
        parts.append(K.surface('Cavalier brim',5,64,brim,M['leather'],wrap=True))
        parts.append(K.surface('Brim underside',5,64,lambda t,s:brim(t,s,-.009),M['accent'],wrap=True,flip=True))
        parts.append(E('Cavalier crown', (0,.39,.23), (.25,.13,.23), M['leather']))
        parts.append(K.tube('Fitted hat braid',[(.247*math.sin(TAU*k/64),.355,.23+.228*math.cos(TAU*k/64)) for k in range(64)],.009,M['accent'],closed=True,segs=6))
        pivots['feather'] = K.B(.18,.44,.21)
        path = [Vector((.18 + .19*t, .44 + .24*math.sin(t*1.8), .21)) for t in np.linspace(0,1,13)]
        def plume(t,s,offset=0):
            return Vector((.18+.19*t,.44+.24*math.sin(t*1.8),.21+offset)) + Vector((1,-.45,0))*(s-.5)*.12*math.sin(math.pi*t)**.7
        parts.append(K.surface('Tapered gold plume',18,6,plume,M['gold'],group='feather'))
        parts.append(K.surface('Plume underside',18,6,lambda t,s:plume(t,s,-.006),M['gold'],group='feather',flip=True))
        for k in range(1,12):
            t=k/13
            for side in (0,1):
                parts.append(K.tube('Feather barb',[plume(t,.5,.003),plume(min(1,t+.07),side,.003)],.0017,M['light'],'feather',segs=4))
        parts.append(K.tube('Plume shaft',path,.004,M['light'],'feather'))
    # Bomb Bird's beak and brows, the dog's muzzle and the Rabbid's teeth live in their face parts (mascot_faces.py),
    # under and round the robot's own eyes instead of on the forehead.
    if kind=='penguin':
        parts.append(E('Beak', (0,.335,.45), (.11,.065,.10), M['accent']))
        for s in (-1,1):
            parts.append(E('Expression brow', (s*.13,.37,.415), (.10,.025,.023), M['main']))
        if kind=='bombbird':
            pivots['feather'] = K.B(0,.37,.23)
            parts.append(K.tube('Fuse feather',[(0,.36,.23),(.015,.52,.20),(.09,.57,.20)],lambda t:.037*(1-t)+.012,M['main'],'feather'))
            parts.append(E('Gold fuse tip',(.09,.57,.20),(.08,.035,.026),M['gold'],'feather'))
    if kind in ('panda','kitty','chipmunk'):
        parts.append(E('Mascot muzzle',(0,.355,.42),(.11,.055,.055),M['light']))
        parts.append(E('Nose',(0,.375,.47),(.032,.021,.015),M['gold'] if kind=='kitty' else M['dark']))
        for s in (-1,1):
            parts.append(E('Face patch',(s*.145,.375,.395),(.055,.050,.022),M['main'] if kind=='panda' else M['light']))
            if kind=='kitty':
                for k in range(3): parts.append(K.tube('Whisker',[(s*.24,.20+k*.035,.35),(s*.34,.20+k*.04,.34)],.004,M['dark']))
        if kind=='kitty':
            pivots['bow'] = K.B(-.2,.34,.40)
            for s in (-1,1): parts.append(E('Bow loop',(-.2+s*.075,.35,.43),(.08,.065,.035),M['accent'],'bow'))
            parts.append(E('Bow knot',(-.2,.35,.46),(.04,.04,.03),M['accent'],'bow'))
    if kind=='jackal':
        parts.append(K.tube('Jackal muzzle',[(0,.36,.40),(0,.32,.57)],lambda t:.09*(1-t)+.025,M['main'],segs=4))
        for s in (-1,1):
            for k in range(5):
                parts.append(box('Nemes stripe',(s*(.23+k*.009),.25-k*.09,.22),(.08,.048,.25),M['gold'] if k%2==0 else M['main']))
        parts.append(E('Brow gold eye',(0,.405,.37),(.05,.023,.02),M['gold']))
    return parts, pivots, K.B(0, .53 if kind!='rabbid' else .9,.25)


def suit(M,kind):
    parts, pivots = [], {}
    main = M['accent'] if kind in ('kitty','chipmunk') else M['main']
    parts += P.body_shell('Padded body',main,-.03,lambda a:.384,.022,rows=16)
    if kind not in ('jackal','kitty','chipmunk'):
        # A sewn patch follows the shell instead of adding a separate spherical belly.
        def bib(t,s):
            angle=.70*t*math.cos(TAU*s);height=.175+.185*t*math.sin(TAU*s)
            return P.on_body(height,angle,.028)
        patch=K.surface('Sewn belly bib',10,56,bib,M['light'],wrap=True,inside=K.B(0,.17,0))
        K.box_uv(patch,scale=4.)
        parts.append(patch)
        parts.append(K.tube('Bib seam',[bib(1,k/64) for k in range(64)],.0022,M['light'],closed=True,segs=4))
    if kind in ('talkingdog','chipmunk'):
        for y in (.30,.18,.06): fur_fringe(parts,(0,y,0),.325,.07,main,count=32)
    if kind=='bombbird':
        for row in range(4):
            for k in range(18):
                a=TAU*(k+(row%2)*.5)/18
                parts.append(E('Layered felt feather',(.33*math.sin(a),.31-row*.09,.33*math.cos(a)),(.047,.078,.023),M['main']))
    if kind=='musketeer':
        # Tailored rear panel, with clear space behind the torso and arms throughout its 16 mm sway.
        def cape(t,s,offset=0):
            u=2*s-1;width=.235+.115*t
            topz=-math.sqrt(.335**2-(.235*u)**2)
            z=K.lerp(topz,-.43,t)+.014*t*math.sin(s*TAU*5)+offset
            y=.345-.72*t+.012*t*math.sin(s*TAU*7)
            return Vector((u*width,y,z))
        parts.append(K.surface('Tailored cape',20,48,cape,M['leather'],group='cape'))
        parts.append(K.surface('Cape lining',20,48,lambda t,s:cape(t,s,.007),M['accent'],group='cape',flip=True))
        parts.append(K.tube('Cape hem',[cape(1,k/48) for k in range(49)],.004,M['accent'],'cape',segs=5))
        pivots['cape']=K.B(0,.345,-.335)
        parts.append(E('Paw clasp',(0,.32,.332),(.055,.045,.018),M['gold']))
    if kind=='jackal':
        obs,_=P.robe('Shoulder drape',M['light'],M['leather'],0,TAU,.37,.28,
                    lambda a:.08+P.tatter(a,9,.08),lambda t,a:.28+.10*t,folds=(.015,10,0))
        parts+=obs
        for k in range(7): parts.append(box('Collar gold tile',(.27*math.sin(k*.38-1.14),.34,.27*math.cos(k*.38-1.14)),(.065,.05,.03),M['gold']))
    if kind=='chipmunk':
        # Raised sewn A on the hoodie, in mesh rather than UI text.
        for a,b in [((-.10,.04,.337),(0,.29,.337)),((0,.29,.337),(.10,.04,.337)),((-.06,.13,.34),(.06,.13,.34))]:
            parts.append(K.tube('Hoodie A',[a,b],.022,M['gold'],segs=4))
        parts.append(E('Kangaroo pocket',(0,.03,.337),(.15,.055,.02),M['accent']))
    return parts,pivots,None


def belt(M,kind):
    parts,pivots=[],{}
    parts += P.body_shell('Hip suit',M['main'],0,lambda a:.28,.016)
    band=M['accent'] if kind in ('panda','kitty','chipmunk') else M['leather']
    parts.append(K.surface('Utility belt',2,48,lambda t,s:Vector((.337*math.sin(TAU*s),.22+t*.065,.337*math.cos(TAU*s))),band,wrap=True,inside=K.B(0,.25,0)))
    parts.append(box('Metal buckle',(0,.25,.353),(.12,.085,.035),M['metal']))
    parts.append(box('Buckle inset',(0,.25,.374),(.079,.049,.011),M['accent']))
    if kind in ('jackal','panda'):
        obs,_=P.robe('Waist panels',M['light'],M['leather'],0,TAU,.20,.33,
                    lambda a:-.34+P.tatter(a,8,.08),lambda t,a:.33+.08*t,folds=(.025,8,0))
        parts+=obs
    if kind=='kitty':
        obs,_=P.robe('Pink skirt',M['accent'],M['light'],0,TAU,.21,.33,lambda a:-.14,lambda t,a:.33+.10*t,folds=(.018,12,0))
        parts+=obs
    if kind in ('musketeer','talkingdog','chipmunk'):
        root=Vector((0,.09,-.30)); pivots['tail']=K.B(*root)
        path=[root,Vector((.04,.02,-.45)),Vector((.13,.18,-.56)),Vector((.12,.34,-.54))]
        if kind=='musketeer':
            # Route the tail under the cape before it curls out; leave clearance for both springs.
            path=[root,Vector((.025,-.13,-.33)),Vector((.10,-.24,-.40)),
                  Vector((.20,-.28,-.53)),Vector((.28,-.20,-.58))]
            path=[K.bez(path,t) for t in np.linspace(0,1,28)]
        parts.append(K.tube('Curved tail',path,lambda t:(.10 if kind=='chipmunk' else .046)*(1-.45*t),M['main'],'tail',segs=12))
    elif kind in ('rabbid','panda','kitty','bombbird','penguin'):
        pivots['tail']=K.B(0,.1,-.30)
        parts.append(E('Tail',(0,.1,-.365),(.08,.085,.09),M['main'],'tail'))
    # Hanging accessories share an anchored spring, never physical gameplay objects.
    pivots['charm']=K.B(.22,.24,.27)
    if kind=='musketeer':
        parts.append(K.tube('Sheathed rapier',[(.23,.15,.28),(.30,-.45,.29)],.014,M['metal'],'charm',segs=8))
        parts.append(E('Rapier cup',(.23,.15,.28),(.065,.04,.06),M['metal'],'charm'))
        parts.append(K.tube('Rapier grip',[(.23,.16,.28),(.21,.27,.28)],.018,M['leather'],'charm'))
    elif kind=='bombbird':
        parts.append(E('Bomb charm',(.23,.08,.29),(.083,.083,.083),M['main'],'charm'))
        parts.append(K.tube('Bomb fuse',[(.23,.15,.29),(.25,.23,.29)],.008,M['gold'],'charm'))
    elif kind=='rabbid':
        parts.append(K.tube('Megaphone',[(.23,.15,.29),(.24,-.05,.34)],lambda t:.025+.07*t,M['accent'],'charm',segs=16))
        parts.append(E('Megaphone opening',(.24,-.055,.34),(.07,.009,.07),M['dark'],'charm'))
    elif kind=='talkingdog':
        parts.append(box('Radio',(-.20,.14,.30),(.10,.15,.06),M['dark']))
        parts.append(box('Radio display',(-.20,.17,.334),(.065,.035,.006),M['gold']))
        parts.append(K.tube('Radio antenna',[(-.23,.20,.30),(-.23,.36,.30)],.008,M['dark']))
        parts.append(K.tube('Bone charm',[(.18,.08,.31),(.28,.08,.31)],.018,M['light'],'charm'))
        for x in (.18,.28):
            for y in (.065,.095): parts.append(E('Bone end',(x,y,.31),(.024,.022,.02),M['light'],'charm'))
    elif kind=='jackal':
        parts.append(K.tube('Ankh staff',[(.32,.21,-.20),(.32,-.52,-.20)],.014,M['leather'],'charm'))
        parts.append(K.ring('Ankh loop',(.32,.29,-.20),(0,0,1),.07,.014,M['gold'],group='charm'))
        parts.append(K.tube('Ankh cross',[(.24,.20,-.20),(.40,.20,-.20)],.015,M['gold'],'charm'))
    else:
        parts.append(K.tube('Belt ribbon',[(.22,.24,.27),(.24,.02,.31)],.025,M['accent'],'charm',segs=4))
    return parts,pivots,None


def glove(M,kind,side):
    main = M['accent'] if kind=='chipmunk' else M['main']
    parts,_=P.sleeve('Padded sleeve',main,M['leather'],side,.02,.41,lambda t,a:.09+.018*math.sin(t*math.pi))
    parts.append(E('Mitten',(side*.47,0,0),(.105,.085,.085),main))
    parts.append(E('Thumb',(side*.43,-.058,.055),(.045,.052,.04),main))
    parts.append(K.ring('Cuff',(side*.32,0,0),(1,0,0),.105,.018,M['accent']))
    if kind in ('talkingdog','panda'):
        parts.append(E('Paw pad',(side*.43,0,.089),(.045,.06,.014),M['leather']))
    if kind in ('bombbird','penguin'):
        parts.append(E('Wing glove',(side*.23,0,-.04),(.22,.075,.13),M['main']))
    if kind=='jackal':
        for x in (.12,.30): parts.append(K.ring('Gold bracer band',(side*x,0,0),(1,0,0),.11,.016,M['gold']))
    return parts,{},None


def boot(M,kind,side):
    parts=[]
    # Leg fabric stops inside the boot shaft; only the leather shaft continues down to the foot.
    def leg(t,s):
        a=TAU*s;r=.114+.006*math.sin(t*math.pi)
        return Vector((r*math.sin(a),.10-.34*t,r*math.cos(a)))
    parts.append(K.surface('Trouser leg',10,32,leg,M['main'],wrap=True,inside=K.B(0,-.05,0)))

    def loft(name,levels,material):
        verts=[];faces=[];n=40
        for y,rx,front,back,cz in levels:
            for k in range(n):
                a=TAU*k/n;sa,ca=math.sin(a),math.cos(a)
                # Rounded rectangular footprint: a longer toe, narrow heel and rounded corners.
                x=rx*math.copysign(abs(sa)**.72,sa)+side*.012
                z=cz+(front if ca>=0 else back)*math.copysign(abs(ca)**.72,ca)
                verts.append(K.B(x,y,z))
        for j in range(len(levels)-1):
            for k in range(n):faces.append((j*n+k,j*n+(k+1)%n,(j+1)*n+(k+1)%n,(j+1)*n+k))
        faces.extend([tuple(reversed(range(n))),tuple((len(levels)-1)*n+k for k in range(n))])
        return K.mesh_object(name,verts,faces,material,inside=K.B(side*.012,-.45,.03))

    sole=[(-.580,.116,.213,.108,.035),(-.558,.122,.220,.112,.035),(-.548,.119,.215,.108,.035)]
    parts.append(loft('Shaped rubber outsole',sole,M['dark']))
    parts.append(loft('Leather boot upper',[
        (-.548,.119,.215,.108,.035),(-.505,.117,.212,.107,.035),
        (-.465,.111,.195,.105,.030),(-.420,.106,.136,.103,.010),
        (-.375,.105,.105,.105,0),(-.225,.108,.108,.108,0)],M['accent'] if kind=='penguin' else M['leather']))
    # Stitched toe cap sits on the vamp, not as another sphere on the sole.
    path=[Vector((side*.012+.103*math.sin(a),-.486,.035+.195*math.cos(a))) for a in np.linspace(-1.05,1.05,22)]
    parts.append(K.tube('Toe cap seam',path,.0025,M['light'],segs=4))
    if kind=='musketeer':
        parts.append(loft('Folded musketeer cuff',[
            (-.270,.127,.127,.127,0),(-.250,.144,.140,.140,0),(-.175,.144,.140,.140,0),
            (-.160,.125,.125,.125,0),(-.185,.114,.114,.114,0)],M['accent']))
    else:
        parts.append(loft('Boot collar',[(-.270,.117,.117,.117,0),(-.225,.12,.12,.12,0),(-.210,.112,.112,.112,0)],M['accent']))
    if kind=='talkingdog':fur_fringe(parts,(side*.012,-.225,0),.12,.038,M['light'],count=22)
    if kind not in ('musketeer','penguin'):
        for k in range(4):
            y=-.32-k*.032;z=.109+max(0,k-1)*.020
            parts.append(K.tube('Crossed boot lace',[(side*.012-.060,y,z),(side*.012+.060,y-.021,z+.008)],.0035,M['light'],segs=5))
    return parts,{},None


if __name__ == '__main__':
    for kind in (args.sets or PALETTES):
        styles={kind+'_hood':('Hat',lambda m,k=kind:hood(m,k)),kind+'_suit':('Body Top',lambda m,k=kind:suit(m,k)),
                kind+'_belt':('Body Bottom',lambda m,k=kind:belt(m,k))}
        for side,suffix in ((1,'r'),(-1,'l')):
            styles[kind+'_glove_'+suffix]=('Arm Right' if side==1 else 'Arm Left',lambda m,s=side,k=kind:glove(m,k,s))
            styles[kind+'_boot_'+suffix]=('Leg Right' if side==1 else 'Leg Left',lambda m,s=side,k=kind:boot(m,k,s))
        import mascot_faces
        if kind in mascot_faces.KINDS:styles.update(mascot_faces.styles(kind))
        K.run(styles,{},args.root/'art'/'cosmetics'/kind,kind,args.preview,setup=lambda k=kind:mats(k),
              preview_views=(('front',(1.3,3.7,1.2)),('back',(-1.3,-3.7,1.2)),('side',(3.8,-.3,1.0))))
        print('MASCOT_COMPLETE',kind,flush=True)
