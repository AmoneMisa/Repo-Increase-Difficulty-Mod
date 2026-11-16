using System;
using System.Collections.Generic;
using System.IO;
using ExitGames.Client.Photon;
using IncreaseHardMod.Challenges;
using IncreaseHardMod.Hud;
using IncreaseHardMod.Valuables;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.AI;
using Random = UnityEngine.Random;

namespace IncreaseHardMod.FableRealm;

/// <summary>
/// The bean puzzle of the Fable Realm special room, built like the map rituals (<see cref="MapRitual"/>).
/// The host decides everything: it hides five coloured beans in other rooms, watches the five plates, lowers the
/// giant bean when every plate holds the bean of its colour and spawns the Golden Egg once. Everyone else only
/// shows what the host reports; the report is repeated every few seconds, which is also how a late joiner catches up.
/// </summary>
internal sealed class BeanPuzzle : MonoBehaviour
{
    private const string HudKey = "ihm.beanpuzzle";
    private const float ScanSeconds = .25f, SyncSeconds = 5f, RecoverySeconds = 6f;
    // A bean counts when it lies still inside the plate's rim (the plate is 0.56 m in radius, 5 cm high).
    private const float PlateRadius = .62f, PlateReach = .8f, RestSpeedSquared = .65f;
    private static readonly int World = LayerMask.GetMask("Default", "PlayerOnlyCollision");
    private static BeanPuzzle _instance;
    private static BeanRewardLedger _ledger;

    private sealed class Bean
    {
        internal PrefabRef Prefab;
        internal Vector3 Floor;
        internal ValuableObject Value;
        internal int ViewId;
    }

    private BeanPuzzleState _state = new();
    private readonly int[] _resting = new int[BeanPuzzleRules.Count];
    private Bean[] _beans;
    private LevelGenerator _generator;
    private BeanGrove _grove;
    private HudListPanel _panel;
    private float _nextScan, _nextSync, _nextRecovery;
    private int _players, _shownMask = -1, _eggAttempts;
    private bool _planned, _shownRussian, _announcedSolved, _announcedReward;

    private void Awake() => _instance = this;
    private void OnDestroy() { _panel?.Destroy(); if (_instance == this) _instance = null; }

    private static double Now() => PhotonNetwork.InRoom ? PhotonNetwork.Time : Time.timeAsDouble;

    private static string LevelKey() => RunManager.instance?.levelCurrent != null
        ? $"{RunManager.instance.levelsCompleted}:{RunManager.instance.levelCurrent.name}" : "";

    private void Update()
    {
        var generator = LevelGenerator.Instance;
        if (_generator != generator)
        {
            Reset();
            _generator = generator;
        }
        if (generator == null || !generator.Generated || RunManager.instance == null || GameDirector.instance == null ||
            !SemiFunc.RunIsLevel() || !FableLevel.IsCurrent)
        {
            _panel?.Hide();
            return;
        }
        // The generator may find no dead-end tile for the special room: then this level simply has no puzzle.
        _grove = BeanGrove.Instance;
        if (_grove == null || !_grove.Complete)
        {
            _panel?.Hide();
            return;
        }
        bool playing = GameDirector.instance.currentState == GameDirector.gameState.Main;
        if (SemiFunc.IsMasterClientOrSingleplayer())
        {
            if (!_planned)
            {
                _planned = true;
                try { Plan(); }
                catch (Exception error) { _beans = null; Plugin.Log.LogWarning("[Fable] Bean puzzle could not start: " + error); }
            }
            if (_beans != null && playing) TickHost();
        }
        UpdateView(playing);
    }

    private void Reset()
    {
        _state = new BeanPuzzleState();
        _beans = null;
        _grove = null;
        _planned = _announcedSolved = _announcedReward = false;
        _nextScan = _nextSync = _nextRecovery = 0f;
        _players = _eggAttempts = 0;
        _shownMask = -1;
        _panel?.Hide();
        HudNotify.Clear(HudKey + ".solved");
        HudNotify.Clear(HudKey + ".reward");
    }

