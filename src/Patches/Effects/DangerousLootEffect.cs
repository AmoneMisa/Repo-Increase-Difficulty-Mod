using HarmonyLib;
using IncreaseHardMod.Modifiers;
using UnityEngine;

namespace IncreaseHardMod.Patches.Effects;

/// <summary>
/// DangerousLoot modifier — hazardous valuables (cursed dolls, radioactive cups,
/// exploding barrels, saw props, etc.) deal MORE damage to players.
///
/// Those hazard valuables hurt players through a HurtCollider: HurtCollider.
/// PlayerHurt(PlayerAvatar) reads its own "playerDamage" field and calls
/// PlayerHealth.Hurt(playerDamage, ...). We do NOT want to scale every HurtCollider
/// (enemies, level traps, fall/crush colliders also use this class) — only the ones
/// that belong to actual LOOT. A HurtCollider that sits on (or under) a
/// ValuableObject is a hazard-valuable collider, so we gate on
/// GetComponentInParent&lt;ValuableObject&gt;().
///
/// Implementation: a Prefix temporarily multiplies the collider's playerDamage and
/// a Postfix restores it, so the boosted value is what PlayerHurt reads for this one
/// hit and the collider's configured value is untouched afterwards. PlayerHurt only
/// runs for the local player (it early-returns for non-owned avatars), and the
/// synced modifier set is identical on every client, so each player takes the
/// boosted damage consistently. Standalone level traps (LightningTrap, ToiletTrap,
/// …) are not ValuableObjects and are intentionally left alone.
/// </summary>
[HarmonyPatch(typeof(HurtCollider), "PlayerHurt")]
internal static class DangerousLootEffect
{
    private static bool _boosted;
    private static int _originalDamage;

    [HarmonyPrefix]
    private static void Prefix(HurtCollider __instance)
    {
        _boosted = false;

        if (!ModifierManager.Current.Has(ModifierType.DangerousLoot))
            return;

        // Only hazard LOOT: the collider must belong to a valuable.
        if (__instance.GetComponentInParent<ValuableObject>() == null)
            return;

        float factor = Mathf.Max(1f, ModConfig.DangerousLootDamageFactor.Value);
        _originalDamage = __instance.playerDamage;
        __instance.playerDamage = Mathf.Max(_originalDamage, Mathf.RoundToInt(_originalDamage * factor));
        _boosted = true;
    }

    [HarmonyPostfix]
    private static void Postfix(HurtCollider __instance)
    {
        if (!_boosted)
            return;

        __instance.playerDamage = _originalDamage;
        _boosted = false;
    }
}
