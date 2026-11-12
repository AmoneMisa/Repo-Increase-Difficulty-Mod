using System.Collections.Generic;
using UnityEngine;

namespace IncreaseHardMod.Monsters;

/// <summary>
/// Hansel and Gretel (Tier II): twin children who never let go of each other's hand - one monster, one health pool.
/// They trail a player, stop and stare with a giggle for 1 s, then rush (5.2 m/s, up to 2.5 s). In reach they stretch
/// out their free hands for 0.7 s; a player still within 1.8 m and in sight is seized, held between them and
/// swung for 1.2 s, then thrown (see Hurl for where). A hit or a stun during the reach or the hold makes them let go.
/// Balance pass: slower and readable - 1.6 s giggling stare, 3.8 m/s rush, 1 s reach, 1.5 m grab range drawn as a ring on
/// the floor around them, 12 s between grabs (6 s after a miss), 150 HP.
/// </summary>
internal sealed class TwinsBrain : MonsterBrain
{
    private const byte Wander = 0, Stalk = 1, Lure = 2, Rush = 3, Reach = 4, Swing = 5, Throw = 6, Skip = 7;
    private PlayerAvatar _victim;
    private float _grabReady;

    internal override MonsterPose Pose => State switch
    {
        Lure or Reach => MonsterPose.Prepare,
        Rush => MonsterPose.Attack,
        Swing => MonsterPose.Hold,
        Throw => MonsterPose.Special,
        Skip => MonsterPose.Flee,
        _ => MonsterPose.Move,
    };

    internal override MonsterAnim Anim => State switch
    {
        Lure => MonsterAnim.TwinsLure,
        Rush => MonsterAnim.TwinsRush,
        Reach => MonsterAnim.TwinsReach,
        Swing => MonsterAnim.TwinsSwing,
        Throw => MonsterAnim.TwinsThrow,
        _ => MonsterAnim.None,
    };

    protected override void OnSpawned() { _victim = null; _grabReady = Time.time + 3f; }

    protected override void OnSeePlayer(PlayerAvatar player)
    {
        if (State == Wander) { Target = player; SetState(Stalk); }
    }

    protected override void OnNoise(Vector3 position)
    {
        if (State == Wander) Destination = position;
    }

    protected override void OnProvoked(PlayerAvatar player)
    {
        if (player == null || State != Wander) return;
        Target = player; SetState(Stalk);
    }

    protected override void OnHurt()
    {
        // A teammate's hit breaks the reach or the hold.
        if (State is not (Reach or Swing)) return;
        LetGo();
        _grabReady = Time.time + Economy.ExtendedMoons.Cooldown(MonsterRules.TwinsMissCooldown);
        Announce(Skip);
    }

    protected override void OnStunned() { LetGo(); if (State is Lure or Rush or Reach or Swing or Throw) Announce(Skip); }
    protected override void OnDied() => LetGo();

    private void LetGo()
    {
        if (_victim != null) MonsterAfflictions.End(_victim, Affliction.Seized);
        _victim = null;
    }

    protected override void PlayStateCue(byte state)
    {
        if (state == Lure) MonsterAudio.Play(MonsterAudio.Cue.Giggle, BodyPosition, 1.1f);
        else if (state == Rush) { MonsterAudio.Play(MonsterAudio.Cue.Giggle, BodyPosition, .9f, 1.3f); MonsterAudio.Play(MonsterAudio.Cue.Whoosh, BodyPosition, .6f, 1.3f); }
        else if (state == Reach) { MonsterAudio.Play(MonsterAudio.Cue.Giggle, BodyPosition, .7f); MonsterAudio.Play(MonsterAudio.Cue.Whoosh, BodyPosition, .7f, 1.6f); }
        else if (state == Swing) MonsterAudio.Play(MonsterAudio.Cue.TwinsLaugh, BodyPosition, 1.2f); // seized: they laugh as they haul you off
        else if (state == Throw) MonsterAudio.Play(MonsterAudio.Cue.SwingWide, BodyPosition, 1.2f);
        // Skipping off (after a throw, a miss or a teammate's hit): quick, high giggles running away.
        else if (state == Skip) MonsterAudio.Play(MonsterAudio.Cue.Giggle, BodyPosition, .6f, 1.35f);
    }

