using System.Collections.Generic;
using ExitGames.Client.Photon;
using HarmonyLib;
using IncreaseHardMod.Modifiers;
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using UnityEngine;

namespace IncreaseHardMod.Patches.Effects;

/// <summary>
/// Battery crystals: when a run level is retried (the fail-vote restart or random map, the host's Esc restart, or the
/// Timed Level running out), the game keeps every player's HP and every item's battery charge from the attempt before.
/// To help, the host places 3-8 small glowing crystals (config) around the map. A living player who walks into one
/// tops up the emptiest battery item they hold or carry by 20% (config), once. A player whose batteries are all full
/// leaves the crystal for someone else.
///
/// Like skill boxes, crystals are not network objects: the host owns placement and pickup and broadcasts the state
/// (event 176). The charge itself is set on the host's ItemBattery, which the game syncs to everyone as usual.
/// Placement uses navigation LevelPoints, never valuable spawn spots, away from the truck, start room, extraction
/// rooms, mystery boxes and skill boxes. Crystals have no colliders.
/// </summary>
internal static class RetryCrystalEffect
{
    internal const byte EventCode = 176;

    private const byte Ready = 0, Taken = 1;
    private const float TouchRadius = 0.75f, TouchHeight = 1.8f, MinSpacing = 4.5f;
    internal static readonly Vector3 Size = new(0.3f, 0.5f, 0.3f);

    private sealed class Crystal
    {
        public Vector3 Pos;
        public float Yaw;
        public byte State;
        public int Taker = -1;           // actor number of the player who took it (0 in single player)
        public string Item = "";
        public RetryCrystalVisual Visual;
    }

    private static readonly List<Crystal> Crystals = new();
    private static string _key = "";
    private static string _lastRunStep;
    private static bool _placePending;
    private static int _revision;
    private static string _sentSignature;
    private static float _nextScan;

    private static string LevelKey() => RunManager.instance != null && RunManager.instance.levelCurrent != null
        ? RunManager.instance.levelsCompleted + ":" + RunManager.instance.levelCurrent.name : "";

    private static void Clear()
    {
        foreach (var c in Crystals)
            if (c.Visual != null) Object.Destroy(c.Visual.gameObject);
        Crystals.Clear();
        _key = "";
    }

    [HarmonyPatch(typeof(LevelGenerator), "GenerateDone")]
    private static class SpawnOnGenerated
    {
        [HarmonyPostfix]
        private static void Postfix()
        {
            if (!SemiFunc.IsMasterClientOrSingleplayer()) return;
            Clear();
            bool runLevel = SemiFunc.RunIsLevel() && RunManager.instance != null;
            string step = runLevel
                ? RetryCrystalRules.RunStep(StatsManager.instance != null ? StatsManager.instance.saveFileCurrent : "", RunManager.instance.levelsCompleted)
                : null;
            bool retry = RetryCrystalRules.IsRetry(_lastRunStep, step);
            _lastRunStep = step; // a shop, lobby, menu or arena in between ends the chain
            _placePending = retry && ModConfig.RetryCrystalsEnabled.Value;
            if (retry) Plugin.Log.LogInfo($"[Crystals] Retried level (step {step}).");
        }
    }

    // ---------------------------------------------------------------- host: placement

    private static void Place()
    {
        _placePending = false;
        _key = LevelKey();
        _revision++;
        int wanted = RetryCrystalRules.Count(Random.value, ModConfig.RetryCrystalMin.Value, ModConfig.RetryCrystalMax.Value);
        var points = SemiFunc.LevelPointsGetAll();
        if (wanted <= 0 || points == null || points.Count == 0)
        {
            Plugin.Log.LogInfo($"[Crystals] No crystals this level (wanted {wanted}).");
            return;
        }

        var avoid = new List<Vector3>(MysteryBoxEffect.Positions());
        avoid.AddRange(SkillBoxEffect.Positions());
        foreach (var p in points)
            if (p != null && (p.Truck || p.inStartRoom)) { avoid.Add(p.transform.position); break; }

        var candidates = new List<(Vector3 pos, float yaw)>();
        foreach (var p in points)
        {
            if (p == null || p.Truck || p.inStartRoom) continue;
            if (p.Room != null && (p.Room.Truck || p.Room.Extraction)) continue;
            if (MysteryBoxEffect.TryFloor(p.transform.position, Size, out var floor, out float yaw) && FarEnough(floor, avoid))
                candidates.Add((floor, yaw));
        }

        while (Crystals.Count < wanted && candidates.Count > 0)
        {
            int i = Random.Range(0, candidates.Count);
            var c = candidates[i];
            candidates.RemoveAt(i);
            if (!FarEnough(c.pos, avoid)) continue;
            Crystals.Add(new Crystal { Pos = c.pos, Yaw = Random.Range(0f, 360f) });
            avoid.Add(c.pos);
        }
        Plugin.Log.LogInfo($"[Crystals] Placed {Crystals.Count}/{wanted} battery crystals.");
    }

