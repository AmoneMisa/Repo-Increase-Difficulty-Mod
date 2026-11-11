using System;

namespace IncreaseHardMod.Gear;

internal static class UtilityGearRules
{
    internal const float GhostSilencerSeconds = 180f, ProtectionDrain = 1.5f, OrbRadius = 1.25f;
    internal static int DefenceCapacity(int configured) => Math.Max(450, configured);
    internal static int LootCapacity(int configured) => Math.Max(10000, configured);
    internal static float DefenceSeconds(float configured) => Math.Max(90f, configured);
    internal static float Protection(int configured) => Math.Min(.9f, Math.Max(30, configured) / 100f);
    internal static float GhostLoss(float loss) => Math.Max(0f, loss) * .5f;
    internal static float SettledBaseline(float previous, float before, float after) =>
        (float.IsNaN(previous) ? before : previous) - Math.Max(0f, before - after);
}
