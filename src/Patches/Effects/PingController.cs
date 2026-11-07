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
    Upgrade = 7,
    Player = 8,
    Weapon = 9,
    Crystal = 10, // battery crystal (no physics body: found by RetryCrystalEffect.TryAim)
}

/// <summary>Callout icon for item pings. Derived locally from the pinged object, so it needs no network field.</summary>
internal enum WeaponKind : byte { None, Gun, Melee, Grenade, Upgrade, Drone, Soul }

/// <summary>Team pings with soft silhouette contours and large cyan location markers.
/// Object IDs and world positions are shared with the team and the Map Suite mirror.
/// Location pings still require Ping Anywhere and retain the per-player/global caps.</summary>
internal static class PingController
{
    internal sealed class ActivePing
    {
        public PingType Type;
        public WeaponKind Kind;              // callout icon/colour for item pings (None = plain contour)
        public Vector3 Pos;                  // updated each Tick so the map marker follows moving targets
        public float Expire;                 // Time.time when it should disappear
        public bool Local;                   // placed by this client (counts toward the local cap)
        public string Sender;
        public PingOutline Graphic; // the native highlight (null if it couldn't spawn)
    }

    private static readonly List<ActivePing> _pings = new();

    // When each pinged player was last marked, so the winged circlet shows over the player who was pinged.
    private static readonly Dictionary<PlayerAvatar, float> _pingedPlayers = new();

    internal static IReadOnlyList<ActivePing> Active => _pings;

    /// <summary>True while someone's ping on this player is still within the ping duration (the winged circlet).</summary>
    internal static bool RecentlyPinged(PlayerAvatar avatar)
    {
        if (avatar == null || !_pingedPlayers.TryGetValue(avatar, out float at)) return false;
        return Time.time < at + ModConfig.PingDurationSeconds.Value;
    }

    /// <summary>Fire a ping at whatever the local player is looking at.</summary>
    public static void PingFromAim()
    {
        if (!ModConfig.PingEnabled.Value)
            return;

        // Ping Anywhere is the base unlock. Ping Info only enriches its announcements.
        if (!OwnsPingAnywhere())
        {
            Announce("Requires Ping Anywhere", Color.gray, "");
            return;
        }

        var cam = ActiveCamera();
        if (cam == null)
            return;

        float maxDist = ModConfig.PingMaxDistance.Value;
        var ray = cam.ViewportPointToRay(new Vector3(.5f,.5f,0f));

        PingType type;
        Vector3 pos;
        PhysGrabObject physGrab = null;
        string label;
        bool info = OwnsPingInfo();

        RaycastHit hit;
        bool aimed = TryAim(ray, maxDist, out hit);
        if (aimed)
        {
            ClassifyHit(hit, info, out type, out pos, out physGrab, out label);
            label = AddSkillInfo(label, type, hit.collider, physGrab);
        }
        else
        {
            type = PingType.Location;
            pos = ray.GetPoint(maxDist);
            label = "Location";
        }

        // Battery crystals have no colliders: a crystal in front of whatever the ray hit wins.
        if (RetryCrystalEffect.TryAim(ray, maxDist, out var gem, out float gemDistance) &&
            (!aimed || gemDistance <= hit.distance + .05f) && !Blocked(ray.origin, gem))
        {
            type = PingType.Crystal;
            pos = gem;
            physGrab = null;
            label = RetryCrystalEffect.PingLabel(info);
        }

        // Location pings need the "Ping Anywhere" upgrade,
        // and are capped per player.
        if (type == PingType.Location)
        {
            if (OwnsPingAnywhere())
            {
                EnforceLocationCap();
            }
            else
            {
                Announce("Requires the Ping Anywhere perk", Color.gray, "");
                return;
            }
        }

        string sender = LocalName();
        var player = type == PingType.Player ? PlayerAt(hit.collider) : null;
        var tint = SpawnPing(type, pos, physGrab, local: true, sender, player);
        Announce(label, tint, sender);

        int viewID = player != null && player.photonView != null ? player.photonView.ViewID :
            physGrab != null && physGrab.photonView != null ? physGrab.photonView.ViewID : 0;
        ModNetwork.Instance?.BroadcastPing((byte)type, pos, label, sender, viewID);
    }

