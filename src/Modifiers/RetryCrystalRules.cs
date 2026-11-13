using System;

namespace IncreaseHardMod.Modifiers;

/// <summary>Pure rules for battery crystals on retried levels (no Unity), shared by the game and the tests.</summary>
public static class RetryCrystalRules
{
    public const int MaxCrystals = 30;

    /// <summary>A run level is a retry when the previous generated level was a run level of the same save at
    /// the same number of completed levels (the game keeps HP and battery charge between the attempts).</summary>
    public static bool IsRetry(string previousRunStep, string runStep) =>
        !string.IsNullOrEmpty(runStep) && runStep == previousRunStep;

    public static string RunStep(string saveFile, int levelsCompleted) => (saveFile ?? "") + ":" + levelsCompleted;

    /// <summary>How many crystals a retried level gets for a roll in [0, 1), between min and max (clamped to 0..MaxCrystals).</summary>
    public static int Count(double roll, int min, int max)
    {
        min = Math.Min(Math.Max(min, 0), MaxCrystals);
        max = Math.Min(Math.Max(max, 0), MaxCrystals);
        if (max < min) max = min;
        return min + (int)(Math.Min(Math.Max(roll, 0d), 0.999999d) * (max - min + 1));
    }

    /// <summary>Index of the emptiest battery that is not full (charges in percent), or -1 when all are full.</summary>
    public static int Emptiest(float[] charges)
    {
        int best = -1;
        for (int i = 0; i < charges.Length; i++)
            if (charges[i] < 99.5f && (best < 0 || charges[i] < charges[best])) best = i;
        return best;
    }

    /// <summary>New charge after a crystal adds <paramref name="percent"/>, never past 100.</summary>
    public static float Charge(float current, float percent) => Math.Min(100f, Math.Max(0f, current) + Math.Max(0f, percent));
}
