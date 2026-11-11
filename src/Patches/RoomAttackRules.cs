namespace IncreaseHardMod.Patches;

/// <summary>Engine-free per-room attack rules (checked by tests/NetworkTests).</summary>
internal static class RoomAttackRules
{
    /// <summary>At most four attackers per room: a fifth monster may not join.</summary>
    internal const int MaxThreats = 4;

    /// <summary>How many monsters of one kind may attack in the same room at once (by EnemyParent.enemyName).</summary>
    internal static int TypeCap(string enemyName) => enemyName switch
    {
        "Loom" => 1,
        "Robe" => 2,
        "Huntsman" => 1,
        "Cleanup Crew" => 1,
        _ => int.MaxValue,
    };

    internal static bool CanEngage(int engagedInRoom, int sameTypeInRoom, string enemyName) =>
        engagedInRoom < MaxThreats && sameTypeInRoom < TypeCap(enemyName);
}
