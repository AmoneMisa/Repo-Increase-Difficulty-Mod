using System.Collections;
using System.Collections.Generic;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.AI;

namespace IncreaseHardMod.Monsters;

/// <summary>How the visual should pose; each brain maps its own states onto these.</summary>
internal enum MonsterPose : byte { Idle, Move, Prepare, Attack, Special, Hold, Stunned, Vulnerable, Flee }

/// <summary>Which animation the visual plays: finer than <see cref="MonsterPose"/> where two states share a pose
/// (the witch's two windups, the naga's two combo hits). View only - it has no meaning for the AI.</summary>
internal enum MonsterAnim : byte
{
    None, FirePrep, FireCast, ZapPrep, Zap, Rattle, BitePrep, Lunge, Coil, Acquire, Roar, Charge, Stare, Daze,
    Combo1, Combo2, BleedCut, StoneGaze, Exposed, Eat, Land,
    FairyLift, FairyCarry, FairyGiggle, TwinsLure, TwinsRush, TwinsReach, TwinsSwing, TwinsThrow,
}

/// <summary>
/// Shared AI frame of the new monsters. Runs on the Enemy object of a cloned native enemy.
///
/// HOST: the state machine (subclass <see cref="HostTick"/>), all targeting, damage and control. It reacts to the
/// native enemy events (spawn, vision, investigate, hurt, death, grab) that the donor's AI used to handle, honours
/// stuns, despawn requests and the per-room attack limit, and keeps EnemyParent's despawn/soul flow.
/// CLIENTS: receive (state, target, extra) snapshots on <see cref="StateCode"/> - sent on every change and once a
/// second for late joiners - and only animate.
/// </summary>
internal abstract class MonsterBrain : MonoBehaviour
{
    internal const byte StateCode = 154, FxCode = 153;
    internal static readonly List<MonsterBrain> All = new();

    // Serialized: the prefab's value must survive Instantiate.
    [SerializeField] internal MonsterKind Kind;
    internal byte State;
    internal int TargetId;
    internal float Extra;
    internal float StateTime;

    protected Enemy E;
    protected EnemyParent Parent;
    protected PlayerAvatar Target;
    protected Vector3 Destination;
    protected float Hover;
    private float _nextSync, _roamTimer;
    private bool _dead;
    private byte _sentState = 255;
    private int _sentTarget = -1;
    private float _sentExtra = float.NaN;

    protected static bool IsHost => SemiFunc.IsMasterClientOrSingleplayer();
    internal bool Dead => _dead;
    internal Enemy Enemy => E;
    internal Vector3 BodyPosition => E != null && E.HasRigidbody ? E.Rigidbody.rb.position : transform.position;
    internal abstract MonsterPose Pose { get; }
    internal virtual MonsterAnim Anim => MonsterAnim.None;
    /// <summary>
    /// Whether a player holds the monster right now, on every client: the game itself syncs who grabs a physics
    /// object (PhysGrabObject.playerGrabbing), so the struggle animation needs no network message of its own.
    /// </summary>
    internal bool Held => E != null && E.HasRigidbody && E.Rigidbody.physGrabObject != null &&
                          E.Rigidbody.physGrabObject.playerGrabbing.Count > 0;
    /// <summary>A point the monster is magically tied to right now (the thing the fairy carries), on every client.</summary>
    internal virtual bool Tether(out Vector3 point) { point = default; return false; }
    /// <summary>This monster's body on this client (set by the visual itself).</summary>
    internal MonsterVisual Visual;

    protected virtual void Awake()
    {
        E = GetComponent<Enemy>();
        Parent = GetComponentInParent<EnemyParent>();
        Hover = MonsterRegistry.Of(Kind).Hover;
    }

