using System.Collections.Generic;
using UnityEngine;

namespace IncreaseHardMod.Visuals;

/// <summary>
/// Drawn map icons for the Map perks: the challenge shrine (a pedestal with a floating gem), a battery crystal, a living
/// player (a figure) and a dead player (a death head). Each is a white shape with a dark outline, made by
/// <see cref="MonsterIcons.Stylise"/>, so the map marker's colour tints it. Built once from shape functions; no art files.
/// </summary>
internal static class MapIcons
{
    internal enum Kind { Shrine, Crystal, Player, DeadPlayer }

    private static readonly Dictionary<Kind, Texture2D> Textures = new();
    private static readonly Dictionary<(Kind, int), Sprite> Sprites = new();

    /// <summary>The icon as a map sprite <paramref name="worldSize"/> units across.</summary>
    internal static Sprite Sprite(Kind kind, float worldSize)
    {
        if (worldSize <= 0f) return null;
        int key = Mathf.RoundToInt(worldSize * 10000f);
        if (Sprites.TryGetValue((kind, key), out var cached) && cached != null) return cached;
        var texture = Texture(kind);
        if (texture == null) return null;
        var sprite = UnityEngine.Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(.5f, .5f), texture.width / worldSize);
        sprite.name = "IHM Map " + kind;
        return Sprites[(kind, key)] = sprite;
    }

    private static Texture2D Texture(Kind kind)
    {
        if (Textures.TryGetValue(kind, out var cached) && cached != null) return cached;
        System.Func<float, float, bool> shape = kind switch
        {
            Kind.Shrine => Shrine,
            Kind.Crystal => Crystal,
            Kind.Player => Figure,
            _ => DeathHead,
        };
        int n = MonsterIcons.Size;
        var mask = new Color32[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
                if (shape((x + .5f) / n, (y + .5f) / n)) mask[y * n + x] = new Color32(255, 255, 255, 255);
        var texture = MonsterIcons.Stylise(mask);
        if (texture != null) texture.name = "IHM Map Icon " + kind;
        return Textures[kind] = texture;
    }

    // Shapes in the unit square, (0,0) bottom left.
    private static bool Shrine(float u, float v)
    {
        float x = Mathf.Abs(u - .5f);
        bool plinth = v < .14f && x < .34f || v >= .14f && v < .22f && x < .24f;
        bool pillar = v >= .22f && v < .5f && x < .1f;
        bool cap = v >= .5f && v < .56f && x < .2f;
        float gy = v - .76f;                                // the floating gem: a diamond above the cap
        bool gem = Mathf.Abs(gy) / .17f + x / .12f <= 1f;
        return plinth || pillar || cap || gem;
    }

    // A cluster: a tall crystal in the middle and two smaller ones leaning out from the same base.
    private static bool Crystal(float u, float v) =>
        Shard(u, v, .5f, .06f, .86f, .13f, 0f) || Shard(u, v, .36f, .08f, .52f, .09f, 28f) || Shard(u, v, .64f, .08f, .58f, .09f, -28f);

    // One pointed crystal standing on (baseU, baseV), leaning by angle degrees (positive = to the left).
    private static bool Shard(float u, float v, float baseU, float baseV, float height, float half, float angle)
    {
        float a = angle * Mathf.Deg2Rad, du = u - baseU, dv = v - baseV;
        float lu = du * Mathf.Cos(a) + dv * Mathf.Sin(a), lv = -du * Mathf.Sin(a) + dv * Mathf.Cos(a);
        if (lv < 0f || lv > height) return false;
        float shoulder = height * .72f;
        return lv < shoulder ? Mathf.Abs(lu) < half : Mathf.Abs(lu) < half * (1f - (lv - shoulder) / (height - shoulder));
    }

    private static bool Figure(float u, float v)
    {
        float x = u - .5f;
        bool head = x * x + (v - .74f) * (v - .74f) < .15f * .15f;
        bool body = v < .56f && v > .08f && (x * x) / (.3f * .3f) + ((v - .08f) * (v - .08f)) / (.46f * .46f) < 1f && v > .08f;
        return head || body;
    }

    private static bool DeathHead(float u, float v)
    {
        // A round head with two hollow eyes and a jaw: the shape of the game's death head.
        float x = u - .5f, y = v - .55f;
        bool skull = x * x / (.36f * .36f) + y * y / (.32f * .32f) < 1f;
        bool jaw = v > .1f && v < .3f && Mathf.Abs(x) < .2f;
        bool eye = (x + .14f) * (x + .14f) + (y + .02f) * (y + .02f) < .085f * .085f || (x - .14f) * (x - .14f) + (y + .02f) * (y + .02f) < .085f * .085f;
        return (skull || jaw) && !eye;
    }
}
