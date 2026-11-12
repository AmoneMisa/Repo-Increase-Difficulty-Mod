using UnityEngine.Localization.Settings;

namespace IncreaseHardMod.Localization;

/// <summary>
/// The game's language, read from Unity Localization.
/// </summary>
internal static class GameLanguage
{
    internal static bool Russian
    {
        get
        {
            try
            {
                // Throws while Unity Localization is still initialising (or failed to); English until then.
                var code = LocalizationSettings.SelectedLocale?.Identifier.Code;
                return code != null && code.StartsWith("ru", System.StringComparison.OrdinalIgnoreCase);
            }
            catch { return false; }
        }
    }
}
