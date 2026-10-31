using System.Collections;
using HarmonyLib;
using UnityEngine;

namespace IncreaseHardMod.Economy;

/// <summary>
/// Depth scaling for the level itself, each with its own switch in the Modules section:
///   * map value    - every valuable is worth more the deeper the run (and by the level's economy event),
///   * enemy count  - more monsters are picked per level,
///   * level size   - more modules (a larger map) via the game's own size multiplier,
///   * extractions  - extra extraction points,
///   * moon phases  - the moon advances every N levels instead of every 5.
/// All of it is host-side generation: clients receive the result through the game's normal sync.
/// </summary>
internal static class LevelScalingPatches
{
    private static int Depth(int start) => Mathf.Max(0, Difficulty.LevelsCompleted - start);

    // ---- map value (+ economy "Loot" multiplier) ---------------------------------------------

    [HarmonyPatch(typeof(ValuableObject), "DollarValueSetLogic")]
    private static class MapValue
    {
        [HarmonyPrefix]
        private static void Prefix(ValuableObject __instance, out bool __state) => __state = __instance.dollarValueSet;

        [HarmonyPostfix]
        private static void Postfix(ValuableObject __instance, bool __state)
        {
            if (__state || !SemiFunc.IsMasterClientOrSingleplayer() || !SemiFunc.RunIsLevel())
                return; // already valued earlier, or not host-authoritative

            float mult = EconomyEvents.LootMult;
            if (ModConfig.ScaleMapValue.Value)
                mult *= Mathf.Min(ModConfig.MapValueMax.Value,
                    Mathf.Pow(1f + ModConfig.MapValuePerLevel.Value, Depth(ModConfig.MapValueStartLevel.Value)));
            if (Mathf.Approximately(mult, 1f))
                return;

            __instance.dollarValueOriginal = Mathf.Max(100f, Mathf.Round(__instance.dollarValueOriginal * mult / 100f) * 100f);
            __instance.dollarValueCurrent = __instance.dollarValueOriginal;
        }
    }

    // ---- enemy count ------------------------------------------------------------------------

    [HarmonyPatch(typeof(EnemyDirector), "AmountSetup")]
    private static class EnemyCap
    {
        [HarmonyPostfix]
        private static void Postfix(EnemyDirector __instance)
        {
            if (!ModConfig.ScaleEnemyCap.Value || !SemiFunc.IsMasterClientOrSingleplayer())
                return;
            float mult = Mathf.Min(ModConfig.EnemyCapMax.Value,
                1f + ModConfig.EnemyCapPerLevel.Value * Depth(ModConfig.EnemyCapStartLevel.Value));
            int extra = Mathf.RoundToInt(__instance.totalAmount * (mult - 1f));
            if (extra <= 0)
                return;

            // Extra picks come from the hardest tier the run has already unlocked.
            var list = SemiFunc.RunGetDifficultyMultiplier3() > 0f ? __instance.enemiesDifficulty3
                : SemiFunc.RunGetDifficultyMultiplier2() > 0f ? __instance.enemiesDifficulty2
                : __instance.enemiesDifficulty1;
            for (int i = 0; i < extra; i++)
                __instance.PickEnemies(list);
            __instance.totalAmount += extra;
        }
    }

    // ---- level size and extraction points -----------------------------------------------------

    [HarmonyPatch(typeof(LevelGenerator), "TileGeneration")]
    private static class LevelSize
    {
        [HarmonyPrefix]
        private static void Prefix(LevelGenerator __instance)
        {
            if (!ModConfig.ScaleLevelSize.Value || !SemiFunc.IsMasterClientOrSingleplayer() || !SemiFunc.RunIsLevel())
                return;
            float mult = Mathf.Min(ModConfig.LevelSizeMax.Value,
                1f + ModConfig.LevelSizePerLevel.Value * Depth(ModConfig.LevelSizeStartLevel.Value));
            if (mult > 1f)
                __instance.DebugLevelSize = mult; // the game's own multiplier on module count and grid size
        }

        // Wrap the generation coroutine: run it up to its first pause (module count and the extraction
        // amount are decided by then; the points are only placed afterwards) and add the bonus.
        [HarmonyPostfix]
        private static IEnumerator Postfix(IEnumerator __result, LevelGenerator __instance)
        {
            bool first = true;
            while (__result.MoveNext())
            {
                if (first)
                {
                    first = false;
                    AddExtractions(__instance);
                }
                yield return __result.Current;
            }
        }

        private static void AddExtractions(LevelGenerator gen)
        {
            if (!ModConfig.ScaleExtractions.Value || !SemiFunc.IsMasterClientOrSingleplayer() || !SemiFunc.RunIsLevel())
                return;
            if (gen.ExtractionAmount <= 0)
                return; // special/small levels without extraction stay as they are
            int interval = Mathf.Max(1, ModConfig.ExtractionBonusInterval.Value);
            int bonus = Depth(0) / interval;
            gen.ExtractionAmount = Mathf.Min(ModConfig.ExtractionMax.Value, gen.ExtractionAmount + bonus);
        }
    }

    // ---- moon phases --------------------------------------------------------------------------

    [HarmonyPatch(typeof(RunManager), "CalculateMoonLevel")]
    private static class Moon
    {
        [HarmonyPostfix]
        private static void Postfix(int _levelsCompleted, ref int __result)
        {
            if (!ModConfig.ScaleMoon.Value)
                return;
            __result = Mathf.Max(0, (_levelsCompleted + 1) / Mathf.Max(1, ModConfig.MoonLevelsPerPhase.Value));
        }
    }
}