    // ---------------------------------------------------------------- host

    private void Plan()
    {
        var beans = new Bean[BeanPuzzleRules.Count];
        for (int i = 0; i < beans.Length; i++)
        {
            var prefab = InteractiveValuables.RitualPrefab(BeanPuzzleRules.BeanKey(i));
            if (prefab == null) { Plugin.Log.LogWarning("[Fable] Bean valuables are not registered; the puzzle is off."); return; }
            beans[i] = new Bean { Prefab = prefab };
        }
        // A guest promoted to host before the first report reached it must not plant a second set.
        if (Adopt(beans)) { _beans = beans; _nextRecovery = Time.time + RecoverySeconds; Broadcast(); return; }

        var grove = _grove.GetComponent<Module>();
        var spots = new List<(Vector3 floor, Module module)>();
        foreach (var point in SemiFunc.LevelPointsGetAll())
        {
            if (point == null || point.Truck || point.inStartRoom || (point.Room != null && (point.Room.Truck || point.Room.Extraction))) continue;
            var module = point.GetComponentInParent<Module>();
            if (module == null || module == grove) continue; // the beans are found elsewhere, never in the grove itself
            if (MapRitual.TryFloor(point.transform.position, out var floor)) spots.Add((floor, module));
        }
        for (int i = spots.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (spots[i], spots[j]) = (spots[j], spots[i]);
        }
        // One bean per room while rooms last, then anywhere at least two metres from another bean.
        var chosen = new List<(Vector3 floor, Module module)>();
        foreach (bool distinctRooms in new[] { true, false })
            foreach (var spot in spots)
            {
                if (chosen.Count == beans.Length) break;
                if (chosen.Exists(c => (c.floor - spot.floor).sqrMagnitude < 4f || (distinctRooms && c.module == spot.module))) continue;
                chosen.Add(spot);
            }
        if (chosen.Count < beans.Length)
        {
            Plugin.Log.LogWarning($"[Fable] Only {chosen.Count} places for five beans; the puzzle is off on this level.");
            return;
        }
        for (int i = 0; i < beans.Length; i++)
        {
            beans[i].Floor = chosen[i].floor;
            Spawn(beans[i], backup: false);
        }
        _beans = beans;
        _nextRecovery = Time.time + RecoverySeconds;
        Broadcast();
        Plugin.Log.LogInfo("[Fable] Bean puzzle started: five beans placed outside the grove.");
    }

    private static bool Adopt(Bean[] beans)
    {
        int found = 0;
        foreach (var art in FindObjectsByType<ValuableArtInstance>(FindObjectsSortMode.None))
        {
            int colour = BeanPuzzleRules.ColourOf(art.assetKey);
            if (colour < 0 || beans[colour].Value != null || !art.TryGetComponent<ValuableObject>(out var value)) continue;
            beans[colour].Value = value;
            beans[colour].Floor = art.transform.position;
            beans[colour].ViewId = art.TryGetComponent<PhotonView>(out var view) ? view.ViewID : 0;
            found++;
        }
        if (found == beans.Length) return true;
        foreach (var bean in beans) { bean.Value = null; bean.ViewId = 0; }
        return false;
    }

    private static void Spawn(Bean bean, bool backup)
    {
        var rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
        var obj = REPOLib.Modules.NetworkPrefabs.SpawnNetworkPrefab(bean.Prefab, bean.Floor + Vector3.up * .08f, rotation,
            data: backup ? new object[] { MapRitual.BackupMarker } : null);
        bean.Value = obj != null ? obj.GetComponent<ValuableObject>() : null;
        bean.ViewId = obj != null && obj.TryGetComponent<PhotonView>(out var view) ? view.ViewID : 0;
        // A replacement bean is worth nothing: breaking or selling beans must not print money.
        if (backup && bean.Value != null) MapRitual.SetBackupValue(bean.Value);
    }

