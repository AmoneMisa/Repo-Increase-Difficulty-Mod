namespace IncreaseHardMod.Gear;

internal static class PoisonBoltDamage
{
    internal static void Configure(HurtCollider hurt, int directDamage)
    {
        // The donor handgun has separate damage and kill flags for players and
        // enemies. Overriding only enemyDamage left full handgun friendly fire.
        hurt.enemyDamage = directDamage;
        hurt.enemyKill = false;
        hurt.enemyStun = false;
        hurt.playerDamage = directDamage;
        hurt.playerKill = false;
    }
}
