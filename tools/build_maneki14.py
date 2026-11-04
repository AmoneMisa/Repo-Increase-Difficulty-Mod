"""New reference-shaped ceramic sculpt. Isolated approval assets; no runtime writes.

Blender --background --factory-startup --python tools/build_maneki14.py
Coordinates are hand-authored reference landmarks, not inherited v11/v13 geometry.
"""
import bpy, math, json, sys
from mathutils import Vector
from pathlib import Path

ROOT=Path(__file__).resolve().parents[1]
OUT=ROOT/'art/valuables/maneki-reference-14'
OUT.mkdir(parents=True,exist_ok=True)
S=.0055
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)

def world(p):
    x,y,z=p
    return ((x-150)*S,y*S,(294-z)*S)

def mat(name,color,rough=.24,metal=0,texture=None):
    m=bpy.data.materials.new(name);m.use_nodes=True
    b=m.node_tree.nodes.get('Principled BSDF')
    b.inputs['Base Color'].default_value=(*color,1)
    b.inputs['Roughness'].default_value=rough;b.inputs['Metallic'].default_value=metal
    b.inputs['Coat Weight'].default_value=.28 if not metal else .12
    b.inputs['Coat Roughness'].default_value=.19
    if texture:
        t=m.node_tree.nodes.new('ShaderNodeTexImage');t.image=bpy.data.images.load(str(OUT/'textures'/texture));t.image.pack();t.extension='EXTEND'
        m.node_tree.links.new(t.outputs['Color'],b.inputs['Base Color'])
    return m

white=mat('Ivory white glazed porcelain',(.93,.915,.85),.23)
face=mat('Flat fired face and inner ear painting',(.93,.915,.85),.23,texture='head_painted.png')
pawpaint=mat('Three painted paw marks',(.93,.915,.85),.23,texture='paw_painted.png')
red=mat('Vermilion glazed collar',(.42,.002,.005),.29)
gold=mat('Warm antique gold',(.75,.44,.075),.27,.78)
coinmat=mat('Fuku medallion gold',(.75,.44,.075),.29,.72,'medallion_gold.png')

def mesh(name,verts,faces,material):
    me=bpy.data.meshes.new(name);me.from_pydata(verts,[],faces);me.update()
    ob=bpy.data.objects.new(name,me);bpy.context.collection.objects.link(ob)
    ob.data.materials.append(material)
    for p in me.polygons:p.use_smooth=True
    bpy.context.view_layer.objects.active=ob;ob.select_set(True)
    # Ensure consistent outward winding for custom lofts.
    bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT');bpy.ops.mesh.normals_make_consistent(inside=False);bpy.ops.object.mode_set(mode='OBJECT')
    ob.select_set(False)
    return ob

def catmull(rows,steps):
    out=[]
    for i in range(len(rows)-1):
        a=rows[max(0,i-1)];b=rows[i];c=rows[i+1];d=rows[min(len(rows)-1,i+2)]
        for j in range(steps):
            t=j/steps
            out.append([.5*((2*b[k])+(-a[k]+c[k])*t+(2*a[k]-5*b[k]+4*c[k]-d[k])*t*t+(-a[k]+3*b[k]-3*c[k]+d[k])*t*t*t) for k in range(len(b))])
    return out+[rows[-1]]

def loft(name,rows,material=white,segments=64,steps=7,exponent=.94):
    # Each sculpted contour: image height, center X, depth center,
    # horizontal half-width, front depth, back depth.
    rows=catmull(rows,steps);vs=[];fs=[]
    for z,x,y,rx,df,db in rows:
        rx=max(.15,rx);df=max(.15,df);db=max(.15,db)
        for i in range(segments):
            t=2*math.pi*i/segments;c=math.cos(t);s=math.sin(t)
            vs.append(world((x+rx*math.copysign(abs(c)**exponent,c),y+(df if s<0 else db)*math.copysign(abs(s)**exponent,s),z)))
    for j in range(len(rows)-1):
        for i in range(segments):
            a=j*segments+i;b=j*segments+(i+1)%segments
            fs.append((a,b,b+segments,a+segments))
    fs.extend([tuple(reversed(range(segments))),tuple((len(rows)-1)*segments+i for i in range(segments))])
    return mesh(name,vs,fs,material)