    private void TickHost()
    {
        float now = Time.time;
        if (!_state.Solved && now >= _nextScan)
        {
            _nextScan = now + ScanSeconds;
            Scan();
            if (_state.Observe(_resting, Now()))
            {
                Broadcast();
                if (_state.Solved) Plugin.Log.LogInfo("[Fable] All five beans match: the giant bean comes down.");
            }
            if (!_state.Solved && now >= _nextRecovery)
            {
                _nextRecovery = now + RecoverySeconds;
                if (Recover()) Broadcast();
            }
        }
        if (_state.RewardDue(Now())) Reward();
        if (now >= _nextSync || (PhotonNetwork.InRoom && _players != PhotonNetwork.CurrentRoom.PlayerCount)) Broadcast();
    }

    // Which bean colours lie still on which plate. A plate only ever lights for its own colour (BeanPuzzleState).
    private void Scan()
    {
        for (int plate = 0; plate < _resting.Length; plate++)
        {
            _resting[plate] = 0;
            var socket = _grove.sockets[plate].position;
            foreach (var collider in Physics.OverlapSphere(socket + Vector3.up * .3f, PlateReach,
                SemiFunc.LayerMaskGetPhysGrabObject(), QueryTriggerInteraction.Ignore))
            {
                var grab = collider.GetComponentInParent<PhysGrabObject>();
                if (grab == null || grab.grabbed || grab.dead || grab.rb == null ||
                    grab.rb.linearVelocity.sqrMagnitude > RestSpeedSquared ||
                    !grab.TryGetComponent<ValuableArtInstance>(out var art)) continue;
                int colour = BeanPuzzleRules.ColourOf(art.assetKey);
                if (colour < 0) continue;
                var offset = grab.centerPoint - socket;
                if (offset.y < -.1f || offset.y > .7f || new Vector2(offset.x, offset.z).sqrMagnitude > PlateRadius * PlateRadius) continue;
                _resting[plate] |= 1 << colour;
            }
        }
    }

    // Keeps the puzzle solvable: a bean that was broken, sold or dropped out of the level comes back where it started.
    private bool Recover()
    {
        bool changed = false;
        foreach (var bean in _beans)
        {
            if (bean.Value == null && bean.ViewId > 0) bean.Value = PhotonView.Find(bean.ViewId)?.GetComponent<ValuableObject>();
            var value = bean.Value;
            var grab = value != null ? value.physGrabObject : null;
            if (value == null || grab == null || grab.dead)
            {
                Spawn(bean, backup: true);
                changed |= bean.Value != null;
                continue;
            }
            if (grab.grabbed || value.roomVolumeCheck == null ||
                NavMesh.SamplePosition(grab.transform.position, out _, .5f, NavMesh.AllAreas)) continue;
            value.roomVolumeCheck.CheckSet();
            if (value.roomVolumeCheck.CurrentRooms.Count > 0) continue;
            grab.Teleport(bean.Floor + Vector3.up * .14f, Quaternion.identity);
            if (grab.rb != null) { grab.rb.linearVelocity = Vector3.zero; grab.rb.angularVelocity = Vector3.zero; }
        }
        return changed;
    }

