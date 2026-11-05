using System.Collections.Generic;
using IncreaseHardMod.Visuals;
using UnityEngine;
using UnityEngine.Rendering;

namespace IncreaseHardMod.Patches.Effects;

/// <summary>
/// Neon "underglow" under a pinged object, in the ping's colour: a real point light just under the item
/// that washes the floor and the item's underside, plus a soft glow pool on the floor and a brighter core,
/// sized to the item's footprint. It follows the item, pulses gently, and fades out with the ping.
/// Local visuals only (each client draws the pings it receives).
/// </summary>
internal sealed class PingUnderglow : MonoBehaviour
{
    private static Mesh _quad;
    private static readonly RaycastHit[] Hits = new RaycastHit[12];

    private PhysGrabObject _target;
    private Object _owner;
    private Color _colour;
    private float _until, _fade;
    private Transform _root, _pool, _core;
    private Material _poolMaterial, _coreMaterial;
    private Light _light;
    private readonly List<Renderer> _renderers = new();

    /// <summary>Start (or refresh) the underglow on a pinged object for the life of its ping.</summary>
    internal static void Attach(PhysGrabObject target, Color colour, float duration, Object owner)
    {
        if (target == null || !ModConfig.PingUnderglowEnabled.Value) return;
        var glow = target.GetComponent<PingUnderglow>() ?? target.gameObject.AddComponent<PingUnderglow>();
        glow.Init(target, colour, duration, owner);
    }

    private void Init(PhysGrabObject target, Color colour, float duration, Object owner)
    {
        _target = target;
        _owner = owner;
        _colour = colour;
        _until = Time.time + duration;
        _renderers.Clear();
        foreach (var r in target.GetComponentsInChildren<Renderer>())
            if (r is MeshRenderer or SkinnedMeshRenderer && !Decoration.Is(r)) _renderers.Add(r);
        if (_root != null) { Recolour(); return; }

        _quad ??= ProcMesh.UnitQuad("IHM Underglow Quad");
        _root = new GameObject("IHM Ping Underglow").transform; // world-space: never inherits the item's tilt
        _poolMaterial = GlowTextures.Sprite(GlowTextures.Soft, "IHM Underglow Pool", 3090);
        _coreMaterial = GlowTextures.Sprite(GlowTextures.Strip, "IHM Underglow Strip", 3091);
        _pool = Quad("Pool", _poolMaterial);
        _core = Quad("Core", _coreMaterial);
        _light = new GameObject("Light").AddComponent<Light>();
        _light.transform.SetParent(_root, false);
        _light.type = LightType.Point;
        _light.shadows = LightShadows.None;
        _light.renderMode = LightRenderMode.ForcePixel; // keep the floor wash smooth
        Recolour();
    }

    private Transform Quad(string name, Material material)
    {
        var obj = new GameObject(name);
        obj.transform.SetParent(_root, false);
        obj.AddComponent<MeshFilter>().sharedMesh = _quad;
        var r = obj.AddComponent<MeshRenderer>();
        r.sharedMaterial = material;
        r.shadowCastingMode = ShadowCastingMode.Off;
        r.receiveShadows = false;
        return obj.transform;
    }

    private void Recolour()
    {
        _light.color = _colour;
        // The core is a whiter, hotter version of the same colour, like a neon strip.
        var hot = Color.Lerp(_colour, Color.white, .15f);
        _poolMaterial.color = new Color(_colour.r, _colour.g, _colour.b, 1f);
        _coreMaterial.color = new Color(hot.r, hot.g, hot.b, 1f);
    }

    private bool ItemBounds(out Bounds bounds)
    {
        bounds = default;
        bool any = false;
        foreach (var r in _renderers)
        {
            if (r == null || !r.enabled || r.forceRenderingOff || !r.gameObject.activeInHierarchy) continue;
            if (!any) { bounds = r.bounds; any = true; }
            else bounds.Encapsulate(r.bounds);
        }
        return any;
    }

