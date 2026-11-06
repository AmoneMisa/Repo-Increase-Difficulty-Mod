using System.Collections.Generic;
using IncreaseHardMod.Patches.Effects;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IncreaseHardMod.Hud;

/// <summary>How one node of a <see cref="HudTreeView"/> looks this frame.</summary>
internal struct HudTreeLook
{
    public string Name, Value;
    public Color Fill, Edge, Text, ValueColour;
    /// <summary>Whether the line from its parent is lit.</summary>
    public bool Linked;
}

/// <summary>
/// A full-screen, mouse-driven tree view (skill-tree style): a title and subtitle, page tabs, nodes laid out by
/// <see cref="HudTreeLayout"/> with connectors between parent and child, a tooltip line and footer buttons.
/// Full-screen menus may have a backplate (AGENTS.md); all text uses the game's own TextMeshPro font through
/// <see cref="NativeHudPanel"/>. The owner decides what nodes mean and how they look; this class only draws and
/// reports what the mouse is over. Coordinates are in the 1920x1080 reference space of the panel.
/// </summary>
internal sealed class HudTreeView
{
    private const float Left = 80f, Right = 1840f, Top = 170f, Bottom = 890f, ColumnGap = 70f, DepthGap = 34f;

    private sealed class Node
    {
        public string Id;
        public RectTransform Box;
        public Image Edge, Fill;
        public TextMeshProUGUI Name, Value;
        public readonly List<Image> Lines = new();
    }

    private sealed class Clickable
    {
        public RectTransform Rect;
        public Image Edge, Fill;
        public TextMeshProUGUI Label;
    }

    private NativeHudPanel _surface;
    private TextMeshProUGUI _title, _subtitle, _corner, _tooltip;
    private RectTransform _frame, _content;
    private Image _cursor, _cursorShadow;
    private readonly Dictionary<string, Node> _nodes = new();
    private readonly List<TextMeshProUGUI> _groupTitles = new();
    private readonly List<Clickable> _tabs = new(), _buttons = new();
    private readonly string _name;

    internal HudTreeView(string name) => _name = name;

    internal string HoveredNode { get; private set; }
    internal int HoveredTab { get; private set; } = -1;
    internal int HoveredButton { get; private set; } = -1;
    internal bool Visible => _surface != null && _surface.gameObject.activeSelf;

    private bool Ensure()
    {
        if (_surface != null && _surface.HasCurrentOwner) return true;
        Destroy();
        _surface = NativeHudPanel.Fullscreen(_name);
        if (_surface == null) return false;
        // Above every HUD layer, including other mods' overlays (voice-chat name bubbles were drawing over nodes).
        _surface.GetComponent<Canvas>().sortingOrder = 1000;
        var backplate = _surface.Panel.GetComponent<Image>();
        if (backplate != null) backplate.color = new Color(0f, 0f, 0f, .985f);
        // A fixed 1920x1080 frame in the middle: wide or tall screens add margins instead of stretching the layout.
        var frame = new GameObject("Frame", typeof(RectTransform));
        frame.transform.SetParent(_surface.Panel, false);
        _frame = (RectTransform)frame.transform;
        _frame.anchorMin = _frame.anchorMax = _frame.pivot = new Vector2(.5f, .5f);
        _frame.anchoredPosition = Vector2.zero;
        _frame.sizeDelta = new Vector2(1920f, 1080f);
        _title = Text("Title", HudStyle.Heading * 2f, TextAlignmentOptions.Center, new Vector2(960f, 62f), new Vector2(1200f, 70f), HudStyle.Title);
        _subtitle = Text("Subtitle", HudStyle.Body * 1.15f, TextAlignmentOptions.Center, new Vector2(960f, 118f), new Vector2(1400f, 34f), Color.white);
        _corner = Text("Points", HudStyle.Heading, TextAlignmentOptions.Left, new Vector2(Left + 300f, 70f), new Vector2(600f, 40f), HudStyle.Good);
        _tooltip = Text("Tooltip", HudStyle.Body, TextAlignmentOptions.TopLeft, new Vector2(960f, 960f), new Vector2(Right - Left, 96f), Color.white);
        _tooltip.enableWordWrapping = true;
        _tooltip.enableAutoSizing = false;
        _tooltip.overflowMode = TextOverflowModes.Truncate;
        var content = new GameObject("Content", typeof(RectTransform));
        content.transform.SetParent(_frame, false);
        _content = Place((RectTransform)content.transform, Vector2.zero, Vector2.zero);
        // Our own pointer on top: the game's menu cursor lives on a lower canvas and showed only dimly through us.
        _cursorShadow = Pointer(new Color(0f, 0f, 0f, .7f));
        _cursor = Pointer(HudStyle.Title);
        return true;
    }

