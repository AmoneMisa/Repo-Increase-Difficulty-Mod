using System.Collections.Generic;
using System.Reflection;
using BepInEx.Configuration;

namespace IncreaseHardMod;

/// <summary>Moves level economy tuning under Surprises and retires its separate module switch.</summary>
internal sealed class EconomicSurpriseConfig
{
    internal ConfigEntry<int> Chance;
    internal ConfigEntry<int> StartLevel;

    internal static EconomicSurpriseConfig Bind(ConfigFile cfg)
    {
        var orphans = cfg.OrphanedEntries; // BepInEx keeps it protected (publicized)
        string Raw(string section, string key)
        {
            var definition = new ConfigDefinition(section, key);
            if (cfg.ContainsKey(definition)) return cfg[definition].GetSerializedValue();
            return orphans != null && orphans.TryGetValue(definition, out var raw) ? raw : null;
        }

        bool chanceExists = Raw("Surprises", "EconomyEventChancePercent") != null;
        bool startExists = Raw("Surprises", "EconomyStartLevel") != null;
        string oldChance = Raw("Economy", "EventChancePercent");
        string oldStart = Raw("Economy", "StartLevel");
        string oldEnabled = Raw("Modules", "EconomyEvents") ?? Raw("Economy", "Enabled");
        bool disabledBefore = bool.TryParse(oldEnabled, out bool enabled) && !enabled;

        bool autoSave = cfg.SaveOnConfigSet;
        cfg.SaveOnConfigSet = false;
        EconomicSurpriseConfig result;
        try
        {
            result = new EconomicSurpriseConfig
            {
                Chance = cfg.Bind("Surprises", "EconomyEventChancePercent", 50,
                    new ConfigDescription("Chance per eligible level of an economic surprise changing loot value, quota or extraction pay. Requires Modules/Surprises. 0 disables this category.",
                        new AcceptableValueRange<int>(0, 100))),
                StartLevel = cfg.Bind("Surprises", "EconomyStartLevel", 2,
                    new ConfigDescription("Levels completed before economic surprises can appear. 2 = from the third level. Requires Modules/Surprises.",
                        new AcceptableValueRange<int>(0, 30))),
            };
            if (!chanceExists)
            {
                if (disabledBefore) result.Chance.Value = 0;
                else if (oldChance != null) result.Chance.SetSerializedValue(oldChance);
            }
            if (!startExists && oldStart != null) result.StartLevel.SetSerializedValue(oldStart);
            foreach (var definition in new[]
            {
                new ConfigDefinition("Modules", "EconomyEvents"),
                new ConfigDefinition("Economy", "Enabled"),
                new ConfigDefinition("Economy", "EventChancePercent"),
                new ConfigDefinition("Economy", "StartLevel"),
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
}
