using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace IncreaseHardMod.Visuals;

/// <summary>
/// The rope net thrown by the Net Launcher, draped over a pinned monster: a square-cornered dome of knotted
/// rope from the floor up over the body, held down by iron weights at the corners and edges. It drops on
/// with a bounce, twitches while the monster struggles, bursts apart into flying pieces when the monster
/// tears free, or sags away when the hold simply runs out. Local visuals; the host owns the hold itself.
/// </summary>
internal sealed class NetDrape : MonoBehaviour
{
    private const int Grid = 10;         // knots per side
    private const int Chunks = 3;        // the net tears into Chunks x Chunks pieces
    private static Material _rope, _iron;

    private sealed class Piece
    {
        public Transform T;
        public Vector3 Rest, Velocity, Spin;
    }

    private readonly List<Piece> _pieces = new();
    private float _age, _end = -1f;
    private bool _torn;

    private bool _localOwner;

    /// <param name="localOwner">The net is on the local player: hide it from their own camera (shadow kept),
    /// the same way the game hides your own body.</param>
    internal static NetDrape Create(Transform host, Bounds body, bool localOwner = false)
    {
        var net = new GameObject("IHM Net", typeof(Decoration)).AddComponent<NetDrape>();
        net._localOwner = localOwner;
        net.Build(host, body);
        return net;
    }

    /// <summary>Remove the net: torn = burst into flying pieces, otherwise sag away.</summary>
    internal void Release(bool torn)
    {
        if (_end >= 0f) return;
        _end = _age;
        _torn = torn;
        foreach (var p in _pieces)
        {
            var outward = p.Rest.sqrMagnitude > .0001f ? p.Rest.normalized : Random.onUnitSphere;
            p.Velocity = torn ? (outward * Random.Range(2.5f, 4.5f) + Vector3.up * Random.Range(1.5f, 3f)) : Vector3.down * .4f;
            p.Spin = torn ? Random.insideUnitSphere * 540f : Vector3.zero;
        }
    }

    private static bool Finite(Vector3 v) => !float.IsNaN(v.x) && !float.IsNaN(v.y) && !float.IsNaN(v.z) &&
                                              !float.IsInfinity(v.x) && !float.IsInfinity(v.y) && !float.IsInfinity(v.z);

