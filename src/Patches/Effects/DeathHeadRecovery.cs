using System.Runtime.CompilerServices;
using HarmonyLib;
using UnityEngine;
using UnityEngine.AI;

namespace IncreaseHardMod.Patches.Effects;

/// <summary>Recover players and their existing death heads after a sustained exit from playable space.</summary>
internal static class DeathHeadRecovery
{
    private const float CheckInterval = .5f, OutsideSeconds = 3f;
    private static readonly int Rooms = LayerMask.GetMask("RoomVolume");
    private static readonly int Geometry = LayerMask.GetMask("Default", "PlayerOnlyCollision");
    private static readonly int BodyObstructions = LayerMask.GetMask("Default", "PlayerOnlyCollision",
        "Player", "Enemy", "PhysGrabObject", "PhysGrabObjectCart", "PhysGrabObjectHinge", "StaticGrabObject");
    private static readonly ConditionalWeakTable<Component, OutsideWatch> Watches = new();

    private sealed class OutsideWatch
    {
        internal float NextCheck, OutsideSince = -1f;
        internal bool SawPlayableSpace;
    }

    private static bool TryFindSafePosition(Vector3 origin, out Vector3 destination)
    {
        float bestDistance = float.PositiveInfinity;
        Vector3 bestPosition = default;
        Consider(origin, 6f);
        foreach (var point in SemiFunc.LevelPointsGetAll())
            if (point != null) Consider(point.transform.position, 1f);

        if (float.IsPositiveInfinity(bestDistance) && TruckSafetySpawnPoint.instance != null)
        {
            var truck = TruckSafetySpawnPoint.instance.transform.position;
            if (HasStandingClearance(truck))
            {
                destination = truck;
                return true;
            }
        }
        destination = bestPosition;
        return !float.IsPositiveInfinity(bestDistance);

        void Consider(Vector3 probe, float snapDistance)
        {
            // Even a perfect snap from this probe cannot beat the current candidate.
            if (Vector3.Distance(probe, origin) - snapDistance >= bestDistance ||
                !NavMesh.SamplePosition(probe, out var floor, snapDistance, NavMesh.AllAreas)) return;
            float distance = Vector3.Distance(floor.position, origin);
            if (distance >= bestDistance || !HasStandingClearance(floor.position)) return;
            bestDistance = distance;
            bestPosition = floor.position;
        }
    }

    /// <summary>
    /// Host: put a death head on the nearest safe floor to where it is now (beside the pit it fell into, or where it
    /// left the level). The truck is only a last resort when the level has no reachable floor at all.
    /// </summary>
    internal static bool TryMoveHeadNear(PlayerDeathHead head)
    {
        if (head == null || head.physGrabObject == null) return false;
        var grab = head.physGrabObject;
        if (!TryFindSafePosition(grab.transform.position, out var destination)) return false;
        for (int i = grab.playerGrabbing.Count - 1; i >= 0; i--)
            if (grab.playerGrabbing[i] != null)
                grab.playerGrabbing[i].OverrideGrabRelease(grab.photonView != null ? grab.photonView.ViewID : -1);
        grab.Teleport(destination + Vector3.up * .5f, grab.transform.rotation);
        if (grab.rb != null) { grab.rb.linearVelocity = Vector3.zero; grab.rb.angularVelocity = Vector3.zero; }
        Plugin.Log.LogInfo($"[DeathHead] Head placed on the floor near its death spot at {destination}.");
        return true;
    }

    private static bool HasStandingClearance(Vector3 floor)
    {
        var feet = floor + Vector3.up * .29f;
        var head = floor + Vector3.up * 1.52f;
        const float radius = .22f;
        if (!Physics.CheckSphere(feet, .15f, Rooms, QueryTriggerInteraction.Collide) ||
            Physics.CheckCapsule(feet, head, radius, BodyObstructions, QueryTriggerInteraction.Ignore)) return false;
        foreach (var collider in Physics.OverlapCapsule(feet, head, radius, Physics.AllLayers, QueryTriggerInteraction.Collide))
            if (collider.isTrigger && collider.GetComponentInParent<HurtCollider>() is { deathPit: true, playerKill: true })
                return false;
        return true;
    }

