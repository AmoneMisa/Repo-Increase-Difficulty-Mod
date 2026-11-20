using System;

namespace IncreaseHardMod.FableRealm;

/// <summary>
/// Which side of the level grid a room exit faces, whether the generator connected it, and which of the two
/// rooms on a shared edge hangs the door. Plain integers, no engine types: tests/FableTests checks these rules.
///
/// Sides follow the generator's grid: 0 top (+Z), 1 right (+X), 2 bottom (-Z), 3 left (-X).
/// </summary>
internal static class FableExitRules
{
    internal const int Top = 0, Right = 1, Bottom = 2, Left = 3;

    /// <summary>
    /// Hinge limits for a gate that swings both ways: the wider of the donor door's two limits, on both sides.
    /// A door that only opens one way (min 0) gets the same reach the other way too.
    /// </summary>
    internal static (float min, float max) BothWays(float min, float max)
    {
        float reach = Math.Max(Math.Abs(min), Math.Abs(max));
        return (-reach, reach);
    }

    /// <summary>Whether a room of this recipe role gets loot volumes. The game's extraction modules have none.</summary>
    internal static bool SpawnsLoot(string role) => !string.Equals(role, "Extraction", StringComparison.Ordinal);

    /// <summary>Share of the game's own valuable count added on top in Fable Realm (its rooms have spots to spare).</summary>
    internal const float LootBoost = .5f;

    /// <summary>How many valuables Fable Realm adds to the <paramref name="spawned"/> the game placed: half as many again.</summary>
    internal static int ExtraLoot(int spawned) => spawned <= 0 ? 0 : (int)Math.Round(spawned * LootBoost, MidpointRounding.AwayFromZero);

    /// <summary>The side an exit at (x, z) of an unrotated 15 m room faces.</summary>
    internal static int LocalSide(float x, float z) =>
        Math.Abs(z) >= Math.Abs(x) ? (z > 0f ? Top : Bottom) : (x > 0f ? Right : Left);

    /// <summary>
    /// The grid side after the room is turned by <paramref name="yawDegrees"/> (Unity yaw: +90 turns +Z into +X).
    /// The game's own ModulePropSwitch counts 90-degree steps while "step &lt; eulerAngles.y", so a yaw read back as
    /// 270.00002 counts four steps and plugs the wrong side; rounding to the nearest quarter turn cannot.
    /// </summary>
    internal static int WorldSide(int localSide, float yawDegrees)
    {
        int steps = (int)Math.Round(yawDegrees / 90.0, MidpointRounding.AwayFromZero);
        return (((localSide + steps) % 4) + 4) % 4;
    }

    /// <summary>
    /// Whether the generator put a room on that side. Only normal rooms have optional exits; passages, dead ends,
    /// extraction, special and start rooms are always placed facing their neighbours.
    /// </summary>
    internal static bool Connected(bool optionalExits, int worldSide, bool top, bool right, bool bottom, bool left) =>
        !optionalExits || worldSide switch { Top => top, Right => right, Bottom => bottom, _ => left };

    /// <summary>
    /// Two rooms meet at every connected edge and both have a doorway there. Only the room below (or to the left)
    /// hangs a door, in its top (or right) exit, so each passage gets exactly one pair of leaves.
    /// </summary>
    internal static bool HangsDoor(int worldSide) => worldSide == Top || worldSide == Right;
}

/// <summary>
/// Second layouts (tools/fable_realm/build_level.py, LAYOUT_SUFFIX): a normal, passage or dead-end room built again
/// with another seed is a room of its own named "{room}-B". Anything that belongs to the room itself, such as its
/// biome, is looked up under the base name. Checked by tests/FableTests.
/// </summary>
internal static class FableLayouts
{
    internal const string Suffix = "-B";

    internal static string BaseId(string roomId) =>
        roomId != null && roomId.Length > Suffix.Length && roomId.EndsWith(Suffix, StringComparison.Ordinal)
            ? roomId.Substring(0, roomId.Length - Suffix.Length) : roomId;
}

/// <summary>When the level-point check may remove points (see FableMonsterPaths). Checked by tests/FableTests.</summary>
internal static class FableMonsterPathRules
{
    /// <summary>
    /// Unreachable points are removed only when they are a few stray spots. When a large share fails, the NavMesh
    /// was built for another agent or the origin is wrong; removing them would empty the level of monster routes.
    /// </summary>
    internal static bool ShouldPrune(int total, int failing) => failing > 0 && total > 0 && failing * 4 <= total;
}
