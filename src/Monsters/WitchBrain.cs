using System.Collections.Generic;
using UnityEngine;

namespace IncreaseHardMod.Monsters;

/// <summary>
/// Witch (Tier II, ranged caster). AirPatrol -> TargetDetected -> ChooseSpell -> Cast -> Reposition -> Search.
///
/// Flies 2-3 m up (lower under a low ceiling), keeps 6-10 m from its target and needs line of sight to cast.
/// Fireball: 1.2 s of fire gathering in the hand (sound + pose), then a dodgeable projectile the host flies and
/// collides (walls block it): 22 damage and Burning (4 HP/s for 3 s, +50% damage taken for 5 s). Cooldown 5 s.
/// Electric Levitation: a 0.8 s crackling warning, then the target is lifted and held 3 s at 5 HP/s while the
/// witch keeps sight; cover, a stun or her death ends it. Cooldown 12 s; the 3 s control immunity applies.
/// Hitting her while she prepares a spell interrupts it. She can only be grabbed once she is down near the floor
/// (body within 1 m of it: a low ceiling, or falling after a stun).
/// </summary>
internal sealed class WitchBrain : MonsterBrain
{
    private const byte Patrol = 0, Hunt = 1, FirePrep = 2, FireCast = 3, ZapPrep = 4, Zap = 5, Reposition = 6, Search = 7;

    private sealed class Ball { public Vector3 Position, Velocity; public float Life; }
    private readonly List<Ball> _balls = new();
    private float _fireReady, _zapReady, _lostSight, _ceilingCheck, _hover, _losCheck;
    private PlayerAvatar _zapTarget;
    private float _grabCheck;
    private bool _nearFloor;
    private float _laughReady, _spotCheck;

    internal const float GrabHeight = 1f;

    // Every client measures her own height above the floor (the synced body), a few times a second.
    protected override bool GrabAllowed()
    {
        if (Time.time >= _grabCheck)
        {
            _grabCheck = Time.time + .15f;
            var body = BodyPosition;
            float bottom = body.y;
            foreach (var c in E.Rigidbody.GetComponentsInChildren<Collider>())
                if (c != null && c.enabled && !c.isTrigger) bottom = Mathf.Min(bottom, c.bounds.min.y);
            _nearFloor = false;
            foreach (var hit in Physics.RaycastAll(new Vector3(body.x, bottom + .05f, body.z), Vector3.down, GrabHeight + .05f,
                         SemiFunc.LayerMaskGetVisionObstruct(), QueryTriggerInteraction.Ignore))
                if (!hit.collider.transform.IsChildOf(Parent.transform) && hit.collider.GetComponentInParent<PlayerAvatar>() == null)
                { _nearFloor = true; break; }
        }
        return _nearFloor;
    }
    private Vector3 _lastSeen;

    internal override MonsterPose Pose => State switch
    {
        FirePrep or ZapPrep => MonsterPose.Prepare,
        FireCast => MonsterPose.Attack,
        Zap => MonsterPose.Special,
        _ => MonsterPose.Move,
    };

    internal override MonsterAnim Anim => State switch
    {
        FirePrep => MonsterAnim.FirePrep,
        FireCast => MonsterAnim.FireCast,
        ZapPrep => MonsterAnim.ZapPrep,
        Zap => MonsterAnim.Zap,
        _ => MonsterAnim.None,
    };

    protected override float HoverHeight()
    {
        if (Time.time >= _ceilingCheck)
        {
            _ceilingCheck = Time.time + .5f;
            float ceiling = Physics.Raycast(E.transform.position + Vector3.up * .3f, Vector3.up, out var hit, 4f,
                SemiFunc.LayerMaskGetVisionObstruct(), QueryTriggerInteraction.Ignore) ? hit.distance + .3f : 4f;
            // In a low room she has to come down, and is easier to hit.
            _hover = Mathf.Clamp(ceiling - 1.1f, 1f, MonsterRules.WitchHover);
        }
        return _hover > 0f ? _hover : Hover;
    }

    protected override void OnSpawned() { _balls.Clear(); _zapTarget = null; _laughReady = 0f; _fireReady = Time.time + 4f; _zapReady = Time.time + 8f; }

    protected override void OnSeePlayer(PlayerAvatar player)
    {
        if (State is Patrol or Search) { Target = player; SetState(Hunt); }
    }

    protected override void OnNoise(Vector3 position)
    {
        if (State == Patrol) { Destination = position; SetState(Search); }
    }

