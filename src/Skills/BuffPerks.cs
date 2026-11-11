using System.Collections.Generic;
using HarmonyLib;
using Photon.Pun;
using Photon.Realtime;
using ExitGames.Client.Photon;
using UnityEngine;
using C = IncreaseHardMod.Skills.SkillCatalog;

namespace IncreaseHardMod.Skills;

internal enum Buff : byte { Adrenaline = 0, IronSkin = 1, HotBarrel = 2, Overheat = 3, BattleTrance = 4, MendingSurge = 5 }

/// <summary>
/// Instincts: quest-only contract perks that fire a short buff when something happens in play, then rest on a
/// cooldown. Level 1/2/3: the buff lasts 3/4/5 s and recharges in 60/45/30 s (see <see cref="PerkRules.BuffSeconds"/>).
///  - Adrenaline: a monster hurts you -> +30% speed (your own client).
///  - Iron Skin: your health drops below 35% -> 40% less damage taken (your own client).
///  - Hot Barrel: you kill a monster with a bullet gun -> +30% bullet gun damage (host).
///  - Overheat: you hit a monster with a laser -> +30% laser damage (host).
///  - Battle Trance: three melee hits within 4 s -> +30% melee damage (host).
///  - Mending Surge: you kill a monster -> heal 5/10/15% of your max health over 5 s (host).
/// The host tells a player when a host-side buff starts (event 167) so their HUD shows it.
/// </summary>
internal static class BuffPerks
{
    internal const byte EventCode = 167;
    internal const float SpeedFactor = 1.3f, IronSkinFactor = .6f, IronSkinThreshold = .35f, DamageFactor = 1.3f;
    private const float TranceWindow = 4f, KillCredit = 5f, SurgeSeconds = 5f;

    internal static string NodeOf(Buff buff) => buff switch
    {
        Buff.Adrenaline => C.Adrenaline,
        Buff.IronSkin => C.IronSkin,
        Buff.HotBarrel => C.HotBarrel,
        Buff.Overheat => C.Overheat,
        Buff.BattleTrance => C.BattleTrance,
        _ => C.MendingSurge,
    };

    private static string Label(Buff buff, bool russian) => buff switch
    {
        Buff.Adrenaline => russian ? "АДРЕНАЛИН  +30% СКОРОСТИ" : "ADRENALINE  +30% SPEED",
        Buff.IronSkin => russian ? "ЖЕЛЕЗНАЯ КОЖА  -40% УРОНА" : "IRON SKIN  -40% DAMAGE TAKEN",
        Buff.HotBarrel => russian ? "РАСКАЛЁННЫЙ СТВОЛ  +30% УРОНА ОРУЖИЯ" : "HOT BARREL  +30% GUN DAMAGE",
        Buff.Overheat => russian ? "ПЕРЕГРЕВ  +30% УРОНА ЛАЗЕРА" : "OVERHEAT  +30% LASER DAMAGE",
        Buff.BattleTrance => russian ? "БОЕВОЙ ТРАНС  +30% УРОНА В БЛИЖНЕМ БОЮ" : "BATTLE TRANCE  +30% MELEE DAMAGE",
        _ => russian ? "ПРИЛИВ СИЛ  ЛЕЧЕНИЕ" : "MENDING SURGE  HEALING",
    };

    // Active-until and ready-at per player and buff (host for host buffs, each client for its own local ones).
    private static readonly Dictionary<(string, Buff), (float Until, float Ready)> State = new();
    // What this client shows on its HUD: buff -> until.
    private static readonly Dictionary<Buff, float> Shown = new();

    private static int Level(PlayerAvatar player, Buff buff) => SkillEffects.Level(player, NodeOf(buff));

    internal static bool Active(PlayerAvatar player, Buff buff) =>
        player != null && !string.IsNullOrEmpty(player.steamID) && State.TryGetValue((player.steamID, buff), out var s) && s.Until > Time.time;

