using System.Collections.Generic;
using ExitGames.Client.Photon;
using HarmonyLib;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.AI;

namespace IncreaseHardMod.Challenges;

/// <summary>
/// Shrine challenges: on a run level the host places one challenge shrine. A living player who presses the interact
/// key (E by default) next to it starts its timed challenge (wisp escort, target practice, hunt, cull or smash); finishing it in time
/// gives one reward: a skill point for everyone, an upgrade or a cosmetic box at the shrine, plus a quest-rank
/// completion. Like skill boxes, nothing is a network object: the host owns the state and broadcasts it (event 168);
/// every client builds the same local props (see <see cref="ChallengeProps"/>) and shows the HUD line.
/// </summary>
internal sealed class ChallengeManager : MonoBehaviour
{
    internal const byte EventCode = 168;
    /// <summary>A guest pressed interact at the shrine (to the host only; the host checks they are really there).</summary>
    internal const byte StartRequestCode = 166;
    private const float SyncInterval = .25f, ScanInterval = .1f;
    private static readonly Vector3 ShrineSize = new(1f, .9f, 1f);

    // ---- state (host authoritative, mirrored on clients)
    private static string _key = "";
    private static bool _has, _placePending;
    private static Vector3 _shrine;
    private static float _yaw;
    private static ChallengeKind _kind;
    private static ChallengePhase _phase;
    private static int _progress, _total, _huntView;
    private static ChallengeReward _reward;
    private static float _startedAt, _endsAtLocal;
    private static Vector3 _wisp, _home;
    private static bool _wispWaiting;
    private static readonly List<Vector4> _props = new();   // xyz + yaw (crates) or drift phase (targets)
    private static readonly List<bool> _propDone = new(), _propDrift = new();

    // ---- host only
    private static readonly List<Vector3> _path = new();
    private static int _pathIndex;
    private static float _nextScan, _nextSync;
    private static string _sent;
    private static EnemyParent _huntTarget;

    // ---- client visuals
    private static ShrineVisual _shrineVisual;
    private static WispVisual _wispVisual;
    private static WispHomeVisual _homeVisual;
    private static HuntBeacon _beacon;
    private static readonly List<Component> _propVisuals = new();
    private static ChallengePhase _shownPhase;
    private static float _finishedAt = -1f;
    private const float VanishSeconds = 3f; // after the result, everything left shrinks away

    private static bool Host => SemiFunc.IsMasterClientOrSingleplayer();
    private static bool Russian => Localization.GameLanguage.Russian;

    /// <summary>Clock shared by host and clients (drifting targets move identically everywhere).</summary>
    internal static float SharedTime => SemiFunc.IsMultiplayer() ? (float)(PhotonNetwork.Time % 3600d) : Time.time;

    /// <summary>Target Practice running: guns use no battery (host checks this in the drain hook).</summary>
    internal static bool FreeGunBattery => _has && _phase == ChallengePhase.Active && _kind == ChallengeKind.Targets;

    private static string LevelKey() => RunManager.instance != null && RunManager.instance.levelCurrent != null
        ? RunManager.instance.levelsCompleted + ":" + RunManager.instance.levelCurrent.name : "";

    private static void Clear()
    {
        if (_shrineVisual != null) Destroy(_shrineVisual.gameObject);
        if (_wispVisual != null) Destroy(_wispVisual.gameObject);
        if (_homeVisual != null) Destroy(_homeVisual.gameObject);
        if (_beacon != null) Destroy(_beacon.gameObject);
        foreach (var v in _propVisuals) if (v != null) Destroy(v.gameObject);
        _propVisuals.Clear();
        _props.Clear(); _propDone.Clear(); _propDrift.Clear(); _path.Clear();
        _has = false; _key = ""; _phase = ChallengePhase.Ready; _shownPhase = ChallengePhase.Ready; _finishedAt = -1f;
        _progress = _total = _huntView = 0; _reward = ChallengeReward.None; _huntTarget = null; _sent = null;
        Localization.HudStatus.Clear(StatusKey);
    }

    private const string StatusKey = "ihm.challenge";

