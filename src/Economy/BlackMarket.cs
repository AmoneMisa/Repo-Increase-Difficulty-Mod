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
/// Black Market: in the shop, pay from the run's currency to change what gets rolled for the rest of the run.
///  - Surprise modifiers: buy a modifier's odds down or up a step at a time (x0.5, x0.25, never / x1.5, x2, x2.5;
///    see <see cref="BlackMarketRules"/>). A step back toward normal is free. A bought change can be switched off
///    and on again for free, so a crew can keep, say, "never Level Timer" and only use it when it wants.
///  - Level economy events and shop economies: block one from ever being rolled again.
/// Everything is stored in the run stats (it persists with the save and resets with a new run) and rolled out to
/// every client. Only the host rolls, so only the host's list decides; clients just display it. Requests from
/// non-hosts go to the host, which re-checks price and balance before charging. Blocks bought for a modifier before
/// the odds existed load as "never".
/// </summary>
internal static class BlackMarket
{
    public enum Kind
    {
        Modifier,
        Economy,
        ShopEconomy,
    }

    private const string StatPrefix = "ihm_ban:", OddsPrefix = "ihm_odds:", OffPrefix = "ihm_oddsoff:";
    private const int OddsStatOffset = 8; // stored as level + 8, so every saved level is positive
    private static readonly HashSet<string> Banned = new();
    private static readonly Dictionary<string, int> Odds = new();   // modifier id -> odds level
    private static readonly HashSet<string> OddsOff = new();        // modifier ids whose bought odds are switched off

    private static string Key(Kind k, string id) => (char)('a' + (int)k) + ":" + id;

    /// <summary>Never rolled: a blocked economy, or a modifier bought all the way down (and switched on).</summary>
    public static bool IsBanned(Kind k, string id) => k == Kind.Modifier ? OddsMultiplier(id) <= 0f : Banned.Contains(Key(k, id));

    public static int OddsLevel(string id) => id != null && Odds.TryGetValue(id, out int level) ? level : 0;
    public static bool OddsEnabled(string id) => id == null || !OddsOff.Contains(id);

