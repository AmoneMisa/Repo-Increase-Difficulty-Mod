using System.Collections.Generic;
using ExitGames.Client.Photon;
using IncreaseHardMod.Hud;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

namespace IncreaseHardMod.Courier;

/// <summary>
/// Courier pets for every player. The host simulates them all (<see cref="CourierSim"/>) and broadcasts their state
/// ten times a second (event 163); every client draws them (<see cref="CourierVisual"/>), lets its player pet any
/// courier by grabbing it (like the game's Elsa) and sends commands from the courier menu (event 162, checked by the host).
/// </summary>
internal sealed class CourierManager : MonoBehaviour
{
    internal const byte StateCode = 163, RequestCode = 162;
    internal enum Op : byte { Pet = 1, Charge = 2, Fetch = 3, Ally = 4, Cart = 5, Dash = 6, Fly = 7, Follow = 8, Look = 9, Stash = 10, Unstash = 11, Coin = 12, Store = 13, HeadTrail = 14, HeadFetch = 15 }

    internal static CourierManager Instance;

    /// <summary>What every client knows about each courier (from the host's state).</summary>
    internal struct Info { public float Hp, MaxHp, DashLeft, DownLeft; public CourierJob Job; public bool Flying, Down; public int Slots; public string[] Stash; }

    private readonly Dictionary<int, CourierSim> _sims = new();      // host
    private readonly Dictionary<int, int> _looks = new();            // host: actor -> look
    private readonly Dictionary<int, CourierVisual> _visuals = new(); // everyone
    internal readonly Dictionary<int, Info> Infos = new();            // everyone
    private float _nextSend, _lastState;
    private Object _level;
    private int _sentLook = int.MinValue;
    private readonly CourierMenu _menu = new();
    private CourierHeadRoute _headRoute;

    private void Awake() { Instance = this; CourierGacha.GiveStarterCoin(); }

    // Gacha coin hidden on the level (host decides; everyone sees it).
    private int _coinId;
    private bool _coinActive;
    private Vector3 _coinPos;
    private CourierCoin _coinVisual;
    private float _nextClaim;

    internal static int ActorOf(PlayerAvatar p) => p != null && p.photonView != null ? p.photonView.OwnerActorNr : 0;
    internal static int LocalActor => ActorOf(PlayerAvatar.instance);
    internal static bool Ru => Localization.GameLanguage.Russian;
    internal static string L(string en, string ru) => Ru ? ru : en;

    private static bool InWorld()
    {
        try
        {
            return LevelGenerator.Instance != null && LevelGenerator.Instance.Generated && PlayerAvatar.instance != null &&
                   (SemiFunc.RunIsLevel() || SemiFunc.RunIsShop() || SemiFunc.RunIsLobby());
        }
        catch { return false; }
    }

    private void Update()
    {
        if (!InWorld())
        {
            if (_sims.Count > 0 || _visuals.Count > 0) ClearAll();
            _menu.Close();
            return;
        }
        var level = (Object)LevelGenerator.Instance;
        if (level != _level)
        {
            ClearAll(); _level = level; _sentLook = int.MinValue; _coinHinted = false;
            if (SemiFunc.IsMasterClientOrSingleplayer()) PlaceCoin();
        }
        CoinReminder();

        // Tell the host what my courier looks like (once per level, and whenever I change it); -1 = no courier yet.
        int look = CourierGacha.LookToSend();
        if (look != _sentLook) { _sentLook = look; Request(Op.Look, look, 0); }

        if (SemiFunc.IsMasterClientOrSingleplayer())
        {
            if (ModConfig.CourierEnabled.Value) HostTick();
            else if (_sims.Count > 0) ClearAll();
        }
        else if (Time.time - _lastState > 3f && _visuals.Count > 0) ClearAll(); // the host stopped sending (courier off)

        PetInput();
        CoinTick();
        _menu.Tick(this);
    }

    private bool _coinHinted;

