using System.Collections.Generic;
using IncreaseHardMod.Hud;
using IncreaseHardMod.Patches.Effects;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IncreaseHardMod.Courier;

/// <summary>How an entry of the courier menu is drawn.</summary>
internal enum CourierItemKind : byte
{
    /// <summary>A command tile with an icon in the two-column grid (main page).</summary>
    Tile,
    /// <summary>A full-width line: label left, note right (sub-pages).</summary>
    Row,
    /// <summary>A small centred text command in the row under the grid ("MORE • FOLLOW ME").</summary>
    Link,
    /// <summary>A pet card with its portrait (gacha list, four to a row).</summary>
    Card,
}

/// <summary>
/// The courier menu panel: a dark plate at the top right (so it never sits on top of other text), a header with the
/// pet's portrait, name, what it is doing, its health and its backpack, then the page - a grid of command tiles with
/// icons and locks, plain rows, or pet cards with portraits - and a key hint line. Native HUD font throughout.
/// It only draws: CourierMenu owns the entries, the cursor and the keys.
/// </summary>
internal sealed class CourierMenuView
{
    internal sealed class Header
    {
        public string Title, Status, Health, Pack, Hint, Coins;
        public Color StatusColor;
        public Sprite Portrait;
    }

    internal sealed class Item
    {
        public CourierItemKind Kind;
        public string Text, Note;
        public Sprite Icon;
        public bool Enabled = true, Locked, Selected, Active;
    }

    private const float Width = 470f, Pad = 16f, TileW = 211f, TileH = 60f, Gap = 10f, RowH = 32f, CardW = 102f, CardH = 132f;
    private static readonly Color Plate = new(.03f, .035f, .05f, .84f), Line = new(1f, 1f, 1f, .16f), Dim = new(.55f, .56f, .6f);
    private const int MaxRows = 11;

    private NativeHudPanel _panel;
    private string _signature;
    private readonly List<GameObject> _parts = new();
    private TextMeshProUGUI _title, _status, _health, _pack, _hint, _coins;
    private Image _portrait, _heart, _bag;
    private readonly List<Cell> _cells = new();

    private sealed class Cell
    {
        public Image Frame, Icon, Lock, Highlight;
        public TextMeshProUGUI Text, Note;
        public RectTransform Root;
    }

    internal void Hide()
    {
        if (_panel != null) { _panel.gameObject.SetActive(false); HudStack.Release("courier"); }
    }

    internal void Destroy()
    {
        if (_panel != null) Object.Destroy(_panel.gameObject);
        _panel = null; _signature = null;
        HudStack.Release("courier");
    }

    internal void Show(Header header, List<Item> items, int first)
    {
        if (_panel != null && !_panel.HasCurrentOwner) Destroy();
        if (_panel == null)
        {
            _panel = NativeHudPanel.Create("IHM_CourierMenu", new Vector2(Width, 200f), Vector2.one, Vector2.zero, 0f);
            if (_panel == null) return;
            _panel.GetComponent<Canvas>().sortingOrder = 45;
            _panel.Panel.GetComponent<Image>().color = Plate;
            _signature = null;
        }
        if (!_panel.gameObject.activeSelf) _panel.gameObject.SetActive(true);

        // Rebuild only when the layout changes (page, number or kind of entries); otherwise just refresh text/colour.
        int shown = 0;
        var visible = new List<Item>();
        int limit = items.TrueForAll(x => x.Kind == CourierItemKind.Row) ? MaxRows : 48;
        for (int i = first; i < items.Count && shown < limit; i++) { visible.Add(items[i]); shown++; }
        string signature = header.Coins != null ? "c" : "n";
        foreach (var item in visible) signature += (char)('a' + (int)item.Kind);
        if (signature != _signature) Build(header, visible, signature);
        Fill(header, visible);
        HudStack.Place(_panel, HudCorner.TopRight, "courier", 50);
    }