    internal void Show() { if (Ensure()) _surface.gameObject.SetActive(true); }
    internal void Hide() { if (_surface != null) _surface.gameObject.SetActive(false); }

    internal void Destroy()
    {
        if (_surface != null) Object.Destroy(_surface.gameObject);
        _surface = null;
        _nodes.Clear();
        _groupTitles.Clear();
        _tabs.Clear();
        _buttons.Clear();
    }

    internal void SetHeader(string title, string subtitle, string corner)
    {
        if (!Ensure()) return;
        Set(_title, title);
        Set(_subtitle, subtitle);
        Set(_corner, corner);
    }

    internal void SetTooltip(string text) { if (Ensure()) Set(_tooltip, text ?? ""); }

    /// <summary>Page tabs at the top right; <paramref name="active"/> is drawn lit.</summary>
    internal void SetTabs(IReadOnlyList<string> labels, int active, Color accent)
    {
        if (!Ensure()) return;
        Rebuild(_tabs, labels.Count, i => new Vector2(Right - (labels.Count - i) * 70f + 35f, 70f), new Vector2(58f, 50f), "Tab");
        for (int i = 0; i < _tabs.Count; i++)
        {
            bool lit = i == active, hover = i == HoveredTab;
            Style(_tabs[i], labels[i], lit ? accent : hover ? accent * .8f : new Color(.5f, .5f, .5f, .8f), lit ? accent * .35f : new Color(0f, 0f, 0f, .6f), lit ? Color.white : new Color(.8f, .8f, .8f));
        }
    }

    /// <summary>Footer buttons, centred below the tooltip.</summary>
    internal void SetButtons(IReadOnlyList<string> labels, IReadOnlyList<Color> colours)
    {
        if (!Ensure()) return;
        const float width = 300f, gap = 24f;
        float start = 960f - (labels.Count * width + (labels.Count - 1) * gap) / 2f + width / 2f;
        Rebuild(_buttons, labels.Count, i => new Vector2(start + i * (width + gap), 1040f), new Vector2(width, 44f), "Button");
        for (int i = 0; i < _buttons.Count; i++)
        {
            var colour = colours[i];
            bool hover = i == HoveredButton;
            Style(_buttons[i], labels[i], hover ? colour : colour * .75f, hover ? colour * .3f : new Color(0f, 0f, 0f, .6f), Color.white);
        }
    }

