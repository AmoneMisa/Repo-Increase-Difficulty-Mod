using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace IncreaseHardMod.Visuals;

/// <summary>
/// The block of ice around a monster frozen by the Ice Pistol, built to read like a real lump of ice:
/// <list type="bullet">
/// <item>a chunky, many-faceted shell (320 jittered facets pushed toward a cube, flat underneath);</item>
/// <item>a fractured, cloudy ice texture: white crack networks and frosted patches, so the monster is half
/// hidden inside, with the cracks faintly glowing;</item>
/// <item>bright glints along every facet edge;</item>
/// <item>white frost rime caked over the lower third;</item>
/// <item>crystal shards at the base, cold mist on the floor, haze around the block, frost sparkles and a
/// faint blue light inside.</item>
/// </list>
/// Procedural and seeded per monster, so every client sees the same block. It cracks in on freeze and
/// melts away on thaw. Cosmetic only: GearEffects owns the freeze. Limitation: the game's built-in Standard
/// shader cannot refract, so the ice does not bend what is behind it.
/// </summary>
internal sealed class IceBlock : MonoBehaviour
{
    private static Material _shardMaterial, _mistMaterial, _sparkMaterial, _edgeMaterial;
    private static Texture2D _crackTexture, _rimeTexture;
    private Transform _root;
    private Material _iceMaterial, _rimeMaterial;
    private ParticleSystem _mist, _haze, _sparks;
    private Light _light;
    private float _fade, _target = 1f;

    private readonly List<Material> _wrapMaterials = new();
    private readonly List<GameObject> _wrapped = new();

    internal static IceBlock Create(Transform host, Bounds body, int seed, IReadOnlyList<Renderer> renderers = null)
    {
        var block = new GameObject("IHM Ice Block", typeof(Decoration)).AddComponent<IceBlock>();
        block.transform.SetParent(host, true);
        block.Build(body, seed, renderers);
        return block;
    }

    // (thickness x, chip x, crystal growth x, opacity, frost, bubbles, refraction, rim) per layer: clear
    // refracting ice inside, frostier and chunkier shells outside. Mirrored in RenderIcePreview.
    internal static readonly float[][] Layers =
    {
        new[] { 1f,   1f,   0f,   1f,   .5f,  .7f, .045f, .35f },
        new[] { 1.5f, 1.3f, 1.2f, .22f, .45f, .35f, .03f, .25f },
        new[] { 2f,   1.6f, 2.2f, .14f, .55f, .2f, .02f,  1f   },
    };