    /// <summary>How much more or less often the roller picks this modifier (1 = untouched or switched off).</summary>
    public static float OddsMultiplier(string id) => OddsEnabled(id) ? BlackMarketRules.Multiplier(OddsLevel(id)) : 1f;

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
        { ModifierType.ThornyMonsters, EconomyTier.Bad },
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
        return Mathf.Max(1, Mathf.RoundToInt(baseCost * tierMult * DepthFactor));
    }

    private static float DepthFactor => 1f + ModConfig.BlackMarketPerLevel.Value * Difficulty.LevelsCompleted;

    /// <summary>Price of moving a modifier's odds one step: 0 = free (back toward normal), -1 = can't go further.</summary>
    public static int StepPrice(string id, bool down)
    {
        if (!Describe(Kind.Modifier, id, out _, out var tier)) return -1;
        int level = OddsLevel(id);
        if (!BlackMarketRules.CanStep(level, down)) return -1;
        if (!BlackMarketRules.StepIsPaid(level, down)) return 0;
        float price = ModConfig.BlackMarketModifierPrice.Value * BlackMarketRules.StepFactor((int)tier, down) * DepthFactor;
        return Mathf.Max(1, Mathf.RoundToInt(price));
    }

    // ---- state ---------------------------------------------------------------------------------------

    /// <summary>Host: rebuild the ban list from the saved run stats (call when a run is loaded / a level changes).</summary>
    public static void LoadFromStats()
    {
        if (StatsManager.instance == null || !SemiFunc.IsMasterClientOrSingleplayer())
            return;
        Banned.Clear(); Odds.Clear(); OddsOff.Clear();
        var stats = StatsManager.instance.runStats;
        var legacy = new List<string>();
        foreach (var kv in stats)
        {
            if (kv.Value <= 0) continue;
            if (kv.Key.StartsWith(StatPrefix, StringComparison.Ordinal))
            {
                string key = kv.Key.Substring(StatPrefix.Length);
                if (key.StartsWith("a:", StringComparison.Ordinal)) legacy.Add(key.Substring(2)); // an old modifier block
                else Banned.Add(key);
            }
            else if (kv.Key.StartsWith(OddsPrefix, StringComparison.Ordinal))
                Odds[kv.Key.Substring(OddsPrefix.Length)] = ClampLevel(kv.Value - OddsStatOffset);
            else if (kv.Key.StartsWith(OffPrefix, StringComparison.Ordinal))
                OddsOff.Add(kv.Key.Substring(OffPrefix.Length));
        }
        // A modifier blocked before the odds existed becomes "never" (once: the odds stat then takes over).
        foreach (string id in legacy)
        {
            if (!Odds.ContainsKey(id)) { Odds[id] = BlackMarketRules.MinLevel; stats[OddsPrefix + id] = BlackMarketRules.MinLevel + OddsStatOffset; }
            stats[StatPrefix + "a:" + id] = 0;
        }
        foreach (var id in new List<string>(Odds.Keys)) if (Odds[id] == 0) { Odds.Remove(id); OddsOff.Remove(id); }
        ModNetwork.Instance?.BroadcastBans(All);
    }

    private static int ClampLevel(int level) => Math.Max(BlackMarketRules.MinLevel, Math.Min(BlackMarketRules.MaxLevel, level));

    /// <summary>Client: the host's state. Blocks are kind keys ("b:3"); odds are "o:id=level"; switched-off odds "x:id".</summary>
    public static void SetFromNetwork(string[] keys)
    {
        Banned.Clear(); Odds.Clear(); OddsOff.Clear();
        foreach (var k in keys)
        {
            if (string.IsNullOrEmpty(k)) continue;
            if (k.StartsWith("o:", StringComparison.Ordinal))
            {
                int eq = k.LastIndexOf('=');
                if (eq > 2 && int.TryParse(k.Substring(eq + 1), out int level) && level != 0) Odds[k.Substring(2, eq - 2)] = ClampLevel(level);
            }
            else if (k.StartsWith("x:", StringComparison.Ordinal)) OddsOff.Add(k.Substring(2));
            else Banned.Add(k);
        }
    }

    /// <summary>Everything to send to clients (see <see cref="SetFromNetwork"/>).</summary>
    public static IEnumerable<string> All
    {
        get
        {
            var keys = new List<string>(Banned);
            foreach (var kv in Odds) keys.Add("o:" + kv.Key + "=" + kv.Value);
            foreach (var id in OddsOff) if (Odds.ContainsKey(id)) keys.Add("x:" + id);
            return keys;
        }
    }

    /// <summary>What a Black Market request did, for the buyer's message.</summary>
    public enum Result { Done, NotNow, Unknown, NoMoney, Limit, NothingToToggle }

    private static bool Open => SemiFunc.IsMasterClientOrSingleplayer() && SemiFunc.RunIsShop() && ModConfig.BlackMarketEnabled.Value;

    /// <summary>Host: move a modifier's odds one step down (rarer) or up (more often), charging if the step is paid.</summary>
    public static Result TryStep(string id, bool down)
    {
        if (!Open) return Result.NotNow;
        if (!Describe(Kind.Modifier, id, out _, out _)) return Result.Unknown;
        int price = StepPrice(id, down);
        if (price < 0) return Result.Limit;
        int money = SemiFunc.StatGetRunCurrency();
        if (money < price) return Result.NoMoney;
        if (price > 0) SemiFunc.StatSetRunCurrency(money - price);
        int level = OddsLevel(id) + (down ? -1 : 1);
        var stats = StatsManager.instance.runStats;
        stats[OddsPrefix + id] = level + OddsStatOffset;
        stats[OffPrefix + id] = 0; // a change you just paid for is switched on
        OddsOff.Remove(id);
        if (level == 0) Odds.Remove(id); else Odds[id] = level;
        Changed();
        Plugin.Log.LogInfo($"[BlackMarket] {id} odds {(down ? "down" : "up")} to {level} (x{BlackMarketRules.Multiplier(level):0.##}) for {price}.");
        return Result.Done;
    }

    /// <summary>Host: switch a modifier's bought odds off (normal odds) or back on, for free.</summary>
    public static Result TryToggle(string id)
    {
        if (!Open) return Result.NotNow;
        if (!Describe(Kind.Modifier, id, out _, out _)) return Result.Unknown;
        if (OddsLevel(id) == 0) return Result.NothingToToggle;
        bool off = OddsOff.Add(id);
        if (!off) OddsOff.Remove(id);
        StatsManager.instance.runStats[OffPrefix + id] = off ? 1 : 0;
        Changed();
        Plugin.Log.LogInfo($"[BlackMarket] {id} odds switched {(off ? "off" : "on")}.");
        return Result.Done;
    }

    private static void Changed()
    {
        if (CurrencyUI.instance != null) CurrencyUI.instance.FetchCurrency();
        ModNetwork.Instance?.BroadcastBans(All);
    }

    /// <summary>Host: validate and charge a block on a level or shop economy. Returns false (and charges nothing) if it can't be bought.</summary>
    public static bool TryBuy(Kind kind, string id)
    {
        if (!Open || kind == Kind.Modifier)
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
        Changed();
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
    private static readonly string[] TabNamesRu = { "Сюрпризы уровня", "Экономика уровня", "Цены в магазине" };
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

        bool odds = Tabs[_tab] == BlackMarket.Kind.Modifier;
        bool enter = Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);
        if (ids.Count > 0 && odds)
        {
            if (Input.GetKeyDown(KeyCode.Minus) || Input.GetKeyDown(KeyCode.KeypadMinus)) Step(ids[_index], true);
            else if (Input.GetKeyDown(KeyCode.Equals) || Input.GetKeyDown(KeyCode.Plus) || Input.GetKeyDown(KeyCode.KeypadPlus)) Step(ids[_index], false);
            else if (enter || Input.GetKeyDown(KeyCode.Space)) Toggle(ids[_index]);
        }
        else if (ids.Count > 0 && enter)
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
            _message = BlackMarket.TryBuy(kind, id) ? (Ru ? "Заблокировано." : "Blocked.") : (Ru ? "Этот эффект нельзя заблокировать." : "Can't buy that.");
        else
        {
            ModNetwork.Instance?.RequestBan((byte)kind, id);
            _message = Ru ? "Запрос отправлен..." : "Requested...";
        }
    }

    private void Step(string id, bool down)
    {
        int price = BlackMarket.StepPrice(id, down);
        if (price < 0)
        {
            _message = down ? (Ru ? "Реже уже некуда: этот сюрприз не выпадет." : "It already never rolls.")
                            : (Ru ? "Чаще уже некуда." : "Already at the highest odds.");
            return;
        }
        if (SemiFunc.StatGetRunCurrency() < price)
        {
            _message = Ru ? "Не хватает денег." : "Not enough money.";
            return;
        }
        if (SemiFunc.IsMasterClientOrSingleplayer())
            _message = Text(BlackMarket.TryStep(id, down));
        else
        {
            ModNetwork.Instance?.RequestBan((byte)BlackMarket.Kind.Modifier, id, down ? ModNetwork.MarketStepDown : ModNetwork.MarketStepUp);
            _message = Ru ? "Запрос отправлен..." : "Requested...";
        }
    }

    private void Toggle(string id)
    {
        if (BlackMarket.OddsLevel(id) == 0)
        {
            _message = Ru ? "Сначала купите шанс: «−» реже, «+» чаще." : "Buy the odds first: - rarer, + more often.";
            return;
        }
        if (SemiFunc.IsMasterClientOrSingleplayer())
            _message = Text(BlackMarket.TryToggle(id));
        else
        {
            ModNetwork.Instance?.RequestBan((byte)BlackMarket.Kind.Modifier, id, ModNetwork.MarketToggle);
            _message = Ru ? "Запрос отправлен..." : "Requested...";
        }
    }

    private static string Text(BlackMarket.Result result) => result switch
    {
        BlackMarket.Result.Done => Ru ? "Готово." : "Done.",
        BlackMarket.Result.NoMoney => Ru ? "Не хватает денег." : "Not enough money.",
        BlackMarket.Result.Limit => Ru ? "Дальше некуда." : "Can't go further.",
        BlackMarket.Result.NothingToToggle => Ru ? "Нечего переключать." : "Nothing to switch.",
        _ => Ru ? "Сейчас нельзя." : "Can't do that now.",
    };

    /// <summary>A modifier's odds as the list shows them: normal, rarer xN, never, more often xN; greyed when switched off.</summary>
    private static string OddsText(string id)
    {
        int level = BlackMarket.OddsLevel(id);
        float mult = Economy.BlackMarketRules.Multiplier(level);
        string text = level == 0 ? (Ru ? "ОБЫЧНО" : "NORMAL")
            : mult <= 0f ? (Ru ? "НИКОГДА" : "NEVER")
            : (level < 0 ? (Ru ? "РЕЖЕ ×" : "RARER ×") : (Ru ? "ЧАЩЕ ×" : "MORE ×")) + mult.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
        if (level == 0) return "<color=#888888>" + text + "</color>";
        if (!BlackMarket.OddsEnabled(id)) return "<color=#777777><s>" + text + "</s> " + (Ru ? "ВЫКЛ" : "OFF") + "</color>";
        return (level < 0 ? "<color=#7CFF7C>" : "<color=#ffb04a>") + text + "</color>";
    }

    private static string StepText(string id, bool down)
    {
        int price = BlackMarket.StepPrice(id, down);
        string sign = down ? "−" : "+";
        if (price < 0) return "<color=#555555>" + sign + " —</color>";
        if (price == 0) return sign + (Ru ? " БЕСПЛ." : " FREE");
        return sign + " $" + price + "K";
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
            if (Tabs[_tab] == BlackMarket.Kind.Modifier)
            {
                sb.Append(i == _index ? "> " : "  ")
                  .Append("<color=").Append(TierColor(tier)).Append(">").Append(name.ToUpperInvariant()).Append("</color>")
                  .Append("<pos=50%>").Append(OddsText(ids[i]))
                  .Append("<pos=72%>").Append(StepText(ids[i], true)).Append("<pos=86%>").Append(StepText(ids[i], false)).Append('\n');
                continue;
            }
            bool banned = BlackMarket.IsBanned(Tabs[_tab], ids[i]);
            string cost = banned ? (Ru ? "<color=#7CFF7C>ЗАБЛОКИРОВАНО</color>" : "<color=#7CFF7C>BLOCKED</color>") : "$" + BlackMarket.Price(Tabs[_tab], tier) + "K";
            string color = TierColor(tier);
            sb.Append(i == _index ? "> " : "  ")
              .Append("<color=").Append(color).Append(">").Append(name.ToUpperInvariant()).Append("</color><pos=78%>").Append(cost).Append('\n');
        }
        if (Tabs[_tab] == BlackMarket.Kind.Modifier)
            sb.Append(Ru ? "\n<color=#888888>Влево/вправо — вкладка, вверх/вниз — выбор, «−» — реже, «+» — чаще (шаг назад к обычному бесплатно), Пробел/Enter — вкл/выкл купленное, "
                         : "\n<color=#888888>Left/Right tab, Up/Down choose, - rarer, + more often (a step back to normal is free), Space/Enter switch it on/off, ");
        else
            sb.Append(Ru ? "\n<color=#888888>Влево/вправо — вкладка, вверх/вниз — выбор, Enter — купить блокировку, " : "\n<color=#888888>Left/Right tab, Up/Down choose, Enter buys a block, ");
        sb.Append(ModConfig.BlackMarketKey.Value).Append(Ru ? " — закрыть</color>" : " closes</color>");
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
