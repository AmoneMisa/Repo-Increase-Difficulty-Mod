using System;
using System.Collections.Generic;

namespace IncreaseHardMod.Patches.Effects;

internal sealed class RoundLootSummary
{
    internal int InitialValue, InitialCount;
    internal int RemainingValue, RemainingCount;
    internal int UnfoundValue, UnfoundCount;
    internal int SoulHaul, SoulIncome;

    internal int[] Serialize() => new[] { InitialValue, InitialCount, RemainingValue, RemainingCount,
        UnfoundValue, UnfoundCount, SoulHaul, SoulIncome };

    internal static RoundLootSummary Deserialize(int[] data)
    {
        if (data == null || data.Length != 8) return null;
        foreach (int value in data) if (value < 0) return null;
        if (data[4] > data[2] || data[5] > data[3]) return null;
        return new RoundLootSummary { InitialValue = data[0], InitialCount = data[1],
            RemainingValue = data[2], RemainingCount = data[3], UnfoundValue = data[4],
            UnfoundCount = data[5], SoulHaul = data[6], SoulIncome = data[7] };
    }
}

/// <summary>One entry per valuable; the initial baseline survives damage, destruction and extraction.</summary>
internal sealed class RoundLootLedger
{
    private sealed class Entry
    {
        internal int Original, Current;
        internal bool Initial, Soul, Alive, Found, Collected;
    }

    private readonly Dictionary<int, Entry> _items = new();
    private bool _sealed;
    private int _soulHaul, _soulIncome;

    internal void Register(int id, int original, bool initial, bool soul)
    {
        if (!_items.TryGetValue(id, out var entry))
            _items[id] = entry = new Entry { Original = Math.Max(0, original), Initial = initial && !soul, Soul = soul };
        else if (!_sealed && !entry.Collected)
        {
            entry.Original = Math.Max(0, original); // generation-time value multipliers settle before sealing
            entry.Initial |= initial && !soul;      // registered before it was known to be starting loot
        }
    }

    internal void SealInitial() => _sealed = true;

    internal void BeginObservation()
    {
        foreach (var entry in _items.Values) entry.Alive = false;
    }

    internal void Observe(int id, int value, bool discovered)
    {
        if (!_items.TryGetValue(id, out var entry) || entry.Collected) return;
        entry.Current = Math.Max(0, value);
        entry.Found |= discovered;
        entry.Alive = true;
    }

    internal bool Collect(int id, int value)
    {
        if (!_items.TryGetValue(id, out var entry) || entry.Collected) return false;
        entry.Collected = true;
        entry.Alive = false;
        if (entry.Soul) _soulHaul += Math.Max(0, value);
        return true;
    }

    internal void CreditSoulShare(int soulValue, int totalValue, int currencyIncrease)
    {
        if (soulValue <= 0 || totalValue <= 0 || currencyIncrease <= 0) return;
        // Run currency is in K units. Allocate the actual credited payout proportionally
        // for mixed extractions, after native rounding and any economy tax/bonus.
        double dollars = currencyIncrease * 1000d * Math.Min(soulValue, totalValue) / totalValue;
        _soulIncome += (int)Math.Min(int.MaxValue - _soulIncome, Math.Round(dollars, MidpointRounding.AwayFromZero));
    }

    internal RoundLootSummary Snapshot()
    {
        var result = new RoundLootSummary { SoulHaul = _soulHaul, SoulIncome = _soulIncome };
        foreach (var entry in _items.Values)
        {
            if (entry.Initial) { result.InitialValue += entry.Original; result.InitialCount++; }
            if (entry.Soul || entry.Collected || !entry.Alive || entry.Current <= 0) continue;
            result.RemainingValue += entry.Current;
            result.RemainingCount++;
            if (!entry.Found) { result.UnfoundValue += entry.Current; result.UnfoundCount++; }
        }
        return result;
    }
}
