using System;
using System.Collections.Generic;
using IncreaseHardMod.Drones;
using IncreaseHardMod.Visuals;
using Photon.Pun;
using REPOLib.Modules;
using UnityEngine;
using Object = UnityEngine.Object;

namespace IncreaseHardMod.Gear;

/// <summary>
/// Flamethrower C.A.R.T.: a wheeled flamethrower turret with on-board loot storage.
///
/// It is NOT a cart. The prefab starts from the vanilla C.A.R.T. Cannon with its cannon/cart logic
/// (ItemCartCannonMain, ItemCartCannon, PhysGrabObjectGrabArea) removed, which leaves a plain
/// physics item: PhysGrabObject (grab it anywhere, like any other item), ItemBattery with the native
/// battery widget (charging station, saved charge) and ItemToggle. The native ItemToggle is what makes
/// ItemAttributes print the item's "[E]" use prompt while you hold it, and it handles the E key.
///
/// Storage: a copy of the Hauler's (Item Vehicle Semiscooter) own "Item Valuable Box" sits on the
/// rear deck. Drop loot onto it and the native ItemValuableBox absorbs it, shows the fill %, refuses
/// over-size/over-limit loot, and is extracted by the extraction point exactly as on the Hauler.
///
/// Model: original Blender art from tools/build_flamethrower_art.py in three parts so the turret can
/// yaw and the gun head can pitch toward where the operator looks.
///
/// Fire: E toggles the native ItemToggle. The host validates (the toggling player must be holding it,
/// it must have fuel), deals damage each tick to monsters inside a cone in front of the nozzle, drains
/// the battery and broadcasts the on/off state; every client draws the flame locally with copies of
/// the game's own explosion fire/smoke materials. Players are never damaged.
/// </summary>
internal static class FlameCartItem
{
    // Kept from the earlier cart-based version so saved purchases still load.
    public const string AssetName = "Item Cart Flamethrower";

    // Unity-space mount points printed by tools/build_flamethrower_art.py (IHM_* lines).
    private static readonly Vector3 TurretMount = new(0f, .244f, .30f);
    private static readonly Vector3 HeadPivot = new(0f, .54f, .02f);   // turret space
    internal static readonly Vector3 NozzleLocal = new(0f, .05f, .74f); // head space (just past the muzzle)
    private static readonly Vector3 BoxMount = new(0f, .215f, -.30f);
    private const float BoxScale = .9f;

    private static readonly string[] StripTypes = { "ItemCartCannon", "ItemCartCannonMain", "PhysGrabObjectGrabArea" };

