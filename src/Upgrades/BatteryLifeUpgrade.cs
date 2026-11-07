using System.Runtime.CompilerServices;
using HarmonyLib;
using IncreaseHardMod.Gear;
using UnityEngine;
using RepoUpgrades = REPOLib.Modules.Upgrades;

namespace IncreaseHardMod.Upgrades;

/// <summary>
/// "Battery Life" upgrades — separate per-player upgrades for each gun type
/// (Bullet / Laser / Tranq) and one for drones. Each level makes the item's battery last
/// longer, up to +100% (double the usage).
///
/// Guns drain their battery from one place (ItemGun.ShootRPC), but drones drain from ~5
/// different components with no shared choke point. Shot/full-bar hooks settle exact fractional
/// costs immediately; a host-side watchdog on <c>ItemBattery.Update</c> watches other
/// changes in each battery's <c>batteryLife</c> and, whenever it drops (real drain, not charging),
/// refunds a fraction back based on the USING player's upgrade level for that battery's
/// category. This covers every current and future drain source automatically.
///
/// Why host-only: battery drain is authoritative on the host (guns drain under
/// IsMasterClientOrSingleplayer; drones bail out on non-host), and the host broadcasts bar
/// changes to everyone. Refunding on the host therefore stays consistent — refunding on
/// each client would multiply the effect.
///
/// "+X% battery life" means X% more usage, so the drain multiplier is 1/(1+X): at +100%
/// the battery drains half as fast (lasts twice as long).
/// </summary>
internal static class BatteryLifeUpgrade
{
    public const string BulletId = "IHM_GunBatteryBullet";
    public const string LaserId = "IHM_GunBatteryLaser";
    public const string TranqId = "IHM_GunBatteryTranq";
    public const string DroneId = "IHM_DroneBattery";

    // Other: every remaining battery item (trackers, melee, cart weapons...), slowed by the Battery Saver perk.
    private enum Category { None, GunBullet, GunLaser, GunTranq, Drone, Other }

    // Per-battery state: last observed batteryLife + cached category (component lookups are
    // not free, and an item's category never changes). ConditionalWeakTable so destroyed
    // batteries are collected without us leaking references.
    private sealed class State
    {
        public float PrevLife = float.NaN;
        public Category Category = (Category)(-1); // -1 = not yet resolved
        public int BaseBars = -1;       // the gun's native bar count, captured before we add any
        public int Level;               // last known upgrade level of the gun's user
        public float BarsCheckTimer;
    }

    private static readonly ConditionalWeakTable<ItemBattery, State> _states = new();

    public static void Register()
    {
        if (!ModConfig.PlayerUpgradesEnabled.Value)
            return;

        // Distinct front artwork and matching inventory renders for each weapon category.
        DefineGun(BulletId, "Bullet Gun Extra Shots");
        DefineGun(LaserId, "Laser Gun Extra Shots");
        DefineGun(TranqId, "Tranq Gun Extra Shots");

        PlayerUpgradeRegistry.Define(new PlayerUpgradeRegistry.Definition
        {
            Id = DroneId,
            DisplayName = "Drone Battery Life",
            MaxLevel = EffectiveMaxLevel(true),
        });
    }

    private static void DefineGun(string id, string name)
    {
        PlayerUpgradeRegistry.Define(new PlayerUpgradeRegistry.Definition
        {
            Id = id,
            DisplayName = name,
            MaxLevel = EffectiveMaxLevel(false),
        });
    }

    internal static int EffectiveMaxLevel(bool drone)
    {
        if (!drone) return Mathf.Clamp(ModConfig.GunExtraBulletsMaxLevel.Value, 1, 100);
        float percent = Mathf.Clamp(ModConfig.DroneBatteryPercentPerLevel.Value, .01f, .5f);
        int configured = ModConfig.DroneBatteryMaxLevel.Value;
        return Mathf.Clamp(configured, 1, Mathf.CeilToInt(1f / percent));
    }