    /// <summary>
    /// Once per level (lobby, shop and levels), when the HUD is up: if this player has gacha coins, say how many and
    /// which key opens the courier menu to spend them. Coins used to be invisible until the menu was opened, and the
    /// starter coin is given silently.
    /// </summary>
    private void CoinReminder()
    {
        if (_coinHinted || !ModConfig.CourierEnabled.Value || !Patches.Effects.LevelInfoHud.GameplayReady) return;
        _coinHinted = true;
        int coins = CourierGacha.Coins;
        if (coins <= 0) return;
        string key = ModConfig.CourierMenuKey.Value.ToString();
        HudNotify.Toast(L($"{coins} GACHA COIN{(coins == 1 ? "" : "S")} TO SPEND: PRESS [{key}] TO PULL A COURIER",
            $"МОНЕТ ГАЧИ: {coins}. НАЖМИТЕ [{key}], ЧТОБЫ ПОЛУЧИТЬ КУРЬЕРА"), HudStyle.Title, 6f, "courier_coin_hint");
    }

    private void ClearAll()
    {
        _headRoute?.Clear();
        foreach (var v in _visuals.Values) if (v != null) Destroy(v.gameObject);
        _visuals.Clear(); _sims.Clear(); Infos.Clear();
        _coinActive = false;
        if (_coinVisual != null) Destroy(_coinVisual.gameObject);
    }

    // ------------------------------------------------------------------ host

    private static Vector3 TruckPoint()
    {
        try
        {
            if (LevelGenerator.Instance != null && LevelGenerator.Instance.LevelPathTruck != null) return LevelGenerator.Instance.LevelPathTruck.transform.position;
            if (TruckSafetySpawnPoint.instance != null) return TruckSafetySpawnPoint.instance.transform.position;
        }
        catch { }
        return PlayerAvatar.instance != null ? PlayerAvatar.instance.transform.position : Vector3.zero;
    }

    private void HostTick()
    {
        var players = GameDirector.instance != null ? GameDirector.instance.PlayerList : null;
        if (players == null) return;
        var present = new HashSet<int>();
        foreach (var p in players)
        {
            if (p == null) continue;
            int actor = ActorOf(p);
            if (!_looks.TryGetValue(actor, out int owned) || owned < 0) continue; // no courier unlocked yet
            present.Add(actor);
            if (!_sims.TryGetValue(actor, out var sim))
            {
                var start = CourierSim.Ground(p.transform.position - p.transform.forward * 1.2f);
                _sims[actor] = sim = new CourierSim(actor, p, start, SemiFunc.RunIsLevel());
            }
            sim.Owner = p;
        }
        var gone = new List<int>();
        foreach (var actor in _sims.Keys) if (!present.Contains(actor)) gone.Add(actor);
        foreach (var actor in gone) { _sims[actor].Cancel(); _sims[actor].UnstashAll(); _sims.Remove(actor); }

        var truck = TruckPoint();
        float dt = Time.deltaTime;
        foreach (var sim in _sims.Values)
        {
            var notice = sim.Tick(dt, truck);
            if (notice.HasValue) Notify(sim.Actor, notice.Value.Item1, notice.Value.Item2);
        }

        // Draw locally from the simulation, and send the same state to everyone else.
        var actors = new int[_sims.Count]; var floats = new float[_sims.Count * 8]; var bytes = new byte[_sims.Count * 5]; var looks = new int[_sims.Count];
        var stash = new string[_sims.Count];
        int i = 0;
        foreach (var sim in _sims.Values)
        {
            actors[i] = sim.Actor;
            floats[i * 8] = sim.Pos.x; floats[i * 8 + 1] = sim.Pos.y; floats[i * 8 + 2] = sim.Pos.z; floats[i * 8 + 3] = sim.Yaw;
            floats[i * 8 + 4] = sim.Hp; floats[i * 8 + 5] = Mathf.Max(0f, sim.DashReadyAt - Time.time); floats[i * 8 + 6] = Mathf.Max(0f, sim.DownUntil - Time.time);
            floats[i * 8 + 7] = sim.MaxHp;
            bytes[i * 5] = (byte)sim.Flags; bytes[i * 5 + 1] = (byte)sim.Mood; bytes[i * 5 + 2] = (byte)sim.Job; bytes[i * 5 + 3] = (byte)(sim.Flying ? 1 : 0);
            bytes[i * 5 + 4] = (byte)sim.Slots;
            stash[i] = sim.StashLabels();
            looks[i] = _looks.TryGetValue(sim.Actor, out int l) ? l : 0;
            i++;
        }
        var coin = _coinActive ? new[] { _coinPos.x, _coinPos.y, _coinPos.z } : new float[0];
        Apply(actors, floats, bytes, looks, stash);
        ApplyCoin(_coinActive ? _coinId : 0, coin);
        if (SemiFunc.IsMultiplayer() && PhotonNetwork.InRoom && Time.time >= _nextSend)
        {
            _nextSend = Time.time + .1f;
            PhotonNetwork.RaiseEvent(StateCode, new object[] { (byte)0, actors, floats, bytes, looks, stash, _coinActive ? _coinId : 0, coin },
                new RaiseEventOptions { Receivers = ReceiverGroup.Others }, SendOptions.SendUnreliable);
        }
    }

