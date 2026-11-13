using System.Runtime.CompilerServices;
using HarmonyLib;
using TMPro;
using UnityEngine;

namespace IncreaseHardMod.Patches.Effects;

/// <summary>
/// Compact native level label (shown in the map / Tab and at level start). Natively it is three stacked lines: a
/// "LEVEL" heading, a very large number under it, and the moon icon above the moon name. That tall block left the
/// modifier descriptions under it almost no room. Here it becomes two small lines, right-aligned to where the heading
/// was:  "LEVEL 16"  and  [moon icon] "HALF MOON".
///
/// Only sizes and positions change. The game's own TextMeshPro components, fonts, materials, colours and glow stay
/// as they are, so the HUD text style is kept. LevelUI (a SemiUI) slides its root in and out every frame, but places
/// the number and moon children only once, so they are laid out here after every LevelUI.Update, relative to the
/// heading. Text sizes are taken from the game's own values each time (never compounded).
/// </summary>
[HarmonyPatch(typeof(LevelUI), nameof(LevelUI.Update))]
internal static class LevelUICompact
{
    private const float HeadingScale = .62f, NumberScale = 1.1f, MoonScale = .62f;

    private sealed class Original { public float Heading, Number, Moon; public Vector4 Margin; }
    private static readonly ConditionalWeakTable<LevelUI, Original> Originals = new();
    private static readonly Vector3[] Corners = new Vector3[4];

    [HarmonyPostfix]
    private static void Postfix(LevelUI __instance)
    {
        try { Layout(__instance); }
        catch (System.Exception e) { Plugin.Log.LogWarning($"[LevelInfo] Compact level label failed harmlessly: {e.Message}"); }
    }

    // LevelUI works out its moon with its own CalculateMoonLevel call. That one-line method gets inlined there, so
    // the ScaleMoon patch is skipped and the label showed the native moon (e.g. moon 5 at level 26 with 7 levels per
    // phase) while the Esc menu, the moon screen and every mechanic used the run's moon (3). Show the run's moon,
    // which on guests is also the host's. Checked every frame because a guest's value can arrive after the fetch.
    private static void SyncMoon(LevelUI ui)
    {
        var run = RunManager.instance;
        if (run == null || ui.fetch || ui.objectMoon == null || ui.textMoon == null) return;
        int moon = Mathf.Clamp(run.moonLevel, 0, run.moons.Count);
        if (ui.moonLevel == moon) return;
        ui.moonLevel = moon;
        if (moon > 0)
        {
            ui.objectMoon.SetActive(true);
            ui.textMoon.text = run.MoonGetName(moon);
            if (ui.iconMoon != null) ui.iconMoon.texture = run.MoonGetIcon(moon);
        }
        else
        {
            if (ui.transformOffset != null) ui.transformOffset.localPosition = new Vector3(0f, -50f, 0f);
            ui.objectMoon.SetActive(false);
        }
    }

    private static void Layout(LevelUI ui)
    {
        SyncMoon(ui);
        var heading = ui.uiText;
        var number = ui.textNumber;
        if (heading == null || number == null) return;
        var o = Originals.GetValue(ui, _ => new Original
        {
            Heading = heading.fontSize, Number = number.fontSize,
            Moon = ui.textMoon != null ? ui.textMoon.fontSize : heading.fontSize, Margin = heading.margin,
        });

        // Row 1: "LEVEL" and the number side by side, right-aligned.
        float size = o.Heading * HeadingScale;
        Text(heading, size, TextAlignmentOptions.MidlineRight);
        Text(number, size * NumberScale, TextAlignmentOptions.MidlineRight);
        var numberSize = number.GetPreferredValues(number.text);
        float headingUnit = Unit(heading.rectTransform), numberUnit = Unit(number.rectTransform);
        float gap = size * .3f;
        // The heading keeps its own rect (the game animates it); a right margin makes room for the number inside it.
        heading.margin = new Vector4(o.Margin.x, o.Margin.y, o.Margin.z + numberSize.x * numberUnit / headingUnit + gap, o.Margin.w);

        var rt = heading.rectTransform;
        rt.GetWorldCorners(Corners); // 0 bottom-left, 1 top-left, 2 top-right, 3 bottom-right
        var rightMid = (Corners[2] + Corners[3]) * .5f - rt.right * (o.Margin.z * headingUnit);
        Place(number.rectTransform, rightMid, numberSize);

        // Row 2: moon icon then moon name, right-aligned under row 1.
        if (ui.objectMoon == null || !ui.objectMoon.activeSelf || ui.textMoon == null) return;
        float moon = size * MoonScale;
        Text(ui.textMoon, moon, TextAlignmentOptions.MidlineRight);
        var nameSize = ui.textMoon.GetPreferredValues(ui.textMoon.text);
        var rowMid = rightMid - rt.up * ((size * .62f + moon * .75f) * headingUnit);
        Place(ui.textMoon.rectTransform, rowMid, nameSize);
        if (ui.iconMoon != null)
        {
            float icon = moon * 1.35f;
            var iconAt = rowMid - rt.right * ((nameSize.x + moon * .3f) * Unit(ui.textMoon.rectTransform));
            Place(ui.iconMoon.rectTransform, iconAt, new Vector2(icon, icon) * (headingUnit / Unit(ui.iconMoon.rectTransform)));
        }
    }

    private static void Text(TextMeshProUGUI text, float size, TextAlignmentOptions alignment)
    {
        text.enableAutoSizing = false;
        text.fontSize = size;
        text.alignment = alignment;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Overflow;
    }

    // Puts a rect's right-middle point on a world position, sized in its own local units.
    private static void Place(RectTransform rect, Vector3 rightMid, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
        rect.pivot = new Vector2(1f, .5f);
        rect.sizeDelta = size;
        rect.position = rightMid;
    }

    private static float Unit(RectTransform rect)
    {
        float s = rect.lossyScale.x;
        return Mathf.Abs(s) > 1e-6f ? s : 1f;
    }
}