    // Shape-wrapped ice: each body renderer gets twins drawn with the IHM/Ice shader (one per layer), which
    // puff the surface outward into chipped crystal facets. Skinned twins share the monster's own bones, so
    // the ice follows its pose; no mesh data is read. Returns false when the shader is unavailable.
    private bool Wrap(IReadOnlyList<Renderer> renderers, Vector3 radii, Bounds body)
    {
        var shader = IceShader.Shader;
        if (shader == null || renderers == null || renderers.Count == 0) return false;
        float size = Mathf.Clamp(radii.magnitude, .3f, 3f);
        float inflate = Mathf.Clamp(size * .05f, .03f, .12f), lumps = Mathf.Clamp(size * .04f, .025f, .1f);
        for (int layer = 0; layer < Layers.Length; layer++)
        {
            var spec = Layers[layer];
            var m = new Material(shader) { name = "IHM Ice layer " + layer, renderQueue = 3020 + layer };
            m.SetTexture("_CrackTex", _crackTexture);
            m.SetFloat("_Inflate", inflate * spec[0]);
            m.SetFloat("_Lumps", lumps * spec[1]);
            m.SetFloat("_Growth", lumps * spec[2]);
            m.SetFloat("_Opacity", spec[3]);
            m.SetFloat("_Frost", spec[4]);
            m.SetFloat("_Bubbles", spec[5]);
            m.SetFloat("_Refraction", spec[6]);
            m.SetFloat("_RimStrength", spec[7]);
            m.SetFloat("_UseGrab", layer == 0 ? 1f : 0f);
            m.SetFloat("_ChipScale", Mathf.Clamp(9f / size, 3f, 14f));
            m.SetFloat("_Tiling", Mathf.Clamp(2.6f / size, 1f, 5f));
            m.SetFloat("_BubbleScale", Mathf.Clamp(14f / size, 6f, 24f));
            m.SetFloat("_Base", body.min.y);
            m.SetFloat("_Height", body.size.y);
            m.SetFloat("_Fade", 0f);
            _wrapMaterials.Add(m);
        }
        foreach (var source in renderers)
        {
            if (source == null || !source.enabled || !source.gameObject.activeInHierarchy) continue;
            foreach (var material in _wrapMaterials)
            {
                if (source is SkinnedMeshRenderer skinned && skinned.sharedMesh != null)
                {
                    var twin = Twin(source);
                    var copy = twin.AddComponent<SkinnedMeshRenderer>();
                    copy.sharedMesh = skinned.sharedMesh;
                    copy.bones = skinned.bones;
                    copy.rootBone = skinned.rootBone;
                    var b = skinned.localBounds; b.Expand(.6f); copy.localBounds = b;
                    copy.updateWhenOffscreen = skinned.updateWhenOffscreen;
                    copy.sharedMaterials = Materials(skinned.sharedMesh.subMeshCount, material);
                    Quiet(copy);
                }
                else if (source is MeshRenderer && source.TryGetComponent<MeshFilter>(out var filter) && filter.sharedMesh != null)
                {
                    var twin = Twin(source);
                    twin.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
                    var copy = twin.AddComponent<MeshRenderer>();
                    copy.sharedMaterials = Materials(filter.sharedMesh.subMeshCount, material);
                    Quiet(copy);
                }
            }
        }
        if (_wrapped.Count > 0) return true;
        foreach (var m in _wrapMaterials) Destroy(m);
        _wrapMaterials.Clear();
        return false;
    }

    private GameObject Twin(Renderer source)
    {
        var twin = new GameObject("IHM Ice Twin", typeof(Decoration)) { layer = source.gameObject.layer };
        twin.transform.SetParent(source.transform, false);
        _wrapped.Add(twin);
        return twin;
    }

    private static Material[] Materials(int count, Material material)
    {
        var all = new Material[Mathf.Max(1, count)];
        for (int i = 0; i < all.Length; i++) all[i] = material;
        return all;
    }
    private static void Quiet(Renderer r)
    {
        r.shadowCastingMode = ShadowCastingMode.Off;
        r.receiveShadows = false;
        r.lightProbeUsage = LightProbeUsage.Off;
        r.reflectionProbeUsage = ReflectionProbeUsage.BlendProbes; // the shader reflects the nearest probe
    }

    /// <summary>Start melting; the object removes itself once faded.</summary>
    internal void Thaw()
    {
        _target = 0f;
        foreach (var ps in new[] { _mist, _haze, _sparks })
            if (ps != null) { var e = ps.emission; e.enabled = false; }
    }

