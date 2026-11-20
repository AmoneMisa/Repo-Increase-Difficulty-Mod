using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace IncreaseHardMod.FableRealm;

/// <summary>
/// The game's own loose physics props (things in vanilla rooms that can be grabbed, pushed and knocked over but are
/// not loot, items, doors, carts or monsters), copied once from Manor and Swiftbroom rooms and set down on the
/// recipe's "prop" spots so Fable Realm rooms react to players like the other maps. Each copy keeps its own
/// physics, sounds and network view; the module that carries it spawns it on every client, as with the doors.
/// </summary>
internal sealed class FableProps
{
    internal readonly List<GameObject> Templates = new();
    internal readonly List<Bounds> Sizes = new();
    internal readonly List<string> Names = new();

    private static readonly string[] Levels = { "Level - Manor", "Level - Wizard" };
    private const float MinSize = .3f, MaxSize = 1.4f;
    private const int MaxKinds = 12;

    internal static FableProps Find(RunManager run, Transform holder)
    {
        var props = new FableProps();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (string wanted in Levels)
            foreach (var level in run.levels)
            {
                if (level == null || level.name != wanted) continue;
                foreach (var list in new[] { level.ModulesNormal1, level.ModulesNormal2, level.ModulesNormal3, level.ModulesPassage1,
                             level.ModulesPassage2, level.ModulesPassage3, level.ModulesDeadEnd1, level.ModulesDeadEnd2, level.ModulesDeadEnd3 })
                {
                    if (list == null) continue;
                    foreach (var reference in list)
                    {
                        var module = reference?.Prefab;
                        if (module == null) continue;
                        foreach (var body in module.GetComponentsInChildren<PhysGrabObject>(true))
                        {
                            if (props.Templates.Count >= MaxKinds) return props;
                            if (!Loose(body)) continue;
                            // Copies in a module are numbered "Chair (2)"; one of each kind is enough.
                            string kind = BaseName(body.name);
                            if (!seen.Add(kind) || !Measure(body.transform, out var bounds)) continue;
                            float largest = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
                            if (largest < MinSize || largest > MaxSize) continue;
                            var copy = Object.Instantiate(body.gameObject, holder);
                            copy.name = body.name;
                            copy.transform.localPosition = Vector3.zero;
                            copy.transform.localRotation = Quaternion.identity;
                            copy.transform.localScale = body.transform.lossyScale;
                            Measure(copy.transform, out bounds);
                            props.Templates.Add(copy); props.Sizes.Add(bounds); props.Names.Add(level.name + " / " + module.name + " / " + body.name);
                        }
                    }
                }
            }
        return props;
    }

    // A loose prop: grabbable physics that is nothing else the game or the mod gives a meaning to.
    private static bool Loose(PhysGrabObject body) =>
        body.GetComponent<ValuableObject>() == null && body.GetComponent<ItemAttributes>() == null &&
        body.GetComponent<PhysGrabHinge>() == null && body.GetComponent<PhysGrabCart>() == null &&
        body.GetComponentInParent<EnemyRigidbody>() == null && body.GetComponentInChildren<HurtCollider>(true) == null &&
        body.GetComponent<Rigidbody>() != null;

    private static string BaseName(string name)
    {
        int paren = name.IndexOf(" (", StringComparison.Ordinal);
        return paren > 0 ? name.Substring(0, paren) : name;
    }

    private static bool Measure(Transform root, out Bounds bounds)
    {
        bounds = default; bool any = false;
        var toRoot = root.worldToLocalMatrix;
        foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>(true))
        {
            if (renderer.GetComponent<MeshFilter>() is not { sharedMesh: { } mesh }) continue;
            var matrix = toRoot * renderer.transform.localToWorldMatrix;
            var b = mesh.bounds;
            for (int i = 0; i < 8; i++)
            {
                var point = matrix.MultiplyPoint3x4(b.center + Vector3.Scale(b.extents,
                    new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f)));
                if (any) bounds.Encapsulate(point); else { bounds = new Bounds(point, Vector3.zero); any = true; }
            }
        }
        return any;
    }

    /// <summary>Sets a prop on every "prop" spot of a room; the same spot always gets the same kind.</summary>
    internal int Place(Transform body, FableRoom room)
    {
        if (Templates.Count == 0) return 0;
        int placed = 0;
        foreach (var anchor in room.anchors)
        {
            if (anchor.kind != "prop") continue;
            var at = new Vector3(anchor.p[0], anchor.p[1], anchor.p[2]);
            int pick = (int)Mathf.Repeat(Mathf.Abs(at.x * 31.7f + at.z * 57.3f + room.id.Length * 13f), Templates.Count);
            pick = Mathf.Clamp(pick, 0, Templates.Count - 1);
            var copy = Object.Instantiate(Templates[pick], body);
            copy.name = Templates[pick].name;
            float yaw = anchor.r != null && anchor.r.Length > 1 ? anchor.r[1] : 0f;
            copy.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            copy.transform.localScale = Templates[pick].transform.localScale;
            // Resting on the floor, a hair above it so physics does not start inside the ground.
            var size = Sizes[pick];
            var offset = copy.transform.localRotation * Vector3.Scale(copy.transform.localScale, new Vector3(size.center.x, size.min.y, size.center.z));
            copy.transform.localPosition = at - offset + Vector3.up * .02f;
            placed++;
        }
        return placed;
    }
}