    /// <summary>Builds a page's nodes; call when the page changes. Groups are titled with <paramref name="groupTitle"/>.</summary>
    internal void BuildPage(IReadOnlyList<HudTreeItem> items, System.Func<string, string> groupTitle, Color accent, int columns = 2)
    {
        if (!Ensure()) return;
        foreach (Transform child in _content) Object.Destroy(child.gameObject);
        _nodes.Clear();
        _groupTitles.Clear();
        HoveredNode = null;

        var slots = HudTreeLayout.Layout(items, columns, out var rows, out _);
        int maxRows = 1;
        foreach (int r in rows) maxRows = Mathf.Max(maxRows, r);
        // Each column is as wide as its deepest chain, so a deep branch (Map) gets room and every node keeps one
        // width; the whole block is then centred on the screen.
        var depths = new int[columns];
        foreach (var slot in slots) if (!slot.Title) depths[slot.Column] = Mathf.Max(depths[slot.Column], slot.Depth + 1);
        int units = 0, used = 0, gaps = 0;
        foreach (int d in depths) if (d > 0) { units += d; used++; gaps += d - 1; }
        units = Mathf.Max(1, units);
        float nodeWidth = Mathf.Min(330f, (Right - Left - ColumnGap * Mathf.Max(0, used - 1) - DepthGap * gaps) / units);
        var columnX = new float[columns];
        var columnWidth = new float[columns];
        float total = 0f;
        for (int c = 0; c < columns; c++)
        {
            columnWidth[c] = depths[c] > 0 ? depths[c] * nodeWidth + (depths[c] - 1) * DepthGap : 0f;
            if (depths[c] > 0) total += columnWidth[c] + (total > 0f ? ColumnGap : 0f);
        }
        float x0 = 960f - total / 2f;
        for (int c = 0; c < columns; c++)
        {
            columnX[c] = x0;
            if (depths[c] > 0) x0 += columnWidth[c] + ColumnGap;
        }
        float rowHeight = Mathf.Clamp((Bottom - Top) / maxRows, 34f, 64f);
        float nodeHeight = rowHeight - Mathf.Max(6f, rowHeight * .18f);
        float top = Top + Mathf.Max(0f, Bottom - Top - maxRows * rowHeight) * .3f;

        var centres = new Dictionary<string, Vector2>();
        foreach (var slot in slots)
        {
            float x = columnX[slot.Column], width = Mathf.Max(nodeWidth, columnWidth[slot.Column]);
            float y = top + slot.Row * rowHeight + rowHeight / 2f;
            if (slot.Title)
            {
                var title = Text("Group " + slot.Group, HudStyle.Heading * Mathf.Clamp(rowHeight / 50f, .8f, 1.1f), TextAlignmentOptions.Left,
                    new Vector2(x + width / 2f, y), new Vector2(width, rowHeight), accent, _content);
                Set(title, groupTitle(slot.Group));
                _groupTitles.Add(title);
                continue;
            }
            var centre = new Vector2(x + slot.Depth * (nodeWidth + DepthGap) + nodeWidth / 2f, y);
            centres[slot.Id] = centre;
            _nodes[slot.Id] = MakeNode(slot.Id, centre, new Vector2(nodeWidth, nodeHeight), rowHeight);
        }

        // Connectors: an elbow from the parent's right edge to the child's left edge.
        foreach (var item in items)
        {
            if (item.Parent == null || !centres.TryGetValue(item.Parent, out var from) || !centres.TryGetValue(item.Id, out var to)) continue;
            var node = _nodes[item.Id];
            float startX = from.x + nodeWidth / 2f, endX = to.x - nodeWidth / 2f, midX = startX + DepthGap / 2f;
            node.Lines.Add(Line(new Vector2((startX + midX) / 2f, from.y), new Vector2(midX - startX + 3f, 3f)));
            if (Mathf.Abs(to.y - from.y) > 1f) node.Lines.Add(Line(new Vector2(midX, (from.y + to.y) / 2f), new Vector2(3f, Mathf.Abs(to.y - from.y) + 3f)));
            node.Lines.Add(Line(new Vector2((midX + endX) / 2f, to.y), new Vector2(endX - midX, 3f)));
        }
    }

    internal void SetNode(string id, in HudTreeLook look)
    {
        if (!_nodes.TryGetValue(id, out var node)) return;
        node.Edge.color = look.Edge;
        node.Fill.color = look.Fill;
        node.Name.color = look.Text;
        node.Value.color = look.ValueColour;
        Set(node.Name, look.Name);
        Set(node.Value, look.Value);
        var line = look.Linked ? look.Edge : new Color(.4f, .4f, .4f, .6f);
        foreach (var image in node.Lines) image.color = line;
    }

    /// <summary>Reads the mouse; call every frame while shown.</summary>
    internal void Tick(Vector2 mouse)
    {
        HoveredNode = null;
        HoveredTab = HoveredButton = -1;
        if (!Visible) return;
        if (_cursor != null && RectTransformUtility.ScreenPointToLocalPointInRectangle(_surface.Panel, mouse, null, out var local))
        {
            _cursor.rectTransform.anchoredPosition = local;
            _cursorShadow.rectTransform.anchoredPosition = local + new Vector2(2f, -3f);
            _cursorShadow.transform.SetAsLastSibling();
            _cursor.transform.SetAsLastSibling();
        }
        foreach (var node in _nodes.Values)
            if (RectTransformUtility.RectangleContainsScreenPoint(node.Box, mouse, null)) { HoveredNode = node.Id; break; }
        for (int i = 0; i < _tabs.Count; i++)
            if (_tabs[i].Rect.gameObject.activeSelf && RectTransformUtility.RectangleContainsScreenPoint(_tabs[i].Rect, mouse, null)) HoveredTab = i;
        for (int i = 0; i < _buttons.Count; i++)
            if (_buttons[i].Rect.gameObject.activeSelf && RectTransformUtility.RectangleContainsScreenPoint(_buttons[i].Rect, mouse, null)) HoveredButton = i;
    }

    // ------------------------------------------------------------------ building blocks (reference px, y down)

    private static RectTransform Place(RectTransform rect, Vector2 centre, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(.5f, .5f);
        rect.anchoredPosition = new Vector2(centre.x, -centre.y);
        rect.sizeDelta = size;
        return rect;
    }

