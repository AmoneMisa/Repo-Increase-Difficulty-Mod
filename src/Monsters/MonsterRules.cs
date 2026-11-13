using System;

namespace IncreaseHardMod.Monsters;

/// <summary>The mod's monsters. Values are stable network/save bytes: append only.</summary>
internal enum MonsterKind : byte { Pig = 0, Witch = 1, Basilisk = 2, Gorgon = 3, Naga = 4, Fairy = 5, Twins = 6 }

/// <summary>Player afflictions the mod's monsters cause. Values are network bytes: append only.</summary>
internal enum Affliction : byte { Burning = 0, Electrified = 1, Constricted = 2, Petrified = 3, Bleeding = 4, Venom = 5, Slowed = 6, Dragged = 7, Seized = 8 }

/// <summary>
/// Engine-free balance and state rules of the new monsters (checked by tests/MonsterTests). Every number the
/// design fixed lives here, so the AI, the HUD texts and the tests share one source.
/// </summary>
internal static class MonsterRules
{
    // ---------------------------------------------------------------- shared

    /// <summary>Number of <see cref="Affliction"/> values (array sizes, packet range).</summary>
    internal const int AfflictionCount = 9;
    /// <summary>Number of <see cref="MonsterKind"/> values (packet range).</summary>
    internal const int MonsterCount = 7;

    /// <summary>After any hard control ends the player cannot be immobilised again for this long.</summary>
    internal const float ControlImmunitySeconds = 3f;

    /// <summary>Hard control = it stops the player acting (levitation, constriction, petrification, drag, the twins' grip).</summary>
    internal static bool IsHardControl(Affliction a) =>
        a is Affliction.Electrified or Affliction.Constricted or Affliction.Petrified or Affliction.Dragged or Affliction.Seized;

    /// <summary>A hard control may start only when none is active and the immunity window has passed.</summary>
    internal static bool CanStartHardControl(float now, float activeUntil, float immuneUntil) => now >= activeUntil && now >= immuneUntil;

    /// <summary>When a hard control ends at <paramref name="endedAt"/>, the immunity runs to this time.</summary>
    internal static float ImmunityAfter(float endedAt) => endedAt + ControlImmunitySeconds;

    /// <summary>Refreshing an effect never shortens it; the result is the later end time.</summary>
    internal static float Refresh(float currentEnd, float now, float seconds) => Math.Max(currentEnd, now + Math.Max(0f, seconds));

    /// <summary>
    /// Whole-number damage-over-time: accumulates <paramref name="perSecond"/> * dt in <paramref name="carry"/> and
    /// returns the whole points due now (never more than the total the effect may still deal).
    /// </summary>
    internal static int DotTick(ref float carry, float perSecond, float dt, ref int remainingTotal)
    {
        if (remainingTotal <= 0 || perSecond <= 0f || dt <= 0f) return 0;
        carry += perSecond * dt;
        int due = (int)Math.Floor(carry);
        if (due <= 0) return 0;
        due = Math.Min(due, remainingTotal);
        carry -= due;
        remainingTotal -= due;
        return due;
    }

    /// <summary>Total points of a DoT (rounded, never negative).</summary>
    internal static int DotTotal(float perSecond, float seconds) => Math.Max(0, (int)Math.Round(perSecond * Math.Max(0f, seconds)));

    // ---------------------------------------------------------------- roster

    /// <summary>Default chance that an enemy slot of a level is filled by one of the mod's monsters.</summary>
    internal const float NewMonsterChance = .2f;

    /// <summary>
    /// Whether an enemy slot goes to the mod's monsters: with <paramref name="chance"/> when both kinds are
    /// eligible for the slot's tier; always when only they are; never when none of them is.
    /// </summary>
    internal static bool SlotForNewMonster(float roll, float chance, int newEligible, int nativeEligible) =>
        newEligible > 0 && (nativeEligible <= 0 || roll < chance);

