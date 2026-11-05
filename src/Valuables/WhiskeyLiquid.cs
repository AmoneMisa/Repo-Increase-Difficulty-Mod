using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace IncreaseHardMod.Valuables;

/// <summary>
/// Whiskey inside the big bottle. The liquid is the bottle's interior (model 'liquid' info: a rounded-square
/// superellipse prism matching the glass, inset from it) cut by a
/// flat surface plane. The plane faces the effective gravity (gravity minus the bottle's own acceleration),
/// reached through a damped spring so it sloshes when carried, pushed or dropped. Its height is solved
/// every frame so the liquid keeps the same volume at any tilt: it stays level when tilted, lies along the
/// side when the bottle is on its side and gathers at the shoulder upside down. Purely cosmetic and local:
/// each client animates from the motion it already sees, so nothing is sent over the network.
/// </summary>
internal sealed class WhiskeyLiquid : MonoBehaviour
{
    private const float Stiffness = 38f, Damping = 5.5f;   // underdamped: two or three visible sloshes
    private const float MaxAccel = 7f;                      // m/s^2; keeps hard yanks from flipping the surface
    private static readonly Color Amber = new(.36f, .13f, .025f);

    private Transform _frame;
    private Rigidbody _body;
    private Mesh _mesh;
    private Material _material;
    private const int Sides = 32;
    private readonly List<Vector3[]> _faces = new();  // convex interior prism, outward winding
    private float _volume;
    private float _fill;
    private Vector3 _up = Vector3.up, _upVelocity, _accel, _lastVelocity, _lastPos, _drawnUp;
    private bool _primed, _drawn;

    private readonly List<Vector3> _verts = new();
    private readonly List<int> _tris = new();
    private readonly List<Vector3> _poly = new(), _clip = new(), _cap = new();

    private void Start()
    {
        var art = GetComponent<ValuableArtInstance>();
        if (art == null || !art.EnsureBindings()) { enabled = false; return; }
        var info = ValuableVisuals.Load(art.assetKey).liquid;
        if (info == null || info.halfWidth <= 0 || info.top <= info.bottom) { enabled = false; return; }
        BuildPrism(info.halfWidth, info.bottom, info.top, info.power > 0 ? info.power : 9f);
        _fill = Mathf.Clamp(info.fill, .05f, .98f);
        _frame = art.visualRoot;
        _body = GetComponent<Rigidbody>() ?? GetComponentInParent<Rigidbody>();

        var node = new GameObject("Whiskey liquid");
        node.transform.SetParent(_frame, false); node.layer = gameObject.layer;
        _mesh = new Mesh { name = "IHM whiskey liquid" };
        _mesh.MarkDynamic();
        node.AddComponent<MeshFilter>().sharedMesh = _mesh;
        var renderer = node.AddComponent<MeshRenderer>();
        _material = new Material(Shader.Find("Standard")) { name = "IHM whiskey" };
        _material.SetColor("_Color", Amber);
        _material.SetFloat("_Glossiness", .88f);
        _material.SetFloat("_Metallic", 0f);
        _material.EnableKeyword("_EMISSION");
        _material.SetColor("_EmissionColor", Amber * .35f); // reads as lit-through liquid in dark rooms
        renderer.sharedMaterial = _material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _up = TargetUp();
        Rebuild(_up);
    }

    // Physics-rate acceleration from the rigidbody's velocity (render-rate position deltas are jerky).
    private void FixedUpdate()
    {
        if (_frame == null) return;
        float dt = Mathf.Max(Time.fixedDeltaTime, .001f);
        Vector3 velocity = _body != null ? _body.velocity : (_frame.position - _lastPos) / dt;
        _lastPos = _frame.position;
        if (!_primed) { _lastVelocity = velocity; _primed = true; return; }
        Vector3 raw = Vector3.ClampMagnitude((velocity - _lastVelocity) / dt, MaxAccel * 3f);
        _lastVelocity = velocity;
        _accel = Vector3.ClampMagnitude(Vector3.Lerp(_accel, raw, .35f), MaxAccel);
    }

    private Vector3 TargetUp()
    {
        Vector3 gravity = Physics.gravity.sqrMagnitude > .01f ? Physics.gravity : Vector3.down * 9.81f;
        Vector3 up = _frame.InverseTransformDirection(-(gravity - _accel));
        return IsFinite(up) && up.sqrMagnitude > 1e-6f ? up.normalized : Vector3.up;
    }

    private void LateUpdate()
    {
        if (_mesh == null || _frame == null) return;
        float dt = Mathf.Clamp(Time.deltaTime, 0f, .05f);
        Vector3 target = TargetUp();
        // Damped spring on the surface normal, in small steps for stability at low frame rates.
        int steps = Mathf.CeilToInt(dt / .01f);
        float h = steps > 0 ? dt / steps : 0f;
        for (int i = 0; i < steps; i++)
        {
            _upVelocity += (Stiffness * (target - _up) - Damping * _upVelocity) * h;
            _up += _upVelocity * h;
        }
        if (!IsFinite(_up) || _up.sqrMagnitude < 1e-6f) { _up = target; _upVelocity = Vector3.zero; }
        _up.Normalize();
        if (_drawn && Vector3.Dot(_up, _drawnUp) > .99999f) return; // at rest: nothing to redraw
        Rebuild(_up);
    }

