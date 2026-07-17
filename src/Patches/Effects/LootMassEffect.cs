using HarmonyLib;
using IncreaseHardMod.Modifiers;

namespace IncreaseHardMod.Patches.Effects;

/// <summary>
/// HeavyLoot / LightLoot modifiers — change how much valuables weigh.
///
/// PhysGrabObject.Awake() captures the object's rigidbody mass into massOriginal.
/// We postfix it to scale both the live rigidbody mass and massOriginal (the game
/// restores mass from massOriginal after temporary overrides, so we must scale
/// both or the change would be reverted).
///
/// We only touch valuables — not carts, players, or enemies — by checking for a
/// ValuableObject component. Mass affects physics identically on every client
/// that simulates the object, and the modifier set is synced, so it's consistent.
/// </summary>
[HarmonyPatch(typeof(PhysGrabObject), "Awake")]
internal static class LootMassEffect
{
    [HarmonyPostfix]
    private static void Postfix(PhysGrabObject __instance)
    {
        var mods = ModifierManager.Current;

        float factor;
        if (mods.Has(ModifierType.HeavyLoot))
            factor = ModConfig.HeavyLootFactor.Value;
        else if (mods.Has(ModifierType.LightLoot))
            factor = ModConfig.LightLootFactor.Value;
        else
            return;

        // Restrict to actual loot.
        if (__instance.GetComponent<ValuableObject>() == null)
            return;

        if (__instance.rb != null)
            __instance.rb.mass *= factor;
        __instance.massOriginal *= factor;
    }
}
