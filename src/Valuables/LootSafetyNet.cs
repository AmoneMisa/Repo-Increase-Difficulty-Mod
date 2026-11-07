using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using HarmonyLib;
using Photon.Pun;
using UnityEngine;
using Random = UnityEngine.Random;

namespace IncreaseHardMod.Valuables;

/// <summary>
/// Loot safety net (host only): keeps an open extraction point fillable.
///
/// The game's own top-up (ValuableDirector.SoftLockLogic) sums the value of every valuable on the map and
/// spawns more only when that sum is below the open point's goal. Loot nobody can reach (fallen outside the
/// rooms, held by a Ghost drone) still counts, so a team can be left with nothing to find and no top-up.
/// This replaces that check (<see cref="DisableNativeTopUp"/>) and, every few seconds while a point is open:
///   * counts only reachable loot (plus valuable boxes) and spawns the shortfall;
///   * never spawns while that loot could still fill the point, however long the team takes to find it;
///   * spawns through the game's own ValuableDirector.SpawnValuable, so networking, value scaling and the
///     custom-valuable footprint check all apply, preferring free spots away from every player;
///   * marks the new loot on the map, tells everyone with a HUD notice and logs what it did not count.
/// </summary>
internal sealed class LootSafetyNet : MonoBehaviour
{
    private const float CheckInterval = 5f;
    private const float AfterTopUpCooldown = 20f;  // let room checks settle on fresh loot before judging again
    private const float FreshSeconds = 15f;        // fresh spawns always count as reachable
    private const float StatusLogInterval = 60f;
    private const float NearFallbackDistance = 5f; // last-resort spots still keep out of players' faces
    private const int MaxExcludedListed = 6;

    private float _checkTimer;
    private float _cooldownUntil;
    private float _nextStatusLog;
    private bool _busy;

    private ExtractionPoint _point;

    private readonly Dictionary<int, float> _fresh = new();

    /// <summary>Shown on every client: the host locally, guests through ModNetwork.LootNoticeEventCode.</summary>
    internal static void ShowNotice(int count, int value, bool marked) =>
        Hud.HudNotify.Toast(LootSafetyRules.Notice(count, value, marked), Hud.HudStyle.Info, 6f, "lootnet");

    private void Update()
    {
        if (!ModConfig.LootSafetyNetEnabled.Value || _busy)
            return;
        _checkTimer -= Time.deltaTime;
        if (_checkTimer > 0f)
            return;
        _checkTimer = CheckInterval;

        try { Check(); }
        catch (Exception e) { Plugin.Log.LogWarning("[LootNet] Check failed: " + e); }
    }

    private void Check()
    {
        if (!SemiFunc.IsMasterClientOrSingleplayer() || !SafeRunIsLevel())
        {
            ResetPoint();
            return;
        }
        var dir = ValuableDirector.instance;
        var round = RoundDirector.instance;
        if (dir == null || round == null || LevelGenerator.Instance == null || !LevelGenerator.Instance.Generated ||
            dir.valuableDebug != ValuableDirector.ValuableDebug.Normal || !PointOpen(round))
        {
            ResetPoint();
            return;
        }

        var point = round.extractionPointCurrent;
        float now = Time.time;
        if (point != _point)
        {
            _point = point;
            _nextStatusLog = now; // log the first look at every newly opened point
        }

        var survey = Survey(now);
        int goal = round.extractionHaulGoal;
        var reason = LootSafetyRules.Decide(goal, survey.Reachable, out int target);

        if (ModConfig.LootSafetyLog.Value && (now >= _nextStatusLog || reason != LootTopUpReason.None))
        {
            _nextStatusLog = now + StatusLogInterval;
            Plugin.Log.LogInfo($"[LootNet] Point goal {goal}, in point {round.currentHaul}, reachable loot {survey.Reachable} " +
                $"({survey.Count} valuables, {survey.BoxValue} in boxes); not counted: outside rooms {survey.OutsideValue} " +
                $"({survey.OutsideCount}), drone-held {survey.GhostValue} ({survey.GhostCount}); in truck {survey.TruckValue} " +
                $"({survey.TruckCount})." +
                (survey.Excluded.Length > 0 ? " Not counted: " + survey.Excluded : ""));
        }

        if (reason == LootTopUpReason.None || now < _cooldownUntil)
            return;
        StartCoroutine(TopUp(dir, round, point, target, reason));
    }

