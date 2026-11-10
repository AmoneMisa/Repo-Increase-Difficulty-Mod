using System;
using System.Collections.Generic;
using UnityEngine;

namespace IncreaseHardMod.Cosmetics;

/// <summary>Local secondary motion only: no rigidbodies, colliders or network traffic.</summary>
internal sealed class CosmeticMotion : MonoBehaviour
{
    private sealed class Joint
    {
        internal Transform Node;
        internal Quaternion Rest;
        internal Vector3 Angle, Velocity;
        internal float Limit, Phase;
        internal string Name;
    }

    private readonly List<Joint> _joints = new();
    private Vector3 _position;
    private Quaternion _rotation;
    private bool _placed;
    internal float Activity, Flutter;

    internal void Add(Transform node, string name)
    {
        _joints.Add(new Joint { Node = node, Rest = node.localRotation, Name = name,
            Limit = name.StartsWith("wing", StringComparison.Ordinal) ? 16f : name == "cape" ? 4f : 9f,
            Phase = _joints.Count * 1.7f });
    }

    private void OnEnable() => _placed = false;

    private void LateUpdate() => Step(Time.deltaTime, Time.time);

    internal void Step(float dt, float time)
    {
        if (dt <= 0f) return;
        var position = transform.position;
        var rotation = transform.rotation;
        bool reset = !_placed || dt > .2f || (position - _position).sqrMagnitude > 4f;
        var velocity = reset ? Vector3.zero : transform.InverseTransformDirection((position - _position) / dt);
        float turn = reset ? 0f : Mathf.DeltaAngle(_rotation.eulerAngles.y, rotation.eulerAngles.y) / dt;
        _position = position; _rotation = rotation; _placed = true;
        int steps = Mathf.Max(1, Mathf.CeilToInt(Mathf.Min(dt, .2f) / (1f / 120f)));
        float h = Mathf.Min(dt, .2f) / steps;
        foreach (var joint in _joints)
        {
            if (joint.Node == null) continue;
            if (reset) { joint.Angle = joint.Velocity = Vector3.zero; }
            float side = joint.Name.EndsWith("_l", StringComparison.Ordinal) ? -1f : 1f;
            bool wing = joint.Name.StartsWith("wing", StringComparison.Ordinal);
            float idle = Mathf.Sin(time * (2.1f + Activity * 3f) + (wing ? 0f : joint.Phase));
            var target = new Vector3(velocity.z * -1.4f + idle * .7f,
                -turn * .025f + idle * side * (joint.Name == "tail" ? 3f : 1.2f), velocity.x * 1.3f);
            if (wing) target.z += side * Flutter * Mathf.Sin(time * 13f) * 15f;
            target = Vector3.ClampMagnitude(target, joint.Limit);
            for (int i = 0; i < steps; i++)
            {
                joint.Velocity += ((target - joint.Angle) * 65f - joint.Velocity * 13f) * h;
                joint.Angle = Vector3.ClampMagnitude(joint.Angle + joint.Velocity * h, joint.Limit);
            }
            joint.Node.localRotation = joint.Rest * Quaternion.Euler(joint.Angle);
        }
    }
}

/// <summary>Bends existing merged cloth with a fixed seam; never mutates a cached/shared mesh.</summary>
internal sealed class CosmeticCloth : MonoBehaviour
{
    private sealed class Panel
    {
        internal Mesh Mesh;
        internal Vector3[] Rest, Vertices, Normals, RestNormals;
        internal float[] Weights;
        internal float Amplitude;
    }
    private readonly List<Panel> _panels = new();
    private Vector3 _last, _offset, _velocity;
    private bool _placed;

