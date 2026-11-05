using System.Collections.Generic;
using HarmonyLib;
using TMPro;
using UnityEngine;

namespace IncreaseHardMod.Patches.Effects;

/// <summary>
/// Keeps the native "UPGRADES" list (StatsUI) on screen however many upgrades a heavily modded run has:
/// after the game rebuilds the list, measure it against the space down to the bottom of the HUD and, only
/// if it would overflow, shrink the names and their level numbers together by one uniform factor. Native
/// font, material/glow and line spacing are kept; nothing is stretched; short lists stay full size.
/// </summary>
[HarmonyPatch(typeof(StatsUI), nameof(StatsUI.Fetch))]
internal static class UpgradesListScale
{
    private sealed class Native { public float Names, Numbers, Scale = 1f; public string Text; public int Screen; }
    private static readonly Dictionary<StatsUI, Native> Sizes = new();
    private static readonly Vector3[] Corners = new Vector3[4];
    private const float MinScale = .42f;

    [HarmonyPostfix]
    private static void Postfix(StatsUI __instance)
    {
        if (!ModConfig.UpgradesListScaleEnabled.Value) return;
        var names = __instance.Text;
        var numbers = __instance.textNumbers;
        if (names == null || numbers == null || HUDCanvas.instance == null) return;

        if (!Sizes.TryGetValue(__instance, out var native))
            Sizes[__instance] = native = new Native { Names = names.fontSize, Numbers = numbers.fontSize };
        if (string.IsNullOrEmpty(names.text)) { Apply(names, numbers, native, 1f); return; }
        // Fetch runs every frame while the list shows: only re-measure when the list or the screen changed.
        int screen = Screen.width * 10000 + Screen.height;
        if (native.Text == names.text && native.Screen == screen) { Apply(names, numbers, native, native.Scale); return; }
        native.Text = names.text;
        native.Screen = screen;

        // Height the list needs at native size vs. the room from its top edge to the bottom of the HUD.
        var canvas = HUDCanvas.instance.rect;
        names.rectTransform.GetWorldCorners(Corners);
        float top = canvas.InverseTransformPoint(Corners[1]).y;
        float bottom = canvas.rect.yMin + canvas.rect.height * .05f;
        float available = top - bottom;
        float lossy = names.rectTransform.lossyScale.y / Mathf.Max(1e-5f, canvas.lossyScale.y);
        float needed = names.GetPreferredValues(names.text, names.rectTransform.rect.width, Mathf.Infinity).y
                       * (native.Names / Mathf.Max(1e-3f, names.fontSize)) * lossy;
        float scale = available > 0f && needed > available ? Mathf.Max(MinScale, available / needed) : 1f;
        native.Scale = scale;
        Apply(names, numbers, native, scale);
    }

    private static void Apply(TextMeshProUGUI names, TextMeshProUGUI numbers, Native native, float scale)
    {
        float n = native.Names * scale, m = native.Numbers * scale;
        if (!Mathf.Approximately(names.fontSize, n)) names.fontSize = n;
        if (!Mathf.Approximately(numbers.fontSize, m)) numbers.fontSize = m;
    }
}
