using UnityEngine;

namespace IncreaseHardMod.Quests;

/// <summary>
/// Classifies every upgrade item (vanilla AND the mod's custom REPOLib upgrades) into a quality
/// <see cref="UpgradeTier"/> by its display name, and rolls a tier weighted by quest rank so that
/// the strong "Power" upgrades only show up (rarely) on high-rank contracts.
///
/// Classification is name-based on <see cref="Item.itemName"/> because the custom upgrades strip
/// their vanilla ItemUpgradePlayer* component, so we can't identify them by component type.
/// </summary>
public static class QuestUpgradePool
{
    public static UpgradeTier TierOf(Item item)
    {
        if (item == null)
            return UpgradeTier.Mid;

        string n = (!string.IsNullOrEmpty(item.itemName) ? item.itemName : item.name ?? "").ToLowerInvariant();

        // Power (best, rarest): grab range, extra jump, grab strength, trackers, tumble launch.
        if (n.Contains("range") || n.Contains("jump") || n.Contains("grab strength") ||
            n.Contains("autoscan") || n.Contains("map suite") || n.Contains("tracker") ||
            n.Contains("tumble launch"))
            return UpgradeTier.Power;

        // Useless: head battery, tumble wings/climb, crouch rest.
        if (n.Contains("head battery") || n.Contains("wings") || n.Contains("climb") ||
            n.Contains("crouch"))
            return UpgradeTier.Useless;

        // Mid: sprint, throw, drone battery, team charge/heal perks.
        if (n.Contains("sprint") || n.Contains("throw") || n.Contains("drone battery") ||
            n.Contains("charge efficiency") || n.Contains("heal power"))
            return UpgradeTier.Mid;

        // Everything else (health, regen, stamina, map count, gun batteries, etc.) is Useful.
        return UpgradeTier.Useful;
    }

    /// <summary>
    /// Which tiers a quest of the given rank may hand out:
    ///   Useless + Mid from C, Useful from A, Power from SS.
    /// </summary>
    public static bool TierUnlocked(UpgradeTier tier, QuestRank rank) => tier switch
    {
        UpgradeTier.Useless => rank >= QuestRank.C,
        UpgradeTier.Mid => rank >= QuestRank.C,
        UpgradeTier.Useful => rank >= QuestRank.A,
        UpgradeTier.Power => rank >= QuestRank.SS,
        _ => false,
    };

    /// <summary>
    /// Roll a reward tier from the tiers unlocked at this rank. Mid/Useful are the staples,
    /// Useless is rare filler, and Power (SS+) is the rare jackpot.
    /// </summary>
    public static UpgradeTier PickTierByRank(QuestRank rank)
    {
        float wUseless = TierUnlocked(UpgradeTier.Useless, rank) ? 1.0f : 0f;
        float wMid = TierUnlocked(UpgradeTier.Mid, rank) ? 3.0f : 0f;
        float wUseful = TierUnlocked(UpgradeTier.Useful, rank) ? 3.0f : 0f;
        float wPower = TierUnlocked(UpgradeTier.Power, rank) ? 1.5f : 0f;

        float total = wUseless + wMid + wUseful + wPower;
        if (total <= 0f)
            return UpgradeTier.Mid;

        float roll = Random.value * total;
        if ((roll -= wPower) < 0f) return UpgradeTier.Power;
        if ((roll -= wUseful) < 0f) return UpgradeTier.Useful;
        if ((roll -= wMid) < 0f) return UpgradeTier.Mid;
        return UpgradeTier.Useless;
    }
}
