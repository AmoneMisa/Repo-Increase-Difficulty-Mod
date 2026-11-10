using System.Collections.Generic;
using ExitGames.Client.Photon;
using HarmonyLib;
using IncreaseHardMod.Skills;
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using UnityEngine;

namespace IncreaseHardMod.Patches.Effects;

/// <summary>
/// Skill boxes: on every run level the host places 1-5 small glowing boxes (config) around the map. The first
/// living player to walk into one opens it and gets 0-3 skill points (65/20/10/5 by default) for their own tree.
///
/// Like mystery boxes, the boxes are not network objects: the host owns placement, opening and the reward, and
/// broadcasts the full state (event 175); every client builds identical local boxes. Placement uses navigation
/// LevelPoints, never valuable spawn spots, away from the truck, start room, extraction rooms and mystery boxes.
/// The boxes have no colliders, so they never block a corridor, a monster or a thrown valuable.
/// </summary>
internal static class SkillBoxEffect
{
    internal const byte EventCode = 175;

    private const byte Ready = 0, Opened = 1;
    private const float TouchRadius = 0.8f, TouchHeight = 1.8f, MinSpacing = 6f;
    internal static readonly Vector3 Size = new(0.42f, 0.42f, 0.42f);

    private sealed class Box
    {
        public Vector3 Pos;
        public float Yaw;
        public byte State, Points;
        public int Opener = -1;           // actor number of the player who opened it (0 in single player)
        public string OpenerName = "";
        public SkillBoxVisual Visual;
    }

    private static readonly List<Box> Boxes = new();
    private static string _key = "";
    private static bool _placePending;
    private static int _revision;
    private static string _sentSignature;
    private static float _nextScan;

    private static string LevelKey() => RunManager.instance != null && RunManager.instance.levelCurrent != null
        ? RunManager.instance.levelsCompleted + ":" + RunManager.instance.levelCurrent.name : "";