    internal static void Build()
    {
        if (!ModConfig.FlameCartEnabled.Value) return;
        try
        {
            var items = StatsManager.instance.itemDictionary;
            var donor = FindDonor(items);
            if (donor == null)
            {
                Plugin.Log.LogWarning("[Flame] No vanilla battery item (C.A.R.T. Cannon) to build on; Flamethrower C.A.R.T. disabled.");
                return;
            }
            var hauler = FindHaulerBox(items);
            if (hauler == null)
            {
                Plugin.Log.LogWarning("[Flame] Vanilla Hauler storage box not found; Flamethrower C.A.R.T. disabled.");
                return;
            }
            // Fail before anything is created if the embedded art is missing.
            foreach (string key in new[] { "flamethrower_base", "flamethrower_turret", "flamethrower_head" }) ItemArt.LoadModel(key);

            var item = Object.Instantiate(donor);
            item.name = AssetName;
            item.itemName = "Flamethrower C.A.R.T.";
            item.itemNameLocalized = null;
            item.itemType = SemiFunc.itemType.tool;
            item.description = "A wheeled flamethrower turret with a loot box on the back (the Hauler's storage). " +
                "Grab the red hand on the back of the box to push it like a C.A.R.T.; press E to toggle a flame that burns " +
                "monsters where you aim; drop loot onto the box to store it.";
            item.maxAmount = 2;
            item.maxAmountInShop = 1;
            item.maxPurchase = false;
            item.minPlayerCount = 1;
            item.value = Object.Instantiate(donor.value);
            // Shop pads are matched by size class: take the Hauler's (vehicle pad), not the cannon's.
            var haulerItem = hauler.GetComponent<ItemAttributes>()?.item;
            item.itemVolume = haulerItem != null ? haulerItem.itemVolume : SemiFunc.itemVolume.vehicle;
            // Shop price in K = value * itemValueMultiplier(4) / 1000, so value = K * 250.
            int minK = Mathf.Max(1, ModConfig.FlameCartPriceMinK.Value);
            int maxK = Mathf.Max(minK, ModConfig.FlameCartPriceMaxK.Value);
            item.value.valueMin = minK * 250f;
            item.value.valueMax = maxK * 250f;
            var preset = ScriptableObject.CreateInstance<ColorPresets>();
            preset.colorMain = new Color(1f, .45f, .1f);
            preset.colorLight = new Color(1f, .75f, .35f);
            preset.colorDark = new Color(.55f, .2f, .05f);
            item.colorPreset = preset;

            var go = Object.Instantiate(donor.prefab.Prefab, DroneItems.EnsureHolder().transform);
            go.name = AssetName;
            go.GetComponent<ItemAttributes>().item = item;
            GrabMarker.LogNativeCandidates(go);
            StripCannon(go);

            var battery = go.GetComponent<ItemBattery>();
            int colliderLayer = ColliderLayer(go);
            var template = ItemArt.FresnelMaterialDonor(go.transform.Find("Mesh"));
            if (template == null) throw new InvalidOperationException("No albedo-capable material for the model");
            int visualLayer = VisualLayer(go);
            RemoveDonorChildren(go, battery);
            foreach (var volume in go.GetComponentsInChildren<ItemVolume>(true)) volume.itemVolume = item.itemVolume;

            var (turret, head) = BuildModel(go, template, visualLayer);
            AddColliders(go, colliderLayer);
            var box = AddStorage(go, hauler, BoxMount, BoxScale, ModConfig.FlameCartValueCap.Value);
            GrabMarker.AttachToBoxBack(box.transform, visualLayer);
            AddHandle(go);
            go.AddComponent<FlameCartGrabState>();
            AddIconCamera(go, hauler);

            // Native ItemLight Get/SetFloats _FresnelScale on these every frame; a material without it
            // logs an error per call, so only pass renderers whose preset exposes the property.
            var glowRenderers = new List<MeshRenderer>();
            foreach (var r in go.transform.Find("IHM Flame Visual").GetComponentsInChildren<MeshRenderer>(true))
                if (ItemArt.HasFresnel(r.sharedMaterial)) glowRenderers.Add(r);
            if (glowRenderers.Count == 0)
                Plugin.Log.LogWarning($"[Flame] No fresnel-capable material ({template.shader.name}); idle item glow disabled.");
            foreach (var light in go.GetComponentsInChildren<ItemLight>(true))
                light.meshRenderers = glowRenderers;

            battery.batteryBars = Mathf.Max(4, battery.batteryBars);
            battery.hasBatteryVisuals = true;
            battery.upOffset = 1.45f;
            battery.autoDrain = false;
            battery.batteryDrainRate = 0f;
            battery.isUnchargable = false;
            if (battery.batteryTransform != null) battery.batteryTransform.localPosition = Vector3.up * battery.upOffset;

            var toggle = go.GetComponent<ItemToggle>();
            toggle.playSound = false; // ignition/shutdown cues are our own
            toggle.toggleState = false;

            var flame = go.AddComponent<FlameCart>();
            flame.turret = turret;
            flame.head = head;
            flame.storage = box;
            flame.nativeFlames = AddNativeFlames(go, head);
            FlameCart.NativeFireMaterial ??= FindFireMaterial(hauler.gameObject);
            if (flame.nativeFlames == null)
                Plugin.Log.LogWarning("[Flame] Native flamethrower effect unavailable; using the fallback particle jet.");

            InventoryItemVisuals.Prepare(go);
            if (Items.RegisterItem(go.GetComponent<ItemAttributes>()) == null)
                Plugin.Log.LogWarning($"[Flame] REPOLib refused to register {AssetName}.");
            else
                Plugin.Log.LogInfo($"[Flame] Registered {AssetName} (base: {donor.name}, storage: Hauler box, limit ${box.valueLimit / 1000f:0.#}K).");
        }
        catch (Exception e)
        {
            Plugin.Log.LogError($"[Flame] Failed to build the Flamethrower C.A.R.T.: {e}");
        }
    }

