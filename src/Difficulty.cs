using UnityEngine;

namespace IncreaseHardMod;

/// <summary>
/// Central place that turns "how deep is the run?" into a multiplier.
///
/// The depth comes from RunManager.instance.levelsCompleted, which is synced
/// by the game across all players. Because every client computes the multiplier
/// from that same synced number, everyone lands on the SAME value with no extra
/// networking required (host-authoritative by construction).
/// </summary>
public static class Difficulty
{
    /// <summary>How many levels have been completed this run (0 if unavailable).</summary>
    public static int LevelsCompleted =>
        RunManager.instance != null ? RunManager.instance.levelsCompleted : 0;

    /// <summary>
    /// How many "ramp steps" of difficulty currently apply.
    /// 0 up to and including the start level, then 1 on the very first level after it, and one more
    /// every RampLevelsPerStep levels (start 1, step 3: steps at 2, 5, 8... completed levels).
    /// Rounding UP matters: rounding down held the first boost back a whole interval (4, 7, 10...),
    /// so monsters seemed not to get stronger at all after the configured start.
    /// </summary>
    public static int Steps
    {
        get
        {
            int depth = LevelsCompleted - ModConfig.RampStartLevel.Value;
            if (depth <= 0)
                return 0;
            int perStep = ModConfig.RampLevelsPerStep.Value;
            if (perStep < 1)
                perStep = 1;
            return (depth + perStep - 1) / perStep;
        }
    }

    /// <summary>
    /// Multiplier for a knob configured as (+perLevel each step, capped at max).
    /// Returns 1.0 while still in the vanilla levels.
    /// </summary>
    public static float Multiplier(float perLevel, float max)
    {
        float m = 1f + perLevel * Steps;
        return Mathf.Clamp(m, 1f, max);
    }

    /// <summary>
    /// Multiplier for a knob that should SHRINK with depth (e.g. respawn wait).
    /// Starts at 1.0 and drops by perLevel each step, floored at minFactor.
    /// </summary>
    public static float ReduceMultiplier(float perLevel, float minFactor)
    {
        float m = 1f - perLevel * Steps;
        return Mathf.Clamp(m, minFactor, 1f);
    }
}
