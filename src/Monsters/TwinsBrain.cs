using UnityEngine;

namespace IncreaseHardMod.Monsters;

/// <summary>
/// Hansel and Gretel (Tier II): twin children who never let go of each other's hand - one monster, one health pool.
/// They trail a player, stop and stare with a giggle for 1 s, then rush (5.2 m/s, up to 2.5 s). In reach they stretch
/// out their free hands for 0.7 s; a player still within 1.8 m and in sight is seized, held between them and
/// swung for 1.2 s, then thrown: 18 damage and a hard tumble the way they face. A hit or a stun during the reach
/// or the hold makes them let go. 7 s between grabs (3 s after a miss); the shared 3 s control immunity applies.
/// </summary>
internal sealed class TwinsBrain : MonsterBrain
{
    private const byte Wander = 0, Stalk = 1, Lure = 2, Rush = 3, Reach = 4, Swing = 5, Throw = 6, Skip = 7;
    private PlayerAvatar _victim;
    private float _grabReady, _lostSight;

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

    protected override void OnHurt()
    {
        // A teammate's hit breaks the reach or the hold.
        if (State is not (Reach or Swing)) return;
        LetGo();
        _grabReady = Time.time + Economy.ExtendedMoons.Cooldown(MonsterRules.TwinsMissCooldown);
        SetState(Skip);
    }

    protected override void OnStunned() { LetGo(); if (State is Lure or Rush or Reach or Swing or Throw) SetState(Skip); }
    protected override void OnDied() => LetGo();

    private void LetGo()
    {
        if (_victim != null) MonsterAfflictions.End(_victim, Affliction.Seized);
        _victim = null;
    }

    protected override void PlayStateCue(byte state)
    {
        if (state == Lure) MonsterAudio.Play(MonsterAudio.Cue.Giggle, BodyPosition, 1.1f);
        else if (state == Reach) MonsterAudio.Play(MonsterAudio.Cue.Giggle, BodyPosition, .7f);
        else if (state == Throw) MonsterAudio.Play(MonsterAudio.Cue.SwingWide, BodyPosition, 1.2f);
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
                if (StateTime >= MonsterRules.TwinsLureSeconds) SetState(Rush);
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
                if (MonsterRules.TwinsGrabLands(DistanceTo(Target), Sees(Target, 4f), MayAttack(Target), MonsterAfflictions.CanControl(Target)) &&
                    MonsterAfflictions.Apply(Target, Affliction.Seized, MonsterRules.TwinsHoldSeconds + .4f, E))
                {
                    _victim = Target;
                    SetState(Swing);
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
                if (StateTime > .6f) SetState(Skip);
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
        if (Sees(Target, range)) _lostSight = 0f;
        else if ((_lostSight += dt) > 5f) { Target = null; SetState(Wander); return false; }
        return true;
    }

    private void Miss()
    {
        _grabReady = Time.time + Economy.ExtendedMoons.Cooldown(MonsterRules.TwinsMissCooldown);
        SetState(Skip);
    }

    // The throw: the grip ends (starting the 3 s immunity), then damage and the game's own tumble, up and forward.
    private void Hurl(PlayerAvatar victim)
    {
        MonsterAfflictions.End(victim, Affliction.Seized);
        MonsterAfflictions.Damage(victim, MonsterRules.TwinsThrowDamage, E);
        var forward = Flat(E.transform.forward);
        if (forward.sqrMagnitude < .01f) forward = Flat(victim.transform.position - BodyPosition);
        Knock(victim, forward.normalized + Vector3.up * .55f, MonsterRules.TwinsThrowForce);
    }
}
