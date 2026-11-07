using System;
using System.Globalization;

namespace IncreaseHardMod.Valuables;

public enum LootTopUpReason { None, Shortfall }

/// <summary>
/// Engine-free rules for the loot safety net (<see cref="LootSafetyNet"/>), shared with tests/CollectionTests.
///
/// The game's own top-up compares the value of EVERY valuable on the map against the open extraction point's
/// goal, so loot nobody can reach (fallen outside the rooms, held by a drone) keeps it from ever firing. The
/// safety net counts everything the team can still collect (in the point, carried, in boxes, found or not) and
/// spawns only when even all of that cannot fill the point. It never spawns while enough loot is left.
/// </summary>
public static class LootSafetyRules
{
    /// <summary>Value still missing when only reachable loot (including loot already in the point) counts.</summary>
    public static int Shortfall(int goal, int reachableValue) => Math.Max(0, goal - Math.Max(0, reachableValue));

    /// <summary>Top up only when all the loot the team can still collect cannot fill the point.</summary>
    public static LootTopUpReason Decide(int goal, int reachableValue, out int target)
    {
        target = Shortfall(goal, reachableValue);
        return target > 0 ? LootTopUpReason.Shortfall : LootTopUpReason.None;
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