def fuse(name,objects,voxel=.75,iterations=5,ratio=.35):
    bpy.ops.object.select_all(action='DESELECT')
    for ob in objects:ob.select_set(True)
    bpy.context.view_layer.objects.active=objects[0];bpy.ops.object.join();ob=objects[0];ob.name=name
    mod=ob.modifiers.new('Continuous sculpt union','REMESH');mod.mode='VOXEL';mod.voxel_size=voxel*S
    bpy.ops.object.modifier_apply(modifier=mod.name)
    mod=ob.modifiers.new('Ceramic transitions','SMOOTH');mod.factor=.8;mod.iterations=iterations
    bpy.ops.object.modifier_apply(modifier=mod.name)
    mod=ob.modifiers.new('Game surface budget','DECIMATE');mod.ratio=ratio
    bpy.ops.object.modifier_apply(modifier=mod.name)
    ob.data.validate(verbose=True);ob.data.update()
    for p in ob.data.polygons:p.use_smooth=True
    ob.select_set(False)
    return ob

def paint_project(ob,material,box):
    ob.data.materials.clear();ob.data.materials.append(white);ob.data.materials.append(material)
    uv=ob.data.uv_layers.new(name='Ceramic paint')
    x0,z0,x1,z1=box
    for poly in ob.data.polygons:
        poly.material_index=1 if poly.normal.y<-.04 else 0
        for li in poly.loop_indices:
            v=ob.data.vertices[ob.data.loops[li].vertex_index].co
            px=v.x/S+150;pz=294-v.z/S
            # Clamp UVs themselves: OBJ/MTL readers need not preserve sampler clamp.
            uv.data[li].uv=(min(.999,max(.001,(px-x0)/(x1-x0))),min(.999,max(.001,1-(pz-z0)/(z1-z0))))

# Broad, seated pear-shaped torso with separately directed haunch contours.
core=loft('Torso contour',[(128,140,8,30,24,24),(145,141,8,53,38,34),(157,141,7,57,40,36),(181,142,8,65,42,43),(214,143,10,71,52,50),(248,145,11,71,54,51),(275,146,7,63,43,45),(289,145,4,45,27,31),(292,145,4,20,14,20)])
haunch=loft('Right folded haunch',[(181,190,8,2,6,7),(194,202,4,13,20,20),(212,211,0,22,28,27),(234,210,-3,26,30,31),(254,198,-10,28,29,32),(271,182,-20,23,25,26),(282,169,-27,16,19,19),(286,165,-27,7,10,10)])
left=loft('Left folded haunch',[(180,97,7,2,6,7),(197,88,3,15,19,22),(222,77,-1,21,23,25),(246,74,-3,18,24,26),(265,82,-13,18,22,22),(277,86,-22,13,17,16),(282,87,-22,5,9,9)])
down=loft('Lowered foreleg and softly bent paw',[(167,92,-15,9,15,16),(182,91,-20,16,20,20),(206,97,-26,17,20,21),(232,107,-29,18,19,22),(258,117,-35,18,18,22),(276,127,-38,22,21,23),(288,128,-38,17,17,17),(292,127,-37,4,7,7)])
foot_r=loft('Rounded right toe pad',[(259,169,-33,5,8,9),(266,169,-38,13,16,15),(276,169,-39,16,19,17),(284,169,-37,13,15,14),(288,169,-35,4,7,7)])
foot_l=loft('Rounded left toe pad',[(257,83,-27,5,8,8),(264,83,-33,12,15,14),(274,85,-34,15,17,15),(281,87,-31,10,12,12),(284,88,-30,3,6,6)])
body=fuse('Porcelain body - continuous seated sculpt',[core,haunch,left,down,foot_r,foot_l],.80,8,.035)

