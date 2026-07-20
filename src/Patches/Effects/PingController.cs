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
/// Team ping system (overhaul). Pressing the ping key marks whatever you're aiming at. Instead of
/// drawing a custom IMGUI overlay, each ping now spawns the game's OWN highlight — a
/// <see cref="ValuableDiscoverGraphic"/> (the animated corner-bracket + fill square used when a
/// valuable is discovered) — so it matches the in-game style exactly, tracks its target, dims off
/// screen, and self-destroys after <see cref="ModConfig.PingDurationSeconds"/> (default 4s). We tint
/// each graphic per <see cref="PingType"/> via <see cref="ColorFor"/>.
///
/// The native graphic needs a <see cref="PhysGrabObject"/> target with MeshRenderers so it can frame
/// its bounds. Object pings (valuable/enemy/item/cart/box/scooter) highlight the real object; the
/// ping event carries the object's PhotonView ViewID so EVERY client resolves and highlights the SAME
/// object. Location pings have no object, so we build a tiny invisible proxy PhysGrabObject at the
/// point (mirroring the "Ping It" mod). Location pings need the "Ping Anywhere"/"Pin" upgrade and are
/// capped at <see cref="ModConfig.MaxLocationPings"/> per player; a global anti-flood cap of
/// <see cref="ModConfig.MaxActivePings"/> bounds the total.
///
/// The active-ping list is still kept so the map mirror (Map Suite L6, <see cref="Upgrades.MapUpgrade"/>)
/// can read each ping's <see cref="ActivePing.Pos"/>/<see cref="ActivePing.Type"/>/<see cref="ActivePing.Expire"/>.
/// RaiseEvent(All) does not echo to the sender, so the sender spawns its own graphic locally.
/// </summary>
internal static class PingController
{
    internal sealed class ActivePing
    {
        public PingType Type;
        public Vector3 Pos;                  // updated each Tick so the map marker follows moving targets
        public float Expire;                 // Time.time when it should disappear
        public bool Local;                   // placed by this client (counts toward the local cap)
        public ValuableDiscoverGraphic Graphic; // the native highlight (null if it couldn't spawn)
        public GameObject Proxy;             // non-null for Location/fallback pings we anchor ourselves
    }

    private static readonly List<ActivePing> _pings = new();
    private static Mesh _cubeMesh;

    internal static IReadOnlyList<ActivePing> Active => _pings;