    private static bool FarEnough(Vector3 pos, List<Vector3> avoid)
    {
        foreach (var a in avoid)
            if ((a - pos).sqrMagnitude < MinSpacing * MinSpacing) return false;
        return true;
    }

    // ---------------------------------------------------------------- per-frame

    /// <summary>Called every frame from ModNetwork.Update (host and clients), after SkillBoxEffect.Tick.</summary>
    internal static void Tick()
    {
        bool host = SemiFunc.IsMasterClientOrSingleplayer();
        if (LevelGenerator.Instance == null || !LevelGenerator.Instance.Generated) return;
        // Placed a frame after generation, so this level's mystery and skill boxes already exist to keep clear of.
        if (host && _placePending)
        {
            try { Place(); }
            catch (System.Exception e) { Plugin.Log.LogError("[Crystals] Placement failed: " + e); Clear(); }
        }
        if (Crystals.Count == 0 || _key != LevelKey()) return;

        foreach (var c in Crystals)
            if (c.Visual == null)
            {
                c.Visual = RetryCrystalVisual.Create(c.Pos, c.Yaw);
                c.Visual.SetState(c.State, animate: false);
            }

        if (!host) return;
        if (Time.time >= _nextScan)
        {
            _nextScan = Time.time + 0.15f;
            foreach (var c in Crystals)
                if (c.State == Ready) TryTake(c);
        }
        Broadcast(force: false);
    }

    private static void TryTake(Crystal c)
    {
        var players = SemiFunc.PlayerGetList();
        if (players == null) return;
        foreach (var p in players)
        {
            if (p == null || p.isDisabled) continue;
            var d = p.transform.position - c.Pos;
            if (d.y < -0.6f || d.y > TouchHeight || d.x * d.x + d.z * d.z > TouchRadius * TouchRadius) continue;
            var battery = EmptiestBattery(p);
            if (battery == null) continue; // nothing to charge: leave it for someone else

            float before = battery.batteryLife;
            battery.batteryLife = RetryCrystalRules.Charge(before, ChargeFor(p));
            c.State = Taken;
            c.Taker = p.photonView != null && p.photonView.Owner != null ? p.photonView.Owner.ActorNumber : 0;
            c.Item = ItemName(battery);
            _revision++;
            Plugin.Log.LogInfo($"[Crystals] {SemiFunc.PlayerGetName(p)} charged {c.Item}: {before:0}% -> {battery.batteryLife:0}%.");
            if (c.Visual != null) c.Visual.SetState(Taken, animate: true);
            Announce(c);
            return;
        }
    }

    // The emptiest not-full battery among the items the player holds or carries in an inventory slot.
    private static ItemBattery EmptiestBattery(PlayerAvatar player)
    {
        var grabber = player.physGrabber;
        int grabberView = grabber != null && grabber.photonView != null ? grabber.photonView.ViewID : -1;
        var mine = new List<ItemBattery>();
        foreach (var battery in Object.FindObjectsOfType<ItemBattery>())
        {
            if (battery == null || !battery.isActiveAndEnabled) continue;
            var grab = battery.GetComponent<PhysGrabObject>();
            bool held = grab != null && grabber != null && grab.playerGrabbing != null && grab.playerGrabbing.Contains(grabber);
            var equip = battery.GetComponent<ItemEquippable>();
            bool carried = equip != null && equip.IsEquipped() &&
                           (!SemiFunc.IsMultiplayer() || (grabberView > 0 && equip.ownerPlayerId == grabberView));
            if (held || carried) mine.Add(battery);
        }
        var charges = new float[mine.Count];
        for (int i = 0; i < mine.Count; i++) charges[i] = mine[i].batteryLife;
        int best = RetryCrystalRules.Emptiest(charges);
        return best >= 0 ? mine[best] : null;
    }

