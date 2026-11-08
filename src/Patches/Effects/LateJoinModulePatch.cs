using HarmonyLib;
using Photon.Pun;

namespace IncreaseHardMod.Patches.Effects;

/// <summary>
/// Lets a late joiner finish building the level's modules.
///
/// The host spawns each level module with InstantiateRoomObject (kept in the room cache) and then sets it up with
/// <c>ModuleConnectionSetRPC</c> / <c>ModuleConnectionSetMegaRPC</c> sent to <c>RpcTarget.All</c>, which is NOT
/// buffered. Only that RPC counts the module as spawned (<c>LevelGenerator.ModulesSpawned++</c>). A player who joins
/// after generation receives the modules from the cache but never the setup call, so their generator waits in
/// <c>ModuleSpawnLocal</c> at 0/N forever and they can only quit.
///
/// While late join is on, the host sends the same RPC buffered instead. The buffered entry is keyed to the module's
/// view, so <see cref="LateJoinCache"/> already drops it with the rest of that level on the next level change.
/// </summary>
internal static class LateJoinModulePatch
{
    private static bool Buffer(Module module) =>
        ModConfig.LateJoinEnabled.Value && SemiFunc.IsMultiplayer() && PhotonNetwork.IsMasterClient &&
        module != null && module.photonView != null;

    [HarmonyPatch(typeof(Module), nameof(Module.ModuleConnectionSet))]
    private static class Simple
    {
        [HarmonyPrefix]
        private static bool Prefix(Module __instance, bool _top, bool _bottom, bool _right, bool _left, bool _first)
        {
            if (!Buffer(__instance)) return true;
            __instance.photonView.RPC("ModuleConnectionSetRPC", RpcTarget.AllBuffered, _top, _bottom, _right, _left, _first);
            return false;
        }
    }

    [HarmonyPatch(typeof(Module), nameof(Module.ModuleConnectionSetMega))]
    private static class Mega
    {
        [HarmonyPrefix]
        private static bool Prefix(Module __instance, bool _topRight, bool _topLeft, bool _rightTop, bool _rightBot,
            bool _botRight, bool _botLeft, bool _leftTop, bool _leftBot, bool _first)
        {
            if (!Buffer(__instance)) return true;
            __instance.photonView.RPC("ModuleConnectionSetMegaRPC", RpcTarget.AllBuffered, _topRight, _topLeft,
                _rightTop, _rightBot, _botRight, _botLeft, _leftTop, _leftBot, _first);
            return false;
        }
    }
}
