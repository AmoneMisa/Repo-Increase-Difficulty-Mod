using System.Collections.Generic;
using UnityEngine;

namespace IncreaseHardMod.Patches.Effects;

/// <summary>
/// White weapon silhouettes for the weapon-ping callout (pistol, blade, grenade), drawn procedurally with
/// 3x3 supersampled edges so they stay crisp at any HUD scale. Tinted at draw time; nothing is shipped.
/// </summary>
internal static class PingWeaponIcons
{
    private const int W = 192, H = 96;
    private static readonly Dictionary<WeaponKind, Texture2D> Cache = new();

    internal static Texture2D Get(WeaponKind kind)
    {
        if (kind == WeaponKind.None) kind = WeaponKind.Gun;
        if (Cache.TryGetValue(kind, out var cached) && cached != null) return cached;
        System.Func<float, float, bool> shape = kind switch
        {
            WeaponKind.Melee => Blade,
            WeaponKind.Grenade => Grenade,
            _ => Pistol,
        };
        var pixels = new Color32[W * H];
        for (int y = 0; y < H; y++)
        for (int x = 0; x < W; x++)
        {
            int hits = 0;
            for (int sy = 0; sy < 3; sy++)
            for (int sx = 0; sx < 3; sx++)
                if (shape(x + (sx + .5f) / 3f, y + (sy + .5f) / 3f)) hits++;
            pixels[y * W + x] = new Color32(255, 255, 255, (byte)(hits * 255 / 9));
        }
        var texture = new Texture2D(W, H, TextureFormat.RGBA32, false)
        {
            name = "IHM Ping Icon " + kind, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave,
        };
        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        return Cache[kind] = texture;
    }

    // ---- shape helpers (pixel space, y up) ----
    private static bool Box(float x, float y, float x0, float y0, float x1, float y1, float r = 0f)
    {
        float cx = Mathf.Clamp(x, x0 + r, x1 - r), cy = Mathf.Clamp(y, y0 + r, y1 - r);
        return x >= x0 && x <= x1 && y >= y0 && y <= y1 && (r <= 0f || (x - cx) * (x - cx) + (y - cy) * (y - cy) <= r * r);
    }

    private static bool Disc(float x, float y, float cx, float cy, float r) => (x - cx) * (x - cx) + (y - cy) * (y - cy) <= r * r;

    private static bool Ring(float x, float y, float cx, float cy, float r, float t)
    {
        float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
        return d <= r && d >= r - t;
    }

    private static bool Capsule(float x, float y, Vector2 a, Vector2 b, float r)
    {
        var p = new Vector2(x, y); var ab = b - a;
        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
        return (p - (a + ab * t)).sqrMagnitude <= r * r;
    }

    private static bool Poly(float x, float y, params Vector2[] v)
    {
        bool inside = false;
        for (int i = 0, j = v.Length - 1; i < v.Length; j = i++)
            if ((v[i].y > y) != (v[j].y > y) && x < (v[j].x - v[i].x) * (y - v[i].y) / (v[j].y - v[i].y) + v[i].x)
                inside = !inside;
        return inside;
    }

    // Pistol facing right: slide and barrel, angled grip at the back, trigger guard.
    private static bool Pistol(float x, float y) =>
        Box(x, y, 28, 54, 168, 76, 4) ||                          // slide
        Box(x, y, 160, 58, 174, 70, 2) ||                         // muzzle
        Box(x, y, 40, 76, 60, 80) ||                              // rear sight
        Box(x, y, 150, 76, 158, 80) ||                            // front sight
        Poly(x, y, new Vector2(34, 56), new Vector2(76, 56), new Vector2(64, 8), new Vector2(26, 12)) || // grip
        (Ring(x, y, 92, 50, 18, 6) && y < 54 && x > 76) ||       // trigger guard
        Capsule(x, y, new Vector2(90, 54), new Vector2(86, 40), 3f); // trigger

    // Sword, bottom-left to top-right: blade with a point, crossguard, grip, pommel.
    private static bool Blade(float x, float y) =>
        Poly(x, y, new Vector2(70, 30), new Vector2(160, 76), new Vector2(182, 88), new Vector2(166, 66), new Vector2(78, 20)) ||
        Capsule(x, y, new Vector2(56, 42), new Vector2(86, 10), 5.5f) ||   // crossguard
        Capsule(x, y, new Vector2(70, 24), new Vector2(40, 10), 6f) ||     // grip
        Disc(x, y, 34, 8, 8);                                              // pommel

    // Grenade: ribbed oval body, cap, spoon lever, pull ring.
    private static bool Grenade(float x, float y)
    {
        float ex = (x - 92) / 30f, ey = (y - 38) / 34f;
        bool body = ex * ex + ey * ey <= 1f && !(Mathf.Abs(y - 38) < 2f && Mathf.Abs(x - 92) < 24f) &&
                    !(Mathf.Abs(x - 92) < 2f && Mathf.Abs(y - 38) < 28f);
        return body ||
               Box(x, y, 80, 68, 104, 80, 3) ||                                    // cap
               Capsule(x, y, new Vector2(104, 78), new Vector2(126, 46), 4.5f) ||  // spoon
               Ring(x, y, 66, 82, 11, 4);                                          // pin ring
    }
}
