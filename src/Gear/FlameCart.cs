using System;
using System.Collections.Generic;
using HarmonyLib;
using IncreaseHardMod.Drones;
using IncreaseHardMod.Visuals;
using Photon.Pun;
using REPOLib.Modules;
using UnityEngine;
using Object = UnityEngine.Object;

namespace IncreaseHardMod.Gear;

/// <summary>
/// Flamethrower C.A.R.T.: a standalone wheeled flamethrower with a small cargo tray.
///
/// Physics/steering: a clone of the vanilla medium C.A.R.T., so grabbing the handle drives it exactly
/// like a cart (Strong/Weak modes, haul screen, extraction). Its vanilla body is hidden; the visible
/// chassis is drawn over the cart's own collision boxes (so the tray you see is the tray that holds
/// loot) plus wheels and the Blender turret from tools/build_flamethrower_art.py.
///
/// Battery: carts have none, so a native ItemBattery (+ the battery widget borrowed from the C.A.R.T.
/// Cannon prefab) is added. Charging stations, saved charge, the held-item battery UI and the floating
/// bars therefore all work natively. Fuel is spent per flame tick on the host.
///
/// Fire: while grabbing it, press Interact (E) to toggle the flame. The host validates the request,
/// deals damage each tick to monsters inside a cone in front of the nozzle (aimed where the operator
/// looks, within the turret's traverse), drains the battery and broadcasts the on/off state; every
/// client draws the flame locally. Players are never damaged.
///
/// Cargo cap: loot worth more than the configured cap overloads the tray; overloaded, it cannot enter
/// Strong mode (it still drags in Weak mode), so unload to drive it properly again.
/// </summary>
internal static class FlameCartItem
{
    public const string AssetName = "Item Cart Flamethrower";

    internal static void Build()
    {
        if (!ModConfig.FlameCartEnabled.Value) return;
        try
        {
            var items = StatsManager.instance.itemDictionary;
            if (!items.TryGetValue("Item Cart Medium", out var baseItem) || baseItem?.prefab?.Prefab == null ||
                baseItem.prefab.Prefab.GetComponent<PhysGrabCart>() == null)
            {
                Plugin.Log.LogWarning("[Flame] Vanilla medium C.A.R.T. not found; Flamethrower C.A.R.T. disabled.");
                return;
            }
            var batteryDonor = FindBatteryDonor(items);
            if (batteryDonor == null)
            {
                Plugin.Log.LogWarning("[Flame] No vanilla battery widget found; Flamethrower C.A.R.T. disabled.");
                return;
            }

            var item = Object.Instantiate(baseItem);
            item.name = AssetName;
            item.itemName = "Flamethrower C.A.R.T.";
            item.itemNameLocalized = null;
            item.description = $"A wheeled flamethrower with a small cargo tray (up to ${ModConfig.FlameCartValueCap.Value / 1000f:0.#}K of loot). " +
                "Grab the handle to drive it like a cart; press E to toggle a flame that burns monsters in front of you.";
            item.maxAmount = 2;
            item.maxAmountInShop = 1;
            item.maxPurchase = false;
            item.minPlayerCount = 1;
            item.value = Object.Instantiate(baseItem.value);
            // Shop price in K = value * itemValueMultiplier(4) / 1000, so value = K * 250. Set directly in shop
            // dollars (no custom-item discount): the configured range is what players see.
            int minK = Mathf.Max(1, ModConfig.FlameCartPriceMinK.Value);
            int maxK = Mathf.Max(minK, ModConfig.FlameCartPriceMaxK.Value);
            item.value.valueMin = minK * 250f;
            item.value.valueMax = maxK * 250f;
            var preset = ScriptableObject.CreateInstance<ColorPresets>();
            preset.colorMain = new Color(1f, .45f, .1f);
            preset.colorLight = new Color(1f, .75f, .35f);
            preset.colorDark = new Color(.55f, .2f, .05f);
            item.colorPreset = preset;

            var go = Object.Instantiate(baseItem.prefab.Prefab, DroneItems.EnsureHolder().transform);
            go.name = AssetName;
            var attributes = go.GetComponent<ItemAttributes>();
            attributes.item = item;

            var cart = go.GetComponent<PhysGrabCart>();
            var layout = FlameCartChassis.Build(go, cart);
            AddBattery(go, batteryDonor, layout.Top + .55f);
            var flame = go.AddComponent<FlameCart>();
            flame.turret = layout.Turret;
            flame.nozzleLocal = layout.NozzleLocal;
            flame.turretBaseYaw = layout.TurretYaw;

            if (Items.RegisterItem(attributes) == null)
                Plugin.Log.LogWarning($"[Flame] REPOLib refused to register {AssetName}.");
            else
                Plugin.Log.LogInfo($"[Flame] Registered {AssetName}.");
        }
        catch (Exception e)
        {
            Plugin.Log.LogError($"[Flame] Failed to build the Flamethrower C.A.R.T.: {e}");
        }
    }

