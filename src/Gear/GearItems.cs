using System;
using System.Collections.Generic;
using IncreaseHardMod.Drones;
using IncreaseHardMod.Visuals;
using REPOLib.Modules;
using UnityEngine;
using Object = UnityEngine.Object;

namespace IncreaseHardMod.Gear;

internal enum GearKind
{
    Poison,
    Ice,
    Sticky,
    Lightning,
}

internal enum ShieldMode
{
    DefenceOrb,
    ProtectionOrb,
    DefenceDrone,
}

/// <summary>
/// Registers the extra shop gear with embedded Blender visuals. Every item is
/// a clone of a vanilla item (orb / gun / grenade / drone) with the vanilla effect scripts stripped and our
/// own component and visual layer added, then handed to REPOLib. Built once from the drones' RunStartStats hook.
/// </summary>
internal static class GearItems
{
    private static bool _built;

    public static void BuildOnce()
    {
        if (_built || !ModConfig.GearEnabled.Value)
            return;
        _built = true;
        try
        {
            DroneItems.EnsureHolder();

            // Bases are picked by their vanilla asset name (falling back to any item with the right component).
            var orb = FindBase<ItemOrb>("Item Orb Indestructible", _ => false);
            var gun = FindBase<ItemGun>("Item Gun Handgun", p => p.GetComponent<ItemGunTranq>() != null || p.GetComponent<ItemGunRevolver>() != null);
            var pulseGun = FindBase<ItemGun>("Item Gun Shockwave", _ => false); // the pink pulse pistol that repels
            var grenade = FindBase<ItemGrenade>("Item Grenade Stun", _ => false);

            if (orb != null)
            {
                if (ModConfig.DefenceOrbEnabled.Value)
                    MakeOrb(orb, "Item Orb Defence", "Defence Orb", ShieldMode.DefenceOrb, 19000f, 24000f, new Color(0.3f, 0.6f, 1f),
                        "Protects loot: value that valuables would lose from breaking inside its field is absorbed while it is charged.");
                if (ModConfig.ProtectionOrbEnabled.Value)
                    MakeOrb(orb, "Item Orb Protection", "Protection Orb", ShieldMode.ProtectionOrb, 11000f, 15000f, new Color(1f, 0.85f, 0.3f),
                        "Players inside its field take 25% less damage. Drains its charge quickly.");
            }
            else
                Plugin.Log.LogWarning("[Gear] No vanilla orb found to clone; orbs disabled.");

            if (ModConfig.DefenceDroneEnabled.Value)
            {
                var drone = DroneItems.FindBaseDrone();
                if (drone != null)
                    DroneItems.Make(drone, "Item Drone Defence", "Defence Drone", DroneKind.Defence, 0, 11000f, 15000f, shop: 1,
                        new Color(0.3f, 0.6f, 1f),
                        "Attach to a player: absorbs damage that player takes while charged (about 350 damage in total). Players only.",
                        targets: (false, false, true, false));
            }

            if (gun != null)
            {
                if (ModConfig.PoisonCrossbowEnabled.Value)
                    MakeGun(gun, "Item Gun Poison Crossbow", "Poison Crossbow", GearKind.Poison, 19000f, 24000f, new Color(0.4f, 1f, 0.3f),
                        "Poison bolts: a little damage plus a 15 second poison that refreshes (never stacks) and keeps the monster hunting players. Fewer shots than a normal gun.");
                if (ModConfig.IcePistolEnabled.Value)
                    MakeGun(pulseGun ?? gun, "Item Gun Ice Pulse", "Pulse Ice Pistol", GearKind.Ice, 11000f, 15000f, new Color(0.5f, 0.85f, 1f),
                        "Freezes monsters and players with visible ice. Players take no shot damage; works in the shop too. Frozen monsters cannot be lifted, take less firearm damage and 35% more melee damage.");
                if (ModConfig.StickyPistolEnabled.Value)
                    MakeGun(gun, "Item Gun Sticky", "Sticky Pistol", GearKind.Sticky, 9000f, 10000f, new Color(1f, 0.5f, 0.9f),
                        "Pulls a monster or object toward you without dealing damage, if you could lift it with one strength level less.");
            }
            else
                Plugin.Log.LogWarning("[Gear] No vanilla gun found to clone; guns disabled.");

            if (gun != null && ModConfig.StaffEnabled.Value)
                MakeStaff(gun, FindBase<ItemGunRevolver>("Item Gun Revolver", _ => false), pulseGun);

            if (grenade != null && ModConfig.DecoyGrenadeEnabled.Value)
                MakeDecoy(grenade);

            FlameCartItem.Build();
        }
        catch (Exception e)
        {
            Plugin.Log.LogError($"[Gear] Failed to build gear items: {e}");
        }
    }

