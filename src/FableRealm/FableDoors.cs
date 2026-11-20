using System;
using System.Collections.Generic;
using HarmonyLib;
using Unity.AI.Navigation;
using UnityEngine;
using Object = UnityEngine.Object;

namespace IncreaseHardMod.FableRealm;

/// <summary>
/// A vanilla room door leaf (PhysGrabHinge with its joint, grab object, sounds and map marker), measured in its own
/// space so a copy can be fitted to a Fable Realm doorway. Taken from Swiftbroom Academy when the game has it.
/// </summary>
internal sealed class FableDoorDonor
{
    internal GameObject Leaf;
    /// <summary>Everything the leaf draws, in the leaf's own space.</summary>
    internal Bounds Bounds;
    internal Vector3 Hinge;
    /// <summary>Leaf axis across the doorway: 0 for x, 2 for z.</summary>
    internal int WidthAxis;
    internal string Source;

    // Swiftbroom first: the request was its door. Any other level with hinged room doors is the fallback.
    private const string PreferredLevel = "Level - Wizard";

    internal static FableDoorDonor Find(RunManager run)
    {
        FableDoorDonor fallback = null;
        foreach (var level in run.levels)
        {
            if (level == null) continue;
            bool preferred = level.name == PreferredLevel;
            if (!preferred && fallback != null) continue;
            var door = FromLevel(level);
            if (door == null) continue;
            if (preferred) return door;
            fallback = door;
        }
        return fallback;
    }

    private static FableDoorDonor FromLevel(Level level)
    {
        foreach (var list in new[] { level.ModulesNormal1, level.ModulesNormal2, level.ModulesNormal3, level.ModulesPassage1,
                     level.ModulesPassage2, level.ModulesPassage3, level.ModulesDeadEnd1, level.ModulesDeadEnd2, level.ModulesDeadEnd3 })
        {
            if (list == null) continue;
            foreach (var reference in list)
            {
                var module = reference?.Prefab;
                if (module == null) continue;
                // A room door is the hinge that also draws itself on the map; cupboards and gates do not.
                foreach (var marker in module.GetComponentsInChildren<DirtFinderMapDoor>(true))
                {
                    var door = Measure(marker.gameObject);
                    if (door == null) continue;
                    door.Source = level.name + " / " + module.name + " / " + marker.name;
                    return door;
                }
            }
        }
        return null;
    }

