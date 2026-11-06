using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using IncreaseHardMod.Networking;
using IncreaseHardMod.Patches.Effects;
using IncreaseHardMod.Upgrades;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IncreaseHardMod;

public static class PluginInfo
{
    // Keep the identity stable across the rename: existing config and integrations use it.
    public const string PLUGIN_GUID = "com.kubai.increasehardmod";
    public const string PLUGIN_NAME = "IncreaseDifficultyMod";
    public const string PLUGIN_VERSION = "0.7.12";
}

[BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
[BepInDependency("REPOLib", BepInDependency.DependencyFlags.HardDependency)]
public class Plugin : BaseUnityPlugin
{
    public static Plugin Instance { get; private set; }
    public static ManualLogSource Log { get; private set; }

    private readonly Harmony _harmony = new(PluginInfo.PLUGIN_GUID);

    // The persistent GameObject that hosts all runtime MonoBehaviours (networking + HUDs).
    // Created lazily on scene load rather than in Awake: the BepInEx chainloader runs so
    // early that anything created there (even DontDestroyOnLoad) is destroyed on the first
    // scene transition, so its components never tick. Recreated automatically if destroyed.
    private static GameObject _runtimeObject;

    private void Awake()
    {
        Instance = this;
        Log = Logger;
        Log.LogInfo($"[Build] DLL={typeof(Plugin).Assembly.Location}; MVID={typeof(Plugin).Module.ModuleVersionId}");

        // Bind config entries first so patches can read them.
        ModConfig.Init(Config);

        if (ModConfig.DiagnosticDisablePatches.Value)
            Log.LogWarning("[Diagnostics] DisablePatches=true — skipping ALL Harmony patches.");
        else
        {
            // Patch class-by-class: one patch broken by a game update must not abort the rest
            // (a throw out of PatchAll used to kill Awake before the runtime object / upgrades existed).
            foreach (var type in typeof(Plugin).Assembly.GetTypes())
            {
                if (!type.IsDefined(typeof(HarmonyPatch), true))
                    continue;
                try { _harmony.CreateClassProcessor(type).Patch(); }
                catch (System.Exception e) { Log.LogError($"Patch {type.FullName} failed: {e.Message}"); }
            }
        }

        // Declare custom shop upgrades before the registration patch fires (on RunStartStats).
        // Effects are applied per-player via REPOLib start/upgrade actions (or a ticking
        // controller / Harmony patch for continuous / event-driven ones).
        StaminaUpgrade.Register();
        HpRegenUpgrade.Register();
        DeathProtectionUpgrade.Register();
        BatteryLifeUpgrade.Register();
        CombatDamageUpgrades.Register();
        SurvivalUpgrades.Register();
        AutoscanUpgrade.Register();
        MapUpgrade.Register();
        InventorySlotsUpgrade.Register();
        PingUpgrades.Register();
        TruckUpgrades.Register();
        // Wing cosmetics register from MetaManager.Awake (see WingCosmetics), not here.

        if (ModConfig.DiagnosticDisableRuntimeObjects.Value)
        {
            Log.LogWarning("[Diagnostics] DisableRuntimeObjects=true — skipping runtime menu/HUD objects.");
        }
        else
        {
            // Ensure the runtime object exists on every scene load (and try once now in case
            // a scene is already active). sceneLoaded is a static event, so it keeps firing
            // even though this plugin MonoBehaviour is destroyed on the boot transition.
            SceneManager.sceneLoaded += OnSceneLoaded;
            EnsureRuntimeObject();
        }

        Log.LogInfo($"{PluginInfo.PLUGIN_NAME} v{PluginInfo.PLUGIN_VERSION} loaded.");
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => EnsureRuntimeObject();

    private static void EnsureRuntimeObject()
    {
        // Unity's overloaded == treats a destroyed object as null, so this recreates the
        // object if REPO ever wipes it (e.g. returning to the main menu).
        if (_runtimeObject != null)
            return;

        var netObject = new GameObject("IncreaseHardMod_Runtime");
        netObject.AddComponent<ModNetwork>();
        netObject.AddComponent<InventorySlotsUpgrade.Controller>();
        netObject.AddComponent<VoiceRecoveryController>();
        // Host-side auto Loot Cash-In for the "Loot Cash-In" truck upgrade (sells leftovers on quota close).
        netObject.AddComponent<Upgrades.TruckUpgrades.SellController>();
        // Main-menu Photon name-server reachability overlay (IMGUI, background TCP probes).
        netObject.AddComponent<PhotonHud>();
        // Team ping system (input + native ValuableDiscoverGraphic highlights, synced over Photon).
        netObject.AddComponent<PingHud>();
        // Level info overlay (native TMP: top-centre title + top-right modifier list, toggle with LevelInfoToggleKey).
        netObject.AddComponent<LevelInfoHud>();
        // Spectator Map: one overlay for dead players, following the active spectator camera.
        netObject.AddComponent<SpectatorMapHud>();
        // Quest tracker from remote master.
        netObject.AddComponent<QuestHud>();
        netObject.AddComponent<ArmorHud>();
        // End-of-level MVP / OOPS CHAMP scoreboard (stats recorded host-side by RoundStats).
        netObject.AddComponent<RoundResultsHud>();
        // Black Market (shop): pay to block unwanted modifiers / economies. Keyboard driven.
        netObject.AddComponent<Economy.BlackMarketHud>();
        // Item Unstuck: host-side valuable safety net (relocate stuck loot, invuln-until-grab)
        // plus a client-side undamaged-value HUD.
        netObject.AddComponent<ItemUnstuckController>();
        // Passive left-side lobby/menu server info (region/ping/room), reads Photon state only.
        netObject.AddComponent<ServerInfoHud>();
        // In-shop cart overlay: aggregates ShopManager.shoppingList into a colour-coded panel.
        netObject.AddComponent<ShoppingListHud>();
        // Map Vote: lobby-phase vote on the next map (host rolls candidates + applies the winner).
        netObject.AddComponent<Patches.Effects.MapVoteController>();
        // Drives the per-player HP Regeneration upgrade (continuous local heal-up-to-cap).
        netObject.AddComponent<Upgrades.HpRegenUpgrade.RegenController>();
        // Drives the per-player Autoscan upgrade (periodic local valuable discovery).
        netObject.AddComponent<Upgrades.AutoscanUpgrade.ScanController>();
        // Drives the per-player Map Suite upgrade (map-open info overlay + zoom-out).
        netObject.AddComponent<Upgrades.MapUpgrade.MapController>();
        // Drives the Map Suite 3D markers (L9 cosmetic boxes, L10 monster dots).
        netObject.AddComponent<Upgrades.MapUpgrade.MarkerController>();
        // Winged circlet above each player's head, coloured from their own cosmetics colour.
        netObject.AddComponent<Visuals.WingedCircletDriver>();
        // Guest-side log of which level-generation step a mid-level joiner is waiting on.
        netObject.AddComponent<Patches.Effects.LateJoinDiagnostics>();
        // Everyone uses the host's moon level (it gates grab overcharge and other moon mechanics).
        netObject.AddComponent<Economy.MoonSync>();
        // Failed-level vote (restart / random map) and the host's Esc-menu run controls.
        netObject.AddComponent<Patches.Effects.HostRunControls>();
        Object.DontDestroyOnLoad(netObject);
        _runtimeObject = netObject;

        Log.LogInfo($"[Init] Runtime object created (scene-load): active={netObject.activeInHierarchy}, components={netObject.GetComponents<Component>().Length}.");
    }
}
