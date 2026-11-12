using System.Collections.Generic;
using BepInEx.Configuration;
using HarmonyLib;
using IncreaseHardMod.Gear;
using UnityEngine;

namespace IncreaseHardMod.Testing;

/// <summary>
/// TESTING ONLY - off by default, host-side. Shortcuts for checking shop items without playing levels:
///  * SkipLevelsToShop: leaving the truck goes straight to the shop instead of a level (shop -> truck -> shop ...).
///  * ShopCurrency: on entering the shop the run's money is raised to at least this much (in K, as the shop shows it).
///  * ForceFlameCart: the Flamethrower C.A.R.T. is always placed on a matching shop pad, regardless of price or roll.
///  * FullInvulnerability (per player, not host-side): your own character takes no damage and cannot die, from
///    monsters, pits, explosions or anything else; status effects and being grabbed or thrown still happen.
/// With any of these on, the flamethrower may also be fired inside the shop (no monsters there; visuals only).
/// </summary>
internal static class TestShortcuts
{
    internal static ConfigEntry<bool> SkipLevelsToShop, ForceFlameCart, ForceMysteryBoxes, FullInvulnerability;
    internal static ConfigEntry<int> ShopCurrency;

    internal static void Bind(ConfigFile cfg)
    {
        const string section = "Testing (do not use in real runs)";
        SkipLevelsToShop = cfg.Bind(section, "SkipLevelsToShop", false,
            "TESTING ONLY. Host: leaving the truck goes straight to the shop instead of a level, so shop items can be tried without playing.");
        ForceFlameCart = cfg.Bind(section, "ForceFlameCartInShop", false,
            "TESTING ONLY. Host: always put the Flamethrower C.A.R.T. in the shop (ignores price, rolls and purchase limits).");
        ForceMysteryBoxes = cfg.Bind(section, "ForceMysteryBoxes", false,
            "TESTING ONLY. Host: every run level gets the Mystery Boxes surprise, on top of whatever else rolled.");
        FullInvulnerability = cfg.Bind(section, "FullInvulnerability", false,
            "TESTING ONLY. Your own character takes no damage and cannot die (monsters, pits, explosions, everything). Works for each player on their own game; status effects, grabs and throws still happen.");
        ShopCurrency = cfg.Bind(section, "ShopCurrencyK", 0,
            new ConfigDescription("TESTING ONLY. Host: when the shop opens, raise the team's money to at least this many K ($1000s). 0 = off.",
                new AcceptableValueRange<int>(0, 100000)));
    }

    internal static bool Any => SkipLevelsToShop.Value || ForceFlameCart.Value || ForceMysteryBoxes.Value || ShopCurrency.Value > 0;

    private static bool Invulnerable(PlayerAvatar avatar) =>
        FullInvulnerability != null && FullInvulnerability.Value && avatar != null &&
        (!SemiFunc.IsMultiplayer() || (avatar.photonView != null && avatar.photonView.IsMine));

    // All player damage ends in PlayerHealth.Hurt on the player's own client (monster hits, pits, falls, our
    // afflictions); instant kills (explosions, arena) go through PlayerAvatar.PlayerDeath.
    [HarmonyPatch(typeof(PlayerHealth), nameof(PlayerHealth.Hurt))]
    private static class NoHurt
    {
        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        private static bool Prefix(PlayerHealth __instance) => !Invulnerable(__instance.GetComponent<PlayerAvatar>() ?? __instance.GetComponentInParent<PlayerAvatar>());
    }

    [HarmonyPatch(typeof(PlayerAvatar), nameof(PlayerAvatar.PlayerDeath))]
    private static class NoDeath
    {
        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        private static bool Prefix(PlayerAvatar __instance)
        {
            if (!Invulnerable(__instance)) return true;
            Plugin.Log.LogWarning("[Testing] FullInvulnerability: a death was blocked.");
            return false;
        }
    }

    [HarmonyPatch(typeof(RunManager), nameof(RunManager.ChangeLevel))]
    private static class SkipToShop
    {
        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        private static void Prefix(RunManager __instance, bool _levelFailed, ref RunManager.ChangeLevelType _changeLevelType)
        {
            if (!SkipLevelsToShop.Value || _levelFailed || __instance.restarting || !SemiFunc.IsMasterClientOrSingleplayer() ||
                _changeLevelType != RunManager.ChangeLevelType.Normal || __instance.levelCurrent != __instance.levelLobby) return;
            _changeLevelType = RunManager.ChangeLevelType.Shop;
            Plugin.Log.LogWarning("[Testing] SkipLevelsToShop: truck -> shop (no level played).");
        }
    }

    [HarmonyPatch(typeof(ShopManager), nameof(ShopManager.ShopInitialize), new[] { typeof(List<PunManager.ItemSpawn>) })]
    private static class Money
    {
        [HarmonyPrefix]
        private static void Prefix()
        {
            int wanted = ShopCurrency.Value;
            if (wanted <= 0 || !SemiFunc.RunIsShop() || !SemiFunc.IsMasterClientOrSingleplayer()) return;
            if (SemiFunc.StatGetRunCurrency() >= wanted) return;
            SemiFunc.StatSetRunCurrency(wanted);
            Plugin.Log.LogWarning($"[Testing] ShopCurrencyK: money set to {wanted}K.");
        }
    }

    [HarmonyPatch(typeof(PunManager), nameof(PunManager.ShopPopulateItemVolumes), new[] { typeof(List<PunManager.ItemSpawn>) })]
    private static class ForceFlame
    {
        [HarmonyPrefix]
        private static void Prefix(PunManager __instance, List<PunManager.ItemSpawn> __0)
        {
            if (!ForceFlameCart.Value || !SemiFunc.IsMasterClientOrSingleplayer() || ShopManager.instance == null ||
                StatsManager.instance == null || !StatsManager.instance.itemDictionary.TryGetValue(FlameCartItem.AssetName, out var item) || item == null)
                return;
            var pool = ShopManager.instance.potentialItems;
            pool.RemoveAll(i => i == item);
            // SpawnShopItem scans from the end: the first matching pad takes our item.
            pool.Add(item);
            // Visit a matching pad first, before the shop's item target is reached.
            var volumes = ShopManager.instance.itemVolumes;
            int pad = volumes.FindIndex(v => v != null && v.itemVolume == item.itemVolume);
            if (pad >= 0)
            {
                var volume = volumes[pad];
                volumes.RemoveAt(pad);
                volumes.Insert(0, volume);
                Plugin.Log.LogWarning("[Testing] ForceFlameCartInShop: Flamethrower C.A.R.T. placed on a shop pad.");
                return;
            }
            // No matching pad in this shop: spawn it on the first free pad instead (testing only, may overlap the shelf).
            pool.Remove(item);
            var spot = volumes.Find(v => v != null);
            if (spot == null) return;
            var spawn = new PunManager.ItemSpawn
            {
                prefab = item.prefab,
                position = spot.transform.position + Vector3.up * .6f,
                rotation = spot.transform.rotation,
            };
            if (__0 != null)
                __0.Add(spawn);
            else
                __instance.ItemSpawnApply(spawn);
            Plugin.Log.LogWarning($"[Testing] ForceFlameCartInShop: no '{item.itemVolume}' pad; spawned on another pad.");
        }
    }
}
