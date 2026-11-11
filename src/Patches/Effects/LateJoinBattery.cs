using System.Collections.Generic;
using System.Runtime.CompilerServices;
using ExitGames.Client.Photon;
using HarmonyLib;
using IncreaseHardMod.Networking;
using IncreaseHardMod.Upgrades;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

namespace IncreaseHardMod.Patches.Effects;

/// <summary>Authoritative live charge/capacity catch-up and native battery colour initialization.</summary>
internal static class LateJoinBattery
{
    private sealed class Stamp { internal bool Started; internal double ChargeAt = -1, CapacityAt = -1; }
    private static readonly ConditionalWeakTable<ItemBattery, Stamp> Stamps = new();
    private static readonly Dictionary<int, JoinBatterySnapshot> Pending = new();
    private static readonly Dictionary<int, float> HostRequests = new();
    private static readonly List<int> Done = new();
    private static readonly float[] RetryDelays = { 0f, 1f, 3f, 7f, 15f };
    private static Room _room;
    private static LevelGenerator _generation;
    private static float _began, _next;
    private static int _requests;

    internal static void Begin()
    {
        Pending.Clear(); HostRequests.Clear();
        _room = PhotonNetwork.CurrentRoom;
        _generation = LevelGenerator.Instance;
        _began = Time.realtimeSinceStartup; _next = 0f; _requests = 0;
    }

    internal static void Tick()
    {
        if (!PhotonNetwork.InRoom || PhotonNetwork.CurrentRoom != _room || LevelGenerator.Instance != _generation)
        { Pending.Clear(); _room = null; return; }
        if (PhotonNetwork.IsMasterClient || !ModConfig.LateJoinEnabled.Value || !SemiFunc.RunIsLevel() ||
            Time.realtimeSinceStartup < _next) return;
        _next = Time.realtimeSinceStartup + .25f;
        Done.Clear();
        foreach (var entry in Pending)
            if (PhotonNetwork.Time - entry.Value.Sent > LateJoinBatteryRules.MaxAge || Apply(entry.Value)) Done.Add(entry.Key);
        foreach (int id in Done) Pending.Remove(id);
        // Retry after initial object/stat delivery. Nothing is written back to the host.
        if (_requests < RetryDelays.Length && Time.realtimeSinceStartup - _began >= RetryDelays[_requests])
        {
            _requests++;
            PhotonNetwork.RaiseEvent(LateJoinCatchUp.RequestCode, new object[] { (byte)2 },
                new RaiseEventOptions { Receivers = ReceiverGroup.MasterClient }, SendOptions.SendReliable);
        }
    }

    internal static void OnRequest(int actor)
    {
        if (!PhotonNetwork.IsMasterClient || !ModConfig.LateJoinEnabled.Value || !PhotonNetwork.InRoom ||
            LevelGenerator.Instance == null || !LevelGenerator.Instance.Generated || !SemiFunc.RunIsLevel()) return;
        var target = PhotonNetwork.CurrentRoom.GetPlayer(actor);
        if (target == null || (HostRequests.TryGetValue(actor, out float next) && Time.realtimeSinceStartup < next)) return;
        HostRequests[actor] = Time.realtimeSinceStartup + .9f;
        SendAll(target);
    }

    internal static void SendAll(Player target)
    {
        var batch = new List<ItemBattery>();
        foreach (var battery in Object.FindObjectsByType<ItemBattery>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (!Stamps.TryGetValue(battery, out var stamp) || !stamp.Started) continue;
            var view = battery.GetComponent<PhotonView>();
            if (view == null || view.ViewID <= 0) continue;
            batch.Add(battery);
            if (batch.Count == LateJoinBatteryRules.BatchSize) { Send(batch, target); batch.Clear(); }
        }
        if (batch.Count > 0) Send(batch, target);
    }

    internal static void CapacityChanged(ItemBattery battery)
    {
        if (!PhotonNetwork.InRoom || !PhotonNetwork.IsMasterClient || !ModConfig.LateJoinEnabled.Value ||
            LevelGenerator.Instance == null || !LevelGenerator.Instance.Generated || !SemiFunc.RunIsLevel()) return;
        var view = battery.GetComponent<PhotonView>();
        if (view != null && view.ViewID > 0) Send(new List<ItemBattery> { battery }, null);
    }

    private static void Send(List<ItemBattery> batteries, Player target)
    {
        int count = batteries.Count;
        var ids = new int[count]; var bars = new int[count]; var segments = new int[count];
        var charge = new float[count]; var charging = new bool[count];
        for (int i = 0; i < count; i++)
        {
            var b = batteries[i];
            ids[i] = b.GetComponent<PhotonView>().ViewID;
            bars[i] = b.batteryBars; segments[i] = b.BatteryBarsGet();
            charge[i] = Mathf.Clamp(b.batteryLife, 0f, 100f); charging[i] = b.isCharging;
        }
        var options = target != null ? new RaiseEventOptions { TargetActors = new[] { target.ActorNumber } } :
            new RaiseEventOptions { Receivers = ReceiverGroup.Others };
        PhotonNetwork.RaiseEvent(LateJoinCatchUp.RequestCode,
            new object[] { (byte)1, ModNetwork.FreezeLevelKey(), PhotonNetwork.Time, ids, bars, segments, charge, charging },
            options, SendOptions.SendReliable);
    }

