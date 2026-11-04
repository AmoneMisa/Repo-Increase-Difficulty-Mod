using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using HarmonyLib;
using IncreaseHardMod.Upgrades;
using UnityEngine;
using Object = UnityEngine.Object;

namespace IncreaseHardMod.Visuals;

/// <summary>Original Blender assets embedded in the plugin; no Unity editor or asset bundle at runtime.</summary>
internal static class ItemArt
{
    private static readonly Dictionary<string, ArtModel> Models = new();

    internal static ArtModel LoadModel(string key)
    {
        if (Models.TryGetValue(key, out var model)) return model;
        var asm = Assembly.GetExecutingAssembly();
        foreach (string name in asm.GetManifestResourceNames())
        {
            if (!name.EndsWith("." + key + ".mesh.json", StringComparison.Ordinal)) continue;
            using var stream = asm.GetManifestResourceStream(name);
            using var reader = new StreamReader(stream);
            model = ArtModelReader.Parse(reader.ReadToEnd(), key);
            Models[key] = model;
            return model;
        }
        throw new FileNotFoundException("Missing embedded item art: " + key);
    }

    private static Texture2D Texture(string name)
    {
        var texture = UpgradeIcons.LoadTexture(name);
        if (texture == null) throw new FileNotFoundException("Missing item texture: " + name);
        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Repeat;
        return texture;
    }

