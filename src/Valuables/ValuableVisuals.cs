using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using IncreaseHardMod.Upgrades;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace IncreaseHardMod.Valuables;

internal static class ValuableVisuals
{
    private static readonly Dictionary<string, ValuableArtModel> Models = new();
    internal static ValuableArtModel Load(string key)
    {
        if (Models.TryGetValue(key, out var value)) return value;
        if (RitualLoot.IsProcedural(key)) return Models[key] = RitualLoot.Build(key);
        var assembly = Assembly.GetExecutingAssembly();
        var names = assembly.GetManifestResourceNames();
        string resource = names.FirstOrDefault(n => n.EndsWith(".v11_" + key + ".mesh.json", StringComparison.Ordinal))
            ?? names.FirstOrDefault(n => n.EndsWith(".v10_" + key + ".mesh.json", StringComparison.Ordinal))
            ?? names.FirstOrDefault(n => n.EndsWith(".v09_" + key + ".mesh.json", StringComparison.Ordinal))
            ?? names.FirstOrDefault(n => n.EndsWith(".v08_" + key + ".mesh.json", StringComparison.Ordinal));
        if (resource == null) throw new FileNotFoundException("Missing valuable art: " + key);
        using var stream = assembly.GetManifestResourceStream(resource);
        var model = JsonConvert.DeserializeObject<ValuableArtModel>(Visuals.EmbeddedArt.ReadText(stream), new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.None, MaxDepth = 64 });
        if (model?.parts == null || model.parts.Length == 0 || model.materials == null) throw new InvalidDataException("Empty valuable model: " + key);
        foreach (var part in model.parts)
        {
            if (part.vertices == null || part.normals?.Length != part.vertices.Length || part.uv?.Length != part.vertices.Length ||
                part.triangles == null || part.triangles.Length % 3 != 0 || part.material < 0 || part.material >= model.materials.Length)
                throw new InvalidDataException("Invalid valuable mesh: " + key);
            foreach (int index in part.triangles) if (index < 0 || index >= part.vertices.Length) throw new InvalidDataException("Invalid valuable index");
        }
        return Models[key] = model;
    }

    internal static ValuableArtInstance Attach(GameObject go, string key, Material native)
    {
        var model = Load(key);
        var art = go.AddComponent<ValuableArtInstance>();
        art.assetKey = key;
        var root = new GameObject("IHM Valuable Visual");root.transform.SetParent(go.transform, false);art.visualRoot = root.transform;
        if (key == "cannon")
        {
            art.leftWheel = Pivot(root.transform, "Left wheel pivot", new Vector3(-.64f, .545f, -.1f));
            art.rightWheel = Pivot(root.transform, "Right wheel pivot", new Vector3(.64f, .545f, -.1f));
        }
        if (key == "water_timer") art.casePivot = Pivot(root.transform, "Water watch flip pivot", new Vector3(0f, .13f, 0f));
        if (key == "maneki_neko") art.beckonPivot = Pivot(root.transform,"Beckoning paw pivot",model.beckonPivot);
        // Generic animated groups: the model names a pivot per group and those parts hang under it.
        var groupPivots = new Dictionary<string, Transform>(StringComparer.Ordinal);
        if (model.pivots != null)
            foreach (var pivot in model.pivots)
                if (!string.IsNullOrEmpty(pivot.group) && !groupPivots.ContainsKey(pivot.group))
                    groupPivots[pivot.group] = Pivot(root.transform, ValuableArtInstance.PivotName(pivot.group), pivot.position);
        var bindings = new List<ValuablePartBinding>();
        var materials = model.materials.Select(m => Material(m, native)).ToArray();
        for (int i = 0; i < model.parts.Length; i++)
        {
            var data = model.parts[i];var obj = new GameObject("Part" + i.ToString("D3"));obj.layer = go.layer;
            obj.transform.SetParent(root.transform, false);obj.transform.localPosition = data.rest.position;
            var mesh = new Mesh { name = key + " " + data.name };
            if (data.vertices.Length > 65000) mesh.indexFormat = IndexFormat.UInt32;
            mesh.vertices = data.vertices;mesh.normals = data.normals;mesh.uv = data.uv;mesh.triangles = data.triangles;mesh.RecalculateBounds();
            obj.AddComponent<MeshFilter>().sharedMesh = mesh;var renderer = obj.AddComponent<MeshRenderer>();renderer.sharedMaterial = materials[data.material];
            if (model.materials[data.material].transmission > .15f) renderer.shadowCastingMode = ShadowCastingMode.Off;
            Transform parent = data.group == "wheel_left" ? art.leftWheel : data.group == "wheel_right" ? art.rightWheel : data.group == "case" ? art.casePivot : data.group == "beckoning_paw" ? art.beckonPivot : null;
            if (parent == null && data.group != null) groupPivots.TryGetValue(data.group, out parent);
            if (parent != null) obj.transform.SetParent(parent, true);
            bindings.Add(new ValuablePartBinding { node = obj.transform, group = data.group, phase = data.phase, rest = obj.transform.localPosition });
        }
        art.bindings = bindings.ToArray();return art;
    }

    private static Transform Pivot(Transform parent, string name, Vector3 center)
    { var obj = new GameObject(name);obj.transform.SetParent(parent, false);obj.transform.localPosition = center;return obj.transform; }

    private static Material Material(ValuableArtMaterial data, Material native)
    {
        bool glass = data.transmission > .15f;
        var shader = glass || data.emission > 0 || data.metallic>.4f ? Shader.Find("Standard") : native != null ? native.shader : Shader.Find("Standard");
        if (shader == null) throw new InvalidOperationException("No compatible valuable material shader.");
        var result = !glass && data.emission <= 0 && data.metallic<=.4f && native != null ? new Material(native) : new Material(shader);
        result.name = "IHM " + data.name;
        foreach (string property in result.GetTexturePropertyNames()) result.SetTexture(property, null);
        var texture = string.IsNullOrEmpty(data.texture) ? Texture2D.whiteTexture : UpgradeIcons.LoadTexture(data.texture);
        if (texture == null) throw new InvalidDataException("Missing valuable texture: " + data.texture);
        texture.filterMode = FilterMode.Bilinear;
        Color c = string.IsNullOrEmpty(data.texture) ? data.color : Color.white;
        if (glass) c.a = .15f;
        foreach (string property in new[] { "_BaseTexture", "_BaseMap", "_MainTex" })
            if (result.HasProperty(property)) { result.SetTexture(property, texture);result.SetTextureScale(property, Vector2.one);result.SetTextureOffset(property, Vector2.zero); }
        foreach (string property in new[] { "_Color", "_BaseColor" }) if (result.HasProperty(property)) result.SetColor(property, c);
        if (result.HasProperty("_Glossiness")) result.SetFloat("_Glossiness", 1f - data.roughness);
        if (result.HasProperty("_Smoothness")) result.SetFloat("_Smoothness", 1f - data.roughness);
        if (result.HasProperty("_Metallic")) result.SetFloat("_Metallic",data.metallic);
        foreach (string property in new[] { "_TilingX", "_TilingY" }) if (result.HasProperty(property)) result.SetFloat(property, 1);
        foreach (string property in new[] { "_OffsetX", "_OffsetY" }) if (result.HasProperty(property)) result.SetFloat(property, 0);
        if (data.emission > 0)
        {
            result.EnableKeyword("_EMISSION");
            if(result.HasProperty("_EmissionMap"))result.SetTexture("_EmissionMap",texture);
            if (result.HasProperty("_EmissionColor")) result.SetColor("_EmissionColor", c * data.emission);
        }
        if (glass)
        {
            result.SetFloat("_Mode", 3);result.SetInt("_SrcBlend", (int)BlendMode.One);result.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            result.SetInt("_ZWrite", 0);result.EnableKeyword("_ALPHAPREMULTIPLY_ON");result.renderQueue = 3000;
        }
        return result;
    }
}

#pragma warning disable CS0649
[Serializable] internal sealed class ValuableArtModel { public ValuableArtPart[] parts;public ValuableArtMaterial[] materials;public Vector3 beckonPivot;public ValuableArtPhysics physics;public ValuableArtLiquid liquid;public ValuableArtPivot[] pivots; }
[Serializable] internal sealed class ValuableArtPivot { public string group;public Vector3 position; }
[Serializable] internal sealed class ValuableArtLiquid { public float halfWidth,bottom,top,fill,power; }
[Serializable] internal sealed class ValuableArtPhysics { public Vector3 centerOfMass,motionMin,motionMax;public ValuableArtCollider[] colliders; }
[Serializable] internal sealed class ValuableArtCollider { public string name;public bool moving;public Vector3[] vertices;public int[] triangles; }
[Serializable] internal sealed class ValuableArtMaterial { public string name, texture;public Color color;public float transmission, emission, roughness,metallic;public bool tint, tintEmission, multiply;public string emissionTexture;public int channel; }
[Serializable] internal sealed class ValuableArtPart
{
    public string name, group;public float phase;public int material;
    public Vector3[] vertices, normals;public Vector2[] uv;public int[] triangles;public Visuals.ArtPose rest;
}
#pragma warning restore CS0649
