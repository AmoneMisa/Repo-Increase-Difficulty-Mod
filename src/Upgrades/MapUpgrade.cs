using TMPro;
using System.Collections.Generic;
using System.Text;
using IncreaseHardMod.Patches.Effects;
using UnityEngine;
using RepoUpgrades = REPOLib.Modules.Upgrades;

namespace IncreaseHardMod.Upgrades;

/// <summary>
/// "Map Suite" — a single per-player upgrade with a FIXED 10-level ladder, where each level
/// unlocks one map feature. Registered via REPOLib (shop presence + per-player level tracking);
/// the effects are read from a ticking controller, so this needs no start/upgrade actions.
///
/// Feature ladder (each level ADDS to the previous):
///   L1  alive players count            L6  team pings shown on the map (3D markers)
///   L2  dead players count             L7  zoom the map view out one step
///   L3  active monsters count          L8  zoom the map view out further
///   L4  respawning monsters count      L9  cosmetic-box tracker (3D markers, Phase 3d)
///   L5  quota heading + valuable       L10 monsters shown as dots (3D markers, Phase 3d)
///       tracker (found/total + haul)
///
/// L1-L5 render in the shared right-hand details section inside the map Tab. L7-L8 hijack the map
/// view's camera zoom. L6/L9/L10 need the map's 3D marker system and are added separately.
/// Everything here is read-only/local — no networking, so it stays per-player and desync-free.
/// </summary>
internal static class MapUpgrade
{
    public const string Id = "IHM_Map";

    // Published by the local controller; the shared native HUD owns layout and pagination.
    internal static string HudInfo { get; private set; } = "";

    // Fixed feature ladder — do not expose as config; each level maps to a specific feature.
    public const int Levels = 10;

    public static void Register()
    {
        if (!ModConfig.PlayerUpgradesEnabled.Value)
            return;

        PlayerUpgradeRegistry.Define(new PlayerUpgradeRegistry.Definition
        {
            Id = Id,
            DisplayName = "Map Suite",
            MaxLevel = Levels,
        });
    }

    /// <summary>
    /// How many of the old Map Suite features the LOCAL player has unlocked in the skill tree (0 = none).
    /// The features are separate Map perks now, so check each one with <see cref="Feature"/>.
    /// </summary>
    internal static int LocalLevel()
    {
        int owned = 0;
        for (int feature = 1; feature <= Levels; feature++) if (Feature(feature)) owned++;
        return owned;
    }

    /// <summary>Old ladder feature N (1 = live players ... 10 = monster dots) is unlocked by its Map perk.</summary>
    internal static bool Feature(int n)
    {
        if (n < 1 || n > Skills.SkillMigration.MapLadder.Length) return false;
        foreach (var node in Skills.SkillMigration.MapLadder[n - 1])
            if (!Skills.SkillEffects.LocalHas(node)) return false;
        return true;
    }

    /// <summary>
    /// Reads the local Map Suite level and, while the map tool is open, shows the unlocked
    /// info lines (L1-5) in Tab and applies the zoom-out (L7-8). Added to the mod's runtime object.
    /// </summary>
    internal sealed class MapController : MonoBehaviour
    {
        private const float RefreshInterval = 0.25f;

        private static readonly string[] Headings =
            { "Ahead", "Ahead right", "Right", "Behind right", "Behind", "Behind left", "Left", "Ahead left" };
        private static readonly string[] HeadingsRu =
            { "впереди", "впереди справа", "справа", "сзади справа", "сзади", "сзади слева", "слева", "впереди слева" };
        private static bool Ru => Localization.GameLanguage.Russian;

        private float _timer;
        private bool _show;
        private LevelGenerator _generation;

        // Runtime zoom offset (in FOV units) added on top of the level's default, driven by the
        // zoom hotkeys. Persists while the game runs so re-opening the map keeps your chosen zoom.
        private static float _zoomOffset;
        private const float ZoomOffsetMin = -12f;
        private const float ZoomOffsetMax = 46f;