    // ---------------------------------------------------------------- witch

    // Balance pass: a slower, clearly telegraphed caster (longer windups, slower dodgeable fireball, longer cooldowns).
    internal const float FireballPrepare = 1.8f, FireballCooldown = 9f, FireballSpeed = 6f;
    internal const int FireballDamage = 14;
    internal const float WitchPatrolSpeed = 1.6f, WitchMoveSpeed = 2.2f, WitchRetreatSpeed = 2.6f, WitchRepositionSeconds = 2.5f, LevitatePrepare = 1.6f;
    /// <summary>Burning: 4 HP/s for 3 s (12 total) and, separately, +50% damage taken for 5 s.</summary>
    internal const float BurnDotPerSecond = 2f, BurnDotSeconds = 3f, BurnVulnerableSeconds = 4f, BurnVulnerability = 1.25f;
    internal const float LevitateSeconds = 2.5f, LevitatePerSecond = 3f, LevitateCooldown = 22f, LevitateHeight = 1.4f;
    internal const float WitchKeepMin = 6f, WitchKeepMax = 10f, WitchHover = 2.5f;
    /// <summary>How far up she looks for the ceiling, and how far her body's centre stays below it.</summary>
    internal const float WitchCeilingProbe = 6f, WitchCeilingGap = 1f, WitchHoverMax = 4f;

    /// <summary>Hover height (body above the floor point) under a ceiling this high: just below it, 1..4 m.</summary>
    internal static float WitchHoverFor(float ceiling) => Math.Max(1f, Math.Min(WitchHoverMax, ceiling - WitchCeilingGap));

    /// <summary>Incoming damage while burning: x1.5 once, however many fireballs hit (no stacking).</summary>
    internal static int BurningDamage(int damage, bool burning) =>
        damage <= 0 || !burning ? damage : (int)Math.Round(damage * BurnVulnerability, MidpointRounding.AwayFromZero);

    // ---------------------------------------------------------------- basilisk

    internal const float ConstrictMax = 5f, ConstrictPerSecond = 5f, ConstrictEscapeSeconds = 2.4f;
    internal const float BasiliskGrabCooldown = 8f, BasiliskBiteCooldown = 10f, BasiliskMissCooldown = 5f;
    internal const int VenomBiteDamage = 16;
    /// <summary>The rattle before the lunge and the wind-up of the venom bite (the body animates to the same timers).</summary>
    internal const float BasiliskRattleSeconds = .8f, BasiliskBiteWindup = .35f;
    internal const float VenomPerSecond = 4f, VenomSeconds = 6f;

    /// <summary>Seconds of active struggling to break free; Evasion I/II/III makes it 15/30/45% faster.</summary>
    internal static float EscapeSeconds(int evasionLevel) => ConstrictEscapeSeconds * (1f - Level(evasionLevel, .15f, .30f, .45f));

    // ---------------------------------------------------------------- gorgon

    internal const float RoarSeconds = 1f, ChargeSpeed = 7.5f, ChargeMaxSeconds = 2.4f, GorgonStunSeconds = 3f;
    /// <summary>Time between two charges (and head tosses) of one gorgon.</summary>
    internal const float ChargeCooldown = 8f, HeadTossCooldown = 4f;
    /// <summary>
    /// Gorgon ranges from the middle of its body (it is about 3 m long): the head toss reaches past the horns, and
    /// it charges only from beyond that, so there is no gap where it neither tosses nor charges.
    /// </summary>
    internal const float HeadTossReach = 3f, ChargeMinDistance = 3.5f, GorgonCloseNotice = 4.5f, GorgonHurtNotice = 20f;
    internal const int RamDamage = 45, WallImpactDamage = 10, TrampleDamage = 15;
    /// <summary>Speed factor of the Slowed affliction (kept for compatibility; the gorgon stare that caused it is gone).</summary>
    internal const float StareSlowFactor = .35f;

