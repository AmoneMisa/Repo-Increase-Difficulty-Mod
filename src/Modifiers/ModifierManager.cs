using IncreaseHardMod.Networking;

namespace IncreaseHardMod.Modifiers;

/// <summary>
/// The single source of truth for "what modifiers are active on the current
/// level". Every feature/patch reads ModifierManager.Current to decide whether
/// and how to alter behavior.
///
/// Lifecycle:
///   - Host: RollForNextLevel() picks the set, stores it locally, and broadcasts
///     it to other clients.
///   - Clients: ApplyReceived() stores the set the host sent.
/// Both paths funnel through SetCurrent so logging/side-effects happen once.
/// </summary>
public static class ModifierManager
{
    public static int Revision { get; private set; }
    private static long _receivedRevision = -1;
    /// <summary>Modifiers active on the level being played right now.</summary>
    public static LevelModifiers Current { get; private set; } = LevelModifiers.Empty;

    /// <summary>
    /// Called on the HOST when the level changes. Rolls the set for the level the
    /// players are about to enter, applies it locally, and tells everyone else.
    /// </summary>
    public static void RollForNextLevel(int levelsCompleted)
    {
        var rolled = ModifierRoller.Roll(levelsCompleted);
        if (Testing.TestShortcuts.ForceMysteryBoxes.Value)
        {
            rolled.Active |= ModifierType.MysteryBoxes;
            rolled.Active &= ~ModifierType.SpreadUpgrades;
        }
        SetCurrent(rolled, "host rolled");
        ModNetwork.Instance?.BroadcastModifiers(rolled);
    }

    /// <summary>Called on CLIENTS when the host's broadcast arrives.</summary>
    public static void ApplyReceived(LevelModifiers modifiers, long revision = -1)
    {
        // A late join snapshot is the same level state, not a new timer/revive budget.
        if (revision >= 0 && revision == _receivedRevision)
            return;
        SetCurrent(modifiers, "received from host");
        _receivedRevision = revision;
    }

    private static void SetCurrent(LevelModifiers modifiers, string source)
    {
        Revision++;
        _receivedRevision = -1;
        Current = modifiers ?? LevelModifiers.Empty;
        StatModifiers.Recalculate(Current.Stats);
        // Per-level effect state must be cleared exactly when the level changes.
        Patches.Effects.AutoReviveEffect.ResetForNewLevel();
        Patches.Effects.LevelTimerController.ResetForNewLevel();
        Plugin.Log.LogInfo($"[Modifiers] ({source}) {Current}");
    }
}
