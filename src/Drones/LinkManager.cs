using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;

namespace IncreaseHardMod.Drones;

/// <summary>
/// Pairs Link drones by their saved set identity and slot and ties their engaged targets together with a physical cable.
///
/// Each purchase delivers two physical items with one saved set ID. Replicated owned-item keys
/// retain that identity across inventory changes, despawns and reloads. Only the host creates joints.
///
/// The joint is a rope (a ConfigurableJoint with a spherical linear limit): slack when close, a firm pull past
/// the cable length. The limit starts at the gap when connected (capped by the configured range) and
/// reels in toward a compact length while exactly one end is being held. The joint is dropped on
/// prolonged over-stretch; toggling either drone off/on re-links.
/// </summary>
internal static class LinkManager
{
    private const float ReelSpeed = 0.75f;
    private const float CompactLength = 1.25f;

    private sealed class Link
    {
        public UtilityDrone A, B;
        public ConfigurableJoint Joint;
        public Rigidbody BodyA, BodyB;
        public LineRenderer Line;
        public float OverstretchTime;
        public bool Broken;
        public readonly LinkedCargoRoute Route = new();
        public readonly UnityEngine.AI.NavMeshPath DrawPath = new();
        public Vector3[] DrawCorners;
        public float DrawNext;
    }

    private static readonly Dictionary<long, Link> Links = new();
    private static readonly List<UtilityDrone> ListA = new();
    private static readonly List<UtilityDrone> ListB = new();
    private static readonly HashSet<long> Alive = new();
    private static GameObject _linesRoot;

    private static readonly Color[] Palette =
    {
        new(1f, 0.6f, 0.2f), new(0.2f, 0.8f, 1f), new(0.6f, 1f, 0.4f), new(1f, 0.4f, 0.8f),
    };

    private static int ViewId(UtilityDrone d)
    {
        var pv = d.GetComponent<PhotonView>();
        return pv != null && pv.ViewID != 0 ? pv.ViewID : d.GetInstanceID();
    }

    public static void Tick(float dt)
    {
        if (!ModConfig.DronesEnabled.Value)
        {
            foreach (var link in Links.Values)
                DestroyLink(link);
            Links.Clear();
            return;
        }

        ListA.Clear();
        ListB.Clear();
        foreach (var d in UtilityDrone.All)
        {
            // Pair the complete deployed inventory before checking engagement. Otherwise turning
            // one drone off silently pairs all the later A drones with the wrong B drones.
            if (d == null || !d.isActiveAndEnabled || d.kind != DroneKind.Link)
                continue;
            (d.slot == 0 ? ListA : ListB).Add(d);
        }
        LinkSets.TickMigration();
        Alive.Clear();
        foreach (var a in ListA)
        {
            int set = LinkSets.SetOf(a.GetComponent<ItemAttributes>());
            if (set <= 0) continue; // Wait for owned-item identity / host's saved pairing to arrive.
            UtilityDrone b = null;
            foreach (var candidate in ListB)
                if (LinkSets.SetOf(candidate.GetComponent<ItemAttributes>()) == set &&
                    (b == null || ViewId(candidate) < ViewId(b))) b = candidate;
            if (b == null || !Alive.Add(set)) continue;
            if (Links.TryGetValue(set, out var previous) && (previous.A != a || previous.B != b))
            {
                DestroyLink(previous);
                Links.Remove(set);
            }
            if (!Links.TryGetValue(set, out var link))
                Links[set] = link = new Link { A = a, B = b };
            UpdateLink(link, set - 1, dt);
        }

        // Drop pairs whose drones were disabled, destroyed or re-paired.
        List<long> dead = null;
        foreach (var kv in Links)
            if (!Alive.Contains(kv.Key))
                (dead ??= new List<long>()).Add(kv.Key);
        if (dead != null)
            foreach (var k in dead)
            {
                DestroyLink(Links[k]);
                Links.Remove(k);
            }
        LinkConvoy.Tick();
    }

    internal static IEnumerable<(PhysGrabObject A, PhysGrabObject B, PlayerAvatar PA, PlayerAvatar PB)> CargoLinks()
    {
        foreach (var link in Links.Values)
        {
            if (link.Broken || link.A == null || link.B == null || !link.A.Engaged || !link.B.Engaged ||
                link.A.Drone.targetIsEnemy || link.B.Drone.targetIsEnemy ||
                Vector3.Distance(link.A.AttachPoint(), link.B.AttachPoint()) > ModConfig.LinkMaxDistance.Value * 1.5f) continue;
            yield return (link.A.TargetPgo, link.B.TargetPgo, link.A.TargetPlayer, link.B.TargetPlayer);
        }
    }

