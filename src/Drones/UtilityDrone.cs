using System.Collections.Generic;
using UnityEngine;

namespace IncreaseHardMod.Drones;

/// <summary>
/// The effect half of a utility drone. The sibling vanilla <see cref="ItemDrone"/> does targeting,
/// battery and state sync on every client, so each client can read "is this drone engaged and what is
/// it attached to?" locally and apply the effect without any extra networking:
///
///  * Ghost  - the attached valuable ignores player colliders (local physics, every client) and is
///             withheld from the extraction haul list (host).
///  * Silencer - registers the target as quiet; the Sound/investigate patches in
///             <see cref="DronePatches"/> consult <see cref="NoiseFactorAt"/>.
///  * Link   - handled by <see cref="LinkManager"/>, which pairs engaged A/B drones.
/// </summary>
internal sealed class UtilityDrone : MonoBehaviour
{
    public DroneKind kind;
    public int slot; // Link: 0 = A, 1 = B

    internal ItemDrone Drone;
    private ItemEquippable _equip;

    public static readonly List<UtilityDrone> All = new();

    private void OnEnable() => All.Add(this);

    private void OnDisable()
    {
        All.Remove(this);
        GhostRelease();
    }

    private void Start()
    {
        Drone = GetComponent<ItemDrone>();
        _equip = GetComponent<ItemEquippable>();
    }

    /// <summary>Switched on, not stowed, and currently holding a target.</summary>
    public bool Engaged =>
        Drone != null && _equip != null && !_equip.isEquipped &&
        Drone.itemActivated && Drone.magnetActive &&
        (Drone.magnetTargetPhysGrabObject != null || Drone.playerAvatarTarget != null || Drone.magnetTargetRigidbody != null);

    public PhysGrabObject TargetPgo => Engaged ? Drone.magnetTargetPhysGrabObject : null;
    public PlayerAvatar TargetPlayer => Engaged ? Drone.playerAvatarTarget : null;

    public Rigidbody TargetBody => Engaged ? Drone.magnetTargetRigidbody : null;

    /// <summary>World-space point on the target where the beam is attached.</summary>
    public Vector3 AttachPoint()
    {
        if (Drone.magnetTarget != null)
            return Drone.magnetTarget.TransformPoint(Drone.rayHitPosition);
        var pgo = TargetPgo;
        return pgo != null ? pgo.midPoint : transform.position;
    }

    private void FixedUpdate()
    {
        switch (kind)
        {
            case DroneKind.Ghost: GhostTick(); break;
            case DroneKind.Silencer: SilencerTick(); break;
            case DroneKind.Repair: RepairTick(); break;
        }
    }

    // ----------------------------------------------------------------- Repair

    private const float RepairPerSecond = 500f;
    private const float RepairPerFullBattery = 25000f; // > a 20k power crystal (one crystal recharges one full battery)
    private float _repairSync;
    private ValuableObject _repairing;

    // Host-authoritative: restores lost value (never above the original) and drains the battery in
    // proportion to the money restored. Clients get the new value through RepairEventCode.
    private void RepairTick()
    {
        if (!SemiFunc.IsMasterClientOrSingleplayer())
            return;
        var pgo = TargetPgo;
        var v = pgo != null ? pgo.GetComponent<ValuableObject>() : null;
        var battery = GetComponent<ItemBattery>();
        if (v == null || battery == null || battery.batteryLife <= 0f || v.dollarValueCurrent >= v.dollarValueOriginal)
        {
            FlushRepairSync(force: true);
            _repairing = null;
            return;
        }

        if (_repairing != v)
        {
            FlushRepairSync(force: true);
            _repairing = v;
        }

        float maxByBattery = battery.batteryLife / 100f * RepairPerFullBattery;
        float amount = Mathf.Min(RepairPerSecond * Time.fixedDeltaTime, v.dollarValueOriginal - v.dollarValueCurrent, maxByBattery);
        v.dollarValueCurrent += amount;
        battery.batteryLife = Mathf.Max(0f, battery.batteryLife - amount / RepairPerFullBattery * 100f);

        _repairSync -= Time.fixedDeltaTime;
        if (_repairSync <= 0f)
            FlushRepairSync(force: false);
    }

    private void FlushRepairSync(bool force)
    {
        if (_repairing == null)
            return;
        _repairSync = 0.2f;
        Networking.ModNetwork.Instance?.BroadcastRepair(_repairing, _repairing.dollarValueCurrent);
    }

    // ------------------------------------------------------------------ Ghost

    private readonly List<(Collider valuable, Collider other)> _ignored = new();
    private ValuableObject _ghosted;
    private float _ghostRefresh;

    /// <summary>True while a Ghost drone is engaged on this valuable (extraction ignores it).</summary>
    public static bool IsGhosted(ValuableObject v)
    {
        if (v == null)
            return false;
        foreach (var d in All)
            if (d.kind == DroneKind.Ghost && d._ghosted == v)
                return true;
        return false;
    }