        private void Update()
        {
            if (_generation != LevelGenerator.Instance)
            {
                _generation = LevelGenerator.Instance;
                _show = false;
                _timer = 0f;
                HudInfo = "";
            }
            if (!ModConfig.PlayerUpgradesEnabled.Value)
            {
                _show = false;
                HudInfo = "";
                return;
            }

            bool mapOpen = LevelInfoHud.InfoReady && LevelInfoHud.TabOpen && SemiFunc.RunIsLevel();
            int level = mapOpen ? LocalLevel() : 0;

            bool wasShowing = _show;
            _show = mapOpen && level > 0;
            if (!_show)
            {
                HudInfo = "";
                _timer = 0f;
                return;
            }
            if (!wasShowing)
                _timer = 0f;

            // Only hijack the camera FOV while the local player is ALIVE. When dead/spectating the
            // map + camera are driven by the spectate system, and forcing our zoom onto that view
            // makes the map open wrong. The statistics remain available in the Tab details section.
            if (LocalAlive())
            {
                HandleZoomKeys(level);
                ApplyZoom(level);
            }

            _timer -= Time.deltaTime;
            if (_timer <= 0f)
            {
                _timer = RefreshInterval;
                HudInfo = BuildInfo(level);
            }
        }

        // True only while the local avatar exists and is not disabled (dead/spectating). Used to
        // keep our camera-FOV override off the spectate view.
        private static bool LocalAlive()
        {
            var avatar = SemiFunc.PlayerAvatarLocal();
            return avatar != null && !avatar.isDisabled;
        }

        // L7+: while the map is open, the zoom hotkeys nudge the FOV offset in/out. We accept the
        // configured keys plus the natural alternates ([ / ] and the keypad +/-) so any layout works.
        private static void HandleZoomKeys(int level)
        {
            if (!Feature(7))
                return;

            bool zoomIn = Input.GetKeyDown(ModConfig.MapZoomInKey.Value)
                          || Input.GetKeyDown(KeyCode.KeypadMinus)
                          || Input.GetKeyDown(KeyCode.LeftBracket);
            bool zoomOut = Input.GetKeyDown(ModConfig.MapZoomOutKey.Value)
                           || Input.GetKeyDown(KeyCode.KeypadPlus)
                           || Input.GetKeyDown(KeyCode.RightBracket);

            if (zoomIn == zoomOut)
                return; // neither, or both at once — do nothing

            float step = ModConfig.MapZoomStep.Value;
            _zoomOffset = Mathf.Clamp(_zoomOffset + (zoomOut ? step : -step), ZoomOffsetMin, ZoomOffsetMax);
        }

        // L7/L8: override the map view's camera FOV so more of the level is visible. The map
        // tool sets FOV 50 at priority 100 every frame; we claim priority 99 (lower number =
        // higher precedence) so ours wins for as long as the map is open at L7+. The runtime
        // zoom-hotkey offset is layered on top and clamped to a sane FOV range.
        private static void ApplyZoom(int level)
        {
            if (!Feature(7) || CameraZoom.Instance == null)
                return;

            float fov = Feature(8) ? ModConfig.MapZoomFovLevel8.Value : ModConfig.MapZoomFovLevel7.Value;
            fov = Mathf.Clamp(fov + _zoomOffset, 30f, 140f);
            CameraZoom.Instance.OverrideZoomSet(fov, 0.1f, 2f, 2f, CameraZoom.Instance.gameObject, 99);
        }

        private static string BuildInfo(int level)
        {
            var sb = new StringBuilder();

            var director = GameDirector.instance;
            if (director != null && director.PlayerList != null)
            {
                int alive = 0, dead = 0;
                foreach (var p in director.PlayerList)
                {
                    if (p == null)
                        continue;
                    if (p.isDisabled)
                        dead++;
                    else
                        alive++;
                }
                // Map Uplink shows both counts (and every teammate on the map); Dead Players is no longer a perk.
                if (Feature(1))
                    sb.Append(Ru ? "Живых игроков: " : "Players alive: ").Append(alive)
                      .Append(Ru ? ", мёртвых игроков: " : "  dead: ").Append(dead > 0 ? $"<color=#FF4545>{dead}</color>" : "0").Append('\n');
            }

            var enemyDir = EnemyDirector.instance;
            if (enemyDir != null && enemyDir.enemiesSpawned != null && (Feature(3) || Feature(4)))
            {
                int active = 0;
                foreach (var ep in enemyDir.enemiesSpawned)
                    if (ep != null && ep.Spawned)
                        active++;
                if (Feature(3))
                    sb.Append(Ru ? "Активных монстров: " : "Monsters active: ").Append(active).Append('\n');
                // Respawning Monsters: each waiting monster and its own time left (host timers, synced to guests).
                if (Feature(4))
                    sb.Append(RespawnList.Lines(8)).Append('\n');
            }

            // Quota Heading and Valuable Tracker are separate perks now (both were old level 5).
            if (Skills.SkillEffects.LocalHas(Skills.SkillCatalog.QuotaHeading))
                sb.Append(BuildQuotaLine()).Append('\n');
            if (Skills.SkillEffects.LocalHas(Skills.SkillCatalog.ValuableTracker))
                sb.Append(BuildValuableLines());

            return sb.ToString().TrimEnd('\n');
        }

