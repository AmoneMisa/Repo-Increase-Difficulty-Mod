"""Clean ceramic silhouette and conformal painted details for the approved photo reference."""
import bpy,math
from mathutils import Vector
from valuable_meshkit import mesh,cyl,torus,tube

# Blender coordinates; the export converts Z-up to Unity Y-up.
PAW_PIVOT=(.145,.050,.286)

def make(prev,sphere):
    white=prev.solid('Ivory porcelain glaze',(.80,.78,.71),.25)
    red=prev.solid('Vermilion collar enamel',(.59,.018,.022),.24)
    pink=prev.solid('Soft coral face paint',(.55,.095,.10),.35)
    ink=prev.solid('Fine charcoal face paint',(.012,.014,.013),.42)
    gold=prev.solid('Warm gilt medallion',(.73,.43,.075),.28,.78)
    stamp=prev.solid('Gold seal engraving',(.30,.16,.025),.36,.65)

    def signpow(x,p):return math.copysign(abs(x)**p,x)
    # A slightly squared, rounded head, with no voxel/remesh ripples or separate cheek lumps.
    rx,ry,rz=.150,.132,.139;cx,cy,cz=-.025,-.035,.396;exponent=.95
    verts=[];faces=[];n=48;rings=25
    for j in range(rings):
        latitude=-math.pi/2+.025+(math.pi-.05)*j/(rings-1)
        for i in range(n):
            a=i*2*math.pi/n
            verts.append((cx+rx*signpow(math.cos(latitude),exponent)*signpow(math.cos(a),exponent),cy+ry*signpow(math.cos(latitude),exponent)*signpow(math.sin(a),exponent),cz+rz*signpow(math.sin(latitude),exponent)))
    for j in range(rings-1):
        for i in range(n):k=j*n+i;q=j*n+(i+1)%n;faces.append((k,q,q+n,k+n))
    faces.extend([tuple(reversed(range(n))),tuple(range((rings-1)*n,rings*n))]);mesh('Single clean rounded cat head',verts,faces,white)

    # A controlled smooth pear profile, broad at the seated hips and small under the chin.
    controls=[(.012,.100),(.025,.145),(.060,.177),(.120,.181),(.182,.164),(.235,.137),(.273,.115),(.298,.075)]
    profile=[]
    for k in range(len(controls)-1):
        p0=Vector(controls[max(0,k-1)]);p1=Vector(controls[k]);p2=Vector(controls[k+1]);p3=Vector(controls[min(len(controls)-1,k+2)])
        for j in range(3):
            t=j/3;v=.5*((2*p1)+(-p0+p2)*t+(2*p0-5*p1+4*p2-p3)*t*t+(-p0+3*p1-3*p2+p3)*t*t*t);profile.append(tuple(v))
    profile.append(controls[-1]);vs=[(r*math.cos(i*2*math.pi/40),.018+r*.80*math.sin(i*2*math.pi/40),z) for z,r in profile for i in range(40)];fs=[]
    for j in range(len(profile)-1):
        for i in range(40):a=j*40+i;b=j*40+(i+1)%40;fs.append((a,b,b+40,a+40))
    fs.extend([tuple(reversed(range(40))),tuple(range((len(profile)-1)*40,len(profile)*40))]);mesh('Smooth seated pear body',vs,fs,white)
    for side in [-1,1]:
        hip=sphere('Sloping folded haunch',(side*.109,.030,.107),(.064,.094,.112),white,24,14);hip.rotation_euler.y=side*.40
    sphere('Small left hind toes',(-.112,-.080,.027),(.034,.060,.028),white,24,14)
    sphere('Small right hind toes',(.081,-.070,.027),(.036,.060,.029),white,24,14)
    arm=sphere('Sloping lowered foreleg',(-.095,-.105,.156),(.036,.040,.095),white,24,16);arm.rotation_euler.y=-.38
    arm=sphere('Forepaw turning toward centre',(-.049,-.137,.077),(.036,.041,.071),white,24,16);arm.rotation_euler.y=-.61
    sphere('Central resting forepaw',(-.010,-.148,.029),(.039,.049,.030),white,24,14)
    # Fixed shoulder and overlapping moving forearm give a continuous wrist at either extreme.
    sphere('Raised fixed shoulder',(.104,.050,.246),(.047,.049,.054),white,24,16)
    # Fuse only the small body pieces; the analytic head keeps its perfectly smooth surface.
    body=[o for o in bpy.context.scene.objects if o.type=='MESH' and o.name!='Single clean rounded cat head']
    bpy.ops.object.select_all(action='DESELECT')
    for ob in body:ob.select_set(True)
    bpy.context.view_layer.objects.active=next(o for o in body if o.name=='Smooth seated pear body');bpy.ops.object.join();ob=bpy.context.object;ob.name='Continuous glazed seated body'
    bpy.ops.object.transform_apply(location=False,rotation=True,scale=True)
    rem=ob.modifiers.new('Seamless body only','REMESH');rem.mode='VOXEL';rem.voxel_size=.0045;bpy.ops.object.modifier_apply(modifier=rem.name)
    sm=ob.modifiers.new('Smooth ceramic joins','SMOOTH');sm.factor=.8;sm.iterations=6;bpy.ops.object.modifier_apply(modifier=sm.name)
    dec=ob.modifiers.new('Body game mesh','DECIMATE');dec.ratio=.15;bpy.ops.object.modifier_apply(modifier=dec.name)
    # One bent forepaw with a tapered wrist and a broad mitten, not a ball on a cylinder.
    controls=[(.243,.104,.050,.027,.031),(.268,.136,.060,.030,.034),(.310,.153,.055,.028,.030),(.355,.154,.035,.024,.027),(.393,.157,.008,.026,.026),(.417,.157,-.009,.031,.027),(.436,.157,-.009,.026,.024),(.445,.156,-.005,.003,.004)]
    sections=[]
    for k in range(len(controls)-1):
        a=Vector(controls[max(0,k-1)]);b=Vector(controls[k]);c=Vector(controls[k+1]);d=Vector(controls[min(len(controls)-1,k+2)])
        for j in range(3):t=j/3;sections.append(.5*((2*b)+(-a+c)*t+(2*a-5*b+4*c-d)*t*t+(-a+3*b-3*c+d)*t*t*t))
    sections.append(Vector(controls[-1]));vs=[(x+max(.002,wx)*math.cos(i*2*math.pi/32),y+max(.002,wy)*math.sin(i*2*math.pi/32),z) for z,x,y,wx,wy in sections for i in range(32)];fs=[]
    for j in range(len(sections)-1):
        for i in range(32):a=j*32+i;b=j*32+(i+1)%32;fs.append((a,b,b+32,a+32))
    fs.extend([tuple(reversed(range(32))),tuple(range((len(sections)-1)*32,len(sections)*32))]);arm=mesh('One curved ceramic beckoning forepaw',vs,fs,white);arm['group']='beckoning_paw'

    for side in [-1,1]:
        x=-.025+side*.120
        # Sink the whole root into the head. A horizontal triangle base would leave
        # an overhanging flange that still reads as a separately glued-on ear.
        inner=-.025+side*.065;outer=-.025+side*.145
        ear=mesh('Soft ceramic ear',[(inner,-.025,.500),(outer,-.025,.430),(x+side*.008,-.008,.565),(inner,.040,.490),(outer,.040,.425),(x+side*.008,.020,.555)],[(0,1,2),(5,4,3),(0,3,4,1),(1,4,5,2),(2,5,3,0)],white)
        bpy.context.view_layer.objects.active=ear;mod=ear.modifiers.new('Rounded ear tips','BEVEL');mod.width=.011;mod.segments=3;bpy.ops.object.modifier_apply(modifier=mod.name)
        inner=mesh('Inset red ear enamel',[(x-.014,-.025,.522),(x+.014,-.025,.522),(x+side*.006,-.009,.545)],[(0,1,2)],red)
        bpy.context.view_layer.objects.active=inner;mod=inner.modifiers.new('Thin painted enamel','SOLIDIFY');mod.thickness=.0007;bpy.ops.object.modifier_apply(modifier=mod.name)

    # Ear bases are sculpted into the head, and paint is projected onto that final surface.
    head_parts=[o for o in bpy.context.scene.objects if o.type=='MESH' and (o.name=='Single clean rounded cat head' or o.name.startswith('Soft ceramic ear'))]
    bpy.ops.object.select_all(action='DESELECT')
    for ob in head_parts:ob.select_set(True)
    bpy.context.view_layer.objects.active=next(o for o in head_parts if o.name=='Single clean rounded cat head');bpy.ops.object.join();head=bpy.context.object;head.name='Continuous porcelain head and ears'
    rem=head.modifiers.new('Ears sculpted into head','REMESH');rem.mode='VOXEL';rem.voxel_size=.0035;bpy.ops.object.modifier_apply(modifier=rem.name)
    sm=head.modifiers.new('Soft ear roots','SMOOTH');sm.factor=.7;sm.iterations=4;bpy.ops.object.modifier_apply(modifier=sm.name)
    dec=head.modifiers.new('Head game mesh','DECIMATE');dec.ratio=.09;bpy.ops.object.modifier_apply(modifier=dec.name)
    from mathutils.bvhtree import BVHTree
    bpy.context.view_layer.update();head_surface=BVHTree.FromObject(head,bpy.context.evaluated_depsgraph_get());paw_surface=BVHTree.FromObject(arm,bpy.context.evaluated_depsgraph_get())
    def project(surface,x,z):
        hit=surface.ray_cast(Vector((x,-.6,z)),Vector((0,1,0)),1.2)[0]
        if hit is None:raise ValueError('Paint misses its ceramic surface: '+str((x,z)))
        return hit.y-.00065
    def face_y(x,z):return project(head_surface,x,z)
    for ob in list(bpy.context.scene.objects):
        if ob.name.startswith('Inset red ear enamel'):bpy.data.objects.remove(ob,do_unlink=True)
    for side in [-1,1]:
        x=-.025+side*.116;corners=[Vector((x-.015,.516)),Vector((x+.015,.516)),Vector((x+side*.010,.547))];vs=[];index={};fs=[];steps=14
        for i in range(steps+1):
            for j in range(steps+1-i):
                p=corners[0]+(corners[1]-corners[0])*i/steps+(corners[2]-corners[0])*j/steps;index[i,j]=len(vs);vs.append((p.x,face_y(p.x,p.y),p.y))
        for i in range(steps):
            for j in range(steps-i):
                fs.append((index[i,j],index[i+1,j],index[i,j+1]))
                if i+j<steps-1:fs.append((index[i+1,j],index[i+1,j+1],index[i,j+1]))
        mesh('Red glaze painted on integrated ear',vs,fs,red)
    # Flat ribbons follow the porcelain surface; the face is painted, never tubular geometry.
    def stroke(name,path,width,material,surface=face_y,group='static'):
        control=[Vector(p) for p in path];points=[]
        for a,b in zip(control,control[1:]):
            steps=max(1,math.ceil((b-a).length/.002))
            points.extend(a.lerp(b,j/steps) for j in range(steps))
        points.append(control[-1]);vs=[]
        for i,p in enumerate(points):
            tangent=(points[min(i+1,len(points)-1)]-points[max(0,i-1)]).normalized();normal=Vector((-tangent.y,tangent.x))
            taper=.55+.45*math.sin(math.pi*i/max(1,len(points)-1))
            for s in [-1,1]:q=p+normal*width*taper*s/2;vs.append((q.x,surface(q.x,q.y),q.y))
        fs=[(2*i,2*i+2,2*i+3,2*i+1) for i in range(len(points)-1)]
        ob=mesh(name,vs,fs,material);ob['group']=group;return ob
    for side in [-1,1]:
        stroke('Fine painted closed eye',[(cx+side*.058+(i/16-.5)*.058,.426+.018*math.sin(i/16*math.pi)) for i in range(17)],.0036,ink)
        for j in range(3):
            stroke('Fine painted whisker',[(cx+side*(.098+i*.0074),.390-j*.015+(i/5-.5)*.005) for i in range(6)],.0020,ink)
        for x in [.098,.112]:stroke('Tiny forehead marking',[(cx+side*x,.475),(cx+side*(x-.002),.486)],.0019,ink)
    points=[(-.010,.402),(-.009,.406),(-.004,.407),(0,.404),(.004,.407),(.009,.406),(.010,.402),(0,.390)]
    mesh('Small coral heart nose',[(cx+x,face_y(cx+x,z)-.0003,z) for x,z in points],[tuple(reversed(range(len(points))))],pink)
    for side in [-1,1]:stroke('Delicate painted smile',[(cx,.391),(cx,.381),(cx+side*.005,.375),(cx+side*.012,.375),(cx+side*.020,.382)],.0021,pink)
    def palm_y(x,z):return project(paw_surface,x,z)
    for x in [.143,.157,.171]:stroke('Small paw ink mark',[(x,.418),(x,.434)],.0018,ink,palm_y,'beckoning_paw')

    # Wide red collar and a small gilt pendant, matching the reference's simple ornament.
    from valuable_meshkit import lathe
    collar=lathe('Raised external red luck collar',[(.230,.130),(.233,.141),(.237,.147),(.243,.148),(.258,.148),(.266,.143),(.274,.130)],red,48);collar.location.y=.012;collar.scale.y=.85
    cyl('Small gilt pendant',(-.014,-.129,.194),.039,.009,gold,40,'Y')
    border=torus('Pendant raised gold rim',(-.014,-.135,.194),.033,.0018,gold,40,(math.pi/2,0,0))
    for x in [-.09,-.045,.045,.09]:sphere('Small collar gilt stud',(x,.012-.148*.85*math.sqrt(1-(x/.148)**2)-.002,.247),(.0043,.0038,.0043),gold,12,8)
    # Original flat strokes for the traditional 福 luck seal; no bulky black cross.
    seal=[ [(-.020,.023),(-.014,.016)],[(-.024,.009),(-.010,.009),(-.019,-.002)],[(-.018,.001),(-.018,-.022)],[(-.017,-.004),(-.009,-.011)],
           [(-.002,.023),(.024,.023)],[(0,.015),(.022,.015),(.022,.003),(0,.003),(0,.015)],
           [(-.002,-.004),(.025,-.004),(.025,-.024),(-.002,-.024),(-.002,-.004)],[(.0115,-.004),(.0115,-.024)],[(-.002,-.014),(.025,-.014)] ]
    from pathlib import Path
    font_path=Path('C:/Windows/Fonts/msyhbd.ttc')
    if font_path.exists():
        text=bpy.data.curves.new('Traditional luck glyph','FONT');text.body='福';text.font=bpy.data.fonts.load(str(font_path));text.align_x='CENTER';text.align_y='CENTER';text.size=.052;text.extrude=.00055;text.bevel_depth=.00012;text.bevel_resolution=1;text.resolution_u=3
        ob=bpy.data.objects.new('Raised gilt luck character',text);bpy.context.collection.objects.link(ob);ob.location=(-.014,-.136,.194);ob.rotation_euler.x=math.pi/2;text.materials.append(gold)
        bpy.ops.object.select_all(action='DESELECT');ob.select_set(True);bpy.context.view_layer.objects.active=ob;bpy.ops.object.convert(target='MESH');ob.select_set(False)
    else:
        for line in seal:stroke('Fine gilt luck seal',[(x*.88-.014,z*.88+.194) for x,z in line],.0020,stamp,lambda x,z:-.136)
    tube('Short curled ceramic tail',[(-.05,.115,.059),(.015,.154,.058),(.092,.144,.068),(.130,.102,.102)],.018,white,12)
