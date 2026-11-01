using IncreaseHardMod.Economy;

namespace IncreaseHardMod.Quests;

/// <summary>Quest payloads/config use dollars; convert once at the authoritative wallet boundary.</summary>
internal static class QuestMoney
{
    // Match native extraction rounding to whole K, without overflow in dollars + 999.
    internal static int WalletUnits(int dollars) => dollars <= 0 ? 0 : (int)(((long)dollars + 999L) / 1000L);

    internal static void Grant(int dollars)
    {
        if (dollars <= 0 || !SemiFunc.IsMasterClientOrSingleplayer()) return;
        int units = WalletUnits(dollars);
        CurrencyPayout.CreditReward(units);
        Plugin.Log.LogInfo($"[Quests] Reward: ${dollars} requested, +${units}K wallet units (now {SemiFunc.StatGetRunCurrency()}K).");
    }
}
