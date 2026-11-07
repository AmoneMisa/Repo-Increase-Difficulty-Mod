using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using IncreaseHardMod.Visuals;

namespace IncreaseHardMod.Challenges;

/// <summary>
/// Local-only challenge props, built on every client from the host's state (see <see cref="ChallengeManager"/>):
/// the shrine, the escort wisp and its lantern home, archery targets, breakable crates and the hunt beacon.
/// Faceted meshes with procedural textures (weathered stone, wood grain, slate, worn metal, painted target faces) and
/// restrained glow, to sit in R.E.P.O.'s grimy facilities rather than look like toys. Destroyed with the level scene.
/// </summary>
internal static class ChallengeProps
{
    internal static readonly Color Gold = new(1f, .78f, .32f), Wisp = new(.62f, .93f, 1f), Hunt = new(1f, .3f, .28f),
        Win = new(.45f, 1f, .45f), Lose = new(1f, .35f, .3f);

    private static readonly Dictionary<string, Mesh> Meshes = new();
    private static readonly Dictionary<string, Material> Materials = new();
    private static Mesh _quad;

    internal static Material Mat(Color color, float emission, float gloss = .5f, float metallic = .2f)
    {
        var m = new Material(Shader.Find("Standard")) { name = "IHM Challenge", color = color };
        if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", gloss);
        if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metallic);
        if (emission > 0f) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", color * emission); }
        return m;
    }

    /// <summary>A shared textured material (stone, wood, slate, metal, target face).</summary>
    internal static Material Textured(string key, Color tint, float gloss, float metallic, Texture2D texture, Color emission = default)
    {
        if (Materials.TryGetValue(key, out var cached) && cached != null) return cached;
        var m = new Material(Shader.Find("Standard")) { name = "IHM Challenge " + key, color = tint, mainTexture = texture, hideFlags = HideFlags.HideAndDontSave };
        if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", gloss);
        if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metallic);
        if (emission.maxColorComponent > 0f) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", emission); m.SetTexture("_EmissionMap", texture); }
        return Materials[key] = m;
    }

    /// <summary>Translucent glass in the style of the battery crystals (premultiplied Standard, ships in the build).</summary>
    internal static Material Glass(Color color, Color emission)
    {
        var m = new Material(Shader.Find("Standard")) { name = "IHM Challenge Glass", color = color };
        m.SetFloat("_Mode", 3);
        m.SetInt("_SrcBlend", (int)BlendMode.One);
        m.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
        m.SetInt("_ZWrite", 0);
        m.EnableKeyword("_ALPHAPREMULTIPLY_ON");
        m.SetFloat("_Glossiness", .95f);
        m.SetFloat("_Metallic", .1f);
        m.EnableKeyword("_EMISSION");
        m.SetColor("_EmissionColor", emission);
        m.renderQueue = 3000;
        return m;
    }

    internal static Mesh Mesh(string key, System.Func<ProcMesh, ProcMesh> build, float uvScale = 0f)
    {
        if (Meshes.TryGetValue(key, out var mesh) && mesh != null) return mesh;
        var proc = build(new ProcMesh());
        mesh = uvScale > 0f ? proc.Build("IHM Challenge " + key, uvScale) : proc.Build("IHM Challenge " + key);
        mesh.hideFlags = HideFlags.HideAndDontSave;
        return Meshes[key] = mesh;
    }

    internal static GameObject Part(Transform parent, string name, Mesh mesh, Material material)
    {
        var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
        go.transform.SetParent(parent, false);
        go.GetComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = go.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        return go;
    }

    internal static Light Lamp(Transform parent, Vector3 at, Color color, float range, float intensity)
    {
        var go = new GameObject("Light");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = at;
        var light = go.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = color;
        light.range = range;
        light.intensity = intensity;
        light.shadows = LightShadows.None;
        return light;
    }

    /// <summary>A camera-facing soft glow (additive halo sprite) at <paramref name="at"/>.</summary>
    internal static Transform Halo(Transform parent, Vector3 at, float size, Color color)
    {
        if (_quad == null) _quad = ProcMesh.UnitQuad("IHM Challenge Halo");
        var go = new GameObject("Halo", typeof(MeshFilter), typeof(MeshRenderer));
        go.transform.SetParent(parent, false);
        go.transform.localPosition = at;
        go.transform.localScale = Vector3.one * size;
        go.GetComponent<MeshFilter>().sharedMesh = _quad;
        var renderer = go.GetComponent<MeshRenderer>();
        renderer.material = GlowTextures.Sprite(GlowTextures.Soft, "IHM Challenge Halo");
        renderer.material.color = color;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        go.AddComponent<Billboard>();
        return go.transform;
    }

    /// <summary>A flat disc in the local XY plane facing -Z (toward a viewer in front), with radial UVs: the centre at
    /// (0.5, 0.5) and the rim on the unit circle, so a centred texture lands exactly once across it.</summary>
    internal static Mesh Disc(string key, float radius, int sides, float z)
    {
        if (Meshes.TryGetValue(key, out var cached) && cached != null) return cached;
        var vertices = new List<Vector3> { new(0f, 0f, z) };
        var uv = new List<Vector2> { new(.5f, .5f) };
        var tris = new List<int>();
        for (int i = 0; i <= sides; i++)
        {
            float a = i * Mathf.PI * 2f / sides;
            vertices.Add(new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius, z));
            uv.Add(new Vector2(.5f + .5f * Mathf.Cos(a), .5f + .5f * Mathf.Sin(a)));
            if (i > 0) { tris.Add(0); tris.Add(i + 1); tris.Add(i); } // wound to face -Z
        }
        var mesh = new Mesh { name = "IHM Challenge " + key, hideFlags = HideFlags.HideAndDontSave };
        mesh.SetVertices(vertices);
        mesh.SetUVs(0, uv);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return Meshes[key] = mesh;
    }

    /// <summary>Soft glowing motes drifting around a prop (additive halo sprites), in world space so moving props leave
    /// a faint wake.</summary>
    internal static ParticleSystem Motes(Transform parent, Vector3 at, Color color, float rate, float radius, float rise, float size, bool world = true)
    {
        var go = new GameObject("Motes");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = at;
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.loop = true; main.playOnAwake = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2.4f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(rise * .4f, rise);
        main.startSize = new ParticleSystem.MinMaxCurve(size * .5f, size);
        main.startColor = color;
        main.maxParticles = 60;
        main.simulationSpace = world ? ParticleSystemSimulationSpace.World : ParticleSystemSimulationSpace.Local;
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = radius;
        var emission = ps.emission; emission.rateOverTime = rate;
        var noise = ps.noise; noise.enabled = true; noise.strength = .25f; noise.frequency = .6f;
        var fade = ps.colorOverLifetime; fade.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, .25f), new GradientAlphaKey(0f, 1f) });
        fade.color = gradient;
        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = GlowTextures.Sprite(GlowTextures.Soft, "IHM Challenge Motes");
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        ps.Play();
        return ps;
    }

    private sealed class Billboard : MonoBehaviour
    {
        private void LateUpdate()
        {
            var cam = Camera.main;
            if (cam != null) transform.rotation = Quaternion.LookRotation(transform.position - cam.transform.position);
        }
    }

    // A ring of short bevelled segments in the local XY plane (targets) or XZ plane (floor rings).
    internal static ProcMesh Ring(ProcMesh mesh, float radius, float thickness, int segments, bool flat)
    {
        float arc = 2f * Mathf.PI * radius / segments * 1.08f;
        for (int i = 0; i < segments; i++)
        {
            float a = i * 360f / segments;
            var dir = flat ? Quaternion.Euler(0f, a, 0f) * Vector3.forward : Quaternion.Euler(0f, 0f, a) * Vector3.up;
            var euler = flat ? new Vector3(0f, a, 0f) : new Vector3(0f, 0f, a);
            var size = flat ? new Vector3(arc, thickness * .5f, thickness) : new Vector3(arc, thickness, thickness * .6f);
            mesh.ChamferBox(ProcMesh.TRS(dir * radius, euler, size), .3f);
        }
        return mesh;
    }

    internal static TextMeshPro Label(Transform parent, Vector3 at, float size, Color color)
    {
        // RectTransform from the start: adding TextMeshPro to a plain Transform swaps it out.
        var go = new GameObject("Label", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        go.transform.localPosition = at;
        var text = go.AddComponent<TextMeshPro>();
        var font = HudFont();
        if (font != null) text.font = font;
        text.fontSize = size;
        text.alignment = TextAlignmentOptions.Center;
        text.color = color;
        text.rectTransform.sizeDelta = new Vector2(4f, 1f);
        return text;
    }

    private static TMP_FontAsset _font;
    private static TMP_FontAsset HudFont()
    {
        if (_font != null) return _font;
        var hud = HUDCanvas.instance != null ? HUDCanvas.instance.GetComponentInChildren<TMP_Text>(true) : null;
        if (hud != null && hud.font != null) return _font = hud.font;
        foreach (var f in Resources.FindObjectsOfTypeAll<TMP_FontAsset>())
            if (f != null && f.name.IndexOf("Teko", System.StringComparison.OrdinalIgnoreCase) >= 0) return _font = f;
        return null;
    }

    /// <summary>Turn a transform to face the camera around Y only.</summary>
    internal static void FaceCamera(Transform t)
    {
        var cam = Camera.main;
        if (cam == null || t == null) return;
        var to = t.position - cam.transform.position;
        to.y = 0f;
        if (to.sqrMagnitude > .0001f) t.rotation = Quaternion.LookRotation(to);
    }

    /// <summary>A map dot on this object (gold shrine, cyan home, red hunt target).</summary>
    internal static void MapDot(GameObject host, Color color)
    {
        var sprite = Upgrades.MapUpgrade.MarkerController.MarkerSprite();
        if (sprite == null || host == null || host.GetComponent<MapCustom>() != null) return;
        var custom = host.AddComponent<MapCustom>();
        custom.autoAdd = false;
        custom.sprite = sprite;
        custom.color = color;
        try { custom.Add(); } catch { /* the map is not ready yet */ }
    }
}

