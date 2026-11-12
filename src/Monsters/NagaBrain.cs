using UnityEngine;

namespace IncreaseHardMod.Monsters;

/// <summary>
/// Naga Queen (Tier III, elite duellist with four arms and four swords).
/// Guard -> Detect -> FastApproach -> SwordCombo -> SpecialAttack -> Reposition.
///
/// Balance pass: slower (3.4 m/s), 10 m sight that she loses after 2.5 s out of view, 0.7 s / 0.85 s swing windups,
/// 4 s between combos, bleeding 10 + 2 HP/s for 4 s, 7 s between specials.
/// Combo: a short single-target cut (22, 70 degrees, 2.3 m), then a wide sweep (22 to everyone in 170 degrees,
/// 3.2 m) - each hit needs line of sight from her, so nothing is hit through a wall - then 2.5 s of recovery
/// in which she is open. Her one special is the bleeding cut (all four swords raised, a distinct 1 s windup),
/// alternating with the combo once it has recovered. Petrification was removed at the players' request; the
/// Petrify byte stays only so old network snapshots still parse.
/// </summary>
internal sealed class NagaBrain : MonsterBrain
{
    private const byte Guard = 0, Hunt = 1, Combo1 = 2, Combo2 = 3, Vulnerable = 4, Petrify = 5, Bleed = 6, Reposition = 7;
    private float _lastSpecialEnd = -99f, _lostSight;
    private bool _bleedNext;
    private float _comboReady;

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

    protected override void OnStunned() { if (State is Combo1 or Combo2 or Petrify or Bleed) Announce(Vulnerable); }

    protected override void PlayStateCue(byte state)
    {
        if (state == Combo1) MonsterAudio.Play(MonsterAudio.Cue.BladeDraw, BodyPosition, .9f);
        else if (state == Combo2) MonsterAudio.Play(MonsterAudio.Cue.BladeDraw, BodyPosition, 1f, .8f);
        // The bleeding cut: a long hiss and a slow, heavy blade draw - different from the combo's quick draws.
        else if (state == Bleed) { MonsterAudio.Play(MonsterAudio.Cue.Chitter, BodyPosition, 1.4f, .9f); MonsterAudio.Play(MonsterAudio.Cue.BladeDraw, BodyPosition, 1f, .6f); }
        // Winded after the combo (or stunned out of an attack): a tired, low hiss - the window to hit her.
        else if (state == Vulnerable) MonsterAudio.Play(MonsterAudio.Cue.Hiss, BodyPosition, 1.1f, .8f);
    }

    private bool SpecialReady => Time.time >= _lastSpecialEnd + Economy.ExtendedMoons.Cooldown(MonsterRules.SpecialRecovery);

    protected override void HostTick(float dt)
    {
        switch (State)
        {
            case Guard:
                Roam(MonsterRules.NagaPatrolSpeed);
                break;
            case Hunt:
                HuntTick(dt);
                break;
            case Combo1:
                if (Target != null) Face(Target.transform.position, 10f);
                Halt();
                if (StateTime < MonsterRules.ComboFirstWindup) break;
                Strike(MonsterRules.ComboFirstRange, MonsterRules.ComboFirstAngle, MonsterRules.ComboHitDamage, single: true, bleed: false);
                Announce(Combo2);
                break;
            case Combo2:
                Halt();
                if (StateTime < MonsterRules.ComboSecondWindup) break;
                Strike(MonsterRules.ComboSecondRange, MonsterRules.ComboSecondAngle, MonsterRules.ComboHitDamage, single: false, bleed: false);
                _comboReady = Time.time + Economy.ExtendedMoons.Cooldown(MonsterRules.ComboCooldown);
                Announce(Vulnerable);
                break;
            case Vulnerable:
                Halt();
                if (StateTime >= Economy.ExtendedMoons.Cooldown(MonsterRules.ComboVulnerableSeconds)) SetState(Reposition);
                break;
            case Bleed:
                if (Target != null) Face(Target.transform.position, 8f);
                Halt();
                if (StateTime < MonsterRules.BleedWindup) break;
                Strike(2.6f, 90f, MonsterRules.BleedHitDamage, single: true, bleed: true);
                _comboReady = Time.time + Economy.ExtendedMoons.Cooldown(MonsterRules.ComboCooldown);
                _lastSpecialEnd = Time.time;
                _bleedNext = false;
                SetState(Reposition);
                break;
            case Reposition:
                if (Target != null) Move(AwayFrom(Target.transform.position, 4f), MonsterRules.NagaChaseSpeed);
                if (StateTime > 2f) SetState(Target != null ? Hunt : Guard);
                break;
        }
    }

    private void HuntTick(float dt)
    {
        if (Target == null) { SetState(Guard); return; }
        // Short sight and a quick give-up: breaking line of sight or getting out of 10 m hides you from her.
        bool sees = Sees(Target, MonsterRules.NagaSight);
        if (sees) _lostSight = 0f;
        else if ((_lostSight += dt) > MonsterRules.NagaLoseSight) { Target = null; SetState(Guard); return; }
        float d = DistanceTo(Target);
        if (d <= 2.2f && MayAttack(Target))
        {
            Halt();
            Face(Target.transform.position, 6f);
            if (Time.time < _comboReady) return; // between combos she circles instead of chaining hits
            // Her one special, the bleeding cut, alternates with the combo once it has recovered.
            if (SpecialReady && _bleedNext) Announce(Bleed); else { _bleedNext = true; Announce(Combo1); }
            return;
        }
        Move(Target.transform.position, MonsterRules.NagaChaseSpeed, 10f);
    }

    private void Strike(float range, float arc, int damage, bool single, bool bleed)
    {
        var forward = E.transform.forward;
        Fx(MonsterFx.Slash, BodyPosition, new Vector3(forward.x * range, arc, forward.z * range));
        PlayerAvatar best = null; float bestD = float.MaxValue;
        foreach (var p in SemiFunc.PlayerGetList())
        {
            if (p == null || p.isDisabled || p.deadSet) continue;
            var to = p.transform.position - E.transform.position; // feet to feet
            float d = Flat(to).magnitude;
            if (!MonsterRules.InReach(Kind, to.x, to.y, to.z, range) || !MonsterRules.InArc(forward.x, forward.z, to.x, to.z, d, range, arc) ||
                !Sees(p, range + 1.5f) || !MayAttack(p)) continue;
            if (single) { if (d < bestD) { best = p; bestD = d; } continue; }
            MonsterAfflictions.Damage(p, damage, E);
        }
        if (!single || best == null) return;
        MonsterAfflictions.Damage(best, damage, E);
        if (bleed) MonsterAfflictions.Apply(best, Affliction.Bleeding, MonsterRules.BleedSeconds, E);
    }
}