    private void Notify(int actor, CourierNotice notice, string arg)
    {
        if (actor == LocalActor || !SemiFunc.IsMultiplayer()) { ShowNotice(actor, notice, arg); return; }
        if (!PhotonNetwork.InRoom) return;
        PhotonNetwork.RaiseEvent(StateCode, new object[] { (byte)1, actor, (byte)notice, arg ?? "" },
            new RaiseEventOptions { TargetActors = new[] { actor } }, SendOptions.SendReliable);
    }

    /// <summary>Host: a player's command (from the network, or the host's own menu).</summary>
    private void Handle(int sender, Op op, int a, int b)
    {
        if (op == Op.Look) { _looks[sender] = a; return; }
        if (op == Op.Coin) { ClaimCoin(sender, a); return; }
        if (op == Op.Pet) { if (_sims.TryGetValue(a, out var petted)) petted.Petted(); return; }
        if (!_sims.TryGetValue(sender, out var sim)) return;
        if (op == Op.Unstash) { if (sim.TryUnstash(a)) Notify(sender, CourierNotice.Stashed, "-"); return; }
        if (sim.Down) { Notify(sender, CourierNotice.Refused, ((byte)CourierReason.KnockedOut).ToString()); return; }
        bool levelJob = op is Op.Charge or Op.Fetch or Op.Ally or Op.Cart or Op.Stash or Op.Store or Op.HeadTrail or Op.HeadFetch;
        if (levelJob && !SemiFunc.RunIsLevel()) { Notify(sender, CourierNotice.Refused, ((byte)CourierReason.LevelsOnly).ToString()); return; }
        PhysGrabObject item = null;
        if (op is Op.Charge or Op.Fetch or Op.Ally or Op.Stash or Op.Store)
        {
            var view = PhotonView.Find(a);
            if (view != null) item = view.GetComponent<PhysGrabObject>();
            if (item != null && ReservedByAnother(sim, item))
            {
                Notify(sender, CourierNotice.Refused, ((byte)CourierReason.ItemBusy).ToString());
                return;
            }
        }
        CourierReason? result = op switch
        {
            Op.Charge => sim.TryCharge(item, b == 1),
            Op.Fetch => sim.TryFetch(item),
            Op.Ally => sim.TryAlly(item, FindPlayer(b)),
            Op.Cart => sim.TryCart(),
            Op.Dash => sim.TryDash(),
            Op.Fly => sim.TryFly(a != 0),
            Op.Stash => sim.TryStash(item),
            Op.Store => sim.TryStore(item),
            Op.HeadTrail => FindHead(sim, carry: false),
            Op.HeadFetch => FindHead(sim, carry: true),
            _ => null,
        };
        if (op == Op.Follow) sim.Cancel();
        if (result.HasValue) Notify(sender, CourierNotice.Refused, ((byte)result.Value).ToString());
        else if (op == Op.Stash) Notify(sender, CourierNotice.Stashed, "+");
        else if (levelJob && op != Op.HeadTrail) Notify(sender, CourierNotice.Started, ((byte)op).ToString());
    }