    private static void UpdateLink(Link link, int index, float dt)
    {
        if (TryRoute(link, index, dt)) return;
        link.Route.Clear();
        var rbA = link.A.TargetBody;
        var rbB = link.B.TargetBody;
        if (rbA == null || rbB == null || rbA == rbB)
        {
            DropJoint(link);
            link.Broken = false;
            SetLine(link, index, null, null, 0);
            return;
        }

        Vector3 ptA = link.A.AttachPoint();
        Vector3 ptB = link.B.AttachPoint();
        float dist = Vector3.Distance(ptA, ptB);
        float range = ModConfig.LinkMaxDistance.Value;

        if (link.BodyA != rbA || link.BodyB != rbB)
        {
            DropJoint(link);
            link.BodyA = rbA;
            link.BodyB = rbB;
            link.Broken = false;
        }

        if (SemiFunc.IsMasterClientOrSingleplayer() && !link.Broken)
            HostJoint(link, rbA, rbB, ptA, ptB, dist, range, dt);

        // 0 = connected, 1 = waiting (out of range), 2 = strained / released
        int state = link.Broken || dist > range * 1.5f ? 2 : dist > range && link.Joint == null ? 1 : 0;
        if (state == 0 && link.Joint != null && dist - link.Joint.linearLimit.limit > 0.5f)
            state = 2;
        SetLine(link, index, ptA, ptB, state);
    }

    private static bool TryRoute(Link link, int index, float dt)
    {
        if (!link.A.Engaged || !link.B.Engaged) return false;
        var a = link.A.TargetPgo; var b = link.B.TargetPgo;
        var playerA = link.A.TargetPlayer; var playerB = link.B.TargetPlayer;
        PhysGrabObject cargo = null;
        Vector3 destination = default;
        if (playerA != null && playerB == null && !playerA.deadSet && !playerA.isDisabled)
        { cargo = b; destination = playerA.transform.position; }
        else if (playerB != null && playerA == null && !playerB.deadSet && !playerB.isDisabled)
        { cargo = a; destination = playerB.transform.position; }
        else if (a != null && b != null && a.grabbed != b.grabbed)
        { cargo = a.grabbed ? b : a; destination = a.grabbed ? a.transform.position : b.transform.position; }
        if (cargo == null || cargo.isPlayer || cargo.GetComponentInParent<EnemyRigidbody>() != null ||
            link.A.Drone.targetIsEnemy || link.B.Drone.targetIsEnemy) return false;
        DropJoint(link); // A straight joint would pull cargo across pits and through inside corners.
        var operatorPlayer = playerA ?? playerB ?? (a != null && a.grabbed ? a.lastPlayerGrabbing : b?.lastPlayerGrabbing);
        if (SemiFunc.IsMasterClientOrSingleplayer()) link.Route.Tick(cargo, destination, dt, operatorPlayer);
        // Every client draws the route the load follows (its own navmesh query), not a straight line through walls.
        if (Time.time >= link.DrawNext)
        {
            link.DrawNext = Time.time + .5f;
            link.DrawCorners = null;
            if (UnityEngine.AI.NavMesh.SamplePosition(cargo.transform.position, out var from, 2f, UnityEngine.AI.NavMesh.AllAreas) &&
                UnityEngine.AI.NavMesh.SamplePosition(destination, out var to, 2f, UnityEngine.AI.NavMesh.AllAreas) &&
                UnityEngine.AI.NavMesh.CalculatePath(from.position, to.position, UnityEngine.AI.NavMesh.AllAreas, link.DrawPath) &&
                link.DrawPath.status == UnityEngine.AI.NavMeshPathStatus.PathComplete && link.DrawPath.corners.Length > 2)
                link.DrawCorners = link.DrawPath.corners;
        }
        bool cargoIsA = cargo == a;
        SetLine(link, index, link.A.AttachPoint(), link.B.AttachPoint(), SemiFunc.IsMasterClientOrSingleplayer() && link.Route.Blocked ? 1 : 0,
            link.DrawCorners, cargoIsA);
        return true;
    }

    internal static bool Protects(PhysGrabObject cargo)
    {
        foreach (var link in Links.Values)
            if (link.A != null && link.B != null && link.A.Engaged && link.B.Engaged && link.Route.Cargo == cargo && !cargo.grabbed)
                return true;
        return false;
    }

