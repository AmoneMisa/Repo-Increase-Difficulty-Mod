using HarmonyLib;
using IncreaseHardMod.Modifiers;
using UnityEngine;

namespace IncreaseHardMod.Patches.Effects;

/// <summary>
/// MoneyLossOnDeath modifier — when a player dies, the crew loses a percentage
/// of its shared run currency.
///
/// PlayerAvatar.PlayerDeathRPC(int enemyIndex) is the single point every death
/// funnels through (both singleplayer, where PlayerDeath calls it directly, and
/// multiplayer, where it's an RPC to All). "currency" is a run-wide stat shared
/// by the whole crew (SemiFunc.StatGetRunCurrency / StatSetRunCurrency, backed by
/// PunManager's synced run stats), so we must deduct it EXACTLY ONCE per death.
/// We therefore gate on IsMasterClientOrSingleplayer: the host is the single
/// authority that mutates the stat, and PunManager syncs the new value to
/// everyone. The rolled MoneyLossPercent parameter (5-25) decides how much.
/// </summary>
[HarmonyPatch(typeof(PlayerAvatar), nameof(PlayerAvatar.PlayerDeathRPC))]
internal static class MoneyLossOnDeathEffect
{
    [HarmonyPostfix]
    private static void Postfix()
    {
        var mods = ModifierManager.Current;
        if (!mods.Has(ModifierType.MoneyLossOnDeath))
            return;

        // Shared run currency is host-authoritative; only the host deducts it.
        if (!SemiFunc.IsMasterClientOrSingleplayer())
            return;

        int current = SemiFunc.StatGetRunCurrency();
        if (current <= 0)
            return;

        int loss = Mathf.FloorToInt(current * (mods.MoneyLossPercent / 100f));
        if (loss <= 0)
            return;

        SemiFunc.StatSetRunCurrency(current - loss);
    }
}