    private static FableDoorDonor Measure(GameObject leaf)
    {
        var hinge = leaf.GetComponent<PhysGrabHinge>();
        var joint = leaf.GetComponent<HingeJoint>();
        // The copy must carry its hinge point with it; a vertical hinge keeps the fitted leaf upright.
        if (hinge == null || joint == null || hinge.hingePoint == null || !hinge.hingePoint.IsChildOf(leaf.transform) ||
            Mathf.Abs(joint.axis.normalized.y) < .9f) return null;
        bool any = false; var bounds = new Bounds();
        var toLeaf = leaf.transform.worldToLocalMatrix;
        foreach (var filter in leaf.GetComponentsInChildren<MeshFilter>(true))
        {
            if (filter.sharedMesh == null) continue;
            var matrix = toLeaf * filter.transform.localToWorldMatrix;
            var mesh = filter.sharedMesh.bounds;
            for (int i = 0; i < 8; i++)
            {
                var point = matrix.MultiplyPoint3x4(mesh.center + Vector3.Scale(mesh.extents,
                    new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f)));
                if (any) bounds.Encapsulate(point); else { bounds = new Bounds(point, Vector3.zero); any = true; }
            }
        }
        // A single leaf: taller than wide, about door-sized.
        if (!any || bounds.size.y < 1.5f || bounds.size.y > 3.5f) return null;
        int axis = bounds.size.x >= bounds.size.z ? 0 : 2;
        if (bounds.size[axis] < .6f || bounds.size[axis] > 2.4f) return null;
        return new FableDoorDonor
        {
            Leaf = leaf, Bounds = bounds, WidthAxis = axis,
            Hinge = leaf.transform.InverseTransformPoint(hinge.hingePoint.position),
        };
    }

    // ---- fitting -----------------------------------------------------------------------------------------------
    // Must match tools/fable_realm/room_finish.py: the frame leaves a 3.9 m by 3.5 m opening inside the wall.
    internal const float HingeOffset = 1.85f, LeafWidth = 1.78f, LeafHeight = 3.3f, FrameInset = .17f, FloorGap = .03f;

    /// <summary>
    /// Builds an inactive pair of leaves for one doorway: <paramref name="holder"/> sits on the doorway's centre line
    /// at floor height with +Z pointing out of the room. FableExits switches the pair on when this room hangs the door.
    /// </summary>
    internal GameObject BuildPair(Transform holder, FableGateLook look)
    {
        var pair = new GameObject("Doors");
        pair.transform.SetParent(holder, false);
        pair.transform.localPosition = new Vector3(0f, 0f, -FrameInset);
        pair.SetActive(false);
        // The game builds the navmesh with the doors in place; monsters walk through doorways and push the leaves.
        pair.AddComponent<NavMeshModifier>().ignoreFromBuild = true;
        for (int i = 0; i < 2; i++)
            Hang(pair.transform, i == 0 ? -1f : 1f, HingeOffset, LeafWidth, LeafHeight, look);
        return pair;
    }

    /// <summary>
    /// One leaf for a house doorway: <paramref name="holder"/> sits on the opening's centre at floor level, +Z out
    /// of the house. Active at once (a house door always hangs) and swings both ways like the gates.
    /// </summary>
    internal GameObject BuildSingle(Transform holder, float width, float height, FableGateLook look)
    {
        var door = new GameObject("Door");
        door.transform.SetParent(holder, false);
        door.AddComponent<NavMeshModifier>().ignoreFromBuild = true;
        Hang(door.transform, -1f, width / 2f, width - .04f, height - FloorGap - .02f, look);
        return door;
    }

    // Pivot on the hinge line; its +X runs from the hinge toward the middle of the doorway.
    private void Hang(Transform parent, float side, float hingeOffset, float width, float height, FableGateLook look)
    {
        var pivot = new GameObject("Hinge " + (side < 0f ? "left" : "right")).transform;
        pivot.SetParent(parent, false);
        pivot.localPosition = new Vector3(side * hingeOffset, FloorGap, 0f);
        pivot.localRotation = Quaternion.Euler(0f, side < 0f ? 0f : 180f, 0f);
        var leaf = Object.Instantiate(Leaf, pivot);
        Fit(leaf, pivot, width, height);
        // The vanilla leaf does the work; the room's own gate is what players see.
        look?.Dress(leaf, pivot, width, height);
    }

    private void Fit(GameObject leaf, Transform pivot, float width, float height)
    {
        leaf.name = Leaf.name;
        var t = leaf.transform;
        // Leaf direction from its hinge to its free edge, turned onto the pivot's +X.
        float far = Bounds.center[WidthAxis] - Hinge[WidthAxis];
        t.localRotation = WidthAxis == 0
            ? Quaternion.Euler(0f, far >= 0f ? 0f : 180f, 0f)
            : Quaternion.Euler(0f, far >= 0f ? 90f : -90f, 0f);
        var scale = Vector3.one;
        scale[WidthAxis] = width / Bounds.size[WidthAxis];
        scale.y = height / Bounds.size.y;
        t.localScale = scale;
        // Hinge line on the pivot, bottom edge on the floor.
        var foot = new Vector3(Hinge.x, Bounds.min.y, Hinge.z);
        t.localPosition = -(t.localRotation * Vector3.Scale(scale, foot));

        // Fable gates swing both ways, like saloon doors: the donor's room door only opens to one side. The game's
        // hinge already handles both signs (its positive and negative offsets), so mirroring the limit is enough.
        var joint = leaf.GetComponent<HingeJoint>();
        if (joint != null && joint.useLimits)
        {
            var limits = joint.limits;
            (limits.min, limits.max) = FableExitRules.BothWays(limits.min, limits.max);
            joint.limits = limits;
        }

        // References the copy brought from outside itself would reach into the donor module: drop them.
        var hinge = leaf.GetComponent<PhysGrabHinge>();
        if (hinge != null)
        {
            hinge.wallTagHinges = Inside(hinge.wallTagHinges, leaf.transform);
            hinge.wallTagObjects = Inside(hinge.wallTagObjects, leaf.transform);
            hinge.lowPassTriggers = Inside(hinge.lowPassTriggers, leaf.transform);
        }
    }

    private static T[] Inside<T>(T[] items, Transform root) where T : Object
    {
        if (items == null) return Array.Empty<T>();
        var kept = new List<T>();
        foreach (var item in items)
        {
            var transform = item is Component c ? c.transform : item is GameObject g ? g.transform : null;
            if (transform != null && transform.IsChildOf(root)) kept.Add(item);
        }
        return kept.ToArray();
    }
}

