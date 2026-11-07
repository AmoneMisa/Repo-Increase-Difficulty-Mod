using System.Runtime.CompilerServices;
using HarmonyLib;
using UnityEngine;
using RepoUpgrades = REPOLib.Modules.Upgrades;

namespace IncreaseHardMod.Upgrades;

/// <summary>
/// Per-player "Melee Damage" and "Grenade Damage" upgrades: each level adds a configurable
/// percentage (default +5%) to the monster damage dealt by that player's melee weapons /
/// thrown grenades. Player (PVP / friendly-fire) damage is left unchanged.
///
/// Melee: HurtCollider.EnemyHurt is the single place a weapon's hurt collider damages a monster
/// (host only, guarded by enemyLogic). We scale <c>enemyDamage</c> for that call and restore it.
/// Grenades: every damaging grenade (explosive, duct-taped cluster and its spawned children)
/// goes through ParticleScriptExplosion.Spawn on an object that has an ItemGrenade with an
/// activator, so its enemyDamage argument is scaled there. Mines and other explosions are untouched.
/// </summary>
internal static class CombatDamageUpgrades
{
    public const string MeleeId = "IHM_MeleeDamage";
    public const string GrenadeId = "IHM_GrenadeDamage";

    private static readonly ConditionalWeakTable<HurtCollider, MeleeRef> MeleeOwners = new();
    private sealed class MeleeRef { internal ItemMelee Melee; }

    public static void Register()
    {
        if (!ModConfig.PlayerUpgradesEnabled.Value) return;
        PlayerUpgradeRegistry.Define(new PlayerUpgradeRegistry.Definition
        {
            Id = MeleeId,
            DisplayName = "Melee Damage",
            MaxLevel = ModConfig.MeleeDamageMaxLevel.Value,
        });
        PlayerUpgradeRegistry.Define(new PlayerUpgradeRegistry.Definition
        {
            Id = GrenadeId,
            DisplayName = "Grenade Damage",
            MaxLevel = ModConfig.GrenadeDamageMaxLevel.Value,
        });
    }

    internal static float Multiplier(string id, PlayerAvatar player)
    {
        if (!ModConfig.PlayerUpgradesEnabled.Value || player == null || string.IsNullOrEmpty(player.steamID)) return 1f;
        bool melee = id == MeleeId;
        int cap = Mathf.Clamp(melee ? ModConfig.MeleeDamageMaxLevel.Value : ModConfig.GrenadeDamageMaxLevel.Value, 1, 100);
        // Skill tree: the node on the old ladder of `cap` levels, plus Brawler's small bonus.
        float level = Mathf.Clamp(Skills.SkillEffects.Equivalent(player, melee ? Skills.SkillCatalog.MeleeDamage : Skills.SkillCatalog.GrenadeDamage, cap), 0f, cap);
        float percent = Mathf.Clamp(melee ? ModConfig.MeleeDamagePercentPerLevel.Value : ModConfig.GrenadeDamagePercentPerLevel.Value, 0f, 1f);
        float root = Skills.SkillEffects.Has(player, Skills.SkillCatalog.Brawler) ? Skills.SkillEffects.RootBonus : 0f;
        return 1f + percent * level + root;
    }

    // Small weapon damage values would lose a +5% bonus to integer rounding; round up with the
    // fractional probability so the average bonus is exact. Host-only, so no sync is needed.
    private static int Scale(int damage, float multiplier)
    {
        if (damage <= 0 || multiplier <= 1f) return damage;
        float scaled = damage * multiplier;
        int whole = Mathf.FloorToInt(scaled);
        return whole + (Random.value < scaled - whole ? 1 : 0);
    }

    private static readonly ConditionalWeakTable<HurtCollider, TumbleRef> TumbleOwners = new();
    private sealed class TumbleRef { internal PlayerTumble Tumble; }

    // The tumble's own hurt collider (PlayerTumble.hurtCollider), not anything a tumbling player touches.
    private static PlayerTumble TumbleOf(HurtCollider collider) =>
        TumbleOwners.GetValue(collider, c =>
        {
            var t = c.GetComponentInParent<PlayerTumble>();
            return new TumbleRef { Tumble = t != null && t.hurtCollider == c ? t : null };
        }).Tumble;

