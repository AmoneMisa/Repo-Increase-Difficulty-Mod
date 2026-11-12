using System.Collections.Generic;
using UnityEngine;

namespace IncreaseHardMod.Courier;

internal static class CourierSurface
{
    private static readonly Dictionary<string, Texture2D> Normals = new();

    internal static void Apply(Material material, string key, bool eyes, bool textured)
    {
        if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", eyes ? .55f : .08f);
        if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", eyes ? .55f : .08f);
        if (!textured || eyes) return;
        string property = material.HasProperty("_NormalTexture") ? "_NormalTexture" : material.HasProperty("_BumpMap") ? "_BumpMap" : null;
        if (property == null) return;
        if (!Normals.TryGetValue(key, out var texture))
        {
            try { texture = Relief(key); }
            catch (System.Exception e) { Plugin.Log.LogWarning($"[Courier] Surface relief of {key} failed: {e.Message}"); texture = null; }
            Normals[key] = texture;
        }
        if (texture == null) return;
        material.SetTexture(property, texture);
        material.EnableKeyword("_NORMALMAP");
        if (material.HasProperty("_BumpScale")) material.SetFloat("_BumpScale", .55f);
        if (material.HasProperty("_NormalStrength")) material.SetFloat("_NormalStrength", .55f);
    }

    /// <summary>
    /// The pet's surface-normal map, made once from its atlas instead of being shipped as a second texture: a small
    /// relief from the pattern's brightness (scales, feathers, cracks), flat where there is no detail. Same formula
    /// the art build used to bake (tools/build_courier.py), so the look is unchanged.
    /// </summary>
    private static Texture2D Relief(string key)
    {
        var atlas = Upgrades.UpgradeIcons.LoadTexture(key + "_atlas.png");
        if (atlas == null || !atlas.isReadable) return null;
        int w = atlas.width, h = atlas.height;
        var source = atlas.GetPixels32();
        var light = new float[source.Length];
        for (int i = 0; i < source.Length; i++) light[i] = (source[i].r + source[i].g + source[i].b) / 765f;
        var pixels = new Color32[source.Length];
        for (int y = 0; y < h; y++)
        {
            int up = (y + 1) % h * w, down = (y + h - 1) % h * w, row = y * w;
            for (int x = 0; x < w; x++)
            {
                float dx = Mathf.Clamp((light[row + (x + 1) % w] - light[row + (x + w - 1) % w]) * 1.2f, -.2f, .2f);
                float dy = Mathf.Clamp((light[up + x] - light[down + x]) * 1.2f, -.2f, .2f);
                float nz = Mathf.Sqrt(Mathf.Max(0f, 1f - 4f * dx * dx - 4f * dy * dy)) * .5f + .5f;
                pixels[row + x] = new Color32((byte)Mathf.RoundToInt((.5f - dx) * 255f), (byte)Mathf.RoundToInt((.5f - dy) * 255f),
                    (byte)Mathf.RoundToInt(nz * 255f), 255);
            }
        }
        // Normal vectors must be sampled in linear space, independently of albedo caching.
        var texture = new Texture2D(w, h, TextureFormat.RGBA32, true, true) { name = key + " normals",
            wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear, hideFlags = HideFlags.DontUnloadUnusedAsset };
        texture.SetPixels32(pixels);
        texture.Apply(true, true);
        return texture;
    }
}
