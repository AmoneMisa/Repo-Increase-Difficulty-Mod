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
/// key (E by default) next to it starts its timed challenge (wisp escort, target practice, hunt, cull, smash, monster waves,
/// vigil, spark hunt, offering, mushroom path or capture the flag); finishing it in time
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
    // Monster Waves: the monsters this shrine spawned, when each died (removed a few seconds later), and wave timing.
    private static readonly List<EnemyParent> _waveEnemies = new();
    private static readonly Dictionary<EnemyParent, float> _waveDead = new();
    private static float _nextWaveAt, _nextLure, _vigilHeld, _nextOfferingScan;

    // Mushroom Path (mirrored): the sequence (mushroom indices), the next step, mistakes so far and when the sequence
    // starts playing (SharedTime, so every client shows it in step). Host: which pads are occupied.
    private static readonly List<int> _seq = new();
    private static int _step, _mistakes;
    private static float _showStart;
    private static int _occupiedMushrooms;
    private static readonly List<Vector3> _mushroomLayout = new();

    // Capture the Flag (mirrored): stage 0 question, 1 flag out (on the shrine, carried or dropped), 2 planted and held.
    // _wisp is the flag (when nobody carries it) and _home its base. Host: the carrier and the defence wave clock.
    private static int _flagStage, _qa, _qb, _qop, _carrierView;
    private static PlayerAvatar _carrier;
    private static float _nextFlagWave;
    private static bool _flagChaser;          // host: the one monster sent after the first carrier was spawned
    private static LineRenderer _flagGuide;   // local: glowing floor path from this player to the base
    private static float _flagGuideAt, _flagGuideMetres = -1f;

    // ---- client visuals
    private static ShrineVisual _shrineVisual;
    private static WispVisual _wispVisual;
    private static WispHomeVisual _homeVisual;
    private static FloorRingVisual _ringVisual;
    private static int _shownProgress = -1;
    private static HuntBeacon _beacon;
    private static readonly List<Component> _propVisuals = new();
    private static ChallengePhase _shownPhase;
    private static readonly List<MushroomVisual> _mushrooms = new();
    private static int _shownStep = -1, _shownMistakes = -1, _shownRound = -1, _shownSlot = -1, _shownStage = -1;
    private static FlagVisual _flagVisual;
    private static FloorRingVisual _baseVisual;
    private static float _finishedAt = -1f;
    private const float VanishSeconds = 3f; // after the result, everything left shrinks away

    private static bool Host => SemiFunc.IsMasterClientOrSingleplayer();

    // Target Practice: whether this client's player was standing at the shrine when it started (and is rooted).
    private static bool _rootDecided, _rooted;

    // Rooted like the net trap: no walking or jumping; looking, aiming, shooting and items are untouched.
    private void FixedUpdate()
    {
        bool running = _has && _phase == ChallengePhase.Active && _kind == ChallengeKind.Targets && _key == LevelKey();
        if (!running) { _rootDecided = _rooted = false; return; }
        var me = SemiFunc.PlayerAvatarLocal();
        if (!_rootDecided)
        {
            _rootDecided = true;
            _rooted = Near(me, ChallengeRules.RootRadius);
        }
        if (!_rooted || me == null || me.isDisabled) return;
        var controller = PlayerController.instance;
        if (controller == null || controller.playerAvatarScript != me) return;
        controller.OverrideSpeed(0f, .12f);
        controller.OverrideJumpCooldown(.2f);
        controller.JumpImpulse = false;
        controller.JumpInputBuffer = 0f;
        controller.VelocityImpulse = Vector3.zero;
        if (controller.rb != null && !controller.rb.isKinematic)
        {
            var v = controller.rb.velocity;
            controller.rb.velocity = new Vector3(0f, Mathf.Min(v.y, 0f), 0f);
        }
    }
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
        if (_ringVisual != null) Destroy(_ringVisual.gameObject);
        if (_flagVisual != null) Destroy(_flagVisual.gameObject);
        if (_baseVisual != null) Destroy(_baseVisual.gameObject);
        if (_flagGuide != null) Destroy(_flagGuide.gameObject);
        _flagGuideMetres = -1f; _flagChaser = false;
        foreach (var m in _mushrooms) if (m != null) Destroy(m.gameObject);
        _mushrooms.Clear(); _seq.Clear(); _mushroomLayout.Clear();
        _step = _mistakes = _occupiedMushrooms = 0; _showStart = 0f;
        _flagStage = _qa = _qb = _qop = _carrierView = 0; _carrier = null;
        _shownStep = _shownMistakes = _shownRound = _shownSlot = _shownStage = -1;
        _waveEnemies.Clear(); _waveDead.Clear(); _shownProgress = -1;
        foreach (var v in _propVisuals) if (v != null) Destroy(v.gameObject);
        _propVisuals.Clear();
        _props.Clear(); _propDone.Clear(); _propDrift.Clear(); _path.Clear(); _pathRadius.Clear();
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
        if (_kind == ChallengeKind.Mushrooms)
        {
            int first = Random.Range(0, spots.Count);
            for (int i = 0; i < spots.Count; i++)
            {
                var candidate = spots[(first + i) % spots.Count];
                if (!PlanMushrooms(candidate.Floor)) continue;
                _shrine = candidate.Floor;
                _yaw = candidate.Yaw;
                break;
            }
            if (_mushroomLayout.Count == 0) _kind = ChallengeKind.Vigil;
        }
        if (_kind == ChallengeKind.Escort && !PlanEscort(points)) _kind = ChallengeKind.Targets;
        Plugin.Log.LogInfo($"[Challenge] Shrine placed at {_shrine}: {_kind}.");
    }

    // The wisp's home: a level point 25-55 m of walking away, by a route through the level's own obstacles
    // (EscortRoute: jumps, climbs, drops and crawl spaces the wisp floats straight across).
    private static readonly List<float> _pathRadius = new();

    private static bool PlanEscort(List<LevelPoint> points)
    {
        if (!NavMesh.SamplePosition(_shrine, out var start, 2f, NavMesh.AllAreas)) return false;
        var route = new List<EscortRoute.Point>();
        if (!EscortRoute.Plan(start.position, points, ChallengeRules.EscortMinPath, ChallengeRules.EscortMaxPath,
                ChallengeRules.EscortFollowRadius, route, out var home, out int features)) return false;
        _path.Clear(); _pathRadius.Clear();
        foreach (var p in route) { _path.Add(p.Position); _pathRadius.Add(p.Radius); }
        _home = home;
        Plugin.Log.LogInfo($"[Challenge] Wisp route: {_path.Count} points, obstacle score {features}.");
        return true;
    }

    // ================================================================ host: running

    private static void HostTick()
    {
        float now = Time.time;
        if (_phase == ChallengePhase.Active)
        {
            if (_kind == ChallengeKind.Flag) FlagStep(now); // keeps its own clock (answer and carry, then the hold)
            else if (ChallengeRules.Expired(_startedAt, now, _kind)) Finish(false, "time");
            else if (_kind == ChallengeKind.Escort) EscortStep();
            else if (_kind == ChallengeKind.Mushrooms) MushroomStep();
            else if (_kind == ChallengeKind.Smash && now >= _nextScan) { _nextScan = now + ScanInterval; ScanCrates(); }
            else if (_kind == ChallengeKind.Hunt && (_huntTarget == null || !_huntTarget.Spawned) && _progress == 0) PickHuntTarget();
            else if (_kind == ChallengeKind.Cull) CullStep(now);
            else if (_kind == ChallengeKind.Waves) WavesStep(now);
            else if (_kind == ChallengeKind.Vigil) VigilStep(now);
            else if (_kind == ChallengeKind.Sparks && now >= _nextScan) { _nextScan = now + ScanInterval; ScanSparks(); }
            else if (_kind == ChallengeKind.Offering && now >= _nextOfferingScan) { _nextOfferingScan = now + .5f; ScanOffering(); }
        }
        RemoveDeadWaveMonsters(now, all: false);
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
            case ChallengeKind.Waves: _total = ChallengeRules.WaveCount; _nextWaveAt = Time.time + 3f; _waveEnemies.Clear(); _waveDead.Clear(); break;
            case ChallengeKind.Vigil: StartVigil(); break;
            case ChallengeKind.Sparks: PlaceSparks(); _total = _props.Count; break;
            case ChallengeKind.Offering: _total = ChallengeRules.OfferingTarget(LevelLootValue()); _nextOfferingScan = 0f; break;
            case ChallengeKind.Mushrooms: PlaceMushrooms(); StartMushrooms(); break;
            case ChallengeKind.Flag: StartFlag(); break;
            default: _total = ChallengeRules.CullTarget(Living().Count); _waveEnemies.Clear(); _waveDead.Clear(); _nextWaveAt = Time.time + 2f; break;
        }
        if (_kind == ChallengeKind.Waves && WaveSetups().Count == 0) { _kind = ChallengeKind.Cull; _total = ChallengeRules.CullTarget(Living().Count); }
        if (_kind == ChallengeKind.Mushrooms && _props.Count < ChallengeRules.MushroomMinCount || _kind == ChallengeKind.Flag && _flagStage < 0)
        {
            _kind = ChallengeKind.Vigil; // no flat ground for mushrooms, or nowhere to carry the flag, on this level
            _props.Clear(); _propDone.Clear(); _propDrift.Clear(); _seq.Clear(); _flagStage = 0;
            StartVigil();
        }
        if (_kind == ChallengeKind.Sparks && _props.Count < 4)
        {
            _kind = ChallengeKind.Vigil; // not enough far-off spots for sparks on this level
            _props.Clear(); _propDone.Clear(); _propDrift.Clear();
            StartVigil();
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

    // Target Practice: the shooters are rooted where they stand, so every target must be in clear sight of every one of
    // them (and of the shrine), along the whole path of a drifting target.
    private static void PlaceTargets()
    {
        int want = ChallengeRules.PropCount(Random.value);
        int mask = SemiFunc.LayerMaskGetVisionObstruct();
        var eyes = new List<Vector3> { _shrine + Vector3.up * 1.4f };
        foreach (var p in Living())
            if (Near(p, ChallengeRules.RootRadius)) eyes.Add(p.transform.position + Vector3.up * 1.5f);
        for (int tries = 0; tries < 240 && _props.Count < want; tries++)
        {
            var dir = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Vector3.forward;
            var p = _shrine + dir * Random.Range(5f, 13f) + Vector3.up * Random.Range(1.2f, 3.2f);
            if (Physics.CheckSphere(p, 1f, mask, QueryTriggerInteraction.Ignore)) continue;
            bool drift = (_props.Count + 1) % 3 == 0;
            if (!InSight(eyes, p, drift, mask)) continue;
            bool spaced = true;
            foreach (var q in _props) if (((Vector3)q - p).sqrMagnitude < 4f) { spaced = false; break; }
            if (!spaced) continue;
            _props.Add(new Vector4(p.x, p.y, p.z, Random.Range(0f, 6.28f)));
            _propDone.Add(false);
            _propDrift.Add(drift);
        }
    }

    // Clear line from each eye to the target (its centre and both ends of its drift loop, see TargetVisual.Position).
    private static bool InSight(List<Vector3> eyes, Vector3 target, bool drift, int mask)
    {
        var points = drift ? new[] { target, target + new Vector3(.9f, .3f, 0f), target - new Vector3(.9f, .3f, 0f) } : new[] { target };
        foreach (var eye in eyes)
            foreach (var point in points)
            {
                var to = point - eye;
                if (to.magnitude < 1f) return false;
                // Start a little out from the eye so the shooter's own body never blocks the check.
                if (Physics.Linecast(eye + to.normalized * .6f, point, mask, QueryTriggerInteraction.Ignore)) return false;
            }
        return true;
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
        // Over a jump or a crawl space it only moves on with someone close behind it (no walking round).
        float radius = _pathIndex < _pathRadius.Count ? _pathRadius[_pathIndex] : ChallengeRules.EscortFollowRadius;
        foreach (var p in Living())
            if ((p.transform.position + Vector3.up * .6f - _wisp).sqrMagnitude <= radius * radius) { near = true; break; }
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

    // ---------------------------------------------------------------- Monster Waves

    // Monsters come one wave at a time: the next wave spawns only after every monster of this one is dead.
    private static void WavesStep(float now)
    {
        if (_waveEnemies.Count > 0)
        {
            bool cleared = true;
            foreach (var e in _waveEnemies)
                if (e != null && !_waveDead.ContainsKey(e)) { cleared = false; break; }
            if (!cleared)
            {
                // Keep the wave coming at the shrine rather than wandering off.
                if (now >= _nextLure) { _nextLure = now + 6f; try { SemiFunc.EnemyInvestigate(_shrine, 45f); } catch { } }
                return;
            }
            _waveEnemies.Clear(); // the dead stay in _waveDead until they are removed
            _progress++;
            if (ChallengeRules.Done(_progress, _total)) { Finish(true, "waves"); return; }
            _nextWaveAt = now + ChallengeRules.WaveBreak;
            Broadcast(force: true);
            return;
        }
        if (now < _nextWaveAt) return;
        SpawnWave(_progress + 1, _shrine);
        _nextLure = now + 4f;
        if (_waveEnemies.Count == 0)
        {
            // Nothing could spawn: count the wave so the shrine never hangs.
            _progress++;
            if (ChallengeRules.Done(_progress, _total)) Finish(true, "waves");
            else _nextWaveAt = now + ChallengeRules.WaveBreak;
        }
    }

    // Later waves draw from the tougher monster pools.
    private static List<EnemySetup> WaveSetups(int wave = 1)
    {
        var list = new List<EnemySetup>();
        var d = EnemyDirector.instance;
        if (d == null) return list;
        void Add(List<EnemySetup> pool)
        {
            if (pool == null) return;
            foreach (var s in pool) if (s != null && s.spawnObjects != null && s.spawnObjects.Count > 0 && s.spawnObjects.Count <= 3) list.Add(s);
        }
        Add(d.enemiesDifficulty1);
        if (wave >= 2) Add(d.enemiesDifficulty2);
        if (wave >= 3) Add(d.enemiesDifficulty3);
        if (list.Count == 0) { Add(d.enemiesDifficulty1); Add(d.enemiesDifficulty2); Add(d.enemiesDifficulty3); }
        return list;
    }

    private static void SpawnWave(int wave, Vector3 centre, int exactCount = 0)
    {
        var setups = WaveSetups(wave);
        if (setups.Count == 0) return;
        var spots = new List<Vector3>();
        var points = SemiFunc.LevelPointsGetAll();
        if (points != null)
            foreach (var p in points)
            {
                if (p == null || p.Truck || p.inStartRoom || (p.Room != null && p.Room.Truck)) continue;
                float d = Vector3.Distance(p.transform.position, centre);
                if (d >= 12f && d <= 32f) spots.Add(p.transform.position);
            }
        if (spots.Count == 0 && points != null)
            foreach (var p in points) if (p != null && !p.Truck && !p.inStartRoom) spots.Add(p.transform.position);
        if (spots.Count == 0) return;
        int count = exactCount > 0 ? exactCount : ChallengeRules.WaveSize(wave, Living().Count);
        for (int i = 0; i < count; i++)
        {
            var setup = setups[Random.Range(0, setups.Count)];
            var at = spots[Random.Range(0, spots.Count)];
            foreach (var prefab in setup.spawnObjects)
            {
                try
                {
                    var go = SemiFunc.IsMultiplayer()
                        ? PhotonNetwork.InstantiateRoomObject(prefab.ResourcePath, at, Quaternion.identity, 0, EnemyFloaterHalloweenState.SpawnData(prefab))
                        : Instantiate(prefab.Prefab, at, Quaternion.identity);
                    var parent = go != null ? go.GetComponent<EnemyParent>() : null;
                    if (parent == null) continue;
                    parent.SetupDone = true;
                    parent.firstSpawnPointUsed = true;
                    var enemy = go.GetComponentInChildren<Enemy>();
                    if (enemy != null) enemy.EnemyTeleported(at);
                    _waveEnemies.Add(parent);
                }
                catch (System.Exception e) { Plugin.Log.LogWarning($"[Challenge] Wave spawn of {setup.name} failed: {e.Message}"); }
            }
        }
        Plugin.Log.LogInfo($"[Challenge] Wave {wave}: spawned {_waveEnemies.Count} monster(s).");
    }

    // Wave monsters do not come back: a few seconds after death (so their orb drops) they are removed for good.
    // all = also remove the living ones (a lost wave challenge takes its monsters with it).
    private static void RemoveDeadWaveMonsters(float now, bool all)
    {
        if (_waveDead.Count == 0 && !(all && _waveEnemies.Count > 0)) return;
        var gone = new List<EnemyParent>();
        foreach (var pair in _waveDead) if (pair.Key == null || all || now - pair.Value >= 6f) gone.Add(pair.Key);
        if (all) foreach (var e in _waveEnemies) if (!gone.Contains(e)) gone.Add(e);
        foreach (var e in gone)
        {
            _waveDead.Remove(e);
            _waveEnemies.Remove(e);
            if (e == null) continue;
            try
            {
                if (EnemyDirector.instance != null) EnemyDirector.instance.enemiesSpawned.Remove(e);
                if (SemiFunc.IsMultiplayer()) PhotonNetwork.Destroy(e.gameObject); else Destroy(e.gameObject);
            }
            catch (System.Exception ex) { Plugin.Log.LogWarning("[Challenge] Removing a wave monster failed: " + ex.Message); }
        }
    }

    // ---------------------------------------------------------------- Vigil, Sparks, Offering

    private static void StartVigil()
    {
        _total = (int)ChallengeRules.VigilSeconds;
        _vigilHeld = 0f;
        _nextLure = Time.time + 4f;
    }

    private static void VigilStep(float now)
    {
        foreach (var p in Living())
            if (Near(p, ChallengeRules.VigilRadius)) { _vigilHeld += Time.deltaTime; break; }
        // The shrine's light calls the monsters nearby while the vigil lasts.
        if (now >= _nextLure) { _nextLure = now + 8f; try { SemiFunc.EnemyInvestigate(_shrine, 30f); } catch { } }
        int held = Mathf.FloorToInt(_vigilHeld);
        if (held == _progress) return;
        _progress = held;
        if (ChallengeRules.Done(_progress, _total)) Finish(true, "held");
    }

    private static void PlaceSparks()
    {
        var points = SemiFunc.LevelPointsGetAll();
        if (points == null) return;
        var shuffled = new List<LevelPoint>(points);
        for (int i = shuffled.Count - 1; i > 0; i--) { int j = Random.Range(0, i + 1); (shuffled[i], shuffled[j]) = (shuffled[j], shuffled[i]); }
        foreach (var p in shuffled)
        {
            if (_props.Count >= ChallengeRules.SparkCount) break;
            if (p == null || p.Truck || p.inStartRoom || (p.Room != null && p.Room.Truck)) continue;
            var at = p.transform.position + Vector3.up * 1.1f;
            float d = Vector3.Distance(at, _shrine);
            if (d < ChallengeRules.SparkMinDistance || d > ChallengeRules.SparkMaxDistance) continue;
            bool spaced = true;
            foreach (var q in _props) if (((Vector3)q - at).sqrMagnitude < 36f) { spaced = false; break; }
            if (!spaced) continue;
            _props.Add(new Vector4(at.x, at.y, at.z, Random.Range(0f, 6.28f)));
            _propDone.Add(false);
            _propDrift.Add(false);
        }
    }

    private static void ScanSparks()
    {
        var living = Living();
        float r2 = ChallengeRules.SparkRadius * ChallengeRules.SparkRadius;
        for (int i = 0; i < _props.Count; i++)
        {
            if (_propDone[i]) continue;
            var at = (Vector3)_props[i];
            foreach (var p in living)
            {
                var d = p.transform.position + Vector3.up * .8f - at;
                if (d.sqrMagnitude <= r2) { BreakProp(i); break; }
            }
            if (_phase != ChallengePhase.Active) return;
        }
    }

    private static float LevelLootValue()
    {
        float total = 0f;
        foreach (var v in FindObjectsOfType<ValuableObject>()) if (v != null) total += v.dollarValueCurrent;
        return total;
    }

    // Loot resting (or held) inside the ring counts; taking it back out lowers the total again.
    private static void ScanOffering()
    {
        float sum = 0f, r2 = ChallengeRules.OfferingRadius * ChallengeRules.OfferingRadius;
        foreach (var v in FindObjectsOfType<ValuableObject>())
        {
            if (v == null) continue;
            var d = v.transform.position - _shrine;
            if (d.y < -1f || d.y > 2.5f || d.x * d.x + d.z * d.z > r2) continue;
            sum += v.dollarValueCurrent;
        }
        int value = Mathf.RoundToInt(sum);
        if (value == _progress) return;
        _progress = value;
        if (ChallengeRules.Done(_progress, _total)) Finish(true, "offered");
    }

    // ---------------------------------------------------------------- Mushroom Path

    // Two staggered rows in the same view from the shrine. Accept the whole layout or try another spot.
    private static bool PlanMushrooms(Vector3 shrine)
    {
        int mask = SemiFunc.LayerMaskGetVisionObstruct();
        float h = ChallengeRules.MushroomCapHeight;
        float startYaw = Random.Range(0f, 360f);
        var eye = shrine + Vector3.up * 1.6f;
        foreach (float spacing in new[] { ChallengeRules.MushroomSpacing, 1.38f })
        for (int direction = 0; direction < 16; direction++)
        {
            _mushroomLayout.Clear();
            var rotation = Quaternion.Euler(0f, startYaw + direction * 22.5f, 0f);
            for (int i = 0; i < ChallengeRules.MushroomCount; i++)
            {
                float x = (i < 3 ? i - 1f : i - 4.5f) * spacing;
                float z = i < 3 ? 3.1f : 3.1f + spacing;
                var probe = shrine + rotation * new Vector3(x, 1f, z);
                if (!Physics.Raycast(probe, Vector3.down, out var floor, 1.45f, mask, QueryTriggerInteraction.Ignore) ||
                    Mathf.Abs(floor.point.y - shrine.y) > .22f || floor.normal.y < .97f ||
                    Physics.Linecast(eye, floor.point + Vector3.up * (h + .2f), mask, QueryTriggerInteraction.Ignore) ||
                    Physics.CheckBox(floor.point + Vector3.up * 1.22f, new Vector3(.53f, 1.14f, .53f),
                        rotation, mask, QueryTriggerInteraction.Ignore)) break;
                _mushroomLayout.Add(floor.point);
            }
            if (_mushroomLayout.Count == ChallengeRules.MushroomCount) return true;
        }
        _mushroomLayout.Clear();
        return false;
    }

    private static void PlaceMushrooms()
    {
        foreach (var floor in _mushroomLayout)
        {
            _props.Add(new Vector4(floor.x, floor.y, floor.z, _props.Count));
            _propDone.Add(false);
            _propDrift.Add(false);
        }
    }

    private static void StartMushrooms()
    {
        _total = ChallengeRules.MushroomRounds;
        _step = _mistakes = 0;
        _seq.Clear();
        _occupiedMushrooms = 0;
        if (_props.Count < ChallengeRules.MushroomMinCount) return;
        for (int i = 0; i < ChallengeRules.MushroomLength(1); i++) AddToSequence();
        _showStart = SharedTime + 3f; // after the start message
    }

    private static void AddToSequence()
    {
        int last = _seq.Count > 0 ? _seq[_seq.Count - 1] : -1, pick;
        do pick = Random.Range(0, _props.Count); while (pick == last && _props.Count > 1);
        _seq.Add(pick);
    }

    private static float MushroomElapsed()
    {
        float elapsed = SharedTime - _showStart;
        if (!SemiFunc.IsMultiplayer()) return elapsed;
        if (elapsed < -1800f) return elapsed + 3600f;
        return elapsed > 1800f ? elapsed - 3600f : elapsed;
    }

    private static bool MushroomShowing() => MushroomElapsed() < _seq.Count * ChallengeRules.MushroomStep;

    // Feet on a cap: within its radius and about its height above the floor it stands on.
    private static int MushroomUnder(PlayerAvatar p)
    {
        if (!p.isGrounded || p.isTumbling || p.deadSet) return -1;
        var capsule = p.isLocal ? PlayerController.instance?.col : p.playerAvatarCollision?.Collider;
        if (capsule == null) return -1;
        var bounds = capsule.bounds;
        var feet = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
        for (int i = 0; i < _mushrooms.Count; i++)
            if (_mushrooms[i] != null && _mushrooms[i].Supports(feet)) return i;
        return -1;
    }

    // Landing on a mushroom presses it once; standing there does not press it again until you leave and come back.
    // While the sequence plays, steps only update where people stand.
    private static void MushroomStep()
    {
        int occupied = 0;
        foreach (var p in Living())
        {
            int at = MushroomUnder(p);
            if (at >= 0) occupied |= 1 << at;
        }
        // One pad is one press. If co-op landings arrive together, count a correct one first.
        int press = _step < _seq.Count
            ? ChallengeRules.MushroomPress(_occupiedMushrooms, occupied, _seq[_step], MushroomShowing())
            : -1;
        _occupiedMushrooms = occupied;
        if (press >= 0) PressMushroom(press);
    }

    private static void PressMushroom(int index)
    {
        if (_step >= _seq.Count) return;
        if (_seq[_step] == index)
        {
            _step++;
            if (_step >= _seq.Count)
            {
                _progress++;
                if (ChallengeRules.Done(_progress, _total)) { Finish(true, "path"); return; }
                AddToSequence(); // the same path again, one mushroom longer
                _step = 0;
                _showStart = SharedTime + 1.8f;
            }
        }
        else
        {
            _mistakes++;
            if (_mistakes >= ChallengeRules.MushroomMistakes) { Finish(false, "mistakes"); return; }
            _step = 0;
            _showStart = SharedTime + 1.8f; // watch it again
        }
        Broadcast(force: true);
    }

    // ---------------------------------------------------------------- Capture the Flag

    private static void StartFlag()
    {
        _flagStage = -1; // stays -1 if there is nowhere to carry the flag (Begin falls back to Vigil)
        if (!PlanFlagBase()) return;
        var q = ChallengeRules.MathQuestion(Random.value, Random.value, Random.value);
        _qa = q.A; _qb = q.B; _qop = q.Op;
        _flagStage = 0;
        _carrier = null; _carrierView = 0; _flagChaser = false;
        _wisp = _shrine + Vector3.up * 1.05f;
        _total = (int)ChallengeRules.FlagDefendSeconds;
        Plugin.Log.LogInfo($"[Challenge] Flag question {ChallengeRules.MathText(_qa, _qb, _qop)} = {ChallengeRules.MathAnswer(_qa, _qb, _qop)}; base at {_home}.");
    }

    // The flag's base: a level point 22-50 m of walking from the shrine.
    private static bool PlanFlagBase()
    {
        var points = SemiFunc.LevelPointsGetAll();
        if (points == null || !NavMesh.SamplePosition(_shrine, out var start, 2f, NavMesh.AllAreas)) return false;
        var shuffled = new List<LevelPoint>(points);
        for (int i = shuffled.Count - 1; i > 0; i--) { int j = Random.Range(0, i + 1); (shuffled[i], shuffled[j]) = (shuffled[j], shuffled[i]); }
        var path = new NavMeshPath();
        foreach (var p in shuffled)
        {
            if (p == null || p.Truck || p.inStartRoom || (p.Room != null && p.Room.Truck)) continue;
            if (!NavMesh.SamplePosition(p.transform.position, out var end, 2f, NavMesh.AllAreas)) continue;
            if (!NavMesh.CalculatePath(start.position, end.position, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete) continue;
            float length = 0f;
            for (int i = 1; i < path.corners.Length; i++) length += Vector3.Distance(path.corners[i - 1], path.corners[i]);
            if (length < ChallengeRules.FlagMinPath || length > ChallengeRules.FlagMaxPath) continue;
            _home = end.position;
            return true;
        }
        return false;
    }

    /// <summary>Host: a chat line from <paramref name="who"/>; the right answer to the flag question releases the flag.</summary>
    private static void OnChat(PlayerAvatar who, string message)
    {
        if (!Host || !_has || _phase != ChallengePhase.Active || _kind != ChallengeKind.Flag || _flagStage != 0 || who == null) return;
        var answer = ChallengeRules.ParseAnswer(message);
        if (answer == null) return;
        if (answer.Value != ChallengeRules.MathAnswer(_qa, _qb, _qop))
        {
            Plugin.Log.LogInfo($"[Challenge] {SemiFunc.PlayerGetName(who)} answered {answer.Value}: wrong.");
            return;
        }
        Plugin.Log.LogInfo($"[Challenge] {SemiFunc.PlayerGetName(who)} answered the flag question.");
        _flagStage = 1;
        Broadcast(force: true);
    }

    private static void FlagStep(float now)
    {
        if (_flagStage < 2)
        {
            if (ChallengeRules.Expired(_startedAt, now, ChallengeKind.Flag)) { Finish(false, "time"); return; }
            if (_flagStage == 0) return;
            if (_carrier != null && (_carrier.isDisabled || !_carrier.gameObject.activeInHierarchy))
            {
                _wisp = _carrier.transform.position + Vector3.up * .1f; // falls where its carrier fell; anyone can pick it up
                _carrier = null; _carrierView = 0;
                Broadcast(force: true);
            }
            if (_carrier == null)
            {
                foreach (var p in Living())
                    if ((p.transform.position + Vector3.up * .9f - _wisp).sqrMagnitude <= ChallengeRules.FlagPickupRadius * ChallengeRules.FlagPickupRadius)
                    {
                        _carrier = p;
                        _carrierView = p.photonView != null ? p.photonView.ViewID : 0;
                        // Taking the flag is noticed: one monster comes for the first carrier.
                        if (!_flagChaser) { _flagChaser = true; SpawnWave(1, p.transform.position, 1); _nextLure = now + 3f; }
                        Broadcast(force: true);
                        break;
                    }
                return;
            }
            _wisp = _carrier.transform.position + Vector3.up * 1.15f;
            // Monsters nearby keep being drawn to whoever carries the flag.
            if (now >= _nextLure) { _nextLure = now + 6f; try { SemiFunc.EnemyInvestigate(_carrier.transform.position, 30f); } catch { } }
            var d = _carrier.transform.position - _home;
            if (Mathf.Abs(d.y) < 2f && d.x * d.x + d.z * d.z <= ChallengeRules.FlagBaseRadius * ChallengeRules.FlagBaseRadius)
            {
                // Planted: from now on nobody may die for two minutes while the monsters come for it.
                _flagStage = 2;
                _carrier = null; _carrierView = 0;
                _wisp = _home;
                _startedAt = now;
                _progress = 0;
                _waveEnemies.Clear(); _waveDead.Clear();
                _nextFlagWave = now + 4f;
                _nextLure = now + 6f;
                Plugin.Log.LogInfo("[Challenge] Flag planted at its base.");
                Broadcast(force: true);
            }
            return;
        }
        float held = now - _startedAt;
        _progress = Mathf.FloorToInt(held);
        if (held >= ChallengeRules.FlagDefendSeconds) { Finish(true, "held"); return; }
        if (now >= _nextFlagWave)
        {
            _nextFlagWave = now + ChallengeRules.FlagWaveEvery;
            SpawnWave(Mathf.Min(3, 1 + Mathf.FloorToInt(held / ChallengeRules.FlagWaveEvery)), _home);
        }
        if (now >= _nextLure) { _nextLure = now + 6f; try { SemiFunc.EnemyInvestigate(_home, 45f); } catch { } }
    }

    // Cull brings its own monsters: up to three of the shrine's alive at once (never more than are still needed), spawned
    // 12-32 m from the shrine and drawn to it, so it can always be finished. Kills of any monster still count.
    private static void CullStep(float now)
    {
        int alive = 0;
        foreach (var e in _waveEnemies) if (e != null && !_waveDead.ContainsKey(e) && e.Spawned) alive++;
        int needed = _total - _progress;
        if (now >= _nextWaveAt && alive < Mathf.Min(3, needed))
        {
            _nextWaveAt = now + 4f;
            SpawnWave(1 + Mathf.Min(2, _progress / 2), _shrine, 1);   // tougher pools as the count climbs
            _nextLure = now + 2f;
        }
        if (now >= _nextLure) { _nextLure = now + 6f; try { SemiFunc.EnemyInvestigate(_shrine, 45f); } catch { } }
    }

    private static float RemainingNow(float now) => _kind == ChallengeKind.Flag && _flagStage == 2
        ? Mathf.Max(0f, ChallengeRules.FlagDefendSeconds - (now - _startedAt))
        : ChallengeRules.Remaining(_startedAt, now, _kind);

    internal static void OnEnemyKilled(EnemyParent enemy)
    {
        if (!Host || !_has || _phase != ChallengePhase.Active) return;
        if (_kind is ChallengeKind.Waves or ChallengeKind.Flag or ChallengeKind.Cull && enemy != null && _waveEnemies.Contains(enemy) && !_waveDead.ContainsKey(enemy)) _waveDead[enemy] = Time.time;
        if (_kind == ChallengeKind.Cull) { _progress++; if (ChallengeRules.Done(_progress, _total)) Finish(true, "kills"); else Broadcast(force: true); }
        else if (_kind == ChallengeKind.Hunt && enemy != null && enemy == _huntTarget) { _progress = 1; Finish(true, "hunted"); }
    }

    internal static void OnPlayerDeath()
    {
        if (!Host || !_has || _phase != ChallengePhase.Active) return;
        if (!ChallengeRules.DeathFails(_kind) && !(_kind == ChallengeKind.Flag && _flagStage == 2)) return;
        Finish(false, "death");
    }

    private static void Finish(bool won, string why)
    {
        _phase = won ? ChallengePhase.Won : ChallengePhase.Lost;
        _wispWaiting = false;
        if (_kind == ChallengeKind.Waves && !won || _kind is ChallengeKind.Flag or ChallengeKind.Cull) RemoveDeadWaveMonsters(Time.time, all: true); // shrine monsters leave with it
        _carrier = null; _carrierView = 0;
        if (won) GiveReward();
        Plugin.Log.LogInfo($"[Challenge] {_kind} {(won ? "won" : "lost")} ({why}); reward {_reward}.");
        Broadcast(force: true);
    }

    private static void GiveReward()
    {
        _reward = ChallengeRules.Reward(Random.value, ModConfig.CostumeRewardsEnabled.Value);
        var at = _shrine + Vector3.up * 1.4f;
        if (_reward == ChallengeReward.Upgrade)
        {
            var item = Quests.QuestRewards.PickAvailableUpgrade();
            if (item == null || !Quests.QuestRewards.SpawnItemAt(item, at, Quaternion.identity)) _reward = ChallengeReward.SkillPoint;
        }
        else if (_reward == ChallengeReward.Cosmetic && Quests.QuestRewards.SpawnCosmetic(Quests.QuestRank.F, at) == null)
            _reward = ChallengeReward.SkillPoint;
        if (_reward == ChallengeReward.Costume)
            Cosmetics.CostumeRewards.GrantToAll("shrine " + ChallengeRules.Name(_kind, false));
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
        string signature = $"{_key}|{_phase}|{_kind}|{_progress}|{_total}|{_props.Count}|{_wispWaiting}|{PhotonNetwork.CurrentRoom.PlayerCount}|{_step}|{_mistakes}|{_seq.Count}|{_flagStage}|{_carrierView}";
        bool moving = _phase == ChallengePhase.Active && (_kind == ChallengeKind.Escort || _kind == ChallengeKind.Flag && _carrierView != 0);
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
        float remaining = _phase == ChallengePhase.Active ? RemainingNow(now) : 0f;
        // Mushroom Path and Capture the Flag: step, mistakes, show start (ms of SharedTime), flag stage, question,
        // carrier view, then the sequence.
        var extra = new int[8 + _seq.Count];
        extra[0] = _step; extra[1] = _mistakes; extra[2] = Mathf.RoundToInt(_showStart * 1000f); extra[3] = _flagStage;
        extra[4] = _qa; extra[5] = _qb; extra[6] = _qop; extra[7] = _carrierView;
        for (int i = 0; i < _seq.Count; i++) extra[8 + i] = _seq[i];
        var data = new object[]
        {
            _key, new[] { _shrine.x, _shrine.y, _shrine.z, _yaw },
            new[] { (int)_kind, (int)_phase, _progress, _total, _huntView, (int)_reward, _wispWaiting ? 1 : 0 },
            remaining, new[] { _wisp.x, _wisp.y, _wisp.z, _home.x, _home.y, _home.z }, props, flags, extra,
        };
        PhotonNetwork.RaiseEvent(EventCode, data, new RaiseEventOptions { Receivers = ReceiverGroup.Others }, SendOptions.SendReliable);
    }

    /// <summary>Client: apply the host's challenge state (ModNetwork checks the sender is the host).</summary>
    internal static void HandleEvent(EventData e)
    {
        if (e.CustomData is not object[] d || d.Length != 8 || d[0] is not string key || key.Length > 256 ||
            d[1] is not float[] shrine || shrine.Length != 4 || d[2] is not int[] meta || meta.Length != 7 || d[3] is not float remaining ||
            d[4] is not float[] wisp || wisp.Length != 6 || d[5] is not float[] props || d[6] is not byte[] flags ||
            props.Length != flags.Length * 4 || flags.Length > 20 || d[7] is not int[] extra || extra.Length < 8 || extra.Length > 8 + 16)
            return;
        for (int i = 8; i < extra.Length; i++) if (extra[i] < 0 || extra[i] >= flags.Length) return;
        if (extra[3] < 0 || extra[3] > 2 || extra[6] < 0 || extra[6] > 2) return;
        foreach (var f in shrine) if (!Networking.NetworkPayload.Finite(f)) return;
        foreach (var f in wisp) if (!Networking.NetworkPayload.Finite(f)) return;
        foreach (var f in props) if (!Networking.NetworkPayload.Finite(f)) return;
        if (!Networking.NetworkPayload.Finite(remaining) || meta[0] < 0 || meta[0] >= ChallengeRules.KindCount || meta[1] < 0 || meta[1] > 3 ||
            meta[5] < 0 || meta[5] > 4)
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
        _step = extra[0]; _mistakes = extra[1]; _showStart = extra[2] / 1000f; _flagStage = extra[3];
        _qa = extra[4]; _qb = extra[5]; _qop = extra[6]; _carrierView = extra[7];
        _seq.Clear();
        for (int i = 8; i < extra.Length; i++) _seq.Add(extra[i]);
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
            if (_ringVisual != null) { Destroy(_ringVisual.gameObject); _ringVisual = null; }
            if (_flagVisual != null) { Destroy(_flagVisual.gameObject); _flagVisual = null; }
            if (_baseVisual != null) { Destroy(_baseVisual.gameObject); _baseVisual = null; }
            foreach (var m in _mushrooms) if (m != null) Destroy(m.gameObject);
            _mushrooms.Clear();
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

        // Vigil and Offering: a glowing ring on the floor around the shrine while they run.
        if ((_kind == ChallengeKind.Vigil || _kind == ChallengeKind.Offering) && active)
        {
            if (_ringVisual == null)
                _ringVisual = FloorRingVisual.Create(_shrine, _kind == ChallengeKind.Vigil ? ChallengeRules.VigilRadius : ChallengeRules.OfferingRadius,
                    _kind == ChallengeKind.Vigil ? ChallengeProps.Wisp : ChallengeProps.Gold);
        }
        else if (_ringVisual != null) _ringVisual.transform.localScale = Vector3.one * shrink;

        // Targets / crates / sparks.
        if (_kind is ChallengeKind.Targets or ChallengeKind.Smash or ChallengeKind.Sparks && _props.Count > 0)
        {
            while (_propVisuals.Count < _props.Count)
            {
                int i = _propVisuals.Count;
                var c = _props[i];
                _propVisuals.Add(_kind == ChallengeKind.Targets ? TargetVisual.Create(new Vector3(c.x, c.y, c.z), _propDrift[i], c.w)
                    : _kind == ChallengeKind.Sparks ? SparkVisual.Create(new Vector3(c.x, c.y, c.z), c.w)
                    : CrateVisual.Create(new Vector3(c.x, c.y, c.z), c.w));
            }
            for (int i = 0; i < _propVisuals.Count && i < _propDone.Count; i++)
            {
                if (_propVisuals[i] == null) continue;
                // Leftovers when the challenge ends (won or lost) just go: no hit flash or shatter for them.
                if (finished && !_propDone[i]) { Destroy(_propVisuals[i].gameObject); continue; }
                if (_propVisuals[i] is TargetVisual t) t.SetHit(_propDone[i]);
                else if (_propVisuals[i] is CrateVisual c) c.SetBroken(_propDone[i]);
                else if (_propVisuals[i] is SparkVisual s) s.SetCollected(_propDone[i]);
            }
        }

        // Mushroom Path: build the mushrooms, play the sequence, flash steps, mistakes and cleared rounds.
        if (_kind == ChallengeKind.Mushrooms && _props.Count > 0)
        {
            while (_mushrooms.Count < _props.Count)
            {
                var c = _props[_mushrooms.Count];
                _mushrooms.Add(MushroomVisual.Create(new Vector3(c.x, c.y, c.z), (int)c.w));
            }
            foreach (var m in _mushrooms) if (m != null) m.transform.localScale = Vector3.one * shrink;
            if (active) MushroomLights();
        }

        // Capture the Flag: the flag once it is out, and its base ring while the challenge runs.
        if (_kind == ChallengeKind.Flag && _flagStage >= 1 && (active || finished))
        {
            if (_flagVisual == null) _flagVisual = FlagVisual.Create(_wisp);
            _flagVisual.MoveTo(FlagFoot());
            bool mine = _flagStage == 1 && LocalCarries();
            _flagVisual.transform.localScale = Vector3.one * shrink * (mine ? .7f : 1f);
        }
        UpdateFlagGuide(active);
        if (_kind == ChallengeKind.Flag && active && _flagStage >= 0)
        {
            if (_baseVisual == null)
            {
                _baseVisual = FloorRingVisual.Create(_home, ChallengeRules.FlagBaseRadius, ChallengeProps.Hunt);
                ChallengeProps.MapDot(_baseVisual.gameObject, ChallengeProps.Hunt);
            }
        }
        else if (_baseVisual != null) _baseVisual.transform.localScale = Vector3.one * shrink;

        // Hunt beacon over the marked monster.
        if (_kind == ChallengeKind.Hunt && active && _huntView != 0 && _beacon == null)
        {
            var view = PhotonView.Find(_huntView);
            var monster = view != null ? view.transform : Host && _huntTarget != null && _huntTarget.Enemy != null ? _huntTarget.Enemy.transform : null;
            if (monster != null) _beacon = HuntBeacon.Create(monster);
        }
        else if (!active && _beacon != null) { Destroy(_beacon.gameObject); _beacon = null; }

        // HUD line while it runs.
        float remaining = Host ? RemainingNow(Time.time) : Mathf.Max(0f, _endsAtLocal - Time.time);
        if (active) Localization.HudStatus.Show(StatusKey, StatusLine(remaining));
        else Localization.HudStatus.Clear(StatusKey);

    }

    private static string StatusLine(float remaining)
    {
        if (_kind == ChallengeKind.Flag)
        {
            string flag = ChallengeRules.FlagStatus(_flagStage, ChallengeRules.MathText(_qa, _qb, _qop), _carrierView != 0 || Host && _carrier != null ? 1 : 0, remaining, Russian);
            return _flagStage == 1 && _flagGuideMetres >= 0f ? $"{flag}  {(Russian ? "БАЗА" : "BASE")} {Mathf.RoundToInt(_flagGuideMetres)} {(Russian ? "М" : "M")}" : flag;
        }
        string line = ChallengeRules.Status(_kind, _progress, _total, remaining, Russian);
        if (_kind != ChallengeKind.Mushrooms) return line;
        string turn = MushroomShowing() ? (Russian ? "СМОТРИТЕ" : "WATCH")
            : (Russian ? "ШАГ " : "STEP ") + (_step + 1) + "/" + _seq.Count;
        return $"{line}  {turn}  {(Russian ? "ОШИБКИ" : "MISSES")} {_mistakes}/{ChallengeRules.MushroomMistakes}";
    }

    // The sequence lights each mushroom in turn (every client on the same shared clock); a step lights the one stepped
    // on, a mistake flashes them all red, a cleared round flashes them all.
    private static void MushroomLights()
    {
        float elapsed = MushroomElapsed();
        int slot = elapsed >= 0f && elapsed < _seq.Count * ChallengeRules.MushroomStep
            ? Mathf.FloorToInt(elapsed / ChallengeRules.MushroomStep) : -1;
        bool lit = slot >= 0 && elapsed - slot * ChallengeRules.MushroomStep < ChallengeRules.MushroomStep * .72f;
        for (int i = 0; i < _mushrooms.Count; i++)
            if (_mushrooms[i] != null) _mushrooms[i].Hold(lit && slot < _seq.Count && _seq[slot] == i);
        if (slot != _shownSlot)
        {
            _shownSlot = slot;
            if (slot >= 0 && slot < _seq.Count && _seq[slot] < _mushrooms.Count && _mushrooms[_seq[slot]] != null) _mushrooms[_seq[slot]].Lit(true);
        }
        if (_shownRound >= 0 && _progress > _shownRound)
        {
            foreach (var m in _mushrooms) if (m != null) m.Lit(false);
        }
        else if (_shownMistakes >= 0 && _mistakes > _shownMistakes)
        {
            foreach (var m in _mushrooms) if (m != null) m.Mistake();
            SemiFunc.UIFocusText(Russian ? "НЕ ТОТ ГРИБ! СМОТРИТЕ ЕЩЁ РАЗ" : "WRONG MUSHROOM! WATCH AGAIN", ChallengeProps.Lose, Color.white, 2.5f);
        }
        else if (_shownStep >= 0 && _step > _shownStep && _step - 1 < _seq.Count && _seq[_step - 1] < _mushrooms.Count && _mushrooms[_seq[_step - 1]] != null)
            _mushrooms[_seq[_step - 1]].Lit(true);
        _shownStep = _step; _shownMistakes = _mistakes; _shownRound = _progress;
    }

    private static bool LocalCarries()
    {
        if (Host) return _carrier != null && _carrier.isLocal;
        if (_carrierView == 0) return false;
        var view = PhotonView.Find(_carrierView);
        return view != null && view.IsMine;
    }

    // While the flag is out (stage 1): a glowing path on the floor from this player to the base, along the walkable
    // route, refreshed twice a second, and its length for the status line. Everyone sees their own route.
    private static void UpdateFlagGuide(bool active)
    {
        bool want = active && _kind == ChallengeKind.Flag && _flagStage == 1 && PlayerAvatar.instance != null && !PlayerAvatar.instance.isDisabled;
        if (!want)
        {
            if (_flagGuide != null) _flagGuide.enabled = false;
            _flagGuideMetres = -1f;
            return;
        }
        if (Time.time < _flagGuideAt) return;
        _flagGuideAt = Time.time + .5f;
        if (_flagGuide == null)
        {
            var go = new GameObject("IHM Flag Guide");
            _flagGuide = go.AddComponent<LineRenderer>();
            _flagGuide.useWorldSpace = true;
            _flagGuide.widthMultiplier = .14f;
            _flagGuide.numCornerVertices = 3;
            _flagGuide.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _flagGuide.receiveShadows = false;
            var shader = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color");
            if (shader != null) _flagGuide.material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            var c = ChallengeProps.Hunt;
            _flagGuide.startColor = new Color(c.r, c.g, c.b, .9f);
            _flagGuide.endColor = new Color(c.r, c.g, c.b, .35f);
        }
        var from = PlayerAvatar.instance.transform.position;
        var path = new NavMeshPath();
        if (!NavMesh.SamplePosition(from, out var a, 3f, NavMesh.AllAreas) || !NavMesh.SamplePosition(_home, out var b, 3f, NavMesh.AllAreas) ||
            !NavMesh.CalculatePath(a.position, b.position, NavMesh.AllAreas, path) || path.corners.Length < 2)
        {
            _flagGuide.enabled = false;
            _flagGuideMetres = Vector3.Distance(from, _home);
            return;
        }
        float metres = 0f;
        _flagGuide.positionCount = path.corners.Length;
        for (int i = 0; i < path.corners.Length; i++)
        {
            _flagGuide.SetPosition(i, path.corners[i] + Vector3.up * .12f);
            if (i > 0) metres += Vector3.Distance(path.corners[i - 1], path.corners[i]);
        }
        _flagGuide.enabled = true;
        _flagGuideMetres = metres;
    }

    // The flag's pole foot: over its carrier's head, or where it rests (on the shrine, where it fell, or in its base).
    private static Vector3 FlagFoot()
    {
        PlayerAvatar carrier = null;
        if (Host) carrier = _carrier;
        else if (_carrierView != 0) { var view = PhotonView.Find(_carrierView); carrier = view != null ? view.GetComponent<PlayerAvatar>() : null; }
        if (carrier != null && _flagStage == 1)
        {
            // Over the head and behind is invisible to the carrier in first person: they see it held at the lower right.
            var cam = carrier.isLocal ? Camera.main : null;
            if (cam != null) return cam.transform.position + cam.transform.right * .5f + cam.transform.forward * .85f - cam.transform.up * 1.45f;
            return carrier.transform.position + Vector3.up * 1.1f - carrier.transform.forward * .25f;
        }
        if (_flagStage == 2) return _home;
        bool onShrine = (_wisp - (_shrine + Vector3.up * 1.05f)).sqrMagnitude < .01f;
        return onShrine ? _shrine + Vector3.up * .9f : _wisp - Vector3.up * .1f; // standing on the shrine, or lying where it fell
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
                SemiFunc.UIFocusText(ChallengeRules.Name(_kind, Russian).ToUpperInvariant() + ": " +
                    (_kind == ChallengeKind.Flag ? ChallengeRules.MathText(_qa, _qb, _qop) + " = ? " : "") + ChallengeRules.Goal(_kind, _total, Russian), ChallengeProps.Gold, Color.white, 5f);
            else if (_phase == ChallengePhase.Won)
                SemiFunc.UIFocusText((Russian ? "ИСПЫТАНИЕ ПРОЙДЕНО: " : "CHALLENGE COMPLETE: ") + ChallengeRules.RewardText(_reward, Russian).ToUpperInvariant(), ChallengeProps.Win, Color.white, 4f);
            else if (_phase == ChallengePhase.Lost)
                SemiFunc.UIFocusText(Russian ? "ИСПЫТАНИЕ ПРОВАЛЕНО" : "CHALLENGE FAILED", ChallengeProps.Lose, Color.white, 3f);
            _shownPhase = _phase;
            _shownProgress = _progress;
        }
        // Capture the Flag: the answer is in, the flag is planted.
        if (_kind == ChallengeKind.Flag && _phase == ChallengePhase.Active && _flagStage != _shownStage)
        {
            if (_shownStage >= 0 && _flagStage == 1)
                SemiFunc.UIFocusText(Russian ? "ВЕРНО! ВОЗЬМИТЕ ФЛАГ У СВЯТИЛИЩА И НЕСИТЕ К КРАСНОМУ КРУГУ" : "CORRECT! TAKE THE FLAG FROM THE SHRINE TO THE RED RING", ChallengeProps.Win, Color.white, 4f);
            else if (_flagStage == 2)
                SemiFunc.UIFocusText(Russian ? "ФЛАГ УСТАНОВЛЕН! ПРОДЕРЖИТЕСЬ 2 МИНУТЫ, НИКТО НЕ ДОЛЖЕН УМЕРЕТЬ" : "FLAG PLANTED! HOLD FOR 2 MINUTES, NOBODY MAY DIE", ChallengeProps.Hunt, Color.white, 4f);
            _shownStage = _flagStage;
        }
        // Monster Waves: announce each new wave as the previous one falls.
        if (_kind == ChallengeKind.Waves && _phase == ChallengePhase.Active && _progress != _shownProgress)
        {
            _shownProgress = _progress;
            if (_progress > 0 && _progress < _total)
                SemiFunc.UIFocusText((Russian ? "ВОЛНА " : "WAVE ") + (_progress + 1) + "/" + _total, ChallengeProps.Hunt, Color.white, 3f);
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

    // Chat answers for Capture the Flag: in multiplayer every message reaches the host through ChatMessageSendRPC
    // (the game checks the sender owns that avatar); in singleplayer ChatMessageSend speaks it locally.
    [HarmonyPatch(typeof(PlayerAvatar), nameof(PlayerAvatar.ChatMessageSendRPC))]
    private static class ChatReceived
    {
        [HarmonyPostfix]
        private static void Postfix(PlayerAvatar __instance, string _message, PhotonMessageInfo _info)
        {
            if (!SemiFunc.IsMultiplayer() || !PhotonNetwork.IsMasterClient || __instance == null) return;
            if (__instance.photonView == null || _info.Sender == null || _info.Sender.ActorNumber != __instance.photonView.OwnerActorNr) return;
            OnChat(__instance, _message);
        }
    }

    [HarmonyPatch(typeof(PlayerAvatar), nameof(PlayerAvatar.ChatMessageSend))]
    private static class ChatSentAlone
    {
        [HarmonyPostfix]
        private static void Postfix(PlayerAvatar __instance, string _message)
        {
            if (SemiFunc.IsMultiplayer()) return;
            OnChat(__instance, _message);
        }
    }

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
