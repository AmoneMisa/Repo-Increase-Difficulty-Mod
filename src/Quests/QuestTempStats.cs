using System.Collections.Generic;
using UnityEngine;

namespace IncreaseHardMod.Quests;

/// <summary>
/// Applies temporary, single-level stat buffs to the LOCAL player and reverts them exactly at
/// level end. Each client runs this for its own character (driven by a host broadcast — see
/// <see cref="Networking.ModNetwork"/> event 186), so the game's "right away" effects (which are
/// gated to the local avatar for stamina/speed/jump) land correctly on everyone.
///
/// We poke the live <see cref="PlayerController"/> / <see cref="PhysGrabber"/> fields directly
/// (the same fields the game's own Update...RightAway methods write) instead of routing through
/// PunManager's upgrade dictionaries — that keeps the buff off the persistent save and lets us
/// revert the exact delta we added, no matter what the synced dictionaries say.
/// </summary>
public static class QuestTempStats
{
    private struct Buff
    {
        public StatKind Stat;
        public int Amount;
    }

    private static readonly List<Buff> _active = new();

    /// <summary>Apply a buff to the local player and remember it so we can undo it at level end.</summary>
    public static void Apply(StatKind stat, int amount)
    {
        if (amount <= 0)
            return;
        if (ApplyDelta(stat, amount))
            _active.Add(new Buff { Stat = stat, Amount = amount });
    }

    /// <summary>Undo every buff applied this level (called on all clients when the level ends).</summary>
    public static void RevertAll()
    {
        foreach (var b in _active)
            ApplyDelta(b.Stat, -b.Amount);
        _active.Clear();
    }

    // Returns true if the delta was applied (the relevant live object existed).
    private static bool ApplyDelta(StatKind stat, int amount)
    {
        var pc = PlayerController.instance;
        var grabber = SemiFunc.PlayerAvatarLocal() != null ? SemiFunc.PlayerAvatarLocal().physGrabber : null;

        switch (stat)
        {
            case StatKind.Stamina:
                if (pc == null) return false;
                pc.EnergyStart += 10f * amount;
                pc.EnergyCurrent = Mathf.Min(pc.EnergyCurrent, pc.EnergyStart);
                if (amount > 0) pc.EnergyCurrent = pc.EnergyStart;
                return true;

            case StatKind.SprintSpeed:
                if (pc == null) return false;
                pc.SprintSpeed += amount;
                pc.SprintSpeedUpgrades += amount;
                pc.playerOriginalSprintSpeed += amount;
                return true;

            case StatKind.ExtraJump:
                if (pc == null) return false;
                pc.JumpExtra += amount;
                return true;

            case StatKind.GrabStrength:
                if (grabber == null) return false;
                grabber.grabStrength += 0.2f * amount;
                return true;

            case StatKind.GrabRange:
                if (grabber == null) return false;
                grabber.grabRange += amount;
                return true;

            case StatKind.ThrowStrength:
                if (grabber == null) return false;
                grabber.throwStrength += 0.3f * amount;
                return true;

            default:
                return false;
        }
    }
}
