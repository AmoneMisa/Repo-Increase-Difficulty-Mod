using IncreaseHardMod.Monsters;
using IncreaseHardMod.Patches;
using IncreaseHardMod.Economy;

// Engine-free checks of the 0.10.0 monsters: timers, control immunity, stacking, damage over time, pig economy,
// perks, the per-room attack limit and network packet validation. They do not replace an in-game test.
int checks = 0;
void Check(bool ok, string label) { if (!ok) throw new Exception("FAIL: " + label); checks++; Console.WriteLine("PASS: " + label); }

// ---- hard control and the 3 s immunity
var t = new AfflictionTimers();
Check(t.TryApply(Affliction.Electrified, 0f, 3f), "First hard control applies");
Check(!t.TryApply(Affliction.Constricted, 1f, 5f), "A second hard control cannot start while one is active");
t.Expire(3f, null);
Check(!t.Active(Affliction.Electrified, 3f), "Hard control expires on time");
Check(!t.TryApply(Affliction.Petrified, 4f, 2.5f), "No new immobilisation inside the 3 s immunity");
Check(t.TryApply(Affliction.Petrified, 6f, 2.5f), "Immobilisation allowed again after the immunity");
t.End(Affliction.Petrified, 7f);
Check(!t.TryApply(Affliction.Dragged, 9.9f, 2f) && t.TryApply(Affliction.Dragged, 10f, 2f), "Ending control early also starts the 3 s immunity");
var soft = new AfflictionTimers();
Check(soft.TryApply(Affliction.Burning, 0f, 5f) && soft.TryApply(Affliction.Burning, 1f, 1f) && soft.Until(Affliction.Burning) == 5f,
    "A shorter repeat burn never shortens the current one");
Check(soft.TryApply(Affliction.Burning, 4f, 5f) && soft.Until(Affliction.Burning) == 9f, "A new fireball refreshes burning (no extra stack)");
Check(soft.TryApply(Affliction.Slowed, 0f, 2f) && soft.TryApply(Affliction.Electrified, 0f, 3f), "Soft effects never block hard control");

// ---- burning: separate DoT and +50% vulnerability, never multiplied twice
Check(MonsterRules.BurningDamage(20, true) == 25 && MonsterRules.BurningDamage(20, false) == 20, "Burning makes incoming damage x1.25 (balance pass)");
Check(MonsterRules.BurningDamage(MonsterRules.BurningDamage(12, true), false) == 15, "Vulnerability is applied once, not per stack");
Check(MonsterRules.DotTotal(MonsterRules.BurnDotPerSecond, MonsterRules.BurnDotSeconds) == 6, "Burn DoT is 2 HP/s for 3 s = 6 (not five ticks)");
Check(MonsterRules.BurnVulnerableSeconds == 4f && MonsterRules.BurnDotSeconds == 3f, "Vulnerability window (4 s) is separate from the DoT (3 s)");

// ---- DoT ticking in whole points
float carry = 0f; int left = MonsterRules.DotTotal(4f, 6f), dealt = 0;
for (int i = 0; i < 1000; i++) dealt += MonsterRules.DotTick(ref carry, 4f, 1f / 60f, ref left);
Check(dealt == 24 && left == 0, "Venom deals exactly 24 over 6 s at 4 HP/s, never more");
carry = 0f; left = 5; int first = MonsterRules.DotTick(ref carry, 100f, 1f, ref left);
Check(first == 5 && left == 0, "A long frame cannot exceed the DoT's remaining total");

