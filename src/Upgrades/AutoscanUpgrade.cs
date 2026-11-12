using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using RepoUpgrades = REPOLib.Modules.Upgrades;

namespace IncreaseHardMod.Upgrades;

/// <summary>Periodically discovers nearby loot through the native, team-synchronized discovery API.</summary>
internal static class AutoscanUpgrade
{
    public const string Id = "IHM_Autoscan";
    private static readonly HashSet<ValuableDiscoverCustom> CustomTargets = new();

    public static void Register()
    {
        if (!ModConfig.PlayerUpgradesEnabled.Value) return;
        PlayerUpgradeRegistry.Define(new PlayerUpgradeRegistry.Definition
        {
            Id = Id, DisplayName = "Autoscan",
            MaxLevel = ModConfig.AutoscanMaxLevel.Value,
        });
    }

    [HarmonyPatch(typeof(ValuableDiscoverCustom), nameof(ValuableDiscoverCustom.Awake))]
    private static class TrackCustomTarget
    {
        [HarmonyPostfix]
        private static void Postfix(ValuableDiscoverCustom __instance) => CustomTargets.Add(__instance);
    }

    internal sealed class ScanController : MonoBehaviour
    {
        private float _timer;
        private ValuableDirector _director;

        private void Update()
        {
            // RunIsLevel and native Discover depend on singletons absent while loading.
            if (!ModConfig.PlayerUpgradesEnabled.Value || RunManager.instance == null || RunManager.instance.levelCurrent == null ||
                !SemiFunc.RunIsLevel() || LevelGenerator.Instance == null || !LevelGenerator.Instance.Generated ||
                ValuableDirector.instance == null || ValuableDiscover.instance == null || Map.Instance == null) return;
            var avatar = SemiFunc.PlayerAvatarLocal();
            if (avatar == null || avatar.isDisabled || string.IsNullOrEmpty(avatar.steamID)) return;
            // Skill tree: Autoscan, on the old ladder of AutoscanMaxLevel levels.
            int max = Mathf.Clamp(ModConfig.AutoscanMaxLevel.Value, 1, 20);
            float level = Mathf.Clamp(Skills.SkillEffects.Equivalent(avatar, Skills.SkillCatalog.Autoscan, max), 0f, max);
            if (level <= 0f) return;

            _timer -= Time.deltaTime;
            if (_timer > 0f) return;
            _timer = Mathf.Max(.1f, ModConfig.AutoscanInterval.Value);
            if (_director != ValuableDirector.instance)
            {
                _director = ValuableDirector.instance;
                // Include objects whose Awake happened before this controller/patch existed.
                CustomTargets.Clear();
                foreach (var target in Object.FindObjectsByType<ValuableDiscoverCustom>(FindObjectsInactive.Include, FindObjectsSortMode.None)) CustomTargets.Add(target);
            }
            Scan(avatar.transform.position, level * Mathf.Clamp(ModConfig.AutoscanRangePerLevel.Value, 1f, 30f) *
                Skills.TradeOffs.Factor(avatar, Skills.Fx.AutoscanReach)); // Scout
        }

        internal static void Scan(Vector3 center, float radius)
        {
            if (ValuableDirector.instance == null || ValuableDiscover.instance == null || Map.Instance == null || radius <= 0f) return;
            float radiusSquared = radius * radius;
            // The full registry handles disabled/trigger/sibling colliders and crowded rooms.
            foreach (var valuable in ValuableDirector.instance.valuableList)
            {
                if (valuable == null || !valuable.isActiveAndEnabled || valuable.discovered || !valuable.dollarValueSet ||
                    !InRange(valuable.physGrabObject, center, radiusSquared)) continue;
                valuable.Discover(ValuableDiscoverGraphic.State.Discover);
            }
            CustomTargets.RemoveWhere(target => target == null);
            foreach (var target in CustomTargets)
            {
                if (!target.isActiveAndEnabled || target.discovered || !InRange(target.physGrabObject, center, radiusSquared)) continue;
                target.Discover(); // Native custom sound/map rules and DiscoverRPC team sync.
            }
        }

        private static bool InRange(PhysGrabObject physical, Vector3 center, float radiusSquared) =>
            physical != null && physical.spawned && physical.isActiveAndEnabled &&
            (!GameManager.Multiplayer() || (physical.photonView != null && physical.photonView.ViewID != 0)) &&
            (physical.centerPoint - center).sqrMagnitude <= radiusSquared;
    }
}
