using System;
using System.Collections.Generic;
using UnityEngine;

namespace IncreaseHardMod.Hud;

/// <summary>
/// Procedural art for the player-condition HUD: full-screen edge frames (frost, toxic haze, rope net, sleepy
/// haze) and the card icons. Drawn once on first use at a low resolution (the frames are soft and stretched
/// to the screen), so nothing is shipped as an image file.
/// </summary>
internal static class HudScreenArt
{
    private const int W = 480, H = 270;
    private static readonly Dictionary<PlayerCondition, Texture2D> Frames = new();

    internal static Texture2D Frame(PlayerCondition condition)
    {
        if (Frames.TryGetValue(condition, out var cached) && cached != null) return cached;
        var pixels = new Color32[W * H];
        switch (condition)
        {
            case PlayerCondition.Frozen: Frost(pixels); break;
            case PlayerCondition.Poisoned: Toxic(pixels); break;
            case PlayerCondition.Netted: Net(pixels); break;
            default: Sleepy(pixels); break;
        }
        var texture = new Texture2D(W, H, TextureFormat.RGBA32, false)
        {
            name = "IHM condition frame " + condition, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave,
        };
        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        Frames[condition] = texture;
        return texture;
    }

    internal static Sprite Icon(PlayerCondition condition) => condition switch
    {
        PlayerCondition.Frozen => HudIcons.Silhouette("snowflake", Snowflake),
        PlayerCondition.Poisoned => HudIcons.Silhouette("biohazard", Biohazard),
        PlayerCondition.Netted => HudIcons.Silhouette("net", NetIcon),
        _ => HudIcons.Silhouette("moon", Moon),
    };

    // ------------------------------------------------------------------ frames

    // Distance to the nearest screen edge in screen heights (0 at the edge, 0.5 in the middle vertically).
    private static float Edge(int x, int y) => Math.Min(Math.Min(x, W - 1 - x), Math.Min(y, H - 1 - y)) / (float)H;

