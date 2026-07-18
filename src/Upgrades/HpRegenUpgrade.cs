using UnityEngine;
using RepoUpgrades = REPOLib.Modules.Upgrades;

namespace IncreaseHardMod.Upgrades;

/// <summary>
/// "HP Regeneration" — a per-player upgrade granting very slow passive healing that only
/// tops you back up to a fraction of max HP (default 30%) and then stops.
///
/// Unlike stamina (a one-shot field tweak applied via start/upgrade actions), regen is a
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
        });
    }

    /// <summary>
    /// Ticks the local player's regen. Added to the mod's persistent runtime object.
    /// Accumulates fractional HP so a sub-1-HP-per-second rate still heals over time.
    /// </summary>
    internal sealed class RegenController : MonoBehaviour
    {
        private float _accumulated;

        private void Update()
        {
            if (!ModConfig.PlayerUpgradesEnabled.Value)
                return;

            // Only regenerate during actual gameplay (not shop/lobby/menu).
            if (!SemiFunc.RunIsLevel())
            {
                _accumulated = 0f;
                return;
            }

            var avatar = SemiFunc.PlayerAvatarLocal();
            if (avatar == null || avatar.isDisabled)
            {
                _accumulated = 0f;
                return;
            }

            var upgrade = RepoUpgrades.GetUpgrade(Id);
            if (upgrade == null)
                return;

            int level = upgrade.GetLevel(avatar.steamID);
            if (level <= 0)
            {
                _accumulated = 0f;
                return;
            }

            var ph = avatar.playerHealth;
            if (ph == null || ph.health <= 0)
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
            float perSecond = level * ModConfig.RegenPercentPerLevel.Value * ph.maxHealth;
            _accumulated += perSecond * Time.deltaTime;

            if (_accumulated < 1f)
                return;

            int healChunk = Mathf.FloorToInt(_accumulated);
            _accumulated -= healChunk;

            // Never overshoot the regen cap this tick.
            healChunk = Mathf.Min(healChunk, cap - ph.health);
            if (healChunk > 0)
                ph.Heal(healChunk, effect: false);
        }
    }
}