    private static ItemBattery FindBatteryDonor(Dictionary<string, Item> items)
    {
        foreach (string name in new[] { "Item Cart Cannon", "Item Cart Laser" })
            if (items.TryGetValue(name, out var item) && Usable(item?.prefab?.Prefab, out var battery)) return battery;
        var names = new List<string>(items.Keys);
        names.Sort(StringComparer.Ordinal);
        foreach (var name in names)
            if (Usable(items[name]?.prefab?.Prefab, out var battery)) return battery;
        return null;

        static bool Usable(GameObject prefab, out ItemBattery battery)
        {
            battery = prefab != null ? prefab.GetComponent<ItemBattery>() : null;
            return battery != null && battery.batteryTransform != null &&
                   battery.batteryTransform.GetComponentInChildren<BatteryVisualLogic>(true) != null;
        }
    }

    private static void AddBattery(GameObject go, ItemBattery donor, float height)
    {
        var widget = Object.Instantiate(donor.batteryTransform.gameObject, go.transform, false);
        widget.name = donor.batteryTransform.name;
        widget.transform.localPosition = Vector3.up * height;
        widget.transform.localRotation = Quaternion.identity;
        var battery = go.AddComponent<ItemBattery>();
        battery.batteryBars = Mathf.Max(4, donor.batteryBars);
        battery.hasBatteryVisuals = true;
        battery.batteryTransform = widget.transform;
        battery.upOffset = height;
        battery.autoDrain = false;
        battery.batteryDrainRate = 0f;
        battery.isUnchargable = false;
        battery.onlyShowWhenItemToggleIsOn = false;
    }
}

/// <summary>Visible body: hides the vanilla cart shell and paints a chassis over its collision boxes.</summary>
internal static class FlameCartChassis
{
    internal struct Layout
    {
        public Transform Turret;
        public Vector3 NozzleLocal;
        public float Top;
        public float TurretYaw;
    }

    private static readonly Color Sand = new(.74f, .63f, .43f), SandDark = new(.52f, .44f, .30f),
        Rubber = new(.06f, .065f, .065f), Steel = new(.30f, .30f, .28f), Hazard = new(.78f, .52f, .12f);
    private static Mesh _cube, _cylinder;

    internal static Layout Build(GameObject go, PhysGrabCart cart)
    {
        var root = go.transform;
        var keep = new HashSet<Renderer>();
        if (cart.grabMesh != null) foreach (var r in cart.grabMesh) if (r != null) keep.Add(r);
        if (cart.valueScreen != null) foreach (var r in cart.valueScreen.GetComponentsInChildren<Renderer>(true)) keep.Add(r);
        if (cart.handlePoint != null) foreach (var r in cart.handlePoint.GetComponentsInChildren<Renderer>(true)) keep.Add(r);

        var body = root.Find("Cart Mesh") ?? FindByName(root, "Cart Mesh");
        var template = ItemArt.WeaponMaterialDonor(body);
        if (template == null) throw new InvalidOperationException("No albedo-capable material for the chassis");
        int layer = body != null ? body.gameObject.layer : go.layer;

        // Body bounds in cart space, from the vanilla shell before it is hidden.
        var bounds = LocalBounds(root, body != null ? body.GetComponentsInChildren<MeshFilter>(true) : root.GetComponentsInChildren<MeshFilter>(true));
        foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            if (keep.Contains(renderer) || renderer is ParticleSystemRenderer) continue;
            if (renderer is MeshRenderer or SkinnedMeshRenderer)
            {
                renderer.enabled = false;
                renderer.forceRenderingOff = true;
            }
        }
        if (cart.thrustEffect != null) cart.thrustEffect.localScale = Vector3.zero;

        var visual = new GameObject("IHM Flame Chassis");
        visual.transform.SetParent(root, false);
        var sand = ItemArt.Paint(template, "workshop_wear.png", Sand, "IHM Flame sand");
        var sandDark = ItemArt.Paint(template, "workshop_wear.png", SandDark, "IHM Flame sand shadow");
        var rubber = ItemArt.Paint(template, "workshop_wear.png", Rubber, "IHM Flame rubber");
        var steel = ItemArt.Paint(template, "workshop_wear.png", Steel, "IHM Flame steel");
        var hazard = ItemArt.Paint(template, "workshop_wear.png", Hazard, "IHM Flame hazard");

