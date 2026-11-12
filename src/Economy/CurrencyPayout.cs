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

    [HarmonyPatch(typeof(ExtractionPoint), nameof(ExtractionPoint.StateComplete))]
    private static class CompleteScope
    {
        [HarmonyPrefix] private static void Prefix() => _extractionDepth++;
        [HarmonyFinalizer] private static void Finalizer() => _extractionDepth = Math.Max(0, _extractionDepth - 1);
    }

    [HarmonyPatch(typeof(ExtractionPoint), nameof(ExtractionPoint.StateTaxReturn))]
    private static class TaxReturnScope
    {
        [HarmonyPrefix] private static void Prefix() => _extractionDepth++;
        [HarmonyFinalizer] private static void Finalizer() => _extractionDepth = Math.Max(0, _extractionDepth - 1);
    }

    [HarmonyPatch(typeof(SemiFunc), nameof(SemiFunc.StatSetRunCurrency))]
    private static class Pay
    {
        [HarmonyPrefix]
        private static void Prefix(ref int value, out int __state)
        {
            __state = -1;
            if (!SemiFunc.IsMasterClientOrSingleplayer() || RunManager.instance == null ||
                RunManager.instance.levelCurrent == null || !SemiFunc.RunIsLevel()) return;
            __state = SemiFunc.StatGetRunCurrency();
            value = ScaleExtraction(__state, value, EconomyEvents.PayMult);
        }

        // Money earned on this attempt at the level (extractions, quest money, mystery boxes...), so a restart can take it back.
        [HarmonyPostfix]
        private static void Postfix(int value, int __state)
        {
            if (__state >= 0 && value > __state && !_rollingBack) _earnedThisAttempt += value - __state;
        }
    }

    private static int _earnedThisAttempt;
    private static bool _rollingBack;

    /// <summary>Host: a run level starts (also a restarted one): nothing earned on this attempt yet.</summary>
    [HarmonyPatch(typeof(LevelGenerator), nameof(LevelGenerator.GenerateDone))]
    private static class AttemptStart
    {
        [HarmonyPostfix] private static void Postfix() => _earnedThisAttempt = 0;
    }

    /// <summary>
    /// Host: the level is being replayed (restart vote, a random map instead, or the host's Restart Level): the money
    /// earned on the failed attempt is taken back, so it can't be farmed by restarting. Money spent during it (skill fees,
    /// Black Market) is not refunded. Never goes below zero.
    /// </summary>
    internal static void RollBackAttempt()
    {
        if (!SemiFunc.IsMasterClientOrSingleplayer() || _earnedThisAttempt <= 0) { _earnedThisAttempt = 0; return; }
        int current = SemiFunc.StatGetRunCurrency();
        int back = Math.Max(0, current - _earnedThisAttempt);
        Plugin.Log.LogInfo($"[Run] Level replayed: taking back ${(long)(current - back) * 1000:N0} earned on the failed attempt.");
        _rollingBack = true;
        try
        {
            SemiFunc.StatSetRunCurrency(back);
            if (CurrencyUI.instance != null) CurrencyUI.instance.FetchCurrency();
        }
        finally { _rollingBack = false; _earnedThisAttempt = 0; }
    }
}
