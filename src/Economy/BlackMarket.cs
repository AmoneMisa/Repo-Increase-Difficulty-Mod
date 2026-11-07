using System;
using System.Collections.Generic;
using System.Text;
using IncreaseHardMod.Modifiers;
using IncreaseHardMod.Networking;
using IncreaseHardMod.Patches.Effects;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IncreaseHardMod.Economy;

/// <summary>
/// Black Market: in the shop, pay to permanently (for this run) keep an unwanted thing from ever being rolled -
/// a surprise modifier, a level economy event or a shop economy. Bans are paid from the run's currency, stored
/// in the run stats (so they persist with the save and reset with a new run) and rolled out to every client.
/// Only the host rolls events, so only the host's list decides; clients just display it.
///
/// Purchases from non-hosts are sent to the host, which re-checks price and balance before charging.
/// </summary>
internal static class BlackMarket
{
    public enum Kind
    {
        Modifier,
        Economy,
        ShopEconomy,
    }

    private const string StatPrefix = "ihm_ban:";
    private static readonly HashSet<string> Banned = new();

    private static string Key(Kind k, string id) => (char)('a' + (int)k) + ":" + id;

    public static bool IsBanned(Kind k, string id) => Banned.Contains(Key(k, id));

    // ---- price ---------------------------------------------------------------------------------------

    // How harmful each surprise modifier is, which is what makes blocking it worth more.
    private static readonly Dictionary<ModifierType, EconomyTier> ModifierTiers = new()
    {
        { ModifierType.FragileLoot, EconomyTier.Bad }, { ModifierType.HeavyLoot, EconomyTier.Bad },
        { ModifierType.DangerousLoot, EconomyTier.Bad }, { ModifierType.NoMap, EconomyTier.Bad },
        { ModifierType.ReducedPlayerHp, EconomyTier.VeryBad }, { ModifierType.MoneyLossOnDeath, EconomyTier.VeryBad },
        { ModifierType.LevelTimer, EconomyTier.Bad }, { ModifierType.SoulsExplodeHarder, EconomyTier.Bad },
        { ModifierType.MonstersPitTanky, EconomyTier.Bad }, { ModifierType.HeavyMonsters, EconomyTier.Bad },
        { ModifierType.ChangeVoices, EconomyTier.Mixed }, { ModifierType.SpreadUpgrades, EconomyTier.Mixed },
        { ModifierType.LightLoot, EconomyTier.Good }, { ModifierType.ProtectedLoot, EconomyTier.Good },
        { ModifierType.ExtraSmallCart, EconomyTier.Good }, { ModifierType.ExtraBigCart, EconomyTier.Good },
        { ModifierType.ExtraSouls, EconomyTier.Good }, { ModifierType.AutoRevive, EconomyTier.VeryGood },
        { ModifierType.MoreGachaCubes, EconomyTier.Good }, { ModifierType.BiggerSoulDrops, EconomyTier.Good },
        { ModifierType.LightMonsters, EconomyTier.Good }, { ModifierType.MysteryBoxes, EconomyTier.Good },
        { ModifierType.SilentNight, EconomyTier.Good }, { ModifierType.LoudFacility, EconomyTier.Bad },
        { ModifierType.GreasedWheels, EconomyTier.Good }, { ModifierType.ShortMemory, EconomyTier.Good },
    };

    internal static EconomyTier TierOfModifier(ModifierType m) => ModifierTiers.TryGetValue(m, out var t) ? t : EconomyTier.Mixed;

    public static int Price(Kind kind, EconomyTier tier)
    {
        float baseCost = kind switch
        {
            Kind.Modifier => ModConfig.BlackMarketModifierPrice.Value,
            Kind.Economy => ModConfig.BlackMarketEconomyPrice.Value,
            _ => ModConfig.BlackMarketShopEconomyPrice.Value,
        };
        // Blocking something nasty is the premium service; blocking a perk is cheap.
        float tierMult = tier switch
        {
            EconomyTier.VeryBad => 2f, EconomyTier.Bad => 1.5f, EconomyTier.Mixed => 1f, EconomyTier.Good => .7f, _ => .5f,
        };
        float level = 1f + ModConfig.BlackMarketPerLevel.Value * Difficulty.LevelsCompleted;
        return Mathf.Max(1, Mathf.RoundToInt(baseCost * tierMult * level));
    }

    // ---- state ---------------------------------------------------------------------------------------

    /// <summary>Host: rebuild the ban list from the saved run stats (call when a run is loaded / a level changes).</summary>
    public static void LoadFromStats()
    {
        if (StatsManager.instance == null || !SemiFunc.IsMasterClientOrSingleplayer())
            return;
        Banned.Clear();
        foreach (var kv in StatsManager.instance.runStats)
            if (kv.Value > 0 && kv.Key.StartsWith(StatPrefix, StringComparison.Ordinal))
                Banned.Add(kv.Key.Substring(StatPrefix.Length));
        ModNetwork.Instance?.BroadcastBans(Banned);
    }

    public static void SetFromNetwork(string[] keys)
    {
        Banned.Clear();
        foreach (var k in keys)
            Banned.Add(k);
    }

