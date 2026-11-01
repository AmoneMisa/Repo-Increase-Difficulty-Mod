using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;

namespace IncreaseHardMod.Drones;

/// <summary>
/// Pairs engaged Link drones and ties their targets together with a physical cable.
///
/// Every client derives the same pairs from replicated drone state (A drones and B drones, each
/// ordered by PhotonView id, paired index by index - so the first-bought A goes with the first B)
/// and draws the cable. Only the host creates the physics joint, so guests never fight it.
///
/// The joint is a rope (a SpringJoint with minDistance 0): slack when close, a firm pull past
/// maxDistance. maxDistance starts at the gap when connected (capped by the configured range) and
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
        public SpringJoint Joint;
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
        return pv != null ? pv.ViewID : d.GetInstanceID();
    }

    public static void Tick(float dt)
    {
        if (!ModConfig.DronesEnabled.Value)
            return;

        ListA.Clear();
        ListB.Clear();
        foreach (var d in UtilityDrone.All)
        {
            if (d.kind != DroneKind.Link || !d.Engaged || d.TargetBody == null)
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

        // Drop links whose drones were switched off, stowed, destroyed or re-paired.
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
            SetLine(link, index, null, null, 0);
            return;
        }

        Vector3 ptA = link.A.AttachPoint();
        Vector3 ptB = link.B.AttachPoint();
        float dist = Vector3.Distance(ptA, ptB);
        float range = ModConfig.LinkMaxDistance.Value;

        if (SemiFunc.IsMasterClientOrSingleplayer() && !link.Broken)
            HostJoint(link, rbA, rbB, ptA, ptB, dist, range, dt);

        // 0 = connected, 1 = waiting (out of range), 2 = strained / released
        int state = link.Broken || dist > range * 1.5f ? 2 : dist > range && link.Joint == null ? 1 : 0;
        if (state == 0 && link.Joint != null && dist - link.Joint.maxDistance > 0.5f)
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
            var j = rbA.gameObject.AddComponent<SpringJoint>();
            j.connectedBody = rbB;
            j.autoConfigureConnectedAnchor = false;
            j.anchor = rbA.transform.InverseTransformPoint(ptA);
            j.connectedAnchor = rbB.transform.InverseTransformPoint(ptB);
            j.spring = 150f;
            j.damper = 12f;
            j.minDistance = 0f;
            j.maxDistance = Mathf.Clamp(dist + 0.25f, 0.75f, range);
            j.tolerance = 0.02f;
            j.enableCollision = true;
            link.Joint = j;
            link.BodyA = rbA;
            link.BodyB = rbB;
            link.OverstretchTime = 0f;
            return;
        }

        var joint = link.Joint;

        // Release on prolonged over-stretch instead of yanking things around forever.
        if (dist > joint.maxDistance + 4f)
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
        if (grabbedA != grabbedB && !enemy && dist <= joint.maxDistance + 0.4f)
            joint.maxDistance = Mathf.Max(CompactLength, joint.maxDistance - ReelSpeed * dt);
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
