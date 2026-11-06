using System;
using System.Collections.Generic;
using HarmonyLib;
using REPOLib.Modules;
using UnityEngine;
using UnityEngine.Events;
using RepoUpgrades = REPOLib.Modules.Upgrades;

namespace IncreaseHardMod.Upgrades;

/// <summary>
/// Builds our custom per-player upgrades at runtime and hands them to REPOLib so the
/// VANILLA shop rolls them (the user explicitly wanted default-shop upgrades, not a
/// custom menu). Nothing here authors Unity assets — every upgrade is a runtime CLONE
/// of a vanilla player-stat upgrade prefab with its effect swapped out.
///
/// Flow per upgrade:
///  1. Clone a vanilla <c>item_upgrade</c> prefab (e.g. "Item Upgrade Player Health").
///  2. Strip the vanilla effect: destroy its ItemUpgradePlayer* component and replace
///     ItemUpgrade.upgradeEvent with a fresh (empty) UnityEvent so buying it no longer
///     runs the vanilla PunManager.UpgradePlayer* call.
///  3. Bolt on <see cref="PlayerUpgradeHook"/>, which re-wires upgradeEvent on every
///     spawned copy to increment OUR level.
///  4. Create a fresh <see cref="Item"/> ScriptableObject (itemType item_upgrade so the
///     shop's upgrade booth rolls it — the shop filters on item_upgrade, NOT player_upgrade).
///  5. Register with REPOLib: <see cref="Items.RegisterItem"/> makes the shop spawn it,
///     <see cref="Upgrades.RegisterUpgrade"/> gives per-player level tracking + save +
///     Photon sync, and runs our startAction/upgradeAction.
///
/// Registration is driven off a postfix on StatsManager.RunStartStats (which populates
/// itemDictionary), ordered BEFORE REPOLib's own postfix that flushes pending content.
/// </summary>
internal static class PlayerUpgradeRegistry
{
    /// <summary>One custom upgrade to build. Effects live in the actions.</summary>
    internal sealed class Definition
    {
        public string Id;
        public string DisplayName;
        public string Description;
        public int MaxLevel;
        // Base shop price at 1 player, before the game's own scaling. Mapped to the
        // item's Value via BaseShopPrice * ValueToPrice below.
        public int BaseShopPrice;
        // Consumable = repurchasable indefinitely (no lifetime purchase cap). Used for
        // charge-style upgrades like death protection where "level" is a spendable count
        // and you re-buy after each use. Leveled upgrades (stamina, regen) leave this false
        // so the shop stops offering them once every level is owned.
        public bool Consumable;
        // Runs at each player's LateStart (re-apply on load) and when a level is bought.
        public Action<PlayerAvatar, int> StartAction;
        public Action<PlayerAvatar, int> UpgradeAction;
        // Printed native-layout atlas; Icon_ + filename is the separate transparent inventory render.
        public string IconFile;
        // When true, source the shop icon + physical card art from the vanilla "Map Player Count"
        // upgrade instead of an embedded PNG. Used by Map Suite, which replaces that vanilla upgrade
        // and reuses its map-themed art. Takes precedence over IconFile.
        public bool UseVanillaMapIcon = false;
        // Replaces the level increment when the item is used (host side), e.g. the Skill Point grants a point.
        public Action<PlayerAvatar> OnUse;
        // How many copies one shop may stock.
        public int ShopCopies = 1;
    }

    // Upgrades that now live in the skill tree keep their REPOLib level storage (old saves are read once for
    // migration) but get no shop item, so the shop and quest rewards stop offering them.
    private static bool LivesInSkillTree(string id) => Skills.SkillMigration.IsTreeCovered(id);

    // REPO shop costs are in THOUSANDS-of-dollars units: ShopCostUI shows the total as
    // "-$" + totalCost + "K", currency (StatGetRunCurrency) is likewise in K-units, and the
    // per-item cost the game compares against currency is  valueMax / 1000 * itemValueMultiplier(4)
    // = valueMax * 0.004  (ShopManager.PopulateItemVolumes / UpgradeValueGet). We want a
    // BaseShopPrice expressed in whole DOLLARS to map to  BaseShopPrice / 1000  K-units, so:
    //   valueMax * 0.004 = BaseShopPrice / 1000  ->  valueMax = BaseShopPrice * 0.25.
    // (The old value of 250 was 1000x too high — it treated the K-unit cost as raw dollars,
    // making every upgrade cost e.g. "$27,000K" = $27,000,000.)
    private const float PriceToValue = 0.25f;