head=loft('Head cheek and crown contours',[(24,137,0,12,12,12),(27,137,0,36,28,29),(36,136,0,58,39,40),(53,135,0,69,47,47),(77,135,0,75,52,53),(103,136,0,76,53,54),(127,137,0,68,46,49),(145,139,0,58,33,36),(156,140,0,34,18,20),(160,140,0,4,6,7)],steps=8)
ear_l=loft('Left ear integrated into crown',[(23,71,0,2,3,4),(25,73,0,8,10,11),(33,77,0,14,17,17),(45,79,0,18,24,22),(60,82,0,21,29,26),(68,83,0,13,22,21)],steps=6)
ear_r=loft('Right ear integrated into crown',[(7,181,1,2,4,4),(11,180,1,8,11,11),(23,179,1,15,19,18),(39,178,1,22,28,26),(55,176,1,25,33,31),(63,176,1,17,25,26)],steps=6)
head=fuse('Porcelain head with grown-in rounded ears',[head,ear_l,ear_r],.65,7,.030)
paint_project(head,face,(48,0,220,168))

# Raised arm is curved and tapered; the small paw is an integral end of the loft.
# Retained as a separate rigid piece for the later approved animation pass.
arm=loft('Raised right arm and cat paw',[(60,229,-2,3,5,5),(62,230,-2,10,12,10),(68,230,-3,15,16,13),(80,229,-4,17,18,15),(91,226,-1,15,16,16),(105,224,5,13,16,17),(126,226,9,15,19,21),(145,224,10,18,21,23),(163,219,9,17,21,23),(179,212,8,12,18,21),(187,206,7,4,9,10)],segments=40,steps=4)
paint_project(arm,pawpaint,(208,58,250,103))
pivot=bpy.data.objects.new('Paw hinge - provisional shoulder axis',None);bpy.context.collection.objects.link(pivot);pivot.location=world((208,8,171));pivot.empty_display_size=.05;pivot.empty_display_type='ARROWS'
arm.parent=pivot;arm.matrix_parent_inverse=pivot.matrix_world.inverted()
# Parenting after a dependency update avoids changing the authored world positions.
bpy.context.view_layer.update();arm.matrix_parent_inverse=pivot.matrix_world.inverted()

# Thick exposed collar band, shaped around the neck with a dipped front edge.
vs=[];fs=[];n=128;m=12
for i in range(n):
    t=2*math.pi*i/n;front=max(0,-math.sin(t));side=math.cos(t)
    for j in range(m):
        q=2*math.pi*j/m
        z=146+front*15+math.sin(q)*(11+front*2)
        rx=65+math.cos(q)*3
        dep=34+math.cos(q)*3
        vs.append(world((140+rx*side,dep*math.sin(t),z)))
for i in range(n):
    for j in range(m):fs.append((i*m+j,((i+1)%n)*m+j,((i+1)%n)*m+(j+1)%m,i*m+(j+1)%m))
collar=mesh('Visible vermilion collar band',vs,fs,red)

def uvball(name,p,scale,material):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=24,ring_count=12,location=world(p))
    ob=bpy.context.object;ob.name=name;ob.scale=tuple(v*S for v in scale);ob.data.materials.append(material)
    bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    for f in ob.data.polygons:f.use_smooth=True
    ob.select_set(False);return ob

for x,z,y in [(99,164,-30),(116,169,-36),(165,169,-36),(184,161,-27)]:
    uvball('Small collar gilt ornament',(x,y-2,z),(4,2.5,4.5),gold)

# The small round pendant sits clearly in front of the neck and chest.
uvball('Pendant eyelet',(144,-41,177),(4,2.7,5.5),gold)
coin=uvball('Round gold pendant',(146,-45,199),(25,5,26),coinmat)
uv=coin.data.uv_layers.active
for poly in coin.data.polygons:
    for li in poly.loop_indices:
        v=coin.data.vertices[coin.data.loops[li].vertex_index].co
        uv.data[li].uv=(.5+v.x/(50*S),.5+v.z/(52*S))

assets=[o for o in bpy.context.scene.objects if o.type=='MESH']
for ob in assets:
    ob['stage']='Geometry approval only; animation and runtime integration pending'
    if not ob.data.uv_layers:ob.data.uv_layers.new(name='UVMap')

# Own ground origin and horizontal bounds centre; no donor transforms are used.
bpy.context.view_layer.update()
points=[ob.matrix_world@v.co for ob in assets for v in ob.data.vertices]
lo=Vector([min(p[k] for p in points) for k in range(3)])
hi=Vector([max(p[k] for p in points) for k in range(3)])
origin=Vector(((lo.x+hi.x)*.5,(lo.y+hi.y)*.5,lo.z))
for ob in assets+[pivot]:
    if ob.parent is None:ob.location-=origin
