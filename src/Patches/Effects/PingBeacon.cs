using IncreaseHardMod.Upgrades;
using UnityEngine;
using UnityEngine.Rendering;

namespace IncreaseHardMod.Patches.Effects;

/// <summary>
/// World-space beacon for area (Location) pings: a rotating rune circle on the floor, a light
/// pillar, a hovering winged-spear emblem inside a soft halo and a few rising motes, all additive blue.
/// </summary>
internal sealed class PingBeacon : MonoBehaviour
{
    private const float RuneRadius = .8f, PillarHeight = 3.2f, PillarWidth = .32f;
    private const float EmblemHeight = 1.15f, EmblemSize = .9f, HaloSize = 1.5f, IntroSeconds = .35f;
    private static readonly Color Tint = new(.62f, .86f, 1f, 1f);
    private static readonly int ColorId = Shader.PropertyToID("_Color"), TintId = Shader.PropertyToID("_TintColor");

    private static Mesh _quad;
    private static Material _rune, _pillar, _emblem, _halo;

    private Transform _runeOuter, _runeInner, _pillarT, _emblemT, _haloT;
    private Renderer[] _renderers;
    private MaterialPropertyBlock _block;
    private ParticleSystem _motes;
    private float _born, _until;

    internal static PingBeacon Create(Vector3 point, float duration)
    {
        if (!EnsureMaterials()) return null;
        var root = new GameObject("IHM_PingBeacon");
        root.transform.position = FloorUnder(point);
        var beacon = root.AddComponent<PingBeacon>();
        beacon._born = Time.time;
        beacon._until = Time.time + duration;
        beacon.Build();
        return beacon;
    }

    internal void Close() { if (this != null) Destroy(gameObject); }

    // Ground the circle under the ping even when the player aimed at a wall or a low ledge.
    private static Vector3 FloorUnder(Vector3 point)
    {
        var mask = LayerMask.GetMask("Default");
        if (Physics.Raycast(point + Vector3.up * .3f, Vector3.down, out var hit, 3f, mask == 0 ? Physics.DefaultRaycastLayers : mask,
                QueryTriggerInteraction.Ignore))
            return hit.point;
        return point;
    }

    private void Build()
    {
        _block = new MaterialPropertyBlock();
        _runeOuter = Part("Rune", _rune, new Vector3(0f, .025f, 0f), Quaternion.Euler(90f, 0f, 0f), Vector3.one * RuneRadius * 2f);
        _runeInner = Part("RuneInner", _rune, new Vector3(0f, .03f, 0f), Quaternion.Euler(90f, 0f, 0f), Vector3.one * RuneRadius * .9f);
        _pillarT = Part("Pillar", _pillar, new Vector3(0f, PillarHeight * .5f, 0f), Quaternion.identity, new Vector3(PillarWidth, PillarHeight, 1f));
        _haloT = Part("Halo", _halo, new Vector3(0f, EmblemHeight, 0f), Quaternion.identity, Vector3.one * HaloSize);
        _emblemT = Part("Emblem", _emblem, new Vector3(0f, EmblemHeight, 0f), Quaternion.identity, Vector3.one * EmblemSize);
        BuildMotes();
        _renderers = GetComponentsInChildren<Renderer>();
        Apply(0f);
    }

    private Transform Part(string name, Material material, Vector3 position, Quaternion rotation, Vector3 scale)
    {
        var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
        go.transform.SetParent(transform, false);
        go.transform.localPosition = position;
        go.transform.localRotation = rotation;
        go.transform.localScale = scale;
        go.GetComponent<MeshFilter>().sharedMesh = _quad;
        var renderer = go.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.lightProbeUsage = LightProbeUsage.Off;
        renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        return go.transform;
    }