        // L5: valuable tracker. Counts every ValuableObject in the level (ValuableDirector's
        // valuableList, which only ever grows), reporting how many have been discovered vs the
        // total and, from RoundDirector, the current secured haul against the quota goal plus how
        // many valuables are sitting in an extraction point right now. All read-only, so it's
        // identical for host and clients.
        private static string BuildValuableLines()
        {
            var vd = ValuableDirector.instance;
            if (vd == null || vd.valuableList == null)
                return "";

            int total = 0, discovered = 0, secured = 0;
            foreach (var v in vd.valuableList)
            {
                if (v == null)
                    continue;
                total++;
                if (v.discovered)
                    discovered++;
                if (v.roomVolumeCheck != null && v.roomVolumeCheck.inExtractionPoint)
                    secured++;
            }

            var sb = new StringBuilder();
            sb.Append(Ru ? "Ценности: найдено " : "Valuables: ").Append(discovered).Append('/').Append(total).Append(Ru ? "" : " found");

            var round = RoundDirector.instance;
            if (round != null && round.haulGoal > 0)
            {
                sb.Append('\n')
                  .Append(Ru ? "Добыча: " : "Haul: ")
                  .Append(SemiFunc.DollarGetString(Mathf.Max(0, round.currentHaul)))
                  .Append(" / ")
                  .Append(SemiFunc.DollarGetString(round.haulGoal));
                if (secured > 0)
                    sb.Append("  (").Append(secured).Append(Ru ? " в точке)" : " secured)");
            }

            return sb.ToString();
        }

        // L5: distance + compass heading (relative to where you're looking) to the closest
        // extraction point that still needs a haul. Falls back to the nearest point overall.
        private static string BuildQuotaLine()
        {
            var avatar = SemiFunc.PlayerAvatarLocal();
            if (avatar == null || RoundDirector.instance == null)
                return "";

            Vector3 from = avatar.transform.position;
            var target = SemiFunc.ExtractionPointGetNearestNotActivated(from)
                         ?? SemiFunc.ExtractionPointGetNearest(from);
            if (target == null)
                return Ru ? "Квота: (нет)" : "Quota: (none)";

            Vector3 to = target.transform.position;
            int dist = Mathf.RoundToInt(Vector3.Distance(from, to));

            var cam = PhysGrabber.instance != null ? PhysGrabber.instance.playerCamera : Camera.main;
            string arrow = cam != null ? HeadingArrow(cam.transform.forward, to - from) : "";

            return Ru ? $"Квота: {dist} м, {arrow}" : $"Quota: {dist}m {arrow}";
        }

        private static string HeadingArrow(Vector3 facing, Vector3 toTarget)
        {
            facing.y = 0f;
            toTarget.y = 0f;
            if (toTarget.sqrMagnitude < 0.001f || facing.sqrMagnitude < 0.001f)
                return "";

            float ang = Vector3.SignedAngle(facing, toTarget, Vector3.up); // + = target to the right
            float a = (ang + 360f) % 360f;
            int idx = Mathf.RoundToInt(a / 45f) % 8;
            return Ru ? HeadingsRu[idx] : Headings[idx];
        }

        private void OnDisable() => HudInfo = "";
    }

    /// <summary>Marks a world object we've already given a map marker, so we don't double-add.</summary>
    internal sealed class IhmMapMarked : MonoBehaviour { }

