using System;

namespace IncreaseHardMod.Gear;

internal static class GearCombatRules
{
    // Refreshing with a shorter effect must not shorten an existing freeze.
    internal static int RefreshFreeze(float now, float previousEnd, float duration, out float end)
    {
        end = Math.Max(previousEnd, now + duration);
        return previousEnd > now ? 0 : 20;
    }

    internal static float PulledLootLoss(float loss) => loss > 0f ? loss * .2f : loss;
}