    private bool ReservedByAnother(CourierSim sim, PhysGrabObject item)
    {
        foreach (var other in _sims.Values)
            if (other != sim && other.HasItem(item)) return true;
        return false;
    }

    private CourierReason? FindHead(CourierSim sim, bool carry)
    {
        if (sim.Level(carry ? Skills.SkillCatalog.CourierHeadFetch : Skills.SkillCatalog.CourierHeadTrail) <= 0)
            return CourierReason.NeedPerk;
        if (sim.Owner == null || sim.Owner.isDisabled || GameDirector.instance == null) return CourierReason.NoHead;
        var from = sim.Owner.transform.position;
        PlayerDeathHead best = null;
        Vector3[] route = null;
        float shortest = float.PositiveInfinity;
        bool tooFar = false, busy = false;
        foreach (var player in GameDirector.instance.PlayerList)
        {
            if (player == null || player == sim.Owner || !CourierHeadRoute.Active(player.playerDeathHead)) continue;
            var head = player.playerDeathHead;
            var path = CourierHeadRoute.Path(from, head.physGrabObject.midPoint);
            if (path == null || path.Length == 0) continue;
            if (carry && !CourierHeadRoute.WithinRoomLimit(path)) { tooFar = true; continue; }
            if (carry && (!CourierSim.CanPickUp(head.physGrabObject) || ReservedByAnother(sim, head.physGrabObject)))
            { busy = true; continue; }
            float length = CourierHeadRoute.Length(path);
            if (length >= shortest) continue;
            shortest = length;
            best = head;
            route = path;
        }
        if (best == null) return busy ? CourierReason.ItemBusy : tooFar ? CourierReason.HeadTooFar : CourierReason.NoHead;
        if (carry) sim.FetchHead(best);
        else SendHeadRoute(sim.Actor, best.photonView != null ? best.photonView.ViewID : 0, route);
        return null;
    }

    private void SendHeadRoute(int actor, int headView, Vector3[] route)
    {
        if (!SemiFunc.IsMultiplayer() || actor == LocalActor) { ShowHeadRoute(headView, route); return; }
        if (!PhotonNetwork.InRoom || route.Length > CourierHeadRoute.MaxCorners) return;
        var coordinates = new float[route.Length * 3];
        for (int i = 0; i < route.Length; i++)
        {
            coordinates[i * 3] = route[i].x;
            coordinates[i * 3 + 1] = route[i].y;
            coordinates[i * 3 + 2] = route[i].z;
        }
        PhotonNetwork.RaiseEvent(StateCode, new object[] { (byte)2, headView, coordinates },
            new RaiseEventOptions { TargetActors = new[] { actor } }, SendOptions.SendReliable);
    }

    private void ShowHeadRoute(int headView, Vector3[] route)
    {
        if (_headRoute == null) _headRoute = gameObject.AddComponent<CourierHeadRoute>();
        _headRoute.Show(headView, route);
        HudNotify.Toast(L("FOLLOW THE TRAIL TO THE HEAD", "СЛЕДУЙТЕ ПО ЛИНИИ К ГОЛОВЕ"), HudStyle.Info, 3f, "courier");
    }

    internal static PlayerAvatar FindPlayer(int actor)
    {
        var players = GameDirector.instance != null ? GameDirector.instance.PlayerList : null;
        if (players != null) foreach (var p in players) if (p != null && ActorOf(p) == actor) return p;
        return null;
    }

    // ------------------------------------------------------------------ network

    internal void Request(Op op, int a, int b)
    {
        if (SemiFunc.IsMasterClientOrSingleplayer()) { Handle(LocalActor, op, a, b); return; }
        if (!PhotonNetwork.InRoom) return;
        PhotonNetwork.RaiseEvent(RequestCode, new object[] { (byte)op, a, b },
            new RaiseEventOptions { Receivers = ReceiverGroup.MasterClient }, SendOptions.SendReliable);
    }

