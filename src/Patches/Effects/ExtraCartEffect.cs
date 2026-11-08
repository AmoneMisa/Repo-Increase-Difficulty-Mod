using System.Collections.Generic;
using HarmonyLib;
using IncreaseHardMod.Modifiers;
using Photon.Pun;
using UnityEngine;

namespace IncreaseHardMod.Patches.Effects;

/// <summary>
/// ExtraSmallCart / ExtraBigCart modifiers — spawn a bonus hauling cart on the
/// special level so the squad has extra carrying capacity.
///
/// Carts are ordinary shop Items (itemType == cart); the game itself spawns
/// purchased carts by calling PhotonNetwork.InstantiateRoomObject on the Item's
/// prefab ResourcePath (see ItemManager.SpawnItem). We reuse that exact mechanism:
/// look the cart Item up in StatsManager.itemDictionary and room-instantiate it, so
/// the cart is a real, fully-networked object every client sees.
///
/// Timing: the level (and its LevelPoints) don't exist yet when modifiers are rolled
/// in RunManager.ChangeLevel — they only exist once generation finishes. The host's
/// LevelGenerator.GenerateDone RPC is that finish line (and is already master-only),
/// so we postfix it: level points are ready, and only the host spawns (room objects
/// must be created by the master client; InstantiateRoomObject requires it).
///
/// We drop the cart at a random LevelPoint in the start room (where the players begin
/// next to the truck) so it's immediately reachable and won't clip into level
/// geometry. Small vs big is picked by the cart Item's name; we log the cart names we
/// find so the exact asset strings can be verified/tuned in-game.
/// </summary>
[HarmonyPatch(typeof(LevelGenerator), "GenerateDone")]
internal static class ExtraCartEffect
{
    [HarmonyPostfix]
    private static void Postfix()
    {
        var mods = ModifierManager.Current;
        bool small = mods.Has(ModifierType.ExtraSmallCart);
        bool big = mods.Has(ModifierType.ExtraBigCart);
        if (!small && !big)
            return;

        // Room objects can only be instantiated by the master client.
        if (!SemiFunc.IsMasterClientOrSingleplayer())
            return;

        if (small)
            SpawnCart(wantBig: false);
        if (big)
            SpawnCart(wantBig: true);
    }

    private static void SpawnCart(bool wantBig)
    {
        Item cart = FindCartItem(wantBig);
        if (cart == null || cart.prefab == null)
        {
            Plugin.Log.LogWarning($"[ExtraCart] No cart item found (wantBig={wantBig}); nothing spawned.");
            return;
        }

        if (!TryGetSpawnPose(out Vector3 pos, out Quaternion rot))
        {
            Plugin.Log.LogWarning("[ExtraCart] No level point to spawn a cart on; nothing spawned.");
            return;
        }

        if (SemiFunc.IsMultiplayer())
            PhotonNetwork.InstantiateRoomObject(cart.prefab.ResourcePath, pos, rot, 0);
        else
            Object.Instantiate(cart.prefab.Prefab, pos, rot);

        Plugin.Log.LogInfo($"[ExtraCart] Spawned '{cart.name}' (wantBig={wantBig}) at {pos}.");
    }

    /// <summary>
    /// Pick a cart Item from the loaded item dictionary. Big carts prefer a name
    /// containing "large"/"big", small carts prefer "small"; both fall back to any
    /// other cart (e.g. the medium) so we always spawn SOMETHING if carts exist.
    /// </summary>
    private static Item FindCartItem(bool wantBig)
    {
        var stats = StatsManager.instance;
        if (stats == null)
            return null;

        var carts = new List<Item>();
        foreach (var item in stats.itemDictionary.Values)
        {
            if (item != null && item.itemType == SemiFunc.itemType.cart && !Gear.CustomCarts.IsModCart(item.name))
                carts.Add(item);
        }

        if (carts.Count == 0)
            return null;

        Plugin.Log.LogInfo("[ExtraCart] Available carts: " + string.Join(", ", carts.ConvertAll(c => c.name)));

        string[] preferred = wantBig
            ? new[] { "large", "big" }
            : new[] { "small" };

        foreach (var key in preferred)
        {
            var match = carts.Find(c => c.name.ToLowerInvariant().Contains(key));
            if (match != null)
                return match;
        }

        // Fallback: for big pick the last (biggest tends to sort later), for small the first.
        return wantBig ? carts[carts.Count - 1] : carts[0];
    }

    private static bool TryGetSpawnPose(out Vector3 pos, out Quaternion rot)
    {
        rot = Quaternion.identity;
        pos = Vector3.zero;

        var points = SemiFunc.LevelPointsGetInStartRoom();
        if (points == null || points.Count == 0)
            points = SemiFunc.LevelPointsGetAll();
        if (points == null || points.Count == 0)
            return false;

        var point = points[Random.Range(0, points.Count)];
        if (point == null)
            return false;

        // Lift slightly so the cart settles onto the floor instead of clipping it.
        pos = point.transform.position + Vector3.up;
        return true;
    }
}
