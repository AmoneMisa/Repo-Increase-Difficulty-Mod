using HarmonyLib;
using UnityEngine;
using UnityEngine.AI;

namespace IncreaseHardMod.Drones;

/// <summary>Host-only physical cargo guidance. Never uses a straight tether across an unwalkable gap.</summary>
internal sealed class LinkedCargoRoute
{
    internal PhysGrabObject Cargo { get; private set; }
    internal bool Blocked { get; private set; }
    private readonly NavMeshPath _path = new();
    private Vector3[] _corners;
    private int _corner;
    private float _repath;
    private Vector3 _safe;
    private bool _hasSafe;
    private Collider[] _colliders;
    private float _stuck, _bestDistance;
    private int _detour;
    private static readonly RaycastHit[] NoHits = new RaycastHit[0];

    /// <summary>The current route corners (host only), for the cable drawing; null without a route.</summary>
    internal Vector3[] Corners => _corners;

    internal void Clear() { Cargo = null; _corners = null; _hasSafe = false; Blocked = false; _stuck = 0f; _detour = 0; _bestDistance = float.MaxValue; }

    internal void Tick(PhysGrabObject cargo, Vector3 goal, float dt, PlayerAvatar operatorPlayer = null)
    {
        if (cargo == null || cargo.rb == null || cargo.rb.isKinematic || cargo.dead || cargo.grabbed) { Clear(); return; }
        if (Cargo != cargo)
        {
            Clear(); Cargo = cargo; _repath = 0f;
            _colliders = cargo.GetComponentsInChildren<Collider>();
        }
        var body = cargo.rb;
        var bounds = new Bounds(body.worldCenterOfMass, Vector3.one * .1f);
        foreach (var c in _colliders) if (c != null && c.enabled && !c.isTrigger) bounds.Encapsulate(c.bounds);
        float halfHeight = Mathf.Max(.1f, bounds.extents.y);
        var center = body.worldCenterOfMass;
        bool onRoute = NavMesh.SamplePosition(center, out var start, halfHeight + .85f, NavMesh.AllAreas) &&
                       Mathf.Abs(center.y - start.position.y) < halfHeight + .85f;
        // Hover assistance only while a valid link exists. Timed native overrides expire after detachment.
        cargo.OverrideZeroGravity(.2f);
        body.WakeUp();
        if (!onRoute)
        {
            Blocked = true; _corners = null;
            body.linearVelocity = Vector3.zero;
            if (_hasSafe && Vector3.Distance(center, _safe) < 2f)
            {
                // Recovery only to the last verified point, not through an arbitrary wall or to the player.
                var back = _safe - center;
                if (!Obstructed(body, back.normalized, back.magnitude))
                    body.linearVelocity = Vector3.ClampMagnitude(back * 5f, 2f);
            }
            return;
        }
        var hover = start.position + Vector3.up * (halfHeight + .15f);
        _safe = hover; _hasSafe = true;
        _repath -= dt;
        if (_repath <= 0f)
        {
            _repath = .4f; _corners = null; _corner = 1;
            if (NavMesh.SamplePosition(goal, out var end, 1.5f, NavMesh.AllAreas) &&
                Mathf.Abs(end.position.y - goal.y) < 1.5f &&
                NavMesh.CalculatePath(start.position, end.position, NavMesh.AllAreas, _path) && _path.status == NavMeshPathStatus.PathComplete)
                _corners = _path.corners;
        }
        Blocked = _corners == null;
        Vector3 aim = hover;
        float radius = Mathf.Max(bounds.extents.x, bounds.extents.z);
        float remaining = Vector3.Distance(start.position, goal);
        if (_corners != null && _corners.Length > 1 && remaining > 1.5f)
        {
            while (_corner < _corners.Length - 1 && Vector3.Distance(start.position, _corners[_corner]) < .35f) _corner++;
            // The navmesh is eroded for a small agent: keep a bulky load's own half-width away from walls at
            // inside corners, so it turns around them instead of scraping and stalling.
            aim = AwayFromEdge(_corners[_corner], radius) + Vector3.up * (halfHeight + .15f);
        }
        // Stuck guard: no progress toward the goal for a while -> step sideways along the navmesh (alternating
        // sides), then retry the route. It never moves the load off the navmesh or through a wall.
        if (remaining < _bestDistance - .25f) { _bestDistance = remaining; _stuck = 0f; }
        else if (remaining > 1.5f) _stuck += dt;
        if (LinkRouteRules.ShouldDetour(_stuck))
        {
            _stuck = 0f; _bestDistance = remaining; _repath = 0f; _detour++;
            var forward = aim - center; forward.y = 0f;
            var side = Vector3.Cross(Vector3.up, forward.sqrMagnitude > .001f ? forward.normalized : body.transform.forward) * ((_detour & 1) == 0 ? 1f : -1f);
            if (NavMesh.SamplePosition(start.position + side * Mathf.Max(.6f, radius), out var aside, .5f, NavMesh.AllAreas) &&
                !NavMesh.Raycast(start.position, aside.position, out _, NavMesh.AllAreas))
                aim = aside.position + Vector3.up * (halfHeight + .15f);
        }
        var delta = aim - center;
        float speedFactor = Skills.PerkRules.LinkSpeed(Skills.SkillEffects.Level(operatorPlayer, Skills.SkillCatalog.LinkOperator));
        Vector3 velocity = Vector3.ClampMagnitude(delta * 4f * speedFactor, 3.5f * speedFactor);
        var step = center + velocity * Mathf.Max(dt, .12f);
        // A navmesh edge guard stops shortcuts over pits, including between path corners.
        if (!NavMesh.SamplePosition(step - Vector3.up * (halfHeight + .15f), out var next, .3f, NavMesh.AllAreas) ||
            NavMesh.Raycast(start.position, next.position, out _, NavMesh.AllAreas))
        { velocity.x = velocity.z = 0f; Blocked = true; _repath = 0f; }
        if (velocity.sqrMagnitude > .001f && Obstructed(body, velocity.normalized, velocity.magnitude * dt + .08f))
        { velocity.x = velocity.z = 0f; velocity.y = Mathf.Max(0f, velocity.y); Blocked = true; _repath = 0f; }
        body.linearVelocity = Vector3.MoveTowards(body.linearVelocity, velocity, 20f * dt);
        body.angularVelocity *= .8f;
    }

