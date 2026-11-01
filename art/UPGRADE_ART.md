# Upgrade packaging

Custom upgrades use the native `Upgrade Pack` mesh, UV layout, material shader, animation,
colliders and pickup behaviour. The printed atlases in `src/Textures/Upgrade_*.png` are
separate from their transparent `Icon_Upgrade_*.png` shop/inventory renders.
`UpgradeAppearance` replaces only material slots holding a vanilla `Upgrade_*_Albedo`
texture, using private material instances. Other renderers and shared vanilla assets
are preserved. Map Suite continues to use the native map upgrade artwork.

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
