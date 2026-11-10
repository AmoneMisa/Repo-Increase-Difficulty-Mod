using System;
using System.Collections.Generic;
using IncreaseHardMod.Hud;
using UnityEngine;
using C = IncreaseHardMod.Skills.SkillCatalog;
using L = IncreaseHardMod.Courier.CourierManager;

namespace IncreaseHardMod.Courier;

/// <summary>
/// The courier command menu (toolkit list panel, native HUD text). The menu key opens and closes it; Up/Down move,
/// Enter or Right picks (Left/Right change a colour or animal), Left/Backspace goes back.
/// </summary>
internal sealed class CourierMenu
{
    private enum Page { Main, Truck, Ally, Look, Gacha }

    private sealed class Entry
    {
        public string Text, Note;
        public bool Enabled = true;
        public Action Pick;
        public Action<int> Step; // Left/Right on appearance rows
    }

    private HudListPanel _panel;
    private Page _page;
    private int _cursor;
    internal bool Open { get; private set; }

    internal void Close()
    {
        Open = false;
        _panel?.Hide();
    }

    internal void Tick(CourierManager manager)
    {
        bool chat = ChatManager.instance != null && ChatManager.instance.chatActive;
        if (!chat && Input.GetKeyDown(ModConfig.CourierMenuKey.Value))
        {
            Open = !Open; _page = Page.Main; _cursor = 0;
            if (!Open) { _panel?.Hide(); return; }
        }
        if (!Open) return;
        if (PlayerAvatar.instance == null || PlayerAvatar.instance.isDisabled) { Close(); return; }

        var entries = Build(manager);
        if (!chat)
        {
            if (Input.GetKeyDown(KeyCode.DownArrow)) _cursor++;
            if (Input.GetKeyDown(KeyCode.UpArrow)) _cursor--;
        }
        _cursor = entries.Count == 0 ? 0 : (_cursor % entries.Count + entries.Count) % entries.Count;
        var current = entries.Count > 0 ? entries[_cursor] : null;
        if (!chat && current != null)
        {
            if (current.Step != null)
            {
                if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.Return)) current.Step(1);
                if (Input.GetKeyDown(KeyCode.LeftArrow)) current.Step(-1);
            }
            else if ((Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.RightArrow)) && current.Enabled) current.Pick?.Invoke();
        }
        if (!chat && _page != Page.Main && (Input.GetKeyDown(KeyCode.Backspace) || (Input.GetKeyDown(KeyCode.LeftArrow) && current?.Step == null)))
        {
            _page = Page.Main; _cursor = 0;
        }
        Render(manager, Build(manager));
    }

    private void Go(Page page) { _page = page; _cursor = 0; }

    private void Render(CourierManager manager, List<Entry> entries)
    {
        _panel ??= new HudListPanel("courier", HudCorner.TopRight, order: 50) { RowsPerPage = 14 };
        string pet = CourierLook.Name(CourierLook.Local & 15).ToUpperInvariant();
        _panel.Title = _page switch
        {
            Page.Truck => L.L("FETCH FROM THE TRUCK", "ПРИНЕСТИ ИЗ МАШИНЫ"),
            Page.Ally => L.L("DELIVER TO A TEAMMATE", "ДОСТАВИТЬ СОЮЗНИКУ"),
            Page.Look => L.L("COURIER LOOK", "ВНЕШНИЙ ВИД"),
            Page.Gacha => L.L("COURIER GACHA", "ГАЧА КУРЬЕРОВ"),
            _ => CourierGacha.HasCourier ? L.L($"COURIER · {pet}", $"КУРЬЕР · {pet}") : L.L("COURIER", "КУРЬЕР"),
        };
        var rows = new List<HudListRow>();
        for (int i = 0; i < entries.Count; i++)
        {
            var e = entries[i];
            bool on = i == _cursor;
            var color = !e.Enabled ? new Color(.55f, .55f, .55f) : on ? HudStyle.Title : HudStyle.Text;
            rows.Add(new HudListRow((on ? "> " : "  ") + e.Text, e.Note ?? "", color));
        }
        if (rows.Count == 0) rows.Add(new HudListRow(L.L("  NOTHING HERE", "  ЗДЕСЬ ПУСТО"), "", new Color(.55f, .55f, .55f)));
        _panel.SetRows(rows);
        var info = manager.Infos.TryGetValue(L.LocalActor, out var i2) ? i2 : default;
        _panel.Footer = !CourierGacha.HasCourier || _page == Page.Gacha
            ? L.L($"COINS {CourierGacha.Coins}   HALVES {CourierGacha.Halves}/2   [↑↓] MOVE  [ENTER] PICK  [←] BACK",
                  $"МОНЕТЫ {CourierGacha.Coins}   ПОЛОВИНКИ {CourierGacha.Halves}/2   [↑↓] ВЫБОР  [ENTER] ОК  [←] НАЗАД")
            : info.Down
            ? L.L($"KNOCKED OUT · BACK IN {Mathf.CeilToInt(info.DownLeft)} S", $"ВЫБИТ · ВЕРНЁТСЯ ЧЕРЕЗ {Mathf.CeilToInt(info.DownLeft)} С")
            : $"HP {Mathf.CeilToInt(info.Hp)}/{Mathf.CeilToInt(info.MaxHp)}   {JobText(info.Job)}   " + L.L("[↑↓] MOVE  [ENTER] PICK  [←] BACK", "[↑↓] ВЫБОР  [ENTER] ОК  [←] НАЗАД");
        _panel.FooterColor = info.Down ? HudStyle.Bad : HudStyle.Info;
        _panel.Show();
    }

    private static string JobText(CourierJob job) => job switch
    {
        CourierJob.Charge => L.L("CHARGING", "ЗАРЯЖАЕТ"),
        CourierJob.Fetch => L.L("FETCHING", "НЕСЁТ ИЗ МАШИНЫ"),
        CourierJob.Ally => L.L("DELIVERING", "ДОСТАВЛЯЕТ"),
        CourierJob.Cart => L.L("TOWING A C.A.R.T.", "ТАЩИТ ТЕЛЕЖКУ"),
        _ => L.L("FOLLOWING", "СЛЕДУЕТ"),
    };

    // ------------------------------------------------------------------ pages

    private List<Entry> Build(CourierManager m) => _page switch
    {
        Page.Truck => TruckPage(m),
        Page.Ally => AllyPage(m),
        Page.Look => LookPage(m),
        Page.Gacha => GachaPage(m),
        _ => MainPage(m),
    };

    private static PhysGrabObject Held()
    {
        var grabber = PlayerAvatar.instance != null ? PlayerAvatar.instance.physGrabber : null;
        return grabber != null && grabber.grabbed ? grabber.grabbedPhysGrabObject : null;
    }

    private static int Lv(string node) => Skills.SkillEffects.LocalLevel(node);

    private List<Entry> MainPage(CourierManager m)
    {
        var list = new List<Entry>();
        if (!CourierGacha.HasCourier)
        {
            list.Add(new Entry { Text = L.L("NO COURIER YET: PULL THE GACHA", "КУРЬЕРА ЕЩЁ НЕТ: КРУТИТЕ ГАЧУ"), Enabled = false });
            list.Add(new Entry { Text = L.L("GACHA", "ГАЧА") + " >", Note = L.L($"{CourierGacha.Coins} COINS", $"МОНЕТ: {CourierGacha.Coins}"), Pick = () => Go(Page.Gacha) });
            return list;
        }
        var info = m.Infos.TryGetValue(L.LocalActor, out var i) ? i : default;
        bool level = SemiFunc.RunIsLevel();
        var held = Held();
        var battery = held != null ? held.GetComponent<ItemBattery>() : null;
        list.Add(new Entry
        {
            Text = L.L("CHARGE THE ITEM IN MY HANDS", "ЗАРЯДИТЬ ПРЕДМЕТ ИЗ РУК"),
            Note = !level ? L.L("LEVELS ONLY", "ТОЛЬКО НА УРОВНЕ") : battery == null ? L.L("HOLD A BATTERY ITEM", "ВОЗЬМИТЕ ПРЕДМЕТ С БАТАРЕЕЙ") : $"{Mathf.RoundToInt(battery.batteryLife)}%",
            Enabled = level && battery != null,
            Pick = () => { m.Request(CourierManager.Op.Charge, held.photonView.ViewID, 0); Close(); },
        });
        list.Add(new Entry { Text = L.L("FETCH FROM THE TRUCK", "ПРИНЕСТИ ИЗ МАШИНЫ") + " >", Note = level ? "" : L.L("LEVELS ONLY", "ТОЛЬКО НА УРОВНЕ"), Enabled = level, Pick = () => Go(Page.Truck) });
        bool ally = Lv(C.CourierAlly) > 0;
        list.Add(new Entry
        {
            Text = L.L("DELIVER MY ITEM TO A TEAMMATE", "ОТНЕСТИ ПРЕДМЕТ СОЮЗНИКУ") + " >",
            Note = !ally ? L.L("PERK", "ПЕРК") : held == null ? L.L("HOLD AN ITEM", "ВОЗЬМИТЕ ПРЕДМЕТ") : "",
            Enabled = ally && level && held != null, Pick = () => Go(Page.Ally),
        });
        bool cart = Lv(C.CourierCart) > 0;
        list.Add(new Entry { Text = L.L("BRING A C.A.R.T.", "ПРИГНАТЬ ТЕЛЕЖКУ"), Note = cart ? "" : L.L("PERK", "ПЕРК"), Enabled = cart && level, Pick = () => { m.Request(CourierManager.Op.Cart, 0, 0); Close(); } });
        bool dash = Lv(C.CourierDash) > 0;
        list.Add(new Entry
        {
            Text = L.L("DASH", "РЫВОК"),
            Note = !dash ? L.L("PERK", "ПЕРК") : info.DashLeft > 0f ? $"{Mathf.CeilToInt(info.DashLeft)} S" : L.L("READY", "ГОТОВ"),
            Enabled = dash && info.DashLeft <= 0f, Pick = () => m.Request(CourierManager.Op.Dash, 0, 0),
        });
        bool fly = Lv(C.CourierFlight) > 0;
        list.Add(new Entry
        {
            Text = L.L("FLIGHT", "ПОЛЁТ"),
            Note = !fly ? L.L("PERK", "ПЕРК") : info.Flying ? L.L("ON", "ВКЛ") : L.L("OFF", "ВЫКЛ"),
            Enabled = fly, Pick = () => m.Request(CourierManager.Op.Fly, info.Flying ? 0 : 1, 0),
        });
        // Backpack: stash what you hold, take things back out.
        var stash = info.Stash ?? new string[0];
        int slots = Mathf.Max(1, info.Slots);
        list.Add(new Entry
        {
            Text = L.L("PUT MY ITEM IN THE BACKPACK", "УБРАТЬ ПРЕДМЕТ В РЮКЗАК"),
            Note = held == null ? L.L($"HOLD AN ITEM · {stash.Length}/{slots}", $"ВОЗЬМИТЕ ПРЕДМЕТ · {stash.Length}/{slots}") : $"{stash.Length}/{slots}",
            Enabled = level && held != null && stash.Length < slots,
            Pick = () => m.Request(CourierManager.Op.Stash, held.photonView.ViewID, 0),
        });
        for (int s = 0; s < stash.Length; s++)
        {
            int index = s;
            list.Add(new Entry { Text = L.L("  TAKE OUT: ", "  ДОСТАТЬ: ") + stash[s].ToUpperInvariant(), Pick = () => m.Request(CourierManager.Op.Unstash, index, 0) });
        }
        list.Add(new Entry { Text = L.L("DROP IT AND FOLLOW ME", "БРОСЬ ВСЁ И ЗА МНОЙ"), Pick = () => { m.Request(CourierManager.Op.Follow, 0, 0); Close(); } });
        list.Add(new Entry { Text = L.L("GACHA", "ГАЧА") + " >", Note = L.L($"{CourierGacha.Coins} COINS", $"МОНЕТ: {CourierGacha.Coins}"), Pick = () => Go(Page.Gacha) });
        list.Add(new Entry { Text = L.L("APPEARANCE", "ВНЕШНИЙ ВИД") + " >", Pick = () => Go(Page.Look) });
        return list;
    }

    private List<Entry> TruckPage(CourierManager m)
    {
        var list = new List<Entry>();
        float cap = CourierRules.MaxLoad(Lv(C.CourierLoad));
        foreach (var attributes in UnityEngine.Object.FindObjectsOfType<ItemAttributes>())
        {
            if (list.Count >= 24) break;
            var grab = attributes.GetComponent<PhysGrabObject>();
            var rooms = attributes.GetComponent<RoomVolumeCheck>();
            if (grab == null || rooms == null || !rooms.inTruck || grab.grabbed || attributes.GetComponent<ValuableObject>() != null ||
                attributes.GetComponent<PhysGrabCart>() != null || grab.photonView == null) continue;
            float mass = grab.rb != null ? grab.rb.mass : 0f;
            int view = grab.photonView.ViewID;
            bool light = mass <= cap + .01f;
            list.Add(new Entry
            {
                Text = Quests.QuestRewards.Label(attributes.item).ToUpperInvariant(),
                Note = light ? $"{mass:0.#} KG" : L.L($"{mass:0.#} KG · TOO HEAVY", $"{mass:0.#} КГ · ТЯЖЕЛО"),
                Enabled = light, Pick = () => { m.Request(CourierManager.Op.Fetch, view, 0); Close(); },
            });
        }
        return list;
    }

    private List<Entry> AllyPage(CourierManager m)
    {
        var list = new List<Entry>();
        var held = Held();
        var players = GameDirector.instance != null ? GameDirector.instance.PlayerList : null;
        if (players == null || held == null) return list;
        foreach (var p in players)
        {
            if (p == null || p == PlayerAvatar.instance || p.isDisabled) continue;
            int actor = CourierManager.ActorOf(p);
            int view = held.photonView.ViewID;
            float d = Vector3.Distance(p.transform.position, PlayerAvatar.instance.transform.position);
            list.Add(new Entry
            {
                Text = (string.IsNullOrEmpty(p.playerName) ? "PLAYER" : p.playerName).ToUpperInvariant(), Note = $"{d:0} M",
                Pick = () => { m.Request(CourierManager.Op.Ally, view, actor); Close(); },
            });
        }
        return list;
    }

    private List<Entry> LookPage(CourierManager m)
    {
        CourierRules.UnpackLook(CourierLook.Local, out int animal, out int main, out int second, out int accent);
        bool here = CanRestyle();
        string Lock(string v) => here ? $"< {v} >" : L.L("AT THE TRUCK", "У МАШИНЫ");
        void Set(int a, int x, int y, int z) { if (here) CourierLook.Local = CourierRules.PackLook(a, x, y, z); }
        return new List<Entry>
        {
            new() { Text = L.L("ANIMAL", "ЖИВОТНОЕ"), Note = Lock(CourierLook.Name(animal).ToUpperInvariant()), Enabled = here,
                Step = s => Set((animal + s + CourierRules.Animals) % CourierRules.Animals, main, second, accent) },
            new() { Text = L.L("MAIN COLOUR", "ОСНОВНОЙ ЦВЕТ"), Note = Lock(CourierLook.SwatchName(main).ToUpperInvariant()), Enabled = here,
                Step = s => Set(animal, CourierRules.NextSwatch(main, s), second, accent) },
            new() { Text = L.L("SECOND COLOUR", "ДОП. ЦВЕТ"), Note = Lock(CourierLook.SwatchName(second).ToUpperInvariant()), Enabled = here,
                Step = s => Set(animal, main, CourierRules.NextSwatch(second, s), accent) },
            new() { Text = L.L("ACCESSORIES", "АКСЕССУАРЫ"), Note = Lock(CourierLook.SwatchName(accent).ToUpperInvariant()), Enabled = here,
                Step = s => Set(animal, main, second, CourierRules.NextSwatch(accent, s)) },
        };
    }

    private List<Entry> GachaPage(CourierManager m)
    {
        var list = new List<Entry>
        {
            new()
            {
                Text = L.L("PULL (1 COIN)", "КРУТИТЬ (1 МОНЕТА)"),
                Note = CourierGacha.Coins > 0 ? L.L($"{CourierGacha.Coins} COINS", $"МОНЕТ: {CourierGacha.Coins}") : L.L("NO COINS: FIND THEM ON LEVELS", "НЕТ МОНЕТ: ИЩИТЕ НА УРОВНЯХ"),
                Enabled = CourierGacha.Coins > 0,
                Pick = Pull,
            },
        };
        int total = 0; foreach (int w in CourierRules.GachaWeights) total += w;
        for (int a = 0; a < CourierRules.Animals; a++)
        {
            int chance = Mathf.RoundToInt(100f * CourierRules.GachaWeights[a] / total);
            bool owned = CourierGacha.Owns(a);
            bool active = owned && (CourierLook.Local & 15) == a;
            int animal = a;
            list.Add(new Entry
            {
                Text = "  " + CourierLook.Name(a).ToUpperInvariant(),
                Note = owned ? (active ? L.L("ACTIVE", "АКТИВЕН") : L.L("OWNED · PICK TO USE", "ЕСТЬ · ВЫБРАТЬ")) : L.L($"LOCKED · {chance}%", $"ЗАКРЫТ · {chance}%"),
                Enabled = owned && !active,
                Pick = () =>
                {
                    CourierRules.UnpackLook(CourierLook.Local, out _, out int x, out int y, out int z);
                    CourierLook.Local = CourierRules.PackLook(animal, x, y, z);
                },
            });
        }
        return list;
    }

    private static void Pull()
    {
        if (!CourierGacha.TryPull(out int animal, out bool isNew)) return;
        string name = CourierLook.Name(animal).ToUpperInvariant();
        if (isNew)
            HudNotify.Toast(L.L($"NEW COURIER: {name}!", $"НОВЫЙ КУРЬЕР: {name}!"), HudStyle.Title, 5f, "courier_gacha");
        else
            HudNotify.Toast(L.L($"DUPLICATE {name}: +1/2 COIN", $"ПОВТОР {name}: +1/2 МОНЕТЫ"), HudStyle.Info, 4f, "courier_gacha");
        try
        {
            var at = PlayerAvatar.instance.transform.position;
            Visuals.ItemAudio.Cue("courier_find", .5f).Play(at);
            Visuals.ItemAudio.Cue($"courier_{CourierLook.Voice(animal)}_happy1", .55f).Play(at);
        }
        catch { }
    }

    /// <summary>Skins and colours change in the lobby, the shop or next to the truck.</summary>
    private static bool CanRestyle()
    {
        if (SemiFunc.RunIsLobby() || SemiFunc.RunIsShop()) return true;
        var me = PlayerAvatar.instance;
        var rooms = me != null ? me.GetComponent<RoomVolumeCheck>() : null;
        if (rooms != null && rooms.inTruck) return true;
        try
        {
            var truck = LevelGenerator.Instance != null && LevelGenerator.Instance.LevelPathTruck != null ? LevelGenerator.Instance.LevelPathTruck.transform.position : Vector3.zero;
            return me != null && truck != Vector3.zero && Vector3.Distance(me.transform.position, truck) < 10f;
        }
        catch { return false; }
    }
}