// ---- perks
Check(Math.Abs(MonsterRules.GroundedSeconds(3f, 3) - 1.5f) < 1e-4, "Grounding III halves the electric hold");
Check(Math.Abs(MonsterRules.ThermalDot(4f, 2) - 2.6f) < 1e-4, "Thermal Protection II: burn DoT -35%");
Check(Math.Abs(MonsterRules.AntidoteSeconds(6f, 3) - 2.4f) < 1e-4, "Antidote III: venom -60%");
Check(Math.Abs(MonsterRules.EscapeSeconds(3) - MonsterRules.ConstrictEscapeSeconds * .55f) < 1e-4, "Evasion III frees 45% faster");
Check(MonsterRules.DodgeCooldown(0) == 0f && MonsterRules.DodgeCooldown(1) == 22f && MonsterRules.DodgeCooldown(3) == 14f, "Dodge Roll cooldown 22/18/14 s");
Check(MonsterRules.GoldenNoseRange(2) == 15f, "Golden Nose II highlights within 15 m");
Check(Math.Abs(MonsterRules.SoulHunterFactor(3) - 1.10f) < 1e-4 && MonsterRules.SoulHunterFactor(0) == 1f, "Soul Hunter is +10% at III, none without it");
Check(MonsterRules.SoulValue(MonsterKind.Gorgon, 1f, 3) == 8250 && MonsterRules.SoulValue(MonsterKind.Gorgon, 1f, 3) < 7500 * 1.11f,
    "Soul Hunter applies once with the team's best level (not per player)");

// ---- souls
for (float roll = 0f; roll <= 1f; roll += .25f)
{
    Check(MonsterRules.SoulValue(MonsterKind.Witch, roll, 0) is >= 3700 and <= 4500, $"Witch soul in $3,700-4,500 (roll {roll})");
    Check(MonsterRules.SoulValue(MonsterKind.Naga, roll, 0) is >= 6500 and <= 7500, $"Naga soul in $6,500-7,500 (roll {roll})");
}

// ---- golden pig
Check(MonsterRules.PigHealth(0) == 60 && MonsterRules.PigHealth(10) == 180 && MonsterRules.PigHealth(50) == 180, "Pig HP 60-180, capped at ten meals");
Check(MonsterRules.PigMass(0) == 18f && Math.Abs(MonsterRules.PigMass(10) - 75f) < 1e-3, "Pig mass 18-75 kg");
Check(!MonsterRules.PigCanFinish(2) && MonsterRules.PigCanFinish(3), "Fattening can be finished from the third meal");
Check(MonsterRules.PigSoul(2, 9000f, 0) == 2000, "Killed before the third meal: only the plain soul");
Check(MonsterRules.PigSoul(10, 100000f, 0) == 7000, "Fully fattened soul is capped at $7,000");
Check(MonsterRules.PigSoul(10, 600f, 0) == 2900, "Cheap food cannot print money: bonus <= 1.5x the value fed");
MonsterRules.PigRewards(2, 0f, out bool sv, out bool up); Check(!sv && !up, "No extra reward before three meals");
MonsterRules.PigRewards(3, 0f, out sv, out up); Check(sv && !up, "Golden souvenir from three meals");
MonsterRules.PigRewards(10, .05f, out sv, out up); Check(sv && up, "10% upgrade roll at the maximum");
MonsterRules.PigRewards(10, .5f, out sv, out up); Check(sv && !up, "No upgrade when the roll misses");

// ---- naga / gorgon rules
Check(!MonsterRules.SpecialReady(8.9f, 2f) && MonsterRules.SpecialReady(9f, 2f), "Naga specials need 7 s recovery between them");
Check(MonsterRules.DotTotal(MonsterRules.BleedPerSecond, MonsterRules.BleedSeconds) == 8 && MonsterRules.BleedHitDamage == 10, "Weaker bleeding: 10 + 2 HP/s for 4 s");
Check(MonsterRules.NagaChaseSpeed < 4f && MonsterRules.NagaSight <= 10f && MonsterRules.ComboFirstWindup >= .7f && MonsterRules.ComboCooldown >= 4f,
    "Naga is slower, sees less far and swings slower");
Check(MonsterRules.FireballPrepare >= 1.8f && MonsterRules.FireballSpeed <= 6f && MonsterRules.FireballCooldown >= 9f && MonsterRules.LevitateCooldown >= 20f,
    "Witch casts are telegraphed, dodgeable and rarer");
