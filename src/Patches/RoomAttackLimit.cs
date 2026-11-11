using System.Collections.Generic;
using System.Runtime.CompilerServices;
using HarmonyLib;
using UnityEngine;

namespace IncreaseHardMod.Patches;

/// <summary>
/// Limit encounters in the target's room while leaving patrols and spawns active across the level: at most four
/// attackers at once per room (a gnome pack and a banger pack each count as one), and per room at most one Loom,
/// two Robes, one Huntsman and one Cleanup Crew (RoomAttackRules). Monsters moving between rooms are counted
/// in the room of the player they attack. The mod's own monsters ask <see cref="CanAttack"/> as well.
/// </summary>
internal static class RoomAttackLimit
{
    private const int MaxThreats = RoomAttackRules.MaxThreats;
    private const float EngagementSeconds = 3f; // keep the slot during a brief break in sight
    private static readonly ConditionalWeakTable<RoomVolume, List<Engagement>> Rooms = new();

    private enum Pack { None, Gnomes, Bangers }

    private sealed class Engagement
    {
        internal EnemyParent Attacker;
        internal Pack Group;
        internal float Expires;
    }

    private static RoomVolume RoomOf(PlayerAvatar player)
    {
        var volumes = player.RoomVolumeCheck;
        if (volumes == null || volumes.CurrentRooms == null) return null;
        RoomVolume room = null;
        foreach (var candidate in volumes.CurrentRooms)
        {
            if (candidate == null) continue;
            if (room == null || (candidate.priorityVolume && !room.priorityVolume) ||
                (candidate.priorityVolume == room.priorityVolume &&
                 (candidate.priority > room.priority ||
                  (candidate.priority == room.priority && candidate.GetInstanceID() < room.GetInstanceID()))))
                room = candidate;
        }
        return room;
    }

    private static bool TryEngage(Enemy enemy, PlayerAvatar player)
    {
        if (enemy == null || enemy.EnemyParent == null || player == null || player.isDisabled ||
            !SemiFunc.RunIsLevel()) return true;
        // The Golden Pig is neutral: seeing a player must not take an attacker slot.
        if (enemy.GetComponent<Monsters.PigBrain>() != null) return true;
        var room = RoomOf(player);
        if (room == null) return true;

        var attacker = enemy.EnemyParent;
        Pack group = attacker.enemyName switch
        {
            "Gnome" => Pack.Gnomes,
            "Banger" => Pack.Bangers,
            _ => Pack.None,
        };
        var engagements = Rooms.GetValue(room, _ => new List<Engagement>(MaxThreats));
        float now = Time.time;
        for (int i = engagements.Count - 1; i >= 0; i--)
        {
            var entry = engagements[i];
            if (entry.Expires <= now || (entry.Group == Pack.None && (entry.Attacker == null || !entry.Attacker.Spawned)))
                engagements.RemoveAt(i);
        }
        foreach (var entry in engagements)
        {
            if (group == Pack.None ? entry.Attacker != attacker : entry.Group != group) continue;
            entry.Expires = now + EngagementSeconds;
            return true;
        }
        int sameType = 0;
        foreach (var entry in engagements)
            if (entry.Attacker != null && entry.Attacker.enemyName == attacker.enemyName) sameType++;
        if (!RoomAttackRules.CanEngage(engagements.Count, sameType, attacker.enemyName)) return false;
        engagements.Add(new Engagement { Attacker = attacker, Group = group, Expires = now + EngagementSeconds });
        return true;
    }

    /// <summary>
    /// Host check for attacks that do not go through vision or PlayerHealth.Hurt (the mod's own monsters' grabs,
    /// spells and stares): true when <paramref name="enemy"/> may engage <paramref name="player"/> in that room.
    /// </summary>
    internal static bool CanAttack(Enemy enemy, PlayerAvatar player) => !SemiFunc.IsMasterClientOrSingleplayer() || TryEngage(enemy, player);

    [HarmonyPatch(typeof(EnemyVision), nameof(EnemyVision.VisionTrigger))]
    private static class AcquireTarget
    {
        [HarmonyPrefix]
        private static bool Prefix(EnemyVision __instance, PlayerAvatar player) =>
            !SemiFunc.IsMasterClientOrSingleplayer() || TryEngage(__instance.Enemy, player);
    }

    [HarmonyPatch(typeof(Enemy), nameof(Enemy.SetChaseTarget))]
    private static class StartChase
    {
        [HarmonyPrefix]
        private static bool Prefix(Enemy __instance, PlayerAvatar playerAvatar)
        {
            // The native method can set ChaseBegin directly even if the vision event is suppressed.
            if (!SemiFunc.IsMasterClientOrSingleplayer() || playerAvatar == null || !__instance.HasVision || __instance.DisableChaseTimer > 0f ||
                __instance.Vision.DisableTimer > 0f || EnemyDirector.instance.debugNoVision.Contains(playerAvatar.steamID))
                return true;
            return TryEngage(__instance, playerAvatar);
        }
    }

    // Some attacks start outside vision (such as retaliating after a grab), or follow the player into
    // another room. Apply the same room limit to their damage on the player's owning client.
    [HarmonyPatch(typeof(PlayerHealth), nameof(PlayerHealth.Hurt))]
    private static class EnemyHit
    {
        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        private static bool Prefix(PlayerHealth __instance, int damage, int enemyIndex, bool hurtByHeal)
        {
            if (damage <= 0 || enemyIndex < 0 || hurtByHeal || __instance.invincibleTimer > 0f ||
                __instance.godMode || (GameManager.Multiplayer() && (__instance.photonView == null || !__instance.photonView.IsMine)) ||
                GameDirector.instance == null || GameDirector.instance.currentState != GameDirector.gameState.Main)
                return true;
            return TryEngage(SemiFunc.EnemyGetFromIndex(enemyIndex), __instance.playerAvatar);
        }
    }
}