    /// <summary>
    /// Drives the 3D map markers: L6 mirrors live team pings, L9 tags cosmetic boxes, L10 tags
    /// active monsters. We reuse the game's own <see cref="MapCustom"/> component (attach +
    /// <c>Add()</c>) so each marker inherits the vanilla follow/layer-alpha/cleanup logic — no
    /// custom positioning. When an enemy despawns the game destroys its marker entity
    /// (<c>MapCustom.mapCustomEntity</c> goes null), which we detect to re-add on respawn.
    ///
    /// L6 pings are transient (4s) and some sit at fixed points with no world object to follow,
    /// so each active ping gets a throwaway proxy GameObject that the MapCustom tracks; the proxy
    /// is moved to the ping's current position every frame and destroyed when the ping expires or
    /// the map closes (which lets the vanilla entity coroutine self-clean).
    ///
    /// All local/read-only: attaching a component client-side isn't networked, so markers stay
    /// per-player. Only runs while the map is open on a level, and only when the level is owned.
    /// </summary>
    internal sealed class MarkerController : MonoBehaviour
    {
        private const float SweepInterval = .25f;
        private static readonly Color MonsterPurple = new(0.6f, 0.2f, 0.9f);
        private static readonly Color CosmeticPink = new(1f, 0.4f, 0.8f);
        private static readonly Color PlayerAlive = new(1f, 1f, 1f);
        private static readonly Color PlayerDead = new(1f, 0.25f, 0.25f);

        private float _timer;

        // L6: pings stay on the map for a while after they fade in the world (a ping lasts only a few seconds, usually
        // gone before the map is even raised). Each remembered ping gets a proxy GameObject the vanilla MapCustom follows.
        private const float MapPingSeconds = 15f, MapPingScale = 2.6f, RippleSeconds = 1.1f;
        private sealed class MapPing
        {
            public Vector3 Pos; public Color Color; public float Until; public GameObject Proxy;
            public Transform Dot; public Vector3 DotScale; public SpriteRenderer Ripple; public EnemyParent Enemy;
        }
        private readonly Dictionary<PingController.ActivePing, MapPing> _mapPings = new();
        private readonly List<PingController.ActivePing> _expiredScratch = new();
        private LevelGenerator _pingLevel;

        private void Update()
        {
            if (!ModConfig.PlayerUpgradesEnabled.Value)
            {
                ClearPingMarkers(forget: true);
                return;
            }
            if (_pingLevel != LevelGenerator.Instance) { _pingLevel = LevelGenerator.Instance; ClearPingMarkers(forget: true); }
            if (Feature(6)) RememberPings();

            // Markers only matter (and only position correctly) while the map is open on a level.
            if (!LevelInfoHud.InfoReady || !LevelInfoHud.TabOpen || !SemiFunc.RunIsLevel())
            {
                ClearPingMarkers(forget: false);
                ShowLabels(false);
                return;
            }
            UpdateLabels();


            // L6: mirror active pings onto the map. Runs every frame (unthrottled) so the proxies
            // track moving pings smoothly and expired pings are cleared promptly.
            if (Feature(6))
                UpdatePingMarkers();
            else
                ClearPingMarkers(forget: true);

            // L9/L10 world-object trackers: throttled sweep is plenty (boxes/monsters move slowly).
            _timer -= Time.deltaTime;
            if (_timer > 0f)
                return;
            _timer = SweepInterval;

            if (MarkerSprite() == null)
                return;

            if (Feature(1))
                SweepPlayers();
            if (Skills.SkillEffects.LocalHas(Skills.SkillCatalog.MapLandmarks))
                SweepLandmarks();

            if (Feature(9))
                SweepCosmetics();
            if (Feature(10))
                SweepMonsters();
        }

        // Record every live ping (yours and your team's) and keep it for MapPingSeconds after it was last active.
        private void RememberPings()
        {
            float now = Time.time;
            var active = PingController.Active;
            for (int i = 0; i < active.Count; i++)
            {
                var ping = active[i];
                if (ping == null) continue;
                if (!_mapPings.TryGetValue(ping, out var mark))
                    _mapPings[ping] = mark = new MapPing { Color = PingController.ColorFor(ping.Type, ping.Kind), Enemy = ping.Enemy };
                mark.Pos = ping.Pos; // follows a moving target while the ping lives
                mark.Until = now + MapPingSeconds;
            }
            _expiredScratch.Clear();
            foreach (var kv in _mapPings) if (now >= kv.Value.Until) _expiredScratch.Add(kv.Key);
            foreach (var gone in _expiredScratch)
            {
                if (_mapPings[gone].Proxy != null) Object.Destroy(_mapPings[gone].Proxy); // the vanilla entity self-destructs
                _mapPings.Remove(gone);
            }
        }