    protected virtual void Start()
    {
        Hover = MonsterRegistry.Of(Kind).Hover;
        if (E == null) return;
        if (E.HasHealth)
        {
            E.Health.onHurt.AddListener(OnHurtEvent);
            E.Health.onDeath.AddListener(OnDeathEvent);
        }
        if (E.HasStateSpawn) E.StateSpawn.OnSpawn.AddListener(OnSpawnEvent);
        if (E.HasVision) E.Vision.onVisionTriggered.AddListener(OnVisionEvent);
        if (E.HasStateInvestigate) E.StateInvestigate.onInvestigateTriggered.AddListener(OnInvestigateEvent);
        if (E.HasRigidbody)
        {
            E.Rigidbody.onGrabbed.AddListener(OnGrabbedEvent);
            // Like native monsters (Hunter, Runner, Shadow): bumping into it, or hitting it with a held object,
            // turns it on that player.
            E.Rigidbody.onTouchPlayer?.AddListener(OnTouchEvent);
            E.Rigidbody.onTouchPlayerGrabbedObject?.AddListener(OnTouchObjectEvent);
        }
        WireImpacts();
        if (E.HasStateDespawn) E.StateDespawn.ChaseDespawn = false; // a long fight must not despawn it mid-attack
    }

    // In native prefabs the impact detector reaches EnemyRigidbody.Light/Medium/HeavyImpact (camera shake and
    // EnemyHealth impact damage) only through persistent UnityEvent listeners. MonsterRegistry.Strip replaces
    // every UnityEvent on the clone, which cut that link: thrown or slammed monsters took no damage at all.
    // Re-add it at runtime on every client, like the prefab did (EnemyHealth.Hurt itself is host-only).
    private void WireImpacts()
    {
        if (!E.HasRigidbody) return;
        var body = E.Rigidbody;
        var detector = body.impactDetector != null ? body.impactDetector : body.GetComponent<PhysGrabObjectImpactDetector>();
        if (detector == null) return;
        detector.onImpactLight ??= new UnityEngine.Events.UnityEvent();
        detector.onImpactMedium ??= new UnityEngine.Events.UnityEvent();
        detector.onImpactHeavy ??= new UnityEngine.Events.UnityEvent();
        detector.onImpactLight.AddListener(body.LightImpact);
        detector.onImpactMedium.AddListener(body.MediumImpact);
        detector.onImpactHeavy.AddListener(body.HeavyImpact);
    }

    private void OnEnable() { if (!All.Contains(this)) All.Add(this); }
    private void OnDisable() { All.Remove(this); }

    // ------------------------------------------------------------------ native events

    private void OnSpawnEvent()
    {
        _dead = false;
        StateTime = 0f;
        if (IsHost && SemiFunc.EnemySpawn(E))
        {
            Target = null;
            OnSpawned();
            SetState(0);
        }
    }

    private void OnVisionEvent()
    {
        if (!IsHost || E.IsStunned() || _leaving) return;
        var seen = E.Vision.onVisionTriggeredPlayer;
        if (seen != null && !seen.isDisabled) OnSeePlayer(seen);
    }

    private void OnInvestigateEvent()
    {
        if (IsHost) OnNoise(E.StateInvestigate.onInvestigateTriggeredPosition);
    }

    private void OnGrabbedEvent()
    {
        if (!IsHost) return;
        _leaving = false;
        OnGrabbed(E.Rigidbody.onGrabbedPlayerAvatar);
    }

    private void OnTouchEvent()
    {
        if (IsHost && Settled) { _leaving = false; OnProvoked(E.Rigidbody.onTouchPlayerAvatar); }
    }

    private void OnTouchObjectEvent()
    {
        if (IsHost && Settled) { _leaving = false; OnProvoked(E.Rigidbody.onTouchPlayerGrabbedObjectAvatar); }
    }

    /// <summary>The native guard: no reactions in the 1.5 s after a stun ends (it is still getting up).</summary>
    protected bool Settled => !E.IsStunned() && E.HasRigidbody && E.Rigidbody.timeSinceStun > 1.5f;

    /// <summary>
    /// Host: a player grabbed it, bumped into it or hit it with something they hold. Monsters turn on that player
    /// (the native Loom / Hunter behaviour); each brain says what "turning on" means for it. Called every frame
    /// while the grab lasts, so it must be idempotent.
    /// </summary>
    protected virtual void OnProvoked(PlayerAvatar player) { }

