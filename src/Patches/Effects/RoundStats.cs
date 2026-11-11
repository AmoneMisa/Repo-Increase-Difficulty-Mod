using System;
using System.Collections.Generic;
using System.Reflection;
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
    private static bool _tracking;
    private static RoundLootLedger _loot = new();
    private static readonly HashSet<int> InitialIds = new();
    private sealed class ExtractionTotals
    {
        internal int Total, Souls;
        internal bool Paid;
    }
    private static readonly Dictionary<ExtractionPoint, ExtractionTotals> Extracted = new();
    private static readonly Dictionary<ItemValuableBox, List<Receipt>> BoxContents = new();
    public static RoundLootSummary ResultsSummary;

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
        if (!_tracking || !SemiFunc.IsMasterClientOrSingleplayer() || ValuableDirector.instance == null)
            return;
        _scanTimer -= dt;
        if (_scanTimer > 0f)
            return;
        _scanTimer = 0.25f;
        RefreshLoot();

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
        InitialIds.Clear();
        Extracted.Clear();
        BoxContents.Clear();
        _loot = new RoundLootLedger();
        _scanTimer = 0f;
        _tracking = false;
    }

    /// <summary>Host: snapshot the level's table, show it locally and send it to everyone else.</summary>
    public static void Publish(RoundLootSummary summary)
    {
        var rows = new List<PlayerRoundStat>();
        foreach (var s in Table.Values)
            rows.Add(s);
        Reset();
        if (rows.Count == 0)
            return;

        // Everyone present gets a row, even with zero activity, so quiet players show up too.
        ApplyResults(rows, summary);
        ModNetwork.Instance?.BroadcastRoundStats(rows, summary);
    }

    public static void ApplyResults(List<PlayerRoundStat> rows, RoundLootSummary summary = null)
    {
        Results = rows;
        ResultsSummary = summary;
        ResultsShownAt = Time.unscaledTime;
    }

    public static void ClearResults() { Results = null; ResultsSummary = null; }

    private static void RegisterValuable(ValuableObject v)
    {
        if (!_tracking || v == null || !v.dollarValueSet) return;
        int id = v.GetInstanceID();
        bool soul = v.GetComponent<EnemyValuable>() != null;
        _loot.Register(id, Mathf.RoundToInt(v.dollarValueOriginal), InitialIds.Contains(id), soul);
    }

    private static void RefreshLoot()
    {
        _loot.BeginObservation();
        var director = ValuableDirector.instance;
        if (director == null) return;
        foreach (var v in director.valuableList)
        {
            if (v == null || !v.dollarValueSet) continue;
            RegisterValuable(v);
            _loot.Observe(v.GetInstanceID(), Mathf.RoundToInt(v.dollarValueCurrent), v.discovered);
        }
        foreach (var box in BoxContents.Keys)
            if (box != null)
                foreach (var receipt in BoxReceipts(box, box.CurrentValue))
                    _loot.Observe(receipt.Id, receipt.Value, true);
    }

    private static IEnumerable<Receipt> BoxReceipts(ItemValuableBox box, float value)
    {
        if (!BoxContents.TryGetValue(box, out var contents)) yield break;
        int total = 0;
        foreach (var receipt in contents) total += receipt.Value;
        float scale = total > 0 ? Mathf.Clamp01(value / total) : 0f;
        foreach (var receipt in contents)
            yield return new Receipt { Id = receipt.Id, Value = Mathf.RoundToInt(receipt.Value * scale),
                OriginalValue = receipt.OriginalValue, Packed = true, Object = receipt.Object, Soul = receipt.Soul, Who = receipt.Who };
    }

    private sealed class Receipt
    {
        internal GameObject Object;
        internal ValuableObject Valuable;
        internal int Id, Value;
        internal float OriginalValue;
        internal bool Soul, Packed;
        internal PlayerRoundStat Who;
    }

    private static Receipt Capture(GameObject go)
    {
        if (go == null || go.GetComponent<PhysGrabObject>() == null) return null;
        var v = go.GetComponent<ValuableObject>();
        if (v == null || !v.dollarValueSet) return null;
        RegisterValuable(v);
        return new Receipt { Object = go, Valuable = v, Id = v.GetInstanceID(),
            Value = Mathf.Max(0, (int)v.dollarValueCurrent), OriginalValue = v.dollarValueOriginal, Soul = v.GetComponent<EnemyValuable>() != null,
            Who = CarrierStat(v) };
    }

    private static int Commit(Receipt receipt)
    {
        if (receipt == null || !_loot.Collect(receipt.Id, receipt.Value)) return 0;
        if (receipt.Who != null) receipt.Who.Delivered += receipt.Value;
        return receipt.Soul ? receipt.Value : 0;
    }

    private static void AddExtraction(ExtractionPoint point, int total, int souls)
    {
        if (total <= 0) return;
        if (!Extracted.TryGetValue(point, out var entry)) Extracted[point] = entry = new ExtractionTotals();
        entry.Total += total;
        entry.Souls += souls;
    }

    // ---------------------------------------------------------------- hooks

    // Start runs after scene Awake methods and before this generator starts spawning valuables.
    // Awake can run before RunManager exists on boot; RunIsLevel dereferences that singleton.
    [HarmonyPatch(typeof(LevelGenerator), "Start")]
    private static class OnGenerationBegin
    {
        [HarmonyPrefix]
        private static void Prefix()
        {
            var run = RunManager.instance;
            if (run == null || run.levelCurrent == null || !SemiFunc.RunIsLevel()) return;
            ClearResults();
            Reset();
            _tracking = SemiFunc.IsMasterClientOrSingleplayer();
        }
    }

    [HarmonyPatch(typeof(ValuableObject), "Start")]
    private static class OnValuableSpawn
    {
        [HarmonyPostfix]
        private static void Postfix(ValuableObject __instance)
        {
            if (_tracking && LevelGenerator.Instance != null && !LevelGenerator.Instance.Generated &&
                __instance.GetComponent<EnemyValuable>() == null)
                InitialIds.Add(__instance.GetInstanceID());
        }
    }

    [HarmonyPatch(typeof(ValuableObject), "DollarValueSetLogic")]
    private static class OnValuablePriced
    {
        [HarmonyPostfix, HarmonyPriority(Priority.Last)]
        private static void Postfix(ValuableObject __instance) => RegisterValuable(__instance);
    }

    [HarmonyPatch(typeof(LevelGenerator), "GenerateDoneApply")]
    private static class OnLevelStart
    {
        [HarmonyPostfix]
        private static void Postfix()
        {
            if (!_tracking) return;
            // Everything the generator placed is starting loot. ValuableObject.Start can run after the level
            // reports Generated (it did in v0.5.1), so the Start hook alone left the start total at $0.
            var director = ValuableDirector.instance;
            if (director != null)
                foreach (var v in director.valuableList)
                    if (v != null && v.GetComponent<EnemyValuable>() == null) InitialIds.Add(v.GetInstanceID());
            RefreshLoot();
            _loot.SealInitial();
            var list = GameDirector.instance != null ? GameDirector.instance.PlayerList : null;
            if (list != null) foreach (var p in list) Get(p);
        }
    }

    // Absorption destroys the original valuable. Keep its identity and soul classification
    // until the containing box is extracted; merely packing it is not an extraction/payout.
    [HarmonyPatch(typeof(ItemValuableBox), "StartAbsorbLocal")]
    private static class OnBoxAbsorb
    {
        [HarmonyPrefix]
        private static void Prefix(ItemValuableBox __instance, PhysGrabObject target, out (Receipt receipt, int count) __state)
        {
            __state = default;
            if (!_tracking || !SemiFunc.IsMasterClientOrSingleplayer() || target == null) return;
            __state = (Capture(target.gameObject), __instance.activeAbsorptions.Count);
        }
        [HarmonyPostfix]
        private static void Postfix(ItemValuableBox __instance, (Receipt receipt, int count) __state)
        {
            if (__state.receipt == null || __instance.activeAbsorptions.Count <= __state.count) return;
            if (!BoxContents.TryGetValue(__instance, out var contents))
                BoxContents[__instance] = contents = new List<Receipt>();
            contents.Add(__state.receipt);
        }
    }

    [HarmonyPatch(typeof(ItemValuableBox), nameof(ItemValuableBox.OnExtracted))]
    private static class ClearExtractedBox
    {
        private static void Postfix(ItemValuableBox __instance) => BoxContents.Remove(__instance);
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

    // Cache values before network destruction; credit only objects the extraction consumed.
    [HarmonyPatch(typeof(ExtractionPoint), "DestroyTheFirstPhysObjectsInHaulList")]
    private static class OnExtractOne
    {
        [HarmonyPrefix]
        private static void Prefix(out (Receipt receipt, int total) __state)
        {
            __state = default;
            if (!_tracking || !SemiFunc.IsMasterClientOrSingleplayer()) return;
            var round = RoundDirector.instance;
            __state.total = round.totalHaul;
            if (round.dollarHaulList.Count > 0) __state.receipt = Capture(round.dollarHaulList[0]);
        }
        [HarmonyPostfix]
        private static void Postfix(ExtractionPoint __instance, (Receipt receipt, int total) __state)
        {
            if (!_tracking || !SemiFunc.IsMasterClientOrSingleplayer()) return;
            int delta = RoundDirector.instance.totalHaul - __state.total;
            if (delta <= 0) return;
            int souls = __state.receipt != null && !RoundDirector.instance.dollarHaulList.Contains(__state.receipt.Object)
                ? Commit(__state.receipt) : 0;
            AddExtraction(__instance, delta, souls);
        }
    }

    [HarmonyPatch(typeof(ExtractionPoint), "DestroyAllPhysObjectsInHaulList")]
    private static class OnExtractAll
    {
        [HarmonyPrefix]
        private static void Prefix(out (List<Receipt> receipts, int total) __state)
        {
            __state = default;
            if (!_tracking || !SemiFunc.IsMasterClientOrSingleplayer()) return;
            __state.receipts = new List<Receipt>();
            __state.total = RoundDirector.instance.totalHaul;
            foreach (var go in RoundDirector.instance.dollarHaulList)
            {
                var receipt = Capture(go);
                if (receipt != null) __state.receipts.Add(receipt);
            }
            foreach (var box in RoundDirector.instance.valuableBoxHaulList)
                if (box != null) __state.receipts.AddRange(BoxReceipts(box, box.ExtractionValue));
        }
        [HarmonyPostfix]
        private static void Postfix(ExtractionPoint __instance, (List<Receipt> receipts, int total) __state)
        {
            if (__state.receipts == null) return;
            int delta = RoundDirector.instance.totalHaul - __state.total;
            if (delta <= 0) return;
            int souls = 0;
            foreach (var receipt in __state.receipts)
            {
                if (RoundDirector.instance.dollarHaulList.Contains(receipt.Object)) continue;
                souls += Commit(receipt);
                if (receipt.Packed && receipt.Value > 0)
                    Quests.QuestManager.OnValuableExtracted(receipt.Id, receipt.Value, receipt.OriginalValue, receipt.Soul);
            }
            // The native total also includes valuable boxes; they belong in the payout denominator.
            AddExtraction(__instance, delta, souls);
        }
    }

    [HarmonyPatch]
    private static class OnExtractionPayout
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(ExtractionPoint), "StateComplete");
            yield return AccessTools.Method(typeof(ExtractionPoint), "StateTaxReturn");
        }
        [HarmonyPrefix]
        private static void Prefix(ExtractionPoint __instance, out (ExtractionTotals totals, int currency) __state)
        {
            __state = default;
            if (!_tracking || !SemiFunc.IsMasterClientOrSingleplayer() ||
                !Extracted.TryGetValue(__instance, out var totals) || totals.Paid) return;
            __state = (totals, SemiFunc.StatGetRunCurrency());
        }
        [HarmonyPostfix]
        private static void Postfix((ExtractionTotals totals, int currency) __state)
        {
            if (__state.totals == null) return;
            int increase = SemiFunc.StatGetRunCurrency() - __state.currency;
            if (increase <= 0) return;
            _loot.CreditSoulShare(__state.totals.Souls, __state.totals.Total, increase);
            __state.totals.Paid = true;
        }
    }

    [HarmonyPatch(typeof(Upgrades.TruckUpgrades), "SellRemainingLoot")]
    private static class OnAutoSale
    {
        [HarmonyPrefix]
        private static void Prefix(out (List<Receipt> receipts, int currency) __state)
        {
            __state = default;
            if (!_tracking || !Upgrades.TruckUpgrades.CanSellNow()) return;
            __state.receipts = new List<Receipt>();
            __state.currency = SemiFunc.StatGetRunCurrency();
            foreach (var v in UnityEngine.Object.FindObjectsOfType<ValuableObject>())
                if (v.dollarValueCurrent > 0f)
                {
                    var receipt = Capture(v.gameObject);
                    if (receipt != null) __state.receipts.Add(receipt);
                }
        }
        [HarmonyPostfix]
        private static void Postfix((List<Receipt> receipts, int currency) __state)
        {
            if (__state.receipts == null) return;
            int total = 0, souls = 0;
            foreach (var receipt in __state.receipts)
                if (receipt.Valuable != null && receipt.Valuable.dollarValueCurrent <= 0f)
                {
                    total += receipt.Value;
                    souls += Commit(receipt);
                }
            _loot.CreditSoulShare(souls, total, SemiFunc.StatGetRunCurrency() - __state.currency);
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

    // Snapshot before ChangeLevel starts scene cleanup. Ignore rejected/repeated requests.
    [HarmonyPatch(typeof(RunManager), "ChangeLevel")]
    private static class OnLevelEnd
    {
        [HarmonyPrefix]
        private static void Prefix(RunManager __instance, out RoundLootSummary __state)
        {
            __state = null;
            if (!_tracking || __instance.restarting || !SemiFunc.IsMasterClientOrSingleplayer() || !SemiFunc.RunIsLevel()) return;
            RefreshLoot();
            __state = _loot.Snapshot();
        }
        [HarmonyPostfix]
        private static void Postfix(RoundLootSummary __state)
        {
            if (__state == null || HostRunControls.HoldingChange) return; // held for the failed-level vote
            if (ModConfig.RoundResultsEnabled.Value) Publish(__state);
            else Reset();
        }
    }
}