        // Tray: one painted slab per solid box collider of the shell (walls and floor), handle excluded.
        int slabs = 0;
        foreach (var box in root.GetComponentsInChildren<BoxCollider>(true))
        {
            if (box.isTrigger || IsUnder(box.transform, cart.handlePoint) || IsUnder(box.transform, cart.inCart ?? root.Find("In Cart"))) continue;
            var m = root.worldToLocalMatrix * box.transform.localToWorldMatrix;
            var center = m.MultiplyPoint3x4(box.center);
            var size = Vector3.Scale(m.lossyScale, box.size);
            size = new Vector3(Mathf.Abs(size.x), Mathf.Abs(size.y), Mathf.Abs(size.z));
            if (size.x > 4f || size.y > 4f || size.z > 4f) continue;
            Block(visual.transform, "Tray panel", center, m.rotation, size + Vector3.one * .01f, slabs % 2 == 0 ? sand : sandDark, layer);
            slabs++;
        }
        if (slabs == 0)
            Plugin.Log.LogWarning("[Flame] No box colliders found on the cart shell; drawing a default tray.");

        float floor = bounds.min.y;
        var size2 = bounds.size;
        // Chassis skirt and bumper bands around the footprint.
        Block(visual.transform, "Chassis skirt", new Vector3(bounds.center.x, floor + .11f, bounds.center.z), Quaternion.identity,
            new Vector3(size2.x * .96f, .12f, size2.z * .96f), sandDark, layer);
        Block(visual.transform, "Front bumper", new Vector3(bounds.center.x, floor + .2f, bounds.max.z + .02f), Quaternion.identity,
            new Vector3(size2.x * .9f, .07f, .05f), hazard, layer);
        Block(visual.transform, "Rear bumper", new Vector3(bounds.center.x, floor + .2f, bounds.min.z - .02f), Quaternion.identity,
            new Vector3(size2.x * .9f, .07f, .05f), hazard, layer);
        if (slabs == 0)
            Block(visual.transform, "Tray", new Vector3(bounds.center.x, floor + .3f, bounds.center.z), Quaternion.identity,
                new Vector3(size2.x, .12f, size2.z), sand, layer);

        // Four wheels slightly inside the corners, touching the bottom of the shell.
        float wheelR = Mathf.Clamp(size2.y * .22f, .09f, .16f);
        foreach (float sx in new[] { -1f, 1f })
            foreach (float sz in new[] { -1f, 1f })
            {
                var c = new Vector3(bounds.center.x + sx * (size2.x / 2f - .02f), floor + wheelR * .85f, bounds.center.z + sz * (size2.z / 2f - wheelR * 1.4f));
                Wheel(visual.transform, c, wheelR, rubber, steel, layer);
            }

        // Turret on the front deck, facing away from the handle (the cart's driving direction).
        float handleZ = cart.handlePoint != null ? root.InverseTransformPoint(cart.handlePoint.position).z : bounds.min.z;
        bool frontIsPositive = handleZ <= bounds.center.z;
        float frontZ = frontIsPositive ? bounds.max.z - .22f : bounds.min.z + .22f;
        float yaw = frontIsPositive ? 0f : 180f;
        float top = bounds.max.y;
        var mount = new GameObject("IHM Flame Turret Mount").transform;
        mount.SetParent(visual.transform, false);
        mount.localPosition = new Vector3(bounds.center.x, top - .02f, frontZ);
        mount.localRotation = Quaternion.Euler(0f, yaw, 0f);
        Block(mount, "Turret deck", new Vector3(0f, -.03f, 0f), Quaternion.identity, new Vector3(Mathf.Min(.7f, size2.x * .8f), .06f, .5f), steel, layer);
        float scale = Mathf.Clamp(size2.x, .75f, 1.25f); // turret is ~0.6 m wide at scale 1
        var turret = ItemArt.AttachStatic(go, "flamethrower", template, layer);
        turret.SetParent(mount, false);
        turret.localPosition = Vector3.zero;
        turret.localRotation = Quaternion.identity;
        turret.localScale = Vector3.one * scale;