Check(MonsterRules.InArc(0, 1, 0, 2, 2f, 2.3f, 70f) && !MonsterRules.InArc(0, 1, 2, 0, 2f, 2.3f, 70f), "Short cut hits ahead, not to the side");
Check(MonsterRules.InArc(0, 1, 2, .2f, 2f, 3.2f, 170f) && !MonsterRules.InArc(0, 1, 0, -2, 2f, 3.2f, 170f), "Wide sweep reaches the sides but not behind");
Check(!MonsterRules.InArc(0, 1, 0, 4, 4f, 3.2f, 170f), "Out of range is never hit");
Check(MonsterRules.RamDamage + MonsterRules.WallImpactDamage == 55, "Gorgon wall slam: 45 ram + 10 impact");

// ---- room attack limit
Check(RoomAttackRules.CanEngage(3, 0, "Gnome") && !RoomAttackRules.CanEngage(4, 0, "Gnome"), "A fifth attacker in a room is refused");
Check(RoomAttackRules.CanEngage(0, 0, "Loom") && !RoomAttackRules.CanEngage(1, 1, "Loom"), "Only one Loom attacks per room");
Check(RoomAttackRules.CanEngage(1, 1, "Robe") && !RoomAttackRules.CanEngage(2, 2, "Robe"), "At most two Robes per room");
Check(!RoomAttackRules.CanEngage(1, 1, "Huntsman") && !RoomAttackRules.CanEngage(1, 1, "Cleanup Crew"), "One Huntsman and one Cleanup Crew per room");
Check(RoomAttackRules.CanEngage(2, 2, "Gorgon"), "New monsters share the 4-per-room cap, without a type cap");

// ---- packets
string level = "L1";
Check(MonsterPackets.ReadAffliction(new object[] { 5, (byte)0, 5f, 10d, level, 0, false }, 11d, level, out _, out _, out float v, out _, out _) && Math.Abs(v - 4f) < 1e-4,
    "Affliction snapshot subtracts its age");
Check(!MonsterPackets.ReadAffliction(new object[] { 5, (byte)0, 5f, 10d, "L0", 0, false }, 11d, level, out _, out _, out _, out _, out _), "Other-level packets are ignored");
Check(!MonsterPackets.ReadAffliction(new object[] { 5, (byte)0, 2f, 10d, level, 0, false }, 13d, level, out _, out _, out _, out _, out _), "Expired snapshots are ignored");
Check(!MonsterPackets.ReadAffliction(new object[] { 5, (byte)9, 2f, 10d, level, 0, false }, 10d, level, out _, out _, out _, out _, out _), "Unknown affliction kinds are refused");
Check(!MonsterPackets.ReadAffliction(new object[] { 5, (byte)255, 999f, 10d, level, 0, false }, 10d, level, out _, out _, out _, out _, out _), "Absurd damage packets are refused");
Check(!MonsterPackets.ReadAffliction(new object[] { 5, (byte)1, float.NaN, 10d, level, 0, false }, 10d, level, out _, out _, out _, out _, out _), "NaN durations are refused");
Check(MonsterPackets.ReadState(new object[] { 7, (byte)3, (byte)4, 0, 0f, level }, level, out _, out byte kind, out byte state, out _, out _) && kind == 3 && state == 4, "State snapshot parses");
Check(!MonsterPackets.ReadState(new object[] { 7, (byte)8, (byte)4, 0, 0f, level }, level, out _, out _, out _, out _, out _), "Unknown monster kinds are refused");
Check(MonsterPackets.ReadState(new object[] { 7, (byte)MonsterKind.Twins, (byte)5, 0, 0f, level }, level, out _, out byte twinsKind, out _, out _, out _) && twinsKind == 6 &&
      !MonsterPackets.ReadState(new object[] { 7, (byte)MonsterRules.MonsterCount, (byte)0, 0, 0f, level }, level, out _, out _, out _, out _, out _),
    "Fairy and Twins snapshots parse; the first unused kind is refused");
Check(MonsterPackets.ReadAffliction(new object[] { 5, (byte)Affliction.Seized, 1.2f, 10d, level, 0, false }, 10d, level, out _, out byte seizedKind, out _, out _, out _) && seizedKind == 8,
    "The twins' grip is a valid affliction packet");

