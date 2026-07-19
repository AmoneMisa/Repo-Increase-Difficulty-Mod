using System.Collections.Generic;
using UnityEngine;

namespace IncreaseHardMod.Patches.Effects;

/// <summary>
/// In-shop "Shopping List" overlay. While you're on the shop level, this reads
/// <c>ShopManager.instance.shoppingList</c> (the shared cart — everything you and your
/// friends have carried into the extraction point) every frame and draws a panel on the
/// left of the screen: each distinct item on its own row as "{qty}x {name}  ${sum}",
/// colour-coded by the item's <see cref="SemiFunc.itemType"/>, followed by a total-cost
/// vs. team-currency footer.
///
/// Pure read-only IMGUI: it never mutates the shopping list or touches Photon, so it runs
/// identically for the host and every client with no networking. Asset-free (uses
/// Texture2D.whiteTexture for the backdrop). Toggle via <see cref="ModConfig.ShoppingListEnabled"/>.
/// </summary>
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

    private GUIStyle _title;
    private GUIStyle _row;
    private GUIStyle _footer;

    // Reused per-frame so OnGUI allocates nothing steady-state.
    private readonly List<Row> _rows = new();
    private readonly Dictionary<string, Row> _byName = new();

    private void OnGUI()
    {
        if (!ModConfig.ShoppingListEnabled.Value)
            return;
        if (!SafeRunIsShop())
            return;

        var shop = ShopManager.instance;
        if (shop == null || shop.shoppingList == null || shop.shoppingList.Count == 0)
            return;

        EnsureStyles();
        Aggregate(shop);

        const float x = 12f;
        const float width = 300f;
        float rowH = 18f;
        float y = 120f;

        float panelH = 30f + _rows.Count * rowH + 26f;
        GUI.color = new Color(0f, 0f, 0f, 0.6f);
        GUI.DrawTexture(new Rect(x - 6f, y - 6f, width + 12f, panelH), Texture2D.whiteTexture);
        GUI.color = Color.white;

        DrawShadowed(new Rect(x, y, width, 22f), "Shopping List", _title, TitleColor);
        y += 26f;

        int total = 0;
        foreach (var r in _rows)
        {
            string text = $"{r.Count}x {r.Name}";
            DrawShadowed(new Rect(x, y, width - 70f, rowH), text, _row, r.Color);
            DrawShadowed(new Rect(x + width - 70f, y, 70f, rowH), $"${r.Sum}", _row, r.Color,
                TextAnchor.MiddleRight);
            total += r.Sum;
            y += rowH;
        }

        y += 4f;
        int currency = SafeCurrency(shop);
        bool afford = currency < 0 || total <= currency;
        var totColor = afford ? new Color(0.5f, 1f, 0.5f) : new Color(1f, 0.45f, 0.45f);
        string footer = currency >= 0 ? $"Total  ${total} / ${currency}" : $"Total  ${total}";
        DrawShadowed(new Rect(x, y, width, 20f), footer, _footer, totColor);
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

    private void EnsureStyles()
    {
        if (_title != null)
            return;
        _title = new GUIStyle
        {
            fontSize = 16, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft, richText = false,
        };
        _row = new GUIStyle
        {
            fontSize = 13, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft, richText = false,
        };
        _footer = new GUIStyle
        {
            fontSize = 14, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft, richText = false,
        };
    }

    private void DrawShadowed(Rect rect, string text, GUIStyle style, Color color,
        TextAnchor align = TextAnchor.MiddleLeft)
    {
        var prevAlign = style.alignment;
        style.alignment = align;

        var prev = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.85f);
        GUI.Label(new Rect(rect.x + 1f, rect.y + 1f, rect.width, rect.height), text, style);
        GUI.color = color;
        GUI.Label(rect, text, style);
        GUI.color = prev;

        style.alignment = prevAlign;
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
