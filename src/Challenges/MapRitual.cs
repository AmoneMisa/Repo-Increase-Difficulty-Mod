using System;
using System.Collections.Generic;
using ExitGames.Client.Photon;
using HarmonyLib;
using IncreaseHardMod.Hud;
using IncreaseHardMod.Valuables;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.AI;
using Random = UnityEngine.Random;

namespace IncreaseHardMod.Challenges;

/// <summary>One untimed, cooperative ritual on each supported map outside Swiftbroom.</summary>
internal sealed class MapRitual : MonoBehaviour
{
    internal const byte EventCode = 158;
    private const string HudKey = "ihm.mapritual";
    private const int PaintingCount = 6;
    private const string BackupMarker = "ihm.ritual.backup";
    private const float ScanSeconds = .25f;
    private static readonly string CubePrefabName = InteractiveValuables.PrefabName(RitualLoot.Cube);
    private static readonly string MousePrefabName = InteractiveValuables.PrefabName("rgb_mouse");
    private static readonly string KeyboardPrefabName = InteractiveValuables.PrefabName("rgb_keyboard");
    private static readonly int World = LayerMask.GetMask("Default", "PlayerOnlyCollision");
    private static MapRitual _instance;

    private sealed class Offering
    {
        internal PrefabRef Prefab;
        internal Vector3 Floor;
        internal ValuableObject Value;
        internal int ViewId;
    }

    internal readonly struct Painting
    {
        internal readonly Vector3 Position;
        internal readonly float Yaw;
        internal readonly bool OnWall;
        internal Painting(Vector3 position, float yaw, bool onWall)
        {
            Position = position;
            Yaw = yaw;
            OnWall = onWall;
        }
    }

    private readonly List<Painting> _paintings = new();
    private readonly List<Offering> _offerings = new();
    private readonly List<ValuableArtInstance> _nearAltar = new();
    private LevelGenerator _generator;
    private GameObject _scene;
    private RitualProps _props;
    private LootMap _map;
    private Vector3 _altar;
    private int _accepted, _variant, _players;
    private float _nextScan, _nextSync, _nextRecovery, _altarYaw;
    private bool _planned, _finished;
    private HudListPanel _clue;
    private int _shownMask = -1;
    private bool _shownRussian, _announced;

    private int Total => GoalCount(_map);
    private static int GoalCount(LootMap map) => map switch
    {
        LootMap.Museum or LootMap.McJannek => 6,
        LootMap.Sunstone => 3,
        _ => 1,
    };

    private void Awake() => _instance = this;
    private void OnDestroy() { _clue?.Destroy(); if (_instance == this) _instance = null; }

    private void Update()
    {
        var generator = LevelGenerator.Instance;
        if (_generator != generator)
        {
            Reset();
            _generator = generator;
        }
        if (generator == null || !generator.Generated || RunManager.instance == null ||
            !SemiFunc.RunIsLevel() || GameDirector.instance == null) return;

        if (SemiFunc.IsMasterClientOrSingleplayer())
        {
            if (!_planned)
            {
                _planned = true;
                try { Plan(); }
                catch (Exception error) { Reset(); _planned = true; Plugin.Log.LogWarning("[Ritual] " + error); }
            }
            if (_scene != null && GameDirector.instance.currentState == GameDirector.gameState.Main) TickHost();
        }
        if (_scene != null && GameDirector.instance.currentState == GameDirector.gameState.Main) UpdateView();
        else _clue?.Hide();
    }

    private void Reset()
    {
        if (_scene != null) Destroy(_scene);
        _scene = null;
        _props = null;
        _planned = _finished = false;
        _accepted = _variant = _players = 0;
        _nextScan = _nextSync = _nextRecovery = _altarYaw = 0f;
        _map = LootMap.None;
        _paintings.Clear();
        _offerings.Clear();
        _nearAltar.Clear();
        _shownMask = -1;
        _announced = false;
        _clue?.Hide();
        HudNotify.Clear(HudKey + ".done");
    }

    private static string LevelKey() => RunManager.instance?.levelCurrent != null
        ? $"{RunManager.instance.levelsCompleted}:{RunManager.instance.levelCurrent.name}" : "";

    private static LootMap MapHere() => CollectionCatalog.Identify(RunManager.instance?.levelCurrent?.name, null);