    private static Vector3 AwayFromEdge(Vector3 corner, float radius)
    {
        if (!NavMesh.FindClosestEdge(corner, out var edge, NavMesh.AllAreas)) return corner;
        LinkRouteRules.EdgeOffset(corner.x - edge.position.x, corner.z - edge.position.z, radius, out float x, out float z);
        var moved = corner + new Vector3(x, 0f, z);
        // Only accept the shifted corner when it is still reachable on the navmesh in a straight line.
        return NavMesh.SamplePosition(moved, out var hit, .3f, NavMesh.AllAreas) &&
               !NavMesh.Raycast(corner, hit.position, out _, NavMesh.AllAreas) ? hit.position : corner;
    }

    // A sweep that ignores what travels with the load: link/utility drones, players (the follower stops near its
    // player anyway) and other carried items. Walls, doors, furniture and monsters still stop it.
    private static bool Obstructed(Rigidbody body, Vector3 direction, float distance)
    {
        var hits = distance > 0f ? body.SweepTestAll(direction, distance, QueryTriggerInteraction.Ignore) : NoHits;
        foreach (var hit in hits)
        {
            var c = hit.collider;
            if (c == null || c.attachedRigidbody == body) continue;
            if (c.GetComponentInParent<ItemDrone>() != null || c.GetComponentInParent<PlayerAvatar>() != null ||
                c.GetComponentInParent<PlayerTumble>() != null) continue;
            var other = c.GetComponentInParent<PhysGrabObject>();
            if (other != null && other.grabbed) continue;
            return true;
        }
        return false;
    }

    [HarmonyPatch(typeof(PhysGrabObjectImpactDetector), "Break")]
    private static class CargoDamage
    {
        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        private static void Prefix(PhysGrabObjectImpactDetector __instance, ref float valueLost)
        {
            if (SemiFunc.IsMasterClientOrSingleplayer() && __instance.isValuable && valueLost > 0f &&
                __instance.physGrabObject != null && LinkManager.Protects(__instance.physGrabObject)) valueLost = LinkRouteRules.TowedLoss(valueLost);
        }
    }
}