        Plugin.Log.LogInfo($"[Flame] Chassis: bounds {bounds.size}, {slabs} tray panels, front {(frontIsPositive ? "+Z" : "-Z")}, turret scale {scale:0.00}.");
        return new Layout
        {
            Turret = turret,
            NozzleLocal = new Vector3(0f, .42f, .74f),
            Top = top + .5f * scale,
            TurretYaw = yaw,
        };
    }

    private static Transform FindByName(Transform root, string name)
    {
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
            if (t.name.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0) return t;
        return null;
    }

    private static bool IsUnder(Transform t, Transform parent)
    {
        if (parent == null) return false;
        for (var p = t; p != null; p = p.parent) if (p == parent) return true;
        return false;
    }

    private static Bounds LocalBounds(Transform root, MeshFilter[] filters)
    {
        bool any = false;
        var bounds = new Bounds();
        foreach (var filter in filters)
        {
            if (filter == null || filter.sharedMesh == null) continue;
            var m = root.worldToLocalMatrix * filter.transform.localToWorldMatrix;
            var b = filter.sharedMesh.bounds;
            for (int i = 0; i < 8; i++)
            {
                var corner = b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                var p = m.MultiplyPoint3x4(corner);
                if (!any) { bounds = new Bounds(p, Vector3.zero); any = true; }
                else bounds.Encapsulate(p);
            }
        }
        return any ? bounds : new Bounds(new Vector3(0f, .45f, 0f), new Vector3(.9f, .9f, 1.3f));
    }

    private static void Primitives()
    {
        if (_cube != null) return;
        foreach (var type in new[] { PrimitiveType.Cube, PrimitiveType.Cylinder })
        {
            var temp = GameObject.CreatePrimitive(type);
            var mesh = temp.GetComponent<MeshFilter>().sharedMesh;
            if (type == PrimitiveType.Cube) _cube = mesh; else _cylinder = mesh;
            Object.DestroyImmediate(temp);
        }
    }

    private static GameObject Block(Transform parent, string name, Vector3 position, Quaternion rotation, Vector3 size, Material material, int layer)
    {
        Primitives();
        var obj = new GameObject(name) { layer = layer };
        obj.transform.SetParent(parent, false);
        obj.transform.localPosition = position;
        obj.transform.localRotation = rotation;
        obj.transform.localScale = size;
        obj.AddComponent<MeshFilter>().sharedMesh = _cube;
        obj.AddComponent<MeshRenderer>().sharedMaterial = material;
        return obj;
    }

    private static void Wheel(Transform parent, Vector3 center, float radius, Material tyre, Material hub, int layer)
    {
        Primitives();
        foreach (var (r, w, m, n) in new[] { (radius, .07f, tyre, "Wheel tyre"), (radius * .55f, .085f, hub, "Wheel hub") })
        {
            var obj = new GameObject(n) { layer = layer };
            obj.transform.SetParent(parent, false);
            obj.transform.localPosition = center;
            obj.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            obj.transform.localScale = new Vector3(r * 2f, w / 2f, r * 2f);
            obj.AddComponent<MeshFilter>().sharedMesh = _cylinder;
            obj.AddComponent<MeshRenderer>().sharedMaterial = m;
        }
    }
}

/// <summary>Runtime behaviour of a spawned Flamethrower C.A.R.T. (see <see cref="FlameCartItem"/>).</summary>
internal sealed class FlameCart : MonoBehaviour
{
    public Transform turret;
    public Vector3 nozzleLocal;
    public float turretBaseYaw;

    private const float MaxTraverse = 70f;
    private PhysGrabObject _pgo;
    private PhysGrabCart _cart;
    private ItemBattery _battery;
    private PhotonView _view;
    private bool _firing;
    private int _operatorViewId = -1;
    private float _tick;
    private float _noGrabTimer;
    private float _haulTimer;
    private int _haul;
    private bool _overloaded;
    private float _turretYaw;
    private ParticleSystem _flame, _smoke;
    private Light _light;
    private AudioSource _loop;
    private Sound _ignite, _off;
    private static Material _flameMaterial, _smokeMaterial;
    private static readonly List<FlameCart> Active = new();

    internal bool Overloaded => _overloaded;
    internal static FlameCart Of(PhysGrabCart cart) => cart != null ? cart.GetComponent<FlameCart>() : null;

    private void OnEnable() => Active.Add(this);
    private void OnDisable() { Active.Remove(this); ApplyState(false, -1, false); }