        // While the map is open: a proxy and map entity per remembered ping, drawn larger than loot dots.
        private void UpdatePingMarkers()
        {
            var sprite = MarkerSprite();
            if (sprite == null) return;
            foreach (var mark in _mapPings.Values)
            {
                if (mark.Proxy == null)
                {
                    mark.Proxy = new GameObject("IHM_PingMapProxy");
                    mark.Proxy.transform.position = mark.Pos;
                    var mc = mark.Proxy.AddComponent<MapCustom>();
                    mc.autoAdd = false;
                    // A pinged monster shows as its own icon; the ripple and beat still mark it as a ping.
                    mc.sprite = MonsterSprite(mark.Enemy, 1f) ?? sprite;
                    mc.color = mark.Color;
                    mc.fadeAlpha = 1f; // full strength on every floor: a ping is meant to be seen
                }
                mark.Proxy.transform.position = mark.Pos;
                var custom = mark.Proxy.GetComponent<MapCustom>();
                if (custom != null && custom.mapCustomEntity == null)
                {
                    mark.Dot = null;
                    try { custom.Add(); } catch { /* not addable this frame; retry next frame */ }
                    if (custom.mapCustomEntity != null) Decorate(mark, custom.mapCustomEntity);
                }
                Animate(mark);
            }
        }

        // A ping dot is much larger than a loot dot, drawn above the other markers, with a ripple ring around it.
        private static void Decorate(MapPing mark, MapCustomEntity entity)
        {
            mark.Dot = entity.transform;
            mark.DotScale = entity.transform.localScale * MapPingScale;
            var dot = entity.spriteRenderer;
            if (dot == null) return;
            dot.sortingOrder += 20;
            var ripple = new GameObject("IHM_PingRipple");
            ripple.layer = entity.gameObject.layer; // the map camera only draws its own layer
            ripple.transform.SetParent(entity.transform, false);
            mark.Ripple = ripple.AddComponent<SpriteRenderer>();
            mark.Ripple.sprite = dot.sprite;
            mark.Ripple.sharedMaterial = dot.sharedMaterial;
            mark.Ripple.sortingLayerID = dot.sortingLayerID;
            mark.Ripple.sortingOrder = dot.sortingOrder - 1;
        }

        // The dot beats gently and a ring keeps spreading out from it and fading, so pings catch the eye on a busy map.
        private static void Animate(MapPing mark)
        {
            if (mark.Dot == null) return;
            float t = Time.unscaledTime;
            mark.Dot.localScale = mark.DotScale * (1f + .15f * Mathf.Sin(t * 7f));
            if (mark.Ripple == null) return;
            float phase = Mathf.Repeat(t / RippleSeconds, 1f);
            mark.Ripple.transform.localScale = Vector3.one * (1f + 1.8f * phase);
            var c = mark.Color;
            c.a = .85f * (1f - phase) * (1f - phase);
            mark.Ripple.color = c;
        }

        // Map closed: drop the proxies (remembered pings come back when it opens). forget = also drop the memory.
        private void ClearPingMarkers(bool forget)
        {
            foreach (var mark in _mapPings.Values)
                if (mark.Proxy != null) { Object.Destroy(mark.Proxy); mark.Proxy = null; }
            if (forget) _mapPings.Clear();
        }

        private static void SweepCosmetics()
        {
            foreach (var box in Object.FindObjectsByType<CosmeticWorldObject>(FindObjectsSortMode.None))
            {
                if (box == null)
                    continue;
                if (!box.gameObject.activeInHierarchy) { RemoveMarker(box.gameObject); continue; }
                EnsureMarker(box.gameObject, box.transform, CosmeticPink);
            }
        }

        private static void SweepMonsters()
        {
            var dir = EnemyDirector.instance;
            if (dir == null || dir.enemiesSpawned == null)
                return;

            foreach (var ep in dir.enemiesSpawned)
            {
                if (ep == null || ep.Enemy == null)
                    continue;
                // The game removes a marker only when its object is disabled, but a despawned monster keeps its main
                // object active (only its body is switched off), so its dot stayed frozen where it vanished. Follow
                // the moving body, and take the dot off while the monster is away.
                var host = ep.Enemy.HasRigidbody && ep.Enemy.Rigidbody != null ? ep.Enemy.Rigidbody.gameObject : ep.Enemy.gameObject;
                if (!ep.Spawned || !host.activeInHierarchy) { RemoveMarker(host); continue; }
                EnsureMarker(host, host.transform, MonsterPurple);
                // Each kind of monster as its own icon once it has been drawn (a plain dot until then).
                var icon = MonsterSprite(ep, 1.6f);
                var mc = host.GetComponent<MapCustom>();
                if (icon != null && mc != null && mc.sprite != icon)
                {
                    mc.sprite = icon;
                    if (mc.mapCustomEntity != null && mc.mapCustomEntity.spriteRenderer != null) mc.mapCustomEntity.spriteRenderer.sprite = icon;
                }
            }
        }