    // A plain floor item with a native battery widget and use toggle, and no cart/vehicle logic.
    private static Item FindDonor(Dictionary<string, Item> items)
    {
        if (items.TryGetValue("Item Cart Cannon", out var named) && Usable(named)) return named;
        var names = new List<string>(items.Keys);
        names.Sort(StringComparer.Ordinal);
        foreach (var name in names)
            if (Usable(items[name]) && items[name].prefab.Prefab.GetComponent<ItemEquippable>() == null) return items[name];
        return null;

        static bool Usable(Item item)
        {
            var prefab = item?.prefab?.Prefab;
            if (prefab == null || prefab.GetComponent<PhysGrabCart>() != null || prefab.GetComponent<ItemVehicle>() != null ||
                prefab.GetComponent<ItemToggle>() == null || prefab.GetComponent<PhysGrabObject>() == null) return false;
            var battery = prefab.GetComponent<ItemBattery>();
            return battery != null && battery.batteryTransform != null &&
                   battery.batteryTransform.GetComponentInChildren<BatteryVisualLogic>(true) != null;
        }
    }

    internal static ItemVehicle FindHaulerBox(Dictionary<string, Item> items)
    {
        if (items.TryGetValue("Item Vehicle Semiscooter", out var named) && Has(named, out var vehicle)) return vehicle;
        var names = new List<string>(items.Keys);
        names.Sort(StringComparer.Ordinal);
        foreach (var name in names)
            if (Has(items[name], out vehicle)) return vehicle;
        return null;

        static bool Has(Item item, out ItemVehicle vehicle)
        {
            vehicle = item?.prefab?.Prefab != null ? item.prefab.Prefab.GetComponent<ItemVehicle>() : null;
            return vehicle != null && vehicle.valuableBox != null && vehicle.valuableBox.boxChecker != null;
        }
    }

    private static void StripCannon(GameObject go)
    {
        // Dependents first, so no RequireComponent blocks the removal.
        foreach (string type in StripTypes)
            foreach (var mb in go.GetComponents<MonoBehaviour>())
                if (mb != null && mb.GetType().Name == type) Object.DestroyImmediate(mb);
        foreach (var mb in go.GetComponents<MonoBehaviour>())
            if (mb != null && Array.IndexOf(StripTypes, mb.GetType().Name) >= 0)
                throw new InvalidOperationException("Could not remove " + mb.GetType().Name);
    }

    private static int ColliderLayer(GameObject go)
    {
        var c = go.GetComponentInChildren<PhysGrabObjectCollider>(true);
        return c != null ? c.gameObject.layer : go.layer;
    }

    private static int VisualLayer(GameObject go)
    {
        foreach (var r in go.GetComponentsInChildren<MeshRenderer>(true))
            if (r.GetComponent<Collider>() == null) return r.gameObject.layer;
        return go.layer;
    }

    // Keeps only children native components still rely on: battery widget, item volume, item lights, audio.
    private static void RemoveDonorChildren(GameObject go, ItemBattery battery)
    {
        var doomed = new List<GameObject>();
        foreach (Transform child in go.transform)
        {
            bool keep = (battery.batteryTransform != null && (child == battery.batteryTransform || battery.batteryTransform.IsChildOf(child))) ||
                        child.GetComponent<ItemVolume>() != null || child.GetComponent<ItemLight>() != null ||
                        (child.GetComponent<AudioSource>() != null && child.GetComponent<Renderer>() == null && child.childCount == 0);
            if (!keep) doomed.Add(child.gameObject);
        }
        foreach (var obj in doomed) Object.DestroyImmediate(obj);
    }

    private static (Transform turret, Transform head) BuildModel(GameObject go, Material template, int layer)
    {
        var visual = new GameObject("IHM Flame Visual") { layer = layer }.transform;
        visual.SetParent(go.transform, false);
        ItemArt.AttachStatic(go, "flamethrower_base", template, layer).SetParent(visual, false);
        var mount = new GameObject("IHM Flame Turret") { layer = layer }.transform;
        mount.SetParent(visual, false);
        mount.localPosition = TurretMount;
        ItemArt.AttachStatic(go, "flamethrower_turret", template, layer).SetParent(mount, false);
        var head = new GameObject("IHM Flame Head") { layer = layer }.transform;
        head.SetParent(mount, false);
        head.localPosition = HeadPivot;
        ItemArt.AttachStatic(go, "flamethrower_head", template, layer).SetParent(head, false);
        return (mount, head);
    }