    private static bool PointOpen(RoundDirector round) =>
        round.extractionPointActive && round.extractionPointCurrent != null &&
        round.extractionPointCurrent.currentState == ExtractionPoint.State.Active;

    private void ResetPoint()
    {
        _point = null;
        if (_fresh.Count > 0) _fresh.Clear();
    }

    // ---- counting --------------------------------------------------------------------------------

    private sealed class LootSurvey
    {
        public int Reachable, Count, BoxValue;
        public int OutsideValue, OutsideCount, GhostValue, GhostCount, TruckValue, TruckCount;
        public string Excluded = "";
    }

    private LootSurvey Survey(float now)
    {
        var s = new LootSurvey();
        var excluded = new StringBuilder();
        int listed = 0;
        foreach (var v in UnityEngine.Object.FindObjectsByType<ValuableObject>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (v == null)
                continue;
            int value = Mathf.Max(0, Mathf.RoundToInt(v.dollarValueCurrent));
            bool fresh = _fresh.TryGetValue(v.GetInstanceID(), out float spawnedAt) && now - spawnedAt < FreshSeconds;
            var rooms = v.roomVolumeCheck != null ? v.roomVolumeCheck : v.GetComponent<RoomVolumeCheck>();
            bool outside = false;
            if (rooms != null && rooms.CurrentRooms != null && rooms.CurrentRooms.Count == 0)
            {
                rooms.CheckSet(); // the cached result can be stale for resting loot: confirm before excluding it
                outside = rooms.CurrentRooms.Count == 0;
            }
            bool ghosted = Drones.UtilityDrone.IsGhosted(v);
            if (rooms != null && rooms.inTruck)
            {
                s.TruckValue += value;
                s.TruckCount++;
            }

            if (LootSafetyRules.Reachable(outside, ghosted, fresh))
            {
                s.Reachable += value;
                s.Count++;
                continue;
            }
            if (outside) { s.OutsideValue += value; s.OutsideCount++; }
            else { s.GhostValue += value; s.GhostCount++; }
            if (listed++ < MaxExcludedListed)
            {
                var p = v.transform.position;
                excluded.Append(listed > 1 ? "; " : "").Append(Patches.Effects.ItemUnstuckController.Clean(v.name))
                    .Append(' ').Append(value).Append(outside ? " outside rooms at " : " drone-held at ")
                    .Append($"({p.x:0},{p.y:0},{p.z:0})");
            }
        }
        foreach (var box in UnityEngine.Object.FindObjectsByType<ItemValuableBox>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            if (box != null)
                s.BoxValue += Mathf.Max(0, Mathf.RoundToInt(box.CurrentValue));
        s.Reachable += s.BoxValue;
        s.Excluded = excluded.ToString();
        return s;
    }

    // ---- spawning --------------------------------------------------------------------------------

    private IEnumerator TopUp(ValuableDirector dir, RoundDirector round, ExtractionPoint point, int target, LootTopUpReason reason)
    {
        _busy = true;
        var spawned = new List<ValuableObject>();
        int value = 0;
        try
        {
            var volumes = new List<ValuableVolume>();
            foreach (var vol in UnityEngine.Object.FindObjectsByType<ValuableVolume>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                if (vol != null && vol.VolumeType <= ValuableVolume.Type.Big)
                    volumes.Add(vol);
            for (int i = volumes.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (volumes[i], volumes[j]) = (volumes[j], volumes[i]);
            }

            int max = Mathf.Max(1, ModConfig.LootSafetyMaxSpawns.Value);
            float far = Mathf.Max(0f, ModConfig.LootSafetyMinPlayerDistance.Value);
            int remaining = target;
            var used = new HashSet<ValuableVolume>();
            // Pass 1: spots away from everyone, like the game's own top-up. Pass 2: any free spot.
            foreach (float minDistance in new[] { far, Mathf.Min(far, NearFallbackDistance) })
            {
                foreach (var vol in volumes)
                {
                    if (!LootSafetyRules.KeepSpawning(remaining, spawned.Count, max))
                        break;
                    if (dir == null || round == null || round.extractionPointCurrent != point || !PointOpen(round))
                    {
                        remaining = 0; // point closed or level left: stop, but still announce what spawned
                        break;
                    }
                    if (vol == null || used.Contains(vol) || NearPlayer(vol.transform.position, minDistance))
                        continue;
                    if (vol.OverlapBoxCheck(SemiFunc.LayerMaskGetPhysGrabObject()).Length > 0)
                        continue;
                    var pool = Pool(dir, vol.VolumeType);
                    if (pool == null || pool.Count == 0)
                        continue;

                    used.Add(vol);
                    ValuableObject v = null;
                    try { v = dir.SpawnValuable(pool[Random.Range(0, pool.Count)], vol); }
                    catch (Exception e) { Plugin.Log.LogWarning("[LootNet] Spawn failed: " + e.Message); }
                    if (v == null)
                        continue; // e.g. a custom valuable that did not fit; try the next spot

                    _fresh[v.GetInstanceID()] = Time.time;
                    int worth = Mathf.Max(0, Mathf.RoundToInt(v.dollarValueCurrent)); // SpawnValuable sets the value
                    remaining -= Mathf.Max(1, worth);
                    value += worth;
                    spawned.Add(v);
                    yield return new WaitForSeconds(0.1f);
                }
                if (!LootSafetyRules.KeepSpawning(remaining, spawned.Count, max))
                    break;
            }

            if (ModConfig.LootSafetyLog.Value || spawned.Count == 0)
                Plugin.Log.LogInfo($"[LootNet] Top-up ({reason}): needed {target}, spawned {spawned.Count} valuable(s) worth {value}" +
                    (spawned.Count == 0 ? " - no free spawn spot was found." : "."));
        }
        finally
        {
            _busy = false;
            _cooldownUntil = Time.time + AfterTopUpCooldown;
        }

        if (spawned.Count == 0)
            yield break;
        // Let the room-object spawns reach the guests before asking them to mark the loot.
        yield return new WaitForSeconds(0.5f);
        bool marked = ModConfig.LootSafetyRevealOnMap.Value;
        if (marked)
            foreach (var v in spawned)
                Reveal(v);
        ShowNotice(spawned.Count, value, marked);
        Networking.ModNetwork.Instance?.BroadcastLootNotice(Mathf.Min(spawned.Count, 100), value, marked);
    }

    private static List<PrefabRef> Pool(ValuableDirector dir, ValuableVolume.Type type) => type switch
    {
        ValuableVolume.Type.Tiny => dir.tinyValuables,
        ValuableVolume.Type.Small => dir.smallValuables,
        ValuableVolume.Type.Medium => dir.mediumValuables,
        ValuableVolume.Type.Big => dir.bigValuables,
        _ => null,
    };

    private static bool NearPlayer(Vector3 position, float distance)
    {
        if (distance <= 0f)
            return false;
        var players = SemiFunc.PlayerGetList();
        if (players == null)
            return false;
        foreach (var p in players)
            if (p != null && !p.isDisabled && Vector3.Distance(p.transform.position, position) < distance)
                return true;
        return false;
    }

    // Same path as the game's own discovery (DiscoverRPC adds the map marker on every client),
    // without the on-screen discovery flash for the host.
    private static void Reveal(ValuableObject v)
    {
        if (v == null || v.discovered)
            return;
        try
        {
            if (!SemiFunc.IsMultiplayer())
                v.DiscoverRPC();
            else if (v.photonView != null && v.photonView.ViewID != 0)
                v.photonView.RPC("DiscoverRPC", RpcTarget.All);
        }
        catch (Exception e) { Plugin.Log.LogWarning("[LootNet] Map mark failed: " + e.Message); }
    }

    private static bool SafeRunIsLevel()
    {
        try { return SemiFunc.RunIsLevel(); }
        catch { return false; }
    }

    /// <summary>The safety net replaces the game's own soft-lock top-up, so the two never spawn at once.</summary>
    [HarmonyPatch(typeof(ValuableDirector), "SoftLockExtractionCheck")]
    private static class DisableNativeTopUp
    {
        [HarmonyPrefix]
        private static bool Prefix(ref bool __result)
        {
            if (!ModConfig.LootSafetyNetEnabled.Value)
                return true;
            __result = false;
            return false;
        }
    }
}
