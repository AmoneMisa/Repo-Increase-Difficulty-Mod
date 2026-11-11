using System.Collections.Generic;
using IncreaseHardMod.Visuals;
using UnityEngine;

namespace IncreaseHardMod.Valuables;

/// <summary>Original low-poly models for the ritual offerings and reward.</summary>
internal static class RitualLoot
{
    internal const int SkeletonCount = 5;
    internal const string Cube = "ritual_cube";
    internal static readonly string[] PcParts = { "ritual_monitor", "ritual_tower", "graphics_card", "ram_pair", "rgb_keyboard", "rgb_mouse" };
    internal static readonly string[] ResortParts = { "gold_ingot", "crystal_skull", "bath_gift_set" };
    internal static string SkeletonKey(int i) => $"ritual_bones_{i}";
    internal static bool RequiredFromCollection(string key) =>
        System.Array.IndexOf(PcParts, key) >= 0 || System.Array.IndexOf(ResortParts, key) >= 0;

    internal static IEnumerable<CollectionEntry> RitualOnly
    {
        get
        {
            yield return new("ritual_monitor", LootMap.None, 2f, 2400, 3400);
            yield return new("ritual_tower", LootMap.None, 3f, 3600, 4800);
            yield return new(Cube, LootMap.None, 4f, 250000, 250000, LootFragility.Reinforced);
            for (int i = 0; i < SkeletonCount; i++)
                yield return new(SkeletonKey(i), LootMap.None, 2.2f, 4200, 5200, LootFragility.Sturdy);
        }
    }

    internal static bool IsProcedural(string key) => key != null &&
        (key is "rgb_mouse" or "rgb_keyboard" or "ritual_monitor" or "ritual_tower" or Cube ||
         key.StartsWith("ritual_bones_", System.StringComparison.Ordinal));

    private static readonly Color Case = new(.08f, .092f, .107f), Trim = new(.23f, .27f, .30f);
    private static readonly Color Bone = new(.72f, .62f, .46f), Inset = new(.065f, .049f, .04f);
    internal static readonly Color[] Glow =
    {
        new(.96f, .28f, .15f), new(.93f, .58f, .15f), new(.5f, .91f, .2f),
        new(.12f, .68f, .93f), new(.58f, .35f, .98f), new(.95f, .23f, .61f),
    };

    internal static ValuableArtModel Build(string key)
    {
        var parts = new List<ValuableArtPart>();
        var materials = new List<ValuableArtMaterial>();
        var body = new ProcMesh();
        var detail = new ProcMesh();
        if (key == "rgb_keyboard")
        {
            Box(body, 0f, .043f, 0f, .64f, .083f, .26f);
            Box(detail, 0f, .087f, 0f, .61f, .02f, .227f);
            for (int row = 0; row < 5; row++)
            {
                var lights = new ProcMesh();
                for (int col = 0; col < 15; col++)
                {
                    if (row == 4 && col is >= 4 and <= 9) continue;
                    float x = (col - 7) * .039f, z = (row - 2) * .041f;
                    Box(detail, x, .106f, z, .032f, .02f, .034f);
                    Box(lights, x, .118f, z, .018f, .005f, .013f);
                }
                if (row == 4)
                {
                    Box(detail, -.019f, .106f, .082f, .23f, .02f, .033f);
                    Box(lights, -.019f, .118f, .082f, .18f, .005f, .007f);
                }
                Part("rgb_row_" + row, lights, Glow[row], 1.25f);
            }
        }
        else if (key == "rgb_mouse")
        {
            body.Crystal(ProcMesh.TRS(new(0f, .068f, .008f), Vector3.zero, new(.22f, .14f, .36f)), 12, -.45f, .12f);
            Box(detail, -.05f, .121f, -.061f, .09f, .022f, .115f, -10f);
            Box(detail, .05f, .121f, -.061f, .09f, .022f, .115f, -10f);
            Box(detail, 0f, .142f, -.055f, .026f, .03f, .044f);
            var light = new ProcMesh();
            for (int side = -1; side <= 1; side += 2)
                Box(light, side * .093f, .047f, .037f, .009f, .013f, .16f);
            Box(light, 0f, .117f, .089f, .043f, .012f, .04f);
            Part("rgb_shell", light, Glow[3], 1.4f);
        }
        else if (key == "ritual_monitor")
        {
            Box(body, 0f, .41f, .025f, .67f, .395f, .105f);
            Box(body, 0f, .146f, .045f, .076f, .25f, .07f);
            Box(body, 0f, .035f, .054f, .38f, .06f, .23f);
            Box(detail, 0f, .419f, -.035f, .61f, .337f, .018f);
            var glow = new ProcMesh();
            Box(glow, .265f, .245f, -.038f, .027f, .01f, .009f);
            Part("standby", glow, Glow[3], 1f);
        }
        else if (key == "ritual_tower")
        {
            Box(body, 0f, .355f, 0f, .31f, .69f, .53f);
            Box(detail, 0f, .365f, -.273f, .275f, .63f, .04f);
            for (int i = 0; i < 9; i++) Box(body, 0f, .14f + i * .044f, -.296f, .205f, .012f, .015f);
            var glow = new ProcMesh();
            Box(glow, 0f, .625f, -.301f, .095f, .014f, .013f);
            Part("power", glow, Glow[3], 1f);
        }
        else if (key == Cube)
        {
            body.ChamferBox(ProcMesh.TRS(Vector3.up * .27f, Vector3.zero, Vector3.one * .54f), .17f);
            Part("awakened", CubeGlyphs(.54f), new Color(1f, .61f, .17f), 3f);
            Part("case", body, Case);
            return Model();
        }
        else
        {
            int pose = key[key.Length - 1] - '0';
            Part("bones", SkeletonMesh(pose), Bone);
            Box(detail, -.053f, .144f, .548f, .066f, .018f, .058f);
            Box(detail, .053f, .144f, .548f, .066f, .018f, .058f);
            Box(detail, 0f, .133f, .493f, .036f, .018f, .035f);
            Part("eye sockets", detail, Inset);
            return Model();
        }
        Part("case", body, Case);
        Part("details", detail, Trim);
        return Model();

        ValuableArtModel Model() => new() { parts = parts.ToArray(), materials = materials.ToArray() };
        void Part(string group, ProcMesh shape, Color color, float emission = 0f)
        {
            var mesh = shape.Build(key + " " + group);
            parts.Add(new ValuableArtPart
            {
                name = group, group = group, material = materials.Count,
                vertices = mesh.vertices, normals = mesh.normals, uv = mesh.uv, triangles = mesh.triangles,
                rest = new ArtPose { rotation = Quaternion.identity, scale = Vector3.one },
            });
            materials.Add(new ValuableArtMaterial { name = group, color = color, roughness = .55f, emission = emission });
            if (Application.isPlaying) Object.Destroy(mesh);
            else Object.DestroyImmediate(mesh);
        }
    }

