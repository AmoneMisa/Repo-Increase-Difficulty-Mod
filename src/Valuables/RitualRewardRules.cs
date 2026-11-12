using System;
using System.Collections.Generic;

namespace IncreaseHardMod.Valuables;

internal static class RitualRewardRules
{
    internal const int Value = 250000, Variants = 5;
    internal const string Quantum = "ritual_reward_quantum", Painting = "ritual_reward_painting", Sun = "ritual_reward_sun";
    internal static string Coffin(int variant) => "ritual_reward_coffin_" + Math.Clamp(variant, 0, Variants - 1);
    internal static string For(LootMap map, int variant) => map switch
    {
        LootMap.McJannek => Quantum,
        LootMap.Manor => Coffin(variant),
        LootMap.Museum => Painting,
        LootMap.Sunstone => Sun,
        _ => null,
    };
    internal static IEnumerable<string> Keys
    {
        get
        {
            yield return Quantum; yield return Painting; yield return Sun;
            for (int i = 0; i < Variants; i++) yield return Coffin(i);
        }
    }
    internal static bool IsReward(string key)
    {
        foreach (string reward in Keys) if (string.Equals(key, reward, StringComparison.Ordinal)) return true;
        return false;
    }
    internal static string Name(LootMap map, int variant, bool russian) => map switch
    {
        LootMap.McJannek => russian ? "Квантовый компьютер" : "Quantum Computer",
        LootMap.Museum => russian ? "Живая картина" : "Living Portrait",
        LootMap.Sunstone => russian ? "Вечное солнце" : "Eternal Sun",
        LootMap.Manor => (russian ? "Хрустальный гроб: " : "Crystal Coffin: ") + Character(variant, russian),
        _ => russian ? "Награда ритуала" : "Ritual Reward",
    };
    private static string Character(int variant, bool ru) => Math.Clamp(variant, 0, Variants - 1) switch
    {
        0 => ru ? "Охотник" : "The Hunter",
        1 => ru ? "Графиня" : "The Countess",
        2 => ru ? "Дворецкий" : "The Butler",
        3 => ru ? "Учёный" : "The Scholar",
        _ => ru ? "Следопыт" : "The Ranger",
    };
}