    [HarmonyPatch(typeof(LevelGenerator), "GenerateDone")]
    private static class OnGenerated
    {
        [HarmonyPostfix]
        private static void Postfix()
        {
            if (!Host) return;
            Clear();
            bool level;
            try { level = SemiFunc.RunIsLevel(); } catch { level = false; }
            _placePending = level && ModConfig.ChallengesEnabled.Value && Random.value * 100f < ModConfig.ChallengeChancePercent.Value;
        }
    }

    // ================================================================ per frame

    private void Update()
    {
        try
        {
            if (LevelGenerator.Instance == null || !LevelGenerator.Instance.Generated) return;
            if (Host && _placePending) { _placePending = false; Place(); }
            if (!_has || _key != LevelKey()) { if (_has && !Host) Clear(); return; }
            if (Host) HostTick();
            ClientTick();
        }
        catch (System.Exception e) { Plugin.Log.LogWarning("[Challenge] Update failed: " + e); }
    }

    // ================================================================ host: placement

    private static void Place()
    {
        var points = SemiFunc.LevelPointsGetAll();
        if (points == null || points.Count == 0) return;
        var avoid = new List<Vector3>(Patches.Effects.MysteryBoxEffect.Positions());
        avoid.AddRange(Patches.Effects.SkillBoxEffect.Positions());
        var spots = new List<(Vector3 Floor, float Yaw)>();
        foreach (var p in points)
        {
            if (p == null || p.Truck || p.inStartRoom || (p.Room != null && (p.Room.Truck || p.Room.Extraction))) continue;
            if (!Patches.Effects.MysteryBoxEffect.TryFloor(p.transform.position, ShrineSize, out var floor, out float yaw)) continue;
            bool clear = true;
            foreach (var a in avoid) if ((a - floor).sqrMagnitude < 36f) { clear = false; break; }
            if (clear) spots.Add((floor, yaw));
        }
        if (spots.Count == 0) { Plugin.Log.LogInfo("[Challenge] No free spot for a shrine this level."); return; }

        var spot = spots[Random.Range(0, spots.Count)];
        _key = LevelKey();
        _has = true;
        _shrine = spot.Floor;
        _yaw = spot.Yaw;
        _phase = ChallengePhase.Ready;
        _kind = ChallengeRules.Kind(Random.value);
        if (_kind == ChallengeKind.Escort && !PlanEscort(points)) _kind = ChallengeKind.Targets;
        Plugin.Log.LogInfo($"[Challenge] Shrine placed at {_shrine}: {_kind}.");
    }