    /// <summary>Positions of this level's skill boxes (host), so other placed props keep clear of them.</summary>
    internal static IEnumerable<Vector3> Positions()
    {
        if (_key != LevelKey()) yield break;
        foreach (var b in Boxes) yield return b.Pos;
    }

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
            if (!SemiFunc.IsMasterClientOrSingleplayer()) return;
            Clear();
            _placePending = SemiFunc.RunIsLevel() && ModConfig.SkillBoxesEnabled.Value && ModConfig.PlayerUpgradesEnabled.Value;
        }
    }

    // ---------------------------------------------------------------- host: placement

    private static void Place()
    {
        _placePending = false;
        _key = LevelKey();
        _revision++;
        int wanted = SkillBoxRules.Count(Random.value, ModConfig.SkillBoxMin.Value, ModConfig.SkillBoxMax.Value);
        var points = SemiFunc.LevelPointsGetAll();
        if (wanted <= 0 || points == null || points.Count == 0)
        {
            Plugin.Log.LogInfo($"[SkillBox] No boxes this level (wanted {wanted}).");
            return;
        }

        var avoid = new List<Vector3>(MysteryBoxEffect.Positions());
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

        // Random spots, but never two boxes (or a box and a mystery box / the truck) close together.
        while (Boxes.Count < wanted && candidates.Count > 0)
        {
            int i = Random.Range(0, candidates.Count);
            var c = candidates[i];
            candidates.RemoveAt(i);
            if (!FarEnough(c.pos, avoid)) continue;
            Boxes.Add(new Box { Pos = c.pos, Yaw = c.yaw });
            avoid.Add(c.pos);
        }
        Plugin.Log.LogInfo($"[SkillBox] Placed {Boxes.Count}/{wanted} skill boxes.");
    }

    private static bool FarEnough(Vector3 pos, List<Vector3> avoid)
    {
        foreach (var a in avoid)
            if ((a - pos).sqrMagnitude < MinSpacing * MinSpacing) return false;
        return true;
    }

    // ---------------------------------------------------------------- per-frame

    /// <summary>Called every frame from ModNetwork.Update (host and clients).</summary>
    internal static void Tick()
    {
        bool host = SemiFunc.IsMasterClientOrSingleplayer();
        if (LevelGenerator.Instance == null || !LevelGenerator.Instance.Generated) return;
        // Placed a frame after generation, so this level's mystery boxes already exist to keep clear of.
        if (host && _placePending)
        {
            try { Place(); }
            catch (System.Exception e) { Plugin.Log.LogError("[SkillBox] Placement failed: " + e); Clear(); }
        }
        if (Boxes.Count == 0 || _key != LevelKey()) return;

        for (int i = 0; i < Boxes.Count; i++)
        {
            var b = Boxes[i];
            if (b.Visual == null)
            {
                b.Visual = SkillBoxVisual.Create(b.Pos, b.Yaw);
                b.Visual.SetState(b.State, b.Points, animate: false);
            }
        }

        if (!host) return;
        if (Time.time >= _nextScan)
        {
            _nextScan = Time.time + 0.15f;
            foreach (var b in Boxes)
                if (b.State == Ready) TryOpen(b);
        }
        Broadcast(force: false);
    }

    private static void TryOpen(Box b)
    {
        var players = SemiFunc.PlayerGetList();
        if (players == null) return;
        PlayerAvatar opener = null;
        float best = TouchRadius * TouchRadius;
        foreach (var p in players)
        {
            if (p == null || p.isDisabled || string.IsNullOrEmpty(p.steamID)) continue;
            var d = p.transform.position - b.Pos;
            if (d.y < -0.6f || d.y > TouchHeight) continue;
            float flat = d.x * d.x + d.z * d.z;
            if (flat < best) { best = flat; opener = p; }
        }
        if (opener == null) return;

        // Lucky Finder moves weight from an empty box to the point results.
        var w = Skills.PerkRules.LuckyWeights(ModConfig.SkillBoxWeight0.Value, ModConfig.SkillBoxWeight1.Value,
            ModConfig.SkillBoxWeight2.Value, ModConfig.SkillBoxWeight3.Value,
            Skills.SkillEffects.Level(opener, Skills.SkillCatalog.LuckyFinder) + Mathf.RoundToInt(Skills.TradeOffs.Sum(opener, Skills.Fx.LuckyLevels)),
            ModConfig.SkillLuckyBoxShift.Value);
        int points = SkillBoxRules.RollPoints(Random.value, w[0], w[1], w[2], w[3]);
        if (points > 0) SkillStore.GrantPoints(opener.steamID, points, "skill box");
        b.State = Opened;
        b.Points = (byte)points;
        b.Opener = opener.photonView != null && opener.photonView.Owner != null ? opener.photonView.Owner.ActorNumber : 0;
        b.OpenerName = SemiFunc.PlayerGetName(opener) ?? "";
        _revision++;
        Plugin.Log.LogInfo($"[SkillBox] {b.OpenerName} opened a skill box: {points} point(s).");
        if (b.Visual != null) b.Visual.SetState(Opened, b.Points, animate: true);
        Announce(b);
    }

    private static bool Mine(Box b) => !PhotonNetwork.InRoom || PhotonNetwork.LocalPlayer.ActorNumber == b.Opener;

    private static void Announce(Box b)
    {
        // The game's own focus text: native font, size, glow and placement.
        var teal = new Color(0.35f, 0.9f, 1f);
        bool ru = Localization.GameLanguage.Russian;
        string points = ru ? "+" + b.Points + " " + Quests.QuestText.Plural(b.Points, "ОЧКО", "ОЧКА", "ОЧКОВ") + " НАВЫКОВ"
            : b.Points == 1 ? "+1 SKILL POINT" : "+" + b.Points + " SKILL POINTS";
        if (Mine(b))
            SemiFunc.UIFocusText(b.Points > 0 ? (ru ? "ЯЩИК НАВЫКОВ: " : "SKILL BOX: ") + points : ru ? "ЯЩИК НАВЫКОВ ПУСТ" : "SKILL BOX: EMPTY", teal, Color.white, 3f);
        else if (b.Points > 0)
            SemiFunc.UIFocusText(b.OpenerName.ToUpperInvariant() + (ru ? " ПОЛУЧАЕТ " : " FOUND ") + points, teal, Color.white, 3f);
    }

    // ---------------------------------------------------------------- pings

    /// <summary>The nearest unopened skill box the ray passes through (it has no colliders, so physics rays never
    /// identify it). <paramref name="marker"/> is where the ping marker sits.</summary>
    internal static bool TryAim(Ray ray, float maxDistance, out Vector3 marker, out float distance)
    {
        marker = default;
        distance = float.MaxValue;
        if (Boxes.Count == 0 || _key != LevelKey()) return false;
        foreach (var b in Boxes)
        {
            if (b.State != Ready || b.Visual == null || !b.Visual.isActiveAndEnabled) continue;
            if (!PingTargets.RayBox(ray, b.Pos, 0f, Size * 1.25f, maxDistance, out float d) || d >= distance) continue;
            distance = d;
            marker = b.Pos + Vector3.up * (Size.y * 0.5f);
        }
        return distance < float.MaxValue;
    }

    /// <summary>Flare the unopened box at (or nearest to) a pinged marker, on any client.</summary>
    internal static void Flare(Vector3 near, float seconds)
    {
        Box best = null;
        float bestSq = 1f;
        foreach (var b in Boxes)
        {
            if (b.State != Ready || b.Visual == null) continue;
            float sq = (b.Pos + Vector3.up * (Size.y * 0.5f) - near).sqrMagnitude;
            if (sq < bestSq) { bestSq = sq; best = b; }
        }
        best?.Visual.Flare(seconds);
    }

    internal static string PingLabel(bool info) => Localization.GameLanguage.Russian
        ? (info ? "Ящик навыков (0–3 очка)" : "Ящик навыков")
        : (info ? "Skill Box (0-3 skill points)" : "Skill Box");

    // ---------------------------------------------------------------- networking

    private static void Broadcast(bool force)
    {
        if (!SemiFunc.IsMultiplayer() || !PhotonNetwork.InRoom || !PhotonNetwork.IsMasterClient) return;
        string signature = _key + "|" + _revision + "|" + PhotonNetwork.CurrentRoom.PlayerCount;
        if (!force && signature == _sentSignature) return;
        _sentSignature = signature;

        var data = new float[Boxes.Count * 4];
        var states = new byte[Boxes.Count];
        var points = new byte[Boxes.Count];
        var openers = new int[Boxes.Count];
        var names = new string[Boxes.Count];
        for (int i = 0; i < Boxes.Count; i++)
        {
            var b = Boxes[i];
            data[i * 4] = b.Pos.x; data[i * 4 + 1] = b.Pos.y; data[i * 4 + 2] = b.Pos.z; data[i * 4 + 3] = b.Yaw;
            states[i] = b.State; points[i] = b.Points; openers[i] = b.Opener; names[i] = b.OpenerName ?? "";
        }
        PhotonNetwork.RaiseEvent(EventCode, new object[] { _key, data, states, points, openers, names },
            new RaiseEventOptions { Receivers = ReceiverGroup.Others }, SendOptions.SendReliable);
    }

    /// <summary>Client: apply the host's full skill box state.</summary>
    internal static void HandleEvent(EventData e)
    {
        if (e.CustomData is not object[] d || d.Length != 6 || d[0] is not string key || key.Length > 256 ||
            d[1] is not float[] data || d[2] is not byte[] states || d[3] is not byte[] points ||
            d[4] is not int[] openers || d[5] is not string[] names || states.Length > SkillBoxRules.MaxBoxes ||
            data.Length != states.Length * 4 || points.Length != states.Length || openers.Length != states.Length ||
            names.Length != states.Length)
            return;
        for (int i = 0; i < data.Length; i++)
            if (!Networking.NetworkPayload.Finite(data[i]) || Mathf.Abs(data[i]) > 100000f) return;
        for (int i = 0; i < states.Length; i++)
            if (states[i] > Opened || points[i] > SkillBoxRules.MaxPoints || names[i] == null || names[i].Length > 64) return;

        if (key != _key || Boxes.Count != states.Length)
        {
            Clear();
            _key = key;
            for (int i = 0; i < states.Length; i++)
                Boxes.Add(new Box
                {
                    Pos = new Vector3(data[i * 4], data[i * 4 + 1], data[i * 4 + 2]), Yaw = data[i * 4 + 3],
                    State = states[i], Points = points[i], Opener = openers[i], OpenerName = names[i],
                });
            return; // first sight of this level's boxes: show them as they are, no announcements
        }

        for (int i = 0; i < states.Length; i++)
        {
            var b = Boxes[i];
            if (b.State == states[i]) continue;
            b.State = states[i]; b.Points = points[i]; b.Opener = openers[i]; b.OpenerName = names[i];
            if (b.Visual != null) b.Visual.SetState(b.State, b.Points, animate: true);
            if (b.State == Opened) Announce(b);
        }
    }
}

