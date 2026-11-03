"""Package the mesh assets after Blender export/reimport validation."""
from pathlib import Path
from PIL import Image
import json,zipfile,hashlib
root=Path(__file__).resolve().parents[1]
folder=root/'art'/'valuables'/'heavy-bomb-reference-05'
data=json.loads((folder/'validation.json').read_text());r=data[0]
assert all(r['round_trip_imports'][k]['status']=='PASS' for k in ['glb','obj'])
assert r['blend_reopen']['status']=='PASS'
sheet=Image.new('RGB',(1560,630))
for i,view in enumerate(['threequarter','side']):
    pic=Image.open(folder/f'{r["asset"]}_{view}.png').convert('RGB').resize((780,630),Image.Resampling.LANCZOS)
    sheet.paste(pic,(i*780,0))
sheet.save(folder/'Bomb_Reference_05_Preview.jpg',quality=95)
b=r['bounds_m_xyz']
(folder/'README.md').write_text(f'''# Heavy bomb — museum photo reference revision 05

Original low-poly visual prop rebuilt from the user's supplied museum photograph. Features: broad barrel-shaped olive casing, tapered rear, four exposed swept tail fins, a rounded yellow/ochre nose cap and rim, two yellow bands with red paint blocks, and five dark circumferential seams. The museum stand is excluded. This is a simplified visual interpretation; details hidden in the photo are inferred.

## Included assets

- `{r['asset']}.glb`: self-contained mesh with PBR color materials.
- `{r['asset']}.obj` plus `.mtl`: triangulated mesh and basic material fallback; keep both together.
- `{r['asset']}.blend`: editable source with the asset under the named origin parent. Studio objects named `Preview_` are excluded from mesh exports.
- Three actual geometry renders: three-quarter, side, rear; plus a combined preview.
- `validation.json` and `SHA256SUMS.txt`.

{r['triangles']:,} triangles; {r['mesh_parts']} mesh parts; {r['materials']} materials. Dimensions in Blender X/Y/Z: {b[0]:.3f} x {b[1]:.3f} x {b[2]:.3f} m. One unit equals one meter. Blender is Z up; GLB and OBJ are Y up. The nose points along +X. The parent is at ground level beneath the main body.

The shell uses smooth normals on low-poly geometry; fins retain hard edges. Paint bands and blocks use surface material assignments, with no floating decal planes or required external textures. UVs are unwrapped independently per mesh; no shared baked texture atlas is included. Closed components intersect as an assembled prop rather than a fused solid. Parts sharing a material can be combined in an engine pipeline.

## Validation and limits

Both exports were imported into fresh Blender scenes. Matching triangle/part counts, dimensions within 0.00002 m, UVs, materials, finite coordinates, ground height, no degenerate faces, and manifold components after welding exported normal/UV seams were verified. GLB header/file-length checks passed. The saved Blender source reopened with the expected asset hierarchy. The three rendered views were visually reviewed. Full results are in validation.json.

These checks establish mesh/export integrity, not tested R.E.P.O. integration. No physics, colliders, explosion behavior, damage, sound, loot registration or game prefab setup is included. The model contains exterior visual geometry only. No player-facing UI text or native game assets have been added.

Rebuild from the repository root with Blender using `--background --factory-startup --python tools/build_bomb_reference05.py`, then run `tools/validate_bomb_reference05.py` with Blender and `tools/package_bomb_reference05.py` with Python/Pillow.
''',encoding='utf-8')
files=sorted(p for p in folder.rglob('*') if p.is_file() and p.name!='SHA256SUMS.txt')
(folder/'SHA256SUMS.txt').write_text('\n'.join(hashlib.sha256(p.read_bytes()).hexdigest()+'  '+p.relative_to(folder).as_posix() for p in files)+'\n')
archive=folder.parent/'Heavy_Bomb_Reference_05.zip'
with zipfile.ZipFile(archive,'w',zipfile.ZIP_DEFLATED) as z:
    for p in sorted(folder.rglob('*')):
        if p.is_file():z.write(p,Path(folder.name)/p.relative_to(folder))
with zipfile.ZipFile(archive) as z:assert z.testzip() is None
print('BOMB_REFERENCE_05_PACKAGE_VERIFIED',archive,archive.stat().st_size)
