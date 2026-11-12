using System.Collections.Generic;
using UnityEngine;

namespace IncreaseHardMod.Monsters;

/// <summary>
/// Gorgon (Tier III, armoured bull). Patrol -> AcquireTarget -> Roar -> Charge -> Impact -> Recover.
///
/// Turns to its target, roars with its head down for 1 s, then charges almost straight (slight steering, up to
/// 7.5 m/s for 2.4 s). The first player it reaches is pinned on its horns and dragged; slamming them into a
/// wall deals 45 (ram) + 10 (impact) once. Others in the way take 15 and are knocked aside with the native
/// tumble; loose items take normal physics damage from the heavy body. Missing and hitting a wall stuns it for
/// 3 s - the team's window. Each player is hurt at most once per charge.
/// Balance pass: the Death Stare is removed (it is a bull, not a Medusa); 8 s between charges, 4 s between head tosses.
/// Every attack respects its height window: it never hits a player on a ledge above or below it, and it does not
/// charge at one.
/// It can only be grabbed while stunned (a grab in progress is released when the stun ends).
/// </summary>
internal sealed class GorgonBrain : MonsterBrain
{
    private const byte Patrol = 0, Hunt = 1, Acquire = 2, Roar = 3, Charge = 4, Impact = 5, Recover = 6, Stare = 7;
    private readonly HashSet<PlayerAvatar> _hit = new();
    private PlayerAvatar _victim;
    private Vector3 _dir;
    private float _lostSight, _tramplReady, _chargeReady;

    internal override MonsterPose Pose => State switch
    {
        Acquire or Roar => MonsterPose.Prepare,
        Charge => MonsterPose.Attack,
        Stare => MonsterPose.Special,
        Impact or Recover => MonsterPose.Vulnerable,
        _ => MonsterPose.Move,
    };

    internal override MonsterAnim Anim => State switch
    {
        Acquire => MonsterAnim.Acquire,
        Roar => MonsterAnim.Roar,
        Charge => MonsterAnim.Charge,
        Stare => MonsterAnim.Stare,
        Impact or Recover => MonsterAnim.Daze,
        _ => MonsterAnim.None,
    };

    protected override bool GrabAllowed() => E.IsStunned();

    protected override void OnSpawned() { _victim = null; _hit.Clear(); _chargeReady = Time.time + 3f; }

    protected override void OnSeePlayer(PlayerAvatar player)
    {
        if (State == Patrol) { Target = player; SetState(Hunt); }
    }

    protected override void OnNoise(Vector3 position)
    {
        if (State == Patrol) Destination = position;
    }

    protected override void OnStunned() { DropVictim(); if (State is Charge or Roar or Acquire or Stare) Announce(Recover); }
    protected override void OnDied() => DropVictim();

    private void DropVictim()
    {
        if (_victim != null) MonsterAfflictions.End(_victim, Affliction.Dragged);
        _victim = null;
    }

    protected override void PlayStateCue(byte state)
    {
        if (state == Acquire) MonsterAudio.Play(MonsterAudio.Cue.Growl, BodyPosition, 1f);
        else if (state == Roar) MonsterAudio.Play(MonsterAudio.Cue.Roar, BodyPosition, 1.4f);
        // Dazed after hitting a wall or being stunned: a heavy, slow snort and a low groan.
        else if (state == Recover) { MonsterAudio.Play(MonsterAudio.Cue.Snort, BodyPosition, .9f, .7f); MonsterAudio.Play(MonsterAudio.Cue.Growl, BodyPosition, .5f, .8f); }
        else if (state == Charge) { MonsterAudio.Play(MonsterAudio.Cue.Snort, BodyPosition, 1.2f); MonsterAudio.Play(MonsterAudio.Cue.Stomp, BodyPosition, 1f, .8f); }
    }

    protected override void HostTick(float dt)
    {
        switch (State)
        {
            case Patrol:
                Roam(2.2f);
                break;
            case Hunt:
                HuntTick(dt);
                break;
            case Acquire:
                Halt();
                if (Target == null) { SetState(Patrol); break; }
                Face(Target.transform.position, 6f);
                if (StateTime >= .6f) Announce(Roar);
                break;
            case Roar:
                Halt();
                if (Target == null) { SetState(Patrol); break; }
                Face(Target.transform.position, 6f);
                if (StateTime < MonsterRules.RoarSeconds) break;
                _dir = Flat(Target.transform.position - BodyPosition).normalized;
                if (_dir.sqrMagnitude < .01f) _dir = E.transform.forward;
                _hit.Clear();
                _victim = null;
                Announce(Charge);
                break;
            case Charge:
                ChargeTick(dt);
                break;
            case Impact:
            case Recover:
                Halt();
                if (StateTime > Economy.ExtendedMoons.Cooldown(1.5f)) SetState(Target != null ? Hunt : Patrol);
                break;
        }
    }