/// <summary>Procedural, tiling textures for the props (no image files ship): stone, wood, slate, metal, target face.</summary>
internal static class ChallengeTex
{
    private static Texture2D _stone, _wood, _slate, _metal, _target;
    private const int N = 128;

    private static float Noise(float x, float y, int octaves, int seed)
    {
        float sum = 0f, amp = .5f, freq = 1f;
        for (int o = 0; o < octaves; o++) { sum += amp * Mathf.PerlinNoise(x * freq + seed * 17.3f, y * freq + seed * 9.1f); amp *= .5f; freq *= 2f; }
        return sum;
    }

    // Tiling noise: blend four offset samples so the edges meet.
    private static float Tile(float u, float v, float scale, int octaves, int seed)
    {
        float a = Noise(u * scale, v * scale, octaves, seed), b = Noise((u - 1f) * scale, v * scale, octaves, seed);
        float c = Noise(u * scale, (v - 1f) * scale, octaves, seed), d = Noise((u - 1f) * scale, (v - 1f) * scale, octaves, seed);
        return Mathf.Lerp(Mathf.Lerp(a, b, u), Mathf.Lerp(c, d, u), v);
    }

    private static Texture2D Make(string name, System.Func<float, float, Color> pixel)
    {
        var tex = new Texture2D(N, N, TextureFormat.RGBA32, true) { name = "IHM " + name, wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.HideAndDontSave };
        var px = new Color[N * N];
        for (int y = 0; y < N; y++) for (int x = 0; x < N; x++) px[y * N + x] = pixel(x / (float)N, y / (float)N);
        tex.SetPixels(px);
        tex.Apply(true, true);
        return tex;
    }

