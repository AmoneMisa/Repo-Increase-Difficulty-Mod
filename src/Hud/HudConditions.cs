using System.Collections.Generic;
using IncreaseHardMod.Patches.Effects;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IncreaseHardMod.Hud;

/// <summary>A status the local player can be under; each has its own screen frame and card.</summary>
internal enum PlayerCondition { Frozen, Poisoned, Sedated, Netted }

/// <summary>
/// The local player's conditions on screen: a themed frame around the screen edges (frost, toxic haze, rope
/// net, sleepy haze) and a status card at the left (icon, name, time bar, what it does), stacked through
/// <see cref="HudStack"/> so several conditions never overlap. Local only: call it from the affected player's
/// own client.
/// <code>
/// HudConditions.Refresh(PlayerCondition.Frozen, remaining);   // every frame while it lasts, or
/// HudConditions.Start(PlayerCondition.Poisoned, 4f);          // once, for a fixed time
/// </code>
/// </summary>
internal static class HudConditions
{
    private sealed class Look
    {
        public string Title;
        public string[] Lines;
        public Color Color;
    }

    private static readonly Dictionary<PlayerCondition, Look> Looks = new()
    {
        [PlayerCondition.Frozen] = new Look { Title = "FROZEN", Color = new Color(.55f, .85f, 1f), Lines = new[] { "MOVEMENT FROZEN", "THAWING OUT" } },
        [PlayerCondition.Poisoned] = new Look { Title = "POISONED", Color = new Color(.66f, 1f, .16f), Lines = new[] { "TOXIC BOLT HIT" } },
        [PlayerCondition.Sedated] = new Look { Title = "SEDATED", Color = new Color(.7f, .62f, 1f), Lines = new[] { "KNOCKED OUT", "WAKING UP SOON" } },
        [PlayerCondition.Netted] = new Look { Title = "NETTED", Color = new Color(1f, .6f, .2f), Lines = new[] { "CAN'T WALK OR JUMP", "STRUGGLE TO BREAK FREE" } },
    };

    private sealed class View
    {
        public readonly HudConditionTimer Timer = new();
        public NativeHudPanel Frame, Card;
        public RawImage FrameImage;
        public Image Icon, Accent, Backplate;
        public TextMeshProUGUI Title, Seconds;
        public readonly List<TextMeshProUGUI> Lines = new();
        public readonly List<Image> Segments = new();
        public readonly List<(TextMeshProUGUI label, Vector2 home, float phase)> Zs = new();
        public int ShownTenths = -1;
    }

    private static readonly Dictionary<PlayerCondition, View> Views = new();
    private const int SegmentCount = 10;
    private const float CardWidth = 380f;

    private static View For(PlayerCondition condition)
    {
        if (!Views.TryGetValue(condition, out var view)) Views[condition] = view = new View();
        return view;
    }

    private static bool Enabled => ModConfig.ScreenEffectsEnabled.Value;

    /// <summary>The condition has <paramref name="remaining"/> seconds left; call every frame while it lasts.</summary>
    internal static void Refresh(PlayerCondition condition, float remaining)
    {
        if (Enabled) For(condition).Timer.Refresh(remaining, Time.unscaledTime);
    }

    /// <summary>Show the condition for a fixed time from now (a repeat hit restarts it).</summary>
    internal static void Start(PlayerCondition condition, float seconds)
    {
        if (Enabled) For(condition).Timer.Start(seconds, Time.unscaledTime);
    }

    internal static void End(PlayerCondition condition) => For(condition).Timer.End(Time.unscaledTime);

    /// <summary>Driven by <see cref="HudRuntime"/> once per frame (LateUpdate).</summary>
    internal static void Tick()
    {
        float now = Time.unscaledTime;
        bool blocked = LevelInfoHud.MenuOpen;
        int order = 0;
        foreach (var pair in Views)
        {
            var view = pair.Value;
            float alpha = blocked ? 0f : view.Timer.Alpha(now);
            if (alpha <= 0f) { Hide(pair.Key, view); continue; }
            if (!Ensure(pair.Key, view)) continue;
            Draw(pair.Key, view, alpha, now, order++);
        }
    }

