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
[HarmonyPatch(typeof(ItemAttributes), "GetValue")]
internal static class ShopPriceScaleEffect
{
    [HarmonyPostfix]
    private static void Postfix(ItemAttributes __instance)
    {
        if (!ModConfig.ShopPriceScaleEnabled.Value)
            return;
        if (!__instance.shopItem)
            return;
        // GetValue's body only assigns value on the master client; skip on remote clients.
        if (GameManager.Multiplayer() && !PhotonNetwork.IsMasterClient)
            return;

        var gd = GameDirector.instance;
        if (gd == null || gd.PlayerList == null)
            return;

        int players = gd.PlayerList.Count;
        int baseline = ModConfig.ShopPriceBaselinePlayers.Value;
        int extra = players - baseline;
        if (extra <= 0)
            return;

        float factor = 1f + ModConfig.ShopPricePerExtraPlayer.Value * extra;
        if (factor <= 1f)
            return;

        __instance.value = Mathf.Max(1, Mathf.CeilToInt(__instance.value * factor));

        // The game already sent the un-scaled value to clients inside GetValue; correct it.
        if (GameManager.Multiplayer() && __instance.photonView != null && __instance.photonView.ViewID != 0)
            __instance.photonView.RPC("GetValueRPC", RpcTarget.Others, __instance.value);
    }
}
