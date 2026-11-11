namespace IncreaseHardMod.Patches.Effects;

internal static class LateJoinRules
{
    internal static bool ShouldSpawn(bool enabled,bool generated,bool spawned)=>!enabled||!generated||!spawned;

    /// <summary>
    /// Health after raising a stale max health: a player who was only clamped by the old maximum gets back the
    /// host's saved health (never above the new maximum); a wounded player keeps exactly their current health.
    /// </summary>
    internal static int ReconciledHealth(int current,int staleMax,int saved,int newMax)
    {
        if(newMax<=0)return current;
        bool clamped=current>=staleMax&&saved>current;
        int health=clamped?saved:current;
        return health<0?0:health>newMax?newMax:health;
    }
}
