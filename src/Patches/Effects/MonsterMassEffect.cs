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
    private sealed class MassState
    {
        internal float Original, Applied, ImpactScale;
    }
    private static readonly ConditionalWeakTable<PhysGrabObject, MassState> Masses = new();

    [HarmonyPostfix]
    private static void Postfix(NotValuableObject __instance)
    {
        // Only affect monsters (they carry an EnemyRigidbody on the same object).
        if (__instance.GetComponent<EnemyRigidbody>() == null)
            return;
        var pgo = __instance.GetComponent<PhysGrabObject>();
        // FixedUpdate may have observed the prefab before Start applied its mass preset.
        if (pgo != null) Masses.Remove(pgo);
        RefreshMass(pgo);
    }

    internal static void RefreshMass(PhysGrabObject pgo)
    {
        if (pgo == null || pgo.rb == null)
            return;
        var state = Masses.GetValue(pgo, key =>
        {
            float baseline = key.massOriginal > 0f ? key.massOriginal : key.rb.mass;
            return new MassState { Original = baseline, Applied = baseline };
        });
        var mods = ModifierManager.Current;
        bool light = EnemyBalance.Surprises && mods.Has(ModifierType.LightMonsters);
        bool heavy = EnemyBalance.Surprises && mods.Has(ModifierType.HeavyMonsters);
        float original = state.Original;
        float mass = original;

        if (light)
            mass = Mathf.Min(mass, EnemyBalance.LightMass);
        else if (heavy)
            mass = Mathf.Max(mass, EnemyBalance.HeavyMass);

        // New moon VI makes bodies heavier after native presets and surprise clamps.
        // Recompute from the stored baseline; never multiply the previous tick's mass.
        if (Economy.ExtendedMoons.Current >= Economy.MoonRules.RandomSouls) mass *= 1.5f;

        if (!Mathf.Approximately(state.Applied, mass))
            pgo.rb.mass *= mass / Mathf.Max(0.01f, state.Applied);
        pgo.massOriginal = mass;
        state.Applied = mass;
        // Temporary native overrides and late modifier snapshots must not restore a heavy body.
        if (light && pgo.rb.mass > mass) pgo.rb.mass = mass;

        // LightMonsters only: keeping the rigidbody light makes them easy to shove, but it also
        // scales down REPO's impactForce (∝ mass) while the impact-DAMAGE thresholds (20/80/150)
        // stay fixed — so gentle slams stop dealing damage. Record how much the clamp reduced the
        // mass so LightMonsterImpactDamageFix can restore impactForce to its full-weight value.
        {
            var detector = pgo.GetComponent<PhysGrabObjectImpactDetector>();
            float ratio = light && mass < original && mass > 0f ? original / mass : 1f;
            if (detector != null && !Mathf.Approximately(state.ImpactScale, ratio))
            {
                LightMonsterImpactDamageFix.Record(detector, ratio);
                state.ImpactScale = ratio;
            }
        }
    }
}

/// <summary>Light monsters yield to one player's grab instead of fighting it with AI follow forces.</summary>
[HarmonyPatch(typeof(EnemyRigidbody), "FixedUpdate")]
internal static class LightMonsterGrabEffect
{
    [HarmonyPrefix]
    private static void Prefix(EnemyRigidbody __instance)
    {
        var pgo = __instance.physGrabObject;
        MonsterMassEffect.RefreshMass(pgo);
        if (!SemiFunc.IsMasterClientOrSingleplayer() || pgo == null || !pgo.spawned ||
            !EnemyBalance.Surprises ||
            Economy.ExtendedMoons.Current >= Economy.MoonRules.RandomSouls ||
            !ModifierManager.Current.Has(ModifierType.LightMonsters) || pgo.rb == null || pgo.rb.isKinematic ||
            pgo.playerGrabbing.Count == 0 || __instance.mountedGrabOverrideTimer > 0f ||
            __instance.enemy == null || __instance.enemy.FreezeTimer > 0f || Gear.GearEffects.IsFrozen(__instance.enemy))
            return;

        // Use the game's grab override and synced GrabbedSet path; leave its max hold time,
        // shake release, mounted behavior and freeze restrictions intact.
        __instance.grabStrengthTimer = Mathf.Max(__instance.grabStrengthTimer, 0.15f);
        __instance.DeactivateFollowTargetPhysics(0.1f);
        __instance.DeactivateCustomGravity(0.1f);
        pgo.OverrideZeroGravity(0.1f);
    }
}

[HarmonyPatch(typeof(PhysGrabObject), "PhysicsGrabbingManipulation")]
internal static class MonsterGrabMassRefresh
{
    [HarmonyPrefix]
    private static void Prefix(PhysGrabObject __instance)
    {
        if (__instance.GetComponent<EnemyRigidbody>() != null) MonsterMassEffect.RefreshMass(__instance);
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
/// before the physics step delivers the collision that reads it, restoring the equivalent native
/// force using the original/clamped mass ratio. The current mass and the native mass floor are
/// included so stun-time overrides and caps below one do not over-amplify impact damage.
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
        if (!EnemyBalance.Surprises || !ModifierManager.Current.Has(ModifierType.LightMonsters))
            return;
        if (Scales.TryGetValue(__instance, out var box) && box.Value > 1f)
        {
            var body = __instance.GetComponent<Rigidbody>();
            if (body == null) return;
            // The game's Max(mass, 1) means the ratio changes when a stun or a very low
            // configured cap takes the current mass below one. Do not amplify impacts twice.
            float factor = Mathf.Max(body.mass * box.Value, 1f) / Mathf.Max(body.mass, 1f);
            __instance.impactForce *= factor;
        }
    }
}