bpy.context.view_layer.update()

# A quiet product studio, with physically rendered reflections on real geometry.
scene=bpy.context.scene;scene.render.engine='CYCLES';scene.cycles.samples=48
scene.cycles.use_denoising=True
scene.world.color=(.7,.7,.7);scene.world.use_nodes=True
scene.world.node_tree.nodes['Background'].inputs['Color'].default_value=(.84,.86,.88,1)
scene.world.node_tree.nodes['Background'].inputs['Strength'].default_value=.32
floor=mat('Studio matte warm grey',(.87,.88,.89),.95)
floor.node_tree.nodes.get('Principled BSDF').inputs['Coat Weight'].default_value=0
bpy.ops.mesh.primitive_plane_add(size=2000,location=(0,0,-.001));ground=bpy.context.object;ground.name='Studio ground - excluded from exports';ground.data.materials.append(floor)
def area(name,pos,energy,size):
    bpy.ops.object.light_add(type='AREA',location=pos);o=bpy.context.object;o.name=name;o.data.energy=energy;o.data.shape='DISK';o.data.size=size;o.rotation_euler=(Vector((0,0,.8))-o.location).to_track_quat('-Z','Y').to_euler()
area('Large softbox left',(-3,-4,5),450,4)
area('Front fill',(3,-4,2.5),160,3)
area('Glaze rim',(2,2,4),400,3)
bpy.ops.object.camera_add();cam=bpy.context.object;cam.name='Review camera';scene.camera=cam;cam.data.type='ORTHO';cam.data.ortho_scale=1.90;cam.data.lens=65
scene.render.resolution_x=1100;scene.render.resolution_y=1100;scene.render.resolution_percentage=100
scene.view_settings.view_transform='AgX'
scene.view_settings.look='AgX - Medium High Contrast'
scene.render.image_settings.file_format='PNG'
scene.render.film_transparent=False
scene.unit_settings.system='METRIC'
scene['approval_status']='Awaiting user geometry review. Not integrated; no approved motion yet.'

def view(label,pos,target=(0,0,.80)):
    cam.location=pos;cam.rotation_euler=(Vector(target)-cam.location).to_track_quat('-Z','Y').to_euler()
    scene.render.filepath=str(OUT/f'maneki_{label}.png');bpy.ops.render.render(write_still=True)

# Preview files are actual Cycles renders of the saved/exported mesh.
cam.location=(0,-6,.83);cam.rotation_euler=(Vector((0,0,.80))-cam.location).to_track_quat('-Z','Y').to_euler()
bpy.ops.object.select_all(action='DESELECT')
for ob in assets:ob.select_set(True)
pivot.select_set(True)
bpy.context.view_layer.objects.active=head
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'maneki_review.blend'))
bpy.ops.export_scene.gltf(filepath=str(OUT/'maneki_review.glb'),use_selection=True,export_format='GLB',export_animations=False)
bpy.ops.wm.obj_export(filepath=str(OUT/'maneki_review.obj'),export_selected_objects=True,export_materials=True,path_mode='RELATIVE',forward_axis='NEGATIVE_Z',up_axis='Y')
# Packed images remain editable in the blend; OBJ uses explicit companion PNGs.
mtl_path=OUT/'maneki_review.mtl'
mtl_lines=mtl_path.read_text().splitlines()
mtl_path.write_text('\n'.join('map_Kd textures/'+Path(line.split()[-1]).name if line.startswith('map_Kd ') else line for line in mtl_lines)+'\n')
stats={'stage':'geometry approval, static exports','scale_m_per_reference_pixel':S,'dimensions_m_xyz':list(hi-lo),'origin':'base at z=0; x/y centred on own bounds','triangles':sum(sum(len(p.vertices)-2 for p in ob.data.polygons) for ob in assets),'objects':len(assets),'paw_pivot_m':list(pivot.location),'animation':'deliberately deferred until silhouette approval'}
(OUT/'geometry_stats.json').write_text(json.dumps(stats,indent=2))
view('front',(0,-6,.83))
view('three_quarter',(3,-6,1.6))
print('MANEKI14_COMPLETE',json.dumps(stats))
