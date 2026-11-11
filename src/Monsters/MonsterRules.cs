using System;

namespace IncreaseHardMod.Monsters;

/// <summary>The mod's five monsters. Values are stable network/save bytes: append only.</summary>
internal enum MonsterKind : byte { Pig = 0, Witch = 1, Basilisk = 2, Gorgon = 3, Naga = 4 }

/// <summary>Player afflictions the mod's monsters cause. Values are network bytes: append only.</summary>
internal enum Affliction : byte { Burning = 0, Electrified = 1, Constricted = 2, Petrified = 3, Bleeding = 4, Venom = 5, Slowed = 6, Dragged = 7 }

/// <summary>
/// Engine-free balance and state rules of the new monsters (checked by tests/MonsterTests). Every number the
/// design fixed lives here, so the AI, the HUD texts and the tests share one source.
/// </summary>
internal static class MonsterRules
{
    // ---------------------------------------------------------------- shared

    /// <summary>After any hard control ends the player cannot be immobilised again for this long.</summary>
    internal const float ControlImmunitySeconds = 3f;

    /// <summary>Hard control = it stops the player acting (levitation, constriction, petrification, drag).</summary>
    internal static bool IsHardControl(Affliction a) =>
        a is Affliction.Electrified or Affliction.Constricted or Affliction.Petrified or Affliction.Dragged;

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

    // ---------------------------------------------------------------- witch

    internal const float FireballPrepare = 1.2f, FireballCooldown = 5f, FireballSpeed = 9f;
    internal const int FireballDamage = 22;
    /// <summary>Burning: 4 HP/s for 3 s (12 total) and, separately, +50% damage taken for 5 s.</summary>
    internal const float BurnDotPerSecond = 4f, BurnDotSeconds = 3f, BurnVulnerableSeconds = 5f, BurnVulnerability = 1.5f;
    internal const float LevitateSeconds = 3f, LevitatePerSecond = 5f, LevitateCooldown = 12f, LevitateHeight = 1.4f;
    internal const float WitchKeepMin = 6f, WitchKeepMax = 10f, WitchHover = 2.5f;

    /// <summary>Incoming damage while burning: x1.5 once, however many fireballs hit (no stacking).</summary>
    internal static int BurningDamage(int damage, bool burning) =>
        damage <= 0 || !burning ? damage : (int)Math.Round(damage * BurnVulnerability, MidpointRounding.AwayFromZero);

    // ---------------------------------------------------------------- basilisk

    internal const float ConstrictMax = 5f, ConstrictPerSecond = 5f, ConstrictEscapeSeconds = 2.4f;
    internal const int VenomBiteDamage = 16;
    internal const float VenomPerSecond = 4f, VenomSeconds = 6f;

    /// <summary>Seconds of active struggling to break free; Evasion I/II/III makes it 15/30/45% faster.</summary>
    internal static float EscapeSeconds(int evasionLevel) => ConstrictEscapeSeconds * (1f - Level(evasionLevel, .15f, .30f, .45f));

    // ---------------------------------------------------------------- gorgon

    internal const float RoarSeconds = 1f, ChargeSpeed = 7.5f, ChargeMaxSeconds = 2.4f, GorgonStunSeconds = 3f;
    internal const int RamDamage = 45, WallImpactDamage = 10, TrampleDamage = 15;
    internal const float StareSeconds = 2f, StareCooldown = 20f, StareSlowSeconds = 2f, StareSlowFactor = .35f;
    internal const int StareDamage = 25;

    // ---------------------------------------------------------------- naga

    internal const int ComboHitDamage = 22, BleedHitDamage = 16;
    internal const float BleedPerSecond = 4f, BleedSeconds = 5f;
    internal const float PetrifyPrepare = 1.5f, PetrifySeconds = 2.5f, ComboVulnerableSeconds = 1.8f, SpecialRecovery = 4f;
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

    // ---------------------------------------------------------------- perks

    /// <summary>Grounding: electric hold time -20/35/50%.</summary>
    internal static float GroundedSeconds(float seconds, int level) => seconds * (1f - Level(level, .20f, .35f, .50f));
    /// <summary>Thermal Protection: burn damage -20/35/50% (the DoT; the vulnerability window is unchanged).</summary>
    internal static float ThermalDot(float perSecond, int level) => perSecond * (1f - Level(level, .20f, .35f, .50f));
    /// <summary>Antidote: poison/venom duration -20/40/60%.</summary>
    internal static float AntidoteSeconds(float seconds, int level) => seconds * (1f - Level(level, .20f, .40f, .60f));
    /// <summary>Stone Resilience: petrification -20/35/50%.</summary>
    internal static float StoneSeconds(float seconds, int level) => seconds * (1f - Level(level, .20f, .35f, .50f));
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

/// <summary>
/// One player's monster afflictions on the host, as plain timers (engine-free so tests can drive it):
/// effect end times, the hard-control immunity window, and the burn vulnerability that never stacks.
/// </summary>
internal sealed class AfflictionTimers
{
    private readonly float[] _until = new float[8];
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
            if (k > 7 || v < 0f || v > 15f) return false;
            if (v > 0f) { v -= (float)System.Math.Max(0d, now - sent); if (v <= 0f) return false; }
        }
        viewId = id; kind = k; value = v; extra = x; flag = f;
        return true;
    }

    internal static bool ReadState(object data, string level, out int viewId, out byte kind, out byte state, out int target, out float extra)
    {
        viewId = 0; kind = 0; state = 0; target = 0; extra = 0f;
        if (data is not object[] p || p.Length != 6 || p[0] is not int id || id <= 0 || p[1] is not byte k || k > 4 ||
            p[2] is not byte s || p[3] is not int t || p[4] is not float e || float.IsNaN(e) || float.IsInfinity(e) ||
            p[5] is not string l || l != level) return false;
        viewId = id; kind = k; state = s; target = t; extra = e;
        return true;
    }
}
