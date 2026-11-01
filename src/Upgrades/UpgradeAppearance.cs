using System;
using UnityEngine;

namespace IncreaseHardMod.Upgrades;

/// <summary>Vanilla Upgrade Pack geometry/UVs with a private printed material and a separate UI render.</summary>
internal static class UpgradeAppearance
{
    // Shared identity for the completed inventory-slot upgrade wrapper.
    public const string InventoryArt = "Upgrade_Inventory.png";

    public static void Apply(GameObject prefab, string id, string atlasFile)
    {
        var atlas = UpgradeIcons.LoadTexture(atlasFile);
        if (atlas == null) return;
        atlas.wrapMode = TextureWrapMode.Clamp;
        atlas.filterMode = FilterMode.Bilinear;
        ApplyCard(prefab, id, atlas);

        // A UV atlas is not a shop icon: render the wrapped package into a transparent square.
        // Assigning the sprite also bypasses stale icons in the game's on-disk cache.
        var attributes = prefab.GetComponent<ItemAttributes>();
        attributes.icon = UpgradeIcons.Load("Icon_" + atlasFile);
    }

    private static Texture CardTexture(Material material)
    {
        if (material == null) return null;
        foreach (string property in new[] { "_MainTex", "_BaseMap" })
        {
            if (!material.HasProperty(property)) continue;
            var texture = material.GetTexture(property);
            if (texture != null && texture.name.StartsWith("Upgrade_", StringComparison.OrdinalIgnoreCase) &&
                texture.name.EndsWith("_Albedo", StringComparison.OrdinalIgnoreCase)) return texture;
        }
        return null;
    }
    private static bool IsCardMaterial(Material material) => CardTexture(material) != null;

    public static Texture FindCardTexture(GameObject prefab)
    {
        foreach (var renderer in prefab.GetComponentsInChildren<MeshRenderer>(true))
            foreach (var material in renderer.sharedMaterials)
                if (IsCardMaterial(material)) return CardTexture(material);
        return null;
    }

    public static void ApplyCard(GameObject prefab, string id, Texture atlas)
    {
        int changed = 0;
        foreach (var renderer in prefab.GetComponentsInChildren<MeshRenderer>(true))
        {
            var materials = renderer.sharedMaterials;
            bool modified = false;
            for (int slot = 0; slot < materials.Length; slot++)
            {
                // Match the vanilla printed atlas, not the largest texture (which could belong
                // to another mesh). Preserve unrelated slots, shaders, UVs, animation and physics.
                if (!IsCardMaterial(materials[slot])) continue;
                var material = new Material(materials[slot]) { name = "IHM Card " + id };
                foreach (string property in new[] { "_MainTex", "_BaseMap" })
                    if (material.HasProperty(property))
                    {
                        material.SetTexture(property, atlas);
                        material.SetTextureScale(property, Vector2.one);
                        material.SetTextureOffset(property, Vector2.zero);
                    }
                materials[slot] = material;
                modified = true;
                changed++;
            }
            if (modified) renderer.sharedMaterials = materials;
        }
        if (changed == 0)
            Plugin.Log.LogWarning($"[Icons] No vanilla upgrade-pack material found for '{id}'; keeping native appearance.");
    }
}
