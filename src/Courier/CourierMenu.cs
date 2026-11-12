using System;
using System.Collections.Generic;
using IncreaseHardMod.Hud;
using IncreaseHardMod.Patches.Effects;
using UnityEngine;
using C = IncreaseHardMod.Skills.SkillCatalog;
using L = IncreaseHardMod.Courier.CourierManager;

namespace IncreaseHardMod.Courier;

/// <summary>
/// The courier command menu (CourierMenuView: a plated panel at the top right with the pet's portrait, a grid of
/// command tiles, sub-page rows and the gacha's pet cards; native HUD text). The menu key opens and closes it; the
/// arrows move through the grid, Enter picks (Left/Right change a colour or animal on the look page), Backspace -
/// or Left at the edge of a list - goes back.
/// </summary>
internal sealed class CourierMenu
{
    private enum Page { Main, Truck, Ally, Look, Gacha, More, Backpack }

    private sealed class Entry
    {
        public string Text, Note;
        public bool Enabled = true, Locked, Active;
        public CourierItemKind Kind = CourierItemKind.Row;
        public Sprite Icon;
        public Action Pick;
        public Action<int> Step; // Left/Right on appearance rows
    }

    private readonly CourierMenuView _view = new();
    private Page _page;
    private int _cursor;
    private PhysGrabObject _aimedChargeItem;
    internal bool Open { get; private set; }

    internal void Close()
    {
        Open = false;
        _view.Hide();
    }

