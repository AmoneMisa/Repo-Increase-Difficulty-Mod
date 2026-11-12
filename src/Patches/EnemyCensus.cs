using System.Collections;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace IncreaseHardMod.Patches;

/// <summary>
/// Diagnostics (host): how many of each enemy the level actually holds, alive or waiting to respawn, 15 s and
/// 60 s after the level is built - to check group enemies (a Gnome pack is a director + 4, Bangers a director + 3)
/// against what players see. One line each time.
/// </summary>
[HarmonyPatch(typeof(LevelGenerator), "GenerateDone")]
internal static class EnemyCensus
{
    private static LevelGenerator _done;

    [HarmonyPostfix]
    private static void Postfix(LevelGenerator __instance)
    {
        if (!SemiFunc.IsMasterClientOrSingleplayer() || !SemiFunc.RunIsLevel() || _done == __instance) return;
        _done = __instance;
        Networking.ModNetwork.Instance?.StartCoroutine(Count(__instance));
    }

    private static IEnumerator Count(LevelGenerator level)
    {
        foreach (float wait in new[] { 15f, 45f })
        {
            yield return new WaitForSeconds(wait);
            if (level == null || EnemyDirector.instance == null) yield break;
            var groups = new Dictionary<string, (int total, int spawned)>();
            foreach (var parent in EnemyDirector.instance.enemiesSpawned)
            {
                if (parent == null) continue;
                groups.TryGetValue(parent.enemyName, out var c);
                groups[parent.enemyName] = (c.total + 1, c.spawned + (parent.Spawned ? 1 : 0));
            }
            Plugin.Log.LogInfo("[Enemy] Census (on the map / total): " +
                               string.Join(", ", groups.OrderBy(g => g.Key).Select(g => $"{g.Key} {g.Value.spawned}/{g.Value.total}")));
        }
    }
}