    private static void HostJoint(Link link, Rigidbody rbA, Rigidbody rbB, Vector3 ptA, Vector3 ptB,
        float dist, float range, float dt)
    {
        if (link.Joint != null && (link.BodyA != rbA || link.BodyB != rbB))
            DropJoint(link);

        if (link.Joint == null)
        {
            if (dist > range)
                return; // waits for the targets to be brought within range; never teleported together
            // SpringJoint measures displacement from its initial separation; its maxDistance
            // is not an absolute cable length. Use a spherical linear limit in world metres.
            var j = rbA.gameObject.AddComponent<ConfigurableJoint>();
            j.autoConfigureConnectedAnchor = false;
            j.connectedBody = rbB;
            j.anchor = rbA.transform.InverseTransformPoint(ptA);
            j.connectedAnchor = rbB.transform.InverseTransformPoint(ptB);
            j.xMotion = j.yMotion = j.zMotion = ConfigurableJointMotion.Limited;
            j.angularXMotion = j.angularYMotion = j.angularZMotion = ConfigurableJointMotion.Free;
            j.linearLimit = new SoftJointLimit { limit = Mathf.Clamp(dist + 0.1f, 0.1f, range), contactDistance = 0.02f };
            // A hard limit holds heavy loot as well as light loot, using the physics solver.
            j.linearLimitSpring = new SoftJointLimitSpring { spring = 0f, damper = 0f };
            j.enableCollision = true;
            rbA.WakeUp();
            rbB.WakeUp();
            link.Joint = j;
            link.BodyA = rbA;
            link.BodyB = rbB;
            link.OverstretchTime = 0f;
            return;
        }

        var joint = link.Joint;
        // Vanilla may move its beam to another collider on the same target.
        joint.anchor = rbA.transform.InverseTransformPoint(ptA);
        joint.connectedAnchor = rbB.transform.InverseTransformPoint(ptB);

        // Release on prolonged over-stretch instead of yanking things around forever.
        if (dist > joint.linearLimit.limit + 4f)
        {
            link.OverstretchTime += dt;
            if (link.OverstretchTime > 3f)
            {
                DropJoint(link);
                link.Broken = true;
                return;
            }
        }
        else
            link.OverstretchTime = 0f;

        // Reel only while exactly one end is held, nothing is overstretched and neither end is an enemy.
        bool grabbedA = link.A.TargetPgo != null && link.A.TargetPgo.grabbed;
        bool grabbedB = link.B.TargetPgo != null && link.B.TargetPgo.grabbed;
        bool enemy = link.A.Drone.targetIsEnemy || link.B.Drone.targetIsEnemy;
        if (grabbedA != grabbedB && !enemy)
        {
            var limit = joint.linearLimit;
            // Never lengthen a cable that was initially shorter than CompactLength. The limit follows the
            // real gap down, so a held end that is pulled away still drags the other end after it.
            float target = Mathf.Min(CompactLength, limit.limit);
            limit.limit = Mathf.MoveTowards(limit.limit, target, ReelSpeed * dt);
            if (dist < limit.limit) limit.limit = Mathf.Max(target, dist);
            joint.linearLimit = limit;
            rbA.WakeUp();
            rbB.WakeUp();

            // The hard limit alone stalls on heavy or snagged loot: also pull the free end toward the held one
            // whenever the cable is taut.
            var free = grabbedA ? rbB : rbA;
            Vector3 to = (grabbedA ? ptA : ptB) - (grabbedA ? ptB : ptA);
            if (!free.isKinematic && dist >= joint.linearLimit.limit - 0.05f && to.magnitude > CompactLength)
            {
                // Documented reel speed: 0.75 m/s toward the held end.
                Vector3 current = free.GetPointVelocity(free.worldCenterOfMass);
                Vector3 wanted = Vector3.MoveTowards(current, to.normalized * ReelSpeed, 20f * dt);
                free.AddForce(wanted - current, ForceMode.VelocityChange);
            }
        }
    }

    private static void DropJoint(Link link)
    {
        if (link.Joint != null)
            Object.Destroy(link.Joint);
        link.Joint = null;
    }

    private static void DestroyLink(Link link)
    {
        link.Route.Clear();
        DropJoint(link);
        if (link.Line != null)
            Object.Destroy(link.Line.gameObject);
    }

    private static void SetLine(Link link, int index, Vector3? a, Vector3? b, int state, Vector3[] via = null, bool viaFromA = true)
    {
        if (a == null || b == null)
        {
            if (link.Line != null)
                link.Line.enabled = false;
            return;
        }

        if (link.Line == null)
        {
            if (_linesRoot == null)
            {
                _linesRoot = new GameObject("IHM_LinkCables");
                Object.DontDestroyOnLoad(_linesRoot);
            }
            var go = new GameObject("Cable");
            go.transform.SetParent(_linesRoot.transform, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.positionCount = 2;
            lr.useWorldSpace = true;
            lr.startWidth = lr.endWidth = 0.035f;
            lr.numCapVertices = 2;
            var template = link.A.GetComponentInChildren<LineRenderer>(true);
            lr.sharedMaterial = template != null ? template.sharedMaterial : new Material(Shader.Find("Sprites/Default"));
            link.Line = lr;
        }

        Color c = state switch
        {
            1 => new Color(1f, 0.75f, 0.2f, 0.4f),   // faint amber: waiting
            2 => new Color(1f, 0.15f, 0.15f, 1f),    // red: strain / released
            _ => Palette[index % Palette.Length],
        };
        link.Line.startColor = link.Line.endColor = c;
        if (via == null || via.Length <= 2)
        {
            link.Line.positionCount = 2;
            link.Line.SetPosition(0, a.Value);
            link.Line.SetPosition(1, b.Value);
        }
        else
        {
            // Inner corners only (the ends are the drones themselves), lifted to roughly waist height.
            int inner = via.Length - 2;
            link.Line.positionCount = inner + 2;
            link.Line.SetPosition(0, viaFromA ? a.Value : b.Value);
            for (int i = 0; i < inner; i++) link.Line.SetPosition(i + 1, via[i + 1] + Vector3.up * .6f);
            link.Line.SetPosition(inner + 1, viaFromA ? b.Value : a.Value);
        }
        link.Line.enabled = true;
    }
}