    // Grab colliders over the rest pose (the turret only turns visually). The storage walls keep loot on the box.
    private static void AddColliders(GameObject go, int layer)
    {
        var root = new GameObject("IHM Flame Colliders") { layer = layer }.transform;
        root.SetParent(go.transform, false);
        Box(root, "Chassis", new Vector3(0f, .115f, -.02f), new Vector3(.80f, .21f, 1.30f), layer);
        Box(root, "Control box", new Vector3(.24f, .29f, .47f), new Vector3(.17f, .14f, .15f), layer);
        Box(root, "Arm", new Vector3(0f, .50f, .28f), new Vector3(.26f, .50f, .24f), layer);
        Box(root, "Gun head", new Vector3(0f, .83f, .62f), new Vector3(.30f, .20f, .62f), layer);
        Box(root, "Fuel tanks", new Vector3(0f, 1.0f, .17f), new Vector3(.36f, .26f, .30f), layer);
        // The donor's "Center of Mass" child went with its other parts; without it Unity centres the mass among the
        // tall gun colliders (high and forward), so the cart pivots off-centre and twists on floor contact.
        // PhysGrabObject.Awake reads this child by name.
        var com = new GameObject("Center of Mass").transform;
        com.SetParent(go.transform, false);
        com.localPosition = new Vector3(0f, .12f, -.02f);
        const float half = StorageHalf, h = StorageHeight;
        float y = BoxMount.y + h / 2f, z = BoxMount.z;
        Box(root, "Storage wall L", new Vector3(-half, y, z), new Vector3(.03f, h, half * 2f), layer);
        Box(root, "Storage wall R", new Vector3(half, y, z), new Vector3(.03f, h, half * 2f), layer);
        Box(root, "Storage wall back", new Vector3(0f, y, z - half), new Vector3(half * 2f, h, .03f), layer);
        Box(root, "Storage wall front", new Vector3(0f, y, z + half), new Vector3(half * 2f, h, .03f), layer);
    }

    // Storage box walls (see AddColliders): the back wall is the push handle.
    private const float StorageHalf = .335f, StorageHeight = .36f;

    private static void AddHandle(GameObject go)
    {
        float back = BoxMount.z - StorageHalf;
        var point = new GameObject("IHM Handle Point").transform;
        point.SetParent(go.transform, false);
        point.localPosition = new Vector3(0f, BoxMount.y + StorageHeight * .75f, back - .04f);
        var handle = go.AddComponent<FlameCartHandle>();
        handle.handlePoint = point;
        // The whole back wall, a little proud of it, so a grab on the hand plate always counts.
        handle.zoneCenter = new Vector3(0f, BoxMount.y + StorageHeight / 2f, back - .03f);
        handle.zoneSize = new Vector3(StorageHalf * 2f + .1f, StorageHeight + .12f, .22f);
    }

    private static void Box(Transform parent, string name, Vector3 center, Vector3 size, int layer)
    {
        var obj = new GameObject(name) { layer = layer };
        obj.transform.SetParent(parent, false);
        obj.transform.localPosition = center;
        obj.AddComponent<BoxCollider>().size = size;
        obj.AddComponent<PhysGrabObjectBoxCollider>().drawGizmos = false;
        obj.AddComponent<PhysGrabObjectCollider>();
        // PhysGrabber only starts a grab on colliders with this tag; untagged ones are ignored.
        obj.tag = GrabTag;
    }

    private const string GrabTag = "Phys Grab Object";

    // The copied Hauler box brings its own solid colliders. Any that are not grab surfaces would stop the
    // grab ray in front of ours, so make every solid one a grab surface of this item.
    private static void MakeBoxGrabbable(ItemValuableBox box)
    {
        int fixedCount = 0;
        foreach (var c in box.GetComponentsInChildren<Collider>(true))
        {
            if (c.isTrigger) continue;
            if (!c.CompareTag(GrabTag)) { c.gameObject.tag = GrabTag; fixedCount++; }
            if (c.GetComponent<PhysGrabObjectCollider>() == null) c.gameObject.AddComponent<PhysGrabObjectCollider>();
        }
        if (fixedCount > 0) Plugin.Log.LogInfo($"[Flame] Storage box: {fixedCount} solid collider(s) made grab surfaces.");
    }

    internal static ItemValuableBox AddStorage(GameObject go, ItemVehicle hauler, Vector3 mount, float scale, int cap)
    {
        var obj = Object.Instantiate(hauler.valuableBox.gameObject, go.transform, false);
        obj.name = "Item Valuable Box";
        obj.SetActive(true);
        obj.transform.localPosition = mount;
        obj.transform.localRotation = Quaternion.identity;
        obj.transform.localScale = Vector3.one * scale;
        var box = obj.GetComponent<ItemValuableBox>();
        if (cap > 0) box.valueLimit = cap;
        MakeBoxGrabbable(box);
        return box;
    }