    internal static Texture2D Stone => _stone ??= Make("stone", (u, v) =>
    {
        float n = Tile(u, v, 6f, 4, 1), crack = Mathf.Abs(Tile(u, v, 3f, 3, 2) - .5f) < .018f ? .55f : 1f;
        float g = Mathf.Lerp(.45f, .8f, n) * crack;
        return new Color(g, g * .97f, g * .93f);
    });

    internal static Texture2D Wood => _wood ??= Make("wood", (u, v) =>
    {
        float grain = .5f + .5f * Mathf.Sin((v * 22f + Tile(u, v, 4f, 3, 3) * 6f) * Mathf.PI);
        float dirt = Tile(u, v, 8f, 3, 4);
        float g = Mathf.Lerp(.55f, .9f, grain * .6f + dirt * .4f);
        return new Color(g, g * .78f, g * .55f);
    });

    internal static Texture2D Slate => _slate ??= Make("slate", (u, v) =>
    {
        float row = Mathf.Repeat(v * 6f, 1f), edge = row < .08f ? .6f : 1f;
        float g = Mathf.Lerp(.45f, .7f, Tile(u, v, 9f, 3, 5)) * edge;
        return new Color(g * .82f, g * .9f, g);
    });

    internal static Texture2D Metal => _metal ??= Make("metal", (u, v) =>
    {
        float brushed = Tile(u * 4f, v * .25f, 6f, 2, 6), rust = Mathf.Clamp01((Tile(u, v, 5f, 3, 7) - .55f) * 4f);
        float g = Mathf.Lerp(.5f, .75f, brushed);
        return Color.Lerp(new Color(g, g, g * 1.03f), new Color(.45f, .28f, .16f), rust * .6f);
    });

    // Painted bullseye centred on UV (0.5, 0.5): the radial disc of a target maps it exactly once.
    internal static Texture2D TargetFace => _target ??= Make("target", (u, v) =>
    {
        float du = u - .5f, dv = v - .5f;
        float r = Mathf.Sqrt(du * du + dv * dv) * 2f; // 0 centre .. 1 rim
        var red = new Color(.72f, .12f, .1f); var cream = new Color(.88f, .84f, .74f); var wood = new Color(.5f, .36f, .22f);
        Color paint = r < .16f ? red : r < .34f ? cream : r < .52f ? red : r < .72f ? cream : r < .9f ? red : wood;
        float wear = Tile(u, v, 10f, 3, 8);
        return Color.Lerp(paint, paint * .55f, Mathf.Clamp01((wear - .6f) * 3f));
    });
}

