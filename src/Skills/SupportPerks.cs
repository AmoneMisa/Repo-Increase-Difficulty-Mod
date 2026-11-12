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
            if (damage <= 0 || hurtByHeal || Monsters.MonsterAfflictions.DotDamage > 0 || __instance.playerAvatar == null) return;
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
            CrouchSpeedPerk.Tick();
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
                if (_hunted) HudNotify.Status(HuntedKey, SkillText.Russian ? "ПОГОНЯ" : "HUNTED", HudStyle.Bad);
            }
            else _hunted = false;

            if (DesperationOn(local.playerHealth))
                HudNotify.Status(DesperationKey, $"{(SkillText.Russian ? "ОТЧАЯНИЕ" : "DESPERATION")}  -{Mathf.RoundToInt(100f * Mathf.Min(.9f, ModConfig.SkillDesperationPerLevel.Value * SkillEffects.Level(local, C.Desperation)))}%{(SkillText.Russian ? " УРОНА" : " DAMAGE")}", HudStyle.Caution);
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

    // ------------------------------------------------------------------ Vampirism (host)

    private static readonly System.Collections.Generic.Dictionary<string, float> VampireCarry = new();

    /// <summary>Host: a melee hit for <paramref name="damage"/> heals its wielder by their Vampirism share. Only the damage
    /// the monster can still take counts, so overkill on a nearly dead monster heals nothing extra.</summary>
    internal static void Vampire(PlayerAvatar wielder, Enemy target, int damage)
    {
        if (!SemiFunc.IsMasterClientOrSingleplayer() || wielder == null || wielder.isDisabled || string.IsNullOrEmpty(wielder.steamID)) return;
        int level = SkillEffects.Level(wielder, C.Vampirism);
        if (level <= 0 || damage <= 0 || wielder.playerHealth == null) return;
        // Caller supplies the confirmed health delta after EnemyHurt, including a
        // killing blow. Capping by remaining HP here would erase healing on kills.
        float share = PerkRules.VampireShare(level, C.Find(C.Vampirism).MaxLevel, ModConfig.SkillVampireMax.Value);
        VampireCarry.TryGetValue(wielder.steamID, out float carry);
        int heal = PerkRules.VampireHeal(damage, share, ref carry);
        VampireCarry[wielder.steamID] = carry;
        var health = wielder.playerHealth;
        if (heal > 0 && health.health > 0 && health.health < health.maxHealth)
            health.HealOther(Mathf.Min(heal, health.maxHealth - health.health), effect: false);
    }

    // ------------------------------------------------------------------ Field Medic (host)

    // ItemHealthPack.Update heals the player who switched the pack on, on the host. Scale that heal for the
    // user's Field Medic level, then give teammates near them a share once the pack is used up.
    [HarmonyPatch(typeof(ItemHealthPack), nameof(ItemHealthPack.Update))]
    private static class MedicPack
    {
        [HarmonyPrefix]
        private static void Prefix(ItemHealthPack __instance, out (int Heal, bool Used, PlayerAvatar User, int Level) __state)
        {
            __state = (__instance.healAmount, __instance.used, null, 0);
            if (__instance.used || !SemiFunc.IsMasterClientOrSingleplayer() || __instance.itemToggle == null || !__instance.itemToggle.toggleState) return;
            var user = SemiFunc.PlayerAvatarGetFromPhotonID(__instance.itemToggle.playerTogglePhotonID);
            int level = SkillEffects.Level(user, C.FieldMedic);
            float self = TradeOffs.Factor(user, Fx.SelfHeal); // Second Heart / Medic's Oath: packs heal you less
            if (level <= 0 && Mathf.Approximately(self, 1f)) return;
            __state = (__instance.healAmount, false, user, level);
            int heal = level > 0 ? PerkRules.MedicSelfHeal(__instance.healAmount, level, PerkRules.MedicRate(ModConfig.SkillMedicPerLevel.Value)) : __instance.healAmount;
            __instance.healAmount = Mathf.Max(1, Mathf.RoundToInt(heal * self));
        }

        [HarmonyPostfix]
        private static void Postfix(ItemHealthPack __instance, (int Heal, bool Used, PlayerAvatar User, int Level) __state)
        {
            if (__state.User == null) return;
            __instance.healAmount = __state.Heal;
            if (__state.Used || !__instance.used || __state.Level <= 0) return; // not used this frame, or no Field Medic

            int share = Mathf.RoundToInt(PerkRules.MedicShare(__state.Heal, __state.Level, PerkRules.MedicRate(ModConfig.SkillMedicPerLevel.Value)) *
                                         TradeOffs.Factor(__state.User, Fx.MedicShare)); // Medic's Oath
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

/// <summary>
/// Local player's movement and grip from the skill tree: Crouch Speed, the speed trade-offs (Sprinter, Thick Skin,
/// Juggernaut, Daredevil, Ghost Runner, Salvager while holding loot) and Overclock's weaker grip. The game moves a
/// player by PlayerController.MoveSpeed / SprintSpeed / CrouchSpeed and resets them from their playerOriginal* copies
/// after speed overrides (slows, hurt, carrying), so each pair is scaled together, once per change of the factor.
/// Additions the game or a quest makes later stay unscaled, which keeps every change reversible.
/// </summary>
internal static class CrouchSpeedPerk
{
    private static PlayerController _controller;
    private static PhysGrabber _grabber;
    private static float _walk = 1f, _sprint = 1f, _crouch = 1f, _grip = 1f;

    internal static void Tick()
    {
        var pc = PlayerController.instance;
        if (pc == null) { _controller = null; _walk = _sprint = _crouch = 1f; return; }
        if (pc != _controller) { _controller = pc; _walk = _sprint = _crouch = 1f; } // a new level builds a new controller

        var local = SemiFunc.PlayerAvatarLocal();
        int level = local != null ? SkillEffects.Level(local, SkillCatalog.CrouchSpeed) : 0;
        var grabber = local != null ? local.physGrabber : null;
        bool loot = grabber != null && grabber.grabbed && grabber.grabbedPhysGrabObject != null &&
                    grabber.grabbedPhysGrabObject.GetComponent<ValuableObject>() != null;
        float holding = loot ? TradeOffs.Factor(local, Fx.HoldingLootSpeed) : 1f;

        float adrenaline = BuffPerks.LocalSpeed; // Adrenaline instinct
        float walk = TradeOffs.Factor(local, Fx.WalkSpeed) * holding * adrenaline;
        float sprint = TradeOffs.Factor(local, Fx.SprintSpeed) * holding * adrenaline;
        float crouch = PerkRules.CrouchSpeedFactor(level, ModConfig.SkillCrouchSpeedPerLevel.Value) * TradeOffs.Factor(local, Fx.CrouchSpeed) * holding;
        if (!Mathf.Approximately(walk, _walk)) { float k = walk / _walk; pc.playerOriginalMoveSpeed *= k; pc.MoveSpeed *= k; _walk = walk; }
        if (!Mathf.Approximately(sprint, _sprint)) { float k = sprint / _sprint; pc.playerOriginalSprintSpeed *= k; pc.SprintSpeed *= k; _sprint = sprint; }
        if (!Mathf.Approximately(crouch, _crouch)) { float k = crouch / _crouch; pc.playerOriginalCrouchSpeed *= k; pc.CrouchSpeed *= k; _crouch = crouch; }

        // Overclock: a weaker grip (the strength upgrade keeps adding to grabStrength on top).
        if (grabber != _grabber) { _grabber = grabber; _grip = 1f; }
        if (grabber != null)
        {
            float grip = TradeOffs.Factor(local, Fx.GrabStrength);
            if (!Mathf.Approximately(grip, _grip)) { grabber.grabStrength *= grip / _grip; _grip = grip; }
        }
    }
}