    /// <summary>Host: a guest's command (ModNetwork checks this client is the host).</summary>
    internal static void HandleRequest(EventData e)
    {
        if (Instance == null || !ModConfig.CourierEnabled.Value) return;
        if (e.CustomData is not object[] d || d.Length != 3 || d[0] is not byte op || d[1] is not int a || d[2] is not int b) return;
        if (op < (byte)Op.Pet || op > (byte)Op.HeadFetch) return;
        Instance.Handle(e.Sender, (Op)op, a, b);
    }

    /// <summary>Guest: the host's courier state or a notice (ModNetwork checks the sender is the host).</summary>
    internal static void HandleState(EventData e)
    {
        if (Instance == null || e.CustomData is not object[] d || d.Length < 1 || d[0] is not byte kind) return;
        if (kind == 0 && d.Length == 8 && d[1] is int[] actors && d[2] is float[] floats && d[3] is byte[] bytes && d[4] is int[] looks &&
            d[5] is string[] stash && d[6] is int coinId && d[7] is float[] coin &&
            actors.Length <= 64 && floats.Length == actors.Length * 8 && bytes.Length == actors.Length * 5 && looks.Length == actors.Length &&
            stash.Length == actors.Length && (coin.Length == 0 || coin.Length == 3))
        {
            if (!InWorld()) return;
            Instance._lastState = Time.time;
            Instance.Apply(actors, floats, bytes, looks, stash);
            Instance.ApplyCoin(coinId, coin);
        }
        else if (kind == 1 && d.Length == 4 && d[1] is int actor && d[2] is byte notice && d[3] is string arg && arg.Length < 200)
            ShowNotice(actor, (CourierNotice)notice, arg);
        else if (kind == 2 && d.Length == 3 && d[1] is int headView && d[2] is float[] coordinates &&
            coordinates.Length >= 3 && coordinates.Length <= CourierHeadRoute.MaxCorners * 3 && coordinates.Length % 3 == 0 &&
            InWorld() && SemiFunc.RunIsLevel())
        {
            var route = new Vector3[coordinates.Length / 3];
            for (int i = 0; i < route.Length; i++)
                route[i] = new Vector3(coordinates[i * 3], coordinates[i * 3 + 1], coordinates[i * 3 + 2]);
            Instance.ShowHeadRoute(headView, route);
        }
    }

    private void Apply(int[] actors, float[] f, byte[] b, int[] looks, string[] stash)
    {
        var seen = new HashSet<int>();
        for (int i = 0; i < actors.Length; i++)
        {
            int actor = actors[i]; seen.Add(actor);
            if (!_visuals.TryGetValue(actor, out var visual) || visual == null)
            {
                try { _visuals[actor] = visual = CourierVisual.Create(looks[i]); }
                catch (System.Exception ex) { Plugin.Log.LogError($"[Courier] Could not build the courier model: {ex.Message}"); continue; }
            }
            else if (visual.Look != looks[i]) visual.SetLook(looks[i]);
            visual.Drive(new Vector3(f[i * 8], f[i * 8 + 1], f[i * 8 + 2]), f[i * 8 + 3], (CourierFlags)b[i * 5], (CourierMood)b[i * 5 + 1]);
            Infos[actor] = new Info
            {
                Hp = f[i * 8 + 4], DashLeft = f[i * 8 + 5], DownLeft = f[i * 8 + 6], MaxHp = f[i * 8 + 7], Job = (CourierJob)b[i * 5 + 2],
                Flying = b[i * 5 + 3] != 0, Down = ((CourierFlags)b[i * 5] & CourierFlags.Down) != 0, Slots = b[i * 5 + 4],
                Stash = string.IsNullOrEmpty(stash[i]) ? new string[0] : stash[i].Split('|'),
            };
        }
        var stale = new List<int>();
        foreach (var actor in _visuals.Keys) if (!seen.Contains(actor)) stale.Add(actor);
        foreach (var actor in stale) { if (_visuals[actor] != null) Destroy(_visuals[actor].gameObject); _visuals.Remove(actor); Infos.Remove(actor); }
    }