    private void Start()
    {
        _pgo = GetComponent<PhysGrabObject>();
        _cart = GetComponent<PhysGrabCart>();
        _battery = GetComponent<ItemBattery>();
        _view = GetComponent<PhotonView>();
        try
        {
            _ignite = ItemAudio.Cue("flame_ignite", .55f);
            _off = ItemAudio.Cue("power_down", .2f);
            BuildEffects();
        }
        catch (Exception e)
        {
            Plugin.Log.LogWarning($"[Flame] Effects unavailable: {e.Message}");
        }
    }

    private Vector3 Nozzle => turret != null ? turret.TransformPoint(nozzleLocal) : transform.position + Vector3.up;

    private PlayerAvatar Operator()
    {
        if (_operatorViewId >= 0)
        {
            var avatar = SemiFunc.PlayerAvatarGetFromPhotonID(_operatorViewId);
            if (avatar != null) return avatar;
        }
        return _pgo != null && _pgo.playerGrabbing.Count > 0 && _pgo.playerGrabbing[0] != null ? _pgo.playerGrabbing[0].playerAvatar : null;
    }

    // Operator's look direction, limited to the turret's traverse around the cart's forward axis.
    private Quaternion AimRotation()
    {
        var forward = Quaternion.Euler(0f, transform.eulerAngles.y + turretBaseYaw, 0f);
        var avatar = Operator();
        Transform view = null;
        try { view = avatar != null && avatar.localCamera != null ? avatar.localCamera.GetOverrideTransform() : null; }
        catch { view = null; }
        if (view == null) return forward;
        var e = view.rotation.eulerAngles;
        float yaw = Mathf.Clamp(Mathf.DeltaAngle(forward.eulerAngles.y, e.y), -MaxTraverse, MaxTraverse);
        float pitch = Mathf.Clamp(Mathf.DeltaAngle(0f, e.x), -20f, 25f);
        return Quaternion.Euler(pitch, forward.eulerAngles.y + yaw, 0f);
    }

    private void Update()
    {
        if (_pgo == null) return;
        LocalInput();
        AimTurret();
        if (_pgo.grabbed && _battery != null) _battery.OverrideBatteryShow(.25f);
        Effects();
        if (SemiFunc.IsMasterClientOrSingleplayer()) HostLogic();
    }

    private void LateUpdate()
    {
        if (_pgo == null || !_pgo.grabbedLocal || ItemInfoExtraUI.instance == null || SemiFunc.RunIsShop()) return;
        if (_overloaded)
            ItemInfoExtraUI.instance.ItemInfoText($"OVERLOADED: MAX ${ModConfig.FlameCartValueCap.Value / 1000f:0.#}K", new Color(1f, .2f, .15f));
        else if (_battery != null && _battery.batteryLife <= 0f)
            ItemInfoExtraUI.instance.ItemInfoText("NO FUEL: CHARGE THE BATTERY", new Color(1f, .46f, 0f));
    }

    private void LocalInput()
    {
        if (!_pgo.heldByLocalPlayer || PlayerController.instance == null || PlayerController.instance.InputDisableTimer > 0f ||
            !SemiFunc.NoTextInputsActive() || !SemiFunc.InputDown(InputKey.Interact)) return;
        if (SemiFunc.IsMasterClientOrSingleplayer()) HostToggle(SemiFunc.PlayerAvatarLocal());
        else _view.RPC(nameof(IHM_FlameToggleRPC), RpcTarget.MasterClient);
    }

    [PunRPC]
    private void IHM_FlameToggleRPC(PhotonMessageInfo info = default)
    {
        if (!SemiFunc.IsMasterClient() || _pgo == null) return;
        // Only a player who is actually holding this cart may operate it.
        foreach (var grabber in _pgo.playerGrabbing)
        {
            var avatar = grabber != null ? grabber.playerAvatar : null;
            if (avatar != null && avatar.photonView != null && avatar.photonView.Owner == info.Sender)
            {
                HostToggle(avatar);
                return;
            }
        }
    }

    private void HostToggle(PlayerAvatar avatar)
    {
        if (avatar == null) return;
        if (_firing) SetState(false, -1);
        else if (_battery != null && _battery.batteryLife > 0f && (!SemiFunc.RunIsShop() || Testing.TestShortcuts.Any)) SetState(true, avatar.photonView != null ? avatar.photonView.ViewID : -1);
    }

    private void SetState(bool on, int viewId)
    {
        if (SemiFunc.IsMultiplayer()) _view.RPC(nameof(IHM_FlameStateRPC), RpcTarget.All, on, viewId);
        else ApplyState(on, viewId, true);
    }

