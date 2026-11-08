using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;

namespace IncreaseHardMod.Patches.Effects;

/// <summary>
/// Ping helpers from the Recon page's Ping branch, drawn for the local player only, for every active ping (yours
/// and your team's):
///  * Range Finder: the distance from you under each ping on screen ("12 m"), and how high it sits off the floor
///    when it is on a shelf or ledge ("4.2 m · 1.8 m UP"). Not for monsters and players, whose centre is always
///    off the ground.
///  * Ping Compass: an arrow on the screen edge pointing to every ping you can't see (off screen or behind you).
///  * Pathfinder: a glowing dashed path on the floor along the walkable route from you to each ping (three newest).
/// Text uses the game's own HUD font and glow through <see cref="NativeHudPanel"/>, with no backplates.
/// </summary>
internal sealed class PingAssist : MonoBehaviour
{
    private const int MaxPaths = 3;
    private NativeHudPanel _panel;
    private readonly List<TextMeshProUGUI> _labels = new();
    private readonly List<RawImage> _arrows = new();
    private readonly List<TextMeshProUGUI> _arrowLabels = new();
    private readonly Dictionary<PingController.ActivePing, PathLine> _paths = new();
    private static Texture2D _chevron, _dash;
    private static Material _pathMaterial;

    private sealed class PathLine
    {
        public LineRenderer Line;
        public float NextRoute;
    }

    private void LateUpdate()
    {
        bool range = Skills.SkillEffects.LocalHas(Skills.SkillCatalog.PingRange);
        bool compass = Skills.SkillEffects.LocalHas(Skills.SkillCatalog.PingCompass);
        bool path = Skills.SkillEffects.LocalHas(Skills.SkillCatalog.PingPath);
        var camera = PingController.ActiveCamera();
        var pings = PingController.Active;
        bool any = camera != null && pings.Count > 0 && (range || compass || path) && HUDCanvas.instance != null;
        if (!any) { HideAll(); ClearPaths(null); return; }
        if (_panel == null && !BuildPanel()) return;
        _panel.gameObject.SetActive(true);

        var display = HudProjection.DisplayRect(camera);
        var me = camera.transform.position;
        int label = 0, arrow = 0;
        foreach (var ping in pings)
        {
            if (ping == null || Time.time >= ping.Expire) continue;
            float distance = Vector3.Distance(me, ping.Pos);
            string text = range ? DistanceText(ping, distance) : null;
            var screen = HudProjection.WorldToScreen(camera, ping.Pos);
            bool visible = screen.z > camera.nearClipPlane && display.Contains(screen);
            if (visible)
            {
                if (text != null) Place(Label(label++, _labels, 20f), screen + Vector3.down * 34f * Scale(), text, ping);
            }
            else if (compass)
                PlaceArrow(arrow++, camera, display, screen, ping, text);
        }
        for (int i = label; i < _labels.Count; i++) _labels[i].gameObject.SetActive(false);
        for (int i = arrow; i < _arrows.Count; i++) { _arrows[i].gameObject.SetActive(false); _arrowLabels[i].gameObject.SetActive(false); }
        if (path) UpdatePaths(pings); else ClearPaths(null);
    }

    private static float Scale() => Mathf.Clamp(Screen.height / 1080f, .8f, 2.5f) * ModConfig.PingMarkerScale.Value;

    // "12 m", "4.2 m", plus "· 1.8 m UP" when the ping sits clearly above the floor below it.
    private static string DistanceText(PingController.ActivePing ping, float distance)
    {
        bool ru = Localization.GameLanguage.Russian;
        string unit = ru ? "м" : "m";
        string text = distance < 10f ? distance.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " " + unit : Mathf.RoundToInt(distance) + " " + unit;
        if (ping.Type is PingType.Enemy or PingType.Player) return text;
        // The ray starts inside the pinged object, which Unity ignores, so it finds the surface it rests on.
        if (Physics.Raycast(ping.Pos + Vector3.up * .05f, Vector3.down, out var hit, 60f, SemiFunc.LayerMaskGetVisionObstruct(), QueryTriggerInteraction.Ignore))
        {
            float height = ping.Pos.y - hit.point.y;
            if (height >= .7f)
                text += "  ·  " + height.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " " + unit + (ru ? " ВЫШЕ ПОЛА" : " UP");
        }
        return text;
    }

    private bool BuildPanel()
    {
        _panel = NativeHudPanel.Create("IHM_PingAssist", new Vector2(1920f, 1080f), Vector2.one * .5f, Vector2.zero, 0f);
        if (_panel == null) return false;
        _panel.GetComponent<Canvas>().sortingOrder = 36;            // just above the ping markers
        _panel.Panel.anchorMin = Vector2.zero; _panel.Panel.anchorMax = Vector2.one;
        _panel.Panel.offsetMin = _panel.Panel.offsetMax = Vector2.zero;
        return true;
    }

