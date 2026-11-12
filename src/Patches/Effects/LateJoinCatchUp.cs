using ExitGames.Client.Photon;
using HarmonyLib;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

namespace IncreaseHardMod.Patches.Effects;

/// <summary>
/// Catches a late joiner up on level state the game sends only once, to the players present at the time, and never
/// keeps for later joiners:
///  - loot values (ValuableObject.DollarValueSetRPC, sent to Others once at spawn): otherwise the joiner sees $0 loot;
///  - broken-off and destroyed doors (PhysGrabHinge.HingeBreakRPC / DestroyHingeRPC);
///  - dead players (PlayerAvatar.PlayerDeathRPC);
///  - extraction points: haul goal, state and surplus (ExtractionPoint.HaulGoalSetRPC / StateSetRPC / SurplusRPC).
/// When a guest's own level finishes generating it asks the host (event <see cref="RequestCode"/>); if the host's
/// level has been running for a while (so this is a late join, not the normal level start), the host re-sends those
/// same native RPCs to that one player. The idea follows L.A.T.E's catch-up sync; this is an independent
/// implementation (no code taken), using only the game's own RPCs.
/// </summary>
internal static class LateJoinCatchUp
{
    internal const byte RequestCode = 161; // request + typed, host-only battery response
    private const float LateAfterSeconds = 8f;
    private static float _hostGeneratedAt = -1f;
    private static bool _subscribed;

    private static void Subscribe()
    {
        if (_subscribed || PhotonNetwork.NetworkingClient == null) return;
        PhotonNetwork.NetworkingClient.EventReceived += OnEvent;
        _subscribed = true;
    }

    [HarmonyPatch(typeof(LevelGenerator), "GenerateDoneApply")]
    private static class Generated
    {
        [HarmonyPostfix]
        private static void Postfix()
        {
            try
            {
                Subscribe();
                LateJoinBattery.Begin();
                if (!ModConfig.LateJoinEnabled.Value || !SemiFunc.IsMultiplayer() || !PhotonNetwork.InRoom) return;
                if (PhotonNetwork.IsMasterClient) { _hostGeneratedAt = Time.realtimeSinceStartup; return; }
                if (!SemiFunc.RunIsLevel()) return;
                PhotonNetwork.RaiseEvent(RequestCode, null,
                    new RaiseEventOptions { Receivers = ReceiverGroup.MasterClient }, SendOptions.SendReliable);
            }
            catch (System.Exception e) { Plugin.Log.LogWarning($"[LateJoin] Catch-up request failed harmlessly: {e.Message}"); }
        }
    }

    private static void OnEvent(EventData ev)
    {
        if (ev.Code != RequestCode || !ModConfig.LateJoinEnabled.Value) return;
        try
        {
            if (!PhotonNetwork.IsMasterClient)
            {
                if (ev.CustomData is object[] round && round.Length == 3 && round[0] is byte rop && rop == RoundOp) { ReceiveRound(ev, round); return; }
                if (ev.CustomData is object[] door && door.Length == 4 && door[0] is byte dop && dop == DoorOp) { ReceiveDoor(ev, door); return; }
                LateJoinBattery.Receive(ev);
                return;
            }
            if (ev.CustomData is object[] request && request.Length == 1 && request[0] is byte op && op == 2)
            { LateJoinBattery.OnRequest(ev.Sender); return; }
            if (ev.CustomData != null) return;
            if (!SemiFunc.RunIsLevel() || LevelGenerator.Instance == null || !LevelGenerator.Instance.Generated) return;
            // At the normal level start every guest asks too, before anything has happened: nothing to catch up on.
            if (_hostGeneratedAt < 0f || Time.realtimeSinceStartup - _hostGeneratedAt < LateAfterSeconds) return;
            var target = PhotonNetwork.CurrentRoom?.GetPlayer(ev.Sender);
            if (target == null) return;
            Send(target);
        }
        catch (System.Exception e) { Plugin.Log.LogWarning($"[LateJoin] Catch-up sync failed harmlessly: {e.Message}"); }
    }

    private const byte RoundOp = 3, DoorOp = 4;

    // Joiner: put a broken door where it really lies on the host, as a loose object (the same state HingeBreakRPC
    // sets: grab layer, no hinge), so it no longer stands in the doorway and can be picked up.
    private static void ReceiveDoor(EventData ev, object[] data)
    {
        if (PhotonNetwork.MasterClient == null || ev.Sender != PhotonNetwork.MasterClient.ActorNumber ||
            data.Length != 4 || data[1] is not int id || data[2] is not Vector3 position || data[3] is not Quaternion rotation) return;
        var view = PhotonView.Find(id);
        var hinge = view != null ? view.GetComponent<PhysGrabHinge>() : null;
        if (hinge == null) return;
        if (hinge.joint != null) { Object.Destroy(hinge.joint); hinge.joint = null; }
        int layer = LayerMask.NameToLayer("PhysGrabObject");
        hinge.gameObject.layer = layer;
        foreach (Transform child in hinge.transform) child.gameObject.layer = layer;
        var pgo = hinge.physGrabObject;
        hinge.transform.SetPositionAndRotation(position, rotation);
        if (pgo != null && pgo.rb != null) { pgo.rb.position = position; pgo.rb.rotation = rotation; }
        Plugin.Log.LogInfo($"[LateJoin] Broken door {id} placed where it lies on the host.");
    }