        // Map Uplink: every teammate on the map whether anyone has seen them or not, white while alive and red at their
        // death head once dead. Your own position is already the map's arrow.
        private static void SweepPlayers()
        {
            var players = GameDirector.instance != null ? GameDirector.instance.PlayerList : null;
            if (players == null) return;
            foreach (var p in players)
            {
                if (p == null || p.isLocal) continue;
                var head = p.playerDeathHead != null ? p.playerDeathHead.gameObject : null;
                GameObject shown = null;
                if (p.isDisabled)
                {
                    RemoveMarker(p.gameObject);
                    if (head != null && head.activeInHierarchy)
                    {
                        EnsureMarker(head, head.transform, PlayerDead);
                        SetSprite(head, Visuals.MapIcons.Sprite(Visuals.MapIcons.Kind.DeadPlayer, IconSize(1.7f)));
                        shown = head;
                    }
                }
                else
                {
                    if (head != null) RemoveMarker(head);
                    if (p.gameObject.activeInHierarchy)
                    {
                        EnsureMarker(p.gameObject, p.transform, PlayerAlive);
                        SetSprite(p.gameObject, Visuals.MapIcons.Sprite(Visuals.MapIcons.Kind.Player, IconSize(1.7f)));
                        shown = p.gameObject;
                    }
                }
                Label(p, shown, p.isDisabled ? PlayerDead : PlayerAlive);
            }
        }

        // ---- Map Landmarks perk: the challenge shrine and every untaken battery crystal, each with its own icon.
        private static readonly Color ShrineGold = new(1f, .78f, .32f), CrystalGreen = new(.45f, 1f, .5f);

        private static void SweepLandmarks()
        {
            foreach (var shrine in Object.FindObjectsByType<Challenges.ShrineVisual>(FindObjectsSortMode.None))
            {
                if (shrine == null) continue;
                EnsureMarker(shrine.gameObject, shrine.transform, ShrineGold);
                SetSprite(shrine.gameObject, Visuals.MapIcons.Sprite(Visuals.MapIcons.Kind.Shrine, IconSize(2f)));
            }
            foreach (var crystal in Patches.Effects.RetryCrystalEffect.ReadyVisuals())
            {
                EnsureMarker(crystal, crystal.transform, CrystalGreen);
                SetSprite(crystal, Visuals.MapIcons.Sprite(Visuals.MapIcons.Kind.Crystal, IconSize(1.7f)));
            }
            foreach (var taken in Patches.Effects.RetryCrystalEffect.TakenVisuals()) RemoveMarker(taken);
        }

        // An icon's width on the map, in loot dots.
        private static float IconSize(float dots)
        {
            var dot = MarkerSprite();
            return dot == null ? 0f : Mathf.Max(dot.bounds.size.x, dot.bounds.size.y) * 1.6f * dots;
        }

        private static void SetSprite(GameObject host, Sprite sprite)
        {
            var mc = host != null ? host.GetComponent<MapCustom>() : null;
            if (mc == null || sprite == null || mc.sprite == sprite) return;
            mc.sprite = sprite;
            if (mc.mapCustomEntity != null && mc.mapCustomEntity.spriteRenderer != null) mc.mapCustomEntity.spriteRenderer.sprite = sprite;
        }

        // ---- teammates' nicknames beside their map icons (Map Uplink)
        private static readonly Dictionary<PlayerAvatar, (TextMeshPro Text, GameObject Host)> Labels = new();

        private static void Label(PlayerAvatar player, GameObject host, Color colour)
        {
            Labels.TryGetValue(player, out var entry);
            if (host == null)
            {
                if (entry.Text != null) entry.Text.gameObject.SetActive(false);
                return;
            }
            if (entry.Text == null)
            {
                entry.Text = Challenges.ChallengeProps.Label(null, Vector3.zero, 1f, colour);
                entry.Text.gameObject.name = "IHM Map Name";
                entry.Text.alignment = TextAlignmentOptions.Left;
                entry.Text.textWrappingMode = TextWrappingModes.NoWrap;
                entry.Text.overflowMode = TextOverflowModes.Overflow;
                entry.Text.rectTransform.pivot = new Vector2(0f, .5f);
            }
            entry.Host = host;
            entry.Text.text = Clean(player.playerName);
            entry.Text.color = colour;
            Labels[player] = entry;
        }

