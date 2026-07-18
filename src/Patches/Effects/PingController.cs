using System.Collections.Generic;
using IncreaseHardMod.Networking;
using IncreaseHardMod.Upgrades;
using Photon.Pun;
using UnityEngine;

namespace IncreaseHardMod.Patches.Effects;

internal enum PingType : byte
{
    Location = 0,
    Valuable = 1,
    Enemy = 2,
    Item = 3,
    Cart = 4,
    CosmeticBox = 5,
    Scooter = 6,
}

/// <summary>
/// Team ping system (overhaul). Pressing the ping key marks whatever you're aiming at; the
/// ping becomes a world-anchored marker that lives for <see cref="ModConfig.PingDurationSeconds"/>
/// (default 4s), is broadcast to everyone (event code 181), and is also mirrored onto the map
/// (Map Suite L6). <see cref="PingHud"/> draws the markers; this class owns classification,
/// the active-ping list, and its lifecycle.
///
/// Object pings (valuable/enemy/item/cart/box/scooter) dim when occluded by geometry; only
/// Location pings are always visible through walls — and Location pings need the "Ping Anywhere"
/// upgrade and are capped at <see cref="ModConfig.MaxLocationPings"/> per player. The "Ping Info"
/// upgrade enriches each ping's label with per-type detail (value, rarity, name).
///
/// All local/per-player: RaiseEvent(All) does not echo to the sender, so the sender adds its
/// own ping locally. Local pings on moving targets keep a Transform so the marker follows.
/// </summary>
internal static class PingController
{
    internal sealed class ActivePing
    {
        public PingType Type;
        public Vector3 Pos;
        public string Label;
        public string Sender;
        public float Expire;      // Time.time when it should disappear
        public bool Local;        // placed by this client (counts toward the local cap)
        public Transform Follow;  // non-null for local pings that should track a moving target
    }

    private static readonly List<ActivePing> _pings = new();

    internal static IReadOnlyList<ActivePing> Active => _pings;

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
        Transform follow = null;
        string label;
        bool info = OwnsPingInfo();

        if (Physics.Raycast(ray, out RaycastHit hit, maxDist, ~0, QueryTriggerInteraction.Ignore))
        {
            var t = hit.collider.transform;
            Classify(t, hit.point, info, out type, out pos, out follow, out label);
        }
        else
        {
            type = PingType.Location;
            pos = cam.transform.position + cam.transform.forward * maxDist;
            label = "Location";
        }

        // Location pings need the "Ping Anywhere" or "Pin" upgrade (both permanent unlocks),
        // and are capped per player.
        if (type == PingType.Location)
        {
            if (OwnsPingAnywhere() || Upgrades.PingUpgrades.OwnsPin())
            {
                EnforceLocationCap();
            }
            else
            {
                Announce("Requires 'Ping Anywhere' or 'Pin' upgrade", Color.gray, "");
                return;
            }
        }

