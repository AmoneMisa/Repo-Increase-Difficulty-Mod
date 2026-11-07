using System;
using System.Collections.Generic;
using IncreaseHardMod.Upgrades;
using IncreaseHardMod.Valuables;
using UnityEngine;
using Object = UnityEngine.Object;

namespace IncreaseHardMod.Cosmetics;

/// <summary>
/// The mod's own cosmetics, registered through REPOLib so they appear in the cosmetics menu, drop like other
/// cosmetics of their tier and sync between players by asset ID:
/// three Rare back wings (Body Top), the six-part Plush Shark set (hood, headphones, suit, tail, two fin sleeves)
/// the seven-part Starlight Gown set (bodice, skirt, two lace sleeves, two boots, earrings) and the Legate set
/// (silver cuirass with or without the red mantle and cape, war skirt, two bracers), each part in its
/// own slot so it mixes with anything else. Plush, galaxy fabric, lace and gems are tintable: they take the colour
/// the player picks for that slot in the cosmetics menu (the galaxy's glowing stars too).
/// Each prefab is a small template: a Cosmetic root with one "Visuals" mesh parent. The meshes are built when the
/// game instantiates it (WingCosmeticVisual), using a copy of a vanilla cosmetic material so they keep the game's
/// Hurtable shader (hurt flash, lighting, colour tint) without shipping any game asset.
/// </summary>
internal static class WingCosmetics
{
    internal readonly struct Definition
    {
        public readonly string Key, Title, Slot;
        public readonly SemiFunc.CosmeticType Type;
        public readonly SemiFunc.Rarity Rarity;
        public readonly bool Tintable;
        public readonly string Set;   // "wings", "shark", "starlight", "legate": each set has its own config switch
        public Definition(string key, string title, SemiFunc.CosmeticType type = SemiFunc.CosmeticType.BodyTop, string slot = "Body Top",
            SemiFunc.Rarity rarity = SemiFunc.Rarity.Rare, bool tintable = false, string set = "wings")
        { Key = key; Title = title; Type = type; Slot = slot; Rarity = rarity; Tintable = tintable; Set = set; }
    }

    internal static readonly Definition[] Definitions =
    {
        new("wings_astral", "Astral Night Wings"),
        new("wings_solar", "Solar Seraph Wings"),
        new("wings_prism", "Prism Chime Wings"),
        new("shark_hood", "Plush Shark Hood", SemiFunc.CosmeticType.Hat, "Hat", SemiFunc.Rarity.Rare, true, "shark"),
        new("shark_phones", "Plush Shark Headphones", SemiFunc.CosmeticType.Ears, "Ears", SemiFunc.Rarity.Uncommon, true, "shark"),
        new("shark_suit", "Plush Shark Suit", SemiFunc.CosmeticType.BodyTop, "Body Top", SemiFunc.Rarity.Rare, true, "shark"),
        new("shark_tail", "Plush Shark Tail", SemiFunc.CosmeticType.BodyBottom, "Body Bottom", SemiFunc.Rarity.Rare, true, "shark"),
        new("shark_fin_r", "Plush Shark Fin Right", SemiFunc.CosmeticType.ArmRight, "Arm Right", SemiFunc.Rarity.Uncommon, true, "shark"),
        new("shark_fin_l", "Plush Shark Fin Left", SemiFunc.CosmeticType.ArmLeft, "Arm Left", SemiFunc.Rarity.Uncommon, true, "shark"),
        new("starlight_bodice", "Starlight Bodice", SemiFunc.CosmeticType.BodyTop, "Body Top", SemiFunc.Rarity.Rare, true, "starlight"),
        new("starlight_skirt", "Starlight Skirt", SemiFunc.CosmeticType.BodyBottom, "Body Bottom", SemiFunc.Rarity.UltraRare, true, "starlight"),
        new("starlight_sleeve_r", "Starlight Lace Sleeve Right", SemiFunc.CosmeticType.ArmRight, "Arm Right", SemiFunc.Rarity.Uncommon, true, "starlight"),
        new("starlight_sleeve_l", "Starlight Lace Sleeve Left", SemiFunc.CosmeticType.ArmLeft, "Arm Left", SemiFunc.Rarity.Uncommon, true, "starlight"),
        new("starlight_boot_r", "Starlight Boot Right", SemiFunc.CosmeticType.LegRight, "Leg Right", SemiFunc.Rarity.Uncommon, true, "starlight"),
        new("starlight_boot_l", "Starlight Boot Left", SemiFunc.CosmeticType.LegLeft, "Leg Left", SemiFunc.Rarity.Uncommon, true, "starlight"),
        new("starlight_earrings", "Starlight Earrings", SemiFunc.CosmeticType.Ears, "Ears", SemiFunc.Rarity.Uncommon, true, "starlight"),
        new("legate_cuirass", "Legate Cuirass", SemiFunc.CosmeticType.BodyTop, "Body Top", SemiFunc.Rarity.Rare, true, "legate"),
        new("legate_cuirass_cape", "Legate Cuirass and Cape", SemiFunc.CosmeticType.BodyTop, "Body Top", SemiFunc.Rarity.UltraRare, true, "legate"),
        new("legate_skirt", "Legate War Skirt", SemiFunc.CosmeticType.BodyBottom, "Body Bottom", SemiFunc.Rarity.Rare, true, "legate"),
        new("legate_bracer_r", "Legate Bracer Right", SemiFunc.CosmeticType.ArmRight, "Arm Right", SemiFunc.Rarity.Uncommon, true, "legate"),
        new("legate_bracer_l", "Legate Bracer Left", SemiFunc.CosmeticType.ArmLeft, "Arm Left", SemiFunc.Rarity.Uncommon, true, "legate"),
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
        if (_registered || !ModConfig.WingCosmeticsEnabled.Value && !ModConfig.SharkCosmeticsEnabled.Value && !ModConfig.StarlightCosmeticsEnabled.Value && !ModConfig.LegateCosmeticsEnabled.Value) return;
        _registered = true;
        // Templates live under an inactive holder: Unity never runs their Awake, while instances made by
        // PlayerCosmetics (parented to the player) are active and build their meshes.
        _templates = new GameObject("IHM Cosmetic Templates");
        _templates.SetActive(false);
        Object.DontDestroyOnLoad(_templates);
        _templates.hideFlags = HideFlags.HideAndDontSave;
        foreach (var definition in Definitions)
        {
            var enabled = definition.Set switch
            {
                "shark" => ModConfig.SharkCosmeticsEnabled.Value,
                "starlight" => ModConfig.StarlightCosmeticsEnabled.Value,
                "legate" => ModConfig.LegateCosmeticsEnabled.Value,
                _ => ModConfig.WingCosmeticsEnabled.Value,
            };
            if (!enabled) continue;
            try { RegisterOne(definition); }
            catch (Exception error) { Plugin.Log?.LogError("[Cosmetics] Could not register " + definition.Title + ": " + error); }
        }
    }