    // ---------------------------------------------------------------- naga

    internal const int ComboHitDamage = 22, BleedHitDamage = 10;
    internal const float BleedPerSecond = 2f, BleedSeconds = 4f;
    internal const float PetrifyPrepare = 1.5f, PetrifySeconds = 2.5f, ComboVulnerableSeconds = 2.5f, SpecialRecovery = 7f;
    // Balance pass: slower naga with slower swings and shorter sight, so players can see the blows coming and hide.
    internal const float NagaChaseSpeed = 3.4f, NagaPatrolSpeed = 1.8f, NagaSight = 10f, NagaLoseSight = 2.5f;
    internal const float ComboFirstWindup = .7f, ComboSecondWindup = .85f, BleedWindup = 1f, ComboCooldown = 4f;
    internal const float ComboFirstRange = 2.3f, ComboFirstAngle = 70f, ComboSecondRange = 3.2f, ComboSecondAngle = 170f;

    /// <summary>A naga special (petrify or bleed) is allowed only after the recovery since the last special.</summary>
    internal static bool SpecialReady(float now, float lastSpecialEnd) => now >= lastSpecialEnd + SpecialRecovery;

    /// <summary>Melee arc test (degrees, full width) on the horizontal plane.</summary>
    internal static bool InArc(float forwardX, float forwardZ, float toX, float toZ, float distance, float range, float arcDegrees)
    {
        if (distance > range) return false;
        float f = (float)Math.Sqrt(forwardX * forwardX + forwardZ * forwardZ), t = (float)Math.Sqrt(toX * toX + toZ * toZ);
        if (f < 1e-4f || t < 1e-4f) return true;
        double cos = (forwardX * toX + forwardZ * toZ) / (f * t);
        double angle = Math.Acos(Math.Max(-1.0, Math.Min(1.0, cos))) * 180.0 / Math.PI;
        return angle <= arcDegrees * .5f;
    }

    // ---------------------------------------------------------------- mischief fairy

    internal const int FairyHealth = 45;
    internal const float FairyHover = 1.5f, FairyLiftMaxMass = 6f, FairySearchRange = 14f, FairyLiftSeconds = .8f;
    /// <summary>She carries a thing for at most this long, or until it is this far from where she took it.</summary>
    internal const float FairyCarrySeconds = 7f, FairyCarryDistance = 10f;
    /// <summary>Pause between thefts; a player this close while she carries makes her drop it and bolt.</summary>
    internal const float FairyCooldown = 14f, FairyStartleRange = 2.2f, FairyFleeSeconds = 3f;

    /// <summary>The fairy only lifts loose, light things lying in a cart or an extraction point.</summary>
    internal static bool FairyMayLift(float mass, bool held, bool inCart, bool inExtraction, bool inTruck) =>
        !held && !inTruck && mass > 0f && mass <= FairyLiftMaxMass && (inCart || inExtraction);

    /// <summary>She lets go when a player takes the thing or startles her, or once it was carried long or far enough.</summary>
    internal static bool FairyDrops(float carriedSeconds, float distance, bool held, bool startled) =>
        held || startled || carriedSeconds >= FairyCarrySeconds || (carriedSeconds >= 1.5f && distance >= FairyCarryDistance);

    // ---------------------------------------------------------------- hansel and gretel

    internal const int TwinsHealth = 150, TwinsThrowDamage = 10, TwinsWallDamage = 25;
    /// <summary>The throw lands this far short of a pit's edge so the player can still save themselves.</summary>
    internal const float TwinsPitMargin = .7f, TwinsForcePerMeter = 3f, TwinsMinForce = 6f, TwinsMaxForce = 20f, TwinsThrowSearch = 8f;
    /// <summary>They stare (lure), rush, reach out, hold the player between them and throw.</summary>
    internal const float TwinsLureSeconds = 1.6f, TwinsRushSpeed = 3.8f, TwinsRushMax = 2f, TwinsReachSeconds = 1f, TwinsReach = 1.5f;
    internal const float TwinsHoldSeconds = 1.2f, TwinsThrowForce = 16f, TwinsCooldown = 12f, TwinsMissCooldown = 6f;

