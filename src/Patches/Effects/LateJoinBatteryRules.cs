using System;
using System.Collections.Generic;

namespace IncreaseHardMod.Patches.Effects;

internal readonly struct JoinBatterySnapshot
{
    internal readonly int View, Bars, Segments;
    internal readonly float Charge;
    internal readonly bool Charging;
    internal readonly double Sent;
    internal JoinBatterySnapshot(int view, int bars, int segments, float charge, bool charging, double sent)
    { View = view; Bars = bars; Segments = segments; Charge = charge; Charging = charging; Sent = sent; }
}

internal static class LateJoinBatteryRules
{
    internal const int BatchSize = 64, MaxBars = 256;
    internal const double MaxAge = 10d;
    internal static bool Read(object raw, double now, string level, out JoinBatterySnapshot[] result)
    {
        result = null;
        if (raw is not object[] data || data.Length != 8 || data[0] is not byte op || op != 1 ||
            data[1] is not string key || key != level || data[2] is not double sent ||
            double.IsNaN(sent) || double.IsInfinity(sent) || double.IsNaN(now) || double.IsInfinity(now) ||
            now - sent > MaxAge || sent - now > .5d || data[3] is not int[] ids || ids.Length == 0 || ids.Length > BatchSize ||
            data[4] is not int[] bars || data[5] is not int[] segments || data[6] is not float[] charge ||
            data[7] is not bool[] charging || bars.Length != ids.Length || segments.Length != ids.Length ||
            charge.Length != ids.Length || charging.Length != ids.Length) return false;
        var seen = new HashSet<int>();
        var rows = new JoinBatterySnapshot[ids.Length];
        for (int i = 0; i < ids.Length; i++)
        {
            if (ids[i] <= 0 || !seen.Add(ids[i]) || bars[i] < 1 || bars[i] > MaxBars ||
                segments[i] < 0 || segments[i] > bars[i] || float.IsNaN(charge[i]) || float.IsInfinity(charge[i]) ||
                charge[i] < 0f || charge[i] > 100f || (charge[i] == 0f && segments[i] != 0)) return false;
            rows[i] = new JoinBatterySnapshot(ids[i], bars[i], segments[i], charge[i], charging[i], sent);
        }
        result = rows;
        return true;
    }
}
