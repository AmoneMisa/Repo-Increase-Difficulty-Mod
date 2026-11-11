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
            if (!PhotonNetwork.IsMasterClient) { LateJoinBattery.Receive(ev); return; }
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
            else if (hinge.broken) { hinge.photon.RPC("HingeBreakRPC", target); doors++; }
        }

        if (GameDirector.instance != null)
            foreach (var avatar in GameDirector.instance.PlayerList)
            {
                if (avatar == null || !avatar.deadSet || avatar.photonView == null) continue;
                if (avatar.photonView.Owner == target) continue; // the joiner's own avatar is theirs to run
                avatar.photonView.RPC("PlayerDeathRPC", target, -1);
                deaths++;
            }

        ExtractionPoint anyPoint = null;
        foreach (var point in Object.FindObjectsByType<ExtractionPoint>(FindObjectsSortMode.None))
        {
            if (point == null || point.photonView == null || point.photonView.ViewID == 0) continue;
            anyPoint ??= point;
            if (point.haulGoalFetched) point.photonView.RPC("HaulGoalSetRPC", target, point.haulGoal);
            if (point.currentState != ExtractionPoint.State.Idle) { point.photonView.RPC("StateSetRPC", target, point.currentState); points++; }
        }
        if (anyPoint != null && RoundDirector.instance != null && RoundDirector.instance.extractionPointSurplus > 0)
            anyPoint.photonView.RPC("ExtractionPointSurplusRPC", target, RoundDirector.instance.extractionPointSurplus);

        PhotonNetwork.SendAllOutgoingCommands();
        Plugin.Log.LogInfo($"[LateJoin] Caught {target.NickName} up: {values} loot value(s), {doors} door(s), {deaths} dead player(s), {points} extraction point state(s).");
    }
}