        string sender = LocalName();
        AddLocal(type, pos, label, sender, follow);
        Announce($"{GlyphFor(type)} {label}", ColorFor(type), sender);
        ModNetwork.Instance?.BroadcastPing((byte)type, pos, label, sender);
    }

    // Decide what was hit and build its label (richer when Ping Info is owned).
    private static void Classify(Transform t, Vector3 hitPoint, bool info,
        out PingType type, out Vector3 pos, out Transform follow, out string label)
    {
        follow = null;

        var enemy = t.GetComponentInParent<EnemyParent>();
        if (enemy != null)
        {
            type = PingType.Enemy;
            follow = enemy.Enemy != null ? enemy.Enemy.transform : enemy.transform;
            pos = follow.position;
            label = info && !string.IsNullOrEmpty(enemy.enemyName) ? enemy.enemyName : "Enemy";
            return;
        }

        var cart = t.GetComponentInParent<PhysGrabCart>();
        if (cart != null)
        {
            type = PingType.Cart;
            follow = cart.transform;
            pos = follow.position;
            label = "C.A.R.T.";
            return;
        }

        var vehicle = t.GetComponentInParent<ItemVehicle>();
        if (vehicle != null)
        {
            type = PingType.Scooter;
            follow = vehicle.transform;
            pos = follow.position;
            label = "Scooter";
            return;
        }

        var box = t.GetComponentInParent<CosmeticWorldObject>();
        if (box != null)
        {
            type = PingType.CosmeticBox;
            pos = box.transform.position;
            label = info ? $"Cosmetic ({box.rarity})" : "Cosmetic Box";
            return;
        }

        var valuable = t.GetComponentInParent<ValuableObject>();
        if (valuable != null)
        {
            type = PingType.Valuable;
            pos = valuable.transform.position;
            label = info ? $"${Mathf.RoundToInt(valuable.dollarValueCurrent)} {Clean(valuable.name)}" : "Valuable";
            return;
        }

        var item = t.GetComponentInParent<ItemAttributes>();
        if (item != null)
        {
            type = PingType.Item;
            pos = item.transform.position;
            label = info ? Clean(item.name) : "Item";
            return;
        }

        type = PingType.Location;
        pos = hitPoint;
        label = "Location";
    }

    /// <summary>Called from ModNetwork.OnEvent when a remote player pings.</summary>
    public static void AddNetworkPing(byte type, Vector3 pos, string label, string sender)
    {
        _pings.Add(new ActivePing
        {
            Type = (PingType)type,
            Pos = pos,
            Label = label,
            Sender = sender,
            Expire = Time.time + ModConfig.PingDurationSeconds.Value,
            Local = false,
        });
        Announce($"{GlyphFor((PingType)type)} {label}", ColorFor((PingType)type), sender);
    }

    private static void AddLocal(PingType type, Vector3 pos, string label, string sender, Transform follow)
    {
        _pings.Add(new ActivePing
        {
            Type = type,
            Pos = pos,
            Label = label,
            Sender = sender,
            Expire = Time.time + ModConfig.PingDurationSeconds.Value,
            Local = true,
            Follow = follow,
        });
    }

    // Keep at most MaxLocationPings local Location pings alive: drop the oldest to make room.
    private static void EnforceLocationCap()
    {
        int max = Mathf.Max(1, ModConfig.MaxLocationPings.Value);
        int count = 0;
        ActivePing oldest = null;
        foreach (var p in _pings)
        {
            if (p.Local && p.Type == PingType.Location)
            {
                count++;
                if (oldest == null || p.Expire < oldest.Expire)
                    oldest = p;
            }
        }
        if (count >= max && oldest != null)
            _pings.Remove(oldest);
    }

    /// <summary>Advance ping lifetimes and follow moving targets. Called from PingHud.Update.</summary>
    public static void Tick()
    {
        float now = Time.time;
        for (int i = _pings.Count - 1; i >= 0; i--)
        {
            var p = _pings[i];
            if (now >= p.Expire)
            {
                _pings.RemoveAt(i);
                continue;
            }
            // Follow the tracked transform if it's still alive; otherwise freeze at last pos.
            if (p.Follow != null)
                p.Pos = p.Follow.position;
        }
    }

    /// <summary>Clear all pings (e.g. on level change) so stale markers don't linger.</summary>
    public static void Clear() => _pings.Clear();

    // Brief native focus-text announcement to complement the world marker.
    private static void Announce(string text, Color color, string sender)
    {
        if (!string.IsNullOrEmpty(sender))
            text += $"  <size=70%>({sender})</size>";
        try
        {
            if (MissionUI.instance != null)
                SemiFunc.UIFocusText(text, color, Color.white, 2f);
        }
        catch
        {
            // HUD not ready (e.g. still on a menu); ignore.
        }
    }

    internal static Color ColorFor(PingType type) => type switch
    {
        PingType.Valuable => new Color(0.3f, 0.9f, 0.3f),   // green
        PingType.Item => new Color(0.4f, 0.6f, 1f),         // blue
        PingType.Enemy => new Color(1f, 0.35f, 0.35f),      // red
        PingType.Cart => new Color(0.3f, 0.9f, 0.95f),      // cyan
        PingType.CosmeticBox => new Color(1f, 0.4f, 0.8f),  // pink
        PingType.Scooter => new Color(0.9f, 0.85f, 0.6f),   // beige
        _ => Color.white,                                    // location
    };

    internal static string GlyphFor(PingType type) => type switch
    {
        PingType.Valuable => "$",
        PingType.Enemy => "\u26A0",
        PingType.Item => "\u25C6",
        PingType.Cart => "\u25A1",
        PingType.CosmeticBox => "\u2726",
        PingType.Scooter => "\u25B2",
        _ => "\u2316",
    };

    private static bool OwnsPingAnywhere() => PingUpgrades.OwnsPingAnywhere();
    private static bool OwnsPingInfo() => PingUpgrades.OwnsPingInfo();

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
