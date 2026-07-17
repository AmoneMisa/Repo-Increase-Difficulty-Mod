using UnityEngine;

namespace IncreaseHardMod.Patches.Effects;

/// <summary>
/// Minimal IMGUI shop for the cart/truck upgrades (Route A: no custom Unity assets).
/// The HOST opens it with CartUpgradeMenuKey while in the truck or shop and buys
/// levels with shared run currency; CartUpgradeState handles the economy, side effects
/// and syncing. Clients don't get the menu — purchasing is host-authoritative — they
/// just receive the resulting upgrade levels.
///
/// IMGUI (OnGUI) is used so the mod ships zero assets. While the menu is open we free
/// the cursor (REPO locks it during play) and restore the previous cursor state on
/// close so game input isn't disturbed.
/// </summary>
internal sealed class CartUpgradeMenu : MonoBehaviour
{
    private bool _open;
    private CursorLockMode _prevLock;
    private bool _prevVisible;
    private GUIStyle _title;
    private GUIStyle _info;

    private static bool HostInTruckOrShop()
        => SemiFunc.IsMasterClientOrSingleplayer()
           && (SemiFunc.RunIsLobby() || SemiFunc.RunIsShop());

    private void Update()
    {
        if (!ModConfig.CartUpgradesEnabled.Value)
            return;

        if (Input.GetKeyDown(ModConfig.CartUpgradeMenuKey.Value) && HostInTruckOrShop())
            SetOpen(!_open);

        // Auto-close if we leave the truck/shop (e.g. a level loads) so the freed
        // cursor doesn't linger into gameplay.
        if (_open && !HostInTruckOrShop())
            SetOpen(false);
    }

    private void SetOpen(bool open)
    {
        if (open == _open)
            return;
        _open = open;

        if (open)
        {
            _prevLock = Cursor.lockState;
            _prevVisible = Cursor.visible;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else
        {
            Cursor.lockState = _prevLock;
            Cursor.visible = _prevVisible;
        }
    }

    private void OnGUI()
    {
        if (!_open)
            return;

        _title ??= new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold };
        _info ??= new GUIStyle(GUI.skin.label) { fontSize = 13, wordWrap = true };

        const float w = 340f;
        const float h = 300f;
        var area = new Rect((Screen.width - w) / 2f, (Screen.height - h) / 2f, w, h);

        GUI.Box(area, GUIContent.none);
        GUILayout.BeginArea(new Rect(area.x + 14f, area.y + 12f, w - 28f, h - 24f));

        GUILayout.Label("Cart Upgrades", _title);
        GUILayout.Label($"Money: {SemiFunc.StatGetRunCurrency()}", _info);
        GUILayout.Space(6f);

        // Heal Power
        if (CartUpgradeState.HealMaxed)
            GUILayout.Label($"Heal Power  Lv.{CartUpgradeState.HealLevel} (MAX)", _info);
        else if (GUILayout.Button($"Heal Power  Lv.{CartUpgradeState.HealLevel}  \u2192  buy for {CartUpgradeState.HealNextCost}"))
            CartUpgradeState.TryBuyHeal();

        // Charge Efficiency
        if (CartUpgradeState.ChargeMaxed)
            GUILayout.Label($"Charge Efficiency  Lv.{CartUpgradeState.ChargeLevel} (MAX)", _info);
        else if (GUILayout.Button($"Charge Efficiency  Lv.{CartUpgradeState.ChargeLevel}  \u2192  buy for {CartUpgradeState.ChargeNextCost}"))
            CartUpgradeState.TryBuyCharge();

        // Loot Cash-In (one-time unlock, then a per-use sell action)
        if (!CartUpgradeState.SellUnlocked)
        {
            if (GUILayout.Button($"Unlock Loot Cash-In  \u2192  buy for {ModConfig.CartSellCost.Value}"))
                CartUpgradeState.TryBuySell();
        }
        else if (CartUpgradeState.CanSellNow())
        {
            if (GUILayout.Button("Cash in leftover loot (quotas closed)"))
                CartUpgradeState.SellRemainingLoot();
        }
        else
        {
            GUILayout.Label("Loot Cash-In: owned. Available once every quota is closed.", _info);
        }

        GUILayout.FlexibleSpace();
        GUILayout.Label($"Press [{ModConfig.CartUpgradeMenuKey.Value}] to close.", _info);

        GUILayout.EndArea();
    }
}