/// <summary>The challenge shrine: a weathered stone pedestal with carved rune marks and a floating amber crystal.</summary>
internal sealed class ShrineVisual : MonoBehaviour
{
    private Transform _gem;
    private TextMeshPro _label;
    private Light _light;
    private Material _glow;
    private ChallengePhase _phase;
    private float _doneAt = -10f, _flareUntil = -10f;

    /// <summary>Pinged: the gem spins up and the light pulses for <paramref name="seconds"/>.</summary>
    internal void Flare(float seconds) => _flareUntil = Mathf.Max(_flareUntil, Time.time + Mathf.Max(.5f, seconds));

    internal static ShrineVisual Create(Vector3 floor, float yaw)
    {
        var root = new GameObject("IHM Challenge Shrine");
        root.transform.SetPositionAndRotation(floor, Quaternion.Euler(0f, yaw, 0f));
        var v = root.AddComponent<ShrineVisual>();
        v.Build();
        return v;
    }

    private void Build()
    {
        var stone = ChallengeProps.Textured("stone", new Color(.62f, .6f, .58f), .18f, 0f, ChallengeTex.Stone);
        ChallengeProps.Part(transform, "Base", ChallengeProps.Mesh("ShrineStone", m => m
            .Crystal(ProcMesh.TRS(new Vector3(0f, .1f, 0f), Vector3.zero, new Vector3(1.05f, .2f, 1.05f)), 8, -.5f, .5f)
            .Crystal(ProcMesh.TRS(new Vector3(0f, .24f, 0f), new Vector3(0f, 22.5f, 0f), new Vector3(.75f, .1f, .75f)), 8, -.5f, .5f)
            .ChamferBox(ProcMesh.TRS(new Vector3(0f, .58f, 0f), new Vector3(0f, 45f, 0f), new Vector3(.3f, .6f, .3f)), .14f)
            .Crystal(ProcMesh.TRS(new Vector3(0f, .92f, 0f), Vector3.zero, new Vector3(.52f, .1f, .52f)), 8, -.5f, .5f), 2f), stone);
        // Rune marks cut into the plinth and the pillar: faint embers, not neon.
        _glow = ChallengeProps.Mat(ChallengeProps.Gold, .7f, .3f, 0f);
        ChallengeProps.Part(transform, "Runes", ChallengeProps.Mesh("ShrineRunes", m =>
        {
            for (int i = 0; i < 8; i++)
            {
                float a = i * 45f + 22.5f;
                var dir = Quaternion.Euler(0f, a, 0f) * Vector3.forward;
                m.ChamferBox(ProcMesh.TRS(dir * .49f + Vector3.up * .11f, new Vector3(0f, a, 0f), new Vector3(.12f, .05f, .012f)), .3f);
                m.ChamferBox(ProcMesh.TRS(dir * .155f + Vector3.up * .62f, new Vector3(0f, a, 0f), new Vector3(.04f, .16f, .01f)), .3f);
            }
            return m;
        }), _glow);
        var glass = ChallengeProps.Glass(new Color(1f, .72f, .28f, .5f), new Color(.45f, .28f, .06f));
        _gem = new GameObject("Gem").transform;
        _gem.SetParent(transform, false);
        _gem.localPosition = new Vector3(0f, 1.25f, 0f);
        ChallengeProps.Part(_gem, "Core", ChallengeProps.Mesh("ShrineCore", m => m
            .Crystal(ProcMesh.TRS(Vector3.zero, Vector3.zero, new Vector3(.08f, .2f, .08f)), 6)), ChallengeProps.Mat(ChallengeProps.Gold, 1.1f, .6f));
        ChallengeProps.Part(_gem, "Shell", ChallengeProps.Mesh("ShrineShell", m => m
            .Crystal(ProcMesh.TRS(Vector3.zero, Vector3.zero, new Vector3(.2f, .36f, .2f)), 6)), glass);
        _light = ChallengeProps.Lamp(transform, new Vector3(0f, 1.25f, 0f), ChallengeProps.Gold, 3.5f, 1f);
        // Embers rising around the plinth and a faint shimmer at the crystal.
        ChallengeProps.Motes(transform, new Vector3(0f, .3f, 0f), new Color(1f, .75f, .35f, .7f), 7f, .55f, .35f, .07f, world: false);
        ChallengeProps.Motes(_gem, Vector3.zero, new Color(1f, .85f, .5f, .8f), 5f, .18f, .12f, .05f, world: false);
        _label = ChallengeProps.Label(transform, new Vector3(0f, 1.8f, 0f), 2.2f, new Color(1f, .9f, .65f));
        ChallengeProps.MapDot(gameObject, ChallengeProps.Gold);
    }