    private static Category Resolve(ItemBattery battery)
    {
        var go = battery.gameObject;
        if (go.GetComponent<ItemDrone>() != null)
            return Category.Drone;
        if (go.GetComponent<ItemGun>() != null)
        {
            // Spritz is built on the tranq gun but is a single-use dose, not a gun: no Extra Shots, no extra bars.
            if (go.GetComponent<SpritzDose>() != null)
                return Category.None;
            // This mod's gear guns: the clone step strips ItemGunTranq, so map them by their marker.
            var gear = go.GetComponent<GearGun>();
            if (gear != null)
                return gear.kind == GearKind.Lightning ? Category.GunLaser : Category.GunTranq; // Wizard Staff = laser; Ice/Poison/Net/Sticky = tranq
            if (go.GetComponent<ItemGunTranq>() != null)
                return Category.GunTranq;
            if (go.GetComponent<ItemGunLaser>() != null)
                return Category.GunLaser;
            // Energy pistols without a laser script: Pulse Pistol ("Item Gun Shockwave") and Boltzap ("Item Gun Stun").
            string asset = go.GetComponent<ItemAttributes>()?.item?.name ?? go.name;
            if (asset.Contains("Gun Shockwave") || asset.Contains("Gun Stun"))
                return Category.GunLaser;
            return Category.GunBullet; // any other gun defaults to the bullet catch-all
        }
        return Category.Other;
    }

    // The HUD/world battery widget draws one segment per ItemBattery.batteryBars, so extra shots must add
    // real bars or the upgrade is invisible. Upper bound keeps very high configured levels readable.
    private const int MaxGunBars = 40;

    private static int BaseBars(ItemBattery battery, State state)
    {
        if (state.BaseBars < 1) state.BaseBars = Mathf.Max(1, battery.batteryBars);
        return state.BaseBars;
    }

    // Shots one full battery gives a gun, before the upgrade: whole bars for bar-per-shot guns, else 100 / drain.
    private static float BaseShots(ItemGun gun, int baseBars)
    {
        float shots = gun.batteryDrainFullBar
            ? baseBars / (float)Mathf.Max(1, gun.batteryDrainFullBars)
            : 100f / Mathf.Max(0.01f, gun.batteryDrain);
        return Mathf.Max(1f, shots);
    }

    // The vanilla Handgun is the reference gun: it gets exactly GunExtraBulletsPerLevel extra shots per level.
    private const string ReferenceGun = "Item Gun Handgun";
    private static float _referenceShots = -1f;
    private static bool _referenceMissing;

    /// <summary>Shots one full battery gives the vanilla Handgun, read from its prefab (0 = unknown yet).</summary>
    private static float ReferenceShots()
    {
        if (_referenceShots > 0f || _referenceMissing) return Mathf.Max(0f, _referenceShots);
        var stats = StatsManager.instance;
        if (stats == null || stats.itemDictionary == null || stats.itemDictionary.Count == 0) return 0f; // not loaded yet, retry later
        try
        {
            if (stats.itemDictionary.TryGetValue(ReferenceGun, out var item) && item != null && item.prefab != null && item.prefab.IsValid())
            {
                var prefab = item.prefab.Prefab;
                var gun = prefab != null ? prefab.GetComponentInChildren<ItemGun>(true) : null;
                var battery = prefab != null ? prefab.GetComponentInChildren<ItemBattery>(true) : null;
                if (gun != null && battery != null)
                {
                    _referenceShots = BaseShots(gun, Mathf.Max(1, battery.batteryBars));
                    Plugin.Log?.LogInfo($"[Upgrades] Gun Extra Shots reference: {ReferenceGun} fires {_referenceShots:0.##} shot(s) per charge.");
                    return _referenceShots;
                }
            }
        }
        catch (System.Exception error) { Plugin.Log?.LogWarning("[Upgrades] Gun Extra Shots reference lookup failed: " + error.Message); }
        _referenceMissing = true;
        Plugin.Log?.LogWarning($"[Upgrades] {ReferenceGun} not found; Gun Extra Shots gives every gun the flat per-level bonus.");
        return 0f;
    }

    // Extra shots scale with the gun's own capacity: the Handgun gets GunExtraBulletsPerLevel per level, and every
    // other gun gets the same share of its own battery, so a gun with fewer shots per charge (the shotgun) gets fewer
    // and one with more gets more. Every gun therefore gains the same percentage. Fractional shots are settled by
    // the cheaper drain below. Without a reference (Handgun missing) it falls back to the flat bonus.
    private static float ExtraShots(int level, float baseShots)
    {
        float flat = level * Mathf.Max(0f, ModConfig.GunExtraBulletsPerLevel.Value);
        float reference = ReferenceShots();
        return reference > 0f ? flat * baseShots / reference : flat;
    }

