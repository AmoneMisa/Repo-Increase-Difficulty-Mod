using UnityEngine;

namespace IncreaseHardMod.Monsters;

/// <summary>
/// Naga Queen (Tier III, elite duellist with four arms and four swords).
/// Guard -> Detect -> FastApproach -> SwordCombo -> SpecialAttack -> Reposition.
///
/// Combo: a short single-target cut (22, 70 degrees, 2.3 m), then a wide sweep (22 to everyone in 170 degrees,
/// 3.2 m) - each hit needs line of sight from her, so nothing is hit through a wall - then 1.8 s of recovery
/// in which she is open. Specials (never back to back: 4 s recovery after one ends):
///  * Petrification: she stops, raises her swords and stares for 1.5 s; if the target is still in sight it is
///    turned to stone for 2.5 s (no movement, no items; Stone Resilience shortens it). Never lethal by itself.
///  * Bleeding cut: all four swords raised, a distinct 0.6 s windup, 16 + bleeding 4 HP/s for 5 s.
/// </summary>
internal sealed class NagaBrain : MonsterBrain
{
    private const byte Guard = 0, Hunt = 1, Combo1 = 2, Combo2 = 3, Vulnerable = 4, Petrify = 5, Bleed = 6, Reposition = 7;
    private float _lastSpecialEnd = -99f, _lostSight, _decide;
    private bool _bleedNext;

    internal override MonsterPose Pose => State switch
    {
        Combo1 or Combo2 => MonsterPose.Attack,
        Petrify => MonsterPose.Prepare,
        Bleed => MonsterPose.Special,
        Vulnerable => MonsterPose.Vulnerable,
        _ => MonsterPose.Move,
    };

    internal override MonsterAnim Anim => State switch
    {
        Combo1 => MonsterAnim.Combo1,
        Combo2 => MonsterAnim.Combo2,
        Bleed => MonsterAnim.BleedCut,
        Petrify => MonsterAnim.StoneGaze,
        Vulnerable => MonsterAnim.Exposed,
        _ => MonsterAnim.None,
    };

    protected override void OnSeePlayer(PlayerAvatar player)
    {
        if (State == Guard) { Target = player; SetState(Hunt); }
    }

    protected override void OnNoise(Vector3 position)
    {
        if (State == Guard) Destination = position;
    }

    protected override void OnStunned() { if (State is Combo1 or Combo2 or Petrify or Bleed) SetState(Vulnerable); }

    protected override void PlayStateCue(byte state)
    {
        if (state == Petrify) MonsterAudio.Play(MonsterAudio.Cue.Stare, BodyPosition, 1.2f);
        else if (state == Bleed) MonsterAudio.Play(MonsterAudio.Cue.Hiss, BodyPosition, 1.1f);
    }

    private bool SpecialReady => Time.time >= _lastSpecialEnd + Economy.ExtendedMoons.Cooldown(MonsterRules.SpecialRecovery);

    protected override void HostTick(float dt)
    {
        switch (State)
        {
            case Guard:
                Roam(2.4f);
                break;
            case Hunt:
                HuntTick(dt);
                break;
            case Combo1:
                if (Target != null) Face(Target.transform.position, 10f);
                Halt();
                if (StateTime < .35f) break;
                Strike(MonsterRules.ComboFirstRange, MonsterRules.ComboFirstAngle, MonsterRules.ComboHitDamage, single: true, bleed: false);
                SetState(Combo2);
                break;
            case Combo2:
                Halt();
                if (StateTime < .45f) break;
                Strike(MonsterRules.ComboSecondRange, MonsterRules.ComboSecondAngle, MonsterRules.ComboHitDamage, single: false, bleed: false);
                SetState(Vulnerable);
                break;
            case Vulnerable:
                Halt();
                if (StateTime >= Economy.ExtendedMoons.Cooldown(MonsterRules.ComboVulnerableSeconds)) SetState(Reposition);
                break;
            case Bleed:
                if (Target != null) Face(Target.transform.position, 8f);
                Halt();
                if (StateTime < .6f) break;
                Strike(2.6f, 90f, MonsterRules.BleedHitDamage, single: true, bleed: true);
                _lastSpecialEnd = Time.time;
                _bleedNext = false;
                SetState(Reposition);
                break;
            case Petrify:
                Halt();
                if (Target == null) { SetState(Guard); break; }
                Face(Target.transform.position, 6f);
                if (StateTime < MonsterRules.PetrifyPrepare) break;
                _lastSpecialEnd = Time.time;
                if (Sees(Target, 14f) && MayAttack(Target) &&
                    MonsterAfflictions.Apply(Target, Affliction.Petrified, MonsterRules.PetrifySeconds, E))
                {
                    _lastSpecialEnd = Time.time + MonsterRules.PetrifySeconds; // recovery counts from the end of the stone
                    Fx(MonsterFx.StoneFlash, Chest(Target), Vector3.zero);
                }
                _bleedNext = true;
                SetState(Reposition);
                break;
            case Reposition:
                if (Target != null) Move(AwayFrom(Target.transform.position, 4f), 4.5f);
                if (StateTime > 1.5f) SetState(Target != null ? Hunt : Guard);
                break;
        }
    }

    private void HuntTick(float dt)
    {
        if (Target == null) { SetState(Guard); return; }
        bool sees = Sees(Target, 18f);
        if (sees) _lostSight = 0f;
        else if ((_lostSight += dt) > 5f) { Target = null; SetState(Guard); return; }
        float d = DistanceTo(Target);
        if (d <= 2.2f && MayAttack(Target))
        {
            Halt();
            if (SpecialReady && _bleedNext) Announce(Bleed); else Announce(Combo1);
            return;
        }
        if (Time.time >= _decide)
        {
            _decide = Time.time + 1f;
            if (sees && d >= 4f && d <= 12f && SpecialReady && !_bleedNext && MonsterAfflictions.CanControl(Target) && MayAttack(Target) && Random.value < .5f)
            { Announce(Petrify); return; }
        }
        Move(Target.transform.position, 6f, 20f);
    }

    private void Strike(float range, float arc, int damage, bool single, bool bleed)
    {
        var forward = E.transform.forward;
        Fx(MonsterFx.Slash, BodyPosition, new Vector3(forward.x * range, arc, forward.z * range));
        PlayerAvatar best = null; float bestD = float.MaxValue;
        foreach (var p in SemiFunc.PlayerGetList())
        {
            if (p == null || p.isDisabled || p.deadSet) continue;
            var to = p.transform.position - BodyPosition;
            float d = Flat(to).magnitude;
            if (Mathf.Abs(to.y) > 2f || !MonsterRules.InArc(forward.x, forward.z, to.x, to.z, d, range, arc) || !Sees(p, range + 1.5f) || !MayAttack(p)) continue;
            if (single) { if (d < bestD) { best = p; bestD = d; } continue; }
            MonsterAfflictions.Damage(p, damage, E);
        }
        if (!single || best == null) return;
        MonsterAfflictions.Damage(best, damage, E);
        if (bleed) MonsterAfflictions.Apply(best, Affliction.Bleeding, MonsterRules.BleedSeconds, E);
    }
}