    // Called once the bean has landed. The ledger on the host's disk makes the egg a once-per-level reward even
    // across a scene reload or a reloaded save; BeanPuzzleState makes it once per puzzle.
    private void Reward()
    {
        string key = BeanRewardLedger.Key(StatsManager.instance != null ? StatsManager.instance.saveFileCurrent : null,
            RunManager.instance.levelsCompleted);
        if (Ledger.Claimed(key))
        {
            _state.SettleReward(false);
            Broadcast();
            Plugin.Log.LogInfo("[Fable] The Golden Egg of this level was already given (" + key + ").");
            return;
        }
        var prefab = InteractiveValuables.RitualPrefab(BeanPuzzleRules.EggKey);
        var body = prefab?.Prefab != null ? prefab.Prefab.GetComponent<PhysGrabObject>() : null;
        var position = MapRitual.OfferingPosition(_grove.eggSpawn.position, Quaternion.identity, body, .35f);
        var egg = prefab != null ? REPOLib.Modules.Valuables.SpawnValuable(prefab, position, Quaternion.identity) : null;
        if (egg == null)
        {
            // Try again for a few seconds, then give up without marking the level as paid.
            if (++_eggAttempts >= 40) { _state.SettleReward(false); Broadcast(); Plugin.Log.LogError("[Fable] The Golden Egg could not be spawned."); }
            return;
        }
        Ledger.Claim(key);
        SaveLedger();
        _state.SettleReward(true);
        Broadcast();
        Plugin.Log.LogInfo("[Fable] Golden Egg spawned (" + key + ").");
    }

    private static string LedgerPath => Path.Combine(BepInEx.Paths.ConfigPath, "DeepRunOverhaul.fable-eggs.txt");

    private static BeanRewardLedger Ledger
    {
        get
        {
            if (_ledger != null) return _ledger;
            _ledger = new BeanRewardLedger();
            try { if (File.Exists(LedgerPath)) _ledger.Load(File.ReadAllLines(LedgerPath)); }
            catch (Exception error) { Plugin.Log.LogWarning("[Fable] Egg ledger could not be read: " + error.Message); }
            return _ledger;
        }
    }

    private static void SaveLedger()
    {
        try { File.WriteAllLines(LedgerPath, Ledger.Lines()); }
        catch (Exception error) { Plugin.Log.LogWarning("[Fable] Egg ledger could not be saved: " + error.Message); }
    }

    // ---------------------------------------------------------------- network

    private void Broadcast()
    {
        _nextSync = Time.time + SyncSeconds;
        if (!PhotonNetwork.InRoom || !PhotonNetwork.IsMasterClient || _beans == null) return;
        _players = PhotonNetwork.CurrentRoom.PlayerCount;
        var ids = new int[_beans.Length];
        var floors = new float[_beans.Length * 3];
        for (int i = 0; i < _beans.Length; i++)
        {
            ids[i] = _beans[i].ViewId;
            floors[i * 3] = _beans[i].Floor.x; floors[i * 3 + 1] = _beans[i].Floor.y; floors[i * 3 + 2] = _beans[i].Floor.z;
        }
        PhotonNetwork.RaiseEvent(BeanPuzzleWire.EventCode, BeanPuzzleWire.Write(LevelKey(), _state.Snapshot, ids, floors),
            new RaiseEventOptions { Receivers = ReceiverGroup.Others }, SendOptions.SendReliable);
    }

    /// <summary>
    /// A report from the host. ModNetwork has already checked that the sender is the host and that we are not;
    /// here the message must also belong to this very level (same level index and name, Fable Realm loaded and
    /// generated) and be well formed. Applying it is idempotent: repeats change nothing.
    /// </summary>
    internal static void Receive(EventData message)
    {
        var self = _instance;
        if (self == null || LevelGenerator.Instance == null || !LevelGenerator.Instance.Generated || !FableLevel.IsCurrent ||
            !BeanPuzzleWire.TryRead(message.CustomData, LevelKey(), out var snapshot, out var ids, out var floors)) return;
        if (self._generator != LevelGenerator.Instance)
        {
            self.Reset();
            self._generator = LevelGenerator.Instance;
        }
        self._planned = true; // the beans exist; a guest that becomes host carries on from here
        self._beans ??= new Bean[BeanPuzzleRules.Count];
        for (int i = 0; i < self._beans.Length; i++)
        {
            var bean = self._beans[i] ??= new Bean { Prefab = InteractiveValuables.RitualPrefab(BeanPuzzleRules.BeanKey(i)) };
            bean.Floor = new Vector3(floors[i * 3], floors[i * 3 + 1], floors[i * 3 + 2]);
            if (bean.ViewId == ids[i]) continue;
            bean.ViewId = ids[i];
            bean.Value = null;
        }
        // Joined after it was all over: show the finished room without announcing old news.
        if (!self._state.Solved && snapshot.Solved && snapshot.Reward != BeanReward.Pending)
            self._announcedSolved = self._announcedReward = true;
        self._state.Apply(snapshot);
        self._nextRecovery = Time.time + 2f;
    }