    public static IEnumerable<string> All => Banned;

    /// <summary>Host: validate and charge. Returns false (and charges nothing) if it can't be bought.</summary>
    public static bool TryBuy(Kind kind, string id)
    {
        if (!SemiFunc.IsMasterClientOrSingleplayer() || !SemiFunc.RunIsShop() || !ModConfig.BlackMarketEnabled.Value)
            return false;
        if (!Describe(kind, id, out _, out var tier) || IsBanned(kind, id))
            return false;
        int price = Price(kind, tier);
        int money = SemiFunc.StatGetRunCurrency();
        if (money < price)
            return false;

        SemiFunc.StatSetRunCurrency(money - price);
        StatsManager.instance.runStats[StatPrefix + Key(kind, id)] = 1;
        Banned.Add(Key(kind, id));
        if (CurrencyUI.instance != null)
            CurrencyUI.instance.FetchCurrency();
        ModNetwork.Instance?.BroadcastBans(Banned);
        Plugin.Log.LogInfo($"[BlackMarket] Bought a block on {kind} {id} for {price}.");
        return true;
    }

    // ---- catalogue -----------------------------------------------------------------------------------

    public static bool Describe(Kind kind, string id, out string name, out EconomyTier tier)
    {
        name = id;
        tier = EconomyTier.Mixed;
        if (string.IsNullOrEmpty(id) || kind < Kind.Modifier || kind > Kind.ShopEconomy) return false;
        switch (kind)
        {
            case Kind.Modifier:
                if (id.StartsWith("stat:", StringComparison.Ordinal))
                {
                    var sm = int.TryParse(id.Substring(5), out int sid) ? StatModifiers.Get(sid) : null;
                    if (sm == null)
                        return false;
                    name = sm.Name;
                    tier = sm.Tier;
                    return true;
                }
                if (!Enum.TryParse(id, out ModifierType m) || m == ModifierType.None || !Enum.IsDefined(typeof(ModifierType), m))
                    return false;
                name = LevelInfoHud.NameOf(m);
                tier = TierOfModifier(m);
                return true;
            case Kind.Economy:
                if (!int.TryParse(id, out int e) || e < 1 || e > EconomyEvents.All.Count)
                    return false;
                name = EconomyEvents.All[e - 1].Name;
                tier = EconomyEvents.All[e - 1].Tier;
                return true;
            default:
                if (!int.TryParse(id, out int s) || s < 1 || s > ShopEconomy.All.Count)
                    return false;
                name = ShopEconomy.All[s - 1].Name;
                tier = ShopEconomy.All[s - 1].Tier;
                return true;
        }
    }

    public static List<string> Ids(Kind kind)
    {
        var ids = new List<string>();
        switch (kind)
        {
            case Kind.Modifier:
                foreach (ModifierType m in Enum.GetValues(typeof(ModifierType)))
                    if (m != ModifierType.None)
                        ids.Add(m.ToString());
                foreach (var sm in StatModifiers.All)
                    ids.Add("stat:" + sm.Id);
                break;
            case Kind.Economy:
                foreach (var e in EconomyEvents.All)
                    ids.Add(e.Id.ToString());
                break;
            default:
                foreach (var s in ShopEconomy.All)
                    ids.Add(s.Id.ToString());
                break;
        }
        return ids;
    }
}

/// <summary>
/// Black Market screen, keyboard driven so it never fights the shop's mouse: <c>B</c> opens it in the shop,
/// Left/Right change the tab, Up/Down move, PageUp/PageDown jump, Enter buys the block. Drawn with the game's
/// own HUD font like the other overlays.
/// </summary>
internal sealed class BlackMarketHud : MonoBehaviour
{
    private static readonly BlackMarket.Kind[] Tabs = { BlackMarket.Kind.Modifier, BlackMarket.Kind.Economy, BlackMarket.Kind.ShopEconomy };
    private static readonly string[] TabNamesEn = { "Surprise modifiers", "Economic surprises", "Shop economies" };
    private static readonly string[] TabNamesRu = { "Модификаторы-сюрпризы", "Экономические сюрпризы", "Экономика магазина" };
    private static bool Ru => Localization.GameLanguage.Russian;
    private static string[] TabNames => Ru ? TabNamesRu : TabNamesEn;

    private bool _open;
    private int _tab, _index;
    private string _message = "";
    private GameObject _panel;
    private TextMeshProUGUI _label;
    private NativeHudPanel _surface;