    private void Plan()
    {
        _map = MapHere();
        if (_map is LootMap.None or LootMap.Swiftbroom) return;
        if (InteractiveValuables.RitualPrefab(RitualLoot.Cube) == null) return;
        var spots = new List<Vector3>();
        foreach (var p in SemiFunc.LevelPointsGetAll())
        {
            if (p == null || p.Truck || p.inStartRoom || (p.Room != null && (p.Room.Truck || p.Room.Extraction))) continue;
            if (TryFloor(p.transform.position, out var floor)) spots.Add(floor);
        }
        Shuffle(spots);
        if (spots.Count == 0) return;
        _altar = spots[0];
        if (_map == LootMap.Manor && !PlaceManorAltar(spots))
        {
            Plugin.Log.LogWarning("[Ritual] No clear place for the Manor chalk drawing.");
            return;
        }
        if (_map == LootMap.Museum)
        {
            PlacePaintings(spots);
            if (_paintings.Count != PaintingCount) { Plugin.Log.LogWarning("[Ritual] No safe layout for six paintings."); return; }
        }
        else if (!PlanOfferings(spots))
        {
            Plugin.Log.LogWarning($"[Ritual] Could not place all offerings for {_map}.");
            return;
        }

        BuildVisuals();
        foreach (var offering in _offerings) Spawn(offering, backup: false);
        _nextRecovery = Time.time + 6f;
        Broadcast();
        Plugin.Log.LogInfo($"[Ritual] Placed {_map} cube at {_altar}.");
    }

    private static bool TryFloor(Vector3 near, out Vector3 floor)
    {
        floor = default;
        if (!NavMesh.SamplePosition(near, out var nav, 1.15f, NavMesh.AllAreas) ||
            !Physics.Raycast(nav.position + Vector3.up * .55f, Vector3.down, out var hit, 1.15f,
                World, QueryTriggerInteraction.Ignore) || hit.normal.y < .94f) return false;
        floor = hit.point;
        return !Physics.CheckBox(floor + Vector3.up * .67f, new Vector3(.65f, .62f, .65f),
            Quaternion.identity, SemiFunc.LayerMaskGetVisionObstruct(), QueryTriggerInteraction.Ignore);
    }

    private static void Shuffle<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    private bool PlaceManorAltar(List<Vector3> spots)
    {
        foreach (var floor in spots)
        for (int i = 0; i < 8; i++)
        {
            float yaw = i * 45f;
            var orientation = Quaternion.Euler(0f, yaw, 0f);
            var chalk = floor + orientation * (Vector3.right * 1.28f);
            if (!TryFloor(chalk, out var traced) || (traced - chalk).sqrMagnitude > .025f ||
                Physics.CheckBox(chalk + Vector3.up * .13f, new Vector3(.48f, .08f, .84f),
                    orientation, World, QueryTriggerInteraction.Ignore)) continue;
            _altar = floor;
            _altarYaw = yaw;
            return true;
        }
        return false;
    }

    private bool PlanOfferings(List<Vector3> spots)
    {
        int count = OfferingCount(_map);
        for (int i = 0; i < count; i++)
        {
            var prefab = InteractiveValuables.RitualPrefab(OfferingKey(_map, i));
            if (prefab == null) return false;
            var spot = spots.FindIndex(p => AvailableOfferingFloor(p, 4f));
            if (spot < 0) spot = spots.FindIndex(p => AvailableOfferingFloor(p, 1.44f));
            if (spot < 0)
            {
                if (_map != LootMap.Manor || _offerings.Count < 2) return false;
                break;
            }
            _offerings.Add(new Offering { Prefab = prefab, Floor = spots[spot] });
        }
        if (_map == LootMap.Manor) _variant = Random.Range(0, _offerings.Count);
        return true;
    }

    private bool AvailableOfferingFloor(Vector3 floor, float spacingSquared) =>
        (floor - _altar).sqrMagnitude >= 6.25f && !_offerings.Exists(o => (o.Floor - floor).sqrMagnitude < spacingSquared);

    private static int OfferingCount(LootMap map) => map switch
    {
        LootMap.Manor => RitualLoot.SkeletonCount,
        LootMap.Museum => 0,
        _ => GoalCount(map),
    };

    private static string OfferingKey(LootMap map, int index) => map switch
    {
        LootMap.Manor => RitualLoot.SkeletonKey(index),
        LootMap.McJannek => RitualLoot.PcParts[index],
        _ => RitualLoot.ResortParts[index],
    };

