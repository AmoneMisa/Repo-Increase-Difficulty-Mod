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
            case PlayerCondition.Shocked: Electric(pixels); break;
            case PlayerCondition.Electrified: Electric(pixels); break;
            case PlayerCondition.Venom: Toxic(pixels); break;
            case PlayerCondition.Burning: Band(pixels, new Color(.9f, .25f, .02f), new Color(1f, .75f, .2f), 51, .2f, .7f); break;
            case PlayerCondition.Bleeding: Band(pixels, new Color(.45f, 0f, 0f), new Color(.85f, .05f, .05f), 61, .16f, .65f); break;
            case PlayerCondition.Constricted: Band(pixels, new Color(.15f, .35f, .1f), new Color(.5f, .8f, .3f), 71, .18f, .7f); break;
            case PlayerCondition.Petrified: Band(pixels, new Color(.35f, .34f, .32f), new Color(.75f, .73f, .68f), 81, .24f, .85f); break;
            case PlayerCondition.Slowed: Band(pixels, new Color(.5f, .35f, .05f), new Color(1f, .8f, .35f), 91, .15f, .55f); break;
            case PlayerCondition.Dragged: Band(pixels, new Color(.5f, .12f, .05f), new Color(1f, .45f, .3f), 101, .15f, .6f); break;
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
        PlayerCondition.Shocked or PlayerCondition.Electrified => HudIcons.Silhouette("bolt", BoltIcon),
        PlayerCondition.Venom => HudIcons.Silhouette("biohazard", Biohazard),
        PlayerCondition.Burning => HudIcons.Silhouette("flame", Flame),
        PlayerCondition.Bleeding => HudIcons.Silhouette("drop", Drop),
        PlayerCondition.Constricted => HudIcons.Silhouette("coil", Coil),
        PlayerCondition.Petrified => HudIcons.Silhouette("stone", Stone),
        PlayerCondition.Slowed => HudIcons.Silhouette("eye", EyeIcon),
        PlayerCondition.Dragged => HudIcons.Silhouette("horns", Horns),
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

    // A soft noisy band along every edge, darker outside, for the monster afflictions.
    private static void Band(Color32[] px, Color dark, Color bright, int seed, float reach, float strength)
    {
        for (int y = 0; y < H; y++)
        for (int x = 0; x < W; x++)
        {
            float n = Fbm(x / 30f, y / 30f, seed);
            float a = Smooth(reach, 0f, Edge(x, y) - .06f * (n - .5f));
            a = Math.Max(a, Mathf.Pow(CornerBump(x, y, reach * 2.6f), 1.8f) * .9f);
            Put(px, x, y, Color.Lerp(dark, bright, n), a * strength * (.55f + .45f * n));
        }
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

    // Shocked: a faint blue haze at the edges with jagged lightning arcs crawling in from every side.
    private static void Electric(Color32[] px)
    {
        var haze = new Color(.25f, .45f, 1f);
        var arc = new Color(.75f, .9f, 1f);
        for (int y = 0; y < H; y++)
        for (int x = 0; x < W; x++)
        {
            float n = Fbm(x / 26f, y / 26f, 41);
            Put(px, x, y, haze, Smooth(.17f, 0f, Edge(x, y) - .05f * (n - .5f)) * .5f);
        }
        var rng = new System.Random(17);
        for (int bolt = 0; bolt < 14; bolt++)
        {
            // Start on an edge, walk inward in jagged steps, sometimes forking.
            int side = bolt % 4;
            float x = side == 0 ? 0 : side == 1 ? W - 1 : (float)rng.NextDouble() * W;
            float y = side == 2 ? 0 : side == 3 ? H - 1 : (float)rng.NextDouble() * H;
            float dx = side == 0 ? 1 : side == 1 ? -1 : 0, dy = side == 2 ? 1 : side == 3 ? -1 : 0;
            int steps = 10 + rng.Next(12);
            for (int s = 0; s < steps; s++)
            {
                float nx = x + dx * (5f + (float)rng.NextDouble() * 6f) + ((float)rng.NextDouble() - .5f) * 10f;
                float ny = y + dy * (5f + (float)rng.NextDouble() * 6f) + ((float)rng.NextDouble() - .5f) * 10f;
                float fade = 1f - s / (float)steps;
                Segment(px, x, y, nx, ny, arc, .95f * fade, 1.4f);
                if (rng.NextDouble() < .18)
                    Segment(px, nx, ny, nx + ((float)rng.NextDouble() - .5f) * 22f, ny + ((float)rng.NextDouble() - .5f) * 22f, arc, .7f * fade, 1f);
                x = nx; y = ny;
            }
        }
    }

    // A soft-edged line segment (lightning arcs).
    private static void Segment(Color32[] px, float x0, float y0, float x1, float y1, Color color, float alpha, float width)
    {
        int steps = (int)Math.Max(1f, Mathf.Max(Mathf.Abs(x1 - x0), Mathf.Abs(y1 - y0)));
        for (int i = 0; i <= steps; i++)
        {
            float t = i / (float)steps, cx = x0 + (x1 - x0) * t, cy = y0 + (y1 - y0) * t;
            for (int oy = -2; oy <= 2; oy++)
            for (int ox = -2; ox <= 2; ox++)
            {
                float d = Mathf.Sqrt(ox * ox + oy * oy);
                if (d > width + 1f) continue;
                Max(px, (int)cx + ox, (int)cy + oy, color, alpha * Clamp01(width + 1f - d));
            }
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

    // A zig-zag lightning bolt (u -1..1 across, v 0..1 top-down).
    private static bool BoltIcon(float u, float v)
    {
        if (v < .04f || v > .96f) return false;
        float centre = v < .5f ? .25f - v * .9f : -.2f + (v - .5f) * .9f; // upper stroke leans left, lower one right
        float half = .2f + (v > .44f && v < .56f ? .25f : 0f);           // the wide middle step
        return Mathf.Abs(u - centre) < half * (1f - Mathf.Abs(v - .5f) * .9f);
    }

    private static bool Flame(float u, float v)
    {
        // A teardrop with a flickering tongue: wide at the bottom, pointed at the top.
        float y = 1f - v;
        float width = y < .35f ? Mathf.Sqrt(Mathf.Max(0f, .35f * .35f - (y - .35f) * (y - .35f))) * 1.9f : .66f * (1f - (y - .35f) / .65f);
        float x = u + .12f * Mathf.Sin(y * 7f) * y;
        return v > .04f && Mathf.Abs(x) < width && !(Mathf.Abs(u + .05f) < width * .35f && y < .3f && y > .08f);
    }

    private static bool Drop(float u, float v)
    {
        float y = 1f - v;
        float width = y < .4f ? Mathf.Sqrt(Mathf.Max(0f, .4f * .4f - (y - .4f) * (y - .4f))) * 1.6f : .64f * (1f - (y - .4f) / .6f);
        return v > .04f && Mathf.Abs(u) < width;
    }

    private static bool Coil(float u, float v)
    {
        float y = (v - .5f) * 2f;
        for (int k = 0; k < 3; k++)
        {
            float cy = -.55f + k * .55f;
            float d = (u * u) / (.78f * .78f) + ((y - cy) * (y - cy)) / (.2f * .2f);
            if (d < 1f && d > .45f) return true;
        }
        return Mathf.Abs(u - .55f) < .12f && y < -.55f && y > -.95f;
    }

    private static bool Stone(float u, float v)
    {
        float y = (v - .5f) * 2f;
        bool inside = Mathf.Abs(u) + Mathf.Abs(y) * .8f < .85f && y > -.75f && y < .7f;
        bool crack = Mathf.Abs(u - (.2f - y * .3f)) < .05f && y > -.3f && y < .5f;
        return inside && !crack;
    }

    private static bool EyeIcon(float u, float v)
    {
        float y = (v - .5f) * 2f;
        bool almond = Mathf.Abs(y) < .55f * (1f - u * u);
        float r = Mathf.Sqrt(u * u + y * y);
        return almond && !(r > .22f && r < .32f);
    }

    private static bool Horns(float u, float v)
    {
        float y = 1f - v;
        float x = Mathf.Abs(u);
        bool head = x < .3f && y < .5f && y > .05f;
        float hornY = .45f + (x - .3f) * .8f;
        bool horn = x >= .25f && x < .95f && Mathf.Abs(y - hornY) < .12f * (1f - (x - .25f) / .75f) + .02f;
        return head || horn;
    }

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