    // ---------------------------------------------------------------- view

    private void UpdateView(bool playing)
    {
        _grove.SetMatched(_state.Matched);
        _grove.SetDrop(_state.Solved ? BeanPuzzleRules.Drop(Now() - _state.SolvedAt) : 0f);
        var player = PlayerAvatar.instance;
        if (!playing || player == null || player.deadSet || player.isDisabled)
        {
            _panel?.Hide();
            return;
        }
        bool ru = Localization.GameLanguage.Russian;
        if (_state.Solved)
        {
            _panel?.Hide();
            if (!_announcedSolved)
            {
                _announcedSolved = true;
                HudNotify.Toast(ru ? "Великаний боб спускается" : "The giant bean is coming down", HudStyle.Info, 4f, HudKey + ".solved");
            }
            if (!_announcedReward && _state.Reward != BeanReward.Pending)
            {
                _announcedReward = true;
                if (_state.Reward == BeanReward.Given)
                    HudNotify.Toast(ru ? "Великаний боб оставил Золотое яйцо" : "The giant bean left a Golden Egg", HudStyle.Good, 5f, HudKey + ".reward");
                else
                    HudNotify.Toast(ru ? "Боб пуст: это яйцо уже забрали" : "The bean is empty: this egg was already taken", HudStyle.Caution, 5f, HudKey + ".reward");
            }
            return;
        }
        // The riddle shows while the player stands in the grove with a clear view of the dais.
        var dais = _grove.landing.position;
        if ((player.transform.position - dais).sqrMagnitude > 64f ||
            Physics.Linecast(player.transform.position + Vector3.up, dais + Vector3.up * 1.2f, World, QueryTriggerInteraction.Ignore) ||
            Patches.Effects.LevelInfoHud.MenuOpen || Skills.SkillTreeScreen.IsOpen)
        {
            _panel?.Hide();
            return;
        }
        _panel ??= new HudListPanel(HudKey, HudCorner.TopRight, order: 121, width: 470f);
        if (_shownMask != _state.Matched || _shownRussian != ru) SetPanel(ru);
        _panel.Show();
    }

    private void SetPanel(bool ru)
    {
        _shownMask = _state.Matched;
        _shownRussian = ru;
        _panel.Title = ru ? "ВЕЛИКАНИЙ БОБ" : "THE GIANT BEAN";
        var rows = new List<HudListRow>
        {
            new(ru ? "Пять бобов ждут в других комнатах." : "Five beans wait in other rooms.", null, HudStyle.Info),
            new(ru ? "Каждый боб — на плиту его цвета." : "Each bean goes on the plate of its colour.", null, HudStyle.Info),
        };
        var names = ru
            ? new[] { "Красный боб", "Жёлтый боб", "Синий боб", "Зелёный боб", "Фиолетовый боб" }
            : new[] { "Red bean", "Yellow bean", "Blue bean", "Green bean", "Violet bean" };
        int count = 0;
        for (int i = 0; i < names.Length; i++)
        {
            bool matched = (_state.Matched & 1 << i) != 0;
            if (matched) count++;
            rows.Add(new HudListRow(names[i], matched ? (ru ? "НА МЕСТЕ" : "IN PLACE") : null, matched ? HudStyle.Good : HudStyle.Text));
        }
        _panel.Footer = $"{(ru ? "НА МЕСТЕ" : "IN PLACE")}  {count}/{BeanPuzzleRules.Count}";
        _panel.SetRows(rows);
    }
}