    // Walls and props between the camera and a point (triggers ignored).
    private static bool Blocked(Vector3 from, Vector3 to)
    {
        var direction = to - from;
        float distance = direction.magnitude - .1f;
        return distance > 0f && Physics.Raycast(from, direction.normalized, distance,
            SemiFunc.LayerMaskGetVisionObstruct(), QueryTriggerInteraction.Ignore);
    }

    // Decide what was hit, its label (richer when Ping Info is owned), and the PhysGrabObject the
    // native highlight should frame. Location falls back to a null target (proxy built on spawn).
    internal static bool TryAim(Ray ray, float maxDistance, out RaycastHit selected)
    {
        int mask = ((int)SemiFunc.LayerMaskGetVisionObstruct() | LayerMask.GetMask("Player")) & ~LayerMask.GetMask("Ignore Raycast", "UI");
        var hits = Physics.RaycastAll(ray,maxDistance,mask,QueryTriggerInteraction.Collide);
        System.Array.Sort(hits,(a,b)=>a.distance.CompareTo(b.distance));
        selected=default;
        bool surface=false;
        foreach(var hit in hits)
        {
            if(hit.collider==null) continue;
            var avatar=PlayerAt(hit.collider);
            if(avatar!=null && avatar.isLocal) continue;
            ClassifyHit(hit,false,out var type,out _,out _,out _);
            if(type!=PingType.Location) {selected=hit;return true;}
            if(!hit.collider.isTrigger) {selected=hit;surface=true;break;} // A solid wall/shelf blocks targets behind it.
        }
        // Small targets may have a narrower collider than their visible wrapper. Allow a tiny
        // aim tolerance only while the crosshair is inside their projected visible bounds.
        float limit=surface?Mathf.Min(maxDistance,selected.distance+.02f):maxDistance;
        var nearby=Physics.SphereCastAll(ray,.035f,limit,mask,QueryTriggerInteraction.Collide);
        System.Array.Sort(nearby,(a,b)=>a.distance.CompareTo(b.distance));
        var camera=ActiveCamera();
        foreach(var hit in nearby)
        {
            var avatar=PlayerAt(hit.collider);
            if(avatar!=null && avatar.isLocal)continue;
            ClassifyHit(hit,false,out var type,out _,out var physical,out _);
            if(type==PingType.Location || physical==null || camera==null)continue;
            bool visible=false;
            foreach(var renderer in physical.GetComponentsInChildren<Renderer>())
                if((renderer is MeshRenderer || renderer is SkinnedMeshRenderer) && renderer.enabled && !renderer.forceRenderingOff && !Visuals.Decoration.Is(renderer) && PingOutline.ProjectBounds(camera,renderer.bounds,out var bounds) &&
                    bounds.Contains(HudProjection.DisplayRect(camera).center)) {visible=true;break;}
            if(!visible)continue;
            var direction=hit.point-ray.origin;
            var blockers=Physics.RaycastAll(ray.origin,direction.normalized,direction.magnitude,mask,QueryTriggerInteraction.Ignore);
            bool blocked=false;
            foreach(var blocker in blockers)
            {
                if(PlayerAt(blocker.collider) is PlayerAvatar self && self.isLocal)continue;
                if(blocker.collider.GetComponentInParent<PhysGrabObject>()!=physical && blocker.distance+.01f<direction.magnitude) {blocked=true;break;}
            }
            if(!blocked){selected=hit;return true;}
        }
        return surface;
    }

    internal static PlayerAvatar PlayerAt(Collider collider)
    {
        if (collider == null) return null;
        return collider.GetComponentInParent<PlayerAvatar>() ?? collider.GetComponentInParent<PlayerTumble>()?.playerAvatar;
    }

