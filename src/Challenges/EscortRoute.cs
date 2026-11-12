using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace IncreaseHardMod.Challenges;

/// <summary>
/// Plans the Wisp Escort route through the level's own obstacles. The monsters' navmesh walks round everything, so
/// the route starts from it and then takes the player shortcuts it skips: wherever a straight line between two
/// points of the walk is much shorter than the walk and a player can physically cross it - jumping a gap or a pit,
/// climbing onto a ledge, a crate or a stack, dropping down, or crawling under a low ceiling, pipe or shelf - the
/// wisp floats straight across (low under a ceiling), and players must stay closer to it there, so the only way to
/// keep up is the jump or the crawl. Among the possible homes it picks the route with the most such spots.
/// Host only, once when the escort starts; checks only the level's static geometry.
/// </summary>
internal static class EscortRoute
{
    internal const float ShortcutRadius = 3.2f;
    private const float Step = .25f, MaxCross = 6.5f, MaxGap = 1.9f, MaxClimb = .95f, MaxDrop = 2.2f, Crawl = .72f, Stand = 1.35f;
    private static readonly int Geometry = LayerMask.GetMask("Default");

    internal readonly struct Point
    {
        internal readonly Vector3 Position; internal readonly float Radius;
        internal Point(Vector3 position, float radius) { Position = position; Radius = radius; }
    }

    /// <summary>The best route from <paramref name="start"/> to one of the level points (25-55 m of walking).</summary>
    internal static bool Plan(Vector3 start, List<LevelPoint> points, float minPath, float maxPath, float followRadius,
        List<Point> route, out Vector3 home, out int features)
    {
        home = default; features = -1;
        var shuffled = new List<LevelPoint>(points);
        for (int i = shuffled.Count - 1; i > 0; i--) { int j = Random.Range(0, i + 1); (shuffled[i], shuffled[j]) = (shuffled[j], shuffled[i]); }
        var navPath = new NavMeshPath();
        var best = new List<Point>();
        int tried = 0;
        foreach (var p in shuffled)
        {
            if (p == null || p.Truck || p.inStartRoom) continue;
            if (!NavMesh.SamplePosition(p.transform.position, out var end, 2f, NavMesh.AllAreas)) continue;
            if (!NavMesh.CalculatePath(start, end.position, NavMesh.AllAreas, navPath) || navPath.status != NavMeshPathStatus.PathComplete) continue;
            float length = 0f;
            for (int i = 1; i < navPath.corners.Length; i++) length += Vector3.Distance(navPath.corners[i - 1], navPath.corners[i]);
            if (length < minPath || length > maxPath * 1.15f) continue;
            var candidate = new List<Point>();
            int score = Build(navPath.corners, followRadius, candidate);
            if (score > features) { features = score; home = end.position; best = candidate; }
            if (++tried >= 24 || features >= 5) break;
        }
        if (features < 0) return false;
        route.Clear(); route.AddRange(best);
        return true;
    }

    // The walk densified to one floor point a metre, then shortcut wherever a player can cross straight.
    private static int Build(Vector3[] corners, float followRadius, List<Point> route)
    {
        var walk = new List<Vector3>();
        for (int i = 1; i < corners.Length; i++)
        {
            float d = Vector3.Distance(corners[i - 1], corners[i]);
            int n = Mathf.Max(1, Mathf.CeilToInt(d));
            for (int k = 0; k < n; k++) walk.Add(Vector3.Lerp(corners[i - 1], corners[i], k / (float)n));
        }
        walk.Add(corners[corners.Length - 1]);
        var along = new float[walk.Count];
        for (int i = 1; i < walk.Count; i++) along[i] = along[i - 1] + Vector3.Distance(walk[i - 1], walk[i]);

        int features = 0;
        int at = 0;
        route.Add(new Point(Hover(walk[0]), followRadius));
        while (at < walk.Count - 1)
        {
            // The farthest point ahead that a player can reach straight across, with a real detour to save.
            int jump = -1;
            for (int j = Mathf.Min(walk.Count - 1, at + 40); j > at + 2; j--)
            {
                float straight = Flat(walk[j] - walk[at]).magnitude;
                if (straight > MaxCross || along[j] - along[at] < straight * 2.2f + 2f) continue;
                if (Cross(walk[at], walk[j], out var floors)) { jump = j; foreach (var f in floors) route.Add(new Point(f, ShortcutRadius)); break; }
            }
            if (jump >= 0) { features += 3; at = jump; continue; }
            at++;
            // Along the walk itself: a step or ledge to climb / jump and a stretch to crouch under also count.
            var prev = walk[at - 1]; var here = walk[at];
            if (Mathf.Abs(here.y - prev.y) > .45f) features++;
            if (Headroom(here) < Stand) features++;
            route.Add(new Point(Hover(here), followRadius));
        }
        return features;
    }

    /// <summary>
    /// Whether a player can go straight from floor point <paramref name="a"/> to <paramref name="b"/>, and if so the
    /// wisp's points over it. Walls and furniture they cannot climb stop it; pits up to 1.9 m are jumped; ledges up to
    /// 0.95 m are climbed, drops up to 2.2 m taken; headroom down to 0.72 m is crawled. Only counts if it needs at least
    /// one of those (a plain straight walk is no obstacle).
    /// </summary>
    private static bool Cross(Vector3 a, Vector3 b, out List<Vector3> wisp)
    {
        wisp = new List<Vector3>();
        var flat = Flat(b - a);
        float distance = flat.magnitude;
        if (distance < .5f) return false;
        var dir = flat / distance;
        float floorY = a.y, gap = 0f;
        bool hard = false;
        for (float s = Step; s <= distance + .01f; s += Step)
        {
            var p = a + dir * Mathf.Min(s, distance);
            // Floor under this point, looking from just above where the player's feet would be.
            if (!Physics.Raycast(new Vector3(p.x, floorY + MaxClimb + .3f, p.z), Vector3.down, out var down, MaxClimb + .3f + MaxDrop, Geometry, QueryTriggerInteraction.Ignore) ||
                down.normal.y < .7f)
            {
                gap += Step;
                if (gap > MaxGap) return false;
                continue;
            }
            if (gap > 0f) { hard = true; gap = 0f; }                    // jumped a pit or a gap
            float rise = down.point.y - floorY;
            if (rise > MaxClimb) return false;                          // a wall or something too tall
            if (rise > .4f || rise < -.8f) hard = true;                 // climbed up or dropped down
            floorY = down.point.y;
            float head = Headroom(down.point);
            if (head < Crawl) return false;                             // too low even to crawl
            if (head < Stand) hard = true;                              // crouch / crawl under it
            // A crawling player's body must fit here (nothing solid at knee height: a wall the ray slipped through).
            if (Physics.CheckSphere(down.point + Vector3.up * .36f, .2f, Geometry, QueryTriggerInteraction.Ignore)) return false;
            wisp.Add(new Vector3(down.point.x, down.point.y + Mathf.Min(1.2f, Mathf.Max(.35f, head - .35f)), down.point.z));
        }
        if (gap > 0f || Mathf.Abs(floorY - b.y) > .5f) return false;
        return hard;
    }

    private static float Headroom(Vector3 floor) =>
        Physics.Raycast(floor + Vector3.up * .05f, Vector3.up, out var up, 3f, Geometry, QueryTriggerInteraction.Ignore) ? up.distance + .05f : 3f;

    private static Vector3 Hover(Vector3 floor) => floor + Vector3.up * Mathf.Min(1.2f, Mathf.Max(.35f, Headroom(floor) - .35f));

    private static Vector3 Flat(Vector3 v) => new(v.x, 0f, v.z);
}
