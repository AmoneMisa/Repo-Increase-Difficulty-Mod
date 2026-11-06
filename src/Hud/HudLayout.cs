using System;
using System.Collections.Generic;

namespace IncreaseHardMod.Hud;

// Engine-free layout pieces of the HUD toolkit. Everything here is plain arithmetic, so the test harness
// (tests/HudToolkitTests) compiles this file on its own and checks it without Unity.

/// <summary>Where a stacked HUD element lives. Each corner keeps its own independent stack.</summary>
internal enum HudCorner { TopRight, TopLeft, UpperCentre, BottomCentre }

/// <summary>
/// One stack of HUD blocks (for one corner). Every block claims its height each frame; a block is placed after
/// every live block with a lower order (ties broken by id), so blocks never overlap and close up when one hides.
/// Placement uses the claims made so far, so a block whose neighbour grew this frame moves on the next frame,
/// which is not visible. A claim not renewed for <see cref="LiveFrames"/> frames is dropped.
/// </summary>
internal sealed class HudStackLayout
{
    internal const int LiveFrames = 2;

    private sealed class Entry
    {
        public string Id;
        public int Order, Frame;
        public float Height, ScreenEdge;
    }

    private readonly Dictionary<string, Entry> _claims = new(StringComparer.Ordinal);

    /// <summary>Distance from the corner edge to the first block.</summary>
    internal float Start;
    /// <summary>Space between two blocks.</summary>
    internal float Gap;

    internal HudStackLayout(float start, float gap) { Start = start; Gap = gap; }

    /// <summary>Distance from the corner edge at which block <paramref name="id"/> goes this frame.</summary>
    internal float OffsetFor(string id, int order, int frame)
    {
        float offset = Start;
        foreach (var other in _claims.Values)
            if (other.Id != id && Live(other, frame) && Before(other, order, id))
                offset += other.Height + Gap;
        return offset;
    }

    /// <summary>Record this frame's height (and the block's far edge on screen, in pixels; -1 if unknown).</summary>
    internal void Claim(string id, int order, float height, int frame, float screenEdge = -1f)
    {
        if (!_claims.TryGetValue(id, out var claim)) _claims[id] = claim = new Entry { Id = id };
        claim.Order = order;
        claim.Height = Math.Max(0f, height);
        claim.Frame = frame;
        claim.ScreenEdge = screenEdge;
    }

    internal void Release(string id) => _claims.Remove(id);

    internal bool IsLive(string id, int frame) => _claims.TryGetValue(id, out var claim) && Live(claim, frame);

    /// <summary>
    /// For top stacks: the lowest screen y (pixels from the bottom) any live block reaches, or -1 when the stack
    /// is empty. HUD drawn elsewhere (the native-canvas modifier list) uses it to start below the stack.
    /// </summary>
    internal float LowestScreenEdge(int frame)
    {
        float lowest = -1f;
        foreach (var claim in _claims.Values)
            if (Live(claim, frame) && claim.ScreenEdge >= 0f && (lowest < 0f || claim.ScreenEdge < lowest))
                lowest = claim.ScreenEdge;
        return lowest;
    }

    private static bool Live(Entry claim, int frame) => frame - claim.Frame <= LiveFrames;

    private static bool Before(Entry other, int order, string id) =>
        other.Order < order || other.Order == order && string.CompareOrdinal(other.Id, id) < 0;
}

/// <summary>Splits entries into pages that fit a height, never cutting an entry in half.</summary>
internal static class HudPaging
{
    /// <param name="entries">Text blocks, each kept whole on one page.</param>
    /// <param name="measure">Rendered height of a full page text (header + body + footer).</param>
    /// <param name="height">Height available to one page.</param>
    /// <param name="header">Text shown above every page (measured with it).</param>
    /// <param name="footerSample">The widest page footer, e.g. "PGUP / PGDN  9/9" (measured with it).</param>
    internal static List<string> Paginate(IReadOnlyList<string> entries, Func<string, float> measure, float height,
        string header = "", string footerSample = "")
    {
        var pages = new List<string>();
        var body = new System.Text.StringBuilder();
        foreach (string entry in entries)
        {
            if (body.Length > 0 && measure(header + body + entry + footerSample) > height)
            {
                pages.Add(body.ToString());
                body.Clear();
            }
            body.Append(entry);
        }
        if (body.Length > 0 || pages.Count == 0) pages.Add(body.ToString());
        return pages;
    }

    /// <summary>Next / previous page index, wrapping around.</summary>
    internal static int Step(int page, int pages, int delta) => pages <= 1 ? 0 : ((page + delta) % pages + pages) % pages;
}

/// <summary>
/// Timed messages for one notification channel. Showing a message with the key of a visible one replaces its
/// text and extends it (no duplicate lines); at most <see cref="MaxVisible"/> show, newest first.
/// </summary>
internal sealed class HudMessageQueue<TColor>
{
    internal sealed class Message
    {
        public string Key, Text;
        public TColor Color;
        public float Shown, Until;
    }

    private readonly List<Message> _messages = new();
    private readonly List<Message> _visible = new();
    private int _anonymous;

    internal int MaxVisible = 3;
    internal const float FadeIn = .15f, FadeOut = .4f;

    internal void Show(string key, string text, TColor color, float seconds, float now)
    {
        key ??= "#" + _anonymous++;
        var message = _messages.Find(m => m.Key == key);
        if (message == null)
        {
            message = new Message { Key = key, Shown = now };
            _messages.Add(message);
        }
        else if (message.Until <= now) message.Shown = now; // an expired one comes back with a fresh fade-in
        message.Text = text;
        message.Color = color;
        message.Until = Math.Max(message.Until, now + Math.Max(.05f, seconds));
    }

    internal void Clear(string key) => _messages.RemoveAll(m => m.Key == key);

    internal void ClearAll() => _messages.Clear();

    /// <summary>Messages to draw now, newest first; expired ones are removed.</summary>
    internal IReadOnlyList<Message> Visible(float now)
    {
        _messages.RemoveAll(m => m.Until <= now);
        _visible.Clear();
        for (int i = _messages.Count - 1; i >= 0 && _visible.Count < MaxVisible; i--) _visible.Add(_messages[i]);
        return _visible;
    }

    /// <summary>Opacity 0..1 with a short fade in and out.</summary>
    internal static float Alpha(Message message, float now)
    {
        float fadeIn = FadeIn <= 0f ? 1f : (now - message.Shown) / FadeIn;
        float fadeOut = FadeOut <= 0f ? 1f : (message.Until - now) / FadeOut;
        return Math.Max(0f, Math.Min(1f, Math.Min(fadeIn, fadeOut)));
    }
}