    /// <summary>Start a buff if the player has its perk and it is off cooldown. Returns true when it fired.</summary>
    private static bool Trigger(PlayerAvatar player, Buff buff)
    {
        if (player == null || string.IsNullOrEmpty(player.steamID)) return false;
        int level = Level(player, buff);
        if (level <= 0) return false;
        var key = (player.steamID, buff);
        float now = Time.time;
        if (State.TryGetValue(key, out var s) && s.Ready > now) return false;
        float seconds = PerkRules.BuffSeconds(level);
        State[key] = (now + seconds, now + PerkRules.BuffCooldown(level));
        if (buff == Buff.MendingSurge) StartSurge(player, level);
        if (player.isLocal) Show(buff, seconds);
        else if (PhotonNetwork.InRoom && PhotonNetwork.IsMasterClient && player.photonView != null && player.photonView.Owner != null)
            PhotonNetwork.RaiseEvent(EventCode, new object[] { (byte)buff, seconds },
                new RaiseEventOptions { TargetActors = new[] { player.photonView.Owner.ActorNumber } }, SendOptions.SendReliable);
        return true;
    }

    private static void Show(Buff buff, float seconds) => Shown[buff] = Time.time + Mathf.Clamp(seconds, 0f, 10f);

    /// <summary>This client: the host says one of our buffs started (ModNetwork checks the sender).</summary>
    internal static void HandleEvent(EventData e)
    {
        if (e.CustomData is not object[] d || d.Length != 2 || d[0] is not byte kind || kind > (byte)Buff.MendingSurge ||
            d[1] is not float seconds || !Networking.NetworkPayload.Finite(seconds) || seconds <= 0f || seconds > 10f) return;
        Show((Buff)kind, seconds);
    }

    // ------------------------------------------------------------------ damage (host)

    /// <summary>Host: outgoing damage factor from Hot Barrel, Overheat and Battle Trance.</summary>
    internal static float DamageFactorFor(PlayerAvatar player, bool melee, bool laser, bool gun)
    {
        if (player == null) return 1f;
        float f = 1f;
        if (melee && Active(player, Buff.BattleTrance)) f *= DamageFactor;
        if (laser && Active(player, Buff.Overheat)) f *= DamageFactor;
        if (gun && !laser && Active(player, Buff.HotBarrel)) f *= DamageFactor;
        return f;
    }

    private enum HitKind : byte { Gun, Laser, Melee, Other }
    private static readonly Dictionary<Enemy, (PlayerAvatar Player, HitKind Kind, float At)> LastHit = new();
    private static readonly Dictionary<string, List<float>> MeleeHits = new();

    /// <summary>Host: a player hit a monster (from the shared EnemyHurt hook). Feeds kill credit and hit triggers.</summary>
    internal static void NoteHit(PlayerAvatar player, Enemy enemy, bool melee, bool laser, bool gun)
    {
        if (!SemiFunc.IsMasterClientOrSingleplayer() || player == null || enemy == null) return;
        LastHit[enemy] = (player, melee ? HitKind.Melee : laser ? HitKind.Laser : gun ? HitKind.Gun : HitKind.Other, Time.time);
        if (laser) Trigger(player, Buff.Overheat);
        if (melee && !string.IsNullOrEmpty(player.steamID))
        {
            if (!MeleeHits.TryGetValue(player.steamID, out var times)) MeleeHits[player.steamID] = times = new List<float>();
            times.Add(Time.time);
            times.RemoveAll(t => Time.time - t > TranceWindow);
            if (times.Count >= 3 && Trigger(player, Buff.BattleTrance)) times.Clear();
        }
    }

    /// <summary>Host: a monster died; whoever hit it last (within 5 s) gets the kill triggers.</summary>
    internal static void OnKill(Enemy enemy)
    {
        if (!SemiFunc.IsMasterClientOrSingleplayer() || enemy == null || !LastHit.TryGetValue(enemy, out var hit)) return;
        LastHit.Remove(enemy);
        if (hit.Player == null || Time.time - hit.At > KillCredit) return;
        if (hit.Kind == HitKind.Gun) Trigger(hit.Player, Buff.HotBarrel);
        Trigger(hit.Player, Buff.MendingSurge);
    }