    /// <summary>Fire a ping at whatever the local player is looking at.</summary>
    public static void PingFromAim()
    {
        if (!ModConfig.PingEnabled.Value)
            return;

        // The whole ping system is a purchase: without ANY ping upgrade you can't ping at all.
        // (Location pings additionally require Ping Anywhere / Pin, checked below.)
        if (!OwnsAnyPing())
        {
            Announce("Requires a ping upgrade (Ping Info / Ping Anywhere / Pin)", Color.gray, "");
            return;
        }

        var cam = ActiveCamera();
        if (cam == null)
            return;

        float maxDist = ModConfig.PingMaxDistance.Value;
        var ray = new Ray(cam.transform.position, cam.transform.forward);

        PingType type;
        Vector3 pos;
        PhysGrabObject physGrab = null;
        string label;
        bool info = OwnsPingInfo();

        if (Physics.Raycast(ray, out RaycastHit hit, maxDist, ~0, QueryTriggerInteraction.Ignore))
        {
            Classify(hit.collider.transform, hit.point, info, out type, out pos, out physGrab, out label);
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
            if (OwnsPingAnywhere() || PingUpgrades.OwnsPin())
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
        SpawnPing(type, pos, physGrab, local: true);
        Announce($"{GlyphFor(type)} {label}", ColorFor(type), sender);

        int viewID = physGrab != null && physGrab.photonView != null ? physGrab.photonView.ViewID : 0;
        ModNetwork.Instance?.BroadcastPing((byte)type, pos, label, sender, viewID);
    }

    // Decide what was hit, its label (richer when Ping Info is owned), and the PhysGrabObject the
    // native highlight should frame. Location falls back to a null target (proxy built on spawn).
    private static void Classify(Transform t, Vector3 hitPoint, bool info,
        out PingType type, out Vector3 pos, out PhysGrabObject physGrab, out string label)
    {
        physGrab = t.GetComponentInParent<PhysGrabObject>();

        var enemy = t.GetComponentInParent<EnemyParent>();
        if (enemy != null)
        {
            type = PingType.Enemy;
            // Enemies keep their PhysGrabObject on the rigidbody child, not the collider's parent chain.
            if (enemy.Enemy != null && enemy.Enemy.Rigidbody != null && enemy.Enemy.Rigidbody.physGrabObject != null)
                physGrab = enemy.Enemy.Rigidbody.physGrabObject;
            pos = physGrab != null ? physGrab.centerPoint : hitPoint;
            label = info && !string.IsNullOrEmpty(enemy.enemyName) ? enemy.enemyName : "Enemy";
            return;
        }

        var cart = t.GetComponentInParent<PhysGrabCart>();
        if (cart != null)
        {
            type = PingType.Cart;
            if (cart.physGrabObject != null)
                physGrab = cart.physGrabObject;
            pos = physGrab != null ? physGrab.centerPoint : cart.transform.position;
            label = "C.A.R.T.";
            return;
        }

        var vehicle = t.GetComponentInParent<ItemVehicle>();
        if (vehicle != null)
        {
            type = PingType.Scooter;
            pos = physGrab != null ? physGrab.centerPoint : vehicle.transform.position;
            label = "Scooter";
            return;
        }

        var box = t.GetComponentInParent<CosmeticWorldObject>();
        if (box != null)
        {
            type = PingType.CosmeticBox;
            pos = physGrab != null ? physGrab.centerPoint : box.transform.position;
            label = info ? $"Cosmetic ({box.rarity})" : "Cosmetic Box";
            return;
        }

        var valuable = t.GetComponentInParent<ValuableObject>();
        if (valuable != null)
        {
            type = PingType.Valuable;
            if (valuable.physGrabObject != null)
                physGrab = valuable.physGrabObject;
            pos = physGrab != null ? physGrab.centerPoint : valuable.transform.position;
            label = info ? $"${Mathf.RoundToInt(valuable.dollarValueCurrent)} {Clean(valuable.name)}" : "Valuable";
            return;
        }

        var item = t.GetComponentInParent<ItemAttributes>();
        if (item != null)
        {
            type = PingType.Item;
            pos = physGrab != null ? physGrab.centerPoint : item.transform.position;
            label = info ? Clean(item.name) : "Item";
            return;
        }

        // No recognised object: a plain Location ping (highlighted via a proxy at the hit point).
        type = PingType.Location;
        physGrab = null;
        pos = hitPoint;
        label = "Location";
    }

    /// <summary>Called from ModNetwork.OnEvent when a remote player pings.</summary>
    public static void AddNetworkPing(byte type, Vector3 pos, string label, string sender, int viewID)
    {
        PhysGrabObject physGrab = null;
        if (viewID != 0)
        {
            var pv = PhotonView.Find(viewID);
            if (pv != null)
                physGrab = pv.GetComponent<PhysGrabObject>()
                           ?? pv.GetComponentInChildren<PhysGrabObject>()
                           ?? pv.GetComponentInParent<PhysGrabObject>();
        }

        SpawnPing((PingType)type, pos, physGrab, local: false);
        Announce($"{GlyphFor((PingType)type)} {label}", ColorFor((PingType)type), sender);
    }

    // Spawn (and track) the native highlight for a ping. If we have no object to frame (Location, or a
    // remote object that despawned), build a tiny invisible proxy at the position so there's something
    // for the graphic to bound.
    private static void SpawnPing(PingType type, Vector3 pos, PhysGrabObject physGrab, bool local)
    {
        EnforceGlobalCap();

        float duration = ModConfig.PingDurationSeconds.Value;
        GameObject proxy = null;
        if (physGrab == null)
        {
            proxy = BuildProxy(pos, duration);
            physGrab = proxy != null ? proxy.GetComponent<PhysGrabObject>() : null;
        }

        var graphic = physGrab != null ? SpawnGraphic(physGrab, type, duration) : null;

        _pings.Add(new ActivePing
        {
            Type = type,
            Pos = physGrab != null ? physGrab.centerPoint : pos,
            Expire = Time.time + duration,
            Local = local,
            Graphic = graphic,
            Proxy = proxy,
        });
    }

    // Instantiate the game's own discover-highlight prefab, aim it at the target, set its lifetime,
    // and tint it to this ping type's colour.
    private static ValuableDiscoverGraphic SpawnGraphic(PhysGrabObject target, PingType type, float duration)
    {
        var vd = ValuableDiscover.instance;
        if (vd == null || vd.graphicPrefab == null)
            return null;

        var go = Object.Instantiate(vd.graphicPrefab, vd.transform);
        go.SetActive(true);
        var g = go.GetComponent<ValuableDiscoverGraphic>();
        if (g == null)
        {
            Object.Destroy(go);
            return null;
        }

        g.target = target;
        g.waitTime = duration;
        g.waitTimer = duration;
        Tint(g, ColorFor(type));
        return g;
    }

    // Recolour the native highlight's corner brackets (full colour) and centre fill (translucent).
    private static void Tint(ValuableDiscoverGraphic g, Color c)
    {
        TintPart(g.topLeft, c);
        TintPart(g.topRight, c);
        TintPart(g.botLeft, c);
        TintPart(g.botRight, c);
        var mid = c;
        mid.a = 0.18f;
        TintPart(g.middle, mid);
    }

    private static void TintPart(RectTransform rt, Color c)
    {
        if (rt == null)
            return;
        var img = rt.GetComponent<UnityEngine.UI.Image>();
        if (img != null)
            img.color = c;
    }

    // A tiny invisible cube with a disabled PhysGrabObject, so a Location ping (no real object) still
    // has something the native highlight can frame. Mirrors the "Ping It" location anchor recipe:
    // PhotonView + kinematic Rigidbody are added first because PhysGrabObject.Awake expects them.
    private static GameObject BuildProxy(Vector3 pos, float duration)
    {
        var go = new GameObject("IHM_PingAnchor");
        go.transform.position = pos;
        go.transform.localScale = Vector3.one * 0.3f;
        go.AddComponent<MeshFilter>().mesh = CubeMesh();
        go.AddComponent<MeshRenderer>().sharedMaterials = new Material[0]; // invisible, but has bounds
        go.AddComponent<PhotonView>();
        go.AddComponent<PhotonTransformView>().enabled = false;
        var rb = go.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.constraints = RigidbodyConstraints.FreezeAll;
        var pg = go.AddComponent<PhysGrabObject>();
        pg.centerPoint = pos;
        pg.enabled = false;
        Object.Destroy(go, duration + 1f);
        return go;
    }

    private static Mesh CubeMesh()
    {
        if (_cubeMesh != null)
            return _cubeMesh;
        var tmp = GameObject.CreatePrimitive(PrimitiveType.Cube);
        _cubeMesh = tmp.GetComponent<MeshFilter>().sharedMesh;
        Object.Destroy(tmp);
        return _cubeMesh;
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
            Remove(oldest);
    }

    // Global anti-flood cap: keep the total number of on-screen pings (all players, all types)
    // at or below MaxActivePings by dropping the oldest until there's room for one more.
    private static void EnforceGlobalCap()
    {
        int max = Mathf.Max(1, ModConfig.MaxActivePings.Value);
        while (_pings.Count >= max)
        {
            ActivePing oldest = null;
            foreach (var p in _pings)
                if (oldest == null || p.Expire < oldest.Expire)
                    oldest = p;
            if (oldest == null)
                break;
            Remove(oldest);
        }
    }

    /// <summary>Advance ping lifetimes and keep positions current. Called from PingHud.Update.</summary>
    public static void Tick()
    {
        float now = Time.time;
        for (int i = _pings.Count - 1; i >= 0; i--)
        {
            var p = _pings[i];

            // The native graphic self-destroys after its wait time; once it (or its target) is gone,
            // the ping is over.
            bool graphicDead = p.Graphic == null || p.Graphic.target == null;
            if (now >= p.Expire || (graphicDead && p.Proxy == null && p.Graphic == null))
            {
                Remove(p);
                continue;
            }

            // Track the target so the map marker follows moving pings.
            if (p.Graphic != null && p.Graphic.target != null)
                p.Pos = p.Graphic.target.centerPoint;
        }
    }

    /// <summary>Clear all pings (e.g. on level change) so stale markers don't linger.</summary>
    public static void Clear()
    {
        for (int i = _pings.Count - 1; i >= 0; i--)
            Remove(_pings[i]);
    }

    // Destroy a ping's native graphic + proxy and drop it from the list.
    private static void Remove(ActivePing p)
    {
        if (p.Graphic != null)
            Object.Destroy(p.Graphic.gameObject);
        if (p.Proxy != null)
            Object.Destroy(p.Proxy);
        _pings.Remove(p);
    }

    // Brief native focus-text announcement to complement the world highlight.
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

    /// <summary>
    /// The camera the local player is actually looking through, in EVERY state.
    /// While alive this is the player's view; while DEAD/spectating the game re-points the
    /// SINGLE shared GameDirector.MainCamera to the spectate view, so aiming from it lets a
    /// dead player still ping what they're spectating. The old code aimed from PhysGrabber's
    /// cached camera, which is stale/misaimed during spectate, so dead players couldn't ping.
    /// </summary>
    internal static Camera ActiveCamera()
    {
        try
        {
            if (GameDirector.instance != null && GameDirector.instance.MainCamera != null)
                return GameDirector.instance.MainCamera;
        }
        catch { /* GameDirector not ready on menus */ }
        return (PhysGrabber.instance != null ? PhysGrabber.instance.playerCamera : null) ?? Camera.main;
    }

    private static bool OwnsPingAnywhere() => PingUpgrades.OwnsPingAnywhere();
    private static bool OwnsPingInfo() => PingUpgrades.OwnsPingInfo();
    // True when the local player owns at least one ping upgrade (the gate for pinging at all).
    private static bool OwnsAnyPing() =>
        PingUpgrades.OwnsPingInfo() || PingUpgrades.OwnsPingAnywhere() || PingUpgrades.OwnsPin();

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
