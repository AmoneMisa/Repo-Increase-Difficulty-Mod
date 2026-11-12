using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using C = IncreaseHardMod.Skills.SkillCatalog;

namespace IncreaseHardMod.Skills;

/// <summary>
/// Game side of the trade-off skills (numbers in <see cref="TradeOffRules"/>, catalogue in <see cref="SkillCatalog"/>).
/// The existing perk hooks ask this class for one player's combined factor per effect; the behaviours that are not a
/// number (Last Stand, Echo Ping, Oracle, Hunter's Eye) live at the bottom.
/// </summary>
internal static class TradeOffs
{
    internal enum Damage { Gun, Cart, Melee, Grenade, Tumble }

    internal static bool Has(PlayerAvatar player, string id) => SkillEffects.Has(player, id);

    internal static float Factor(PlayerAvatar player, Fx fx)
    {
        if (!SkillEffects.On || player == null || string.IsNullOrEmpty(player.steamID)) return 1f;
        string steam = player.steamID;
        return TradeOffRules.Factor(id => SkillStore.Level(steam, id) > 0, fx);
    }

    internal static float Sum(PlayerAvatar player, Fx fx)
    {
        if (!SkillEffects.On || player == null || string.IsNullOrEmpty(player.steamID)) return 0f;
        string steam = player.steamID;
        return TradeOffRules.Sum(id => SkillStore.Level(steam, id) > 0, fx);
    }

    /// <summary>Damage this player deals of one kind; with a target, Executioner's bonus or penalty too.</summary>
    internal static float DamageFactor(PlayerAvatar player, Damage kind, Enemy target = null)
    {
        if (player == null) return 1f;
        float factor = Factor(player, Fx.DamageDealt) * Factor(player, kind switch
        {
            Damage.Gun => Fx.GunDamage,
            Damage.Cart => Fx.CartDamage,
            Damage.Melee => Fx.MeleeDamage,
            Damage.Grenade => Fx.GrenadeDamage,
            _ => Fx.TumbleDamage,
        });
        return factor * TargetFactor(player, target);
    }

    /// <summary>Executioner: more damage to a disabled monster, less to any other (1 without the skill or a target).</summary>
    internal static float TargetFactor(PlayerAvatar player, Enemy target) =>
        target == null || !Has(player, C.Executioner) ? 1f : Factor(player, Disabled(target) ? Fx.StunnedTargetDamage : Fx.OtherTargetDamage);

    internal static Damage KindOf(SkillEffects.Weapon weapon) =>
        weapon is SkillEffects.Weapon.CartCannon or SkillEffects.Weapon.CartLaser or SkillEffects.Weapon.FlameCart ? Damage.Cart : Damage.Gun;

    /// <summary>Stunned (melee or tranquilizer), frozen or netted.</summary>
    internal static bool Disabled(Enemy enemy) =>
        enemy != null && (enemy.IsStunned() || Gear.GearEffects.IsFrozen(enemy) || Gear.GearEffects.IsNetted(enemy));

    /// <summary>Status length from this player's weapons, with the trade-off factor and raised cap.</summary>
    internal static float StatusSeconds(PlayerAvatar player, float seconds)
    {
        float factor = Factor(player, Fx.StatusLength);
        float cap = Mathf.Max(C.StatusCapSeconds, Sum(player, Fx.StatusCapSeconds));
        return TradeOffRules.StatusSeconds(seconds, factor, cap);
    }

    // ------------------------------------------------------------------ Last Stand (hurt player's own client)

    private static readonly HashSet<string> _lastStandUsed = new();
    private static string _lastStandLevel;

    /// <summary>True, once per level, when Last Stand turns this lethal hit into one that leaves 1 HP.</summary>
    internal static bool SpendLastStand(PlayerAvatar player)
    {
        if (player == null || !Has(player, C.LastStand)) return false;
        var run = RunManager.instance;
        string level = run != null && run.levelCurrent != null ? run.levelsCompleted + ":" + run.levelCurrent.name : "";
        if (level != _lastStandLevel) { _lastStandUsed.Clear(); _lastStandLevel = level; }
        if (!_lastStandUsed.Add(player.steamID)) return false;
        Hud.HudNotify.Toast(Localization.GameLanguage.Russian ? "ПОСЛЕДНИЙ РУБЕЖ: вы выжили с 1 HP" : "LAST STAND: you survive at 1 HP", Hud.HudStyle.Caution, 3f, "skills.laststand");
        return true;
    }

    // ------------------------------------------------------------------ Echo Ping

    private const float EchoRadius = 8f, EchoInvestigateRadius = 10f;

    /// <summary>The local player pinged: show monsters near the spot on their map for the ping's length.</summary>
    internal static void LocalPinged(Vector3 position, float seconds)
    {
        var local = SemiFunc.PlayerAvatarLocal();
        if (!Has(local, C.EchoPing)) return;
        var director = EnemyDirector.instance;
        if (director == null || director.enemiesSpawned == null) return;
        int shown = 0;
        foreach (var parent in director.enemiesSpawned)
        {
            if (parent == null || !parent.Spawned || parent.Enemy == null) continue;
            var t = parent.Enemy.transform;
            if ((t.position - position).sqrMagnitude > EchoRadius * EchoRadius) continue;
            EchoMarker.Show(t, seconds);
            shown++;
        }
        if (shown > 0)
            Hud.HudNotify.Toast(Localization.GameLanguage.Russian
                ? $"ЭХО: рядом {shown} {Quests.QuestText.Plural(shown, "монстр", "монстра", "монстров")}"
                : $"ECHO: {shown} monster{(shown == 1 ? "" : "s")} nearby", Hud.HudStyle.Caution, 2.5f, "skills.echo");
    }

