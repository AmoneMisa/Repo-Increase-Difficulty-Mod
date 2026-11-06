# Upgrade packaging

Custom shop upgrades use the native `Upgrade Pack` mesh, UV layout, material shader, animation,
colliders and pickup behaviour. The printed atlases in `src/Textures/Upgrade_*.png` are
separate from their transparent `Icon_Upgrade_*.png` shop/inventory renders.
`UpgradeAppearance` replaces only material slots holding a vanilla `Upgrade_*_Albedo`
texture, using private material instances. Other renderers and shared vanilla assets
are preserved.

Since 0.8.0 most personal upgrades are skill tree perks and have no shop box. The upgrades still sold,
and so the only atlases shipped, are:

| Atlas | Upgrade | Front illustration | Packaging |
| --- | --- | --- | --- |
| Skill-Point | Skill Point | Skill-tree node: hexagon, star, plus badge | Teal |
| Additional-Life | Death Protection | Robot head, shield and plus | Brick red |
| Truck_Heal-Power | Heal Power (truck) | Boxy truck and medical plus | Sage |
| Truck_Charge-Power | Charge Efficiency (truck) | Boxy truck and battery/lightning | Ochre |
| Truck_Cache | Loot Cash-In (truck) | Boxy truck, coins and arrow | Tan brown |

## Rebuilding

The Skill Point atlas is drawn by `tools/build_skill_point_atlas.py` on top of the old bullet-gun
wrapper, kept as a source reference in `art/upgrade-reference/Upgrade_Gun-Bullet-Count.png` (not shipped):

```powershell
python tools/build_skill_point_atlas.py --root .
```

Render one atlas's transparent 256 × 256 UI icon, preview and editable scene:

```powershell
& 'C:/Program Files/Blender Foundation/Blender 5.2/blender.exe' --background --factory-startup --python tools/build_upgrade_art.py -- --root . --atlas Upgrade_Skill-Point.png
```

Without `--atlas` it renders every `src/Textures/Upgrade_*.png`.

`upgrade_wrappers.blend` and `upgrade_lineup.png` are older editable/preview sources of the original
packaging set (native package dimensions 0.176342 × 0.070177 × 0.267431 m).

## Artwork provenance

The hand-made atlases were edited with the built-in imagegen tool using the locally installed vanilla
health upgrade atlas as a style/UV reference. No extracted game geometry or source texture is shipped.
Each prompt kept the normalized vanilla UV islands, label positions, matte distressed print, chunky dark
outlines, cream paper labels, low saturation, minimal shading and a centred front illustration.