    internal void Add(MeshFilter filter, Vector3 origin, float seam, float amplitude = .016f)
    {
        var mesh = Instantiate(filter.sharedMesh);
        mesh.MarkDynamic(); filter.sharedMesh = mesh;
        var rest = mesh.vertices;
        var weights = new float[rest.Length];
        for (int i = 0; i < rest.Length; i++)
            weights[i] = Mathf.Pow(Mathf.Clamp01((seam - (rest[i].y + origin.y)) / .6f), 2f);
        // The maximum displacement is bounded to 16 mm, so static expanded bounds suffice.
        var bounds = mesh.bounds; bounds.Expand(amplitude * 2.5f); mesh.bounds = bounds;
        _panels.Add(new Panel { Mesh = mesh, Rest = rest, Vertices = new Vector3[rest.Length],
            Normals = new Vector3[rest.Length], RestNormals = mesh.normals, Weights = weights, Amplitude = amplitude });
    }

    internal static bool Seam(string key, string material, out float seam)
    {
        seam = .15f;
        // Stone stays rigid. Boots, gloves, armour and fitted bodices follow the native rig.
        if (key.StartsWith("angel_", StringComparison.Ordinal)) return false;
        if (key.EndsWith("_skirt", StringComparison.Ordinal)) return true;
        if (key == "legate_cuirass_cape") { seam = .3f; return material.Contains("cloth") || material.Contains("laurel"); }
        if (key == "bride_veil") { seam = .35f; return material.Contains("tulle"); }
        if (key == "gilded_top") { seam = .05f; return true; }
        if (key == "seer_robe") { seam = .1f; return true; }
        if (key == "jester_trousers") { seam = .15f; return true; }
        if (key is "jackal_belt" or "panda_belt" or "kitty_belt" or "sunpriestess_belt" or "assassin_belt") { seam = .20f; return true; }
        return false;
    }

    private void OnEnable() => _placed = false;
    private void LateUpdate() => Step(Time.deltaTime, Time.time);

    internal void Step(float dt, float time)
    {
        if (dt <= 0f) return;
        Vector3 delta = transform.position - _last;
        bool reset = !_placed || dt > .2f || delta.sqrMagnitude > 4f;
        var speed = reset ? Vector3.zero : transform.InverseTransformDirection(delta / dt);
        _placed = true; _last = transform.position;
        if (reset) _offset = _velocity = Vector3.zero;
        var target = Vector3.ClampMagnitude(new Vector3(-speed.x * .003f + Mathf.Sin(time * 1.8f) * .002f, 0f, -speed.z * .003f), .016f);
        int steps = Mathf.Max(1, Mathf.CeilToInt(Mathf.Min(dt, .2f) * 120f));
        float h = Mathf.Min(dt, .2f) / steps;
        for (int i = 0; i < steps; i++)
        {
            _velocity += ((target - _offset) * 70f - _velocity * 14f) * h;
            _offset = Vector3.ClampMagnitude(_offset + _velocity * h, .016f);
        }
        foreach (var p in _panels)
        {
            for (int i = 0; i < p.Rest.Length; i++)
            {
                float flutter = p.Amplitude > .016f ? Mathf.Sin(time * 2.5f + p.Rest[i].x * 8f) * .012f : 0f;
                var offset = Vector3.ClampMagnitude(_offset * (p.Amplitude / .016f) + new Vector3(0, 0, flutter), p.Amplitude);
                p.Vertices[i] = p.Rest[i] + offset * p.Weights[i];
                // Inverse-transpose of the displacement shear; avoids stale lighting on moving cloth.
                var n = p.RestNormals[i];
                float slope = p.Weights[i] > 0f && p.Weights[i] < 1f ? -2f * Mathf.Sqrt(p.Weights[i]) / .6f : 0f;
                float dx = p.Amplitude > .016f ? Mathf.Cos(time * 2.5f + p.Rest[i].x * 8f) * .096f * p.Weights[i] : 0f;
                p.Normals[i] = new Vector3(n.x-dx*n.z, n.y - slope * (offset.x * n.x + offset.z * n.z), n.z).normalized;
            }
            p.Mesh.vertices = p.Vertices; p.Mesh.normals = p.Normals;
        }
    }
    private void OnDestroy() { foreach (var p in _panels) if (p.Mesh != null) Destroy(p.Mesh); }
}
