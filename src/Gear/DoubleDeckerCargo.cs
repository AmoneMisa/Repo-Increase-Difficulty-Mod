using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace IncreaseHardMod.Gear;

/// <summary>Apply the native cargo rules before a double-decker load takes impact damage.</summary>
internal sealed class DoubleDeckerCargo : MonoBehaviour
{
    private static readonly List<DoubleDeckerCargo> Active = new();
    private PhysGrabCart _cart;
    private BoxCollider _cargo;

    private void Awake()
    {
        _cart = GetComponent<PhysGrabCart>();
        _cargo = transform.Find("In Cart")?.GetComponent<BoxCollider>();
    }

    private void OnEnable()
    {
        if (_cart != null && _cargo != null) Active.Add(this);
    }

    private void OnDisable() => Active.Remove(this);

    [HarmonyPatch(typeof(PhysGrabObjectImpactDetector), nameof(PhysGrabObjectImpactDetector.OnCollisionStay))]
    private static class RegisterLoadBeforeImpact
    {
        [HarmonyPrefix]
        private static void Prefix(PhysGrabObjectImpactDetector __instance)
        {
            if (Active.Count == 0 || __instance.inCart || __instance.isCart || __instance.isEnemy ||
                !SemiFunc.IsMasterClientOrSingleplayer()) return;
            var body = __instance.physGrabObject;
            if (body == null || body.dead || body.isPlayer || body.GetComponent<FlameCartHandle>() != null) return;

            var center = __instance.transform.TransformPoint(__instance.centerPoint);
            foreach (var load in Active)
            {
                if (load._cart.physGrabInCart == null || !load._cargo.enabled ||
                    (load._cargo.ClosestPoint(center) - center).sqrMagnitude > .0009f) continue;

                // Trigger-stay can lag the first collision or stop while a load sleeps.
                // Refresh the same native state in time for this impact's break check.
                __instance.currentCartPrev = __instance.currentCart;
                __instance.currentCart = load._cart;
                load._cart.physGrabInCart.Add(body);
                __instance.timerInCart = .1f;
                __instance.inCart = true;
                return;
            }
        }
    }

    [HarmonyPatch(typeof(PhysGrabObjectImpactDetector), nameof(PhysGrabObjectImpactDetector.OnTriggerStay))]
    private static class KeepCartsIndependent
    {
        [HarmonyPrefix]
        private static bool Prefix(PhysGrabObjectImpactDetector __instance, Collider other)
        {
            if (!other.CompareTag("Cart")) return true;
            if (!__instance.isCart && __instance.GetComponent<FlameCartHandle>() == null) return true;
            // A real cart must never become another cart's cargo. Safe-area triggers
            // outside carts still run the game's normal protection.
            return other.GetComponentInParent<PhysGrabCart>() == null;
        }
    }
}
