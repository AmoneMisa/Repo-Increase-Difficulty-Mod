using System;
using HarmonyLib;

namespace IncreaseHardMod.Patches.Effects;

internal static class ItemDisplayName
{
    internal static string Get(ItemAttributes attributes)
    {
        if (IsDisplayName(attributes.cachedItemName))
            return attributes.cachedItemName;

        var item = attributes.item;
        if (item != null && IsDisplayName(item.itemName))
            return item.itemName;
        if (IsDisplayName(attributes.itemName))
            return attributes.itemName;

        // Items registered without a display name can still have a readable prefab name.
        string name = attributes.name ?? string.Empty;
        int clone = name.IndexOf("(Clone)", StringComparison.Ordinal);
        if (clone >= 0)
            name = name.Substring(0, clone);
        if (name.StartsWith("Item ", StringComparison.Ordinal))
            name = name.Substring("Item ".Length);
        name = name.Trim();
        return IsDisplayName(name) ? name : "Item";
    }

    private static bool IsDisplayName(string name)
        => !string.IsNullOrWhiteSpace(name)
           && !name.Equals("N/A", StringComparison.OrdinalIgnoreCase)
           && !name.StartsWith("ITEM.", StringComparison.Ordinal)
           && !name.StartsWith("No translation found for ", StringComparison.Ordinal);

    [HarmonyPatch(typeof(ItemAttributes), "RefreshLocalizations")]
    private static class RefreshLocalizations
    {
        // Unity can return a diagnostic string when a translation is missing.
        // Repair the game's cached label so its native item prompt uses the same fallback.
        [HarmonyPostfix]
        private static void Postfix(ItemAttributes __instance)
        {
            __instance.cachedItemName = Get(__instance);
        }
    }
}