    // Runs on every client (HurtRPC raises onHurt everywhere): the red flash is drawn by our own visual, because
    // the native flash needs a _ColorOverlay shader property our painted materials may not have.
    private void OnHurtEvent()
    {
        MonsterAudio.Play(MonsterAudio.Cue.Hurt, BodyPosition, .6f);
        if (Visual != null) Visual.Flash();
        if (!IsHost) return;
        _leaving = false;
        OnHurt();
        // Shot or hit while it had nobody to chase: it turns on the nearest player it can see (the attacker is not
        // known; the nearest one in sight is the usual culprit).
        if (Target == null && !_dead) OnProvoked(Notice(20f, force: true));
    }

    private void OnDeathEvent()
    {
        if (_dead) return;
        _dead = true;
        MonsterAudio.Play(MonsterAudio.Cue.Death, BodyPosition);
        GetComponentInParent<EnemyParent>()?.GetComponentInChildren<MonsterVisual>(true)?.Burst();
        if (!IsHost) return;
        MonsterAfflictions.ReleaseFrom(E);
        var spawn = E.CustomValuableSpawnTransform != null ? E.CustomValuableSpawnTransform : E.CenterTransform;
        if (Economy.ExtendedMoons.AllowsSoul(E.Health))
            MonsterRegistry.ExpectSoul(spawn != null ? spawn.position : BodyPosition, SoulValue());
        OnDied();
        Parent.Despawn();
    }

    // ------------------------------------------------------------------ hooks

    protected virtual void OnSpawned() { }
    protected virtual void OnSeePlayer(PlayerAvatar player) { }
    protected virtual void OnNoise(Vector3 position) { }
    protected virtual void OnGrabbed(PlayerAvatar player) { if (Settled || E.IsStunned()) OnProvoked(player); }
    protected virtual void OnHurt() { }
    protected virtual void OnDied() { }
    protected virtual void OnStunned() { }
    protected virtual int SoulValue() => MonsterRules.SoulValue(Kind, Random.value, MonsterRegistry.TeamSoulHunter());
    protected abstract void HostTick(float dt);

    // ------------------------------------------------------------------ frame

    protected virtual void Update()
    {
        if (E == null || Parent == null) return;
        StateTime += Time.deltaTime;
        GuardGrab();
        if (!IsHost || !Parent.Spawned || _dead) return;
        HoldHurts();
        if (E.CurrentState == EnemyState.Despawn)
        {
            MonsterAfflictions.ReleaseFrom(E);
            if (E.HasStateDespawn) E.StateDespawn.Despawn(); else Parent.Despawn();
            return;
        }
        if (E.CurrentState == EnemyState.Spawn) return;
        if (Hover > 0f && E.HasRigidbody && E.Rigidbody.followTarget != null)
        {
            bool fall = E.IsStunned() || HoverHeight() <= 0f;
            E.Rigidbody.gravity = fall;
            if (!fall) E.Rigidbody.followTarget.position = E.transform.position + Vector3.up * HoverHeight();
        }
        if (E.IsStunned()) { OnStunned(); Sync(); return; }
        if (Target != null && (Target.isDisabled || Target.deadSet)) Target = null;
        if (TickLeave(Time.deltaTime)) { UpdateAttackStatus(); Sync(); return; }
        HostTick(Time.deltaTime);
        UpdateAttackStatus();
        Sync();
    }