    private void Build(Bounds body, int seed, IReadOnlyList<Renderer> renderers)
    {
        var radii = Vector3.Max(body.extents * 1.3f + Vector3.one * .08f, new Vector3(.3f, .35f, .3f));
        transform.position = body.center;
        transform.rotation = Quaternion.identity;
        _root = transform;
        var random = new System.Random(seed);

        _crackTexture ??= CrackTexture();
        _edgeMaterial ??= GlowTextures.Sprite(Texture2D.whiteTexture, "IHM Ice Edge", 3010);
        if (!Wrap(renderers, radii, body))
        {
            // Fallback: the Standard-shader cracked block.
            _rimeTexture ??= RimeTexture();
            _iceMaterial = Glass("IHM Ice", new Color(.8f, .94f, 1f, 1f), _crackTexture, new Color(.16f, .3f, .42f));
            _iceMaterial.SetFloat("_Glossiness", .97f);
            _rimeMaterial = Glass("IHM Ice Rime", new Color(.92f, .97f, 1f, 1f), _rimeTexture, new Color(.18f, .24f, .3f));
            _rimeMaterial.SetFloat("_Glossiness", .35f);
            var shell = BuildShell(random, radii, body.extents.y);
            Part("Shell", shell.ice, _iceMaterial);
            Part("Rime", shell.rime, _rimeMaterial);
            Part("Edges", shell.edges, _edgeMaterial);
        }

        // Crystal shards: clusters jutting out around the base and a few up the sides.
        _shardMaterial ??= Glass("IHM Ice Shard", new Color(.85f, .96f, 1f, .55f), null, new Color(.1f, .24f, .36f), shared: true);
        var shards = new ProcMesh();
        int count = 8 + random.Next(4);
        for (int i = 0; i < count; i++)
        {
            float a = (float)(i / (double)count * Mathf.PI * 2 + random.NextDouble() * .5);
            float up = i % 4 == 0 ? (float)random.NextDouble() * .5f - .2f : -.8f; // mostly round the base
            var at = new Vector3(Mathf.Cos(a) * radii.x * .95f, up * radii.y, Mathf.Sin(a) * radii.z * .95f);
            var dir = new Vector3(at.x, Mathf.Abs(at.y) * .3f + radii.y * .9f, at.z).normalized;
            float length = Mathf.Lerp(.18f, .45f, (float)random.NextDouble()) * Mathf.Clamp(radii.y, .4f, 1.4f);
            var rotation = Quaternion.FromToRotation(Vector3.up, dir);
            shards.Diamond(Matrix4x4.TRS(at + dir * length * .35f, rotation, new Vector3(length * .28f, length, length * .24f)));
        }
        Part("Shards", shards.Build("IHM Ice Shards"), _shardMaterial);

        _light = new GameObject("Cold Light").AddComponent<Light>();
        _light.transform.SetParent(_root, false);
        _light.type = LightType.Point;
        _light.color = new Color(.5f, .8f, 1f);
        _light.range = Mathf.Clamp(radii.magnitude * 2.4f, 1.5f, 6f);
        _light.shadows = LightShadows.None;
        _light.intensity = 0f;

        _mistMaterial ??= GlowTextures.Sprite(GlowTextures.Soft, "IHM Ice Mist", 3050);
        _sparkMaterial ??= GlowTextures.Sprite(GlowTextures.Star, "IHM Ice Sparkle", 3060);
        _mist = Particles("Mist", _mistMaterial, radii, Kind.Mist);
        _haze = Particles("Haze", _mistMaterial, radii, Kind.Haze);
        _sparks = Particles("Frost", _sparkMaterial, radii, Kind.Sparkle);
        _root.localScale = Vector3.one * .86f;
    }

    // ------------------------------------------------------------------ geometry

    private (Mesh ice, Mesh rime, Mesh edges) BuildShell(System.Random random, Vector3 radii, float halfHeight)
    {
        var (vertices, faces) = Icosphere(2); // 320 facets
        var points = new Vector3[vertices.Count];
        for (int i = 0; i < points.Length; i++)
        {
            // Push part-way toward a cube: a chunky block of ice rather than an egg.
            var d = vertices[i];
            var cube = d / Mathf.Max(Mathf.Abs(d.x), Mathf.Max(Mathf.Abs(d.y), Mathf.Abs(d.z))) * .82f;
            float jitter = Mathf.Lerp(.95f, 1.22f, (float)random.NextDouble());
            // Sideways nudge too, so the facets are irregular chips rather than a geodesic pattern.
            var nudge = new Vector3((float)random.NextDouble() - .5f, (float)random.NextDouble() - .5f, (float)random.NextDouble() - .5f) * .16f;
            var p = Vector3.Scale((Vector3.Lerp(d, cube, .45f) + nudge) * jitter, radii);
            p.y = Mathf.Max(p.y, -halfHeight - .02f);                                // sits on the floor
            p.y = Mathf.Min(p.y, Mathf.Max(radii.y * .96f, halfHeight + .12f));       // blunt, chipped top
            points[i] = p;
        }

        var iv = new List<Vector3>(); var iu = new List<Vector2>(); var it = new List<int>();
        var rv = new List<Vector3>(); var ru = new List<Vector2>(); var rt = new List<int>();
        float rimeLine = -halfHeight * .5f;
        foreach (var f in faces)
        {
            Vector3 a = points[f.a], b = points[f.b], c = points[f.c];
            var normal = Vector3.Cross(b - a, c - a).normalized;
            // Planar UVs per facet (dominant axis), with a random offset so the cracks never line up.
            var offset = new Vector2((float)random.NextDouble(), (float)random.NextDouble());
            Facet(iv, iu, it, a, b, c, normal, offset, 1.4f, Vector3.zero);
            if ((a.y + b.y + c.y) / 3f < rimeLine)
                Facet(rv, ru, rt, a, b, c, normal, offset, 2.6f, normal * .012f);
        }

        // Thin bright bars along every facet edge: the glints that make cut ice read as ice.
        var edges = new ProcMesh();
        var seen = new HashSet<long>();
        float thickness = Mathf.Clamp(radii.magnitude * .0035f, .0025f, .008f);
        foreach (var f in faces)
            foreach (var (x, y) in new[] { (f.a, f.b), (f.b, f.c), (f.c, f.a) })
            {
                long key = x < y ? ((long)x << 32) | (uint)y : ((long)y << 32) | (uint)x;
                if (!seen.Add(key)) continue;
                Vector3 p0 = points[x], p1 = points[y];
                var along = p1 - p0;
                if (along.sqrMagnitude < 1e-6f) continue; // collapsed by the floor/top clamp
                var mid = (p0 + p1) * .5f;
                var rotation = Quaternion.LookRotation(along, mid.sqrMagnitude > 1e-6f ? mid.normalized : Vector3.up);
                edges.Box(Matrix4x4.TRS(mid + mid.normalized * .004f, rotation, new Vector3(thickness, thickness, along.magnitude)));
            }

        return (MakeMesh("IHM Ice Shell", iv, iu, it), MakeMesh("IHM Ice Rime", rv, ru, rt), edges.Build("IHM Ice Edges"));
    }