    private void PlacePaintings(List<Vector3> spots)
    {
        // Favor real walls. Use easels in rooms without a clear, reachable stretch of wall.
        foreach (var (onWall, spacingSquared) in new[] { (true, 4f), (false, 4f), (true, 1.44f), (false, 1.44f) })
        foreach (var floor in spots)
        {
            if (_paintings.Count == PaintingCount) return;
            if ((floor - _altar).sqrMagnitude < 6.25f) continue;
            Painting painting;
            if (onWall)
            {
                if (!FindWall(floor, out painting)) continue;
            }
            else painting = new Painting(floor + Vector3.up * 1.18f, Random.Range(0f, 360f), false);
            if ((painting.Position - _altar).sqrMagnitude < 6.25f ||
                _paintings.Exists(p => (p.Position - painting.Position).sqrMagnitude < spacingSquared)) continue;
            _paintings.Add(painting);
        }
    }

    private static bool FindWall(Vector3 floor, out Painting painting)
    {
        painting = default;
        var eye = floor + Vector3.up * 1.18f;
        float seed = Random.Range(0f, 360f);
        for (int i = 0; i < 12; i++)
        {
            var dir = Quaternion.Euler(0f, seed + i * 30f, 0f) * Vector3.forward;
            if (!Physics.Raycast(eye, dir, out var hit, 3.2f, World, QueryTriggerInteraction.Ignore) ||
                Mathf.Abs(hit.normal.y) > .07f || hit.collider.attachedRigidbody != null) continue;
            var towardRoom = hit.normal.normalized;
            var approach = hit.point + towardRoom * .6f - Vector3.up * 1.18f;
            if (!NavMesh.SamplePosition(approach, out var nav, .65f, NavMesh.AllAreas) ||
                Mathf.Abs(nav.position.y - floor.y) > .28f ||
                Physics.Linecast(nav.position + Vector3.up * 1.18f, hit.point + towardRoom * .085f,
                    World, QueryTriggerInteraction.Ignore)) continue;

            var side = Vector3.Cross(Vector3.up, towardRoom);
            bool fits = true;
            foreach (float x in new[] { -.42f, .42f })
            foreach (float y in new[] { -.32f, .32f })
            {
                var corner = hit.point + side * x + Vector3.up * y + towardRoom * .13f;
                if (!Physics.Raycast(corner, -towardRoom, out var surface, .19f, World, QueryTriggerInteraction.Ignore) ||
                    surface.collider != hit.collider) fits = false;
            }
            if (!fits) continue;
            painting = new Painting(hit.point + towardRoom * .061f, Quaternion.LookRotation(towardRoom).eulerAngles.y, true);
            return true;
        }
        return false;
    }

    private static void Spawn(Offering offering, bool backup)
    {
        if (offering.Prefab == null) return;
        var rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
        var template = offering.Prefab.Prefab;
        var body = template != null ? template.GetComponent<PhysGrabObject>() : null;
        var position = OfferingPosition(offering.Floor, rotation, body, .06f);
        var obj = REPOLib.Modules.NetworkPrefabs.SpawnNetworkPrefab(offering.Prefab, position,
            rotation, data: backup ? new object[] { BackupMarker } : null);
        offering.Value = obj != null ? obj.GetComponent<ValuableObject>() : null;
        offering.ViewId = obj != null && obj.TryGetComponent<PhotonView>(out var view) ? view.ViewID : 0;
        if (backup && offering.Value != null) SetBackupValue(offering.Value);
    }

    private static Vector3 OfferingPosition(Vector3 floor, Quaternion rotation, PhysGrabObject body, float lift)
    {
        var position = floor + Vector3.up * lift;
        if (body == null) return position;
        var center = Vector3.Scale(body.midPointOffset, body.transform.localScale);
        center.y = 0f;
        return position - rotation * center;
    }

    private void TickHost()
    {
        float now = Time.time;
        if (now >= _nextScan && !_finished)
        {
            _nextScan = now + ScanSeconds;
            int before = _accepted;
            if (_map == LootMap.Museum) ScanPaintings();
            else ScanOfferings();
            if (_accepted == (1 << Total) - 1) AwardCube();
            if (before != _accepted) Broadcast();
            if (now >= _nextRecovery)
            {
                _nextRecovery = now + 6f;
                if (RecoverOfferings()) Broadcast();
            }
        }
        if (now >= _nextSync || (PhotonNetwork.InRoom && _players != PhotonNetwork.CurrentRoom.PlayerCount))
            Broadcast();
    }

