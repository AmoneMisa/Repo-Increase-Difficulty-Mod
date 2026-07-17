using UnityEngine;

namespace IncreaseHardMod.Patches.Effects;

/// <summary>
/// Reads the ping key and draws active pings as world-anchored labels. Asset-free IMGUI:
/// each ping is projected to screen space with the local aim camera every frame, so the
/// marker sticks to its world position as you move. All shared state lives in
/// <see cref="PingController"/>.
/// </summary>
internal sealed class PingHud : MonoBehaviour
{
    private GUIStyle _style;

    private void Update()
    {
        if (!ModConfig.PingEnabled.Value)
            return;

        PingController.Prune();

        // Don't ping from menus, and only when actually controlling a character.
        if (SafeMenuLevel())
            return;

        if (Input.GetKeyDown(ModConfig.PingKey.Value))
        {
            Plugin.Log.LogInfo($"[Ping] key '{ModConfig.PingKey.Value}' pressed — firing ping.");
            PingController.PingFromAim();
        }
    }

    private void OnGUI()
    {
        if (!ModConfig.PingEnabled.Value)
            return;

        var pings = PingController.Active;
        if (pings.Count == 0)
            return;

        var cam = (PhysGrabber.instance != null ? PhysGrabber.instance.playerCamera : null) ?? Camera.main;
        if (cam == null)
            return;

        if (_style == null)
        {
            _style = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 15,
                fontStyle = FontStyle.Bold,
            };
        }

        for (int i = 0; i < pings.Count; i++)
        {
            var ping = pings[i];
            Vector3 sp = cam.WorldToScreenPoint(ping.Position);
            if (sp.z <= 0f)
                continue; // behind the camera

            float x = sp.x;
            float y = Screen.height - sp.y; // GUI y is top-down

            (string glyph, Color color) = Visual(ping.Type);
            string text = $"{glyph} {ping.Label}";
            if (!string.IsNullOrEmpty(ping.Sender))
                text += $"\n({ping.Sender})";

            var size = _style.CalcSize(new GUIContent(text));
            float w = Mathf.Max(size.x + 12f, 60f);
            float h = size.y + 6f;
            var rect = new Rect(x - w / 2f, y - h / 2f, w, h);

            // Shadow for legibility.
            _style.normal.textColor = new Color(0f, 0f, 0f, 0.7f);
            GUI.Label(new Rect(rect.x + 2f, rect.y + 2f, rect.width, rect.height), text, _style);

            _style.normal.textColor = color;
            GUI.Label(rect, text, _style);
        }
    }

    private static (string, Color) Visual(PingType type)
    {
        switch (type)
        {
            case PingType.Valuable: return ("$", new Color(1f, 0.85f, 0.25f));
            case PingType.Enemy: return ("\u26A0", new Color(1f, 0.35f, 0.35f));
            case PingType.Item: return ("\u25C6", new Color(0.4f, 0.9f, 1f));
            default: return ("\u2316", Color.white);
        }
    }

    private static bool SafeMenuLevel()
    {
        try
        {
            return SemiFunc.MenuLevel();
        }
        catch
        {
            return false;
        }
    }
}