    private static Item FindBase<T>(string preferredName, Func<GameObject, bool> reject) where T : Component
    {
        if (StatsManager.instance.itemDictionary.TryGetValue(preferredName, out var named) &&
            named.prefab != null && named.prefab.Prefab != null && named.prefab.Prefab.GetComponent<T>() != null)
            return named;
        var names = new List<string>(StatsManager.instance.itemDictionary.Keys);
        names.Sort(StringComparer.Ordinal);
        Item fallback = null;
        foreach (var n in names)
        {
            var item = StatsManager.instance.itemDictionary[n];
            var prefab = item != null && item.prefab != null ? item.prefab.Prefab : null;
            if (prefab == null || prefab.GetComponent<T>() == null)
                continue;
            fallback ??= item;
            if (!reject(prefab))
                return item;
        }
        return fallback;
    }

    // Clones the Item asset + prefab, strips vanilla components chosen by `strip`, and tints it.
    private static (Item item, GameObject go, ItemAttributes attributes) Clone(Item baseItem, string assetName,
        string display, float priceMin, float priceMax, Color color, string description, Func<MonoBehaviour, bool> strip, bool tint)
    {
        var item = Object.Instantiate(baseItem);
        item.name = assetName;
        item.itemName = display;
        item.itemNameLocalized = null;
        item.description = description;
        item.maxAmount = 3;
        item.maxAmountInShop = 1;
        item.maxPurchase = false;
        item.minPlayerCount = 1;
        item.value = Object.Instantiate(baseItem.value);
        item.value.valueMin = priceMin * CustomItemPrices.Multiplier;
        item.value.valueMax = priceMax * CustomItemPrices.Multiplier;
        var preset = ScriptableObject.CreateInstance<ColorPresets>();
        preset.colorMain = color;
        preset.colorLight = Color.Lerp(color, Color.white, 0.5f);
        preset.colorDark = Color.Lerp(color, Color.black, 0.5f);
        item.colorPreset = preset;

        var go = Object.Instantiate(baseItem.prefab.Prefab, DroneItems.EnsureHolder().transform);
        go.name = assetName;
        foreach (var mb in go.GetComponents<MonoBehaviour>())
            if (mb != null && strip(mb))
                Object.DestroyImmediate(mb);

        if (tint)
            Tint(go, color);

        var attributes = go.GetComponent<ItemAttributes>();
        attributes.item = item;
        return (item, go, attributes);
    }

    private static void Tint(GameObject go, Color color)
    {
        foreach (var r in go.GetComponentsInChildren<Renderer>(true))
        {
            if (r is not (MeshRenderer or SkinnedMeshRenderer))
                continue;
            foreach (var m in r.materials)
            {
                if (m.HasProperty("_BaseColor"))
                    m.SetColor("_BaseColor", Color.Lerp(m.GetColor("_BaseColor"), color, 0.45f));
                else if (m.HasProperty("_Color"))
                    m.SetColor("_Color", Color.Lerp(m.color, color, 0.45f));
            }
        }
    }

    private static void Register(ItemAttributes attributes, string name)
    {
        InventoryItemVisuals.Prepare(attributes.gameObject);
        HeroAura.Mark(attributes.gameObject);
        if (Items.RegisterItem(attributes) == null)
            Plugin.Log.LogWarning($"[Gear] REPOLib refused to register {name}.");
        else
            Plugin.Log.LogInfo($"[Gear] Registered {name}.");
    }

    private static void MakeOrb(Item baseItem, string name, string display, ShieldMode mode, float priceMin, float priceMax, Color color, string desc)
    {
        var (_, go, attributes) = Clone(baseItem, name, display, priceMin, priceMax, color, desc,
            mb => mb.GetType().Name.StartsWith("ItemOrb") && mb.GetType().Name != "ItemOrb", tint: false);
        go.AddComponent<ItemHover>();
        var orb = go.GetComponent<ItemOrb>();
        orb.targetValuables = orb.targetNonValuables = orb.targetEnemies = false;
        orb.targetPlayers = true;
        var shield = go.AddComponent<ShieldItem>();
        shield.mode = mode;
        ItemArt.Skin(go, mode == ShieldMode.DefenceOrb ? "defence" : "protection", orb: true);
        ItemAudio.Attach(go, mode == ShieldMode.DefenceOrb ? "defence" : "protection");
        Register(attributes, name);
    }

