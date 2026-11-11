using System.Collections.Generic;
using IncreaseHardMod.Valuables;
using IncreaseHardMod.Visuals;
using UnityEngine;

namespace IncreaseHardMod.Challenges;

/// <summary>Local, decorative geometry. The host owns all progress and the carryable reward.</summary>
internal sealed class RitualProps : MonoBehaviour
{
    private static readonly Texture2D[] Paint = new Texture2D[6];
    private readonly List<Renderer> _runes = new(), _frames = new();
    private Transform _cube;
    private MaterialPropertyBlock _glow;
    private int _mask = -1;
    private bool _finished;

    internal void Build(Vector3 at, float yaw, LootMap map, int variant, int total, IReadOnlyList<MapRitual.Painting> paintings)
    {
        _glow = new MaterialPropertyBlock();
        var altar = Node(transform, "Altar", at, Quaternion.Euler(0f, yaw, 0f));
        var stone = ChallengeProps.Textured("ritual slate", new Color(.35f, .38f, .41f), .19f, .05f, ChallengeTex.Stone);
        var bronze = ChallengeProps.Textured("ritual brass", new Color(.62f, .4f, .19f), .55f, .6f, Texture2D.whiteTexture);
        var rune = ChallengeProps.Textured("ritual glyph", new Color(.26f, .21f, .13f), .32f, .2f, Texture2D.whiteTexture, new Color(.25f, .18f, .08f));
        var baseMesh = ChallengeProps.Mesh("ritual base", m => m.Crystal(
            ProcMesh.TRS(Vector3.up * .075f, Vector3.zero, new(1.58f, .17f, 1.58f)), 8, -.45f, .45f));
        ChallengeProps.Part(altar, "Octagonal stone plinth", baseMesh, stone);
        _cube = Node(altar, "Dormant cube", Vector3.up * .48f, Quaternion.identity);
        ChallengeProps.Part(_cube, "Cube", ChallengeProps.Mesh("ritual cube", m => m.ChamferBox(
            ProcMesh.TRS(Vector3.up * .27f, Vector3.zero, Vector3.one * .54f), .17f)), bronze);
        ChallengeProps.Part(_cube, "Cube glyphs", ChallengeProps.Mesh("ritual cube glyphs", _ => RitualLoot.CubeGlyphs(.54f)), rune);
        ChallengeProps.Lamp(_cube, Vector3.up * .35f, new Color(.93f, .62f, .24f), 2.6f, .32f);

        for (int i = 0; i < total; i++)
        {
            float angle = i * (360f / total);
            var marker = Node(altar, $"Offering rune {i + 1}",
                Quaternion.Euler(0f, angle, 0f) * (Vector3.forward * .63f) + Vector3.up * .171f, Quaternion.Euler(0f, angle, 0f));
            _runes.Add(ChallengeProps.Part(marker, "Inlay", ChallengeProps.Mesh("ritual inlay", m => m.Diamond(
                ProcMesh.TRS(Vector3.zero, Vector3.zero, new(.22f, .033f, .16f)))), rune).GetComponent<Renderer>());
        }
        if (map == LootMap.Manor)
        {
            var chalk = ChallengeProps.Textured("ritual chalk", new Color(.76f, .75f, .65f), .04f, 0f,
                Texture2D.whiteTexture, new Color(.14f, .13f, .09f));
            ChallengeProps.Part(altar, "Chalk outline", ChallengeProps.Mesh("chalk outline " + variant,
                _ => RitualLoot.SkeletonMesh(variant, chalk: true)), chalk).transform.localPosition = Vector3.right * 1.28f;
        }

        for (int i = 0; i < paintings.Count; i++)
        {
            var p = paintings[i];
            var frame = Node(transform, "Museum painting " + (i + 1), p.Position, Quaternion.Euler(0f, p.Yaw, 0f));
            var frameMesh = ChallengeProps.Mesh("ritual painting frame", m =>
            {
                m.ChamferBox(ProcMesh.TRS(Vector3.zero, Vector3.zero, new(.98f, .74f, .075f)), .12f);
                foreach (int sign in new[] { -1, 1 })
                {
                    m.ChamferBox(ProcMesh.TRS(new(sign * .454f, 0, .046f), Vector3.zero, new(.035f, .72f, .03f)), .17f);
                    m.ChamferBox(ProcMesh.TRS(new(0, sign * .341f, .046f), Vector3.zero, new(.94f, .035f, .03f)), .17f);
                }
                return m;
            });
            ChallengeProps.Part(frame, "Wood and gilding", frameMesh, bronze);
            var canvas = ChallengeProps.Mesh("ritual painting canvas", m =>
                m.Box(ProcMesh.TRS(Vector3.forward * .054f, Vector3.zero, new(.856f, .62f, .014f))));
            var picture = ChallengeProps.Textured("ritual oil painting " + i, Color.white, .12f, 0f,
                PaintingTexture(i), new Color(.17f, .17f, .17f));
            // Unit quad's positive-Z face is the front of the painting, where the touch is checked.
            ChallengeProps.Part(frame, "Canvas", canvas, stone);
            var art = ChallengeProps.Part(frame, "Paint", PaintingFace(), picture);
            art.transform.localPosition = Vector3.forward * .063f;
            art.transform.localScale = new(.855f, .62f, 1f);
            _frames.Add(ChallengeProps.Part(frame, "Touch rune", ChallengeProps.Mesh("ritual painting sigil", m => m.Diamond(
                ProcMesh.TRS(new(0, -.339f, .072f), Vector3.zero, new(.145f, .095f, .039f)))), rune).GetComponent<Renderer>());
            if (!p.OnWall)
                ChallengeProps.Part(frame, "Tripod easel", ChallengeProps.Mesh("ritual easel", m => m
                    .Box(ProcMesh.TRS(new(-.28f, -.71f, -.071f), new(-5, 0, -9), new(.055f, 1.12f, .06f)))
                    .Box(ProcMesh.TRS(new(.28f, -.71f, -.071f), new(-5, 0, 9), new(.055f, 1.12f, .06f)))
                    .Box(ProcMesh.TRS(new(0, -.69f, -.22f), new(17, 0, 0), new(.055f, 1.12f, .06f)))), stone);
        }
        Refresh(0, false);
    }