        private static string Clean(string name) => string.IsNullOrEmpty(name) ? "?" : name.Replace("<", "").Replace(">", "");

        // Every frame while the map is up: each name sits just right of its icon, faces the map camera (so it reads
        // upright however the map turns) and is as tall as the icon.
        private static void UpdateLabels()
        {
            var camera = MapCamera();
            List<PlayerAvatar> gone = null;
            foreach (var pair in Labels)
            {
                var (text, host) = pair.Value;
                if (pair.Key == null || text == null) { (gone ??= new List<PlayerAvatar>()).Add(pair.Key); if (text != null) Object.Destroy(text.gameObject); continue; }
                var mc = host != null ? host.GetComponent<MapCustom>() : null;
                var entity = mc != null ? mc.mapCustomEntity : null;
                var dot = entity != null ? entity.spriteRenderer : null;
                if (camera == null || dot == null || !Feature(1)) { text.gameObject.SetActive(false); continue; }
                var bounds = dot.bounds;
                float size = Mathf.Max(bounds.size.x, bounds.size.z, .0001f);
                text.gameObject.SetActive(true);
                text.gameObject.layer = entity.gameObject.layer; // the map camera only draws the map's own layer
                text.transform.rotation = camera.transform.rotation;
                text.fontSize = size * 9f;                        // TextMeshPro: about 0.1 units of line height per point
                text.transform.position = bounds.center + camera.transform.right * (size * .65f) + camera.transform.forward * -.001f;
                text.sortingOrder = dot.sortingOrder + 30;
            }
            if (gone != null) foreach (var p in gone) Labels.Remove(p);
        }

        private static void ShowLabels(bool show)
        {
            foreach (var entry in Labels.Values)
                if (entry.Text != null) entry.Text.gameObject.SetActive(show);
        }

        private static Camera _mapCamera;
        private static Camera MapCamera()
        {
            if (_mapCamera != null && _mapCamera.isActiveAndEnabled) return _mapCamera;
            _mapCamera = null;
            var map = Map.Instance;
            if (map == null || map.ActiveParent == null) return null;
            foreach (var camera in map.ActiveParent.GetComponentsInChildren<Camera>(true))
                if (camera.targetTexture != null) { _mapCamera = camera; break; }
            return _mapCamera;
        }

        private static void RemoveMarker(GameObject host)
        {
            var mc = host != null ? host.GetComponent<MapCustom>() : null;
            if (mc == null || mc.mapCustomEntity == null) return;
            Object.Destroy(mc.mapCustomEntity.gameObject);
            mc.mapCustomEntity = null;
        }

        // Attach a MapCustom once, then (re)create its map entity whenever it's missing.
        private static void EnsureMarker(GameObject host, Transform follow, Color color)
        {
            if (host == null)
                return;

            var mc = host.GetComponent<MapCustom>();
            if (mc == null)
            {
                mc = host.AddComponent<MapCustom>();
                mc.autoAdd = false;              // we control when it's added
                mc.sprite = MarkerSprite();
                mc.color = color;
                host.AddComponent<IhmMapMarked>();
            }

            // mapCustomEntity is nulled by the game when the entity self-destroys (e.g. the
            // enemy despawned); re-add so the marker comes back when it's active again.
            if (mc.mapCustomEntity == null)
            {
                try { mc.Add(); }
                catch { /* not addable this frame (mid level transition); retry next sweep */ }
            }
        }

        // A monster's icon sized relative to the game's loot dot (Visuals.MonsterIcons draws it from the monster).
        private static Sprite MonsterSprite(EnemyParent enemy, float dots)
        {
            var dot = MarkerSprite();
            if (enemy == null || dot == null) return null;
            float world = Mathf.Max(dot.bounds.size.x, dot.bounds.size.y) * 1.6f * dots;
            return Visuals.MonsterIcons.MapSprite(enemy, world);
        }

        // Reuse the sprite the game already uses for valuable map dots — no assets to author.
        internal static Sprite MarkerSprite()
        {
            if (Map.Instance == null || Map.Instance.ValuableObject == null)
                return null;
            var mv = Map.Instance.ValuableObject.GetComponent<MapValuable>();
            return mv != null ? mv.spriteSmall : null;
        }
    }
}

