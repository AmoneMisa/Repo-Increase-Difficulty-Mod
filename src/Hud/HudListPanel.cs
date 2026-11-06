using System.Collections.Generic;
using IncreaseHardMod.Patches.Effects;
using TMPro;
using UnityEngine;

namespace IncreaseHardMod.Hud;

/// <summary>One line of a <see cref="HudListPanel"/>: text on the left, an optional value on the right.</summary>
internal readonly struct HudListRow
{
    public readonly string Left, Right;
    public readonly Color Color;
    public HudListRow(string left, string right, Color color) { Left = left; Right = right; Color = color; }
}

/// <summary>
/// A titled list in the native HUD font (title, rows of name + value, footer), stacked in a corner through
/// <see cref="HudStack"/>, with no backplate. Long lists page; when <see cref="PageKeys"/> is on, PgUp / PgDn
/// turn the pages. Own it from a MonoBehaviour: set the content, then call <see cref="Show"/> every frame it should
/// be visible, <see cref="Hide"/> otherwise, and <see cref="Destroy"/> from OnDestroy.
/// <code>
/// _list ??= new HudListPanel("shopping", HudCorner.TopRight, order: 100);
/// _list.Title = "SHOPPING LIST";
/// _list.SetRows(rows);
/// _list.Footer = "TOTAL  $12K / $40K";
/// _list.Show();
/// </code>
/// </summary>
internal sealed class HudListPanel
{
    private const float Pad = 16f, TitleHeight = 42f, RowHeight = 30f, FooterHeight = 34f;

    private readonly string _id;
    private readonly HudCorner _corner;
    private readonly int _order;
    private readonly float _width;
    private readonly List<HudListRow> _rows = new();
    private readonly List<(TextMeshProUGUI left, TextMeshProUGUI right)> _labels = new();
    private NativeHudPanel _surface;
    private TextMeshProUGUI _title, _footer;
    private int _page;

    internal string Title = "";
    internal Color TitleColor = HudStyle.Title;
    internal string Footer;
    internal Color FooterColor = HudStyle.Good;
    internal int RowsPerPage = 12;
    internal bool PageKeys;

    internal HudListPanel(string id, HudCorner corner, int order, float width = 460f)
    {
        _id = id; _corner = corner; _order = order; _width = width;
    }

    internal void SetRows(IReadOnlyList<HudListRow> rows)
    {
        _rows.Clear();
        if (rows != null) _rows.AddRange(rows);
    }

    internal bool Visible => _surface != null && _surface.gameObject.activeSelf;

    /// <summary>Lay out and show this frame. False while the native HUD is not ready.</summary>
    internal bool Show()
    {
        if (!Ensure()) return false;
        _surface.gameObject.SetActive(true);
        int pages = Mathf.Max(1, Mathf.CeilToInt(_rows.Count / (float)Mathf.Max(1, RowsPerPage)));
        if (PageKeys && pages > 1 && SemiFunc.NoTextInputsActive())
        {
            if (Input.GetKeyDown(KeyCode.PageDown)) _page = HudPaging.Step(_page, pages, 1);
            if (Input.GetKeyDown(KeyCode.PageUp)) _page = HudPaging.Step(_page, pages, -1);
        }
        _page = Mathf.Clamp(_page, 0, pages - 1);
        int first = _page * RowsPerPage, count = Mathf.Min(RowsPerPage, _rows.Count - first);

        _title.text = pages > 1 ? $"{Title}  <size=75%>{_page + 1}/{pages}</size>" : Title;
        _title.color = TitleColor;
        Place(_title, Pad, 12f, _width - 2f * Pad, 36f);
        for (int i = 0; i < count; i++)
        {
            if (i == _labels.Count)
                _labels.Add((_surface.Label("Item", HudStyle.Body, TextAlignmentOptions.TopLeft),
                    _surface.Label("Value", HudStyle.Body, TextAlignmentOptions.TopRight)));
            var (left, right) = _labels[i];
            var row = _rows[first + i];
            left.gameObject.SetActive(true);
            right.gameObject.SetActive(!string.IsNullOrEmpty(row.Right));
            left.richText = right.richText = false;
            left.text = row.Left ?? "";
            right.text = row.Right ?? "";
            left.color = right.color = row.Color;
            float y = TitleHeight + 12f + i * RowHeight;
            if (string.IsNullOrEmpty(row.Right)) Place(left, Pad, y, _width - 2f * Pad, RowHeight);
            else
            {
                Place(left, Pad, y, _width * .73f - Pad - 4f, RowHeight);
                Place(right, _width * .73f, y, _width * .27f - Pad, RowHeight);
            }
        }
        for (int i = count; i < _labels.Count; i++)
        {
            _labels[i].left.gameObject.SetActive(false);
            _labels[i].right.gameObject.SetActive(false);
        }
        bool footer = !string.IsNullOrEmpty(Footer);
        _footer.gameObject.SetActive(footer);
        float height = TitleHeight + 12f + count * RowHeight + 10f;
        if (footer)
        {
            _footer.text = Footer;
            _footer.color = FooterColor;
            Place(_footer, Pad, height, _width - 2f * Pad, FooterHeight);
            height += FooterHeight;
        }
        _surface.Panel.sizeDelta = new Vector2(_width, height + 8f);
        HudStack.Place(_surface, _corner, _id, _order);
        return true;
    }

    internal void Hide()
    {
        if (_surface != null) _surface.gameObject.SetActive(false);
        HudStack.Release(_id);
    }

    internal void Destroy()
    {
        HudStack.Release(_id);
        if (_surface != null) Object.Destroy(_surface.gameObject);
        _surface = null;
    }

    private bool Ensure()
    {
        if (_surface != null && !_surface.HasCurrentOwner) Destroy(); // the HUD canvas was replaced
        if (_surface != null) return true;
        _surface = NativeHudPanel.Create("IHM_List_" + _id, new Vector2(_width, 200f), new Vector2(1f, 1f), Vector2.zero, 0f);
        if (_surface == null) return false;
        _labels.Clear();
        _title = _surface.Label("Title", HudStyle.Heading, TextAlignmentOptions.TopLeft);
        _footer = _surface.Label("Footer", HudStyle.Body + 2f, TextAlignmentOptions.TopLeft);
        return true;
    }

    private static void Place(TextMeshProUGUI label, float x, float y, float width, float height)
    {
        var rt = label.rectTransform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(x, -y);
        rt.sizeDelta = new Vector2(width, height);
    }
}
