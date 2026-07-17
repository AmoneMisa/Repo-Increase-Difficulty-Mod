namespace IncreaseHardMod.Modifiers;

/// <summary>
/// The full set of surprise modifiers active for ONE level, plus the rolled
/// numeric parameters some of them need. This is the shared "truth" that every
/// client holds identically after the host broadcasts it.
///
/// Kept deliberately small and flat so it serializes to a plain object[] for
/// Photon's RaiseEvent (Photon can send primitive arrays out of the box).
/// </summary>
public sealed class LevelModifiers
{
    /// <summary>Bitmask of which modifiers are active this level.</summary>
    public ModifierType Active;

    // Rolled numeric parameters. Only meaningful when the matching flag is set.
    public int ExtraSouls;              // ExtraSouls: how many souls monsters drop (e.g. 4-10)
    public int PlayerHpPercent = 100;   // ReducedPlayerHp: max HP as a percent of normal (10-90)
    public int MoneyLossPercent;        // MoneyLossOnDeath: percent of money lost per death

    /// <summary>True if the given modifier is active this level.</summary>
    public bool Has(ModifierType m) => (Active & m) != 0;

    /// <summary>An empty set (normal, non-special level).</summary>
    public static LevelModifiers Empty => new();

    public bool IsSpecial => Active != ModifierType.None;

    /// <summary>
    /// Pack into a primitive array for network send. Order MUST match Deserialize.
    /// We send ints only (Photon serializes int[] natively and cheaply).
    /// </summary>
    public object[] Serialize() => new object[]
    {
        (int)Active,
        ExtraSouls,
        PlayerHpPercent,
        MoneyLossPercent,
    };

    /// <summary>Rebuild from the array produced by Serialize (order-sensitive).</summary>
    public static LevelModifiers Deserialize(object[] data)
    {
        var m = new LevelModifiers();
        if (data == null || data.Length < 4)
            return m;

        m.Active           = (ModifierType)(int)data[0];
        m.ExtraSouls       = (int)data[1];
        m.PlayerHpPercent  = (int)data[2];
        m.MoneyLossPercent = (int)data[3];
        return m;
    }

    public override string ToString()
    {
        if (!IsSpecial)
            return "Normal level (no modifiers)";
        return $"Active=[{Active}], ExtraSouls={ExtraSouls}, " +
               $"PlayerHpPercent={PlayerHpPercent}, MoneyLossPercent={MoneyLossPercent}";
    }
}
