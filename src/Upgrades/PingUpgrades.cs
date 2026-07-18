using RepoUpgrades = REPOLib.Modules.Upgrades;

namespace IncreaseHardMod.Upgrades;

/// <summary>
/// Ping-system shop upgrades sold in the default shop:
///   • "Ping Anywhere" — permanent unlock: place Location pings on empty surfaces / through walls
///     (capped at <see cref="ModConfig.MaxLocationPings"/> live per player).
///   • "Ping Info" — permanent unlock: enrich your pings' labels with per-type detail.
///   • "Pin" — permanent one-time unlock (buy once, keep forever): lets you place Location pins
///     without owning Ping Anywhere. Reuses the exact same pin logic
///     (<see cref="Effects.PingController"/>); this upgrade only gates it.
///
/// The unlocks are read-only (no start/upgrade action). The ping code queries the LOCAL player's
/// ownership at ping time; because the pinging player computes the broadcast label, a Ping Info
/// owner's richer labels benefit the whole team (same as Autoscan discovery).
/// </summary>
internal static class PingUpgrades
{
    public const string PingAnywhereId = "IHM_PingAnywhere";
    public const string PingInfoId = "IHM_PingInfo";
    public const string PinId = "IHM_Pin";

    public static void Register()
    {
        if (!ModConfig.PlayerUpgradesEnabled.Value || !ModConfig.PingEnabled.Value)
            return;

        PlayerUpgradeRegistry.Define(new PlayerUpgradeRegistry.Definition
        {
            Id = PingAnywhereId,
            DisplayName = "Ping Anywhere",
            Description = "Place location pings on any surface (visible through walls). Limited live pings per player.",
            MaxLevel = 1,
            BaseShopPrice = ModConfig.PingAnywhereBasePrice.Value,
            IconFile = "Upgrade_Ping.png",
        });

        PlayerUpgradeRegistry.Define(new PlayerUpgradeRegistry.Definition
        {
            Id = PingInfoId,
            DisplayName = "Ping Info",
            Description = "Your pings reveal extra detail: valuable value, cosmetic rarity, and exact names.",
            MaxLevel = 1,
            BaseShopPrice = ModConfig.PingInfoBasePrice.Value,
            IconFile = "Upgrade_Ping.png",
        });

        PlayerUpgradeRegistry.Define(new PlayerUpgradeRegistry.Definition
        {
            Id = PinId,
            DisplayName = "Pin",
            Description = "Place location pins without the 'Ping Anywhere' upgrade. One-time unlock — buy once, keep it forever.",
            MaxLevel = 1,
            BaseShopPrice = ModConfig.PinBasePrice.Value,
            IconFile = "Upgrade_Ping.png",
        });
    }

    public static bool OwnsPingAnywhere() => OwnsLocal(PingAnywhereId);
    public static bool OwnsPingInfo() => OwnsLocal(PingInfoId);
    public static bool OwnsPin() => OwnsLocal(PinId);

    private static bool OwnsLocal(string id)
    {
        if (!ModConfig.PlayerUpgradesEnabled.Value)
            return false;
        var avatar = SemiFunc.PlayerAvatarLocal();
        if (avatar == null)
            return false;
        var up = RepoUpgrades.GetUpgrade(id);
        return up != null && up.GetLevel(avatar.steamID) > 0;
    }
}
