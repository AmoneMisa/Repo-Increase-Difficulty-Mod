"""Four reference-inspired modular outfits fitted to the native robot rig."""
import math
import sys
from pathlib import Path
sys.path.insert(0,str(Path(__file__).resolve().parent))
import numpy as np
from mathutils import Vector
import build_mascots as B
from cosmetic_hair import hair
K,P=B.K,B.P
TAU=math.tau
B.PALETTES.update({
 'assassin':((.84,.82,.73),(.17,.12,.09),(.62,.025,.025)),
 'pirate':((.12,.17,.20),(.70,.64,.45),(.46,.065,.035)),
 'ogre':((.39,.56,.10),(.83,.77,.59),(.24,.13,.055)),
 'sunpriestess':((.84,.77,.61),(.31,.13,.07),(.82,.09,.48))})

def head(M,kind):
    pivots={}
    if kind in ('assassin','ogre'):
        mat=M['white'] if kind=='assassin' else M['main']
        obs,point,_=P.hood('Open face hood',mat,M['leather'],P.HEAD_C,Vector((.285,.405,.29)),
            lambda a:-10+72*P.front(a)**1.5,peak=.025,brim=.018)
        if kind=='assassin':
            # The characteristic pointed brow stays above the robot's eyes.
            obs.append(B.box('Brow clasp',(0,.31,.47),(.055,.06,.025),M['metal']))
        else:
            for side,suffix in ((-1,'l'),(1,'r')):
                group='ear_'+suffix;pivots[group]=K.B(side*.18,.26,.25)
                path=[(side*.18,.26,.25),(side*.36,.34,.25),(side*.43,.40,.25)]
                obs.append(K.tube('Ogre trumpet ear',path,lambda t:.025+.031*t,M['main'],group,segs=12))
                obs.append(B.E('Ear hollow',(side*.435,.408,.275),(.037,.040,.009),M['dark'],group))
        return obs,pivots,None
    hm=K.material(kind+' hair',(.045,.025,.016),.9,clean=True,tint=kind=='sunpriestess')
    obs=hair(hm,.34 if kind=='pirate' else .42)
    if kind=='pirate':
        obs.append(K.surface('Red head wrap',5,64,lambda t,s:Vector((.275*math.sin(TAU*s),.25+.09*t,.254+.275*math.cos(TAU*s))),M['accent'],wrap=True,inside=K.B(0,.29,.254)))
        def brim(t,s):
            a=TAU*s;r=.23+.20*t
            return Vector((r*math.sin(a),.36+.12*t**2*(.5+.5*math.cos(3*a)),.254+r*math.cos(a)))
        obs.append(K.surface('Tricorn brim',9,72,brim,M['leather'],wrap=True))
        obs.append(K.surface('Tricorn underside',9,72,lambda t,s:brim(t,s)-Vector((0,.008,0)),M['leather'],wrap=True,flip=True))
        obs.append(B.E('Hat crown',(0,.42,.254),(.25,.115,.24),M['leather']))
        for side in (-1,1):
            for k in range(3):
                x=side*(.27+.012*k);z=.27-.06*k
                path=[(x,.24,z),(x+side*.02,-.06,z),(x,-.35-k*.025,z+.015)]
                obs.append(K.tube('Braided lock',path,.012,hm,segs=6))
                for y in (-.08,-.22):obs.append(B.E('Hair bead',(x,y,z),(.018,.022,.018),M['gold']))
    else:
        for side in (-1,1):
            obs.append(B.E('Turquoise earring',(side*.285,.04,.28),(.045,.075,.025),M['jewel']))
        # Blunt fringe follows the curved scalp above the unobstructed eye region.
        for k in range(9):
            a=-.65+k*.1625
            obs.append(K.tube('Straight fringe',[(.267*math.sin(a),.35,.254+.269*math.cos(a)),(.267*math.sin(a),.27,.254+.275*math.cos(a))],.025,hm,segs=6))
    return obs,pivots,K.B(0,.58,.254)

def body(M,kind):
    parts=[];pivots={}
    low,high=(.17,.32) if kind=='sunpriestess' else (-.03,.384)
    parts+=P.body_shell('Fitted garment',M['accent'] if kind=='sunpriestess' else M['white'] if kind in ('assassin','ogre') else M['main'],low,lambda a:high,.025)
    if kind in ('assassin','pirate','ogre'):
        # Separate open front vest panels with curved torso fit.
        for side in (-1,1):
            def panel(t,s,side=side):
                inner=.18+.48*t if kind=='assassin' else .20
                a=side*(inner+(1.65-inner)*s);h=.08+.28*t
                return P.on_body(h,a,.04)
            parts.append(K.surface('Leather vest panel',10,24,panel,M['leather'] if kind!='ogre' else M['accent'],uv=(2,2)))
        for k in range(5):
            y=.12+.044*k
            parts.append(B.E('Vest fastening',(.09,y,.35),(.014,.012,.012),M['metal']))
        if kind=='pirate':
            for k in range(2):
                path=[P.on_body(.05+.30*t,-.68+1.36*t+k*.15,.058) for t in np.linspace(0,1,28)]
                parts.append(K.tube('Diagonal leather baldric',path,.022,M['leather'],segs=6))
            parts.append(B.box('Baldric buckle',(.07,.21,.375),(.085,.065,.025),M['metal']))
    if kind in ('assassin','pirate'):
        def cape(t,s):
            u=2*s-1
            return Vector((u*(.22+.09*t),.34-.68*t,-.34-.085*t+.012*t*math.sin(s*TAU*7)))
        parts.append(K.surface('Rear coat cloth',20,48,cape,M['accent'] if kind=='assassin' else M['leather'],group='cape'))
        parts.append(K.surface('Coat lining',20,48,lambda t,s:cape(t,s)+Vector((0,0,.008)),M['light'],group='cape',flip=True))
        pivots['cape']=K.B(0,.345,-.34)
    if kind=='sunpriestess':
        for h in (.17,.32):
            parts.append(K.tube('Ivory top piping',[P.on_body(h,TAU*k/64,.030) for k in range(64)],.008,M['white'],closed=True,segs=6))
    return parts,pivots,None