    internal void SetState(ChallengePhase phase, string label)
    {
        if (phase != _phase && (phase == ChallengePhase.Won || phase == ChallengePhase.Lost)) _doneAt = Time.time;
        _phase = phase;
        if (_label != null) _label.text = label ?? "";
        var colour = phase == ChallengePhase.Won ? ChallengeProps.Win : phase == ChallengePhase.Lost ? ChallengeProps.Lose : ChallengeProps.Gold;
        _glow.color = colour;
        _glow.SetColor("_EmissionColor", colour * (phase >= ChallengePhase.Won ? .3f : phase == ChallengePhase.Active ? 1.4f : .7f));
        if (_light != null) _light.color = colour;
    }

    private void Update()
    {
        if (_gem == null) return;
        float now = Time.time;
        bool active = _phase == ChallengePhase.Active || now < _flareUntil, done = _phase >= ChallengePhase.Won;
        _gem.localRotation = Quaternion.Euler(0f, now * (active ? 120f : 25f), 0f);
        _gem.localPosition = new Vector3(0f, 1.25f + Mathf.Sin(now * 1.8f) * .04f, 0f);
        _light.intensity = done ? Mathf.Lerp(2f, .2f, Mathf.Clamp01((now - _doneAt) / 1.5f)) : active ? 1.8f + Mathf.Sin(now * 7f) * .4f : .9f + Mathf.Sin(now * 2.5f) * .15f;
        if (_label != null) { ChallengeProps.FaceCamera(_label.transform); _label.gameObject.SetActive(!done || now - _doneAt < 4f); }
    }
}

/// <summary>The escort wisp: a soft glowing mote with a halo, two drifting sparks and a faint trail.</summary>
internal sealed class WispVisual : MonoBehaviour
{
    private Transform _core, _sparks;
    private Light _light;
    private Vector3 _target;
    private bool _waiting;

    internal static WispVisual Create(Vector3 at)
    {
        var root = new GameObject("IHM Challenge Wisp");
        root.transform.position = at;
        var v = root.AddComponent<WispVisual>();
        v._target = at;
        v.Build();
        return v;
    }

    private void Build()
    {
        _core = ChallengeProps.Part(transform, "Core", ChallengeProps.Mesh("WispCore", m => m
            .Crystal(ProcMesh.TRS(Vector3.zero, Vector3.zero, new Vector3(.12f, .14f, .12f)), 10, -.25f, .25f)), ChallengeProps.Mat(ChallengeProps.Wisp, 1.6f, .9f, 0f)).transform;
        ChallengeProps.Halo(transform, Vector3.zero, .7f, new Color(ChallengeProps.Wisp.r, ChallengeProps.Wisp.g, ChallengeProps.Wisp.b, .55f));
        _sparks = new GameObject("Sparks").transform;
        _sparks.SetParent(transform, false);
        for (int i = 0; i < 2; i++)
            ChallengeProps.Halo(_sparks, Quaternion.Euler(0f, i * 180f, 0f) * new Vector3(.22f, .05f * (i * 2 - 1), 0f), .14f, new Color(.8f, .97f, 1f, .8f));
        _light = ChallengeProps.Lamp(transform, Vector3.zero, ChallengeProps.Wisp, 3f, 1.2f);
        // Sparkles shed as it flies (world space: they hang in the air behind it).
        ChallengeProps.Motes(transform, Vector3.zero, new Color(.7f, .95f, 1f, .85f), 14f, .1f, .08f, .05f);
        var trail = gameObject.AddComponent<TrailRenderer>();
        trail.time = .5f;
        trail.startWidth = .07f;
        trail.endWidth = 0f;
        trail.sharedMaterial = GlowTextures.Sprite(GlowTextures.Soft, "IHM Wisp Trail");
        trail.startColor = new Color(ChallengeProps.Wisp.r, ChallengeProps.Wisp.g, ChallengeProps.Wisp.b, .6f);
        trail.endColor = new Color(ChallengeProps.Wisp.r, ChallengeProps.Wisp.g, ChallengeProps.Wisp.b, 0f);
        trail.shadowCastingMode = ShadowCastingMode.Off;
    }

    internal void MoveTo(Vector3 position, bool waiting)
    {
        _target = position;
        _waiting = waiting;
    }

    private void Update()
    {
        float now = Time.time;
        transform.position = Vector3.Lerp(transform.position, _target, 1f - Mathf.Exp(-6f * Time.deltaTime));
        _core.localPosition = Vector3.up * Mathf.Sin(now * 3f) * .05f;
        _sparks.localRotation = Quaternion.Euler(0f, now * (_waiting ? 260f : 120f), 0f);
        _light.intensity = _waiting ? .9f + Mathf.Abs(Mathf.Sin(now * 4f)) * 1.2f : 1.2f; // calls you while waiting
    }
}

