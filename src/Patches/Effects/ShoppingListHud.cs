using System.Collections.Generic;
using UnityEngine;
using TMPro;

namespace IncreaseHardMod.Patches.Effects;

/// <summary>Shared shop cart, rendered in the native game font on a scaled screen canvas.</summary>
internal sealed class ShoppingListHud : MonoBehaviour
{
    private static readonly Color TitleColor = new(1f, 0.85f, 0.4f);

    // Aggregated row: one distinct item name, its running count and summed value.
    private sealed class Row
    {
        public string Name;
        public int Count;
        public int Sum;
        public Color Color;
    }

    private NativeHudPanel _surface;
    private TextMeshProUGUI _title, _footer;
    private readonly List<(TextMeshProUGUI name, TextMeshProUGUI price)> _labels = new();
    private readonly List<Row> _rows = new();
    private readonly Dictionary<string, Row> _byName = new();

    private void Update()
    {
        var shop = ShopManager.instance;
        if (!ModConfig.ShoppingListEnabled.Value || !SafeRunIsShop() || shop == null ||
            shop.shoppingList == null || shop.shoppingList.Count == 0)
        {
            if (_surface != null) _surface.gameObject.SetActive(false);
            return;
        }
        if (_surface == null)
        {
            _surface = NativeHudPanel.Create("IHM_ShoppingList", new Vector2(460f, 200f),
                new Vector2(1f, 1f), new Vector2(-24f, -100f), 0f);
            if (_surface == null) return;
            _labels.Clear();
            _title = _surface.Label("Title", 26f, TextAlignmentOptions.TopLeft);
            _title.text = "SHOPPING LIST";
            _title.color = TitleColor;
            _footer = _surface.Label("Total", 24f, TextAlignmentOptions.TopLeft);
        }
        _surface.gameObject.SetActive(true);
        Aggregate(shop);
        const float rowHeight = 30f;
        int total = 0;
        for (int i = 0; i < _rows.Count; i++)
        {
            if (i == _labels.Count)
                _labels.Add((_surface.Label("Item", 22f, TextAlignmentOptions.TopLeft),
                    _surface.Label("Price", 22f, TextAlignmentOptions.TopRight)));
            var (name, price) = _labels[i];
            name.gameObject.SetActive(true);
            price.gameObject.SetActive(true);
            name.richText = price.richText = false;
            name.text = $"{_rows[i].Count}x {_rows[i].Name}";
            price.text = $"${_rows[i].Sum:N0}K";
            name.color = price.color = _rows[i].Color;
            Place(name, 16f, 54f + i * rowHeight, 316f, rowHeight);
            Place(price, 338f, 54f + i * rowHeight, 106f, rowHeight);
            total += _rows[i].Sum;
        }
        for (int i = _rows.Count; i < _labels.Count; i++)
        {
            _labels[i].name.gameObject.SetActive(false);
            _labels[i].price.gameObject.SetActive(false);
        }
        int currency = SafeCurrency(shop);
        _footer.color = currency < 0 || total <= currency ? new Color(0.5f, 1f, 0.5f) : new Color(1f, 0.45f, 0.45f);
        _footer.text = currency >= 0 ? $"TOTAL  ${total:N0}K / ${currency:N0}K" : $"TOTAL  ${total:N0}K";
        Place(_title, 16f, 12f, 428f, 36f);
        Place(_footer, 16f, 64f + _rows.Count * rowHeight, 428f, 34f);
        _surface.Panel.sizeDelta = new Vector2(460f, 112f + _rows.Count * rowHeight);
        _surface.Fit();
    }

    private static void Place(TextMeshProUGUI label, float x, float y, float width, float height)
    {
        var rt = label.rectTransform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(x, -y);
        rt.sizeDelta = new Vector2(width, height);
    }

    private void OnDestroy()
    {
        if (_surface != null) Destroy(_surface.gameObject);
    }

    // Collapse the flat shopping list into distinct-name rows with counts + summed value,
    // preserving first-seen order.
    private void Aggregate(ShopManager shop)
    {
        _rows.Clear();
        _byName.Clear();

        foreach (var attr in shop.shoppingList)
        {
            if (attr == null)
                continue;

            string name = DisplayName(attr);
            if (!_byName.TryGetValue(name, out var row))
            {
                row = new Row { Name = name, Color = ColorFor(attr) };
                _byName[name] = row;
                _rows.Add(row);
            }
            row.Count++;
            row.Sum += attr.value;
        }
    }

    private static string DisplayName(ItemAttributes attr)
    {
        if (attr.item != null && !string.IsNullOrEmpty(attr.item.itemName) && attr.item.itemName != "N/A")
            return attr.item.itemName;
        if (!string.IsNullOrEmpty(attr.itemName))
            return Clean(attr.itemName);
        return Clean(attr.name);
    }

    // Colour by item category so groups are visually distinct, mirroring the reference mod.
    private static Color ColorFor(ItemAttributes attr)
    {
        if (attr.item == null)
            return Color.white;
        return attr.item.itemType switch
        {
            SemiFunc.itemType.healthPack => new Color(1f, 0.45f, 0.55f),      // pink/red
            SemiFunc.itemType.item_upgrade => new Color(0.5f, 0.8f, 1f),      // blue
            SemiFunc.itemType.player_upgrade => new Color(0.5f, 0.8f, 1f),    // blue
            SemiFunc.itemType.power_crystal => new Color(0.75f, 0.55f, 1f),   // purple
            SemiFunc.itemType.grenade => new Color(1f, 0.6f, 0.3f),           // orange
            SemiFunc.itemType.mine => new Color(1f, 0.6f, 0.3f),              // orange
            SemiFunc.itemType.launcher => new Color(1f, 0.6f, 0.3f),          // orange
            SemiFunc.itemType.gun => new Color(1f, 0.8f, 0.35f),              // gold
            SemiFunc.itemType.melee => new Color(1f, 0.8f, 0.35f),            // gold
            SemiFunc.itemType.tracker => new Color(0.5f, 1f, 0.7f),           // teal-green
            SemiFunc.itemType.tool => new Color(0.5f, 1f, 0.7f),              // teal-green
            SemiFunc.itemType.drone => new Color(0.6f, 0.9f, 0.95f),          // cyan
            SemiFunc.itemType.orb => new Color(0.6f, 0.9f, 0.95f),            // cyan
            SemiFunc.itemType.cart => new Color(0.8f, 0.8f, 0.8f),            // grey
            SemiFunc.itemType.pocket_cart => new Color(0.8f, 0.8f, 0.8f),     // grey
            SemiFunc.itemType.vehicle => new Color(0.9f, 0.85f, 0.6f),        // beige
            _ => Color.white,
        };
    }

    private static string Clean(string name)
    {
        if (string.IsNullOrEmpty(name))
            return "Item";
        int clone = name.IndexOf("(Clone)", System.StringComparison.Ordinal);
        if (clone >= 0)
            name = name.Substring(0, clone);
        return name.Replace("Item ", "").Trim();
    }

    private static int SafeCurrency(ShopManager shop)
    {
        try { return shop.totalCurrency; }
        catch { return -1; }
    }

    private static bool SafeRunIsShop()
    {
        try { return SemiFunc.RunIsShop(); }
        catch { return false; }
    }
}
