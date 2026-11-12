using System.Runtime.CompilerServices;
using HarmonyLib;
using UnityEngine;

namespace IncreaseHardMod.Gear;

/// <summary>Give the local driver a higher first-person view while pushing the flame cart by its handle.</summary>
[HarmonyPatch(typeof(CameraPosition), nameof(CameraPosition.Update))]
internal static class CartDrivingView
{
    private const float FlameLift = .6f, LiftSpeed = 2f;
    private const float CameraRadius = .12f, CeilingMargin = .04f;
    private static readonly int Obstructions = LayerMask.GetMask(
        "Default", "PhysGrabObject", "PhysGrabObjectCart", "PhysGrabObjectHinge", "StaticGrabObject");
    private static readonly ConditionalWeakTable<CameraPosition, ViewState> Views = new();

    private sealed class ViewState
    {
        internal float Lift;
        internal Vector3 OriginalLocalPosition, LiftedLocalPosition;
    }

    [HarmonyPrefix]
    private static void RestoreBasePosition(CameraPosition __instance)
    {
        if (!Views.TryGetValue(__instance, out var view) || view.Lift <= 0f) return;
        var camera = __instance.transform;
        // Keep the native smoothing independent of the lift. A teleport may have moved
        // the camera since our last frame, in which case the game owns the new position.
        if (camera.localPosition == view.LiftedLocalPosition)
            camera.localPosition = view.OriginalLocalPosition;
    }

    [HarmonyPostfix]
    private static void ApplyView(CameraPosition __instance)
    {
        var player = PlayerAvatar.instance;
        if (player == null || !player.isLocal || player.deadSet || player.isDisabled ||
            player.isTumbling || player.isCrawling || player.spectating ||
            __instance.overridePositionActive || __instance.overridePositionTimer > 0f ||
            GameDirector.instance == null || GameDirector.instance.currentState != GameDirector.gameState.Main ||
            SemiFunc.MenuLevel())
        {
            Views.Remove(__instance);
            return;
        }

        float target = HandleLift(player.physGrabber);
        if (player.isCrouching) target *= .6f;
        if (target <= 0f && !Views.TryGetValue(__instance, out _)) return;
        var view = Views.GetValue(__instance, _ => new ViewState());
        float lift = Mathf.MoveTowards(view.Lift, target, LiftSpeed * Time.deltaTime);
        var camera = __instance.transform;
        view.Lift = Clearance(camera.position, lift);
        if (view.Lift <= 0f)
        {
            if (target <= 0f) Views.Remove(__instance);
            return;
        }

        view.OriginalLocalPosition = camera.localPosition;
        camera.position += Vector3.up * view.Lift;
        view.LiftedLocalPosition = camera.localPosition;
    }

    private static float HandleLift(PhysGrabber grabber)
    {
        if (grabber == null || !grabber.grabbed || grabber.grabbedPhysGrabObject == null) return 0f;
        var held = grabber.grabbedPhysGrabObject;
        // Only the flame cart lifts the view; the Wide cart keeps the native camera height.
        if (held.TryGetComponent<FlameCartHandle>(out var flame) && flame.IsPushing(grabber)) return FlameLift;
        return 0f;
    }

    private static float Clearance(Vector3 origin, float lift)
    {
        if (lift <= 0f || Physics.CheckSphere(origin, CameraRadius, Obstructions, QueryTriggerInteraction.Ignore))
            return 0f;
        if (Physics.SphereCast(origin, CameraRadius, Vector3.up, out var hit, lift + CeilingMargin,
            Obstructions, QueryTriggerInteraction.Ignore))
            return Mathf.Min(lift, Mathf.Max(0f, hit.distance - CeilingMargin));
        return lift;
    }
}
