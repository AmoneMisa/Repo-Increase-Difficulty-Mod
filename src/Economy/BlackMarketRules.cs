using System;

namespace IncreaseHardMod.Economy;

/// <summary>
/// Pure rules for the Black Market's modifier odds (no Unity, shared by the game and the tests). A surprise modifier
/// can be bought down or up a step at a time, from <see cref="MinLevel"/> (never rolled) to <see cref="MaxLevel"/>.
/// </summary>
public static class BlackMarketRules
{
    public const int MinLevel = -3, MaxLevel = 3;

    /// <summary>How much more or less often a modifier is rolled at an odds level: x0.5, x0.25, never / x1.5, x2, x2.5.</summary>
    public static float Multiplier(int level)
    {
        level = Math.Max(MinLevel, Math.Min(MaxLevel, level));
        return level switch
        {
            <= MinLevel => 0f,
            -2 => .25f,
            -1 => .5f,
            0 => 1f,
            _ => 1f + .5f * level,
        };
    }

    /// <summary>
    /// Price of one step as a share of the modifier's base price. <paramref name="harm"/> is its tier from 0 (very
    /// good for the players) to 4 (very bad). Steps that help the crew cost more: making a nasty modifier rarer or a
    /// generous one more common. Three steps down cost a little more than the old flat block (0.4 of it each).
    /// </summary>
    public static float StepFactor(int harm, bool down)
    {
        harm = Math.Max(0, Math.Min(4, harm));
        int helpful = down ? harm : 4 - harm; // 0 = least helpful step, 4 = most helpful
        float[] factor = { .5f, .7f, 1f, 1.5f, 2f };
        return .4f * factor[helpful];
    }

    /// <summary>A step back toward normal odds is free (nothing is refunded); a step away from them is bought.</summary>
    public static bool StepIsPaid(int level, bool down) => down ? level <= 0 : level >= 0;

    public static bool CanStep(int level, bool down) => down ? level > MinLevel : level < MaxLevel;
}
