using HarmonyLib;
using Photon.Pun;
using UnityEngine;

namespace IncreaseHardMod.Patches.Effects;

/// <summary>
/// Shop price scaling for large lobbies. More players means more combined income, so we
/// raise shop prices with the player count to keep purchasing meaningful.
///
/// ItemAttributes.GetValue() is the host-only choke point that rolls each shop item's
/// price into <c>value</c> and broadcasts it to clients via GetValueRPC. Because the
/// game's own RPC has already fired by the time our postfix runs, we re-broadcast the
/// scaled value so every client agrees. We only touch shop items (shopItem == true), and
/// GetValue only executes on the master client, so this stays host-authoritative.
///
/// The multiplier is 1 + PricePerExtraPlayer * (players - BaselinePlayers), clamped so it
/// never drops below vanilla for small lobbies.
/// </summary>
[HarmonyPatch(typeof(ItemAttributes), nameof(ItemAttributes.GetValue))]
internal static class ShopPriceScaleEffect
{
    [HarmonyPostfix]
    private static void Postfix(ItemAttributes __instance)
    {
        if (!__instance.shopItem)
            return;
        // GetValue's body only assigns value on the master client; skip on remote clients.
        if (GameManager.Multiplayer() && !PhotonNetwork.IsMasterClient)
            return;

        float factor = 1f;
        if (ModConfig.ShopPriceScaleEnabled.Value)
        {
            var gd = GameDirector.instance;
            if (gd != null && gd.PlayerList != null)
            {
                int extra = gd.PlayerList.Count - ModConfig.ShopPriceBaselinePlayers.Value;
                if (extra > 0)
                    factor += ModConfig.ShopPricePerExtraPlayer.Value * extra;
            }
        }

        // The shop economy event: upgrades, health packs and crystals are scaled where the game prices them
        // (Economy.ShopEconomy); every other item goes through here, including this mod's drones and gear.
        var type = __instance.item != null ? __instance.item.itemType : SemiFunc.itemType.gun;
        if (type != SemiFunc.itemType.item_upgrade && type != SemiFunc.itemType.player_upgrade &&
            type != SemiFunc.itemType.healthPack && type != SemiFunc.itemType.power_crystal)
            factor *= Economy.ShopEconomy.ItemMult;

        if (Mathf.Approximately(factor, 1f))
            return;

        __instance.value = Mathf.Max(1, Mathf.CeilToInt(__instance.value * factor));

        // The game already sent the un-scaled value to clients inside GetValue; correct it.
        if (GameManager.Multiplayer() && __instance.photonView != null && __instance.photonView.ViewID != 0)
            __instance.photonView.RPC("GetValueRPC", RpcTarget.Others, __instance.value);
    }
}
