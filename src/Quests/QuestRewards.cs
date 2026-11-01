using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;

namespace IncreaseHardMod.Quests;

/// <summary>
/// Payouts for completed quests. Both paths are HOST-authoritative: currency is a shared
/// run stat (SemiFunc.StatSetRunCurrency routes through PunManager, host-owned) and item
/// spawns are room objects that only the master client may instantiate. A non-host player
/// never calls these directly — for its personal-quest money it asks the host over the
/// network (see ModNetwork.RequestQuestMoney).
/// </summary>
public static class QuestRewards
{
    public static void GrantMoney(int amount) => QuestMoney.Grant(amount);

    /// <summary>
    /// Heal the whole squad. HealOther is owner-authoritative but RPCs to everyone, so the host
    /// may call it on any avatar and each player's own client applies it.
    /// </summary>
    public static void HealAll(int amount)
    {
        if (amount <= 0 || !SemiFunc.IsMasterClientOrSingleplayer())
            return;

        var players = SemiFunc.PlayerGetList();
        if (players == null)
            return;
        foreach (var p in players)
        {
            if (p != null && p.playerHealth != null)
                p.playerHealth.HealOther(amount, true);
        }
        Plugin.Log.LogInfo($"[Quests] Reward: healed squad for {amount}.");
    }

    /// <summary>
    /// Spawn an upgrade item of the given quality <paramref name="tier"/> (vanilla OR the mod's
    /// custom upgrades), skipping any that are already at their purchase cap. Falls back to any
    /// available upgrade if the tier is empty. Returns the item's display name, or null.
    /// </summary>
    public static string SpawnTieredUpgrade(UpgradeTier tier, QuestRank rank)
    {
        if (!SemiFunc.IsMasterClientOrSingleplayer())
            return null;

        var available = CollectUpgradeItems();
        // Only upgrades that are (a) not maxed and (b) in a tier this rank has unlocked.
        available.RemoveAll(it => !IsUpgradeAvailable(it) ||
                                  !QuestUpgradePool.TierUnlocked(QuestUpgradePool.TierOf(it), rank));
        if (available.Count == 0)
            return null;

        var inTier = available.FindAll(it => QuestUpgradePool.TierOf(it) == tier);
        var pick = inTier.Count > 0 ? inTier : available;
        return SpawnItem(pick[Random.Range(0, pick.Count)]);
    }

    // An upgrade is unavailable when it's a capped item that's already hit its lifetime purchase
    // limit (this covers the mod's custom maxLevel upgrades and one-time unlocks). Spawning one
    // anyway would waste the reward, so we skip it.
    private static bool IsUpgradeAvailable(Item item)
    {
        if (item == null)
            return false;
        var stats = StatsManager.instance;
        if (stats == null)
            return true;
        if (item.maxPurchase && stats.GetItemsUpgradesPurchasedTotal(item.name) >= item.maxPurchaseAmount)
            return false;
        return true;
    }

    /// <summary>
    /// Spawn one random GENERAL shop item (a consumable/tool/weapon, not an upgrade or cosmetic).
    /// Returns the item's display name, or null.
    /// </summary>
    public static string SpawnGeneralItem()
    {
        if (!SemiFunc.IsMasterClientOrSingleplayer())
            return null;

        var stats = StatsManager.instance;
        if (stats == null)
            return null;

        var pool = new List<Item>();
        foreach (var item in stats.itemDictionary.Values)
        {
            if (item != null && IsGeneralShopItem(item.itemType))
                pool.Add(item);
        }
        if (pool.Count == 0)
            return null;
        return SpawnItem(pool[Random.Range(0, pool.Count)]);
    }

    private static bool IsGeneralShopItem(SemiFunc.itemType type)
    {
        switch (type)
        {
            case SemiFunc.itemType.grenade:
            case SemiFunc.itemType.healthPack:
            case SemiFunc.itemType.gun:
            case SemiFunc.itemType.tracker:
            case SemiFunc.itemType.mine:
            case SemiFunc.itemType.tool:
            case SemiFunc.itemType.melee:
            case SemiFunc.itemType.launcher:
                return true;
            default:
                return false;
        }
    }