    internal void Tick(CourierManager manager)
    {
        bool chat = ChatManager.instance != null && ChatManager.instance.chatActive;
        if (!chat && Input.GetKeyDown(ModConfig.CourierMenuKey.Value))
        {
            Open = !Open; _page = Page.Main; _cursor = 0;
            if (!Open) { _view.Hide(); return; }
        }
        if (!Open) return;
        if (PlayerAvatar.instance == null || PlayerAvatar.instance.isDisabled) { Close(); return; }

        _aimedChargeItem = _page == Page.Main && CourierGacha.HasCourier && SemiFunc.RunIsLevel() && Held() == null
            ? AimedChargeItem() : null;
        var entries = Build(manager);
        _cursor = entries.Count == 0 ? 0 : Mathf.Clamp(_cursor, 0, entries.Count - 1);
        var rows = Rows(entries);
        Locate(rows, _cursor, out int row, out int col);
        var current = entries.Count > 0 ? entries[_cursor] : null;
        bool back = false;
        if (!chat && entries.Count > 0)
        {
            if (Input.GetKeyDown(KeyCode.DownArrow)) _cursor = Pick(rows, row + 1, col, rows.Count);
            else if (Input.GetKeyDown(KeyCode.UpArrow)) _cursor = Pick(rows, row - 1, col, rows.Count);
            else if (current?.Step != null && (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.LeftArrow)))
                current.Step(Input.GetKeyDown(KeyCode.RightArrow) ? 1 : -1);
            else if (Input.GetKeyDown(KeyCode.RightArrow) && col + 1 < rows[row].Count) _cursor = rows[row][col + 1];
            else if (Input.GetKeyDown(KeyCode.LeftArrow))
            {
                if (col > 0) _cursor = rows[row][col - 1];
                else back = _page != Page.Main;
            }
            else if (Input.GetKeyDown(KeyCode.Return))
            {
                if (current?.Step != null) current.Step(1);
                else if (current != null && current.Enabled) current.Pick?.Invoke();
            }
        }
        if (!chat && (back || (_page != Page.Main && Input.GetKeyDown(KeyCode.Backspace))))
        {
            _page = _page is Page.Look or Page.Gacha ? Page.More : Page.Main; _cursor = 0;
        }
        if (!Open) return;
        Render(manager, Build(manager));
    }

    private void Go(Page page) { _page = page; _cursor = 0; }

    // Rows of the page as the view lays them out: tiles two to a row, pet cards four to a row, links share one row,
    // every plain row on its own.
    private static List<List<int>> Rows(List<Entry> entries)
    {
        var rows = new List<List<int>>();
        List<int> open = null; CourierItemKind? kind = null;
        for (int i = 0; i < entries.Count; i++)
        {
            var k = entries[i].Kind;
            int width = k switch { CourierItemKind.Tile => 2, CourierItemKind.Card => 4, CourierItemKind.Link => 8, _ => 1 };
            if (open == null || kind != k || open.Count >= width) { open = new List<int>(); rows.Add(open); kind = k; }
            open.Add(i);
        }
        return rows;
    }

    private static void Locate(List<List<int>> rows, int index, out int row, out int col)
    {
        for (row = 0; row < rows.Count; row++) { col = rows[row].IndexOf(index); if (col >= 0) return; }
        row = 0; col = 0;
    }

    private static int Pick(List<List<int>> rows, int row, int col, int count)
    {
        row = (row % count + count) % count;
        var r = rows[row];
        return r[Mathf.Clamp(col, 0, r.Count - 1)];
    }

    private void Render(CourierManager manager, List<Entry> entries)
    {
        var info = manager.Infos.TryGetValue(L.LocalActor, out var i2) ? i2 : default;
        int animal = CourierLook.Local & 15;
        string pet = CourierLook.Name(animal).ToUpperInvariant();
        bool has = CourierGacha.HasCourier;
        var header = new CourierMenuView.Header
        {
            Title = _page switch
            {
                Page.Truck => L.L("FROM THE TRUCK", "ИЗ МАШИНЫ"),
                Page.Ally => L.L("TO A TEAMMATE", "СОЮЗНИКУ"),
                Page.Look => L.L("APPEARANCE", "ВНЕШНИЙ ВИД"),
                Page.Gacha => L.L("COURIER GACHA", "ГАЧА КУРЬЕРОВ"),
                Page.More => L.L("MORE", "ДОПОЛНИТЕЛЬНО"),
                Page.Backpack => L.L("BACKPACK", "РЮКЗАК"),
                _ => has ? L.L($"COURIER · {pet}", $"КУРЬЕР · {pet}") : L.L("COURIER", "КУРЬЕР"),
            },
            Portrait = has ? CourierPortraits.Get(animal) : null,
            Status = !has ? L.L("NO COURIER YET", "КУРЬЕРА ЕЩЁ НЕТ") : info.Down
                ? L.L($"KNOCKED OUT · {Mathf.CeilToInt(info.DownLeft)} S", $"ВЫБИТ · {Mathf.CeilToInt(info.DownLeft)} С")
                : JobText(info.Job),
            StatusColor = info.Down ? HudStyle.Bad : HudStyle.Info,
            Health = has ? $"{Mathf.CeilToInt(info.Hp)}/{Mathf.CeilToInt(info.MaxHp)}" : null,
            Pack = has ? $"{(info.Stash ?? new string[0]).Length}/{Mathf.Max(1, info.Slots)}" : null,
            Coins = _page == Page.Gacha || !has
                ? L.L($"COINS {CourierGacha.Coins}   HALVES {CourierGacha.Halves}/2", $"МОНЕТЫ {CourierGacha.Coins}   ПОЛОВИНКИ {CourierGacha.Halves}/2")
                : null,
            Hint = Hint(_page != Page.Main),
        };
        var items = new List<CourierMenuView.Item>();
        for (int i = 0; i < entries.Count; i++)
        {
            var e = entries[i];
            items.Add(new CourierMenuView.Item
            {
                Kind = e.Kind, Text = e.Text, Note = e.Note, Icon = e.Icon, Enabled = e.Enabled, Locked = e.Locked,
                Active = e.Active, Selected = i == _cursor,
            });
        }
        if (items.Count == 0) items.Add(new CourierMenuView.Item { Text = L.L("NOTHING HERE", "ЗДЕСЬ ПУСТО"), Enabled = false });
        // Long lists scroll so the cursor stays in view.
        int first = items.Count > 11 && items.TrueForAll(x => x.Kind == CourierItemKind.Row) ? Mathf.Clamp(_cursor - 5, 0, items.Count - 11) : 0;
        _view.Show(header, items, first);
    }

    private static string Hint(bool back)
    {
        string key = ModConfig.CourierMenuKey.Value.ToString().ToUpperInvariant();
        string gold = HudStyle.Hex(HudStyle.Title);
        return Localization.GameLanguage.Russian
            ? $"<color={gold}>{key}</color> — закрыть   <color={gold}>↑↓←→</color> — выбрать   <color={gold}>ENTER</color> — команда" + (back ? $"   <color={gold}>BACKSPACE</color> — назад" : "")
            : $"<color={gold}>{key}</color> close   <color={gold}>↑↓←→</color> select   <color={gold}>ENTER</color> command" + (back ? $"   <color={gold}>BACKSPACE</color> back" : "");
    }

    private static string JobText(CourierJob job) => job switch
    {
        CourierJob.Charge => L.L("CHARGING", "ЗАРЯЖАЕТ"),
        CourierJob.Fetch => L.L("FETCHING", "НЕСЁТ ИЗ МАШИНЫ"),
        CourierJob.Ally => L.L("DELIVERING", "ДОСТАВЛЯЕТ"),
        CourierJob.Cart => L.L("TOWING A C.A.R.T.", "ТАЩИТ ТЕЛЕЖКУ"),
        CourierJob.Store => L.L("HEADING TO THE TRUCK", "НЕСЁТ В МАШИНУ"),
        CourierJob.Head => L.L("RETRIEVING A HEAD", "ЗАБИРАЕТ ГОЛОВУ"),
        _ => L.L("FOLLOWING", "СЛЕДУЕТ"),
    };

    // ------------------------------------------------------------------ pages

    private List<Entry> Build(CourierManager m) => _page switch
    {
        Page.Truck => TruckPage(m),
        Page.Ally => AllyPage(m),
        Page.Look => LookPage(m),
        Page.Gacha => GachaPage(m),
        Page.More => MorePage(m),
        Page.Backpack => BackpackPage(m),
        _ => MainPage(m),
    };

    private static PhysGrabObject Held()
    {
        var grabber = PlayerAvatar.instance != null ? PlayerAvatar.instance.physGrabber : null;
        return grabber != null && grabber.grabbed ? grabber.grabbedPhysGrabObject : null;
    }

    private static PhysGrabObject AimedChargeItem()
    {
        var camera = PingController.ActiveCamera();
        if (camera == null) return null;
        var ray = camera.ViewportPointToRay(new Vector3(.5f, .5f, 0f));
        if (!PingController.TryAim(ray, CourierRules.ChargeTargetRange, out var hit)) return null;
        var item = hit.collider.GetComponentInParent<PhysGrabObject>();
        return item != null && !item.grabbed ? item : null;
    }

    private static int Lv(string node) => Skills.SkillEffects.LocalLevel(node);

    private List<Entry> MainPage(CourierManager m)
    {
        var list = new List<Entry>();
        if (!CourierGacha.HasCourier)
        {
            list.Add(new Entry { Text = L.L("PULL THE GACHA TO GET A COURIER", "КРУТИТЕ ГАЧУ, ЧТОБЫ ПОЛУЧИТЬ КУРЬЕРА"), Enabled = false });
            list.Add(new Entry { Text = L.L("GACHA", "ГАЧА") + " ›", Note = L.L($"{CourierGacha.Coins} COINS", $"МОНЕТ: {CourierGacha.Coins}"), Pick = () => Go(Page.Gacha) });
            return list;
        }
        bool level = SemiFunc.RunIsLevel();
        string levelsOnly = L.L("LEVELS ONLY", "ТОЛЬКО НА УРОВНЕ");
        var held = Held();
        var chargeItem = held != null ? held : _aimedChargeItem;
        var battery = chargeItem != null ? chargeItem.GetComponent<ItemBattery>() : null;
        int chargeView = battery != null && chargeItem.photonView != null ? chargeItem.photonView.ViewID : 0;
        bool pickUp = held == null;
        list.Add(new Entry
        {
            Kind = CourierItemKind.Tile, Icon = CourierIcons.Bolt, Text = L.L("CHARGE", "ЗАРЯДИТЬ"),
            Note = !level ? levelsOnly : battery == null ? L.L("AIM AT A BATTERY", "НАВЕДИТЕСЬ НА БАТАРЕЮ") : $"{Mathf.RoundToInt(battery.batteryLife)}%",
            Enabled = level && chargeView != 0 && battery.batteryLife < 100f,
            Pick = () => { m.Request(CourierManager.Op.Charge, chargeView, pickUp ? 1 : 0); Close(); },
        });
        list.Add(new Entry { Kind = CourierItemKind.Tile, Icon = CourierIcons.Crate, Text = L.L("FROM TRUCK", "ИЗ МАШИНЫ"), Note = level ? "" : levelsOnly, Enabled = level, Pick = () => Go(Page.Truck) });
        int heldView = held != null && held.photonView != null ? held.photonView.ViewID : 0;
        list.Add(new Entry
        {
            Kind = CourierItemKind.Tile, Icon = CourierIcons.Store, Text = L.L("TO TRUCK", "В МАШИНУ"),
            Note = !level ? levelsOnly : heldView == 0 ? L.L("HOLD AN ITEM", "ВОЗЬМИТЕ ВЕЩЬ") : "",
            Enabled = level && heldView != 0,
            Pick = () => { m.Request(CourierManager.Op.Store, heldView, 0); Close(); },
        });
        bool ally = Lv(C.CourierAlly) > 0;
        list.Add(new Entry
        {
            Kind = CourierItemKind.Tile, Icon = CourierIcons.People, Text = L.L("TEAMMATE", "СОЮЗНИКУ"), Locked = !ally,
            Note = !ally ? L.L("PERK", "ПЕРК") : held == null ? L.L("HOLD AN ITEM", "ВОЗЬМИТЕ ВЕЩЬ") : "",
            Enabled = ally && level && held != null, Pick = () => Go(Page.Ally),
        });
        bool cart = Lv(C.CourierCart) > 0;
        list.Add(new Entry
        {
            Kind = CourierItemKind.Tile, Icon = CourierIcons.Cart, Text = L.L("C.A.R.T.", "ТЕЛЕЖКА"), Locked = !cart,
            Note = cart ? (level ? "" : levelsOnly) : L.L("PERK", "ПЕРК"), Enabled = cart && level,
            Pick = () => { m.Request(CourierManager.Op.Cart, 0, 0); Close(); },
        });
        list.Add(new Entry { Kind = CourierItemKind.Tile, Icon = CourierIcons.Backpack, Text = L.L("BACKPACK", "РЮКЗАК"), Pick = () => Go(Page.Backpack) });
        list.Add(new Entry { Kind = CourierItemKind.Link, Text = L.L("MORE", "ДОПОЛНИТЕЛЬНО"), Pick = () => Go(Page.More) });
        list.Add(new Entry { Kind = CourierItemKind.Link, Text = L.L("FOLLOW ME", "КО МНЕ"), Pick = () => { m.Request(CourierManager.Op.Follow, 0, 0); Close(); } });
        return list;
    }

    // Everything that does not fit the six tiles: heads, dash, flight, the gacha and the look.
    private List<Entry> MorePage(CourierManager m)
    {
        var list = new List<Entry>();
        var info = m.Infos.TryGetValue(L.LocalActor, out var i) ? i : default;
        bool level = SemiFunc.RunIsLevel();
        void Head(string perk, CourierManager.Op op, string text)
        {
            bool unlocked = Lv(perk) > 0;
            list.Add(new Entry
            {
                Text = text, Locked = !unlocked,
                Note = !unlocked ? L.L("PERK", "ПЕРК") : !level ? L.L("LEVELS ONLY", "ТОЛЬКО НА УРОВНЕ") : "",
                Enabled = unlocked && level, Pick = () => { m.Request(op, 0, 0); Close(); },
            });
        }
        Head(C.CourierHeadTrail, CourierManager.Op.HeadTrail, L.L("SHOW THE PATH TO A HEAD", "ПОКАЗАТЬ ПУТЬ К ГОЛОВЕ"));
        Head(C.CourierHeadFetch, CourierManager.Op.HeadFetch, L.L("BRING A HEAD", "ПРИНЕСТИ ГОЛОВУ"));
        bool dash = Lv(C.CourierDash) > 0;
        list.Add(new Entry
        {
            Text = L.L("DASH", "РЫВОК"), Locked = !dash,
            Note = !dash ? L.L("PERK", "ПЕРК") : info.DashLeft > 0f ? $"{Mathf.CeilToInt(info.DashLeft)} S" : L.L("READY", "ГОТОВ"),
            Enabled = dash && info.DashLeft <= 0f, Pick = () => m.Request(CourierManager.Op.Dash, 0, 0),
        });
        bool fly = Lv(C.CourierFlight) > 0;
        list.Add(new Entry
        {
            Text = L.L("FLIGHT", "ПОЛЁТ"), Locked = !fly,
            Note = !fly ? L.L("PERK", "ПЕРК") : info.Flying ? L.L("ON", "ВКЛ") : L.L("OFF", "ВЫКЛ"),
            Enabled = fly, Pick = () => m.Request(CourierManager.Op.Fly, info.Flying ? 0 : 1, 0),
        });
        list.Add(new Entry { Text = L.L("GACHA", "ГАЧА") + " ›", Note = L.L($"{CourierGacha.Coins} COINS", $"МОНЕТ: {CourierGacha.Coins}"), Pick = () => Go(Page.Gacha) });
        list.Add(new Entry { Text = L.L("APPEARANCE", "ВНЕШНИЙ ВИД") + " ›", Pick = () => Go(Page.Look) });
        return list;
    }

    // Backpack: stash what you hold, take things back out.
    private List<Entry> BackpackPage(CourierManager m)
    {
        var list = new List<Entry>();
        var info = m.Infos.TryGetValue(L.LocalActor, out var i) ? i : default;
        bool level = SemiFunc.RunIsLevel();
        var held = Held();
        var stash = info.Stash ?? new string[0];
        int slots = Mathf.Max(1, info.Slots);
        list.Add(new Entry
        {
            Text = L.L("PUT MY ITEM IN", "УБРАТЬ ПРЕДМЕТ"),
            Note = held == null ? L.L($"HOLD AN ITEM · {stash.Length}/{slots}", $"ВОЗЬМИТЕ ПРЕДМЕТ · {stash.Length}/{slots}") : $"{stash.Length}/{slots}",
            Enabled = level && held != null && stash.Length < slots,
            Pick = () => m.Request(CourierManager.Op.Stash, held.photonView.ViewID, 0),
        });
        for (int s = 0; s < stash.Length; s++)
        {
            int index = s;
            list.Add(new Entry { Text = L.L("TAKE OUT: ", "ДОСТАТЬ: ") + stash[s].ToUpperInvariant(), Pick = () => m.Request(CourierManager.Op.Unstash, index, 0) });
        }
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
                Note = CourierGacha.Coins > 0 ? L.L($"{CourierGacha.Coins} COINS", $"МОНЕТ: {CourierGacha.Coins}") : L.L("FIND COINS ON LEVELS", "МОНЕТЫ НАХОДЯТСЯ НА УРОВНЯХ"),
                Enabled = CourierGacha.Coins > 0,
                Pick = Pull,
            },
        };
        int total = 0; foreach (int w in CourierRules.GachaWeights) total += w;
        // Every pet as a card with its portrait, like the game's cosmetics list: owned ones in colour (pick one to
        // use it), the rest dark with a lock and their pull chance.
        for (int a = 0; a < CourierRules.Animals; a++)
        {
            int chance = Mathf.RoundToInt(100f * CourierRules.GachaWeights[a] / total);
            bool owned = CourierGacha.Owns(a);
            bool active = owned && CourierGacha.HasCourier && (CourierLook.Local & 15) == a;
            int animal = a;
            list.Add(new Entry
            {
                Kind = CourierItemKind.Card, Icon = CourierPortraits.Get(a), Text = CourierLook.Name(a).ToUpperInvariant(),
                Note = owned ? (active ? L.L("ACTIVE", "АКТИВЕН") : L.L("OWNED", "ЕСТЬ")) : $"{chance}%",
                Locked = !owned, Active = active, Enabled = owned,
                Pick = () =>
                {
                    if (active) return;
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
