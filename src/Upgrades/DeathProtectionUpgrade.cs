using HarmonyLib;
using RepoUpgrades = REPOLib.Modules.Upgrades;

namespace IncreaseHardMod.Upgrades;

/// <summary>
/// "Death Protection" — a consumable per-player charge. When a hit would kill you, one
/// charge is spent and you survive at 1 HP instead. Charges do NOT auto-reset per level;
/// you must re-buy after each use (registered as a Consumable so the shop keeps offering it).
///
/// Implementation: a prefix on PlayerHealth.Hurt. If the incoming damage would drop you to
/// 0 or below and you own a charge, we CLAMP the damage so it leaves exactly 1 HP, then let
/// the original method run normally (updating stats + syncing health). This avoids
/// re-implementing the game's death/health bookkeeping — we just prevent the lethal blow.
///
/// PlayerHealth.Hurt only executes on the owning client (it early-returns unless the
/// PhotonView IsMine), so the consuming player spends their own charge; RemoveLevel then
/// broadcasts the decremented count to everyone via REPOLib.
/// </summary>
internal static class DeathProtectionUpgrade
{
    public const string Id = "IHM_DeathProtect";

    public static void Register()
    {
        if (!ModConfig.PlayerUpgradesEnabled.Value)
            return;

        PlayerUpgradeRegistry.Define(new PlayerUpgradeRegistry.Definition
        {
            Id = Id,
            DisplayName = "Death Protection",
            Description = "Survive one lethal hit at 1 HP. Consumed on use — re-buy to restore.",
            MaxLevel = ModConfig.DeathProtectMaxCharges.Value,
            BaseShopPrice = ModConfig.DeathProtectBasePrice.Value,
            Consumable = true,
            IconFile = "Upgrade_Additional-Life.png",
            StartAction = null,
            UpgradeAction = null,
        });
    }

    [HarmonyPatch(typeof(PlayerHealth), "Hurt")]
    private static class HurtPatch
    {
        // __instance fields (health, playerAvatar) are accessible via Publicizer.
        // Run LAST among our Hurt prefixes so we clamp the FINAL (already-scaled) damage;
        // otherwise EnemyDamagePatch could multiply our "leave 1 HP" value back up and kill
        // the player anyway, wasting a charge.
        [HarmonyPrefix]
        [HarmonyPriority(Priority.Last)]
        private static void Prefix(PlayerHealth __instance, ref int damage)
        {
            if (!ModConfig.PlayerUpgradesEnabled.Value || damage <= 0)
                return;

            // Not a lethal hit — nothing to protect against.
            if (__instance.health - damage > 0)
                return;

            var avatar = __instance.playerAvatar;
            if (avatar == null)
                return;

            var upgrade = RepoUpgrades.GetUpgrade(Id);
            if (upgrade == null || upgrade.GetLevel(avatar.steamID) <= 0)
                return;

            // Spend one charge and clamp the blow so it leaves the player at 1 HP.
            upgrade.RemoveLevel(avatar);
            damage = __instance.health - 1;
            Plugin.Log.LogInfo($"[Upgrades] Death Protection saved {avatar.steamID}; charges left {upgrade.GetLevel(avatar.steamID)}.");
        }
    }
}
