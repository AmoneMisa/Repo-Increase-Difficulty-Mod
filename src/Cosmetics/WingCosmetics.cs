using System;
using System.Collections.Generic;
using IncreaseHardMod.Upgrades;
using IncreaseHardMod.Valuables;
using UnityEngine;
using Object = UnityEngine.Object;

namespace IncreaseHardMod.Cosmetics;

/// <summary>
/// Three Rare (purple tier) back-wing cosmetics in the game's Body Top slot, registered through REPOLib so
/// they appear in the cosmetics menu, drop like other Rare cosmetics and sync between players by asset ID.
/// Each prefab is a small template: a Cosmetic root with one "Visuals" mesh parent. The wing meshes are
/// built when the game instantiates it (WingCosmeticVisual), using a copy of a vanilla cosmetic material so
/// they keep the game's Hurtable shader (hurt flash, lighting) without shipping any game asset.
/// </summary>
internal static class WingCosmetics
{
    internal readonly struct Definition
    {
        public readonly string Key, Title;
        public Definition(string key, string title) { Key = key; Title = title; }
    }

    internal static readonly Definition[] Definitions =
    {
        new("wings_astral", "Astral Night Wings"),
        new("wings_solar", "Solar Seraph Wings"),
        new("wings_prism", "Prism Chime Wings"),
    };

    private static GameObject _templates;
    private static bool _registered;

    // Registered from MetaManager.Awake, not from the plugin's Awake: objects created during the BepInEx
    // chainloader can be destroyed on the first scene change, which would leave the cosmetic prefabs dead.
    // MetaManager is the game's persistent cosmetics owner, and REPOLib accepts cosmetics registered after
    // its initial pass (it adds them at once and reloads saved modded unlocks).
    [HarmonyLib.HarmonyPatch(typeof(MetaManager), "Awake")]
    private static class RegisterWithMetaManager
    {
        [HarmonyLib.HarmonyPostfix]
        [HarmonyLib.HarmonyPriority(HarmonyLib.Priority.Low)]
        private static void Postfix(MetaManager __instance)
        {
            if (__instance == null || MetaManager.instance != __instance) return; // the duplicate that destroys itself
            Register();
        }
    }

    internal static void Register()
    {
        if (_registered || !ModConfig.WingCosmeticsEnabled.Value) return;
        _registered = true;
        // Templates live under an inactive holder: Unity never runs their Awake, while instances made by
        // PlayerCosmetics (parented to the player) are active and build their meshes.
        _templates = new GameObject("IHM Cosmetic Templates");
        _templates.SetActive(false);
        Object.DontDestroyOnLoad(_templates);
        _templates.hideFlags = HideFlags.HideAndDontSave;
        foreach (var definition in Definitions)
        {
            try { RegisterOne(definition); }
            catch (Exception error) { Plugin.Log?.LogError("[Cosmetics] Could not register " + definition.Title + ": " + error); }
        }
    }

    private static void RegisterOne(Definition definition)
    {
        string name = "Cosmetic - Body Top - " + definition.Title;
        var prefab = new GameObject(name);
        prefab.transform.SetParent(_templates.transform, false);
        var visuals = new GameObject("Visuals").transform;
        visuals.SetParent(prefab.transform, false);
        var cosmetic = prefab.AddComponent<Cosmetic>();
        cosmetic.type = SemiFunc.CosmeticType.BodyTop;
        cosmetic.meshParents = new List<Transform> { visuals };
        prefab.AddComponent<WingCosmeticVisual>().modelKey = definition.Key;

        var asset = ScriptableObject.CreateInstance<CosmeticAsset>();
        asset.name = name;
        asset.assetName = definition.Title;
        asset.assetId = "modded:IncreaseDifficultyMod." + definition.Key;
        asset.type = SemiFunc.CosmeticType.BodyTop;
        asset.rarity = SemiFunc.Rarity.Rare;
        asset.status = CosmeticAsset.Status.Finalized;
        asset.customTypeList = new List<CosmeticCustomCondition.Type>();
        asset.tintable = false;
        var icon = UpgradeIcons.LoadTexture("cosmetic_" + definition.Key + "_icon.png");
        if (icon != null)
        {
            // A ready icon means the game never tries to render one (that needs an icon camera in the prefab).
            asset.icon = Sprite.Create(icon, new Rect(0, 0, icon.width, icon.height), new Vector2(.5f, .5f), 100f);
            asset.icon.name = name + " icon";
        }
        var prefabRef = REPOLib.Modules.NetworkPrefabs.RegisterNetworkPrefab("Cosmetics/" + name, prefab);
        if (prefabRef == null) { Plugin.Log?.LogError("[Cosmetics] No prefab reference for " + definition.Title); return; }
        asset.prefab = prefabRef;
        REPOLib.Modules.Cosmetics.RegisterCosmetic(asset);
        Plugin.Log?.LogInfo("[Cosmetics] Registered " + definition.Title + " (Rare, Body Top).");
    }
}

