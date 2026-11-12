using HarmonyLib;
using UnityEngine;

namespace IncreaseHardMod.Patches;

/// <summary>
/// MorePlayers — raise the room's player cap above REPO's default of 6.
///
/// GameManager.SetMaxPlayers(target) is the single choke point the game uses to set the
/// cap (called from the lobby settings and on room creation). It clamps the target into
/// [1, maxPlayersPhoton] (maxPlayersPhoton defaults to 20). We prefix it so that, when the
/// feature is on, we force the target up to our configured value and make sure the Photon
/// ceiling allows it. The room's MaxPlayers is authored by the host, so only the host needs
/// this enabled; the patch is a no-op when disabled or when our value isn't larger.
/// </summary>
[HarmonyPatch(typeof(GameManager), nameof(GameManager.SetMaxPlayers))]
internal static class MaxPlayersPatch
{
    [HarmonyPrefix]
    private static void Prefix(GameManager __instance, ref int _target)
    {
        if (!ModConfig.MorePlayersEnabled.Value)
            return;

        int desired = Mathf.Clamp(ModConfig.MorePlayersMax.Value, 2, 20);

        // Let the Photon-side clamp permit our value.
        if (__instance.maxPlayersPhoton < desired)
            __instance.maxPlayersPhoton = desired;

        // Only ever raise the cap, never lower what the game asked for.
        if (_target <= 0 || desired > _target)
            _target = desired;
    }
}
