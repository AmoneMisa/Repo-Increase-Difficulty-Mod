using System;
using System.Collections.Generic;
using IncreaseHardMod.Drones;
using IncreaseHardMod.Visuals;
using HarmonyLib;
using REPOLib.Modules;
using UnityEngine;
using Object = UnityEngine.Object;

namespace IncreaseHardMod.Gear;

internal enum CartKind : byte { Soul = 1, Wide = 2, Double = 3 }

/// <summary>
/// Three extra C.A.R.T.s for the shop, each a copy of a vanilla cart (so pushing, the haul screen, cart physics
/// and extraction all stay the game's own):
///  - Soul C.A.R.T.: the little Pocket C.A.R.T., 33% dearer than the big C.A.R.T. It carries monster souls only, up
///    to $55K of them: anything else, or a soul that would go over the limit, is pushed back out (host).
///  - Wide C.A.R.T.: 1.5x the big C.A.R.T.'s floor space; 1.25x wider by default (ModConfig.WideCartWidth, so it
///    still fits through doorways) and longer by the rest (1.2x); 20% dearer.
///  - Double-Decker C.A.R.T.: the big C.A.R.T. with walls twice as tall, in the cart's own textures. It weighs as
///    much as two C.A.R.T.s; 25% dearer.
/// Reshaping moves and scales the cart's own parts (walls, floor, the in-cart volume the haul screen reads), so
/// the game's logic sees a bigger or taller cart.
/// </summary>
internal static class CustomCarts
{
    internal const string SoulAsset = "Item Cart Soul", WideAsset = "Item Cart Wide", DoubleAsset = "Item Cart Double";
    internal static bool IsModCart(string assetName) => assetName == SoulAsset || assetName == WideAsset || assetName == DoubleAsset;

    private const float WideArea = 1.5f; // floor space of the Wide C.A.R.T. relative to the big C.A.R.T.
    private static float WideWidth => Mathf.Clamp(ModConfig.WideCartWidth.Value, 1f, 1.5f);
    private const float DoubleHeight = 2f;
    private const float StandardMass = 8f; // PhysGrabCart.Start sets every cart to this

    internal static void Build()
    {
        if (!ModConfig.CustomCartsEnabled.Value) return;
        try
        {
            var items = StatsManager.instance.itemDictionary;
            if (!items.TryGetValue("Item Cart Medium", out var big) || big == null || big.prefab == null || big.prefab.Prefab == null)
            {
                Plugin.Log.LogWarning("[Carts] Vanilla C.A.R.T. not found; extra carts disabled.");
                return;
            }
            items.TryGetValue("Item Cart Small", out var small);
            if (small == null || small.prefab == null || small.prefab.Prefab == null) small = null;

            Make(small ?? big, big, SoulAsset, "Soul C.A.R.T.", CartKind.Soul, 1.33f, new Color(.65f, .4f, 1f),
                $"A little C.A.R.T. built for monster souls: it carries souls only, up to ${ModConfig.SoulCartValueCap.Value / 1000f:0.#}K of them. " +
                "Anything else, or a soul over the limit, is pushed back out.");
            Make(big, big, WideAsset, "Wide C.A.R.T.", CartKind.Wide, 1.2f, new Color(.3f, .85f, .8f),
                $"A bigger C.A.R.T.: 50% more room than the big C.A.R.T. ({Mathf.RoundToInt((WideWidth - 1f) * 100f)}% wider and {Mathf.RoundToInt((WideArea / WideWidth - 1f) * 100f)}% longer), still narrow enough for doorways.");
            Make(big, big, DoubleAsset, "Double-Decker C.A.R.T.", CartKind.Double, 1.25f, new Color(.55f, .9f, .35f),
                "A C.A.R.T. with walls twice as tall: stack loot twice as high. Heavy: it weighs as much as two C.A.R.T.s.");
        }
        catch (Exception e)
        {
            Plugin.Log.LogError($"[Carts] Failed to build the extra carts: {e}");
        }
    }