    // ------------------------------------------------------------------ local: Adrenaline, Iron Skin

    [HarmonyPatch(typeof(PlayerHealth), nameof(PlayerHealth.Hurt))]
    private static class HurtTriggers
    {
        // Iron Skin cuts the hit itself once it is running (before Armor, with the other damage-taken changes).
        [HarmonyPrefix]
        [HarmonyPriority(Priority.Low + 1)]
        private static void Prefix(PlayerHealth __instance, ref int damage, bool hurtByHeal)
        {
            if (Monsters.MonsterAfflictions.DotDamage > 0) return;
            var me = __instance != null ? __instance.playerAvatar : null;
            if (damage > 0 && !hurtByHeal && me != null && me.isLocal && Active(me, Buff.IronSkin))
                damage = Mathf.Max(1, Mathf.RoundToInt(damage * IronSkinFactor));
        }

        [HarmonyPostfix]
        private static void Postfix(PlayerHealth __instance, int damage, int enemyIndex, bool hurtByHeal)
        {
            var me = __instance != null ? __instance.playerAvatar : null;
            if (me == null || !me.isLocal || hurtByHeal || damage <= 0 || __instance.health <= 0) return;
            if (enemyIndex >= 0) Trigger(me, Buff.Adrenaline);
            if (__instance.health < __instance.maxHealth * IronSkinThreshold) Trigger(me, Buff.IronSkin);
        }
    }

    /// <summary>Local: Adrenaline's speed factor for the movement tick.</summary>
    internal static float LocalSpeed => Active(SemiFunc.PlayerAvatarLocal(), Buff.Adrenaline) ? SpeedFactor : 1f;

    // ------------------------------------------------------------------ Mending Surge (host)

    private static readonly List<(PlayerAvatar Player, int PerTick, int Ticks, float Next)> Surges = new();

    private static void StartSurge(PlayerAvatar player, int level)
    {
        if (!SemiFunc.IsMasterClientOrSingleplayer() || player.playerHealth == null) return;
        int total = Mathf.Max(1, Mathf.RoundToInt(player.playerHealth.maxHealth * PerkRules.SurgeShare(level)));
        int ticks = (int)SurgeSeconds;
        Surges.Add((player, Mathf.Max(1, Mathf.CeilToInt(total / (float)ticks)), ticks, Time.time + 1f));
    }

    /// <summary>Runs the host's heal-over-time ticks and draws this client's buff lines.</summary>
    internal sealed class Controller : MonoBehaviour
    {
        private void Update()
        {
            for (int i = Surges.Count - 1; i >= 0; i--)
            {
                var s = Surges[i];
                if (Time.time < s.Next) continue;
                var health = s.Player != null ? s.Player.playerHealth : null;
                if (health != null && !s.Player.isDisabled && health.health > 0 && health.health < health.maxHealth)
                    health.HealOther(Mathf.Min(s.PerTick, health.maxHealth - health.health), effect: true);
                if (s.Ticks <= 1) Surges.RemoveAt(i);
                else Surges[i] = (s.Player, s.PerTick, s.Ticks - 1, s.Next + 1f);
            }

            bool russian = Localization.GameLanguage.Russian;
            foreach (Buff buff in System.Enum.GetValues(typeof(Buff)))
            {
                string key = "ihm.buff." + (int)buff;
                if (Shown.TryGetValue(buff, out float until) && until > Time.time)
                    Hud.HudNotify.Status(key, $"{Label(buff, russian)}  {(until - Time.time).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)}{(russian ? " с" : "s")}", Hud.HudStyle.Good);
                else if (Shown.Remove(buff)) Hud.HudNotify.Clear(key);
            }
        }
    }
}
