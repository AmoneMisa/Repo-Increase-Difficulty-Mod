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

    private void FixedUpdate()
    {
        if (_grabbers.Count == 0 || _rb == null || !SemiFunc.IsMasterClientOrSingleplayer()) return;
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
        float t = Mathf.Clamp01(Vector3.Dot(_rb.velocity, g.transform.forward) / topSpeed);
        float distance = Mathf.Lerp(near, far, t);
        var target = avatar.transform.position - (g.transform.rotation * Vector3.back) * distance;
        float pull = Mathf.Clamp01(Vector3.Distance(transform.position, target));
        var wanted = Vector3.ClampMagnitude((target - transform.position).normalized * 5f * pull, 5f);
        float y = _rb.velocity.y;
        var v = Vector3.MoveTowards(_rb.velocity, wanted, pull * 2f);
        _rb.velocity = Vector3.ClampMagnitude(new Vector3(v.x, y, v.z), 5f);

        // Face away from the player (gun side forward). Gentler while firing: the turret aims, not the cart.
        var facing = Quaternion.Euler(0f, Quaternion.LookRotation(g.transform.position - transform.position, Vector3.up).eulerAngles.y + 180f, 0f);
        var current = Quaternion.Euler(0f, _rb.rotation.eulerAngles.y, 0f);
        (facing * Quaternion.Inverse(current)).ToAngleAxis(out float angle, out var axis);
        if (angle > 180f) angle -= 360f;
        float cap = _flame != null && _flame.Firing ? 1.5f : 4f;
        float turn = Mathf.Clamp(Mathf.Clamp(Mathf.Abs(angle) / 180f, .2f, 1f) * 20f, 0f, cap);
        var spin = Vector3.ClampMagnitude(Mathf.Deg2Rad * angle * axis.normalized * turn, cap);
        _rb.angularVelocity = Vector3.ClampMagnitude(Vector3.MoveTowards(_rb.angularVelocity, spin, turn), cap);
    }
}
