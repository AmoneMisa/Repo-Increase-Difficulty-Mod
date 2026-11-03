using HarmonyLib;
using IncreaseHardMod.Modifiers;
using IncreaseHardMod.Networking;

namespace IncreaseHardMod.Patches;

/// <summary>
/// Triggers the surprise-modifier roll each time the run advances to a new level.
///
/// RunManager.ChangeLevel is the game's own "go to the next level/shop/lobby"
/// method. It already returns early for non-host clients (see its first lines),
/// so a [HarmonyPostfix] here effectively runs HOST-ONLY — exactly what we want,
/// since only the host should roll. By the time this postfix runs, levelCurrent
/// has been set, so SemiFunc.RunIsLevel() tells us whether we're heading into an
/// actual run level (we skip shops, lobby, menus).
///
/// The host rolls, stores the result locally, and broadcasts it to clients.
/// </summary>
[HarmonyPatch(typeof(RunManager), "ChangeLevel")]
internal static class ChangeLevelPatch
{
    [HarmonyPrefix]
    private static void Prefix(RunManager __instance, out bool __state)
    {
        // Harmony postfixes also run when the original immediately rejects a request.
        __state = !__instance.restarting && (SemiFunc.MenuLevel() || SemiFunc.IsMasterClientOrSingleplayer());
    }

    [HarmonyPostfix]
    private static void Postfix(bool __state)
    {
        if (!__state)
            return;
        Effects.LateJoinSceneState.Publish();
        if (SemiFunc.IsMasterClientOrSingleplayer())
            Economy.BlackMarket.LoadFromStats();

        // Only roll for real run levels; shops/lobby/menus get no modifiers.
        if (!SemiFunc.RunIsLevel())
        {
            // Clear any leftover modifiers so shops/lobby are clean.
            ModifierManager.ApplyReceived(LevelModifiers.Empty);
            Economy.EconomyEvents.Clear();
            if (SemiFunc.IsMasterClientOrSingleplayer() && SemiFunc.RunIsShop())
                Economy.ShopEconomy.RollForShop();
            else if (SemiFunc.IsMasterClientOrSingleplayer())
                Economy.ShopEconomy.Clear();
            // The host must also TELL clients to clear. Otherwise a client keeps the last level's
            // modifier set into the shop and its per-level effects (e.g. ReducedPlayerHp on the
            // shop's health Fetch) fire when they shouldn't. RollForNextLevel broadcasts for real
            // levels, but the non-level path only cleared locally until now.
            if (SemiFunc.IsMasterClientOrSingleplayer())
                ModNetwork.Instance?.BroadcastModifiers(LevelModifiers.Empty);
            return;
        }

        // ChangeLevel already short-circuits for non-host clients, but be explicit.
        if (!SemiFunc.IsMasterClientOrSingleplayer())
            return;

        ModifierManager.RollForNextLevel(RunManager.instance.levelsCompleted);
        Economy.EconomyEvents.RollForNextLevel(RunManager.instance.levelsCompleted);
    }
}