    [PunRPC]
    private void IHM_FlameStateRPC(bool on, int viewId, PhotonMessageInfo info = default)
    {
        if (!SemiFunc.MasterOnlyRPC(info)) return;
        ApplyState(on, viewId, true);
    }

    private void ApplyState(bool on, int viewId, bool sound)
    {
        if (on == _firing && viewId == _operatorViewId) return;
        bool was = _firing;
        _firing = on;
        _operatorViewId = on ? viewId : -1;
        _tick = .3f; // first burn shortly after ignition
        if (!sound) return;
        if (on && !was) _ignite?.Play(Nozzle);
        if (!on && was) _off?.Play(Nozzle);
    }

    private void HostLogic()
    {
        _haulTimer -= Time.deltaTime;
        if (_haulTimer <= 0f)
        {
            _haulTimer = .5f;
            _haul = Haul();
            int cap = ModConfig.FlameCartValueCap.Value;
            _overloaded = cap > 0 && _haul > cap;
        }
        if (!_firing) return;
        _noGrabTimer = _pgo.playerGrabbing.Count > 0 ? 0f : _noGrabTimer + Time.deltaTime;
        if (_noGrabTimer > 1f || _battery == null || _battery.batteryLife <= 0f || (SemiFunc.RunIsShop() && !Testing.TestShortcuts.Any))
        {
            SetState(false, -1);
            return;
        }
        _tick -= Time.deltaTime;
        if (_tick > 0f) return;
        _tick = Mathf.Max(.25f, ModConfig.FlameTickSeconds.Value);
        int damage = ModConfig.FlameDamagePerTick.Value;
        Burn(damage);
        float perTick = 100f * damage / Mathf.Max(1f, ModConfig.FlameBatteryDamage.Value);
        _battery.batteryLife = Mathf.Max(0f, _battery.batteryLife - perTick);
        if (_battery.batteryLife <= 0f) SetState(false, -1);
    }

    private void Burn(int damage)
    {
        var origin = Nozzle;
        var aim = AimRotation() * Vector3.forward;
        float range = ModConfig.FlameRange.Value;
        float cone = ModConfig.FlameConeAngle.Value;
        if (EnemyDirector.instance != null) EnemyDirector.instance.SetInvestigate(origin, 15f);
        if (EnemyDirector.instance == null || EnemyDirector.instance.enemiesSpawned == null) return;
        foreach (var parent in EnemyDirector.instance.enemiesSpawned)
        {
            var enemy = parent != null ? parent.Enemy : null;
            if (enemy == null || !enemy.isActiveAndEnabled || !enemy.HasHealth || enemy.Health == null || enemy.Health.dead) continue;
            var point = enemy.CenterTransform != null ? enemy.CenterTransform.position : enemy.transform.position;
            var to = point - origin;
            float distance = to.magnitude;
            if (distance > range + .5f) continue;
            if (distance > 1.2f && Vector3.Angle(aim, to) > cone) continue;
            if (Blocked(origin, to, distance, enemy)) continue;
            enemy.Health.Hurt(damage, to.sqrMagnitude > .0001f ? to.normalized : aim);
        }
    }

    // Walls stop the flame; the cart itself and the target's own colliders do not.
    private bool Blocked(Vector3 origin, Vector3 to, float distance, Enemy target)
    {
        if (distance < .3f) return false;
        var hits = Physics.RaycastAll(origin, to / distance, distance, SemiFunc.LayerMaskGetVisionObstruct(), QueryTriggerInteraction.Ignore);
        foreach (var hit in hits)
        {
            if (hit.collider == null || hit.collider.transform.IsChildOf(transform)) continue;
            if (hit.collider.GetComponentInParent<Enemy>() == target || hit.collider.GetComponentInParent<EnemyParent>() == target.EnemyParent) continue;
            if (hit.collider.GetComponentInParent<PhysGrabObject>() != null || hit.collider.GetComponentInParent<PlayerAvatar>() != null) continue;
            if (hit.distance < distance - .3f) return true;
        }
        return false;
    }

    private int Haul()
    {
        var inside = _cart != null ? _cart.inCart : null;
        if (inside == null) return 0;
        int total = 0;
        var seen = new HashSet<ValuableObject>();
        foreach (var collider in Physics.OverlapBox(inside.position, inside.lossyScale / 2f, inside.rotation))
        {
            if (collider.gameObject.layer != LayerMask.NameToLayer("PhysGrabObject")) continue;
            var valuable = collider.GetComponentInParent<ValuableObject>();
            if (valuable != null && seen.Add(valuable)) total += (int)valuable.dollarValueCurrent;
        }
        return total;
    }