    // Bars the widget should show: the same proportional growth as the shots, so a fuller bar reads as
    // "more shots" for bar-per-shot and continuous-drain (laser) guns alike.
    private static int TargetGunBars(ItemGun gun, int baseBars, int level)
    {
        if (level <= 0) return baseBars;
        float baseShots = BaseShots(gun, baseBars);
        float scaled = baseBars * (baseShots + ExtraShots(level, baseShots)) / baseShots;
        return Mathf.Clamp(Mathf.RoundToInt(scaled), baseBars, Mathf.Max(baseBars, MaxGunBars));
    }

    private static string NodeFor(Category c) => c switch
    {
        Category.GunBullet => Skills.SkillCatalog.BulletShots,
        Category.GunLaser => Skills.SkillCatalog.LaserShots,
        Category.GunTranq => Skills.SkillCatalog.TranqShots,
        Category.Drone => Skills.SkillCatalog.DroneBattery,
        _ => null,
    };

    // Technician root perk: drones last this much longer.
    private const float TechnicianDroneBonus = .10f;

    // Skill tree levels on the old ladder, plus the branch root: Gunsmith gives every gun one extra level's worth
    // of shots, Technician adds TechnicianDroneBonus to drones.
    private static int LevelOf(PlayerAvatar user, Category category)
    {
        if (user == null || string.IsNullOrEmpty(user.steamID)) return -1;
        bool drone = category == Category.Drone;
        int cap = EffectiveMaxLevel(drone);
        int level = Mathf.RoundToInt(Skills.SkillEffects.Equivalent(user, NodeFor(category), cap));
        int bonus = drone
            ? (Skills.SkillEffects.Has(user, Skills.SkillCatalog.Technician)
                ? Mathf.Max(1, Mathf.RoundToInt(TechnicianDroneBonus / Mathf.Clamp(ModConfig.DroneBatteryPercentPerLevel.Value, .01f, .5f))) : 0)
            : (Skills.SkillEffects.Has(user, Skills.SkillCatalog.Gunsmith) ? 1 : 0);
        return Mathf.Clamp(level, 0, Mathf.Max(0, cap)) + bonus;
    }

    private static bool IsGun(Category c) => c is Category.GunBullet or Category.GunLaser or Category.GunTranq;
    private static bool HasUpgradeBars(Category c) => IsGun(c) || c == Category.Drone;

    // Drones: "+X% battery life" draws X% more segments (drain is already slowed by the same factor).
    private static int TargetDroneBars(int baseBars, int level)
    {
        if (level <= 0) return baseBars;
        float scaled = baseBars * (1f + Mathf.Clamp01(ModConfig.DroneBatteryPercentPerLevel.Value * level));
        return Mathf.Clamp(Mathf.RoundToInt(scaled), baseBars, Mathf.Max(baseBars, MaxGunBars));
    }

    // Runs on every client: upgrade levels are synced by REPOLib, and each peer draws its own widget.
    // The charge percentage is preserved; only the number of segments it is split into changes.
    private static void SyncUpgradeBars(ItemBattery battery, State state)
    {
        if (!HasUpgradeBars(state.Category)) return;
        var gun = IsGun(state.Category) ? battery.GetComponent<ItemGun>() : null;
        if (IsGun(state.Category) && gun == null) return;
        int baseBars = BaseBars(battery, state);
        int level = LevelOf(UserOf(battery), state.Category);
        if (level >= 0) state.Level = level; // nobody using it: keep the last user's bars
        int target = !ModConfig.PlayerUpgradesEnabled.Value ? baseBars
            : gun != null ? TargetGunBars(gun, baseBars, state.Level)
            : TargetDroneBars(baseBars, state.Level);
        if (target == battery.batteryBars) return;

        battery.batteryBars = target;
        battery.BatteryStateInitialize();          // recompute batteryLifeInt and the change-detection baselines
        battery.currentBars = battery.batteryLifeInt;
        var visual = battery.batteryVisualLogic;
        if (visual != null) visual.BatteryBarsSet();
        var ui = BatteryUI.instance != null ? BatteryUI.instance.batteryVisualLogic : null;
        if (ui != null && ui.itemBattery == battery) ui.BatteryBarsSet();
    }

