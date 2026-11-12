using HarmonyLib;
using UnityEngine;
using C = IncreaseHardMod.Skills.SkillCatalog;

namespace IncreaseHardMod.Skills;

/// <summary>
/// Wide View perk: widens the local player's camera by <see cref="PerkRules.FovBonus"/> degrees.
///
/// How the game zooms: <c>CameraZoom.Update</c> blends from <c>zoomPrev</c> to <c>zoomNew</c>; with no zoom
/// effect running, <c>zoomNew</c> is <c>playerZoomDefault</c> (the base field of view). Sprinting and tumbling add
/// on top, and the map, aiming and other effects call <c>OverrideZoomSet</c> with their own absolute values, then
/// fall back to <c>playerZoomDefault</c> when they end. So we only raise <c>playerZoomDefault</c> (and the resting
/// <c>zoomNew</c>): every native zoom keeps working, and the camera returns to the wider view after it.
/// Only the rig with a PlayerController is touched, and that rig exists only for the local player, so nobody
/// else's screen changes. The level is the synced tree level; the on/off switch is a local config value.
/// </summary>
[HarmonyPatch(typeof(CameraZoom), nameof(CameraZoom.Update))]
internal static class WideView
{
    private const float DegreesPerSecond = 30f; // switching on or off eases in over half a second

    private static CameraZoom _zoom;
    private static float _native, _applied;

    internal static bool Enabled => ModConfig.SkillWideViewOn == null || ModConfig.SkillWideViewOn.Value;

    internal static void Toggle()
    {
        if (ModConfig.SkillWideViewOn == null || !SkillEffects.LocalHas(C.WideView)) return;
        ModConfig.SkillWideViewOn.Value = !ModConfig.SkillWideViewOn.Value;
        Plugin.Log.LogInfo($"[Skills] Wide View {(ModConfig.SkillWideViewOn.Value ? "on" : "off")}.");
    }

    private static float TargetBonus() => Enabled ? PerkRules.FovBonus(SkillEffects.LocalLevel(C.WideView)) : 0f;

    // Harmony: a Prefix runs before the game's own Update, so the game's zoom math then uses our base value.
    private static void Prefix(CameraZoom __instance)
    {
        if (__instance.PlayerController == null) return; // menu and other cameras: not the player's view
        if (__instance != _zoom)
        {
            // A new camera rig (new level): remember its untouched base.
            _zoom = __instance;
            _native = __instance.playerZoomDefault;
            _applied = 0f;
        }

        float target = TargetBonus();
        if (_applied == target) return;
        float next = Mathf.MoveTowards(_applied, target, DegreesPerSecond * Time.deltaTime);
        float before = _native + _applied, after = _native + next;
        __instance.playerZoomDefault = after;
        // At rest (or blending back to rest) the game aims at the old default: move that target too.
        // While a zoom effect is active, zoomNew is its value and the new default is picked up when it ends.
        // zoomPrev too: before the first zoom effect of a level the blend never advances, so the view is zoomPrev.
        if (!__instance.OverrideActive)
        {
            if (Mathf.Abs(__instance.zoomNew - before) < .01f) __instance.zoomNew = after;
            if (Mathf.Abs(__instance.zoomPrev - before) < .01f) __instance.zoomPrev = after;
        }
        _applied = next;
    }
}