    private static void RegisterOne(Definition definition)
    {
        string name = "Cosmetic - " + definition.Slot + " - " + definition.Title;
        var prefab = new GameObject(name);
        prefab.transform.SetParent(_templates.transform, false);
        var visuals = new GameObject("Visuals").transform;
        visuals.SetParent(prefab.transform, false);
        var cosmetic = prefab.AddComponent<Cosmetic>();
        cosmetic.type = definition.Type;
        cosmetic.meshParents = new List<Transform> { visuals };
        var visual = prefab.AddComponent<WingCosmeticVisual>();
        visual.modelKey = definition.Key;
        visual.cosmeticType = definition.Type;
        if (definition.Type == SemiFunc.CosmeticType.Hat) AddCrownAnchor(prefab, visuals, definition.Key);

        var asset = ScriptableObject.CreateInstance<CosmeticAsset>();
        asset.name = name;
        asset.assetName = definition.Title;
        asset.assetId = "modded:IncreaseDifficultyMod." + definition.Key;
        asset.type = definition.Type;
        asset.rarity = definition.Rarity;
        asset.status = CosmeticAsset.Status.Finalized;
        asset.customTypeList = new List<CosmeticCustomCondition.Type>();
        asset.tintable = definition.Tintable;
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
        Plugin.Log?.LogInfo("[Cosmetics] Registered " + definition.Title + " (" + definition.Rarity + ", " + definition.Slot + ").");
    }

    // Hats tell the game where the leader's crown sits (CosmeticPlayerCrown.targetMain): on top of the hood.
    private static void AddCrownAnchor(GameObject prefab, Transform visuals, string key)
    {
        var position = new Vector3(0f, .45f, .25f);
        var model = ValuableVisuals.Load(key);
        if (model.pivots != null)
            foreach (var pivot in model.pivots)
                if (pivot.group == "crown") position = pivot.position;
        var anchor = new GameObject("Crown Anchor").transform;
        anchor.SetParent(visuals, false);
        anchor.localPosition = position;
        var crown = prefab.AddComponent<CosmeticPlayerCrown>();
        crown.targetMain = anchor;
        crown.priority = 1;
    }
}