// ---- roster slots and pig offers
Check(MonsterRules.SlotForNewMonster(.19f, .2f, 2, 10) && !MonsterRules.SlotForNewMonster(.2f, .2f, 2, 10), "A slot goes to the new monsters with the configured chance");
Check(!MonsterRules.SlotForNewMonster(0f, 1f, 0, 10) && MonsterRules.SlotForNewMonster(.99f, 0f, 2, 0), "No new candidates: native slot; no native candidates: new slot");
Check(!MonsterRules.SlotForNewMonster(0f, 0f, 2, 10) && MonsterRules.SlotForNewMonster(.999f, 1f, 2, 10), "Chance 0 never and chance 1 always picks a new monster");
Check(MonsterRules.PigAccepts(600f, 2f, false, true, false, false, false), "Pig eats a small valuable a player put down near it");
Check(!MonsterRules.PigAccepts(600f, 2f, false, false, false, false, false), "Pig never eats loot nobody offered it");
Check(!MonsterRules.PigAccepts(600f, 2f, true, true, false, false, false) && !MonsterRules.PigAccepts(600f, 2f, false, true, true, false, false) &&
      !MonsterRules.PigAccepts(600f, 2f, false, true, false, true, false) && !MonsterRules.PigAccepts(600f, 2f, false, true, false, false, true),
    "Pig never eats held loot or anything in a cart, an extraction point or the truck");
Check(!MonsterRules.PigAccepts(3501f, 2f, false, true, false, false, false) && !MonsterRules.PigAccepts(600f, 4.6f, false, true, false, false, false), "Pig food stays small: at most $3,500 and 4.5 kg");

// ---- mischief fairy
Check(MonsterRules.FairyMayLift(3f, false, true, false, false) && MonsterRules.FairyMayLift(6f, false, false, true, false), "Fairy lifts light things from a cart or an extraction point");
Check(!MonsterRules.FairyMayLift(3f, false, false, false, false), "Fairy leaves things lying on the floor alone");
Check(!MonsterRules.FairyMayLift(6.1f, false, true, false, false) && !MonsterRules.FairyMayLift(3f, true, true, false, false) && !MonsterRules.FairyMayLift(3f, false, true, false, true),
    "Fairy never lifts heavy or held things, or anything in the truck");
Check(!MonsterRules.FairyDrops(3f, 4f, false, false) && MonsterRules.FairyDrops(3f, 4f, true, false) && MonsterRules.FairyDrops(3f, 4f, false, true), "A player taking the thing or startling the fairy makes her drop it");
Check(MonsterRules.FairyDrops(MonsterRules.FairyCarrySeconds, 0f, false, false) && MonsterRules.FairyDrops(2f, MonsterRules.FairyCarryDistance, false, false) &&
      !MonsterRules.FairyDrops(1f, 50f, false, false), "Fairy carries for at most 7 s or 10 m, never dropping in the first moment");
for (int i = 0; i <= 10; i++)
    Check(MonsterRules.SoulValue(MonsterKind.Fairy, i / 10f, 0) is >= 1500 and <= 2200 && MonsterRules.SoulValue(MonsterKind.Twins, i / 10f, 0) is >= 3800 and <= 4600,
        $"Fairy soul in $1,500-2,200 and Twins soul in $3,800-4,600 (roll {i / 10f})");

// ---- hansel and gretel
Check(MonsterRules.IsHardControl(Affliction.Seized), "The twins' grip is hard control");
var grip = new AfflictionTimers();
Check(grip.TryApply(Affliction.Seized, 0f, MonsterRules.TwinsHoldSeconds) && !grip.TryApply(Affliction.Constricted, .5f, 5f), "A seized player cannot be immobilised by another monster");
grip.End(Affliction.Seized, 1f);
Check(!grip.TryApply(Affliction.Seized, 3.9f, 1.2f) && grip.TryApply(Affliction.Seized, 4f, 1.2f), "After the throw the 3 s immunity stops a second grab");
Check(MonsterRules.TwinsGrabLands(MonsterRules.TwinsReach, true, true, true) && !MonsterRules.TwinsGrabLands(MonsterRules.TwinsReach + .01f, true, true, true) && !MonsterRules.TwinsGrabLands(1f, false, true, true) &&
      !MonsterRules.TwinsGrabLands(1f, true, false, true) && !MonsterRules.TwinsGrabLands(1f, true, true, false),
    "The grab needs reach, sight, the room attack limit and no control immunity");