    protected override void HostTick(float dt)
    {
        switch (State)
        {
            case Wander:
                Roam(1.8f);
                break;
            case Stalk:
            {
                if (!KeepTarget(dt, 18f)) break;
                float d = DistanceTo(Target);
                bool ready = Time.time >= _grabReady && MonsterAfflictions.CanControl(Target) && MayAttack(Target);
                if (ready && d <= 8f && Sees(Target, 10f)) { Announce(Lure); break; }
                // Not ready: hang back hand in hand a few steps away instead of crowding the player.
                if (!ready && d < 4f) Move(AwayFrom(Target.transform.position, 5f), 2.4f);
                else Move(Target.transform.position, 2.6f);
                break;
            }
            case Lure:
                Halt();
                if (Target == null) { SetState(Wander); break; }
                Face(Target.transform.position, 6f);
                if (StateTime >= MonsterRules.TwinsLureSeconds) Announce(Rush);
                break;
            case Rush:
                if (Target == null) { SetState(Wander); break; }
                Move(Target.transform.position, MonsterRules.TwinsRushSpeed, 30f);
                if (DistanceTo(Target) <= MonsterRules.TwinsReach * .9f) { Announce(Reach); break; }
                if (StateTime > MonsterRules.TwinsRushMax) Miss();
                break;
            case Reach:
                Halt();
                if (Target == null) { Miss(); break; }
                Face(Target.transform.position, 10f);
                if (StateTime < MonsterRules.TwinsReachSeconds) break;
                if (InReach(Target, MonsterRules.TwinsReach) &&
                    MonsterRules.TwinsGrabLands(DistanceTo(Target), Sees(Target, 4f), MayAttack(Target), MonsterAfflictions.CanControl(Target)) &&
                    MonsterAfflictions.Apply(Target, Affliction.Seized, MonsterRules.TwinsHoldSeconds + .4f, E))
                {
                    _victim = Target;
                    Announce(Swing);
                }
                else Miss();
                break;
            case Swing:
                Halt();
                if (_victim == null || _victim.isDisabled || _victim.deadSet || !MonsterAfflictions.Has(_victim, Affliction.Seized))
                { _victim = null; Miss(); break; }
                if (StateTime < MonsterRules.TwinsHoldSeconds) break;
                Hurl(_victim);
                _victim = null;
                _grabReady = Time.time + Economy.ExtendedMoons.Cooldown(MonsterRules.TwinsCooldown);
                Announce(Throw);
                break;
            case Throw:
                Halt();
                if (StateTime > .6f) Announce(Skip);
                break;
            case Skip:
                if (Target != null) Move(AwayFrom(Target.transform.position, 6f), 3.6f); else Roam(2.4f);
                if (StateTime > 2.5f) SetState(Target != null ? Stalk : Wander);
                break;
        }
    }

    private bool KeepTarget(float dt, float range)
    {
        if (Target == null) { SetState(Wander); return false; }
        if (Sees(Target, range)) LostSight = 0f;
        else if ((LostSight += dt) > 5f) { Target = null; SetState(Wander); return false; }
        return true;
    }

    private void Miss()
    {
        _grabReady = Time.time + Economy.ExtendedMoons.Cooldown(MonsterRules.TwinsMissCooldown);
        Announce(Skip);
    }

    // The throw, aimed by the rule the players can learn (current room and the rooms next to it):
    //  1. a pit -> thrown to just short of its edge (they can still save themselves), 10 damage;
    //  2. else another monster -> thrown into it, 10 damage;
    //  3. else the nearest wall -> thrown into it, 25 damage when they hit it (native tumble impact);
    //  4. else straight ahead, 10 damage.
    private void Hurl(PlayerAvatar victim)
    {
        MonsterAfflictions.End(victim, Affliction.Seized);
        var from = victim.transform.position;
        var aim = PlanThrow(victim, out var point);
        var dir = Flat(point - from);
        float meters = dir.magnitude;
        if (aim == MonsterRules.ThrowAim.Forward || meters < .3f)
        {
            dir = Flat(E.transform.forward);
            if (dir.sqrMagnitude < .01f) dir = Flat(from - BodyPosition);
            meters = 3f;
        }
        if (aim == MonsterRules.ThrowAim.Wall)
        {
            // The 25 lands when they hit the wall, through the game's own tumble impact damage.
            if (victim.tumble != null) victim.tumble.ImpactHurtSet(2f, MonsterRules.TwinsWallDamage);
        }
        else MonsterAfflictions.Damage(victim, MonsterRules.ThrowDamage(aim), E);
        Knock(victim, dir.normalized + Vector3.up * .45f, MonsterRules.ThrowForce(meters));
        Plugin.Log.LogInfo($"[Monsters] Hansel and Gretel threw a player toward {aim} ({meters:0.0} m).");
    }

