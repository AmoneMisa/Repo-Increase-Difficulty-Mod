using System.Collections.Generic;
using UnityEngine;

namespace IncreaseHardMod.Visuals;

/// <summary>Tiny flat-shaded mesh builder (boxes, diamonds, quads) for small procedural cosmetics.</summary>
internal sealed class ProcMesh
{
    private readonly List<Vector3> _v = new();
    private readonly List<int> _t = new();

    private void Tri(Vector3 a, Vector3 b, Vector3 c)
    {
        int i = _v.Count;
        _v.Add(a); _v.Add(b); _v.Add(c);
        _t.Add(i); _t.Add(i + 1); _t.Add(i + 2);
    }

    private void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d) { Tri(a, b, c); Tri(a, c, d); }

    /// <summary>Unit cube (-0.5..0.5) transformed by <paramref name="m"/>.</summary>
    internal ProcMesh Box(Matrix4x4 m)
    {
        Vector3 P(float x, float y, float z) => m.MultiplyPoint3x4(new Vector3(x, y, z));
        const float h = .5f;
        Quad(P(-h, -h, h), P(h, -h, h), P(h, h, h), P(-h, h, h));
        Quad(P(h, -h, -h), P(-h, -h, -h), P(-h, h, -h), P(h, h, -h));
        Quad(P(h, -h, h), P(h, -h, -h), P(h, h, -h), P(h, h, h));
        Quad(P(-h, -h, -h), P(-h, -h, h), P(-h, h, h), P(-h, h, -h));
        Quad(P(-h, h, h), P(h, h, h), P(h, h, -h), P(-h, h, -h));
        Quad(P(-h, -h, -h), P(h, -h, -h), P(h, -h, h), P(-h, -h, h));
        return this;
    }

    /// <summary>Unit octahedron (a cut-gem / feather blade), tips at ±0.5 on each axis.</summary>
    internal ProcMesh Diamond(Matrix4x4 m)
    {
        var px = m.MultiplyPoint3x4(new Vector3(.5f, 0, 0)); var nx = m.MultiplyPoint3x4(new Vector3(-.5f, 0, 0));
        var py = m.MultiplyPoint3x4(new Vector3(0, .5f, 0)); var ny = m.MultiplyPoint3x4(new Vector3(0, -.5f, 0));
        var pz = m.MultiplyPoint3x4(new Vector3(0, 0, .5f)); var nz = m.MultiplyPoint3x4(new Vector3(0, 0, -.5f));
        Tri(py, pz, px); Tri(py, px, nz); Tri(py, nz, nx); Tri(py, nx, pz);
        Tri(ny, px, pz); Tri(ny, nz, px); Tri(ny, nx, nz); Tri(ny, pz, nx);
        return this;
    }

    /// <summary>Doubly terminated crystal along Y (tips at ±0.5, radius 0.5): a prism of <paramref name="sides"/>
    /// faces between y = <paramref name="bodyBottom"/> and <paramref name="bodyTop"/>, closed by a pyramid at each end.</summary>
    internal ProcMesh Crystal(Matrix4x4 m, int sides = 6, float bodyBottom = -.3f, float bodyTop = .22f)
    {
        sides = Mathf.Max(3, sides);
        var top = m.MultiplyPoint3x4(new Vector3(0f, .5f, 0f));
        var bottom = m.MultiplyPoint3x4(new Vector3(0f, -.5f, 0f));
        Vector3 Ring(int i, float y)
        {
            float a = i * Mathf.PI * 2f / sides;
            return m.MultiplyPoint3x4(new Vector3(Mathf.Cos(a) * .5f, y, Mathf.Sin(a) * .5f));
        }
        for (int i = 0; i < sides; i++)
        {
            var b0 = Ring(i, bodyBottom); var b1 = Ring(i + 1, bodyBottom);
            var t0 = Ring(i, bodyTop); var t1 = Ring(i + 1, bodyTop);
            Quad(b0, t0, t1, b1);   // outward-facing side (clockwise seen from outside)
            Tri(t0, top, t1);       // upper tip
            Tri(b1, bottom, b0);    // lower tip
        }
        return this;
    }

    /// <summary>Unit cube with bevelled edges and corners (-0.5..0.5, faces inset by <paramref name="bevel"/>),
    /// transformed by <paramref name="m"/>: 6 faces, 12 edge bevels and 8 corner facets, so it reads rounded.</summary>
    internal ProcMesh ChamferBox(Matrix4x4 m, float bevel = .12f)
    {
        float h = .5f, i = .5f - Mathf.Clamp(bevel, .01f, .45f);
        Vector3 V(int axis, float a, float b, float c)
        {
            // a on `axis`, b and c on the next two axes (cyclic), so every face is built the same way.
            var v = Vector3.zero;
            v[axis] = a; v[(axis + 1) % 3] = b; v[(axis + 2) % 3] = c;
            return v;
        }
        for (int axis = 0; axis < 3; axis++)
            foreach (float s in new[] { -1f, 1f })
            {
                Facet(m, V(axis, s * h, -i, -i), V(axis, s * h, i, -i), V(axis, s * h, i, i), V(axis, s * h, -i, i));
                // The edge bevels between this face's axis and the next one, and the corner facets.
                foreach (float t in new[] { -1f, 1f })
                {
                    Facet(m, V(axis, s * h, t * i, -i), V(axis, s * h, t * i, i), V(axis, s * i, t * h, i), V(axis, s * i, t * h, -i));
                    if (axis == 0)
                        foreach (float u in new[] { -1f, 1f })
                            Facet(m, new Vector3(s * h, t * i, u * i), new Vector3(s * i, t * h, u * i), new Vector3(s * i, t * i, u * h));
                }
            }
        return this;
    }

    // A convex face given in the unit shape's own space, wound to face away from its centre, then transformed.
    private void Facet(Matrix4x4 m, params Vector3[] local)
    {
        var centroid = Vector3.zero;
        foreach (var p in local) centroid += p;
        if (Vector3.Dot(Vector3.Cross(local[1] - local[0], local[2] - local[0]), centroid) < 0f)
            System.Array.Reverse(local);
        for (int k = 1; k + 1 < local.Length; k++)
            Tri(m.MultiplyPoint3x4(local[0]), m.MultiplyPoint3x4(local[k]), m.MultiplyPoint3x4(local[k + 1]));
    }

    internal Mesh Build(string name)
    {
        var mesh = new Mesh { name = name };
        mesh.SetVertices(_v);
        mesh.SetTriangles(_t, 0);
        mesh.RecalculateNormals(); // vertices are unshared, so this gives crisp faceted shading
        mesh.RecalculateBounds();
        return mesh;
    }

    internal static Matrix4x4 TRS(Vector3 position, Vector3 euler, Vector3 size) =>
        Matrix4x4.TRS(position, Quaternion.Euler(euler), size);

    /// <summary>Camera-facing unit quad centred on the origin (double-sided materials only).</summary>
    internal static Mesh UnitQuad(string name)
    {
        var mesh = new Mesh { name = name };
        mesh.vertices = new[] { new Vector3(-.5f, -.5f, 0), new Vector3(.5f, -.5f, 0), new Vector3(.5f, .5f, 0), new Vector3(-.5f, .5f, 0) };
        mesh.uv = new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up };
        mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }
}

