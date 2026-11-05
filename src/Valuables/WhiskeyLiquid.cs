using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace IncreaseHardMod.Valuables;

/// <summary>
/// Whiskey inside the big bottle. A small prism mesh fills the bottle's interior box (model 'liquid' info);
/// every frame its surface is re-levelled to the effective gravity (gravity minus the bottle's own
/// acceleration) through a damped spring, so it stays level while tilted, sloshes when carried, pushed or
/// dropped, and pools at the shoulder when turned upside down. Purely cosmetic and local: each client
/// animates from the bottle motion it already sees, so nothing is sent over the network.
/// </summary>
internal sealed class WhiskeyLiquid : MonoBehaviour
{
    private const int N = 6;            // grid columns per side of the surface
    private const float Spring = 55f, Damping = 4.2f;
    private static readonly Color Amber = new(.36f, .13f, .025f);

    private Transform _frame;
    private Mesh _mesh;
    private Material _material;
    private Vector3[] _rest, _vertices;
    private bool[] _upper;
    private float _bottom, _top, _fill;
    private Vector3 _normal = Vector3.up, _normalVelocity, _lastPos, _lastVel;
    private bool _primed;

    private void Start()
    {
        var art = GetComponent<ValuableArtInstance>();
        if (art == null || !art.EnsureBindings()) { enabled = false; return; }
        var info = ValuableVisuals.Load(art.assetKey).liquid;
        if (info == null || info.halfWidth <= 0 || info.top <= info.bottom) { enabled = false; return; }
        _bottom = info.bottom; _top = info.top; _fill = Mathf.Clamp(info.fill, .05f, .98f);
        _frame = art.visualRoot;

        var node = new GameObject("Whiskey liquid");
        node.transform.SetParent(_frame, false); node.layer = gameObject.layer;
        _mesh = Build(info.halfWidth);
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
        Shape(Vector3.up);
    }

    /// <summary>Top and bottom grids plus four side walls; each vertex remembers whether it is on the surface.</summary>
    private Mesh Build(float half)
    {
        var verts = new List<Vector3>(); var upper = new List<bool>(); var tris = new List<int>();
        void Grid(bool top)
        {
            int start = verts.Count;
            for (int i = 0; i <= N; i++)
            for (int j = 0; j <= N; j++)
            {
                verts.Add(new Vector3(-half + 2 * half * i / N, 0, -half + 2 * half * j / N)); upper.Add(top);
            }
            for (int i = 0; i < N; i++)
            for (int j = 0; j < N; j++)
            {
                int a = start + i * (N + 1) + j, b = a + 1, c = a + N + 1, d = c + 1;
                if (top) tris.AddRange(new[] { a, b, d, a, d, c }); else tris.AddRange(new[] { a, d, b, a, c, d });
            }
        }
        Grid(true); Grid(false);
        // Side walls: (edge start, edge direction, outward normal sign) around the square.
        var edges = new[]
        {
            (new Vector3(-half, 0, -half), new Vector3(1, 0, 0)), (new Vector3(half, 0, -half), new Vector3(0, 0, 1)),
            (new Vector3(half, 0, half), new Vector3(-1, 0, 0)), (new Vector3(-half, 0, half), new Vector3(0, 0, -1)),
        };
        foreach (var (origin, dir) in edges)
        {
            int start = verts.Count;
            for (int k = 0; k <= N; k++)
            {
                var p = origin + dir * (2 * half * k / N);
                verts.Add(p); upper.Add(false);
                verts.Add(p); upper.Add(true);
            }
            for (int k = 0; k < N; k++)
            {
                int a = start + 2 * k, b = a + 1, c = a + 2, d = a + 3;
                tris.AddRange(new[] { a, b, d, a, d, c });
            }
        }
        _rest = verts.ToArray(); _vertices = (Vector3[])_rest.Clone(); _upper = upper.ToArray();
        var mesh = new Mesh { name = "IHM whiskey liquid" };
        mesh.MarkDynamic();
        mesh.vertices = _vertices; mesh.triangles = tris.ToArray();
        mesh.RecalculateNormals(); mesh.RecalculateBounds();
        return mesh;
    }

    private void LateUpdate()
    {
        if (_mesh == null || _frame == null) return;
        float dt = Mathf.Clamp(Time.deltaTime, .0001f, .05f);
        // Effective gravity in the bottle's frame: real gravity minus the bottle's acceleration.
        Vector3 pos = _frame.position;
        Vector3 vel = _primed ? (pos - _lastPos) / dt : Vector3.zero;
        Vector3 accel = _primed ? Vector3.ClampMagnitude((vel - _lastVel) / dt, 40f) : Vector3.zero;
        _lastPos = pos; _lastVel = vel; _primed = true;
        Vector3 gravity = Physics.gravity.sqrMagnitude > .01f ? Physics.gravity : Vector3.down * 9.81f;
        Vector3 target = _frame.InverseTransformDirection(-(gravity - accel)).normalized;
        if (!IsFinite(target)) target = Vector3.up;
        // Damped spring on the surface normal: overshoots a little, then settles (the slosh).
        _normalVelocity += (Spring * (target - _normal) - Damping * _normalVelocity) * dt;
        _normal = (_normal + _normalVelocity * dt).normalized;
        if (!IsFinite(_normal)) { _normal = target; _normalVelocity = Vector3.zero; }
        Shape(_normal);
    }

    private void Shape(Vector3 up)
    {
        float height = _top - _bottom;
        bool upright = up.y >= 0;
        // Keep the slope finite when the bottle lies on its side.
        float ny = upright ? Mathf.Max(up.y, .22f) : Mathf.Min(up.y, -.22f);
        float centre = upright ? _bottom + _fill * height : _top - _fill * height;
        for (int i = 0; i < _rest.Length; i++)
        {
            var r = _rest[i];
            float plane = Mathf.Clamp(centre - (up.x * r.x + up.z * r.z) / ny, _bottom, _top);
            // Upright: the floor stays at the bottom and the surface floats. Inverted: the liquid lies against the shoulder.
            float y = upright ? (_upper[i] ? plane : _bottom) : (_upper[i] ? _top : plane);
            _vertices[i] = new Vector3(r.x, y, r.z);
        }
        _mesh.vertices = _vertices;
        _mesh.RecalculateNormals();
        _mesh.RecalculateBounds();
    }

    private static bool IsFinite(Vector3 v) => ValuableDynamics.Finite(v.x) && ValuableDynamics.Finite(v.y) && ValuableDynamics.Finite(v.z);

    private void OnDestroy()
    {
        if (_material != null) Object.Destroy(_material);
        if (_mesh != null) Object.Destroy(_mesh);
    }
}
