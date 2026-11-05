using System.Collections.Generic;
using ExitGames.Client.Photon;
using HarmonyLib;
using IncreaseHardMod.Modifiers;
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using UnityEngine;
using UnityEngine.AI;

namespace IncreaseHardMod.Patches.Effects;

/// <summary>
/// MysteryBoxes modifier — a few open crates appear around the map. Dropping ONE enemy soul
/// (EnemyValuable orb) into a crate consumes the soul and, after a short spin, pays out a random
/// prize: an upgrade pickup, team money, or a weapon/grenade that only exists on this level.
///
/// Authority: the HOST places the boxes, detects souls inside them, destroys the soul and spawns
/// or pays the prize. The boxes themselves are NOT network objects: the host broadcasts box
/// positions + state (event 199) and every client builds an identical local crate. The host
/// re-sends the whole state whenever it changes or the player count changes (late joiners).
///
/// Placement: boxes sit on navigation LevelPoints (never ValuableVolumes, so they don't eat a
/// valuable spawn spot), away from the truck/start room/extraction rooms, on a floor with free
/// space around them, and spread as far apart from each other as possible.
///
/// "This level only" weapons: a level-found item is never added to StatsManager.itemsPurchased
/// (only shop extraction does that), so the game does not respawn it on the next level.
/// </summary>
internal static class MysteryBoxEffect
{
    // Photon reserves 200+; 199 is the last free custom code.
    internal const byte EventCode = 199;

    private const byte Ready = 0, Rolling = 1, Opened = 2;
    private const float RollSeconds = 2.4f;
    internal static readonly Vector3 Size = new(0.95f, 0.6f, 0.95f);

    private sealed class Box
    {
        public Vector3 Pos;
        public float Yaw;
        public byte State;
        public string Label = "";
        public float GrantAt;
        public MysteryBoxVisual Visual;
    }

    private static readonly List<Box> Boxes = new();
    private static string _key = "";
    private static int _revision;
    private static string _sentSignature;
    private static float _nextScan;

    private static string LevelKey() => RunManager.instance != null && RunManager.instance.levelCurrent != null
        ? RunManager.instance.levelsCompleted + ":" + RunManager.instance.levelCurrent.name : "";

    private static void Clear()
    {
        foreach (var b in Boxes)
            if (b.Visual != null) Object.Destroy(b.Visual.gameObject);
        Boxes.Clear();
        _key = "";
    }

    [HarmonyPatch(typeof(LevelGenerator), "GenerateDone")]
    private static class SpawnOnGenerated
    {
        [HarmonyPostfix]
        private static void Postfix()
        {
            if (!SemiFunc.IsMasterClientOrSingleplayer())
                return;
            Clear();
            if (!SemiFunc.RunIsLevel() || !ModifierManager.Current.Has(ModifierType.MysteryBoxes))
                return;
            try { Place(); }
            catch (System.Exception e) { Plugin.Log.LogError("[MysteryBox] Placement failed: " + e); Clear(); }
        }
    }

    // ---------------------------------------------------------------- host: placement

    private static void Place()
    {
        var points = SemiFunc.LevelPointsGetAll();
        if (points == null || points.Count == 0)
        {
            Plugin.Log.LogWarning("[MysteryBox] No level points; no boxes placed.");
            return;
        }

        var candidates = new List<(Vector3 pos, float yaw)>();
        foreach (var p in points)
        {
            if (p == null || p.Truck || p.inStartRoom) continue;
            if (p.Room != null && (p.Room.Truck || p.Room.Extraction)) continue;
            if (TryFloor(p.transform.position, out var floor, out float yaw))
                candidates.Add((floor, yaw));
        }

        int wanted = Mathf.Clamp(ModConfig.MysteryBoxCount.Value, 1, 8);
        var spawn = RunManager.instance != null && LevelGenerator.Instance != null ? FirstTruckPoint(points) : (Vector3?)null;
        var chosen = new List<(Vector3 pos, float yaw)>();
        while (chosen.Count < wanted && candidates.Count > 0)
        {
            // Farthest-point sampling: each new box goes where it is farthest from every
            // box already placed (and from the truck), so the three cover the whole map.
            int best = 0;
            float bestScore = -1f;
            for (int i = 0; i < candidates.Count; i++)
            {
                float score = float.MaxValue;
                foreach (var c in chosen) score = Mathf.Min(score, (candidates[i].pos - c.pos).sqrMagnitude);
                if (spawn.HasValue) score = Mathf.Min(score, (candidates[i].pos - spawn.Value).sqrMagnitude * 0.6f);
                score *= Random.Range(0.85f, 1f); // a little variety between identical layouts
                if (score > bestScore) { bestScore = score; best = i; }
            }
            chosen.Add(candidates[best]);
            candidates.RemoveAt(best);
        }

        _key = LevelKey();
        foreach (var c in chosen)
            Boxes.Add(new Box { Pos = c.pos, Yaw = c.yaw });
        _revision++;
        Plugin.Log.LogInfo($"[MysteryBox] Placed {Boxes.Count}/{wanted} boxes ({candidates.Count + chosen.Count} usable points).");
    }