    internal static void Receive(EventData message)
    {
        if (!ModConfig.LateJoinEnabled.Value || !PhotonNetwork.InRoom || PhotonNetwork.IsMasterClient ||
            PhotonNetwork.MasterClient == null || message.Sender != PhotonNetwork.MasterClient.ActorNumber ||
            !LateJoinBatteryRules.Read(message.CustomData, PhotonNetwork.Time, ModNetwork.FreezeLevelKey(), out var rows)) return;
        foreach (var row in rows)
        {
            if (Pending.TryGetValue(row.View, out var older) && older.Sent > row.Sent) continue;
            if (Apply(row)) Pending.Remove(row.View);
            else if (Pending.Count < 2048) Pending[row.View] = row;
        }
    }

    private static bool Apply(JoinBatterySnapshot snapshot)
    {
        var view = PhotonView.Find(snapshot.View);
        var battery = view != null ? view.GetComponent<ItemBattery>() : null;
        if (battery == null) return false;
        var stamp = Stamps.GetOrCreateValue(battery);
        if (!stamp.Started) return false;
        if (snapshot.Sent < stamp.CapacityAt) return true;
        stamp.CapacityAt = snapshot.Sent;
        BatteryLifeUpgrade.AuthoritativeBars(battery, snapshot.Bars);
        EnsureColors(battery);
        // An ordinary native update received after this snapshot wins for charge.
        if (snapshot.Sent >= stamp.ChargeAt)
        {
            // Rebuild the native bars quietly, without replaying loss/charge particles.
            battery.batteryLife = snapshot.Charge;
            battery.isCharging = snapshot.Charging;
            var attributes = battery.GetComponent<ItemAttributes>();
            if (StatsManager.instance != null && attributes != null && !string.IsNullOrEmpty(attributes.instanceName))
                SemiFunc.StatSetBattery(attributes.instanceName, Mathf.FloorToInt(snapshot.Charge));
            stamp.ChargeAt = snapshot.Sent;
        }
        battery.BatteryStateInitialize();
        battery.currentBars = battery.batteryLifeInt;
        Rebuild(battery);
        return true;
    }

    private static bool EnsureColors(ItemBattery battery)
    {
        if (battery == null || (battery.batteryColor.a > 0f && battery.batteryColorMedium.a > 0f)) return false;
        var preset = battery.GetComponent<ItemAttributes>()?.colorPreset;
        if (preset == null) return false;
        battery.batteryColor = preset.GetColorLight();
        battery.batteryColorMedium = preset.GetColorMain();
        return true;
    }

    private static void Rebuild(ItemBattery battery)
    {
        if (battery.batteryVisualLogic != null) battery.batteryVisualLogic.BatteryBarsSet();
        var hud = BatteryUI.instance != null ? BatteryUI.instance.batteryVisualLogic : null;
        if (hud != null && hud.itemBattery == battery) hud.BatteryBarsSet();
    }

    [HarmonyPatch(typeof(ItemBattery), "Start")]
    private static class Started
    {
        [HarmonyPostfix]
        private static void Postfix(ItemBattery __instance)
        {
            Stamps.GetOrCreateValue(__instance).Started = true;
            if (EnsureColors(__instance)) Rebuild(__instance);
        }
    }

    [HarmonyPatch(typeof(BatteryVisualLogic), nameof(BatteryVisualLogic.BatteryBarsSet))]
    private static class VisualColors
    {
        [HarmonyPrefix] private static void Prefix(BatteryVisualLogic __instance) => EnsureColors(__instance.itemBattery);
    }

    [HarmonyPatch(typeof(ItemBattery), "BatteryFullPercentChangeRPC")]
    private static class NativeUpdate
    {
        [HarmonyPrefix]
        private static bool Prefix(ItemBattery __instance, PhotonMessageInfo _info) =>
            !PhotonNetwork.InRoom || PhotonNetwork.IsMasterClient || _info.Sender == null || _info.Sender != PhotonNetwork.MasterClient ||
            !Stamps.TryGetValue(__instance, out var stamp) || _info.SentServerTime >= stamp.ChargeAt;
        [HarmonyPostfix]
        private static void Postfix(ItemBattery __instance, PhotonMessageInfo _info, bool __runOriginal)
        {
            if (!__runOriginal || !PhotonNetwork.InRoom || PhotonNetwork.IsMasterClient || _info.Sender == null || _info.Sender != PhotonNetwork.MasterClient) return;
            Stamps.GetOrCreateValue(__instance).ChargeAt = _info.SentServerTime;
            if (EnsureColors(__instance)) Rebuild(__instance);
        }
    }
}
