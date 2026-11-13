using System;

namespace IncreaseHardMod.Gear;

internal static class UtilityGearRules
{
    internal const float GhostSilencerSeconds = 180f, ProtectionDrain = 1.5f, OrbRadius = 1.25f;
    /// <summary>Poison bolt total: the larger of the flat floor and a share of the monster's max HP, plus half the
    /// damage the previous poison still had to deal.</summary>
    internal static float PoisonTotal(int flat, int maxHpPercent, int maxHealth, float leftover) =>
        Math.Max(Math.Max(0, flat), Math.Max(0, maxHealth) * Math.Max(0, maxHpPercent) / 100f) + Math.Max(0f, leftover) * .5f;

    internal static int DefenceCapacity(int configured) => Math.Max(450, configured);
    internal static int LootCapacity(int configured) => Math.Max(10000, configured);
    internal static float DefenceSeconds(float configured) => Math.Max(90f, configured);
    internal static float Protection(int configured) => Math.Min(.9f, Math.Max(30, configured) / 100f);
    internal static float GhostLoss(float loss) => Math.Max(0f, loss) * .5f;
    internal static float SettledBaseline(float previous, float before, float after) =>
        (float.IsNaN(previous) ? before : previous) - Math.Max(0f, before - after);

    // ---- Gun battery: shots versus bars ----
    // The game draws ItemBattery.batteryLifeInt bars (charge rounded to the nearest bar) and refuses to fire at
    // zero bars. A gun only spends exactly what it shows when every shot removes whole bars: ItemBattery.RemoveFullBar
    // (full-bar drain, or the shooting state of a gun that is not one-shot). A fractional percent drain makes the
    // bars and the real charge disagree, and the gun goes dry with up to half a bar still in it.

    /// <summary>True when a shot removes whole bars, so the bars on the gun are exactly the shots left.</summary>
    internal static bool SpendsWholeBars(bool oneShot, bool fullBar) => fullBar || !oneShot;

    /// <summary>Bars one shot removes (a gun that fires through its shooting state always removes one).</summary>
    internal static int BarsPerShot(bool oneShot, bool fullBar, int fullBars) => oneShot && fullBar ? Math.Max(1, fullBars) : 1;

    /// <summary>Shots one full battery gives a gun with these native settings.</summary>
    internal static float ShotsPerCharge(bool oneShot, bool fullBar, int fullBars, int bars, float drain) =>
        Math.Max(1f, SpendsWholeBars(oneShot, fullBar)
            ? Math.Max(1, bars) / (float)BarsPerShot(oneShot, fullBar, fullBars)
            : 100f / Math.Max(.01f, drain));

    /// <summary>Whole shots, never fewer than one.</summary>
    internal static int WholeShots(float shots) => Math.Max(1, (int)Math.Round(shots, MidpointRounding.AwayFromZero));

    /// <summary>The Poison Crossbow: a third fewer shots than its donor gun.</summary>
    internal static int PoisonShots(float donorShots) => WholeShots(donorShots * 2f / 3f);

    /// <summary>
    /// Bars a whole-bar gun shows once extra shots (upgrades, skill tree) and battery trade-offs (drainFactor below 1 =
    /// slower drain, above 1 = dearer) are applied. Capacity changes are expressed only as bars, so a shot keeps costing
    /// exactly the bars it shows; capped at maxBars so the widget stays readable.
    /// </summary>
    internal static int UpgradedBars(int baseBars, float baseShots, float extraShots, float drainFactor, int barsPerShot, int maxBars)
    {
        baseBars = Math.Max(1, baseBars);
        barsPerShot = Math.Max(1, barsPerShot);
        float shots = (Math.Max(1f, baseShots) + Math.Max(0f, extraShots)) / Math.Max(.05f, drainFactor);
        if (Math.Abs(shots - baseShots) < .001f) return baseBars;
        int bars = WholeShots(shots) * barsPerShot;
        int cap = Math.Max(baseBars, maxBars);
        if (bars > cap) bars = Math.Max(barsPerShot, cap - cap % barsPerShot);
        return bars;
    }
}