    private static void Make(Item donor, Item priceRef, string asset, string display, CartKind kind, float priceFactor, Color color, string description)
    {
        var item = Object.Instantiate(donor);
        item.name = asset;
        item.itemName = display;
        item.itemNameLocalized = null;
        item.description = description;
        item.maxAmountInShop = 1;
        item.maxPurchase = false;
        item.minPlayerCount = 1;
        item.value = Object.Instantiate(priceRef.value);
        item.value.valueMin = priceRef.value.valueMin * priceFactor;
        item.value.valueMax = priceRef.value.valueMax * priceFactor;

        var go = Object.Instantiate(donor.prefab.Prefab, DroneItems.EnsureHolder().transform);
        go.name = asset;
        var attributes = go.GetComponent<ItemAttributes>();
        attributes.item = item;

        float mass = 0f;
        // 1.5x (the first version) no longer fit through doorways; the width is now a setting, 1.25x by default.
        // Built on every PC from its own config: the host's value drives the physics, so players should keep it equal.
        // Longer by as much as it is narrower than the first 1.5x version, so the floor keeps 1.5x the space.
        if (kind == CartKind.Wide) Reshape(go, new Vector3(WideWidth, 1f, WideArea / WideWidth));
        if (kind == CartKind.Double)
        {
            Reshape(go, new Vector3(1f, DoubleHeight, 1f));
            RaiseWalls(go, DoubleHeight);
            mass = StandardMass * 2f;
            var rb = go.GetComponent<Rigidbody>();
            if (rb != null) rb.mass = mass;
            var grab = go.GetComponent<PhysGrabObject>();
            if (grab != null) grab.massOriginal = mass;
        }
        Tint(go, color);
        var cart = go.AddComponent<ModCart>();
        cart.kind = kind;
        cart.mass = mass;

        InventoryItemVisuals.Prepare(go);
        if (Items.RegisterItem(attributes) == null)
            Plugin.Log.LogWarning($"[Carts] REPOLib refused to register {asset}.");
        else
            Plugin.Log.LogInfo($"[Carts] Registered {asset} (base {donor.name}, price x{priceFactor:0.##}{(mass > 0f ? $", mass {mass:0}" : "")}).");
    }

    // ------------------------------------------------------------------ shape

    // The bottom of the cart's walls: vertical stretching grows the walls upward from here.
    private const float WallBottom = -.255f;

    /// <summary>
    /// Scales the cart by <paramref name="scale"/> (cart space) by moving and resizing its direct children. Parts that
    /// must stay where the player reaches or where the cart meets the ground (handle, grab area, screen, floor, hover
    /// capsules, body mesh) keep their height and only follow the length and width.
    /// </summary>
    private static void Reshape(GameObject go, Vector3 scale)
    {
        var children = new List<Transform>();
        foreach (Transform t in go.transform) children.Add(t);
        foreach (var t in children)
        {
            string n = t.name;
            var p = t.localPosition;
            bool keepHeight = n.Contains("Capsule") || n.Contains("Thrust Effect") || n == "Screen" || n.Contains("Grab Area") ||
                              n.Contains("Cart Handle") || n.Contains("Cart Grab Point") || n.Contains("Cart Mesh") || n.Contains("Inside") ||
                              n.Contains("Phys Object Collider") || n.Contains("Item Volume") || n.Contains("SemiIcon") ||
                              n.Contains("Hurt Collider") || (n.Contains("Cart Wall Collider") && p.y > .3f); // the slanted handle guard
            var axes = keepHeight ? new Vector3(scale.x, 1f, scale.z) : scale;
            p.x *= scale.x;
            p.z *= scale.z;
            if (!keepHeight) p.y = WallBottom + (p.y - WallBottom) * scale.y;
            t.localPosition = p;
            // The screen, shop volume and icon camera only move: stretching them would skew text and pads.
            if (n == "Screen" || n.Contains("Item Volume") || n.Contains("SemiIcon")) continue;
            t.localScale = Vector3.Scale(t.localScale, LocalFactors(t.localRotation, axes));
        }
    }

