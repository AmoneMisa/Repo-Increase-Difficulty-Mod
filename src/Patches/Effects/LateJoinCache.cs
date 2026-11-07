using System.Collections.Generic;
using ExitGames.Client.Photon;
using HarmonyLib;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

namespace IncreaseHardMod.Patches.Effects;

/// <summary>
/// Keeps Photon's join cache down to the current level, so a late joiner builds only the level the host is on.
///
/// Photon replays every cached event to a player who joins: each object made with PhotonNetwork.Instantiate or
/// InstantiateRoomObject (level modules, valuables, items, player avatars) and every buffered RPC (the level
/// generator's ModuleAmount / GenerateDone and so on). A scene change does not clear that cache; the game only
/// wipes it at the lobby menu, because without this mod nobody joins mid-run. So a guest joining in the truck or a
/// level received every level since the lobby on top of each other, and saw a broken or empty map.
///
/// On each level change the host removes, for every object of the level being left (except objects kept across
/// scenes, such as voice): its instantiation event and its buffered RPCs. Scene objects (the level generator and
/// other fixed objects) keep their view ids in the next level, so their RPCs are removed at once, before the new
/// level sends its own; spawned objects have ids that are never reused, so those are removed a batch per frame.
/// Admission stays closed until the queue is empty.
/// </summary>
internal static class LateJoinCache
{
    private const int PerFrame = 80;
    private const byte InstantiationEvent = 202, RpcEvent = 200;

    private static readonly Queue<(byte Code, int Id)> Pending = new();
    private static readonly HashSet<long> Queued = new();
    private static readonly HashSet<int> DestroyedInstantiations = new(), DestroyedViews = new();
    private static bool _leaving;
    private static int _total;

    private static readonly RaiseEventOptions Remove = new() { CachingOption = EventCaching.RemoveFromRoomCache };

    /// <summary>True while removals are still being sent; the room must not admit anyone yet.</summary>
    internal static bool Busy => Pending.Count > 0;

    private static bool On => ModConfig.LateJoinEnabled.Value && PhotonNetwork.InRoom && PhotonNetwork.IsMasterClient;

    private static bool Persistent(PhotonView view) =>
        view == null || view.gameObject == null || view.gameObject.scene.buildIndex == -1 || view.gameObject.scene.name == "DontDestroyOnLoad";

    private static void Enqueue(byte code, int id)
    {
        if (id <= 0 || !Queued.Add(((long)code << 32) | (uint)id)) return;
        Pending.Enqueue((code, id));
    }

    private static void Send(byte code, int id)
    {
        var filter = new Hashtable { [code == InstantiationEvent ? (byte)7 : (byte)0] = id };
        PhotonNetwork.NetworkingClient.OpRaiseEvent(code, filter, Remove, SendOptions.SendReliable);
    }

    /// <summary>Host, right before the scene changes: queue every cached entry of the level being left.</summary>
    internal static void LeaveLevel(string scene)
    {
        if (!On) { Clear(); return; }
        _leaving = true;
        int sceneViews = 0;
        foreach (var view in PhotonNetwork.PhotonViewCollection)
        {
            if (view == null || Persistent(view)) continue;
            if (view.isRuntimeInstantiated)
            {
                Enqueue(InstantiationEvent, view.InstantiationId);
                Enqueue(RpcEvent, view.ViewID);
            }
            else if (view.GetComponent<RunManagerPUN>() == null)
            {
                // RunManagerPUN is skipped: its buffered UpdateLevelRPC already names the level being loaded
                // (ChangeLevelPatch keeps only the latest one), and joiners need exactly that.
                // Fixed scene object: the next level reuses this id, so its old RPCs must go before new ones arrive.
                Send(RpcEvent, view.ViewID);
                sceneViews++;
            }
        }
        foreach (int id in DestroyedInstantiations) Enqueue(InstantiationEvent, id);
        foreach (int id in DestroyedViews) Enqueue(RpcEvent, id);
        DestroyedInstantiations.Clear();
        DestroyedViews.Clear();
        _total = Pending.Count;
        Plugin.Log.LogInfo($"[LateJoin] Clearing the join cache of the level being left (loading {scene}): " +
                           $"{sceneViews} scene object(s) now, {_total} spawned entr(y/ies) queued.");
        PhotonNetwork.SendAllOutgoingCommands();
    }

    /// <summary>Every frame (host): send the next batch of removals.</summary>
    internal static void Tick()
    {
        if (Pending.Count == 0) return;
        if (!On) { Clear(); return; }
        for (int i = 0; i < PerFrame && Pending.Count > 0; i++)
        {
            var (code, id) = Pending.Dequeue();
            Send(code, id);
        }
        if (Pending.Count == 0)
        {
            Queued.Clear();
            Plugin.Log.LogInfo($"[LateJoin] Join cache cleared ({_total} spawned entr(y/ies)).");
        }
    }

    /// <summary>The new level is built: objects destroyed from now on belong to it.</summary>
    internal static void LevelReady() => _leaving = false;

    private static void Clear()
    {
        Pending.Clear();
        Queued.Clear();
        DestroyedInstantiations.Clear();
        DestroyedViews.Clear();
        _leaving = false;
    }

    // An object destroyed only on the host's side (not through PhotonNetwork.Destroy) leaves its cache entry
    // behind and is gone by the next level change, so remember it now.
    [HarmonyPatch(typeof(PhotonNetwork), nameof(PhotonNetwork.LocalCleanPhotonView))]
    private static class RememberDestroyed
    {
        [HarmonyPrefix]
        private static void Prefix(PhotonView view)
        {
            if (_leaving || view == null || !view.isRuntimeInstantiated || !On || Persistent(view)) return;
            DestroyedInstantiations.Add(view.InstantiationId);
            DestroyedViews.Add(view.ViewID);
        }
    }

    // Every scene change of a run goes through PhotonNetwork.LoadLevel on the host ("Reload", then the level).
    [HarmonyPatch(typeof(PhotonNetwork), nameof(PhotonNetwork.LoadLevel), typeof(string))]
    private static class LeaveOnLoad
    {
        [HarmonyPrefix]
        private static void Prefix(string levelName)
        {
            try { LeaveLevel(levelName); }
            catch (System.Exception e) { Plugin.Log.LogWarning($"[LateJoin] Join cache cleanup failed: {e.Message}"); }
        }
    }
}