    /// <summary>
    /// The native enemy state the game and other systems read (chase music, Hunted Warning, monster HUDs, pings):
    /// ChaseBegin while it winds an attack up, Chase while it attacks or holds someone, ChaseSlow while it hunts a
    /// target, Roaming otherwise. The current target is published the native way (TargetPlayerAvatar / ViewID).
    /// </summary>
    private void UpdateAttackStatus()
    {
        if (E.CurrentState is EnemyState.Stunned or EnemyState.Spawn or EnemyState.Despawn) return;
        var pose = Pose;
        bool attacking = pose is MonsterPose.Attack or MonsterPose.Special or MonsterPose.Hold;
        bool winding = pose == MonsterPose.Prepare;
        if (Target != null && Target.photonView != null)
        {
            E.TargetPlayerAvatar = Target;
            E.TargetPlayerViewID = Target.photonView.ViewID;
        }
        E.CurrentState = Target == null && !attacking ? EnemyState.Roaming
            : winding ? EnemyState.ChaseBegin : attacking ? EnemyState.Chase : EnemyState.ChaseSlow;
        if (attacking || winding) E.SetChaseTimer();
    }

    private float _heldHurtUntil, _lastHeldAt = -100f, _heldSpeed;

    // Native rule: impacts hurt a monster only while it is stunned or a "non-stun hurt" window is open. Keep that
    // window open while players hold it and briefly after they let go, so slamming it into the floor or a wall
    // and throwing it deal the impact damage set on the prefab (MonsterRules.ImpactDamage).
    private void HoldHurts()
    {
        if (!E.HasRigidbody || !E.HasHealth || E.Rigidbody.physGrabObject == null) return;
        // Impacts hurt only while it is swung or thrown (fast), not while it simply hangs in someone's grip.
        var pgo = E.Rigidbody.physGrabObject;
        bool held = pgo.playerGrabbing.Count > 0;
        float speed = pgo.rb != null ? pgo.rb.linearVelocity.magnitude : 0f;
        // Smoothed speed in the hands (one-frame physics spikes from the body fighting the grab do not count).
        if (held) { _lastHeldAt = Time.time; _heldSpeed = Mathf.Lerp(_heldSpeed, speed, Mathf.Clamp01(Time.deltaTime / .12f)); }
        if (MonsterRules.HoldImpactArms(held, Time.time - _lastHeldAt, Mathf.Min(speed, _heldSpeed + 1f))) _heldHurtUntil = Time.time + MonsterRules.ThrownHurtSeconds;
        if (held) { _heldHurtUntil = 0f; return; } // never while it is simply in someone's hands
        if (Time.time < _heldHurtUntil) E.Health.NonStunHurtOverride(.25f);
    }

    /// <summary>
    /// Host: where a player is looking. The local player uses their own camera; remote players use the camera
    /// state the game itself syncs to the host (PlayerAvatar.clientCameraPosition / Rotation). Without valid
    /// data it returns false, which the callers treat as "not looking" (never a reason to hurt the player).
    /// </summary>
    protected static bool View(PlayerAvatar p, out Vector3 eye, out Vector3 forward)
    {
        eye = default; forward = default;
        if (p == null) return false;
        bool local = !SemiFunc.IsMultiplayer() || (p.photonView != null && p.photonView.IsMine);
        if (local)
        {
            var cam = Camera.main != null ? Camera.main.transform : null;
            if (cam == null) return false;
            eye = cam.position; forward = cam.forward;
            return true;
        }
        if (!p.clientCameraStateValid) return false;
        eye = p.clientCameraPosition;
        forward = p.clientCameraRotation * Vector3.forward;
        return !float.IsNaN(forward.x) && forward.sqrMagnitude > .5f;
    }

    /// <summary>
    /// Host: whether a player is inside this monster's reach - horizontal range and the height window of its
    /// body (MonsterRules.Reach), measured feet to feet, plus line of sight so nothing is hit through a wall.
    /// </summary>
    protected bool InReach(PlayerAvatar p, float range = -1f)
    {
        if (p == null || p.isDisabled || p.deadSet) return false;
        if (range < 0f) MonsterRules.Reach(Kind, out range, out _, out _);
        var feet = E.transform.position;
        var d = p.transform.position - feet;
        return MonsterRules.InReach(Kind, d.x, d.y, d.z, range) && Sees(p, range + 2f);
    }

    /// <summary>Whether players may grab the monster right now (checked on every client).</summary>
    protected virtual bool GrabAllowed() => true;