    /// <summary>Box below the plane dot(p, up) = d, with d solved so the liquid volume stays constant.</summary>
    private void Rebuild(Vector3 up)
    {
        float lo = float.MaxValue, hi = float.MinValue;
        foreach (var face in _faces)
            foreach (var p in face) { float k = Vector3.Dot(p, up); lo = Mathf.Min(lo, k); hi = Mathf.Max(hi, k); }
        float target = _fill * _volume;
        for (int i = 0; i < 22; i++)
        {
            float mid = (lo + hi) * .5f;
            if (Clip(up, mid, false) < target) lo = mid; else hi = mid;
        }
        Clip(up, (lo + hi) * .5f, true);
        _mesh.Clear();
        _mesh.SetVertices(_verts);
        _mesh.SetTriangles(_tris, 0);
        _mesh.RecalculateNormals();
        _mesh.RecalculateBounds();
        _drawnUp = up; _drawn = true;
    }

    /// <summary>Rounded-square prism: same superellipse as the glass. Faces wind so that
    /// Cross(b - a, c - a) points outward, which is also Unity's front face.</summary>
    private void BuildPrism(float half, float bottom, float top, float power)
    {
        var low = new Vector3[Sides]; var high = new Vector3[Sides];
        for (int j = 0; j < Sides; j++)
        {
            float a = 2 * Mathf.PI * j / Sides, c = Mathf.Cos(a), s = Mathf.Sin(a);
            float x = half * Mathf.Sign(c) * Mathf.Pow(Mathf.Abs(c), 2f / power), z = half * Mathf.Sign(s) * Mathf.Pow(Mathf.Abs(s), 2f / power);
            low[j] = new Vector3(x, bottom, z); high[j] = new Vector3(x, top, z);
        }
        _faces.Clear();
        _faces.Add((Vector3[])low.Clone());
        var lid = (Vector3[])high.Clone(); System.Array.Reverse(lid); _faces.Add(lid);
        for (int j = 0; j < Sides; j++)
            _faces.Add(new[] { low[j], high[j], high[(j + 1) % Sides], low[(j + 1) % Sides] });
        _volume = Clip(Vector3.up, top + 1f, false);
    }

    /// <summary>Clips the prism to dot(p, up) &lt;= d. Returns the volume; with build, fills the mesh lists.</summary>
    private float Clip(Vector3 up, float d, bool build)
    {
        if (build) { _verts.Clear(); _tris.Clear(); }
        _cap.Clear();
        float volume = 0f;
        foreach (var face in _faces)
        {
            _poly.Clear();
            _poly.AddRange(face);
            _clip.Clear();
            for (int i = 0; i < _poly.Count; i++)
            {
                Vector3 a = _poly[i], b = _poly[(i + 1) % _poly.Count];
                float da = Vector3.Dot(a, up) - d, db = Vector3.Dot(b, up) - d;
                if (da <= 0f) _clip.Add(a);
                if ((da < 0f && db > 0f) || (da > 0f && db < 0f))
                {
                    var p = Vector3.Lerp(a, b, da / (da - db));
                    _clip.Add(p); _cap.Add(p);
                }
            }
            volume += Emit(_clip, build);
        }
        if (_cap.Count >= 3)
        {
            // Order the surface outline counter-clockwise around 'up' so it faces outward (upward).
            Vector3 centre = Vector3.zero;
            foreach (var p in _cap) centre += p;
            centre /= _cap.Count;
            Vector3 u = Vector3.Cross(up, Mathf.Abs(up.y) < .9f ? Vector3.up : Vector3.right).normalized;
            Vector3 v = Vector3.Cross(up, u);
            _cap.Sort((a, b) => Angle(a - centre, u, v).CompareTo(Angle(b - centre, u, v)));
            // The same point arrives once from each of the two faces sharing an edge: drop the repeats.
            for (int i = _cap.Count - 1; i > 0; i--) if ((_cap[i] - _cap[i - 1]).sqrMagnitude < 1e-10f) _cap.RemoveAt(i);
            if (_cap.Count > 1 && (_cap[0] - _cap[_cap.Count - 1]).sqrMagnitude < 1e-10f) _cap.RemoveAt(_cap.Count - 1);
            volume += Emit(_cap, build);
        }
        return volume;
    }

    private static float Angle(Vector3 p, Vector3 u, Vector3 v) => Mathf.Atan2(Vector3.Dot(p, v), Vector3.Dot(p, u));

    // Fan-triangulates a convex polygon. Volume via the divergence theorem (signed tetrahedra from the origin).
    private float Emit(List<Vector3> polygon, bool build)
    {
        if (polygon.Count < 3) return 0f;
        float volume = 0f;
        int start = _verts.Count;
        if (build) _verts.AddRange(polygon);
        for (int i = 1; i < polygon.Count - 1; i++)
        {
            volume += Vector3.Dot(polygon[0], Vector3.Cross(polygon[i], polygon[i + 1])) / 6f;
            // Outward Cross(b - a, c - a) is Unity's front face too (RecalculateNormals agrees), so no flip.
            if (build) { _tris.Add(start); _tris.Add(start + i); _tris.Add(start + i + 1); }
        }
        return volume;
    }

    private static bool IsFinite(Vector3 v) => ValuableDynamics.Finite(v.x) && ValuableDynamics.Finite(v.y) && ValuableDynamics.Finite(v.z);

    private void OnDestroy()
    {
        if (_material != null) Object.Destroy(_material);
        if (_mesh != null) Object.Destroy(_mesh);
    }
}