    private void Build(Transform host, Bounds body)
    {
        _rope ??= Lit("IHM Net Rope", new Color(.86f, .79f, .62f), .12f, .0f);
        _iron ??= Lit("IHM Net Weight", new Color(.28f, .29f, .31f), .55f, .6f);
        // A host without renderers/colliders yields empty or non-finite bounds: use a small default net.
        if (!Finite(body.center) || !Finite(body.extents) || body.extents.sqrMagnitude < 1e-6f)
            body = new Bounds(host.position + Vector3.up * .5f, Vector3.one);

        // Floor under the monster (ignoring the monster itself), else the bottom of its body.
        float floor = body.min.y;
        float best = float.NegativeInfinity;
        foreach (var hit in Physics.RaycastAll(body.center, Vector3.down, body.extents.y + 2f, SemiFunc.LayerMaskGetVisionObstruct(), QueryTriggerInteraction.Ignore))
        {
            if (hit.collider == null || hit.collider.transform.root == host.root) continue; // not the monster itself
            if (hit.collider.GetComponentInParent<EnemyRigidbody>() != null || hit.collider.GetComponentInParent<Enemy>() != null ||
                hit.collider.GetComponentInParent<PlayerAvatar>() != null || hit.collider.GetComponentInParent<PlayerController>() != null) continue;
            if (hit.point.y > best) best = hit.point.y;
        }
        if (!float.IsNegativeInfinity(best)) floor = best;
        transform.SetParent(host, true);
        transform.position = new Vector3(body.center.x, floor, body.center.z);
        transform.rotation = Quaternion.identity;

        float rx = body.extents.x * 1.3f + .12f, rz = body.extents.z * 1.3f + .12f;
        float top = body.max.y - floor + .06f;
        float scale = Mathf.Clamp((rx + rz) * .5f, .35f, 2f);
        float thick = .011f * scale + .004f;

        Vector3 Knot(int i, int j)
        {
            float u = i / (float)(Grid - 1) * 2f - 1f, v = j / (float)(Grid - 1) * 2f - 1f;
            // Map the knot grid onto a disc: a round net, not a square tent.
            float du = u * Mathf.Sqrt(1f - v * v * .5f), dv = v * Mathf.Sqrt(1f - u * u * .5f);
            float r = Mathf.Clamp01(Mathf.Sqrt(du * du + dv * dv));
            // Soft dome that clings to the body and spreads out to the floor at the rim.
            // cos(π/2) can round below zero; a fractional power would poison the rim vertices.
            float y = top * Mathf.Pow(Mathf.Max(0f, Mathf.Cos(r * Mathf.PI * .5f)), .75f);
            float sag = Mathf.Sin(u * 3.1f + v * 2.3f) * .02f * scale;
            return new Vector3(du * rx, Mathf.Max(.02f, y + sag), dv * rz);
        }

        // Shrink-wrap: the dome above is only a first guess from the bounding box. Push every knot out of
        // the monster's own colliders (heads, arms, spikes) so the ropes lie ON the body, never through it.
        var monster = host.GetComponentInParent<EnemyParent>() != null ? host.GetComponentInParent<EnemyParent>().transform :
            host.GetComponentInParent<PlayerAvatar>() != null ? host.GetComponentInParent<PlayerAvatar>().transform : host.root;
        float clearance = thick * 2.5f + .025f;
        bool IsMonster(Collider c) => c != null && c.transform.IsChildOf(monster);
        Vector3 Conform(Vector3 local)
        {
            var origin = transform.position;
            var world = origin + local;
            var axis = new Vector3(origin.x, world.y, origin.z);
            var outward = world - axis;
            float radial = outward.magnitude;
            if (radial > .04f)
            {
                // Sideways: from well outside, inward toward the axis; the first monster hit is its surface.
                var dir = outward / radial;
                float reach = radial + 2f;
                float surface = -1f;
                foreach (var hit in Physics.RaycastAll(axis + dir * reach, -dir, reach, ~0, QueryTriggerInteraction.Ignore))
                    if (IsMonster(hit.collider)) surface = Mathf.Max(surface, reach - hit.distance);
                if (surface + clearance > radial) world = axis + dir * (surface + clearance);
            }
            // From above: never let a knot sit under the top of a head or shoulder.
            float lift = float.NegativeInfinity;
            foreach (var hit in Physics.RaycastAll(world + Vector3.up * 2f, Vector3.down, 2f + clearance, ~0, QueryTriggerInteraction.Ignore))
                if (IsMonster(hit.collider)) lift = Mathf.Max(lift, hit.point.y);
            if (!float.IsNegativeInfinity(lift) && lift + clearance > world.y) world.y = lift + clearance;
            return world - origin;
        }
        var knots = new Vector3[Grid, Grid];
        for (int i = 0; i < Grid; i++)
        for (int j = 0; j < Grid; j++)
        {
            var knot = Conform(Knot(i, j));
            knots[i, j] = Finite(knot) ? knot : Knot(i, j); // never let a degenerate ray result poison the mesh
        }

        // Ropes and knots, grouped by which chunk of the net they belong to.
        var meshes = new ProcMesh[Chunks * Chunks];
        var centres = new Vector3[Chunks * Chunks];
        var counts = new int[Chunks * Chunks];
        for (int k = 0; k < meshes.Length; k++) meshes[k] = new ProcMesh();
        int ChunkOf(int i, int j) => Mathf.Min(Chunks - 1, i * Chunks / Grid) * Chunks + Mathf.Min(Chunks - 1, j * Chunks / Grid);
        void Rope(ProcMesh m, Vector3 a, Vector3 b)
        {
            var d = b - a;
            if (d.sqrMagnitude < 1e-6f) return;
            m.Box(Matrix4x4.TRS((a + b) * .5f, Quaternion.LookRotation(d), new Vector3(thick, thick, d.magnitude + thick)));
        }
        for (int i = 0; i < Grid; i++)
        for (int j = 0; j < Grid; j++)
        {
            int c = ChunkOf(i, j);
            var p = knots[i, j];
            centres[c] += p; counts[c]++;
            meshes[c].Diamond(Matrix4x4.TRS(p, Quaternion.Euler(0, 45, 0), Vector3.one * thick * 3.2f));
            if (i + 1 < Grid) Rope(meshes[c], p, knots[i + 1, j]);
            if (j + 1 < Grid) Rope(meshes[c], p, knots[i, j + 1]);
        }
        for (int c = 0; c < meshes.Length; c++)
        {
            var centre = counts[c] > 0 ? centres[c] / counts[c] : Vector3.zero;
            var piece = Part("Net piece", _rope, null);
            piece.localPosition = centre;
            // Rebuild the chunk mesh around its own centre so it can spin about it when torn.
            piece.GetComponent<MeshFilter>().sharedMesh = Recentered(meshes[c].Build("IHM Net Piece"), centre);
            _pieces.Add(new Piece { T = piece, Rest = centre });
        }

        // Iron weights at the corners and edge midpoints.
        foreach (var (i, j) in new[] { (0, 0), (0, Grid - 1), (Grid - 1, 0), (Grid - 1, Grid - 1), (0, Grid / 2), (Grid - 1, Grid / 2), (Grid / 2, 0), (Grid / 2, Grid - 1) })
        {
            var at = knots[i, j]; at.y = Mathf.Max(at.y, .035f * scale + .01f);
            var weight = Part("Net weight", _iron, PrimitiveType.Sphere);
            weight.localPosition = at;
            weight.localScale = Vector3.one * (.075f * scale + .02f);
            _pieces.Add(new Piece { T = weight, Rest = at });
        }

        // Drop in from above.
        transform.localScale = Vector3.one * 1.25f;
        foreach (var p in _pieces) p.T.localPosition = p.Rest + Vector3.up * .5f;
    }

