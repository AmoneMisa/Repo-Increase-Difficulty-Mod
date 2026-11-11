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
Console.WriteLine($"GEAR_CHECKS_PASSED: {checks}");

public class HurtCollider
{
    public int playerDamage, enemyDamage;
    public bool playerKill, enemyKill, enemyStun;
}