    private static void Facet(List<Vector3> v, List<Vector2> u, List<int> t, Vector3 a, Vector3 b, Vector3 c,
        Vector3 normal, Vector2 offset, float tiling, Vector3 push)
    {
        var n = new Vector3(Mathf.Abs(normal.x), Mathf.Abs(normal.y), Mathf.Abs(normal.z));
        Vector2 UV(Vector3 p) => (n.x >= n.y && n.x >= n.z ? new Vector2(p.z, p.y) :
            n.y >= n.z ? new Vector2(p.x, p.z) : new Vector2(p.x, p.y)) * tiling + offset;
        int start = v.Count;
        v.Add(a + push); v.Add(b + push); v.Add(c + push);
        u.Add(UV(a)); u.Add(UV(b)); u.Add(UV(c));
        t.Add(start); t.Add(start + 1); t.Add(start + 2);
    }

    private static Mesh MakeMesh(string name, List<Vector3> v, List<Vector2> u, List<int> t)
    {
        var mesh = new Mesh { name = name };
        mesh.SetVertices(v);
        mesh.SetUVs(0, u);
        mesh.SetTriangles(t, 0);
        mesh.RecalculateNormals(); // unshared vertices: hard facets catch the light like cut ice
        mesh.RecalculateBounds();
        return mesh;
    }

    private static (List<Vector3>, List<(int a, int b, int c)>) Icosphere(int levels)
    {
        float g = (1f + Mathf.Sqrt(5f)) / 2f;
        var v = new List<Vector3>
        {
            new(-1, g, 0), new(1, g, 0), new(-1, -g, 0), new(1, -g, 0), new(0, -1, g), new(0, 1, g),
            new(0, -1, -g), new(0, 1, -g), new(g, 0, -1), new(g, 0, 1), new(-g, 0, -1), new(-g, 0, 1),
        };
        for (int i = 0; i < v.Count; i++) v[i] = v[i].normalized;
        var f = new List<(int, int, int)>
        {
            (0,11,5),(0,5,1),(0,1,7),(0,7,10),(0,10,11),(1,5,9),(5,11,4),(11,10,2),(10,7,6),(7,1,8),
            (3,9,4),(3,4,2),(3,2,6),(3,6,8),(3,8,9),(4,9,5),(2,4,11),(6,2,10),(8,6,7),(9,8,1),
        };
        for (int level = 0; level < levels; level++)
        {
            var cache = new Dictionary<long, int>();
            int Mid(int a, int b)
            {
                long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
                if (cache.TryGetValue(key, out int m)) return m;
                v.Add(((v[a] + v[b]) * .5f).normalized);
                return cache[key] = v.Count - 1;
            }
            var split = new List<(int, int, int)>();
            foreach (var (a, b, c) in f)
            {
                int ab = Mid(a, b), bc = Mid(b, c), ca = Mid(c, a);
                split.Add((a, ab, ca)); split.Add((b, bc, ab)); split.Add((c, ca, bc)); split.Add((ab, bc, ca));
            }
            f = split;
        }
        // Reverse winding for Unity's clockwise front faces.
        var faces = new List<(int a, int b, int c)>();
        foreach (var (a, b, c) in f) faces.Add((a, c, b));
        return (v, faces);
    }

