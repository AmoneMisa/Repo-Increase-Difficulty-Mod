using UnityEngine;
using RepoUpgrades = REPOLib.Modules.Upgrades;

namespace IncreaseHardMod.Upgrades;

/// <summary>
/// "Autoscan" — a per-player upgrade that passively discovers valuables around you. Each
/// level widens the scan radius. Discovering a valuable reveals its value and puts it on
/// the map, exactly like the handheld tracker does.
///
/// Like HP regen, this is a continuous effect, so it runs from a ticking controller rather
/// than REPOLib start/upgrade actions. The REPOLib upgrade is registered only for shop
/// presence + per-player level tracking; the controller reads the LOCAL player's level.
///
/// Discovery is inherently team-wide (ValuableObject.Discover RPCs to everyone and adds to
/// the shared map), so a scanning player benefits the whole squad — acceptable and matches
/// how the vanilla tracker already works.
/// </summary>
internal static class AutoscanUpgrade
{
    public const string Id = "IHM_Autoscan";

    public static void Register()
    {
        if (!ModConfig.PlayerUpgradesEnabled.Value)
            return;

        PlayerUpgradeRegistry.Define(new PlayerUpgradeRegistry.Definition
        {
            Id = Id,
            DisplayName = "Autoscan",
            Description = "Automatically discovers nearby valuables. Higher levels scan farther.",
            MaxLevel = ModConfig.AutoscanMaxLevel.Value,
            BaseShopPrice = ModConfig.AutoscanBasePrice.Value,
            IconFile = "Upgrade_Autoscan.png",
        });
    }

    /// <summary>Runs the local player's periodic scan. Added to the mod's runtime object.</summary>
    internal sealed class ScanController : MonoBehaviour
    {
        private float _timer;
        // Reused to avoid per-scan allocation of the overlap buffer's managed wrapper.
        private static readonly Collider[] _hits = new Collider[128];

        private void Update()
        {
            if (!ModConfig.PlayerUpgradesEnabled.Value || !SemiFunc.RunIsLevel())
                return;

            _timer -= Time.deltaTime;
            if (_timer > 0f)
                return;
            _timer = Mathf.Max(0.1f, ModConfig.AutoscanInterval.Value);

            var avatar = SemiFunc.PlayerAvatarLocal();
            if (avatar == null || avatar.isDisabled)
                return;

            var upgrade = RepoUpgrades.GetUpgrade(Id);
            int level = upgrade != null ? upgrade.GetLevel(avatar.steamID) : 0;
            if (level <= 0)
                return;

            float radius = level * ModConfig.AutoscanRangePerLevel.Value;
            Scan(avatar.transform.position, radius);
        }

        private static void Scan(Vector3 center, float radius)
        {
            int count = Physics.OverlapSphereNonAlloc(
                center, radius, _hits, SemiFunc.LayerMaskGetPhysGrabObject());

            for (int i = 0; i < count; i++)
            {
                var valuable = _hits[i].GetComponentInParent<ValuableObject>();
                if (valuable != null && !valuable.discovered)
                    valuable.Discover(ValuableDiscoverGraphic.State.Discover);
            }
        }
    }
}
