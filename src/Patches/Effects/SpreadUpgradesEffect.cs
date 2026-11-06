using System.Collections.Generic;
using HarmonyLib;
using IncreaseHardMod.Modifiers;
using Photon.Pun;
using UnityEngine;

namespace IncreaseHardMod.Patches.Effects;

/// <summary>
/// SpreadUpgrades modifier — scatter a handful of upgrade-item pickups across the
/// special level. Players haul them out like loot, but they can only be APPLIED once
/// the squad is back in the truck; using them on the special level itself is blocked.
///
/// Two halves:
///
/// 1) SPAWN (SpawnOnGenerated): upgrade items are ordinary shop Items
///    (itemType == item_upgrade / player_upgrade), and the game spawns items by
///    room-instantiating their prefab (see ItemManager.SpawnItem). We reuse that:
///    after the host finishes generating the level (LevelGenerator.GenerateDone, a
///    master-only RPC), we room-instantiate SpreadUpgradesCount upgrade items at
///    random LevelPoints spread over the whole map. Only the host spawns, since room
///    objects require the master client.
///
/// 2) GATE (BlockUseOnLevel): normally an upgrade item is consumed the instant a
///    holder toggles it (ItemToggle -> ItemUpgrade.PlayerUpgrade -> the stat RPC),
///    which would let players use these the moment they pick them up. To enforce the
///    "only in the truck" rule we prefix ItemUpgrade.PlayerUpgrade and no-op it while
///    we're on an actual run level AND SpreadUpgrades is active. The modifier set is
///    cleared when the run moves to the truck/shop (ChangeLevelPatch), so the gate
///    lifts automatically there and the upgrades work normally. This only affects
///    special SpreadUpgrades levels; vanilla levels never carry upgrade items anyway.
/// </summary>
internal static class SpreadUpgradesEffect
{
    [HarmonyPatch(typeof(LevelGenerator), "GenerateDone")]
    private static class SpawnOnGenerated
    {
        [HarmonyPostfix]
        private static void Postfix()
        {
            if (!ModifierManager.Current.Has(ModifierType.SpreadUpgrades))
                return;

            // Room objects can only be instantiated by the master client.
            if (!SemiFunc.IsMasterClientOrSingleplayer())
                return;

            int count = Mathf.Max(0, ModConfig.SpreadUpgradesCount.Value);
            if (count == 0)
                return;

            var upgrades = CollectUpgradeItems();
            if (upgrades.Count == 0)
            {
                Plugin.Log.LogWarning("[SpreadUpgrades] No upgrade items found in item dictionary; nothing spawned.");
                return;
            }

            var points = SemiFunc.LevelPointsGetAll();
            if (points == null || points.Count == 0)
            {
                Plugin.Log.LogWarning("[SpreadUpgrades] No level points to scatter upgrades on; nothing spawned.");
                return;
            }

            for (int i = 0; i < count; i++)
            {
                var item = upgrades[Random.Range(0, upgrades.Count)];
                var point = points[Random.Range(0, points.Count)];
                if (item == null || item.prefab == null || point == null)
                    continue;

                Vector3 pos = point.transform.position + Vector3.up;
                if (SemiFunc.IsMultiplayer())
                    PhotonNetwork.InstantiateRoomObject(item.prefab.ResourcePath, pos, Quaternion.identity, 0);
                else
                    Object.Instantiate(item.prefab.Prefab, pos, Quaternion.identity);

                Plugin.Log.LogInfo($"[SpreadUpgrades] Scattered '{item.name}' at {pos}.");
            }
        }

        private static List<Item> CollectUpgradeItems()
        {
            var result = new List<Item>();
            var stats = StatsManager.instance;
            if (stats == null)
                return result;

            var allowed = ParseAllowed();

            foreach (var item in stats.itemDictionary.Values)
            {
                if (item == null)
                    continue;
                if (item.itemType != SemiFunc.itemType.item_upgrade &&
                    item.itemType != SemiFunc.itemType.player_upgrade)
                    continue;
                if (ModConfig.PlayerUpgradesEnabled.Value && Skills.SkillShopPatch.IsTreeOwnedVanilla(item))
                    continue; // the skill tree owns it now

                if (ModConfig.LogSpreadUpgradeNames.Value)
                    Plugin.Log.LogInfo($"[SpreadUpgrades] Upgrade item available: '{item.name}'.");

                if (allowed.Count == 0 || IsAllowed(item.name, allowed))
                    result.Add(item);
            }
            return result;
        }

        private static List<string> ParseAllowed()
        {
            var list = new List<string>();
            string raw = ModConfig.SpreadUpgradesAllowed.Value;
            if (string.IsNullOrWhiteSpace(raw))
                return list;
            foreach (var part in raw.Split(','))
            {
                string trimmed = part.Trim();
                if (trimmed.Length > 0)
                    list.Add(trimmed);
            }
            return list;
        }

        private static bool IsAllowed(string itemName, List<string> allowed)
        {
            if (string.IsNullOrEmpty(itemName))
                return false;
            foreach (var a in allowed)
            {
                if (itemName.IndexOf(a, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }
            return false;
        }
    }

    [HarmonyPatch(typeof(ItemUpgrade), "PlayerUpgrade")]
    private static class BlockUseOnLevel
    {
        // Return false to skip the original: the upgrade stays inert until the squad
        // reaches the truck, where SpreadUpgrades is no longer active.
        [HarmonyPrefix]
        private static bool Prefix()
        {
            if (SemiFunc.RunIsLevel() && ModifierManager.Current.Has(ModifierType.SpreadUpgrades))
                return false;
            return true;
        }
    }
}
