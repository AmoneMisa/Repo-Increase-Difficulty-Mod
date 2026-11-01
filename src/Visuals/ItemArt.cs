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
            Material template = null;
            int layer = go.layer;
            foreach (var renderer in renderers)
            {
                if (!renderer.enabled) continue;
                foreach (var material in renderer.sharedMaterials)
                    if (material != null && (material.HasProperty("_MainTex") || material.HasProperty("_BaseMap")))
                    { template = material; layer = renderer.gameObject.layer; break; }
                if (template != null) break;
            }
            if (template == null) throw new InvalidDataException("No textured native weapon material");
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
            var materialCache = new Dictionary<string, Material>();
            for (int i = 0; i < model.parts.Length; i++)
            {
                var part = model.parts[i];
                var obj = new GameObject("Part" + i.ToString("D3")) { layer = layer };
                obj.transform.SetParent(root.transform, false);
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
                    material = new Material(template) { name = "IHM " + key + " " + materialKey };
                    createdMaterials.Add(material);
                    // Do not inherit the donor weapon's texture atlas or emission map.
                    foreach (string property in material.GetTexturePropertyNames()) material.SetTexture(property, null);
                    var texture = string.IsNullOrEmpty(part.texture) ? Texture2D.whiteTexture : Texture(part.texture);
                    if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", texture);
                    if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", texture);
                    if (material.HasProperty("_Color")) material.SetColor("_Color", part.color);
                    if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", part.color);
                    if (material.HasProperty("_EmissionColor")) material.SetColor("_EmissionColor", Color.black);
                    if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", 0.15f);
                    material.DisableKeyword("_EMISSION");
                    material.DisableKeyword("_NORMALMAP");
                    material.DisableKeyword("_METALLICGLOSSMAP");
                    foreach (string property in new[] { "_MainTex", "_BaseMap" })
                        if (material.HasProperty(property))
                        {
                            material.SetTextureScale(property, Vector2.one);
                            material.SetTextureOffset(property, Vector2.zero);
                        }
                    materialCache[materialKey] = material;
                }
                obj.AddComponent<MeshRenderer>().sharedMaterial = material;
            }
            var icon = UpgradeIcons.Load(key + "_inventory.png");
            if (icon == null) throw new InvalidDataException("Missing inventory render");
            var motion = go.AddComponent<ItemArtMotion>();
            motion.assetKey = key;
            motion.visualRoot = root.transform;
            // Hide only the donor body, never disable transforms used by native scripts or colliders.
            foreach (var renderer in renderers) renderer.forceRenderingOff = true;
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