    internal static void ClearAll()
    {
        foreach (var pair in Views)
        {
            if (pair.Value.Frame != null) Object.Destroy(pair.Value.Frame.gameObject);
            if (pair.Value.Card != null) Object.Destroy(pair.Value.Card.gameObject);
            HudStack.Release(CardId(pair.Key));
        }
        Views.Clear();
    }

    private static string CardId(PlayerCondition condition) => "condition." + condition;

    private static void Hide(PlayerCondition condition, View view)
    {
        if (view.Frame != null && view.Frame.gameObject.activeSelf) view.Frame.gameObject.SetActive(false);
        if (view.Card != null && view.Card.gameObject.activeSelf) view.Card.gameObject.SetActive(false);
        HudStack.Release(CardId(condition));
    }

    private static void Draw(PlayerCondition condition, View view, float alpha, float now, int order)
    {
        var look = Looks[condition];
        // Edge frame: a gentle pulse, faster for poison, slow breathing for sleep, none for ice.
        float pulse = condition switch
        {
            PlayerCondition.Poisoned => .9f + .1f * Mathf.Sin(now * 5f),
            PlayerCondition.Sedated => .85f + .15f * Mathf.Sin(now * 1.6f),
            _ => 1f,
        };
        view.Frame.gameObject.SetActive(true);
        view.FrameImage.color = new Color(1f, 1f, 1f, alpha * pulse);
        foreach (var (label, home, phase) in view.Zs)
        {
            float t = Mathf.Repeat(now * .35f + phase, 1f);
            label.rectTransform.anchoredPosition = home + new Vector2(8f * Mathf.Sin(t * 6.28f), 46f * t);
            label.alpha = alpha * Mathf.Sin(t * Mathf.PI);
        }

        // Card: icon, title, time bar, effect lines.
        view.Card.gameObject.SetActive(true);
        float fraction = view.Timer.Fraction(now);
        int filled = Mathf.CeilToInt(fraction * SegmentCount);
        for (int i = 0; i < view.Segments.Count; i++)
            view.Segments[i].color = i < filled ? WithAlpha(look.Color, alpha) : new Color(look.Color.r * .3f, look.Color.g * .3f, look.Color.b * .3f, .55f * alpha);
        view.Title.alpha = alpha;
        float seconds = view.Timer.RemainingSeconds(now);
        // Tenths only change ten times a second: rebuild the string only then.
        int tenths = Mathf.CeilToInt(seconds * 10f);
        if (tenths != view.ShownTenths) { view.ShownTenths = tenths; view.Seconds.text = tenths > 0 ? (tenths / 10f).ToString("0.0") + "s" : ""; }
        view.Seconds.alpha = alpha;
        foreach (var line in view.Lines) line.alpha = alpha * .9f;
        view.Icon.color = view.Accent.color = WithAlpha(look.Color, alpha);
        view.Backplate.color = new Color(.02f, .03f, .05f, .5f * alpha);
        HudStack.Place(view.Card, HudCorner.TopLeft, CardId(condition), 10 + order);
    }

    private static Color WithAlpha(Color color, float alpha) { color.a = alpha; return color; }