    private TextMeshProUGUI Label(int index, List<TextMeshProUGUI> pool, float size)
    {
        while (pool.Count <= index) pool.Add(NewLabel(size));
        return pool[index];
    }

    private TextMeshProUGUI NewLabel(float size)
    {
        var text = _panel.Label("Ping distance", size, TextAlignmentOptions.Center);
        text.rectTransform.anchorMin = text.rectTransform.anchorMax = new Vector2(.5f, .5f);
        text.rectTransform.sizeDelta = new Vector2(320f, 34f);
        return text;
    }

    private void Place(TextMeshProUGUI text, Vector3 screen, string value, PingController.ActivePing ping)
    {
        text.gameObject.SetActive(true);
        if (text.text != value) text.text = value;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(_panel.Panel, screen, null, out var local);
        text.rectTransform.anchoredPosition = local;
        var tint = ping.Graphic != null ? ping.Graphic.color : Color.white;
        text.color = Color.Lerp(Color.white, tint, .35f);
        text.alpha = Mathf.Clamp01((ping.Expire - Time.time) / .35f);
    }

    // An arrow on the screen edge in the ping's direction (mirrored when it is behind you).
    private void PlaceArrow(int index, Camera camera, Rect display, Vector3 screen, PingController.ActivePing ping, string text)
    {
        while (_arrows.Count <= index)
        {
            var go = new GameObject("Ping compass arrow", typeof(RectTransform), typeof(CanvasRenderer));
            go.transform.SetParent(_panel.Panel, false);
            var image = go.AddComponent<RawImage>();
            image.texture = Chevron();
            image.raycastTarget = false;
            image.rectTransform.anchorMin = image.rectTransform.anchorMax = new Vector2(.5f, .5f);
            _arrows.Add(image);
            _arrowLabels.Add(NewLabel(18f));
        }
        var arrow = _arrows[index];
        var centre = display.center;
        var dir = (Vector2)screen - centre;
        if (screen.z < 0f) dir = -dir;                                 // behind the camera the projection flips
        if (dir.sqrMagnitude < 1f) dir = Vector2.down;
        dir.Normalize();
        float margin = 60f * Scale();
        var half = new Vector2(display.width * .5f - margin, display.height * .5f - margin);
        float t = Mathf.Min(Mathf.Abs(half.x / Mathf.Max(.0001f, Mathf.Abs(dir.x))), Mathf.Abs(half.y / Mathf.Max(.0001f, Mathf.Abs(dir.y))));
        var at = centre + dir * t;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(_panel.Panel, at, null, out var local);
        arrow.gameObject.SetActive(true);
        arrow.rectTransform.anchoredPosition = local;
        arrow.rectTransform.sizeDelta = Vector2.one * 44f;
        arrow.rectTransform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f);
        var tint = ping.Graphic != null ? ping.Graphic.color : Color.white;
        float fade = Mathf.Clamp01((ping.Expire - Time.time) / .35f);
        float pulse = .8f + .2f * Mathf.Sin(Time.time * 6f);
        arrow.color = new Color(tint.r, tint.g, tint.b, fade * pulse);
        var caption = _arrowLabels[index];
        if (text == null) { caption.gameObject.SetActive(false); return; }
        Place(caption, at - dir * 40f * Scale(), text, ping);
    }

    private static Texture2D Chevron()
    {
        if (_chevron != null) return _chevron;
        const int n = 64;
        _chevron = new Texture2D(n, n, TextureFormat.RGBA32, false) { name = "IHM Ping Chevron", wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
        var pixels = new Color[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float u = (x + .5f) / n * 2f - 1f, v = (y + .5f) / n * 2f - 1f;
                // A chevron pointing up (+v): two slanted bars meeting at the tip, with a soft edge.
                float bar = Mathf.Abs(Mathf.Abs(u) * 1.1f + v - .35f);
                float alpha = Mathf.Clamp01((.22f - bar) / .06f) * (v > -.75f ? 1f : 0f) * (Mathf.Abs(u) < .85f ? 1f : 0f);
                pixels[y * n + x] = new Color(1f, 1f, 1f, alpha);
            }
        _chevron.SetPixels(pixels);
        _chevron.Apply(false, true);
        return _chevron;
    }

    // ---------------------------------------------------------------- Pathfinder
    private void UpdatePaths(IReadOnlyList<PingController.ActivePing> pings)
    {
        var keep = new List<PingController.ActivePing>();
        for (int i = pings.Count - 1; i >= 0 && keep.Count < MaxPaths; i--)
            if (pings[i] != null && Time.time < pings[i].Expire) keep.Add(pings[i]);
        ClearPaths(keep);
        var me = SemiFunc.PlayerAvatarLocal();
        if (me == null || me.isDisabled) { ClearPaths(new List<PingController.ActivePing>()); return; }
        foreach (var ping in keep)
        {
            if (!_paths.TryGetValue(ping, out var line)) _paths[ping] = line = new PathLine { Line = NewLine() };
            var tint = ping.Graphic != null ? ping.Graphic.color : Color.white;
            float fade = Mathf.Clamp01((ping.Expire - Time.time) / .5f);
            line.Line.startColor = line.Line.endColor = new Color(tint.r, tint.g, tint.b, .85f * fade);
            if (Time.time >= line.NextRoute) { line.NextRoute = Time.time + .4f; Route(line.Line, me.transform.position, ping.Pos); }
        }
        if (_pathMaterial != null) _pathMaterial.mainTextureOffset = new Vector2(-Time.time * 1.6f, 0f);   // dashes flow toward the ping
    }

    private static void Route(LineRenderer line, Vector3 from, Vector3 to)
    {
        var path = new NavMeshPath();
        if (!NavMesh.SamplePosition(from, out var a, 2.5f, NavMesh.AllAreas) || !NavMesh.SamplePosition(to, out var b, 4f, NavMesh.AllAreas) ||
            !NavMesh.CalculatePath(a.position, b.position, NavMesh.AllAreas, path) || path.corners.Length < 2)
        {
            line.positionCount = 0;
            return;
        }
        var points = new List<Vector3>();
        for (int i = 0; i < path.corners.Length; i++)
        {
            if (i == 0) { points.Add(path.corners[0] + Vector3.up * .12f); continue; }
            // Subdivide long legs so the line follows slopes and stairs instead of cutting through them.
            var p0 = path.corners[i - 1]; var p1 = path.corners[i];
            int steps = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(p0, p1) / 1.5f));
            for (int s = 1; s <= steps; s++)
            {
                var p = Vector3.Lerp(p0, p1, s / (float)steps);
                if (NavMesh.SamplePosition(p, out var snap, 1f, NavMesh.AllAreas)) p = snap.position;
                points.Add(p + Vector3.up * .12f);
            }
        }
        line.positionCount = points.Count;
        line.SetPositions(points.ToArray());
    }

    private LineRenderer NewLine()
    {
        var go = new GameObject("IHM Ping Path");
        var line = go.AddComponent<LineRenderer>();
        if (_pathMaterial == null)
        {
            _pathMaterial = new Material(Shader.Find("Sprites/Default")) { name = "IHM Ping Path", mainTexture = Dash(), hideFlags = HideFlags.HideAndDontSave };
            _pathMaterial.mainTexture.wrapMode = TextureWrapMode.Repeat;
        }
        line.sharedMaterial = _pathMaterial;
        line.textureMode = LineTextureMode.Tile;
        line.widthMultiplier = .09f;
        line.numCornerVertices = 2;
        line.alignment = LineAlignment.View;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.positionCount = 0;
        return line;
    }

    private static Texture2D Dash()
    {
        if (_dash != null) return _dash;
        const int w = 64, h = 16;
        _dash = new Texture2D(w, h, TextureFormat.RGBA32, false) { name = "IHM Ping Dash", wrapMode = TextureWrapMode.Repeat, hideFlags = HideFlags.HideAndDontSave };
        var pixels = new Color[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float along = x < w * .6f ? 1f : 0f;                        // dash, then a gap
                float across = 1f - Mathf.Abs((y + .5f) / h * 2f - 1f);    // soft round edge
                pixels[y * w + x] = new Color(1f, 1f, 1f, along * Mathf.SmoothStep(0f, 1f, across * 1.6f));
            }
        _dash.SetPixels(pixels);
        _dash.Apply(false, true);
        return _dash;
    }

    private void ClearPaths(List<PingController.ActivePing> keep)
    {
        if (_paths.Count == 0) return;
        var gone = new List<PingController.ActivePing>();
        foreach (var pair in _paths) if (keep == null || !keep.Contains(pair.Key)) gone.Add(pair.Key);
        foreach (var ping in gone)
        {
            if (_paths[ping].Line != null) Destroy(_paths[ping].Line.gameObject);
            _paths.Remove(ping);
        }
    }

    private void HideAll()
    {
        if (_panel != null) _panel.gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        ClearPaths(null);
        if (_panel != null) Destroy(_panel.gameObject);
    }
}
