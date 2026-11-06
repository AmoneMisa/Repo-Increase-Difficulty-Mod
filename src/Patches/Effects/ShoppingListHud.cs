using System.Collections.Generic;
using UnityEngine;
using IncreaseHardMod.Hud;

namespace IncreaseHardMod.Patches.Effects;

/// <summary>Shared shop cart, rendered in the native game font on a scaled screen canvas.</summary>
internal sealed class ShoppingListHud : MonoBehaviour
{
    // Aggregated row: one distinct item name, its running count and summed value.
    private sealed class Row
    {
        public string Name;
        public int Count;
        public int Sum;
        public Color Color;
    }

    private HudListPanel _list;
    private readonly List<Row> _rows = new();
    private readonly List<HudListRow> _lines = new();
    private readonly Dictionary<string, Row> _byName = new();

    private void Update()
    {
        var shop = ShopManager.instance;
        if (!ModConfig.ShoppingListEnabled.Value || !SafeRunIsShop() || shop == null ||
            shop.shoppingList == null || shop.shoppingList.Count == 0)
        {
            _list?.Hide();
            return;
        }
        // Built on the HUD toolkit: a native-font list stacked at the top right (order 100), so the
        // modifier list and Black Market hint start below it instead of drawing over it.
        _list ??= new HudListPanel("shopping", HudCorner.TopRight, order: 100) { Title = "SHOPPING LIST", PageKeys = false };
        Aggregate(shop);
        int total = 0;
        _lines.Clear();
        foreach (var row in _rows)
        {
            _lines.Add(new HudListRow($"{row.Count}x {row.Name}", $"${row.Sum:N0}K", row.Color));
            total += row.Sum;
        }
        _list.SetRows(_lines);
        int currency = SafeCurrency(shop);
        _list.FooterColor = currency < 0 || total <= currency ? HudStyle.Good : HudStyle.Bad;
        _list.Footer = currency >= 0 ? $"TOTAL  ${total:N0}K / ${currency:N0}K" : $"TOTAL  ${total:N0}K";
        _list.Show();
    }

    private void OnDestroy() => _list?.Destroy();

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

            string name = ItemDisplayName.Get(attr);
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
