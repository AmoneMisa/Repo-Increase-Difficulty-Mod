# Upgrade packaging

Custom upgrades use the native `Upgrade Pack` mesh, UV layout, material shader, animation,
colliders and pickup behaviour. The printed atlases in `src/Textures/Upgrade_*.png` are
separate from their transparent `Icon_Upgrade_*.png` shop/inventory renders.
`UpgradeAppearance` replaces only material slots holding a vanilla `Upgrade_*_Albedo`
texture, using private material instances. Other renderers and shared vanilla assets
are preserved. Map Suite uses a unique blue wrapper with a floorplan, compass and radar dots.

## Inventory upgrade integration

Inventory Slot is registered in 0.6.0 with +1 slot per purchase, capped at seven total slots. Its wrapper is connected through:

```csharp
PlayerUpgradeRegistry.Define(new PlayerUpgradeRegistry.Definition
{
    Id = InventorySlotsUpgrade.Id, // Gameplay is implemented in InventorySlotsUpgrade.
    IconFile = UpgradeAppearance.InventoryArt, // "Upgrade_Inventory.png"
});
```

The registry clones the physical vanilla pack, applies the inventory atlas, and assigns
`Icon_Upgrade_Inventory.png`. No additional model loader or gameplay component is required.
For an existing independently registered native pack, call
`UpgradeAppearance.Apply(prefab, id, UpgradeAppearance.InventoryArt)` before registration.

## Editable model and previews

`upgrade_wrappers.blend` contains authored cuboid wrappers at native package dimensions
(0.176342 × 0.070177 × 0.267431 m), packed textures, and the inventory variant.
Runtime reuses the game's geometry; this scene is an editable art/preview source.
`upgrade_lineup.png` shows the new packaging applied to the models.

Rebuild the model scene, transparent 256 × 256 UI icons, and review image:

```powershell
& 'C:/Program Files/Blender Foundation/Blender 5.2/blender.exe' --background --factory-startup --python tools/build_upgrade_art.py -- --root .
```

## Artwork provenance

Atlases were edited with the built-in imagegen tool using the locally installed vanilla
health upgrade atlas as a style/UV reference. No extracted game geometry or source texture
is shipped. Each prompt specified the same normalized vanilla UV islands, preserved label
positions, matte distressed print, chunky dark outlines, cream paper labels, low saturation,
minimal shading, no gloss/neon/bevelled badges, and a centered front illustration.
The per-asset subjects/palettes were:

| Atlas | Front illustration | Packaging |
| --- | --- | --- |
| HP-Regen | Robot and medical crosses | Dusty sage |
| Slower_Stamina_Use | Walking robot and lightning | Ochre |
| Additional-Life | Robot head, shield and plus | Brick red |
| Autoscan | Robot and radar | Grey teal |
| Ping (Anywhere) | Remote location marker, target and wall | Dusty blue |
| Ping_Info | Information card attached to a small location marker | Dusty teal |
| Pin | Thumbtack piercing a square map tile | Slate lavender |
| Gun-Bullet-Count | Robot, stubby gun and battery | Rust orange |
| Drone-Charge-Capacity | Robot, boxy drone and battery | Dusty lavender |
| Truck_Heal-Power | Boxy truck and medical plus | Sage |
| Truck_Charge-Power | Boxy truck and battery/lightning | Ochre |
| Truck_Cache | Boxy truck, coins and arrow | Tan brown |
| Inventory | Robot holding a box, three slots and plus | Grey olive |

Validation includes Blender renders, transparent icon coverage, resource pairing and a
.NET build. In-game lighting, outlines and host/client pickup behaviour still need a
play-test. The inventory wrapper uses the single InventorySlotsUpgrade implementation.

The three ping upgrades have separate atlases and UI renders. Their revised imagegen
prompts remove the robot and all body parts, preserve the atlas layout and packaging labels,
and place simple cream/dark pictograms within x53–82%, y55–91% of the atlas so they read
clearly at inventory-icon size. Ping Anywhere retains `Upgrade_Ping.png` for compatibility;
Ping Info uses `Upgrade_Ping_Info.png`, and Pin uses `Upgrade_Pin.png`.


## Map Suite

`src/Textures/Upgrade_Map_Suite.png` is the 512 × 512 printed atlas;
`src/Textures/Icon_Upgrade_Map_Suite.png` is the transparent 256 × 256 UI render.
`art/Upgrade_Map_Suite_preview.png` and `art/Upgrade_Map_Suite.blend` provide the isolated
preview and editable scene. Rebuild just this wrapper without rewriting other artwork:

```powershell
& 'C:/Program Files/Blender Foundation/Blender 5.2/blender.exe' --background --factory-startup --python tools/build_upgrade_art.py -- --root . --atlas Upgrade_Map_Suite.png
```

Generated with the built-in imagegen tool, using `Upgrade_Pin.png` as an edit reference.
Final prompt:

> Use case: precise-object-edit. Asset type: square UV texture atlas for a REPO game upgrade package. Input image is edit target and exact UV layout reference. Create unique MAP SUITE upgrade packaging by changing only the lavender ink to distressed dusty petrol blue and replacing the front thumbtack/map illustration with a chunky cream and near-black folded floorplan, compass rose and three radar dots, no robot. Preserve EVERY UV island boundary and all black empty area exactly: main panels y46%-100%, front panel x50%-85%, sides x36%-50% and x85%-100%; two flaps y30%-45%. Front illustration must stay inside x53%-82%, y55%-91%. Keep cream labels, barcode, scuffed paper, flat matte print and strong dark contours. Existing text labels stay at their exact positions; replace UPGRADE+++ with MAP SUITE in same cream label. No perspective, no 3D render, no neon, gloss, gradients, new badges or decorative background. Match reference texture style, original square proportions and native-game appearance.

The HUD now uses native TMP text under HUDCanvas and appends Map Suite after the active
modifier description. Wrapped content continues on TMP pages at the native body size,
with an eight-second page interval and a page counter. All pages of an entry are shown
before advancing to the next modifier. The map HUD remains available when Level Info is
turned off. Reopening the map starts at page one.

Validation: .NET build; atlas and icon dimensions/transparency; isolated Blender preview.
Live game validation is still required for native glow/softness, 4:3 / 16:9 / 21:9 layouts,
long descriptions, opening/closing the map, low/high upgrade levels and shop lighting.
