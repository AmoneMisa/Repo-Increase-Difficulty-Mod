using System;
using System.Collections.Generic;
using HarmonyLib;
using IncreaseHardMod.Networking;
using UnityEngine;

namespace IncreaseHardMod.Patches.Effects;

/// <summary>One player's tallies for a single level.</summary>
public sealed class PlayerRoundStat
{
    public string Name;
    public int BrokenValue;   // $ of valuable value lost to breakage
    public int BrokenCount;   // number of break events
    public int Delivered;     // $ of loot extracted (credited on a SUCCESSFUL extraction only)
    public int Deaths;
    public int Revives;       // teammates this player revived

    // Same weighting everywhere so the MVP/OOPS pick and the on-screen score always agree.
    public int Score => Delivered + Revives * 2000 - BrokenValue - Deaths * 1000;
    public bool IsEmpty => BrokenValue == 0 && BrokenCount == 0 && Delivered == 0 && Deaths == 0 && Revives == 0;
}

/// <summary>
/// Host-authoritative per-level scoreboard (who broke / hauled / died / revived).
///
/// Only the host records: every hook below is gated on master-client, and the finished table is
/// broadcast once at level end (<see cref="ModNetwork.BroadcastRoundStats"/>), so all clients show
/// the same numbers. Clients without the mod simply ignore the event.
///
/// Attribution uses a sticky "last carrier" per valuable (the game's own lastPlayerGrabbing expires
/// 3s after letting go, which would lose credit for thrown loot).
/// </summary>
internal static class RoundStats
{
    private static readonly Dictionary<string, PlayerRoundStat> Table = new();
    private static readonly Dictionary<int, string> Carrier = new(); // valuable instanceId -> player key
    private static readonly Dictionary<string, string> HeadCarrier = new(); // dead player key -> who last carried their head
    private static float _scanTimer;

    /// <summary>Last results received (host: its own) for the overlay; null until a level finishes.</summary>
    public static List<PlayerRoundStat> Results;
    public static float ResultsShownAt;

    private static string KeyOf(PlayerAvatar p) =>
        p == null ? null : (!string.IsNullOrEmpty(p.steamID) ? p.steamID : p.playerName);

    private static PlayerRoundStat Get(PlayerAvatar p)
    {
        string key = KeyOf(p);
        if (key == null)
            return null;
        if (!Table.TryGetValue(key, out var s))
            Table[key] = s = new PlayerRoundStat();
        s.Name = p.playerName;
        return s;
    }

    private static PlayerAvatar Find(string key)
    {
        var list = GameDirector.instance != null ? GameDirector.instance.PlayerList : null;
        if (list == null)
            return null;
        foreach (var p in list)
            if (p != null && KeyOf(p) == key)
                return p;
        return null;
    }

    private static PlayerRoundStat CarrierStat(ValuableObject v)
    {
        if (v == null)
            return null;
        var pgo = v.GetComponent<PhysGrabObject>();
        if (pgo != null && pgo.lastPlayerGrabbing != null)
            return Get(pgo.lastPlayerGrabbing);
        return Carrier.TryGetValue(v.GetInstanceID(), out var key) && Table.TryGetValue(key, out var s) ? s : null;
    }

    /// <summary>Host tick: remember who last held each valuable.</summary>
    public static void Tick(float dt)
    {
        if (!SemiFunc.IsMasterClientOrSingleplayer() || ValuableDirector.instance == null)
            return;
        _scanTimer -= dt;
        if (_scanTimer > 0f)
            return;
        _scanTimer = 0.25f;

        var players = GameDirector.instance != null ? GameDirector.instance.PlayerList : null;
        if (players != null)
            foreach (var p in players)
            {
                var head = p != null ? p.playerDeathHead : null;
                var hp = head != null ? head.physGrabObject : null;
                string who = hp != null ? KeyOf(hp.lastPlayerGrabbing) : null;
                if (who != null && who != KeyOf(p))
                {
                    HeadCarrier[KeyOf(p)] = who;
                    Get(hp.lastPlayerGrabbing);
                }
            }

        foreach (var v in ValuableDirector.instance.valuableList)
        {
            if (v == null)
                continue;
            var pgo = v.GetComponent<PhysGrabObject>();
            if (pgo == null || pgo.lastPlayerGrabbing == null)
                continue;
            string key = KeyOf(pgo.lastPlayerGrabbing);
            if (key != null)
            {
                Carrier[v.GetInstanceID()] = key;
                Get(pgo.lastPlayerGrabbing); // make sure the player has a row
            }
        }
    }

    public static void Reset()
    {
        Table.Clear();
        Carrier.Clear();
        HeadCarrier.Clear();
    }

    /// <summary>Host: snapshot the level's table, show it locally and send it to everyone else.</summary>
    public static void Publish()
    {
        var rows = new List<PlayerRoundStat>();
        foreach (var s in Table.Values)
            rows.Add(s);
        Reset();
        if (rows.Count == 0)
            return;

        // Everyone present gets a row, even with zero activity, so quiet players show up too.
        ApplyResults(rows);
        ModNetwork.Instance?.BroadcastRoundStats(rows);
    }

    public static void ApplyResults(List<PlayerRoundStat> rows)
    {
        Results = rows;
        ResultsShownAt = Time.unscaledTime;
    }

    public static void ClearResults() => Results = null;

    // ---------------------------------------------------------------- hooks

