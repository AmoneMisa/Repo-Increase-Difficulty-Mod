using HarmonyLib;
using IncreaseHardMod.Modifiers;
using UnityEngine;

namespace IncreaseHardMod.Patches.Effects;

/// <summary>
/// ReducedPlayerHp modifier — every player's maximum HP is cut to a rolled
/// percentage (PlayerHpPercent, 10-90) for the special level.
///
/// A player's max HP is computed exactly once per level, inside the PlayerHealth.
/// Fetch() coroutine: maxHealth = 100 + StatsManager.GetPlayerMaxHealth(steamID),
/// where GetPlayerMaxHealth returns the health-upgrade bonus (upgrades * 20). That
/// getter is called from NOWHERE else in the game, so it is a clean, single choke
/// point — patching it has no shop/UI side effects. We can't postfix the Fetch
/// coroutine itself (its body lives in a compiler-generated state machine), but by
/// reshaping the getter's return value we control the final maxHealth.
///
/// The getter only returns the bonus, so to scale the WHOLE max (base 100 + bonus)
/// we return newBonus = round((100 + bonus) * pct) - 100. That may be negative
/// (e.g. a no-upgrade player at 10% -> 10 - 100 = -90), which is fine: Fetch adds
/// the base 100 back, yielding maxHealth = round(origMax * pct), and then clamps
/// current health into the new range. GetPlayerMaxHealth is deterministic from
/// synced stats and the modifier set is synced, so every client computes the same
/// reduction; the owner also broadcasts the result via UpdateHealthRPC. Between
/// levels the modifier set is Empty, so max HP returns to normal automatically.
/// </summary>
[HarmonyPatch(typeof(StatsManager), "GetPlayerMaxHealth")]
internal static class ReducedPlayerHpEffect
{
    [HarmonyPostfix]
    private static void Postfix(ref int __result)
    {
        var mods = ModifierManager.Current;
        if (!mods.Has(ModifierType.ReducedPlayerHp))
            return;

        int pct = Mathf.Clamp(mods.PlayerHpPercent, 1, 100);
        if (pct >= 100)
            return;

        int originalMax = 100 + __result;
        int reducedMax = Mathf.Max(1, Mathf.RoundToInt(originalMax * (pct / 100f)));
        __result = reducedMax - 100;
    }
}
