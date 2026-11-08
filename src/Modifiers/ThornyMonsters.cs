using System.Runtime.CompilerServices;
using UnityEngine;

namespace IncreaseHardMod.Modifiers;

/// <summary>
/// Thorny Monsters surprise: each monster on the level has a medium chance (half, by default) to grow thorns. A
/// melee weapon hit on a thorny monster reflects a small share of the damage it dealt (3-10% by default, rolled
/// once per monster) back to the player swinging it. Guns, grenades, tumbles and carts are never reflected.
/// The host decides (it runs monster damage); the hurt reaches the wielder through the game's own HurtOther.
/// </summary>
internal static class ThornyMonsters
{
    private sealed class Thorns { internal float Share; }
    private static readonly ConditionalWeakTable<Enemy, Thorns> Rolled = new();

    /// <summary>This monster's reflected share of melee damage (0 when it has no thorns). Rolled once per monster.</summary>
    internal static float Share(Enemy enemy)
    {
        if (enemy == null) return 0f;
        return Rolled.GetValue(enemy, _ =>
        {
            float max = Mathf.Clamp(ModConfig.ThornyMonstersMaxReflect.Value, 0f, .5f);
            bool thorny = Random.value < Mathf.Clamp01(ModConfig.ThornyMonstersChance.Value);
            return new Thorns { Share = thorny ? Random.Range(max / 3f, max) : 0f };
        }).Share;
    }

    /// <summary>Host: a melee hit on <paramref name="enemy"/> dealt <paramref name="dealt"/> damage.</summary>
    internal static void Reflect(PlayerAvatar wielder, Enemy enemy, int dealt)
    {
        if (!SemiFunc.IsMasterClientOrSingleplayer() || !ModifierManager.Current.Has(ModifierType.ThornyMonsters)) return;
        if (wielder == null || wielder.playerHealth == null || wielder.deadSet || dealt <= 0) return;
        float share = Share(enemy);
        if (share <= 0f) return;
        int damage = Mathf.Max(1, Mathf.RoundToInt(dealt * share));
        var from = enemy.CenterTransform != null ? enemy.CenterTransform.position : enemy.transform.position;
        wielder.playerHealth.HurtOther(damage, from, savingGrace: true, SemiFunc.EnemyGetIndex(enemy));
    }
}
