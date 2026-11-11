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
        if (E.HasRigidbody) E.Rigidbody.onGrabbed.AddListener(OnGrabbedEvent);
        if (E.HasStateDespawn) E.StateDespawn.ChaseDespawn = false; // a long fight must not despawn it mid-attack
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
        if (!IsHost || E.IsStunned()) return;
        var seen = E.Vision.onVisionTriggeredPlayer;
        if (seen != null && !seen.isDisabled) OnSeePlayer(seen);
    }

    private void OnInvestigateEvent()
    {
        if (IsHost) OnNoise(E.StateInvestigate.onInvestigateTriggeredPosition);
    }

    private void OnGrabbedEvent()
    {
        if (IsHost) OnGrabbed(E.Rigidbody.onGrabbedPlayerAvatar);
    }

    private void OnHurtEvent()
    {
        MonsterAudio.Play(MonsterAudio.Cue.Hurt, BodyPosition, .6f);
        if (IsHost) OnHurt();
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
    protected virtual void OnGrabbed(PlayerAvatar player) { }
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
        HostTick(Time.deltaTime);
        if (E.CurrentState != EnemyState.Stunned)
            E.CurrentState = Target != null ? EnemyState.Chase : EnemyState.Roaming;
        Sync();
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

    protected void SetState(byte state)
    {
        if (state == State) return;
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
        var from = Eye;
        var to = Chest(p) - from;
        if (to.magnitude > range) return false;
        foreach (var hit in Physics.RaycastAll(from, to.normalized, to.magnitude, SemiFunc.LayerMaskGetVisionObstruct(), QueryTriggerInteraction.Ignore))
        {
            if (hit.collider.GetComponentInParent<PlayerAvatar>() == p || hit.collider.GetComponentInParent<PlayerTumble>() != null) continue;
            if (hit.collider.transform.IsChildOf(Parent.transform)) continue;
            if (hit.collider.GetComponentInParent<PhysGrabObject>() != null && hit.collider.attachedRigidbody != null &&
                hit.collider.attachedRigidbody.mass < 5f) continue; // small loose items do not block sight
            return false;
        }
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