def waist(M,kind):
    parts=[]
    if kind=='sunpriestess':
        obs,_=P.robe('Wrapped ivory skirt',M['white'],M['white'],0,TAU,.18,.33,lambda a:-.22+.06*P.front(a),lambda t,a:.33+.05*t,folds=(.012,8,0))
        parts+=obs
        parts.append(K.tube('Diagonal wrap seam',[P.on_body(.15-t*.26,-.7+t*1.4,.05) for t in np.linspace(0,1,25)],.006,M['light'],segs=5))
    else:
        parts+=P.body_shell('Lower tunic',M['white'] if kind=='assassin' else M['light'],0,lambda a:.28,.022)
        if kind=='assassin':
            for side in (-1,1):
                parts.append(K.surface('Split coat panel',12,18,lambda t,s,side=side:Vector((side*(.025+.26*s),.18-.46*t,.34+.07*t)),M['white']))
    parts.append(K.surface('Waist sash',3,64,lambda t,s:Vector((.342*math.sin(TAU*s),.21+.07*t,.342*math.cos(TAU*s))),M['accent'],wrap=True,inside=K.B(0,.24,0)))
    if kind!='sunpriestess':parts.append(B.box('Waist buckle',(0,.24,.36),(.11,.065,.025),M['metal']))
    return parts,{},None

def gloves(M,kind,side):
    parts,pivots,_=B.glove(M,kind,side)
    if kind=='ogre':
        for ob in parts:
            if ob.name.startswith('Padded sleeve'):ob.data.materials.clear();ob.data.materials.append(M['white'])
    if kind=='sunpriestess':
        # Broad turquoise bracelets, native hand remains covered by the neutral glove.
        parts.append(B.E('Turquoise bracelet',(side*.35,0,0),(.045,.10,.10),M['jewel']))
    return parts,pivots,None

def materials(kind):
    m=B.mats(kind)
    y,x=np.mgrid[0:256,0:256]
    K.save_png('adventure_linen.png',.94+.025*np.sin(x*math.pi/2)*np.cos(y*math.pi/2),worn=False)
    K.KEEP_PATTERN.add('adventure_linen.png')
    m['white']=K.material(kind+' ivory linen',(.87,.84,.76),.88,image='adventure_linen.png',tint=True)
    m['skin']=K.material(kind+' warm skin',(.58,.25,.095),.83,clean=True,tint=True)
    m['dark']=K.material(kind+' dark trousers',(.035,.029,.025),.85,image='adventure_linen.png',tint=True)
    m['leather']=K.material(kind+' worn leather',(.065,.033,.017),.79,image='adventure_linen.png')
    m['jewel']=K.material(kind+' turquoise',(.055,.58,.48),.58,clean=True)
    return m

def boots(M,kind,side):
    if kind=='ogre':
        m=dict(M);m['main']=M['accent'];return B.boot(m,kind,side)
    return B.boot(M,kind,side)

from adventure_reference_shapes import install
head,body,waist,gloves,boots=install(B,head,body,waist,gloves,boots)

import chel_shapes as CHEL

for kind in B.args.sets or ('assassin','pirate','ogre','sunpriestess'):
    styles={kind+'_hood':('Hat',lambda m,k=kind:head(m,k)),kind+'_suit':('Body Top',lambda m,k=kind:body(m,k)),kind+'_belt':('Body Bottom',lambda m,k=kind:waist(m,k))}
    for side,suffix in ((1,'r'),(-1,'l')):
        styles[kind+'_glove_'+suffix]=('Arm Right' if side==1 else 'Arm Left',lambda m,s=side,k=kind:gloves(m,k,s))
        styles[kind+'_boot_'+suffix]=('Leg Right' if side==1 else 'Leg Left',lambda m,s=side,k=kind:boots(m,k,s))
    setup=lambda k=kind:materials(k)
    if kind=='ogre':
        import mascot_faces
        styles.update(mascot_faces.styles(kind))
    if kind=='sunpriestess':
        # Chel has her own shapes and three face parts in the game's Face Top, Face Bottom and Head Bottom slots.
        styles={kind+'_hood':('Hat',CHEL.head),kind+'_face':('Face Top',CHEL.face),kind+'_lips':('Face Bottom',CHEL.lips),
                kind+'_neck':('Head Bottom',CHEL.neck),kind+'_suit':('Body Top',CHEL.body),kind+'_belt':('Body Bottom',CHEL.waist)}
        for side,suffix in ((1,'r'),(-1,'l')):
            styles[kind+'_glove_'+suffix]=('Arm Right' if side==1 else 'Arm Left',lambda m,s=side:CHEL.gloves(m,s))
            styles[kind+'_boot_'+suffix]=('Leg Right' if side==1 else 'Leg Left',lambda m,s=side:CHEL.boots(m,s,B.E))
        setup=lambda k=kind:CHEL.materials(materials(k))
    K.run(styles,{},B.args.root/'art/cosmetics'/kind,kind,B.args.preview,setup=setup)
    print('ADVENTURE_COMPLETE',kind,flush=True)
