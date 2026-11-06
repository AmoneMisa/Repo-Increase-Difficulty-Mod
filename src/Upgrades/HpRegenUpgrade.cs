using UnityEngine;
using RepoUpgrades = REPOLib.Modules.Upgrades;

namespace IncreaseHardMod.Upgrades;

/// <summary>
/// "HP Regeneration" — a per-player upgrade granting very slow passive healing that only
/// tops you back up to a fraction of max HP (default 30%) and then stops.
///
/// Regen is a
/// CONTINUOUS effect, so it lives in a ticking MonoBehaviour (<see cref="RegenController"/>)
/// rather than REPOLib's start/upgrade callbacks. The REPOLib upgrade is still registered
/// (null actions) purely so it appears in the shop and its per-player level is tracked,
/// saved and synced. The controller reads that level for the LOCAL player each frame.
///
/// Only the local player heals themselves: PlayerHealth.Heal early-returns unless the
/// PhotonView IsMine, so each client regenerates its own avatar independently.
/// </summary>
internal static class HpRegenUpgrade
{
    public const string Id = "IHM_HpRegen";

    public static void Register()
    {
        if (!ModConfig.PlayerUpgradesEnabled.Value)
            return;

        // No start/upgrade actions — RegenController drives the effect continuously.
        PlayerUpgradeRegistry.Define(new PlayerUpgradeRegistry.Definition
        {
            Id = Id,
            DisplayName = "HP Regeneration",
            Description = "Slowly regenerate health, but only up to a fraction of your max HP.",
            MaxLevel = ModConfig.RegenMaxLevel.Value,
            BaseShopPrice = ModConfig.RegenBasePrice.Value,
            StartAction = null,
            UpgradeAction = null,
            IconFile = "Upgrade_HP-Regen.png",
        });
    }

    /// <summary>
    /// Ticks the local player's regen. Added to the mod's persistent runtime object.
    /// Accumulates fractional HP so a sub-1-HP-per-second rate still heals over time.
    /// </summary>
    internal sealed class RegenController : MonoBehaviour
    {
        private float _accumulated;
        private PlayerAvatar _avatar;

        private void Update() => Tick(Time.deltaTime);

        internal void Tick(float dt)
        {
            if (!ModConfig.PlayerUpgradesEnabled.Value)
            {
                _accumulated = 0f;
                return;
            }

            // Only regenerate during actual gameplay (not shop/lobby/menu).
            if (RunManager.instance == null || RunManager.instance.levelCurrent == null || !SemiFunc.RunIsLevel())
            {
                _accumulated = 0f;
                return;
            }

            var avatar = SemiFunc.PlayerAvatarLocal();
            if (_avatar != avatar) { _avatar = avatar; _accumulated = 0f; }
            if (avatar == null || avatar.isDisabled)
            {
                _accumulated = 0f;
                return;
            }

            // Skill tree: HP Regeneration, on the old ladder of RegenMaxLevel levels.
            float level = Mathf.Clamp(Skills.SkillEffects.Equivalent(avatar, Skills.SkillCatalog.HpRegen, ModConfig.RegenMaxLevel.Value), 0f, ModConfig.RegenMaxLevel.Value);
            if (level <= 0f)
            {
                _accumulated = 0f;
                return;
            }

            var ph = avatar.playerHealth;
            if (ph == null || !ph.healthSet || ph.health <= 0)
            {
                _accumulated = 0f;
                return;
            }

            // Cap: stop once current HP reaches the configured fraction of max HP.
            int cap = Mathf.RoundToInt(ph.maxHealth * Mathf.Clamp01(ModConfig.RegenMaxHpFraction.Value));
            if (ph.health >= cap)
            {
                _accumulated = 0f;
                return;
            }

            // Rate = level * percent-of-maxHP per second. Accumulate until >= 1 HP.
            float perSecond = level * ModConfig.RegenPercentPerLevel.Value * ph.maxHealth * Patches.Effects.HealPowerEffect.Multiplier;
            _accumulated += perSecond * Mathf.Max(0f, dt);

            if (_accumulated < 1f)
                return;

            int healChunk = Mathf.FloorToInt(_accumulated);
            _accumulated -= healChunk;

            // Never overshoot the regen cap this tick.
            healChunk = Mathf.Min(healChunk, cap - ph.health);
            if (healChunk > 0)
                Patches.Effects.HealPowerEffect.HealRegeneration(ph, healChunk);
        }
    }
}