    // Joiner: how many extraction points are already done (the game only counts them as they happen).
    private static void ReceiveRound(EventData ev, object[] data)
    {
        if (PhotonNetwork.MasterClient == null || ev.Sender != PhotonNetwork.MasterClient.ActorNumber ||
            data[1] is not int completed || data[2] is not bool allDone || completed < 0 || completed > 64) return;
        var round = RoundDirector.instance;
        if (round == null) return;
        round.extractionPointsCompleted = Mathf.Max(round.extractionPointsCompleted, completed);
        round.allExtractionPointsCompleted |= allDone;
        Plugin.Log.LogInfo($"[LateJoin] Extraction progress from the host: {completed} completed{(allDone ? ", all done" : "")}.");
    }

    private static void Send(Player target)
    {
        LateJoinBattery.SendAll(target);
        int values = 0, doors = 0, deaths = 0, points = 0;

        foreach (var valuable in Object.FindObjectsByType<ValuableObject>(FindObjectsSortMode.None))
        {
            if (valuable == null || !valuable.dollarValueSet) continue;
            var view = valuable.GetComponent<PhotonView>();
            if (view == null || view.ViewID == 0) continue;
            view.RPC("DollarValueSetRPC", target, valuable.dollarValueCurrent);
            values++;
        }

        foreach (var hinge in Object.FindObjectsByType<PhysGrabHinge>(FindObjectsSortMode.None))
        {
            if (hinge == null || hinge.photon == null || hinge.photon.ViewID == 0) continue;
            if (hinge.dead) { hinge.photon.RPC("DestroyHingeRPC", target); doors++; }
            else if (hinge.broken)
            {
                hinge.photon.RPC("HingeBreakRPC", target);
                // A broken door is a loose physics object whose transform only syncs while it moves: a resting one
                // stayed in the doorway on the joiner's screen, blocking a passage everyone else could use.
                // Teleport it to where it really lies (host-authoritative; same place for everyone else).
                var pgo = hinge.physGrabObject;
                if (pgo != null && pgo.rb != null)
                {
                    pgo.Teleport(pgo.rb.position, pgo.rb.rotation);
                    pgo.rb.WakeUp();
                    // A resting body may never be serialized again, so the joiner kept the door upright in its
                    // frame (blocking the way, and its hinge layer made it look unusable). Send the pose itself.
                    PhotonNetwork.RaiseEvent(RequestCode, new object[] { DoorOp, hinge.photon.ViewID, pgo.rb.position, pgo.rb.rotation },
                        new RaiseEventOptions { TargetActors = new[] { target.ActorNumber } }, SendOptions.SendReliable);
                }
                doors++;
            }
        }

        if (GameDirector.instance != null)
            foreach (var avatar in GameDirector.instance.PlayerList)
            {
                if (avatar == null || !avatar.deadSet || avatar.photonView == null) continue;
                if (avatar.photonView.Owner == target) continue; // the joiner's own avatar is theirs to run
                avatar.photonView.RPC("PlayerDeathRPC", target, -1);
                deaths++;
            }

        // Extraction points. Order matters on the joiner:
        //  1. every point's own goal, the OPEN point's last (HaulGoalSetRPC also sets the shared quota shown on the HUD);
        //  2. the round director's "a point is open" + which one (ExtractionPointActivateRPC: the HUD quota only shows
        //     while a point is active, the route to the quota follows extractionPointCurrent, and the other points get
        //     locked so they no longer look openable);
        //  3. then each point's real state, which overrides anything the activation did locally;
        //  4. the completed count, which the game never sends after the fact.
        var round = RoundDirector.instance;
        var current = round != null && round.extractionPointActive ? round.extractionPointCurrent : null;
        ExtractionPoint anyPoint = null;
        var all = Object.FindObjectsByType<ExtractionPoint>(FindObjectsSortMode.None);
        foreach (var point in all)
        {
            if (point == null || point.photonView == null || point.photonView.ViewID == 0) continue;
            anyPoint ??= point;
            if (point.haulGoalFetched && point != current) point.photonView.RPC("HaulGoalSetRPC", target, point.haulGoal);
        }
        if (current != null && current.photonView != null && current.photonView.ViewID != 0)
        {
            if (current.haulGoalFetched) current.photonView.RPC("HaulGoalSetRPC", target, current.haulGoal);
            if (round.photonView != null) round.photonView.RPC("ExtractionPointActivateRPC", target, current.photonView.ViewID);
        }
        foreach (var point in all)
        {
            if (point == null || point.photonView == null || point.photonView.ViewID == 0) continue;
            if (point.currentState != ExtractionPoint.State.Idle) { point.photonView.RPC("StateSetRPC", target, point.currentState); points++; }
        }
        if (anyPoint != null && round != null && round.extractionPointSurplus > 0)
            anyPoint.photonView.RPC("ExtractionPointSurplusRPC", target, round.extractionPointSurplus);
        if (round != null)
            PhotonNetwork.RaiseEvent(RequestCode, new object[] { RoundOp, round.extractionPointsCompleted, round.allExtractionPointsCompleted },
                new RaiseEventOptions { TargetActors = new[] { target.ActorNumber } }, SendOptions.SendReliable);

        PhotonNetwork.SendAllOutgoingCommands();
        Plugin.Log.LogInfo($"[LateJoin] Caught {target.NickName} up: {values} loot value(s), {doors} door(s), {deaths} dead player(s), {points} extraction point state(s).");
    }
}