    private void Build(Header header, List<Item> items, string signature)
    {
        foreach (var part in _parts) if (part != null) Object.Destroy(part);
        _parts.Clear(); _cells.Clear();
        _signature = signature;
        var root = _panel.Panel;

        // Header: portrait, name, status; health and backpack on the right.
        _portrait = AddImage(root, null, new Vector2(Pad, -12f), new Vector2(56f, 56f));
        _title = Label(HudStyle.Heading * 1.12f, TextAlignmentOptions.TopLeft, new Vector2(Pad + 66f, -8f), new Vector2(260f, 36f));
        _status = Label(HudStyle.Body, TextAlignmentOptions.TopLeft, new Vector2(Pad + 66f, -42f), new Vector2(240f, 28f));
        _heart = AddImage(root, CourierIcons.Heart, new Vector2(Width - Pad - 112f, -14f), new Vector2(22f, 22f));
        _health = Label(HudStyle.Body, TextAlignmentOptions.TopRight, new Vector2(Width - Pad - 86f, -10f), new Vector2(86f, 28f));
        _bag = AddImage(root, CourierIcons.Backpack, new Vector2(Width - Pad - 112f, -44f), new Vector2(22f, 22f));
        _pack = Label(HudStyle.Body, TextAlignmentOptions.TopRight, new Vector2(Width - Pad - 86f, -40f), new Vector2(86f, 28f));
        float y = -80f;
        Rule(y); y -= 12f;
        if (header.Coins != null)
        {
            _coins = Label(HudStyle.Small, TextAlignmentOptions.TopLeft, new Vector2(Pad, y), new Vector2(Width - 2f * Pad, 24f));
            y -= 28f;
        }
        else _coins = null;

        int column = 0;
        CourierItemKind? previous = null;
        float rowTop = y;
        var links = new List<int>();
        for (int i = 0; i < items.Count; i++)
        {
            var kind = items[i].Kind;
            if (previous != null && previous != kind)
            {
                // Close the previous block.
                if (previous is CourierItemKind.Tile or CourierItemKind.Card && column > 0) y = rowTop - (previous == CourierItemKind.Tile ? TileH : CardH) - Gap;
                if (kind == CourierItemKind.Link || previous == CourierItemKind.Link) { Rule(y + Gap * .5f - 2f); y -= 8f; }
                column = 0;
            }
            previous = kind;
            switch (kind)
            {
                case CourierItemKind.Tile:
                    if (column == 0) rowTop = y;
                    _cells.Add(Tile(new Vector2(Pad + column * (TileW + Gap + 4f), rowTop)));
                    if (++column == 2) { column = 0; y = rowTop - TileH - Gap; }
                    break;
                case CourierItemKind.Card:
                    if (column == 0) rowTop = y;
                    _cells.Add(Card(new Vector2(Pad + column * (CardW + 8f), rowTop)));
                    if (++column == 4) { column = 0; y = rowTop - CardH - Gap; }
                    break;
                case CourierItemKind.Row:
                    _cells.Add(Row(y));
                    y -= RowH + 4f;
                    break;
                case CourierItemKind.Link:
                    links.Add(i);
                    _cells.Add(null); // laid out below, all links share one centred line
                    break;
            }
        }
        if (previous is CourierItemKind.Tile or CourierItemKind.Card && column > 0) y = rowTop - (previous == CourierItemKind.Tile ? TileH : CardH) - Gap;
        if (links.Count > 0)
        {
            float slot = (Width - 2f * Pad) / links.Count;
            for (int k = 0; k < links.Count; k++)
                _cells[links[k]] = Link(new Vector2(Pad + k * slot, y), slot, k > 0);
            y -= RowH + 6f;
        }
        Rule(y); y -= 8f;
        _hint = Label(HudStyle.Small, TextAlignmentOptions.TopLeft, new Vector2(Pad, y), new Vector2(Width - 2f * Pad, 24f));
        y -= 30f;
        root.sizeDelta = new Vector2(Width, -y);
    }

