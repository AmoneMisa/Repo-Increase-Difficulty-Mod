using HarmonyLib;

namespace IncreaseHardMod.Patches;

/// <summary>
/// Makes the mod's "Map Suite" upgrade REPLACE the vanilla "Map Player Count" upgrade: the
/// vanilla one is removed from the shop rotation so only Map Suite is offered (Map Suite already
/// covers live player counts and much more).
///
/// ShopManager.ShopInitialize rebuilds the shop's candidate pools every shop visit, dropping each
/// vanilla upgrade item into <c>potentialItemUpgrades</c>. We postfix it and strip out the entry
/// whose prefab carries the <see cref="ItemUpgradeMapPlayerCount"/> component (that component is
/// the vanilla map upgrade). Removing it from the pool means it can never be spawned onto a shop
/// shelf. Host-only concern in practice, but harmless everywhere since it just filters a local
/// candidate list; gated behind config + the player-upgrade system being on.
/// </summary>
// ShopInitialize() just forwards to ShopInitialize(List) - patch the real one (the name alone is ambiguous).
[HarmonyPatch(typeof(ShopManager), "ShopInitialize", new[] { typeof(System.Collections.Generic.List<PunManager.ItemSpawn>) })]
internal static class MapUpgradeReplacePatch
{
    [HarmonyPostfix]
    private static void Postfix(ShopManager __instance)
    {
        if (!ModConfig.PlayerUpgradesEnabled.Value || !ModConfig.MapSuiteReplacesVanilla.Value)
            return;

        var pool = __instance.potentialItemUpgrades;
        if (pool == null)
            return;

        int removed = pool.RemoveAll(IsVanillaMapUpgrade);
        if (removed > 0)
            Plugin.Log.LogInfo($"[MapSuite] Removed {removed} vanilla 'Map Player Count' entr(ies) from the shop (replaced by Map Suite).");
    }

    private static bool IsVanillaMapUpgrade(Item item)
    {
        var prefab = item != null && item.prefab != null ? item.prefab.Prefab : null;
        return prefab != null && prefab.GetComponentInChildren<ItemUpgradeMapPlayerCount>(true) != null;
    }
}
