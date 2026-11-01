using System;
using System.Collections.Generic;
using UnityEngine;

namespace IncreaseHardMod.Patches.Effects;

internal static class UnstuckGeometry
{
    // Scale to small valuables while keeping normal shelf/floor contact below the threshold.
    internal static float PullEmbedDepth(Vector3 size) =>
        Mathf.Clamp(Mathf.Min(size.x, Mathf.Min(size.y, size.z)) * 0.25f, 0.025f, 0.12f);

    internal static bool Opposed(IReadOnlyList<Vector3> normals)
    {
        for (int i = 0; i < normals.Count; i++)
            for (int j = i + 1; j < normals.Count; j++)
                if (Vector3.Dot(normals[i], normals[j]) < -0.5f) return true;
        return false;
    }

    /// <summary>Test every solid child collider at a proposed root pose without moving the object.</summary>
    internal static bool IsClear(Transform root, Collider[] colliders, Vector3 position,
        Quaternion rotation, Func<Collider, bool> blocks)
    {
        Quaternion delta = rotation * Quaternion.Inverse(root.rotation);
        bool tested = false;
        foreach (var mine in colliders)
        {
            if (mine == null || mine.isTrigger || !mine.enabled || !mine.gameObject.activeInHierarchy) continue;
            if (mine is MeshCollider mesh && !mesh.convex) return false;
            tested = true;
            var bounds = mine.bounds;
            var centre = position + delta * (bounds.center - root.position);
            var extents = Abs(delta * Vector3.right) * bounds.extents.x +
                          Abs(delta * Vector3.up) * bounds.extents.y +
                          Abs(delta * Vector3.forward) * bounds.extents.z;
            var colliderPosition = position + delta * (mine.transform.position - root.position);
            var colliderRotation = delta * mine.transform.rotation;
            foreach (var other in Physics.OverlapBox(centre, extents + Vector3.one * 0.01f,
                         Quaternion.identity, ~0, QueryTriggerInteraction.Ignore))
            {
                if (other == mine || !blocks(other)) continue;
                if (Physics.ComputePenetration(mine, colliderPosition, colliderRotation,
                        other, other.transform.position, other.transform.rotation, out _, out float depth) && depth > 0.005f)
                    return false;
            }
        }
        return tested;
    }

    internal static bool Visible(Vector3 eye, Vector3 centre, Func<Collider, bool> blocks)
    {
        var direction = centre - eye;
        if (direction.sqrMagnitude < 0.0001f) return true;
        foreach (var hit in Physics.RaycastAll(eye, direction.normalized, direction.magnitude,
                     ~0, QueryTriggerInteraction.Ignore))
            if (blocks(hit.collider)) return false;
        return true;
    }

    private static Vector3 Abs(Vector3 v) => new(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));
}