    private void Fill(Header header, List<Item> items)
    {
        _portrait.sprite = header.Portrait != null ? header.Portrait : CourierIcons.Paw;
        _portrait.color = header.Portrait != null ? Color.white : HudStyle.Title;
        _title.text = header.Title; _title.color = HudStyle.Title;
        _status.text = header.Status ?? ""; _status.color = header.StatusColor;
        bool stats = !string.IsNullOrEmpty(header.Health);
        _heart.enabled = _bag.enabled = stats;
        _health.text = header.Health ?? ""; _pack.text = header.Pack ?? "";
        _health.color = _pack.color = HudStyle.Text;
        _heart.color = _bag.color = new Color(.92f, .92f, .95f);
        if (_coins != null) { _coins.text = header.Coins; _coins.color = HudStyle.Info; }
        _hint.text = header.Hint; _hint.color = new Color(.85f, .87f, .92f);

        for (int i = 0; i < items.Count && i < _cells.Count; i++)
        {
            var item = items[i]; var cell = _cells[i];
            if (cell == null) continue;
            var colour = !item.Enabled ? Dim : item.Selected ? HudStyle.Title : HudStyle.Text;
            cell.Text.text = item.Text ?? "";
            cell.Text.color = colour;
            if (cell.Note != null) { cell.Note.text = item.Note ?? ""; cell.Note.color = item.Active ? HudStyle.Good : item.Selected && item.Enabled ? HudStyle.Info : Dim; }
            if (cell.Icon != null)
            {
                cell.Icon.sprite = item.Icon;
                cell.Icon.enabled = item.Icon != null;
                // Cards: portraits keep their colours; locked pets are shown as dark shapes.
                cell.Icon.color = item.Kind == CourierItemKind.Card
                    ? (item.Locked ? new Color(.12f, .12f, .15f, .95f) : Color.white)
                    : colour;
            }
            if (cell.Lock != null) { cell.Lock.enabled = item.Locked; cell.Lock.color = Dim; }
            if (cell.Frame != null) { cell.Frame.enabled = item.Selected; cell.Frame.color = HudStyle.Title; }
            if (cell.Highlight != null) cell.Highlight.color = item.Selected ? new Color(1f, .85f, .4f, .1f) : item.Active ? new Color(.5f, 1f, .5f, .08f) : new Color(1f, 1f, 1f, .03f);
        }
    }

    // ------------------------------------------------------------------ parts

    private Cell Tile(Vector2 at)
    {
        var cell = new Cell { Root = Group(at, new Vector2(TileW, TileH)) };
        cell.Highlight = AddImage(cell.Root, null, Vector2.zero, new Vector2(TileW, TileH));
        cell.Frame = AddImage(cell.Root, CourierIcons.Brackets, Vector2.zero, new Vector2(TileW, TileH));
        cell.Frame.type = Image.Type.Sliced; cell.Frame.preserveAspect = false;
        cell.Icon = AddImage(cell.Root, null, new Vector2(12f, -13f), new Vector2(34f, 34f));
        cell.Text = Label(HudStyle.Body * 1.05f, TextAlignmentOptions.MidlineLeft, new Vector2(54f, -6f), new Vector2(TileW - 86f, 30f), cell.Root);
        cell.Note = Label(HudStyle.Small * .85f, TextAlignmentOptions.TopLeft, new Vector2(54f, -34f), new Vector2(TileW - 86f, 22f), cell.Root);
        cell.Lock = AddImage(cell.Root, CourierIcons.Lock, new Vector2(TileW - 30f, -19f), new Vector2(20f, 22f));
        return cell;
    }

