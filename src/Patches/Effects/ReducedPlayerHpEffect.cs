using HarmonyLib;
using IncreaseHardMod.Modifiers;
using UnityEngine;

namespace IncreaseHardMod.Patches.Effects;

/// <summary>
/// ReducedPlayerHp modifier — every player STARTS the special level with their current
/// HP cut to a rolled percentage (PlayerHpPercent, 10-90). Max HP is left untouched, so
/// players can heal back up to full; they just begin the level wounded.
///
/// A player's starting health for a level is read exactly once, inside the
/// PlayerHealth.Fetch() coroutine: health = StatsManager.GetPlayerHealth(steamID). That
/// getter is called from NOWHERE else in the game, so it is a clean, single choke point —
/// patching it has no shop/UI side effects. We can't postfix the Fetch coroutine itself
/// (its body lives in a compiler-generated state machine), but by reshaping this getter's
/// return value we control the health the level starts with.
///
/// Fetch clamps the result into [0, maxHealth] and then both writes it back with
/// SetPlayerHealth and broadcasts it via UpdateHealthRPC, so every client stays in sync.
/// GetPlayerHealth is deterministic from synced stats and the modifier set is synced, so
/// each owner computes the same reduction for their own avatar. Between levels the modifier
/// set is Empty, so no reduction is applied and carried-over HP is used as normal.
/// </summary>
[HarmonyPatch(typeof(StatsManager), nameof(StatsManager.GetPlayerHealth))]
internal static class ReducedPlayerHpEffect
{
    [HarmonyPostfix]
    private static void Postfix(ref int __result)
    {
        // Only wound players at the START of an actual run level. GetPlayerHealth is also read by
        // Fetch() on shop/lobby loads; a client can still be holding the previous level's modifier
        // set at that moment (the host clears its own set on a shop transition but the broadcast of
        // that clear may not have arrived yet), so without this gate the reduction would re-apply on
        // shop entry and eat HP that should instead have healed. RunIsLevel is false in the shop.
        if (!SafeRunIsLevel())
            return;

        var mods = ModifierManager.Current;
        if (!mods.Has(ModifierType.ReducedPlayerHp))
            return;

        int pct = Mathf.Clamp(mods.PlayerHpPercent, 1, 100);
        if (pct >= 100)
            return;

        // Leave the dead/near-dead alone; only scale a meaningful current value.
        if (__result <= 1)
            return;

        __result = Mathf.Max(1, Mathf.RoundToInt(__result * (pct / 100f)));
    }

    private static bool SafeRunIsLevel()
    {
        try { return SemiFunc.RunIsLevel(); }
        catch { return false; }
    }
}