/// <summary>The wisp's home: a small plank lantern house on a stone footing, with a slate roof, a chimney and warm windows.</summary>
internal sealed class WispHomeVisual : MonoBehaviour
{
    private Light _light;

    internal static WispHomeVisual Create(Vector3 floor)
    {
        var root = new GameObject("IHM Wisp Home");
        root.transform.position = floor;
        var v = root.AddComponent<WispHomeVisual>();
        var wood = ChallengeProps.Textured("wood", new Color(.62f, .52f, .42f), .15f, 0f, ChallengeTex.Wood);
        var slate = ChallengeProps.Textured("slate", new Color(.45f, .5f, .58f), .3f, .1f, ChallengeTex.Slate);
        var stone = ChallengeProps.Textured("stone", new Color(.62f, .6f, .58f), .18f, 0f, ChallengeTex.Stone);
        var window = ChallengeProps.Mat(new Color(1f, .78f, .42f), 1.1f, .7f, 0f);
        ChallengeProps.Part(root.transform, "Base", ChallengeProps.Mesh("HomeBase", m => m
            .ChamferBox(ProcMesh.TRS(new Vector3(0f, .04f, 0f), Vector3.zero, new Vector3(.66f, .08f, .66f)), .2f), 2f), stone);
        ChallengeProps.Part(root.transform, "Walls", ChallengeProps.Mesh("HomeWalls", m => m
            .ChamferBox(ProcMesh.TRS(new Vector3(0f, .28f, 0f), Vector3.zero, new Vector3(.52f, .42f, .52f)), .06f), 2.5f), wood);
        ChallengeProps.Part(root.transform, "Roof", ChallengeProps.Mesh("HomeRoof", m => m
            .Crystal(ProcMesh.TRS(new Vector3(0f, .7f, 0f), new Vector3(0f, 45f, 0f), new Vector3(.88f, .42f, .88f)), 4, -.5f, -.47f)
            .ChamferBox(ProcMesh.TRS(new Vector3(.14f, .78f, .1f), Vector3.zero, new Vector3(.08f, .2f, .08f)), .15f), 3f), slate);
        ChallengeProps.Part(root.transform, "Windows", ChallengeProps.Mesh("HomeWindow", m => m
            .ChamferBox(ProcMesh.TRS(new Vector3(0f, .3f, .262f), Vector3.zero, new Vector3(.12f, .14f, .02f)), .3f)
            .ChamferBox(ProcMesh.TRS(new Vector3(0f, .3f, -.262f), Vector3.zero, new Vector3(.12f, .14f, .02f)), .3f)
            .ChamferBox(ProcMesh.TRS(new Vector3(.262f, .3f, 0f), Vector3.zero, new Vector3(.02f, .14f, .12f)), .3f)
            .ChamferBox(ProcMesh.TRS(new Vector3(-.262f, .3f, 0f), Vector3.zero, new Vector3(.02f, .14f, .12f)), .3f)), window);
        v._light = ChallengeProps.Lamp(root.transform, new Vector3(0f, .35f, 0f), new Color(1f, .78f, .42f), 3f, .9f);
        // Fireflies drifting about the house, and a thin wisp of chimney glow.
        ChallengeProps.Motes(root.transform, new Vector3(0f, .45f, 0f), new Color(1f, .85f, .45f, .75f), 4f, .7f, .1f, .05f, world: false);
        ChallengeProps.Motes(root.transform, new Vector3(.14f, .92f, .1f), new Color(.75f, .9f, 1f, .45f), 3f, .03f, .3f, .08f, world: false);
        ChallengeProps.MapDot(root, ChallengeProps.Wisp);
        return v;
    }

    private void Update() { if (_light != null) _light.intensity = .85f + Mathf.Sin(Time.time * 2.3f) * .12f + Mathf.PerlinNoise(Time.time * 3f, 0f) * .15f; }
}

/// <summary>A floating archery target: a painted wooden board on a dark metal rim; knocked flat when hit.</summary>
internal sealed class TargetVisual : MonoBehaviour
{
    private Transform _body;
    private Vector3 _base;
    private float _phase, _hitAt = -10f;
    private bool _drift, _hit;

