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
            case PlayerCondition.Electrified: Levitation(pixels); break;
            case PlayerCondition.Venom: Venom(pixels); break;
            case PlayerCondition.Burning: Fire(pixels); break;
            case PlayerCondition.Bleeding: Blood(pixels); break;
            case PlayerCondition.Constricted: Coils(pixels); break;
            case PlayerCondition.Petrified: Stone(pixels); break;
            case PlayerCondition.Slowed: Stare(pixels); break;
            case PlayerCondition.Dragged: Drag(pixels); break;
            case PlayerCondition.Seized: Hands(pixels); break;
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
        PlayerCondition.Shocked => HudIcons.Silhouette("bolt", BoltIcon),
        PlayerCondition.Electrified => HudIcons.Silhouette("charged orb", ChargedOrb),
        PlayerCondition.Venom => HudIcons.Silhouette("fangs", Fangs),
        PlayerCondition.Burning => HudIcons.Silhouette("flame", Flame),
        PlayerCondition.Bleeding => HudIcons.Silhouette("blood drops", Drop),
        PlayerCondition.Constricted => HudIcons.Silhouette("wrapped", Coil),
        PlayerCondition.Petrified => HudIcons.Silhouette("statue", StoneIcon),
        PlayerCondition.Slowed => HudIcons.Silhouette("slit eye", EyeIcon),
        PlayerCondition.Dragged => HudIcons.Silhouette("bull head", Horns),
        PlayerCondition.Seized => HudIcons.Silhouette("twins", TwinsIcon),
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

    // A soft noisy haze along every edge: the base layer under each monster affliction's own motif.
    private static void Haze(Color32[] px, Color dark, Color bright, int seed, float reach, float strength, float corner = 2.6f)
    {
        for (int y = 0; y < H; y++)
        for (int x = 0; x < W; x++)
        {
            float n = Fbm(x / 30f, y / 30f, seed);
            float a = Smooth(reach, 0f, Edge(x, y) - .06f * (n - .5f));
            a = Math.Max(a, Mathf.Pow(CornerBump(x, y, reach * corner), 1.8f) * .9f);
            Put(px, x, y, Color.Lerp(dark, bright, n), a * strength * (.55f + .45f * n));
        }
    }

    // Burning: tongues of flame climbing from the bottom edge, heat haze up the sides, drifting sparks.
    private static void Fire(Color32[] px)
    {
        var deep = new Color(.75f, .12f, .02f); var mid = new Color(1f, .45f, .05f); var hot = new Color(1f, .86f, .38f);
        Haze(px, deep, mid, 51, .17f, .6f);
        for (int x = 0; x < W; x++)
        {
            float tongue = .09f + .2f * Fbm(x / 13f, 0f, 52) + .05f * Mathf.Sin(x * .21f); // flame height, screen heights
            for (int y = 0; y < H * .45f; y++)
            {
                float f = 1f - (y / (float)H + .035f * (Fbm(x / 9f, y / 12f, 53) - .5f)) / tongue; // 1 at the root, 0 at the tip
                if (f <= 0f) continue;
                var c = f < .5f ? Color.Lerp(deep, mid, f * 2f) : Color.Lerp(mid, hot, (f - .5f) * 2f);
                Max(px, x, y, c, .35f + .6f * Clamp01(f));
            }
        }
        var rng = new System.Random(54);
        for (int i = 0; i < 46; i++)
        {
            int x = rng.Next(W), y = rng.Next((int)(H * .75f));
            if (Edge(x, y) > .2f) continue;
            Disc(px, x, y, .9f + (float)rng.NextDouble(), hot, .85f);
        }
    }

    // Electric hold: violet arcs from every side, and thin streaks rising from the floor - you are being lifted.
    private static void Levitation(Color32[] px)
    {
        Electric(px, new Color(.42f, .25f, 1f), new Color(.86f, .78f, 1f), 43);
        var rng = new System.Random(44);
        var streak = new Color(.8f, .7f, 1f);
        for (int i = 0; i < 34; i++)
        {
            float x = (float)rng.NextDouble() * W, length = 20f + (float)rng.NextDouble() * 55f;
            Streak(px, x, 0f, x, length, streak, .8f, 1f);
        }
    }

    // Constricted: the snake's scaled body lying along every edge, thicker where it bulges.
    private static void Coils(Color32[] px)
    {
        var dark = new Color(.07f, .2f, .05f); var mid = new Color(.2f, .46f, .14f); var rim = new Color(.6f, .86f, .36f);
        const float cell = 9f, rowHeight = cell * .8f;
        for (int y = 0; y < H; y++)
        for (int x = 0; x < W; x++)
        {
            float e = Edge(x, y), body = .12f + .035f * Mathf.Sin((x + y) * .045f) + .02f * Mathf.Sin((x - y) * .11f);
            if (e > body + .06f) continue;
            if (e >= body) { Put(px, x, y, dark, .75f * (1f - (e - body) / .06f)); continue; } // its shadow on the inside
            float row = Mathf.Floor(y / rowHeight), shift = ((int)row & 1) == 0 ? 0f : cell * .5f;
            float cx = (Mathf.Floor((x + shift) / cell) + .5f) * cell - shift, cy = (row + .5f) * rowHeight;
            float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)) / (cell * .62f);
            float round = 1f - e / body; // darker towards the screen edge: a round body
            var c = d > .8f ? rim : Color.Lerp(mid, dark, d);
            Put(px, x, y, Color.Lerp(c, dark, round * .55f), .93f);
        }
    }

    // Petrified: rough grey stone closing in from the corners, split by dark cracks.
    private static void Stone(Color32[] px)
    {
        var dark = new Color(.2f, .19f, .18f); var light = new Color(.72f, .7f, .65f);
        for (int y = 0; y < H; y++)
        for (int x = 0; x < W; x++)
        {
            float n = Fbm(x / 34f, y / 34f, 81);
            float a = Smooth(.25f, 0f, Edge(x, y) - .1f * (n - .5f));
            a = Math.Max(a, Mathf.Pow(CornerBump(x, y, .72f), 1.5f));
            Put(px, x, y, Color.Lerp(dark, light, n * .75f + Hash(x, y, 82) * .25f), a * .9f);
        }
        var rng = new System.Random(83);
        for (int i = 0; i < 24; i++) Crack(px, rng, new Color(.05f, .05f, .05f), 14, 6f, over: true);
    }

    // Bleeding: a dark red vignette, blood running down from the top edge, spatter in the corners.
    private static void Blood(Color32[] px)
    {
        var dark = new Color(.3f, 0f, 0f); var bright = new Color(.78f, .03f, .03f);
        Haze(px, dark, bright, 61, .18f, .7f);
        var rng = new System.Random(62);
        for (int x = 0; x < W; x++)
        {
            int band = 3 + (int)(2f * Mathf.Sin(x * .07f) + 1.5f * Mathf.Sin(x * .19f + 2f));
            for (int y = 0; y < band; y++) Max(px, x, H - 1 - y, bright, .95f);
        }
        for (int i = 0; i < 15; i++)
        {
            float cx = (float)rng.NextDouble() * W, width = 1.6f + (float)rng.NextDouble() * 2.4f;
            int length = 14 + rng.Next(74);
            for (int t = 0; t < length; t++)
            {
                float w = width * (1f - t / (float)length * .5f);
                for (int x = (int)(cx - w); x <= (int)(cx + w); x++) Max(px, x, H - 1 - t, bright, .93f);
            }
            Disc(px, cx, H - 1 - length, width * 1.4f, bright, .95f);
        }
        for (int i = 0; i < 16; i++)
        {
            int x = rng.Next(W), y = rng.Next(H);
            if (Edge(x, y) > .16f) continue;
            float r = 2f + (float)rng.NextDouble() * 4f;
            Disc(px, x, y, r, bright, .9f);
            for (int k = 0; k < 4; k++) Disc(px, x + rng.Next(-14, 15), y + rng.Next(-14, 15), .8f + (float)rng.NextDouble() * 1.2f, bright, .85f);
        }
    }

    // Venom: a sickly green haze with poisoned veins creeping in from the edges, and two fang marks dripping.
    private static void Venom(Color32[] px)
    {
        var dark = new Color(.1f, .32f, .04f); var bright = new Color(.62f, 1f, .16f);
        Haze(px, dark, bright, 111, .19f, .6f);
        var rng = new System.Random(112);
        for (int i = 0; i < 34; i++) Crack(px, rng, new Color(.78f, 1f, .3f), 17, 7f);
        foreach (float cx in new[] { W * .455f, W * .545f })
        {
            for (int t = 0; t < 26; t++)
            {
                float w = 4.5f * (1f - t / 26f);
                for (int x = (int)(cx - w); x <= (int)(cx + w); x++) Max(px, x, H - 1 - t, bright, .92f);
            }
            Disc(px, cx, H - 36f, 2.4f, bright, .92f);
        }
    }

    // Death Stare: a heavy amber vignette with streaks converging on the centre - you cannot look away.
    private static void Stare(Color32[] px)
    {
        var dark = new Color(.22f, .1f, 0f); var amber = new Color(1f, .76f, .3f);
        Haze(px, dark, new Color(.7f, .42f, .08f), 91, .27f, .78f, 2f);
        var rng = new System.Random(92);
        for (int i = 0; i < 90; i++)
        {
            int side = i % 4;
            float x = side == 0 ? 0 : side == 1 ? W - 1 : (float)rng.NextDouble() * W;
            float y = side == 2 ? 0 : side == 3 ? H - 1 : (float)rng.NextDouble() * H;
            float dx = W * .5f - x, dy = H * .5f - y, length = Mathf.Sqrt(dx * dx + dy * dy);
            float reach = 22f + (float)rng.NextDouble() * 48f;
            Streak(px, x, y, x + dx / length * reach, y + dy / length * reach, amber, .85f, 1.1f);
        }
    }

    // On the horns: speed lines tearing across the screen from both sides.
    private static void Drag(Color32[] px)
    {
        var dark = new Color(.4f, .07f, .03f); var bright = new Color(1f, .52f, .32f);
        Haze(px, dark, bright, 101, .14f, .55f);
        var rng = new System.Random(102);
        for (int i = 0; i < 70; i++)
        {
            float y = (float)rng.NextDouble() * H, length = 30f + (float)rng.NextDouble() * 120f;
            bool left = (i & 1) == 0;
            Streak(px, left ? 0f : W - 1f, y, left ? length : W - 1f - length, y, bright, .9f, 1f + (float)rng.NextDouble() * .6f);
        }
        for (int i = 0; i < 46; i++)
        {
            float x = (float)rng.NextDouble() * W, y = (float)rng.NextDouble() * H * .1f, length = 18f + (float)rng.NextDouble() * 60f;
            if ((i & 1) == 0) y = H - 1 - y;
            Streak(px, x, y, x + length, y, bright, .7f, 1f);
        }
    }

    // Seized: small pale handprints pressing in along every edge.
    private static void Hands(Color32[] px)
    {
        var dark = new Color(.35f, .1f, .16f); var pale = new Color(1f, .78f, .82f);
        Haze(px, dark, new Color(.8f, .38f, .48f), 121, .16f, .6f);
        var rng = new System.Random(122);
        for (int i = 0; i < 20; i++)
        {
            int side = i % 4;
            float along = (float)rng.NextDouble(), depth = 14f + (float)rng.NextDouble() * 16f;
            float x = side == 0 ? depth : side == 1 ? W - 1 - depth : along * W, y = side == 2 ? depth : side == 3 ? H - 1 - depth : along * H;
            // Fingers point at the middle of the screen.
            float angle = Mathf.Atan2(H * .5f - y, W * .5f - x) + ((float)rng.NextDouble() - .5f) * .7f, size = 5.5f + (float)rng.NextDouble() * 2.5f;
            float alpha = .7f + (float)rng.NextDouble() * .25f;
            Disc(px, x, y, size, pale, alpha);
            for (int f = -2; f <= 2; f++)
            {
                float a = angle + f * .34f, reach = size * (f == 0 ? 2.5f : Mathf.Abs(f) == 1 ? 2.3f : 1.8f);
                if (f == -2) { a = angle - 1.1f; reach = size * 1.5f; } // the thumb sticks out
                for (float d = size * .9f; d <= reach; d += .7f)
                    Disc(px, x + Mathf.Cos(a) * d, y + Mathf.Sin(a) * d, size * .24f, pale, alpha);
            }
        }
    }

    // A line that fades out towards its far end.
    private static void Streak(Color32[] px, float x0, float y0, float x1, float y1, Color color, float alpha, float width)
    {
        const int pieces = 6;
        for (int i = 0; i < pieces; i++)
        {
            float a = i / (float)pieces, b = (i + 1) / (float)pieces;
            Segment(px, Mathf.Lerp(x0, x1, a), Mathf.Lerp(y0, y1, a), Mathf.Lerp(x0, x1, b), Mathf.Lerp(y0, y1, b), color, alpha * (1f - a), width);
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
    private static void Electric(Color32[] px) => Electric(px, new Color(.25f, .45f, 1f), new Color(.75f, .9f, 1f), 17);

    private static void Electric(Color32[] px, Color haze, Color arc, int seed)
    {
        for (int y = 0; y < H; y++)
        for (int x = 0; x < W; x++)
        {
            float n = Fbm(x / 26f, y / 26f, 41);
            Put(px, x, y, haze, Smooth(.17f, 0f, Edge(x, y) - .05f * (n - .5f)) * .5f);
        }
        var rng = new System.Random(seed);
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

    private static void Crack(Color32[] px, System.Random rng, Color color, int steps, float stepLength, bool over = false)
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
            Line(px, x, y, nx, ny, color, a, over);
            if (rng.NextDouble() < .18f)
            {
                float bx = x, by = y, ba = angle + (rng.NextDouble() < .5 ? .8f : -.8f);
                for (int b = 0; b < 4; b++)
                {
                    float ex = bx + Mathf.Cos(ba) * stepLength * .7f, ey = by + Mathf.Sin(ba) * stepLength * .7f;
                    Line(px, bx, by, ex, ey, color, a * .7f, over);
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

    // Icons below use x = u / 2 (so a unit is the same on both axes) and y up from the bottom edge.

    // A teardrop: round below the centre, pointed above it.
    private static bool Teardrop(float x, float y, float r, float tall = 2.2f) =>
        y < 0f ? x * x + y * y < r * r : y < r * tall && Mathf.Abs(x) < r * Mathf.Pow(1f - y / (r * tall), .85f);

    // A leaning flame with a hollow heart.
    private static bool Flame(float u, float v)
    {
        float x = u * .5f, y = 1f - v;
        float lean = y > .36f ? .07f * Mathf.Sin((y - .36f) * 5.5f) : 0f;
        bool outer = Teardrop(x - lean, y - .36f, .32f, 1.9f);
        bool heart = Teardrop(x - lean * .4f + .01f, y - .27f, .14f, 2.3f);
        return y > .03f && outer && !heart;
    }

    // A lightning bolt held inside a broken ring of charge.
    private static bool ChargedOrb(float u, float v)
    {
        float y = (.5f - v) * 2f, r = Mathf.Sqrt(u * u + y * y);
        bool ring = r > .82f && r < .97f && Mathf.Abs(Mathf.Sin(Mathf.Atan2(y, u) * 2f + .5f)) > .3f;
        if (ring) return true;
        // The bolt: two slanted strokes joined by a step.
        float bx = u / .62f, by = y / .62f;
        bool upper = by >= 0f && by < .95f && Mathf.Abs(bx - (-.14f + by * .5f)) < .2f;
        bool lower = by > -.95f && by <= .08f && Mathf.Abs(bx - (.14f + by * .5f)) < .2f * (by + .95f) / 1.03f + .015f;
        return upper || lower;
    }

    // Two drops of blood, the big one with a highlight.
    private static bool Drop(float u, float v)
    {
        float x = u * .5f, y = 1f - v;
        bool big = Teardrop(x + .06f, y - .36f, .27f) && (x + .17f) * (x + .17f) + (y - .3f) * (y - .3f) > .055f * .055f;
        return y > .03f && (big || Teardrop(x - .32f, y - .2f, .105f));
    }

    // A figure wrapped in three turns of the snake, its head at the top.
    private static bool Coil(float u, float v)
    {
        float y = (.5f - v) * 2f;
        bool near = false, band = false;
        for (int k = -1; k <= 1; k++)
        {
            float d = Mathf.Abs(y - k * .55f - u * .4f);
            band |= d < .12f && Mathf.Abs(u) < .66f;
            near |= d < .2f;
        }
        float cap = Mathf.Abs(y) > .68f ? Mathf.Abs(y) - .68f : 0f;
        bool body = Mathf.Abs(u) < Mathf.Sqrt(Mathf.Max(0f, .28f * .28f - cap * cap)) && !near;
        bool head = (u - .7f) * (u - .7f) + (y - .84f) * (y - .84f) < .15f * .15f;
        return band || body || head;
    }

    // A stone bust, cracked across.
    private static bool StoneIcon(float u, float v)
    {
        float y = (.5f - v) * 2f;
        bool head = u * u + (y - .48f) * (y - .48f) < .34f * .34f;
        bool shoulders = y < .12f && y > -.9f && u * u / (.84f * .84f) + (y + .36f) * (y + .36f) / (.5f * .5f) < 1f || (y <= -.36f && y > -.9f && Mathf.Abs(u) < .84f);
        float zig = .04f + .14f * Mathf.Abs(Mathf.Repeat(y * 1.7f, 1f) - .5f) * 2f - .12f;
        bool crack = Mathf.Abs(u - zig - y * .22f) < .045f && y > -.62f;
        bool chip = u > .28f && y > .2f && y < .12f + (u - .28f) * 1.4f;
        return (head || shoulders) && !crack && !chip;
    }

    // An eye with a slit pupil under a row of glaring rays.
    private static bool EyeIcon(float u, float v)
    {
        float y = (.5f - v) * 2f + .2f;
        float lid = .5f * (1f - u * u);
        bool almond = Mathf.Abs(y) < lid, inner = Mathf.Abs(y) < lid - .1f && Mathf.Abs(u) < .86f;
        float r = Mathf.Sqrt(u * u + y * y);
        bool iris = r < .33f && !(Mathf.Abs(u) < .07f && Mathf.Abs(y) < .25f);
        if ((almond && !inner) || (almond && iris)) return true;
        var p = new Vector2(u, y);
        for (int k = -2; k <= 2; k++)
        {
            float a = Mathf.PI / 2f + k * .4f;
            var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            if (SegmentDistance(p, dir * .68f, dir * (1.02f - Mathf.Abs(k) * .06f)) < .045f) return true;
        }
        return false;
    }

    // A bull's head from the front: horns sweeping out and up, eyes and nostrils cut out.
    private static bool Horns(float u, float v)
    {
        float y = (.5f - v) * 2f, x = Mathf.Abs(u);
        bool head = y < .34f && y > -.9f && x < (y > -.1f ? .36f : .36f - (-.1f - y) * .22f);
        bool holes = (x - .18f) * (x - .18f) + (y - .02f) * (y - .02f) < .06f * .06f || (x - .09f) * (x - .09f) + (y + .7f) * (y + .7f) < .045f * .045f;
        float along = (x - .3f) / .66f;
        bool horn = along >= 0f && along <= 1f && Mathf.Abs(y - (.12f + .82f * Mathf.Pow(along, 2.1f))) < .17f * (1f - along) + .025f;
        return (head && !holes) || horn;
    }

    // Two children hand in hand: the boy on the left, the girl in a dress on the right.
    private static bool TwinsIcon(float u, float v)
    {
        float y = (.5f - v) * 2f;
        foreach (float side in new[] { -1f, 1f })
        {
            float x = u - side * .46f, ax = Mathf.Abs(x);
            if (x * x + (y - .52f) * (y - .52f) < .25f * .25f) return true;                       // head
            if (side < 0f)
            {
                if (y < .22f && y > -.3f && ax < .2f) return true;                                 // shirt
                if (y <= -.3f && y > -.9f && ax < .2f && ax > .045f) return true;                  // legs
            }
            else
            {
                if (y < .22f && y > -.55f && ax < .14f + (.22f - y) * .3f) return true;            // dress
                if (y <= -.55f && y > -.9f && ax < .16f && ax > .045f) return true;                // legs
                if ((ax - .3f) * (ax - .3f) + (y - .4f) * (y - .4f) < .075f * .075f) return true;  // braids
            }
            if (SegmentDistance(new Vector2(x, y), new Vector2(side * .2f, .12f), new Vector2(side * .42f, -.25f)) < .06f) return true; // outer arm
        }
        // Joined hands.
        return SegmentDistance(new Vector2(u, y), new Vector2(-.28f, .1f), new Vector2(0f, -.12f)) < .06f ||
               SegmentDistance(new Vector2(u, y), new Vector2(.3f, .1f), new Vector2(0f, -.12f)) < .06f;
    }

    // Two fangs under the gum line and a drop of venom.
    private static bool Fangs(float u, float v)
    {
        float x = u * .5f, y = 1f - v, ax = Mathf.Abs(x);
        bool gum = y > .84f && y < .97f && ax < .43f;
        float down = (.86f - y) / .56f; // 0 at the gum, 1 at the tip
        bool fang = down >= 0f && down <= 1f && Mathf.Abs(ax - (.24f - .05f * down)) < .13f * Mathf.Pow(1f - down, .8f) + .008f;
        return gum || fang || Teardrop(x, y - .13f, .085f, 2.4f);
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

    // Paint over what is there (dark cracks in stone), keeping the stronger alpha.
    private static void Over(Color32[] px, int x, int y, Color color, float alpha)
    {
        if (x < 0 || y < 0 || x >= W || y >= H) return;
        int i = y * W + x;
        Color old = px[i];
        var mixed = Color.Lerp(old, color, alpha);
        mixed.a = Math.Max(old.a, Clamp01(alpha));
        px[i] = mixed;
    }

    private static void Line(Color32[] px, float x0, float y0, float x1, float y1, Color color, float alpha, bool over = false)
    {
        int steps = Math.Max(1, (int)(Mathf.Max(Mathf.Abs(x1 - x0), Mathf.Abs(y1 - y0)) * 1.5f));
        for (int i = 0; i <= steps; i++)
        {
            float t = i / (float)steps;
            int x = (int)Mathf.Lerp(x0, x1, t), y = (int)Mathf.Lerp(y0, y1, t);
            if (over) Over(px, x, y, color, alpha); else Max(px, x, y, color, alpha);
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