/// <summary>Builds one wing cosmetic's meshes on a live instance, before the game's Cosmetic.Setup.</summary>
internal sealed class WingCosmeticVisual : MonoBehaviour
{
    public string modelKey;   // serialized, so it survives Instantiate
    public SemiFunc.CosmeticType cosmeticType = SemiFunc.CosmeticType.BodyTop;

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
        if (visuals == null || string.IsNullOrEmpty(modelKey) || visuals.Find("Part 000") != null) return;
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
                if (pivot.group == "crown") continue;
                var node = new GameObject("Pivot " + pivot.group).transform;
                node.SetParent(visuals, false); node.localPosition = pivot.position;
                pivots[pivot.group] = node;
            }
        for (int i = 0; i < model.parts.Length; i++)
        {
            var data = model.parts[i];
            var part = new GameObject("Part " + i.ToString("D3")) { layer = gameObject.layer };
            part.transform.SetParent(visuals, false);
            part.transform.localPosition = data.rest.position;
            if (data.group != null && pivots.TryGetValue(data.group, out var parent)) part.transform.SetParent(parent, true);
            part.AddComponent<MeshFilter>().sharedMesh = cached.meshes[i];
            part.AddComponent<MeshRenderer>().sharedMaterial = cached.materials[data.material];
            var playerMaterial = part.AddComponent<PlayerMaterial>();   // hurt flash and colour hooks
            playerMaterial.cosmeticType = cosmeticType;
            playerMaterial.tintable = model.materials[data.material].tint;   // takes the slot colour picked in the menu
            var art = model.materials[data.material];
            playerMaterial.tintType = art.tintEmission ? PlayerMaterial.TintType.Both : PlayerMaterial.TintType.Albedo;   // glowing stars and gems take the tint too
        }
        if (pivots.ContainsKey("wing_r") || pivots.ContainsKey("wing_l"))
        {
            var flutter = gameObject.AddComponent<WingFlutter>();
            pivots.TryGetValue("wing_r", out flutter.right);
            pivots.TryGetValue("wing_l", out flutter.left);
        }
        if (pivots.TryGetValue("tail", out var tail)) gameObject.AddComponent<TailSway>().tail = tail;
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
        bool tiled = data.texture != null && (data.texture.StartsWith("starlight_", StringComparison.Ordinal) || data.texture.StartsWith("legate_", StringComparison.Ordinal));   // fabrics and trims repeat
        if (loaded != null) { loaded.wrapMode = tiled ? TextureWrapMode.Repeat : TextureWrapMode.Clamp; loaded.filterMode = FilterMode.Bilinear; }
        var glow = string.IsNullOrEmpty(data.emissionTexture) ? null : UpgradeIcons.LoadTexture(data.emissionTexture);
        if (glow != null) { glow.wrapMode = tiled ? TextureWrapMode.Repeat : TextureWrapMode.Clamp; glow.filterMode = FilterMode.Bilinear; }
        var texture = loaded != null ? loaded : Texture2D.whiteTexture;
        // Tintable textures are grey and take their colour from the material (the slot tint replaces it in game).
        Color colour = string.IsNullOrEmpty(data.texture) || data.tint ? data.color : Color.white;
        Material material;
        if (template != null)
        {
            material = new Material(template) { name = "IHM cosmetic " + data.name };
            material.SetTexture(AlbedoTex, texture);
            material.SetColor(Albedo, colour);
            if (material.HasProperty(NormalTex)) material.SetTexture(NormalTex, null);
            if (material.HasProperty(SmoothTex)) material.SetTexture(SmoothTex, null);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", Mathf.Clamp01(1f - data.roughness));
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", data.metallic);
            if (material.HasProperty(EmissionTex)) material.SetTexture(EmissionTex, glow != null ? glow : texture);
            material.SetColor(Emission, data.emission > 0 ? colour * data.emission : Color.black);
        }
        else
        {
            material = new Material(Shader.Find("Standard")) { name = "IHM cosmetic " + data.name };
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

/// <summary>The shark tail's lazy swish: side to side about its root, with a small lift, like a plush being carried.</summary>
internal sealed class TailSway : MonoBehaviour
{
    public Transform tail;
    private float _phase;

    private void Start() => _phase = UnityEngine.Random.value * 10f;

    private void LateUpdate()
    {
        if (tail == null) return;
        float t = Time.time + _phase;
        float swish = 11f * Mathf.Sin(t * Mathf.PI * 2f * .55f);
        float lift = 3f * Mathf.Sin(t * Mathf.PI * 2f * 1.1f + .6f);
        tail.localRotation = Quaternion.AngleAxis(swish, Vector3.up) * Quaternion.AngleAxis(lift, Vector3.right);
    }
}