    private static Mesh Recentered(Mesh mesh, Vector3 centre)
    {
        var v = mesh.vertices;
        for (int i = 0; i < v.Length; i++) v[i] -= centre;
        mesh.vertices = v;
        mesh.RecalculateBounds();
        mesh.hideFlags = HideFlags.DontSave;
        return mesh;
    }

    private Transform Part(string name, Material material, PrimitiveType? primitive)
    {
        GameObject obj;
        if (primitive.HasValue)
        {
            obj = GameObject.CreatePrimitive(primitive.Value);
            Destroy(obj.GetComponent<Collider>());
            obj.name = name;
        }
        else
        {
            obj = new GameObject(name);
            obj.AddComponent<MeshFilter>();
            obj.AddComponent<MeshRenderer>();
        }
        obj.transform.SetParent(transform, false);
        var r = obj.GetComponent<MeshRenderer>();
        r.sharedMaterial = material;
        r.shadowCastingMode = _localOwner ? ShadowCastingMode.ShadowsOnly : ShadowCastingMode.On;
        r.receiveShadows = true;
        if (_localOwner) obj.layer = LayerMask.NameToLayer("PlayerVisualsLocal");
        return obj.transform;
    }

    private static Material Lit(string name, Color colour, float smooth, float metal)
    {
        var m = new Material(Shader.Find("Standard")) { name = name, color = colour, hideFlags = HideFlags.HideAndDontSave };
        m.SetFloat("_Glossiness", smooth);
        m.SetFloat("_Metallic", metal);
        return m;
    }

    private void LateUpdate()
    {
        _age += Time.deltaTime;
        if (_end < 0f)
        {
            // Land with a small bounce, then twitch as the monster strains against it.
            float land = Mathf.Clamp01(_age / .22f);
            float bounce = land < 1f ? (1f - land) * .5f : Mathf.Max(0f, Mathf.Sin((_age - .22f) * 18f)) * .03f * Mathf.Exp(-(_age - .22f) * 6f);
            transform.localScale = Vector3.one * Mathf.Lerp(1.25f, 1f, land);
            for (int i = 0; i < _pieces.Count; i++)
            {
                var p = _pieces[i];
                float twitch = Mathf.PerlinNoise(Time.time * 3f, i * 1.7f) - .5f;
                p.T.localPosition = p.Rest + Vector3.up * (bounce + twitch * .025f);
            }
            return;
        }

        float t = _age - _end;
        float life = _torn ? .7f : .45f;
        if (t >= life) { Destroy(gameObject); return; }
        float dt = Time.deltaTime;
        foreach (var p in _pieces)
        {
            p.Velocity += Physics.gravity * dt * (_torn ? 1f : .2f);
            p.T.localPosition += p.Velocity * dt;
            p.T.localRotation *= Quaternion.Euler(p.Spin * dt);
            p.T.localScale = (p.T.name == "Net weight" ? p.T.localScale : Vector3.one) * (_torn ? 1f : Mathf.Lerp(1f, .97f, t / life));
        }
        if (!_torn) transform.localScale = Vector3.one * Mathf.Lerp(1f, .92f, t / life);
        if (t > life * .6f)
        {
            float shrink = Mathf.Clamp01(1f - (t - life * .6f) / (life * .4f));
            foreach (var p in _pieces) if (p.T.name != "Net weight") p.T.localScale = Vector3.one * shrink;
        }
    }

    private void OnDestroy()
    {
        foreach (var p in _pieces)
        {
            if (p.T == null) continue;
            var filter = p.T.GetComponent<MeshFilter>();
            if (filter != null && filter.sharedMesh != null && filter.sharedMesh.name == "IHM Net Piece") Destroy(filter.sharedMesh);
        }
    }
}