    private Cell Card(Vector2 at)
    {
        var cell = new Cell { Root = Group(at, new Vector2(CardW, CardH)) };
        cell.Highlight = AddImage(cell.Root, null, Vector2.zero, new Vector2(CardW, CardH));
        cell.Frame = AddImage(cell.Root, CourierIcons.Brackets, Vector2.zero, new Vector2(CardW, CardH));
        cell.Frame.type = Image.Type.Sliced; cell.Frame.preserveAspect = false;
        cell.Icon = AddImage(cell.Root, null, new Vector2(8f, -4f), new Vector2(CardW - 16f, CardW - 16f));
        cell.Lock = AddImage(cell.Root, CourierIcons.Lock, new Vector2(CardW - 26f, -8f), new Vector2(18f, 20f));
        cell.Text = Label(HudStyle.Small, TextAlignmentOptions.Top, new Vector2(2f, -CardW + 12f), new Vector2(CardW - 4f, 22f), cell.Root);
        cell.Note = Label(HudStyle.Small * .78f, TextAlignmentOptions.Top, new Vector2(2f, -CardW - 8f), new Vector2(CardW - 4f, 20f), cell.Root);
        return cell;
    }

    private Cell Row(float y)
    {
        var cell = new Cell { Root = Group(new Vector2(Pad, y), new Vector2(Width - 2f * Pad, RowH)) };
        cell.Highlight = AddImage(cell.Root, null, Vector2.zero, new Vector2(Width - 2f * Pad, RowH));
        cell.Frame = AddImage(cell.Root, CourierIcons.Brackets, Vector2.zero, new Vector2(Width - 2f * Pad, RowH));
        cell.Frame.type = Image.Type.Sliced; cell.Frame.preserveAspect = false;
        cell.Text = Label(HudStyle.Body, TextAlignmentOptions.MidlineLeft, new Vector2(10f, 0f), new Vector2(Width - 2f * Pad - 150f, RowH), cell.Root);
        cell.Note = Label(HudStyle.Small, TextAlignmentOptions.MidlineRight, new Vector2(Width - 2f * Pad - 190f, 0f), new Vector2(180f, RowH), cell.Root);
        return cell;
    }

    private Cell Link(Vector2 at, float width, bool dot)
    {
        var cell = new Cell { Root = Group(at, new Vector2(width, RowH)) };
        if (dot) Label(HudStyle.Body, TextAlignmentOptions.MidlineLeft, new Vector2(-6f, 0f), new Vector2(12f, RowH), cell.Root).text = "•";
        cell.Text = Label(HudStyle.Body * 1.05f, TextAlignmentOptions.Center, Vector2.zero, new Vector2(width, RowH), cell.Root);
        return cell;
    }

    private RectTransform Group(Vector2 at, Vector2 size)
    {
        var go = new GameObject("Cell", typeof(RectTransform));
        go.transform.SetParent(_panel.Panel, false);
        _parts.Add(go);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = at; rt.sizeDelta = size;
        return rt;
    }

    private void Rule(float y)
    {
        var image = AddImage(_panel.Panel, null, new Vector2(Pad, y), new Vector2(Width - 2f * Pad, 1.5f));
        image.color = Line;
    }

    private TextMeshProUGUI Label(float size, TextAlignmentOptions alignment, Vector2 at, Vector2 box, RectTransform parent = null)
    {
        var label = _panel.Label("Text", size, alignment);
        var rt = label.rectTransform;
        if (parent != null) rt.SetParent(parent, false); else _parts.Add(label.gameObject);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = at; rt.sizeDelta = box;
        return label;
    }

    private Image AddImage(RectTransform parent, Sprite sprite, Vector2 at, Vector2 size)
    {
        var go = new GameObject("Image", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        if (parent == _panel.Panel) _parts.Add(go);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = at; rt.sizeDelta = size;
        var image = go.GetComponent<Image>();
        image.sprite = sprite;
        image.preserveAspect = sprite != null;
        image.raycastTarget = false;
        image.color = sprite != null ? Color.white : Color.clear;
        return image;
    }
}

/// <summary>White silhouette icons for the courier menu (HudIcons.Silhouette: u -1..1 left to right, v 0..1 top to bottom).</summary>
internal static class CourierIcons
{
    internal static Sprite Bolt => HudIcons.Silhouette("courier_bolt", (u, v) =>
        Tri(u, v, .25f, 0f, -.55f, .58f, .1f, .58f) || Tri(u, v, -.25f, 1f, .55f, .42f, -.1f, .42f) || Box(u, v, -.12f, .4f, .12f, .6f));