    internal enum ThrowAim : byte { Pit, Monster, Wall, Forward }

    /// <summary>Where the twins throw (in the current room and the rooms next to it): a pit edge first, else a
    /// monster, else the nearest wall (25 damage), else straight ahead.</summary>
    internal static ThrowAim ChooseThrow(bool pit, bool monster, bool wall) =>
        pit ? ThrowAim.Pit : monster ? ThrowAim.Monster : wall ? ThrowAim.Wall : ThrowAim.Forward;

    /// <summary>Throw strength for a flight of <paramref name="meters"/>.</summary>
    internal static float ThrowForce(float meters) => Math.Max(TwinsMinForce, Math.Min(TwinsMaxForce, meters * TwinsForcePerMeter));

    internal static int ThrowDamage(ThrowAim aim) => aim == ThrowAim.Wall ? TwinsWallDamage : TwinsThrowDamage;

    /// <summary>The grab lands only on a visible player still within reach when the windup ends.</summary>
    internal static bool TwinsGrabLands(float distance, bool sees, bool mayAttack, bool canControl) =>
        distance <= TwinsReach && sees && mayAttack && canControl;

    // ---------------------------------------------------------------- reach and impacts

    /// <summary>
    /// Melee/grab reach window per monster, relative to its feet: horizontal range and how far below / above its
    /// feet a player's feet may be. A low snake cannot reach someone on a table; a tall naga reaches up a step.
    /// </summary>
    internal static void Reach(MonsterKind kind, out float range, out float below, out float above)
    {
        switch (kind)
        {
            case MonsterKind.Basilisk: range = 1.5f; below = .5f; above = 1.1f; break;
            case MonsterKind.Gorgon: range = 1.6f; below = .6f; above = 1.5f; break;
            case MonsterKind.Naga: range = ComboSecondRange; below = .8f; above = 2f; break;
            case MonsterKind.Twins: range = 1.6f; below = .5f; above = 1.3f; break;
            default: range = 1.5f; below = .6f; above = 1.6f; break;
        }
    }

    /// <summary>Whether a player whose feet are (dx, dy, dz) from the monster's feet is inside its reach.</summary>
    internal static bool InReach(MonsterKind kind, float dx, float dy, float dz, float range)
    {
        Reach(kind, out _, out float below, out float above);
        return dx * dx + dz * dz <= range * range && dy >= -below && dy <= above;
    }

    /// <summary>
    /// Damage a monster takes from a light / medium / heavy impact (thrown against the floor or a wall, slammed while
    /// grabbed): 2.5% / 7% / 14% of its health, at least 3 / 6 / 10. Only while stunned or held (native rule).
    /// </summary>
    internal static int ImpactDamage(int health, int level) => level switch
    {
        0 => Math.Max(3, (int)Math.Round(health * .025f)),
        1 => Math.Max(6, (int)Math.Round(health * .07f)),
        _ => Math.Max(10, (int)Math.Round(health * .14f)),
    };

    /// <summary>After a player lets go, impacts keep hurting this long (the throw and its landing).</summary>
    internal const float ThrownHurtSeconds = 1.5f;

    // ---------------------------------------------------------------- native-style noticing and leaving

    /// <summary>Longest walk away when the director sends monsters off (EnemyParent.forceLeave).</summary>
    internal const float LeaveMaxSeconds = 20f;

    /// <summary>The native view cone (EnemyVision dot): 0.4 standing, 0.6 crouching, 0.9 crawling.</summary>
    internal static bool InViewCone(float dot, bool crouching, bool crawling) =>
        dot >= (crawling ? .9f : crouching ? .6f : .4f);

