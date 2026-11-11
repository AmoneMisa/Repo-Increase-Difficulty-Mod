using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;

namespace IncreaseHardMod.Gear;

/// <summary>
/// The Flamethrower C.A.R.T.'s push handle, behaving like a vanilla C.A.R.T. handle (PhysGrabCart.CartSteer):
/// a grab that STARTS on the back of the storage box (the red hand plate) snaps to the handle point, the
/// normal grab pull is switched off and the cart is driven ahead of the player, front (gun side) away from
/// them, at vanilla speeds, sprint boost and light handling mass. A grab anywhere else stays a normal drag.
///
/// The host decides who is on the handle and broadcasts it; every client snaps its own grab point, the host
/// moves the rigidbody. While the flame is lit the cart turns more gently, so the turret does the aiming.
/// </summary>
internal sealed class FlameCartHandle : MonoBehaviour
{
    /// <summary>Grab point the handle snaps to (item space, behind the box's back wall).</summary>
    public Transform handlePoint;
    /// <summary>Item-space zone a grab must start in to count as the handle.</summary>
    public Vector3 zoneCenter, zoneSize;

    private PhysGrabObject _pgo;
    private Rigidbody _rb;
    private PhotonView _view;
    private FlameCart _flame;
    private readonly List<PhysGrabber> _grabbers = new();
    private bool _dirty;

    internal bool Pushed => _grabbers.Count > 0;
    internal bool IsPushing(PhysGrabber grabber) => _grabbers.Contains(grabber);

    private void Start()
    {
        _pgo = GetComponent<PhysGrabObject>();
        _rb = GetComponent<Rigidbody>();
        _view = GetComponent<PhotonView>();
        _flame = GetComponent<FlameCart>();
    }

    private void OnDisable() => _grabbers.Clear();

    private bool InZone(Vector3 world)
    {
        var p = transform.InverseTransformPoint(world) - zoneCenter;
        var e = zoneSize * .5f;
        return Mathf.Abs(p.x) <= e.x && Mathf.Abs(p.y) <= e.y && Mathf.Abs(p.z) <= e.z;
    }

    private void Update()
    {
        if (_pgo == null || handlePoint == null) return;
        if (SemiFunc.IsMasterClientOrSingleplayer()) HostTrack();
        _grabbers.RemoveAll(g => g == null);
        foreach (var grabber in _grabbers)
        {
            grabber.OverrideGrabPoint(handlePoint);
            grabber.OverrideDisableGrabLookAt(.2f);
            if (grabber.isLocal) SemiFunc.PhysGrabberLocalChangeAlpha(.1f); // vanilla: beam fades while pushing
        }
    }

    // Same rule as PhysGrabObjectGrabArea: only a fresh press inside the zone joins; letting go leaves.
    private void HostTrack()
    {
        for (int i = _grabbers.Count - 1; i >= 0; i--)
        {
            var g = _grabbers[i];
            if (g == null || !g.grabbed || g.grabbedPhysGrabObject != _pgo) { _grabbers.RemoveAt(i); _dirty = true; }
        }
        foreach (var g in _pgo.playerGrabbing)
        {
            if (g == null || _grabbers.Contains(g) || g.initialPressTimer <= 0f || g.physGrabPoint == null) continue;
            if (!InZone(g.physGrabPoint.position)) continue;
            _grabbers.Add(g);
            _dirty = true;
        }
        if (!_dirty) return;
        _dirty = false;
        if (!SemiFunc.IsMultiplayer() || _view == null) return;
        var ids = new int[_grabbers.Count];
        for (int i = 0; i < ids.Length; i++)
            ids[i] = _grabbers[i].playerAvatar != null && _grabbers[i].playerAvatar.photonView != null ? _grabbers[i].playerAvatar.photonView.ViewID : -1;
        _view.RPC(nameof(IHM_FlameHandleRPC), RpcTarget.Others, ids);
    }

