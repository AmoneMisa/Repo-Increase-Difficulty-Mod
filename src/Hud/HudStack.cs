using System.Collections.Generic;
using IncreaseHardMod.Patches.Effects;
using UnityEngine;

namespace IncreaseHardMod.Hud;

/// <summary>
/// Places toolkit panels in a corner so they stack instead of overlapping: call <see cref="Place"/> every frame
/// the panel is visible (after setting its size) and <see cref="Release"/> when it hides. Lower order sits
/// closer to the corner. Panels drawn on the native HUD canvas (the modifier list) ask
/// <see cref="LowestScreenEdge"/> where the top-right stack ends and start below it.
/// Orders in use: 100 Shopping List.
/// </summary>
internal static class HudStack
{
    // Start = distance from the corner edge, in the overlay canvases' 1920x1080 reference pixels.
    private static readonly Dictionary<HudCorner, HudStackLayout> Layouts = new()
    {
        [HudCorner.TopRight] = new HudStackLayout(100f, HudStyle.Gap),   // below the native currency / quota line
        [HudCorner.TopLeft] = new HudStackLayout(200f, HudStyle.Gap),    // below health and energy
        [HudCorner.UpperCentre] = new HudStackLayout(270f, 8f),          // status messages (frozen, netted)
        [HudCorner.BottomCentre] = new HudStackLayout(180f, 6f),         // toasts, above the inventory bar
    };

    private static readonly Vector3[] Corners = new Vector3[4];

    internal static HudStackLayout Layout(HudCorner corner) => Layouts[corner];

    internal static void Place(NativeHudPanel surface, HudCorner corner, string id, int order)
    {
        if (surface == null || surface.Panel == null) return;
        var layout = Layouts[corner];
        var panel = surface.Panel;
        int frame = Time.frameCount;
        float offset = layout.OffsetFor(id, order, frame);
        Vector2 anchor = corner switch
        {
            HudCorner.TopRight => new Vector2(1f, 1f),
            HudCorner.TopLeft => new Vector2(0f, 1f),
            HudCorner.UpperCentre => new Vector2(.5f, 1f),
            _ => new Vector2(.5f, 0f),
        };
        panel.anchorMin = panel.anchorMax = panel.pivot = anchor;
        panel.anchoredPosition = corner switch
        {
            HudCorner.TopRight => new Vector2(-HudStyle.Margin, -offset),
            HudCorner.TopLeft => new Vector2(HudStyle.Margin, -offset),
            HudCorner.UpperCentre => new Vector2(0f, -offset),
            _ => new Vector2(0f, offset),
        };
        surface.Fit();
        // The overlay canvas's world space is screen pixels; record the far edge for HUD on other canvases.
        panel.GetWorldCorners(Corners);
        bool top = corner != HudCorner.BottomCentre;
        float edge = top ? Mathf.Min(Corners[0].y, Corners[3].y) : Mathf.Max(Corners[1].y, Corners[2].y);
        layout.Claim(id, order, panel.sizeDelta.y * panel.localScale.y, frame, edge);
    }

    internal static void Release(string id)
    {
        foreach (var layout in Layouts.Values) layout.Release(id);
    }

    /// <summary>For top corners: the lowest screen y (pixels from the bottom) the stack reaches, or -1 if empty.</summary>
    internal static float LowestScreenEdge(HudCorner corner) => Layouts[corner].LowestScreenEdge(Time.frameCount);
}