    internal void Refresh(int mask, bool finished)
    {
        if (_cube != null && !finished) _cube.localRotation = Quaternion.Euler(10f, Time.time * 24f, 8f);
        if (_mask == mask && _finished == finished) return;
        _mask = mask;
        _finished = finished;
        if (_cube != null) _cube.gameObject.SetActive(!finished);
        for (int i = 0; i < _runes.Count; i++) SetGlow(_runes[i], i, (mask & 1 << i) != 0);
        for (int i = 0; i < _frames.Count; i++) SetGlow(_frames[i], i, (mask & 1 << i) != 0);
    }

    private void SetGlow(Renderer renderer, int index, bool accepted)
    {
        _glow.Clear();
        var color = accepted ? ChallengeProps.Win : RitualLoot.Glow[index];
        _glow.SetColor("_Color", color * .6f);
        _glow.SetColor("_EmissionColor", color * (accepted ? .8f : .34f));
        renderer.SetPropertyBlock(_glow);
    }

    private static Transform Node(Transform parent, string name, Vector3 position, Quaternion rotation)
    {
        var node = new GameObject(name).transform;
        node.SetParent(parent, false);
        node.localPosition = position;
        node.localRotation = rotation;
        return node;
    }

    private static Mesh _paintingFace;
    private static Mesh PaintingFace()
    {
        if (_paintingFace != null) return _paintingFace;
        _paintingFace = new Mesh
        {
            name = "IHM painted canvas",
            vertices = new Vector3[] { new(-.5f,-.5f,0), new(.5f,-.5f,0), new(.5f,.5f,0), new(-.5f,.5f,0) },
            uv = new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up },
            triangles = new[] { 0, 1, 2, 0, 2, 3 },
        };
        _paintingFace.RecalculateNormals();
        return _paintingFace;
    }

    // Six small original landscapes. Grain and a muted palette keep the canvases legible in the museum's lighting.
    private static Texture2D PaintingTexture(int index)
    {
        if (Paint[index] != null) return Paint[index];
        const int width = 128, height = 96;
        var pixels = new Color[width * height];
        Color palette = RitualLoot.Glow[index];
        var random = new System.Random(index * 997 + 65);
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            float u = x / (float)width, v = y / (float)height;
            float ridge = .44f + .09f * Mathf.Sin(u * 7.6f + index) + .044f * Mathf.Sin(u * 21f + index);
            var sky = Color.Lerp(palette * .68f, new Color(.08f, .11f, .17f), v);
            float moonX = .24f + (index % 3) * .22f;
            var toMoon = new Vector2((u - moonX) * 1.3f, v - .75f);
            if (toMoon.sqrMagnitude < .0058f) sky = Color.Lerp(palette, new Color(.92f, .82f, .59f), .68f);
            if (v < ridge)
            {
                var hills = Color.Lerp(new Color(.082f, .094f, .091f), palette * .3f, v);
                float bank = .27f + .04f * Mathf.Cos(u * 10f + index);
                sky = v < bank ? Color.Lerp(palette * .36f, hills, v / bank) : hills;
                if (index % 2 == 0 && v < bank && Mathf.Abs(u - moonX) < .12f * (1f - v))
                    sky = Color.Lerp(sky, palette * .53f, Mathf.Abs(Mathf.Sin(v * 123f + u * 14f)) * .6f);
            }
            float grain = .89f + .22f * (float)random.NextDouble();
            float vignette = 1f - .64f * new Vector2(u - .5f, v - .5f).sqrMagnitude;
            pixels[y * width + x] = sky * (grain * vignette);
        }
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            name = "IHM ritual landscape " + index, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear,
        };
        texture.SetPixels(pixels);
        texture.Apply(false, true);
        return Paint[index] = texture;
    }
}
