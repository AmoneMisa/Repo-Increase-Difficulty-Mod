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
        // Optional custom shop icon: file name of an embedded PNG (e.g. "Upgrade_Ping.png").
        // When set, it's assigned to the item's ItemAttributes.icon so the shop shows our art
        // instead of an auto-generated render. Null = keep the game's default generated icon.
        public string IconFile;
    }

    // Shop price ≈ value.valueMax / 1000 * itemValueMultiplier(4) = valueMax * 0.004 at
    // one player (see ShopManager.UpgradeValueGet). So valueMax = price * 250.
    private const float PriceToValue = 250f;

    private static readonly List<Definition> _definitions = new();
    private static bool _registered;
    private static GameObject _templateHolder;

    /// <summary>Called from ModConfig/Plugin init to declare an upgrade before registration.</summary>
    public static void Define(Definition def) => _definitions.Add(def);

    /// <summary>
    /// Idempotently clone + register every declared upgrade. Safe to call repeatedly
    /// (RunStartStats fires on every run start); only does the work once.
    /// </summary>
    public static void EnsureRegistered()
    {
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
        item.maxAmountInShop = 1;
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

        // Custom shop icon: pre-assign our sprite so the game skips its auto-generated icon.
        if (!string.IsNullOrEmpty(def.IconFile))
        {
            var sprite = UpgradeIcons.Load(def.IconFile);
            if (sprite != null)
                attributes.icon = sprite;
        }

        // 5. Register with REPOLib (sets item.prefab, adds to StatsManager, enables shop roll).
        var prefabRef = Items.RegisterItem(attributes);
        if (prefabRef == null)
        {
            Plugin.Log.LogError($"[Upgrades] REPOLib refused to register item for '{def.Id}'.");
            return false;
        }

        RepoUpgrades.RegisterUpgrade(def.Id, item, def.StartAction, def.UpgradeAction);
        Plugin.Log.LogInfo($"[Upgrades] Built '{def.Id}' ({def.DisplayName}) maxLvl={def.MaxLevel} price~{def.BaseShopPrice}.");
        return true;
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