Check(MonsterRules.TwinsHoldSeconds == 1.2f && MonsterRules.TwinsCooldown == 12f && MonsterRules.TwinsReach == 1.5f, "Twins: 1.2 s hold, 12 s cooldown, 1.5 m grab");
Check(MonsterRules.ChooseThrow(true, true, true) == MonsterRules.ThrowAim.Pit, "A pit nearby wins over monsters and walls");
Check(MonsterRules.ChooseThrow(false, true, true) == MonsterRules.ThrowAim.Monster, "Without a pit, the player is thrown into a monster");
Check(MonsterRules.ChooseThrow(false, false, true) == MonsterRules.ThrowAim.Wall && MonsterRules.ThrowDamage(MonsterRules.ThrowAim.Wall) == 25,
    "Without a pit or a monster, into the wall for 25");
Check(MonsterRules.ChooseThrow(false, false, false) == MonsterRules.ThrowAim.Forward, "Open space: thrown straight ahead");
Check(MonsterRules.TwinsPitMargin > 0f, "A pit throw lands short of the edge, so the player can survive");
Check(MonsterRules.ThrowForce(.1f) == MonsterRules.TwinsMinForce && MonsterRules.ThrowForce(100f) == MonsterRules.TwinsMaxForce, "Throw force is bounded");

Check(MoonRules.Damage(100, 4, false) == 100 && MoonRules.Damage(100, 5, false) == 95 && MoonRules.Damage(100, 6, false) == 95 && MoonRules.Damage(100, 7, false) == 90,
    "New moon defence replaces the old step instead of adding 5 and 10 percent");
Check(MoonRules.Damage(100, 7, true) == 60 && MoonRules.Damage(0, 7, true) == 0 && MoonRules.Damage(1, 7, true) == 1,
    "Guard multiplies the final phase reduction without making small hits heal or vanish");
Check(MoonRules.GuardProcs(7, 100f, 100f, .01f) && !MoonRules.GuardProcs(7, 174.9f, 175f, 0f) &&
      MoonRules.GuardProcs(7, 175f, 175f, .01f) && !MoonRules.GuardProcs(6, 175f, 0f, 0f), "Guard has a per-monster 75-second cooldown and only exists on phase VII");
Check(MoonRules.CooldownFactor(5) == .9f && MoonRules.CooldownFactor(6) == .8f && MoonRules.CooldownFactor(7) == .8f,
    "Phase VI means 20 percent shorter cooldown, not five times faster attacks");
Check(Math.Abs(MoonRules.CooldownRemaining(1f, .8f, 6) - .75f) < .0001f && MoonRules.CooldownRemaining(.2f, 3f, 6) == 3f,
    "Cooldown clocks accelerate elapsed time without corrupting a new reset");
Check(MoonRules.StunFactor(6) == 1f && MoonRules.StunFactor(7) == .6f, "Stun becomes 40 percent shorter on VII");
int drops = 0;
for (int i = 0; i < 10000; i++) if (MoonRules.SoulDrops(6, (i + .5f) / 10000f)) drops++;
Check(drops == 5000 && MoonRules.SoulDrops(5, .9999f), "Random soul chance is 50 percent per life; earlier moons retain guaranteed drops");
Check(MoonRules.Strength(1) == 3 && MoonRules.Strength(2) == 5 && MoonRules.Strength(3) == 7 &&
      Math.Abs(MoonRules.GrabForce(3) - 9.6f) < .001f, "Moon lift budgets use native forceMax and Strength progression for 3/5/7 upgrades");
// ---- reach and impacts
Check(MonsterRules.InReach(MonsterKind.Basilisk, 1f, 0f, 0f, 1.3f) && !MonsterRules.InReach(MonsterKind.Basilisk, 1f, 1.4f, 0f, 1.3f),
    "The basilisk reaches a player on the floor, not one standing on a table");