    // ------------------------------------------------------------------ notices

    private static void ShowNotice(int actor, CourierNotice notice, string arg)
    {
        if (actor != LocalActor && SemiFunc.IsMultiplayer()) return;
        var pet = CourierLook.Name(CourierLook.Local & 15).ToUpperInvariant();
        switch (notice)
        {
            case CourierNotice.Refused:
                byte.TryParse(arg, out byte r);
                HudNotify.Toast($"{pet}: {Reason((CourierReason)r)}", HudStyle.Bad, 3f, "courier");
                Instance?.CallOwn(CourierMood.Sad);
                break;
            case CourierNotice.Started:
                HudNotify.Toast(L($"{pet}: ON IT!", $"{pet}: УЖЕ БЕГУ!"), HudStyle.Info, 2f, "courier");
                Instance?.CallOwn(CourierMood.Surprised);
                break;
            case CourierNotice.Done:
            {
                var bits = (arg ?? "").Split('|');
                byte.TryParse(bits[0], out byte job);
                string item = bits.Length > 1 && bits[1].Length > 0 ? bits[1].ToUpperInvariant() : L("ITEM", "ПРЕДМЕТ");
                string text = (CourierJob)job switch
                {
                    CourierJob.Charge => L($"CHARGED AND BACK: {item}", $"ЗАРЯЖЕНО И ДОСТАВЛЕНО: {item}"),
                    CourierJob.Fetch => L($"BROUGHT FROM THE TRUCK: {item}", $"ПРИНЁС ИЗ МАШИНЫ: {item}"),
                    CourierJob.Ally => L($"DELIVERED TO TEAMMATE: {item}", $"ОТНЁС СОЮЗНИКУ: {item}"),
                    CourierJob.Cart => L("THE C.A.R.T. IS HERE", "ТЕЛЕЖКА НА МЕСТЕ"),
                    CourierJob.Store => L($"LEFT IN THE TRUCK: {item}", $"В МАШИНЕ: {item}"),
                    CourierJob.Head => L("BROUGHT BACK THE HEAD", "ПРИНЁС ГОЛОВУ"),
                    _ => L("DONE", "ГОТОВО"),
                };
                HudNotify.Toast($"{pet}: {text}", HudStyle.Good, 3f, "courier");
                break;
            }
            case CourierNotice.Found:
                HudNotify.Toast(L($"{pet} FOUND AN UPGRADE: {arg?.ToUpperInvariant()}", $"{pet} НАШЁЛ УЛУЧШЕНИЕ: {arg?.ToUpperInvariant()}"), HudStyle.Title, 5f, "courier_find");
                try { Visuals.ItemAudio.Cue("courier_find", .45f).Play(PlayerAvatar.instance.transform.position); } catch { }
                break;
            case CourierNotice.Down:
                HudNotify.Toast(L($"{pet} WAS KNOCKED OUT: BACK AT THE TRUCK IN {arg} S", $"{pet} ВЫБИТ: ВЕРНЁТСЯ К МАШИНЕ ЧЕРЕЗ {arg} С"), HudStyle.Bad, 4f, "courier");
                break;
            case CourierNotice.CoinGot:
                CourierGacha.AddCoin();
                break;
            case CourierNotice.Stashed:
                HudNotify.Toast(arg == "+" ? L($"{pet}: PUT IT IN MY BACKPACK", $"{pet}: УБРАЛ В РЮКЗАК") : L($"{pet}: HERE YOU GO", $"{pet}: ДЕРЖИ"), HudStyle.Good, 2f, "courier");
                break;
            case CourierNotice.Back:
                HudNotify.Toast(L($"{pet} IS BACK AT THE TRUCK", $"{pet} СНОВА У МАШИНЫ"), HudStyle.Good, 3f, "courier");
                break;
        }
    }