    internal static void ClassifyHit(RaycastHit hit, bool info,
        out PingType type, out Vector3 pos, out PhysGrabObject physGrab, out string label)
    {
        var avatar = PlayerAt(hit.collider);
        if (avatar != null && !avatar.isLocal)
        {
            type = PingType.Player;
            pos = avatar.transform.position + Vector3.up * .8f;
            physGrab = null;
            label = !info || string.IsNullOrEmpty(avatar.playerName) ? "Player" : avatar.playerName.Replace("<", "[").Replace(">", "]");
            return;
        }
        var t=hit.collider.transform;
        var body=hit.collider.attachedRigidbody;
        physGrab=t.GetComponentInParent<PhysGrabObject>() ?? (body!=null?body.GetComponent<PhysGrabObject>():null);
        var scope=physGrab!=null?physGrab.transform:body!=null?body.transform:null;
        Classify(t,scope,hit.point,info,out type,out pos,ref physGrab,out label);
    }

    private static T Find<T>(Transform hit,Transform physicalRoot) where T:Component =>
        hit.GetComponentInParent<T>() ?? (physicalRoot!=null?physicalRoot.GetComponentInChildren<T>(true):null);

    private static void Classify(Transform t, Transform scope, Vector3 hitPoint, bool info,
        out PingType type, out Vector3 pos, ref PhysGrabObject physGrab, out string label)
    {
        var enemy = Find<EnemyParent>(t,scope);
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

        var cart = Find<PhysGrabCart>(t,scope);
        if (cart != null)
        {
            type = PingType.Cart;
            if (cart.physGrabObject != null)
                physGrab = cart.physGrabObject;
            pos = physGrab != null ? physGrab.centerPoint : cart.transform.position;
            label = "C.A.R.T.";
            return;
        }

        var vehicle = Find<ItemVehicle>(t,scope);
        if (vehicle != null)
        {
            type = PingType.Scooter;
            pos = physGrab != null ? physGrab.centerPoint : vehicle.transform.position;
            label = "Scooter";
            return;
        }

        var box = Find<CosmeticWorldObject>(t,scope);
        if (box != null)
        {
            type = PingType.CosmeticBox;
            pos = physGrab != null ? physGrab.centerPoint : box.transform.position;
            label = info ? $"Cosmetic ({box.rarity})" : "Cosmetic Box";
            return;
        }

        var valuable = Find<ValuableObject>(t,scope);
        if (valuable != null)
        {
            type = PingType.Valuable;
            if (valuable.physGrabObject != null)
                physGrab = valuable.physGrabObject;
            pos = physGrab != null ? physGrab.centerPoint : valuable.transform.position;
            label = info ? $"${Mathf.RoundToInt(valuable.dollarValueCurrent)} {Clean(valuable.name)}" : "Valuable";
            return;
        }

        var item = Find<ItemAttributes>(t,scope);
        if (item != null)
        {
            bool upgrade=Find<ItemUpgrade>(t,scope)!=null || (item.item!=null &&
                (item.item.itemType==SemiFunc.itemType.player_upgrade || item.item.itemType==SemiFunc.itemType.item_upgrade));
            var weapon = upgrade ? WeaponKind.None : WeaponOf(item);
            type = upgrade ? PingType.Upgrade : weapon != WeaponKind.None ? PingType.Weapon : PingType.Item;
            physGrab ??= item.GetComponentInParent<PhysGrabObject>() ?? item.GetComponentInChildren<PhysGrabObject>(true);
            pos = physGrab != null ? physGrab.centerPoint : item.transform.position;
            label = info ? ItemDisplayName.Get(item) : upgrade ? "Upgrade" : weapon != WeaponKind.None ? "Weapon" : "Item";
            return;
        }

        if(physGrab!=null)
        {
            type=Find<ItemUpgrade>(t,scope)!=null?PingType.Upgrade:PingType.Item;
            pos=physGrab.centerPoint;
            label=info?Clean(physGrab.name):type==PingType.Upgrade?"Upgrade":"Object";
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
        if (!ModConfig.PingEnabled.Value) return;
        PhysGrabObject physGrab = null;
        PlayerAvatar player = null;
        if (viewID != 0)
        {
            var pv = PhotonView.Find(viewID);
            if (pv != null && type == (byte)PingType.Player)
                player = pv.GetComponent<PlayerAvatar>() ?? pv.GetComponentInParent<PlayerAvatar>() ?? pv.GetComponentInChildren<PlayerAvatar>();
            else if (pv != null)
                physGrab = pv.GetComponent<PhysGrabObject>()
                           ?? pv.GetComponentInChildren<PhysGrabObject>()
                           ?? pv.GetComponentInParent<PhysGrabObject>();
        }

        if(type>(byte)PingType.Crystal) return;
        var tint = SpawnPing((PingType)type, pos, physGrab, local: false, sender, player);
        Announce(label, tint, sender);
    }

    // Spawn (and track) the native highlight for a ping. If we have no object to frame (Location, or a
    // remote object that despawned), build a tiny invisible proxy at the position so there's something
    // for the graphic to bound.
    private static Color SpawnPing(PingType type, Vector3 pos, PhysGrabObject physGrab, bool local, string sender, PlayerAvatar player = null)
    {
        var kind = player != null ? WeaponKind.None : CalloutOf(type, physGrab);
        var tint = ColorFor(type, kind);
        // Refresh a repeated mark instead of stacking rings/contours at the same place.
        for(int i=_pings.Count-1;i>=0;i--)
        {
            var old=_pings[i];
            if(old.Sender!=sender || old.Local!=local || old.Type!=type) continue;
            if((physGrab!=null && old.Graphic!=null && old.Graphic.target==physGrab) ||
                (player!=null && old.Graphic!=null && old.Graphic.PlayerTarget==player) ||
                ((type==PingType.Location || type==PingType.Crystal) && Vector3.SqrMagnitude(old.Pos-pos)<.09f)) Remove(old);
        }
        EnforceGlobalCap();

        float duration = ModConfig.PingDurationSeconds.Value;
        if (type == PingType.Player && player != null)
        {
            if (_pingedPlayers.Count > 16)
                foreach (var gone in new List<PlayerAvatar>(_pingedPlayers.Keys)) if (gone == null) _pingedPlayers.Remove(gone);
            _pingedPlayers[player] = Time.time;
        }
        var graphic = player != null ? PingOutline.CreatePlayer(player, pos, tint, duration) :
            PingOutline.Create(physGrab, pos, tint, duration);
        if (type == PingType.Location) graphic.AttachBeacon(duration);
        else if (type == PingType.Crystal)
        {
            // Its own mark: green floor runes and pillar on the crystal, which flares while the ping lasts.
            graphic.AttachBeacon(duration, tint);
            RetryCrystalEffect.Flare(pos, duration);
        }
        else if (type == PingType.Enemy) graphic.UseReticle();
        // No pinged object gets a silhouette outline: weapons, upgrades and drones get their icon callout, and
        // every object (loot, items, carts, vehicles, cosmetic boxes too) the locator arrow and underglow.
        if (type != PingType.Location && type != PingType.Enemy && kind != WeaponKind.None && kind != WeaponKind.Soul)
            graphic.UseWeaponCallout(kind, contour: false);
        // Souls get only the underglow and their red-velvet aura (HeroAura soul style), nothing on screen.
        if (kind == WeaponKind.Soul) graphic.HideMarker();
        else if (player == null && physGrab != null && type != PingType.Enemy) graphic.HideContour();
        // Neon underglow in the ping colour for physical objects (loot, items, carts...), not monsters/players.
        if (physGrab != null && player == null && type != PingType.Enemy && type != PingType.Location)
            PingUnderglow.Attach(physGrab, tint, duration, graphic);

        _pings.Add(new ActivePing
        {
            Type = type,
            Kind = kind,
            Pos = physGrab != null ? physGrab.centerPoint : pos,
            Expire = Time.time + duration,
            Local = local,
            Sender = sender,
            Graphic = graphic,
        });
        PingAudio.Play(type);
        return tint;
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
            bool graphicDead = p.Graphic == null || p.Graphic.TargetGone;
            if (now >= p.Expire || graphicDead)
            {
                Remove(p);
                continue;
            }

            // Track the target so the map marker follows moving pings.
            if (p.Graphic != null && p.Graphic.target != null)
                p.Pos = p.Graphic.target.centerPoint;
            else if (p.Graphic != null && p.Graphic.PlayerTarget != null)
                p.Pos = p.Graphic.PlayerTarget.transform.position + Vector3.up * .8f;
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
            p.Graphic.Close();
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

    /// <summary>Which weapon icon a pinged item gets (None = not a weapon).</summary>
    internal static WeaponKind WeaponOf(ItemAttributes item)
    {
        if (item == null) return WeaponKind.None;
        if (item.item != null)
            switch (item.item.itemType)
            {
                case SemiFunc.itemType.gun:
                case SemiFunc.itemType.launcher: return WeaponKind.Gun;
                case SemiFunc.itemType.melee: return WeaponKind.Melee;
                case SemiFunc.itemType.grenade:
                case SemiFunc.itemType.mine: return WeaponKind.Grenade;
            }
        // Modded or re-typed items: fall back to the weapon behaviour they carry.
        if (item.GetComponent<ItemGun>() != null) return WeaponKind.Gun;
        if (item.GetComponent<ItemMelee>() != null) return WeaponKind.Melee;
        if (item.GetComponent<ItemGrenade>() != null || item.GetComponent<ItemMine>() != null) return WeaponKind.Grenade;
        return WeaponKind.None;
    }

    /// <summary>Which callout a ping gets: upgrades and drones get their own icon, weapons theirs.</summary>
    internal static WeaponKind CalloutOf(PingType type, PhysGrabObject physGrab)
    {
        if (type == PingType.Upgrade) return WeaponKind.Upgrade;
        if (type == PingType.Valuable)
            return physGrab != null && (physGrab.GetComponent<EnemyValuable>() ?? physGrab.GetComponentInParent<EnemyValuable>()) != null
                ? WeaponKind.Soul : WeaponKind.None;
        if (type != PingType.Weapon && type != PingType.Item) return WeaponKind.None;
        var kind = WeaponOf(physGrab);
        if (kind != WeaponKind.None) return kind;
        if (physGrab == null) return WeaponKind.None;
        var item = physGrab.GetComponent<ItemAttributes>() ?? physGrab.GetComponentInParent<ItemAttributes>() ?? physGrab.GetComponentInChildren<ItemAttributes>(true);
        bool drone = (item != null && item.item != null && item.item.itemType == SemiFunc.itemType.drone) ||
                     physGrab.GetComponentInChildren<ItemDrone>(true) != null;
        return drone ? WeaponKind.Drone : WeaponKind.None;
    }

    /// <summary>Ping colour, with a distinct colour per callout category.</summary>
    internal static Color ColorFor(PingType type, WeaponKind kind) => kind switch
    {
        WeaponKind.Gun => ColorFor(PingType.Weapon),                // neon magenta
        WeaponKind.Melee => new Color(1f, .58f, .14f),             // orange
        WeaponKind.Grenade => new Color(.72f, 1f, .2f),            // acid lime
        WeaponKind.Upgrade => new Color(1f, .84f, .25f),           // gold
        WeaponKind.Drone => new Color(.6f, .45f, 1f),              // violet
        WeaponKind.Soul => new Color(.8f, .07f, .22f),             // red velvet
        _ => ColorFor(type),
    };

    internal static WeaponKind WeaponOf(PhysGrabObject physGrab) =>
        physGrab == null ? WeaponKind.None :
        WeaponOf(physGrab.GetComponent<ItemAttributes>() ?? physGrab.GetComponentInParent<ItemAttributes>() ?? physGrab.GetComponentInChildren<ItemAttributes>(true));

    internal static Color ColorFor(PingType type) => type switch
    {
        PingType.Valuable => new Color(0.3f, 0.9f, 0.3f),   // green
        PingType.Item => new Color(0.61f, 0.96f, 0.61f),    // green, sampled from the reference ping image
        PingType.Upgrade => new Color(0.64f, 0.75f, 0.94f), // blue, sampled from the reference ping image
        PingType.Player => new Color(.4f,.85f,1f),
        PingType.Enemy => new Color(1f, 0.35f, 0.35f),      // red
        PingType.Cart => new Color(0.3f, 0.9f, 0.95f),      // cyan
        PingType.CosmeticBox => new Color(1f, 0.4f, 0.8f),  // pink
        PingType.Scooter => new Color(0.9f, 0.85f, 0.6f),   // beige
        PingType.Weapon => new Color(1f, 0.28f, 0.86f),     // neon magenta
        PingType.Crystal => new Color(0.55f, 1f, 0.45f),    // crystal green (matches its pickup text)
        _ => new Color(.45f,.82f,1f),                         // soft cyan location
    };

    internal static string GlyphFor(PingType type) => type switch
    {
        PingType.Valuable => "$",
        PingType.Enemy => "\u26A0",
        PingType.Item => "\u25C6",
        PingType.Upgrade => "\u25C6",
        PingType.Cart => "\u25A1",
        PingType.CosmeticBox => "\u2726",
        PingType.Scooter => "\u25B2",
        PingType.Crystal => "\u25C6",
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

    // Skill tree Ping add-ons, decided by the player who pings: the extra facts ride along in the label,
    // so every teammate sees them on that ping.
    private static string AddSkillInfo(string label, PingType type, Collider collider, PhysGrabObject physGrab)
    {
        var parts = new List<string>(3);
        if (type == PingType.Enemy)
        {
            var enemy = collider != null ? collider.GetComponentInParent<EnemyParent>() : null;
            if (enemy == null && physGrab != null) enemy = physGrab.GetComponentInParent<EnemyParent>();
            var health = enemy != null && enemy.Enemy != null && enemy.Enemy.HasHealth ? enemy.Enemy.Health : null;
            if (health != null && PingUpgrades.OwnsEnemyHp())
                parts.Add($"HP {Mathf.Max(0, health.healthCurrent)}/{Mathf.Max(1, health.health)}");
            if (enemy != null && PingUpgrades.OwnsThreat())
                parts.Add($"Tier {(int)enemy.difficulty + 1}");
        }
        else if (type == PingType.Valuable && PingUpgrades.OwnsLootCondition())
        {
            var valuable = physGrab != null ? physGrab.GetComponent<ValuableObject>() : collider != null ? collider.GetComponentInParent<ValuableObject>() : null;
            if (valuable != null && valuable.dollarValueOriginal > 0f)
                parts.Add($"{Mathf.Clamp(Mathf.RoundToInt(100f * valuable.dollarValueCurrent / valuable.dollarValueOriginal), 0, 100)}% value");
        }
        if (type != PingType.Enemy && type != PingType.Player && type != PingType.Location && physGrab != null && physGrab.rb != null && PingUpgrades.OwnsWeight())
            parts.Add($"{physGrab.rb.mass:0.#} kg");
        return parts.Count == 0 ? label : label + " | " + string.Join(" | ", parts);
    }
    // True when the local player owns at least one ping upgrade (the gate for pinging at all).


    private static string LocalName()
    {
        if (PhotonNetwork.InRoom && PhotonNetwork.LocalPlayer != null &&
            !string.IsNullOrEmpty(PhotonNetwork.LocalPlayer.NickName))
            return PhotonNetwork.LocalPlayer.NickName;
        return "You";
    }

    internal static string Clean(string name)
    {
        if (string.IsNullOrEmpty(name))
            return "Object";
        int clone = name.IndexOf("(Clone)", System.StringComparison.Ordinal);
        if (clone >= 0)
            name = name.Substring(0, clone);
        return name.Replace("Valuable ", "").Replace("Item ", "").Trim();
    }
}