    internal static void Skin(GameObject go, string key, bool orb)
    {
        try
        {
            var icon = Texture(key + "_icon.png");
            var coat = Texture(key + "_coat.png");
            // Native sockets and UVs retain their opening animations and emissive masks.
            var body = go.transform.Find(orb ? "Item Orb Mesh" : "Drone");
            if (body == null) throw new InvalidDataException("Unknown body hierarchy: " + go.name);
            foreach (var renderer in body.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (renderer.name.IndexOf("Icon", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    renderer.name.IndexOf("Core", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                foreach (var material in renderer.materials)
                {
                    if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", coat);
                    if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", coat);
                    if (material.HasProperty("_Color")) material.SetColor("_Color", Color.white);
                    if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", Color.white);
                }
            }
            if (orb) go.GetComponent<ItemOrb>().orbIcon = icon;
            else go.GetComponent<ItemDrone>().droneIcon = icon;
            go.GetComponent<ItemAttributes>().icon = UpgradeIcons.Load(key + "_skin.png");
        }
        catch (Exception e)
        {
            Plugin.Log.LogWarning($"[Art] Keeping native appearance for {go.name}: {e.Message}");
        }
    }

    internal static void Weapon(GameObject go, string key)
    {
        GameObject root = null;
        var createdMeshes = new List<Mesh>();
        var createdMaterials = new List<Material>();
        try
        {
            var model = LoadModel(key);
            var native = go.transform.Find("Mesh");
            if (native == null) throw new InvalidDataException("Unknown weapon mesh hierarchy");
            var renderers = native.GetComponentsInChildren<MeshRenderer>(true);
            int layer = renderers.Length > 0 ? renderers[0].gameObject.layer : go.layer;
            var template = FindTexturedMaterial(native);
            // REPO's Fresnel Opaque shader exposes _BaseTexture, not _MainTex/_BaseMap.
            // Prefer the item's own body preset, then a native gun preset if no body supports albedo.
            if (template == null)
                template = FindWeaponMaterialDonor();
            if (template == null)
                throw new InvalidDataException("No albedo-capable native item material (including weapon donors)");
            var gun = go.GetComponent<ItemGun>();
            if (gun != null && gun.gunMuzzle == null) throw new InvalidDataException("Missing gun muzzle");
            root = new GameObject("IHM Blender Visual");
            root.transform.SetParent(go.transform, false);
            // Export origin is the muzzle. Keep the vanilla hit ray and muzzle flash exactly aligned.
            if (gun != null)
            {
                root.transform.position = gun.gunMuzzle.position;
                root.transform.rotation = gun.gunMuzzle.rotation;
            }
            else root.transform.localScale = Vector3.one * 0.8f;
            BuildParts(root.transform, model, key, template, layer, createdMeshes, createdMaterials);
            var icon = UpgradeIcons.Load(key + "_inventory.png");
            if (icon == null) throw new InvalidDataException("Missing inventory render");
            var motion = go.AddComponent<ItemArtMotion>();
            motion.assetKey = key;
            motion.visualRoot = root.transform;
            // Hide only the donor body, never disable transforms used by native scripts or colliders.
            DonorVisuals.RemoveGeometry(renderers);
            if (gun == null)
            {
                var pin = go.transform.Find("Splinter");
                if (pin != null)
                    foreach (var renderer in pin.GetComponentsInChildren<MeshRenderer>(true)) renderer.forceRenderingOff = true;
            }
            go.GetComponent<ItemAttributes>().icon = icon;
            Plugin.Log.LogInfo($"[Art] Loaded {key}: {model.parts.Length} parts, {model.frames} animation frames.");
        }
        catch (Exception e)
        {
            if (root != null) Object.DestroyImmediate(root);
            foreach (var mesh in createdMeshes) Object.DestroyImmediate(mesh);
            foreach (var material in createdMaterials) Object.DestroyImmediate(material);
            Plugin.Log.LogWarning($"[Art] Keeping native appearance for {go.name}: {e.Message}");
        }
    }

    /// <summary>Instantiates every exported part under <paramref name="root"/> with private albedo materials.</summary>
    internal static void BuildParts(Transform root, ArtModel model, string key, Material template, int layer,
        List<Mesh> createdMeshes, List<Material> createdMaterials)
    {
        var materialCache = new Dictionary<string, Material>();
        for (int i = 0; i < model.parts.Length; i++)
        {
            var part = model.parts[i];
            var obj = new GameObject("Part" + i.ToString("D3")) { layer = layer };
            obj.transform.SetParent(root, false);
            ApplyPose(obj.transform, part.rest);
            var mesh = new Mesh { name = "IHM " + key + " " + part.name };
            createdMeshes.Add(mesh);
            mesh.vertices = part.vertices;
            mesh.normals = part.normals;
            mesh.uv = part.uv;
            mesh.triangles = part.triangles;
            mesh.RecalculateBounds();
            obj.AddComponent<MeshFilter>().sharedMesh = mesh;
            string materialKey = part.texture + ":" + ColorUtility.ToHtmlStringRGBA(part.color);
            if (!materialCache.TryGetValue(materialKey, out var material))
            {
                material = Paint(template, string.IsNullOrEmpty(part.texture) ? null : part.texture, part.color, "IHM " + key + " " + materialKey);
                createdMaterials.Add(material);
                materialCache[materialKey] = material;
            }
            obj.AddComponent<MeshRenderer>().sharedMaterial = material;
        }
    }

    /// <summary>Private matte copy of a native albedo-capable material, with an embedded texture and tint.</summary>
    internal static Material Paint(Material template, string textureFile, Color color, string name)
    {
        var material = new Material(template) { name = name };
        // Do not inherit the donor weapon's texture atlas or emission map.
        foreach (string property in material.GetTexturePropertyNames()) material.SetTexture(property, null);
        var texture = string.IsNullOrEmpty(textureFile) ? Texture2D.whiteTexture : Texture(textureFile);
        string albedo = BaseTextureProperty(material);
        material.SetTexture(albedo, texture);
        material.SetTextureScale(albedo, Vector2.one);
        material.SetTextureOffset(albedo, Vector2.zero);
        if (material.HasProperty("_Color")) material.SetColor("_Color", color);
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        if (material.HasProperty("_EmissionColor")) material.SetColor("_EmissionColor", Color.black);
        if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", 0.15f);
        material.DisableKeyword("_EMISSION");
        material.DisableKeyword("_NORMALMAP");
        material.DisableKeyword("_METALLICGLOSSMAP");
        if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.15f);
        if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0f);
        if (material.HasProperty("_NormalStrength")) material.SetFloat("_NormalStrength", 0f);
        // Fresnel Opaque uses explicit UV controls in addition to texture scale/offset.
        if (material.HasProperty("_TilingX")) material.SetFloat("_TilingX", 1f);
        if (material.HasProperty("_TilingY")) material.SetFloat("_TilingY", 1f);
        if (material.HasProperty("_OffsetX")) material.SetFloat("_OffsetX", 0f);
        if (material.HasProperty("_OffsetY")) material.SetFloat("_OffsetY", 0f);
        return material;
    }

    /// <summary>Static (non-animated) model under a new child, for non-weapon items such as carts.</summary>
    internal static Transform AttachStatic(GameObject go, string key, Material template, int layer)
    {
        var meshes = new List<Mesh>();
        var materials = new List<Material>();
        var root = new GameObject("IHM Blender Visual " + key);
        root.transform.SetParent(go.transform, false);
        try
        {
            BuildParts(root.transform, LoadModel(key), key, template, layer, meshes, materials);
            return root.transform;
        }
        catch
        {
            Object.DestroyImmediate(root);
            foreach (var mesh in meshes) Object.DestroyImmediate(mesh);
            foreach (var material in materials) Object.DestroyImmediate(material);
            throw;
        }
    }

    internal static Material WeaponMaterialDonor(Transform preferred) => FindTexturedMaterial(preferred) ?? FindWeaponMaterialDonor();

    private static string BaseTextureProperty(Material material)
    {
        if (material.HasProperty("_BaseTexture")) return "_BaseTexture";
        if (material.HasProperty("_BaseMap")) return "_BaseMap";
        if (material.HasProperty("_MainTex")) return "_MainTex";
        return null;
    }

    private static Material FindTexturedMaterial(Transform body)
    {
        if (body == null) return null;
        foreach (var renderer in body.GetComponentsInChildren<MeshRenderer>(true))
        {
            // Prefabs are inspected before activation; disabled renderers still carry valid presets.
            // Inspect every slot rather than assuming the first material is the body albedo.
            foreach (var material in renderer.sharedMaterials)
                if (material != null && material.shader != null &&
                    BaseTextureProperty(material) != null)
                    return material;
        }
        return null;
    }

    private static Material FindWeaponMaterialDonor()
    {
        var items = StatsManager.instance != null ? StatsManager.instance.itemDictionary : null;
        if (items == null) return null;
        // Stable known body presets first, followed by other registered vanilla gun prefabs.
        foreach (string name in new[] { "Item Gun Handgun", "Item Gun Shotgun", "Item Gun Shockwave" })
        {
            if (!items.TryGetValue(name, out var item)) continue;
            var prefab = item != null && item.prefab != null ? item.prefab.Prefab : null;
            var material = prefab != null ? FindTexturedMaterial(prefab.transform.Find("Mesh")) : null;
            if (material != null) return material;
        }
        foreach (var item in items.Values)
        {
            var prefab = item != null && item.prefab != null ? item.prefab.Prefab : null;
            if (prefab == null || prefab.GetComponent<ItemGun>() == null) continue;
            var material = FindTexturedMaterial(prefab.transform.Find("Mesh"));
            if (material != null) return material;
        }
        return null;
    }

    internal static void ApplyPose(Transform target, ArtPose pose)
    {
        target.localPosition = pose.position;
        target.localRotation = pose.rotation;
        target.localScale = pose.scale;
    }
}

/// <summary>Samples the actual Blender keyframes; runs locally on every client, with no new RPCs.</summary>
internal sealed class ItemArtMotion : MonoBehaviour
{
    public string assetKey;
    public Transform visualRoot;
    private ArtModel _model;
    private Transform[] _parts;
    private ItemGrenade _grenade;
    private bool _wasArmed;
    private float _elapsed = -1f;

