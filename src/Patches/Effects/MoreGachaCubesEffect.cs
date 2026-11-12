using System.Collections.Generic;
using HarmonyLib;
using IncreaseHardMod.Modifiers;
using UnityEngine;

namespace IncreaseHardMod.Patches.Effects;

/// <summary>
/// MoreGachaCubes modifier — more "gacha" cosmetic cubes (the rarity cubes that
/// roll shop skins) spawn on the level.
///
/// The game spawns these via ValuableDirector.SpawnCosmeticWorldObject(rarity,
/// volumeIndex, volumes): it places ONE cube of the given rarity at the next free
/// spawn volume, advancing volumeIndex[volume] and incrementing the target count.
/// The whole thing runs host-side in a spawn coroutine and syncs cubes via
/// PhotonNetwork.InstantiateRoomObject, so only the host needs the modifier.
///
/// Rather than reimplement the placement/networking, our Postfix simply re-invokes
/// the original method a few more times for the SAME rarity. Because we pass the
/// same volumeIndex/volumes arrays back in, the game's own "is there a free volume
/// left?" guard (volumeIndex[volume] &lt; volumes[volume].Count) naturally caps how
/// many extra cubes can appear — we never spawn past the level's available slots.
///
/// A static reentry flag stops our own re-invocations from triggering this Postfix
/// again (which would recurse). Unity's single main thread makes the flag safe.
/// </summary>
[HarmonyPatch(typeof(ValuableDirector), nameof(ValuableDirector.SpawnCosmeticWorldObject))]
internal static class MoreGachaCubesEffect
{
    private static bool _reentry;

    [HarmonyPostfix]
    private static void Postfix(
        ValuableDirector __instance,
        SemiFunc.Rarity _cosmeticRarity,
        int[] _volumeIndex,
        List<ValuableVolume>[] _volumes)
    {
        if (_reentry)
            return;
        if (!ModifierManager.Current.Has(ModifierType.MoreGachaCubes))
            return;

        int extra = Mathf.Max(0, ModConfig.ExtraGachaCubesPerSpawn.Value);
        if (extra == 0)
            return;

        _reentry = true;
        try
        {
            // The game's own spawn logic (publicized), called again for each extra cube.
            for (int i = 0; i < extra; i++)
                __instance.SpawnCosmeticWorldObject(_cosmeticRarity, _volumeIndex, _volumes);
        }
        finally
        {
            _reentry = false;
        }
    }
}
