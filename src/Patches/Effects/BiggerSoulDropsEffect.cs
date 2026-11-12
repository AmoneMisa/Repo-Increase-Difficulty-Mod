using HarmonyLib;
using IncreaseHardMod.Modifiers;
using UnityEngine;

namespace IncreaseHardMod.Patches.Effects;

/// <summary>
/// BiggerSoulDrops modifier — each individual soul drop (enemy valuable) is worth
/// more money. This changes VALUE PER DROP, not the number of drops (that's the
/// separate ExtraSouls modifier).
///
/// Enemy souls are ValuableObject prefabs: AssetManager.enemyValuableSmall /
/// Medium / Big (chosen by level difficulty). Their dollar value is rolled in
/// ValuableObject.DollarValueSetLogic(), which the host runs exclusively and then
/// broadcasts via the DollarValueSetRPC that fires immediately after it returns
/// (see ValuableObject.DollarValueSet coroutine). Because our Postfix mutates
/// dollarValueCurrent BEFORE that RPC reads it, the boosted value syncs to every
/// client and is also what gets added to RoundDirector.haulGoalMax. We only touch
/// the three enemy-valuable prefabs so ordinary loot is untouched.
/// </summary>
[HarmonyPatch(typeof(ValuableObject), nameof(ValuableObject.DollarValueSetLogic))]
internal static class BiggerSoulDropsEffect
{
    [HarmonyPrefix]
    private static void Prefix(ValuableObject __instance, out bool __state) => __state = __instance.dollarValueSet;

    [HarmonyPostfix]
    private static void Postfix(ValuableObject __instance, bool __state)
    {
        // Only the first valuation, and never a fixed value the game set (surplus/money bags).
        if (__state || __instance.dollarValueOverride != 0) return;
        var mods = ModifierManager.Current;
        if (!mods.Has(ModifierType.BiggerSoulDrops))
            return;

        if (!IsEnemyValuable(__instance))
            return;

        float factor = Mathf.Max(1f, ModConfig.BiggerSoulDropsFactor.Value);
        __instance.dollarValueOriginal = Mathf.Round(__instance.dollarValueOriginal * factor);
        __instance.dollarValueCurrent = __instance.dollarValueOriginal;
    }

    /// <summary>
    /// True if this valuable is one of the enemy soul-drop prefabs. Instantiated
    /// objects carry a "(Clone)" suffix, so we compare against the prefab names
    /// with StartsWith.
    /// </summary>
    internal static bool IsEnemyValuable(ValuableObject obj)
    {
        var am = AssetManager.instance;
        if (am == null)
            return false;

        string name = obj.name;
        return NameMatches(name, am.enemyValuableSmall)
            || NameMatches(name, am.enemyValuableMedium)
            || NameMatches(name, am.enemyValuableBig);
    }

    private static bool NameMatches(string instanceName, GameObject prefab)
    {
        return prefab != null && instanceName.StartsWith(prefab.name);
    }
}
