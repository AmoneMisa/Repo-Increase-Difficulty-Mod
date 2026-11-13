using System.Collections.Generic;
using HarmonyLib;
using Photon.Pun;
using UnityEngine;

namespace IncreaseHardMod.Gear;

/// <summary>Use the Hauler's storage and extraction, with a soul-only intake.</summary>
internal sealed class SoulCartHatch : MonoBehaviour
{
    internal static void Attach(GameObject cart, ItemVehicle hauler)
    {
        // Seat the cassette inside the Pocket C.A.R.T. with both leaves above its rim.
        var box = FlameCartItem.AddStorage(cart, hauler, new Vector3(.022f, .052f, 0f), .96f, ModConfig.SoulCartValueCap.Value);
        box.gameObject.AddComponent<SoulCartHatch>();
        box.typeLimit = ValuableVolume.Type.Medium;
        box.boxChecker.checkForValuableTypes = false; // filter by soul component instead of generic loot size
        foreach (var scan in box.boxChecker.layerMasksToCheck)
            scan.checkInterval = Mathf.Min(scan.checkInterval, .08f);
    }

    internal static ValuableObject SoulValue(PhysGrabObject target)
    {
        if (target == null || target.dead || target.GetComponent<EnemyValuable>() == null) return null;
        var value = target.GetComponent<ValuableObject>();
        return value != null && value.dollarValueSet && value.dollarValueCurrent > 0f ? value : null;
    }

    [HarmonyPatch(typeof(ItemValuableBox), nameof(ItemValuableBox.OnObjectsFound))]
    private static class FilterIntake
    {
        private static bool Prefix(ItemValuableBox __instance, ref List<PhysGrabObject> foundObjects)
        {
            if (!__instance.TryGetComponent<SoulCartHatch>(out _)) return true;
            var souls = new List<PhysGrabObject>(foundObjects.Count);
            foreach (var target in foundObjects)
            {
                var value = SoulValue(target);
                if (value == null) continue;
                souls.Add(target);
                // Cover the short window between approaching the hatch and native absorption.
                if (__instance.currentState is ItemValuableBox.State.Idle or ItemValuableBox.State.Open or ItemValuableBox.State.Absorbing &&
                    __instance.CurrentValue + value.dollarValueCurrent <= __instance.valueLimit)
                    target.OverrideIndestructible(.4f);
            }
            foundObjects = souls;
            return true;
        }
    }

    [HarmonyPatch(typeof(ItemValuableBox), nameof(ItemValuableBox.StateOpen))]
    private static class DiscardStaleSouls
    {
        private static void Prefix(ItemValuableBox __instance, List<PhysGrabObject> ___validObjectsInBox)
        {
            if (__instance.TryGetComponent<SoulCartHatch>(out _)) ___validObjectsInBox.RemoveAll(target => SoulValue(target) == null);
        }
    }

    [HarmonyPatch(typeof(ItemValuableBox), nameof(ItemValuableBox.UpdateValue))]
    private static class PreserveValueForLateJoin
    {
        private static bool Prefix(ItemValuableBox __instance, float newValue, PhotonView ___photonView)
        {
            if (!__instance.TryGetComponent<SoulCartHatch>(out _) || !SemiFunc.IsMasterClient() ||
                ___photonView == null || ___photonView.ViewID == 0) return true;
            PhotonNetwork.RemoveBufferedRPCs(___photonView.ViewID, "UpdateValueRPC");
            ___photonView.RPC("UpdateValueRPC", RpcTarget.AllBuffered, newValue);
            return false;
        }
    }
}
