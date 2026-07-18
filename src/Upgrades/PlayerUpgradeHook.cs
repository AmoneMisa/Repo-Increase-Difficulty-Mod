using REPOLib.Modules;
using UnityEngine;
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
/// route to <see cref="OnUpgrade"/>. That increments the LOCAL player's level through
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
        // owning (local) player increments; REPOLib broadcasts the result to everyone else.
        var avatar = SemiFunc.PlayerAvatarGetFromPhotonID(_toggle.playerTogglePhotonID);
        if (avatar == null || !avatar.isLocal)
            return;

        if (RepoUpgrades.TryGetUpgrade(UpgradeId, out var upgrade))
            upgrade.AddLevel(avatar);
        else
            Plugin.Log.LogError($"[Upgrades] Hook fired for unknown upgrade id '{UpgradeId}'.");
    }
}
