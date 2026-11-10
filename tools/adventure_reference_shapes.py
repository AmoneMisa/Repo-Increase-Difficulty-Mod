"""Distinctive silhouettes and garment construction for the three character references."""
import math
import numpy as np
from mathutils import Vector
import cosmetic_kit as K
import costume_parts as P
from cosmetic_hair import hair
TAU=math.tau

def install(B,old_head,old_body,old_waist,old_gloves,old_boots):
    def head(M,k):
        if k=='ogre':return old_head(M,k)
        if k=='assassin':
            # A deep cloth hood: the front edge dips to a beak between the eyes, the sides fall past the temples and
            # the back hangs loose on the shoulders.
            centre=P.HEAD_C;radii=Vector((.305,.42,.31))
            def edge(a):return -44+106*P.front(a)**1.5-31*math.exp(-(a/.2)**2)
            def point(a,lat,off=0.):
                la=math.radians(lat)
                d=Vector((math.cos(la)*math.sin(a),math.sin(la),math.cos(la)*math.cos(a)))
                p=centre+Vector((radii.x*d.x,radii.y*d.y,radii.z*d.z))
                p+=Vector((0,.03,.012))*math.exp(-((lat-87)/10)**2)                 # soft peak at the crown
                p+=Vector((0,0,.075))*P.front(a)**3*math.exp(-((lat-edge(a)-4)/20)**2) # the brow overhangs the face
                flare=max(0.,-lat)/44                                                 # loose cloth below the head
                p+=Vector((math.sin(a),0,math.cos(a)))*(.10*flare+.006*math.sin(9*a)*flare)
                return p+d*off
            def cowl(t,s,off=0.):
                a=TAU*s-math.pi
                return point(a,K.lerp(edge(a),89.5,t**.9),off)
            inside=K.B(*centre)
            parts=[K.surface('Pointed assassin cowl',18,128,cowl,M['white'],wrap=True,uv=(6,3),inside=inside),
                   K.surface('Pointed assassin cowl lining',18,128,lambda t,s:cowl(t,s,-.012),M['dark'],wrap=True,uv=(6,3),inside=inside,flip=True,hem=0)]
            # crimson piping along the whole opening and a seam down the beak
            parts.append(K.tube('Crimson hood piping',[cowl(0,k/128,-.004) for k in range(128)],.008,M['accent'],closed=True,segs=6))
            parts.append(K.tube('Beak seam',[point(0,lat,.003) for lat in np.linspace(edge(0)+1,80,10)],.004,M['white'],segs=5))
            return parts,{},K.B(0,.46,.254)
        dark=K.material(k+' blue black hair',(.003,.004,.009),.72,clean=True,tint=k=='sunpriestess')
        parts=hair(dark,.67 if k=='sunpriestess' else .49,wave=.018)
        if k=='sunpriestess':
            # Continuous blunt fringe, not a row of cylindrical beads.
            def fringe(t,s):
                a=-.84+1.68*s
                lat=math.radians(76-33*t)
                return Vector((.305*math.cos(lat)*math.sin(a),.423*math.sin(lat),.254+.305*math.cos(lat)*math.cos(a)))
            parts.append(K.surface('Blunt black fringe',9,48,fringe,dark))
            for side in (-1,1):
                c=Vector((side*.30,.035,.32))
                verts=[K.B(*(c+Vector(v))) for v in [(0,.075,0),(-.055,0,0),(0,-.075,0),(.055,0,0),(0,0,.025)]]
                parts.append(K.mesh_object('Turquoise diamond earring',verts,[(0,1,4),(1,2,4),(2,3,4),(3,0,4),(3,2,1,0)],M['jewel']))
            return parts,{},K.B(0,.44,.254)
        # Jack's worn three-cornered hat, red bandana, loose locks, beads and two beard braids.
        parts.append(K.surface('Bandana',6,64,lambda t,s:Vector((.293*math.sin(TAU*s),.25+.105*t,.254+.293*math.cos(TAU*s))),M['accent'],wrap=True,inside=K.B(0,.3,.254)))
        def brim(t,s):
            a=TAU*s;corner=.5+.5*math.cos(3*a)
            r=.23+t*(.13+.13*corner)
            return Vector((r*math.sin(a),.36+t*t*(.075+.065*(1-corner)),.254+r*math.cos(a)))
        parts.append(K.surface('Three corner hat',12,96,brim,M['leather'],wrap=True))
        parts.append(K.surface('Hat underside',12,96,lambda t,s:brim(t,s)-Vector((0,.012,0)),M['leather'],wrap=True,flip=True))
        parts.append(K.tube('Tricorn stitched brim',[brim(1,s) for s in np.linspace(0,1,97)],.005,M['light'],segs=5))
        parts.append(B.E('Low hat crown',(0,.43,.254),(.245,.115,.235),M['leather']))
        for side in (-1,1):
            for j in range(4):
                x=side*(.255+.016*j);z=.32-.045*j
                path=[(x,.25,z),(x+side*.035,-.12,z+.015),(x-side*.02,-.49+j*.025,z)]
                parts.append(K.tube('Loose dreadlock',[K.bez(path,t) for t in np.linspace(0,1,24)],.010+(j%2)*.004,dark,segs=7))
                for y in (-.18,-.31):parts.append(B.E('Dreadlock bead',(x,y,z),(.018,.025,.018),M['gold'] if j%2 else M['jewel']))
            parts.append(K.tube('Swept moustache',[(0,.008,.525),(side*.08,.025,.52),(side*.14,-.008,.49)],lambda t:.012*(1-.6*t),dark,segs=6))
            parts.append(K.tube('Braided goatee',[(side*.034,-.07,.53),(side*.04,-.14,.63),(side*.055,-.24,.66)],lambda t:.014-.008*t,dark,segs=6))
            parts.append(B.E('Beard bead',(side*.053,-.215,.652),(.014,.018,.014),M['gold']))
        return parts,{},K.B(0,.57,.254)

    def body(M,k):
        if k=='ogre':return old_body(M,k)
        parts=[];pivots={}
        if k=='sunpriestess':
            parts+=P.body_shell('Warm skin torso',M['skin'],-.03,lambda a:.38,.019)
            parts+=P.body_shell('White cropped wrap',M['white'],.18,lambda a:.32,.027)
            parts+=P.body_shell('Pink bust band',M['accent'],.255,lambda a:.33-.015*P.front(a),.032)
            parts.append(K.tube('Gold neckline',[P.on_body(.33-.015*P.front(TAU*s),TAU*s,.037) for s in np.linspace(0,1,65)],.005,M['gold'],closed=True,segs=5))
            return parts,{},None
        parts+=P.body_shell('Ivory shirt',M['white'],-.03,lambda a:.384,.023)
        for side in (-1,1):
            def panel(t,s):
                inner=.1+(.66 if k=='assassin' else .30)*t
                a=side*(inner+(2.0-inner)*s)
                return P.on_body(.035+.33*t,a,.045)
            parts.append(K.surface('Fitted waistcoat',14,30,panel,M['leather'] if k=='assassin' else M['main']))
            edge=[panel(t,0) for t in np.linspace(0,1,25)]
            parts.append(K.tube('Waistcoat piping',edge,.007,M['accent'] if k=='assassin' else M['gold'],segs=6))
            for j in range(6):parts.append(B.E('Brass waistcoat button',(side*.08,.07+.044*j,.358),(.011,.011,.009),M['metal']))
        if k=='assassin':
            for side in (-1,1):
                # Silver filigree scrolls on the chest.
                for j in range(3):
                    pts=[P.on_body(.11+.07*j+.025*math.sin(t*TAU),side*(.45+.15*math.cos(t*TAU)),.054) for t in np.linspace(0,1,22)]
                    parts.append(K.tube('Silver chest scroll',pts,.0035,M['metal'],segs=5))
            parts.append(B.E('Left shoulder armour',(-.29,.34,-.015),(.14,.075,.18),M['leather']))
            parts.append(K.tube('Shoulder silver rim',[(-.38,.36,-.15),(-.40,.39,0),(-.36,.35,.14)],.009,M['metal'],segs=6))
            def cape(t,s):
                return Vector((-.12-(2*s-1)*(.16+.08*t),.33-.78*t,-.35-.10*t+.012*t*math.sin(s*TAU*5)))
            parts.append(K.surface('Asymmetric shoulder cape',22,40,cape,M['leather'],group='cape'))
            parts.append(K.surface('Red cape lining',22,40,lambda t,s:cape(t,s)+Vector((0,0,.008)),M['accent'],group='cape',flip=True))
            pivots['cape']=K.B(-.12,.345,-.35)
        else:
            for side in (-1,1):
                obs,_=P.robe('Long open pirate coat',M['leather'],M['light'],side*.8,side*math.pi,.36,.385,lambda a:-.46,lambda t,a:.385+.035*t,folds=(.008,9,0),rows=18,cols=40)
                parts+=obs
            pts=[P.on_body(.02+.35*t,-.85+1.6*t,.068) for t in np.linspace(0,1,32)]
            parts+=K.ribbon('Broad leather baldric',pts,lambda p,i:Vector((1,0,0)),.055,M['leather'])
            parts.append(B.box('Baldric silver buckle',(.05,.21,.39),(.09,.07,.025),M['metal']))
        return parts,pivots,None

    def waist(M,k):
        if k=='ogre':return old_waist(M,k)
        parts=[]
        if k=='sunpriestess':
            parts+=P.body_shell('Bare waist',M['skin'],.04,lambda a:.28,.022)
            o,pt=P.robe('Asymmetric short wrap',M['white'],M['white'],-.4,TAU-.4,.12,.34,lambda a:-.14-.12*(.5+.5*math.sin(a)),lambda t,a:.34+.035*t,folds=(.008,5,0),rows=12,cols=72)
            parts+=o
            parts.append(K.tube('Diagonal white wrap edge',[pt(t,0,.005) for t in np.linspace(0,1,20)],.006,M['white'],segs=6))
            return parts,{},None
        parts+=P.body_shell('Hip garment',M['white'] if k=='assassin' else M['light'],0,lambda a:.28,.02)
        if k=='assassin':
            for side in (-1,1):
                def flap(t,s):
                    x=side*(.025+(.25-.08*t)*s)
                    return Vector((x,.20-.60*t+.10*s*t,.35+.06*t+.012*math.sin(s*math.pi)))
                parts.append(K.surface('Pointed embroidered tunic tail',18,24,flap,M['white']))
                parts.append(K.tube('Red tunic border',[flap(t,.95) for t in np.linspace(0,1,25)],.008,M['accent'],segs=6))
            parts.append(K.surface('Wide red sash',5,64,lambda t,s:Vector((.347*math.sin(TAU*s),.17+.12*t,.347*math.cos(TAU*s))),M['accent'],wrap=True,inside=K.B(0,.22,0)))
            # Recognizable pointed silver belt emblem rather than a square buckle.
            path=[(-.09,.16,.37),(0,.29,.38),(.09,.16,.37),(.045,.18,.39),(0,.12,.40),(-.045,.18,.39),(-.09,.16,.37)]
            parts.append(K.tube('Assassin silver insignia',path,.012,M['metal'],segs=6))
            for side in (-1,1):
                pts=[(side*(.24+.09*t),.20-.52*t,.10+.02*math.sin(t*4)) for t in np.linspace(0,1,25)]
                parts+=K.ribbon('Hanging red sash',pts,lambda p,i:Vector((1,0,0)),.04,M['accent'])
        else:
            parts.append(K.surface('Ivory waist sash',5,64,lambda t,s:Vector((.35*math.sin(TAU*s),.09+.17*t,.35*math.cos(TAU*s))),M['light'],wrap=True,inside=K.B(0,.2,0)))
            for y,tilt in ((.25,.04),(.14,-.06)):
                parts.append(K.surface('Crossed pirate belt',3,64,lambda t,s,y=y,tilt=tilt:Vector((.36*math.sin(TAU*s),y+.05*t+tilt*math.sin(TAU*s),.36*math.cos(TAU*s))),M['leather'],wrap=True,inside=K.B(0,y,0)))
                parts.append(B.box('Large ornate belt buckle',(.09 if tilt>0 else -.12,y+.025,.38),(.12,.075,.028),M['gold']))
            def sash(t,s):return Vector((.27+.075*s,.17-.70*t,.18+.025*math.sin(t*5)))
            parts.append(K.surface('Long striped sash',20,12,sash,M['white']))
            for j in range(6):parts.append(K.surface('Red sash stripe',20,2,lambda t,s,j=j:sash(t,(j+.25*s)/6)+Vector((0,0,.003)),M['accent']))
        return parts,{},None

    def gloves(M,k,side):
        if k=='ogre':return old_gloves(M,k,side)
        skin=M['skin'] if k=='sunpriestess' else M['light']
        parts=[]
        if k=='sunpriestess':
            obs,_=P.sleeve('Bare arm',skin,skin,side,.02,.43,lambda t,a:.078)
            parts+=obs
            parts.append(K.ring('Turquoise wrist cuff',(side*.35,0,0),(1,0,0),.09,.028,M['jewel']))
        else:
            obs,_=P.sleeve('Full gathered shirt sleeve',M['white'],M['white'],side,.02,.29,lambda t,a:.083+.035*math.sin(t*math.pi))
            parts+=obs
            obs,_=P.sleeve('Leather forearm bracer',M['leather'],M['leather'],side,.275,.43,lambda t,a:.103)
            parts+=obs
            for x in (.29,.40):parts.append(K.ring('Metal bracer edge',(side*x,0,0),(1,0,0),.093,.007,M['metal']))
            if k=='assassin':parts.append(B.box('Wrist blade housing',(side*.35,-.09,0),(.17,.025,.055),M['metal']))
        parts.append(B.E('Hand',(side*.48,0,0),(.10,.078,.078),skin))
        parts.append(B.E('Thumb',(side*.44,-.055,.048),(.04,.045,.035),skin))
        return parts,{},None

    def boots(M,k,side):
        if k=='ogre':return old_boots(M,k,side)
        if k=='sunpriestess':
            # The reference has bare legs and feet, not tall work boots.
            parts=[B.E('Bare leg',(0,-.22,0),(.112,.33,.11),M['skin']),B.E('Bare foot',(side*.01,-.51,.075),(.115,.065,.19),M['skin'])]
            for j in range(4):parts.append(B.E('Toe',(side*.01+(j-1.5)*.038,-.51,.22),(.025,.035,.05),M['skin']))
            return parts,{},None
        m=dict(M);m['main']=M['dark'];m['accent']=M['leather']
        parts,pivots,_=old_boots(m,k,side)
        parts.append(K.ring('Boot cuff',(0,-.21,0),(0,1,0),.12,.02,M['leather']))
        return parts,pivots,None
    return head,body,waist,gloves,boots
