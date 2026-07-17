using HarmonyLib;
using IncreaseHardMod.Modifiers;
using UnityEngine;

namespace IncreaseHardMod.Patches.Effects;

/// <summary>
/// LightMonsters / HeavyMonsters modifiers — clamp how much every monster weighs.
///
/// Monsters are physics objects too: the same GameObject that has a PhysGrabObject
/// also carries an EnemyRigidbody (EnemyRigidbody.physGrabObject = GetComponent
/// &lt;PhysGrabObject&gt;()). So a Postfix on PhysGrabObject.Awake that detects an
/// EnemyRigidbody lets us retune monster mass the same way we retune loot mass.
///
///   - LightMonsters clamps mass DOWN to at most LightMonsterMaxMass (easy shove)
///   - HeavyMonsters clamps mass UP to at least HeavyMonsterMinMass (hard to move)
///
/// We set both the live rigidbody mass and massOriginal, because the game restores
/// mass from massOriginal after temporary overrides. Mass simulates identically on
/// every client and the modifier set is synced, so behavior stays consistent.
/// </summary>
[HarmonyPatch(typeof(PhysGrabObject), "Awake")]
internal static class MonsterMassEffect
{
    [HarmonyPostfix]
    private static void Postfix(PhysGrabObject __instance)
    {
        var mods = ModifierManager.Current;

        bool light = mods.Has(ModifierType.LightMonsters);
        bool heavy = mods.Has(ModifierType.HeavyMonsters);
        if (!light && !heavy)
            return;

        // Only affect monsters (they carry an EnemyRigidbody on the same object).
        if (__instance.GetComponent<EnemyRigidbody>() == null)
            return;

        if (__instance.rb == null)
            return;

        float mass = __instance.rb.mass;

        if (light)
            mass = Mathf.Min(mass, ModConfig.LightMonsterMaxMass.Value);
        else // heavy
            mass = Mathf.Max(mass, ModConfig.HeavyMonsterMinMass.Value);

        __instance.rb.mass = mass;
        __instance.massOriginal = mass;
    }
}