    /// <summary>
    /// Whether a monster notices a player it has line of sight to (native EnemyVision rules): always within 3.5 m
    /// standing or 2 m crouched / crawling; beyond that only inside the view cone, and a crouching / crawling
    /// player only once watched for 0.5 / 1 s (watchedSeconds below 0: not being watched).
    /// </summary>
    internal static bool SpotsPlayer(float distance, float dot, bool crouching, bool crawling, float watchedSeconds)
    {
        if (distance <= (crouching || crawling ? 2f : 3.5f)) return true;
        if (!InViewCone(dot, crouching, crawling)) return false;
        return watchedSeconds >= (crawling ? 1f : crouching ? .5f : 0f);
    }

    /// <summary>
    /// Speed (m/s) at which a held or just-released monster counts as swung or thrown (above a sprint). Simply holding it is not
    /// an attack: the body twitches against the grab (its own walking pull) and those small knocks must not hurt.
    /// </summary>
    internal const float SwingHurtSpeed = 6.5f, ReleaseGraceSeconds = .3f;

    /// <summary>Whether this frame opens the impact-damage window: only a fast release (a throw), never while held.</summary>
    internal static bool HoldImpactArms(bool held, float sinceRelease, float speed) =>
        !held && sinceRelease <= ReleaseGraceSeconds && speed >= SwingHurtSpeed;

    // ---------------------------------------------------------------- perks

    /// <summary>Grounding: electric hold time -20/35/50%.</summary>
    internal static float GroundedSeconds(float seconds, int level) => seconds * (1f - Level(level, .20f, .35f, .50f));
    /// <summary>Thermal Protection: burn damage -20/35/50% (the DoT; the vulnerability window is unchanged).</summary>
    internal static float ThermalDot(float perSecond, int level) => perSecond * (1f - Level(level, .20f, .35f, .50f));
    /// <summary>Antidote: poison/venom duration -20/40/60%.</summary>
    internal static float AntidoteSeconds(float seconds, int level) => seconds * (1f - Level(level, .20f, .40f, .60f));
    /// <summary>Stone Resilience: petrification -20/35/50%.</summary>
    /// <summary>Dodge Roll cooldown 22/18/14 s (0 = not learned).</summary>
    internal static float DodgeCooldown(int level) => level <= 0 ? 0f : level == 1 ? 22f : level == 2 ? 18f : 14f;
    /// <summary>Golden Nose: the pig is highlighted within 8/15/25 m.</summary>
    internal static float GoldenNoseRange(int level) => level <= 0 ? 0f : level == 1 ? 8f : level == 2 ? 15f : 25f;
    /// <summary>Soul Hunter: +5/8/10% soul value of the new monsters, by the team's best level (never summed).</summary>
    internal static float SoulHunterFactor(int teamBestLevel) => 1f + Level(teamBestLevel, .05f, .08f, .10f);

    private static float Level(int level, float one, float two, float three) =>
        level <= 0 ? 0f : level == 1 ? one : level == 2 ? two : three;

    // ---------------------------------------------------------------- souls

    /// <summary>Soul value range per monster (the pig's is its base before fattening).</summary>
    internal static void SoulRange(MonsterKind kind, out int min, out int max)
    {
        switch (kind)
        {
            case MonsterKind.Pig: min = 2000; max = 2000; break;
            case MonsterKind.Witch: min = 3700; max = 4500; break;
            case MonsterKind.Basilisk: min = 4000; max = 4500; break;
            case MonsterKind.Gorgon: min = 6000; max = 7500; break;
            case MonsterKind.Fairy: min = 1500; max = 2200; break;
            case MonsterKind.Twins: min = 3800; max = 4600; break;
            default: min = 6500; max = 7500; break;
        }
    }

