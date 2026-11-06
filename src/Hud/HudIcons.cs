using System;
using System.Collections.Generic;
using UnityEngine;

namespace IncreaseHardMod.Hud;

/// <summary>
/// Small white silhouette icons drawn in code (tinted where they are used), so the HUD ships no image files.
/// Add a new icon with <see cref="Silhouette"/>: give it a name and an inside test over u -1..1 (left to right)
/// and v 0..1 (top to bottom); it is anti-aliased and cached.
/// </summary>
internal static class HudIcons
{
    private static readonly Dictionary<string, Sprite> Cache = new(StringComparer.Ordinal);

    /// <summary>Heater shield (Armor row).</summary>
    internal static Sprite Shield => Silhouette("shield", ShieldInside);

    internal static Sprite Silhouette(string name, Func<float, float, bool> inside, int size = 128)
    {
        if (Cache.TryGetValue(name, out var cached) && cached != null) return cached;
        var pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            int hits = 0;
            for (int sy = 0; sy < 3; sy++)
            for (int sx = 0; sx < 3; sx++)
                if (inside((x + (sx + .5f) / 3f) / size * 2f - 1f, 1f - (y + (sy + .5f) / 3f) / size)) hits++;
            pixels[y * size + x] = new Color32(255, 255, 255, (byte)(hits * 255 / 9));
        }
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "IHM HUD icon " + name, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave,
        };
        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        var sprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(.5f, .5f), 100f);
        sprite.hideFlags = HideFlags.HideAndDontSave;
        Cache[name] = sprite;
        return sprite;
    }

    private static bool ShieldInside(float u, float v)
    {
        const float top = .08f, bottom = .94f, shoulder = .5f, halfWidth = .78f;
        if (v < top || v > bottom) return false;
        float t = (v - top) / (bottom - top);
        float half = t < shoulder ? halfWidth : halfWidth * Mathf.Sqrt(Mathf.Max(0f, 1f - Mathf.Pow((t - shoulder) / (1f - shoulder), 2f)));
        float dip = .04f * (1f - u * u); // a slightly curved top edge
        return Mathf.Abs(u) <= half && v >= top + dip;
    }
}
