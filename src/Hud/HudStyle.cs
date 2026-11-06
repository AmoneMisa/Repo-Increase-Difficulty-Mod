using UnityEngine;

namespace IncreaseHardMod.Hud;

/// <summary>
/// The toolkit's shared look: text sizes in the native HUD's hierarchy (heavier headings, lighter body and
/// values, see AGENTS.md) and the status colours already used across the mod's HUD. Text itself always uses the
/// game's own TextMeshPro font and material through <see cref="Patches.Effects.NativeHudPanel"/>; nothing here
/// picks a font.
/// </summary>
internal static class HudStyle
{
    // Sizes in the 1920x1080 reference pixels of the toolkit's overlay canvases.
    internal const float Heading = 26f;
    internal const float Body = 22f;
    internal const float Small = 18f;
    internal const float Status = 26f;
    internal const float Toast = 22f;

    // Screen margin and spacing between stacked blocks.
    internal const float Margin = 24f;
    internal const float Gap = 12f;

    internal static readonly Color Title = new(1f, .85f, .4f);     // panel headings (gold)
    internal static readonly Color Text = Color.white;
    internal static readonly Color Good = new(.5f, 1f, .5f);        // affordable, done
    internal static readonly Color Bad = new(1f, .45f, .45f);       // over budget, failed, warnings
    internal static readonly Color Info = new(.5f, .9f, 1f);        // neutral notices
    internal static readonly Color Ice = new(.45f, .8f, 1f);        // frozen
    internal static readonly Color Caution = new(1f, .8f, .3f);     // netted, held back
    internal static readonly Color Armor = new(.32f, .56f, 1f);     // armor row value and icon
    internal static readonly Color ArmorMax = new(.26f, .44f, .95f);

    /// <summary>"#RRGGBB" for TextMeshPro rich-text colour tags.</summary>
    internal static string Hex(Color color) => "#" + ColorUtility.ToHtmlStringRGB(color);
}
