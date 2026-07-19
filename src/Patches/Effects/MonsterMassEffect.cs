using System.Runtime.CompilerServices;
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

        float original = pgo.rb.mass;
        float mass = original;

        if (light)
            mass = Mathf.Min(mass, ModConfig.LightMonsterMaxMass.Value);
        else // heavy
            mass = Mathf.Max(mass, ModConfig.HeavyMonsterMinMass.Value);

        pgo.rb.mass = mass;
        pgo.massOriginal = mass;

        // LightMonsters only: keeping the rigidbody light makes them easy to shove, but it also
        // scales down REPO's impactForce (∝ mass) while the impact-DAMAGE thresholds (20/80/150)
        // stay fixed — so gentle slams stop dealing damage. Record how much the clamp reduced the
        // mass so LightMonsterImpactDamageFix can restore impactForce to its full-weight value.
        if (light && mass < original && mass > 0f)
        {
            var detector = pgo.GetComponent<PhysGrabObjectImpactDetector>();
            if (detector != null)
                LightMonsterImpactDamageFix.Record(detector, original / mass);
        }
    }
}

/// <summary>
/// Companion to <see cref="MonsterMassEffect"/>. LightMonsters clamps monster mass so they're easy
/// to shove, but REPO computes <c>impactForce ∝ Max(rb.mass, 1)</c> and then compares it against the
/// FIXED impact-damage thresholds 20 / 80 / 150 (<c>PhysGrabObjectImpactDetector</c> collision handler).
/// A mass-3 monster therefore needs a far bigger speed change to register the same impact tier, so
/// glancing wall slams deal no damage even though harder "downward" slams still do. (The break-damage
/// path is unaffected because its threshold also scales with mass, cancelling out.)
///
/// This postfix runs right after <c>FixedUpdate</c> recomputes <c>impactForce</c> for the frame and
/// before the physics step delivers the collision that reads it, multiplying it back up by the
/// original/clamped mass ratio captured at spawn. The rigidbody stays light (easy to shove); only the
/// force the damage thresholds see is restored to full-weight. Ratio-based so it's independent of the
/// stun-time mass override (which multiplies light and normal monsters alike).
/// </summary>
[HarmonyPatch(typeof(PhysGrabObjectImpactDetector), "FixedUpdate")]
internal static class LightMonsterImpactDamageFix
{
    // Compensation factor (originalMass / clampedMass) per detector. ConditionalWeakTable lets the
    // GC drop entries when a monster is destroyed on level change — no manual cleanup, no leak.
    private static readonly ConditionalWeakTable<PhysGrabObjectImpactDetector, StrongBox<float>> Scales = new();

    internal static void Record(PhysGrabObjectImpactDetector detector, float scale)
    {
        Scales.Remove(detector);
        Scales.Add(detector, new StrongBox<float>(scale));
    }

    [HarmonyPostfix]
    private static void Postfix(PhysGrabObjectImpactDetector __instance)
    {
        if (__instance.impactForce <= 0f)
            return;
        if (!ModifierManager.Current.Has(ModifierType.LightMonsters))
            return;
        if (Scales.TryGetValue(__instance, out var box) && box.Value > 1f)
            __instance.impactForce *= box.Value;
    }
}
