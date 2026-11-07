using UnityEngine;

namespace IncreaseHardMod.Patches.Effects;

/// <summary>
/// Ping targets that are not physics objects: battery crystals, skill boxes and mystery boxes are local props
/// built from the host's state (crystals and skill boxes have no colliders at all), so the ping's physics ray never
/// identifies them. Each effect tests the ping ray against its own props; this picks the nearest one.
/// </summary>
internal static class PingTargets
{
    /// <summary>A prop the ping ray passes through: where to put the marker and how far along the ray it was.</summary>
    internal struct Hit
    {
        public PingType Type;
        public Vector3 Marker;
        public float Distance;
        public string Label;
    }

    /// <summary>Nearest crystal or box along the ray within <paramref name="maxDistance"/>.</summary>
    internal static bool TryAim(Ray ray, float maxDistance, bool info, out Hit hit)
    {
        hit = default;
        hit.Distance = float.MaxValue;
        if (RetryCrystalEffect.TryAim(ray, maxDistance, out var gem, out float d) && d < hit.Distance)
            hit = new Hit { Type = PingType.Crystal, Marker = gem, Distance = d, Label = RetryCrystalEffect.PingLabel(info) };
        if (SkillBoxEffect.TryAim(ray, maxDistance, out var skill, out d) && d < hit.Distance)
            hit = new Hit { Type = PingType.SkillBox, Marker = skill, Distance = d, Label = SkillBoxEffect.PingLabel(info) };
        if (MysteryBoxEffect.TryAim(ray, maxDistance, out var mystery, out d) && d < hit.Distance)
            hit = new Hit { Type = PingType.MysteryBox, Marker = mystery, Distance = d, Label = MysteryBoxEffect.PingLabel(info) };
        return hit.Distance < float.MaxValue;
    }

    /// <summary>Make the pinged prop at (or nearest to) <paramref name="marker"/> flare, on any client.</summary>
    internal static void Flare(PingType type, Vector3 marker, float seconds)
    {
        if (type == PingType.Crystal) RetryCrystalEffect.Flare(marker, seconds);
        else if (type == PingType.SkillBox) SkillBoxEffect.Flare(marker, seconds);
        else if (type == PingType.MysteryBox) MysteryBoxEffect.Flare(marker, seconds);
    }

    internal static bool IsProp(PingType type) =>
        type == PingType.Crystal || type == PingType.SkillBox || type == PingType.MysteryBox;

    /// <summary>Ray against a box standing on <paramref name="floor"/>, turned <paramref name="yaw"/> degrees,
    /// <paramref name="size"/> wide/high/deep (slab test in the box's own space).</summary>
    internal static bool RayBox(Ray ray, Vector3 floor, float yaw, Vector3 size, float maxDistance, out float distance)
    {
        distance = 0f;
        var inverse = Quaternion.Inverse(Quaternion.Euler(0f, yaw, 0f));
        Vector3 o = inverse * (ray.origin - floor), d = inverse * ray.direction;
        Vector3 min = new(-size.x * 0.5f, 0f, -size.z * 0.5f), max = new(size.x * 0.5f, size.y, size.z * 0.5f);
        float near = 0f, far = maxDistance;
        for (int axis = 0; axis < 3; axis++)
        {
            if (Mathf.Abs(d[axis]) < 1e-6f)
            {
                if (o[axis] < min[axis] || o[axis] > max[axis]) return false;
                continue;
            }
            float t1 = (min[axis] - o[axis]) / d[axis], t2 = (max[axis] - o[axis]) / d[axis];
            if (t1 > t2) (t1, t2) = (t2, t1);
            near = Mathf.Max(near, t1);
            far = Mathf.Min(far, t2);
            if (near > far) return false;
        }
        distance = near;
        return true;
    }

    /// <summary>Flare countdown shared by the prop visuals: 0 when idle, else 0..1 pulse strength.</summary>
    internal static float FlarePulse(float until, float speed = 9f)
    {
        if (Time.time >= until) return 0f;
        return 0.5f + 0.5f * Mathf.Sin(Time.time * speed);
    }
}