    // The grabber's own client refuses a target whose grab is disabled, so every client keeps it disabled while
    // not allowed; the host also lets go of a grab that started before the condition ended.
    private void GuardGrab()
    {
        if (E == null || !E.HasRigidbody || E.Rigidbody.physGrabObject == null || _dead || GrabAllowed()) return;
        var pgo = E.Rigidbody.physGrabObject;
        pgo.OverrideGrabDisable(.25f);
        if (IsHost && pgo.playerGrabbing.Count > 0) E.Rigidbody.GrabRelease();
    }

    /// <summary>Flying height above the agent; 0 = on the ground (falls when stunned).</summary>
    protected virtual float HoverHeight() => Hover;

    /// <summary>
    /// Seconds the current target has been out of sight. Reset on every state change: a stale value from an earlier
    /// hunt made the witch drop a player the moment she spotted them again.
    /// </summary>
    protected float LostSight;

    protected void SetState(byte state)
    {
        if (state == State) return;
        LostSight = 0f;
        // Diagnostics for the witch's attack cycle (a playtest reported she never casts).
        if (Kind == MonsterKind.Witch)
            Plugin.Log.LogInfo($"[Monsters] Witch state {State} -> {state}, target {(Target != null ? Target.playerName : "none")}" +
                               (Target != null ? $" at {DistanceTo(Target):0.0} m, sees={Sees(Target, 18f)}" : ""));
        State = state;
        StateTime = 0f;
        Sync();
    }

    // ------------------------------------------------------------------ network

    private void Sync()
    {
        if (!IsHost) return;
        TargetId = Target != null && Target.photonView != null ? Target.photonView.ViewID : 0;
        bool changed = State != _sentState || TargetId != _sentTarget || !Mathf.Approximately(Extra, _sentExtra);
        if (!changed && Time.time < _nextSync) return;
        _nextSync = Time.time + 1f;
        _sentState = State; _sentTarget = TargetId; _sentExtra = Extra;
        if (!SemiFunc.IsMultiplayer() || E.PhotonView == null || !PhotonNetwork.InRoom) return;
        PhotonNetwork.RaiseEvent(StateCode, new object[] { E.PhotonView.ViewID, (byte)Kind, State, TargetId, Extra, MonsterAfflictions.LevelKey() },
            new RaiseEventOptions { Receivers = ReceiverGroup.Others }, SendOptions.SendUnreliable);
    }

    internal static void OnStateEvent(EventData message)
    {
        if (!PhotonNetwork.InRoom || PhotonNetwork.MasterClient == null || message.Sender != PhotonNetwork.MasterClient.ActorNumber ||
            !MonsterPackets.ReadState(message.CustomData, MonsterAfflictions.LevelKey(), out int id, out byte kind, out byte state, out int target, out float extra)) return;
        var view = PhotonView.Find(id);
        var brain = view != null ? view.GetComponent<MonsterBrain>() : null;
        if (brain == null || (byte)brain.Kind != kind) return;
        if (brain.State != state) { brain.State = state; brain.StateTime = 0f; brain.OnClientState(state); }
        brain.TargetId = target;
        brain.Extra = extra;
    }

    /// <summary>Clients: a new state arrived (warning sounds and effects that every client plays).</summary>
    protected virtual void OnClientState(byte state) => PlayStateCue(state);

    /// <summary>Warning cue for a state; the host calls it from <see cref="Announce"/>, clients on receipt.</summary>
    protected virtual void PlayStateCue(byte state) { }

    /// <summary>Host: enter a state and play its warning locally (clients play it on the snapshot).</summary>
    protected void Announce(byte state) { SetState(state); PlayStateCue(state); }

    internal PlayerAvatar TargetAvatar()
    {
        if (TargetId == 0) return null;
        var view = PhotonView.Find(TargetId);
        return view != null ? view.GetComponent<PlayerAvatar>() : null;
    }

    // ------------------------------------------------------------------ helpers (host)

    protected Vector3 Eye => E.HasVision && E.Vision.VisionTransform != null ? E.Vision.VisionTransform.position : BodyPosition + Vector3.up * .5f;

