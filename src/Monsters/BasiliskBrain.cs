using UnityEngine;

namespace IncreaseHardMod.Monsters;

/// <summary>
/// Basilisk (Tier II, stealth grappler). Hide -> Stalk -> Ambush -> Constrict -> Retreat; Provoked -> VenomBite.
///
/// Moves slowly and quietly on the navmesh with a low body. When close it warns (rustle, hiss, raised head for
/// 0.8 s), lunges, and constricts: no movement, 5 HP/s, at most 5 s. The victim breaks free by struggling (the
/// meter on their screen; Evasion makes it faster), or a teammate hitting the snake frees them at once; a stun
/// or the snake's death releases them too. The 3 s control immunity then applies.
/// A loud noise right next to it, being grabbed or being hit makes it bite instead: 16 damage plus venom
/// 4 HP/s for 6 s (Antidote shortens it). Its model has no colliders, so the tail never scatters loot.
/// </summary>
internal sealed class BasiliskBrain : MonsterBrain
{
    private const byte Hide = 0, Stalk = 1, Rattle = 2, Lunge = 3, Constrict = 4, Retreat = 5, BitePrep = 6;
    private PlayerAvatar _victim;
    private float _biteReady, _grabReady;

    internal override MonsterPose Pose => State switch
    {
        Rattle or BitePrep => MonsterPose.Prepare,
        Lunge => MonsterPose.Attack,
        Constrict => MonsterPose.Hold,
        Retreat => MonsterPose.Flee,
        _ => MonsterPose.Move,
    };

    internal override MonsterAnim Anim => State switch
    {
        Rattle => MonsterAnim.Rattle,
        BitePrep => MonsterAnim.BitePrep,
        Lunge => MonsterAnim.Lunge,
        Constrict => MonsterAnim.Coil,
        _ => MonsterAnim.None,
    };

    protected override void OnSpawned() => _victim = null;

    protected override void OnSeePlayer(PlayerAvatar player)
    {
        if (State == Hide) { Target = player; SetState(Stalk); }
    }

    protected override void OnNoise(Vector3 position)
    {
        if (State is Constrict or Lunge or BitePrep) return;
        if (Vector3.Distance(position, BodyPosition) <= 4f) Provoke();
        else if (State == Hide) Destination = position;
    }

    protected override void OnProvoked(PlayerAvatar player)
    {
        if (State is Constrict or Lunge or BitePrep) return;
        if (player != null) Target = player;
        Provoke();
    }

    protected override void OnGrabbed(PlayerAvatar player)
    {
        if (State is Constrict) return;
        if (player != null) Target = player;
        Provoke();
    }

    protected override void OnHurt()
    {
        if (State == Constrict)
        {
            // A teammate's hit frees the victim at once.
            Release();
            Announce(Retreat);
            return;
        }
        Provoke();
    }

    protected override void OnStunned() { if (_victim != null) { Release(); Announce(Retreat); } }
    protected override void OnDied() => Release();

    private void Release()
    {
        if (_victim != null) MonsterAfflictions.End(_victim, Affliction.Constricted);
        _victim = null;
        _grabReady = Time.time + Economy.ExtendedMoons.Cooldown(MonsterRules.BasiliskGrabCooldown);
    }

    private void Provoke()
    {
        var near = Target != null && DistanceTo(Target) <= 2.6f ? Target : NearestVisible(2.6f);
        if (near != null && Time.time >= _biteReady && MayAttack(near)) { Target = near; Announce(BitePrep); }
        else if (State == Hide) { Target = near ?? NearestVisible(10f); if (Target != null) SetState(Stalk); }
    }

