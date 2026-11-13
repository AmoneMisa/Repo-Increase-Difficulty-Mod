using System;
using System.Collections.Generic;
using IncreaseHardMod.Drones;
using IncreaseHardMod.Visuals;
using HarmonyLib;
using REPOLib.Modules;
using UnityEngine;
using Object = UnityEngine.Object;

namespace IncreaseHardMod.Gear;

internal enum CartKind : byte { Soul = 1, Wide = 2, Double = 3, Armored = 4 }

/// <summary>
/// Three extra C.A.R.T.s for the shop, each a copy of a vanilla cart (so pushing, the haul screen, cart physics
/// and extraction all stay the game's own):
///  - Soul C.A.R.T.: the little Pocket C.A.R.T. with a Hauler hatch for up to $65K in monster souls.
///  - Wide C.A.R.T.: 1.25x the big C.A.R.T.'s width by default (ModConfig.WideCartWidth) and at least 1.4x its
///    length to fit the Museum Tray, with slightly higher walls and 10% more mass; 20% dearer.
///  - Double-Decker C.A.R.T.: the big C.A.R.T. 15% shorter, with walls twice as tall (the upper half clear glass)
///    and 20% more mass; 25% dearer.
/// Reshaping moves and scales the cart's own parts (walls, floor, the in-cart volume the haul screen reads), so
/// the game's logic sees a bigger or taller cart.
/// </summary>
internal static class CustomCarts
{
    internal const string SoulAsset = "Item Cart Soul", WideAsset = "Item Cart Wide", DoubleAsset = "Item Cart Double", ArmoredAsset = "Item Cart Armored";
    internal static bool IsModCart(string assetName) => assetName == SoulAsset || assetName == WideAsset || assetName == DoubleAsset || assetName == ArmoredAsset;

    /// <summary>Armored C.A.R.T.: loot inside loses this share of the value it would lose; the fairy cannot lift from it.</summary>
    internal const float ArmoredLossFactor = .6f, ArmoredMassFactor = 1.3f;

    internal static bool InArmoredCart(PhysGrabObjectImpactDetector detector) =>
        detector != null && detector.inCart && detector.currentCart != null &&
        detector.currentCart.GetComponent<ModCart>() is { kind: CartKind.Armored };