    internal static Sprite Crate => HudIcons.Silhouette("courier_crate", (u, v) =>
    {
        // An open box seen from the front: body, lid flaps, a dark seam.
        bool body = Box(u, v, -.72f, .38f, .72f, .95f) && !Box(u, v, -.06f, .38f, .06f, .62f);
        bool flapL = Tri(u, v, -.72f, .38f, -.05f, .38f, -.9f, .14f) || Tri(u, v, -.05f, .38f, -.9f, .14f, -.2f, .14f);
        bool flapR = Tri(u, v, .72f, .38f, .05f, .38f, .9f, .14f) || Tri(u, v, .05f, .38f, .9f, .14f, .2f, .14f);
        return body || flapL || flapR;
    });

    internal static Sprite Store => HudIcons.Silhouette("courier_store", (u, v) =>
    {
        // An arrow down into an open tray.
        bool tray = (Box(u, v, -.85f, .62f, -.68f, .95f) || Box(u, v, .68f, .62f, .85f, .95f) || Box(u, v, -.85f, .82f, .85f, .95f));
        bool shaft = Box(u, v, -.12f, .02f, .12f, .45f);
        bool head = Tri(u, v, -.42f, .4f, .42f, .4f, 0f, .74f);
        return tray || shaft || head;
    });

    internal static Sprite People => HudIcons.Silhouette("courier_people", (u, v) =>
        Circle(u, v, -.32f, .28f, .2f) || Circle(u, v, .38f, .24f, .23f) ||
        Ellipse(u, v, -.32f, .82f, .42f, .3f) && v < .95f || Ellipse(u, v, .38f, .8f, .48f, .34f) && v < .95f);

    internal static Sprite Cart => HudIcons.Silhouette("courier_cart", (u, v) =>
        Quad(u, v, -.7f, .2f, .85f, .2f, .65f, .66f, -.5f, .66f) || Box(u, v, -.95f, .1f, -.62f, .2f) ||
        Circle(u, v, -.35f, .84f, .11f) || Circle(u, v, .45f, .84f, .11f));

    internal static Sprite Backpack => HudIcons.Silhouette("courier_backpack", (u, v) =>
    {
        bool body = RoundBox(u, v, -.62f, .22f, .62f, .98f, .22f);
        bool handle = Ring(u, v, 0f, .22f, .26f, .1f) && v < .22f;
        bool pocket = Box(u, v, -.36f, .6f, .36f, .62f);
        return (body && !pocket) || handle;
    });

    internal static Sprite Lock => HudIcons.Silhouette("courier_lock", (u, v) =>
        RoundBox(u, v, -.7f, .45f, .7f, .98f, .12f) && !Circle(u, v, 0f, .68f, .1f) || Ring(u, v, 0f, .45f, .42f, .14f) && v < .46f);

    internal static Sprite Heart => HudIcons.Silhouette("courier_menu_heart", (u, v) =>
    {
        float x = u * 1.15f, y = (.55f - v) * 2.4f + .15f;
        float a = x * x + y * y - 1f;
        return a * a * a - x * x * y * y * y <= 0f;
    });

    internal static Sprite Paw => HudIcons.Silhouette("courier_paw", (u, v) =>
        Ellipse(u, v, 0f, .72f, .42f, .26f) || Circle(u, v, -.55f, .38f, .15f) || Circle(u, v, -.2f, .2f, .15f) ||
        Circle(u, v, .2f, .2f, .15f) || Circle(u, v, .55f, .38f, .15f));

    internal static Sprite Head => HudIcons.Silhouette("courier_head", (u, v) =>
        Circle(u, v, 0f, .45f, .4f) && !Circle(u, v, -.16f, .42f, .08f) && !Circle(u, v, .16f, .42f, .08f) || Box(u, v, -.28f, .78f, .28f, .95f));

    internal static Sprite Dash => HudIcons.Silhouette("courier_dash", (u, v) =>
        Tri(u, v, .1f, .1f, .9f, .5f, .1f, .9f) || Box(u, v, -.9f, .3f, -.05f, .4f) || Box(u, v, -.7f, .6f, -.05f, .7f));

