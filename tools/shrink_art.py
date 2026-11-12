"""Repack the embedded PNG textures in src/Art without changing a single pixel the game sees.

    python tools/shrink_art.py [--check]

- A colour PNG whose three channels are identical in every pixel (the baked shading of costume parts, which the game
  multiplies by the part's colour) is stored as 8-bit greyscale. Unity's LoadImage expands it back to R = G = B.
- Every PNG is rewritten at the strongest deflate setting, and kept only if that is smaller.
Run it after any build that writes textures (build_courier.py, build_*costumes*.py, ...). --check only reports.
"""
import io
import pathlib
import sys

import numpy as np
from PIL import Image

ART = pathlib.Path(__file__).resolve().parent.parent / 'src' / 'Art'
check = '--check' in sys.argv
before = after = grey = touched = 0
for path in sorted(ART.glob('*.png')):
    size = path.stat().st_size
    before += size
    with Image.open(path) as image:
        image.load()
        out = image
        if image.mode == 'RGB':
            a = np.asarray(image)
            if (a[..., 0] == a[..., 1]).all() and (a[..., 1] == a[..., 2]).all():
                out = image.convert('L'); grey += 1
        elif image.mode not in ('L', 'RGBA', 'LA', 'P'):
            after += size; continue   # 16-bit and other rare formats are left alone
        buffer = io.BytesIO()
        out.save(buffer, 'PNG', optimize=True)
    if buffer.tell() < size:
        # The repacked file must decode to exactly the same pixels.
        with Image.open(io.BytesIO(buffer.getvalue())) as again:
            same = np.array_equal(np.asarray(again.convert('RGBA')), np.asarray(image.convert('RGBA')))
        if not same: raise SystemExit(f'{path.name}: repacked pixels differ')
        if not check: path.write_bytes(buffer.getvalue())
        after += buffer.tell(); touched += 1
    else:
        after += size
print(f'{"would shrink" if check else "shrank"} {touched} file(s), {grey} to greyscale: {before / 1e6:.1f} MB -> {after / 1e6:.1f} MB')
