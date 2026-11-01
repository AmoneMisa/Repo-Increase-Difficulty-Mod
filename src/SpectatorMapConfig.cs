using System.Collections.Generic;
using System.Reflection;
using BepInEx.Configuration;
using UnityEngine;

namespace IncreaseHardMod;

/// <summary>Migrates the two retired map modules into one user-facing switch.</summary>
internal sealed class SpectatorMapConfig
{
    internal ConfigEntry<bool> Enabled;
    internal ConfigEntry<KeyCode> HoldKey;
    internal ConfigEntry<float> ScreenFraction;

    internal static SpectatorMapConfig Bind(ConfigFile cfg)
    {
        var orphans = typeof(ConfigFile).GetProperty("OrphanedEntries",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(cfg)
            as Dictionary<ConfigDefinition, string>;
        var enabledDef = new ConfigDefinition("Modules", "SpectatorMap");
        var keyDef = new ConfigDefinition("Spectator Map", "HoldKey");
        bool enabledExists = cfg.ContainsKey(enabledDef) || (orphans?.ContainsKey(enabledDef) ?? false);
        bool keyExists = cfg.ContainsKey(keyDef) || (orphans?.ContainsKey(keyDef) ?? false);

        bool? deadMap = LegacyEnabled(cfg, orphans, "DeadMapAccess", "Dead Map Access");
        bool? minimap = LegacyEnabled(cfg, orphans, "SpectatorMinimap", "Spectator Minimap");
        // Preserve access if either previous feature was enabled; keep it disabled if both
        // were explicitly disabled. An existing canonical setting always takes precedence.
        bool legacyEnabled = deadMap.HasValue || minimap.HasValue
            ? deadMap == true || minimap == true : true;
        string oldKey = Raw(cfg, orphans, new ConfigDefinition("Spectator Minimap", "HoldKey"));

        bool autoSave = cfg.SaveOnConfigSet;
        cfg.SaveOnConfigSet = false;
        SpectatorMapConfig result;
        try
        {
            result = new SpectatorMapConfig
            {
                Enabled = cfg.Bind(enabledDef, true,
                    new ConfigDescription("Map for dead/spectating players. Follows the spectator camera. Normal map key or hold key opens it; arrows pan, map zoom keys change scale, Home restores follow.")),
                HoldKey = cfg.Bind(keyDef, KeyCode.Tab,
                    new ConfigDescription("Hold this key while dead to show the spectator map. The game's normal map binding also works.")),
                ScreenFraction = cfg.Bind("Spectator Map", "ScreenFraction", 0.8f,
                    new ConfigDescription("Map size relative to reference screen height. 0.8 = a large map.",
                        new AcceptableValueRange<float>(0.1f, 0.9f))),
            };
            if (!enabledExists) result.Enabled.Value = legacyEnabled;
            if (!keyExists && !string.IsNullOrEmpty(oldKey)) result.HoldKey.SetSerializedValue(oldKey);
            // The old Scale belonged to the compact minimap. The merged map starts large;
            // subsequent ScreenFraction customizations are retained by normal config binding.
            foreach (var definition in new[]
            {
                new ConfigDefinition("Modules", "DeadMapAccess"),
                new ConfigDefinition("Modules", "SpectatorMinimap"),
                new ConfigDefinition("Dead Map Access", "Enabled"),
                new ConfigDefinition("Spectator Minimap", "Enabled"),
                new ConfigDefinition("Spectator Minimap", "HoldKey"),
                new ConfigDefinition("Spectator Minimap", "ToggleKey"),
                new ConfigDefinition("Spectator Minimap", "Scale"),
            })
            {
                cfg.Remove(definition);
                orphans?.Remove(definition);
            }
        }
        finally
        {
            cfg.SaveOnConfigSet = autoSave;
        }
        if (autoSave) cfg.Save();
        return result;
    }

    private static bool? LegacyEnabled(ConfigFile cfg, Dictionary<ConfigDefinition, string> orphans,
        string module, string section)
    {
        var raw = Raw(cfg, orphans, new ConfigDefinition("Modules", module)) ??
                  Raw(cfg, orphans, new ConfigDefinition(section, "Enabled"));
        return bool.TryParse(raw, out var value) ? value : null;
    }

    private static string Raw(ConfigFile cfg, Dictionary<ConfigDefinition, string> orphans, ConfigDefinition key)
    {
        if (cfg.ContainsKey(key)) return cfg[key].GetSerializedValue();
        return orphans != null && orphans.TryGetValue(key, out var raw) ? raw : null;
    }
}