    /// <summary>Charge one crystal gives this player: the config value plus their Lucky Finder bonus.</summary>
    private static float ChargeFor(PlayerAvatar player) =>
        Skills.PerkRules.LuckyCrystalCharge(ModConfig.RetryCrystalCharge.Value,
            Skills.SkillEffects.Level(player, Skills.SkillCatalog.LuckyFinder), ModConfig.SkillLuckyCrystalBonus.Value);

    private static string ItemName(ItemBattery battery)
    {
        var item = battery.GetComponent<ItemAttributes>()?.item;
        string name = item == null ? battery.name : string.IsNullOrEmpty(item.itemName) || item.itemName == "N/A" ? item.name : item.itemName;
        name = (name ?? "").Replace("(Clone)", "").Trim().ToUpperInvariant();
        return name.Length > 48 ? name.Substring(0, 48) : name;
    }

    private static void Announce(Crystal c)
    {
        if (PhotonNetwork.InRoom && PhotonNetwork.LocalPlayer.ActorNumber != c.Taker) return;
        // The game's own focus text: native font, size, glow and placement.
        SemiFunc.UIFocusText($"BATTERY CRYSTAL: +{ChargeFor(SemiFunc.PlayerAvatarLocal()):0}% {c.Item}", new Color(0.55f, 1f, 0.45f), Color.white, 3f);
    }

    // ---------------------------------------------------------------- networking

    private static void Broadcast(bool force)
    {
        if (!SemiFunc.IsMultiplayer() || !PhotonNetwork.InRoom || !PhotonNetwork.IsMasterClient) return;
        string signature = _key + "|" + _revision + "|" + PhotonNetwork.CurrentRoom.PlayerCount;
        if (!force && signature == _sentSignature) return;
        _sentSignature = signature;

        var data = new float[Crystals.Count * 4];
        var states = new byte[Crystals.Count];
        var takers = new int[Crystals.Count];
        var items = new string[Crystals.Count];
        for (int i = 0; i < Crystals.Count; i++)
        {
            var c = Crystals[i];
            data[i * 4] = c.Pos.x; data[i * 4 + 1] = c.Pos.y; data[i * 4 + 2] = c.Pos.z; data[i * 4 + 3] = c.Yaw;
            states[i] = c.State; takers[i] = c.Taker; items[i] = c.Item ?? "";
        }
        PhotonNetwork.RaiseEvent(EventCode, new object[] { _key, data, states, takers, items },
            new RaiseEventOptions { Receivers = ReceiverGroup.Others }, SendOptions.SendReliable);
    }

    /// <summary>Client: apply the host's full crystal state.</summary>
    internal static void HandleEvent(EventData e)
    {
        if (e.CustomData is not object[] d || d.Length != 5 || d[0] is not string key || key.Length > 256 ||
            d[1] is not float[] data || d[2] is not byte[] states || d[3] is not int[] takers || d[4] is not string[] items ||
            states.Length > RetryCrystalRules.MaxCrystals || data.Length != states.Length * 4 ||
            takers.Length != states.Length || items.Length != states.Length)
            return;
        for (int i = 0; i < data.Length; i++)
            if (!Networking.NetworkPayload.Finite(data[i]) || Mathf.Abs(data[i]) > 100000f) return;
        for (int i = 0; i < states.Length; i++)
            if (states[i] > Taken || items[i] == null || items[i].Length > 64) return;

        if (key != _key || Crystals.Count != states.Length)
        {
            Clear();
            _key = key;
            for (int i = 0; i < states.Length; i++)
                Crystals.Add(new Crystal
                {
                    Pos = new Vector3(data[i * 4], data[i * 4 + 1], data[i * 4 + 2]), Yaw = data[i * 4 + 3],
                    State = states[i], Taker = takers[i], Item = items[i],
                });
            return; // first sight of this level's crystals: show them as they are, no announcements
        }

        for (int i = 0; i < states.Length; i++)
        {
            var c = Crystals[i];
            if (c.State == states[i]) continue;
            c.State = states[i]; c.Taker = takers[i]; c.Item = items[i];
            if (c.Visual != null) c.Visual.SetState(c.State, animate: true);
            if (c.State == Taken) Announce(c);
        }
    }
}