    [PunRPC]
    private void IHM_FlameHandleRPC(int[] ids, PhotonMessageInfo info = default)
    {
        if (!SemiFunc.MasterOnlyRPC(info) || ids == null || ids.Length > 8) return;
        _grabbers.Clear();
        foreach (int id in ids)
        {
            var avatar = id >= 0 ? SemiFunc.PlayerAvatarGetFromPhotonID(id) : null;
            if (avatar != null && avatar.physGrabber != null) _grabbers.Add(avatar.physGrabber);
        }
    }

    private float _tippedTimer;
    private bool _righting;

    // Host: a cart left lying on its side or roof stands itself back up (hop + rotate to its own yaw).
    private void SelfRight()
    {
        float tilt = Vector3.Angle(transform.up, Vector3.up);
        if (_righting)
        {
            var upright = Quaternion.Euler(0f, _rb.rotation.eulerAngles.y, 0f);
            _rb.MoveRotation(Quaternion.RotateTowards(_rb.rotation, upright, 220f * Time.fixedDeltaTime));
            _rb.angularVelocity = Vector3.zero;
            if (tilt < 4f) { _righting = false; _tippedTimer = 0f; }
            return;
        }
        bool resting = _rb.velocity.sqrMagnitude < .09f && _rb.angularVelocity.sqrMagnitude < .25f;
        _tippedTimer = tilt > 50f && resting && _pgo.playerGrabbing.Count == 0 ? _tippedTimer + Time.fixedDeltaTime : 0f;
        if (_tippedTimer < 1.2f) return;
        _righting = true;
        _rb.velocity = Vector3.up * 2.2f; // clear the floor before turning
    }

    // ------------------------------------------------------------------ air cushion

    // While pushed on the handle the cart rides on an air cushion: it floats RideHeight above the highest floor
    // under (and just ahead of) it, so it rises onto a stair step before the chassis reaches the edge, and glides
    // down again. Anything higher than MaxClimb (tables, walls) is not floor. Released, it settles under gravity.
    private const float RideHeight = .3f, MaxClimb = .65f, LookAhead = .35f;
    private static readonly RaycastHit[] GroundHits = new RaycastHit[16];
    private static readonly Vector3[] Probes =
    {
        new(0f, 0f, 0f), new(.38f, 0f, .62f), new(-.38f, 0f, .62f), new(.38f, 0f, -.62f), new(-.38f, 0f, -.62f), new(0f, 0f, .75f),
    };

    /// <summary>Vertical velocity that keeps the cart floating; the current one when there is no floor below.</summary>
    private float Hover(Vector3 horizontalVelocity, float currentY)
    {
        float bottom = transform.position.y;
        var yaw = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
        var ahead = horizontalVelocity * LookAhead;
        float ground = float.NegativeInfinity;
        foreach (var probe in Probes)
        {
            var p = transform.position + yaw * probe;
            ground = Mathf.Max(ground, FloorAt(p, bottom));
            if (ahead.sqrMagnitude > .0001f) ground = Mathf.Max(ground, FloorAt(p + ahead, bottom));
        }
        if (float.IsNegativeInfinity(ground)) return currentY; // over a drop: fall normally

        // A slow 1.5 cm bob sells the cushion; the spring keeps the ride height.
        float desired = ground + RideHeight + Mathf.Sin(Time.time * 3f) * .015f;
        float wantY = Mathf.Clamp((desired - bottom) * 10f, -4f, 5f);
        return Mathf.MoveTowards(currentY, wantY, 60f * Time.fixedDeltaTime);
    }

