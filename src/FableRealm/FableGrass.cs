using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace IncreaseHardMod.FableRealm;

/// <summary>
/// The game's own tall grass for Fable Realm's grass clumps: a "Dark Grass patch" from Swiftbroom Academy's grounds
/// (Manor's as a fallback), copied once without colliders and planted in place of the recipe's stick blades.
/// The recipe still marks where each clump stands and how wide it is (tools/fable_realm/fable_dressing.tall_grass).
/// </summary>
internal sealed class FableGrass
{
    internal GameObject Template;
    internal Bounds Bounds;
    internal string Source;

    // Asset names, not code: the game's grass is plain meshes with no component of its own to look for.
    private const string PatchName = "Dark Grass patch";
    private static readonly string[] Levels = { "Level - Wizard", "Level - Manor" };
    /// <summary>Recipe blade pieces are named "{clump}-blade-{n}".</summary>
    internal const string BladeMark = "-blade-";
    // A clump in the recipe is about 2 m across; a vanilla patch is planted at that size, within reason.
    private const float ClumpWidth = 2.1f, MinScale = .45f, MaxScale = 1.6f;

    internal static FableGrass Find(RunManager run, Transform holder)
    {
        foreach (string wanted in Levels)
            foreach (var level in run.levels)
            {
                if (level == null || level.name != wanted) continue;
                var grass = FromLevel(level, holder);
                if (grass != null) return grass;
            }
        return null;
    }

    private static FableGrass FromLevel(Level level, Transform holder)
    {
        foreach (var list in new[] { level.StartRooms, level.ModulesNormal1, level.ModulesNormal2, level.ModulesNormal3,
                     level.ModulesPassage1, level.ModulesPassage2, level.ModulesPassage3, level.ModulesDeadEnd1,
                     level.ModulesDeadEnd2, level.ModulesDeadEnd3 })
        {
            if (list == null) continue;
            foreach (var reference in list)
            {
                var module = reference?.Prefab;
                if (module == null) continue;
                foreach (var t in module.GetComponentsInChildren<Transform>(true))
                {
                    // Prefix: the copies in a module are numbered "Dark Grass patch (3)" and so on.
                    if (!t.name.StartsWith(PatchName, StringComparison.Ordinal)) continue;
                    if (!Measure(t, out var bounds) || bounds.size.x > 6f || bounds.size.z > 6f) continue;
                    var copy = Object.Instantiate(t.gameObject, holder);
                    copy.name = "Fable grass";
                    copy.transform.localPosition = Vector3.zero;
                    copy.transform.localRotation = Quaternion.identity;
                    copy.transform.localScale = t.lossyScale;
                    // Grass is walked through: no colliders, nothing that could reach back into the donor module.
                    foreach (var collider in copy.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(collider);
                    foreach (var behaviour in copy.GetComponentsInChildren<MonoBehaviour>(true)) Object.DestroyImmediate(behaviour);
                    Measure(copy.transform, out bounds);
                    return new FableGrass { Template = copy, Bounds = bounds, Source = level.name + " / " + module.name + " / " + t.name };
                }
            }
        }
        return null;
    }

    // Everything the patch draws, in the patch's own space.
    private static bool Measure(Transform root, out Bounds bounds)
    {
        bounds = default; bool any = false;
        var toRoot = root.worldToLocalMatrix;
        foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>(true))
        {
            var filter = renderer.GetComponent<MeshFilter>();
            if (filter == null || filter.sharedMesh == null) continue;
            var matrix = toRoot * renderer.transform.localToWorldMatrix;
            var mesh = filter.sharedMesh.bounds;
            for (int i = 0; i < 8; i++)
            {
                var point = matrix.MultiplyPoint3x4(mesh.center + Vector3.Scale(mesh.extents,
                    new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f)));
                if (any) bounds.Encapsulate(point); else { bounds = new Bounds(point, Vector3.zero); any = true; }
            }
        }
        return any;
    }

    /// <summary>Replaces every stick-blade clump in a room with a vanilla grass patch; returns how many.</summary>
    internal int Plant(Transform body)
    {
        var clumps = new Dictionary<string, List<Transform>>(StringComparer.Ordinal);
        foreach (var t in body.GetComponentsInChildren<Transform>(true))
        {
            int mark = t.name.IndexOf(BladeMark, StringComparison.Ordinal);
            if (mark <= 0) continue;
            string clump = t.name.Substring(0, mark);
            if (!clumps.TryGetValue(clump, out var blades)) clumps[clump] = blades = new List<Transform>();
            blades.Add(t);
        }
        float width = Mathf.Max(Bounds.size.x, Bounds.size.z);
        float scale = width > .01f ? Mathf.Clamp(ClumpWidth / width, MinScale, MaxScale) : 1f;
        foreach (var pair in clumps)
        {
            var centre = Vector3.zero; float floor = float.PositiveInfinity;
            foreach (var blade in pair.Value)
            {
                var local = body.InverseTransformPoint(blade.position);
                centre += local; floor = Mathf.Min(floor, local.y);
                blade.gameObject.SetActive(false);
            }
            centre /= pair.Value.Count;
            var patch = Object.Instantiate(Template, body);
            patch.name = "Grass " + pair.Key;
            // Same turn for a clump on every client: derived from where it stands, not from a random roll.
            float yaw = Mathf.Repeat(centre.x * 37.1f + centre.z * 53.7f, 360f);
            patch.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            patch.transform.localScale = Template.transform.localScale * scale;
            // Bottom of the grass on the floor, middle of its footprint on the clump's centre.
            var offset = patch.transform.localRotation * Vector3.Scale(patch.transform.localScale,
                new Vector3(Bounds.center.x, Bounds.min.y, Bounds.center.z));
            patch.transform.localPosition = new Vector3(centre.x, floor, centre.z) - offset;
        }
        return clumps.Count;
    }
}
