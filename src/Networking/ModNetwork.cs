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
/// Authoritative state is applied locally, then sent to ReceiverGroup.Others.
/// This avoids depending on sender-echo behavior or applying local effects twice.
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
    public const byte EconomyEventCode = 188;
    public const byte ShopEconomyEventCode = 189;
    public const byte BanRequestEventCode = 190;
    public const byte BanListEventCode = 191;
    public const byte BatteryDrainEventCode = 186;
    public const byte EnemyStatusEventCode = 187;
    public const byte PlayerFreezeEventCode = 192;
    public const byte StickyTetherEventCode = 193;

    // Host broadcasts the authoritative team-quest set; any client asks the host to pay a personal reward.
    public const byte QuestStateEventCode = 194;
    public const byte QuestMoneyRequestEventCode = 195;
    public const byte QuestBuffEventCode = 196;

    public static ModNetwork Instance { get; private set; }

    private bool _registered;
    private float _enemyTuningNext;
    private string _enemyTuningSignature;

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

        if (inRoom && PhotonNetwork.IsMasterClient && Time.unscaledTime >= _enemyTuningNext)
        {
            _enemyTuningNext = Time.unscaledTime + 1f;
            string signature = PhotonNetwork.CurrentRoom.Name + "|" + PhotonNetwork.MasterClient?.ActorNumber + "|" +
                PhotonNetwork.CurrentRoom.PlayerCount + "|" + EnemyBalance.Signature();
            if (signature != _enemyTuningSignature)
            {
                _enemyTuningSignature = signature;
                BroadcastModifiers(ModifierManager.Current);
            }
        }
        if (!inRoom) _enemyTuningSignature = null;

        LevelTimerController.Tick(Time.deltaTime);
        Quests.QuestManager.Tick(Time.deltaTime);
        RoundStats.Tick(Time.deltaTime);
        LateJoinController.Tick();
        // Client-side: keep every enemy's per-player dictionaries in sync with mid-run joins
        // (and mid-run enemy spawns) so late joiners don't crash the enemy AI coroutines.
        LateJoinController.EnsureEnemyRegistrations();
        // Client-side: unfreeze a mid-run joiner whose PlayerHealth.Fetch() never completed
        // (health/maxHealth stuck at 100 defaults -> immune to all damage AND healing).
        LateJoinController.EnsureLocalHealthInitialized();
    }

    private void FixedUpdate()
    {
        Drones.LinkManager.Tick(Time.fixedDeltaTime);
        Gear.GearEffects.Tick(Time.fixedDeltaTime);
    }

    /// <summary>Host broadcasts the authoritative timer state to other clients.</summary>
    public void BroadcastLevelTimer(float remaining, bool running)
    {
        if (!PhotonNetwork.InRoom || !PhotonNetwork.IsMasterClient)
            return;

        var options = new RaiseEventOptions { Receivers = ReceiverGroup.Others };
        PhotonNetwork.RaiseEvent(
            LevelTimerEventCode,
            new object[] { remaining, running },
            options,
            SendOptions.SendUnreliable);
    }

    /// <summary>
    /// Host broadcasts the rolled modifiers to all other clients. Reliable so it
    /// can't be dropped. Includes host enemy tuning; also used for late joins and
    /// same-level tuning updates without resetting per-level effect budgets.
    /// </summary>
    public void BroadcastModifiers(LevelModifiers modifiers)
    {
        if (!PhotonNetwork.InRoom || !PhotonNetwork.IsMasterClient)
            return;

        var options = new RaiseEventOptions { Receivers = ReceiverGroup.Others };
        var payload = modifiers.Serialize();
        System.Array.Resize(ref payload, 9);
        payload[5] = ModifierManager.Revision;
        payload[6] = EnemyBalance.Capture();
        payload[7] = ModConfig.EnemyHealthMaxPerEnemy.Value;
        payload[8] = ModConfig.SurprisesEnabled.Value;
        PhotonNetwork.RaiseEvent(
            ModifierEventCode,
            payload,
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

        var options = new RaiseEventOptions { Receivers = ReceiverGroup.Others };
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
        if (!PhotonNetwork.InRoom || !PhotonNetwork.IsMasterClient)
            return;

        var options = new RaiseEventOptions { Receivers = ReceiverGroup.Others };
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

        var options = new RaiseEventOptions { Receivers = ReceiverGroup.Others };
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
    internal void BroadcastRoundStats(System.Collections.Generic.List<PlayerRoundStat> rows, RoundLootSummary summary)
    {
        if (!PhotonNetwork.InRoom || !PhotonNetwork.IsMasterClient)
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
            new object[] { names, broken, brokenCount, delivered, deaths, revives, summary?.Serialize() ?? System.Array.Empty<int>() },
            options,
            SendOptions.SendReliable);
    }

    /// <summary>A client that absorbed damage with a shield item asks the host to drain its battery.</summary>
    public void BroadcastBatteryDrain(int viewId, float percent)
    {
        if (!PhotonNetwork.InRoom)
            return;
        PhotonNetwork.RaiseEvent(BatteryDrainEventCode, new object[] { viewId, percent },
            new RaiseEventOptions { Receivers = ReceiverGroup.MasterClient }, SendOptions.SendReliable);
    }

    /// <summary>Host tells everyone which shop economy (0 = none) is active.</summary>
    public void BroadcastShopEconomy(int id)
    {
        if (!PhotonNetwork.InRoom || !PhotonNetwork.IsMasterClient)
            return;
        PhotonNetwork.RaiseEvent(ShopEconomyEventCode, new object[] { id },
            new RaiseEventOptions { Receivers = ReceiverGroup.Others }, SendOptions.SendReliable);
    }

    /// <summary>A client asks the host to buy a Black Market block.</summary>
    public void RequestBan(byte kind, string id)
    {
        if (!PhotonNetwork.InRoom)
            return;
        PhotonNetwork.RaiseEvent(BanRequestEventCode, new object[] { kind, id },
            new RaiseEventOptions { Receivers = ReceiverGroup.MasterClient }, SendOptions.SendReliable);
    }

    /// <summary>Host tells everyone the full list of blocked keys.</summary>
    public void BroadcastBans(System.Collections.Generic.IEnumerable<string> keys)
    {
        if (!PhotonNetwork.InRoom || !PhotonNetwork.IsMasterClient)
            return;
        PhotonNetwork.RaiseEvent(BanListEventCode, new object[] { new System.Collections.Generic.List<string>(keys).ToArray() },
            new RaiseEventOptions { Receivers = ReceiverGroup.Others }, SendOptions.SendReliable);
    }

    /// <summary>Host tells everyone which level economy event (0 = none) is active.</summary>
    public void BroadcastEconomy(int id)
    {
        if (!PhotonNetwork.InRoom || !PhotonNetwork.IsMasterClient)
            return;
        PhotonNetwork.RaiseEvent(EconomyEventCode, new object[] { id },
            new RaiseEventOptions { Receivers = ReceiverGroup.Others }, SendOptions.SendReliable);
    }

    /// <summary>Host tells everyone a valuable's current value after the Repair drone restored some.</summary>
    public void BroadcastRepair(ValuableObject valuable, float value)
    {
        var pv = valuable != null ? valuable.GetComponent<PhotonView>() : null;
        if (!PhotonNetwork.InRoom || !PhotonNetwork.IsMasterClient || pv == null)
            return;
        PhotonNetwork.RaiseEvent(RepairEventCode, new object[] { pv.ViewID, value },
            new RaiseEventOptions { Receivers = ReceiverGroup.Others }, SendOptions.SendReliable);
    }

    /// <summary>Host-confirmed target identity and remaining lifetime; never infer hits on clients.</summary>
    internal static void BroadcastEnemyStatus(Enemy enemy, byte kind, float seconds)
    {
        if (enemy == null || !SemiFunc.IsMasterClientOrSingleplayer()) return;
        Visuals.EnemyStatusVisual.Apply(enemy, kind, seconds);
        if (!PhotonNetwork.InRoom || !PhotonNetwork.IsMasterClient) return;
        var parent = enemy.GetComponentInParent<EnemyParent>();
        var view = enemy.GetComponent<PhotonView>();
        if (view == null && parent != null) view = parent.GetComponent<PhotonView>();
        if (view == null || view.ViewID == 0) return;
        PhotonNetwork.RaiseEvent(EnemyStatusEventCode,
            new object[] { view.ViewID, kind, seconds, PhotonNetwork.Time },
            new RaiseEventOptions { Receivers = ReceiverGroup.Others }, SendOptions.SendReliable);
    }

    private static string FreezeLevelKey() => RunManager.instance != null && RunManager.instance.levelCurrent != null
        ? RunManager.instance.levelsCompleted + ":" + RunManager.instance.levelCurrent.name : "";

    internal static void BroadcastPlayerFreeze(PlayerAvatar avatar, float seconds)
    {
        if (avatar == null || !SemiFunc.IsMasterClientOrSingleplayer()) return;
        Gear.PlayerFreezeEffect.Apply(avatar, seconds);
        if (!PhotonNetwork.InRoom || !PhotonNetwork.IsMasterClient) return;
        var view = avatar.GetComponent<PhotonView>();
        if (view == null || view.ViewID == 0) return;
        PhotonNetwork.RaiseEvent(PlayerFreezeEventCode,
            new object[] { view.ViewID, seconds, PhotonNetwork.Time, FreezeLevelKey() },
            new RaiseEventOptions { Receivers = ReceiverGroup.Others }, SendOptions.SendReliable);
    }

    internal static void BroadcastStickyTether(ItemGun gun, PhysGrabObject target, Vector3 point, float seconds)
    {
        if (!SemiFunc.IsMasterClientOrSingleplayer() || gun == null || target == null) return;
        Visuals.StickyTether.Apply(gun, target, point, seconds);
        if (!PhotonNetwork.InRoom || !PhotonNetwork.IsMasterClient) return;
        var gunView = gun.GetComponent<PhotonView>();
        var targetView = target.GetComponent<PhotonView>();
        if (gunView == null || targetView == null || gunView.ViewID <= 0 || targetView.ViewID <= 0) return;
        PhotonNetwork.RaiseEvent(StickyTetherEventCode,
            new object[] { gunView.ViewID, targetView.ViewID, point.x, point.y, point.z, seconds, PhotonNetwork.Time, FreezeLevelKey() },
            new RaiseEventOptions { Receivers = ReceiverGroup.Others }, SendOptions.SendReliable);
    }

    /// <summary>Host broadcasts the authoritative team-quest set to all clients.</summary>
    public void BroadcastQuestState(object[] data)
    {
        if (!PhotonNetwork.InRoom || !PhotonNetwork.IsMasterClient || !NetworkPayload.QuestState(data, out _))
            return;

        var options = new RaiseEventOptions { Receivers = ReceiverGroup.Others };
        PhotonNetwork.RaiseEvent(
            QuestStateEventCode,
            data,
            options,
            SendOptions.SendReliable);
    }

    /// <summary>
    /// Host tells every other client to apply a temporary stat buff to their own local player
    /// (the host applies its own buff directly before broadcasting to the others).
    /// </summary>
    public void BroadcastQuestBuff(byte stat, int amount)
    {
        if (!PhotonNetwork.InRoom || !PhotonNetwork.IsMasterClient || !NetworkPayload.QuestBuff(new object[] { stat, amount }, out _))
            return;

        var options = new RaiseEventOptions { Receivers = ReceiverGroup.Others };
        PhotonNetwork.RaiseEvent(
            QuestBuffEventCode,
            new object[] { stat, amount },
            options,
            SendOptions.SendReliable);
    }

    /// <summary>A non-host client asks the host to pay out a completed personal-quest reward.</summary>
    public void RequestQuestMoney(int amount)
    {
        if (!PhotonNetwork.InRoom || amount <= 0)
            return;

        var options = new RaiseEventOptions { Receivers = ReceiverGroup.MasterClient };
        PhotonNetwork.RaiseEvent(
            QuestMoneyRequestEventCode,
            new object[] { amount },
            options,
            SendOptions.SendReliable);
    }

    /// <summary>Photon calls this on every client for every raised event.</summary>
    private static bool HostSender(EventData message) => NetworkPayload.Host(message.Sender,
        PhotonNetwork.MasterClient != null ? PhotonNetwork.MasterClient.ActorNumber : -1, PhotonNetwork.InRoom);

    public void OnEvent(EventData photonEvent)
    {
        if (photonEvent == null || !PhotonNetwork.InRoom) return;
        if (photonEvent.Code == StickyTetherEventCode)
        {
            if (!PhotonNetwork.InRoom || PhotonNetwork.MasterClient == null || photonEvent.Sender != PhotonNetwork.MasterClient.ActorNumber ||
                photonEvent.CustomData is not object[] data || data.Length != 8 ||
                data[0] is not int gunId || gunId <= 0 || data[1] is not int targetId || targetId <= 0 ||
                data[2] is not float x || data[3] is not float y || data[4] is not float z ||
                data[5] is not float seconds || data[6] is not double sent || data[7] is not string level || level != FreezeLevelKey() ||
                float.IsNaN(x) || float.IsInfinity(x) || float.IsNaN(y) || float.IsInfinity(y) || float.IsNaN(z) || float.IsInfinity(z) ||
                float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds <= 0 || seconds > 2f || double.IsNaN(sent) || double.IsInfinity(sent)) return;
            float remaining = seconds - (float)System.Math.Max(0d, PhotonNetwork.Time-sent);
            if (remaining <= 0f) return;
            var gunView = PhotonView.Find(gunId); var targetView = PhotonView.Find(targetId);
            Visuals.StickyTether.Apply(gunView != null ? gunView.GetComponent<ItemGun>() : null,
                targetView != null ? targetView.GetComponent<PhysGrabObject>() : null, new Vector3(x,y,z), remaining);
            return;
        }
        if (photonEvent.Code == PlayerFreezeEventCode)
        {
            if (!PhotonNetwork.InRoom || PhotonNetwork.MasterClient == null ||
                photonEvent.Sender != PhotonNetwork.MasterClient.ActorNumber ||
                !Gear.PlayerFreezeEffect.ReadSnapshot(photonEvent.CustomData, PhotonNetwork.Time, FreezeLevelKey(),
                    out int id, out float remaining)) return;
            var view = PhotonView.Find(id);
            Gear.PlayerFreezeEffect.Apply(view != null ? view.GetComponent<PlayerAvatar>() : null, remaining);
            return;
        }
        if (photonEvent.Code == EnemyStatusEventCode)
        {
            if (!PhotonNetwork.InRoom || PhotonNetwork.MasterClient == null ||
                photonEvent.Sender != PhotonNetwork.MasterClient.ActorNumber ||
                photonEvent.CustomData is not object[] data || data.Length != 4 ||
                data[0] is not int id || data[1] is not byte kind || kind > 1 ||
                data[2] is not float seconds || data[3] is not double sent ||
                float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds <= 0f || seconds > 600f ||
                double.IsNaN(sent) || double.IsInfinity(sent)) return;
            float age = (float)System.Math.Max(0d, PhotonNetwork.Time - sent);
            if (age >= seconds) return;
            var view = PhotonView.Find(id);
            var enemy = view != null ? view.GetComponentInChildren<Enemy>(true) : null;
            Visuals.EnemyStatusVisual.Apply(enemy, kind, seconds - age);
            return;
        }

        if (photonEvent.Code == ModifierEventCode)
        {
            if (!PhotonNetwork.InRoom || PhotonNetwork.IsMasterClient || PhotonNetwork.MasterClient == null ||
                photonEvent.Sender != PhotonNetwork.MasterClient.ActorNumber ||
                photonEvent.CustomData is not object[] data || data.Length < 4 ||
                data[0] is not int || data[1] is not int || data[2] is not int || data[3] is not int)
                return;
            // Refresh tuning even when the same-level modifier revision is unchanged;
            // do not reset timers/revive budgets just because the host edited a setting.
            if (data.Length >= 9 && data[6] is float[] factors && data[7] is string caps && data[8] is bool surprises)
                EnemyBalance.Apply(factors, caps, surprises);
            var modifiers = LevelModifiers.Deserialize(data);
            int revision = data.Length > 5 && data[5] is int value ? value : -1;
            long token = revision >= 0 ? ((long)photonEvent.Sender << 32) | (uint)revision : -1;
            ModifierManager.ApplyReceived(modifiers, token);
            return;
        }

        if (photonEvent.Code == LevelTimerEventCode)
        {
            if (HostSender(photonEvent) && NetworkPayload.Timer(photonEvent.CustomData, out var data))
                LevelTimerController.ApplyNetworkState((float)data[0], (bool)data[1]);
            return;
        }


        if (photonEvent.Code == PingEventCode)
        {
            // The sender already draws locally; also tolerate older peers broadcasting to All.
            if (PhotonNetwork.LocalPlayer != null && photonEvent.Sender == PhotonNetwork.LocalPlayer.ActorNumber) return;
            if (!NetworkPayload.Ping(photonEvent.CustomData, out var data)) return;
            var pos = new Vector3((float)data[1], (float)data[2], (float)data[3]);
            int viewID = data.Length > 6 ? (int)data[6] : 0;
            PingController.AddNetworkPing((byte)data[0], pos, (string)data[4], (string)data[5], viewID);
            return;
        }

        if (photonEvent.Code == MapVoteCandidatesEventCode)
        {
            if (!HostSender(photonEvent) || !NetworkPayload.Candidates(photonEvent.CustomData, out var data)) return;
            MapVoteState.ReceiveCandidates((int)data[0], (string[])data[1], (float)data[2], (bool)data[3], (int)data[4]);
            return;
        }

        if (photonEvent.Code == MapVoteBallotEventCode)
        {
            if (!NetworkPayload.Ballot(photonEvent.CustomData, photonEvent.Sender, out var data)) return;
            MapVoteState.ReceiveBallot((int)data[0], photonEvent.Sender, (int)data[2], (string)data[3]);
            return;
        }

        if (photonEvent.Code == ShopEconomyEventCode)
        {
            if (photonEvent.CustomData is object[] sd && sd.Length == 1 && sd[0] is int sid &&
                PhotonNetwork.MasterClient != null && photonEvent.Sender == PhotonNetwork.MasterClient.ActorNumber)
                Economy.ShopEconomy.Set(sid);
            return;
        }

        if (photonEvent.Code == BanRequestEventCode)
        {
            if (PhotonNetwork.IsMasterClient && photonEvent.CustomData is object[] bd && bd.Length == 2 &&
                bd[0] is byte bk && bk <= 2 && bd[1] is string bid && bid.Length < 48)
                Economy.BlackMarket.TryBuy((Economy.BlackMarket.Kind)bk, bid);
            return;
        }

        if (photonEvent.Code == BanListEventCode)
        {
            if (photonEvent.CustomData is object[] ld && ld.Length == 1 && ld[0] is string[] keys &&
                PhotonNetwork.MasterClient != null && photonEvent.Sender == PhotonNetwork.MasterClient.ActorNumber)
                Economy.BlackMarket.SetFromNetwork(keys);
            return;
        }

        if (photonEvent.Code == EconomyEventCode)
        {
            if (photonEvent.CustomData is object[] ed && ed.Length == 1 && ed[0] is int eid &&
                PhotonNetwork.MasterClient != null && photonEvent.Sender == PhotonNetwork.MasterClient.ActorNumber)
                Economy.EconomyEvents.Set(eid);
            return;
        }

        if (photonEvent.Code == BatteryDrainEventCode)
        {
            if (!PhotonNetwork.IsMasterClient || !NetworkPayload.BatteryDrain(photonEvent.CustomData, out var data)) return;
            var view = PhotonView.Find((int)data[0]);
            var shield = view != null ? view.GetComponent<Gear.ShieldItem>() : null;
            var battery = view != null ? view.GetComponent<ItemBattery>() : null;
            if (shield == null || battery == null || shield.mode != Gear.ShieldMode.DefenceDrone) return;
            var players = GameDirector.instance != null ? GameDirector.instance.PlayerList : null;
            if (players == null) return;
            foreach (var player in players)
                if (player != null && player.photonView != null && player.photonView.OwnerActorNr == photonEvent.Sender && shield.Covers(player))
                {
                    battery.batteryLife = Mathf.Clamp(battery.batteryLife - (float)data[1], 0f, 100f);
                    return;
                }
            return;
        }

        if (photonEvent.Code == RepairEventCode)
        {
            if (!HostSender(photonEvent) || !NetworkPayload.Repair(photonEvent.CustomData, out var data)) return;
            var view = PhotonView.Find((int)data[0]);
            var valuable = view != null ? view.GetComponent<ValuableObject>() : null;
            if (valuable != null) valuable.dollarValueCurrent = Mathf.Clamp((float)data[1], 0f, valuable.dollarValueOriginal);
            return;
        }

        if (photonEvent.Code == RoundStatsEventCode)
        {
            if (!PhotonNetwork.InRoom || PhotonNetwork.IsMasterClient || PhotonNetwork.MasterClient == null ||
                photonEvent.Sender != PhotonNetwork.MasterClient.ActorNumber ||
                photonEvent.CustomData is not object[] d || d.Length < 6 ||
                d[0] is not string[] names || names.Length > 128 || d[1] is not int[] broken || d[2] is not int[] brokenCount ||
                d[3] is not int[] delivered || d[4] is not int[] deaths || d[5] is not int[] revives ||
                broken.Length != names.Length || brokenCount.Length != names.Length || delivered.Length != names.Length ||
                deaths.Length != names.Length || revives.Length != names.Length) return;
            var rows = new System.Collections.Generic.List<PlayerRoundStat>();
            for (int i = 0; i < names.Length; i++)
                rows.Add(new PlayerRoundStat
                {
                    Name = names[i], BrokenValue = Mathf.Max(0, broken[i]), BrokenCount = Mathf.Max(0, brokenCount[i]),
                    Delivered = Mathf.Max(0, delivered[i]), Deaths = Mathf.Max(0, deaths[i]), Revives = Mathf.Max(0, revives[i]),
                });
            var summary = d.Length > 6 && d[6] is int[] totals ? RoundLootSummary.Deserialize(totals) : null;
            if (ModConfig.RoundResultsEnabled.Value) RoundStats.ApplyResults(rows, summary);
        }

        if (photonEvent.Code == QuestStateEventCode)
        {
            if (ModConfig.QuestsEnabled.Value && HostSender(photonEvent) &&
                NetworkPayload.QuestState(photonEvent.CustomData, out var data))
                Quests.QuestManager.ReceiveTeamState(data);
            return;
        }

        if (photonEvent.Code == QuestMoneyRequestEventCode)
        {
            // Only the host honours the payout request.
            if (ModConfig.QuestsEnabled.Value && PhotonNetwork.IsMasterClient && photonEvent.Sender > 0 &&
                photonEvent.CustomData is object[] data && data.Length == 1 && data[0] is int amount && amount > 0)
                Quests.QuestRewards.GrantMoney(amount);
            return;
        }

        if (photonEvent.Code == QuestBuffEventCode)
        {
            if (ModConfig.QuestsEnabled.Value && HostSender(photonEvent) &&
                NetworkPayload.QuestBuff(photonEvent.CustomData, out var data))
                Quests.QuestManager.ApplyTempBuff((Quests.StatKind)(byte)data[0], (int)data[1]);
        }
    }
}