    private void ScanPaintings()
    {
        foreach (var player in GameDirector.instance.PlayerList)
        {
            if (player == null || player.deadSet || player.isDisabled) continue;
            var handHeight = player.transform.position + Vector3.up * 1.1f;
            for (int i = 0; i < _paintings.Count; i++)
            {
                if ((_accepted & 1 << i) != 0) continue;
                var p = _paintings[i];
                var front = p.Position + Quaternion.Euler(0f, p.Yaw, 0f) * Vector3.forward * .12f;
                if ((front - handHeight).sqrMagnitude > .75f * .75f ||
                    Physics.Linecast(handHeight, front, World, QueryTriggerInteraction.Ignore)) continue;
                _accepted |= 1 << i;
            }
        }
    }

    private void ScanOfferings()
    {
        _nearAltar.Clear();
        var surface = _altar + Vector3.up * .24f;
        foreach (var collider in Physics.OverlapBox(surface + Vector3.up * .25f, new Vector3(.94f, .63f, .94f),
            Quaternion.identity, SemiFunc.LayerMaskGetPhysGrabObject(), QueryTriggerInteraction.Ignore))
        {
            var grab = collider.GetComponentInParent<PhysGrabObject>();
            if (grab == null || grab.grabbed || grab.dead || grab.rb == null || grab.rb.velocity.sqrMagnitude > .65f ||
                (grab.centerPoint - surface).sqrMagnitude > 1.22f ||
                Physics.Linecast(surface, grab.centerPoint, World, QueryTriggerInteraction.Ignore) ||
                !grab.TryGetComponent<ValuableArtInstance>(out var art) || _nearAltar.Contains(art)) continue;
            _nearAltar.Add(art);
            int index = _map == LootMap.Manor ? art.assetKey == RitualLoot.SkeletonKey(_variant) ? 0 : -1
                : Array.IndexOf(_map == LootMap.McJannek ? RitualLoot.PcParts : RitualLoot.ResortParts, art.assetKey);
            if (index >= 0) _accepted |= 1 << index;
        }
    }

    private bool RecoverOfferings()
    {
        // Keep the puzzle reachable if a required offering falls out of the map.
        bool changed = false;
        for (int i = 0; i < _offerings.Count; i++)
        {
            if (_map == LootMap.Manor ? i != _variant || _accepted != 0 : (_accepted & 1 << i) != 0) continue;
            var offering = _offerings[i];
            if (offering.Value == null && offering.ViewId > 0)
                offering.Value = PhotonView.Find(offering.ViewId)?.GetComponent<ValuableObject>();
            var value = offering.Value;
            var grab = value != null ? value.physGrabObject : null;
            if (value == null || grab == null || grab.dead)
            {
                Spawn(offering, backup: true);
                changed |= offering.Value != null;
                continue;
            }
            if (grab.grabbed || value.roomVolumeCheck == null ||
                NavMesh.SamplePosition(grab.transform.position, out _, .5f, NavMesh.AllAreas)) continue;
            value.roomVolumeCheck.CheckSet();
            if (value.roomVolumeCheck.CurrentRooms.Count > 0) continue;
            grab.Teleport(OfferingPosition(offering.Floor, Quaternion.identity, grab, .14f), Quaternion.identity);
            if (grab.rb != null) { grab.rb.velocity = Vector3.zero; grab.rb.angularVelocity = Vector3.zero; }
        }
        return changed;
    }

    private void AwardCube()
    {
        var reward = REPOLib.Modules.Valuables.SpawnValuable(InteractiveValuables.RitualPrefab(RitualLoot.Cube),
            _altar + Vector3.up * .55f, Quaternion.identity);
        if (reward == null) return;
        _finished = true;
        Broadcast();
        Plugin.Log.LogInfo($"[Ritual] {_map} cube awakened.");
    }

    private void BuildVisuals()
    {
        _scene = new GameObject("IHM Map Ritual");
        _props = _scene.AddComponent<RitualProps>();
        _props.Build(_altar, _altarYaw, _map, _variant, Total, _paintings);
        if (_finished) _props.Refresh(_accepted, true);
    }

