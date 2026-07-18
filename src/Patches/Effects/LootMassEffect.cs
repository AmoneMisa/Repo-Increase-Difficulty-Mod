using HarmonyLib;
using IncreaseHardMod.Modifiers;

namespace IncreaseHardMod.Patches.Effects;

/// <summary>
/// HeavyLoot / LightLoot modifiers — change how much valuables weigh.
///
/// We must scale mass AFTER the game sets it, not before. ValuableObject.Start()
/// assigns rb.mass and physGrabObject.massOriginal from its PhysAttributePreset, and
/// that runs after PhysGrabObject.Awake(). Patching Awake would therefore be clobbered
/// (which is exactly why HeavyLoot did nothing). Postfixing ValuableObject.Start()
/// runs once the preset mass is already applied, so our multiplier sticks.
///
/// We scale both the live rigidbody mass and massOriginal, because the game restores
/// mass from massOriginal after temporary physics overrides — scaling only one would
/// get reverted. This targets valuables only (it's a ValuableObject patch), and mass
/// simulates identically on every client, so the effect stays consistent in co-op.
/// </summary>
[HarmonyPatch(typeof(ValuableObject), "Start")]
internal static class LootMassEffect
{
    [HarmonyPostfix]
    private static void Postfix(ValuableObject __instance)
    {
        var mods = ModifierManager.Current;

        float factor;
        if (mods.Has(ModifierType.HeavyLoot))
            factor = ModConfig.HeavyLootFactor.Value;
        else if (mods.Has(ModifierType.LightLoot))
            factor = ModConfig.LightLootFactor.Value;
        else
            return;

        var pgo = __instance.physGrabObject;
        if (pgo == null)
            return;

        if (pgo.rb != null)
            pgo.rb.mass *= factor;
        pgo.massOriginal *= factor;
    }
}