    internal static TargetVisual Create(Vector3 at, bool drift, float phase)
    {
        var root = new GameObject("IHM Challenge Target");
        root.transform.position = at;
        var v = root.AddComponent<TargetVisual>();
        v._base = at;
        v._drift = drift;
        v._phase = phase;
        v._body = new GameObject("Body").transform;
        v._body.SetParent(root.transform, false);
        // The board: a plain wooden disc, with the painted bullseye on its own radial-mapped face in front (and behind).
        ChallengeProps.Part(v._body, "Board", ChallengeProps.Mesh("TargetBoard", m => m
                .Crystal(ProcMesh.TRS(Vector3.zero, new Vector3(90f, 0f, 0f), new Vector3(.8f, .06f, .8f)), 20, -.5f, .5f), 2.5f),
            ChallengeProps.Textured("wood", new Color(.62f, .52f, .42f), .15f, 0f, ChallengeTex.Wood));
        var paint = ChallengeProps.Textured("target", Color.white, .2f, 0f, ChallengeTex.TargetFace, new Color(.18f, .14f, .12f));
        ChallengeProps.Part(v._body, "Face", ChallengeProps.Disc("TargetFaceFront", .385f, 32, -.032f), paint);
        var back = ChallengeProps.Part(v._body, "FaceBack", ChallengeProps.Disc("TargetFaceFront", .385f, 32, -.032f), paint).transform;
        back.localRotation = Quaternion.Euler(0f, 180f, 0f);
        ChallengeProps.Part(v._body, "Rim", ChallengeProps.Mesh("TargetRim", m => ChallengeProps.Ring(m, .41f, .045f, 20, false), 4f),
            ChallengeProps.Textured("metal", new Color(.42f, .42f, .45f), .45f, .7f, ChallengeTex.Metal));
        ChallengeProps.Lamp(root.transform, new Vector3(0f, 0f, -.3f), new Color(1f, .82f, .6f), 1.8f, .5f);
        return v;
    }

    /// <summary>Where the target is now (drifting ones move on a small loop; host and clients compute the same).</summary>
    internal static Vector3 Position(Vector3 basePos, bool drift, float phase, float time) =>
        drift ? basePos + new Vector3(Mathf.Sin(time * .9f + phase) * .9f, Mathf.Sin(time * 1.3f + phase) * .3f, 0f) : basePos;

    internal void SetHit(bool hit)
    {
        if (hit && !_hit) { _hitAt = Time.time; Visuals.ItemAudio.Cue("ping_upgrade", .5f).Play(transform.position); }
        _hit = hit;
    }

    private void Update()
    {
        if (_body == null) return;
        if (_hit)
        {
            float k = (Time.time - _hitAt) / .35f;
            if (k >= 1f) { gameObject.SetActive(false); return; }
            _body.localScale = new Vector3(1f + k * .3f, Mathf.Max(.02f, 1f - k), 1f); // knocked flat
            _body.localRotation *= Quaternion.Euler(Time.deltaTime * 600f, 0f, 0f);
            return;
        }
        transform.position = Position(_base, _drift, _phase, ChallengeManager.SharedTime); // same clock as the host's scoring
        ChallengeProps.FaceCamera(_body); // both faces are painted, so either side toward the viewer reads
    }
}

/// <summary>A breakable crate: planked wood with iron corner brackets and a faded paint stripe; bursts into planks.</summary>
internal sealed class CrateVisual : MonoBehaviour
{
    internal static readonly Vector3 Size = new(.6f, .6f, .6f);
    private Transform _body;
    private readonly List<(Transform T, Vector3 V)> _shards = new();
    private float _brokeAt = -10f;
    private bool _broken;

    internal static CrateVisual Create(Vector3 floor, float yaw)
    {
        var root = new GameObject("IHM Challenge Crate");
        root.transform.SetPositionAndRotation(floor, Quaternion.Euler(0f, yaw, 0f));
        var v = root.AddComponent<CrateVisual>();
        v._body = new GameObject("Body").transform;
        v._body.SetParent(root.transform, false);
        var s = Size;
        var wood = ChallengeProps.Textured("wood", new Color(.62f, .52f, .42f), .15f, 0f, ChallengeTex.Wood);
        ChallengeProps.Part(v._body, "Planks", ChallengeProps.Mesh("CratePlanks", m =>
        {
            m.ChamferBox(ProcMesh.TRS(new Vector3(0f, s.y * .5f, 0f), Vector3.zero, s * .94f), .04f); // the box
            // Three proud boards on each side, with gaps between them.
            for (int side = 0; side < 4; side++)
                for (int b = 0; b < 3; b++)
                {
                    var rot = Quaternion.Euler(0f, side * 90f, 0f);
                    var at = rot * new Vector3(0f, s.y * (b + .5f) / 3f, s.z * .48f);
                    m.ChamferBox(ProcMesh.TRS(at, new Vector3(0f, side * 90f, 0f), new Vector3(s.x * .96f, s.y / 3f - .025f, .035f)), .2f);
                }
            return m;
        }, 2.5f), wood);
        ChallengeProps.Part(v._body, "Brackets", ChallengeProps.Mesh("CrateBrackets", m =>
        {
            float h = s.x * .5f + .01f;
            foreach (var (x, z) in new[] { (1, 1), (1, -1), (-1, 1), (-1, -1) })
                foreach (float y in new[] { .06f, s.y - .06f })
                    m.ChamferBox(ProcMesh.TRS(new Vector3(x * (h - .04f), y, z * (h - .04f)), Vector3.zero, new Vector3(.11f, .1f, .11f)), .2f);
            return m;
        }, 4f), ChallengeProps.Textured("metal", new Color(.42f, .42f, .45f), .45f, .7f, ChallengeTex.Metal));
        // A faded cyan paint stripe, just bright enough to spot the crates in the dark.
        ChallengeProps.Part(v._body, "Stripe", ChallengeProps.Mesh("CrateStripe", m =>
        {
            for (int side = 0; side < 4; side++)
                m.ChamferBox(ProcMesh.TRS(Quaternion.Euler(0f, side * 90f, 0f) * new Vector3(0f, s.y * .5f, s.z * .5f + .02f),
                    new Vector3(0f, side * 90f, 0f), new Vector3(s.x * .7f, .05f, .01f)), .3f);
            return m;
        }), ChallengeProps.Mat(new Color(.35f, .85f, .95f), .5f, .3f, 0f));
        // A faint cyan dust off the paint stripe, so a crate catches the eye in a dark room.
        ChallengeProps.Motes(v._body, new Vector3(0f, s.y * .5f, 0f), new Color(.45f, .9f, 1f, .5f), 3f, s.x * .45f, .12f, .05f, world: false);
        var box = root.AddComponent<BoxCollider>();
        box.center = new Vector3(0f, s.y * .5f, 0f);
        box.size = s;
        return v;
    }