    private static Vector3? FirstTruckPoint(List<LevelPoint> points)
    {
        foreach (var p in points)
            if (p != null && (p.Truck || p.inStartRoom)) return p.transform.position;
        return null;
    }

    // Floor under the point, with enough empty space for the crate plus a walking margin.
    private static bool TryFloor(Vector3 point, out Vector3 floor, out float yaw)
    {
        floor = default;
        yaw = 90f * Random.Range(0, 4);
        int mask = SemiFunc.LayerMaskGetVisionObstruct();
        if (!Physics.Raycast(point + Vector3.up * 1.2f, Vector3.down, out var hit, 4f, mask, QueryTriggerInteraction.Ignore))
            return false;
        if (Vector3.Angle(hit.normal, Vector3.up) > 12f)
            return false;
        floor = hit.point;

        var rot = Quaternion.Euler(0f, yaw, 0f);
        var center = floor + Vector3.up * (Size.y * 0.5f + 0.08f);
        // Nothing (walls, props, valuables, items) may already occupy the crate's footprint.
        if (Physics.CheckBox(center, new Vector3(Size.x * 0.5f + 0.15f, Size.y * 0.5f, Size.z * 0.5f + 0.15f), rot,
                ~0, QueryTriggerInteraction.Ignore))
            return false;
        // Keep at least a body's width of space around it so it never plugs a corridor or doorway.
        var chest = floor + Vector3.up * 0.9f;
        int open = 0;
        foreach (var dir in new[] { Vector3.forward, Vector3.back, Vector3.left, Vector3.right })
            if (!Physics.Raycast(chest, dir, 1.6f, mask, QueryTriggerInteraction.Ignore)) open++;
        if (open < 3)
            return false;
        // Head room, so a held soul can be lowered into it.
        return !Physics.Raycast(floor + Vector3.up * 0.2f, Vector3.up, 2f, mask, QueryTriggerInteraction.Ignore);
    }

    // ---------------------------------------------------------------- per-frame

    /// <summary>Called every frame from ModNetwork.Update (host and clients).</summary>
    internal static void Tick()
    {
        if (Boxes.Count == 0)
            return;
        // Another level's state (or this client hasn't caught up to the host's level yet):
        // keep it, build nothing. The host clears on its next GenerateDone; clients replace
        // it when the host's next state arrives. The crates themselves die with the scene.
        if (_key != LevelKey())
            return;
        if (LevelGenerator.Instance == null || !LevelGenerator.Instance.Generated)
            return;

        for (int i = 0; i < Boxes.Count; i++)
        {
            var b = Boxes[i];
            if (b.Visual == null)
            {
                b.Visual = MysteryBoxVisual.Create(b.Pos, b.Yaw, i);
                b.Visual.SetState(b.State, b.Label, animate: false);
            }
        }

        if (!SemiFunc.IsMasterClientOrSingleplayer())
            return;

        if (Time.time >= _nextScan)
        {
            _nextScan = Time.time + 0.2f;
            // Fallback sweep (e.g. a soul set down very gently inside); the trigger usually wins.
            foreach (var b in Boxes)
                if (b.State == Ready) TryConsumeSoul(b);
        }

        foreach (var b in Boxes)
            if (b.State == Rolling && Time.time >= b.GrantAt)
            {
                b.Label = Grant(b);
                b.State = Opened;
                b.Visual.SetState(Opened, b.Label, animate: true);
                Announce(b.Label);
                _revision++;
            }

        Broadcast(force: false);
    }

