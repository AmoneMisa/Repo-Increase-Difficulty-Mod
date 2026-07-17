using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using IncreaseHardMod.Networking;
using IncreaseHardMod.Patches.Effects;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IncreaseHardMod;

public static class PluginInfo
{
    public const string PLUGIN_GUID = "com.kubai.increasehardmod";
    public const string PLUGIN_NAME = "Increase Hard Mod";
    public const string PLUGIN_VERSION = "0.1.0";
}

[BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
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

        // Bind config entries first so patches can read them.
        ModConfig.Init(Config);

        if (ModConfig.DiagnosticDisablePatches.Value)
            Log.LogWarning("[Diagnostics] DisablePatches=true — skipping ALL Harmony patches.");
        else
            _harmony.PatchAll();

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
        // On-screen countdown for the LevelTimer modifier (IMGUI, no assets needed).
        netObject.AddComponent<LevelTimerHud>();
        // Host-only cart/truck upgrade shop (IMGUI, opened with CartUpgradeMenuKey).
        netObject.AddComponent<CartUpgradeMenu>();
        // Main-menu Photon name-server reachability overlay (IMGUI, background TCP probes).
        netObject.AddComponent<PhotonHud>();
        // Team ping system (input + world-anchored IMGUI markers, synced over Photon).
        netObject.AddComponent<PingHud>();
        // Right-side level-change log (IMGUI, toggle with LevelInfoToggleKey).
        netObject.AddComponent<LevelInfoHud>();
        // Passive left-side lobby/menu server info (region/ping/room), reads Photon state only.
        netObject.AddComponent<ServerInfoHud>();
        Object.DontDestroyOnLoad(netObject);
        _runtimeObject = netObject;

        Log.LogInfo($"[Init] Runtime object created (scene-load): active={netObject.activeInHierarchy}, components={netObject.GetComponents<Component>().Length}.");
    }
}