    /// <summary>A soul value for a roll in 0..1, with the team's Soul Hunter factor applied once.</summary>
    internal static int SoulValue(MonsterKind kind, float roll, int teamSoulHunter)
    {
        SoulRange(kind, out int min, out int max);
        float value = min + (max - min) * Math.Max(0f, Math.Min(1f, roll));
        return (int)Math.Round(value * SoulHunterFactor(teamSoulHunter));
    }

    // ---------------------------------------------------------------- pig

    internal const int PigMinFeeds = 3, PigMaxFeeds = 10;
    internal const float PigSpawnChance = .11f;
    /// <summary>Only small valuables are food: at most this value and this mass.</summary>
    internal const float PigFoodMaxValue = 3500f, PigFoodMaxMass = 4.5f, PigEatRange = 1.6f;
    /// <summary>After the third meal, no food for this long ends the fattening by itself.</summary>
    internal const float PigSatedAfter = 25f;

    /// <summary>How long a valuable a player put down near the pig stays on offer.</summary>
    internal const float PigOfferSeconds = 30f;

    /// <summary>
    /// The pig eats only what a player offered it: a small loose valuable a player let go of near it. Loot still
    /// lying where the level put it, and anything in a cart, an extraction point or the truck, is never food.
    /// </summary>
    internal static bool PigAccepts(float value, float mass, bool held, bool offered, bool inCart, bool inExtraction, bool inTruck) =>
        offered && !held && !inCart && !inExtraction && !inTruck && value > 0f && value <= PigFoodMaxValue && mass <= PigFoodMaxMass;

    internal static int PigHealth(int feeds) => 60 + 12 * Clamp(feeds, 0, PigMaxFeeds);
    internal static float PigMass(int feeds) => 18f + (75f - 18f) * Clamp(feeds, 0, PigMaxFeeds) / PigMaxFeeds;
    internal static float PigScale(int feeds) => 1f + 1.2f * Clamp(feeds, 0, PigMaxFeeds) / PigMaxFeeds;
    internal static bool PigCanFinish(int feeds) => feeds >= PigMinFeeds;

    /// <summary>
    /// The pig's soul: $2,000 before the third meal; after it, the fattening bonus grows by $500 a meal up to
    /// $7,000, but never exceeds 1.5x the value actually fed (no money from nothing).
    /// </summary>
    internal static int PigSoul(int feeds, float consumedValue, int teamSoulHunter)
    {
        feeds = Clamp(feeds, 0, PigMaxFeeds);
        float bonus = feeds < PigMinFeeds ? 0f : Math.Min(500f * feeds, 1.5f * Math.Max(0f, consumedValue));
        return (int)Math.Round(Math.Min(7000f, 2000f + bonus) * SoulHunterFactor(teamSoulHunter));
    }

    /// <summary>Extra rewards at death: a small golden souvenir from 3 meals; at 10 a 10% upgrade roll.</summary>
    internal static void PigRewards(int feeds, float roll, out bool souvenir, out bool upgrade)
    {
        souvenir = feeds >= PigMinFeeds;
        upgrade = feeds >= PigMaxFeeds && roll < .10f;
    }

    private static int Clamp(int v, int lo, int hi) => v < lo ? lo : v > hi ? hi : v;
}

/// <summary>How a monster gets around: decides which movement animation its body plays.</summary>
internal enum MonsterGait : byte { Walk, Crawl, Fly }

/// <summary>
/// Engine-free rules of the monsters' animation states (checked by
/// tests/MonsterTests). View only: nothing here changes damage, timers or the AI.
/// </summary>
internal static class MonsterAnimRules
{
    /// <summary>Snakes crawl, the witch and the fairy fly, the pig flies until it has landed; the rest walk.</summary>
    internal static MonsterGait Gait(MonsterKind kind, bool pigLanded) => kind switch
    {
        MonsterKind.Basilisk or MonsterKind.Naga => MonsterGait.Crawl,
        MonsterKind.Witch or MonsterKind.Fairy => MonsterGait.Fly,
        MonsterKind.Pig => pigLanded ? MonsterGait.Walk : MonsterGait.Fly,
        _ => MonsterGait.Walk,
    };