Check(!MonsterRules.InReach(MonsterKind.Basilisk, 1.4f, 0f, 0f, 1.3f), "Out of horizontal reach is never grabbed");
Check(MonsterRules.InReach(MonsterKind.Naga, 2f, 1.8f, 0f, 3.2f) && !MonsterRules.InReach(MonsterKind.Basilisk, 1f, 1.8f, 0f, 3f),
    "The tall naga reaches up a step the snake cannot");
Check(!MonsterRules.InReach(MonsterKind.Gorgon, 0f, -1f, 0f, 1f), "The gorgon's horns miss a player well below its feet");
Check(MonsterRules.ImpactDamage(600, 0) == 15 && MonsterRules.ImpactDamage(600, 1) == 42 && MonsterRules.ImpactDamage(600, 2) == 84,
    "Gorgon impact damage 15/42/84 (light/medium/heavy)");
Check(MonsterRules.ImpactDamage(60, 0) == 3 && MonsterRules.ImpactDamage(60, 2) == 10, "Small monsters still take minimum impact damage");
// ---- animation states (view only): gait per monster, struggle in a grip, hurt flinch
Check(MonsterAnimRules.Gait(MonsterKind.Basilisk, false) == MonsterGait.Crawl && MonsterAnimRules.Gait(MonsterKind.Naga, false) == MonsterGait.Crawl,
    "Snakes crawl");
Check(MonsterAnimRules.Gait(MonsterKind.Witch, false) == MonsterGait.Fly && MonsterAnimRules.Gait(MonsterKind.Fairy, true) == MonsterGait.Fly,
    "The witch and the fairy fly (the pig-only landed flag does not ground them)");
Check(MonsterAnimRules.Gait(MonsterKind.Pig, false) == MonsterGait.Fly && MonsterAnimRules.Gait(MonsterKind.Pig, true) == MonsterGait.Walk,
    "The pig flies until it lands, then walks");
Check(MonsterAnimRules.Gait(MonsterKind.Gorgon, false) == MonsterGait.Walk && MonsterAnimRules.Gait(MonsterKind.Twins, false) == MonsterGait.Walk,
    "The gorgon and the twins walk");
Check(MonsterAnimRules.Struggle(false, false, true) == 1f && MonsterAnimRules.Struggle(false, false, false) == 0f,
    "A held monster struggles fully; a free one does not");
Check(MonsterAnimRules.Struggle(false, true, true) is > 0f and < 1f, "A stunned monster in a grip struggles weakly (the stun pose stays readable)");
Check(MonsterAnimRules.Struggle(true, false, true) == 0f, "A dead monster does not struggle");
Check(MonsterAnimRules.AttackAnimShown(false, false, false) && !MonsterAnimRules.AttackAnimShown(false, true, false) &&
      !MonsterAnimRules.AttackAnimShown(false, false, true) && !MonsterAnimRules.AttackAnimShown(true, false, false),
    "Attack animations play only while alive, awake and not held");
Check(MonsterAnimRules.Flinch(-1f) == 0f && MonsterAnimRules.Flinch(0f) == 0f && MonsterAnimRules.Flinch(MonsterAnimRules.FlinchSeconds) == 0f,
    "No flinch before a hit, at its very start, or after it has settled");
