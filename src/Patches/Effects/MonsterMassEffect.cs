using HarmonyLib;
using IncreaseHardMod.Modifiers;
using UnityEngine;

namespace IncreaseHardMod.Patches.Effects;

/// <summary>
/// LightMonsters / HeavyMonsters modifiers — clamp how much every monster weighs.
///
/// As with loot, mass must be retuned AFTER the game finalises it, not before. A
/// monster is a non-valuable grabbable, so it carries a NotValuableObject whose Start()
/// assigns rb.mass and physGrabObject.massOriginal from its PhysAttributePreset — and
/// that runs after PhysGrabObject.Awake(). Patching Awake would be clobbered (the same
/// bug that made HeavyLoot do nothing). Postfixing NotValuableObject.Start() runs once
/// the preset mass is already applied, so the clamp sticks.
///
///   - LightMonsters clamps mass DOWN to at most LightMonsterMaxMass (easy shove)
///   - HeavyMonsters clamps mass UP to at least HeavyMonsterMinMass (hard to move)
///
/// We set both the live rigidbody mass and massOriginal, because the game restores mass
/// from massOriginal after temporary overrides. Only monsters are affected (we require an
/// EnemyRigidbody on the object). Mass simulates identically on every client and the
/// modifier set is synced, so behaviour stays consistent in co-op.
/// </summary>
[HarmonyPatch(typeof(NotValuableObject), "Start")]
internal static class MonsterMassEffect
{
    [HarmonyPostfix]
    private static void Postfix(NotValuableObject __instance)
    {
        var mods = ModifierManager.Current;

        bool light = mods.Has(ModifierType.LightMonsters);
        bool heavy = mods.Has(ModifierType.HeavyMonsters);
        if (!light && !heavy)
            return;

        // Only affect monsters (they carry an EnemyRigidbody on the same object).
        if (__instance.GetComponent<EnemyRigidbody>() == null)
            return;

        var pgo = __instance.GetComponent<PhysGrabObject>();
        if (pgo == null || pgo.rb == null)
            return;

        float mass = pgo.rb.mass;

        if (light)
            mass = Mathf.Min(mass, ModConfig.LightMonsterMaxMass.Value);
        else // heavy
            mass = Mathf.Max(mass, ModConfig.HeavyMonsterMinMass.Value);

        pgo.rb.mass = mass;
        pgo.massOriginal = mass;
    }
}