    internal static string Reason(CourierReason r) => r switch
    {
        CourierReason.NotHolding => L("HOLD THE ITEM IN YOUR HANDS FIRST", "СНАЧАЛА ВОЗЬМИТЕ ПРЕДМЕТ В РУКИ"),
        CourierReason.BatteryFull => L("ITS BATTERY IS ALREADY FULL", "ЕГО БАТАРЕЯ УЖЕ ПОЛНАЯ"),
        CourierReason.NoBattery => L("IT HAS NO BATTERY TO CHARGE", "ЕМУ НЕЧЕГО ЗАРЯЖАТЬ"),
        CourierReason.NoStation => L("THE CHARGING STATION IS EMPTY OR MISSING", "ЗАРЯДНАЯ СТАНЦИЯ ПУСТА ИЛИ ЕЁ НЕТ"),
        CourierReason.TooHeavy => L("TOO HEAVY FOR ME", "СЛИШКОМ ТЯЖЕЛО"),
        CourierReason.NotInTruck => L("THAT ITEM IS NOT IN THE TRUCK ANY MORE", "ЭТОГО ПРЕДМЕТА УЖЕ НЕТ В МАШИНЕ"),
        CourierReason.Valuable => L("I DON'T CARRY VALUABLES", "ЦЕННОСТИ Я НЕ НОШУ"),
        CourierReason.NoTeammate => L("NO TEAMMATE TO DELIVER TO", "НЕКОМУ ДОСТАВИТЬ"),
        CourierReason.NoCart => L("NO FREE C.A.R.T. NEARBY", "РЯДОМ НЕТ СВОБОДНОЙ ТЕЛЕЖКИ"),
        CourierReason.NeedPerk => L("NEEDS A COURIER PERK", "НУЖЕН ПЕРК КУРЬЕРА"),
        CourierReason.Cooldown => L("STILL CATCHING MY BREATH", "ЕЩЁ ПЕРЕВОЖУ ДУХ"),
        CourierReason.KnockedOut => L("KNOCKED OUT", "ВЫБИТ"),
        CourierReason.LevelsOnly => L("ONLY ON A LEVEL", "ТОЛЬКО НА УРОВНЕ"),
        CourierReason.Gone => L("THE ITEM IS GONE", "ПРЕДМЕТ ПРОПАЛ"),
        CourierReason.BackpackFull => L("MY BACKPACK IS FULL", "РЮКЗАК ПОЛОН"),
        CourierReason.ItemBusy => L("THAT ITEM IS BUSY", "ПРЕДМЕТ ЗАНЯТ"),
        CourierReason.ChargeTimedOut => L("I BROUGHT IT BACK WITHOUT A FULL CHARGE", "ВЕРНУЛ ПРЕДМЕТ БЕЗ ПОЛНОЙ ЗАРЯДКИ"),
        CourierReason.NoHead => L("NO REACHABLE DEATH HEAD", "НЕТ ДОСТУПНОЙ ГОЛОВЫ"),
        CourierReason.HeadTooFar => L("THE HEAD IS MORE THAN THREE ROOMS AWAY", "ДО ГОЛОВЫ БОЛЬШЕ ТРЁХ КОМНАТ"),
        _ => L("CAN'T DO THAT", "НЕ МОГУ"),
    };

    private void CallOwn(CourierMood mood)
    {
        if (_visuals.TryGetValue(LocalActor, out var own) && own != null && mood == CourierMood.Surprised) own.Call();
    }

    // ------------------------------------------------------------------ gacha coins

    /// <summary>Host: rarely hides one gacha coin on a level, away from the truck.</summary>
    private void PlaceCoin()
    {
        _coinActive = false;
        if (!ModConfig.CourierEnabled.Value || !SemiFunc.RunIsLevel() || Random.value >= ModConfig.CourierCoinChance.Value) return;
        var points = SemiFunc.LevelPointsGetAll();
        if (points == null || points.Count == 0) return;
        var truck = TruckPoint();
        for (int tries = 0; tries < 20; tries++)
        {
            var point = points[Random.Range(0, points.Count)];
            if (point == null || Vector3.Distance(point.transform.position, truck) < 25f) continue;
            _coinPos = CourierSim.Ground(point.transform.position) + Vector3.up * .45f;
            _coinId = Random.Range(1, int.MaxValue);
            _coinActive = true;
            Plugin.Log.LogInfo($"[Courier] A gacha coin is hidden at {_coinPos}.");
            return;
        }
    }