    private void UpdateView()
    {
        _props.Refresh(_accepted, _finished);
        var player = PlayerAvatar.instance;
        if (player == null || player.deadSet || player.isDisabled)
        {
            _clue?.Hide();
            return;
        }
        bool ru = Localization.GameLanguage.Russian;
        if (_finished)
        {
            _clue?.Hide();
            if (!_announced) HudNotify.Toast(ru ? "Куб пробудился" : "The cube has awakened", HudStyle.Good, 4f, HudKey + ".done");
            _announced = true;
            return;
        }
        if ((player.transform.position - _altar).sqrMagnitude > 16f ||
            Physics.Linecast(player.transform.position + Vector3.up, _altar + Vector3.up * .7f, World, QueryTriggerInteraction.Ignore) ||
            Patches.Effects.LevelInfoHud.MenuOpen || Skills.SkillTreeScreen.IsOpen)
        {
            _clue?.Hide();
            return;
        }
        _clue ??= new HudListPanel(HudKey, HudCorner.TopRight, order: 120, width: 470f);
        if (_shownMask != _accepted || _shownRussian != ru) SetClue(ru);
        _clue.Show();
    }

    private void SetClue(bool ru)
    {
        _shownMask = _accepted;
        _shownRussian = ru;
        _clue.Title = ru ? "ЗАГАДКА КУБА" : "CUBE RIDDLE";
        var lines = (_map, ru) switch
        {
            (LootMap.McJannek, true) => new[] { "Шесть частей. Один разум.", "Соберите компьютер у куба." },
            (LootMap.McJannek, false) => new[] { "Six parts. One mind.", "Assemble a computer at the cube." },
            (LootMap.Museum, true) => new[] { "Шесть рам хранят шесть тайн.", "Коснитесь картин с рунами." },
            (LootMap.Museum, false) => new[] { "Six frames hold six secrets.", "Touch the paintings with runes." },
            (LootMap.Manor, true) => new[] { "Из всех костей лишь одни помнит пол.", "Принесите скелет по меловому контуру." },
            (LootMap.Manor, false) => new[] { "The floor remembers only one set of bones.", "Bring the skeleton that fits the chalk outline." },
            (_, true) => new[] { "Плата курорту за отдых вечный.", "Оставьте три дара у куба." },
            _ => new[] { "Pay the resort for an eternal stay.", "Leave three offerings at the cube." },
        };
        var names = (_map, ru) switch
        {
            (LootMap.McJannek, true) => new[] { "Монитор", "Системный блок", "Видеокарта", "Оперативная память", "Клавиатура", "Мышь" },
            (LootMap.McJannek, false) => new[] { "Monitor", "PC tower", "Graphics card", "Memory", "Keyboard", "Mouse" },
            (LootMap.Sunstone, true) => new[] { "Золотой слиток", "Кварцевый череп", "Банный набор" },
            (LootMap.Sunstone, false) => new[] { "Gold ingot", "Quartz skull", "Bath set" },
            _ => Array.Empty<string>(),
        };
        var rows = new List<HudListRow>();
        foreach (string line in lines) rows.Add(new HudListRow(line, null, HudStyle.Info));
        for (int i = 0; i < names.Length; i++)
            rows.Add(new HudListRow(names[i], null, (_accepted & 1 << i) != 0 ? HudStyle.Good : HudStyle.Info));
        int count = 0;
        for (int i = 0; i < Total; i++) if ((_accepted & 1 << i) != 0) count++;
        _clue.Footer = $"{(ru ? "ЗАСЧИТАНО" : "COMPLETE")}  {count}/{Total}";
        _clue.SetRows(rows);
    }

    private void Broadcast()
    {
        _nextSync = Time.time + 5f;
        if (!PhotonNetwork.InRoom || !PhotonNetwork.IsMasterClient) return;
        _players = PhotonNetwork.CurrentRoom.PlayerCount;
        var art = new float[_paintings.Count * 5];
        for (int i = 0; i < _paintings.Count; i++)
        {
            var p = _paintings[i];
            art[i * 5] = p.Position.x; art[i * 5 + 1] = p.Position.y; art[i * 5 + 2] = p.Position.z;
            art[i * 5 + 3] = p.Yaw; art[i * 5 + 4] = p.OnWall ? 1f : 0f;
        }
        var offeringPositions = new float[_offerings.Count * 3];
        var viewIds = new int[_offerings.Count];
        for (int i = 0; i < _offerings.Count; i++)
        {
            var o = _offerings[i];
            offeringPositions[i * 3] = o.Floor.x; offeringPositions[i * 3 + 1] = o.Floor.y; offeringPositions[i * 3 + 2] = o.Floor.z;
            viewIds[i] = o.ViewId;
        }
        PhotonNetwork.RaiseEvent(EventCode, new object[] { LevelKey(), new[] { _altar.x, _altar.y, _altar.z, _altarYaw },
            _variant, _accepted, _finished, art, offeringPositions, viewIds },
            new RaiseEventOptions { Receivers = ReceiverGroup.Others }, SendOptions.SendReliable);
    }