    private void HuntTick(float dt)
    {
        if (Target == null) { SetState(Patrol); return; }
        bool sees = Sees(Target, 20f);
        if (sees) _lostSight = 0f;
        else if ((_lostSight += dt) > 5f) { Target = null; SetState(Patrol); return; }
        float d = DistanceTo(Target);
        if (d < 2.2f && Time.time >= _tramplReady && InReach(Target, 2.2f) && MayAttack(Target))
        {
            // Too close to charge: a head toss.
            _tramplReady = Time.time + Economy.ExtendedMoons.Cooldown(MonsterRules.HeadTossCooldown);
            MonsterAfflictions.Damage(Target, MonsterRules.TrampleDamage, E);
            Knock(Target, Flat(Target.transform.position - BodyPosition) + Vector3.up * .4f, 9f);
            Fx(MonsterFx.HeadToss, BodyPosition, Vector3.zero);
            return;
        }
        if (sees && MayAttack(Target))
        {
            var dy = Target.transform.position.y - E.transform.position.y;
            bool sameFloor = MonsterRules.InReach(Kind, 0f, dy, 0f, 1f); // no charge at someone on a ledge
            if (sameFloor && d >= 3.5f && d <= 14f && Time.time >= _chargeReady && ClearRun(Target))
            { _chargeReady = Time.time + Economy.ExtendedMoons.Cooldown(MonsterRules.ChargeCooldown); Announce(Acquire); return; }
        }
        Move(Target.transform.position, 2.6f);
    }

    // A straight, knee-high run to the target without a wall in between.
    private bool ClearRun(PlayerAvatar target)
    {
        var from = BodyPosition + Vector3.up * .3f;
        var to = new Vector3(target.transform.position.x, from.y, target.transform.position.z);
        return !Physics.SphereCast(from, .4f, (to - from).normalized, out var hit, Vector3.Distance(from, to),
                   SemiFunc.LayerMaskGetVisionObstruct(), QueryTriggerInteraction.Ignore) ||
               hit.collider.GetComponentInParent<PlayerAvatar>() != null || hit.collider.transform.IsChildOf(Parent.transform);
    }

    private void ChargeTick(float dt)
    {
        if (!E.HasRigidbody) { Announce(Recover); return; }
        var rb = E.Rigidbody.rb;
        // Almost straight: a little steering toward the target until someone is on the horns.
        if (_victim == null && Target != null)
        {
            var want = Flat(Target.transform.position - rb.position).normalized;
            if (want.sqrMagnitude > .01f) _dir = Vector3.RotateTowards(_dir, want, 20f * Mathf.Deg2Rad * dt, 0f);
        }
        E.NavMeshAgent.Disable(.25f);
        E.Rigidbody.DisableFollowPosition(.25f, 5f);
        rb.velocity = new Vector3(_dir.x * MonsterRules.ChargeSpeed, rb.velocity.y, _dir.z * MonsterRules.ChargeSpeed);
        E.transform.rotation = Quaternion.LookRotation(_dir);

        var front = rb.position + _dir * .9f;
        foreach (var p in SemiFunc.PlayerGetList())
        {
            if (p == null || p.isDisabled || p.deadSet || p == _victim || _hit.Contains(p)) continue;
            // Horns at body height: the player's feet must be inside the bull's height window (not on a shelf above it).
            var dFeet = p.transform.position - E.transform.position;
            if (Vector3.Distance(Flat(p.transform.position), Flat(front)) > 1.2f || !MonsterRules.InReach(Kind, 0f, dFeet.y, 0f, 1f)) continue;
            _hit.Add(p);
            if (!MayAttack(p)) continue;
            if (_victim == null && MonsterAfflictions.Apply(p, Affliction.Dragged, MonsterRules.ChargeMaxSeconds, E)) _victim = p;
            else
            {
                MonsterAfflictions.Damage(p, MonsterRules.TrampleDamage, E);
                var side = Vector3.Cross(Vector3.up, _dir) * (Vector3.Dot(p.transform.position - rb.position, Vector3.Cross(Vector3.up, _dir)) >= 0f ? 1f : -1f);
                Knock(p, side + _dir * .5f + Vector3.up * .5f, 14f);
            }
        }

        if (Physics.SphereCast(rb.position, .5f, _dir, out var wall, 1f, SemiFunc.LayerMaskGetVisionObstruct(), QueryTriggerInteraction.Ignore) &&
            !wall.collider.transform.IsChildOf(Parent.transform) && wall.collider.GetComponentInParent<PlayerAvatar>() == null &&
            wall.collider.GetComponentInParent<PlayerTumble>() == null &&
            (wall.collider.attachedRigidbody == null || wall.collider.attachedRigidbody.isKinematic || wall.collider.attachedRigidbody.mass > 60f))
        {
            Fx(MonsterFx.WallImpact, wall.point, Vector3.zero);
            rb.velocity = new Vector3(0f, rb.velocity.y, 0f);
            E.NavMeshAgent.Warp(rb.position);
            if (_victim != null)
            {
                MonsterAfflictions.Damage(_victim, MonsterRules.RamDamage + MonsterRules.WallImpactDamage, E);
                DropVictim();
                Announce(Impact);
            }
            else
            {
                if (E.HasStateStunned) Economy.ExtendedMoons.SelfStun(E.StateStunned, MonsterRules.GorgonStunSeconds);
                Announce(Recover);
            }
            return;
        }
        if (StateTime >= MonsterRules.ChargeMaxSeconds)
        {
            rb.velocity = new Vector3(rb.velocity.x * .3f, rb.velocity.y, rb.velocity.z * .3f);
            E.NavMeshAgent.Warp(rb.position);
            if (_victim != null) { MonsterAfflictions.Damage(_victim, MonsterRules.TrampleDamage, E); DropVictim(); }
            Announce(Recover);
        }
    }
}