    private static void MakeGun(Item baseItem, string name, string display, GearKind kind, float priceMin, float priceMax, Color color, string desc)
    {
        var (_, go, attributes) = Clone(baseItem, name, display, priceMin, priceMax, color, desc,
            mb => mb.GetType().Name == "ItemGunTranq", tint: true);

        var gun = go.GetComponent<ItemGun>();
        gun.numberOfBullets = 1;
        gun.gunRandomSpread = 0f;
        if (kind == GearKind.Sticky)
        {
            gun.gunRecoilForce = 0f;
            gun.cameraShakeMultiplier = .1f;
            gun.torqueMultiplier = .15f;
        }

        // The bullet prefab is instantiated locally on every client, so a private clone is enough.
        if (gun.bulletPrefab != null)
        {
            var bullet = Object.Instantiate(gun.bulletPrefab, DroneItems.EnsureHolder().transform);
            bullet.name = name + " Bullet";
            foreach (var hurt in bullet.GetComponentsInChildren<HurtCollider>(true))
            {
                if (kind == GearKind.Ice)
                {
                    NoKnockback(hurt);
                    hurt.playerLogic = false;
                    hurt.playerDamage = 0;
                    hurt.playerKill = false;
                }
                if (kind == GearKind.Poison)
                {
                    PoisonBoltDamage.Configure(hurt, ModConfig.PoisonHitDamage.Value);
                }
                else if (kind == GearKind.Sticky)
                {
                    hurt.playerLogic = hurt.physLogic = hurt.enemyLogic = false;
                    hurt.playerKill = hurt.physDestroy = hurt.physHingeDestroy = hurt.enemyKill = false;
                    hurt.playerDamage = 0;
                    NoKnockback(hurt);
                    hurt.enemyDamage = 0;
                    hurt.enemyStun = false;
                }
            }
            gun.bulletPrefab = bullet;
        }

        if (kind == GearKind.Poison)
        {
            // A third fewer shots from the same battery = each shot costs 1.5x as much.
            if (gun.batteryDrainFullBar)
                gun.batteryDrainFullBars = Mathf.Max(1, Mathf.RoundToInt(gun.batteryDrainFullBars * 1.5f));
            else
                gun.batteryDrain *= 1.5f;
        }

        var marker = go.AddComponent<GearGun>();
        marker.kind = kind;
        ItemArt.Weapon(go, kind.ToString().ToLowerInvariant());
        ItemAudio.Attach(go, kind.ToString().ToLowerInvariant());
        Register(attributes, name);
    }

    /// <summary>
    /// Wizard Staff: handgun base (native holding, aiming, trigger and battery), revolver price, one battery
    /// bar per bolt. The native bullet is replaced by <see cref="WizardStaff"/>'s chain lightning.
    /// </summary>
    private static void MakeStaff(Item baseItem, Item priceItem, Item beamItem)
    {
        var (item, go, attributes) = Clone(baseItem, "Item Gun Wizard Staff", "Wizard Staff", 0f, 0f, new Color(.45f, .65f, 1f),
            $"A carved wand that throws chain lightning: {ModConfig.StaffDamage.Value} damage to the first monster within " +
            $"{ModConfig.StaffRange.Value:0.#} m, jumping to up to {Mathf.Max(1, ModConfig.StaffMaxTargets.Value)} monsters in total. " +
            $"{ModConfig.StaffCharges.Value} bolts per charge.",
            mb => mb.GetType().Name is "ItemGunTranq" or "ItemGunRevolver", tint: false);
        // Same shop price as the vanilla revolver.
        if (priceItem != null && priceItem.value != null)
            item.value = Object.Instantiate(priceItem.value);
        else
        {
            item.value.valueMin = 9000f * CustomItemPrices.Multiplier;
            item.value.valueMax = 12000f * CustomItemPrices.Multiplier;
            Plugin.Log.LogWarning("[Gear] Vanilla revolver not found; Wizard Staff uses a fallback price.");
        }

        var gun = go.GetComponent<ItemGun>();
        gun.numberOfBullets = 1;
        gun.gunRandomSpread = 0f;
        gun.gunRange = ModConfig.StaffRange.Value;
        gun.gunRecoilForce *= .35f;
        gun.cameraShakeMultiplier = .35f;
        gun.batteryDrainFullBar = true;
        gun.batteryDrainFullBars = 1;
        var battery = go.GetComponent<ItemBattery>();
        battery.batteryBars = Mathf.Max(1, ModConfig.StaffCharges.Value);

        // The wand is longer than the pistol: its tip (the muzzle) sits further out and the carved crystal
        // handle lies in the hand. Reshape the two grab colliders to the wand.
        if (gun.gunMuzzle != null) gun.gunMuzzle.localPosition = new Vector3(0f, -.03f, .52f);
        var colliders = go.transform.Find("Colliders");
        var body = colliders != null ? colliders.Find("Semi Box Collider") : null;
        var handle = colliders != null ? colliders.Find("Semi Box Collider (1)") : null;
        if (body != null)
        {
            body.localPosition = new Vector3(0f, -.03f, .20f);
            body.localRotation = Quaternion.identity;
            body.localScale = new Vector3(.05f, .05f, .64f);
        }
        if (handle != null)
        {
            handle.localPosition = new Vector3(0f, -.03f, -.055f);
            handle.localRotation = Quaternion.identity;
            handle.localScale = new Vector3(.06f, .06f, .14f);
        }
        if (body == null || handle == null)
            Plugin.Log.LogWarning("[Gear] Handgun collider layout changed; Wizard Staff keeps the pistol's grab shape.");

        // Native additive beam material from the pulse pistol's tracer, for the bolts.
        var beam = beamItem?.prefab?.Prefab != null ? beamItem.prefab.Prefab.GetComponent<ItemGun>() : null;
        var beamLine = beam != null && beam.bulletPrefab != null ? beam.bulletPrefab.GetComponentInChildren<LineRenderer>(true) : null;
        beamLine ??= gun.bulletPrefab != null ? gun.bulletPrefab.GetComponentInChildren<LineRenderer>(true) : null;
        WizardStaff.UseNativeBeam(beamLine != null ? beamLine.sharedMaterial : null);

        go.AddComponent<GearGun>().kind = GearKind.Lightning;
        go.AddComponent<WizardStaff>();
        ItemArt.Weapon(go, "staff");
        ItemAudio.Attach(go, "lightning");
        Register(attributes, "Item Gun Wizard Staff");
    }

