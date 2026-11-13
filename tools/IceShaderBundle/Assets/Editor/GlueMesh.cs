using UnityEngine;

namespace IncreaseHardMod.Visuals
{

/// <summary>
/// Procedural glue geometry (UnityEngine only, C# 9, so tools/IceShaderBundle/Assets/Editor/GlueMesh.cs can be an exact
/// copy for the shader preview - keep the two in sync).
/// <list type="bullet">
/// <item><see cref="Puddle"/>: an irregular blob with a domed middle and a rolled lip at the edge, plus a few
/// droplet splashes around it; seeded, so every client builds the same shape.</item>
/// <item><see cref="Strand"/>: a sagging tube of gel between the floor and the stuck body, thinnest in the middle.
/// Rewritten in place every frame while it stretches.</item>
/// </list>
/// </summary>
internal static class GlueMesh
{
    private const int Sides = 56, Rings = 8;

    internal static Mesh Puddle(int seed, float radius)
    {
        var random = new System.Random(seed);
        float Range(float a, float b) => a + (float)random.NextDouble() * (b - a);
        var phases = new[] { Range(0f, 6.3f), Range(0f, 6.3f), Range(0f, 6.3f), Range(0f, 6.3f) };
        var freqs = new[] { 2f, 3f, 5f, 7f };
        var amps = new[] { .13f, .09f, .05f, .03f };
        float Edge(float angle)
        {
            float r = 1f;
            for (int k = 0; k < 4; k++) r += amps[k] * Mathf.Sin(angle * freqs[k] + phases[k]);
            return radius * r;
        }
        float thickness = radius * .11f;
        float Height(float t) =>
            thickness * Mathf.Pow(Mathf.Max(0f, 1f - t * t), .55f) + thickness * .5f * Mathf.Exp(-Mathf.Pow((t - .84f) / .08f, 2f));

        var vertices = new System.Collections.Generic.List<Vector3>();
        var triangles = new System.Collections.Generic.List<int>();
        vertices.Add(new Vector3(0f, Height(0f), 0f));
        for (int ring = 1; ring <= Rings; ring++)
        {
            float t = (float)ring / Rings;
            for (int s = 0; s < Sides; s++)
            {
                float angle = s * Mathf.PI * 2f / Sides;
                float r = Edge(angle) * t;
                vertices.Add(new Vector3(Mathf.Cos(angle) * r, ring == Rings ? 0f : Height(t), Mathf.Sin(angle) * r));
            }
        }
        for (int s = 0; s < Sides; s++) triangles.AddRange(new[] { 0, 1 + (s + 1) % Sides, 1 + s });
        for (int ring = 1; ring < Rings; ring++)
        {
            int a = 1 + (ring - 1) * Sides, b = 1 + ring * Sides;
            for (int s = 0; s < Sides; s++)
            {
                int n = (s + 1) % Sides;
                triangles.AddRange(new[] { a + s, a + n, b + n, a + s, b + n, b + s });
            }
        }
        // Splashed droplets around the puddle.
        int drops = 4 + random.Next(4);
        for (int d = 0; d < drops; d++)
        {
            float angle = Range(0f, Mathf.PI * 2f);
            float distance = Edge(angle) * Range(1.12f, 1.55f);
            Dome(vertices, triangles, new Vector3(Mathf.Cos(angle) * distance, 0f, Mathf.Sin(angle) * distance),
                radius * Range(.05f, .11f), thickness * Range(.35f, .7f));
        }
        var mesh = new Mesh { name = "IHM Glue Puddle" };
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private static void Dome(System.Collections.Generic.List<Vector3> vertices, System.Collections.Generic.List<int> triangles,
        Vector3 centre, float radius, float height)
    {
        const int sides = 14, rings = 3;
        int top = vertices.Count;
        vertices.Add(centre + Vector3.up * height);
        for (int ring = 1; ring <= rings; ring++)
        {
            float t = (float)ring / rings;
            for (int s = 0; s < sides; s++)
            {
                float angle = s * Mathf.PI * 2f / sides;
                vertices.Add(centre + new Vector3(Mathf.Cos(angle) * radius * t, ring == rings ? 0f : height * Mathf.Sqrt(1f - t * t), Mathf.Sin(angle) * radius * t));
            }
        }
        for (int s = 0; s < sides; s++) triangles.AddRange(new[] { top, top + 1 + (s + 1) % sides, top + 1 + s });
        for (int ring = 1; ring < rings; ring++)
        {
            int a = top + 1 + (ring - 1) * sides, b = top + 1 + ring * sides;
            for (int s = 0; s < sides; s++)
            {
                int n = (s + 1) % sides;
                triangles.AddRange(new[] { a + s, a + n, b + n, a + s, b + n, b + s });
            }
        }
    }

    internal const int StrandSides = 6, StrandSegments = 10;

    internal static Mesh NewStrand()
    {
        var mesh = new Mesh { name = "IHM Glue Strand" };
        mesh.MarkDynamic();
        var vertices = new Vector3[(StrandSegments + 1) * StrandSides];
        var triangles = new int[StrandSegments * StrandSides * 6];
        int k = 0;
        for (int i = 0; i < StrandSegments; i++)
        for (int s = 0; s < StrandSides; s++)
        {
            int a = i * StrandSides + s, b = i * StrandSides + (s + 1) % StrandSides;
            int c = a + StrandSides, d = b + StrandSides;
            triangles[k++] = a; triangles[k++] = c; triangles[k++] = b;
            triangles[k++] = b; triangles[k++] = c; triangles[k++] = d;
        }
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        return mesh;
    }

    /// <summary>World-space strand from <paramref name="from"/> to <paramref name="to"/>, sagging by <paramref name="sag"/>.
    /// The mesh's object is expected at the world origin with no rotation or scale.</summary>
    internal static void Strand(Mesh mesh, Vector3[] buffer, Vector3 from, Vector3 to, float radius, float sag)
    {
        var axis = to - from;
        var forward = axis.sqrMagnitude > 1e-6f ? axis.normalized : Vector3.up;
        var side = Vector3.Cross(forward, Mathf.Abs(forward.y) > .95f ? Vector3.right : Vector3.up).normalized;
        var up = Vector3.Cross(side, forward);
        for (int i = 0; i <= StrandSegments; i++)
        {
            float t = (float)i / StrandSegments;
            var centre = Vector3.Lerp(from, to, t) + Vector3.down * (sag * 4f * t * (1f - t));
            // Thick where it holds on, thin and stretched in the middle.
            float r = radius * (1f - .78f * Mathf.Sin(t * Mathf.PI));
            for (int s = 0; s < StrandSides; s++)
            {
                float angle = s * Mathf.PI * 2f / StrandSides;
                buffer[i * StrandSides + s] = centre + (side * Mathf.Cos(angle) + up * Mathf.Sin(angle)) * r;
            }
        }
        mesh.vertices = buffer;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
    }
}
}