    internal void SetBroken(bool broken)
    {
        if (!broken || _broken) return;
        _broken = true;
        _brokeAt = Time.time;
        var collider = GetComponent<Collider>();
        if (collider != null) collider.enabled = false;
        _body.gameObject.SetActive(false);
        var wood = ChallengeProps.Textured("wood", new Color(.62f, .52f, .42f), .15f, 0f, ChallengeTex.Wood);
        var plank = ChallengeProps.Mesh("CrateShard", m => m.ChamferBox(ProcMesh.TRS(Vector3.zero, Vector3.zero, new Vector3(.5f, .18f, .035f)), .2f), 2.5f);
        for (int i = 0; i < 8; i++)
        {
            var t = ChallengeProps.Part(transform, "Plank", plank, wood).transform;
            t.localPosition = new Vector3(0f, Size.y * .5f, 0f);
            t.localRotation = Random.rotation;
            _shards.Add((t, Random.insideUnitSphere * 2.2f + Vector3.up * 2.4f));
        }
        Visuals.ItemAudio.Cue("ping_cosmetic", .55f).Play(transform.position);
    }

    private void Update()
    {
        if (!_broken) return;
        float k = (Time.time - _brokeAt) / 1.1f;
        if (k >= 1f) { gameObject.SetActive(false); return; }
        foreach (var (t, velocity) in _shards)
        {
            if (t == null) continue;
            t.localPosition += (velocity + Vector3.down * 9f * k) * Time.deltaTime;
            t.Rotate(380f * Time.deltaTime, 240f * Time.deltaTime, 0f);
            t.localScale = Vector3.one * Mathf.Clamp01(1.5f - k * 1.5f);
        }
    }
}

/// <summary>The hunt beacon: a red glass marker, a soft halo and a light floating over the hunted monster.</summary>
internal sealed class HuntBeacon : MonoBehaviour
{
    private Transform _follow, _gem;

    internal static HuntBeacon Create(Transform monster)
    {
        var root = new GameObject("IHM Hunt Beacon");
        var b = root.AddComponent<HuntBeacon>();
        b._follow = monster;
        b._gem = ChallengeProps.Part(root.transform, "Gem", ChallengeProps.Mesh("HuntGem", m => m
            .Crystal(ProcMesh.TRS(Vector3.zero, new Vector3(180f, 0f, 0f), new Vector3(.26f, .44f, .26f)), 4)),
            ChallengeProps.Glass(new Color(1f, .3f, .25f, .55f), new Color(.6f, .1f, .08f))).transform;
        ChallengeProps.Halo(root.transform, Vector3.zero, .6f, new Color(1f, .3f, .28f, .45f));
        ChallengeProps.Lamp(root.transform, Vector3.zero, ChallengeProps.Hunt, 3.5f, 1.2f);
        ChallengeProps.MapDot(root, ChallengeProps.Hunt);
        return b;
    }

    private void Update()
    {
        if (_follow == null || !_follow.gameObject.activeInHierarchy) { Destroy(gameObject); return; }
        transform.position = _follow.position + Vector3.up * (2.4f + Mathf.Sin(Time.time * 2.5f) * .12f);
        _gem.localRotation = Quaternion.Euler(0f, Time.time * 110f, 0f);
    }
}
