using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace IncreaseHardMod.FableRealm;

/// <summary>
/// Host only. Fable Realm's rooms carry many more loot spots than the game's per-level budget fills (about 30 per
/// room against roughly 40 valuables for a whole level), so the level looked bare. Once the game's own valuable
/// setup has finished, this adds <see cref="FableExitRules.ExtraLoot"/> more valuables into free spots of every size,
/// through ValuableDirector.SpawnValuable (networking, value scaling and the custom-valuable footprint check apply).
/// Once per level.
/// </summary>
internal sealed class FableLootBoost : MonoBehaviour
{
    private object _doneFor;
    private bool _busy;

    private void Update()
    {
        if (_busy || !FableLevel.IsCurrent || !SemiFunc.IsMasterClientOrSingleplayer()) return;
        var dir = ValuableDirector.instance;
        var generator = LevelGenerator.Instance;
        if (dir == null || generator == null || !dir.setupComplete || ReferenceEquals(_doneFor, generator)) return;
        _doneFor = generator;
        StartCoroutine(Boost(dir));
    }

    private IEnumerator Boost(ValuableDirector dir)
    {
        _busy = true;
        // Cleared even when a step throws: a stuck flag would switch the boost off for every later level of the session.
        try
        {
            int spawned = 0, value = 0, already = 0;
            try
            {
                already = FindObjectsByType<ValuableObject>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Length;
            }
            catch (Exception e) { Plugin.Log.LogWarning("[Fable] Loot boost count failed: " + e.Message); }
            int extra = FableExitRules.ExtraLoot(already);
            var volumes = new List<ValuableVolume>();
            foreach (var vol in FindObjectsByType<ValuableVolume>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                if (vol != null) volumes.Add(vol);
            for (int i = volumes.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (volumes[i], volumes[j]) = (volumes[j], volumes[i]);
            }
            foreach (var vol in volumes)
            {
                if (spawned >= extra || dir == null || !FableLevel.IsCurrent) break;
                if (vol == null || vol.OverlapBoxCheck(SemiFunc.LayerMaskGetPhysGrabObject()).Length > 0) continue;
                var pool = Pool(dir, vol.VolumeType);
                if (pool == null || pool.Count == 0) continue;
                ValuableObject v = null;
                try { v = dir.SpawnValuable(pool[Random.Range(0, pool.Count)], vol); }
                catch (Exception e) { Plugin.Log.LogWarning("[Fable] Loot boost spawn failed: " + e.Message); }
                if (v == null) continue; // e.g. a custom valuable that did not fit: try the next spot
                spawned++;
                value += Mathf.Max(0, Mathf.RoundToInt(v.dollarValueCurrent));
                if (spawned % 4 == 0) yield return null; // spread the network spawns over a few frames
            }
            Plugin.Log.LogInfo($"[Fable] Loot boost: {already} valuables from the game's budget, added {spawned} of {extra} worth {value} " +
                               $"({volumes.Count} loot spots on the map).");
        }
        finally { _busy = false; }
    }

    private static List<PrefabRef> Pool(ValuableDirector dir, ValuableVolume.Type type) => type switch
    {
        ValuableVolume.Type.Tiny => dir.tinyValuables,
        ValuableVolume.Type.Small => dir.smallValuables,
        ValuableVolume.Type.Medium => dir.mediumValuables,
        ValuableVolume.Type.Big => dir.bigValuables,
        ValuableVolume.Type.Wide => dir.wideValuables,
        ValuableVolume.Type.Tall => dir.tallValuables,
        ValuableVolume.Type.VeryTall => dir.veryTallValuables,
        _ => null,
    };
}