    protected static Vector3 Chest(PlayerAvatar p) =>
        p.PlayerVisionTarget != null && p.PlayerVisionTarget.VisionTransform != null ? p.PlayerVisionTarget.VisionTransform.position : p.transform.position + Vector3.up;

    /// <summary>Line of sight from the monster's eyes to a player's chest, walls and doors blocking.</summary>
    protected bool Sees(PlayerAvatar p, float range)
    {
        if (p == null || p.isDisabled) return false;
        // The donor's vision point may sit far from our body (on the floor under a flying witch, inside a long bull):
        // a clear line from either it or the body's head counts.
        return SeesFrom(Eye, p, range) || SeesFrom(BodyPosition + Vector3.up * .5f, p, range);
    }

    private bool SeesFrom(Vector3 from, PlayerAvatar p, float range)
    {
        var to = Chest(p) - from;
        if (to.magnitude > range) return false;
        if (Gear.ModGrenades.Hidden(from, Chest(p))) return false; // smoke grenade cloud
        foreach (var hit in Physics.RaycastAll(from, to.normalized, to.magnitude, SemiFunc.LayerMaskGetVisionObstruct(), QueryTriggerInteraction.Ignore))
        {
            // The last stretch of the ray is inside the player's own body: the local player's collision capsule
            // lives on PlayerController, not under PlayerAvatar, and used to hide the host from every monster.
            if (hit.distance >= to.magnitude - .6f) continue;
            if (hit.collider.GetComponentInParent<PlayerAvatar>() == p || hit.collider.GetComponentInParent<PlayerTumble>() != null) continue;
            if (hit.collider.transform.IsChildOf(Parent.transform)) continue;
            if (hit.collider.GetComponentInParent<PhysGrabObject>() != null && hit.collider.attachedRigidbody != null &&
                hit.collider.attachedRigidbody.mass < 5f) continue; // small loose items do not block sight
            return false;
        }
        return true;
    }

    private float _noticeAt;

    /// <summary>
    /// Host: a player it can see right next to it (or anyone in sight after it was hit) - for monsters whose native
    /// vision cone can miss someone standing at their flank or shooting them. Checked at most every 0.4 s.
    /// </summary>
    protected PlayerAvatar Notice(float range, bool force = false)
    {
        if (force) return NearestVisible(range); // it was hit: whoever it can see, wherever it faces
        if (_leaving || Time.time < _noticeAt) return null;
        _noticeAt = Time.time + .4f;
        return NearestSpotted(range);
    }

    private readonly Dictionary<PlayerAvatar, float> _watchedSince = new();

    /// <summary>
    /// Host: the nearest player it actually notices, by the native vision rules (EnemyVision): anyone right next to it
    /// (3.5 m standing, 2 m crouched or crawling), otherwise only inside its view cone - narrower for a crouching
    /// player and narrower still for a crawling one - and a crouching / crawling player only after it has watched them
    /// for 0.5 / 1 s. Line of sight as Sees. Used to pick up a target; keeping one uses Sees.
    /// </summary>
    protected PlayerAvatar NearestSpotted(float range)
    {
        if (_leaving) return null;
        PlayerAvatar best = null; float bestD = float.MaxValue;
        var forward = Flat(E.transform.forward).normalized;
        foreach (var p in SemiFunc.PlayerGetList())
        {
            if (p == null) continue;
            if (p.isDisabled || p.deadSet || !Sees(p, range)) { _watchedSince.Remove(p); continue; }
            var to = Chest(p) - Eye;
            var flatTo = Flat(to);
            float dot = forward.sqrMagnitude > .01f && flatTo.sqrMagnitude > .0001f ? Vector3.Dot(forward, flatTo.normalized) : 1f;
            bool crawling = p.isCrawling && !p.isTumbling, crouching = (p.isCrouching || p.isTumbling) && !crawling;
            if (MonsterRules.InViewCone(dot, crouching, crawling)) { if (!_watchedSince.ContainsKey(p)) _watchedSince[p] = Time.time; }
            else _watchedSince.Remove(p);
            float watched = _watchedSince.TryGetValue(p, out var since) ? Time.time - since : -1f;
            if (!MonsterRules.SpotsPlayer(to.magnitude, dot, crouching, crawling, watched)) continue;
            float d = Vector3.Distance(p.transform.position, BodyPosition);
            if (d < bestD) { best = p; bestD = d; }
        }
        return best;
    }

