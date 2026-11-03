from pathlib import Path
from PIL import Image,ImageDraw,ImageFont
import json,zipfile,hashlib
ROOT=Path(__file__).resolve().parents[1];OUT=ROOT/'art/valuables/skull-quartz-10'
row=next(r for r in json.loads((ROOT/'art/valuables/interaction-rework-09/manifest.json').read_text()) if r['asset']=='crystal_skull')
assert row['checks']['glb']['status']==row['checks']['obj']['status']=='PASS' and row['blend_reopen']=='PASS'
model=json.loads((OUT/'v09_crystal_skull.mesh.json').read_text())
assert all(m['transmission']==1 and abs(m['roughness']-.055)<1e-5 for m in model['materials'])
for part in model['parts']:
    p=part['rest']['position']
    for v in part['vertices']:
        x,y,z=[v[a]+p[a] for a in ['x','y','z']]
        # Unity axes: Y is height, Z is the anterior/posterior axis.
        assert not (.020<abs(x)<.072 and .183<y<.200 and z<-.046),'Projecting brow geometry in the cleared orbit'
row['checks']['cleared_brow_band']='PASS';row['checks']['original_quartz_parameters']='PASS'
(OUT/'manifest.json').write_text(json.dumps([row],indent=2))
font=ImageFont.truetype('C:/Windows/Fonts/arial.ttf',22)
sheet=Image.new('RGB',(1600,860),'#151d23');d=ImageDraw.Draw(sheet)
for i,(name,label) in enumerate([('quartz_shape_view.png','COLORLESS CUT QUARTZ / ACTUAL MESH'),('clay_shape_view.png','SAME GEOMETRY / CLAY SHAPE CHECK')]):
    im=Image.open(OUT/name).convert('RGB').resize((800,800),Image.Resampling.LANCZOS);sheet.paste(im,(i*800,0));d.text((i*800+18,818),label,font=font,fill='#dae4e9')
sheet.save(OUT/'skull_comparison.png')
sheet.save(OUT/'skull_cheek_refinement.png')
(OUT/'README.md').write_text('''# Crystal skull — quartz and shape revision 10

Restores the approved faceted colorless quartz material (transmission 1, IOR 1.46, roughness 0.055). The projecting orbital shelf geometry is removed by recutting larger, rounded-square sockets. The cheek arches are thinner, narrower and recessed into the face to remove the oversized lateral knobs. The lower jaw closes the former tooth gap while retaining the rounded chin. Shape reference: the user-supplied frosted anatomical skull photo.

GLB, OBJ/MTL and editable Blender source are included. The two comparison panels render exactly the same mesh in quartz and neutral clay. Clay is a preview-only material. The runtime JSON also updates the current revision-09 collection and embedded mod model.

GLB/OBJ reimport, topology, UV/materials, bounds, runtime winding/normals and Blender reopening pass. The old projecting brow band and quartz parameters are checked. Studio material appearance is verified in Blender; native game glass and lighting remain untested. No proprietary game images/fonts are included.
''',encoding='utf-8')
files=sorted(p for p in OUT.iterdir() if p.is_file() and p.name!='SHA256SUMS.txt' and p.suffix!='.blend1')
(OUT/'SHA256SUMS.txt').write_text(''.join(hashlib.sha256(p.read_bytes()).hexdigest()+'  '+p.name+'\n' for p in files))
archive=OUT.parent/'Crystal_Skull_Quartz_10_Cheek_Refinement.zip'
with zipfile.ZipFile(archive,'w',zipfile.ZIP_DEFLATED) as z:
    for p in files+[OUT/'SHA256SUMS.txt']:z.write(p,'skull-quartz-10/'+p.name)
with zipfile.ZipFile(archive) as z:assert z.testzip() is None
import shutil
shutil.copyfile(archive,OUT.parent/'Crystal_Skull_Quartz_10.zip')
print('SKULL10_PACKAGE_PASS',row['triangles'],'triangles',archive.stat().st_size,'bytes')
