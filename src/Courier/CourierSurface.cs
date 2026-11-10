using System.Collections.Generic;
using System.IO;
using System.Reflection;
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
            var assembly = Assembly.GetExecutingAssembly();
            string suffix = "." + key + "_normal.png";
            foreach (string resource in assembly.GetManifestResourceNames())
            {
                if (!resource.EndsWith(suffix, System.StringComparison.Ordinal)) continue;
                using var input = assembly.GetManifestResourceStream(resource);
                using var bytes = new MemoryStream();
                input.CopyTo(bytes);
                // Normal vectors must be sampled in linear space, independently of albedo caching.
                texture = new Texture2D(2, 2, TextureFormat.RGBA32, true, true) { name = key + " normals",
                    wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear,
                    hideFlags = HideFlags.DontUnloadUnusedAsset };
                if (!texture.LoadImage(bytes.ToArray())) { Object.Destroy(texture); texture = null; }
                break;
            }
            Normals[key] = texture;
        }
        if (texture == null) return;
        material.SetTexture(property, texture);
        material.EnableKeyword("_NORMALMAP");
        if (material.HasProperty("_BumpScale")) material.SetFloat("_BumpScale", .55f);
        if (material.HasProperty("_NormalStrength")) material.SetFloat("_NormalStrength", .55f);
    }
}