    [HarmonyPatch(typeof(LevelGenerator), "GenerateDone")]
    private static class OnLevelStart
    {
        [HarmonyPostfix]
        private static void Postfix()
        {
            ClearResults(); // never keep the previous level's board over a new one
            if (!SemiFunc.IsMasterClientOrSingleplayer())
                return;
            Reset();
            var list = GameDirector.instance != null ? GameDirector.instance.PlayerList : null;
            if (list != null)
                foreach (var p in list)
                    Get(p);
        }
    }

    [HarmonyPatch(typeof(PhysGrabObjectImpactDetector), "BreakRPC")]
    private static class OnBreak
    {
        [HarmonyPostfix]
        private static void Postfix(PhysGrabObjectImpactDetector __instance, float valueLost, bool _loseValue)
        {
            if (!_loseValue || !SemiFunc.IsMasterClientOrSingleplayer())
                return;
            var v = __instance.valuableObject;
            if (v == null)
                return;
            var s = CarrierStat(v);
            if (s == null)
                return;
            s.BrokenValue += Mathf.RoundToInt(Mathf.Clamp(valueLost, 0f, v.dollarValueOriginal));
            s.BrokenCount++;
        }
    }

    // Deliveries are credited when the loot is actually consumed by a SUCCESSFUL extraction (the
    // game only destroys haul items then), so a cancelled extraction never awards anything.
    [HarmonyPatch(typeof(ExtractionPoint), "DestroyTheFirstPhysObjectsInHaulList")]
    private static class OnExtractOne
    {
        [HarmonyPrefix]
        private static void Prefix(out (GameObject go, int value, PlayerRoundStat who) __state)
        {
            __state = default;
            if (!SemiFunc.IsMasterClientOrSingleplayer())
                return;
            var list = RoundDirector.instance.dollarHaulList;
            if (list.Count == 0 || list[0] == null)
                return;
            var v = list[0].GetComponent<ValuableObject>();
            if (v == null)
                return;
            __state = (list[0], (int)v.dollarValueCurrent, CarrierStat(v));
        }

        [HarmonyPostfix]
        private static void Postfix((GameObject go, int value, PlayerRoundStat who) __state)
        {
            if (__state.go == null || __state.who == null)
                return;
            // Only credit if the game really consumed it this call.
            if (!RoundDirector.instance.dollarHaulList.Contains(__state.go))
                __state.who.Delivered += __state.value;
        }
    }

    [HarmonyPatch(typeof(ExtractionPoint), "DestroyAllPhysObjectsInHaulList")]
    private static class OnExtractAll
    {
        [HarmonyPrefix]
        private static void Prefix(out List<(int value, PlayerRoundStat who)> __state)
        {
            __state = null;
            if (!SemiFunc.IsMasterClientOrSingleplayer())
                return;
            __state = new List<(int, PlayerRoundStat)>();
            foreach (var go in RoundDirector.instance.dollarHaulList)
            {
                if (go == null || go.GetComponent<PhysGrabObject>() == null)
                    continue;
                var v = go.GetComponent<ValuableObject>();
                if (v != null)
                    __state.Add(((int)v.dollarValueCurrent, CarrierStat(v)));
            }
        }

        [HarmonyPostfix]
        private static void Postfix(List<(int value, PlayerRoundStat who)> __state)
        {
            if (__state == null)
                return;
            foreach (var (value, who) in __state)
                if (who != null)
                    who.Delivered += value;
        }
    }

    [HarmonyPatch(typeof(PlayerAvatar), "PlayerDeathRPC")]
    private static class OnDeath
    {
        [HarmonyPostfix]
        private static void Postfix(PlayerAvatar __instance)
        {
            if (SemiFunc.IsMasterClientOrSingleplayer())
            {
                Get(__instance).Deaths++;
                HeadCarrier.Remove(KeyOf(__instance)); // fresh body, fresh credit
            }
        }
    }

    // Credit goes to whoever brought the head back (held it, or last carried it before it was
    // revived by the truck / extraction). Self-revives and never-carried heads give nobody credit.
    [HarmonyPatch(typeof(PlayerAvatar), "ReviveRPC")]
    private static class OnRevive
    {
        [HarmonyPostfix]
        private static void Postfix(PlayerAvatar __instance)
        {
            if (!SemiFunc.IsMasterClientOrSingleplayer())
                return;
            string key = KeyOf(__instance);
            var head = __instance.playerDeathHead;
            var reviver = head != null && head.physGrabObject != null ? head.physGrabObject.lastPlayerGrabbing : null;
            string who = KeyOf(reviver);
            if (who == null && key != null)
                HeadCarrier.TryGetValue(key, out who);
            if (key != null)
                HeadCarrier.Remove(key);
            if (who == null || who == key)
                return;
            var p = reviver != null && KeyOf(reviver) == who ? reviver : Find(who);
            if (p != null)
                Get(p).Revives++;
        }
    }

    // Level over: freeze and publish the board. Prefix so RunIsLevel() still describes the level we
    // are LEAVING (the host's ChangeLevel is the only one that runs past its own early-return).
    [HarmonyPatch(typeof(RunManager), "ChangeLevel")]
    private static class OnLevelEnd
    {
        [HarmonyPrefix]
        private static void Prefix(out bool __state)
        {
            __state = false;
            try { __state = SemiFunc.IsMasterClientOrSingleplayer() && SemiFunc.RunIsLevel(); }
            catch (Exception) { /* menu / early boot */ }
        }

        [HarmonyPostfix]
        private static void Postfix(bool __state)
        {
            if (__state && ModConfig.RoundResultsEnabled.Value)
                Publish();
        }
    }
}
