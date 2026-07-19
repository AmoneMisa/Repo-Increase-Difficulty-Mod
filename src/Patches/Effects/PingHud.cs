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

        var cam = PingController.ActiveCamera();
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

            // Only mark pings whose anchor is actually ON-SCREEN. Otherwise a ping you've turned
            // away from (or one at the screen edge) projects to the border and smears a big bracket
            // into a corner instead of sitting on the object. The highlight stays strictly on the
            // pinged object while it's in view, and shows nothing once it leaves the frame.
            if (x < 0f || x > Screen.width || y < 0f || y > Screen.height)
                continue;

            // Fade out over the last second of life.
            float remaining = ping.Expire - Time.time;
            float alpha = Mathf.Clamp01(remaining);

            // Object pings dim when something blocks line of sight; Location pings ignore walls.
            if (ping.Type != PingType.Location && IsOccluded(cam.transform.position, ping.Pos))
                alpha *= 0.35f;

            var color = PingController.ColorFor(ping.Type);
            color.a = alpha;

            // Size the highlight by distance so nearer targets get a slightly bigger frame
            // (sp.z = camera depth). Kept small so it snugly frames the object rather than
            // covering a big chunk of the screen.
            float half = Mathf.Clamp(Screen.height * 0.25f / Mathf.Max(1f, sp.z), 12f, 60f);
            DrawHighlightBox(x, y, half, color);

            string text = $"{PingController.GlyphFor(ping.Type)} {ping.Label}";
            if (!string.IsNullOrEmpty(ping.Sender))
                text += $"  ({ping.Sender})";
            DrawLabel(x + half + 6f, y - 10f, text, color);
        }

        GUI.color = Color.white;
    }

    // A coloured highlight like the game's own marker: a faint translucent fill plus four
    // L-shaped corner brackets. Centred on (cx, cy) with the given half-size in pixels.
    private static void DrawHighlightBox(float cx, float cy, float half, Color color)
    {
        float size = half * 2f;
        var box = new Rect(cx - half, cy - half, size, size);

        // Translucent fill (the "cube" glow).
        var fill = color;
        fill.a *= 0.14f;
        GUI.color = fill;
        GUI.DrawTexture(box, _dot);

        // Corner brackets.
        float len = Mathf.Clamp(half * 0.4f, 6f, 26f);
        float t = 2f;
        GUI.color = color;

        // Top-left
        Line(box.xMin, box.yMin, len, t);            // horizontal
        Line(box.xMin, box.yMin, t, len);            // vertical
        // Top-right
        Line(box.xMax - len, box.yMin, len, t);
        Line(box.xMax - t, box.yMin, t, len);
        // Bottom-left
        Line(box.xMin, box.yMax - t, len, t);
        Line(box.xMin, box.yMax - len, t, len);
        // Bottom-right
        Line(box.xMax - len, box.yMax - t, len, t);
        Line(box.xMax - t, box.yMax - len, t, len);
    }

    private static void Line(float x, float y, float w, float h)
    {
        GUI.DrawTexture(new Rect(x, y, w, h), _dot);
    }

    private static bool IsOccluded(Vector3 from, Vector3 to)
    {
        float d = Vector3.Distance(from, to);
        if (d <= 0.6f)
            return false;
        // Stop short of the target so we don't hit the pinged object's own collider.
        return Physics.Raycast(from, (to - from).normalized, d - 0.5f, ~0, QueryTriggerInteraction.Ignore);
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
