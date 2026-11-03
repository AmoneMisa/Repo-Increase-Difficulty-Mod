using System;

namespace IncreaseHardMod.Valuables;

/// <summary>Deterministic motion rules, independent of rendering and Unity physics queries.</summary>
internal static class ValuableDynamics
{
    internal static bool ShouldSpill(float upDot) => Finite(upDot) && upDot < -.35f;
    internal static float WheelDegrees(float travelled, float radius, bool grabbed, bool grounded, bool teleport)
    {
        if (!grabbed || !grounded || teleport || radius <= 0f || !Finite(travelled) || !Finite(radius)) return 0f;
        return travelled / radius * (180f / (float)Math.PI);
    }

    internal static float Progress(double now, double epoch, float seconds)
    {
        if (!Finite(now) || !Finite(epoch) || !Finite(seconds) || seconds <= 0f) return 0f;
        return (float)Math.Max(0, Math.Min(1, (now - epoch) / seconds));
    }

    internal static bool AcceptFlip(bool holderMatches, double now, double lastFlip, float cooldown)
        => holderMatches && Finite(now) && Finite(lastFlip) && now >= lastFlip + cooldown;

    internal static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
}