    /// <summary>
    /// The game's own flamethrower effect (SemiZuperFlames on the Arctic Flamethrower valuable): flame jet,
    /// ignition/stop bursts, smoke, flicker light and its native start/loop/stop sounds. Only the visuals
    /// are copied: its particle hurt logic and colliders are removed, the host deals our damage.
    /// </summary>
    private static SemiZuperFlames AddNativeFlames(GameObject go, Transform head)
    {
        try
        {
            var prefab = Resources.Load<GameObject>("Valuables/04 Big/Valuable Arctic Flamethrower");
            var source = prefab != null ? prefab.GetComponentInChildren<SemiZuperFlames>(true) : null;
            if (source == null) return null;
            var obj = Object.Instantiate(source.gameObject, head, false);
            obj.name = "IHM Native Flames";
            obj.transform.localPosition = FlameCartItem.NozzleLocal;
            obj.transform.localRotation = Quaternion.identity;
            obj.transform.localScale = Vector3.one;
            foreach (var mb in obj.GetComponentsInChildren<EnemySlowMouthParticlePukeCollision>(true)) Object.DestroyImmediate(mb);
            foreach (var hurt in obj.GetComponentsInChildren<HurtCollider>(true)) Object.DestroyImmediate(hurt);
            foreach (var collider in obj.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(collider);
            // Stretch the jet so what you see matches the configured damage reach (the native jet is ~6 m).
            float reach = Mathf.Clamp(ModConfig.FlameRange.Value / 6f, .6f, 2f);
            var flames = obj.GetComponent<SemiZuperFlames>();
            // Idle until lit: SemiZuperFlames only stops systems it started itself.
            foreach (var ps in obj.GetComponentsInChildren<ParticleSystem>(true))
            {
                var main = ps.main;
                main.playOnAwake = false;
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
            if (flames.flameLight != null) flames.flameLight.enabled = false;
            foreach (var ps in flames.pukeParticles)
            {
                if (ps == null) continue;
                var main = ps.main;
                main.startSpeedMultiplier *= reach;
                // Never let the jet bounce off the flamethrower itself or the player holding it.
                var collision = ps.collision;
                if (collision.enabled)
                    collision.collidesWith = SemiFunc.LayerMaskGetVisionObstruct() &
                        ~LayerMask.GetMask("PhysGrabObject", "PhysGrabObjectCart", "PhysGrabObjectHinge", "Player");
            }
            return flames;
        }
        catch (Exception e)
        {
            Plugin.Log.LogWarning($"[Flame] Could not copy the native flamethrower effect: {e.Message}");
            return null;
        }
    }

    // The soft flame-tongue particle material the game uses for fires ("Truck Menu Animated Fire").
    private static Material FindFireMaterial(GameObject root)
    {
        foreach (var r in root.GetComponentsInChildren<ParticleSystemRenderer>(true))
            if (r.sharedMaterial != null && r.sharedMaterial.name.IndexOf("Animated Fire", StringComparison.OrdinalIgnoreCase) >= 0)
                return r.sharedMaterial;
        return null;
    }

    private static void AddIconCamera(GameObject go, ItemVehicle hauler)
    {
        if (go.GetComponentInChildren<SemiIconMaker>(true) != null) return;
        var maker = hauler.GetComponentInChildren<SemiIconMaker>(true);
        if (maker == null) return; // keeps the donor icon
        var copy = Object.Instantiate(maker.gameObject, go.transform, false);
        copy.name = maker.gameObject.name;
        copy.transform.localPosition = maker.transform.localPosition;
        copy.transform.localRotation = maker.transform.localRotation;
    }
}

/// <summary>Runtime behaviour of a spawned Flamethrower C.A.R.T. (see <see cref="FlameCartItem"/>).</summary>
internal sealed class FlameCart : MonoBehaviour
{
    public Transform turret, head;
    public ItemValuableBox storage;
    public SemiZuperFlames nativeFlames;
    internal static Material NativeFireMaterial;

    private const float MaxTraverse = 115f, MinPitch = -35f, MaxPitch = 45f;
    private float _damageCarry;
    private PhysGrabObject _pgo;
    private ItemBattery _battery;
    private ItemToggle _toggle;
    private PhotonView _view;
    private bool _firing;
    private int _operatorViewId = -1;
    private float _tick;
    private float _noGrabTimer;
    private float _turretYaw, _headPitch, _yawSpeed, _pitchSpeed;
    private ParticleSystem _flame, _smoke;
    private Light _light;
    private AudioSource _loop;
    private Sound _ignite, _off;
    private static Material _flameMaterial, _smokeMaterial;

    private void OnDisable() => ApplyState(false, -1, false);

    private void Start()
    {
        _pgo = GetComponent<PhysGrabObject>();
        _battery = GetComponent<ItemBattery>();
        _toggle = GetComponent<ItemToggle>();
        _view = GetComponent<PhotonView>();
        if (nativeFlames != null) return; // the native effect brings its own light and sounds
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

    private Vector3 Nozzle => head != null ? head.TransformPoint(FlameCartItem.NozzleLocal) : transform.position + Vector3.up;

    // The flame only ever hurts enemies (see Burn), so it is safe to try out in the shop too.
    private bool FiringAllowedHere => true;

    internal PlayerAvatar Operator()
    {
        if (_operatorViewId >= 0)
        {
            var avatar = SemiFunc.PlayerAvatarGetFromPhotonID(_operatorViewId);
            if (avatar != null) return avatar;
        }
        return _pgo != null && _pgo.playerGrabbing.Count > 0 && _pgo.playerGrabbing[0] != null ? _pgo.playerGrabbing[0].playerAvatar : null;
    }

    internal bool Firing => _firing;

    private static readonly RaycastHit[] AimHits = new RaycastHit[16];

    // The point under the operator's crosshair (first wall/floor, never this item), or max reach.
    private Vector3 CrosshairPoint(Transform view)
    {
        float reach = ModConfig.FlameRange.Value + 2f;
        var best = view.position + view.forward * reach;
        float bestDistance = reach;
        // Monsters count too, so the barrels converge on the monster under the crosshair, not the wall behind it.
        int count = Physics.RaycastNonAlloc(view.position, view.forward, AimHits, reach, (int)SemiFunc.LayerMaskGetVisionObstruct() | LayerMask.GetMask("Enemy"), QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            var hit = AimHits[i];
            if (hit.collider == null || hit.collider.transform.IsChildOf(transform) || hit.collider.GetComponentInParent<PlayerAvatar>() != null) continue;
            if (hit.distance < bestDistance) { bestDistance = hit.distance; best = hit.point; }
        }
        return best;
    }

    // Barrels converge on what the operator's crosshair is on (comfortable from behind the box while
    // pushing), limited to the turret's traverse around the item's forward axis.
    private Quaternion AimRotation()
    {
        var forward = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
        var avatar = Operator();
        Transform view = null;
        try { view = avatar != null && avatar.localCamera != null ? avatar.localCamera.GetOverrideTransform() : null; }
        catch { view = null; }
        if (view == null) return forward;
        var pivot = head != null ? head.position : transform.position + Vector3.up;
        var direction = CrosshairPoint(view) - pivot;
        // Crosshair on (or right beside) the turret itself: just follow the look direction.
        if (direction.sqrMagnitude < .64f) direction = view.forward;
        var e = Quaternion.LookRotation(direction).eulerAngles;
        float yaw = Mathf.Clamp(Mathf.DeltaAngle(forward.eulerAngles.y, e.y), -MaxTraverse, MaxTraverse);
        float pitch = Mathf.Clamp(Mathf.DeltaAngle(0f, e.x), MinPitch, MaxPitch);
        return Quaternion.Euler(pitch, forward.eulerAngles.y + yaw, 0f);
    }

    private void Update()
    {
        if (_pgo == null) return;
        AimTurret();
        if (_pgo.grabbed && _battery != null) _battery.OverrideBatteryShow(.25f);
        Effects();
        if (SemiFunc.IsMasterClientOrSingleplayer()) HostLogic();
    }

    private void LateUpdate()
    {
        if (_pgo == null || !_pgo.grabbedLocal || ItemInfoExtraUI.instance == null || SemiFunc.RunIsShop()) return;
        if (_battery != null && _battery.batteryLife <= 0f)
            ItemInfoExtraUI.instance.ItemInfoText("NO FUEL: CHARGE THE BATTERY", new Color(1f, .46f, 0f));
    }

    // ------------------------------------------------------------------ host authority

    // The native ItemToggle flips on whoever pressed E; the host decides whether the flame actually lights.
    private void SyncToggle()
    {
        if (_toggle == null) return;
        bool want = _toggle.toggleState;
        if (want == _firing) return;
        if (!want) { SetState(false, -1); return; }
        var avatar = Toggler();
        if (avatar != null && _battery != null && _battery.batteryLife > 0f && FiringAllowedHere)
            SetState(true, avatar.photonView != null ? avatar.photonView.ViewID : -1);
        else
            _toggle.ToggleItem(false);
    }

    // Only a player who is actually holding the item may light it.
    private PlayerAvatar Toggler()
    {
        if (!SemiFunc.IsMultiplayer())
            return _pgo.playerGrabbing.Count > 0 && _pgo.playerGrabbing[0] != null ? _pgo.playerGrabbing[0].playerAvatar : null;
        foreach (var grabber in _pgo.playerGrabbing)
        {
            var avatar = grabber != null ? grabber.playerAvatar : null;
            if (avatar != null && avatar.photonView != null && avatar.photonView.ViewID == _toggle.playerTogglePhotonID) return avatar;
        }
        return null;
    }

    private void Stop()
    {
        SetState(false, -1);
        if (_toggle != null && _toggle.toggleState) _toggle.ToggleItem(false);
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
        _tick = .05f; // first burn almost at once after ignition
        _damageCarry = 0f;
        if (!sound) return;
        if (on && !was) _ignite?.Play(Nozzle);
        if (!on && was) _off?.Play(Nozzle);
    }

    private void HostLogic()
    {
        SyncToggle();
        if (!_firing) return;
        _noGrabTimer = _pgo.playerGrabbing.Count > 0 ? 0f : _noGrabTimer + Time.deltaTime;
        if (_noGrabTimer > 1f || _battery == null || _battery.batteryLife <= 0f || !FiringAllowedHere)
        {
            Stop();
            return;
        }
        _tick -= Time.deltaTime;
        if (_tick > 0f) return;
        float step = Mathf.Max(.1f, ModConfig.FlameTickSeconds.Value);
        _tick += step;
        // Damage per second split into small ticks; the fraction carries over so low rates still add up exactly.
        _damageCarry += ModConfig.FlameDamagePerSecond.Value * step *
            Skills.SkillEffects.WeaponMultiplier(Operator(), Skills.SkillEffects.Weapon.FlameCart);
        int damage = Mathf.FloorToInt(_damageCarry);
        _damageCarry -= damage;
        if (damage > 0) Burn(damage);
        float perTick = 100f * step / Mathf.Max(1f, ModConfig.FlameFuelSeconds.Value);
        _battery.DrainContinuous(perTick / (2f * step), step);
        if (_battery.batteryLife <= 0f) Stop();
    }

    private void Burn(int damage)
    {
        var origin = Nozzle;
        // Hurt along the barrels you see (the flame jet comes out of the head), not the raw crosshair ray.
        var aim = head != null ? head.forward : AimRotation() * Vector3.forward;
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
            // Monsters are bodies, not points: widen the cone by about half a monster's width at that distance.
            if (distance > 1.2f && Vector3.Angle(aim, to) > cone + Mathf.Atan2(.6f, distance) * Mathf.Rad2Deg) continue;
            if (Blocked(origin, to, distance, enemy)) continue;
            int dealt = Mathf.Max(1, Mathf.RoundToInt(damage * Skills.TradeOffs.TargetFactor(Operator(), enemy)));
            enemy.Health.Hurt(dealt, to.sqrMagnitude > .0001f ? to.normalized : aim);
        }
    }

    // Walls stop the flame; the item itself and the target's own colliders do not.
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

    // ------------------------------------------------------------------ presentation

    private void AimTurret()
    {
        if (turret == null) return;
        float yaw = 0f, pitch = 0f;
        if (_pgo.grabbed || _firing)
        {
            var aim = AimRotation();
            yaw = Mathf.DeltaAngle(transform.eulerAngles.y, aim.eulerAngles.y);
            // Pitch relative to the item's own tilt, so a tipped base still aims at the look target.
            var local = Quaternion.Inverse(transform.rotation) * (aim * Vector3.forward);
            pitch = -Mathf.Atan2(local.y, new Vector2(local.x, local.z).magnitude) * Mathf.Rad2Deg;
        }
        // Eased (not linear) so the turret settles on target without jerking as the cart bumps along.
        _turretYaw = Mathf.SmoothDampAngle(_turretYaw, Mathf.Clamp(yaw, -MaxTraverse, MaxTraverse), ref _yawSpeed, .045f, 540f);
        _headPitch = Mathf.SmoothDampAngle(_headPitch, Mathf.Clamp(pitch, MinPitch, MaxPitch), ref _pitchSpeed, .05f, 360f);
        turret.localRotation = Quaternion.Euler(0f, _turretYaw, 0f);
        if (head != null) head.localRotation = Quaternion.Euler(_headPitch, 0f, 0f);
    }

    private void Effects()
    {
        if (nativeFlames != null)
        {
            // FlamesActive re-anchors the effect every frame, so the jet follows the aimed barrels.
            if (_firing) nativeFlames.FlamesActive(Nozzle, head != null ? head.rotation : AimRotation());
            else if (nativeFlames.flamesActive) nativeFlames.FlamesInactive();
            return;
        }
        if (_flame == null) return;
        var nozzle = Nozzle;
        // The jet leaves along the barrels, which track the operator's aim.
        var rotation = head != null ? head.rotation : AimRotation();
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
        main.startColor = Color.white;
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
        // Walls and props only: never the flamethrower's own colliders, other items or players.
        collision.collidesWith = SemiFunc.LayerMaskGetVisionObstruct() &
            ~LayerMask.GetMask("PhysGrabObject", "PhysGrabObjectCart", "PhysGrabObjectHinge", "Player");
        collision.quality = ParticleSystemCollisionQuality.Low;
        var renderer = obj.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = material;
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        return ps;
    }

    /// <summary>
    /// Fire and smoke use copies of the game's own explosion particle materials. The previous
    /// Shader.Find("Legacy Shaders/Particles/...") path is not in the shipped build, so it fell back to a
    /// fresh, OPAQUE "Particles/Standard Unlit" material and every puff rendered as a hard orange quad.
    /// </summary>
    private static void EnsureMaterials()
    {
        if (_flameMaterial != null) return;
        Material fire = null, smoke = null;
        try
        {
            var prefab = Resources.Load<GameObject>("Effects/Part Prefab Explosion");
            var explosion = prefab != null ? prefab.GetComponent<ParticlePrefabExplosion>() : null;
            if (explosion != null)
            {
                fire = explosion.particleFire != null ? explosion.particleFire.GetComponent<ParticleSystemRenderer>()?.sharedMaterial : null;
                smoke = explosion.particleSmoke != null ? explosion.particleSmoke.GetComponent<ParticleSystemRenderer>()?.sharedMaterial : null;
            }
            // Prefer the soft flame-tongue texture over the explosion's speckled burst.
            if (NativeFireMaterial != null) fire = NativeFireMaterial;
        }
        catch (Exception e)
        {
            Plugin.Log.LogWarning($"[Flame] Native explosion materials unavailable: {e.Message}");
        }
        _flameMaterial = fire != null ? new Material(fire) : Transparent(additive: true);
        _smokeMaterial = smoke != null ? new Material(smoke) : Transparent(additive: false);
        _flameMaterial.name = "IHM Flame";
        _smokeMaterial.name = "IHM Flame Smoke";
        _flameMaterial.hideFlags = _smokeMaterial.hideFlags = HideFlags.HideAndDontSave;
        if (_flameMaterial.mainTexture == null) _flameMaterial.mainTexture = SoftPuff();
        if (_smokeMaterial.mainTexture == null) _smokeMaterial.mainTexture = SoftPuff();
        Plugin.Log.LogInfo($"[Flame] Flame materials: fire={(fire != null ? fire.name : "fallback")}, smoke={(smoke != null ? smoke.name : "fallback")}.");
    }

    // Fallback only: a correctly configured transparent Standard particle material.
    private static Material Transparent(bool additive)
    {
        var shader = Shader.Find("Particles/Standard Unlit") ?? Shader.Find("Sprites/Default");
        var m = new Material(shader) { mainTexture = SoftPuff() };
        if (m.HasProperty("_Mode")) m.SetFloat("_Mode", additive ? 4f : 2f);
        if (m.HasProperty("_SrcBlend")) m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        if (m.HasProperty("_DstBlend")) m.SetInt("_DstBlend", (int)(additive ? UnityEngine.Rendering.BlendMode.One : UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha));
        if (m.HasProperty("_ZWrite")) m.SetInt("_ZWrite", 0);
        m.DisableKeyword("_ALPHATEST_ON");
        m.EnableKeyword("_ALPHABLEND_ON");
        m.renderQueue = additive ? 3100 : 3050;
        return m;
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
                float a = Mathf.Clamp01(1f - d);
                a = Mathf.Clamp01(a * a * (1.2f - wisp));
                pixels[y * n + x] = new Color(1f, 1f, 1f, a);
            }
        tex.SetPixels(pixels);
        tex.Apply(false, true);
        return tex;
    }
}
