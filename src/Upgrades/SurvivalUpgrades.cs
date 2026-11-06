using HarmonyLib;
using UnityEngine;
using RepoUpgrades = REPOLib.Modules.Upgrades;

namespace IncreaseHardMod.Upgrades;

/// <summary>
/// Armor, Overcharge and the Tumble Launch bonuses.
/// <list type="bullet">
/// <item><b>Armor</b> (new upgrade): each level cuts incoming damage by a percentage, always rounded in the
/// player's favour. Applied in PlayerHealth.Hurt, which runs on the hurt player's own client.</item>
/// <item><b>Overcharge</b> (new upgrade): holding a monster with the grab beam builds overcharge until the grab
/// is torn away. Each level slows that build-up (you can hold monsters longer), shortens the wait before it
/// drains and drains it faster. Runs on each player's own PhysGrabber.</item>
/// <item><b>Tumble Damage</b> (new upgrade): launching into a monster deals compounding extra damage per
/// level, and its self-damage shrinks per level. Host-side, like the native hits.</item>
/// </list>
/// </summary>
internal static class SurvivalUpgrades
{
    public const string ArmorId = "IHM_Armor";
    public const string OverchargeId = "IHM_Overcharge";
    public const string TumbleId = "IHM_TumbleDamage";

    public static void Register()
    {
        if (!ModConfig.PlayerUpgradesEnabled.Value) return;
        if (ModConfig.ArmorEnabled.Value)
            PlayerUpgradeRegistry.Define(new PlayerUpgradeRegistry.Definition
            {
                Id = ArmorId,
                DisplayName = "Armor",
                Description = $"Reduces incoming damage by {ModConfig.ArmorPercentPerLevel.Value * 100f:0.#}% per level (up to {ArmorCap * 100f:0.#}%). Always rounded in your favour.",
                MaxLevel = ModConfig.ArmorMaxLevel.Value,
                BaseShopPrice = ModConfig.ArmorBasePrice.Value,
                IconFile = "Upgrade_Armor.png",
            });
        if (ModConfig.OverchargeEnabled.Value)
            PlayerUpgradeRegistry.Define(new PlayerUpgradeRegistry.Definition
            {
                Id = OverchargeId,
                DisplayName = "Overcharge",
                Description = $"Hold monsters longer before your grab beam overloads: overcharge builds up {ModConfig.OverchargeBuildupPerLevel.Value * 100f:0.#}% slower per level and recovers faster.",
                MaxLevel = ModConfig.OverchargeMaxLevel.Value,
                BaseShopPrice = ModConfig.OverchargeBasePrice.Value,
                IconFile = "Upgrade_Overcharge.png",
            });
        if (ModConfig.TumbleBonusEnabled.Value)
            PlayerUpgradeRegistry.Define(new PlayerUpgradeRegistry.Definition
            {
                Id = TumbleId,
                DisplayName = "Tumble Damage",
                Description = $"Launching yourself into a monster hits harder: +{ModConfig.TumbleDamagePerLevel.Value * 100f:0.#}% damage per level, compounding, and you take {ModConfig.TumbleSelfDamageReductionPerLevel.Value * 100f:0.#}% less self-damage per level.",
                MaxLevel = ModConfig.TumbleMaxLevel.Value,
                BaseShopPrice = ModConfig.TumbleBasePrice.Value,
                IconFile = "Upgrade_Tumble-Launch.png",
            });
    }

    internal static float ArmorCap => Mathf.Clamp01(Mathf.Min(ModConfig.ArmorMaxReduction.Value, ModConfig.ArmorPercentPerLevel.Value * ModConfig.ArmorMaxLevel.Value));

    /// <summary>Damage reduction (0..1) the Armor upgrade gives this player right now.</summary>
    internal static float ArmorReduction(PlayerAvatar player)
    {
        if (!ModConfig.ArmorEnabled.Value) return 0f;
        float level = Level(ArmorId, player, ModConfig.ArmorMaxLevel.Value);
        return level > 0f ? Mathf.Min(ArmorCap, ModConfig.ArmorPercentPerLevel.Value * level) : 0f;
    }

    // Skill tree: the old upgrade's node, expressed on its old ladder of `max` levels.
    private static float Level(string id, PlayerAvatar player, int max)
    {
        string node = id == ArmorId ? Skills.SkillCatalog.Armor : id == TumbleId ? Skills.SkillCatalog.TumbleDamage : Skills.SkillCatalog.Overcharge;
        return Mathf.Clamp(Skills.SkillEffects.Equivalent(player, node, max), 0f, Mathf.Max(1, max));
    }

    // ------------------------------------------------------------------ Armor + tumble self-damage

    // Set while PlayerTumble.HitEnemy deals its self-damage, so Hurt/HurtOther can scale just that hit.
    private static float _tumbleSelfScale = 1f;

    private static int Reduce(int damage, float fraction)
    {
        if (damage <= 0 || fraction <= 0f) return damage;
        return Mathf.FloorToInt(damage * (1f - Mathf.Clamp01(fraction))); // rounded in the player's favour
    }