/// <summary>
/// Sits on every Fable Realm module, on every client. For each exit: shows the wall plug when the generator left
/// that side empty, and switches on the door pair when this room is the one that hangs the door on that edge.
/// Replaces the game's ModulePropSwitch for these rooms (see FableExitRules.WorldSide for why).
/// </summary>
internal sealed class FableExits : MonoBehaviour
{
    // Public so Unity copies them from the template onto each spawned module. Entries may be null.
    public int[] sides;
    public GameObject[] plugs, doors;
    private bool _applied;

    // The start room is spawned without a connection call: its only exit always leads into the level.
    private void Start()
    {
        if (!_applied && GetComponent<StartRoom>() != null) Apply(GetComponent<Module>());
    }

    internal void Apply(Module module)
    {
        if (module == null || sides == null) return;
        _applied = true;
        bool optional = module.type == Module.Type.Normal && GetComponent<StartRoom>() == null;
        float yaw = transform.eulerAngles.y;
        for (int i = 0; i < sides.Length; i++)
        {
            int side = FableExitRules.WorldSide(sides[i], yaw);
            bool connected = FableExitRules.Connected(optional, side, module.ConnectingTop, module.ConnectingRight,
                module.ConnectingBottom, module.ConnectingLeft);
            if (plugs != null && i < plugs.Length && plugs[i] != null) plugs[i].SetActive(!connected);
            if (doors != null && i < doors.Length && doors[i] != null) doors[i].SetActive(connected && FableExitRules.HangsDoor(side));
        }
    }

    // The generator tells each module which sides it joined, on every client; the module then sets up its props.
    [HarmonyPatch(typeof(Module), nameof(Module.ConnectionSetup))]
    private static class AfterConnection
    {
        [HarmonyPostfix]
        private static void Postfix(Module __instance)
        {
            var exits = __instance.GetComponent<FableExits>();
            if (exits != null) exits.Apply(__instance);
        }
    }
}

/// <summary>
/// A floating Fable Realm piece (a loose story page, a star on a ceiling mobile): a slow bob, a little sway and a lazy
/// turn around its resting pose. Each piece gets its own phase from its position, so a room full of pages never moves
/// in step. Purely visual, no physics or network traffic.
/// </summary>
internal sealed class FableDrift : MonoBehaviour
{
    private Vector3 _rest;
    private Quaternion _restRotation;
    private float _phase, _speed;

    private void Start()
    {
        _rest = transform.localPosition;
        _restRotation = transform.localRotation;
        var p = transform.position;
        _phase = Mathf.Repeat(p.x * 1.7f + p.z * 2.3f + p.y * 3.1f, Mathf.PI * 2f);
        _speed = .55f + Mathf.Repeat(_phase * 7.3f, .5f);
    }

    private void Update()
    {
        float t = Time.time * _speed + _phase;
        transform.localPosition = _rest + new Vector3(0f, Mathf.Sin(t) * .08f, 0f);
        transform.localRotation = _restRotation * Quaternion.Euler(Mathf.Sin(t * .8f) * 6f, Mathf.Sin(t * .35f) * 25f, Mathf.Cos(t * .7f) * 5f);
    }
}
