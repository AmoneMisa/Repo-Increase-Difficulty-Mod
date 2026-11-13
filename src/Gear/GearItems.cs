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
    Net,
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
                        $"Protects loot: a full charge absorbs ${UtilityGearRules.LootCapacity(ModConfig.DefenceItemAbsorb.Value):N0} of value loss across its field. Wider radius and slower idle drain.");
                if (ModConfig.ProtectionOrbEnabled.Value)
                    MakeOrb(orb, "Item Orb Protection", "Protection Orb", ShieldMode.ProtectionOrb, 11000f, 15000f, new Color(1f, 0.85f, 0.3f),
                        $"Players inside its wider field take {UtilityGearRules.Protection(ModConfig.ProtectionReductionPercent.Value):P0} less direct damage. Damage-over-time is unchanged.");
            }
            else
                Plugin.Log.LogWarning("[Gear] No vanilla orb found to clone; orbs disabled.");

            if (ModConfig.DefenceDroneEnabled.Value)
            {
                var drone = DroneItems.FindBaseDrone();
                if (drone != null)
                    DroneItems.Make(drone, "Item Drone Defence", "Defence Drone", DroneKind.Defence, 0, 11000f, 15000f, shop: 1,
                        new Color(0.3f, 0.6f, 1f),
                        $"Attach to a player: absorbs up to {UtilityGearRules.DefenceCapacity(ModConfig.DefenceAbsorb.Value)} direct damage per charge. At least 90 seconds of idle operation. Monster hits still deal at least 1 damage; damage-over-time bypasses the shield.",
                        targets: (false, false, true, false));
            }

            if (gun != null)
            {
                if (ModConfig.PoisonCrossbowEnabled.Value)
                    MakeGun(gun, "Item Gun Poison Crossbow", "Poison Crossbow", GearKind.Poison, 19000f, 24000f, new Color(0.4f, 1f, 0.3f),
                        $"Poison bolts: a little damage plus a 15 second poison worth {ModConfig.PoisonMaxHpPercent.Value}% of the monster's max health (at least {ModConfig.PoisonTotalDamage.Value}). A new bolt refreshes it and keeps half of what was left. The poisoned monster keeps hunting players. Fewer shots than a normal gun.");
                if (ModConfig.IcePistolEnabled.Value)
                    MakeGun(pulseGun ?? gun, "Item Gun Ice Pulse", "Pulse Ice Pistol", GearKind.Ice, 11000f, 15000f, new Color(0.5f, 0.85f, 1f),
                        $"Freezes monsters and players. Deals 20 damage to monsters on the first freeze; further shots extend it without extra freeze damage. Frozen monsters take {ModConfig.IceGunDamagePercent.Value}% firearm damage, +{ModConfig.IceMeleeBonusPercent.Value}% melee damage and +80% grenade damage. They cannot be lifted.");
                if (ModConfig.NetLauncherEnabled.Value)
                    MakeGun(gun, "Item Gun Net Launcher", "Net Launcher", GearKind.Net, 14000f, 18000f, new Color(1f, .78f, .2f),
                        $"Fires a weighted rope net that pins a monster to the floor for {ModConfig.NetSeconds.Value:0.#} seconds. It can still turn and attack anything in reach, and may tear free early. {ModConfig.NetShots.Value} nets per charge.");
                if (ModConfig.StickyPistolEnabled.Value)
                    MakeGun(gun, "Item Gun Sticky", "Sticky Pistol", GearKind.Sticky, 9000f, 10000f, new Color(1f, 0.5f, 0.9f),
                        "Glues monsters to the ground for 3 seconds and players for 2. Pulls loot you could lift with one strength level less; pulled loot takes 80% less damage.");
            }
            else
                Plugin.Log.LogWarning("[Gear] No vanilla gun found to clone; guns disabled.");

            if (gun != null && ModConfig.StaffEnabled.Value)
                MakeStaff(gun, FindBase<ItemGunRevolver>("Item Gun Revolver", _ => false), pulseGun);

            if (grenade != null && ModConfig.DecoyGrenadeEnabled.Value)
                MakeDecoy(grenade);
            if (grenade != null && ModConfig.NewGrenadesEnabled.Value)
            {
                bool ru = Localization.GameLanguage.Russian;
                MakeGrenade(grenade, "Item Grenade Smoke", ru ? "Дымовая граната" : "Smoke Grenade", GrenadeKind.Smoke, 1500f, 1900f, new Color(.55f, .56f, .6f),
                    ru ? $"Облако дыма радиусом {GrenadeRules.SmokeRadius:0.#} м на {GrenadeRules.SmokeSeconds:0} с. Монстры не видят игроков в дыму и сквозь него, если не стоят вплотную."
                       : $"A {GrenadeRules.SmokeRadius:0.#} m smoke cloud for {GrenadeRules.SmokeSeconds:0} s. Monsters cannot see players inside or through it unless they are right next to them.");
                MakeGrenade(grenade, "Item Grenade Cryo", ru ? "Крио-граната" : "Cryo Grenade", GrenadeKind.Cryo, 2400f, 3000f, new Color(.55f, .85f, 1f),
                    ru ? $"Замораживает всех монстров в радиусе {GrenadeRules.CryoRadius:0.#} м на {GrenadeRules.CryoSeconds:0} с, как ледяной пистолет. Игроков не задевает."
                       : $"Freezes every monster within {GrenadeRules.CryoRadius:0.#} m for {GrenadeRules.CryoSeconds:0} s, like the Ice Pistol. Players are unaffected.");
                MakeGrenade(grenade, "Item Grenade Glue", ru ? "Клеевая граната" : "Glue Grenade", GrenadeKind.Glue, 1600f, 2000f, new Color(1f, .5f, .9f),
                    ru ? $"Приклеивает к полу всех монстров в радиусе {GrenadeRules.GlueRadius:0.#} м на {StickyRoot.MonsterSeconds:0} с. Игроков не задевает."
                       : $"Glues every monster within {GrenadeRules.GlueRadius:0.#} m to the floor for {StickyRoot.MonsterSeconds:0} s. Players are unaffected.");
            }

            if (ModConfig.ConsumablesEnabled.Value)
                MakeConsumables();

            FlameCartItem.Build();
            CustomCarts.Build();
            if (StatsManager.instance.itemDictionary.TryGetValue("Item Melee Sword", out var sword) && sword?.prefab?.Prefab != null)
            {
                const float premium = 1.35f / CustomItemPrices.Multiplier;
                bool russian = Localization.GameLanguage.Russian;
                var (_, go, attributes) = Clone(sword, Katana.Asset, russian ? "Катана" : "Katana", sword.value.valueMin * premium,
                    sword.value.valueMax * premium, new Color(.62f, .45f, .24f), russian
                        ? $"Точный удар на {Katana.Damage} урона по монстрам. Узкая зона удара, пауза между взмахами и быстрый износ."
                        : $"A precise {Katana.Damage}-damage cut against monsters. Narrow strikes, recovery between swings and fast wear.",
                    _ => false, tint: false);
                Katana.Configure(go);
                Register(attributes, Katana.Asset);
            }
            else Plugin.Log.LogWarning("[Gear] Vanilla sword not found; Katana disabled.");
            if (ModConfig.SickleEnabled.Value && StatsManager.instance.itemDictionary.TryGetValue("Item Melee Sword", out var blade) && blade?.prefab?.Prefab != null)
            {
                bool ru = Localization.GameLanguage.Russian;
                const float factor = 1.1f / CustomItemPrices.Multiplier;
                var (_, go, attributes) = Clone(blade, SickleRules.Asset, ru ? "Серп-бумеранг" : "Boomerang Sickle", blade.value.valueMin * factor,
                    blade.value.valueMax * factor, new Color(.75f, .8f, .55f), ru
                        ? $"Бьёт как меч. Брошенный серп вращается, наносит {SickleRules.ThrowDamage} урона каждому монстру на пути и возвращается к вам."
                        : $"Swings like a sword. Thrown, it spins, cuts every monster in its path for {SickleRules.ThrowDamage} damage and flies back to you.",
                    _ => false, tint: true);
                go.AddComponent<BoomerangSickle>();
                BoomerangSickle.BuildModel(go);
                Register(attributes, SickleRules.Asset);
            }
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
        OneBarPerShot(gun, go.GetComponent<ItemBattery>(), name, kind switch
        {
            GearKind.Net => Mathf.Max(1, ModConfig.NetShots.Value),
            GearKind.Poison => -2, // a third fewer shots than the donor
            _ => -1,               // as many shots as the donor
        });
        if (kind == GearKind.Net)
            gun.gunRecoilForce *= .6f;
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
                    hurt.enemyDamage = 0;
                    hurt.enemyKill = false;
                    hurt.enemyStun = false;
                    hurt.playerLogic = false;
                    hurt.playerDamage = 0;
                    hurt.playerKill = false;
                }
                if (kind == GearKind.Poison)
                {
                    PoisonBoltDamage.Configure(hurt, ModConfig.PoisonHitDamage.Value);
                }
                else if (kind == GearKind.Net)
                {
                    // A net does no harm: it only pins the monster (GearEffects).
                    hurt.playerLogic = false;
                    hurt.playerDamage = 0;
                    hurt.playerKill = false;
                    hurt.enemyDamage = 0;
                    hurt.enemyKill = false;
                    hurt.enemyStun = false;
                    NoKnockback(hurt);
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

        var marker = go.AddComponent<GearGun>();
        marker.kind = kind;
        ItemArt.Weapon(go, kind.ToString().ToLowerInvariant());
        ItemAudio.Attach(go, kind.ToString().ToLowerInvariant());
        Register(attributes, name);
    }

    /// <summary>
    /// Every mod gun spends exactly one battery bar per shot and shows one bar per shot left. A donor that drains a
    /// percentage per shot (the handgun) showed bars that did not match the charge, and stopped firing with up to half a
    /// bar of charge left. shots: a fixed count, -1 = the donor's own shots per charge, -2 = a third fewer (Poison).
    /// </summary>
    private static void OneBarPerShot(ItemGun gun, ItemBattery battery, string name, int shots)
    {
        if (gun == null || battery == null) return;
        float donor = UtilityGearRules.ShotsPerCharge(gun.hasOneShot, gun.batteryDrainFullBar, gun.batteryDrainFullBars,
            battery.batteryBars, gun.batteryDrain);
        int count = shots > 0 ? shots : shots == -2 ? UtilityGearRules.PoisonShots(donor) : UtilityGearRules.WholeShots(donor);
        gun.batteryDrainFullBar = true;
        gun.batteryDrainFullBars = 1;
        battery.batteryBars = Mathf.Max(1, count);
        Plugin.Log.LogInfo($"[Gear] {name}: {battery.batteryBars} shot(s) per charge, one battery bar each (donor {donor:0.##}).");
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
        // No gun recoil: the native kick pushes the tip back and up. The staff swings forward and down
        // like a melee strike instead (WizardStaff.Strike).
        gun.gunRecoilForce = 0f;
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

    private static void MakeGrenade(Item baseItem, string asset, string display, GrenadeKind kind, float priceMin, float priceMax, Color color, string desc)
    {
        var (_, go, attributes) = Clone(baseItem, asset, display, priceMin, priceMax, color, desc,
            mb => mb.GetType().Name.StartsWith("ItemGrenade") && mb.GetType().Name != "ItemGrenade", tint: true);
        go.AddComponent<ModGrenade>().kind = kind;
        ItemArt.Weapon(go, kind.ToString().ToLowerInvariant()); // Blender model; arming animation plays on isActive
        Register(attributes, asset);
    }

    private static void MakeConsumables()
    {
        if (!StatsManager.instance.itemDictionary.TryGetValue("Item Health Pack Small", out var pack) || pack?.prefab?.Prefab == null ||
            pack.prefab.Prefab.GetComponent<ItemHealthPack>() == null)
        {
            Plugin.Log.LogWarning("[Gear] Small health pack not found; consumables disabled.");
            return;
        }
        bool ru = Localization.GameLanguage.Russian;
        MakeConsumable(pack, "Item Health Pack Antidote", ru ? "Антидот" : "Antidote", ConsumableKind.Antidote, 1f, new Color(.45f, 1f, .4f),
            ru ? $"Снимает горение, кровотечение, яд и замедление и защищает от горения, кровотечения и яда на {ConsumableRules.AntidoteImmunitySeconds:0} с."
               : $"Ends burning, bleeding, venom and slowness, and keeps burning, bleeding and venom away for {ConsumableRules.AntidoteImmunitySeconds:0} s.");
        MakeConsumable(pack, "Item Health Pack Adrenaline", ru ? "Шприц адреналина" : "Adrenaline Shot", ConsumableKind.Adrenaline, 1.3f, new Color(1f, .35f, .3f),
            ru ? $"Вырывает из любого захвата монстра (левитация, удушение, окаменение, хватка, язык Похитителя, левитация Менталиста; держащего монстра ненадолго оглушает), затем {ConsumableRules.AdrenalineSeconds:0} с +30% скорости и полная выносливость."
               : $"Breaks any monster hold (levitation, constriction, petrification, a grip, the Snatcher's tongue, the Mentalist's levitation; the holder is briefly stunned), then +30% speed and a full stamina bar for {ConsumableRules.AdrenalineSeconds:0} s.");
        MakeConsumable(pack, "Item Health Pack Repair Kit", ru ? "Ремкомплект" : "Repair Kit", ConsumableKind.RepairKit, 1.5f, new Color(1f, .75f, .3f),
            ru ? $"Возвращает половину потерянной ценности самому повреждённому луту в радиусе {ConsumableRules.RepairRange:0} м. Без повреждённого лута рядом не срабатывает."
               : $"Gives the most damaged valuable within {ConsumableRules.RepairRange:0} m back half of the value it lost. Refused when nothing nearby is damaged.");
    }

    private static void MakeConsumable(Item pack, string asset, string display, ConsumableKind kind, float priceFactor, Color color, string desc)
    {
        float min = pack.value.valueMin * priceFactor / CustomItemPrices.Multiplier;
        float max = pack.value.valueMax * priceFactor / CustomItemPrices.Multiplier;
        var (item, go, attributes) = Clone(pack, asset, display, min, max, color, desc, _ => false, tint: true);
        item.maxAmountInShop = 2;
        var health = go.GetComponent<ItemHealthPack>();
        health.healAmount = 0;
        go.AddComponent<ModConsumable>().kind = kind;
        string key = kind switch { ConsumableKind.Antidote => "antidote", ConsumableKind.Adrenaline => "adrenaline", _ => "repairkit" };
        ItemArt.Weapon(go, key); // Blender model; the use animation plays from UsedRPC on every client
        try { health.soundUse = ItemAudio.Cue(kind == ConsumableKind.RepairKit ? "repair_use" : key + "_use", .5f); }
        catch (Exception e) { Plugin.Log.LogWarning($"[Audio] Keeping the health pack sound for {asset}: {e.Message}"); }
        Register(attributes, asset);
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
/// <c>DecoySeconds</c>, which sends nearby monsters to investigate it. The vanilla fuse is held far out while it
/// lures; when the time is up the fuse is set to zero so the game's own detonation runs (the explosion parts are
/// stripped). That path makes it one-use like any grenade: it is removed from the run's purchased items and
/// destroyed, or, in the shop, put back on its shelf. Putting it in an inventory slot disarms it, as with vanilla.
/// </summary>
internal sealed class DecoyGrenade : MonoBehaviour
{
    private const float HeldFuse = 9999f;
    private ItemGrenade _grenade;
    private float _armedFor = -1f;
    private float _ping;

    private void Start()
    {
        _grenade = GetComponent<ItemGrenade>();
        if (_grenade == null) return;
        _grenade.tickTime = HeldFuse;
        // The decoy only borrows the vanilla detonation for its removal: no stun or blast may run off it.
        _grenade.onDetonate = new UnityEngine.Events.UnityEvent();
    }

    private void Update()
    {
        if (_grenade == null || !SemiFunc.IsMasterClientOrSingleplayer())
            return;

        if (!_grenade.isActive)
        {
            // Not armed, disarmed by an inventory slot, or reset on the shop shelf after going off: ready again.
            _armedFor = -1f;
            _grenade.tickTime = HeldFuse;
            return;
        }
        if (_armedFor < 0f)
            _armedFor = 0f;
        int rank = Skills.SkillEffects.Level(_grenade.activatorPlayerAvatar, Skills.SkillCatalog.DecoyGrenade);
        float duration = ModConfig.DecoySeconds.Value + rank;
        if (_armedFor >= duration)
            return; // fuse already released; the game's detonation is under way

        _armedFor += Time.deltaTime;
        _ping -= Time.deltaTime;
        if (_ping <= 0f)
        {
            _ping = 0.4f;
            EnemyDirector.instance.SetInvestigate(transform.position, 40f * Skills.PerkRules.GrenadeRadius(rank));
        }

        // Time is up: release the fuse so the vanilla grenade goes off this frame (consumed, or reset in the shop).
        if (_armedFor >= duration)
            _grenade.tickTime = 0f;
    }
}