    private static readonly List<Definition> _definitions = new();
    private static bool _registered;
    private static GameObject _templateHolder;

    /// <summary>Called from ModConfig/Plugin init to declare an upgrade before registration.</summary>
    public static void Define(Definition def) => _definitions.Add(def);
    internal static Definition FindDefinition(string id) => _definitions.Find(d => d.Id == id);

    // REPOLib 4.3 loads an existing stats dictionary into a local variable during registration.
    // Rebind the public PlayerDictionary as well so GetLevel, saved levels and effect gates agree.
    [HarmonyPatch(typeof(RepoUpgrades), "RegisterUpgrades")]
    private static class BindSavedLevels
    {
        [HarmonyPostfix]
        private static void Postfix()
        {
            PingUpgrades.MigrateLegacyPins();
            var stats = StatsManager.instance;
            if (stats == null) return;
            foreach (var upgrade in RepoUpgrades.PlayerUpgrades)
                if ((FindDefinition(upgrade.UpgradeId) != null || upgrade.UpgradeId.StartsWith(Skills.SkillStore.Prefix, StringComparison.Ordinal)) &&
                    stats.dictionaryOfDictionaries.TryGetValue("playerUpgrade" + upgrade.UpgradeId, out var saved))
                    upgrade.PlayerDictionary = saved;
        }
    }

    /// <summary>
    /// Idempotently clone + register every declared upgrade. Safe to call repeatedly
    /// (RunStartStats fires on every run start); only does the work once.
    /// </summary>
    public static void EnsureRegistered()
    {
        PingUpgrades.MigrateLegacyPins();
        if (_registered)
            return;

        var statsManager = StatsManager.instance;
        if (statsManager == null)
        {
            Plugin.Log.LogWarning("[Upgrades] StatsManager missing at registration time; skipping.");
            return;
        }

        var template = FindTemplatePrefab(statsManager, out var templateItem);
        if (template == null)
        {
            Plugin.Log.LogError("[Upgrades] Could not find a vanilla player upgrade prefab to clone. Custom upgrades disabled this session.");
            return;
        }

        _templateHolder = new GameObject("IHM_UpgradeTemplates");
        _templateHolder.SetActive(false); // keep clones dormant (no Awake) until the shop spawns them
        UnityEngine.Object.DontDestroyOnLoad(_templateHolder);

        int built = 0;
        foreach (var def in _definitions)
        {
            try
            {
                if (BuildAndRegister(def, template, templateItem))
                    built++;
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"[Upgrades] Failed to build upgrade '{def.Id}': {e}");
            }
        }