    // How much each of a child's own axes stretches when the parent space is scaled by `scale`.
    private static Vector3 LocalFactors(Quaternion rotation, Vector3 scale)
    {
        float Along(Vector3 axis)
        {
            var w = rotation * axis;
            return new Vector3(w.x * scale.x, w.y * scale.y, w.z * scale.z).magnitude;
        }
        return new Vector3(Along(Vector3.right), Along(Vector3.up), Along(Vector3.forward));
    }

    // The top of the vanilla cart's walls (the rim), in cart space.
    private const float WallTop = .255f;

    /// <summary>
    /// Double-Decker: the cart body's own walls made <paramref name="factor"/> times as tall, in the cart's own textures.
    /// Every vertex of the body mesh in the upper half of the walls (rim, top edges, wall decals) is lifted by the extra
    /// height, so the wall faces stretch and the floor, chassis and handle stay as they are. No added plates or floor.
    /// If the game's mesh can't be read, the body is stretched upward from the wall bottom instead.
    /// </summary>
    private static void RaiseWalls(GameObject go, float factor)
    {
        var body = go.transform.Find("Cart Mesh");
        if (body == null) { Plugin.Log.LogWarning("[Carts] Double-Decker: cart body not found; walls not raised."); return; }
        float lift = (WallTop - WallBottom) * (factor - 1f);
        float split = (WallBottom + WallTop) / 2f; // vertices above the middle of the wall move up with the rim
        var cart = go.transform;
        var filters = body.GetComponentsInChildren<MeshFilter>(true);
        int raised = 0, stretched = 0;
        // One path for the whole body, so a part is never both raised and stretched.
        foreach (var filter in filters) if (filter.sharedMesh != null && !filter.sharedMesh.isReadable) stretched++;
        if (stretched == 0)
        foreach (var filter in filters)
        {
            var source = filter.sharedMesh;
            if (source == null) continue;
            var mesh = Object.Instantiate(source);
            mesh.name = source.name + " (IHM tall walls)";
            var vertices = mesh.vertices;
            var t = filter.transform;
            for (int i = 0; i < vertices.Length; i++)
            {
                var p = cart.InverseTransformPoint(t.TransformPoint(vertices[i]));
                if (p.y <= split) continue;
                p.y += lift;
                vertices[i] = t.InverseTransformPoint(cart.TransformPoint(p));
            }
            mesh.vertices = vertices;
            mesh.RecalculateBounds();
            filter.sharedMesh = mesh;
            raised++;
        }
        if (stretched > 0)
        {
            // Fallback: stretch the whole body upward from the wall bottom (its parts below the floor barely move).
            var p = body.localPosition;
            p.y = WallBottom + (p.y - WallBottom) * factor;
            body.localPosition = p;
            body.localScale = Vector3.Scale(body.localScale, LocalFactors(body.localRotation, new Vector3(1f, factor, 1f)));
        }
        Plugin.Log.LogInfo($"[Carts] Double-Decker walls x{factor:0.#}: {raised} mesh(es) raised" +
                           (stretched > 0 ? $", {stretched} unreadable: body stretched instead" : "") + ".");
    }

    private static void Tint(GameObject go, Color color)
    {
        var mesh = go.transform.Find("Cart Mesh");
        if (mesh == null) return;
        foreach (var r in mesh.GetComponentsInChildren<MeshRenderer>(true))
            foreach (var m in r.materials)
            {
                if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", Color.Lerp(m.GetColor("_BaseColor"), color, .35f));
                else if (m.HasProperty("_Color")) m.SetColor("_Color", Color.Lerp(m.color, color, .35f));
            }
    }

