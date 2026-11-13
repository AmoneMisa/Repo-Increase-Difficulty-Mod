using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;

namespace IncreaseHardMod.Visuals;

/// <summary>
/// Loads the mod's own "IHM/Glue" shader from the embedded bundle (src/Art/ihm_glue.bundle, built by
/// tools/IceShaderBundle: BuildGlueBundle). If it is missing or unsupported, the glue falls back to a glossy
/// Standard material, so a glue hit never breaks.
/// </summary>
internal static class GlueShader
{
    private static bool _tried;
    private static Shader _shader;

    internal static Shader Shader
    {
        get
        {
            if (_tried) return _shader;
            _tried = true;
            try
            {
                var asm = Assembly.GetExecutingAssembly();
                string resource = Array.Find(asm.GetManifestResourceNames(), n => n.EndsWith(".ihm_glue.bundle", StringComparison.Ordinal));
                if (resource == null) { Plugin.Log.LogWarning("[Glue] Embedded glue shader bundle missing; using the fallback glue."); return null; }
                byte[] bytes;
                using (var stream = asm.GetManifestResourceStream(resource))
                using (var memory = new MemoryStream()) { stream.CopyTo(memory); bytes = memory.ToArray(); }
                var bundle = AssetBundle.LoadFromMemory(bytes);
                if (bundle == null) { Plugin.Log.LogWarning("[Glue] Glue shader bundle did not load; using the fallback glue."); return null; }
                var shader = bundle.LoadAsset<Shader>("Assets/Shaders/IHMGlue.shader") ?? bundle.LoadAsset<Shader>("IHMGlue");
                bundle.Unload(false);
                if (shader == null || !shader.isSupported)
                {
                    Plugin.Log.LogWarning($"[Glue] Glue shader {(shader == null ? "not found in bundle" : "not supported by this GPU")}; using the fallback glue.");
                    return null;
                }
                Plugin.Log.LogInfo("[Glue] Loaded the IHM/Glue shader.");
                return _shader = shader;
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"[Glue] Glue shader failed to load ({e.Message}); using the fallback glue.");
                return null;
            }
        }
    }
}

/// <summary>
/// The look of a Sticky Pistol / Glue Grenade hit (cosmetic only; <see cref="Gear.StickyRoot"/> owns the hold):
/// <list type="bullet">
/// <item>a lumpy pink gel puddle with a rolled lip and splashed droplets under the target;</item>
/// <item>sagging gel strands from the puddle up to the target's legs, thinning as they stretch;</item>
/// <item>on monsters, a gel coat over the lower body (twins of its own renderers, like the ice), with drips;</item>
/// <item>a burst of droplets when it lands; it spreads out and melts away when the hold ends.</item>
/// </list>
/// Seeded from the target's network id, so every client sees the same puddle.
/// </summary>
internal sealed class GlueBlob : MonoBehaviour
{
    private const float GrowSeconds = .22f, MeltSeconds = .55f;
    private static readonly Color Pink = new(.95f, .36f, .68f);

    private Transform _host;
    private Bounds _localBody;      // body bounds in the host's space when the glue landed
    private bool _monster;
    private GameObject _puddle;
    private Material _puddleMaterial, _coatMaterial;
    private readonly List<GameObject> _owned = new();
    private readonly List<(Mesh Mesh, Vector3[] Buffer, Vector3 Floor, Vector3 Body, float Radius, float Sag)> _strands = new();
    private float _began, _meltAt = -1f;

    internal static GlueBlob Create(Transform host, Bounds body, int seed, IReadOnlyList<Renderer> renderers, bool monster)
    {
        var go = new GameObject("IHM Glue", typeof(Decoration));
        var blob = go.AddComponent<GlueBlob>();
        blob.Build(host, body, seed, renderers, monster);
        return blob;
    }

    /// <summary>The hold ended: spread out, snap the strands and melt away.</summary>
    internal void Release()
    {
        if (_meltAt < 0f) _meltAt = Time.time;
    }

    /// <summary>Hit again while still stuck: back to full strength.</summary>
    internal void Refresh() { _meltAt = -1f; }