    /// <summary>
    /// How hard a monster struggles in a player's grip, 0..1: fully when it is awake, weakly while stunned (the
    /// stun pose stays readable), not at all when dead or not held.
    /// </summary>
    internal static float Struggle(bool dead, bool stunned, bool held) => dead || !held ? 0f : stunned ? .4f : 1f;

    /// <summary>A monster in a grip or stunned does not play its attack animation (the reaction reads instead).</summary>
    internal static bool AttackAnimShown(bool dead, bool stunned, bool held) => !dead && !stunned && !held;

    /// <summary>The hurt flinch lasts this long; it peaks almost at once and settles.</summary>
    internal const float FlinchSeconds = .4f, FlinchPeak = .06f;

    /// <summary>Flinch strength 0..1 this long after a hit: a fast snap to 1, then an eased return to 0.</summary>
    internal static float Flinch(float sinceHit) => Impulse(sinceHit, FlinchPeak, FlinchSeconds);

    /// <summary>A one-off jolt 0..1: a fast snap to 1 at <paramref name="peak"/>, then an eased return to 0.</summary>
    internal static float Impulse(float since, float peak, float seconds)
    {
        if (since < 0f || since >= seconds) return 0f;
        if (since < peak) return since / peak;
        float u = 1f - (since - peak) / (seconds - peak);
        return u * u;
    }

    // ---- attack timelines. Every attack is wind-up -> strike -> follow-through, as continuous curves of the
    // state time, so a pose never jumps: the strike lands exactly when the host deals the damage and the
    // follow-through carries on into the next state.

    /// <summary>Smoothstep of a 0..1 fraction (clamped): no speed at either end.</summary>
    internal static float Ease(float u)
    {
        if (u <= 0f) return 0f;
        if (u >= 1f) return 1f;
        return u * u * (3f - 2f * u);
    }

    /// <summary>
    /// Wind-up 0..1 over <paramref name="seconds"/>: most of the way in the first third (the weapon comes up
    /// where players can read it), then a slow draw to the full pose.
    /// </summary>
    internal static float Draw(float st, float seconds)
    {
        if (seconds <= 0f) return 1f;
        return .75f * Ease(st / (seconds * .35f)) + .25f * Ease(st / seconds);
    }

    /// <summary>How far a strike swings past its end pose, and how long it takes to settle back onto it.</summary>
    internal const float StrikeOvershoot = .1f, StrikeSettle = .24f;

    /// <summary>
    /// Strike 0..1: nothing until <paramref name="lead"/> seconds before <paramref name="hitAt"/>, then it
    /// accelerates and is exactly 1 at the hit; after it, it swings a little past (follow-through) and settles on 1.
    /// </summary>
    internal static float Strike(float st, float hitAt, float lead)
    {
        float s = st - (hitAt - lead);
        if (s <= 0f) return 0f;
        if (s < lead) { float u = s / lead; return u * u; }
        float v = (s - lead) / StrikeSettle;
        if (v >= 1f) return 1f;
        return 1f + StrikeOvershoot * (float)Math.Sin(Math.PI * v) * (1f - v);
    }

    /// <summary>Weight of the previous attack's end pose: held for <paramref name="hold"/>, then eased out.</summary>
    internal static float Release(float since, float hold, float fade)
    {
        if (since <= hold) return 1f;
        return fade <= 0f ? 0f : 1f - Ease((since - hold) / fade);
    }
}

/// <summary>
/// One player's monster afflictions on the host, as plain timers (engine-free so tests can drive it):
/// effect end times, the hard-control immunity window, and the burn vulnerability that never stacks.
/// </summary>
internal sealed class AfflictionTimers
{
    private readonly float[] _until = new float[MonsterRules.AfflictionCount];
    private float _immuneUntil;