/// <summary>Local-only battery crystal visual (no colliders). Destroyed with the level scene.</summary>
internal sealed class RetryCrystalVisual : MonoBehaviour
{
    private static readonly Color Green = new(0.45f, 1f, 0.35f);
    private Transform _gem;
    private Light _light;
    private byte _state;
    private float _takenAt = -10f;
    private float _phase;

    internal static RetryCrystalVisual Create(Vector3 floor, float yaw)
    {
        var root = new GameObject("IHM Battery Crystal");
        root.transform.SetPositionAndRotation(floor, Quaternion.Euler(0f, yaw, 0f));
        var v = root.AddComponent<RetryCrystalVisual>();
        v.Build();
        return v;
    }

    private void Build()
    {
        _phase = Random.Range(0f, 6.28f);
        var mat = new Material(Shader.Find("Standard")) { name = "IHM Battery Crystal", color = Green };
        if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", 0.9f);
        mat.EnableKeyword("_EMISSION");
        mat.SetColor("_EmissionColor", Green * 2.2f);

        var s = RetryCrystalEffect.Size;
        _gem = new GameObject("Gem").transform;
        _gem.SetParent(transform, false);
        _gem.localPosition = new Vector3(0f, s.y * 0.5f + 0.12f, 0f);
        // A tall diamond: a cube turned onto its corner and stretched upward, plus a small shard beside it.
        Shard(Vector3.zero, new Vector3(s.x, s.y, s.x), mat);
        Shard(new Vector3(0.11f, -0.1f, 0.04f), new Vector3(s.x * 0.45f, s.y * 0.5f, s.x * 0.45f), mat);

        var lightGo = new GameObject("Light");
        lightGo.transform.SetParent(transform, false);
        lightGo.transform.localPosition = new Vector3(0f, s.y + 0.15f, 0f);
        _light = lightGo.AddComponent<Light>();
        _light.type = LightType.Point;
        _light.color = Green;
        _light.range = 2.5f;
        _light.intensity = 1f;
        _light.shadows = LightShadows.None;
    }

    private void Shard(Vector3 pos, Vector3 size, Material mat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = "Shard";
        go.transform.SetParent(_gem, false);
        go.transform.localPosition = pos;
        go.transform.localRotation = Quaternion.Euler(45f, 0f, 45f);
        go.transform.localScale = size * 0.72f;
        go.GetComponent<MeshRenderer>().sharedMaterial = mat;
        Destroy(go.GetComponent<Collider>()); // walk-through: never blocks players, monsters or loot
    }

    internal void SetState(byte state, bool animate)
    {
        _state = state;
        if (state == 1)
        {
            _takenAt = animate ? Time.time : -10f;
            if (animate) Visuals.ItemAudio.Cue("ping_upgrade", 0.45f).Play(transform.position);
        }
    }

    private void Update()
    {
        if (_gem == null) return;
        float now = Time.time;
        if (_state == 0)
        {
            _gem.localRotation = Quaternion.Euler(0f, now * 40f + _phase * 57f, 0f);
            _gem.localPosition = new Vector3(0f, RetryCrystalEffect.Size.y * 0.5f + 0.12f + Mathf.Sin(now * 2f + _phase) * 0.04f, 0f);
            _light.intensity = 0.9f + Mathf.Sin(now * 3f + _phase) * 0.25f;
            return;
        }
        // Taken: a quick flash while the gem shrinks away, then it is gone.
        float k = (now - _takenAt) / 0.5f;
        if (k >= 1f) { gameObject.SetActive(false); return; }
        _gem.localScale = Vector3.one * (1f - k);
        _light.intensity = Mathf.Lerp(4f, 0f, k);
    }
}