Check(Math.Abs(MonsterAnimRules.Flinch(MonsterAnimRules.FlinchPeak) - 1f) < 1e-4, "The flinch peaks right after the hit");
float flinchPrev = 1f; bool flinchFalls = true;
for (float s = MonsterAnimRules.FlinchPeak + .02f; s < MonsterAnimRules.FlinchSeconds; s += .02f)
{
    float f = MonsterAnimRules.Flinch(s);
    if (f > flinchPrev || f < 0f || f > 1f) flinchFalls = false;
    flinchPrev = f;
}
Check(flinchFalls && flinchPrev < .05f, "After the peak the flinch only eases back to rest, staying within 0..1");
// Attack timelines: wind-up, a strike that lands on the damage moment, a small follow-through.
{
    const float atk_hitAt = MonsterRules.ComboFirstWindup, atk_lead = .13f;
    Check(MonsterAnimRules.Strike(atk_hitAt - atk_lead, atk_hitAt, atk_lead) == 0f && MonsterAnimRules.Strike(0f, atk_hitAt, atk_lead) == 0f, "A strike does not start before its lead-in");
    Check(Math.Abs(MonsterAnimRules.Strike(atk_hitAt, atk_hitAt, atk_lead) - 1f) < 1e-4, "The strike lands exactly when the damage is dealt");
    bool atk_rises = true, atk_bounded = true; float atk_prev = 0f, atk_peak = 0f, atk_step = 0f;
    for (float atk_s = 0f; atk_s < atk_hitAt + 1f; atk_s += .005f)
    {
        float atk_v = MonsterAnimRules.Strike(atk_s, atk_hitAt, atk_lead);
        if (atk_s < atk_hitAt - .0051f && atk_v < atk_prev) atk_rises = false;
        if (atk_v < 0f || atk_v > 1f + MonsterAnimRules.StrikeOvershoot) atk_bounded = false;
        atk_step = Math.Max(atk_step, Math.Abs(atk_v - atk_prev));
        atk_peak = Math.Max(atk_peak, atk_v); atk_prev = atk_v;
    }
    Check(atk_rises && atk_bounded, "The strike only accelerates into the hit and never swings further than its overshoot");
    Check(atk_peak > 1.02f && atk_prev == 1f, "After the hit it follows through a little and settles on the end pose");
    Check(atk_step < .1f, "The strike has no jump between two 5 ms samples");
    Check(MonsterAnimRules.Strike(atk_hitAt + MonsterAnimRules.StrikeSettle, atk_hitAt, atk_lead) == 1f, "The follow-through is over after the settle time");
    bool atk_drawOk = true; float atk_d0 = 0f;
    for (float atk_s = 0f; atk_s <= 1f; atk_s += .01f) { float atk_d = MonsterAnimRules.Draw(atk_s, .57f); if (atk_d < atk_d0 - 1e-6f || atk_d < 0f || atk_d > 1f + 1e-5f) atk_drawOk = false; atk_d0 = atk_d; }
    Check(atk_drawOk && MonsterAnimRules.Draw(0f, .57f) == 0f && Math.Abs(MonsterAnimRules.Draw(.57f, .57f) - 1f) < 1e-5 && MonsterAnimRules.Draw(.2f, .57f) > .7f,
        "The wind-up comes up early, keeps drawing back and is complete when the strike begins");
    Check(MonsterAnimRules.Release(0f, .1f, .3f) == 1f && MonsterAnimRules.Release(.1f, .1f, .3f) == 1f && MonsterAnimRules.Release(.4f, .1f, .3f) == 0f &&
          MonsterAnimRules.Release(.25f, .1f, .3f) is > 0f and < 1f, "The previous strike'atk_s pose is held, then eased out");
    Check(MonsterAnimRules.Impulse(.08f, .08f, .6f) == 1f && MonsterAnimRules.Impulse(.6f, .08f, .6f) == 0f && MonsterAnimRules.Impulse(-1f, .08f, .6f) == 0f,
        "A jolt (head toss, wall recoil) peaks at once and is gone at its end");
}
// Holding a monster still is not an attack; swinging or throwing it is.
Check(!MonsterRules.HoldImpactArms(true, 0f, 5.5f), "Holding a monster still or carrying it at a sprint does not arm impact damage");
Check(!MonsterRules.HoldImpactArms(true, 0f, 9f), "Even fast movement in a player's hands does not arm impact damage while held");
Check(MonsterRules.HoldImpactArms(false, .1f, 8f), "A fast release (a throw) arms impact damage");
Check(!MonsterRules.HoldImpactArms(false, .1f, 1f), "Gently letting go does not arm impact damage");
Check(!MonsterRules.HoldImpactArms(false, 2f, 8f), "A monster nobody touched recently is not hurt by its own fast movement");
// The bull has no close-range dead zone: the head toss reaches almost to where a charge starts.
Check(MonsterRules.HeadTossReach >= 2.8f && MonsterRules.ChargeMinDistance - MonsterRules.HeadTossReach <= .6f,
    "Gorgon: no gap between head-toss reach and the closest charge distance");
