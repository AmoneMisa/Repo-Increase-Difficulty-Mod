using IncreaseHardMod.Gear;

int checks = 0;
void Check(bool result, string message)
{
    if (!result) throw new Exception(message);
    checks++;
    Console.WriteLine("PASS: " + message);
}

foreach (int damage in new[] { 1, 7, 25, 100 })
{
    // Reproduce a cloned donor retaining lethal friendly fire, independently of enemy damage.
    var hit = new HurtCollider { playerDamage = 100, enemyDamage = 100, playerKill = true, enemyKill = true, enemyStun = true };
    PoisonBoltDamage.Configure(hit, damage);
    Check(hit.playerDamage == damage && !hit.playerKill, $"Poison bolt uses configured {damage} player damage without donor kill flag");
    Check(hit.enemyDamage == damage && !hit.enemyKill && !hit.enemyStun, $"Poison bolt preserves {damage} direct enemy damage without donor kill/stun");
}
Check(GearCombatRules.RefreshFreeze(10f, 0f, 4.5f, out float until) == 20 && until == 14.5f, "First freeze deals 20 damage");
Check(GearCombatRules.RefreshFreeze(12f, until, 4.5f, out until) == 0 && until == 16.5f, "Repeated hit extends freeze without damage");
Check(GearCombatRules.RefreshFreeze(13f, until, .5f, out until) == 0 && until == 16.5f, "Weaker repeat does not shorten freeze");
Check(GearCombatRules.RefreshFreeze(16.5f, until, 4.5f, out until) == 20 && until == 21f, "After thaw a new freeze deals damage again");
foreach (float loss in new[] { .1f, 1f, 500f, 10000f })
    Check(Math.Abs(GearCombatRules.PulledLootLoss(loss) - loss / 5f) < .001f, $"Pull absorbs 80% of {loss} loss");
Check(GearCombatRules.PulledLootLoss(0f) == 0f, "Pull never creates damage");
Check(UtilityGearRules.DefenceCapacity(350) == 450 && UtilityGearRules.DefenceCapacity(600) == 600 &&
      UtilityGearRules.LootCapacity(3500) == 10000, "Utility buffs apply with old defaults while retaining stronger configured capacities");
Check(UtilityGearRules.Protection(25) == .3f && UtilityGearRules.DefenceSeconds(45f) == 90f,
    "Old protection/drone defaults get the new minimum effectiveness");
Check(UtilityGearRules.GhostLoss(1000f) == 500f && UtilityGearRules.GhostLoss(0f) == 0f, "Ghost halves value loss without creating it");
Check(UtilityGearRules.SettledBaseline(100f, 100f, 95f) == 95f, "Continuous drain is settled once, so watchdog cannot refund twice");
Check(UtilityGearRules.SettledBaseline(100f, 90f, 85f) == 95f, "An unrelated ten-point drain remains outstanding after settling the continuous five-point cost");
Check(UtilityGearRules.SettledBaseline(float.NaN, 70f, 65f) == 65f && UtilityGearRules.SettledBaseline(70f, 70f, 75f) == 70f,
    "First consumption and charging do not create a second drain");

// ---- Gun battery: shots versus bars (mirrors ItemBattery.BatteryBarsGet / RemoveFullBar and ItemGun.Shoot) ----
static int ShownBars(float life, int bars) => Math.Clamp((int)Math.Round(life / (100f / bars)), 0, bars); // Mathf.RoundToInt: to even
// Fires until the gun refuses (zero bars shown); checks the bars shown before each shot equal the shots still to come.
static (int shots, bool agreed, float left) Empty(float life, int bars, bool fullBar, float drain)
{
    var shown = new List<int>();
    while (ShownBars(life, bars) > 0 && shown.Count < 500)
    {
        shown.Add(ShownBars(life, bars));
        if (fullBar) { int left = ShownBars(life, bars) - 1; life = left <= 0 ? 0f : left * (100f / bars); }
        else life = Math.Max(0f, life - drain);
    }
    bool agreed = true;
    for (int i = 0; i < shown.Count; i++) agreed &= shown[i] == shown.Count - i;
    return (shown.Count, agreed, life);
}
// Before the fix: the crossbow drained 1.5x the handgun's percentage (15 shots -> 10% a shot) whatever its bars.
var old6 = Empty(100f, 6, false, 100f / 15f * 1.5f);
var old5 = Empty(100f, 5, false, 100f / 15f * 1.5f);
Check(!old6.agreed && old5.left > 0f,
    $"Old percent-drain crossbow: bars disagreed with shots, and it could stop with {old5.left:0}% charge left");
float handgun = UtilityGearRules.ShotsPerCharge(true, false, 1, 6, 100f / 15f);
Check(handgun == 15f && UtilityGearRules.PoisonShots(handgun) == 10 && UtilityGearRules.WholeShots(handgun) == 15,
    "Handgun donor: 15 shots; crossbow keeps a third fewer (10), sticky keeps 15");
Check(UtilityGearRules.ShotsPerCharge(false, false, 1, 5, 2f) == 5f && UtilityGearRules.SpendsWholeBars(false, false),
    "A gun that fires through its shooting state spends one bar per shot, whatever its percent drain says");
foreach (int bars in new[] { 1, 8, 10, 15, 13, 40 })
    foreach (float start in new[] { 100f, 20f, 37f, 51f, 3f })
    {
        var run = Empty(start, bars, true, 0f);
        Check(run.agreed && run.shots == ShownBars(start, bars),
            $"One bar per shot: {bars} bars from {start}% fire exactly the {ShownBars(start, bars)} shown, bars always = shots left");
    }
Check(UtilityGearRules.UpgradedBars(10, 10f, 0f, 1f, 1, 40) == 10, "No upgrade keeps the native bars");
Check(UtilityGearRules.UpgradedBars(10, 10f, 10f / 15f * 6f, 1f, 1, 40) == 14, "Six handgun-levels of extra shots give the crossbow 4 more whole bars");
Check(UtilityGearRules.UpgradedBars(10, 10f, 0f, .7f, 1, 40) == 14 && UtilityGearRules.UpgradedBars(15, 15f, 0f, 1.25f, 1, 40) == 12,
    "Battery trade-offs change the bar count, not the cost of a shot (Overclock 10->14, Overdrive 15->12)");
Check(UtilityGearRules.UpgradedBars(15, 15f, 30f, .7f, 1, 40) == 40 && UtilityGearRules.UpgradedBars(4, 2f, 3f, 1f, 2, 9) == 8,
    "Upgraded bars respect the readable cap and stay a whole number of shots for multi-bar guns");
Console.WriteLine($"GEAR_CHECKS_PASSED: {checks}");

public class HurtCollider
{
    public int playerDamage, enemyDamage;
    public bool playerKill, enemyKill, enemyStun;
}
