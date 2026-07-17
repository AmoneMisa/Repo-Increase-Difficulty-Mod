using HarmonyLib;
using IncreaseHardMod.Modifiers;
using UnityEngine;

namespace IncreaseHardMod.Patches.Effects;

/// <summary>
/// SoulsExplodeHarder modifier — enemy-soul explosions (which only occur on moon 4+)
/// hit players harder.
///
/// A soul's explosion is a self-contained prefab GameObject on EnemyValuable
/// (enemyValuableExplosion) that gets SetActive'd when the soul is destroyed; the
/// EnemyValuableExplosion script on it drives only sound + camera shake, while the
/// actual damage is dealt by a HurtCollider on that same explosion object — either
/// directly (inspector-set HurtCollider.playerDamage) or via a ParticlePrefabExplosion
/// whose Update copies its explosionDamage onto the HurtCollider a frame later.
///
/// EnemyValuableExplosion.Start runs the instant that explosion object activates,
/// which is the perfect, soul-SPECIFIC hook: postfixing it lets us scale the damage
/// without touching grenades, barrels or any other explosion (those never carry an
/// EnemyValuableExplosion). By the time Start runs the object has been unparented
/// (DestroyImpulse sets parent = null), so transform.root is the explosion object and
/// GetComponentsInChildren finds its damage components.
///
/// We scale BOTH possible sources: ParticlePrefabExplosion.explosionDamage (the value
/// that gets written to the collider next frame — scaling it here means the boosted
/// number is what lands) AND any HurtCollider.playerDamage directly (covers a
/// standalone collider with no ParticlePrefabExplosion). We log what we found so the
/// exact prefab layout can be confirmed in-game.
/// </summary>
[HarmonyPatch(typeof(EnemyValuableExplosion), "Start")]
internal static class SoulsExplodeHarderEffect
{
    [HarmonyPostfix]
    private static void Postfix(EnemyValuableExplosion __instance)
    {
        if (!ModifierManager.Current.Has(ModifierType.SoulsExplodeHarder))
            return;

        float factor = Mathf.Max(1f, ModConfig.SoulsExplodeHarderFactor.Value);
        if (factor <= 1f)
            return;

        var root = __instance.transform.root;

        int scaledPpe = 0;
        foreach (var ppe in root.GetComponentsInChildren<ParticlePrefabExplosion>(true))
        {
            if (ppe.explosionDamage > 0)
            {
                ppe.explosionDamage = Mathf.RoundToInt(ppe.explosionDamage * factor);
                scaledPpe++;
            }
        }

        int scaledHc = 0;
        // Only scale colliders directly when nothing will overwrite them from a
        // ParticlePrefabExplosion; otherwise the PPE scaling above already covers it.
        if (scaledPpe == 0)
        {
            foreach (var hc in root.GetComponentsInChildren<HurtCollider>(true))
            {
                if (hc.playerDamage > 0)
                {
                    hc.playerDamage = Mathf.RoundToInt(hc.playerDamage * factor);
                    scaledHc++;
                }
            }
        }

        Plugin.Log.LogInfo($"[SoulsExplodeHarder] Boosted soul explosion x{factor} " +
                           $"(ParticlePrefabExplosions={scaledPpe}, HurtColliders={scaledHc}).");
    }
}