Check(MonsterRules.GorgonCloseNotice > MonsterRules.HeadTossReach, "Gorgon notices a player standing within head-toss reach even outside its vision cone");
// The witch flies just under the ceiling: high in a tall hall, low in a low room, never below 1 m.
Check(Math.Abs(MonsterRules.WitchHoverFor(3.2f) - 2.2f) < .01f, "Witch: in a 3.2 m room she flies 1 m below the ceiling");
Check(MonsterRules.WitchHoverFor(12f) == MonsterRules.WitchHoverMax, "Witch: in a tall hall she stops at her highest hover");
Check(MonsterRules.WitchHoverFor(1.5f) == 1f, "Witch: under a very low ceiling she still keeps 1 m");
// Native vision rules for noticing a player.
Check(MonsterRules.SpotsPlayer(3f, -1f, false, false, -1f), "A standing player right next to a monster is noticed even behind it");
Check(!MonsterRules.SpotsPlayer(3f, -1f, true, false, -1f), "A crouching player 3 m behind a monster is not noticed");
Check(MonsterRules.SpotsPlayer(1.5f, -1f, true, false, -1f), "A crouching player within 2 m is noticed");
Check(MonsterRules.SpotsPlayer(8f, .5f, false, false, 0f), "A standing player inside the view cone is noticed at once");
Check(!MonsterRules.SpotsPlayer(8f, .5f, true, false, 5f), "A crouching player at the edge of the cone stays hidden");
Check(!MonsterRules.SpotsPlayer(8f, .8f, true, false, .2f) && MonsterRules.SpotsPlayer(8f, .8f, true, false, .6f), "A crouching player in front is noticed only after half a second");
Check(!MonsterRules.SpotsPlayer(8f, .8f, false, true, 3f) && MonsterRules.SpotsPlayer(8f, .95f, false, true, 1.1f), "A crawling player needs to be almost straight ahead for a second");
// Procedural cues: every one renders to sane audio. Pass a folder as the first argument to export them as WAV.
{
    string dump = args.Length > 0 ? args[0] : null;
    if (dump != null) Directory.CreateDirectory(dump);
    bool sane = true; string worst = "";
    foreach (MonsterAudio.Cue cue in Enum.GetValues<MonsterAudio.Cue>())
    {
        var samples = MonsterAudio.Render(cue);
        double sum = 0; float top = 0f; bool finite = true;
        foreach (float x in samples) { if (float.IsNaN(x) || float.IsInfinity(x)) finite = false; sum += x * x; top = Math.Max(top, Math.Abs(x)); }
        double rms = Math.Sqrt(sum / samples.Length);
        float edge = Math.Abs(samples[^1]);
        if (!finite || top > 1.0001f || top < .15f || rms < .01 || edge > .05f || Math.Abs(samples.Length - MonsterAudio.SynthLength(cue) * MonsterAudio.SynthRate) > 1f)
        { sane = false; worst += $" {cue}(peak {top:0.00}, rms {rms:0.000}, end {edge:0.00})"; }
        if (dump == null) continue;
        using var w = new BinaryWriter(File.Create(Path.Combine(dump, "monster_" + cue.ToString().ToLowerInvariant() + ".wav")));
        int bytes = samples.Length * 2;
        w.Write("RIFF"u8); w.Write(36 + bytes); w.Write("WAVEfmt "u8); w.Write(16); w.Write((short)1); w.Write((short)1);
        w.Write(MonsterAudio.SynthRate); w.Write(MonsterAudio.SynthRate * 2); w.Write((short)2); w.Write((short)16); w.Write("data"u8); w.Write(bytes);
        foreach (float x in samples) w.Write((short)(Math.Clamp(x, -1f, 1f) * 32767f));
    }
    Check(sane, "Every procedural monster cue is finite, audible, unclipped and ends in silence:" + worst);
    var once = MonsterAudio.Render(MonsterAudio.Cue.Hurt); var twice = MonsterAudio.Render(MonsterAudio.Cue.Hurt);
    Check(once.AsSpan().SequenceEqual(twice), "A cue renders the same samples every time (all clients hear the same sound)");
}
Console.WriteLine("MONSTER_CHECKS_PASSED: " + checks);