/// <summary>
/// Respawning Monsters perk: the list of monsters waiting to respawn and each one's time left. Only the host runs the
/// game's respawn timers (EnemyParent.DespawnedTimer), so the host measures them and sends the list to everyone once a
/// second (event 165); guests count each entry down locally between updates.
/// </summary>
internal sealed class RespawnList : MonoBehaviour
{
    internal const byte EventCode = 165;
    private const int MaxEntries = 24;
    private static readonly List<(string Name, float Seconds)> _entries = new();
    private static float _receivedAt;
    private static LevelGenerator _level;
    private float _next;

    private void Update()
    {
        if (_level != LevelGenerator.Instance) { _level = LevelGenerator.Instance; _entries.Clear(); }
        if (!SemiFunc.IsMasterClientOrSingleplayer() || Time.unscaledTime < _next) return;
        _next = Time.unscaledTime + 1f;
        bool level;
        try { level = SemiFunc.RunIsLevel(); } catch { level = false; }
        if (!level) { _entries.Clear(); return; }
        Measure();
        var room = Photon.Pun.PhotonNetwork.InRoom ? Photon.Pun.PhotonNetwork.CurrentRoom : null;
        if (room == null || room.PlayerCount < 2) return;
        var names = new string[_entries.Count];
        var seconds = new float[_entries.Count];
        for (int i = 0; i < _entries.Count; i++) { names[i] = _entries[i].Name; seconds[i] = _entries[i].Seconds; }
        Photon.Pun.PhotonNetwork.RaiseEvent(EventCode, new object[] { names, seconds },
            new Photon.Realtime.RaiseEventOptions { Receivers = Photon.Realtime.ReceiverGroup.Others },
            ExitGames.Client.Photon.SendOptions.SendUnreliable);
    }

    private static void Measure()
    {
        _entries.Clear();
        _receivedAt = Time.time;
        var director = EnemyDirector.instance;
        if (director == null || director.enemiesSpawned == null) return;
        foreach (var e in director.enemiesSpawned)
        {
            if (e == null || e.Spawned) continue;
            string name = string.IsNullOrEmpty(e.enemyName) ? "Monster" : e.enemyName;
            _entries.Add((name, Mathf.Max(0f, e.DespawnedTimer)));
            if (_entries.Count >= MaxEntries) break;
        }
        _entries.Sort((a, b) => a.Seconds.CompareTo(b.Seconds));
    }

    /// <summary>Guest: the host's list (ModNetwork checks the sender is the host).</summary>
    internal static void HandleEvent(ExitGames.Client.Photon.EventData e)
    {
        if (e.CustomData is not object[] d || d.Length != 2 || d[0] is not string[] names || d[1] is not float[] seconds ||
            names.Length != seconds.Length || names.Length > MaxEntries) return;
        var fresh = new List<(string, float)>();
        for (int i = 0; i < names.Length; i++)
        {
            if (!Networking.NetworkPayload.Finite(seconds[i]) || seconds[i] < 0f || seconds[i] > 3600f) return;
            string name = names[i] ?? "";
            if (name.Length > 40) name = name.Substring(0, 40);
            fresh.Add((name, seconds[i]));
        }
        _entries.Clear();
        _entries.AddRange(fresh);
        _receivedAt = Time.time;
    }

    /// <summary>"Respawning: 3" and one line per monster, soonest first, e.g. "  Huntsman 0:42".</summary>
    internal static string Lines(int maxLines)
    {
        var sb = new StringBuilder();
        bool ru = Localization.GameLanguage.Russian;
        sb.Append(ru ? "Возрождаются: " : "Respawning: ").Append(_entries.Count);
        float elapsed = Time.time - _receivedAt;
        int shown = 0;
        foreach (var (name, seconds) in _entries)
        {
            if (shown >= maxLines) { sb.Append("\n  +").Append(_entries.Count - shown).Append(ru ? " ещё" : " more"); break; }
            int left = Mathf.CeilToInt(Mathf.Max(0f, seconds - elapsed));
            sb.Append("\n  ").Append(Localization.EnemyText.Name(name)).Append(' ').Append(left / 60).Append(':').Append((left % 60).ToString("00"));
            shown++;
        }
        return sb.ToString();
    }
}