    // Sender validation is handled by ModNetwork.
    internal static void Receive(EventData message)
    {
        var self = _instance;
        if (self == null || LevelGenerator.Instance == null || !LevelGenerator.Instance.Generated ||
            message.CustomData is not object[] d || d.Length != 8 || d[0] is not string key || key != LevelKey() ||
            d[1] is not float[] altar || altar.Length != 4 || d[2] is not int variant || variant < 0 ||
            d[3] is not int accepted || accepted < 0 || d[4] is not bool finished || d[5] is not float[] art ||
            d[6] is not float[] offeringPositions || d[7] is not int[] viewIds)
            return;
        var map = MapHere();
        if (map is LootMap.None or LootMap.Swiftbroom || art.Length != (map == LootMap.Museum ? PaintingCount * 5 : 0)) return;
        int goal = (1 << GoalCount(map)) - 1;
        int count = viewIds.Length;
        bool validCount = map == LootMap.Manor ? count is >= 2 and <= RitualLoot.SkeletonCount && variant < count
            : count == OfferingCount(map) && variant == 0;
        if ((accepted & ~goal) != 0 || (finished && accepted != goal) ||
            !validCount || offeringPositions.Length != count * 3) return;
        foreach (float f in altar) if (!Networking.NetworkPayload.Finite(f)) return;
        foreach (float f in art) if (!Networking.NetworkPayload.Finite(f)) return;
        foreach (float f in offeringPositions) if (!Networking.NetworkPayload.Finite(f)) return;
        foreach (int id in viewIds) if (id < 0) return;
        if (self._generator != LevelGenerator.Instance || self._scene == null)
        {
            self.Reset();
            self._generator = LevelGenerator.Instance;
            self._planned = true;
            self._map = map;
            self._variant = variant;
            self._altar = new Vector3(altar[0], altar[1], altar[2]);
            self._altarYaw = altar[3];
            for (int i = 0; i < art.Length; i += 5)
                self._paintings.Add(new Painting(new Vector3(art[i], art[i + 1], art[i + 2]), art[i + 3], art[i + 4] == 1f));
            for (int i = 0; i < count; i++)
            {
                int j = i * 3;
                self._offerings.Add(new Offering
                {
                    Prefab = InteractiveValuables.RitualPrefab(OfferingKey(map, i)),
                    Floor = new Vector3(offeringPositions[j], offeringPositions[j + 1], offeringPositions[j + 2]),
                });
            }
            self.BuildVisuals();
        }
        if (self._offerings.Count != count) return;
        for (int i = 0; i < self._offerings.Count; i++)
        {
            var offering = self._offerings[i];
            if (offering.ViewId == viewIds[i]) continue;
            offering.ViewId = viewIds[i];
            offering.Value = null;
        }
        self._nextRecovery = Time.time + 2f; // let room objects resolve before a promoted guest replaces anything
        self._accepted = accepted;
        self._finished = finished;
    }

    private static void SetBackupValue(ValuableObject value)
    {
        value.dollarValueOriginal = value.dollarValueCurrent = 0f;
        value.dollarValueSet = true;
    }

    // Fixed cube value matches Swiftbroom. Replacements keep the puzzle solvable without creating extra money.
    [HarmonyPatch(typeof(ValuableObject), nameof(ValuableObject.DollarValueSetLogic))]
    private static class RewardValue
    {
        [HarmonyPrefix]
        private static void Prefix(ValuableObject __instance)
        {
            if (!SemiFunc.IsMasterClientOrSingleplayer() || __instance.dollarValueSet) return;
            if (__instance.photonView != null && __instance.photonView.InstantiationData is { Length: 1 } data &&
                data[0] is string marker && marker == BackupMarker)
                SetBackupValue(__instance);
            else if (__instance.dollarValueOverride != 0) return;
            else if (__instance.name.StartsWith(CubePrefabName, StringComparison.Ordinal))
                __instance.dollarValueOverride = 250000;
            else if (__instance.name.StartsWith(MousePrefabName, StringComparison.Ordinal))
                __instance.dollarValueOverride = Random.Range(5, 8) * 100;
            else if (__instance.name.StartsWith(KeyboardPrefabName, StringComparison.Ordinal))
                __instance.dollarValueOverride = Random.Range(9, 14) * 100;
        }
    }
}
