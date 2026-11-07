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
                if (Feature(1))
                    sb.Append("Players alive: ").Append(alive).Append('\n');
                if (Feature(2))
                    sb.Append("Players dead: ").Append(dead > 0 ? $"<color=#FF4545>{dead}</color>" : "0").Append('\n');
            }

            var enemyDir = EnemyDirector.instance;
            if (enemyDir != null && enemyDir.enemiesSpawned != null && (Feature(3) || Feature(4)))
            {
                int active = 0, respawning = 0;
                foreach (var ep in enemyDir.enemiesSpawned)
                {
                    if (ep == null)
                        continue;
                    if (ep.Spawned)
                        active++;
                    else
                        respawning++;
                }
                if (Feature(3))
                    sb.Append("Monsters active: ").Append(active).Append('\n');
                if (Feature(4))
                    sb.Append("Monsters respawning: ").Append(respawning).Append('\n');
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
            sb.Append("Valuables: ").Append(discovered).Append('/').Append(total).Append(" found");

            var round = RoundDirector.instance;
            if (round != null && round.haulGoal > 0)
            {
                sb.Append('\n')
                  .Append("Haul: ")
                  .Append(SemiFunc.DollarGetString(Mathf.Max(0, round.currentHaul)))
                  .Append(" / ")
                  .Append(SemiFunc.DollarGetString(round.haulGoal));
                if (secured > 0)
                    sb.Append("  (").Append(secured).Append(" secured)");
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
                return "Quota: (none)";

            Vector3 to = target.transform.position;
            int dist = Mathf.RoundToInt(Vector3.Distance(from, to));

            var cam = PhysGrabber.instance != null ? PhysGrabber.instance.playerCamera : Camera.main;
            string arrow = cam != null ? HeadingArrow(cam.transform.forward, to - from) : "";

            return $"Quota: {dist}m {arrow}";
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
            return Headings[idx];
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
        private const float SweepInterval = 1.5f;
        private static readonly Color MonsterPurple = new(0.6f, 0.2f, 0.9f);
        private static readonly Color CosmeticPink = new(1f, 0.4f, 0.8f);

        private float _timer;

        // L6: one proxy GameObject per live ping, so the vanilla MapCustom has a transform to follow.
        private readonly Dictionary<PingController.ActivePing, GameObject> _pingProxies = new();
        private readonly List<PingController.ActivePing> _expiredScratch = new();

        private void Update()
        {
            if (!ModConfig.PlayerUpgradesEnabled.Value)
            {
                ClearPingMarkers();
                return;
            }

            // Markers only matter (and only position correctly) while the map is open on a level.
            if (!LevelInfoHud.InfoReady || !LevelInfoHud.TabOpen || !SemiFunc.RunIsLevel())
            {
                ClearPingMarkers();
                return;
            }


            // L6: mirror active pings onto the map. Runs every frame (unthrottled) so the proxies
            // track moving pings smoothly and expired pings are cleared promptly.
            if (Feature(6))
                UpdatePingMarkers();
            else
                ClearPingMarkers();

            // L9/L10 world-object trackers: throttled sweep is plenty (boxes/monsters move slowly).
            _timer -= Time.deltaTime;
            if (_timer > 0f)
                return;
            _timer = SweepInterval;

            if (!Feature(9) && !Feature(10))
                return;

            if (MarkerSprite() == null)
                return;

            if (Feature(9))
                SweepCosmetics();
            if (Feature(10))
                SweepMonsters();
        }

        // Keep the proxy set in sync with the live ping list: drop proxies for expired pings,
        // create proxies for new pings, and move each surviving proxy to its ping's position.
        private void UpdatePingMarkers()
        {
            var sprite = MarkerSprite();
            if (sprite == null)
            {
                ClearPingMarkers();
                return;
            }

            var active = PingController.Active;

            // Remove proxies whose ping is gone (expired / cleared on level change).
            _expiredScratch.Clear();
            foreach (var kv in _pingProxies)
            {
                bool stillActive = false;
                for (int i = 0; i < active.Count; i++)
                {
                    if (ReferenceEquals(active[i], kv.Key))
                    {
                        stillActive = true;
                        break;
                    }
                }
                if (!stillActive)
                    _expiredScratch.Add(kv.Key);
            }
            foreach (var gone in _expiredScratch)
            {
                if (_pingProxies.TryGetValue(gone, out var go) && go != null)
                    Object.Destroy(go); // Parent nulls -> vanilla entity coroutine self-destructs
                _pingProxies.Remove(gone);
            }

            // Ensure + position a proxy for each active ping.
            for (int i = 0; i < active.Count; i++)
            {
                var ping = active[i];
                if (!_pingProxies.TryGetValue(ping, out var proxy) || proxy == null)
                {
                    proxy = new GameObject("IHM_PingMapProxy");
                    proxy.transform.position = ping.Pos;
                    var mc = proxy.AddComponent<MapCustom>();
                    mc.autoAdd = false;
                    mc.sprite = sprite;
                    mc.color = PingController.ColorFor(ping.Type, ping.Kind);
                    try { mc.Add(); } catch { /* not addable this frame; retry below */ }
                    _pingProxies[ping] = proxy;
                    continue;
                }

                proxy.transform.position = ping.Pos;
                var existing = proxy.GetComponent<MapCustom>();
                if (existing != null && existing.mapCustomEntity == null)
                {
                    try { existing.Add(); } catch { /* retry next frame */ }
                }
            }
        }

        private void ClearPingMarkers()
        {
            if (_pingProxies.Count == 0)
                return;
            foreach (var go in _pingProxies.Values)
            {
                if (go != null)
                    Object.Destroy(go);
            }
            _pingProxies.Clear();
        }

        private static void SweepCosmetics()
        {
            foreach (var box in Object.FindObjectsOfType<CosmeticWorldObject>())
            {
                if (box == null)
                    continue;
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
                if (ep == null || !ep.Spawned)
                    continue;
                var t = ep.Enemy != null ? ep.Enemy.transform : ep.transform;
                EnsureMarker(t.gameObject, t, MonsterPurple);
            }
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