    // ------------------------------------------------------------------ leaving (native forceLeave)

    private bool _leaving;
    private Vector3 _leaveTo;
    private float _leaveUntil, _leaveStuck;

    /// <summary>
    /// Host: the game's director tells every monster to leave once monsters have pressed the players' rooms for
    /// long enough (EnemyParent.forceLeave, which Hunter and Runner obey). Out of an attack, ours drop their target
    /// and walk 25-50 m away from the players, ignoring them on the way unless they are hit, grabbed or touched.
    /// True while it is leaving (the brain's own logic is paused).
    /// </summary>
    private bool TickLeave(float dt)
    {
        if (!_leaving)
        {
            if (Kind == MonsterKind.Pig || Pose != MonsterPose.Move || !SemiFunc.EnemyForceLeave(E)) return false;
            if (!Parent.playerClose) return false; // nobody near: nothing to leave
            var point = SemiFunc.LevelPointGetPlayerDistance(BodyPosition, 25f, 50f) ?? SemiFunc.LevelPointGetFurthestFromPlayer(BodyPosition, 5f);
            if (point == null || !NavMesh.SamplePosition(point.transform.position, out var hit, 5f, NavMesh.AllAreas)) return false;
            _leaveTo = hit.position;
            _leaving = true;
            _leaveUntil = Time.time + MonsterRules.LeaveMaxSeconds;
            _leaveStuck = 0f;
            Target = null;
            SetState(0);
            Plugin.Log.LogInfo($"[Monsters] {Kind} leaves the players' area ({Vector3.Distance(BodyPosition, _leaveTo):0} m).");
        }
        Move(_leaveTo, MonsterRegistry.Of(Kind).Speed * 1.15f);
        if (E.HasRigidbody && E.Rigidbody.notMovingTimer > 2f) _leaveStuck += dt;
        if (Vector3.Distance(Flat(E.transform.position), Flat(_leaveTo)) < 1.5f || _leaveStuck > 5f || Time.time > _leaveUntil)
            _leaving = false;
        return true;
    }

    protected PlayerAvatar NearestVisible(float range)
    {
        PlayerAvatar best = null; float bestD = float.MaxValue;
        foreach (var p in SemiFunc.PlayerGetList())
        {
            if (p == null || p.isDisabled || p.deadSet) continue;
            float d = Vector3.Distance(p.transform.position, BodyPosition);
            if (d < bestD && Sees(p, range)) { best = p; bestD = d; }
        }
        return best;
    }

    protected float DistanceTo(PlayerAvatar p) => p == null ? float.MaxValue : Vector3.Distance(Flat(p.transform.position), Flat(BodyPosition));
    protected static Vector3 Flat(Vector3 v) => new(v.x, 0f, v.z);

    /// <summary>The room attack limit (max four attackers, per-type caps) applies to every attack.</summary>
    protected bool MayAttack(PlayerAvatar p) => p != null && Patches.RoomAttackLimit.CanAttack(E, p);

    protected void Move(Vector3 destination, float speed, float acceleration = 12f)
    {
        if (Kind == MonsterKind.Basilisk && BasiliskCrawl.TryMove(E, destination, speed)) return;
        if (!E.HasNavMeshAgent) return;
        E.NavMeshAgent.SetDestination(destination);
        E.NavMeshAgent.OverrideAgent(speed, acceleration, .25f);
    }

    protected void Halt()
    {
        if (!E.HasNavMeshAgent) return;
        E.NavMeshAgent.ResetPath();
        E.NavMeshAgent.Stop(.15f);
    }

