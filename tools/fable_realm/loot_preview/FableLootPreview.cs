// Editor-only check of the Fable Realm valuables (src/Valuables/FableLoot.cs): builds every model the way the mod
// does, logs its size, and renders a contact sheet. Run through tools/fable_realm/loot_preview/run.sh, which copies
// this file and the production sources into the Unity validation project.
using System.Collections.Generic;
using System.IO;
using IncreaseHardMod.Valuables;
using UnityEditor;
using UnityEngine;

namespace IncreaseHardMod.Valuables
{
    [System.Serializable] internal sealed class ValuableArtModel { public ValuableArtPart[] parts; public ValuableArtMaterial[] materials; }
    [System.Serializable] internal sealed class ValuableArtMaterial { public string name, texture; public Color color; public float transmission, emission, roughness, metallic; }
    [System.Serializable] internal sealed class ValuableArtPart
    {
        public string name, group; public float phase; public int material;
        public Vector3[] vertices, normals; public Vector2[] uv; public int[] triangles; public IncreaseHardMod.Visuals.ArtPose rest;
    }
}

namespace IncreaseHardMod.Visuals
{
    [System.Serializable] internal sealed class ArtPose { public Vector3 position, scale; public Quaternion rotation; }
}

public static class FableLootPreview
{
    // Spawn volume sizes the recipes use (tools/fable_realm/room_kit.py LOOT).
    static readonly (string name, Vector3 size)[] Volumes =
    {
        ("Tiny", new Vector3(.3f, .3f, .3f)), ("Small", new Vector3(.5f, .5f, .5f)), ("Medium", new Vector3(.9f, .9f, .9f)),
        ("Big", new Vector3(1.4f, 1.4f, 1.4f)), ("Wide", new Vector3(2.2f, 1f, 1f)), ("Tall", new Vector3(.9f, 1.9f, .9f)), ("VeryTall", new Vector3(1.2f, 2.9f, 1.2f)),
    };

    public static void Run()
    {
        string output = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "LootPreview"));
        Directory.CreateDirectory(output);
        const int cell = 512;
        int columns = 4, rows = Mathf.CeilToInt(FableLoot.Entries.Count / (float)columns);
        var sheet = new Texture2D(columns * cell, rows * cell, TextureFormat.RGB24, false);
        var light = new GameObject("Key").AddComponent<Light>();
        light.type = LightType.Directional; light.intensity = 1.25f; light.transform.rotation = Quaternion.Euler(38f, 28f, 0f);
        var fill = new GameObject("Fill").AddComponent<Light>();
        fill.type = LightType.Directional; fill.intensity = .5f; fill.transform.rotation = Quaternion.Euler(20f, -140f, 0f);
        RenderSettings.ambientLight = new Color(.42f, .42f, .46f);
        var camera = new GameObject("Camera").AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.16f, .17f, .2f); camera.fieldOfView = 30f;
        var target = new RenderTexture(cell, cell, 24);
        camera.targetTexture = target;
        for (int index = 0; index < FableLoot.Entries.Count; index++)
        {
            var entry = FableLoot.Entries[index];
            var model = FableLoot.Build(entry.Key);
            var root = new GameObject(entry.Key);
            var bounds = new Bounds(); bool first = true; int triangles = 0;
            foreach (var part in model.parts)
            {
                var mesh = new Mesh { vertices = part.vertices, normals = part.normals, triangles = part.triangles };
                mesh.RecalculateBounds(); triangles += part.triangles.Length / 3;
                var go = new GameObject(part.name); go.transform.SetParent(root.transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var data = model.materials[part.material];
                var material = new Material(Shader.Find("Standard")) { color = data.color };
                material.SetFloat("_Glossiness", 1f - data.roughness); material.SetFloat("_Metallic", data.metallic);
                if (data.emission > 0f) { material.EnableKeyword("_EMISSION"); material.SetColor("_EmissionColor", data.color * data.emission); }
                if (data.transmission > .15f)
                {
                    var c = data.color; c.a = .15f; material.color = c;
                    material.SetFloat("_Mode", 3); material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
                    material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha); material.SetInt("_ZWrite", 0);
                    material.EnableKeyword("_ALPHAPREMULTIPLY_ON"); material.renderQueue = 3000;
                }
                go.AddComponent<MeshRenderer>().sharedMaterial = material;
                if (first) { bounds = mesh.bounds; first = false; } else bounds.Encapsulate(mesh.bounds);
            }
            // The same choice the mod makes: the volume that is exceeded least, then the smallest.
            string volume = "?"; float bestOver = float.MaxValue, bestRoom = float.MaxValue;
            foreach (var (name, size) in Volumes)
            {
                float over = Mathf.Max(0f, (bounds.size.x + .025f - size.x) / size.x) + Mathf.Max(0f, (bounds.size.y + .025f - size.y) / size.y) + Mathf.Max(0f, (bounds.size.z + .025f - size.z) / size.z);
                float room = size.x * size.y * size.z;
                if (over < bestOver - .0001f || (Mathf.Abs(over - bestOver) < .0001f && room < bestRoom)) { bestOver = over; bestRoom = room; volume = name; }
            }
            Debug.Log($"FABLE LOOT {entry.Key}: size {bounds.size.x:F2} x {bounds.size.y:F2} x {bounds.size.z:F2} m, floor {bounds.min.y:F3}, {model.parts.Length} parts, {triangles} triangles, volume {volume}{(bestOver > 0f ? " (overflows by " + bestOver.ToString("P0") + ")" : "")}, {entry.Mass} kg, ${entry.Min}-{entry.Max}");
            // Three-quarter view of the front (-Z) from slightly above, framed on the bounds.
            float radius = bounds.extents.magnitude;
            var direction = new Vector3(.55f, .42f, -1f).normalized;
            camera.transform.position = bounds.center + direction * (radius / Mathf.Sin(camera.fieldOfView * .5f * Mathf.Deg2Rad) * 1.05f);
            camera.transform.LookAt(bounds.center);
            camera.Render();
            RenderTexture.active = target;
            var shot = new Texture2D(cell, cell, TextureFormat.RGB24, false);
            shot.ReadPixels(new Rect(0, 0, cell, cell), 0, 0); shot.Apply();
            sheet.SetPixels((index % columns) * cell, (rows - 1 - index / columns) * cell, cell, cell, shot.GetPixels());
            RenderTexture.active = null;
            Object.DestroyImmediate(root);
        }
        sheet.Apply();
        File.WriteAllBytes(Path.Combine(output, "fable-loot-sheet.png"), sheet.EncodeToPNG());
        Debug.Log("FABLE LOOT SHEET: " + Path.Combine(output, "fable-loot-sheet.png"));
    }
}