    private const float MinWideArea = 1.5f; // retain the original floor space for narrower configurations
    internal const float MinWideLength = 1.4f; // about 2.27 m between the end walls for the 2.18 m Museum Tray
    private const float ManorWidthLimit = 1.05f; // ~1.30 m of collision width in the manor's 1.50 m doorway
    private static float WideWidth => Mathf.Clamp(ModConfig.WideCartWidth.Value, 1f, 1.5f);
    private static float WideLength => Mathf.Max(MinWideLength, MinWideArea / WideWidth);
    private const float WideHeight = 1.15f;
    private const float DoubleLength = .85f, DoubleHeight = 2f, DoubleMassFactor = 1.2f;
    private const float WideMassFactor = 1.1f;
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
                    ? $"На {widthPercent}% шире и на {lengthPercent}% длиннее большой тележки. На 10% тяжелее. При стандартной ширине сервиз помещается вдоль кузова."
                    : $"{widthPercent}% wider and {lengthPercent}% longer than the big C.A.R.T. and 10% heavier. The Museum Tray fits lengthwise at the default width.");
            Make(big, big, DoubleAsset, "Double-Decker C.A.R.T.", CartKind.Double, 1.25f, new Color(.55f, .9f, .35f),
                Localization.GameLanguage.Russian
                    ? "На 15% короче и на 20% тяжелее большой тележки. Стеклянные стенки вдвое выше."
                    : "15% shorter and 20% heavier than the big C.A.R.T., with glass walls twice as tall.");
            Make(big, big, ArmoredAsset, "Armored C.A.R.T.", CartKind.Armored, 1.4f, new Color(.55f, .58f, .62f),
                Localization.GameLanguage.Russian
                    ? "Бронированная большая тележка: лут внутри теряет на 40% меньше ценности при ударах, а Фея не может его утащить. На 30% тяжелее."
                    : "An armored big C.A.R.T.: loot inside loses 40% less value from knocks, and the Mischief Fairy cannot lift it out. 30% heavier.");
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
            mass = StandardMass * WideMassFactor;
        }
        if (kind == CartKind.Double)
        {
            Reshape(go, new Vector3(1f, DoubleHeight, DoubleLength));
            // The body keeps the normal walls; the extra height is clear glass on top (the wall colliders are tall).
            AddGlassWalls(go);
            mass = StandardMass * DoubleMassFactor;
        }
        if (kind == CartKind.Armored)
            mass = StandardMass * ArmoredMassFactor;
        if (mass > 0f)
        {
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
        // The handle plates (hand icon, green/red switch, arrows) are separate from the body: move them with it.
        LiftHandleDisplay(go, body, y => stretched > 0 ? (y - WallBottom) * (factor - 1f) : y > split ? lift : 0f);
    }

    /// <summary>
    /// The handle's hand icon and green/red state plates (PhysGrabCart.grabMesh, under "Cart Handle") are separate
    /// renderers from the body. Taller walls lift the handle bump they sit on, so they move up by the same amount;
    /// otherwise they end up buried inside the body.
    /// </summary>
    private static void LiftHandleDisplay(GameObject go, Transform body, Func<float, float> liftAt)
    {
        var physCart = go.GetComponent<PhysGrabCart>();
        if (physCart == null || physCart.grabMesh == null) return;
        var cart = go.transform;
        // Move each top-most transform once, so a plate under an already moved parent isn't moved twice.
        var roots = new List<Transform>();
        foreach (var r in physCart.grabMesh)
            if (r != null && !r.transform.IsChildOf(body) && !roots.Contains(r.transform)) roots.Add(r.transform);
        roots.RemoveAll(t => roots.Exists(o => o != t && t.IsChildOf(o)));
        foreach (var t in roots)
        {
            var mf = t.GetComponent<MeshFilter>();
            var centre = mf != null && mf.sharedMesh != null ? t.TransformPoint(mf.sharedMesh.bounds.center) : t.position;
            float delta = liftAt(cart.InverseTransformPoint(centre).y);
            if (delta <= 0f) continue;
            t.position += cart.TransformVector(Vector3.up * delta);
            Plugin.Log.LogInfo($"[Carts] {go.name}: handle display '{t.name}' lifted {delta:0.000} with the body.");
        }
    }

    /// <summary>
    /// Clear glass panels from the normal rim up to the top of the (already raised) wall colliders, one per wall,
    /// with a thin dark frame along their top edge. Visual only: the wall colliders keep the cargo in.
    /// </summary>
    private static void AddGlassWalls(GameObject go)
    {
        var cart = go.transform;
        // Clearly see-through, yet tinted, glossy and faintly lit so the panes read as glass even in dark levels.
        var glass = Challenges.ChallengeProps.Glass(new Color(.7f, .88f, 1f, .3f), new Color(.05f, .08f, .1f));
        glass.name = "IHM Cart Glass";
        var frame = new Material(Shader.Find("Standard")) { name = "IHM Cart Glass Frame", color = new Color(.2f, .18f, .15f) };
        frame.SetFloat("_Glossiness", .45f);
        frame.SetFloat("_Metallic", .6f);
        var root = new GameObject("IHM Glass Walls").transform;
        root.SetParent(cart, false);
        int panels = 0;
        var walls = new List<Transform>();
        foreach (Transform t in cart) if (t.name.Contains("Cart Wall Collider") && t.localPosition.y <= .3f) walls.Add(t);
        foreach (var t in walls)
        {
            // The wall's shape lives on its collider (the "Semi Box Collider" child), not on a mesh.
            if (!WallBounds(cart, t, out var min, out var max)) continue;
            float height = max.y - WallTop;
            if (height < .05f) continue;
            var size = max - min;
            if (Mathf.Max(size.x, size.z) < .5f) continue; // the small bump under the screen, not a wall
            // A thin pane in the middle of the wall's thickness.
            bool alongX = size.x >= size.z;
            var pane = new Vector3(alongX ? size.x : .02f, height, alongX ? .02f : size.z);
            var centre = new Vector3((min.x + max.x) / 2f, WallTop + height / 2f, (min.z + max.z) / 2f);
            Block(root, "Glass", centre, pane, glass);
            Block(root, "Glass Frame", new Vector3(centre.x, max.y - .012f, centre.z),
                new Vector3(pane.x + (alongX ? .01f : .03f), .024f, pane.z + (alongX ? .03f : .01f)), frame);
            panels++;
        }
        Plugin.Log.LogInfo($"[Carts] {go.name}: {panels} glass wall panel(s) above the rim.");
    }

    // Cart-space bounds of a wall from its box/mesh colliders (or a mesh, if it has one).
    private static bool WallBounds(Transform cart, Transform wall, out Vector3 min, out Vector3 max)
    {
        Vector3 lo = Vector3.positiveInfinity, hi = Vector3.negativeInfinity;
        bool any = false;
        void Add(Transform t, Bounds b)
        {
            for (int i = 0; i < 8; i++)
            {
                var corner = b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                var p = cart.InverseTransformPoint(t.TransformPoint(corner));
                lo = Vector3.Min(lo, p); hi = Vector3.Max(hi, p);
            }
            any = true;
        }
        foreach (var c in wall.GetComponentsInChildren<Collider>(true))
        {
            if (c is BoxCollider box) Add(c.transform, new Bounds(box.center, box.size));
            else if (c is MeshCollider mc && mc.sharedMesh != null) Add(c.transform, mc.sharedMesh.bounds);
        }
        if (!any)
            foreach (var f in wall.GetComponentsInChildren<MeshFilter>(true))
                if (f.sharedMesh != null) Add(f.transform, f.sharedMesh.bounds);
        min = lo; max = hi;
        return any;
    }

    private static void Block(Transform parent, string name, Vector3 centre, Vector3 size, Material material)
    {
        var block = GameObject.CreatePrimitive(PrimitiveType.Cube);
        block.name = name;
        Object.DestroyImmediate(block.GetComponent<Collider>());
        block.transform.SetParent(parent, false);
        block.transform.localPosition = centre;
        block.transform.localScale = size;
        var r = block.GetComponent<MeshRenderer>();
        r.sharedMaterial = material;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
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

    [HarmonyPatch(typeof(PhysGrabObjectImpactDetector), nameof(PhysGrabObjectImpactDetector.Break))]
    private static class ArmoredCargo
    {
        [HarmonyPrefix]
        private static void Prefix(PhysGrabObjectImpactDetector __instance, ref float valueLost)
        {
            if (valueLost > 0f && __instance.isValuable && InArmoredCart(__instance)) valueLost *= ArmoredLossFactor;
        }
    }

    // PhysGrabCart.Start puts every cart back to the standard mass; the Wide and Double-Decker keep their own.
    [HarmonyPatch(typeof(PhysGrabCart), nameof(PhysGrabCart.Start))]
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