    private void EnsureReady()
    {
        if (_model != null) return;
        _model = ItemArt.LoadModel(assetKey);
        _parts = new Transform[_model.parts.Length];
        for (int i = 0; i < _parts.Length; i++) _parts[i] = visualRoot.Find("Part" + i.ToString("D3"));
        _grenade = GetComponent<ItemGrenade>();
    }

    internal void Play()
    {
        EnsureReady();
        _elapsed = 0f;
        Sample(0f);
    }

    private void OnEnable()
    {
        // Serialized fields are assigned before prefab instances are enabled.
        if (string.IsNullOrEmpty(assetKey) || visualRoot == null) return;
        EnsureReady();
        _elapsed = -1f;
        _wasArmed = false;
        Sample(0f);
    }

    private void LateUpdate()
    {
        EnsureReady();
        if (_grenade != null)
        {
            if (_grenade.isActive && !_wasArmed) Play();
            if (!_grenade.isActive && _wasArmed) { _elapsed = -1f; Sample(0f); }
            _wasArmed = _grenade.isActive;
        }
        if (_elapsed < 0f) return;
        _elapsed += Time.deltaTime;
        float frame = Mathf.Min(_elapsed * _model.fps, _model.frames - 1);
        Sample(frame);
        if (frame >= _model.frames - 1) _elapsed = -1f;
    }

    private void Sample(float frame)
    {
        for (int i = 0; i < _parts.Length; i++)
        {
            var samples = _model.parts[i].samples;
            if (_parts[i] == null || samples == null || samples.Length == 0) continue;
            int a = Mathf.Min(Mathf.FloorToInt(frame), samples.Length - 1);
            int b = Mathf.Min(a + 1, samples.Length - 1);
            float t = frame - a;
            _parts[i].localPosition = Vector3.Lerp(samples[a].position, samples[b].position, t);
            _parts[i].localRotation = Quaternion.Slerp(samples[a].rotation, samples[b].rotation, t);
            _parts[i].localScale = Vector3.Lerp(samples[a].scale, samples[b].scale, t);
        }
    }

    [HarmonyPatch(typeof(ItemGun), "ShootBulletRPC")]
    private static class ShotAnimation
    {
        // Deliberately independent of host-only hit effects: misses also animate on all clients.
        [HarmonyPostfix]
        private static void Postfix(ItemGun __instance) => __instance.GetComponent<ItemArtMotion>()?.Play();
    }
}