/// <summary>Builds one wing cosmetic's meshes on a live instance, before the game's Cosmetic.Setup.</summary>
internal sealed class WingCosmeticVisual : MonoBehaviour
{
    public string modelKey;   // serialized, so it survives Instantiate

    private static readonly Dictionary<string, (Mesh[] meshes, Material[] materials)> Cache = new(StringComparer.Ordinal);
    private static Material _hurtable;
    private static bool _searched;

    private static readonly int Albedo = Shader.PropertyToID("_AlbedoColor"), AlbedoTex = Shader.PropertyToID("_AlbedoTexture");
    private static readonly int Emission = Shader.PropertyToID("_EmissionColor"), EmissionTex = Shader.PropertyToID("_EmissionTexture");
    private static readonly int NormalTex = Shader.PropertyToID("_NormalTexture"), SmoothTex = Shader.PropertyToID("_SmoothnessTexture");

    private void Awake()
    {
        try { Build(); }
        catch (Exception error) { Plugin.Log?.LogError("[Cosmetics] Wing visuals failed for " + modelKey + ": " + error.Message); }
    }

    private void Build()
    {
        var visuals = transform.Find("Visuals");
        if (visuals == null || string.IsNullOrEmpty(modelKey) || visuals.childCount > 0) return;
        var model = ValuableVisuals.Load(modelKey);
        if (!Cache.TryGetValue(modelKey, out var cached))
        {
            var meshes = new Mesh[model.parts.Length];
            for (int i = 0; i < meshes.Length; i++)
            {
                var data = model.parts[i];
                var mesh = new Mesh { name = modelKey + " " + data.name, hideFlags = HideFlags.DontUnloadUnusedAsset };
                mesh.vertices = data.vertices; mesh.normals = data.normals; mesh.uv = data.uv; mesh.triangles = data.triangles;
                mesh.RecalculateBounds();
                meshes[i] = mesh;
            }
            var materials = new Material[model.materials.Length];
            for (int i = 0; i < materials.Length; i++) materials[i] = MakeMaterial(model.materials[i]);
            Cache[modelKey] = cached = (meshes, materials);
        }

        var pivots = new Dictionary<string, Transform>(StringComparer.Ordinal);
        if (model.pivots != null)
            foreach (var pivot in model.pivots)
            {
                var node = new GameObject("Pivot " + pivot.group).transform;
                node.SetParent(visuals, false); node.localPosition = pivot.position;
                pivots[pivot.group] = node;
            }
        for (int i = 0; i < model.parts.Length; i++)
        {
            var data = model.parts[i];
            var part = new GameObject("Wing part " + i.ToString("D3")) { layer = gameObject.layer };
            part.transform.SetParent(visuals, false);
            part.transform.localPosition = data.rest.position;
            if (data.group != null && pivots.TryGetValue(data.group, out var parent)) part.transform.SetParent(parent, true);
            part.AddComponent<MeshFilter>().sharedMesh = cached.meshes[i];
            part.AddComponent<MeshRenderer>().sharedMaterial = cached.materials[data.material];
            var playerMaterial = part.AddComponent<PlayerMaterial>();   // hurt flash and colour hooks
            playerMaterial.cosmeticType = SemiFunc.CosmeticType.BodyTop;
            playerMaterial.tintable = false;
        }
        var flutter = gameObject.AddComponent<WingFlutter>();
        pivots.TryGetValue("wing_r", out flutter.right);
        pivots.TryGetValue("wing_l", out flutter.left);
    }