    private void Part(string name, Mesh mesh, Material material)
    {
        var obj = new GameObject(name);
        obj.transform.SetParent(_root, false);
        obj.AddComponent<MeshFilter>().sharedMesh = mesh;
        var r = obj.AddComponent<MeshRenderer>();
        r.sharedMaterial = material;
        r.shadowCastingMode = ShadowCastingMode.Off;
        r.receiveShadows = false;
    }

    // ------------------------------------------------------------------ textures

    // The crack texture is tens of millions of distance checks: done all at once on the first freeze it stalled
    // the game for a moment. Prewarm builds the textures a few rows per frame from game start (and loads the ice
    // shader bundle), so the first shot only has to build the block itself.
    private const int CrackSize = 256, RimeSize = 128;
    private static Color[] _crackPixels, _rimePixels;
    private static int _crackRows, _rimeRows;
    private static Vector2[] _bold, _fine;

    internal static System.Collections.IEnumerator Prewarm()
    {
        var clock = System.Diagnostics.Stopwatch.StartNew();
        while (_crackTexture == null && _crackRows < CrackSize)
        {
            CrackRow();
            if (clock.ElapsedMilliseconds >= 2) { yield return null; clock.Restart(); }
        }
        while (_rimeTexture == null && _rimeRows < RimeSize)
        {
            RimeRow();
            if (clock.ElapsedMilliseconds >= 2) { yield return null; clock.Restart(); }
        }
        _crackTexture ??= CrackTexture();
        _rimeTexture ??= RimeTexture();
        yield return null;
        // The embedded shader bundle, loaded now rather than mid-fight, and its GPU programs compiled up front:
        // the first draw with a new shader otherwise compiles it on the spot.
        var ice = IceShader.Shader;
        yield return null;
        WarmUp(ice, PassType.Normal);
        yield return null;
        var standard = Shader.Find("Standard");
        WarmUp(standard, PassType.ForwardBase, "_ALPHAPREMULTIPLY_ON", "_EMISSION");
        WarmUp(standard, PassType.ForwardBase, "_ALPHAPREMULTIPLY_ON");
    }

    private static void WarmUp(Shader shader, PassType pass, params string[] keywords)
    {
        if (shader == null) return;
        try
        {
            var variants = new ShaderVariantCollection();
            variants.Add(new ShaderVariantCollection.ShaderVariant(shader, pass, keywords));
            variants.WarmUp();
        }
        catch (System.Exception error) { Plugin.Log.LogInfo($"[Ice] Shader warm-up skipped for {shader.name}: {error.Message}"); }
    }

    // Fractured, cloudy ice: alpha = cloudy frost + bold crack network + fine hairline cracks. Tiles seamlessly.
    private static void CrackRow()
    {
        if (_crackPixels == null)
        {
            var random = new System.Random(9137);
            _bold = Seeds(random, 11);
            _fine = Seeds(random, 34);
            _crackPixels = new Color[CrackSize * CrackSize];
        }
        int y = _crackRows++;
        for (int x = 0; x < CrackSize; x++)
        {
            var p = new Vector2((x + .5f) / CrackSize, (y + .5f) / CrackSize);
            float crack = Mathf.Exp(-Mathf.Pow(Edge(p, _bold) / .006f, 2f));
            float hair = Mathf.Exp(-Mathf.Pow(Edge(p, _fine) / .003f, 2f)) * .55f;
            float cloud = Fbm(p * 4f, 4);
            float frost = Mathf.SmoothStep(.35f, .8f, cloud) * .42f + .16f;
            float a = Mathf.Clamp01(frost + crack * .8f + hair * .6f);
            float bright = Mathf.Clamp01(.75f + crack * .25f);
            _crackPixels[y * CrackSize + x] = new Color(bright, bright, 1f, a);
        }
    }

