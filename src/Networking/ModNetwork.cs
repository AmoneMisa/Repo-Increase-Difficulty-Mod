using ExitGames.Client.Photon;
using IncreaseHardMod.Modifiers;
using IncreaseHardMod.Patches.Effects;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

namespace IncreaseHardMod.Networking;

/// <summary>
/// Handles sending and receiving the rolled modifier set over Photon.
///
/// We use Photon's low-level RaiseEvent API (the same one REPO itself uses for
/// kick/ban, codes 123/124). It doesn't need a PhotonView, which is exactly what
/// we want since a BepInEx mod can't easily own one.
///
/// This is a MonoBehaviour so it can implement IOnEventCallback and be registered
/// with PhotonNetwork.AddCallbackTarget — that's how Photon delivers raised
/// events to us via OnEvent().
///
/// IMPORTANT: RaiseEvent to ReceiverGroup.All does NOT echo back to the sender.
/// So the host must apply its rolled state locally itself (ModifierManager does
/// that) and only RaiseEvent to inform the OTHER clients.
/// </summary>
public sealed class ModNetwork : MonoBehaviour, IOnEventCallback
{
    // Our private event codes. Must not collide with REPO's (123/124/199) or PUN's
    // reserved range (200+). 178/179 are free.
    public const byte ModifierEventCode = 178;
    public const byte LevelTimerEventCode = 179;
    // 180 previously carried cart-upgrade state; truck upgrades now sync via REPOLib per-player.
    public const byte PingEventCode = 181;
    public const byte MapVoteCandidatesEventCode = 182;
    public const byte MapVoteBallotEventCode = 183;
    public const byte RoundStatsEventCode = 184;
    public const byte RepairEventCode = 185;

    public static ModNetwork Instance { get; private set; }

    private bool _registered;

