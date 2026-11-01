using System;
using HarmonyLib;

namespace IncreaseHardMod.Economy;

/// <summary>Native wallet units are thousands of dollars. Taxes apply only to extraction payouts.</summary>
internal static class CurrencyPayout
{
    [ThreadStatic] private static int _extractionDepth;
    [ThreadStatic] private static int _rewardDepth;

    internal static void CreditReward(int walletUnits)
    {
        if (walletUnits <= 0 || !SemiFunc.IsMasterClientOrSingleplayer()) return;
        int current = SemiFunc.StatGetRunCurrency();
        int value = (int)Math.Min(int.MaxValue, (long)current + walletUnits);
        _rewardDepth++;
        try
        {
            SemiFunc.StatSetRunCurrency(value);
            if (CurrencyUI.instance != null) CurrencyUI.instance.FetchCurrency();
        }
        finally { _rewardDepth--; }
    }

    internal static int ScaleExtraction(int current, int intended, float multiplier)
    {
        if (_extractionDepth <= 0 || _rewardDepth > 0 || intended <= current ||
            float.IsNaN(multiplier) || float.IsInfinity(multiplier)) return intended;
        double increase = Math.Max(0d, Math.Round((float)((long)intended - current) * multiplier));
        return (int)Math.Min(int.MaxValue, current + increase);
    }

    [HarmonyPatch(typeof(ExtractionPoint), "StateComplete")]
    private static class CompleteScope
    {
        [HarmonyPrefix] private static void Prefix() => _extractionDepth++;
        [HarmonyFinalizer] private static void Finalizer() => _extractionDepth = Math.Max(0, _extractionDepth - 1);
    }

    [HarmonyPatch(typeof(ExtractionPoint), "StateTaxReturn")]
    private static class TaxReturnScope
    {
        [HarmonyPrefix] private static void Prefix() => _extractionDepth++;
        [HarmonyFinalizer] private static void Finalizer() => _extractionDepth = Math.Max(0, _extractionDepth - 1);
    }

    [HarmonyPatch(typeof(SemiFunc), "StatSetRunCurrency")]
    private static class Pay
    {
        [HarmonyPrefix]
        private static void Prefix(ref int value)
        {
            if (!SemiFunc.IsMasterClientOrSingleplayer() || RunManager.instance == null ||
                RunManager.instance.levelCurrent == null || !SemiFunc.RunIsLevel()) return;
            value = ScaleExtraction(SemiFunc.StatGetRunCurrency(), value, EconomyEvents.PayMult);
        }
    }
}