    /// <summary>Host: a player touched the coin. The first one close enough gets it.</summary>
    private void ClaimCoin(int actor, int id)
    {
        if (!_coinActive || id != _coinId) return;
        var player = FindPlayer(actor);
        if (player == null || player.isDisabled || Vector3.Distance(player.transform.position, _coinPos) > 2.5f) return;
        _coinActive = false;
        Notify(actor, CourierNotice.CoinGot, "");
    }

    private int _shownCoin;

    private void ApplyCoin(int id, float[] at)
    {
        if (id == 0 || at.Length != 3)
        {
            if (_coinVisual != null) { _coinVisual.Collect(); _coinVisual = null; }
            _shownCoin = 0;
            return;
        }
        if (id != _shownCoin || _coinVisual == null)
        {
            if (_coinVisual != null) Destroy(_coinVisual.gameObject);
            _coinVisual = CourierCoin.Create(new Vector3(at[0], at[1], at[2]));
            _shownCoin = id;
        }
    }

    // Everyone: walk into the coin to pick it up (the host confirms).
    private void CoinTick()
    {
        if (_coinVisual == null || _shownCoin == 0 || Time.time < _nextClaim) return;
        var me = PlayerAvatar.instance;
        if (me == null || me.isDisabled) return;
        if (Vector3.Distance(me.transform.position + Vector3.up * .5f, _coinVisual.transform.position) > 1.2f) return;
        _nextClaim = Time.time + .5f;
        Request(Op.Coin, _shownCoin, 0);
    }

    // ------------------------------------------------------------------ petting

    private float _nextPet;

    /// <summary>
    /// Petting works like the game's Elsa: aim your grab at a courier and grab. It wriggles free at once and is happy;
    /// keep holding the grab to keep petting. Couriers have no colliders, so the aim is a ray test against a small
    /// sphere round the pet, blocked by walls, within your grab range.
    /// </summary>
    private void PetInput()
    {
        if (_menu.Open || ChatManager.instance != null && ChatManager.instance.chatActive) return;
        bool pressed = SemiFunc.InputDown(InputKey.Grab), held = SemiFunc.InputHold(InputKey.Grab);
        if (!pressed && !(held && Time.time >= _nextPet)) return;
        var me = PlayerAvatar.instance;
        var cam = Camera.main;
        if (me == null || cam == null || me.isDisabled) return;
        if (me.physGrabber != null && me.physGrabber.grabbed) return; // the grab caught a real object
        float range = me.physGrabber != null ? me.physGrabber.grabRange : 4f;
        var origin = cam.transform.position; var dir = cam.transform.forward;
        CourierVisual best = null; int bestActor = 0; float bestAlong = range;
        foreach (var kv in _visuals)
        {
            var v = kv.Value;
            if (v == null || (Infos.TryGetValue(kv.Key, out var info) && info.Down)) continue;
            var centre = v.Centre;
            var to = centre - origin;
            float along = Vector3.Dot(to, dir);
            if (along <= 0f || along > bestAlong) continue;
            if ((to - dir * along).magnitude > .33f) continue;           // the ray misses the pet
            if (Physics.Raycast(origin, dir, out var hit, along - .25f, ~0, QueryTriggerInteraction.Ignore) &&
                hit.collider.GetComponentInParent<PlayerAvatar>() == null) continue; // a wall or object in between
            bestAlong = along; best = v; bestActor = kv.Key;
        }
        if (best == null) return;
        _nextPet = Time.time + .9f;
        best.PetReaction();
        Request(Op.Pet, bestActor, 0);
    }

    internal CourierVisual OwnVisual => _visuals.TryGetValue(LocalActor, out var v) ? v : null;

    private void OnDestroy() { ClearAll(); _menu.Close(); }
}