    protected void Face(Vector3 point, float speed = 8f)
    {
        var dir = Flat(point - E.transform.position);
        if (dir.sqrMagnitude < .001f) return;
        E.transform.rotation = Quaternion.Slerp(E.transform.rotation, Quaternion.LookRotation(dir), speed * Time.deltaTime);
    }

    protected void Roam(float speed)
    {
        _roamTimer -= Time.deltaTime;
        if (_roamTimer <= 0f || Vector3.Distance(Flat(E.transform.position), Flat(Destination)) < 1f ||
            (E.HasRigidbody && E.Rigidbody.notMovingTimer > 2f))
        {
            _roamTimer = Random.Range(6f, 12f);
            bool leave = SemiFunc.EnemyForceLeave(E);
            Vector3 point;
            bool found = leave ? SemiFunc.EnemyLeavePoint(E, out point) : SemiFunc.EnemyRoamPoint(E, out point);
            Destination = found ? point : E.transform.position;
        }
        if (SemiFunc.EnemySpawnIdlePause()) { Halt(); return; }
        Move(Destination, speed);
    }

    /// <summary>A navmesh point about <paramref name="distance"/> away from <paramref name="from"/>.</summary>
    protected Vector3 AwayFrom(Vector3 from, float distance)
    {
        var dir = Flat(BodyPosition - from);
        if (dir.sqrMagnitude < .01f) dir = Random.insideUnitSphere;
        dir.y = 0f;
        var wanted = E.transform.position + dir.normalized * distance;
        return NavMesh.SamplePosition(wanted, out var hit, 3f, NavMesh.AllAreas) ? hit.position : E.transform.position;
    }

    /// <summary>Knocks a player aside with the game's own tumble (host requests it; the owner simulates it).</summary>
    protected void Knock(PlayerAvatar p, Vector3 direction, float force)
    {
        if (p == null || p.tumble == null) return;
        p.tumble.TumbleRequest(true, false);
        p.tumble.TumbleOverrideTime(1.2f);
        StartCoroutine(KnockLater(p, direction.normalized * force));
    }

    private static IEnumerator KnockLater(PlayerAvatar p, Vector3 force)
    {
        yield return new WaitForSeconds(.12f);
        if (p != null && p.tumble != null) p.tumble.TumbleForce(force);
    }

    /// <summary>Host: a one-off effect every client plays (projectile, impact, beam).</summary>
    protected void Fx(byte fx, Vector3 a, Vector3 b)
    {
        MonsterFx.Play(this, fx, a, b);
        if (!SemiFunc.IsMultiplayer() || E.PhotonView == null || !PhotonNetwork.InRoom) return;
        PhotonNetwork.RaiseEvent(FxCode, new object[] { E.PhotonView.ViewID, fx, a, b, MonsterAfflictions.LevelKey() },
            new RaiseEventOptions { Receivers = ReceiverGroup.Others }, SendOptions.SendReliable);
    }

    internal static void OnFxEvent(EventData message)
    {
        if (!PhotonNetwork.InRoom || PhotonNetwork.MasterClient == null || message.Sender != PhotonNetwork.MasterClient.ActorNumber ||
            message.CustomData is not object[] p || p.Length != 5 || p[0] is not int id || p[1] is not byte fx ||
            p[2] is not Vector3 a || p[3] is not Vector3 b || p[4] is not string level || level != MonsterAfflictions.LevelKey()) return;
        if (!Valid(a) || !Valid(b)) return;
        var view = PhotonView.Find(id);
        var brain = view != null ? view.GetComponent<MonsterBrain>() : null;
        if (brain != null) MonsterFx.Play(brain, fx, a, b);
    }

    private static bool Valid(Vector3 v) => !float.IsNaN(v.x) && !float.IsNaN(v.y) && !float.IsNaN(v.z) &&
                                             Mathf.Abs(v.x) < 1e5f && Mathf.Abs(v.y) < 1e5f && Mathf.Abs(v.z) < 1e5f;
}