    /// <summary>Host: a collider entered a box's catch zone above its floor plate.</summary>
    internal static void SoulTouched(int index, Collider c)
    {
        if (!SemiFunc.IsMasterClientOrSingleplayer() || index < 0 || index >= Boxes.Count || _key != LevelKey())
            return;
        var b = Boxes[index];
        if (b.State == Ready) Consume(b, c);
    }

    // Consumes the soul the instant it crosses the rim, while it is still falling: a hard drop onto the
    // floor plate would otherwise run the native impact/break logic (and the moon-4 soul explosion) first.
    private static bool Consume(Box b, Collider c)
    {
        var soul = c != null ? c.GetComponentInParent<EnemyValuable>() : null;
        var grab = soul != null ? soul.GetComponent<PhysGrabObject>() : null;
        if (grab == null || grab.dead) return false;
        grab.DestroyPhysGrabObject();
        b.State = Rolling;
        b.GrantAt = Time.time + RollSeconds;
        if (b.Visual != null) b.Visual.SetState(Rolling, "", animate: true);
        _revision++;
        Plugin.Log.LogInfo($"[MysteryBox] Soul consumed by box at {b.Pos}.");
        return true;
    }

    private static bool TryConsumeSoul(Box b)
    {
        var rot = Quaternion.Euler(0f, b.Yaw, 0f);
        var center = b.Pos + Vector3.up * (Size.y * 0.5f + 0.05f);
        var hits = Physics.OverlapBox(center, new Vector3(Size.x * 0.5f - 0.07f, Size.y * 0.5f, Size.z * 0.5f - 0.07f), rot,
            ~0, QueryTriggerInteraction.Ignore);
        foreach (var h in hits)
            if (Consume(b, h)) return true;
        return false;
    }

    // ---------------------------------------------------------------- host: prizes

    private static string Grant(Box b)
    {
        int wUp = Mathf.Max(0, ModConfig.MysteryBoxWeightUpgrade.Value);
        int wMoney = Mathf.Max(0, ModConfig.MysteryBoxWeightMoney.Value);
        int wWeapon = Mathf.Max(0, ModConfig.MysteryBoxWeightWeapon.Value);
        if (wUp + wMoney + wWeapon == 0) wMoney = 1;
        int roll = Random.Range(0, wUp + wMoney + wWeapon);

        var drop = b.Pos + Vector3.up * (Size.y + 0.45f);
        var rot = Quaternion.Euler(0f, b.Yaw, 0f);
        if (roll < wUp)
        {
            var item = Quests.QuestRewards.PickAvailableUpgrade();
            if (item != null && Quests.QuestRewards.SpawnItemAt(item, drop, rot))
                return "UPGRADE: " + DisplayName(item);
        }
        else if (roll >= wUp + wMoney)
        {
            var item = Quests.QuestRewards.PickWeaponOrGrenade();
            if (item != null && Quests.QuestRewards.SpawnItemAt(item, drop, rot))
                return DisplayName(item) + " (THIS LEVEL)";
        }
        // Money (also the fallback when nothing could be spawned).
        int lo = Mathf.Max(1000, ModConfig.MysteryBoxMoneyMin.Value);
        int hi = Mathf.Max(lo, ModConfig.MysteryBoxMoneyMax.Value);
        float depth = 1f + 0.05f * Difficulty.Steps;
        int dollars = Mathf.RoundToInt(Random.Range(lo, hi + 1) * depth / 1000f) * 1000;
        dollars = Mathf.Max(1000, dollars);
        Quests.QuestMoney.Grant(dollars);
        return "$" + dollars.ToString("N0");
    }

    private static string DisplayName(Item item) =>
        (string.IsNullOrEmpty(item.itemName) || item.itemName == "N/A" ? item.name : item.itemName).ToUpperInvariant();

    private static void Announce(string label)
    {
        if (string.IsNullOrEmpty(label)) return;
        // The game's own focus text: native font, size, glow and placement.
        SemiFunc.UIFocusText("MYSTERY BOX: " + label, new Color(0.85f, 0.55f, 1f), Color.white, 3f);
    }