/// <summary>
/// Marks the root of a purely decorative effect (hero aura, winged circlet) parented to an item or player.
/// Anything that measures or outlines an object (ping silhouette, ping targeting, underglow footprint)
/// must skip renderers under it, or the halo quads would be outlined as part of the object.
/// </summary>
internal sealed class Decoration : MonoBehaviour
{
    internal static bool Is(Renderer renderer) => renderer != null && renderer.GetComponentInParent<Decoration>(true) != null;
}

/// <summary>Procedural soft textures shared by the glow cosmetics.</summary>
internal static class GlowTextures
{
    private static Texture2D _soft, _rays, _star;
    private static Material _sprite;

    internal static Texture2D Soft => _soft ??= Make("IHM Glow Soft", 128, (x, y) =>
    {
        float d = Mathf.Clamp01(Mathf.Sqrt(x * x + y * y));
        float a = 1f - d;
        return a * a * a;
    });

    // Starburst: thin radiating rays that fade outward, like a hero/holy glint.
    internal static Texture2D Rays => _rays ??= Make("IHM Glow Rays", 256, (x, y) =>
    {
        float d = Mathf.Sqrt(x * x + y * y);
        if (d > 1f) return 0f;
        float angle = Mathf.Atan2(y, x);
        float ray = Mathf.Pow(Mathf.Abs(Mathf.Cos(angle * 6f)), 40f) + .6f * Mathf.Pow(Mathf.Abs(Mathf.Cos(angle * 6f + .26f)), 90f);
        float fall = Mathf.Pow(1f - d, 1.6f);
        float core = Mathf.Pow(Mathf.Clamp01(1f - d * 2.4f), 2f);
        return Mathf.Clamp01(ray * fall + core);
    });

