# Gun upgrade artwork — 0.6.4

Generated using the built-in imagegen tool, then rendered onto the authored native-size UV wrapper with Blender. No API/CLI image-generation fallback was used. `tools/build_upgrade_art.py` performs the existing 512px texture import and 256px transparent icon render pipeline.

The bullet, laser and tranquilizer upgrades now have different front illustrations and separate shop/inventory renders. Drone Battery Life retains its existing drone artwork.

Final files in the production repository:

- `C:/Users/kubai/IdeaProjects/Repo-Increase-Difficulty-Mod/src/Textures/Upgrade_Gun-Bullet-Count.png`
- `C:/Users/kubai/IdeaProjects/Repo-Increase-Difficulty-Mod/src/Textures/Upgrade_Gun-Laser-Capacity.png`
- `C:/Users/kubai/IdeaProjects/Repo-Increase-Difficulty-Mod/src/Textures/Upgrade_Gun-Tranq-Capacity.png`
- Each atlas has a matching transparent `Icon_` PNG in the same directory.

Preview: `gun_upgrade_lineup_0.6.4.png`. Editable wrapper scene: `gun_upgrade_wrappers_0.6.4.blend`.

## Final prompt set

Common request: edit the existing bullet upgrade atlas as a flat unwrapped game albedo texture. Preserve the normalized native UV islands, seams, labels, barcode, fine print and black unused region. Use distressed matte cartoon print, cream ink and chunky dark outlines. Keep the main illustration within the front panel; no robot, hands, glow, glossy badges or product photography.

- **Bullet:** three chunky rifle cartridges beside a square battery with lightning symbol; muted rust-orange packaging.
- **Laser:** a chunky laser emitter with a straight beam ending in a starburst, beside a square lightning battery; dusty-blue packaging. Final correction: shorten/angle the beam so emitter, beam and starburst remain inside front-panel x53–82%, y55–91%; leave the side panel's original labels and background clear of the illustration.
- **Tranq:** a large tranquilizer dart with clear needle, cylindrical barrel and feathered tail beside a square lightning battery; sage-green packaging. No beam or ordinary cartridge.

Style/edit reference: the existing `Upgrade_Gun-Bullet-Count.png` atlas from 0.6.3. The first laser variant was rejected because the starburst crossed a side seam; only the corrected variant is shipped.
