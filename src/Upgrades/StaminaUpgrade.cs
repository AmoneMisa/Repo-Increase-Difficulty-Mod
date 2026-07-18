using UnityEngine;

namespace IncreaseHardMod.Upgrades;

/// <summary>
/// "Slower Stamina Use" — a per-player upgrade that reduces sprint energy drain.
///
/// Effect: PlayerController drains EnergyCurrent by EnergySprintDrain each physics tick
/// while sprinting (see PlayerController sprint logic). We scale that field down by the
/// upgrade level. Only the LOCAL player has a PlayerController (it's a singleton), so we
/// apply on the local avatar only; each client applies its own level.
///
/// We cache the controller's original drain the first time we touch it so re-applying at
/// a new level (or after a load) always scales from the authored base, never compounding.
/// </summary>
internal static class StaminaUpgrade
{
    public const string Id = "IHM_Stamina";

    private static PlayerController _cachedController;
    private static float _baseDrain;

    public static void Register()
    {
        if (!ModConfig.PlayerUpgradesEnabled.Value)
            return;

        PlayerUpgradeRegistry.Define(new PlayerUpgradeRegistry.Definition
        {
            Id = Id,
            DisplayName = "Slower Stamina Use",
            Description = "Sprinting drains less stamina. Stacks per level.",
            MaxLevel = ModConfig.StaminaMaxLevel.Value,
            BaseShopPrice = ModConfig.StaminaBasePrice.Value,
            IconFile = "Upgrade_Slower_Stamina_Use.png",
            StartAction = Apply,
            UpgradeAction = Apply,
        });
    }

    private static void Apply(PlayerAvatar avatar, int level)
    {
        if (avatar == null || !avatar.isLocal)
            return;

        var pc = PlayerController.instance;
        if (pc == null)
            return;

        // Capture the authored drain once (per controller instance).
        if (_cachedController != pc)
        {
            _cachedController = pc;
            _baseDrain = pc.EnergySprintDrain;
        }

        float reduction = Mathf.Clamp01(ModConfig.StaminaPercentPerLevel.Value * level);
        pc.EnergySprintDrain = _baseDrain * (1f - reduction);
    }
}