    /// <summary>A vanilla cosmetic material (Hurtable shader) to copy, found once.</summary>
    private static Material Template()
    {
        if (_searched) return _hurtable;
        _searched = true;
        try
        {
            var assets = MetaManager.instance != null ? MetaManager.instance.cosmeticAssets : null;
            if (assets != null)
                foreach (var asset in assets)
                {
                    if (asset == null || asset.prefab == null || !asset.prefab.IsValid() || asset.assetId != null && asset.assetId.StartsWith("modded:", StringComparison.Ordinal)) continue;
                    var prefab = asset.prefab.Prefab;
                    if (prefab == null) continue;
                    foreach (var renderer in prefab.GetComponentsInChildren<MeshRenderer>(true))
                    {
                        var material = renderer.sharedMaterial;
                        if (material != null && material.shader != null && material.shader.name.Contains("Hurtable") && material.HasProperty(AlbedoTex))
                            return _hurtable = material;
                    }
                }
        }
        catch (Exception error) { Plugin.Log?.LogWarning("[Cosmetics] Vanilla material lookup failed: " + error.Message); }
        var shader = Shader.Find("Hurtable/Hurtable");
        if (shader != null) return _hurtable = new Material(shader);
        Plugin.Log?.LogWarning("[Cosmetics] Hurtable shader not found; wings use Standard.");
        return null;
    }

    private static Material MakeMaterial(ValuableArtMaterial data)
    {
        var template = Template();
        var loaded = string.IsNullOrEmpty(data.texture) ? null : UpgradeIcons.LoadTexture(data.texture);
        if (loaded != null) { loaded.wrapMode = TextureWrapMode.Clamp; loaded.filterMode = FilterMode.Bilinear; }
        var texture = loaded != null ? loaded : Texture2D.whiteTexture;
        Color colour = string.IsNullOrEmpty(data.texture) ? data.color : Color.white;
        Material material;
        if (template != null)
        {
            material = new Material(template) { name = "IHM wing " + data.name };
            material.SetTexture(AlbedoTex, texture);
            material.SetColor(Albedo, colour);
            if (material.HasProperty(NormalTex)) material.SetTexture(NormalTex, null);
            if (material.HasProperty(SmoothTex)) material.SetTexture(SmoothTex, null);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", Mathf.Clamp01(1f - data.roughness));
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", data.metallic);
            if (material.HasProperty(EmissionTex)) material.SetTexture(EmissionTex, texture);
            material.SetColor(Emission, data.emission > 0 ? colour * data.emission : Color.black);
        }
        else
        {
            material = new Material(Shader.Find("Standard")) { name = "IHM wing " + data.name };
            material.mainTexture = texture; material.color = colour;
            material.SetFloat("_Glossiness", 1f - data.roughness); material.SetFloat("_Metallic", data.metallic);
            if (data.emission > 0) { material.EnableKeyword("_EMISSION"); material.SetColor("_EmissionColor", colour * data.emission); }
        }
        material.hideFlags = HideFlags.DontUnloadUnusedAsset;
        return material;
    }
}

/// <summary>Slow idle flutter: each wing sweeps a few degrees about its root, slightly out of phase.</summary>
internal sealed class WingFlutter : MonoBehaviour
{
    public Transform right, left;
    private float _phase;

    private void Start() => _phase = UnityEngine.Random.value * 10f;

    private void LateUpdate()
    {
        float t = Time.time + _phase;
        float sweep = 5f * Mathf.Sin(t * Mathf.PI * 2f * .42f);
        float lift = 2.5f * Mathf.Sin(t * Mathf.PI * 2f * .42f + .8f);
        if (right != null) right.localRotation = Quaternion.AngleAxis(-sweep, Vector3.up) * Quaternion.AngleAxis(lift, Vector3.forward);
        if (left != null) left.localRotation = Quaternion.AngleAxis(sweep, Vector3.up) * Quaternion.AngleAxis(-lift, Vector3.forward);
    }
}
