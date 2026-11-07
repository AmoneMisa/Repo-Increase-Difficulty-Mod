using System;

namespace IncreaseHardMod.Skills;

/// <summary>Pure rules for skill boxes (no Unity), shared by the game and the tests.</summary>
public static class SkillBoxRules
{
    public const int MaxBoxes = 5;
    public const int MaxPoints = 3;

    /// <summary>Points a box gives for a roll in [0, 1): weights[i] is the weight of i points.</summary>
    public static int RollPoints(double roll, int w0, int w1, int w2, int w3)
    {
        int[] weights = { Math.Max(0, w0), Math.Max(0, w1), Math.Max(0, w2), Math.Max(0, w3) };
        int total = weights[0] + weights[1] + weights[2] + weights[3];
        if (total <= 0) return 0;
        double pick = Math.Min(Math.Max(roll, 0d), 0.999999d) * total;
        for (int i = 0; i < weights.Length; i++)
        {
            if (pick < weights[i]) return i;
            pick -= weights[i];
        }
        return MaxPoints;
    }

    /// <summary>How many boxes a level gets for a roll in [0, 1), between min and max (both clamped to 0..5).</summary>
    public static int Count(double roll, int min, int max)
    {
        min = Math.Min(Math.Max(min, 0), MaxBoxes);
        max = Math.Min(Math.Max(max, 0), MaxBoxes);
        if (max < min) max = min;
        int span = max - min + 1;
        int step = (int)(Math.Min(Math.Max(roll, 0d), 0.999999d) * span);
        return min + step;
    }
}
