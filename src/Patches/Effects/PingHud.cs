using UnityEngine;

namespace IncreaseHardMod.Patches.Effects;

/// <summary>
/// Reads the ping key, advances ping lifetimes, and draws the world-anchored ping markers.
/// Each active ping (from <see cref="PingController"/>) is projected from its world position
/// to screen space and drawn as a coloured dot + label. Object pings dim when occluded by
/// geometry; Location pings are always shown (through walls). Pings are cleared on level
/// change so stale markers never carry over.
/// </summary>
internal sealed class PingHud : MonoBehaviour
{
    private static Texture2D _dot;
    private static GUIStyle _label;

    private Level _lastLevel;

    private void Update()
    {
        if (!ModConfig.PingEnabled.Value)
            return;

        if (SafeMenuLevel())
            return;

        // Drop all pings when the level changes.
        var run = RunManager.instance;
        if (run != null && run.levelCurrent != _lastLevel)
        {
            _lastLevel = run.levelCurrent;
            PingController.Clear();
        }

        PingController.Tick();

        if (Input.GetKeyDown(ModConfig.PingKey.Value))
            PingController.PingFromAim();
    }

    private void OnGUI()
    {
        if (!ModConfig.PingEnabled.Value || PingController.Active.Count == 0)
            return;

        var cam = (PhysGrabber.instance != null ? PhysGrabber.instance.playerCamera : null) ?? Camera.main;
        if (cam == null)
            return;

        EnsureResources();

        foreach (var ping in PingController.Active)
        {
            Vector3 sp = cam.WorldToScreenPoint(ping.Pos);
            if (sp.z <= 0f)
                continue; // behind the camera

            float x = sp.x;
            float y = Screen.height - sp.y;

            // Fade out over the last second of life.
            float remaining = ping.Expire - Time.time;
            float alpha = Mathf.Clamp01(remaining);

            // Object pings dim when something blocks line of sight; Location pings ignore walls.
            if (ping.Type != PingType.Location && IsOccluded(cam.transform.position, ping.Pos))
                alpha *= 0.35f;

            var color = PingController.ColorFor(ping.Type);
            color.a = alpha;

            DrawDot(x, y, color);

            string text = $"{PingController.GlyphFor(ping.Type)} {ping.Label}";
            if (!string.IsNullOrEmpty(ping.Sender))
                text += $"  ({ping.Sender})";
            DrawLabel(x + 12f, y - 10f, text, color);
        }

        GUI.color = Color.white;
    }

    private static bool IsOccluded(Vector3 from, Vector3 to)
    {
        float d = Vector3.Distance(from, to);
        if (d <= 0.6f)
            return false;
        // Stop short of the target so we don't hit the pinged object's own collider.
        return Physics.Raycast(from, (to - from).normalized, d - 0.5f, ~0, QueryTriggerInteraction.Ignore);
    }

    private static void DrawDot(float cx, float cy, Color color)
    {
        const float s = 8f;
        GUI.color = color;
        GUI.DrawTexture(new Rect(cx - s * 0.5f, cy - s * 0.5f, s, s), _dot);
    }

    private static void DrawLabel(float x, float y, string text, Color color)
    {
        var rect = new Rect(x, y, 320f, 22f);

        // Shadow for readability against bright backgrounds.
        GUI.color = new Color(0f, 0f, 0f, color.a * 0.9f);
        GUI.Label(new Rect(rect.x + 1f, rect.y + 1f, rect.width, rect.height), text, _label);

        GUI.color = color;
        GUI.Label(rect, text, _label);
    }

    private static void EnsureResources()
    {
        if (_dot == null)
        {
            _dot = new Texture2D(1, 1);
            _dot.SetPixel(0, 0, Color.white);
            _dot.Apply();
        }
        _label ??= new GUIStyle
        {
            fontSize = 13,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleLeft,
            richText = false,
        };
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