    private static float CornerBump(int x, int y, float reach)
    {
        float dx = Math.Min(x, W - 1 - x), dy = Math.Min(y, H - 1 - y);
        return Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy) / (reach * H));
    }

    private static void Frost(Color32[] px)
    {
        var deep = new Color(.55f, .78f, 1f);
        var light = new Color(.92f, .97f, 1f);
        for (int y = 0; y < H; y++)
        for (int x = 0; x < W; x++)
        {
            float n = Fbm(x / 38f, y / 38f, 11);
            float fine = Fbm(x / 5f, y / 5f, 12);
            float frost = Smooth(.17f, 0f, Edge(x, y) - .06f * (n - .5f));
            frost = Math.Max(frost, Mathf.Pow(CornerBump(x, y, .55f), 1.6f) * .95f);
            float a = frost * (.5f + .5f * n);
            if (fine > .7f) a = Math.Min(1f, a * 1.35f); // crystal sparkle
            Put(px, x, y, Color.Lerp(deep, light, n), a * .82f);
        }
        var rng = new System.Random(7);
        for (int i = 0; i < 26; i++) Crack(px, rng, new Color(.85f, .95f, 1f), 12, 6f);
    }

    private static void Toxic(Color32[] px)
    {
        var dark = new Color(.38f, .8f, .05f);
        var bright = new Color(.72f, 1f, .2f);
        for (int y = 0; y < H; y++)
        for (int x = 0; x < W; x++)
        {
            float n = Fbm(x / 34f, y / 34f, 21);
            float haze = Smooth(.2f, 0f, Edge(x, y) - .08f * (n - .5f)) * .62f;
            Put(px, x, y, Color.Lerp(dark, bright, n), haze * (.55f + .45f * n));
        }
        // Slime band and drips along the top edge.
        var rng = new System.Random(3);
        for (int x = 0; x < W; x++)
        {
            int band = 4 + (int)(3f * Mathf.Sin(x * .09f) + 2f * Mathf.Sin(x * .23f + 1f));
            for (int y = 0; y < band; y++) Max(px, x, H - 1 - y, bright, .92f);
        }
        for (int i = 0; i < 11; i++)
        {
            float cx = (float)rng.NextDouble() * W, width = 2.5f + (float)rng.NextDouble() * 3f;
            int length = 18 + rng.Next(60);
            for (int t = 0; t < length; t++)
            {
                float w = width * (1f - t / (float)length * .35f);
                for (int x = (int)(cx - w); x <= (int)(cx + w); x++) Max(px, x, H - 1 - t, bright, .9f);
            }
            Disc(px, cx, H - 1 - length, width * 1.35f, bright, .92f);
        }
        // Hazard stripes down both sides.
        var stripe = new Color(.85f, 1f, .25f);
        for (int y = (int)(H * .2f); y < (int)(H * .8f); y++)
        for (int i = 5; i < 14; i++)
        {
            if (((i + y) / 7) % 2 != 0) continue;
            Max(px, i, y, stripe, .8f);
            Max(px, W - 1 - i, y, stripe, .8f);
        }
    }

    private static void Net(Color32[] px)
    {
        var ropeDark = new Color(.45f, .28f, .14f);
        var ropeLight = new Color(.78f, .55f, .3f);
        var orange = new Color(1f, .5f, .12f);
        const float spacing = 44f, thick = 2.4f;
        float c = Mathf.Cos(35f * Mathf.Deg2Rad), s = Mathf.Sin(35f * Mathf.Deg2Rad);
        for (int y = 0; y < H; y++)
        for (int x = 0; x < W; x++)
        {
            float mask = Smooth(.24f, .03f, Edge(x, y));
            mask = Math.Max(mask, Mathf.Pow(CornerBump(x, y, .6f), 1.2f));
            if (mask <= .02f) continue;
            float u = x * c + y * s, v = x * c - y * s;
            float du = Mathf.Abs(Mathf.Repeat(u, spacing) - spacing * .5f), dv = Mathf.Abs(Mathf.Repeat(v, spacing) - spacing * .5f);
            bool onU = du < thick, onV = dv < thick;
            if (!onU && !onV) continue;
            float twist = .5f + .5f * Mathf.Sin((onU ? v : u) * .9f);
            var rope = Color.Lerp(ropeDark, ropeLight, twist);
            if (onU && onV) rope = ropeDark * .8f; // knot
            Put(px, x, y, rope, mask * .95f);
        }
        // Orange corner brackets.
        const int inset = 9, arm = 36, line = 3;
        foreach (var (cx, cy, sx, sy) in new[] { (inset, inset, 1, 1), (W - 1 - inset, inset, -1, 1), (inset, H - 1 - inset, 1, -1), (W - 1 - inset, H - 1 - inset, -1, -1) })
            for (int t = 0; t < arm; t++)
            for (int k = 0; k < line; k++)
            {
                Max(px, cx + sx * t, cy + sy * k, orange, .9f);
                Max(px, cx + sx * k, cy + sy * t, orange, .9f);
            }
    }

    private static void Sleepy(Color32[] px)
    {
        var deep = new Color(.16f, .09f, .36f);
        var soft = new Color(.45f, .32f, .8f);
        var rng = new System.Random(5);
        for (int y = 0; y < H; y++)
        for (int x = 0; x < W; x++)
        {
            float n = Fbm(x / 46f, y / 46f, 31);
            float haze = Smooth(.31f, 0f, Edge(x, y) - .1f * (n - .5f)) * .78f;
            Put(px, x, y, Color.Lerp(deep, soft, n), haze * (.6f + .4f * n));
        }
        for (int i = 0; i < 40; i++)
        {
            int x = rng.Next(W), y = rng.Next(H);
            if (Edge(x, y) > .16f) continue;
            Disc(px, x, y, 1.2f, new Color(.85f, .8f, 1f), .7f);
        }
    }

    private static void Crack(Color32[] px, System.Random rng, Color color, int steps, float stepLength)
    {
        // Start on a random edge point and wander inward, thinning out.
        int side = rng.Next(4);
        float x = side switch { 0 => 0, 1 => W - 1, _ => (float)rng.NextDouble() * W };
        float y = side switch { 2 => 0, 3 => H - 1, _ => (float)rng.NextDouble() * H };
        float angle = Mathf.Atan2(H * .5f - y, W * .5f - x) + ((float)rng.NextDouble() - .5f) * 1.2f;
        for (int i = 0; i < steps; i++)
        {
            angle += ((float)rng.NextDouble() - .5f) * .9f;
            float nx = x + Mathf.Cos(angle) * stepLength, ny = y + Mathf.Sin(angle) * stepLength;
            float a = .85f * (1f - i / (float)steps);
            Line(px, x, y, nx, ny, color, a);
            if (rng.NextDouble() < .18f)
            {
                float bx = x, by = y, ba = angle + (rng.NextDouble() < .5 ? .8f : -.8f);
                for (int b = 0; b < 4; b++)
                {
                    float ex = bx + Mathf.Cos(ba) * stepLength * .7f, ey = by + Mathf.Sin(ba) * stepLength * .7f;
                    Line(px, bx, by, ex, ey, color, a * .7f);
                    bx = ex; by = ey;
                }
            }
            x = nx; y = ny;
        }
    }

    // ------------------------------------------------------------------ icons (u -1..1, v 0..1 top-down)

    private static bool Snowflake(float u, float v)
    {
        float x = u, y = (v - .5f) * 2f, r = Mathf.Sqrt(x * x + y * y);
        if (r > .95f) return false;
        for (int k = 0; k < 6; k++)
        {
            float a = k * Mathf.PI / 3f;
            var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            if (SegmentDistance(new Vector2(x, y), Vector2.zero, dir * .9f) < .07f) return true;
            foreach (float along in new[] { .45f, .68f })
            {
                var root = dir * along;
                for (int side = -1; side <= 1; side += 2)
                {
                    float b = a + side * .8f;
                    if (SegmentDistance(new Vector2(x, y), root, root + new Vector2(Mathf.Cos(b), Mathf.Sin(b)) * .22f) < .055f) return true;
                }
            }
        }
        return r < .14f;
    }

    // Three crescent lobes (a disc minus an offset hole) around a ring with a hollow centre.
    private static bool Biohazard(float u, float v)
    {
        var p = new Vector2(u, (.5f - v) * 2f);
        float r = p.magnitude;
        if (r < .16f) return false;
        if (r > .3f && r < .4f) return true;
        for (int k = 0; k < 3; k++)
        {
            float a = Mathf.PI / 2f + k * 2f * Mathf.PI / 3f;
            var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            if ((p - dir * .4f).magnitude < .45f && (p - dir * .56f).magnitude > .32f) return true;
        }
        return false;
    }

    private static bool NetIcon(float u, float v)
    {
        float x = u, y = v;
        // A dome of net: inside an arch, a diamond lattice.
        float arch = .12f + .7f * (x * x);
        if (y < arch || y > .88f || Mathf.Abs(x) > .85f) return false;
        if (y > .82f || Mathf.Abs(y - arch) < .05f) return true;
        float a = Mathf.Repeat((x + y) * 4f, 1f), b = Mathf.Repeat((x - y) * 4f, 1f);
        return a < .16f || b < .16f;
    }

    private static bool Moon(float u, float v)
    {
        float x = u, y = (v - .5f) * 2f;
        bool disc = x * x + y * y < .7f * .7f;
        float ox = x - .32f, oy = y + .22f;
        return disc && ox * ox + oy * oy > .6f * .6f;
    }

    private static float SegmentDistance(Vector2 p, Vector2 a, Vector2 b)
    {
        var ab = b - a;
        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(1e-6f, ab.sqrMagnitude));
        return (p - (a + ab * t)).magnitude;
    }

    // ------------------------------------------------------------------ raster helpers

    private static void Put(Color32[] px, int x, int y, Color color, float alpha)
    {
        if (alpha <= .002f) return;
        color.a = Clamp01(alpha);
        px[y * W + x] = color;
    }

    // Keep the stronger of the existing and new pixel (for strokes over the haze).
    private static void Max(Color32[] px, int x, int y, Color color, float alpha)
    {
        if (x < 0 || y < 0 || x >= W || y >= H) return;
        int i = y * W + x;
        if (px[i].a / 255f >= alpha) return;
        color.a = Clamp01(alpha);
        px[i] = color;
    }

    private static void Line(Color32[] px, float x0, float y0, float x1, float y1, Color color, float alpha)
    {
        int steps = Math.Max(1, (int)(Mathf.Max(Mathf.Abs(x1 - x0), Mathf.Abs(y1 - y0)) * 1.5f));
        for (int i = 0; i <= steps; i++)
        {
            float t = i / (float)steps;
            Max(px, (int)Mathf.Lerp(x0, x1, t), (int)Mathf.Lerp(y0, y1, t), color, alpha);
        }
    }

    private static void Disc(Color32[] px, float cx, float cy, float radius, Color color, float alpha)
    {
        for (int y = (int)(cy - radius - 1); y <= (int)(cy + radius + 1); y++)
        for (int x = (int)(cx - radius - 1); x <= (int)(cx + radius + 1); x++)
            if ((x - cx) * (x - cx) + (y - cy) * (y - cy) <= radius * radius) Max(px, x, y, color, alpha);
    }

    private static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;

    private static float Smooth(float edge0, float edge1, float x)
    {
        float t = Clamp01((x - edge0) / (edge1 - edge0));
        return t * t * (3f - 2f * t);
    }

    private static float Hash(int x, int y, int seed)
    {
        unchecked
        {
            uint h = (uint)(x * 374761393 + y * 668265263 + seed * 1442695041);
            h = (h ^ (h >> 13)) * 1274126177u;
            h ^= h >> 16;
            return (h & 0xffffff) / 16777216f;
        }
    }

    private static float ValueNoise(float x, float y, int seed)
    {
        int xi = Mathf.FloorToInt(x), yi = Mathf.FloorToInt(y);
        float fx = x - xi, fy = y - yi;
        fx = fx * fx * (3f - 2f * fx);
        fy = fy * fy * (3f - 2f * fy);
        float a = Hash(xi, yi, seed), b = Hash(xi + 1, yi, seed), c = Hash(xi, yi + 1, seed), d = Hash(xi + 1, yi + 1, seed);
        return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
    }

    private static float Fbm(float x, float y, int seed) =>
        .55f * ValueNoise(x, y, seed) + .3f * ValueNoise(x * 2.1f, y * 2.1f, seed + 1) + .15f * ValueNoise(x * 4.3f, y * 4.3f, seed + 2);
}