    private static bool Ensure(PlayerCondition condition, View view)
    {
        if (view.Frame != null && !view.Frame.HasCurrentOwner) { Object.Destroy(view.Frame.gameObject); view.Frame = null; }
        if (view.Card != null && !view.Card.HasCurrentOwner) { Object.Destroy(view.Card.gameObject); view.Card = null; }
        var look = Looks[condition];
        if (view.Frame == null)
        {
            var frame = NativeHudPanel.Create("IHM_ConditionFrame_" + condition, new Vector2(1920f, 1080f), Vector2.one * .5f, Vector2.zero, 0f);
            if (frame == null) return false;
            frame.GetComponent<Canvas>().sortingOrder = 30; // under the mod's panels (40), over the world
            var panel = frame.Panel;
            panel.anchorMin = Vector2.zero; panel.anchorMax = Vector2.one; panel.offsetMin = panel.offsetMax = Vector2.zero;
            panel.GetComponent<Image>().color = Color.clear;
            var imageObject = new GameObject("Edge", typeof(RectTransform), typeof(RawImage));
            imageObject.transform.SetParent(panel, false);
            var rect = (RectTransform)imageObject.transform;
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
            view.FrameImage = imageObject.GetComponent<RawImage>();
            view.FrameImage.texture = HudScreenArt.Frame(condition);
            view.FrameImage.raycastTarget = false;
            view.Zs.Clear();
            if (condition == PlayerCondition.Sedated)
                foreach (var (anchor, home, phase, size) in new[]
                         {
                             (new Vector2(1f, 1f), new Vector2(-140f, -150f), 0f, 46f), (new Vector2(1f, 0f), new Vector2(-150f, 220f), .4f, 38f),
                             (new Vector2(0f, 0f), new Vector2(130f, 150f), .7f, 34f),
                         })
                {
                    var z = frame.Label("Zzz", size, TextAlignmentOptions.Center);
                    z.text = "Z<size=75%>z<size=60%>z";
                    z.color = new Color(.78f, .72f, 1f);
                    var rt = z.rectTransform;
                    rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
                    rt.sizeDelta = new Vector2(160f, 80f);
                    view.Zs.Add((z, home, phase));
                }
            view.Frame = frame;
        }
        if (view.Card == null)
        {
            int lines = look.Lines.Length;
            float height = 96f + lines * 28f + 12f;
            var card = NativeHudPanel.Create("IHM_ConditionCard_" + condition, new Vector2(CardWidth, height), new Vector2(0f, 1f), Vector2.zero, .5f);
            if (card == null) return false;
            card.Panel.name = "Panel";
            view.Backplate = card.Panel.GetComponent<Image>();
            view.Accent = AddImage(card.Panel, "Accent", null, new Vector2(0f, 0f), new Vector2(5f, height), stretchHeight: true);
            view.Icon = AddImage(card.Panel, "Icon", HudScreenArt.Icon(condition), new Vector2(16f, -14f), new Vector2(70f, 70f));
            view.ShownTenths = -1;
            view.Title = card.Label("Title", HudStyle.Heading * 1.35f, TextAlignmentOptions.TopLeft);
            view.Title.text = look.Title;
            view.Title.color = look.Color;
            Place(view.Title, 100f, 10f, 250f, 44f);
            view.Seconds = card.Label("Seconds", HudStyle.Small, TextAlignmentOptions.TopLeft);
            view.Seconds.color = Color.white;
            Place(view.Seconds, 100f + SegmentCount * 24f + 6f, 54f, 70f, 24f);
            view.Segments.Clear();
            for (int i = 0; i < SegmentCount; i++)
                view.Segments.Add(AddImage(card.Panel, "Segment", null, new Vector2(100f + i * 24f, -60f), new Vector2(20f, 11f)));
            view.Lines.Clear();
            for (int i = 0; i < lines; i++)
            {
                var line = card.Label("Line", HudStyle.Small, TextAlignmentOptions.TopLeft);
                line.text = look.Lines[i];
                line.color = new Color(.9f, .92f, .95f);
                Place(line, 22f, 96f + i * 28f, CardWidth - 36f, 26f);
                view.Lines.Add(line);
            }
            view.Card = card;
        }
        return true;
    }

    private static Image AddImage(RectTransform parent, string name, Sprite sprite, Vector2 position, Vector2 size, bool stretchHeight = false)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        if (stretchHeight)
        {
            rt.anchorMin = new Vector2(0f, 0f); rt.anchorMax = new Vector2(0f, 1f); rt.pivot = new Vector2(0f, .5f);
            rt.anchoredPosition = Vector2.zero; rt.sizeDelta = new Vector2(size.x, 0f);
        }
        else
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = position; rt.sizeDelta = size;
        }
        var image = go.GetComponent<Image>();
        image.sprite = sprite;
        image.preserveAspect = sprite != null;
        image.raycastTarget = false;
        return image;
    }

    private static void Place(TextMeshProUGUI label, float x, float y, float width, float height)
    {
        var rt = label.rectTransform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(x, -y);
        rt.sizeDelta = new Vector2(width, height);
    }
}
