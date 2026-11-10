"""Additional courier silhouettes and surface construction. Called by build_courier.py."""
import math
import random


def install(G):
    E,C,B,R,T = (G[n] for n in ('ellipsoid','cone','box','ring','tail_curve'))
    chibi,eyes,gear = (G[n] for n in ('chibi','eyes','gear'))
    cream=(.92,.79,.59); pink=(.92,.55,.55); dark=(.055,.026,.018)

    def smile(y=.19,z=.32,width=.045):
        E('body','fixed',(.15,.025,.035),'mouth',(0,y,z),(width,.016,.038))
        E('body','fixed',(.92,.30,.38),'tongue',(0,y+.014,z-.015),(width*.60,.006,.016))

    def limbs(main,second,hoof=False):
        for s,side in ((1,'l'),(-1,'r')):
            E('arm_'+side,'main',main,'arm',(s*.16,.005,.18),(.046,.055,.105),rot=(0,s*.3,0))
            E('foot_'+side,'main',main,'leg',(s*.08,0,.055),(.047,.05,.065))
            E('foot_'+side,'fixed' if hoof else 'second',dark if hoof else second,'foot',(s*.08,.035,.024),(.052,.072,.028))
            if hoof: B('foot_'+side,'fixed',(.02,.015,.013),'hoof split',(s*.08,.102,.02),(.004,.007,.03),bevel=0)

    def curls(main,around_body=False):
        rng=random.Random(44)
        # Irregular overlapping wool locks, not identical round beads.
        for row in range(4 if around_body else 3):
            for k in range(20):
                a=math.tau*(k+row*.45)/20; z=(.075+row*.045) if around_body else (.48+row*.027)
                rad=.15 if around_body else (.15-row*.035)
                E('body','main',main,'wool lock',(math.sin(a)*rad,math.cos(a)*rad*.85,z),
                  (.029+rng.random()*.008,.025,.034),rot=(.2,math.sin(a)*.3,a),segs=12,rings=8)

    def ears(main,long=False):
        for s,side in ((1,'l'),(-1,'r')):
            E('ear_'+side,'second',main,'ear',(s*.205,.005,.37 if long else .43),(.065,.038,.13 if long else .05),rot=(0,-s*.18,0))
            if not long: E('ear_'+side,'fixed',pink,'inner ear',(s*.22,.049,.43),(.048,.012,.029))

    def sheep():
        main=(.92,.83,.69); second=(.64,.35,.73)
        chibi(main,head=(.175,.155,.155)); limbs(main,cream,True)
        E('body','fixed',cream,'face',(0,.13,.40),(.14,.06,.135))
        eyes(.067,.19,.42,.054); smile(.197,.33,.032)
        ears(cream); curls(main,True); curls(main)
        E('tail','main',main,'tail',(0,-.16,.08),(.055,.06,.055))
        gear(head=(.175,.155,.155),accent=second,with_cap=False)
        # Brass goggles are sewn into the wool cap.
        for s in (-1,1):
            R('body','fixed',(.73,.46,.12),'goggle rim',(s*.075,.075,.548),.048,.009,rot=(math.pi/3,0,0))
            E('body','second',second,'goggle lens',(s*.075,.079,.55),(.040,.022,.028))

    def cow():
        main=(.92,.84,.69); second=(.16,.085,.10)
        chibi(main,head=(.185,.16,.16)); limbs(main,cream,True); ears(main)
        for s in (-1,1):
            T('body','fixed',(.83,.59,.23),'horn',[(s*.12,-.01,.51),(s*.14,-.01,.58),(s*.125,.0,.63)],.028,.006)
            E('body','second',second,'eye patch',(s*.10,.12,.435),(.085,.055,.09))
        eyes(.078,.174,.435,.055)
        E('body','fixed',pink,'muzzle',(0,.178,.338),(.11,.065,.068))
        for s in (-1,1): E('body','fixed',(.30,.11,.13),'nostril',(s*.046,.233,.357),(.013,.007,.019))
        smile(.239,.315)
        for s in (-1,1): E('body','second',second,'flank patch',(s*.12,-.06,.14),(.058,.071,.080))
        T('tail','main',main,'tail',[(0,-.14,.10),(.09,-.20,.09),(.13,-.20,.16)],.013,.008)
        E('tail','second',second,'tail tassel',(.13,-.20,.17),(.025,.023,.045))
        gear(accent=(.77,.09,.10),with_cap=False)
        E('body','fixed',(.95,.61,.08),'bell',(0,.17,.20),(.027,.025,.032))

    def dog():
        main=(.77,.47,.20); second=(.94,.77,.52)
        chibi(main,head=(.18,.155,.16)); limbs(main,second); ears((.36,.15,.055),True)
        E('body','second',second,'muzzle',(0,.16,.34),(.095,.055,.06))
        E('body','second',second,'belly',(0,.075,.15),(.12,.09,.14))
        eyes(.069,.167,.42,.055); smile(.207,.315)
        E('body','fixed',dark,'nose',(0,.216,.367),(.032,.017,.023))
        T('tail','main',main,'tail',[(.04,-.14,.08),(.16,-.20,.11),(.20,-.15,.20)],.038,.016)
        gear(head=(.18,.155,.16),accent=(.98,.64,.08))

    def wings(kind,main,second):
        import bpy
        from mathutils import Vector

        def leaf(group,root,tip,width,color,label):
            root,tip=Vector(root),Vector(tip);axis=tip-root
            across=Vector((axis.z,0,-axis.x)).normalized()
            verts,faces=[],[];rows=16;cols=6
            for layer in (-1,1):
                for i in range(rows+1):
                    t=i/rows;shape=math.sin(math.pi*t)**.85
                    for j in range(cols+1):
                        u=2*j/cols-1
                        pos=root+axis*t+across*(width*shape*u)
                        pos.y += layer*.003 + .012*shape*(1-u*u)
                        verts.append(tuple(pos))
            stride=(rows+1)*(cols+1)
            for layer in range(2):
                for i in range(rows):
                    for j in range(cols):
                        k=layer*stride+i*(cols+1)+j
                        face=(k,k+1,k+cols+2,k+cols+1)
                        faces.append(face if layer==0 else face[::-1])
            edge=list(range(cols+1))+[i*(cols+1)+cols for i in range(1,rows+1)]
            edge+=list(range(rows*(cols+1)+cols-1,rows*(cols+1)-1,-1))
            edge+=[i*(cols+1) for i in range(rows-1,0,-1)]
            for k,i in enumerate(edge):
                j=edge[(k+1)%len(edge)];faces.append((i,j,j+stride,i+stride))
            mesh=bpy.data.meshes.new(label);mesh.from_pydata(verts,[],faces);mesh.update()
            ob=bpy.data.objects.new(label,mesh);bpy.context.collection.objects.link(ob)
            G['add'](ob,group,'second',color,label)
            T(group,'second',color,label+' vein',[root,root.lerp(tip,.5),tip],.004,.001)

        for s,side in ((1,'l'),(-1,'r')):
            group='wing_'+side;root=(s*.12,-.11,.29)
            if kind=='fairy':
                leaf(group,root,(s*.35,-.16,.48),.065,second,'upper petal wing')
                leaf(group,root,(s*.30,-.16,.14),.053,second,'lower petal wing')
            elif kind=='angel':
                # Broad overlapping feather vanes, with their roots hidden inside the next row.
                for k in range(8):
                    f=k/7
                    base=(s*(.12+.14*f),-.13,.29+.06*f)
                    tip=(s*(.19+.17*f),-.15,.16+.18*f)
                    leaf(group,base,tip,.027,second,'flight feather')
                for k in range(5):
                    f=k/4
                    leaf(group,(s*(.12+.12*f),-.115,.30+.055*f),
                         (s*(.18+.11*f),-.12,.245+.065*f),.024,second,'covert feather')
            else:
                # Scalloped bat membrane: curved spans meet ribs at the root, elbow and fingers.
                tips=[Vector((s*.22,-.145,.44)),Vector((s*.35,-.16,.39)),Vector((s*.32,-.16,.22)),Vector((s*.16,-.12,.17))]
                outline=[Vector(root)]
                outline+=tips[:1]
                for start,end in zip(tips,tips[1:]):
                    for k in range(1,9):
                        t=k/8;point=start.lerp(end,t)
                        inward=(Vector(root)-point)*(.26*math.sin(math.pi*t))
                        outline.append(point+inward)
                n=len(outline);vs=[tuple(p+Vector((0,off,0))) for off in (-.003,.003) for p in outline]
                faces=[]
                for k in range(1,n-1):faces.extend([(0,k+1,k),(n,n+k,n+k+1)])
                faces.extend((k,(k+1)%n,(k+1)%n+n,k+n) for k in range(n))
                mesh=bpy.data.meshes.new('bat wing');mesh.from_pydata(vs,[],faces);mesh.update()
                ob=bpy.data.objects.new('bat wing',mesh);bpy.context.collection.objects.link(ob)
                G['add'](ob,group,'second',second,'scalloped wing')
                for tip in tips[:3]:T(group,'main',main,'wing finger',[root,(Vector(root)+tip)*.5,tip],.010,.003)

    def spirit(kind):
        colors={'fairy':((.96,.66,.57),(.57,.24,.70),(.60,.68,.18)),
                'devil':((.79,.13,.10),(.20,.07,.21),(.96,.61,.08)),
                'angel':((.98,.76,.51),(.91,.86,.72),(.98,.65,.13)),
                'dragon':((.53,.19,.64),(.97,.65,.27),(.99,.53,.08))}
        main,second,accent=colors[kind]
        chibi(main,head=(.18,.155,.16),body=(.14,.13,.16)); limbs(main,main)
        eyes(.071,.170,.425,.058);smile(.19,.33,.048)
        wings(kind,main,second)
        if kind in ('devil','dragon'):
            for s in (-1,1):
                T('body','second',second,'horn',[(s*.12,0,.51),(s*.15,0,.59),(s*.12,0,.64)],.032,.004)
                C('ear_'+('l' if s>0 else 'r'),'main',main,'pointed ear',(s*.16,.0,.44),(s*.29,.02,.48),.043,verts=12)
            T('tail','main',main,'tail',[(.02,-.13,.08),(.17,-.20,.07),(.25,-.17,.15),(.24,-.10,.22)],.03,.009)
            C('tail','second',second,'tail point',(.24,-.10,.21),(.24,-.10,.28),.032,verts=4)
        if kind=='dragon':
            E('body','second',second,'belly plate',(0,.095,.15),(.115,.060,.13))
            for k in range(4):
                B('body','fixed',(.60,.34,.17),'belly seam',(0,.155,.075+k*.045),(.15,.004,.004),bevel=0)
            for s in (-1,1): C('body','fixed',cream,'fang',(s*.029,.209,.347),(s*.029,.213,.32),.009,verts=8)
        elif kind=='angel':
            curls(accent)
            R('body','accent',accent,'halo',(0,0,.64),.13,.012,rot=(0,.18,0))
            for k in range(8):
                a=math.tau*k/8
                E('body','second',second,'robe petal',(math.sin(a)*.10,math.cos(a)*.10,.105),(.045,.035,.11),segs=14,rings=8)
        elif kind=='fairy':
            E('body','second',second,'hair cap',(0,-.01,.515),(.18,.14,.07))
            E('body','second',second,'hair bun',(.07,-.04,.59),(.08,.075,.08))
            for k in range(8):
                a=math.tau*k/8
                E('body','accent',accent,'leaf skirt',(math.sin(a)*.115,math.cos(a)*.115,.11),(.038,.026,.09),rot=(0,.3,a))
            for k in range(5):
                a=math.tau*k/5
                E('body','fixed',cream,'flower petal',(-.13+math.cos(a)*.032,.108,.52+math.sin(a)*.032),(.024,.012,.024),segs=12,rings=8)
        gear(head=(.18,.155,.16),body=(.14,.13,.16),accent=accent,with_cap=False)

    more={'sheep':sheep,'cow':cow,'dog':dog,'fairy':lambda:spirit('fairy'),'devil':lambda:spirit('devil'),
          'angel':lambda:spirit('angel'),'dragon':lambda:spirit('dragon')}
    G['ANIMALS'].update(more)
    for key in more:
        G['PIVOTS'][key]={'arm_l':(.16,0,.25),'arm_r':(-.16,0,.25),'foot_l':(.08,0,.06),'foot_r':(-.08,0,.06),
                          'tail':(.02,-.14,.09),'ear_l':(.17,0,.48),'ear_r':(-.17,0,.48),
                          'wing_l':(.12,-.10,.30),'wing_r':(-.12,-.10,.30),'scarf':(0,.145,.27)}