    internal static ProcMesh CubeGlyphs(float size)
    {
        var mesh = new ProcMesh();
        for (int face = 0; face < 4; face++)
        {
            float yaw = face * 90f;
            var rot = Quaternion.Euler(0f, yaw, 0f);
            foreach (float angle in new[] { -45f, 45f })
                mesh.ChamferBox(ProcMesh.TRS(rot * (Vector3.forward * (size * .505f)) + Vector3.up * (size * .5f),
                    new(0f, yaw, angle), new(size * .07f, size * .52f, .016f)), .1f);
        }
        return mesh;
    }

    // One bone layout drives both the carryable skeleton and its chalk trace.
    internal static ProcMesh SkeletonMesh(int pose, bool chalk = false)
    {
        var mesh = new ProcMesh();
        float y = chalk ? .012f : .07f;
        Vector3 P(float x, float z) => new(x, y, z);
        var neck = P(0f, .39f);
        var hip = P(0f, -.09f);
        var skull = P(0f, .535f);
        if (!chalk)
        {
            mesh.Crystal(ProcMesh.TRS(skull + Vector3.up * .025f, Vector3.zero, new(.238f, .14f, .247f)), 12, -.48f, .28f);
            Box(mesh, 0f, .09f, .432f, .168f, .077f, .117f);
        }
        else
        {
            for (int i = 0; i < 12; i++)
            {
                float a = i * Mathf.PI / 6f, b = (i + 1) * Mathf.PI / 6f;
                Stroke(skull + P(Mathf.Cos(a) * .13f, Mathf.Sin(a) * .15f) - P(0,0),
                    skull + P(Mathf.Cos(b) * .13f, Mathf.Sin(b) * .15f) - P(0,0), .025f);
            }
        }
        Stroke(neck, hip, .073f);
        Stroke(P(-.16f, -.09f), P(.16f, -.09f), .087f);
        for (int side = 0; side < 2; side++)
        {
            float sign = side == 0 ? -1f : 1f;
            var shoulder = P(sign * .21f, .31f);
            Stroke(neck, shoulder, .055f);
            for (int rib = 0; rib < 5; rib++)
            {
                float z = .29f - rib * .061f, width = .14f - rib * .009f;
                var edge = P(sign * width, z - .026f);
                Stroke(P(0f, z), edge, .026f);
                Stroke(edge, P(sign * .041f, z - .055f), .026f);
            }
            var elbow = P(sign * (.37f + (pose == 2 ? .09f : 0f)), .18f);
            bool raised = pose == 3 || pose == 0 && side == 0 || pose == 1 && side == 1;
            var hand = P(sign * (raised ? .32f : .4f), raised ? .52f : -.10f);
            Stroke(shoulder, elbow, .063f);
            Stroke(elbow, hand, .055f);
            var leg = P(sign * .12f, -.12f);
            float kneeX = pose == 4 && side == 1 ? .35f : sign * (pose == 2 ? .29f : .16f);
            var knee = P(kneeX, -.4f);
            var foot = P(pose == 4 && side == 1 ? .05f : kneeX + sign * .045f, -.76f);
            Stroke(leg, knee, .076f);
            Stroke(knee, foot, .065f);
            Stroke(foot, foot + P(sign * .08f, -.085f) - P(0,0), .058f);
        }
        return mesh;

        void Stroke(Vector3 a, Vector3 b, float width)
        {
            var d = b - a;
            float yaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
            var center = (a + b) * .5f;
            if (chalk)
            {
                var edge = Vector3.Cross(Vector3.up, d.normalized) * (width * .56f);
                foreach (int sign in new[] { -1, 1 })
                    mesh.ChamferBox(ProcMesh.TRS(center + sign * edge, new(0f, yaw, 0f),
                        new(.011f, .008f, d.magnitude + .027f)), .23f);
            }
            else mesh.ChamferBox(ProcMesh.TRS(center, new(0f, yaw, 0f),
                new(width, width, d.magnitude + .019f)), .23f);
        }
    }

    private static void Box(ProcMesh mesh, float x, float y, float z, float w, float h, float d, float pitch = 0f) =>
        mesh.ChamferBox(ProcMesh.TRS(new(x, y, z), new(pitch, 0f, 0f), new(w, h, d)), .11f);
}