    internal static Sprite Wings => HudIcons.Silhouette("courier_wings", (u, v) =>
        Ellipse(u, v, -.45f, .45f, .45f, .28f) && v < .62f || Ellipse(u, v, .45f, .45f, .45f, .28f) && v < .62f || Circle(u, v, 0f, .6f, .14f));

    internal static Sprite Coin => HudIcons.Silhouette("courier_coin", (u, v) =>
        Circle(u, v, 0f, .5f, .46f) && !Ring(u, v, 0f, .5f, .32f, .05f));

    internal static Sprite Palette => HudIcons.Silhouette("courier_palette", (u, v) =>
        Ellipse(u, v, 0f, .5f, .9f, .44f) && !Circle(u, v, .45f, .62f, .12f) && !Circle(u, v, -.4f, .35f, .09f) && !Circle(u, v, 0f, .28f, .09f));

    /// <summary>Four gold corner brackets (9-sliced) marking the selected tile.</summary>
    internal static Sprite Brackets
    {
        get
        {
            if (_brackets != null) return _brackets;
            const int size = 48, arm = 14, thick = 3;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                bool nearX = x < thick || x >= size - thick, nearY = y < thick || y >= size - thick;
                bool inArmX = x < arm || x >= size - arm, inArmY = y < arm || y >= size - arm;
                bool on = (nearX && inArmY) || (nearY && inArmX);
                pixels[y * size + x] = on ? new Color32(255, 255, 255, 255) : new Color32(255, 255, 255, 0);
            }
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "IHM courier brackets", filterMode = FilterMode.Bilinear, hideFlags = HideFlags.HideAndDontSave };
            texture.SetPixels32(pixels); texture.Apply();
            _brackets = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(.5f, .5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(arm, arm, arm, arm));
            return _brackets;
        }
    }
    private static Sprite _brackets;

    private static bool Box(float u, float v, float x0, float y0, float x1, float y1) => u >= x0 && u <= x1 && v >= y0 && v <= y1;
    private static bool Circle(float u, float v, float cx, float cy, float r) => (u - cx) * (u - cx) + ((v - cy) * 2f) * ((v - cy) * 2f) <= r * r;
    private static bool Ellipse(float u, float v, float cx, float cy, float rx, float ry) => (u - cx) * (u - cx) / (rx * rx) + (v - cy) * (v - cy) / (ry * ry) <= 1f;
    private static bool Ring(float u, float v, float cx, float cy, float r, float w)
    {
        float d = Mathf.Sqrt((u - cx) * (u - cx) + ((v - cy) * 2f) * ((v - cy) * 2f));
        return d <= r && d >= r - w * 2f;
    }
    private static bool RoundBox(float u, float v, float x0, float y0, float x1, float y1, float r)
    {
        if (!Box(u, v, x0, y0, x1, y1)) return false;
        float cx = Mathf.Clamp(u, x0 + r, x1 - r), cy = Mathf.Clamp(v, y0 + r * .5f, y1 - r * .5f);
        return (u - cx) * (u - cx) + ((v - cy) * 2f) * ((v - cy) * 2f) <= r * r;
    }
    private static bool Tri(float x, float y, float ax, float ay, float bx, float by, float cx, float cy)
    {
        float d1 = (x - bx) * (ay - by) - (ax - bx) * (y - by), d2 = (x - cx) * (by - cy) - (bx - cx) * (y - cy), d3 = (x - ax) * (cy - ay) - (cx - ax) * (y - ay);
        bool negative = d1 < 0 || d2 < 0 || d3 < 0, positive = d1 > 0 || d2 > 0 || d3 > 0;
        return !(negative && positive);
    }
    private static bool Quad(float x, float y, float ax, float ay, float bx, float by, float cx, float cy, float dx, float dy) =>
        Tri(x, y, ax, ay, bx, by, cx, cy) || Tri(x, y, ax, ay, cx, cy, dx, dy);
}