    [HarmonyPatch(typeof(PlayerHealth), nameof(PlayerHealth.Hurt))]
    private static class ArmorHurt
    {
        [HarmonyPrefix]
        [HarmonyPriority(Priority.Low)]
        private static void Prefix(PlayerHealth __instance, ref int damage, bool hurtByHeal)
        {
            if (damage <= 0) return;
            if (_tumbleSelfScale < 1f) damage = Mathf.FloorToInt(damage * _tumbleSelfScale);
            if (hurtByHeal || !ModConfig.ArmorEnabled.Value || __instance.playerAvatar == null) return;
            float level = Level(ArmorId, __instance.playerAvatar, ModConfig.ArmorMaxLevel.Value);
            if (level > 0f) damage = Reduce(damage, Mathf.Min(ArmorCap, ModConfig.ArmorPercentPerLevel.Value * level));
        }
    }

    // The host sends other players' tumble self-damage through HurtOther; scale it before it leaves.
    [HarmonyPatch(typeof(PlayerHealth), nameof(PlayerHealth.HurtOther))]
    private static class TumbleSelfHurtOther
    {
        [HarmonyPrefix]
        private static void Prefix(ref int damage)
        {
            if (_tumbleSelfScale < 1f && damage > 0) damage = Mathf.FloorToInt(damage * _tumbleSelfScale);
        }
    }

    [HarmonyPatch(typeof(PlayerTumble), nameof(PlayerTumble.HitEnemy))]
    private static class TumbleSelfDamage
    {
        [HarmonyPrefix]
        private static bool Prefix(PlayerTumble __instance)
        {
            _tumbleSelfScale = 1f;
            if (!ModConfig.TumbleBonusEnabled.Value || __instance == null) return true;
            float level = Level(TumbleId, __instance.playerAvatar, ModConfig.TumbleMaxLevel.Value);
            if (level <= 0f) return true;
            float reduction = Mathf.Min(ModConfig.TumbleSelfDamageMaxReduction.Value, ModConfig.TumbleSelfDamageReductionPerLevel.Value * level);
            if (reduction >= 1f) return false; // fully protected: no self-damage at all
            _tumbleSelfScale = 1f - Mathf.Max(0f, reduction);
            return true;
        }

        [HarmonyPostfix]
        private static void Postfix() => _tumbleSelfScale = 1f;
    }

    /// <summary>Monster damage multiplier for a tumbling player's launch (used by the shared EnemyHurt hook).</summary>
    internal static float TumbleDamageMultiplier(PlayerTumble tumble)
    {
        if (!ModConfig.TumbleBonusEnabled.Value || tumble == null) return 1f;
        float level = Level(TumbleId, tumble.playerAvatar, ModConfig.TumbleMaxLevel.Value);
        float root = Skills.SkillEffects.Has(tumble.playerAvatar, Skills.SkillCatalog.Brawler) ? Skills.SkillEffects.RootBonus : 0f;
        // Compounding: each level multiplies the previous one (x1.05 per level by default); Brawler adds on top.
        return (level <= 0f ? 1f : Mathf.Pow(1f + Mathf.Max(0f, ModConfig.TumbleDamagePerLevel.Value), level)) + root;
    }

    // ------------------------------------------------------------------ Overcharge

    private static float OverchargeLevel(PhysGrabber grabber) =>
        grabber != null && grabber.isLocal && ModConfig.OverchargeEnabled.Value
            ? Level(OverchargeId, grabber.playerAvatar, ModConfig.OverchargeMaxLevel.Value) : 0f;

    [HarmonyPatch(typeof(PhysGrabber), nameof(PhysGrabber.PhysGrabOverCharge))]
    private static class OverchargeBuildup
    {
        [HarmonyPrefix]
        private static void Prefix(PhysGrabber __instance, ref float _amount, ref float _multiplier)
        {
            float level = OverchargeLevel(__instance);
            if (level <= 0) return;
            float slow = 1f - Mathf.Min(.8f, ModConfig.OverchargeBuildupPerLevel.Value * level);
            _amount *= slow;
            _multiplier *= slow; // also the first-grab jump
        }

        [HarmonyPostfix]
        private static void Postfix(PhysGrabber __instance)
        {
            float level = OverchargeLevel(__instance);
            if (level <= 0) return;
            // Shorter pause before the bar starts draining once you stop holding a monster.
            float shorter = 1f - Mathf.Min(.7f, .1f * level);
            __instance.physGrabBeamOverchargeDecreaseCooldown = Mathf.Min(__instance.physGrabBeamOverchargeDecreaseCooldown, 1.5f * shorter);
        }
    }

    [HarmonyPatch(typeof(PhysGrabber), "PhysGrabOverChargeLogic")]
    private static class OverchargeRecovery
    {
        [HarmonyPostfix]
        private static void Postfix(PhysGrabber __instance)
        {
            float level = OverchargeLevel(__instance);
            if (level <= 0 || __instance.physGrabBeamOverChargeTimer > 0f || __instance.physGrabBeamOverchargeDecreaseCooldown > 0f ||
                __instance.physGrabBeamOverChargeFloat <= 0f) return;
            // Vanilla drains 0.1/s; each level adds OverchargeRecoveryPerLevel of that.
            float extra = .1f * ModConfig.OverchargeRecoveryPerLevel.Value * level * Time.deltaTime;
            __instance.physGrabBeamOverChargeFloat = Mathf.Max(0f, __instance.physGrabBeamOverChargeFloat - extra);
            __instance.physGrabBeamOverCharge = (byte)(__instance.physGrabBeamOverChargeFloat * 200f);
        }
    }
}