    // The wisp's home: a level point 25-55 m of walking away; the path is the navmesh route there.
    private static bool PlanEscort(List<LevelPoint> points)
    {
        if (!NavMesh.SamplePosition(_shrine, out var start, 2f, NavMesh.AllAreas)) return false;
        var shuffled = new List<LevelPoint>(points);
        for (int i = shuffled.Count - 1; i > 0; i--) { int j = Random.Range(0, i + 1); (shuffled[i], shuffled[j]) = (shuffled[j], shuffled[i]); }
        var path = new NavMeshPath();
        foreach (var p in shuffled)
        {
            if (p == null || p.Truck || p.inStartRoom) continue;
            if (!NavMesh.SamplePosition(p.transform.position, out var end, 2f, NavMesh.AllAreas)) continue;
            if (!NavMesh.CalculatePath(start.position, end.position, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete) continue;
            float length = 0f;
            for (int i = 1; i < path.corners.Length; i++) length += Vector3.Distance(path.corners[i - 1], path.corners[i]);
            if (length < ChallengeRules.EscortMinPath || length > ChallengeRules.EscortMaxPath) continue;
            _path.Clear();
            foreach (var c in path.corners) _path.Add(c + Vector3.up * 1.2f);
            _home = end.position;
            return true;
        }
        return false;
    }

    // ================================================================ host: running

    private static void HostTick()
    {
        float now = Time.time;
        if (_phase == ChallengePhase.Active)
        {
            if (ChallengeRules.Expired(_startedAt, now, _kind)) Finish(false, "time");
            else if (_kind == ChallengeKind.Escort) EscortStep();
            else if (_kind == ChallengeKind.Smash && now >= _nextScan) { _nextScan = now + ScanInterval; ScanCrates(); }
            else if (_kind == ChallengeKind.Hunt && (_huntTarget == null || !_huntTarget.Spawned) && _progress == 0) PickHuntTarget();
        }
        Broadcast(force: false);
    }

    private static List<PlayerAvatar> Living()
    {
        var list = new List<PlayerAvatar>();
        var players = SemiFunc.PlayerGetList();
        if (players != null) foreach (var p in players) if (p != null && !p.isDisabled) list.Add(p);
        return list;
    }

    private static bool Near(PlayerAvatar p, float radius)
    {
        if (p == null || p.isDisabled) return false;
        var d = p.transform.position - _shrine;
        return Mathf.Abs(d.y) < 1.8f && d.x * d.x + d.z * d.z <= radius * radius;
    }

    /// <summary>Host: someone pressed interact at the shrine. Starts it if they are really next to a ready shrine.</summary>
    private static void HostStartRequest(PlayerAvatar who)
    {
        if (!Host || !_has || _phase != ChallengePhase.Ready || !Near(who, ChallengeRules.StartRadius + .5f)) return;
        Plugin.Log.LogInfo($"[Challenge] {SemiFunc.PlayerGetName(who)} started the shrine.");
        Begin();
    }

    /// <summary>Host: a guest's start request (ModNetwork passes any sender; the request carries nothing to trust).</summary>
    internal static void HandleStartRequest(EventData e)
    {
        if (!Host) return;
        string steam = Skills.SkillStore.SteamIdOfActor(e.Sender);
        if (!string.IsNullOrEmpty(steam)) HostStartRequest(SemiFunc.PlayerAvatarGetFromSteamID(steam));
    }

    // Every client: the local player pressing interact next to a ready shrine.
    private static void WatchInteract()
    {
        if (_phase != ChallengePhase.Ready) return;
        var me = SemiFunc.PlayerAvatarLocal();
        if (!Near(me, ChallengeRules.StartRadius) || (ChatManager.instance != null && ChatManager.instance.chatActive) ||
            Skills.SkillTreeScreen.IsOpen || !SemiFunc.InputDown(InputKey.Interact)) return;
        if (Host) HostStartRequest(me);
        else if (PhotonNetwork.InRoom)
            PhotonNetwork.RaiseEvent(StartRequestCode, null, new RaiseEventOptions { Receivers = ReceiverGroup.MasterClient }, SendOptions.SendReliable);
    }

    private static void Begin()
    {
        _progress = 0;
        _props.Clear(); _propDone.Clear(); _propDrift.Clear();
        if (_kind == ChallengeKind.Hunt && !PickHuntTarget()) _kind = ChallengeKind.Cull;
        switch (_kind)
        {
            case ChallengeKind.Escort:
                _pathIndex = 1;
                _wisp = _path.Count > 0 ? _path[0] : _shrine + Vector3.up * 1.2f;
                _total = 1;
                break;
            case ChallengeKind.Targets: PlaceTargets(); _total = _props.Count; break;
            case ChallengeKind.Smash: PlaceCrates(); _total = _props.Count; break;
            case ChallengeKind.Hunt: _total = 1; break;
            default: _total = ChallengeRules.CullTarget(Living().Count); break;
        }
        if ((_kind == ChallengeKind.Targets || _kind == ChallengeKind.Smash) && _props.Count < ChallengeRules.MinProps)
        {
            _kind = ChallengeKind.Cull; // no room around this shrine for props
            _props.Clear(); _propDone.Clear(); _propDrift.Clear();
            _total = ChallengeRules.CullTarget(Living().Count);
        }
        _phase = ChallengePhase.Active;
        _startedAt = Time.time;
        Plugin.Log.LogInfo($"[Challenge] Started {_kind}: goal {_total}.");
        Broadcast(force: true);
    }

    private static void PlaceTargets()
    {
        int want = ChallengeRules.PropCount(Random.value);
        int mask = SemiFunc.LayerMaskGetVisionObstruct();
        var eye = _shrine + Vector3.up * 1.4f;
        for (int tries = 0; tries < 160 && _props.Count < want; tries++)
        {
            var dir = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Vector3.forward;
            var p = _shrine + dir * Random.Range(5f, 13f) + Vector3.up * Random.Range(1.2f, 3.2f);
            if (Physics.CheckSphere(p, 1f, mask, QueryTriggerInteraction.Ignore) || Physics.Linecast(eye, p, mask, QueryTriggerInteraction.Ignore)) continue;
            bool spaced = true;
            foreach (var q in _props) if (((Vector3)q - p).sqrMagnitude < 4f) { spaced = false; break; }
            if (!spaced) continue;
            _props.Add(new Vector4(p.x, p.y, p.z, Random.Range(0f, 6.28f)));
            _propDone.Add(false);
            _propDrift.Add(_props.Count % 3 == 0);
        }
    }

    private static void PlaceCrates()
    {
        int want = ChallengeRules.PropCount(Random.value);
        int mask = SemiFunc.LayerMaskGetVisionObstruct();
        var size = CrateVisual.Size;
        for (int tries = 0; tries < 160 && _props.Count < want; tries++)
        {
            var dir = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Vector3.forward;
            var above = _shrine + dir * Random.Range(1.8f, 9f) + Vector3.up * 1.5f;
            if (Physics.Linecast(_shrine + Vector3.up * 1.5f, above, mask, QueryTriggerInteraction.Ignore)) continue;
            if (!Physics.Raycast(above, Vector3.down, out var hit, 3.5f, mask, QueryTriggerInteraction.Ignore) || Vector3.Angle(hit.normal, Vector3.up) > 15f) continue;
            float yaw = Random.Range(0f, 360f);
            var center = hit.point + Vector3.up * (size.y * .5f + .03f);
            if (Physics.CheckBox(center, size * .5f, Quaternion.Euler(0f, yaw, 0f), ~0, QueryTriggerInteraction.Ignore)) continue;
            bool spaced = true;
            foreach (var q in _props) if (((Vector3)q - hit.point).sqrMagnitude < 1.4f) { spaced = false; break; }
            if (!spaced) continue;
            _props.Add(new Vector4(hit.point.x, hit.point.y, hit.point.z, yaw));
            _propDone.Add(false);
            _propDrift.Add(false);
        }
    }

    private static bool PickHuntTarget()
    {
        var director = EnemyDirector.instance;
        EnemyParent best = null;
        if (director != null && director.enemiesSpawned != null)
            foreach (var e in director.enemiesSpawned)
            {
                if (e == null || !e.Spawned || e.Enemy == null) continue;
                if (best == null || e.difficulty > best.difficulty ||
                    e.difficulty == best.difficulty && Health(e) > Health(best)) best = e;
            }
        _huntTarget = best;
        var view = best != null && best.Enemy != null ? best.Enemy.GetComponent<PhotonView>() ?? best.GetComponentInChildren<PhotonView>() : null;
        _huntView = view != null ? view.ViewID : 0;
        return best != null;
    }

    private static int Health(EnemyParent e) => e != null && e.Enemy != null && e.Enemy.Health != null ? e.Enemy.Health.health : 0;

    private static void EscortStep()
    {
        bool near = false;
        foreach (var p in Living())
            if ((p.transform.position - _wisp).sqrMagnitude <= ChallengeRules.EscortFollowRadius * ChallengeRules.EscortFollowRadius) { near = true; break; }
        _wispWaiting = !near;
        if (!near || _pathIndex >= _path.Count) { if (_pathIndex >= _path.Count) Finish(true, "home"); return; }
        _wisp = Vector3.MoveTowards(_wisp, _path[_pathIndex], ChallengeRules.EscortSpeed * Time.deltaTime);
        if ((_wisp - _path[_pathIndex]).sqrMagnitude < ChallengeRules.EscortArriveRadius * ChallengeRules.EscortArriveRadius) _pathIndex++;
    }

    // A crate breaks on a melee swing, a fast physics object (thrown loot, a tumbling player) or (OnShot) a bullet.
    private static void ScanCrates()
    {
        var half = CrateVisual.Size * .5f + Vector3.one * .12f;
        for (int i = 0; i < _props.Count; i++)
        {
            if (_propDone[i]) continue;
            var c = _props[i];
            var center = new Vector3(c.x, c.y + CrateVisual.Size.y * .5f, c.z);
            foreach (var hit in Physics.OverlapBox(center, half, Quaternion.Euler(0f, c.w, 0f), ~0, QueryTriggerInteraction.Collide))
            {
                if (hit == null || hit.GetComponentInParent<CrateVisual>() != null) continue;
                bool melee = hit.GetComponent<HurtCollider>() != null && hit.GetComponentInParent<ItemMelee>() != null && hit.gameObject.activeInHierarchy;
                var body = hit.attachedRigidbody;
                bool impact = body != null && body.GetComponentInParent<PhysGrabObject>() != null && body.velocity.magnitude >= ChallengeRules.CrateImpactSpeed;
                if (melee || impact) { BreakProp(i); break; }
            }
        }
    }

    private static void BreakProp(int i)
    {
        if (i < 0 || i >= _propDone.Count || _propDone[i]) return;
        _propDone[i] = true;
        _progress++;
        if (ChallengeRules.Done(_progress, _total)) Finish(true, "all");
        else Broadcast(force: true);
    }

    /// <summary>Host: a gun shot from <paramref name="from"/> to <paramref name="to"/> (targets and crates).</summary>
    private static void OnShot(Vector3 from, Vector3 to)
    {
        if (!_has || _phase != ChallengePhase.Active || (_kind != ChallengeKind.Targets && _kind != ChallengeKind.Smash)) return;
        var dir = to - from;
        float length = dir.magnitude;
        if (length < .01f) return;
        var ray = new Ray(from, dir / length);
        float time = SharedTime;
        for (int i = 0; i < _props.Count; i++)
        {
            if (_propDone[i]) continue;
            var c = _props[i];
            if (_kind == ChallengeKind.Targets)
            {
                var p = TargetVisual.Position(c, _propDrift[i], c.w, time);
                float along = Vector3.Dot(p - from, ray.direction);
                if (along < 0f || along > length + .5f) continue;
                if ((ray.GetPoint(along) - p).sqrMagnitude <= ChallengeRules.TargetHitRadius * ChallengeRules.TargetHitRadius) BreakProp(i);
            }
            else if (Patches.Effects.PingTargets.RayBox(ray, new Vector3(c.x, c.y, c.z), c.w, CrateVisual.Size + Vector3.one * .1f, length + .5f, out _))
                BreakProp(i);
        }
    }

    internal static void OnEnemyKilled(EnemyParent enemy)
    {
        if (!Host || !_has || _phase != ChallengePhase.Active) return;
        if (_kind == ChallengeKind.Cull) { _progress++; if (ChallengeRules.Done(_progress, _total)) Finish(true, "kills"); else Broadcast(force: true); }
        else if (_kind == ChallengeKind.Hunt && enemy != null && enemy == _huntTarget) { _progress = 1; Finish(true, "hunted"); }
    }

    internal static void OnPlayerDeath()
    {
        if (!Host || !_has || _phase != ChallengePhase.Active || !ChallengeRules.DeathFails(_kind)) return;
        Finish(false, "death");
    }

    private static void Finish(bool won, string why)
    {
        _phase = won ? ChallengePhase.Won : ChallengePhase.Lost;
        _wispWaiting = false;
        if (won) GiveReward();
        Plugin.Log.LogInfo($"[Challenge] {_kind} {(won ? "won" : "lost")} ({why}); reward {_reward}.");
        Broadcast(force: true);
    }

    private static void GiveReward()
    {
        _reward = ChallengeRules.Reward(Random.value);
        var at = _shrine + Vector3.up * 1.4f;
        if (_reward == ChallengeReward.Upgrade)
        {
            var item = Quests.QuestRewards.PickAvailableUpgrade();
            if (item == null || !Quests.QuestRewards.SpawnItemAt(item, at, Quaternion.identity)) _reward = ChallengeReward.SkillPoint;
        }
        else if (_reward == ChallengeReward.Cosmetic && Quests.QuestRewards.SpawnCosmetic(Quests.QuestRank.F, at) == null)
            _reward = ChallengeReward.SkillPoint;
        if (_reward == ChallengeReward.SkillPoint)
            Quests.QuestRewards.GrantSkillPointsToAll(1, "challenge " + ChallengeRules.Name(_kind, false));
        foreach (var p in Living())
            if (!string.IsNullOrEmpty(p.steamID)) Quests.QuestRankProgress.AddCompletion(p.steamID);
    }

    // ================================================================ sync

    private static void Broadcast(bool force)
    {
        if (!SemiFunc.IsMultiplayer() || !PhotonNetwork.InRoom || !PhotonNetwork.IsMasterClient || !_has) return;
        float now = Time.time;
        string signature = $"{_key}|{_phase}|{_kind}|{_progress}|{_total}|{_props.Count}|{_wispWaiting}|{PhotonNetwork.CurrentRoom.PlayerCount}";
        bool moving = _phase == ChallengePhase.Active && _kind == ChallengeKind.Escort;
        if (!force && signature == _sent && !(moving && now >= _nextSync) && now < _nextSync + 8f) return;
        _sent = signature;
        _nextSync = now + SyncInterval;
        var props = new float[_props.Count * 4];
        var flags = new byte[_props.Count];
        for (int i = 0; i < _props.Count; i++)
        {
            props[i * 4] = _props[i].x; props[i * 4 + 1] = _props[i].y; props[i * 4 + 2] = _props[i].z; props[i * 4 + 3] = _props[i].w;
            flags[i] = (byte)((_propDone[i] ? 1 : 0) | (_propDrift[i] ? 2 : 0));
        }
        float remaining = _phase == ChallengePhase.Active ? ChallengeRules.Remaining(_startedAt, now, _kind) : 0f;
        var data = new object[]
        {
            _key, new[] { _shrine.x, _shrine.y, _shrine.z, _yaw },
            new[] { (int)_kind, (int)_phase, _progress, _total, _huntView, (int)_reward, _wispWaiting ? 1 : 0 },
            remaining, new[] { _wisp.x, _wisp.y, _wisp.z, _home.x, _home.y, _home.z }, props, flags,
        };
        PhotonNetwork.RaiseEvent(EventCode, data, new RaiseEventOptions { Receivers = ReceiverGroup.Others }, SendOptions.SendReliable);
    }

    /// <summary>Client: apply the host's challenge state (ModNetwork checks the sender is the host).</summary>
    internal static void HandleEvent(EventData e)
    {
        if (e.CustomData is not object[] d || d.Length != 7 || d[0] is not string key || key.Length > 256 ||
            d[1] is not float[] shrine || shrine.Length != 4 || d[2] is not int[] meta || meta.Length != 7 || d[3] is not float remaining ||
            d[4] is not float[] wisp || wisp.Length != 6 || d[5] is not float[] props || d[6] is not byte[] flags ||
            props.Length != flags.Length * 4 || flags.Length > 20)
            return;
        foreach (var f in shrine) if (!Networking.NetworkPayload.Finite(f)) return;
        foreach (var f in wisp) if (!Networking.NetworkPayload.Finite(f)) return;
        foreach (var f in props) if (!Networking.NetworkPayload.Finite(f)) return;
        if (!Networking.NetworkPayload.Finite(remaining) || meta[0] < 0 || meta[0] >= ChallengeRules.KindCount || meta[1] < 0 || meta[1] > 3 ||
            meta[5] < 0 || meta[5] > 3)
            return;
        if (key != _key || !_has) { Clear(); _key = key; _has = true; }
        _shrine = new Vector3(shrine[0], shrine[1], shrine[2]);
        _yaw = shrine[3];
        _kind = (ChallengeKind)meta[0];
        _phase = (ChallengePhase)meta[1];
        _progress = meta[2]; _total = meta[3]; _huntView = meta[4]; _reward = (ChallengeReward)meta[5]; _wispWaiting = meta[6] != 0;
        _endsAtLocal = Time.time + remaining;
        _wisp = new Vector3(wisp[0], wisp[1], wisp[2]);
        _home = new Vector3(wisp[3], wisp[4], wisp[5]);
        if (_props.Count != flags.Length) { foreach (var v in _propVisuals) if (v != null) Destroy(v.gameObject); _propVisuals.Clear(); }
        _props.Clear(); _propDone.Clear(); _propDrift.Clear();
        for (int i = 0; i < flags.Length; i++)
        {
            _props.Add(new Vector4(props[i * 4], props[i * 4 + 1], props[i * 4 + 2], props[i * 4 + 3]));
            _propDone.Add((flags[i] & 1) != 0);
            _propDrift.Add((flags[i] & 2) != 0);
        }
    }

    // ================================================================ every client: props, HUD, messages

    private static void ClientTick()
    {
        ShowMessages();
        WatchInteract();
        // Won or lost: the reward is already out; the shrine, the wisp's home and anything left shrink away and go.
        bool finished = _phase >= ChallengePhase.Won;
        if (!finished) _finishedAt = -1f;
        else if (_finishedAt < 0f) _finishedAt = Time.time;
        float sinceEnd = finished ? Time.time - _finishedAt : 0f;
        if (finished && sinceEnd >= VanishSeconds)
        {
            if (_shrineVisual != null) { Destroy(_shrineVisual.gameObject); _shrineVisual = null; }
            if (_homeVisual != null) { Destroy(_homeVisual.gameObject); _homeVisual = null; }
            if (_wispVisual != null) { Destroy(_wispVisual.gameObject); _wispVisual = null; }
            if (_beacon != null) { Destroy(_beacon.gameObject); _beacon = null; }
            foreach (var v in _propVisuals) if (v != null) Destroy(v.gameObject);
            _propVisuals.Clear();
            Localization.HudStatus.Clear(StatusKey);
            return;
        }
        float shrink = finished ? Mathf.Clamp01((VanishSeconds - sinceEnd) / 1f) : 1f; // last second: scale to nothing
        if (_shrineVisual == null) _shrineVisual = ShrineVisual.Create(_shrine, _yaw);
        _shrineVisual.transform.localScale = Vector3.one * shrink;
        if (_homeVisual != null) _homeVisual.transform.localScale = Vector3.one * shrink;
        bool active = _phase == ChallengePhase.Active;
        _shrineVisual.SetState(_phase, _phase == ChallengePhase.Ready ? ShrineLabel() : "");

        // Escort: wisp and its home.
        // Escort: the wisp flies while it runs; it and its home stay through the win and vanish with the shrine.
        if (_kind == ChallengeKind.Escort && active)
        {
            if (_homeVisual == null) _homeVisual = WispHomeVisual.Create(_home);
            if (_wispVisual == null) _wispVisual = WispVisual.Create(_wisp);
            _wispVisual.MoveTo(_wisp, _wispWaiting);
        }
        else if (_wispVisual != null)
        {
            _wispVisual.MoveTo(_home + Vector3.up * .6f, false); // settles into its home
            _wispVisual.transform.localScale = _shrineVisual.transform.localScale;
        }

        // Targets / crates.
        if (_kind is ChallengeKind.Targets or ChallengeKind.Smash && _props.Count > 0)
        {
            while (_propVisuals.Count < _props.Count)
            {
                int i = _propVisuals.Count;
                var c = _props[i];
                _propVisuals.Add(_kind == ChallengeKind.Targets
                    ? TargetVisual.Create(new Vector3(c.x, c.y, c.z), _propDrift[i], c.w)
                    : CrateVisual.Create(new Vector3(c.x, c.y, c.z), c.w));
            }
            for (int i = 0; i < _propVisuals.Count && i < _propDone.Count; i++)
            {
                if (_propVisuals[i] == null) continue;
                // Leftovers when the challenge ends (won or lost) just go: no hit flash or shatter for them.
                if (finished && !_propDone[i]) { Destroy(_propVisuals[i].gameObject); continue; }
                if (_propVisuals[i] is TargetVisual t) t.SetHit(_propDone[i]);
                else if (_propVisuals[i] is CrateVisual c) c.SetBroken(_propDone[i]);
            }
        }

        // Hunt beacon over the marked monster.
        if (_kind == ChallengeKind.Hunt && active && _huntView != 0 && _beacon == null)
        {
            var view = PhotonView.Find(_huntView);
            var monster = view != null ? view.transform : Host && _huntTarget != null && _huntTarget.Enemy != null ? _huntTarget.Enemy.transform : null;
            if (monster != null) _beacon = HuntBeacon.Create(monster);
        }
        else if (!active && _beacon != null) { Destroy(_beacon.gameObject); _beacon = null; }

        // HUD line while it runs.
        float remaining = Host ? ChallengeRules.Remaining(_startedAt, Time.time, _kind) : Mathf.Max(0f, _endsAtLocal - Time.time);
        if (active) Localization.HudStatus.Show(StatusKey, ChallengeRules.Status(_kind, _progress, _total, remaining, Russian));
        else Localization.HudStatus.Clear(StatusKey);

    }

    // "[E] START · TARGET PRACTICE" up close (the player's own interact key), just the challenge name from afar.
    private static string ShrineLabel()
    {
        string name = ChallengeRules.Name(_kind, Russian).ToUpperInvariant();
        if (!Near(SemiFunc.PlayerAvatarLocal(), ChallengeRules.PromptRadius)) return name;
        string key;
        try { key = SemiFunc.InputDisplayReplaceTags("[interact]", "", ""); } catch { key = "E"; }
        return $"[{key}] {(Russian ? "НАЧАТЬ" : "START")} · {name}";
    }

    // Start / end messages (the game's own focus text), once per phase change.
    private static void ShowMessages()
    {
        if (_phase != _shownPhase)
        {
            if (_phase == ChallengePhase.Active)
                SemiFunc.UIFocusText(ChallengeRules.Name(_kind, Russian).ToUpperInvariant() + ": " + ChallengeRules.Goal(_kind, _total, Russian), ChallengeProps.Gold, Color.white, 4f);
            else if (_phase == ChallengePhase.Won)
                SemiFunc.UIFocusText((Russian ? "ИСПЫТАНИЕ ПРОЙДЕНО: " : "CHALLENGE COMPLETE: ") + ChallengeRules.RewardText(_reward, Russian).ToUpperInvariant(), ChallengeProps.Win, Color.white, 4f);
            else if (_phase == ChallengePhase.Lost)
                SemiFunc.UIFocusText(Russian ? "ИСПЫТАНИЕ ПРОВАЛЕНО" : "CHALLENGE FAILED", ChallengeProps.Lose, Color.white, 3f);
            _shownPhase = _phase;
        }
    }

    // ================================================================ pings

    private static readonly Vector3 PingSize = new(1.1f, 1.5f, 1.1f);

    /// <summary>The ping ray against the shrine (not after its challenge ended). <paramref name="marker"/> sits on the gem.</summary>
    internal static bool TryAim(Ray ray, float maxDistance, out Vector3 marker, out float distance)
    {
        marker = default;
        distance = 0f;
        if (!_has || _phase >= ChallengePhase.Won || _key != LevelKey() || _shrineVisual == null) return false;
        if (!Patches.Effects.PingTargets.RayBox(ray, _shrine, _yaw, PingSize, maxDistance, out distance)) return false;
        marker = _shrine + Vector3.up * 1.1f;
        return true;
    }

    internal static string PingLabel(bool info)
    {
        string name = Russian ? "Святилище испытаний" : "Challenge Shrine";
        if (!info) return name;
        string state = _phase == ChallengePhase.Active ? (Russian ? "идёт" : "running") : (Russian ? "нажмите взаимодействие, чтобы начать" : "press interact to start");
        return $"{name}: {ChallengeRules.Name(_kind, Russian)} ({state})";
    }

    internal static void Flare(float seconds) { if (_shrineVisual != null) _shrineVisual.Flare(seconds); }

    // ================================================================ hooks

    // Gun shots (every client runs ShootBulletRPC; the host scores it).
    [HarmonyPatch(typeof(ItemGun), nameof(ItemGun.ShootBulletRPC))]
    private static class GunShot
    {
        [HarmonyPostfix]
        private static void Postfix(ItemGun __instance, Vector3 _endPosition)
        {
            if (!Host || __instance == null || __instance.gunMuzzle == null) return;
            OnShot(__instance.gunMuzzle.position, _endPosition);
        }
    }
}