    /// <summary>Host: a ping from this player draws monsters near the spot to come and look.</summary>
    internal static void HostPinged(PlayerAvatar pinger, Vector3 position)
    {
        if (!SemiFunc.IsMasterClientOrSingleplayer() || !Has(pinger, C.EchoPing) || EnemyDirector.instance == null) return;
        try { EnemyDirector.instance.SetInvestigate(position, EchoInvestigateRadius); }
        catch (System.Exception e) { Plugin.Log.LogWarning("[Skills] Echo Ping investigate failed: " + e.Message); }
    }

    /// <summary>Host: a teammate's ping arrived; Echo Ping draws monsters to it.</summary>
    internal static void HostPingedByActor(int actor, Vector3 position)
    {
        string steam = SkillStore.SteamIdOfActor(actor);
        if (!string.IsNullOrEmpty(steam)) HostPinged(SemiFunc.PlayerAvatarGetFromSteamID(steam), position);
    }

    /// <summary>A temporary map dot that follows one monster.</summary>
    private sealed class EchoMarker : MonoBehaviour
    {
        private Transform _follow;
        private float _until;

        internal static void Show(Transform monster, float seconds)
        {
            var sprite = Upgrades.MapUpgrade.MarkerController.MarkerSprite();
            if (sprite == null) return;
            var go = new GameObject("IHM_EchoMarker");
            go.transform.position = monster.position;
            var marker = go.AddComponent<EchoMarker>();
            marker._follow = monster;
            marker._until = Time.time + Mathf.Max(1f, seconds);
            var custom = go.AddComponent<MapCustom>();
            custom.autoAdd = false;
            custom.sprite = sprite;
            custom.color = new Color(1f, .35f, .35f);
            try { custom.Add(); } catch { /* the map is not ready this frame: no dot */ }
        }

        private void Update()
        {
            if (_follow == null || Time.time >= _until) { Destroy(gameObject); return; } // the map entity follows us out
            transform.position = _follow.position;
        }
    }

    // ------------------------------------------------------------------ Oracle (local map)

    private static readonly HashSet<int> _revealed = new();
    private static Map _revealedMap;

    /// <summary>Local: put every valuable on the map. Valuables shown this way are not marked "discovered", so your
    /// own discoveries still reach your team; the game's later discovery just skips the second dot.</summary>
    internal sealed class OracleController : MonoBehaviour
    {
        private float _next;

        private void Update()
        {
            if (Time.time < _next) return;
            _next = Time.time + 2f;
            var map = Map.Instance;
            if (map != _revealedMap) { _revealed.Clear(); _revealedMap = map; }
            bool level;
            try { level = SemiFunc.RunIsLevel(); } catch { level = false; }
            if (map == null || !level || !Has(SemiFunc.PlayerAvatarLocal(), C.Oracle) || ValuableDirector.instance == null) return;
            foreach (var valuable in ValuableDirector.instance.valuableList)
            {
                if (valuable == null || valuable.discovered || !_revealed.Add(valuable.GetInstanceID())) continue;
                try { map.AddValuable(valuable); } catch { _revealed.Remove(valuable.GetInstanceID()); }
            }
        }
    }

    [HarmonyPatch(typeof(ValuableObject), nameof(ValuableObject.DiscoverRPC))]
    private static class OracleNoDoubleDot
    {
        [HarmonyPrefix]
        private static bool Prefix(ValuableObject __instance)
        {
            if (__instance == null || Map.Instance != _revealedMap || !_revealed.Contains(__instance.GetInstanceID())) return true;
            __instance.discovered = true; // already on this player's map: keep the flag, skip the second dot
            return false;
        }
    }

    // ------------------------------------------------------------------ Hunter's Eye (host: monster vision)

    // While a monster looks around, a Hunter's Eye player who crouches or crawls is seen as standing.
    [HarmonyPatch]
    private static class HunterSeen
    {
        private static System.Reflection.MethodBase TargetMethod() =>
            AccessTools.EnumeratorMoveNext(AccessTools.Method(typeof(EnemyVision), nameof(EnemyVision.Vision)));

        [HarmonyPrefix]
        private static void Prefix(out List<(PlayerAvatar Player, bool Crouch, bool Crawl)> __state)
        {
            __state = null;
            var players = GameDirector.instance != null ? GameDirector.instance.PlayerList : null;
            if (players == null || !SkillEffects.On) return;
            foreach (var p in players)
            {
                if (p == null || (!p.isCrouching && !p.isCrawling) || !Has(p, C.HuntersEye)) continue;
                (__state ??= new()).Add((p, p.isCrouching, p.isCrawling));
                p.isCrouching = false;
                p.isCrawling = false;
            }
        }

        // A finalizer, so the real flags come back even if the game's vision step throws.
        [HarmonyFinalizer]
        private static System.Exception Finalizer(System.Exception __exception, List<(PlayerAvatar Player, bool Crouch, bool Crawl)> __state)
        {
            if (__state != null)
                foreach (var (p, crouch, crawl) in __state)
                    if (p != null) { p.isCrouching = crouch; p.isCrawling = crawl; }
            return __exception;
        }
    }
}
