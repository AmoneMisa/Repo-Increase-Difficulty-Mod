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
/// different components with no shared choke point. Rather than patch every drain site, we
/// use ONE uniform host-side watchdog: a postfix on <c>ItemBattery.Update</c> that watches
/// each battery's <c>batteryLife</c> and, whenever it drops (real drain, not charging),
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

        // All gun batteries share the "common gun" icon (bullet/laser/tranq use one texture).
        DefineGun(BulletId, "Bullet Gun Battery Life", "Upgrade_Gun-Bullet-Count.png");
        DefineGun(LaserId, "Laser Gun Battery Life", "Upgrade_Gun-Bullet-Count.png");
        DefineGun(TranqId, "Tranq Gun Battery Life", "Upgrade_Gun-Bullet-Count.png");

        PlayerUpgradeRegistry.Define(new PlayerUpgradeRegistry.Definition
        {
            Id = DroneId,
            DisplayName = "Drone Battery Life",
            Description = "Your drones' batteries last longer. Stacks per level (up to +100%).",
            MaxLevel = ModConfig.DroneBatteryMaxLevel.Value,
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
            MaxLevel = ModConfig.GunBatteryMaxLevel.Value,
            BaseShopPrice = ModConfig.GunBatteryBasePrice.Value,
            IconFile = iconFile,
        });
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

    [HarmonyPatch(typeof(ItemBattery), "Update")]
    private static class RefundPatch
    {
        [HarmonyPostfix]
        private static void Postfix(ItemBattery __instance)
        {
            if (!ModConfig.PlayerUpgradesEnabled.Value)
                return;

            // Battery drain is authoritative on the host; only refund there.
            if (!SemiFunc.IsMasterClientOrSingleplayer())
                return;

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

            var pgo = __instance.physGrabObject != null
                ? __instance.physGrabObject
                : __instance.GetComponentInChildren<PhysGrabObject>();
            var user = pgo != null ? pgo.lastPlayerGrabbing : null;
            if (user == null)
            {
                state.PrevLife = life;
                return;
            }

            var upgrade = RepoUpgrades.GetUpgrade(UpgradeIdFor(state.Category));
            int level = upgrade != null ? upgrade.GetLevel(user.steamID) : 0;
            if (level <= 0)
            {
                state.PrevLife = life;
                return;
            }

            // bonus = +X% battery life (capped at +100%). Refund the drain the extra life
            // "should" have absorbed: with drain multiplier 1/(1+bonus), the refunded
            // portion of this frame's drain is bonus/(1+bonus).
            float bonus = Mathf.Clamp01(PercentPerLevelFor(state.Category) * level);
            float refund = drained * (bonus / (1f + bonus));

            __instance.batteryLife = Mathf.Clamp(life + refund, 0f, 100f);
            state.PrevLife = __instance.batteryLife;
        }
    }
}
