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
///  - Soul C.A.R.T.: the little Pocket C.A.R.T. with a Hauler hatch for up to $65K in monster souls.
///  - Wide C.A.R.T.: 1.25x the big C.A.R.T.'s width by default (ModConfig.WideCartWidth) and at least 1.4x its
///    length to fit the Museum Tray, with slightly higher walls; 20% dearer.
///  - Double-Decker C.A.R.T.: the big C.A.R.T. 15% shorter, with walls twice as tall and 40% more mass; 25% dearer.
/// Reshaping moves and scales the cart's own parts (walls, floor, the in-cart volume the haul screen reads), so
/// the game's logic sees a bigger or taller cart.
/// </summary>
internal static class CustomCarts
{
    internal const string SoulAsset = "Item Cart Soul", WideAsset = "Item Cart Wide", DoubleAsset = "Item Cart Double";
    internal static bool IsModCart(string assetName) => assetName == SoulAsset || assetName == WideAsset || assetName == DoubleAsset;

    private const float MinWideArea = 1.5f; // retain the original floor space for narrower configurations
    internal const float MinWideLength = 1.4f; // about 2.27 m between the end walls for the 2.18 m Museum Tray
    private const float ManorWidthLimit = 1.05f; // ~1.30 m of collision width in the manor's 1.50 m doorway
    private static float WideWidth => Mathf.Clamp(ModConfig.WideCartWidth.Value, 1f, 1.5f);
    private static float WideLength => Mathf.Max(MinWideLength, MinWideArea / WideWidth);
    private const float WideHeight = 1.15f;
    private const float DoubleLength = .85f, DoubleHeight = 2f, DoubleMassFactor = 1.4f;
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

            int widthPercent = Mathf.RoundToInt((WideWidth - 1f) * 100f);
            int lengthPercent = Mathf.RoundToInt((WideLength - 1f) * 100f);
            Make(big, big, WideAsset, "Wide C.A.R.T.", CartKind.Wide, 1.2f, new Color(.3f, .85f, .8f),
                Localization.GameLanguage.Russian
                    ? $"На {widthPercent}% шире и на {lengthPercent}% длиннее большой тележки. При стандартной ширине сервиз помещается вдоль кузова."
                    : $"{widthPercent}% wider and {lengthPercent}% longer than the big C.A.R.T. The Museum Tray fits lengthwise at the default width.");
            Make(big, big, DoubleAsset, "Double-Decker C.A.R.T.", CartKind.Double, 1.25f, new Color(.55f, .9f, .35f),
                Localization.GameLanguage.Russian
                    ? "На 15% короче и на 40% тяжелее большой тележки. Стенки вдвое выше."
                    : "15% shorter and 40% heavier than the big C.A.R.T., with walls twice as tall.");
            var hauler = FlameCartItem.FindHaulerBox(items);
            if (hauler != null)
            {
                string cap = $"${ModConfig.SoulCartValueCap.Value / 1000f:0.#}K";
                Make(small ?? big, big, SoulAsset, "Soul C.A.R.T.", CartKind.Soul, 1.33f, new Color(.65f, .4f, 1f),
                    Localization.GameLanguage.Russian
                        ? $"Опустите душу в люк. Корзина безопасно хранит души на сумму до {cap}."
                        : $"Drop souls into the hatch to store them safely. Holds up to {cap} in monster souls.", hauler);
            }
            else Plugin.Log.LogWarning("[Carts] Hauler storage box not found; Soul C.A.R.T. disabled.");
        }
        catch (Exception e)
        {
            Plugin.Log.LogError($"[Carts] Failed to build the extra carts: {e}");
        }
    }

    private static void Make(Item donor, Item priceRef, string asset, string display, CartKind kind, float priceFactor, Color color,
        string description, ItemVehicle hauler = null)
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
        if (kind == CartKind.Wide)
        {
            Reshape(go, new Vector3(WideWidth, WideHeight, WideLength));
            RaiseWalls(go, WideHeight);
        }
        if (kind == CartKind.Double)
        {
            Reshape(go, new Vector3(1f, DoubleHeight, DoubleLength));
            RaiseWalls(go, DoubleHeight);
            mass = StandardMass * DoubleMassFactor;
            var rb = go.GetComponent<Rigidbody>();
            if (rb != null) rb.mass = mass;
            var grab = go.GetComponent<PhysGrabObject>();
            if (grab != null) grab.massOriginal = mass;
        }
        Tint(go, color);
        var cart = go.AddComponent<ModCart>();
        cart.kind = kind;
        cart.mass = mass;
        if (kind == CartKind.Wide) cart.builtWidth = WideWidth;
        if (kind == CartKind.Double) go.AddComponent<DoubleDeckerCargo>();
        if (kind == CartKind.Soul) SoulCartHatch.Attach(go, hauler);

        InventoryItemVisuals.Prepare(go);
        if (Items.RegisterItem(attributes) == null)
            Plugin.Log.LogWarning($"[Carts] REPOLib refused to register {asset}.");
        else
            Plugin.Log.LogInfo($"[Carts] Registered {asset} (base {donor.name}, price x{priceFactor:0.##}{(mass > 0f ? $", mass {mass:0.#}" : "")}).");
    }

    // ------------------------------------------------------------------ shape

    // The bottom of the cart's walls: vertical stretching grows the walls upward from here.
    private const float WallBottom = -.255f;

    internal static void FitManor(GameObject cart, float builtWidth)
    {
        if (builtWidth > ManorWidthLimit && RunManager.instance?.levelCurrent?.name == "Level - Manor")
            Reshape(cart, new Vector3(ManorWidthLimit / builtWidth, 1f, 1f));
    }

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
            var factors = LocalFactors(t.localRotation, axes);
            if (n.Contains("Capsule") && t.TryGetComponent<CapsuleCollider>(out var skid))
            {
                // Non-uniform scale also changes a capsule's vertical radius. Extend the skid
                // along its axis only, keeping the donor's ground clearance and rounded ends.
                int axis = skid.direction;
                var center = skid.center;
                center[axis] *= factors[axis];
                skid.center = center;
                skid.height = Mathf.Max(2f * skid.radius,
                    (skid.height - 2f * skid.radius) * factors[axis] + 2f * skid.radius);
                continue;
            }
            t.localScale = Vector3.Scale(t.localScale, factors);
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
    /// The cart body's own walls made <paramref name="factor"/> times as tall, in the cart's own textures.
    /// Every vertex of the body mesh in the upper half of the walls (rim, top edges, wall decals) is lifted by the extra
    /// height, so the wall faces stretch and the floor, chassis and handle stay as they are. No added plates or floor.
    /// If the game's mesh can't be read, the body is stretched upward from the wall bottom instead.
    /// </summary>
    private static void RaiseWalls(GameObject go, float factor)
    {
        var body = go.transform.Find("Cart Mesh");
        if (body == null) { Plugin.Log.LogWarning($"[Carts] {go.name}: cart body not found; walls not raised."); return; }
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
        Plugin.Log.LogInfo($"[Carts] {go.name} walls x{factor:0.##}: {raised} mesh(es) raised" +
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

/// <summary>Prefab settings that the native cart applies when it spawns.</summary>
internal sealed class ModCart : MonoBehaviour
{
    public CartKind kind;
    public float mass;
    public float builtWidth = 1f;

    private void Awake()
    {
        if (kind == CartKind.Wide) CustomCarts.FitManor(gameObject, builtWidth);
    }
}