    private static Texture2D _strip;

    // Neon strip: a soft rounded-rectangle outline (like an underglow LED tube) with a faint inner wash.
    internal static Texture2D Strip => _strip ??= Make("IHM Glow Strip", 256, (x, y) =>
    {
        const float half = .78f, corner = .3f;
        float qx = Mathf.Abs(x) - (half - corner), qy = Mathf.Abs(y) - (half - corner);
        float outside = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude;
        float d = outside + Mathf.Min(Mathf.Max(qx, qy), 0f) - corner; // signed distance to the outline
        float tube = Mathf.Exp(-(d / .045f) * (d / .045f));
        float halo = Mathf.Exp(-Mathf.Abs(d) / .12f) * .45f;
        float wash = d < 0f ? .12f : 0f;
        // Fade to nothing at the quad's border so the halo never shows a hard square edge.
        float edge = Mathf.Clamp01((1f - Mathf.Max(Mathf.Abs(x), Mathf.Abs(y))) / .14f);
        return Mathf.Clamp01(tube + halo + wash) * edge * edge;
    });

    // Four-point sparkle.
    internal static Texture2D Star => _star ??= Make("IHM Glow Star", 64, (x, y) =>
    {
        float cross = Mathf.Max(Mathf.Clamp01(1f - Mathf.Abs(x) * 9f) * (1f - Mathf.Abs(y)),
                                Mathf.Clamp01(1f - Mathf.Abs(y) * 9f) * (1f - Mathf.Abs(x)));
        float d = Mathf.Sqrt(x * x + y * y);
        float core = Mathf.Pow(Mathf.Clamp01(1f - d * 3f), 2f);
        return Mathf.Clamp01(cross * cross + core);
    });

    /// <summary>Unlit, alpha-blended, double-sided (Sprites/Default ships in every build).</summary>
    internal static Material Sprite(Texture2D texture, string name, int queue = 3100)
    {
        _sprite ??= new Material(Shader.Find("Sprites/Default")) { hideFlags = HideFlags.HideAndDontSave };
        return new Material(_sprite) { name = name, mainTexture = texture, renderQueue = queue, hideFlags = HideFlags.HideAndDontSave };
    }

    private static Texture2D Make(string name, int size, System.Func<float, float, float> alpha)
    {
        var pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float u = (x + .5f) / size * 2f - 1f, v = (y + .5f) / size * 2f - 1f;
            pixels[y * size + x] = new Color(1f, 1f, 1f, alpha(u, v));
        }
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = name, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.HideAndDontSave,
        };
        texture.SetPixels(pixels);
        texture.Apply(false, true);
        return texture;
    }
}
