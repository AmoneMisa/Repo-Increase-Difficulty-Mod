using System;
using System.Collections.Generic;

namespace IncreaseHardMod.Drones;

/// <summary>Engine-free rules of the linked-cargo route (checked by tests/LinkTests).</summary>
internal static class LinkRouteRules
{
    internal static HashSet<int> ConnectedCargo(IEnumerable<(int A, int B)> edges, IEnumerable<int> roots)
    {
        var graph = new Dictionary<int, List<int>>();
        foreach (var (a, b) in edges)
        {
            if (a == 0 || b == 0) continue;
            if (!graph.TryGetValue(a, out var from)) graph[a] = from = new();
            if (!graph.TryGetValue(b, out var to)) graph[b] = to = new();
            from.Add(b); to.Add(a);
        }
        var result = new HashSet<int>();
        var queue = new Queue<int>();
        foreach (int root in roots) if (root != 0 && result.Add(root)) queue.Enqueue(root);
        while (queue.Count > 0)
        {
            int node = queue.Dequeue();
            if (!graph.TryGetValue(node, out var adjacent)) continue;
            foreach (int next in adjacent) if (result.Add(next)) queue.Enqueue(next);
        }
        return result;
    }
    /// <summary>Seconds without progress before the load steps aside and re-plans.</summary>
    internal const float DetourAfter = 1.6f;
    /// <summary>A Link drone runs this long on a full base battery.</summary>
    internal const float ActiveSeconds = 300f;
    /// <summary>Share of value loss a towed valuable still takes, compared with being dragged by hand.</summary>
    internal const float TowedDamageShare = .5f;

    internal static bool ShouldDetour(float stuckSeconds) => stuckSeconds >= DetourAfter;

    /// <summary>The native battery subtracts rate * 2 per second from 100 points.</summary>
    internal static float DrainRate(float seconds) => 100f / (Math.Max(1f, seconds) * 2f);

    internal static float TowedLoss(float loss) => loss > 0f ? loss * TowedDamageShare : loss;

    /// <summary>
    /// Horizontal shift that moves a path corner (gapX, gapZ away from the nearest navmesh edge) far enough for
    /// a load of <paramref name="radius"/> (capped at 0.6 m, so a huge load never leaves the corridor).
    /// </summary>
    internal static void EdgeOffset(float gapX, float gapZ, float radius, out float x, out float z)
    {
        x = z = 0f;
        float gap = (float)Math.Sqrt(gapX * gapX + gapZ * gapZ);
        float wanted = Math.Min(radius, .6f);
        if (gap >= wanted || gap < .0001f) return;
        x = gapX / gap * (wanted - gap);
        z = gapZ / gap * (wanted - gap);
    }
}
