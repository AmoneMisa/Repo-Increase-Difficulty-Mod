"""Portable authoring/reimport files and a preview assembled from actual mesh renders."""
from pathlib import Path
import zipfile,json
from PIL import Image
ROOT=Path(__file__).resolve().parents[1];OUT=ROOT/'art/valuables/lighting-collection-11'
frames=ROOT/'work/neko11-motion-frames'
images=[Image.open(frames/('%03d.png'%i)).convert('RGB') for i in range(24)]
images[0].save(OUT/'maneki_neko/maneki_neko_motion.gif',save_all=True,append_images=images[1:],duration=112,loop=0)
for im in images:im.close()
rows=json.loads((OUT/'validation.json').read_text());assert len(rows)==6
dest=ROOT/'bin/thunderstore/Valuables-Lighting-11-Models.zip'
with zipfile.ZipFile(dest,'w',zipfile.ZIP_DEFLATED,compresslevel=6) as archive:
 for path in sorted(OUT.rglob('*')):
  if path.is_file() and path.suffix in ['.glb','.obj','.mtl','.blend','.png','.json','.gif','.md']:archive.write(path,path.relative_to(OUT))
with zipfile.ZipFile(dest) as archive:assert archive.testzip() is None
print('ASSET_PACK_PASS',dest,dest.stat().st_size)
