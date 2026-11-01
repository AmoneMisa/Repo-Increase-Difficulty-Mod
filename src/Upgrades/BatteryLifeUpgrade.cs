using System.Runtime.CompilerServices;
using HarmonyLib;
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

    private enum Category { None, GunBullet, GunLaser, GunTranq, Drone }

    // Per-battery state: last observed batteryLife + cached category (component lookups are
    // not free, and an item's category never changes). ConditionalWeakTable so destroyed
    // batteries are collected without us leaking references.
    private sealed class State
    {
        public float PrevLife = float.NaN;
        public Category Category = (Category)(-1); // -1 = not yet resolved
    }

    private static readonly ConditionalWeakTable<ItemBattery, State> _states = new();

    public static void Register()
    {
        if (!ModConfig.PlayerUpgradesEnabled.Value)
            return;

        // Distinct front artwork and matching inventory renders for each weapon category.
        DefineGun(BulletId, "Bullet Gun Battery Life", "Upgrade_Gun-Bullet-Count.png");
        DefineGun(LaserId, "Laser Gun Battery Life", "Upgrade_Gun-Laser-Capacity.png");
        DefineGun(TranqId, "Tranq Gun Battery Life", "Upgrade_Gun-Tranq-Capacity.png");

        PlayerUpgradeRegistry.Define(new PlayerUpgradeRegistry.Definition
        {
            Id = DroneId,
            DisplayName = "Drone Battery Life",
            Description = "Your drones' batteries last longer. Stacks per level (up to +100%).",
            MaxLevel = EffectiveMaxLevel(true),
            BaseShopPrice = ModConfig.DroneBatteryBasePrice.Value,
            IconFile = "Upgrade_Drone-Charge-Capacity.png",
        });
    }

    private static void DefineGun(string id, string name, string iconFile = null)
    {
        PlayerUpgradeRegistry.Define(new PlayerUpgradeRegistry.Definition
        {
            Id = id,
            DisplayName = name,
            Description = "This gun's battery lasts longer. Stacks per level (up to +100%).",
            MaxLevel = EffectiveMaxLevel(false),
            BaseShopPrice = ModConfig.GunBatteryBasePrice.Value,
            IconFile = iconFile,
        });
    }

    internal static int EffectiveMaxLevel(bool drone)
    {
        float percent = Mathf.Clamp(drone ? ModConfig.DroneBatteryPercentPerLevel.Value : ModConfig.GunBatteryPercentPerLevel.Value, .01f, .5f);
        int configured = drone ? ModConfig.DroneBatteryMaxLevel.Value : ModConfig.GunBatteryMaxLevel.Value;
        return Mathf.Clamp(configured, 1, Mathf.CeilToInt(1f / percent));
    }

    private static Category Resolve(ItemBattery battery)
    {
        var go = battery.gameObject;
        if (go.GetComponent<ItemDrone>() != null)
            return Category.Drone;
        if (go.GetComponent<ItemGun>() != null)
        {
            if (go.GetComponent<ItemGunTranq>() != null)
                return Category.GunTranq;
            if (go.GetComponent<ItemGunLaser>() != null)
                return Category.GunLaser;
            return Category.GunBullet; // any other gun defaults to the bullet catch-all
        }
        return Category.None;
    }

    private static string UpgradeIdFor(Category c) => c switch
    {
        Category.GunBullet => BulletId,
        Category.GunLaser => LaserId,
        Category.GunTranq => TranqId,
        Category.Drone => DroneId,
        _ => null,
    };

    private static float PercentPerLevelFor(Category c) =>
        c == Category.Drone ? ModConfig.DroneBatteryPercentPerLevel.Value
                            : ModConfig.GunBatteryPercentPerLevel.Value;

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

    private static float DrainMultiplier(ItemBattery battery)
    {
        if (!ModConfig.PlayerUpgradesEnabled.Value || !SemiFunc.IsMasterClientOrSingleplayer() || battery == null) return 1f;
        var category = Resolve(battery);
        if (category == Category.None) return 1f;
        var user = UserOf(battery);
        if (user == null || string.IsNullOrEmpty(user.steamID)) return 1f;
        int cap = EffectiveMaxLevel(category == Category.Drone);
        int level = Mathf.Clamp(RepoUpgrades.GetUpgrade(UpgradeIdFor(category))?.GetLevel(user.steamID) ?? 0, 0, Mathf.Max(0, cap));
        float bonus = Mathf.Clamp01(PercentPerLevelFor(category) * level);
        return 1f / (1f + bonus);
    }

    private static void SetDrainResult(ItemBattery battery, float before, float rawCost)
    {
        float multiplier = DrainMultiplier(battery);
        if (battery.batteryLife >= before) return;
        if (multiplier < 1f)
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