    private void GhostTick()
    {
        var pgo = TargetPgo;
        var valuable = pgo != null ? pgo.GetComponent<ValuableObject>() : null;
        if (valuable == null)
        {
            GhostRelease();
            RestoreFreedColliders();
            return;
        }

        if (_ghosted != valuable)
        {
            GhostRelease();
            _ghosted = valuable;
            if (SemiFunc.IsMasterClientOrSingleplayer())
            {
                // Take it out of the haul if it was already counted.
                RoundDirector.instance.dollarHaulList.Remove(valuable.gameObject);
            }
        }

        _ghostRefresh -= Time.fixedDeltaTime;
        if (_ghostRefresh > 0f)
            return;
        _ghostRefresh = 0.5f; // also picks up players that joined / respawned

        var mine = valuable.GetComponentsInChildren<Collider>();
        foreach (var other in PlayerColliders())
        {
            foreach (var c in mine)
            {
                if (c == null || c.isTrigger)
                    continue;
                if (Physics.GetIgnoreCollision(c, other))
                    continue;
                Physics.IgnoreCollision(c, other, true);
                _ignored.Add((c, other));
            }
        }
    }

    private static IEnumerable<Collider> PlayerColliders()
    {
        if (PlayerController.instance != null)
            foreach (var c in PlayerController.instance.GetComponentsInChildren<Collider>(true))
                if (c != null && !c.isTrigger)
                    yield return c;

        var players = GameDirector.instance != null ? GameDirector.instance.PlayerList : null;
        if (players == null)
            yield break;
        foreach (var p in players)
        {
            if (p == null)
                continue;
            foreach (var c in p.GetComponentsInChildren<Collider>(true))
                if (c != null && !c.isTrigger)
                    yield return c;
            if (p.tumble != null && p.tumble.colliders != null)
                foreach (var c in p.tumble.colliders)
                    if (c != null && !c.isTrigger)
                        yield return c;
        }
    }

    // Drone switched off / target lost: normal extraction resumes, and collisions come back per pair
    // once nobody is inside the valuable any more (checked in RestoreFreedColliders).
    private void GhostRelease()
    {
        if (_ghosted == null)
            return;
        if (SemiFunc.IsMasterClientOrSingleplayer())
            _ghosted.inStartRoom = false; // let ValuableObject.Update re-evaluate the extraction room
        _ghosted = null;
    }

    private void RestoreFreedColliders()
    {
        for (int i = _ignored.Count - 1; i >= 0; i--)
        {
            var (a, b) = _ignored[i];
            if (a == null || b == null)
            {
                _ignored.RemoveAt(i);
                continue;
            }
            // Someone still overlapping stays pass-through until they step out; nobody gets pushed.
            if (a.bounds.Intersects(b.bounds))
                continue;
            Physics.IgnoreCollision(a, b, false);
            _ignored.RemoveAt(i);
        }
    }

    // --------------------------------------------------------------- Silencer

    private struct QuietZone
    {
        public Bounds Bounds;
        public PlayerAvatar Player;
    }

    private static readonly List<QuietZone> Zones = new();
    private static int _zonesFrame = -1;

    private Bounds _quietBounds;
    private bool _quiet;
    private PlayerAvatar _quietPlayer;

    private void SilencerTick()
    {
        if (!Engaged)
        {
            _quiet = false;
            return;
        }

        _quiet = true;
        _quietPlayer = TargetPlayer;
        Transform root = Drone.magnetTarget != null ? Drone.magnetTarget : null;
        var body = Drone.magnetTargetRigidbody;
        var colliders = body != null ? body.GetComponentsInChildren<Collider>() :
            root != null ? root.GetComponentsInChildren<Collider>() : null;

        Bounds b;
        if (_quietPlayer != null)
            b = new Bounds(_quietPlayer.transform.position + Vector3.up, new Vector3(1.2f, 2.2f, 1.2f));
        else if (colliders != null && colliders.Length > 0)
        {
            b = colliders[0].bounds;
            for (int i = 1; i < colliders.Length; i++)
                b.Encapsulate(colliders[i].bounds);
        }
        else
            b = new Bounds(AttachPoint(), Vector3.one);

        b.Expand(0.8f); // sounds are emitted at contact points on or right next to the object
        _quietBounds = b;
    }

    private static void RefreshZones()
    {
        if (_zonesFrame == Time.frameCount)
            return;
        _zonesFrame = Time.frameCount;
        Zones.Clear();
        foreach (var d in All)
            if (d.kind == DroneKind.Silencer && d._quiet)
                Zones.Add(new QuietZone { Bounds = d._quietBounds, Player = d._quietPlayer });
    }

    /// <summary>1 = normal; the configured multiplier if the position is on/next to a silenced target.</summary>
    public static float NoiseFactorAt(Vector3 position)
    {
        RefreshZones();
        foreach (var z in Zones)
            if (z.Bounds.Contains(position))
                return ModConfig.SilencerNoiseMultiplier.Value;
        return 1f;
    }

    public static bool IsPlayerSilenced(PlayerAvatar p)
    {
        if (p == null)
            return false;
        RefreshZones();
        foreach (var z in Zones)
            if (z.Player == p)
                return true;
        return false;
    }
}