    protected override void OnHurt()
    {
        if (State is FirePrep or ZapPrep)
        {
            _fireReady = Mathf.Max(_fireReady, Time.time + Economy.ExtendedMoons.Cooldown(4f));
            _zapReady = Mathf.Max(_zapReady, Time.time + Economy.ExtendedMoons.Cooldown(6f));
            Announce(Reposition);
        }
    }

    protected override void OnStunned()
    {
        EndZap();
        if (State is FirePrep or ZapPrep or Zap) SetState(Hunt);
    }

    protected override void OnDied() { EndZap(); _balls.Clear(); }

    private void EndZap()
    {
        if (_zapTarget != null) MonsterAfflictions.End(_zapTarget, Affliction.Electrified);
        _zapTarget = null;
    }

    protected override void PlayStateCue(byte state)
    {
        if (state == FirePrep) MonsterAudio.Play(MonsterAudio.Cue.Charge, BodyPosition);
        else if (state == ZapPrep) MonsterAudio.Play(MonsterAudio.Cue.Zap, BodyPosition);
        else if (state == FireCast || state == Zap) TryLaugh(1f);
        // Darting off on the broom (after a cast, or knocked out of a wind-up by a hit).
        else if (state == Reposition) MonsterAudio.Play(MonsterAudio.Cue.Whoosh, BodyPosition, .9f, 1.35f);
    }

    /// <summary>
    /// Every laugh (attack or hunting call) shares one cooldown: the 2.7 s recording repeated every few seconds
    /// drowned out her attack warnings.
    /// </summary>
    internal bool TryLaugh(float volume)
    {
        if (Time.time < _laughReady) return false;
        _laughReady = Time.time + 12f;
        MonsterAudio.Play(MonsterAudio.Cue.WitchLaugh, BodyPosition, volume);
        return true;
    }

    private float _humAt;

    // Every client: an electric hum for as long as she holds someone up, so the zap is audible, not only its wind-up.
    protected override void Update()
    {
        base.Update();
        if (State != Zap || Dead) { _humAt = 0f; return; }
        if (Time.time < _humAt) return;
        _humAt = Time.time + .9f;
        MonsterAudio.Play(MonsterAudio.Cue.Hum, BodyPosition, .7f);
    }

    protected override void HostTick(float dt)
    {
        TickBalls(dt);
        switch (State)
        {
            case Patrol:
                Roam(MonsterRules.WitchPatrolSpeed);
                // Native vision looks from the donor's vision point, not from her flying body, and may never fire
                // for her: she also checks her own line of sight a few times a second.
                if (Time.time >= _spotCheck)
                {
                    _spotCheck = Time.time + .3f;
                    var spotted = NearestVisible(14f);
                    if (spotted != null) { Target = spotted; SetState(Hunt); }
                }
                break;
            case Search:
                Move(Destination, MonsterRules.WitchMoveSpeed);
                if (StateTime > 8f || Vector3.Distance(Flat(E.transform.position), Flat(Destination)) < 1.2f) SetState(Patrol);
                var seen = NearestVisible(14f);
                if (seen != null) { Target = seen; SetState(Hunt); }
                break;
            case Hunt:
                HuntTick(dt);
                break;
            case FirePrep:
                Halt();
                if (Target == null) { SetState(Search); break; }
                Face(Target.transform.position);
                if (StateTime < MonsterRules.FireballPrepare) break;
                _fireReady = Time.time + Economy.ExtendedMoons.Cooldown(MonsterRules.FireballCooldown);
                if (Sees(Target, 18f) && MayAttack(Target)) { Launch(Target); Announce(FireCast); }
                else SetState(Hunt);
                break;
            case FireCast:
                if (StateTime > .4f) Announce(Reposition);
                break;
            case ZapPrep:
                Halt();
                if (Target == null) { SetState(Search); break; }
                Face(Target.transform.position);
                if (StateTime < MonsterRules.LevitatePrepare) break;
                if (Sees(Target, 13f) && MayAttack(Target) && MonsterAfflictions.Apply(Target, Affliction.Electrified, MonsterRules.LevitateSeconds, E))
                { _zapTarget = Target; Announce(Zap); }
                else { _zapReady = Time.time + Economy.ExtendedMoons.Cooldown(3f); SetState(Hunt); }
                break;
            case Zap:
                Halt();
                if (_zapTarget != null) Face(_zapTarget.transform.position);
                if (Time.time >= _losCheck)
                {
                    _losCheck = Time.time + .2f;
                    // Continuous sight is required: breaking it (cover) ends the hold at once.
                    if (_zapTarget == null || !Sees(_zapTarget, 14f)) EndZap();
                }
                if (_zapTarget == null || !MonsterAfflictions.Has(_zapTarget, Affliction.Electrified))
                {
                    _zapTarget = null;
                    _zapReady = Time.time + Economy.ExtendedMoons.Cooldown(MonsterRules.LevitateCooldown);
                    Announce(Reposition);
                }
                break;
            case Reposition:
                if (Target != null) Move(AwayFrom(Target.transform.position, 5f), MonsterRules.WitchRetreatSpeed);
                // After every spell she drifts away for a while: no back-to-back casts.
                if (StateTime > MonsterRules.WitchRepositionSeconds) SetState(Target != null ? Hunt : Patrol);
                break;
        }
    }

