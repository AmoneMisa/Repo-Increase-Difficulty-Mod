using System.Collections.Generic;
using IncreaseHardMod.Networking;
using Photon.Pun;
using UnityEngine;

namespace IncreaseHardMod.Patches.Effects;

internal enum PingType : byte
{
    Location = 0,
    Valuable = 1,
    Enemy = 2,
    Item = 3,
}

/// <summary>
/// Shared state + logic for the team ping system. Any player can press the ping key to
/// mark whatever they're aiming at; the marker is broadcast to everyone (event code 181)
/// and shown as a world-anchored label by <see cref="PingHud"/>.
///
/// Placement uses a raycast from the local player's aim camera
/// (<c>PhysGrabber.instance.playerCamera</c>, exposed by the publicizer). The hit is
/// classified into valuable / enemy / item / plain location so the label + colour can
/// differ. Because Photon's RaiseEvent(All) does not echo to the sender, the sender also
/// adds the ping locally.
/// </summary>
internal static class PingController
{
    internal sealed class Ping
    {
        public PingType Type;
        public Vector3 Position;
        public string Label;
        public string Sender;
        public float ExpireTime;
    }

    private static readonly List<Ping> _active = new();
    public static IReadOnlyList<Ping> Active => _active;

    /// <summary>Fire a ping at whatever the local player is looking at.</summary>
    public static void PingFromAim()
    {
        if (!ModConfig.PingEnabled.Value)
            return;

        var cam = (PhysGrabber.instance != null ? PhysGrabber.instance.playerCamera : null) ?? Camera.main;
        if (cam == null)
            return;

        float maxDist = ModConfig.PingMaxDistance.Value;
        var ray = new Ray(cam.transform.position, cam.transform.forward);

        PingType type = PingType.Location;
        Vector3 pos;
        string label = "Ping";

        if (Physics.Raycast(ray, out RaycastHit hit, maxDist, ~0, QueryTriggerInteraction.Ignore))
        {
            var t = hit.collider.transform;

            var valuable = t.GetComponentInParent<ValuableObject>();
            var enemyParent = t.GetComponentInParent<EnemyParent>();
            var item = t.GetComponentInParent<ItemAttributes>();

            if (valuable != null)
            {
                type = PingType.Valuable;
                pos = valuable.transform.position;
                label = $"${Mathf.RoundToInt(valuable.dollarValueCurrent)} {Clean(valuable.name)}";
            }
            else if (enemyParent != null)
            {
                type = PingType.Enemy;
                pos = enemyParent.transform.position;
                label = string.IsNullOrEmpty(enemyParent.enemyName) ? "Enemy" : enemyParent.enemyName;
            }
            else if (item != null)
            {
                type = PingType.Item;
                pos = item.transform.position;
                label = Clean(item.name);
            }
            else
            {
                pos = hit.point;
                label = "Ping";
            }
        }
        else
        {
            pos = cam.transform.position + cam.transform.forward * maxDist;
        }

        string sender = LocalName();
        Plugin.Log.LogInfo($"[Ping] placed {type} '{label}' at {pos} by {sender} (InRoom={PhotonNetwork.InRoom}).");
        Add(type, pos, label, sender);
        ModNetwork.Instance?.BroadcastPing((byte)type, pos, label, sender);
    }

    /// <summary>Called from ModNetwork.OnEvent when a remote player pings.</summary>
    public static void AddNetworkPing(byte type, Vector3 pos, string label, string sender)
    {
        Add((PingType)type, pos, label, sender);
    }

    private static void Add(PingType type, Vector3 pos, string label, string sender)
    {
        _active.Add(new Ping
        {
            Type = type,
            Position = pos,
            Label = label,
            Sender = sender,
            ExpireTime = Time.realtimeSinceStartup + ModConfig.PingDurationSeconds.Value,
        });
    }

    /// <summary>Drop expired pings. Called from PingHud each frame.</summary>
    public static void Prune()
    {
        float now = Time.realtimeSinceStartup;
        for (int i = _active.Count - 1; i >= 0; i--)
        {
            if (_active[i].ExpireTime <= now)
                _active.RemoveAt(i);
        }
    }

    private static string LocalName()
    {
        if (PhotonNetwork.InRoom && PhotonNetwork.LocalPlayer != null &&
            !string.IsNullOrEmpty(PhotonNetwork.LocalPlayer.NickName))
            return PhotonNetwork.LocalPlayer.NickName;
        return "You";
    }

    private static string Clean(string name)
    {
        if (string.IsNullOrEmpty(name))
            return "Object";
        int clone = name.IndexOf("(Clone)", System.StringComparison.Ordinal);
        if (clone >= 0)
            name = name.Substring(0, clone);
        return name.Replace("Valuable ", "").Replace("Item ", "").Trim();
    }
}