    private Image Box(string name, Transform parent, Vector2 centre, Vector2 size, Color colour)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var image = go.GetComponent<Image>();
        image.raycastTarget = false;
        image.color = colour;
        Place((RectTransform)go.transform, centre, size);
        return image;
    }

    private Image Pointer(Color colour)
    {
        var image = Box("Pointer", _surface.Panel, Vector2.zero, new Vector2(30f, 30f), colour);
        image.sprite = HudIcons.Pointer;
        var rect = image.rectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
        rect.pivot = new Vector2(0f, 1f);
        return image;
    }

    private Image Line(Vector2 centre, Vector2 size)
    {
        var line = Box("Line", _content, centre, size, Color.gray);
        line.transform.SetAsFirstSibling(); // under the nodes
        return line;
    }

    private TextMeshProUGUI Text(string name, float size, TextAlignmentOptions alignment, Vector2 centre, Vector2 box, Color colour, Transform parent = null)
    {
        var label = _surface.Label(name, size, alignment);
        label.transform.SetParent(parent != null ? parent : _frame, false);
        Place(label.rectTransform, centre, box);
        label.color = colour;
        return label;
    }

    private Node MakeNode(string id, Vector2 centre, Vector2 size, float rowHeight)
    {
        var node = new Node { Id = id };
        node.Edge = Box("Node " + id, _content, centre, size, Color.gray);
        node.Box = node.Edge.rectTransform;
        node.Fill = Box("Fill", node.Box, Vector2.zero, size - new Vector2(4f, 4f), Color.black);
        var fillRect = node.Fill.rectTransform;
        fillRect.anchorMin = fillRect.anchorMax = new Vector2(.5f, .5f);
        fillRect.anchoredPosition = Vector2.zero;
        float text = Mathf.Clamp(rowHeight * .46f, HudStyle.Small, HudStyle.Body * 1.05f);
        node.Name = Text("Name", text, TextAlignmentOptions.Left, Vector2.zero, new Vector2(size.x - 92f, size.y - 4f), Color.white, node.Box);
        // Long names take a second line rather than shrinking below the page's size.
        node.Name.enableWordWrapping = true;
        node.Name.fontSizeMin = text * .82f;
        node.Name.lineSpacing = -18f;
        node.Name.rectTransform.anchorMin = node.Name.rectTransform.anchorMax = new Vector2(.5f, .5f);
        node.Name.rectTransform.anchoredPosition = new Vector2(-38f, 0f);
        node.Value = Text("Value", text * .9f, TextAlignmentOptions.Right, Vector2.zero, new Vector2(82f, size.y), Color.white, node.Box);
        node.Value.rectTransform.anchorMin = node.Value.rectTransform.anchorMax = new Vector2(.5f, .5f);
        node.Value.rectTransform.anchoredPosition = new Vector2(size.x / 2f - 48f, 0f);
        return node;
    }

    private void Rebuild(List<Clickable> list, int count, System.Func<int, Vector2> centre, Vector2 size, string name)
    {
        while (list.Count < count)
        {
            var edge = Box(name, _frame, Vector2.zero, size, Color.gray);
            var fill = Box("Fill", edge.transform, Vector2.zero, size - new Vector2(4f, 4f), Color.black);
            fill.rectTransform.anchorMin = fill.rectTransform.anchorMax = new Vector2(.5f, .5f);
            fill.rectTransform.anchoredPosition = Vector2.zero;
            var label = Text("Label", HudStyle.Body, TextAlignmentOptions.Center, Vector2.zero, size, Color.white, edge.transform);
            label.rectTransform.anchorMin = label.rectTransform.anchorMax = new Vector2(.5f, .5f);
            label.rectTransform.anchoredPosition = Vector2.zero;
            list.Add(new Clickable { Rect = edge.rectTransform, Edge = edge, Fill = fill, Label = label });
        }
        for (int i = 0; i < list.Count; i++)
        {
            bool used = i < count;
            list[i].Rect.gameObject.SetActive(used);
            if (used) Place(list[i].Rect, centre(i), size);
        }
    }

    private static void Style(Clickable item, string text, Color edge, Color fill, Color label)
    {
        item.Edge.color = edge;
        item.Fill.color = fill;
        item.Label.color = label;
        Set(item.Label, text);
    }

    private static void Set(TextMeshProUGUI label, string text)
    {
        if (label != null && label.text != text) label.text = text;
    }
}
