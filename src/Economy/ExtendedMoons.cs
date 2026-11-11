using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using HarmonyLib;
using UnityEngine;

namespace IncreaseHardMod.Economy;

internal static class ExtendedMoons
{
    internal static int Current => RunManager.instance != null ? RunManager.instance.moonLevel : 0;
    internal static float Cooldown(float seconds) => seconds * MoonRules.CooldownFactor(Current);
    internal static bool SelfStunning;
    internal static void SelfStun(EnemyStateStunned state, float seconds)
    {
        bool previous = SelfStunning;
        try { SelfStunning = true; state.Set(seconds); }
        finally { SelfStunning = previous; }
    }
    private sealed class DeathRoll { internal bool Drops; }
    private sealed class Guard { internal float Ready; }
    private static readonly ConditionalWeakTable<EnemyHealth, DeathRoll> Deaths = new();
    private static readonly ConditionalWeakTable<EnemyHealth, Guard> Guards = new();

    internal static bool AllowsSoul(EnemyHealth health)
    {
        if (health == null || !health.spawnValuable) return false;
        if (Current < MoonRules.RandomSouls) return health.spawnValuableCurrent < health.spawnValuableMax;
        return Deaths.GetValue(health, _ => new DeathRoll { Drops = MoonRules.SoulDrops(Current, UnityEngine.Random.value) }).Drops;
    }

    [HarmonyPatch(typeof(EnemyHealth), "OnSpawn")]
    private static class NewLife
    {
        [HarmonyPostfix] private static void Postfix(EnemyHealth __instance) => Deaths.Remove(__instance);
    }

    [HarmonyPatch(typeof(EnemyParent), nameof(EnemyParent.Despawn))]
    private static class SoulDrop
    {
        [HarmonyPrefix]
        private static void Prefix(EnemyParent __instance, out (EnemyHealth Health, bool Enabled, int Max) __state)
        {
            __state = default;
            if (!SemiFunc.IsMasterClientOrSingleplayer() || Current < MoonRules.RandomSouls ||
                __instance.Enemy == null || !__instance.Enemy.HasHealth) return;
            var health = __instance.Enemy.Health;
            if (health.healthCurrent > 0) return;
            __state = (health, health.spawnValuable, health.spawnValuableMax);
            health.spawnValuable &= AllowsSoul(health);
            // Replace the first-three-deaths quota with an independent roll each life.
            health.spawnValuableMax = Math.Max(health.spawnValuableMax, health.spawnValuableCurrent + 1);
        }
        [HarmonyFinalizer]
        private static void Restore((EnemyHealth Health, bool Enabled, int Max) __state)
        {
            if (__state.Health == null) return;
            __state.Health.spawnValuable = __state.Enabled;
            __state.Health.spawnValuableMax = __state.Max;
        }
    }

    [HarmonyPatch(typeof(EnemyHealth), nameof(EnemyHealth.Hurt))]
    private static class Resistance
    {
        [HarmonyPrefix, HarmonyPriority(Priority.Last)]
        private static void Prefix(EnemyHealth __instance, ref int _damage)
        {
            if (!SemiFunc.IsMasterClientOrSingleplayer() || Current < MoonRules.First || _damage <= 0 ||
                __instance.dead || __instance.healthCurrent <= 0) return;
            var guard = Guards.GetValue(__instance, _ => new Guard());
            bool proc = MoonRules.GuardProcs(Current, Time.time, guard.Ready, UnityEngine.Random.value);
            if (proc) guard.Ready = Time.time + MoonRules.GuardCooldown;
            _damage = MoonRules.Damage(_damage, Current, proc);
        }
    }

    [HarmonyPatch(typeof(EnemyStateStunned), nameof(EnemyStateStunned.Set))]
    private static class Stun
    {
        [HarmonyPrefix]
        private static bool Prefix(EnemyStateStunned __instance, ref float time)
        {
            if (!SemiFunc.IsMasterClientOrSingleplayer() || Current < MoonRules.Resistant || time <= 0f) return true;
            // Native grab maintenance repeatedly calls Set(.1): resisting that would break
            // already-held bodies. Self-inflicted recovery is also not a player stun.
            if (time <= .1f) return true;
            time *= MoonRules.StunFactor(Current);
            if (SelfStunning) return true;
            if (time <= __instance.stunTimer) return true;
            return UnityEngine.Random.value >= MoonRules.StunResistChance;
        }
    }

    // Keep the native moon screen, TMP styles and layouts. Only add data entries.
    [HarmonyPatch(typeof(RunManager), nameof(RunManager.CalculateMoonLevel))]
    private static class Entries
    {
        [HarmonyPrefix]
        private static void Prefix(RunManager __instance)
        {
            if (__instance.moons.Count != 4) return;
            for (int phase = MoonRules.First; phase <= MoonRules.Resistant; phase++)
            {
                var moon = ScriptableObject.CreateInstance<Moon>();
                moon.name = "IHM Moon " + phase;
                moon.moonName = "MOON " + phase;
                moon.moonIcon = __instance.moons[3].moonIcon;
                moon.moonAttributes = new List<Moon.MoonAttribute>();
                __instance.moons.Add(moon);
            }
        }
    }

    [HarmonyPatch(typeof(RunManager), nameof(RunManager.MoonGetName))]
    private static class Name
    {
        [HarmonyPostfix]
        private static void Postfix(int _moonIndex, ref string __result)
        {
            if (_moonIndex < 5 || _moonIndex > 7) return;
            __result = Localization.GameLanguage.Russian ? "ЛУНА " + _moonIndex : "MOON " + _moonIndex;
        }
    }

    [HarmonyPatch(typeof(RunManager), nameof(RunManager.MoonGetAttributes))]
    private static class Description
    {
        [HarmonyPostfix]
        private static void Postfix(int _moonIndex, ref List<Moon.MoonAttribute> __result)
        {
            if (_moonIndex < 5 || _moonIndex > 7) return;
            bool ru = Localization.GameLanguage.Russian;
            var lines = new List<string>
            {
                ru ? $"Монстры получают на {(_moonIndex >= 7 ? 10 : 5)}% меньше урона" : $"Monsters take {(_moonIndex >= 7 ? 10 : 5)}% less damage",
                ru ? "Души: урон игрокам и вещам ×2; монстрам −50%" : "Souls: players and items ×2 damage; monsters −50%",
                ru ? $"Перезарядка атак −{(_moonIndex >= 6 ? 20 : 10)}%" : $"Attack cooldown −{(_moonIndex >= 6 ? 20 : 10)}%"
            };
            if (_moonIndex >= 6) lines.Add(ru ? "Душа: шанс 50%; подъём монстров: 3–7 силы" : "Soul drop: 50%; monster lifting: 3–7 Strength");
            if (_moonIndex >= 7)
            {
                lines.Add(ru ? "15% шанс снизить урон попадания на 33%; раз в 75 с" : "15% chance of −33% hit damage; once per 75s");
                lines.Add(ru ? "Стан короче на 40%; шанс сопротивления 10%" : "Stuns 40% shorter; 10% stun resistance");
            }
            __result = lines.ConvertAll(text => new Moon.MoonAttribute { text = text });
        }
    }
}
