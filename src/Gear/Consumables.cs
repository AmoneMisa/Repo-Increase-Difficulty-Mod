using HarmonyLib;
using Photon.Pun;
using UnityEngine;

namespace IncreaseHardMod.Gear;

/// <summary>Marks a cloned health pack as one of the mod's consumables (see <see cref="Consumables"/>).</summary>
internal sealed class ModConsumable : MonoBehaviour
{
    public ConsumableKind kind;
}

/// <summary>
/// Antidote, Adrenaline Shot and Repair Kit: clones of the small health pack. The pack's own switch, use and reject
/// RPCs, light and sound stay native; only what happens on use (host) is ours:
///  - Antidote: ends burning, bleeding, venom and slowness and blocks them for 20 s.
///  - Adrenaline Shot: breaks every hold (levitation, constriction, petrification, drag, the twins' grip),
///    then +30% speed and a full stamina bar for 8 s.
///  - Repair Kit: gives the most damaged valuable within 3 m back half of the value it lost; refused when none.
/// </summary>
internal static class Consumables
{
    /// <summary>Host: apply the consumable for <paramref name="user"/>. False = nothing to do, the pack is refused.</summary>
    private static bool Use(ConsumableKind kind, PlayerAvatar user)
    {
        switch (kind)
        {
            case ConsumableKind.Antidote:
                Monsters.MonsterAfflictions.Cure(user, ConsumableRules.AntidoteImmunitySeconds);
                return true;
            case ConsumableKind.Adrenaline:
                Monsters.MonsterAfflictions.BreakFree(user);
                Skills.BuffPerks.GrantAdrenalineShot(user, ConsumableRules.AdrenalineSeconds);
                return true;
            default:
                return Repair(user);
        }
    }

    private static bool Repair(PlayerAvatar user)
    {
        ValuableObject best = null;
        float bestLoss = 0f;
        foreach (var collider in Physics.OverlapSphere(user.transform.position + Vector3.up * .5f, ConsumableRules.RepairRange))
        {
            var valuable = collider.GetComponentInParent<ValuableObject>();
            if (valuable == null || Patches.Effects.BiggerSoulDropsEffect.IsEnemyValuable(valuable)) continue;
            float loss = valuable.dollarValueOriginal - valuable.dollarValueCurrent;
            if (loss > bestLoss + .5f) { best = valuable; bestLoss = loss; }
        }
        if (best == null) return false;
        best.dollarValueCurrent = ConsumableRules.Repaired(best.dollarValueOriginal, best.dollarValueCurrent);
        Networking.ModNetwork.Instance?.BroadcastRepair(best, best.dollarValueCurrent);
        Plugin.Log.LogInfo($"[Gear] Repair Kit: {best.name} restored by ${bestLoss * ConsumableRules.RepairShare:0} for {SemiFunc.PlayerGetName(user)}.");
        return true;
    }

    // Replaces the health pack's heal for our clones; everything else mirrors the native Update.
    [HarmonyPatch(typeof(ItemHealthPack), nameof(ItemHealthPack.Update))]
    private static class Use_
    {
        [HarmonyPrefix, HarmonyPriority(Priority.First)]
        private static bool Prefix(ItemHealthPack __instance)
        {
            var consumable = __instance.GetComponent<ModConsumable>();
            if (consumable == null) return true;
            if (SemiFunc.RunIsShop() || RunManager.instance.levelIsShop) return false;
            __instance.LightLogic();
            if (!SemiFunc.IsMasterClientOrSingleplayer() || __instance.itemToggle == null || !__instance.itemToggle.toggleState || __instance.used)
                return false;
            var user = SemiFunc.PlayerAvatarGetFromPhotonID(__instance.itemToggle.playerTogglePhotonID);
            if (user == null || user.isDisabled) return false;
            if (!Use(consumable.kind, user))
            {
                if (SemiFunc.IsMultiplayer()) __instance.photonView.RPC("RejectRPC", RpcTarget.All);
                else __instance.RejectRPC();
                __instance.itemToggle.ToggleItem(toggle: false);
                __instance.physGrabObject.rb.AddForce(Vector3.up * 2f, ForceMode.Impulse);
                return false;
            }
            StatsManager.instance.ItemRemove(__instance.itemAttributes.instanceName);
            if (SemiFunc.IsMultiplayer()) __instance.photonView.RPC("UsedRPC", RpcTarget.All);
            else __instance.UsedRPC();
            __instance.itemToggle.ToggleDisable(_disable: true);
            return false;
        }
    }
}
