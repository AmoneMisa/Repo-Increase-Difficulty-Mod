namespace IncreaseHardMod.Patches.Effects;

internal static class LateJoinRules
{
    internal static bool ShouldSpawn(bool enabled,bool generated,bool spawned)=>!enabled||!generated||!spawned;
}