    // The Ice Pistol borrows the pink pulse gun, but must not push anything: strip every force/stun from its hits.
    private static void NoKnockback(HurtCollider h)
    {
        h.playerHitForce = 0f;
        h.playerTumbleForce = 0f;
        h.playerTumbleTorque = 0f;
        h.playerTumbleTime = 0f;
        h.playerStunTime = 0f;
        h.physHitForce = 0f;
        h.physHitTorque = 0f;
        h.enemyHitForce = 0f;
        h.enemyHitTorque = 0f;
        h.enemyStun = false;
        h.enemyStunTime = 0f;
        h.horizontalVerticalHitForce = Vector2.zero;
    }

    private static void MakeDecoy(Item baseItem)
    {
        var (_, go, attributes) = Clone(baseItem, "Item Grenade Decoy", "Decoy Grenade", 1000f, 1300f, new Color(1f, 0.6f, 0.2f),
            "Once armed it lures nearby monsters to itself for a few seconds, then disappears.",
            mb => mb.GetType().Name.StartsWith("ItemGrenade") && mb.GetType().Name != "ItemGrenade", tint: true);
        go.AddComponent<DecoyGrenade>();
        ItemArt.Weapon(go, "decoy");
        ItemAudio.Attach(go, "decoy");
        Register(attributes, "Item Grenade Decoy");
    }
}

/// <summary>Marks a cloned gun with its special behaviour (see <see cref="GearEffects"/>).</summary>
internal sealed class GearGun : MonoBehaviour
{
    public GearKind kind;
}

/// <summary>
/// Decoy grenade: from the moment it is armed it keeps pinging the enemy director at its position for
/// <c>DecoySeconds</c>, which sends nearby monsters to investigate it, then removes itself. The vanilla
/// fuse is pushed far out so the (stripped) explosion logic never fires.
/// </summary>
internal sealed class DecoyGrenade : MonoBehaviour
{
    private ItemGrenade _grenade;
    private float _armedFor = -1f;
    private float _ping;

    private void Start()
    {
        _grenade = GetComponent<ItemGrenade>();
        if (_grenade != null)
            _grenade.tickTime = 9999f;
    }

    private void Update()
    {
        if (_grenade == null || !SemiFunc.IsMasterClientOrSingleplayer())
            return;

        if (_grenade.isActive && _armedFor < 0f)
            _armedFor = 0f;
        if (_armedFor < 0f)
            return;

        _armedFor += Time.deltaTime;
        _ping -= Time.deltaTime;
        if (_ping <= 0f)
        {
            _ping = 0.4f;
            EnemyDirector.instance.SetInvestigate(transform.position, 40f);
        }

        if (_armedFor >= ModConfig.DecoySeconds.Value)
        {
            var pgo = GetComponent<PhysGrabObject>();
            if (pgo != null)
                pgo.DestroyPhysGrabObject();
            else
                Destroy(gameObject);
        }
    }
}
