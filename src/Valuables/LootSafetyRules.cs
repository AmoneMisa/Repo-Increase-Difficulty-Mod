using System;
using System.Globalization;

namespace IncreaseHardMod.Valuables;

public enum LootTopUpReason { None, Shortfall, Stall }

/// <summary>
/// Engine-free rules for the loot safety net (<see cref="LootSafetyNet"/>), shared with tests/CollectionTests.
///
/// The game's own top-up compares the value of EVERY valuable on the map against the open extraction point's
/// goal, so loot nobody can reach (fallen outside the rooms, held by a drone) keeps it from ever firing. The
/// safety net counts only loot the team can get to, and also tops up when an open point stops making progress.
/// </summary>
public static class LootSafetyRules
{
    /// <summary>Value still missing when only reachable loot (including loot already in the point) counts.</summary>
    public static int Shortfall(int goal, int reachableValue) => Math.Max(0, goal - Math.Max(0, reachableValue));

    /// <summary>What a stalled point still needs: its goal minus what already sits in it.</summary>
    public static int StallTarget(int goal, int haulInPoint) => Math.Max(0, goal - Math.Max(0, haulInPoint));

    /// <summary>An open point counts as stalled after <paramref name="stallSeconds"/> without its haul rising. 0 turns it off.</summary>
    public static bool Stalled(float secondsSinceProgress, float stallSeconds) =>
        stallSeconds > 0f && secondsSinceProgress >= stallSeconds;

    /// <summary>A shortfall wins over a stall; a stall only tops up what the point still needs.</summary>
    public static LootTopUpReason Decide(int goal, int reachableValue, int haulInPoint,
        float secondsSinceProgress, float stallSeconds, out int target)
    {
        target = Shortfall(goal, reachableValue);
        if (target > 0)
            return LootTopUpReason.Shortfall;
        if (Stalled(secondsSinceProgress, stallSeconds))
        {
            target = StallTarget(goal, haulInPoint);
            if (target > 0)
                return LootTopUpReason.Stall;
        }
        target = 0;
        return LootTopUpReason.None;
    }

    /// <summary>Loot counts unless it is outside every room or held by a drone. Fresh spawns always count,
    /// because their room check has not run yet.</summary>
    public static bool Reachable(bool outsideRooms, bool ghosted, bool justSpawned) =>
        justSpawned || (!outsideRooms && !ghosted);

    public static bool KeepSpawning(int remaining, int spawned, int maxSpawns) => remaining > 0 && spawned < maxSpawns;

    /// <summary>Compact money text in the game's style: $900, $12K, $12.5K.</summary>
    public static string Money(int value)
    {
        value = Math.Max(0, value);
        if (value < 1000)
            return "$" + value.ToString(CultureInfo.InvariantCulture);
        return "$" + (Math.Floor(value / 100f) / 10f).ToString("0.#", CultureInfo.InvariantCulture) + "K";
    }

    public static string Notice(int count, int value, bool marked) =>
        $"New loot appeared: {count} valuable{(count == 1 ? "" : "s")} worth {Money(value)}" +
        (marked ? ", marked on the map" : "");
}