    private void Awake()
    {
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnDisable()
    {
        if (_registered)
        {
            PhotonNetwork.RemoveCallbackTarget(this);
            _registered = false;
        }
    }

    // Drives the LevelTimer modifier: the host counts down and periodically
    // broadcasts the authoritative remaining time; clients smooth locally between
    // broadcasts. Persistent (DontDestroyOnLoad), so it survives level loads.
    private void Update()
    {
        // Only be a Photon callback target WHILE ACTUALLY IN A ROOM. Registering on the
        // main menu (the old OnEnable behaviour) interferes with PUN's "pick best region"
        // pinging — the regions list spins forever and online sits on the loading screen.
        // We only need OnEvent while in a room, so register lazily here and drop it otherwise.
        bool inRoom = PhotonNetwork.InRoom;
        if (inRoom && !_registered)
        {
            PhotonNetwork.AddCallbackTarget(this);
            _registered = true;
        }
        else if (!inRoom && _registered)
        {
            PhotonNetwork.RemoveCallbackTarget(this);
            _registered = false;
        }

        LevelTimerController.Tick(Time.deltaTime);
        RoundStats.Tick(Time.deltaTime);
        Drones.LinkManager.Tick(Time.deltaTime);
        LateJoinController.Tick();
        // Client-side: keep every enemy's per-player dictionaries in sync with mid-run joins
        // (and mid-run enemy spawns) so late joiners don't crash the enemy AI coroutines.
        LateJoinController.EnsureEnemyRegistrations();
        // Client-side: unfreeze a mid-run joiner whose PlayerHealth.Fetch() never completed
        // (health/maxHealth stuck at 100 defaults -> immune to all damage AND healing).
        LateJoinController.EnsureLocalHealthInitialized();
    }

    /// <summary>Host broadcasts the authoritative timer state to other clients.</summary>
    public void BroadcastLevelTimer(float remaining, bool running)
    {
        if (!PhotonNetwork.InRoom)
            return;

        var options = new RaiseEventOptions { Receivers = ReceiverGroup.All };
        PhotonNetwork.RaiseEvent(
            LevelTimerEventCode,
            new object[] { remaining, running },
            options,
            SendOptions.SendUnreliable);
    }

    /// <summary>
    /// Host broadcasts the rolled modifiers to all other clients. Reliable so it
    /// can't be dropped. (Late joiners are not covered yet — REPO players join at
    /// the lobby, not mid-level, so this is an acceptable v1 limitation.)
    /// </summary>
    public void BroadcastModifiers(LevelModifiers modifiers)
    {
        if (!PhotonNetwork.InRoom)
            return;

        var options = new RaiseEventOptions { Receivers = ReceiverGroup.All };
        PhotonNetwork.RaiseEvent(
            ModifierEventCode,
            modifiers.Serialize(),
            options,
            SendOptions.SendReliable);
    }

    /// <summary>
    /// Any player broadcasts a ping to all other clients (not echoed to self). The pinged
    /// object's PhotonView <paramref name="viewID"/> (0 for Location pings) lets every client
    /// resolve the SAME PhysGrabObject and spawn the game's native highlight on it.
    /// </summary>
    public void BroadcastPing(byte type, Vector3 pos, string label, string sender, int viewID)
    {
        if (!PhotonNetwork.InRoom)
            return;

        var options = new RaiseEventOptions { Receivers = ReceiverGroup.All };
        PhotonNetwork.RaiseEvent(
            PingEventCode,
            new object[] { type, pos.x, pos.y, pos.z, label, sender, viewID },
            options,
            SendOptions.SendReliable);
    }

    /// <summary>
    /// Host broadcasts the current map-vote candidate set (by level name) plus the authoritative
    /// countdown state (remaining seconds, whether the vote has closed, and the locked winner index
    /// once closed). Rebroadcast periodically so late lobby joiners get the candidates and everyone
    /// stays in sync on the countdown.
    /// </summary>
    public void BroadcastMapVoteCandidates(int sessionId, string[] levelNames, float remainingSeconds, bool closed, int lockedWinner)
    {
        if (!PhotonNetwork.InRoom)
            return;

        var options = new RaiseEventOptions { Receivers = ReceiverGroup.All };
        PhotonNetwork.RaiseEvent(
            MapVoteCandidatesEventCode,
            new object[] { sessionId, levelNames, remainingSeconds, closed, lockedWinner },
            options,
            SendOptions.SendReliable);
    }

    /// <summary>Any player broadcasts their map-vote ballot (not echoed to self).</summary>
    public void BroadcastMapVoteBallot(int sessionId, int actorNumber, int choiceIndex, string voterName)
    {
        if (!PhotonNetwork.InRoom)
            return;

        var options = new RaiseEventOptions { Receivers = ReceiverGroup.All };
        PhotonNetwork.RaiseEvent(
            MapVoteBallotEventCode,
            new object[] { sessionId, actorNumber, choiceIndex, voterName },
            options,
            SendOptions.SendReliable);
    }

    /// <summary>
    /// Host broadcasts the finished level scoreboard (parallel arrays, one slot per player) so every
    /// client's results screen shows identical numbers. Reliable; not echoed to the host.
    /// </summary>
    public void BroadcastRoundStats(System.Collections.Generic.List<PlayerRoundStat> rows)
    {
        if (!PhotonNetwork.InRoom)
            return;

        int n = rows.Count;
        var names = new string[n];
        var broken = new int[n];
        var brokenCount = new int[n];
        var delivered = new int[n];
        var deaths = new int[n];
        var revives = new int[n];
        for (int i = 0; i < n; i++)
        {
            names[i] = rows[i].Name ?? "?";
            broken[i] = rows[i].BrokenValue;
            brokenCount[i] = rows[i].BrokenCount;
            delivered[i] = rows[i].Delivered;
            deaths[i] = rows[i].Deaths;
            revives[i] = rows[i].Revives;
        }

        var options = new RaiseEventOptions { Receivers = ReceiverGroup.Others };
        PhotonNetwork.RaiseEvent(
            RoundStatsEventCode,
            new object[] { names, broken, brokenCount, delivered, deaths, revives },
            options,
            SendOptions.SendReliable);
    }

    /// <summary>Host tells everyone a valuable's current value after the Repair drone restored some.</summary>
    public void BroadcastRepair(ValuableObject valuable, float value)
    {
        var pv = valuable != null ? valuable.GetComponent<PhotonView>() : null;
        if (!PhotonNetwork.InRoom || pv == null)
            return;
        PhotonNetwork.RaiseEvent(RepairEventCode, new object[] { pv.ViewID, value },
            new RaiseEventOptions { Receivers = ReceiverGroup.Others }, SendOptions.SendReliable);
    }

    /// <summary>Photon calls this on every client for every raised event.</summary>
    public void OnEvent(EventData photonEvent)
    {
        if (photonEvent.Code == ModifierEventCode)
        {
            var data = (object[])photonEvent.CustomData;
            var modifiers = LevelModifiers.Deserialize(data);
            ModifierManager.ApplyReceived(modifiers);
            return;
        }

        if (photonEvent.Code == LevelTimerEventCode)
        {
            var data = (object[])photonEvent.CustomData;
            LevelTimerController.ApplyNetworkState((float)data[0], (bool)data[1]);
            return;
        }


        if (photonEvent.Code == PingEventCode)
        {
            var data = (object[])photonEvent.CustomData;
            var pos = new Vector3((float)data[1], (float)data[2], (float)data[3]);
            int viewID = data.Length > 6 ? (int)data[6] : 0;
            PingController.AddNetworkPing((byte)data[0], pos, (string)data[4], (string)data[5], viewID);
            return;
        }

        if (photonEvent.Code == MapVoteCandidatesEventCode)
        {
            var data = (object[])photonEvent.CustomData;
            MapVoteState.ReceiveCandidates((int)data[0], (string[])data[1], (float)data[2], (bool)data[3], (int)data[4]);
            return;
        }

        if (photonEvent.Code == MapVoteBallotEventCode)
        {
            var data = (object[])photonEvent.CustomData;
            MapVoteState.ReceiveBallot((int)data[0], (int)data[1], (int)data[2], (string)data[3]);
            return;
        }

        if (photonEvent.Code == RepairEventCode)
        {
            var d = (object[])photonEvent.CustomData;
            var pv = PhotonView.Find((int)d[0]);
            var v = pv != null ? pv.GetComponent<ValuableObject>() : null;
            if (v != null)
                v.dollarValueCurrent = Mathf.Min((float)d[1], v.dollarValueOriginal);
            return;
        }

        if (photonEvent.Code == RoundStatsEventCode)
        {
            var d = (object[])photonEvent.CustomData;
            var names = (string[])d[0];
            var rows = new System.Collections.Generic.List<PlayerRoundStat>();
            for (int i = 0; i < names.Length; i++)
                rows.Add(new PlayerRoundStat
                {
                    Name = names[i],
                    BrokenValue = ((int[])d[1])[i],
                    BrokenCount = ((int[])d[2])[i],
                    Delivered = ((int[])d[3])[i],
                    Deaths = ((int[])d[4])[i],
                    Revives = ((int[])d[5])[i],
                });
            if (ModConfig.RoundResultsEnabled.Value)
                RoundStats.ApplyResults(rows);
        }
    }
}