    // ---------------------------------------------------------------- networking

    private static void Broadcast(bool force)
    {
        if (!SemiFunc.IsMultiplayer() || !PhotonNetwork.InRoom || !PhotonNetwork.IsMasterClient)
            return;
        string signature = _key + "|" + _revision + "|" + PhotonNetwork.CurrentRoom.PlayerCount;
        if (!force && signature == _sentSignature)
            return;
        _sentSignature = signature;

        var data = new float[Boxes.Count * 4];
        var states = new byte[Boxes.Count];
        var labels = new string[Boxes.Count];
        for (int i = 0; i < Boxes.Count; i++)
        {
            data[i * 4] = Boxes[i].Pos.x;
            data[i * 4 + 1] = Boxes[i].Pos.y;
            data[i * 4 + 2] = Boxes[i].Pos.z;
            data[i * 4 + 3] = Boxes[i].Yaw;
            states[i] = Boxes[i].State;
            labels[i] = Boxes[i].Label ?? "";
        }
        PhotonNetwork.RaiseEvent(EventCode, new object[] { _key, data, states, labels },
            new RaiseEventOptions { Receivers = ReceiverGroup.Others }, SendOptions.SendReliable);
    }

    /// <summary>Client: apply the host's full box state.</summary>
    internal static void HandleEvent(EventData e)
    {
        if (e.CustomData is not object[] d || d.Length != 4 || d[0] is not string key || key.Length > 256 ||
            d[1] is not float[] data || d[2] is not byte[] states || d[3] is not string[] labels ||
            states.Length > 8 || data.Length != states.Length * 4 || labels.Length != states.Length)
            return;
        for (int i = 0; i < data.Length; i++)
            if (!Networking.NetworkPayload.Finite(data[i]) || Mathf.Abs(data[i]) > 100000f) return;
        foreach (var s in states) if (s > Opened) return;
        foreach (var l in labels) if (l == null || l.Length > 128) return;

        bool sameLevel = key == _key && Boxes.Count == states.Length;
        if (!sameLevel)
        {
            Clear();
            _key = key;
            for (int i = 0; i < states.Length; i++)
                Boxes.Add(new Box
                {
                    Pos = new Vector3(data[i * 4], data[i * 4 + 1], data[i * 4 + 2]), Yaw = data[i * 4 + 3],
                    State = states[i], Label = labels[i],
                });
            return; // first sight of this level's boxes: show them as they are, no announcements
        }

        for (int i = 0; i < states.Length; i++)
        {
            var b = Boxes[i];
            if (b.State == states[i]) continue;
            b.State = states[i];
            b.Label = labels[i];
            if (b.Visual != null) b.Visual.SetState(b.State, b.Label, animate: true);
            if (b.State == Opened) Announce(b.Label);
        }
    }
}

/// <summary>Local-only crate visual + colliders. Destroyed with the level scene.</summary>
internal sealed class MysteryBoxVisual : MonoBehaviour
{
    private static readonly Color Purple = new(0.62f, 0.25f, 1f);
    private Transform _shell, _mark;
    private TextMeshPro _text;
    private Light _light;
    private Material _trim;
    private byte _state;
    private float _rollStart = -10f, _openedAt = -10f;
    private Vector3 _markRest;

    internal static MysteryBoxVisual Create(Vector3 floor, float yaw, int index)
    {
        var root = new GameObject("IHM Mystery Box");
        root.transform.SetPositionAndRotation(floor, Quaternion.Euler(0f, yaw, 0f));
        var v = root.AddComponent<MysteryBoxVisual>();
        v.Build(index);
        return v;
    }