    private static bool PlayingLevel => SemiFunc.IsMasterClientOrSingleplayer() &&
        LevelGenerator.Instance != null && LevelGenerator.Instance.Generated &&
        GameDirector.instance != null && GameDirector.instance.currentState == GameDirector.gameState.Main &&
        SemiFunc.RunIsLevel();

    private static bool SustainedExit(Component subject, Vector3 position, bool requirePriorSafePosition)
    {
        var watch = Watches.GetValue(subject, _ => new OutsideWatch());
        float now = Time.time;
        if (now < watch.NextCheck) return false;
        watch.NextCheck = now + CheckInterval;

        // Probe the body itself. The game's broader room checks can still overlap a room
        // from the far side of a ceiling. Walkable floor covers gaps between room volumes.
        bool playable = Physics.CheckSphere(position, .15f, Rooms, QueryTriggerInteraction.Collide) ||
            (NavMesh.SamplePosition(position, out var floor, 1f, NavMesh.AllAreas) &&
             !Physics.Linecast(position, floor.position + Vector3.up * .1f, Geometry, QueryTriggerInteraction.Ignore));
        if (playable)
        {
            watch.SawPlayableSpace = true;
            watch.OutsideSince = -1f;
            return false;
        }
        if (requirePriorSafePosition && !watch.SawPlayableSpace) return false;
        if (watch.OutsideSince < 0f) watch.OutsideSince = now;
        return now - watch.OutsideSince >= OutsideSeconds;
    }

    [HarmonyPatch(typeof(PlayerAvatar), nameof(PlayerAvatar.Update))]
    private static class RecoverPlayer
    {
        [HarmonyPostfix]
        private static void Postfix(PlayerAvatar __instance)
        {
            if (!PlayingLevel || !__instance.spawned || __instance.deadSet || __instance.isDisabled ||
                __instance.playerHealth == null || !__instance.playerHealth.healthSet || __instance.playerHealth.health <= 0)
            {
                Watches.Remove(__instance);
                return;
            }
            var tumble = __instance.tumble;
            var body = tumble != null ? tumble.physGrabObject : null;
            if (body == null || body.rb == null) return;
            var position = __instance.isTumbling
                ? body.transform.position
                : __instance.transform.position + Vector3.up * .3f;
            if (!SustainedExit(__instance, position, requirePriorSafePosition: true) ||
                !TryFindSafePosition(position, out var destination)) return;

            Watches.Remove(__instance);
            // Use the same networked relocation as the game's own out-of-bounds rescue.
            if ((position - destination).sqrMagnitude > 25f) __instance.OutOfBoundsCameraTransition(destination);
            __instance.FallDamageResetSet(2f);
            tumble.TumbleSet(_isTumbling: true, _playerInput: false, _reset: true);
            body.Teleport(destination + Vector3.up * .3f, __instance.transform.rotation);
            body.rb.linearVelocity = Vector3.zero;
            body.rb.angularVelocity = Vector3.zero;
            Plugin.Log.LogInfo($"[DeathHead] Moved living player from outside the level at {position} to {destination}.");
        }
    }

    [HarmonyPatch(typeof(PlayerDeathHead), nameof(PlayerDeathHead.Update))]
    private static class RecoverHead
    {
        [HarmonyPostfix]
        private static void Postfix(PlayerDeathHead __instance)
        {
            if (!PlayingLevel || !__instance.setup || !__instance.triggered || __instance.triggeredTimer > 0f ||
                __instance.physGrabObject == null)
            {
                Watches.Remove(__instance);
                return;
            }
            var position = __instance.physGrabObject.transform.position;
            if (!SustainedExit(__instance, position, requirePriorSafePosition: false)) return;
            // Back onto the nearest safe floor near where it left the level, not to the truck or extraction.
            if (!TryMoveHeadNear(__instance)) return;

            Watches.Remove(__instance);
            __instance.outsideLevelTimer = 0f;
            Plugin.Log.LogInfo($"[DeathHead] Returned head from outside the level at {position}.");
        }
    }
}