    // ------------------------------------------------------------------ presentation

    private void AimTurret()
    {
        if (turret == null || turret.parent == null) return;
        float target = 0f;
        if (_pgo.grabbed || _firing)
        {
            var aim = AimRotation();
            target = Mathf.DeltaAngle(turret.parent.eulerAngles.y, aim.eulerAngles.y);
        }
        _turretYaw = Mathf.MoveTowardsAngle(_turretYaw, Mathf.Clamp(target, -MaxTraverse, MaxTraverse), 160f * Time.deltaTime);
        turret.localRotation = Quaternion.Euler(0f, _turretYaw, 0f);
    }

    private void Effects()
    {
        if (_flame == null) return;
        var nozzle = Nozzle;
        var rotation = AimRotation();
        _flame.transform.SetPositionAndRotation(nozzle, rotation);
        _smoke.transform.SetPositionAndRotation(nozzle, rotation);
        var flameEmission = _flame.emission;
        var smokeEmission = _smoke.emission;
        flameEmission.enabled = _firing;
        smokeEmission.enabled = _firing;
        if (_firing && !_flame.isPlaying) { _flame.Play(); _smoke.Play(); }
        if (_light != null)
        {
            _light.enabled = _firing;
            if (_firing) _light.intensity = 2.2f + Mathf.PerlinNoise(Time.time * 9f, 0f) * 1.6f;
        }
        if (_loop != null)
        {
            float target = _firing ? .55f : 0f;
            _loop.volume = Mathf.MoveTowards(_loop.volume, target, Time.deltaTime * (_firing ? 3f : 2f));
            if (_loop.volume > 0f && !_loop.isPlaying) _loop.Play();
            if (_loop.volume <= 0f && _loop.isPlaying) _loop.Stop();
        }
    }

    private void BuildEffects()
    {
        EnsureMaterials();
        float range = ModConfig.FlameRange.Value;
        _flame = Emitter("IHM Flame", _flameMaterial, range, flame: true);
        _smoke = Emitter("IHM Flame Smoke", _smokeMaterial, range, flame: false);
        var lightObj = new GameObject("IHM Flame Light");
        lightObj.transform.SetParent(_flame.transform, false);
        lightObj.transform.localPosition = new Vector3(0f, 0f, 1.5f);
        _light = lightObj.AddComponent<Light>();
        _light.type = LightType.Point;
        _light.color = new Color(1f, .55f, .2f);
        _light.range = range * .9f;
        _light.shadows = LightShadows.None;
        _light.enabled = false;

        var clip = ItemAudio.Cue("flame_loop").Sounds[0];
        _loop = _flame.gameObject.AddComponent<AudioSource>();
        _loop.clip = clip;
        _loop.loop = true;
        _loop.playOnAwake = false;
        _loop.spatialBlend = 1f;
        _loop.rolloffMode = AudioRolloffMode.Linear;
        _loop.minDistance = 2f;
        _loop.maxDistance = 28f;
        _loop.dopplerLevel = 0f;
        _loop.volume = 0f;
        if (AudioManager.instance != null) _loop.outputAudioMixerGroup = AudioManager.instance.SoundMasterGroup;
    }