    protected override void PlayStateCue(byte state)
    {
        // Ambush wind-up (0.8 s): the tail rattle, then a whip-fast lunge and the squeeze of the coils.
        if (state == Rattle) { MonsterAudio.Play(MonsterAudio.Cue.Rattle, BodyPosition, 1.1f); MonsterAudio.Play(MonsterAudio.Cue.Hiss, BodyPosition, .7f); }
        else if (state == Lunge) { MonsterAudio.Play(MonsterAudio.Cue.Whoosh, BodyPosition, 1f, 1.6f); MonsterAudio.Play(MonsterAudio.Cue.Rustle, BodyPosition, 1.1f); }
        else if (state == Constrict) { MonsterAudio.Play(MonsterAudio.Cue.Bite, BodyPosition, .6f); MonsterAudio.Play(MonsterAudio.Cue.Squeeze, BodyPosition, 1f); }
        // Provoked (loud noise, grabbed, hit): a startled rattle and the venom hiss before it strikes.
        else if (state == BitePrep) { MonsterAudio.Play(MonsterAudio.Cue.Rattle, BodyPosition, 1f, 1.35f); MonsterAudio.Play(MonsterAudio.Cue.Spit, BodyPosition, .9f, .85f); }
        // Backing off (after a strike, or scared off by a hit or a stun): a short nervous rattle.
        else if (state == Retreat) MonsterAudio.Play(MonsterAudio.Cue.Rattle, BodyPosition, .6f, 1.5f);
    }

    private float _squeezeAt;

    // Every client: the coils keep creaking while someone is held, so teammates hear where to help.
    protected override void Update()
    {
        base.Update();
        if (State != Constrict || Dead) { _squeezeAt = Time.time + 1.1f; return; }
        if (Time.time < _squeezeAt) return;
        _squeezeAt = Time.time + 1.1f;
        MonsterAudio.Play(MonsterAudio.Cue.Squeeze, BodyPosition, .7f, Random.Range(.92f, 1.08f));
    }

    protected override void HostTick(float dt)
    {
        switch (State)
        {
            case Hide:
                Roam(1.3f);
                break;
            case Stalk:
                if (Target == null) { SetState(Hide); break; }
                if (Sees(Target, 14f)) LostSight = 0f;
                else if ((LostSight += dt) > 4f) { Target = null; SetState(Hide); break; }
                Move(Target.transform.position, 2.2f);
                float d = DistanceTo(Target);
                if (d <= 2.6f && Time.time >= _grabReady && MayAttack(Target) && MonsterAfflictions.CanControl(Target)) Announce(Rattle);
                else if (d <= 1.8f && Time.time >= _biteReady && !MonsterAfflictions.CanControl(Target) && MayAttack(Target)) Announce(BitePrep);
                break;
            case Rattle:
                Halt();
                if (Target == null) { SetState(Hide); break; }
                Face(Target.transform.position, 12f);
                if (StateTime >= .8f) Announce(Lunge);
                break;
            case Lunge:
                if (Target == null) { Announce(Retreat); break; }
                Move(Target.transform.position, 9f, 40f);
                if (InReach(Target, 1.3f)) // horizontal reach and the snake's low height window
                {
                    if (MayAttack(Target) && MonsterAfflictions.Apply(Target, Affliction.Constricted, MonsterRules.ConstrictMax, E))
                    { _victim = Target; Announce(Constrict); }
                    else { _grabReady = Time.time + Economy.ExtendedMoons.Cooldown(MonsterRules.BasiliskMissCooldown); Announce(Retreat); }
                }
                else if (StateTime > .7f) { _grabReady = Time.time + Economy.ExtendedMoons.Cooldown(MonsterRules.BasiliskMissCooldown); Announce(Retreat); } // missed
                break;
            case Constrict:
                if (_victim == null || !MonsterAfflictions.Has(_victim, Affliction.Constricted)) { Release(); Announce(Retreat); break; }
                Move(_victim.transform.position, 6f, 30f);
                break;
            case BitePrep:
                Halt();
                if (Target == null) { SetState(Hide); break; }
                Face(Target.transform.position, 14f);
                if (StateTime < .35f) break;
                _biteReady = Time.time + Economy.ExtendedMoons.Cooldown(MonsterRules.BasiliskBiteCooldown);
                if (InReach(Target, 2.1f) && MayAttack(Target))
                {
                    Fx(MonsterFx.Bite, BodyPosition, Vector3.zero);
                    MonsterAfflictions.Damage(Target, MonsterRules.VenomBiteDamage, E);
                    MonsterAfflictions.Apply(Target, Affliction.Venom, MonsterRules.VenomSeconds, E);
                }
                Announce(Retreat);
                break;
            case Retreat:
                if (Target != null) Move(AwayFrom(Target.transform.position, 7f), 3.5f); else Roam(2f);
                if (StateTime > 3f) { Target = null; SetState(Hide); }
                break;
        }
    }
}