    private static ItemMelee MeleeOf(HurtCollider collider)
    {
        var entry = MeleeOwners.GetValue(collider, c => new MeleeRef { Melee = c.GetComponentInParent<ItemMelee>() });
        return entry.Melee;
    }

    private static PlayerAvatar Wielder(ItemMelee melee)
    {
        var grab = melee.physGrabObject;
        if (grab == null) return null;
        if (grab.playerGrabbing.Count > 0 && grab.playerGrabbing[0] != null) return grab.playerGrabbing[0].playerAvatar;
        return grab.lastPlayerGrabbing;
    }

    [HarmonyPatch(typeof(HurtCollider), "EnemyHurt")]
    private static class MeleeEnemyHurt
    {
        [HarmonyPrefix]
        private static void Prefix(HurtCollider __instance, out (int Damage, float Stun) __state)
        {
            __state = (__instance.enemyDamage, __instance.enemyStunTime);
            // Guns and cart weapons: the Arsenal perks (damage per weapon, longer tranquilizer stun).
            var weapon = Skills.SkillEffects.WeaponHit(__instance, out var shooter, out bool tranq);
            if (weapon != Skills.SkillEffects.Weapon.None)
            {
                if (tranq && __instance.enemyStun && __instance.enemyStunTime > 0f)
                    __instance.enemyStunTime = Skills.SkillEffects.StatusSeconds(shooter, Skills.SkillEffects.Status.Tranq, __instance.enemyStunTime);
                if (__instance.enemyDamage > 0)
                    __instance.enemyDamage = Scale(__instance.enemyDamage, Skills.SkillEffects.WeaponMultiplier(shooter, weapon));
                return;
            }
            if (__instance.enemyDamage <= 0) return;
            // One hook for every player-caused monster hit, so a single saved value is always restored.
            var tumble = TumbleOf(__instance);
            if (tumble != null)
            {
                __instance.enemyDamage = Scale(__instance.enemyDamage, SurvivalUpgrades.TumbleDamageMultiplier(tumble));
                return;
            }
            var melee = MeleeOf(__instance);
            if (melee == null) return;
            var wielder = Wielder(melee);
            __instance.enemyDamage = Scale(__instance.enemyDamage, Multiplier(MeleeId, wielder));
            // Heavy Hitter: a longer stun from the weapon's own stun. Monsters too big for it still aren't stunned.
            int heavy = Skills.SkillEffects.Level(wielder, Skills.SkillCatalog.HeavyHitter);
            if (heavy > 0 && __instance.enemyStun && __instance.enemyStunTime > 0f)
                __instance.enemyStunTime = Skills.PerkRules.MeleeStun(__instance.enemyStunTime, heavy,
                    ModConfig.SkillHeavyHitterPerLevel.Value, Skills.SkillCatalog.StatusCapSeconds);
        }

        [HarmonyPostfix]
        private static void Postfix(HurtCollider __instance, (int Damage, float Stun) __state)
        {
            __instance.enemyDamage = __state.Damage;
            __instance.enemyStunTime = __state.Stun;
        }
    }

    [HarmonyPatch(typeof(ParticleScriptExplosion), nameof(ParticleScriptExplosion.Spawn))]
    private static class GrenadeExplosion
    {
        [HarmonyPrefix]
        private static void Prefix(ParticleScriptExplosion __instance, ref int enemyDamage, bool onlyParticleEffect)
        {
            if (onlyParticleEffect || enemyDamage <= 0) return;
            var grenade = __instance.GetComponent<ItemGrenade>();
            if (grenade == null) return;
            var thrower = grenade.activatorPlayerAvatar != null ? grenade.activatorPlayerAvatar : __instance.playerCausingHurtOverride;
            float multiplier = Multiplier(GrenadeId, thrower);
            if (multiplier > 1f) enemyDamage = Mathf.RoundToInt(enemyDamage * multiplier);
        }
    }
}