    // Who is using this item. PhysGrabObject.lastPlayerGrabbing only tracks physical grabs and clears ~3 s after
    // release, so it misses equipped guns and deployed drones. Equipped items name their owner; drones name
    // the player who switched them on.
    private static PlayerAvatar UserOf(ItemBattery battery)
    {
        var equippable = battery.GetComponent<ItemEquippable>();
        if (equippable != null && equippable.isEquipped)
            return equippable.GetOwnerPlayerAvatar();

        // A deployed drone belongs to the player who activated it, not the last passer-by who grabbed it.
        var drone = battery.GetComponent<ItemDrone>();
        var toggle = battery.GetComponent<ItemToggle>();
        if (drone != null)
        {
            var owner = drone.droneOwner != null ? drone.droneOwner :
                toggle != null ? SemiFunc.PlayerAvatarGetFromPhotonID(toggle.playerTogglePhotonID) : null;
            if (owner != null) return owner;
        }
        var pgo = battery.physGrabObject != null ? battery.physGrabObject : battery.GetComponentInChildren<PhysGrabObject>();
        if (pgo != null && pgo.lastPlayerGrabbing != null) return pgo.lastPlayerGrabbing;
        return toggle != null ? SemiFunc.PlayerAvatarGetFromPhotonID(toggle.playerTogglePhotonID) : null;
    }

    // Perk drain times the trade-off drain (Overdrive, Tinkerer, Drone Jockey, Sharpshooter, Overclock).
    private static float DrainMultiplier(ItemBattery battery)
    {
        float perks = PerkDrainMultiplier(battery);
        if (!ModConfig.PlayerUpgradesEnabled.Value || !SemiFunc.IsMasterClientOrSingleplayer() || battery == null) return perks;
        var category = _states.GetOrCreateValue(battery).Category;
        if ((int)category == -1) category = Resolve(battery);
        var fx = category == Category.Drone ? Skills.Fx.BatteryDrones : IsGun(category) ? Skills.Fx.BatteryGuns :
            category == Category.Other ? Skills.Fx.BatteryItems : (Skills.Fx?)null;
        return fx == null ? perks : perks * Skills.TradeOffs.Factor(UserOf(battery), fx.Value);
    }

    private static float PerkDrainMultiplier(ItemBattery battery)
    {
        if (!ModConfig.PlayerUpgradesEnabled.Value || !SemiFunc.IsMasterClientOrSingleplayer() || battery == null) return 1f;
        var category = Resolve(battery);
        if (category == Category.None) return 1f;
        if (category == Category.Other)
        {
            int saver = Skills.SkillEffects.Level(UserOf(battery), Skills.SkillCatalog.BatterySaver);
            return Skills.PerkRules.BatteryDrainMultiplier(saver, ModConfig.SkillBatterySaverPerLevel.Value);
        }
        int level = LevelOf(UserOf(battery), category);
        if (category == Category.Drone)
        {
            var droneState = _states.GetOrCreateValue(battery);
            if ((int)droneState.Category == -1) droneState.Category = category;
            if (level >= 0) droneState.Level = level;
            SyncUpgradeBars(battery, droneState);
            return level <= 0 ? 1f : 1f / (1f + Mathf.Clamp01(ModConfig.DroneBatteryPercentPerLevel.Value * level));
        }

        // Guns: the bar count already grew with the level (SyncUpgradeBars), so a bar-per-shot gun's native
        // RemoveFullBar is usually exact. Whatever the bars don't cover (laser drain, the MaxGunBars cap,
        // fractional config values) is settled as a cheaper drain: desired cost / native cost.
        var gun = battery.GetComponent<ItemGun>();
        if (gun == null) return 1f;
        var state = _states.GetOrCreateValue(battery);
        if ((int)state.Category == -1) state.Category = category;
        if (level >= 0) state.Level = level;
        SyncUpgradeBars(battery, state);
        int baseBars = BaseBars(battery, state);
        float baseShots = BaseShots(gun, baseBars);
        float desiredCost = 100f / (baseShots + ExtraShots(state.Level, baseShots));
        float nativeCost = gun.batteryDrainFullBar
            ? Mathf.Max(1, gun.batteryDrainFullBars) * 100f / Mathf.Max(1, battery.batteryBars)
            : Mathf.Max(0.01f, gun.batteryDrain);
        return Mathf.Clamp(desiredCost / nativeCost, 0.01f, 1f);
    }