    // Spawn a catalogue Item at a random level point (same mechanism as SpawnUpgradeItem).
    private static string SpawnItem(Item item)
    {
        if (item == null || item.prefab == null)
            return null;
        var points = SemiFunc.LevelPointsGetAll();
        if (points == null || points.Count == 0)
            return null;
        var point = points[Random.Range(0, points.Count)];
        if (point == null)
            return null;

        Vector3 pos = point.transform.position + Vector3.up;
        if (SemiFunc.IsMultiplayer())
            PhotonNetwork.InstantiateRoomObject(item.prefab.ResourcePath, pos, Quaternion.identity, 0);
        else
            Object.Instantiate(item.prefab.Prefab, pos, Quaternion.identity);

        Plugin.Log.LogInfo($"[Quests] Reward: spawned item '{item.name}' at {pos}.");
        return item.name;
    }

    /// <summary>
    /// Spawn one random upgrade item on the level as loot (same mechanism the SpreadUpgrades
    /// modifier uses). Returns the item's display name, or null if nothing could be spawned.
    /// </summary>
    public static string SpawnUpgradeItem()
    {
        if (!SemiFunc.IsMasterClientOrSingleplayer())
            return null;

        var upgrades = CollectUpgradeItems();
        upgrades.RemoveAll(it => !IsUpgradeAvailable(it));
        if (upgrades.Count == 0)
            return null;

        return SpawnItem(upgrades[Random.Range(0, upgrades.Count)]);
    }

    /// <summary>
    /// Spawn one cosmetic "gacha" box (a rarity cube to roll a skin) on the level, reusing the
    /// prefabs the game's ValuableDirector uses for its own world cosmetics. Higher-rank quests
    /// bias toward rarer boxes. Returns the rarity label, or null if none could be spawned.
    /// </summary>
    public static string SpawnCosmetic(QuestRank rank)
    {
        if (!SemiFunc.IsMasterClientOrSingleplayer())
            return null;

        var director = ValuableDirector.instance;
        var setups = director != null ? director.cosmeticWorldObjectSetups : null;
        if (setups == null || setups.Count == 0)
            return null;

        var points = SemiFunc.LevelPointsGetAll();
        if (points == null || points.Count == 0)
            return null;

        var setup = PickCosmetic(setups, rank);
        var point = points[Random.Range(0, points.Count)];
        if (setup == null || setup.prefab == null || point == null)
            return null;

        Vector3 pos = point.transform.position + Vector3.up;
        if (SemiFunc.IsMultiplayer())
            PhotonNetwork.InstantiateRoomObject(setup.prefab.ResourcePath, pos, Quaternion.identity, 0);
        else
            Object.Instantiate(setup.prefab.Prefab, pos, Quaternion.identity);

        Plugin.Log.LogInfo($"[Quests] Reward: spawned cosmetic box ({setup.rarity}) at {pos}.");
        return setup.rarity.ToString();
    }

    // Weighted pick: higher ranks lean toward rarer boxes. We sort by rarity enum (Common..UltraRare)
    // and bias the index upward with rank.
    private static ValuableDirector.CosmeticWorldObjectSetup PickCosmetic(
        List<ValuableDirector.CosmeticWorldObjectSetup> setups, QuestRank rank)
    {
        // Chance to "upgrade" the pick toward a rarer box grows with rank.
        int index = Random.Range(0, setups.Count);
        float rareBias = 0.08f * (int)rank;
        for (int i = 0; i < 2; i++)
        {
            if (Random.value < rareBias)
            {
                int rarer = Random.Range(0, setups.Count);
                if ((int)setups[rarer].rarity > (int)setups[index].rarity)
                    index = rarer;
            }
        }
        return setups[index];
    }

    private static List<Item> CollectUpgradeItems()
    {
        var result = new List<Item>();
        var stats = StatsManager.instance;
        if (stats == null)
            return result;

        foreach (var item in stats.itemDictionary.Values)
        {
            if (item == null)
                continue;
            if (item.itemType == SemiFunc.itemType.item_upgrade ||
                item.itemType == SemiFunc.itemType.player_upgrade)
                result.Add(item);
        }
        return result;
    }
}