    private void Update()
    {
        bool shop = false;
        try { shop = SemiFunc.RunIsShop(); } catch { }

        if (!ModConfig.BlackMarketEnabled.Value || !shop)
        {
            _open = false;
            Hide();
            return;
        }

        if (Input.GetKeyDown(ModConfig.BlackMarketKey.Value))
            _open = !_open;
        if (Input.GetKeyDown(KeyCode.Escape)) _open = false;
        if (!_open)
        {
            Hide();
            return;
        }

        var ids = BlackMarket.Ids(Tabs[_tab]);
        if (Input.GetKeyDown(KeyCode.RightArrow)) { _tab = (_tab + 1) % Tabs.Length; _index = 0; }
        if (Input.GetKeyDown(KeyCode.LeftArrow)) { _tab = (_tab + Tabs.Length - 1) % Tabs.Length; _index = 0; }
        ids = BlackMarket.Ids(Tabs[_tab]);
        if (Input.GetKeyDown(KeyCode.DownArrow)) _index = Mathf.Min(ids.Count - 1, _index + 1);
        if (Input.GetKeyDown(KeyCode.UpArrow)) _index = Mathf.Max(0, _index - 1);
        if (Input.GetKeyDown(KeyCode.PageDown)) _index = Mathf.Min(ids.Count - 1, _index + 6);
        if (Input.GetKeyDown(KeyCode.PageUp)) _index = Mathf.Max(0, _index - 6);
        _index = Mathf.Clamp(_index, 0, Mathf.Max(0, ids.Count - 1));

        if (ids.Count > 0 && (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)))
            Buy(Tabs[_tab], ids[_index]);

        if (!Ensure())
            return;
        _label.text = Build(ids);
        _panel.SetActive(true);
        _surface.Fit();
    }

    private void Buy(BlackMarket.Kind kind, string id)
    {
        if (BlackMarket.IsBanned(kind, id))
        {
            _message = Ru ? "Уже заблокировано." : "Already blocked.";
            return;
        }
        BlackMarket.Describe(kind, id, out _, out var tier);
        if (SemiFunc.StatGetRunCurrency() < BlackMarket.Price(kind, tier))
        {
            _message = Ru ? "Не хватает денег." : "Not enough money.";
            return;
        }
        if (SemiFunc.IsMasterClientOrSingleplayer())
            _message = BlackMarket.TryBuy(kind, id) ? (Ru ? "Заблокировано." : "Blocked.") : (Ru ? "Это купить нельзя." : "Can't buy that.");
        else
        {
            ModNetwork.Instance?.RequestBan((byte)kind, id);
            _message = Ru ? "Запрос отправлен..." : "Requested...";
        }
    }

    private string Build(List<string> ids)
    {
        var sb = new StringBuilder();
        sb.Append(Ru ? "<line-height=130%><size=54><color=#FF7060>ЧЁРНЫЙ РЫНОК</color></size>   ДЕНЬГИ: $" : "<line-height=130%><size=54><color=#FF7060>BLACK MARKET</color></size>   MONEY: $").Append(SemiFunc.StatGetRunCurrency()).Append("K\n\n");
        for (int i = 0; i < Tabs.Length; i++)
            sb.Append(i == _tab ? "<color=#ffd24a>[" + TabNames[i] + "]</color> " : TabNames[i] + " ");
        sb.Append("\n\n");

        int first = Mathf.Clamp(_index - 5, 0, Mathf.Max(0, ids.Count - 11));
        for (int i = first; i < Mathf.Min(ids.Count, first + 11); i++)
        {
            BlackMarket.Describe(Tabs[_tab], ids[i], out var name, out var tier);
            name = Localization.ModifierText.T(name);
            bool banned = BlackMarket.IsBanned(Tabs[_tab], ids[i]);
            string cost = banned ? (Ru ? "<color=#7CFF7C>ЗАБЛОКИРОВАНО</color>" : "<color=#7CFF7C>BLOCKED</color>") : "$" + BlackMarket.Price(Tabs[_tab], tier) + "K";
            string color = TierColor(tier);
            sb.Append(i == _index ? "> " : "  ")
              .Append("<color=").Append(color).Append(">").Append(name.ToUpperInvariant()).Append("</color><pos=78%>").Append(cost).Append('\n');
        }
        sb.Append(Ru ? "\n<color=#888888>Влево/вправо — вкладка, вверх/вниз — выбор, Enter — купить блокировку, " : "\n<color=#888888>Left/Right tab, Up/Down choose, Enter buys a block, ")
          .Append(ModConfig.BlackMarketKey.Value).Append(Ru ? " — закрыть</color>" : " closes</color>");
        if (_message.Length > 0)
            sb.Append("\n").Append(_message);
        return sb.ToString();
    }

    private static string TierColor(EconomyTier t) => t switch
    {
        EconomyTier.VeryGood => "#5dff8a", EconomyTier.Good => "#9be37a", EconomyTier.Mixed => "#dddddd",
        EconomyTier.Bad => "#ff9f5a", _ => "#ff5a5a",
    };

    private void Hide()
    {
        if (_panel != null)
            _panel.SetActive(false);
    }

    private bool Ensure()
    {
        if (_panel != null && _label != null)
            return true;
        _surface = NativeHudPanel.Fullscreen("IHM_BlackMarket");
        if (_surface == null) return false;
        _panel = _surface.gameObject;
        _label = _surface.Label("Market", 32f, TextAlignmentOptions.TopLeft);
        _label.enableAutoSizing = true;
        _label.fontSizeMin = 22f;
        _label.fontSizeMax = 32f;
        var rt = _label.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(96f, 64f);
        rt.offsetMax = new Vector2(-96f, -64f);
        return true;
    }
    private void OnDestroy()
    {
        if (_surface != null) Destroy(_surface.gameObject);
    }

}