    private void Build(Transform host, Bounds body, int seed, IReadOnlyList<Renderer> renderers, bool monster)
    {
        _host = host; _monster = monster; _began = Time.time;
        _localBody = new Bounds(host.InverseTransformPoint(body.center), body.size);
        var random = new System.Random(seed);
        float Range(float a, float b) => a + (float)random.NextDouble() * (b - a);

        float footprint = Mathf.Max(body.extents.x, body.extents.z);
        float radius = monster ? Mathf.Clamp(footprint * 1.35f, .45f, 1.7f) : .55f;
        var floor = Floor(body.center, body.min.y);
        transform.position = floor;

        var shader = GlueShader.Shader;
        _puddleMaterial = shader != null ? new Material(shader) { name = "IHM Glue puddle" } : Fallback();
        if (shader != null)
        {
            // The puddle's own shape carries the volume: only broad, slow lumps and a faint wobble on top.
            _puddleMaterial.SetFloat("_Scale", 3.5f);
            _puddleMaterial.SetFloat("_Inflate", .004f);
            _puddleMaterial.SetFloat("_Wobble", .003f);
        }
        _puddle = new GameObject("IHM Glue puddle") { layer = host.gameObject.layer };
        _puddle.transform.SetParent(transform, false);
        _puddle.transform.localRotation = Quaternion.Euler(0f, Range(0f, 360f), 0f);
        _puddle.transform.localPosition = Vector3.up * .006f;
        _puddle.AddComponent<MeshFilter>().sharedMesh = GlueMesh.Puddle(seed, radius);
        Quiet(_puddle.AddComponent<MeshRenderer>(), _puddleMaterial);
        _owned.Add(_puddle);

        // Strands: from the puddle up to the legs (local to the host, so they follow it while it struggles).
        int count = monster ? 5 : 3;
        for (int i = 0; i < count; i++)
        {
            float angle = (i + Range(-.3f, .3f)) * Mathf.PI * 2f / count;
            var dir = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            var foot = floor + dir * radius * Range(.45f, .8f);
            var bodyPoint = monster
                ? new Vector3(body.center.x + dir.x * body.extents.x * .7f, body.min.y + body.size.y * Range(.12f, .32f), body.center.z + dir.z * body.extents.z * .7f)
                : host.position + dir * .14f + Vector3.up * Range(.2f, .55f);
            var mesh = GlueMesh.NewStrand();
            var strand = new GameObject("IHM Glue strand") { layer = host.gameObject.layer };
            strand.transform.SetParent(null, false); // world space: vertices are written in world coordinates
            strand.AddComponent<MeshFilter>().sharedMesh = mesh;
            Quiet(strand.AddComponent<MeshRenderer>(), _puddleMaterial);
            _owned.Add(strand);
            _strands.Add((mesh, new Vector3[(GlueMesh.StrandSegments + 1) * GlueMesh.StrandSides], foot,
                host.InverseTransformPoint(bodyPoint), (monster ? .03f : .02f) * Range(.7f, 1.2f), Range(.10f, .22f)));
        }

        if (monster && shader != null && renderers != null) Coat(renderers, body, shader);
        Splash(floor + Vector3.up * .3f, radius);
        Update();
    }

    private static Vector3 Floor(Vector3 centre, float bottom)
    {
        var start = new Vector3(centre.x, Mathf.Max(centre.y, bottom + .3f), centre.z);
        return Physics.Raycast(start, Vector3.down, out var hit, 6f, SemiFunc.LayerMaskGetVisionObstruct(), QueryTriggerInteraction.Ignore)
            ? hit.point : new Vector3(centre.x, bottom, centre.z);
    }

    private void Coat(IReadOnlyList<Renderer> renderers, Bounds body, Shader shader)
    {
        float size = Mathf.Clamp(body.extents.magnitude, .3f, 3f);
        _coatMaterial = new Material(shader) { name = "IHM Glue coat", renderQueue = 3016 };
        _coatMaterial.SetFloat("_Coat", 1f);
        _coatMaterial.SetFloat("_Base", body.min.y);
        _coatMaterial.SetFloat("_Height", body.size.y);
        _coatMaterial.SetFloat("_Coverage", .34f);
        _coatMaterial.SetFloat("_Inflate", Mathf.Clamp(size * .02f, .012f, .045f));
        _coatMaterial.SetFloat("_Scale", Mathf.Clamp(9f / size, 4f, 16f));
        _coatMaterial.SetFloat("_Fade", 0f);
        foreach (var source in renderers)
        {
            if (source == null || !source.enabled || !source.gameObject.activeInHierarchy) continue;
            if (source is SkinnedMeshRenderer skinned && skinned.sharedMesh != null)
            {
                var twin = Twin(source);
                var copy = twin.AddComponent<SkinnedMeshRenderer>();
                copy.sharedMesh = skinned.sharedMesh; copy.bones = skinned.bones; copy.rootBone = skinned.rootBone;
                var b = skinned.localBounds; b.Expand(.4f); copy.localBounds = b;
                Quiet(copy, _coatMaterial, skinned.sharedMesh.subMeshCount);
            }
            else if (source is MeshRenderer && source.TryGetComponent<MeshFilter>(out var filter) && filter.sharedMesh != null)
            {
                var twin = Twin(source);
                twin.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
                Quiet(twin.AddComponent<MeshRenderer>(), _coatMaterial, filter.sharedMesh.subMeshCount);
            }
        }
    }