    private void BuildMotes()
    {
        var go = new GameObject("Motes");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = new Vector3(0f, .05f, 0f);
        _motes = go.AddComponent<ParticleSystem>();
        _motes.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = _motes.main;
        main.loop = true; main.playOnAwake = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.1f, 1.9f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(.25f, .7f);
        main.startSize = new ParticleSystem.MinMaxCurve(.04f, .09f);
        main.startColor = Tint;
        main.maxParticles = 40;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        var shape = _motes.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = RuneRadius * .85f;
        shape.rotation = new Vector3(-90f, 0f, 0f); // emit upward from the floor circle
        var emission = _motes.emission; emission.rateOverTime = 14f;
        var fade = _motes.colorOverLifetime; fade.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, .2f), new GradientAlphaKey(0f, 1f) });
        fade.color = gradient;
        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = _halo;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        _motes.Play();
    }

    private void LateUpdate()
    {
        float now = Time.time;
        if (now >= _until) { Close(); return; }
        float age = now - _born;
        float intro = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(age / IntroSeconds));
        float alpha = intro * Mathf.Clamp01((_until - now) / .35f);

        _runeOuter.localRotation = Quaternion.Euler(90f, age * 18f, 0f);
        _runeInner.localRotation = Quaternion.Euler(90f, -age * 32f, 0f);
        _runeOuter.localScale = Vector3.one * RuneRadius * 2f * Mathf.Lerp(.6f, 1f, intro);
        _runeInner.localScale = Vector3.one * RuneRadius * .9f * Mathf.Lerp(.4f, 1f, intro);
        _pillarT.localScale = new Vector3(PillarWidth, PillarHeight * intro, 1f);
        _pillarT.localPosition = new Vector3(0f, PillarHeight * intro * .5f, 0f);

        float bob = Mathf.Sin(age * 2.2f) * .05f;
        _emblemT.localPosition = _haloT.localPosition = new Vector3(0f, EmblemHeight + bob, 0f);
        _emblemT.localScale = Vector3.one * EmblemSize * Mathf.Lerp(.5f, 1f, intro);
        _haloT.localScale = Vector3.one * HaloSize * (1f + Mathf.Sin(age * 3.1f) * .04f) * Mathf.Lerp(.3f, 1f, intro);

        var camera = PingController.ActiveCamera();
        if (camera != null)
        {
            var toCamera = camera.transform.position - transform.position;
            var flat = new Vector3(toCamera.x, 0f, toCamera.z);
            if (flat.sqrMagnitude > .0001f)
            {
                // Pillar and emblem stay upright and turn only around Y; the halo faces the camera fully.
                var yaw = Quaternion.LookRotation(-flat.normalized, Vector3.up);
                _pillarT.rotation = yaw;
                _emblemT.rotation = yaw;
            }
            _haloT.rotation = Quaternion.LookRotation(_haloT.position - camera.transform.position, Vector3.up);
        }
        Apply(alpha);
    }

    private void Apply(float alpha)
    {
        var tint = Tint; tint.a = alpha;
        var legacy = tint * .5f; legacy.a = alpha; // legacy additive shaders double _TintColor
        _block.SetColor(ColorId, tint);
        _block.SetColor(TintId, legacy);
        foreach (var renderer in _renderers)
            if (renderer != null && !(renderer is ParticleSystemRenderer)) renderer.SetPropertyBlock(_block);
    }

    private static bool EnsureMaterials()
    {
        if (_rune != null && _pillar != null && _emblem != null && _halo != null && _quad != null) return true;
        var shader = Shader.Find("Particles/Standard Unlit") ?? Shader.Find("Legacy Shaders/Particles/Additive") ??
                     Shader.Find("Sprites/Default");
        if (shader == null)
        {
            Plugin.Log.LogWarning("[Ping] No additive shader available; area ping beacon disabled.");
            return false;
        }
        _quad ??= BuildQuad();
        _rune = Additive(shader, "ping_rune.png", 3100);
        _pillar = Additive(shader, "ping_pillar.png", 3101);
        _halo = Additive(shader, "ping_halo.png", 3102);
        _emblem = Additive(shader, "ping_emblem.png", 3103);
        return _rune.mainTexture != null && _emblem.mainTexture != null;
    }

    private static Material Additive(Shader shader, string texture, int queue)
    {
        var material = new Material(shader) { name = "IHM Ping " + texture, renderQueue = queue, hideFlags = HideFlags.HideAndDontSave };
        material.mainTexture = UpgradeIcons.LoadTexture(texture);
        if (material.mainTexture != null) material.mainTexture.wrapMode = TextureWrapMode.Clamp;
        if (material.HasProperty("_SrcBlend")) material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
        if (material.HasProperty("_DstBlend")) material.SetInt("_DstBlend", (int)BlendMode.One);
        if (material.HasProperty("_ZWrite")) material.SetInt("_ZWrite", 0);
        if (material.HasProperty("_Cull")) material.SetInt("_Cull", (int)CullMode.Off);
        material.EnableKeyword("_ALPHABLEND_ON");
        return material;
    }

    private static Mesh BuildQuad()
    {
        var mesh = new Mesh { name = "IHM Ping Quad", hideFlags = HideFlags.HideAndDontSave };
        mesh.vertices = new[] { new Vector3(-.5f, -.5f), new Vector3(.5f, -.5f), new Vector3(.5f, .5f), new Vector3(-.5f, .5f) };
        mesh.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f) };
        mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }
}