    // Nearest surface under the item that is not part of the item itself.
    private bool Floor(Vector3 from, float reach, out RaycastHit floor)
    {
        floor = default;
        float best = float.MaxValue;
        int count = Physics.RaycastNonAlloc(from, Vector3.down, Hits, reach, SemiFunc.LayerMaskGetVisionObstruct(), QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            var hit = Hits[i];
            if (hit.collider == null || hit.collider.transform.IsChildOf(transform) || hit.distance >= best) continue;
            best = hit.distance;
            floor = hit;
        }
        return best < float.MaxValue;
    }

    private void LateUpdate()
    {
        if (_root == null) return;
        bool alive = _target != null && Time.time < _until && _owner != null;
        _fade = Mathf.MoveTowards(_fade, alive ? 1f : 0f, Time.deltaTime / (alive ? .25f : .35f));
        if (!alive && _fade <= 0f) { Destroy(this); return; }

        var equippable = _target != null ? _target.GetComponent<ItemEquippable>() : null;
        bool stowed = equippable != null && equippable.IsEquipped();
        if (_target == null || stowed || !ItemBounds(out var bounds))
        {
            _root.gameObject.SetActive(false);
            return;
        }
        _root.gameObject.SetActive(true);

        float footprint = Mathf.Max(bounds.size.x, bounds.size.z);
        float pulse = .88f + Mathf.Sin(Time.time * 3.2f) * .12f;
        bool grounded = Floor(bounds.center, bounds.extents.y + 1.6f, out var floor);
        var ground = grounded ? floor.point : new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
        var normal = grounded ? floor.normal : Vector3.up;
        _root.position = ground + normal * .015f;
        // Quad faces -Z: point -Z up out of the floor, with its X/Y along world X/Z to match the bounds.
        var along = Vector3.ProjectOnPlane(Vector3.forward, normal);
        _root.rotation = Quaternion.LookRotation(-normal, along.sqrMagnitude > .001f ? along.normalized : Vector3.forward);

        // Strip traces the item's footprint; the pool spills further out around it.
        float sx = Mathf.Clamp(bounds.size.x, .06f, 3.5f), sz = Mathf.Clamp(bounds.size.z, .06f, 3.5f);
        _core.localScale = new Vector3(sx * 1.15f + .14f, sz * 1.15f + .14f, 1f);
        _pool.localScale = new Vector3(sx * 2.2f + .5f, sz * 2.2f + .5f, 1f);
        // Brightest right under the item, fading off as it rises.
        float height = Mathf.Max(0f, bounds.min.y - ground.y);
        float near = Mathf.Clamp01(1f - height / 1.2f);
        _pool.gameObject.SetActive(grounded);
        _core.gameObject.SetActive(grounded);
        _poolMaterial.color = new Color(_colour.r, _colour.g, _colour.b, .6f * _fade * pulse * near);
        var hot = Color.Lerp(_colour, Color.white, .15f);
        _coreMaterial.color = new Color(hot.r, hot.g, hot.b, 1f * _fade * pulse * near);

        // A light tucked just under the item: real colour on the floor and the item's underside.
        _light.transform.position = new Vector3(bounds.center.x, Mathf.Lerp(ground.y, bounds.min.y, .5f) + .06f, bounds.center.z);
        _light.range = Mathf.Clamp(footprint * 1.8f + .9f, 1.2f, 5f);
        // Small items (guns, potions) get a gentler wash than carts, so the glow never drowns the item.
        _light.intensity = Mathf.Lerp(1.4f, 3.2f, Mathf.Clamp01(footprint / 1.5f)) * _fade * pulse;
    }

    private void OnDestroy()
    {
        if (_root != null) Destroy(_root.gameObject);
        if (_poolMaterial != null) Destroy(_poolMaterial);
        if (_coreMaterial != null) Destroy(_coreMaterial);
    }
}