    private ParticleSystem Emitter(string name, Material material, float range, bool flame)
    {
        var obj = new GameObject(name);
        obj.transform.SetParent(transform, false);
        var ps = obj.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.loop = true;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        float speed = flame ? 11f : 6f;
        main.startSpeed = new ParticleSystem.MinMaxCurve(speed * .8f, speed * 1.1f);
        main.startLifetime = new ParticleSystem.MinMaxCurve(range / speed * (flame ? .75f : 1.1f), range / speed * (flame ? .95f : 1.6f));
        main.startSize = flame ? new ParticleSystem.MinMaxCurve(.12f, .22f) : new ParticleSystem.MinMaxCurve(.3f, .5f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.maxParticles = flame ? 260 : 90;
        main.gravityModifier = flame ? -.08f : -.15f;
        var emission = ps.emission;
        emission.rateOverTime = flame ? 150f : 32f;
        emission.enabled = false;
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = Mathf.Clamp(ModConfig.FlameConeAngle.Value * .55f, 4f, 40f);
        shape.radius = .04f;
        var size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, .4f), new Keyframe(.5f, flame ? 4.5f : 3f), new Keyframe(1f, flame ? 7f : 6f)));
        var color = ps.colorOverLifetime;
        color.enabled = true;
        var gradient = new Gradient();
        if (flame)
            gradient.SetKeys(
                new[] { new GradientColorKey(new Color(1f, .95f, .7f), 0f), new GradientColorKey(new Color(1f, .62f, .16f), .25f),
                        new GradientColorKey(new Color(.95f, .26f, .05f), .65f), new GradientColorKey(new Color(.35f, .08f, .02f), 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(.95f, .08f), new GradientAlphaKey(.7f, .6f), new GradientAlphaKey(0f, 1f) });
        else
            gradient.SetKeys(
                new[] { new GradientColorKey(new Color(.25f, .22f, .2f), 0f), new GradientColorKey(new Color(.12f, .11f, .1f), 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(.32f, .35f), new GradientAlphaKey(0f, 1f) });
        color.color = gradient;
        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = flame ? .6f : .9f;
        noise.frequency = 1.2f;
        var collision = ps.collision;
        collision.enabled = true;
        collision.type = ParticleSystemCollisionType.World;
        collision.mode = ParticleSystemCollisionMode.Collision3D;
        collision.dampen = .6f;
        collision.bounce = .05f;
        collision.lifetimeLoss = .25f;
        collision.collidesWith = SemiFunc.LayerMaskGetVisionObstruct();
        collision.quality = ParticleSystemCollisionQuality.Low;
        var renderer = obj.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = material;
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        return ps;
    }

    private static void EnsureMaterials()
    {
        if (_flameMaterial != null) return;
        var additive = Shader.Find("Legacy Shaders/Particles/Additive") ?? Shader.Find("Particles/Standard Unlit") ?? Shader.Find("Sprites/Default");
        var blended = Shader.Find("Legacy Shaders/Particles/Alpha Blended") ?? Shader.Find("Particles/Standard Unlit") ?? Shader.Find("Sprites/Default");
        var puff = SoftPuff();
        _flameMaterial = new Material(additive) { name = "IHM Flame", mainTexture = puff, renderQueue = 3100, hideFlags = HideFlags.HideAndDontSave };
        _smokeMaterial = new Material(blended) { name = "IHM Flame Smoke", mainTexture = puff, renderQueue = 3050, hideFlags = HideFlags.HideAndDontSave };
        foreach (var m in new[] { _flameMaterial, _smokeMaterial })
            if (m.HasProperty("_TintColor")) m.SetColor("_TintColor", new Color(.5f, .5f, .5f, .5f));
    }

    private static Texture2D SoftPuff()
    {
        const int n = 64;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { name = "IHM Flame Puff", wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
        var pixels = new Color[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = (x + .5f) / n * 2f - 1f, dy = (y + .5f) / n * 2f - 1f;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float wisp = Mathf.PerlinNoise(x * .13f, y * .13f) * .35f;
                float a = Mathf.Clamp01(1f - d) ;
                a = Mathf.Clamp01(a * a * (1.2f - wisp));
                pixels[y * n + x] = new Color(1f, 1f, 1f, a);
            }
        tex.SetPixels(pixels);
        tex.Apply(false, true);
        return tex;
    }

    /// <summary>Overloaded tray: the host never lets the cart enter Strong mode (Weak dragging still works).</summary>
    [HarmonyPatch(typeof(PhysGrabCart), "Update")]
    private static class OverloadPatch
    {
        [HarmonyPrefix]
        private static void Prefix(PhysGrabCart __instance)
        {
            if (!SemiFunc.IsMasterClientOrSingleplayer() || !__instance.cartActive) return;
            var flame = Of(__instance);
            if (flame != null && flame._overloaded) __instance.cartActive = false;
        }
    }

    [HarmonyPatch(typeof(PhysGrabCart), "CartSteer")]
    private static class OverloadSteerPatch
    {
        [HarmonyPrefix]
        private static void Prefix(PhysGrabCart __instance)
        {
            if (!SemiFunc.IsMasterClientOrSingleplayer() || Active.Count == 0) return;
            var flame = Of(__instance);
            if (flame == null || !flame._overloaded) return;
            __instance.cartActive = false;
            // Swallow the fresh press that would otherwise re-enable Strong mode this step.
            foreach (var grabber in __instance.physGrabObjectGrabArea != null ? __instance.physGrabObjectGrabArea.listOfAllGrabbers : null ?? new List<PhysGrabber>())
                if (grabber != null) grabber.initialPressTimer = 0f;
        }
    }
}
