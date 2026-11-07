using HarmonyLib;
using IncreaseHardMod.Hud;
using UnityEngine;
using C = IncreaseHardMod.Skills.SkillCatalog;

namespace IncreaseHardMod.Skills;

/// <summary>
/// Desperation, Hunted Warning and Field Medic. Battery Saver lives in BatteryLifeUpgrade (it shares that
/// file's drain watchdog), Heavy Hitter in CombatDamageUpgrades (the melee hit hook), Lucky Finder in the
/// skill box and battery crystal effects.
/// </summary>
internal static class SupportPerks
{
    // ------------------------------------------------------------------ Desperation (hurt player's own client)

    internal static bool DesperationOn(PlayerHealth health) =>
        health != null && SkillEffects.Has(health.playerAvatar, C.Desperation) &&
        PerkRules.DesperationActive(health.health, health.maxHealth, ModConfig.SkillDesperationThreshold.Value);

    // PlayerHealth.Hurt runs on the hurt player's own client, like Armor. Runs after Armor, so both multiply.
    [HarmonyPatch(typeof(PlayerHealth), nameof(PlayerHealth.Hurt))]
    private static class DesperationHurt
    {
        [HarmonyPrefix]
        [HarmonyPriority(Priority.Low - 1)]
        private static void Prefix(PlayerHealth __instance, ref int damage, bool hurtByHeal)
        {
            if (damage <= 0 || hurtByHeal || __instance.playerAvatar == null) return;
            int level = SkillEffects.Level(__instance.playerAvatar, C.Desperation);
            float cut = PerkRules.DesperationReduction(__instance.health, __instance.maxHealth, level,
                ModConfig.SkillDesperationPerLevel.Value, ModConfig.SkillDesperationThreshold.Value);
            if (cut > 0f) damage = PerkRules.Reduce(damage, cut);
        }
    }

    // ------------------------------------------------------------------ status lines (local player)

    private const string HuntedKey = "ihm.hunted", DesperationKey = "ihm.desperation";

    /// <summary>Per-frame status lines for the local player: HUNTED and DESPERATION.</summary>
    internal sealed class StatusController : MonoBehaviour
    {
        private float _nextScan;
        private bool _hunted;

        private void Update()
        {
            var local = SemiFunc.PlayerAvatarLocal();
            if (local == null || local.isDisabled || !SemiFunc.RunIsLevel())
            {
                _hunted = false;
                return;
            }

            if (SkillEffects.Has(local, C.HuntedWarning))
            {
                if (Time.time >= _nextScan)
                {
                    _nextScan = Time.time + .2f;
                    _hunted = IsHunted(local);
                }
                if (_hunted) HudNotify.Status(HuntedKey, "HUNTED", HudStyle.Bad);
            }
            else _hunted = false;

            if (DesperationOn(local.playerHealth))
                HudNotify.Status(DesperationKey, $"DESPERATION  -{Mathf.RoundToInt(100f * Mathf.Min(.9f, ModConfig.SkillDesperationPerLevel.Value * SkillEffects.Level(local, C.Desperation)))}% DAMAGE", HudStyle.Caution);
        }

        // The host sends every monster's state and target to all players, so any client can read them.
        private static bool IsHunted(PlayerAvatar local)
        {
            var director = EnemyDirector.instance;
            if (director == null || director.enemiesSpawned == null || local.photonView == null) return false;
            int id = local.photonView.ViewID;
            foreach (var parent in director.enemiesSpawned)
            {
                if (parent == null || !parent.Spawned) continue;
                var enemy = parent.Enemy;
                if (enemy == null || enemy.TargetPlayerViewID != id) continue;
                var state = enemy.CurrentState;
                if (state == EnemyState.ChaseBegin || state == EnemyState.Chase || state == EnemyState.ChaseSlow) return true;
            }
            return false;
        }
    }

    // ------------------------------------------------------------------ Field Medic (host)

    // ItemHealthPack.Update heals the player who switched the pack on, on the host. Scale that heal for the
    // user's Field Medic level, then give teammates near them a share once the pack is used up.
    [HarmonyPatch(typeof(ItemHealthPack), "Update")]
    private static class MedicPack
    {
        [HarmonyPrefix]
        private static void Prefix(ItemHealthPack __instance, out (int Heal, bool Used, PlayerAvatar User, int Level) __state)
        {
            __state = (__instance.healAmount, __instance.used, null, 0);
            if (__instance.used || !SemiFunc.IsMasterClientOrSingleplayer() || __instance.itemToggle == null || !__instance.itemToggle.toggleState) return;
            var user = SemiFunc.PlayerAvatarGetFromPhotonID(__instance.itemToggle.playerTogglePhotonID);
            int level = SkillEffects.Level(user, C.FieldMedic);
            if (level <= 0) return;
            __state = (__instance.healAmount, false, user, level);
            __instance.healAmount = PerkRules.MedicSelfHeal(__instance.healAmount, level, ModConfig.SkillMedicPerLevel.Value);
        }

        [HarmonyPostfix]
        private static void Postfix(ItemHealthPack __instance, (int Heal, bool Used, PlayerAvatar User, int Level) __state)
        {
            if (__state.User == null) return;
            __instance.healAmount = __state.Heal;
            if (__state.Used || !__instance.used) return; // not used this frame (rejected at full health, or still waiting)

            int share = PerkRules.MedicShare(__state.Heal, __state.Level, ModConfig.SkillMedicPerLevel.Value);
            float radius = Mathf.Max(0f, ModConfig.SkillMedicRadius.Value);
            var players = SemiFunc.PlayerGetList();
            if (share <= 0 || players == null) return;
            int healed = 0;
            foreach (var mate in players)
            {
                if (mate == null || mate == __state.User || mate.isDisabled || mate.playerHealth == null) continue;
                if (mate.playerHealth.health >= mate.playerHealth.maxHealth) continue;
                if ((mate.transform.position - __state.User.transform.position).sqrMagnitude > radius * radius) continue;
                mate.playerHealth.HealOther(share, effect: true);
                healed++;
            }
            if (healed > 0)
                Plugin.Log.LogInfo($"[Skills] Field Medic: {SemiFunc.PlayerGetName(__state.User)}'s health pack healed {healed} teammate(s) for {share}.");
        }
    }
}
