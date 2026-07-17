using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using IncreaseHardMod.Networking;
using IncreaseHardMod.Patches.Effects;
using UnityEngine;

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

    private void Awake()
    {
        Instance = this;
        Log = Logger;

        // Bind config entries first so patches can read them.
        ModConfig.Init(Config);

        _harmony.PatchAll();

        // Create the persistent networking object that sends/receives the rolled
        // modifier set over Photon. It survives scene loads (DontDestroyOnLoad).
        var netObject = new GameObject("IncreaseHardMod_Network");
        netObject.AddComponent<ModNetwork>();
        // On-screen countdown for the LevelTimer modifier (IMGUI, no assets needed).
        netObject.AddComponent<LevelTimerHud>();
        // Host-only cart/truck upgrade shop (IMGUI, opened with CartUpgradeMenuKey).
        netObject.AddComponent<CartUpgradeMenu>();
        DontDestroyOnLoad(netObject);

        Log.LogInfo($"{PluginInfo.PLUGIN_NAME} v{PluginInfo.PLUGIN_VERSION} loaded.");
    }
}
