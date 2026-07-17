using HarmonyLib;
using IncreaseHardMod.Modifiers;
using UnityEngine;

namespace IncreaseHardMod.Patches.Effects;

/// <summary>
/// ExtraSouls modifier — monsters drop more "souls" (enemy valuables).
///
/// EnemyHealth.spawnValuableMax caps how many valuable orbs an enemy drops on
/// death (vanilla default 3). The rolled parameter ExtraSouls (4-10) is the new
/// target maximum. We raise spawnValuableMax to it in a Postfix on Awake so it's
/// set before the enemy can ever die.
///
/// The actual spawning happens host-side (PhotonNetwork.InstantiateRoomObject),
/// and spawnValuableMax is deterministic from the synced modifier set, so counts
/// stay consistent for everyone.
/// </summary>
[HarmonyPatch(typeof(EnemyHealth), "Awake")]
internal static class ExtraSoulsEffect
{
    [HarmonyPostfix]
    private static void Postfix(EnemyHealth __instance)
    {
        var mods = ModifierManager.Current;
        if (!mods.Has(ModifierType.ExtraSouls))
            return;

        // Never reduce an enemy that already drops more than the roll.
        __instance.spawnValuableMax = Mathf.Max(__instance.spawnValuableMax, mods.ExtraSouls);
    }
}