/// <summary>
/// Local-only skill box visual (no colliders). Destroyed with the level scene.
/// A rounded present: a bevelled body under a slightly wider bevelled lid, two glowing ribbons wrapped round it
/// and a two-loop bow on top, all faceted meshes (no hard cube silhouette). A ping makes it flare.
/// </summary>
internal sealed class SkillBoxVisual : MonoBehaviour
{
    private static readonly Color Teal = new(0.3f, 0.85f, 1f);
    private static Mesh _bodyMesh, _trimMesh;
    private Transform _shell, _mark;
    private TextMeshPro _text;
    private Light _light;
    private byte _state;
    private float _openedAt = -10f, _flareUntil = -10f;
    private Vector3 _markRest;

    /// <summary>Pinged: spin faster, bob and glow brighter for <paramref name="seconds"/>.</summary>
    internal void Flare(float seconds) => _flareUntil = Mathf.Max(_flareUntil, Time.time + Mathf.Max(0.5f, seconds));

    internal static SkillBoxVisual Create(Vector3 floor, float yaw)
    {
        var root = new GameObject("IHM Skill Box");
        root.transform.SetPositionAndRotation(floor, Quaternion.Euler(0f, yaw, 0f));
        var v = root.AddComponent<SkillBoxVisual>();
        v.Build();
        return v;
    }

