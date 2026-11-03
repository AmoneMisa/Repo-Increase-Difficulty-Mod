# Heavy bomb — museum photo reference revision 05

Original low-poly visual prop rebuilt from the user's supplied museum photograph. Features: broad barrel-shaped olive casing, tapered rear, four exposed swept tail fins, a rounded yellow/ochre nose cap and rim, two yellow bands with red paint blocks, and five dark circumferential seams. The museum stand is excluded. This is a simplified visual interpretation; details hidden in the photo are inferred.

## Included assets

- `Heavy_Bomb_Reference_05.glb`: self-contained mesh with PBR color materials.
- `Heavy_Bomb_Reference_05.obj` plus `.mtl`: triangulated mesh and basic material fallback; keep both together.
- `Heavy_Bomb_Reference_05.blend`: editable source with the asset under the named origin parent. Studio objects named `Preview_` are excluded from mesh exports.
- Three actual geometry renders: three-quarter, side, rear; plus a combined preview.
- `validation.json` and `SHA256SUMS.txt`.

4,336 triangles; 16 mesh parts; 7 materials. Dimensions in Blender X/Y/Z: 2.867 x 1.680 x 1.680 m. One unit equals one meter. Blender is Z up; GLB and OBJ are Y up. The nose points along +X. The parent is at ground level beneath the main body.

The shell uses smooth normals on low-poly geometry; fins retain hard edges. Paint bands and blocks use surface material assignments, with no floating decal planes or required external textures. UVs are unwrapped independently per mesh; no shared baked texture atlas is included. Closed components intersect as an assembled prop rather than a fused solid. Parts sharing a material can be combined in an engine pipeline.

## Validation and limits

Both exports were imported into fresh Blender scenes. Matching triangle/part counts, dimensions within 0.00002 m, UVs, materials, finite coordinates, ground height, no degenerate faces, and manifold components after welding exported normal/UV seams were verified. GLB header/file-length checks passed. The saved Blender source reopened with the expected asset hierarchy. The three rendered views were visually reviewed. Full results are in validation.json.

These checks establish mesh/export integrity, not tested R.E.P.O. integration. No physics, colliders, explosion behavior, damage, sound, loot registration or game prefab setup is included. The model contains exterior visual geometry only. No player-facing UI text or native game assets have been added.

Rebuild from the repository root with Blender using `--background --factory-startup --python tools/build_bomb_reference05.py`, then run `tools/validate_bomb_reference05.py` with Blender and `tools/package_bomb_reference05.py` with Python/Pillow.