    private GameObject Twin(Renderer source)
    {
        var twin = new GameObject("IHM Glue Twin", typeof(Decoration)) { layer = source.gameObject.layer };
        twin.transform.SetParent(source.transform, false);
        _owned.Add(twin);
        return twin;
    }

    private static void Quiet(Renderer renderer, Material material, int slots = 1)
    {
        var materials = new Material[Mathf.Max(1, slots)];
        for (int i = 0; i < materials.Length; i++) materials[i] = material;
        renderer.sharedMaterials = materials;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.lightProbeUsage = LightProbeUsage.BlendProbes;
    }

    private static Material Fallback()
    {
        var m = new Material(Shader.Find("Standard")) { name = "IHM Glue fallback", color = Pink };
        m.SetFloat("_Glossiness", .92f);
        m.SetFloat("_Metallic", .05f);
        return m;
    }

    private void Splash(Vector3 at, float radius)
    {
        var go = new GameObject("IHM Glue splash");
        go.transform.position = at;
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.loop = false; main.playOnAwake = false; main.duration = .1f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(.35f, .7f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(1.2f, 2.6f + radius);
        main.startSize = new ParticleSystem.MinMaxCurve(.04f, .11f);
        main.startColor = new Color(Pink.r, Pink.g, Pink.b, .95f);
        main.gravityModifier = 1.6f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 60;
        var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Hemisphere; shape.radius = .15f;
        var emission = ps.emission; emission.rateOverTime = 0f; emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 36) });
        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = GlowTextures.Sprite(GlowTextures.Soft, "IHM Glue droplets", 3000);
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        ps.Play();
        Destroy(go, 1.5f);
    }

    private void Update()
    {
        float grow = Mathf.Clamp01((Time.time - _began) / GrowSeconds);
        grow = 1f - (1f - grow) * (1f - grow);
        float melt = _meltAt < 0f ? 0f : Mathf.Clamp01((Time.time - _meltAt) / MeltSeconds);
        if (melt >= 1f || (_host == null && _meltAt < 0f)) { if (_host == null) Release(); if (melt >= 1f) { Destroy(gameObject); return; } }

        // The puddle splats out fast, then spreads a little and thins away as it melts.
        float spread = Mathf.Lerp(.25f, 1f, grow) * (1f + .18f * melt);
        _puddle.transform.localScale = new Vector3(spread, Mathf.Lerp(.4f, 1f, grow) * (1f - melt * .8f), spread);
        float fade = grow * (1f - melt);
        if (_puddleMaterial.HasProperty("_Fade")) _puddleMaterial.SetFloat("_Fade", fade);
        if (_coatMaterial != null) _coatMaterial.SetFloat("_Fade", fade);

        if (_host == null) return;
        foreach (var s in _strands)
        {
            var top = _host.TransformPoint(s.Body);
            // Strands stretch up from the puddle as it lands and snap (thin to nothing) when it melts.
            var end = Vector3.Lerp(s.Floor, top, grow);
            float radius = s.Radius * grow * (1f - Mathf.Clamp01(melt * 2.2f));
            GlueMesh.Strand(s.Mesh, s.Buffer, s.Floor, end, Mathf.Max(radius, .0005f), s.Sag * grow);
        }
    }

    private void OnDestroy()
    {
        if (_puddle != null && _puddle.TryGetComponent<MeshFilter>(out var filter)) Destroy(filter.sharedMesh);
        foreach (var go in _owned) if (go != null) Destroy(go);
        foreach (var s in _strands) if (s.Mesh != null) Destroy(s.Mesh);
        if (_puddleMaterial != null) Destroy(_puddleMaterial);
        if (_coatMaterial != null) Destroy(_coatMaterial);
    }
}
