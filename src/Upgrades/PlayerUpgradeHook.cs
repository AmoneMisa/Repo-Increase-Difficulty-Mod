using REPOLib.Modules;
using UnityEngine;
using HarmonyLib;
using RepoUpgrades = REPOLib.Modules.Upgrades;

namespace IncreaseHardMod.Upgrades;

/// <summary>
/// Small self-wiring component we bolt onto every runtime-cloned upgrade prefab.
///
/// Why it exists: the vanilla shop <c>Instantiate()</c>s the upgrade prefab for each
/// spawned copy. UnityEvent listeners added at runtime (via AddListener) are NOT
/// serialized, so they do NOT survive that Instantiate — only persistent (editor-baked)
/// listeners do. Since we build these prefabs in code, we have no editor-baked listener,
/// so we re-wire <see cref="ItemUpgrade.upgradeEvent"/> in Start() on every instance.
///
/// When the player activates the upgrade item, ItemUpgrade fires upgradeEvent, which we
/// route to <see cref="OnUpgrade"/>. The host increments the activating player's level through
/// REPOLib's PlayerUpgrade (which then saves + broadcasts the new level to other clients).
/// </summary>
internal sealed class PlayerUpgradeHook : MonoBehaviour
{
    // Set by PlayerUpgradeRegistry when the prefab is built. Matches the REPOLib UpgradeId.
    public string UpgradeId;

    private ItemToggle _toggle;
    private bool _wired;

    private void Start()
    {
        _toggle = GetComponent<ItemToggle>();

        var itemUpgrade = GetComponent<ItemUpgrade>();
        if (itemUpgrade != null && itemUpgrade.upgradeEvent != null && !_wired)
        {
            itemUpgrade.upgradeEvent.AddListener(OnUpgrade);
            _wired = true;
        }
    }

    private void OnUpgrade()
    {
        // ItemToggle.playerTogglePhotonID identifies who activated the item. Only the
        // host validates/increments for the activating player; REPOLib syncs all peers.
        var avatar = SemiFunc.PlayerAvatarGetFromPhotonID(_toggle.playerTogglePhotonID);
        if (avatar == null || !SemiFunc.IsMasterClientOrSingleplayer())
            return;

        if (RepoUpgrades.TryGetUpgrade(UpgradeId, out var upgrade))
        {
            var definition = PlayerUpgradeRegistry.FindDefinition(UpgradeId);
            if (definition == null) return;
            int current = TruckUpgrades.IsShared(UpgradeId) ? TruckUpgrades.TeamLevel(UpgradeId) : upgrade.GetLevel(avatar);
            if (current >= definition.MaxLevel) return;
            int next = Mathf.Min(definition.MaxLevel, current + 1);
            upgrade.SetLevel(avatar, next);
            if (TruckUpgrades.IsShared(UpgradeId)) TruckUpgrades.StoreShared(UpgradeId, next);
        }
        else
            Plugin.Log.LogError($"[Upgrades] Hook fired for unknown upgrade id '{UpgradeId}'.");
    }

    [HarmonyPatch(typeof(ItemUpgrade), "PlayerUpgrade")]
    private static class PreventOverCap
    {
        private static float _nextHint;
        [HarmonyPrefix]
        private static bool Prefix(ItemUpgrade __instance)
        {
            var hook = __instance.GetComponent<PlayerUpgradeHook>();
            if (hook == null) return true;
            var toggle = __instance.GetComponent<ItemToggle>();
            if (toggle == null || !toggle.toggleState) return true;
            var avatar = SemiFunc.PlayerAvatarGetFromPhotonID(toggle.playerTogglePhotonID);
            var definition = PlayerUpgradeRegistry.FindDefinition(hook.UpgradeId);
            var upgrade = RepoUpgrades.GetUpgrade(hook.UpgradeId);
            if (avatar == null || definition == null || upgrade == null) return false;
            int current = TruckUpgrades.IsShared(hook.UpgradeId) ? TruckUpgrades.TeamLevel(hook.UpgradeId) : upgrade.GetLevel(avatar);
            if (current < definition.MaxLevel) return true;
            if (avatar.isLocal && MissionUI.instance != null && Time.unscaledTime >= _nextHint)
            {
                _nextHint = Time.unscaledTime + 1f;
                SemiFunc.UIFocusText("MAX LEVEL: " + definition.DisplayName, Color.white, Color.white, 1f);
            }
            return false; // Before the native method hides/destroys the card or decrements purchases.
        }
    }
}