    internal float Until(Affliction a) => _until[(int)a];
    internal bool Active(Affliction a, float now) => _until[(int)a] > now;
    internal float ImmuneUntil => _immuneUntil;

    internal float HardControlUntil
    {
        get
        {
            float until = 0f;
            for (int i = 0; i < _until.Length; i++)
                if (MonsterRules.IsHardControl((Affliction)i)) until = Math.Max(until, _until[i]);
            return until;
        }
    }

    /// <summary>
    /// Tries to start (or refresh) an affliction. Hard control is refused while another one runs or during the
    /// immunity window; soft effects always refresh without shortening.
    /// </summary>
    internal bool TryApply(Affliction a, float now, float seconds)
    {
        if (seconds <= 0f) return false;
        if (MonsterRules.IsHardControl(a))
        {
            if (!MonsterRules.CanStartHardControl(now, HardControlUntil, _immuneUntil)) return false;
            _until[(int)a] = now + seconds;
            return true;
        }
        _until[(int)a] = MonsterRules.Refresh(_until[(int)a], now, seconds);
        return true;
    }

    /// <summary>Ends an affliction now; ending a hard control starts the immunity window.</summary>
    internal void End(Affliction a, float now)
    {
        if (_until[(int)a] <= now) { _until[(int)a] = 0f; return; }
        _until[(int)a] = 0f;
        if (MonsterRules.IsHardControl(a)) _immuneUntil = Math.Max(_immuneUntil, MonsterRules.ImmunityAfter(now));
    }

    /// <summary>Expires finished effects; a hard control that ran out starts the immunity from its end time.</summary>
    internal void Expire(float now, Action<Affliction> ended)
    {
        for (int i = 0; i < _until.Length; i++)
        {
            if (_until[i] <= 0f || _until[i] > now) continue;
            var a = (Affliction)i;
            if (MonsterRules.IsHardControl(a)) _immuneUntil = Math.Max(_immuneUntil, MonsterRules.ImmunityAfter(_until[i]));
            _until[i] = 0f;
            ended?.Invoke(a);
        }
    }

    internal void Clear() { Array.Clear(_until, 0, _until.Length); _immuneUntil = 0f; }
}

/// <summary>Validation of the monsters' network packets (engine-free; checked by tests/MonsterTests).</summary>
internal static class MonsterPackets
{
    internal static bool ReadAffliction(object data, double now, string level, out int viewId, out byte kind, out float value, out int extra, out bool flag)
    {
        viewId = 0; kind = 0; value = 0f; extra = 0; flag = false;
        if (data is not object[] p || p.Length != 7 || p[0] is not int id || id <= 0 || p[1] is not byte k ||
            p[2] is not float v || p[3] is not double sent || p[4] is not string l || l != level ||
            p[5] is not int x || p[6] is not bool f || float.IsNaN(v) || float.IsInfinity(v) || double.IsNaN(sent) || double.IsInfinity(sent))
            return false;
        if (k == 255) { if (v <= 0f || v > 200f) return false; }
        else
        {
            if (k >= MonsterRules.AfflictionCount || v < 0f || v > 15f) return false;
            if (v > 0f) { v -= (float)System.Math.Max(0d, now - sent); if (v <= 0f) return false; }
        }
        viewId = id; kind = k; value = v; extra = x; flag = f;
        return true;
    }

    internal static bool ReadState(object data, string level, out int viewId, out byte kind, out byte state, out int target, out float extra)
    {
        viewId = 0; kind = 0; state = 0; target = 0; extra = 0f;
        if (data is not object[] p || p.Length != 6 || p[0] is not int id || id <= 0 || p[1] is not byte k || k >= MonsterRules.MonsterCount ||
            p[2] is not byte s || p[3] is not int t || p[4] is not float e || float.IsNaN(e) || float.IsInfinity(e) ||
            p[5] is not string l || l != level) return false;
        viewId = id; kind = k; state = s; target = t; extra = e;
        return true;
    }
}
