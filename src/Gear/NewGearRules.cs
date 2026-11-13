using System;

namespace IncreaseHardMod.Gear;

internal enum GrenadeKind : byte { Smoke, Cryo, Glue }
internal enum ConsumableKind : byte { Antidote, Adrenaline, RepairKit }

// Engine-free rules for the 0.10.15 gear: grenades, consumables and the Boomerang Sickle (shared with tests/GearTests).

/// <summary>Numbers and geometry for the Smoke, Cryo and Glue grenades.</summary>
internal static class GrenadeRules
{
    internal const float SmokeRadius = 4.5f, SmokeSeconds = 12f;
    /// <summary>Monsters this close still notice you, smoke or not (they bump into you).</summary>
    internal const float SmokeTouchRange = 1.5f;
    internal const float CryoRadius = 4f, CryoSeconds = 3f;
    internal const float GlueRadius = 4.5f;

    internal static float Radius(GrenadeKind kind) => kind switch
    {
        GrenadeKind.Smoke => SmokeRadius,
        GrenadeKind.Cryo => CryoRadius,
        _ => GlueRadius,
    };

    /// <summary>
    /// True when a cloud at (cx, cy, cz) hides the line from a monster's eye (a) to a player (b): the line passes
    /// through the cloud, or the player stands inside it. Never true at touching range.
    /// </summary>
    internal static bool Hides(float ax, float ay, float az, float bx, float by, float bz, float cx, float cy, float cz, float radius)
    {
        float dx = bx - ax, dy = by - ay, dz = bz - az;
        float length2 = dx * dx + dy * dy + dz * dz;
        if (length2 <= SmokeTouchRange * SmokeTouchRange) return false;
        float t = ((cx - ax) * dx + (cy - ay) * dy + (cz - az) * dz) / length2;
        t = Math.Max(0f, Math.Min(1f, t));
        float px = ax + dx * t - cx, py = ay + dy * t - cy, pz = az + dz * t - cz;
        return px * px + py * py + pz * pz <= radius * radius;
    }

    internal static bool Within(float ax, float ay, float az, float bx, float by, float bz, float radius)
    {
        float dx = bx - ax, dy = by - ay, dz = bz - az;
        return dx * dx + dy * dy + dz * dz <= radius * radius;
    }
}

/// <summary>Engine-free numbers for the consumables (shared with tests/GearTests).</summary>
internal static class ConsumableRules
{
    internal const float AntidoteImmunitySeconds = 20f;
    internal const float AdrenalineSeconds = 8f;
    internal const float RepairRange = 3f, RepairShare = .5f;

    /// <summary>Value a Repair Kit gives back: half of what the valuable lost, never above its original value.</summary>
    internal static float Repaired(float original, float current) =>
        original <= 0f || current >= original ? current : current + (original - System.Math.Max(0f, current)) * RepairShare;
}

/// <summary>Engine-free numbers for the Boomerang Sickle (shared with tests/GearTests).</summary>
internal static class SickleRules
{
    internal const string Asset = "Item Melee Boomerang Sickle";
    internal const float ThrowSpeed = 5f;        // released faster than this = thrown
    internal const float ReturnAfter = .55f, MaxFlight = 2.6f, CatchRange = 1.2f;
    internal const float ReturnSpeed = 9f, Steer = 4f, Spin = 25f;
    internal const int ThrowDamage = 60;         // per monster, once per throw

    internal static bool Thrown(bool wasHeld, bool held, float speed) => wasHeld && !held && speed > ThrowSpeed;
    internal static bool Returning(float flight) => flight >= ReturnAfter;
    internal static bool Ends(float flight, float distanceToThrower) =>
        flight >= MaxFlight || (flight >= ReturnAfter && distanceToThrower <= CatchRange);
}
