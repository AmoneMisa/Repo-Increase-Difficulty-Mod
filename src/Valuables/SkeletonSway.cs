using System.Collections.Generic;
using UnityEngine;

namespace IncreaseHardMod.Valuables;

/// <summary>
/// The teaching skeleton on its stand: one rigid prop whose bones hang in a chain of joints (pelvis, spine, head,
/// arms, legs) and sway on damped springs when the stand moves, tips or is dropped. Replaces the old per-bone
/// physics bodies, which could be knocked loose and leave bones floating in the air. Purely visual and local:
/// every client sways its own copy from the stand's motion, which the game already syncs.
/// </summary>
internal sealed class SkeletonSway : MonoBehaviour
{
    // Bone groups in parent-first order, their parent bone, and the joint each one hangs from (visual-root space).
    private static readonly string[] Bones =
        { "torso", "pelvis", "head", "upper_arm_left", "forearm_left", "hand_left", "upper_arm_right", "forearm_right", "hand_right",
          "thigh_left", "shin_left", "foot_left", "thigh_right", "shin_right", "foot_right" };
    private static readonly string[] Parents =
        { "", "torso", "torso", "torso", "upper_arm_left", "forearm_left", "torso", "upper_arm_right", "forearm_right",
          "pelvis", "thigh_left", "shin_left", "pelvis", "thigh_right", "shin_right" };
    private static readonly Vector3[] Joints =
    {
        new(0, 1.43f, .04f), new(0, 1.09f, .04f), new(0, 1.49f, .02f),
        new(-.19f, 1.395f, 0), new(-.235f, 1.10f, -.01f), new(-.26f, .85f, -.02f),
        new(.19f, 1.395f, 0), new(.235f, 1.10f, -.01f), new(.26f, .85f, -.02f),
        new(-.095f, 1.02f, 0), new(-.095f, .68f, -.009f), new(-.095f, .26f, -.01f),
        new(.095f, 1.02f, 0), new(.095f, .68f, -.009f), new(.095f, .26f, -.01f),
    };

    private sealed class Joint
    {
        internal Transform pivot;
        internal Vector2 angle, velocity;     // x: tilt about the stand's X axis (forward/back), y: about Z (sideways), degrees
        internal float gain, limit, stiffness, damping;
    }

    private readonly List<Joint> _joints = new();
    private Transform _frame;
    private Vector3 _lastPos, _lastVel, _accel;
    private bool _primed;
    private Quaternion _lastRotation;

    private void Start()
    {
        var art = GetComponent<ValuableArtInstance>();
        if (art == null || !art.EnsureBindings() || art.visualRoot == null) { enabled = false; return; }
        _frame = art.visualRoot;
        // A visible rigid bracket connects the stand pole to the upper spine suspension.
        var support = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        support.name = "Skeleton suspension bracket";
        if (Application.isPlaying) Destroy(support.GetComponent<Collider>());
        else DestroyImmediate(support.GetComponent<Collider>());
        support.transform.SetParent(_frame, false);
        support.transform.localPosition = new Vector3(0, 1.43f, .075f);
        support.transform.localRotation = Quaternion.Euler(90, 0, 0);
        support.transform.localScale = new Vector3(.018f, .035f, .018f);
        foreach (var b in art.bindings)
            if (b.group == "static") { support.GetComponent<Renderer>().sharedMaterial = b.node.GetComponent<Renderer>().sharedMaterial; break; }
        var pivots = new Dictionary<string, Transform>();
        for (int i = 0; i < Bones.Length; i++)
        {
            ValuablePartBinding bone = null;
            foreach (var b in art.bindings) if (b.group == Bones[i]) { bone = b; break; }
            if (bone == null) continue;
            var parent = Parents[i].Length > 0 && pivots.TryGetValue(Parents[i], out var p) ? p : _frame;
            var pivot = new GameObject("Skeleton joint " + Bones[i]).transform;
            pivot.SetParent(parent, false);
            pivot.position = _frame.TransformPoint(Joints[i]);
            foreach (var b in art.bindings) if (b.group == Bones[i]) b.node.SetParent(pivot, true);
            pivots[Bones[i]] = pivot;
            bool core = i < 3, hand = Bones[i].StartsWith("hand") || Bones[i].StartsWith("foot");
            _joints.Add(new Joint
            {
                pivot = pivot,
                gain = core ? .6f : hand ? 1.6f : 1.2f,
                limit = i == 0 ? 3f : core ? 12f : hand ? 22f : 28f,
                stiffness = core ? 70f : 34f,
                damping = core ? 9f : 4.5f,
            });
        }
        if (_joints.Count == 0) enabled = false;
    }

    private void LateUpdate() => Step(Time.deltaTime);

    internal void Step(float elapsed)
    {
        if (_frame == null) return;
        float dt = Mathf.Clamp(elapsed, 0f, .05f);
        if (dt <= 0f) return;
        Vector3 pos = _frame.position;
        if ((pos - _lastPos).sqrMagnitude > 4f || elapsed > .2f) _primed = false;
        var rotation = _frame.rotation;
        float turn = _primed ? Mathf.DeltaAngle(_lastRotation.eulerAngles.y, rotation.eulerAngles.y) / dt : 0;
        _lastRotation = rotation;
        Vector3 vel = _primed ? (pos - _lastPos) / dt : Vector3.zero;
        Vector3 raw = _primed ? (vel - _lastVel) / dt : Vector3.zero;
        _lastPos = pos; _lastVel = vel; _primed = true;
        _accel = Vector3.Lerp(_accel, Vector3.ClampMagnitude(raw, 40f), 1f - Mathf.Exp(-dt * 10f));
        // Bones lag behind the stand's motion, and gravity pulls them back down when it is tipped.
        Vector3 local = _frame.InverseTransformDirection(_accel);
        Vector3 down = _frame.InverseTransformDirection(Vector3.down);
        var push = new Vector2(local.z + down.z * 9f, -local.x - down.x * 9f + Mathf.Clamp(turn * .04f,-8,8));
        foreach (var j in _joints)
        {
            var target = push * j.gain;
            j.velocity += (j.stiffness * (target - j.angle) - j.damping * j.velocity) * dt;
            j.angle += j.velocity * dt;
            j.angle = Vector2.ClampMagnitude(j.angle, j.limit);
            if (!ValuableDynamics.Finite(j.angle.x) || !ValuableDynamics.Finite(j.angle.y)) { j.angle = Vector2.zero; j.velocity = Vector2.zero; }
            j.pivot.localRotation = Quaternion.Euler(j.angle.x, 0f, j.angle.y);
        }
    }
}
