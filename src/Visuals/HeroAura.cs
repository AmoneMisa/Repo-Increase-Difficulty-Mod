using UnityEngine;
using UnityEngine.Rendering;

namespace IncreaseHardMod.Visuals;

/// <summary>
/// Golden "hero" aura on the mod's own shop items (gear, drones, orbs, upgrades, the Flamethrower C.A.R.T.):
/// a soft halo, slowly turning light rays, twinkling sparkles and a warm point light around the item.
/// Added to each prefab before registration, so every spawned copy carries it. Local visuals only.
/// It hides while the item sits in an inventory slot and beyond 25 m.
/// </summary>
internal sealed class HeroAura : MonoBehaviour
{
    private static readonly Color Gold = new(1f, .84f, .45f);
    private static Mesh _quad;
    private static Material _glowMaterial, _rayMaterial, _sparkMaterial;

    private Transform _root, _glow, _rays;
    private Light _light;
    private ParticleSystem _sparks;
    private ItemEquippable _equippable;
    private float _radius = .3f;

    /// <summary>Called on each custom item prefab right before it is registered with REPOLib.</summary>
    internal static void Mark(GameObject prefab)
    {
        if (prefab != null && prefab.GetComponent<HeroAura>() == null) prefab.AddComponent<HeroAura>();
    }

    private void Start()
    {
        if (!ModConfig.HeroAuraEnabled.Value) { enabled = false; return; }
        _equippable = GetComponent<ItemEquippable>();
        _quad ??= ProcMesh.UnitQuad("IHM Aura Quad");
        _glowMaterial ??= GlowTextures.Sprite(GlowTextures.Soft, "IHM Aura Glow");
        _rayMaterial ??= GlowTextures.Sprite(GlowTextures.Rays, "IHM Aura Rays", 3101);
        _sparkMaterial ??= GlowTextures.Sprite(GlowTextures.Star, "IHM Aura Sparkle", 3102);
        _glowMaterial.color = new Color(Gold.r, Gold.g, Gold.b, .32f);
        _rayMaterial.color = new Color(1f, .93f, .7f, .2f);
        _sparkMaterial.color = Color.white;

        var center = transform.position;
        if (ItemBounds(out var bounds))
        {
            center = bounds.center;
            _radius = Mathf.Clamp(bounds.extents.magnitude, .15f, 1.1f);
        }
        _root = new GameObject("IHM Hero Aura", typeof(Decoration)).transform;
        _root.SetParent(transform, false);
        _root.position = center;
        _glow = Billboard("Glow", _glowMaterial, _radius * 2.6f);
        _rays = Billboard("Rays", _rayMaterial, _radius * 2.6f);

        _light = new GameObject("Light").AddComponent<Light>();
        _light.transform.SetParent(_root, false);
        _light.type = LightType.Point;
        _light.color = Gold;
        _light.intensity = .65f;
        _light.range = _radius * 2.5f + .8f;
        _light.shadows = LightShadows.None;

        _sparks = Sparkles();
    }

    // Bounds of the item's own visible renderers (before the aura exists).
    private bool ItemBounds(out Bounds bounds)
    {
        bounds = default;
        bool any = false;
        foreach (var r in GetComponentsInChildren<MeshRenderer>())
        {
            if (!r.enabled || r.forceRenderingOff) continue;
            if (!any) { bounds = r.bounds; any = true; }
            else bounds.Encapsulate(r.bounds);
        }
        return any && bounds.size.sqrMagnitude > .0001f;
    }

    private Transform Billboard(string name, Material material, float size)
    {
        var obj = new GameObject(name);
        obj.transform.SetParent(_root, false);
        obj.transform.localScale = Vector3.one * size / Mathf.Max(.0001f, transform.lossyScale.x);
        obj.AddComponent<MeshFilter>().sharedMesh = _quad;
        var r = obj.AddComponent<MeshRenderer>();
        r.sharedMaterial = material;
        r.shadowCastingMode = ShadowCastingMode.Off;
        r.receiveShadows = false;
        return obj.transform;
    }

    private ParticleSystem Sparkles()
    {
        var obj = new GameObject("Sparkles");
        obj.transform.SetParent(_root, false);
        var ps = obj.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.loop = true;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(.7f, 1.4f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(.02f, .12f);
        main.startSize = new ParticleSystem.MinMaxCurve(_radius * .12f, _radius * .26f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = new ParticleSystem.MinMaxGradient(Color.white, Gold);
        main.gravityModifier = -.02f;
        main.maxParticles = 40;
        var emission = ps.emission;
        emission.rateOverTime = 7f;
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = _radius * .9f;
        var size = ps.sizeOverLifetime;
        size.enabled = true; // twinkle: pop in, shrink out
        size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(.2f, 1f), new Keyframe(1f, 0f)));
        var spin = ps.rotationOverLifetime;
        spin.enabled = true;
        spin.z = new ParticleSystem.MinMaxCurve(-1.5f, 1.5f);
        var r = obj.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = _sparkMaterial;
        r.renderMode = ParticleSystemRenderMode.Billboard;
        r.shadowCastingMode = ShadowCastingMode.Off;
        r.receiveShadows = false;
        ps.Play();
        return ps;
    }

    // The game photographs the live item for its inventory icon: keep the aura out of that picture.
    [HarmonyLib.HarmonyPatch]
    private static class KeepOutOfIcons
    {
        private static System.Collections.Generic.IEnumerable<System.Reflection.MethodBase> TargetMethods()
        {
            foreach (string name in new[] { "CreateIconReadback", "CreateIconFromRenderTexture" })
            {
                var method = HarmonyLib.AccessTools.Method(typeof(SemiIconMaker), name);
                if (method != null) yield return method;
            }
        }

        private static void Prefix(SemiIconMaker __instance, out HeroAura __state)
        {
            __state = __instance != null ? __instance.GetComponentInParent<HeroAura>() : null;
            if (__state != null && __state._root != null) __state._root.gameObject.SetActive(false);
        }

        private static void Postfix(HeroAura __state)
        {
            if (__state != null && __state._root != null) __state._root.gameObject.SetActive(true);
        }
    }

    private void LateUpdate()
    {
        if (_root == null) return;
        var cam = Camera.main;
        bool stowed = _equippable != null && _equippable.IsEquipped();
        bool near = cam != null && (cam.transform.position - _root.position).sqrMagnitude < 25f * 25f;
        bool show = !stowed && near;
        if (_root.gameObject.activeSelf != show) _root.gameObject.SetActive(show);
        if (!show) return;
        var facing = Quaternion.LookRotation(_root.position - cam.transform.position, cam.transform.up);
        _glow.rotation = facing;
        _rays.rotation = facing * Quaternion.Euler(0f, 0f, Time.time * 14f);
        float pulse = .85f + Mathf.Sin(Time.time * 2.1f + GetInstanceID()) * .15f;
        _light.intensity = .65f * pulse;
    }
}
