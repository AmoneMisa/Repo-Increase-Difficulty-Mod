using HarmonyLib;
using IncreaseHardMod.Modifiers;

namespace IncreaseHardMod.Patches.Effects;

/// <summary>
/// FragileLoot / ProtectedLoot modifiers.
///
/// PhysGrabObjectImpactDetector.Start() reads the object's "fragility" from its
/// durability preset. In the break math (breakForce *= fragility/100; then break
/// levels trigger when breakForce exceeds thresholds), HIGHER fragility = breaks
/// more easily. So:
///   - FragileLoot   multiplies fragility up   (breaks sooner)
///   - ProtectedLoot multiplies fragility down (survives more hits)
///
/// A [HarmonyPostfix] runs after Start, once per object, so the change persists
/// for that valuable's whole life. Every client runs Start on its own copy and
/// reads the same synced modifier set, so break behavior stays consistent.
/// </summary>
[HarmonyPatch(typeof(PhysGrabObjectImpactDetector), "Start")]
internal static class LootFragilityEffect
{
    [HarmonyPostfix]
    private static void Postfix(PhysGrabObjectImpactDetector __instance)
    {
        // Only valuables have break logic worth tuning.
        if (!__instance.isValuable)
            return;

        var mods = ModifierManager.Current;

        if (mods.Has(ModifierType.FragileLoot))
            __instance.fragility *= ModConfig.FragileLootFactor.Value;
        else if (mods.Has(ModifierType.ProtectedLoot))
            __instance.fragility *= ModConfig.ProtectedLootFactor.Value;
    }
}
