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
/// (silver cuirass with or without the red mantle and cape, war skirt, two bracers) and the Petal Fairy set (crop top
/// with or without glowing petal wings, wrap skirt, two sleeves, two striped stockings with boots) and the Gilded Shadow
/// set (hood, corset and coat, veiled skirt, two puff sleeves, two stockings with boots), plus the Horned Seer, Card
/// Jester, Ghost Bride and Mossy Stone Angel sets, each part in its
/// own slot so it mixes with anything else. Character sets (Ghost Bride, Chel) also have a face, lips and a jaw in the
/// game's Face Top, Face Bottom and Head Bottom slots: skin round the native eyes, brows, lashes and lips. Plush, galaxy fabric, lace and gems are tintable: they take the colour
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
        public readonly string Set;   // "wings", "shark", "starlight", "legate", "fairy", "gilded", "seer", "jester", "bride", "angel": each set has its own config switch
        public Definition(string key, string title, SemiFunc.CosmeticType type = SemiFunc.CosmeticType.BodyTop, string slot = "Body Top",
            SemiFunc.Rarity rarity = SemiFunc.Rarity.Rare, bool tintable = false, string set = "wings")
        { Key = key; Title = title; Type = type; Slot = slot; Rarity = rarity; Tintable = tintable; Set = set; }
    }

    internal static readonly Definition[] Definitions =
    {
        new("wings_astral", "Astral Night Wings", tintable: true),
        new("wings_solar", "Solar Seraph Wings", tintable: true),
        new("wings_prism", "Prism Chime Wings", tintable: true),
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
        new("fairy_top", "Petal Fairy Top", SemiFunc.CosmeticType.BodyTop, "Body Top", SemiFunc.Rarity.Uncommon, true, "fairy"),
        new("fairy_hair", "Petal Fairy Hair", SemiFunc.CosmeticType.Hat, "Hat", SemiFunc.Rarity.Uncommon, true, "fairy"),
        new("fairy_top_wings", "Petal Fairy Top and Wings", SemiFunc.CosmeticType.BodyTop, "Body Top", SemiFunc.Rarity.UltraRare, true, "fairy"),
        new("fairy_skirt", "Petal Fairy Skirt", SemiFunc.CosmeticType.BodyBottom, "Body Bottom", SemiFunc.Rarity.Rare, true, "fairy"),
        new("fairy_sleeve_r", "Petal Fairy Sleeve Right", SemiFunc.CosmeticType.ArmRight, "Arm Right", SemiFunc.Rarity.Uncommon, true, "fairy"),
        new("fairy_sleeve_l", "Petal Fairy Sleeve Left", SemiFunc.CosmeticType.ArmLeft, "Arm Left", SemiFunc.Rarity.Uncommon, true, "fairy"),
        new("fairy_leg_r", "Petal Fairy Stocking Right", SemiFunc.CosmeticType.LegRight, "Leg Right", SemiFunc.Rarity.Uncommon, true, "fairy"),
        new("fairy_leg_l", "Petal Fairy Stocking Left", SemiFunc.CosmeticType.LegLeft, "Leg Left", SemiFunc.Rarity.Uncommon, true, "fairy"),
        new("gilded_hood", "Gilded Shadow Hood", SemiFunc.CosmeticType.Hat, "Hat", SemiFunc.Rarity.Rare, true, "gilded"),
        new("gilded_top", "Gilded Shadow Corset and Coat", SemiFunc.CosmeticType.BodyTop, "Body Top", SemiFunc.Rarity.Rare, true, "gilded"),
        new("gilded_skirt", "Gilded Shadow Veil Skirt", SemiFunc.CosmeticType.BodyBottom, "Body Bottom", SemiFunc.Rarity.UltraRare, true, "gilded"),
        new("gilded_sleeve_r", "Gilded Shadow Sleeve Right", SemiFunc.CosmeticType.ArmRight, "Arm Right", SemiFunc.Rarity.Uncommon, true, "gilded"),
        new("gilded_sleeve_l", "Gilded Shadow Sleeve Left", SemiFunc.CosmeticType.ArmLeft, "Arm Left", SemiFunc.Rarity.Uncommon, true, "gilded"),
        new("gilded_leg_r", "Gilded Shadow Stocking Right", SemiFunc.CosmeticType.LegRight, "Leg Right", SemiFunc.Rarity.Uncommon, true, "gilded"),
        new("gilded_leg_l", "Gilded Shadow Stocking Left", SemiFunc.CosmeticType.LegLeft, "Leg Left", SemiFunc.Rarity.Uncommon, true, "gilded"),
        new("seer_hood", "Horned Seer Hood", SemiFunc.CosmeticType.Hat, "Hat", SemiFunc.Rarity.Rare, true, "seer"),
        new("seer_robe", "Horned Seer Robe", SemiFunc.CosmeticType.BodyTop, "Body Top", SemiFunc.Rarity.Rare, true, "seer"),
        new("seer_skirt", "Horned Seer Robe Skirt", SemiFunc.CosmeticType.BodyBottom, "Body Bottom", SemiFunc.Rarity.UltraRare, true, "seer"),
        new("seer_sleeve_r", "Horned Seer Sleeve Right", SemiFunc.CosmeticType.ArmRight, "Arm Right", SemiFunc.Rarity.Uncommon, true, "seer"),
        new("seer_sleeve_l", "Horned Seer Sleeve Left", SemiFunc.CosmeticType.ArmLeft, "Arm Left", SemiFunc.Rarity.Uncommon, true, "seer"),
        new("jester_hat", "Card Jester Hat", SemiFunc.CosmeticType.Hat, "Hat", SemiFunc.Rarity.Rare, true, "jester"),
        new("jester_mask", "Card Jester Mask", SemiFunc.CosmeticType.FaceTop, "Face Top", SemiFunc.Rarity.Uncommon, true, "jester"),
        new("jester_doublet", "Card Jester Doublet", SemiFunc.CosmeticType.BodyTop, "Body Top", SemiFunc.Rarity.Rare, true, "jester"),
        new("jester_trousers", "Card Jester Breeches", SemiFunc.CosmeticType.BodyBottom, "Body Bottom", SemiFunc.Rarity.Rare, true, "jester"),
        new("jester_leg_r", "Card Jester Trouser Leg Right", SemiFunc.CosmeticType.LegRight, "Leg Right", SemiFunc.Rarity.Uncommon, true, "jester"),
        new("jester_leg_l", "Card Jester Trouser Leg Left", SemiFunc.CosmeticType.LegLeft, "Leg Left", SemiFunc.Rarity.Uncommon, true, "jester"),
        new("jester_sleeve_r", "Card Jester Sleeve Right", SemiFunc.CosmeticType.ArmRight, "Arm Right", SemiFunc.Rarity.Uncommon, true, "jester"),
        new("jester_sleeve_l", "Card Jester Sleeve Left", SemiFunc.CosmeticType.ArmLeft, "Arm Left", SemiFunc.Rarity.Uncommon, true, "jester"),
        new("bride_veil", "Ghost Bride Veil", SemiFunc.CosmeticType.Hat, "Hat", SemiFunc.Rarity.Rare, true, "bride"),
        new("bride_corset", "Ghost Bride Corset", SemiFunc.CosmeticType.BodyTop, "Body Top", SemiFunc.Rarity.Rare, true, "bride"),
        new("bride_skirt", "Ghost Bride Skirt", SemiFunc.CosmeticType.BodyBottom, "Body Bottom", SemiFunc.Rarity.UltraRare, true, "bride"),
        new("bride_glove_r", "Ghost Bride Glove Right", SemiFunc.CosmeticType.ArmRight, "Arm Right", SemiFunc.Rarity.Uncommon, true, "bride"),
        new("bride_glove_l", "Ghost Bride Glove Left", SemiFunc.CosmeticType.ArmLeft, "Arm Left", SemiFunc.Rarity.Uncommon, true, "bride"),
        new("bride_leg_r", "Ghost Bride Bone Leg Right", SemiFunc.CosmeticType.LegRight, "Leg Right", SemiFunc.Rarity.Uncommon, true, "bride"),
        new("bride_leg_l", "Ghost Bride Leg Left", SemiFunc.CosmeticType.LegLeft, "Leg Left", SemiFunc.Rarity.Uncommon, true, "bride"),
        // Faces keep their own colours (skin, brows, lips), so they are not tintable.
        new("bride_face", "Ghost Bride Face", SemiFunc.CosmeticType.FaceTop, "Face Top", SemiFunc.Rarity.Rare, false, "bride"),
        new("bride_lips", "Ghost Bride Lips", SemiFunc.CosmeticType.FaceBottom, "Face Bottom", SemiFunc.Rarity.Uncommon, false, "bride"),
        new("bride_neck", "Ghost Bride Jaw", SemiFunc.CosmeticType.HeadBottom, "Head Bottom", SemiFunc.Rarity.Uncommon, false, "bride"),
        new("angel_veil", "Stone Angel Veil", SemiFunc.CosmeticType.Hat, "Hat", SemiFunc.Rarity.Rare, true, "angel"),
        new("angel_robe", "Stone Angel Robe", SemiFunc.CosmeticType.BodyTop, "Body Top", SemiFunc.Rarity.Uncommon, true, "angel"),
        new("angel_robe_wings", "Stone Angel Robe and Wings", SemiFunc.CosmeticType.BodyTop, "Body Top", SemiFunc.Rarity.UltraRare, true, "angel"),
        new("angel_skirt", "Stone Angel Robe Skirt", SemiFunc.CosmeticType.BodyBottom, "Body Bottom", SemiFunc.Rarity.Rare, true, "angel"),
        new("angel_sleeve_r", "Stone Angel Sleeve Right", SemiFunc.CosmeticType.ArmRight, "Arm Right", SemiFunc.Rarity.Uncommon, true, "angel"),
        new("angel_sleeve_l", "Stone Angel Sleeve Left", SemiFunc.CosmeticType.ArmLeft, "Arm Left", SemiFunc.Rarity.Uncommon, true, "angel"),
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
        if (_registered || !ModConfig.WingCosmeticsEnabled.Value && !ModConfig.SharkCosmeticsEnabled.Value && !ModConfig.StarlightCosmeticsEnabled.Value && !ModConfig.LegateCosmeticsEnabled.Value && !ModConfig.FairyCosmeticsEnabled.Value && !ModConfig.GildedCosmeticsEnabled.Value && !ModConfig.SeerCosmeticsEnabled.Value && !ModConfig.JesterCosmeticsEnabled.Value
            && !ModConfig.BrideCosmeticsEnabled.Value && !ModConfig.AngelCosmeticsEnabled.Value && !ModConfig.MascotCosmeticsEnabled.Value) return;
        _registered = true;
        // Templates live under an inactive holder: Unity never runs their Awake, while instances made by
        // PlayerCosmetics (parented to the player) are active and build their meshes.
        _templates = new GameObject("IHM Cosmetic Templates");
        _templates.SetActive(false);
        Object.DontDestroyOnLoad(_templates);
        _templates.hideFlags = HideFlags.HideAndDontSave;
        foreach (var definition in AllDefinitions())
        {
            var enabled = definition.Set switch
            {
                "shark" => ModConfig.SharkCosmeticsEnabled.Value,
                "starlight" => ModConfig.StarlightCosmeticsEnabled.Value,
                "legate" => ModConfig.LegateCosmeticsEnabled.Value,
                "fairy" => ModConfig.FairyCosmeticsEnabled.Value,
                "gilded" => ModConfig.GildedCosmeticsEnabled.Value,
                "seer" => ModConfig.SeerCosmeticsEnabled.Value,
                "jester" => ModConfig.JesterCosmeticsEnabled.Value,
                "bride" => ModConfig.BrideCosmeticsEnabled.Value,
                "angel" => ModConfig.AngelCosmeticsEnabled.Value,
                "mascots" => ModConfig.MascotCosmeticsEnabled.Value,
                _ => ModConfig.WingCosmeticsEnabled.Value,
            };
            if (!enabled) continue;
            try { RegisterOne(definition); }
            catch (Exception error) { Plugin.Log?.LogError("[Cosmetics] Could not register " + definition.Title + ": " + error); }
        }
    }

    internal static IEnumerable<Definition> AllDefinitions()
    {
        foreach (var definition in Definitions) yield return definition;
        foreach (var definition in MascotCosmetics.Definitions) yield return definition;
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
        // A part of fixed fabrics only still opens the colour page when one of them is an optional channel.
        asset.tintable = definition.Tintable || CostumeChannels.Channels(definition.Key).Count > 0;
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

    // Some menu avatars are instantiated inactive: their Awake can run after Cosmetic.Setup.
    [HarmonyLib.HarmonyPatch(typeof(Cosmetic), nameof(Cosmetic.Setup))]
    private static class BuildBeforeSetup
    {
        private static void Prefix(Cosmetic __instance) => __instance.GetComponent<WingCosmeticVisual>()?.Build();
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
                mesh.RecalculateTangents();
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
        CosmeticCloth cloth = null;
        for (int i = 0; i < model.parts.Length; i++)
        {
            var data = model.parts[i];
            var part = new GameObject("Part " + i.ToString("D3")) { layer = gameObject.layer };
            part.transform.SetParent(visuals, false);
            part.transform.localPosition = data.rest.position;
            if (data.group != null && pivots.TryGetValue(data.group, out var parent)) part.transform.SetParent(parent, true);
            var filter = part.AddComponent<MeshFilter>();
            filter.sharedMesh = cached.meshes[i];
            bool cape = data.group == "cape";
            float seam = .345f;
            if (cape || (data.group == null || data.group == "static") && CosmeticCloth.Seam(modelKey, model.materials[data.material].name, out seam))
            {
                cloth ??= gameObject.AddComponent<CosmeticCloth>();
                cloth.Add(filter, data.rest.position, seam, modelKey == "bride_veil" ? .045f : .016f);
            }
            part.AddComponent<MeshRenderer>().sharedMaterial = cached.materials[data.material];
            var playerMaterial = part.AddComponent<PlayerMaterial>();   // hurt flash and colour hooks
            playerMaterial.cosmeticType = cosmeticType;
            playerMaterial.tintable = model.materials[data.material].tint;   // takes the slot colour picked in the menu
            var art = model.materials[data.material];
            playerMaterial.tintType = art.tintEmission ? PlayerMaterial.TintType.Both : PlayerMaterial.TintType.Albedo;   // glowing stars and gems take the tint too
            // Second and accent fabrics take the player's colour for that channel (CostumeChannels) instead of the slot colour.
            // Fixed fabrics on a channel (hair, lining, leather) keep their own colour until the player sets that channel.
            if (art.channel > 0)
            {
                var tag = part.AddComponent<CostumeChannelTag>();
                tag.channel = Mathf.Min(art.channel, CostumeChannels.Extra);
                tag.optional = !art.tint; tag.natural = art.color; tag.emission = art.emission;
            }
        }
        if (pivots.Count > 0)
        {
            var motion = gameObject.AddComponent<CosmeticMotion>();
            foreach (var pivot in pivots)
                if (pivot.Key != "cape") motion.Add(pivot.Value, pivot.Key); // cape cloth keeps the shoulder seam fixed
        }
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
        // Always Repeat: the game's materials keep the Hurtable shader's UV offset of 1 (a whole tile, harmless on a
        // repeating texture). A clamped texture would be read at its corner texel instead: a baked atlas turns black.
        if (loaded != null) { loaded.wrapMode = TextureWrapMode.Repeat; loaded.filterMode = FilterMode.Bilinear; }
        var glow = string.IsNullOrEmpty(data.emissionTexture) ? null : UpgradeIcons.LoadTexture(data.emissionTexture);
        if (glow != null) { glow.wrapMode = TextureWrapMode.Repeat; glow.filterMode = FilterMode.Bilinear; }
        var texture = loaded != null ? loaded : Texture2D.whiteTexture;
        // Tintable textures are grey and take their colour from the material (the slot tint replaces it in game).
        Color colour = string.IsNullOrEmpty(data.texture) || data.tint || data.multiply ? data.color : Color.white;
        Material material;
        if (template != null)
        {
            // Keep the game's shader, not a donor cosmetic's masks, effect keywords or UV transforms.
            material = new Material(template.shader) { name = "IHM cosmetic " + data.name };
            material.SetTexture(AlbedoTex, texture);
            material.SetColor(Albedo, colour);
            // Preserve the shader's neutral normal default. A null/black normal tilts every surface.
            if (material.HasProperty(SmoothTex)) material.SetTexture(SmoothTex, Texture2D.blackTexture);
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