    private static Texture2D CrackTexture()
    {
        while (_crackRows < CrackSize) CrackRow(); // finishes whatever Prewarm has not done yet
        var texture = MakeTexture("IHM Ice Cracks", CrackSize, _crackPixels, TextureWrapMode.Repeat);
        _crackPixels = null;
        return texture;
    }

    // Dense white frost crust: speckled crystals over a milky base.
    private static void RimeRow()
    {
        _rimePixels ??= new Color[RimeSize * RimeSize];
        int y = _rimeRows++;
        for (int x = 0; x < RimeSize; x++)
        {
            var p = new Vector2((x + .5f) / RimeSize, (y + .5f) / RimeSize);
            float grain = Fbm(p * 16f, 3);
            float speck = Mathf.SmoothStep(.5f, .7f, grain);
            float a = Mathf.Clamp01(.45f + speck * .5f + Fbm(p * 3f, 2) * .15f);
            _rimePixels[y * RimeSize + x] = new Color(1f, 1f, 1f, a);
        }
    }

    private static Texture2D RimeTexture()
    {
        while (_rimeRows < RimeSize) RimeRow();
        var texture = MakeTexture("IHM Ice Rime", RimeSize, _rimePixels, TextureWrapMode.Repeat);
        _rimePixels = null;
        return texture;
    }

    private static Vector2[] Seeds(System.Random random, int count)
    {
        var s = new Vector2[count];
        for (int i = 0; i < count; i++) s[i] = new Vector2((float)random.NextDouble(), (float)random.NextDouble());
        return s;
    }

    // Distance to the nearest Voronoi cell border (wrapping, so the texture tiles).
    private static float Edge(Vector2 p, Vector2[] seeds)
    {
        float d1 = float.MaxValue, d2 = float.MaxValue;
        foreach (var s in seeds)
            for (int ox = -1; ox <= 1; ox++)
            for (int oy = -1; oy <= 1; oy++)
            {
                float d = (p - (s + new Vector2(ox, oy))).magnitude;
                if (d < d1) { d2 = d1; d1 = d; } else if (d < d2) d2 = d;
            }
        return (d2 - d1) * .5f;
    }

    private static float Fbm(Vector2 p, int octaves)
    {
        float sum = 0f, amp = .5f, norm = 0f;
        for (int i = 0; i < octaves; i++)
        {
            sum += Mathf.PerlinNoise(p.x * (1 << i) + 17.3f * i, p.y * (1 << i) + 9.1f * i) * amp;
            norm += amp; amp *= .5f;
        }
        return sum / norm;
    }