    private static List<Collider> _pits;
    private static LevelGenerator _pitsLevel;

    private MonsterRules.ThrowAim PlanThrow(PlayerAvatar victim, out Vector3 point)
    {
        point = default;
        var from = victim.transform.position;
        var rooms = NearbyRooms();
        float best;

        // 1. Pits: deadly HurtColliders in these rooms; aim at the edge nearest to the player, a margin short of it.
        if (_pitsLevel != LevelGenerator.Instance || _pits == null)
        {
            _pitsLevel = LevelGenerator.Instance;
            _pits = new List<Collider>();
            foreach (var hurt in Object.FindObjectsOfType<HurtCollider>())
                if (hurt != null && hurt.deathPit && hurt.TryGetComponent<Collider>(out var c)) _pits.Add(c);
        }
        bool pit = false; best = MonsterRules.TwinsThrowSearch;
        foreach (var c in _pits)
        {
            if (c == null || !c.enabled || !InRooms(c.bounds, rooms)) continue;
            var b = c.bounds;
            if (b.Contains(new Vector3(from.x, b.center.y, from.z))) continue; // already over it
            var edge = b.ClosestPoint(new Vector3(from.x, b.center.y, from.z));
            float d = Vector3.Distance(Flat(edge), Flat(from));
            if (d >= best) continue;
            best = d; pit = true;
            var back = Flat(from - edge).normalized;
            point = new Vector3(edge.x, from.y, edge.z) + back * MonsterRules.TwinsPitMargin;
        }
        if (pit) return MonsterRules.ThrowAim.Pit;

        // 2. Another monster in these rooms.
        bool monster = false; best = MonsterRules.TwinsThrowSearch;
        if (EnemyDirector.instance != null)
            foreach (var other in EnemyDirector.instance.enemiesSpawned)
            {
                if (other == null || other == Parent || !other.Spawned || other.Enemy == null ||
                    (other.Enemy.HasHealth && other.Enemy.Health.dead)) continue;
                bool here = false;
                foreach (var r in other.currentRooms) if (r != null && rooms.Contains(r)) { here = true; break; }
                if (!here) continue;
                var at = other.Enemy.HasRigidbody ? other.Enemy.Rigidbody.rb.position : other.Enemy.transform.position;
                float d = Vector3.Distance(at, from);
                if (d < best) { best = d; monster = true; point = at; }
            }
        if (monster) return MonsterRules.ThrowAim.Monster;

        // 3. The nearest wall at chest height.
        bool wall = false; best = 6f;
        var chest = from + Vector3.up * .9f;
        for (int k = 0; k < 16; k++)
        {
            var dir = Quaternion.Euler(0f, k * 22.5f, 0f) * Vector3.forward;
            foreach (var hit in Physics.RaycastAll(chest, dir, best, SemiFunc.LayerMaskGetVisionObstruct(), QueryTriggerInteraction.Ignore))
            {
                var col = hit.collider;
                if (hit.distance >= best || col.GetComponentInParent<PlayerAvatar>() != null || col.GetComponentInParent<PlayerTumble>() != null ||
                    col.GetComponentInParent<EnemyParent>() != null || (col.attachedRigidbody != null && !col.attachedRigidbody.isKinematic)) continue;
                best = hit.distance; wall = true; point = hit.point;
            }
        }
        return MonsterRules.ChooseThrow(false, false, wall);
    }

    // The twins' room plus every room a level-path point in it connects to ("current room + 1").
    private HashSet<RoomVolume> NearbyRooms()
    {
        var rooms = new HashSet<RoomVolume>();
        foreach (var r in Parent.currentRooms) if (r != null) rooms.Add(r);
        var points = LevelGenerator.Instance != null ? LevelGenerator.Instance.LevelPathPoints : null;
        if (points != null)
            foreach (var lp in points)
            {
                if (lp == null || lp.Room == null || !rooms.Contains(lp.Room) || lp.ConnectedPoints == null) continue;
                foreach (var c in lp.ConnectedPoints) if (c != null && c.Room != null) _scratch.Add(c.Room);
            }
        rooms.UnionWith(_scratch);
        _scratch.Clear();
        return rooms;
    }

    private static readonly HashSet<RoomVolume> _scratch = new();

    private static bool InRooms(Bounds b, HashSet<RoomVolume> rooms)
    {
        foreach (var room in rooms)
            foreach (var c in room.GetComponentsInChildren<Collider>())
                if (c != null && c.bounds.Intersects(b)) return true;
        return false;
    }
}
