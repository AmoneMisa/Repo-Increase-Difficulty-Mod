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
Console.WriteLine($"GEAR_CHECKS_PASSED: {checks}");

public class HurtCollider
{
    public int playerDamage, enemyDamage;
    public bool playerKill, enemyKill, enemyStun;
}