    private static Texture2D MakeTexture(string name, int size, Color[] pixels, TextureWrapMode wrap)
    {
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, true)
        {
            name = name, wrapMode = wrap, filterMode = FilterMode.Trilinear, anisoLevel = 4, hideFlags = HideFlags.HideAndDontSave,
        };
        texture.SetPixels(pixels);
        texture.Apply(true, true);
        return texture;
    }

    // Same premultiplied-transparent Standard setup the mod's glass valuables use (it ships in the build).
    private static Material Glass(string name, Color color, Texture2D texture, Color emission, bool shared = false)
    {
        var m = new Material(Shader.Find("Standard")) { name = name, color = color };
        m.SetFloat("_Mode", 3);
        m.SetInt("_SrcBlend", (int)BlendMode.One);
        m.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
        m.SetInt("_ZWrite", 0);
        m.EnableKeyword("_ALPHAPREMULTIPLY_ON");
        m.SetFloat("_Glossiness", .96f);
        m.SetFloat("_Metallic", .1f);
        if (texture != null)
        {
            m.SetTexture("_MainTex", texture);
            m.SetTexture("_EmissionMap", texture); // the cracks glow faintly from within
        }
        m.EnableKeyword("_EMISSION");
        m.SetColor("_EmissionColor", emission);
        m.renderQueue = 3000;
        if (shared) m.hideFlags = HideFlags.HideAndDontSave;
        return m;
    }

    // ------------------------------------------------------------------ particles

    private enum Kind { Mist, Haze, Sparkle }

    private ParticleSystem Particles(string name, Material material, Vector3 radii, Kind kind)
    {
        var obj = new GameObject(name);
        obj.transform.SetParent(_root, false);
        var ps = obj.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.loop = true; main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        float r = Mathf.Max(radii.x, radii.z);
        var shape = ps.shape;
        var emission = ps.emission;
        switch (kind)
        {
            case Kind.Mist: // cold air pooling on the floor
                obj.transform.localPosition = Vector3.down * radii.y * .6f;
                main.startLifetime = new ParticleSystem.MinMaxCurve(1.8f, 3.2f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(.04f, .16f);
                main.startSize = new ParticleSystem.MinMaxCurve(r * .7f, r * 1.4f);
                main.startColor = new Color(.86f, .95f, 1f, .32f);
                main.maxParticles = 90;
                main.gravityModifier = .01f;
                shape.shapeType = ParticleSystemShapeType.Circle; shape.radius = r; shape.rotation = new Vector3(90f, 0f, 0f);
                emission.rateOverTime = 24f;
                break;
            case Kind.Haze: // frozen vapour hanging round the whole block
                main.startLifetime = new ParticleSystem.MinMaxCurve(2f, 3.5f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(.01f, .05f);
                main.startSize = new ParticleSystem.MinMaxCurve(r * .8f, r * 1.5f);
                main.startColor = new Color(.82f, .92f, 1f, .14f);
                main.maxParticles = 50;
                shape.shapeType = ParticleSystemShapeType.Sphere; shape.radius = r * 1.05f; shape.radiusThickness = .3f;
                emission.rateOverTime = 12f;
                break;
            default: // frost glints on the surface
                main.startLifetime = new ParticleSystem.MinMaxCurve(.5f, 1.1f);
                main.startSpeed = 0f;
                main.startSize = new ParticleSystem.MinMaxCurve(r * .05f, r * .12f);
                main.startColor = new Color(.9f, .97f, 1f, 1f);
                main.maxParticles = 30;
                shape.shapeType = ParticleSystemShapeType.Sphere; shape.radius = r; shape.radiusThickness = .1f;
                emission.rateOverTime = 7f;
                break;
        }
        var size = ps.sizeOverLifetime; size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, kind == Kind.Sparkle
            ? new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(.25f, 1f), new Keyframe(1f, 0f))
            : new AnimationCurve(new Keyframe(0f, .6f), new Keyframe(1f, 1.4f)));
        var colour = ps.colorOverLifetime; colour.enabled = true;
        var fade = new Gradient();
        fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, .25f), new GradientAlphaKey(0f, 1f) });
        colour.color = fade;
        var renderer = obj.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        ps.Play();
        return ps;
    }

    // ------------------------------------------------------------------ animation

    private void LateUpdate()
    {
        _fade = Mathf.MoveTowards(_fade, _target, Time.deltaTime / (_target > 0f ? .22f : .45f));
        if (_target <= 0f && _fade <= 0f) { Destroy(gameObject); return; }
        // Snap-freeze in with a slight overshoot; shrink a little while melting away.
        float grow = _target > 0f ? 1f + Mathf.Sin(_fade * Mathf.PI) * .05f : Mathf.Lerp(.92f, 1f, _fade);
        _root.localScale = Vector3.one * Mathf.Lerp(.86f, 1f, _fade) * grow;
        SetAlpha(_iceMaterial, _fade);
        SetAlpha(_rimeMaterial, _fade);
        if (_iceMaterial != null) _edgeMaterial.color = new Color(.85f, .95f, 1f, .13f * _fade);
        foreach (var m in _wrapMaterials) m.SetFloat("_Fade", _fade);
        _light.intensity = (.9f + Mathf.Sin(Time.time * 2.2f) * .12f) * _fade;
    }

    private static void SetAlpha(Material m, float a)
    {
        if (m == null) return;
        var c = m.color; c.a = a; m.color = c;
    }

    private void OnDestroy()
    {
        if (_iceMaterial != null) Destroy(_iceMaterial);
        if (_rimeMaterial != null) Destroy(_rimeMaterial);
        foreach (var twin in _wrapped) if (twin != null) Destroy(twin);
        foreach (var m in _wrapMaterials) if (m != null) Destroy(m);
    }
}
