using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;

namespace IncreaseHardMod.Drones;

/// <summary>
/// Pairs Link drones by slot and ties their engaged targets together with a physical cable.
///
/// Every client derives the same pairs from replicated drone state (A drones and B drones, each
/// ordered by PhotonView id, paired index by index - so the first-bought A goes with the first B)
/// and draws the cable. Only the host creates the physics joint, so guests never fight it.
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
        ListA.Sort((x, y) => ViewId(x).CompareTo(ViewId(y)));
        ListB.Sort((x, y) => ViewId(x).CompareTo(ViewId(y)));

        Alive.Clear();
        int pairs = Mathf.Min(ListA.Count, ListB.Count);
        for (int i = 0; i < pairs; i++)
        {
            var a = ListA[i];
            var b = ListB[i];
            long key = ((long)ViewId(a) << 32) | (uint)ViewId(b);
            Alive.Add(key);
            if (!Links.TryGetValue(key, out var link))
                Links[key] = link = new Link { A = a, B = b };
            UpdateLink(link, i, dt);
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
    }

    private static void UpdateLink(Link link, int index, float dt)
    {
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
        if (grabbedA != grabbedB && !enemy && dist <= joint.linearLimit.limit + 0.4f)
        {
            var limit = joint.linearLimit;
            // Never lengthen a cable that was initially shorter than CompactLength.
            limit.limit = Mathf.MoveTowards(limit.limit, Mathf.Min(CompactLength, limit.limit), ReelSpeed * dt);
            joint.linearLimit = limit;
            rbA.WakeUp();
            rbB.WakeUp();
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
        DropJoint(link);
        if (link.Line != null)
            Object.Destroy(link.Line.gameObject);
    }

    private static void SetLine(Link link, int index, Vector3? a, Vector3? b, int state)
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
        link.Line.SetPosition(0, a.Value);
        link.Line.SetPosition(1, b.Value);
        link.Line.enabled = true;
    }
}