    // Highest walkable surface under a point, no higher than MaxClimb above the cart's bottom.
    private float FloorAt(Vector3 point, float bottom)
    {
        var origin = new Vector3(point.x, bottom + MaxClimb + .25f, point.z);
        int count = Physics.RaycastNonAlloc(origin, Vector3.down, GroundHits, MaxClimb + 1.6f,
            SemiFunc.LayerMaskGetVisionObstruct(), QueryTriggerInteraction.Ignore);
        float best = float.NegativeInfinity;
        for (int i = 0; i < count; i++)
        {
            var hit = GroundHits[i];
            var c = hit.collider;
            if (c == null || c.transform.IsChildOf(transform)) continue;
            // Loose things (loot, items, players, monsters) are not floor.
            if (c.attachedRigidbody != null && !c.attachedRigidbody.isKinematic) continue;
            if (c.GetComponentInParent<PlayerAvatar>() != null || c.GetComponentInParent<EnemyParent>() != null) continue;
            if (hit.point.y > bottom + MaxClimb) continue;
            if (hit.point.y > best) best = hit.point.y;
        }
        return best;
    }

    private void FixedUpdate()
    {
        if (_rb == null || _pgo == null || !SemiFunc.IsMasterClientOrSingleplayer()) return;
        if (_grabbers.Count == 0) { SelfRight(); return; }
        _righting = false;
        var g = _grabbers[0];
        if (g == null || g.playerAvatar == null) return;
        var avatar = g.playerAvatar;
        _pgo.OverrideMass(4f);
        g.OverridePhysGrabForcesDisable(.1f);

        // Vanilla follow distance: 2 m, 2.5 m at speed; 3-4 m when sprinting forward.
        float near = 2f, far = 2.5f;
        Vector3 playerVelocity = g.isLocal && PlayerController.instance != null && PlayerController.instance.rb != null
            ? PlayerController.instance.rb.velocity : avatar.rbVelocityRaw;
        bool forward = Vector3.Dot(playerVelocity, transform.forward) > 0f;
        float topSpeed = avatar.isSprinting ? 7f : 5f;
        if (avatar.isSprinting && forward) { near = 3f; far = 4f; }
        // Look direction flattened to yaw: vanilla uses the full camera forward, which is harmless for a low cart,
        // but this one is tall and you look down at its box, and that pitch pulled the target in under your feet.
        var look = g.transform.forward;
        look.y = 0f;
        if (look.sqrMagnitude < .01f) look = avatar.transform.forward;
        look.Normalize();
        float t = Mathf.Clamp01(Vector3.Dot(_rb.velocity, look) / topSpeed);
        float distance = Mathf.Lerp(near, far, t);
        var target = avatar.transform.position + look * distance;
        target.y = transform.position.y; // steer flat: on stairs the player's height must not slow the push
        float pull = Mathf.Clamp01(Vector3.Distance(transform.position, target));
        var wanted = Vector3.ClampMagnitude((target - transform.position).normalized * 5f * pull, 5f);
        float y = _rb.velocity.y;
        var v = Vector3.MoveTowards(_rb.velocity, wanted, pull * 2f);
        var flat = Vector3.ClampMagnitude(new Vector3(v.x, 0f, v.z), 5f);
        _rb.velocity = new Vector3(flat.x, Hover(flat, y), flat.z);

        // Face away from the player (gun side forward). Gentler while firing: the turret aims, not the cart.
        // Measured flat from the player's feet; right next to the player that direction is noise, so hold the heading.
        var away = transform.position - avatar.transform.position;
        away.y = 0f;
        var current = Quaternion.Euler(0f, _rb.rotation.eulerAngles.y, 0f);
        var facing = away.sqrMagnitude > .36f ? Quaternion.LookRotation(away, Vector3.up) : current;
        (facing * Quaternion.Inverse(current)).ToAngleAxis(out float angle, out var axis);
        if (angle > 180f) angle -= 360f;
        float cap = _flame != null && _flame.Firing ? 2.5f : 4f;
        float turn = Mathf.Clamp(Mathf.Clamp(Mathf.Abs(angle) / 180f, .2f, 1f) * 20f, 0f, cap);
        var spin = Vector3.ClampMagnitude(Mathf.Deg2Rad * angle * axis.normalized * turn, cap);
        _rb.angularVelocity = Vector3.ClampMagnitude(Vector3.MoveTowards(_rb.angularVelocity, spin, turn), cap);
    }
}
