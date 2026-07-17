using UnityEngine;

namespace IncreaseHardMod.Patches.Effects;

/// <summary>
/// Minimal on-screen countdown for the LevelTimer modifier. Uses IMGUI (OnGUI) on
/// purpose: it needs no font asset, prefab, or canvas, so it works from a pure
/// code-only BepInEx mod without shipping any Unity assets. It only draws; all timer
/// state and authority live in LevelTimerController.
/// </summary>
internal sealed class LevelTimerHud : MonoBehaviour
{
    private GUIStyle _style;

    private void OnGUI()
    {
        if (!LevelTimerController.Running)
            return;

        float remaining = Mathf.Max(0f, LevelTimerController.Remaining);
        int minutes = (int)(remaining / 60f);
        int seconds = (int)(remaining % 60f);
        string label = $"\u23F1 {minutes:0}:{seconds:00}";

        if (_style == null)
        {
            _style = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.UpperCenter,
                fontSize = 34,
                fontStyle = FontStyle.Bold,
            };
        }

        // Turn red in the final 30 seconds as a warning.
        _style.normal.textColor = remaining <= 30f ? new Color(1f, 0.3f, 0.3f) : Color.white;

        float width = 260f;
        var rect = new Rect((Screen.width - width) / 2f, 18f, width, 60f);

        // Cheap drop shadow for legibility over bright scenes.
        var shadow = rect;
        shadow.x += 2f;
        shadow.y += 2f;
        var prev = _style.normal.textColor;
        _style.normal.textColor = new Color(0f, 0f, 0f, 0.6f);
        GUI.Label(shadow, label, _style);
        _style.normal.textColor = prev;

        GUI.Label(rect, label, _style);
    }
}