    private static Material Mat(Color color, float emission)
    {
        var m = new Material(Shader.Find("Standard")) { name = "IHM Skill Box", color = color };
        if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", 0.45f);
        if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0.4f);
        if (emission > 0f)
        {
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", color * emission);
        }
        return m;
    }

    private void Build()
    {
        var s = SkillBoxEffect.Size;
        var body = Mat(new Color(0.12f, 0.16f, 0.2f), 0f);
        var trim = Mat(Teal, 2.4f);

        _shell = new GameObject("Shell").transform;
        _shell.SetParent(transform, false);
        EnsureMeshes(s);
        Part("Body", _bodyMesh, body);
        Part("Trim", _trimMesh, trim);

        // Floating "?" (later "+N") above the box, in the game's own font asset.
        // Built with a RectTransform: adding TextMeshPro to a plain Transform swaps it out, which left _mark
        // pointing at a destroyed Transform and stopped Update (opened boxes never disappeared).
        _mark = new GameObject("Mark", typeof(RectTransform)).transform;
        _mark.SetParent(transform, false);
        _markRest = new Vector3(0f, s.y + 0.45f, 0f);
        _mark.localPosition = _markRest;
        _text = _mark.gameObject.AddComponent<TextMeshPro>();
        var font = NativeFont();
        if (font != null) _text.font = font;
        _text.text = "?";
        _text.fontSize = 4f;
        _text.alignment = TextAlignmentOptions.Center;
        _text.color = new Color(0.75f, 0.95f, 1f);
        _text.rectTransform.sizeDelta = new Vector2(1f, 1f);

        var lightGo = new GameObject("Light");
        lightGo.transform.SetParent(transform, false);
        lightGo.transform.localPosition = new Vector3(0f, s.y + 0.25f, 0f);
        _light = lightGo.AddComponent<Light>();
        _light.type = LightType.Point;
        _light.color = Teal;
        _light.range = 3f;
        _light.intensity = 1.2f;
        _light.shadows = LightShadows.None;
    }

    // Shared by every skill box (kept across levels). Built from the box's floor up to Size.y.
    private static void EnsureMeshes(Vector3 s)
    {
        if (_bodyMesh != null && _trimMesh != null) return;
        Matrix4x4 At(float x, float y, float z, Vector3 size, Vector3 euler = default) =>
            Visuals.ProcMesh.TRS(new Vector3(x, y, z), euler, size);
        _bodyMesh = new Visuals.ProcMesh()
            .ChamferBox(At(0f, s.y * 0.39f, 0f, new Vector3(s.x * 0.9f, s.y * 0.78f, s.z * 0.9f)), 0.16f)
            .ChamferBox(At(0f, s.y * 0.84f, 0f, new Vector3(s.x, s.y * 0.24f, s.z)), 0.3f)
            .Build("IHM Skill Box Body");
        float r = 0.045f;
        _trimMesh = new Visuals.ProcMesh()
            // Ribbons wrap over the lid and down both sides; slightly proud of the body so they read as bands.
            .ChamferBox(At(0f, s.y * 0.5f, 0f, new Vector3(r, s.y * 1.02f, s.z * 1.04f)), 0.35f)
            .ChamferBox(At(0f, s.y * 0.5f, 0f, new Vector3(s.x * 1.04f, s.y * 1.02f, r)), 0.35f)
            // Bow: two flattened loops tilted up and out from a small knot.
            .Diamond(At(-0.075f, s.y + 0.045f, 0f, new Vector3(0.15f, 0.07f, 0.06f), new Vector3(0f, 0f, -28f)))
            .Diamond(At(0.075f, s.y + 0.045f, 0f, new Vector3(0.15f, 0.07f, 0.06f), new Vector3(0f, 0f, 28f)))
            .ChamferBox(At(0f, s.y + 0.03f, 0f, new Vector3(0.06f, 0.055f, 0.06f)), 0.3f)
            .Build("IHM Skill Box Trim");
        _bodyMesh.hideFlags = _trimMesh.hideFlags = HideFlags.HideAndDontSave;
    }

    private void Part(string name, Mesh mesh, Material mat)
    {
        // Mesh only, no collider: walk-through, never blocks players, monsters or loot.
        var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
        go.transform.SetParent(_shell, false);
        go.GetComponent<MeshFilter>().sharedMesh = mesh;
        go.GetComponent<MeshRenderer>().sharedMaterial = mat;
    }

    private static TMP_FontAsset _font;
    private static TMP_FontAsset NativeFont()
    {
        if (_font != null) return _font;
        var hud = HUDCanvas.instance != null ? HUDCanvas.instance.GetComponentInChildren<TMP_Text>(true) : null;
        if (hud != null && hud.font != null) return _font = hud.font;
        foreach (var f in Resources.FindObjectsOfTypeAll<TMP_FontAsset>())
            if (f != null && f.name.IndexOf("Teko", System.StringComparison.OrdinalIgnoreCase) >= 0) return _font = f;
        return null;
    }

    internal void SetState(byte state, byte points, bool animate)
    {
        _state = state;
        if (state != 1) return;
        _openedAt = animate ? Time.time : -10f;
        _text.text = points > 0 ? "+" + points : "0";
        if (animate) Visuals.ItemAudio.Cue(points > 0 ? "ping_upgrade" : "ping_cosmetic", 0.6f).Play(transform.position);
    }

    private void Update()
    {
        if (_mark == null) return;
        float now = Time.time;
        if (_state == 0)
        {
            bool flaring = now < _flareUntil;
            float flare = PingTargets.FlarePulse(_flareUntil);
            _mark.localPosition = _markRest + Vector3.up * (Mathf.Sin(now * 2.2f) * 0.05f + (flaring ? 0.1f : 0f));
            _shell.localRotation = Quaternion.Euler(0f, now * (flaring ? 90f : 25f), 0f);
            _shell.localPosition = Vector3.up * (flaring ? 0.04f + flare * 0.03f : 0f);
            _light.intensity = flaring ? 2.2f + flare : 1.1f + Mathf.Sin(now * 3f) * 0.25f;
        }
        else
        {
            // Opened: the result floats up and fades, then the box is gone.
            float since = now - _openedAt;
            _shell.gameObject.SetActive(false);
            if (since > 2.5f) { gameObject.SetActive(false); return; }
            float k = since / 2.5f;
            _mark.localPosition = _markRest + Vector3.up * (0.6f * k);
            var c = _text.color; c.a = 1f - k; _text.color = c;
            _light.intensity = Mathf.Lerp(3f, 0f, k);
        }

        var cam = Camera.main;
        if (cam != null)
        {
            var to = _mark.position - cam.transform.position;
            to.y = 0f;
            if (to.sqrMagnitude > 0.01f) _mark.rotation = Quaternion.LookRotation(to);
        }
    }
}
