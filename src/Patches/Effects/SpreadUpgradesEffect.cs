using System.Collections.Generic;
using HarmonyLib;
using IncreaseHardMod.Modifiers;
using Photon.Pun;
using UnityEngine;

namespace IncreaseHardMod.Patches.Effects;

/// <summary>
/// SpreadUpgrades modifier: up to six upgrade pickups (a hard cap for the whole map, whatever an old config
/// says) are scattered over the level. A found upgrade is loot, not a free upgrade:
///
/// 1) SPAWN (SpawnOnGenerated, host only, once per LevelGenerator): room objects tagged with Photon
///    InstantiationData "IHM.SpreadUpgrade" (singleplayer: a <see cref="SpreadUpgradePickup"/> marker).
/// 2) LOCKED (BlockPickupToggle / BlockUseOnLevel): a tagged pickup can be carried but never toggled or applied.
///    Untagged (bought) upgrades are never affected.
/// 3) EXTRACTION (ExtractUpgrades): when an extraction point finishes successfully the game calls
///    DestroyAllPhysObjectsInHaulList on the host. Tagged pickups standing in THAT point's room volume are then
///    recorded as native purchases (StatsManager.ItemPurchase, plus AddItemsUpgradesPurchased for item_upgrade,
///    exactly as the shop does) and destroyed. The run save then delivers them in the next truck fill
///    (TruckItemSlotsPatch always finds room for upgrades). Failed or cancelled quotas never call it, other
///    extraction points do not count, and each pickup is credited at most once.
/// </summary>
internal static class SpreadUpgradesEffect
{
    private const string SpawnTag = "IHM.SpreadUpgrade";
    private static LevelGenerator _generated;
    private static readonly HashSet<GameObject> Collected = new();

    internal static bool IsUncollected(Component target)
    {
        if (target == null) return false;
        var view = target.GetComponent<PhotonView>();
        return target.GetComponent<SpreadUpgradePickup>() != null ||
            (view?.InstantiationData is object[] data && data.Length == 1 && data[0] as string == SpawnTag);
    }

    [HarmonyPatch(typeof(ExtractionPoint), nameof(ExtractionPoint.DestroyAllPhysObjectsInHaulList))]
    private static class ExtractUpgrades
    {
        [HarmonyPrefix]
        private static void Prefix(ExtractionPoint __instance)
        {
            if (!SemiFunc.IsMasterClientOrSingleplayer() || !SemiFunc.RunIsLevel() ||
                StatsManager.instance == null || __instance.roomVolume == null) return;
            var rooms = __instance.roomVolume.GetComponentsInChildren<RoomVolume>(true);
            var volumes = __instance.roomVolume.GetComponentsInChildren<BoxCollider>(true);
            foreach (var attributes in Object.FindObjectsByType<ItemAttributes>(FindObjectsSortMode.None))
            {
                if (!IsUncollected(attributes) || Collected.Contains(attributes.gameObject) || attributes.item == null) continue;
                var body = attributes.GetComponent<PhysGrabObject>();
                if (body == null || body.dead) continue;
                bool inside = InPoint(body, rooms, volumes);
                if (!inside) continue;
                // Native purchases persist in the run save and populate the next truck.
                // Do not credit anything on failed/cancelled quotas, or when merely entering the truck.
                Collected.Add(attributes.gameObject);
                StatsManager.instance.ItemPurchase(attributes.item.name);
                if (attributes.item.itemType == SemiFunc.itemType.item_upgrade)
                    StatsManager.instance.AddItemsUpgradesPurchased(attributes.item.name);
                body.DestroyPhysGrabObject();
                Plugin.Log.LogInfo($"[SpreadUpgrades] Extracted '{attributes.item.name}' for the next truck.");
            }
        }
    }

    // Native membership first (the same RoomVolumeCheck that fills the valuables' haul list), then the
    // point's own box volumes as a fallback for a pickup whose room check has not run since it moved.
    private static bool InPoint(PhysGrabObject body, RoomVolume[] rooms, BoxCollider[] volumes)
    {
        var check = body.roomVolumeCheck;
        if (check != null && check.CurrentRooms != null)
            foreach (var room in check.CurrentRooms)
                if (room != null && System.Array.IndexOf(rooms, room) >= 0) return true;
        Vector3 center = body.rb != null ? body.rb.worldCenterOfMass : body.transform.position;
        foreach (var box in volumes)
        {
            if (box == null || !box.enabled || !box.gameObject.activeInHierarchy) continue;
            Vector3 local = box.transform.InverseTransformPoint(center) - box.center;
            Vector3 half = box.size * .5f;
            if (Mathf.Abs(local.x) <= half.x && Mathf.Abs(local.y) <= half.y && Mathf.Abs(local.z) <= half.z) return true;
        }
        return false;
    }

    [HarmonyPatch(typeof(ItemToggle), nameof(ItemToggle.ToggleItemLogic))]
    private static class BlockPickupToggle
    {
        [HarmonyPrefix]
        private static bool Prefix(ItemToggle __instance, bool toggle) => !toggle || !IsUncollected(__instance);
    }

    [HarmonyPatch(typeof(LevelGenerator), nameof(LevelGenerator.GenerateDone))]
    private static class SpawnOnGenerated
    {
        [HarmonyPostfix]
        private static void Postfix(LevelGenerator __instance)
        {
            if (!ModifierManager.Current.Has(ModifierType.SpreadUpgrades))
                return;

            // Room objects can only be instantiated by the master client.
            if (!SemiFunc.IsMasterClientOrSingleplayer())
                return;

            if (_generated == __instance) return;
            _generated = __instance;
            Collected.Clear();
            int count = Mathf.Clamp(ModConfig.SpreadUpgradesCount.Value, 0, 6);
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
                    PhotonNetwork.InstantiateRoomObject(item.prefab.ResourcePath, pos, Quaternion.identity, 0, new object[] { SpawnTag });
                else
                    Object.Instantiate(item.prefab.Prefab, pos, Quaternion.identity).AddComponent<SpreadUpgradePickup>();

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

    [HarmonyPatch(typeof(ItemUpgrade), nameof(ItemUpgrade.PlayerUpgrade))]
    private static class BlockUseOnLevel
    {
        // A found pickup stays inert until it has been extracted and re-delivered by the truck as a normal
        // purchased item (which no longer carries the tag). Bought upgrades are untouched.
        [HarmonyPrefix]
        private static bool Prefix(ItemUpgrade __instance)
        {
            if (IsUncollected(__instance))
                return false;
            return true;
        }
    }
}

internal sealed class SpreadUpgradePickup : MonoBehaviour { }