    private static void SetDrainResult(ItemBattery battery, float before, float rawCost)
    {
        float multiplier = DrainMultiplier(battery);
        if (battery.batteryLife >= before) return;
        if (Mathf.Abs(multiplier - 1f) > 0.001f) // cheaper or (trade-offs) dearer
        {
            battery.batteryLife = Mathf.Clamp(before - Mathf.Max(0f, rawCost) * multiplier, 0f, 100f);
            battery.batteryLifeInt = battery.BatteryBarsGet();
            // Native full-bar consumption may already have saved/broadcast the rounded raw charge.
            // Publish the corrected fractional snapshot without playing a second deduction sound.
            battery.BatteryFullPercentChange(battery.batteryLifeInt, false, false);
        }
        // The periodic watchdog must not refund the same shot a second time.
        _states.GetOrCreateValue(battery).PrevLife = battery.batteryLife;
    }

    [HarmonyPatch(typeof(ItemGun), "ShootRPC")]
    private static class ShotDrain
    {
        [HarmonyPrefix]
        private static void Prefix(ItemGun __instance, out float __state) => __state = __instance.itemBattery != null ? __instance.itemBattery.batteryLife : 0f;
        [HarmonyPostfix]
        private static void Postfix(ItemGun __instance, float __state)
        {
            if (__instance.itemBattery != null && !__instance.batteryDrainFullBar)
                SetDrainResult(__instance.itemBattery, __state, __instance.batteryDrain);
        }
    }

    [HarmonyPatch(typeof(ItemBattery), "RemoveFullBar")]
    private static class FullBarDrain
    {
        [HarmonyPrefix]
        private static void Prefix(ItemBattery __instance, out float __state) => __state = __instance.batteryLife;
        [HarmonyPostfix]
        private static void Postfix(ItemBattery __instance, int _bars, float __state)
        {
            if (_bars > 0 && __instance.batteryBars > 0)
                SetDrainResult(__instance, __state, _bars * (100f / __instance.batteryBars));
        }
    }

    [HarmonyPatch(typeof(ItemBattery), "Update")]
    private static class RefundPatch
    {
        [HarmonyPrefix]
        private static void Prefix(ItemBattery __instance)
        {
            var state = _states.GetOrCreateValue(__instance);
            if (float.IsNaN(state.PrevLife)) state.PrevLife = __instance.batteryLife;

            if ((int)state.Category == -1)
            {
                state.Category = Resolve(__instance);
                if (HasUpgradeBars(state.Category)) BaseBars(__instance, state); // capture native bars before any change
            }
            if (!HasUpgradeBars(state.Category)) return;
            state.BarsCheckTimer -= Time.deltaTime;
            if (state.BarsCheckTimer > 0f) return;
            state.BarsCheckTimer = 0.25f;
            SyncUpgradeBars(__instance, state);
        }

        [HarmonyPostfix]
        private static void Postfix(ItemBattery __instance)
        {
            if (!ModConfig.PlayerUpgradesEnabled.Value || !SemiFunc.IsMasterClientOrSingleplayer())
            {
                _states.GetOrCreateValue(__instance).PrevLife = __instance.batteryLife;
                return;
            }

            var state = _states.GetOrCreateValue(__instance);

            if ((int)state.Category == -1)
                state.Category = Resolve(__instance);

            if (state.Category == Category.None)
                return;

            float life = __instance.batteryLife;

            // First sighting: seed the baseline, don't refund yet.
            if (float.IsNaN(state.PrevLife))
            {
                state.PrevLife = life;
                return;
            }

            float drained = state.PrevLife - life;

            // Only react to real drain, and never while charging (charging raises life).
            if (drained <= 0f || __instance.isCharging)
            {
                state.PrevLife = life;
                return;
            }

            float multiplier = DrainMultiplier(__instance);
            float refund = drained * (1f - multiplier);

            __instance.batteryLife = Mathf.Clamp(life + refund, 0f, 100f);
            state.PrevLife = __instance.batteryLife;
        }
    }
}