    private void HuntTick(float dt)
    {
        if (Target == null) { Destination = _lastSeen; SetState(Search); return; }
        bool sees = Sees(Target, 16f);
        if (sees) { _lastSeen = Target.transform.position; _lostSight = 0f; }
        else if ((_lostSight += dt) > 4f) { Destination = _lastSeen; Target = null; SetState(Search); return; }
        float d = DistanceTo(Target);
        if (d < MonsterRules.WitchKeepMin) Move(AwayFrom(Target.transform.position, 4f), MonsterRules.WitchRetreatSpeed);
        else if (d > MonsterRules.WitchKeepMax || !sees) Move(Target.transform.position, MonsterRules.WitchMoveSpeed);
        else { Halt(); Face(Target.transform.position); }
        if (!sees || !MayAttack(Target)) return;
        // She only casts after looking at you for a moment (predictable), and one spell pushes the other back.
        if (StateTime < 1.2f) return;
        if (Time.time >= _zapReady && d <= 12f && MonsterAfflictions.CanControl(Target))
        { _fireReady = Mathf.Max(_fireReady, Time.time + 4f); Announce(ZapPrep); }
        else if (Time.time >= _fireReady && d <= 16f)
        { _zapReady = Mathf.Max(_zapReady, Time.time + 4f); Announce(FirePrep); }
    }

    private void Launch(PlayerAvatar target)
    {
        var hand = BodyPosition + Vector3.up * .7f + E.transform.forward * .5f;
        var velocity = (Chest(target) - hand).normalized * MonsterRules.FireballSpeed;
        _balls.Add(new Ball { Position = hand, Velocity = velocity });
        Fx(MonsterFx.FireballLaunch, hand, velocity);
    }

    // The host flies its own copy and decides hits; clients only draw theirs until the burst arrives.
    private void TickBalls(float dt)
    {
        for (int i = _balls.Count - 1; i >= 0; i--)
        {
            var b = _balls[i];
            b.Life += dt;
            var step = b.Velocity * dt;
            Vector3 burst = Vector3.zero;
            bool done = b.Life > 3f;
            foreach (var p in SemiFunc.PlayerGetList())
            {
                if (p == null || p.isDisabled || p.deadSet) continue;
                if (DistanceToSegment(Chest(p), b.Position, b.Position + step) < .55f ||
                    DistanceToSegment(p.transform.position + Vector3.up * .4f, b.Position, b.Position + step) < .5f)
                {
                    MonsterAfflictions.Damage(p, MonsterRules.FireballDamage, E);
                    MonsterAfflictions.Apply(p, Affliction.Burning, MonsterRules.BurnVulnerableSeconds, E);
                    done = true; burst = b.Position; break;
                }
            }
            if (!done && Physics.SphereCast(b.Position, .18f, step.normalized, out var hit, step.magnitude,
                    SemiFunc.LayerMaskGetVisionObstruct(), QueryTriggerInteraction.Ignore) &&
                !hit.collider.transform.IsChildOf(Parent.transform) && hit.collider.GetComponentInParent<PlayerAvatar>() == null)
            { done = true; burst = hit.point; }
            b.Position += step;
            if (!done) continue;
            _balls.RemoveAt(i);
            Fx(MonsterFx.FireballBurst, burst == Vector3.zero ? b.Position : burst, Vector3.zero);
        }
    }

    private static float DistanceToSegment(Vector3 p, Vector3 a, Vector3 b)
    {
        var ab = b - a;
        float t = ab.sqrMagnitude < 1e-6f ? 0f : Mathf.Clamp01(Vector3.Dot(p - a, ab) / ab.sqrMagnitude);
        return Vector3.Distance(p, a + ab * t);
    }
}