    // PhysGrabCart.Start puts every cart back to the standard mass; the Double-Decker keeps its own.
    [HarmonyPatch(typeof(PhysGrabCart), "Start")]
    private static class KeepMass
    {
        [HarmonyPostfix]
        private static void Postfix(PhysGrabCart __instance)
        {
            var cart = __instance.GetComponent<ModCart>();
            if (cart == null || cart.mass <= 0f || __instance.rb == null) return;
            __instance.rb.mass = cart.mass;
            var grab = __instance.GetComponent<PhysGrabObject>();
            if (grab != null) grab.massOriginal = cart.mass;
        }
    }
}

/// <summary>Which extra cart this is; the Soul C.A.R.T. also enforces its souls-only, value-capped load (host).</summary>
internal sealed class ModCart : MonoBehaviour
{
    public CartKind kind;
    public float mass;

    private PhysGrabObject _self;
    private Rigidbody _rb;
    private Transform _inCart;
    private float _nextCheck;
    private int _grabLayer = -1;
    private readonly Dictionary<PhysGrabObject, float> _firstSeen = new();
    private readonly Dictionary<PhysGrabObject, float> _ejected = new();
    private readonly List<PhysGrabObject> _found = new();
    private readonly List<PhysGrabObject> _stale = new();

    private void Awake()
    {
        _self = GetComponent<PhysGrabObject>();
        _rb = GetComponent<Rigidbody>();
        _inCart = transform.Find("In Cart");
    }

    private void Update()
    {
        if (kind != CartKind.Soul || _inCart == null || !SemiFunc.IsMasterClientOrSingleplayer()) return;
        if (Time.time < _nextCheck) return;
        _nextCheck = Time.time + .2f;
        if (_grabLayer < 0) _grabLayer = LayerMask.NameToLayer("PhysGrabObject");
        FilterLoad();
    }

    private void FilterLoad()
    {
        _found.Clear();
        foreach (var c in Physics.OverlapBox(_inCart.position, _inCart.localScale / 2f, _inCart.rotation))
        {
            if (c == null || c.gameObject.layer != _grabLayer) continue;
            var p = c.GetComponentInParent<PhysGrabObject>();
            if (p == null || p == _self || _found.Contains(p)) continue;
            if (p.GetComponentInParent<Enemy>() != null) continue; // monsters climb in on their own
            _found.Add(p);
        }
        _stale.Clear();
        foreach (var p in _firstSeen.Keys) if (p == null || !_found.Contains(p)) _stale.Add(p);
        foreach (var p in _stale) _firstSeen.Remove(p);
        foreach (var p in _found) if (!_firstSeen.ContainsKey(p)) _firstSeen[p] = Time.time;

        // Souls that were in first keep their place; a newer soul that would go over the limit is the one turned away.
        _found.Sort((a, b) => _firstSeen[a].CompareTo(_firstSeen[b]));
        float cap = Mathf.Max(0, ModConfig.SoulCartValueCap.Value), total = 0f;
        foreach (var p in _found)
        {
            if (p.grabbed) continue; // still in someone's hands
            var value = p.GetComponent<ValuableObject>();
            bool soul = value != null && p.GetComponent<EnemyValuable>() != null;
            if (soul && total + value.dollarValueCurrent <= cap) { total += value.dollarValueCurrent; continue; }
            Eject(p);
        }
    }

    private void Eject(PhysGrabObject p)
    {
        var rb = p.rb;
        if (rb == null || rb.isKinematic) return;
        if (_ejected.TryGetValue(p, out float at) && Time.time - at < .6f) return;
        _ejected[p] = Time.time;
        var away = p.transform.position - transform.position;
        away.y = 0f;
        if (away.sqrMagnitude < .0025f) away = transform.right;
        var carried = _rb != null ? _rb.velocity : Vector3.zero;
        rb.velocity = carried + away.normalized * 2.4f + Vector3.up * 3.4f;
    }
}