    private static Material Mat(Color color, float emission)
    {
        var m = new Material(Shader.Find("Standard")) { name = "IHM Mystery Box", color = color };
        if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", 0.35f);
        if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0.5f);
        if (emission > 0f)
        {
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", color * emission);
        }
        return m;
    }

    private void Build(int index)
    {
        var s = MysteryBoxEffect.Size;
        const float t = 0.07f;
        var body = Mat(new Color(0.16f, 0.15f, 0.2f), 0f);
        _trim = Mat(Purple, 2.2f);

        _shell = new GameObject("Shell").transform;
        _shell.SetParent(transform, false);

        // Open-top crate: floor plate and four walls, each a solid collider so souls can rest inside.
        Part("Bottom", new Vector3(0f, t * 0.5f, 0f), new Vector3(s.x, t, s.z), body, true);
        Part("WallN", new Vector3(0f, s.y * 0.5f, s.z * 0.5f - t * 0.5f), new Vector3(s.x, s.y, t), body, true);
        Part("WallS", new Vector3(0f, s.y * 0.5f, -s.z * 0.5f + t * 0.5f), new Vector3(s.x, s.y, t), body, true);
        Part("WallE", new Vector3(s.x * 0.5f - t * 0.5f, s.y * 0.5f, 0f), new Vector3(t, s.y, s.z - 2 * t), body, true);
        Part("WallW", new Vector3(-s.x * 0.5f + t * 0.5f, s.y * 0.5f, 0f), new Vector3(t, s.y, s.z - 2 * t), body, true);
        // Glowing rim and corner posts.
        float r = 0.035f, top = s.y + r * 0.5f;
        Part("RimN", new Vector3(0f, top, s.z * 0.5f), new Vector3(s.x + r, r, r), _trim, false);
        Part("RimS", new Vector3(0f, top, -s.z * 0.5f), new Vector3(s.x + r, r, r), _trim, false);
        Part("RimE", new Vector3(s.x * 0.5f, top, 0f), new Vector3(r, r, s.z + r), _trim, false);
        Part("RimW", new Vector3(-s.x * 0.5f, top, 0f), new Vector3(r, r, s.z + r), _trim, false);
        foreach (var (x, z) in new[] { (1, 1), (1, -1), (-1, 1), (-1, -1) })
            Part("Post", new Vector3(x * s.x * 0.5f, s.y * 0.5f, z * s.z * 0.5f), new Vector3(r, s.y, r), _trim, false);
        // A glowing band across each outer face.
        Part("BandN", new Vector3(0f, s.y * 0.45f, s.z * 0.5f + 0.004f), new Vector3(s.x * 0.8f, 0.05f, 0.01f), _trim, false);
        Part("BandS", new Vector3(0f, s.y * 0.45f, -s.z * 0.5f - 0.004f), new Vector3(s.x * 0.8f, 0.05f, 0.01f), _trim, false);

        // Floating "?" above the crate, in the game's own font asset.
        _mark = new GameObject("Mark").transform;
        _mark.SetParent(transform, false);
        _markRest = new Vector3(0f, s.y + 0.55f, 0f);
        _mark.localPosition = _markRest;
        _text = _mark.gameObject.AddComponent<TextMeshPro>();
        var font = NativeFont();
        if (font != null) _text.font = font;
        _text.text = "?";
        _text.fontSize = 6f;
        _text.alignment = TextAlignmentOptions.Center;
        _text.color = new Color(0.9f, 0.75f, 1f);
        _text.rectTransform.sizeDelta = new Vector2(1f, 1f);

        // Catch zone: from just above the floor plate to a little over the rim. On "Ignore Raycast" so
        // grab/ping/vision raycasts never hit it; trigger events still fire against the soul's rigidbody.
        var zone = new GameObject("Catch Zone");
        zone.transform.SetParent(transform, false);
        int ignoreRaycast = LayerMask.NameToLayer("Ignore Raycast");
        if (ignoreRaycast >= 0) zone.layer = ignoreRaycast;
        var trigger = zone.AddComponent<BoxCollider>();
        trigger.isTrigger = true;
        trigger.center = new Vector3(0f, (t + s.y + 0.2f) * 0.5f + 0.02f, 0f);
        trigger.size = new Vector3(s.x - 2 * t - 0.02f, s.y + 0.2f - t, s.z - 2 * t - 0.02f);
        zone.AddComponent<MysteryBoxTrigger>().index = index;

        // Monsters path around the crate instead of walking into it and grinding against a static wall.
        var obstacle = gameObject.AddComponent<NavMeshObstacle>();
        obstacle.shape = NavMeshObstacleShape.Box;
        obstacle.center = new Vector3(0f, s.y * 0.5f, 0f);
        obstacle.size = new Vector3(s.x, s.y, s.z);
        obstacle.carving = true;
        obstacle.carveOnlyStationary = true;

        var lightGo = new GameObject("Light");
        lightGo.transform.SetParent(transform, false);
        lightGo.transform.localPosition = new Vector3(0f, s.y + 0.3f, 0f);
        _light = lightGo.AddComponent<Light>();
        _light.type = LightType.Point;
        _light.color = Purple;
        _light.range = 4.5f;
        _light.intensity = 1.6f;
        _light.shadows = LightShadows.None;
    }

    private void Part(string name, Vector3 pos, Vector3 size, Material mat, bool solid)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(_shell, false);
        go.transform.localPosition = pos;
        go.transform.localScale = size;
        go.GetComponent<MeshRenderer>().sharedMaterial = mat;
        if (!solid) Destroy(go.GetComponent<Collider>());
    }

    private static TMP_FontAsset _font;
    private static TMP_FontAsset NativeFont()
    {
        if (_font != null) return _font;
        // Prefer the HUD's live font (Teko); fall back to any loaded Teko asset.
        var hud = HUDCanvas.instance != null ? HUDCanvas.instance.GetComponentInChildren<TMP_Text>(true) : null;
        if (hud != null && hud.font != null) return _font = hud.font;
        foreach (var f in Resources.FindObjectsOfTypeAll<TMP_FontAsset>())
            if (f != null && f.name.IndexOf("Teko", System.StringComparison.OrdinalIgnoreCase) >= 0) return _font = f;
        return null;
    }

    internal void SetState(byte state, string label, bool animate)
    {
        _state = state;
        if (state == 1)
        {
            _rollStart = Time.time;
            if (animate) Visuals.ItemAudio.Cue("ping_upgrade", 0.6f).Play(transform.position);
        }
        else if (state == 2)
        {
            _openedAt = animate ? Time.time : -10f;
            if (animate) Visuals.ItemAudio.Cue("ping_cosmetic", 0.7f).Play(transform.position);
            _trim.SetColor("_EmissionColor", new Color(0.3f, 0.3f, 0.35f) * 0.4f);
            _trim.color = new Color(0.3f, 0.3f, 0.35f);
        }
    }

    private void Update()
    {
        if (_mark == null) return;
        float now = Time.time;
        var cam = Camera.main;

        if (_state == 0)
        {
            _mark.gameObject.SetActive(true);
            _mark.localPosition = _markRest + Vector3.up * (Mathf.Sin(now * 2f) * 0.06f);
            _light.intensity = 1.4f + Mathf.Sin(now * 3f) * 0.3f;
            _shell.localPosition = Vector3.zero;
        }
        else if (_state == 1)
        {
            float k = Mathf.Clamp01((now - _rollStart) / 2.4f);
            _mark.gameObject.SetActive(true);
            _mark.localPosition = _markRest + Vector3.up * (0.25f * k);
            _mark.Rotate(0f, Mathf.Lerp(360f, 1800f, k) * Time.deltaTime, 0f, Space.World);
            _light.intensity = 2f + 2.5f * k * Mathf.Abs(Mathf.Sin(now * 18f));
            _shell.localPosition = new Vector3(Mathf.Sin(now * 47f), 0f, Mathf.Cos(now * 41f)) * (0.012f * k);
        }
        else
        {
            _shell.localPosition = Vector3.zero;
            float since = now - _openedAt;
            _mark.gameObject.SetActive(false);
            _light.intensity = since < 0.6f ? Mathf.Lerp(5f, 0f, since / 0.6f) : 0f;
            _light.enabled = _light.intensity > 0.01f;
        }

        // The "?" faces the viewer when it is not spinning hard.
        if (_state == 0 && cam != null)
        {
            var to = _mark.position - cam.transform.position;
            to.y = 0f;
            if (to.sqrMagnitude > 0.01f)
                _mark.rotation = Quaternion.LookRotation(to) * Quaternion.Euler(0f, Mathf.Sin(now * 1.5f) * 25f, 0f);
        }
    }
}

/// <summary>Forwards souls entering a crate's catch zone to the host logic.</summary>
internal sealed class MysteryBoxTrigger : MonoBehaviour
{
    internal int index;
    private void OnTriggerEnter(Collider other) => MysteryBoxEffect.SoulTouched(index, other);
    private void OnTriggerStay(Collider other) => MysteryBoxEffect.SoulTouched(index, other);
}