        _registered = true;
        Plugin.Log.LogInfo($"[Upgrades] Registered {built}/{_definitions.Count} custom shop upgrades.");
    }

    /// <summary>Find a vanilla item_upgrade whose prefab is a player-stat upgrade we can clone.</summary>
    private static GameObject FindTemplatePrefab(StatsManager statsManager, out Item templateItem)
    {
        templateItem = null;

        // Prefer the health upgrade by name; it's a stable, well-formed player upgrade.
        if (statsManager.itemDictionary.TryGetValue("Item Upgrade Player Health", out var health)
            && TryResolvePrefab(health, out var healthPrefab))
        {
            templateItem = health;
            return healthPrefab;
        }

        // Fallback: any item_upgrade whose prefab has an ItemUpgrade flagged isPlayerUpgrade.
        foreach (var item in statsManager.itemDictionary.Values)
        {
            if (item == null || item.itemType != SemiFunc.itemType.item_upgrade)
                continue;
            if (!TryResolvePrefab(item, out var prefab))
                continue;
            var upgrade = prefab.GetComponent<ItemUpgrade>();
            if (upgrade != null && upgrade.isPlayerUpgrade)
            {
                templateItem = item;
                return prefab;
            }
        }

        return null;
    }

    private static bool TryResolvePrefab(Item item, out GameObject prefab)
    {
        prefab = item != null && item.prefab != null ? item.prefab.Prefab : null;
        return prefab != null && prefab.GetComponent<ItemUpgrade>() != null;
    }

    private static bool BuildAndRegister(Definition def, GameObject template, Item templateItem)
    {
        if (LivesInSkillTree(def.Id))
        {
            RepoUpgrades.RegisterUpgrade(def.Id, null, def.StartAction, def.UpgradeAction == null ? null : (avatar, level) => def.UpgradeAction(avatar, level));
            Plugin.Log.LogInfo($"[Upgrades] '{def.Id}' moved to the skill tree: stored, not sold.");
            return true;
        }

        // 1. Clone under the inactive holder so no Awake/networking runs on the template.
        var clone = UnityEngine.Object.Instantiate(template, _templateHolder.transform);
        clone.name = "IHM_Upgrade_" + def.Id;

        // 2. Strip the vanilla effect: destroy its ItemUpgradePlayer* component(s) and
        //    replace upgradeEvent with an empty one (persistent listeners can't be removed
        //    individually at runtime, but swapping the whole UnityEvent wipes them).
        foreach (var mb in clone.GetComponents<MonoBehaviour>())
        {
            if (mb != null && mb.GetType().Name.StartsWith("ItemUpgradePlayer", StringComparison.Ordinal))
                UnityEngine.Object.DestroyImmediate(mb);
        }

        var itemUpgrade = clone.GetComponent<ItemUpgrade>();
        itemUpgrade.upgradeEvent = new UnityEvent();
        itemUpgrade.isPlayerUpgrade = true;

        // 3. Our self-wiring hook (re-links upgradeEvent -> our level increment per instance).
        var hook = clone.AddComponent<PlayerUpgradeHook>();
        hook.UpgradeId = def.Id;

        // 4. Fresh Item + Value so we don't mutate the shared vanilla asset.
        var value = ScriptableObject.CreateInstance<Value>();
        value.name = "IHM Value " + def.Id;
        value.valueMin = value.valueMax = def.BaseShopPrice * PriceToValue;

        var item = ScriptableObject.CreateInstance<Item>();
        item.name = "IHM Upgrade " + def.Id;
        item.itemName = def.DisplayName;
        item.description = def.Description;
        item.itemType = SemiFunc.itemType.item_upgrade;
        item.itemVolume = templateItem.itemVolume;
        item.emojiIcon = templateItem.emojiIcon;
        item.colorPreset = templateItem.colorPreset;
        item.value = value;
        item.maxAmount = Mathf.Max(1, def.MaxLevel);
        item.maxAmountInShop = Mathf.Max(1, def.ShopCopies);
        // Leveled upgrades cap total lifetime purchases at MaxLevel so the shop stops
        // offering them once maxed. Consumables have NO lifetime cap so they keep
        // reappearing to be re-bought after each use.
        item.maxPurchase = !def.Consumable;
        item.maxPurchaseAmount = Mathf.Max(1, def.MaxLevel);
        item.minPlayerCount = 1;
        item.spawnRotationOffset = templateItem.spawnRotationOffset;
        item.physicalItem = templateItem.physicalItem;

        var attributes = clone.GetComponent<ItemAttributes>();
        attributes.item = item;

        // Apply the authored UV atlas and its separate transparent shop/inventory render.
        if (def.UseVanillaMapIcon)
        {
            ApplyVanillaMapIcon(clone, def.Id, attributes);
        }
        else if (!string.IsNullOrEmpty(def.IconFile))
        {
            UpgradeAppearance.Apply(clone, def.Id, def.IconFile);
        }

        // 5. Register with REPOLib (sets item.prefab, adds to StatsManager, enables shop roll).
        var prefabRef = Items.RegisterItem(attributes);
        if (prefabRef == null)
        {
            Plugin.Log.LogError($"[Upgrades] REPOLib refused to register item for '{def.Id}'.");
            return false;
        }

        RepoUpgrades.RegisterUpgrade(def.Id, item, def.StartAction, (avatar, level) =>
        {
            def.UpgradeAction?.Invoke(avatar, level);
            if (avatar != null && avatar.isLocal && StatsUI.instance != null && PlayerController.instance != null)
                StatsUI.instance.Fetch();
        });

        // Give the truck's upgrade screen a clean label. That screen (StatsUI.GetUpgradeDisplayName)
        // shows StatsManager.upgradesInfo[key].displayName if present, otherwise it space-splits the
        // raw upgrade id — turning "IHM_DroneBattery" into "IHM_ DRONE BATTERY". Registering our own
        // UpgradeInfo makes it read our DisplayName ("Drone Battery") instead.
        var stats = StatsManager.instance;
        if (stats != null && stats.upgradesInfo != null && !stats.upgradesInfo.ContainsKey(def.Id))
            stats.upgradesInfo[def.Id] = new StatsManager.UpgradeInfo { displayName = def.DisplayName };

        Plugin.Log.LogInfo($"[Upgrades] Built '{def.Id}' ({def.DisplayName}) maxLvl={def.MaxLevel} price~{def.BaseShopPrice}.");
        return true;
    }

    // Resolve before StatsUI's fallback/cache; REPOLib and StatsManager may rebuild their dictionaries.
    [HarmonyPatch(typeof(StatsUI), "GetUpgradeDisplayName")]
    private static class DisplayNamePatch
    {
        [HarmonyPrefix]
        private static bool Prefix(string key, ref string __result)
        {
            if (string.IsNullOrEmpty(key)) return true;
            string id = key.StartsWith("playerUpgrade", StringComparison.Ordinal) ? key.Substring("playerUpgrade".Length) : key;
            foreach (var definition in _definitions)
                if (definition.Id == id)
                {
                    __result = definition.DisplayName;
                    return false;
                }
            return true;
        }
    }

    /// <summary>
    /// Give an upgrade the vanilla "Map Player Count" upgrade's art: copy that upgrade's shop icon
    /// sprite onto our ItemAttributes, and retexture our physical card with its card-face texture.
    /// Map Suite removes the vanilla map upgrade from the shop, so reusing its map-themed art makes
    /// Map Suite read as its clear replacement without authoring a new PNG. Best-effort: if the
    /// vanilla prefab can't be found the card just keeps the default clone art.
    /// </summary>
    private static void ApplyVanillaMapIcon(GameObject clone, string id, ItemAttributes attributes)
    {
        var vanilla = FindVanillaMapUpgradePrefab();
        if (vanilla == null)
        {
            Plugin.Log.LogWarning($"[Icons] Vanilla 'Map Player Count' upgrade not found; '{id}' keeps default art.");
            return;
        }

        var vanillaAttr = vanilla.GetComponentInChildren<ItemAttributes>(true);
        if (vanillaAttr != null && vanillaAttr.icon != null)
            attributes.icon = vanillaAttr.icon;

        foreach (var renderer in clone.GetComponentsInChildren<Renderer>(true))
        {
            var original = vanilla.transform.Find(Path(renderer.transform, clone.transform));
            var native = original != null ? original.GetComponent<Renderer>() : null;
            if (native != null) renderer.sharedMaterials = native.sharedMaterials;
        }
    }

    /// <summary>Locate the vanilla map-player-count upgrade prefab via its marker component.</summary>
    private static GameObject FindVanillaMapUpgradePrefab()
    {
        var statsManager = StatsManager.instance;
        if (statsManager == null || statsManager.itemDictionary == null)
            return null;

        foreach (var item in statsManager.itemDictionary.Values)
        {
            var prefab = item != null && item.prefab != null ? item.prefab.Prefab : null;
            if (prefab != null && prefab.GetComponentInChildren<ItemUpgradeMapPlayerCount>(true) != null)
                return prefab;
        }
        return null;
    }

    private static string Path(Transform t, Transform root)
    {
        var stack = new System.Collections.Generic.Stack<string>();
        while (t != null && t != root)
        {
            stack.Push(t.name);
            t = t.parent;
        }
        return string.Join("/", stack);
    }

    /// <summary>Runs after itemDictionary is populated, before REPOLib flushes its content.</summary>
    [HarmonyPatch(typeof(StatsManager), "RunStartStats")]
    private static class RegisterPatch
    {
        [HarmonyPostfix]
        [HarmonyBefore("REPOLib")]
        private static void Postfix() => EnsureRegistered();
    }
}
