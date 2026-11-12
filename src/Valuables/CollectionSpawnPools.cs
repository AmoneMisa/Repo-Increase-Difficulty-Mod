using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using REPOLib.Modules;
using UnityEngine;

namespace IncreaseHardMod.Valuables;

internal static class CollectionSpawnPools
{
    private static readonly Dictionary<LootMap,string> Names=new();
    internal static void Prepare(RunManager run)
    {
        foreach(var level in run.levels)
        {
            if(level==null)continue;
            var map=CollectionCatalog.Identify(level.name,level.NarrativeName);if(map==LootMap.None)continue;
            string name="Valuables - IHM Collection - "+map;
            if(!ValuablePresets.AllValuablePresets.TryGetValue(name,out var preset))
            {
                preset=ScriptableObject.CreateInstance<LevelValuables>();preset.name=name;
                preset.tiny=new();preset.small=new();preset.medium=new();preset.big=new();preset.wide=new();preset.tall=new();preset.veryTall=new();
                // REPOLib 4.3 exposes registration only internally (publicized). Register an empty preset
                // before adding network prefabs, including when this is a later RunManager.
                ValuablePresets.RegisterValuablePreset(preset);
            }
            if(!level.ValuablePresets.Contains(preset))level.ValuablePresets.Add(preset);
            Names[map]=name;
            Plugin.Log.LogInfo($"[Valuables] Dedicated spawn pool: {level.NarrativeName} ({level.name}) -> {name}");
        }
    }
    internal static List<string> For(CollectionEntry entry)
    {
        var names=Names.Where(p=>CollectionCatalog.Allows(entry,p.Key)).OrderBy(p=>p.Key).Select(p=>p.Value).ToList();
        if(names.Count==0||names.Any(n=>!ValuablePresets.AllValuablePresets.ContainsKey(n)))
            throw new InvalidOperationException("No validated location-specific pool for "+entry.Key+"; refusing generic fallback.");
        return names;
    }
}
