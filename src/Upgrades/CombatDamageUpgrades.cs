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
        if (damage <= 0 || Mathf.Approximately(multiplier, 1f)) return damage;
        float scaled = damage * Mathf.Max(0f, multiplier);
        int whole = Mathf.FloorToInt(scaled);
        return Mathf.Max(1, whole + (Random.value < scaled - whole ? 1 : 0)); // a penalty never turns a hit into 0
    }

    // Trade-off stun changes (Overdrive longer, Berserker +1 s on melee); never shortens below what it was.
    private static float TradeOffStun(PlayerAvatar player, float stun, float extraSeconds)
    {
        float factor = Skills.TradeOffs.Factor(player, Skills.Fx.StunLength);
        if (extraSeconds <= 0f && Mathf.Approximately(factor, 1f)) return stun;
        return Mathf.Min((stun + extraSeconds) * factor, Mathf.Max(stun, Skills.SkillCatalog.StatusCapSeconds) + extraSeconds);
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
        private static void Prefix(HurtCollider __instance, Enemy _enemy, out (int Damage, float Stun, PlayerAvatar Wielder, int Dealt, int HealthBefore) __state)
        {
            __state = (__instance.enemyDamage, __instance.enemyStunTime, null, 0,
                _enemy != null && _enemy.HasHealth && _enemy.Health != null ? _enemy.Health.healthCurrent : 0);
            // Guns and cart weapons: the Arsenal perks (damage per weapon, longer tranquilizer stun).
            var weapon = Skills.SkillEffects.WeaponHit(__instance, out var shooter, out bool tranq);
            if (weapon != Skills.SkillEffects.Weapon.None)
            {
                if (tranq && __instance.enemyStun && __instance.enemyStunTime > 0f)
                    __instance.enemyStunTime = TradeOffStun(shooter,
                        Skills.SkillEffects.StatusSeconds(shooter, Skills.SkillEffects.Status.Tranq, __instance.enemyStunTime), 0f);
                bool cartLaser = weapon is Skills.SkillEffects.Weapon.CartLaser or Skills.SkillEffects.Weapon.HandheldLaser;
                if (__instance.enemyDamage > 0)
                    __instance.enemyDamage = Scale(__instance.enemyDamage,
                        Skills.SkillEffects.WeaponMultiplier(shooter, weapon) * Skills.TradeOffs.TargetFactor(shooter, _enemy) *
                        Skills.BuffPerks.DamageFactorFor(shooter, false, cartLaser, true)); // Hot Barrel / Overheat
                Skills.BuffPerks.NoteHit(shooter, _enemy, false, cartLaser, true);
                return;
            }
            if (__instance.enemyDamage <= 0) return;
            // Handheld laser gun: its beam is its own hurt collider (no bullet). Trade-offs and Overheat apply.
            var laserGun = __instance.GetComponentInParent<ItemGunLaser>();
            if (laserGun != null)
            {
                var laserGrab = laserGun.GetComponent<PhysGrabObject>();
                var laserUser = __instance.playerCausingHurt != null ? __instance.playerCausingHurt : laserGrab != null ? laserGrab.lastPlayerGrabbing : null;
                __instance.enemyDamage = Scale(__instance.enemyDamage,
                    Skills.TradeOffs.DamageFactor(laserUser, Skills.TradeOffs.Damage.Gun, _enemy) * Skills.BuffPerks.DamageFactorFor(laserUser, false, true, true));
                Skills.BuffPerks.NoteHit(laserUser, _enemy, false, true, true);
                return;
            }
            // One hook for every player-caused monster hit, so a single saved value is always restored.
            var tumble = TumbleOf(__instance);
            if (tumble != null)
            {
                __instance.enemyDamage = Scale(__instance.enemyDamage, SurvivalUpgrades.TumbleDamageMultiplier(tumble) *
                    Skills.TradeOffs.DamageFactor(tumble.playerAvatar, Skills.TradeOffs.Damage.Tumble, _enemy));
                Skills.BuffPerks.NoteHit(tumble.playerAvatar, _enemy, false, false, false);
                return;
            }
            var melee = MeleeOf(__instance);
            if (melee == null) return;
            var wielder = Wielder(melee);
            __instance.enemyDamage = Scale(__instance.enemyDamage,
                Multiplier(MeleeId, wielder) * Skills.TradeOffs.DamageFactor(wielder, Skills.TradeOffs.Damage.Melee, _enemy) *
                Skills.BuffPerks.DamageFactorFor(wielder, true, false, false)); // Battle Trance
            Skills.BuffPerks.NoteHit(wielder, _enemy, true, false, false);
            __state.Wielder = wielder;
            __state.Dealt = __instance.enemyDamage;
            // Heavy Hitter: a longer stun from the weapon's own stun. Monsters too big for it still aren't stunned.
            int heavy = Skills.SkillEffects.Level(wielder, Skills.SkillCatalog.HeavyHitter);
            if (heavy > 0 && __instance.enemyStun && __instance.enemyStunTime > 0f)
                __instance.enemyStunTime = Skills.PerkRules.MeleeStun(__instance.enemyStunTime, heavy,
                    ModConfig.SkillHeavyHitterPerLevel.Value, Skills.SkillCatalog.StatusCapSeconds);
            if (__instance.enemyStun && __instance.enemyStunTime > 0f)
                __instance.enemyStunTime = TradeOffStun(wielder, __instance.enemyStunTime, Skills.TradeOffs.Sum(wielder, Skills.Fx.MeleeStunSeconds));
        }

        [HarmonyPostfix]
        private static void Postfix(HurtCollider __instance, Enemy _enemy, bool __result, (int Damage, float Stun, PlayerAvatar Wielder, int Dealt, int HealthBefore) __state)
        {
            if (__result && __state.Wielder != null && _enemy != null && _enemy.HasHealth && _enemy.Health != null)
            {
                int dealt = Skills.PerkRules.ActualHitDamage(__state.HealthBefore, _enemy.Health.healthCurrent);
                Skills.SupportPerks.Vampire(__state.Wielder, _enemy, dealt);
            }
            // Thorny Monsters: a melee hit that landed may sting the wielder back.
            if (__result && __state.Wielder != null && __state.Dealt > 0) Modifiers.ThornyMonsters.Reflect(__state.Wielder, _enemy, __state.Dealt);
            __instance.enemyDamage = __state.Damage;
            __instance.enemyStunTime = __state.Stun;
        }
    }

    [HarmonyPatch(typeof(ParticleScriptExplosion), nameof(ParticleScriptExplosion.Spawn))]
    private static class GrenadeExplosion
    {
        [HarmonyPrefix]
        private static void Prefix(ParticleScriptExplosion __instance, ref float size, ref int enemyDamage, bool onlyParticleEffect,
            out int __state)
        {
            __state = 0;
            if (onlyParticleEffect || enemyDamage <= 0) return;
            var grenade = __instance.GetComponent<ItemGrenade>();
            if (grenade == null) return;
            var thrower = grenade.activatorPlayerAvatar != null ? grenade.activatorPlayerAvatar : __instance.playerCausingHurtOverride;
            __state = Skills.GrenadePerks.BlastRank(grenade, thrower);
            size *= Skills.PerkRules.GrenadeRadius(__state);
            enemyDamage = Skills.GrenadePerks.DamageWithGeneralPerks(Skills.PerkRules.GrenadeDamage(enemyDamage, __state), thrower);
        }

        [HarmonyPostfix]
        private static void Postfix(ParticlePrefabExplosion __result, int __state)
        {
            if (__state > 0 && __result != null) Skills.GrenadePerks.BoostStun(__result.HurtCollider, __state);
        }
    }
}
