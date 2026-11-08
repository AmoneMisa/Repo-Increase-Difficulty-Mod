using UnityEngine;

namespace IncreaseHardMod.Valuables;

/// <summary>
/// The plush corgi comes alive while someone holds it: ears flap, the tongue wags and the paws paddle.
/// The ears also flop with the toy's own motion (a damped spring on its acceleration), so carrying,
/// shaking or dropping it moves them even after it is let go. Every client animates locally: whether a
/// player holds it comes from each PhysGrabber's grab link, which the game already sends to everyone.
/// </summary>
internal sealed class CorgiPlushMotion : MonoBehaviour
{
    private const float Rise = 3f, Fall = 1.4f;              // excitement ramp per second
    private const float FlopStiffness = 60f, FlopDamping = 7f, FlopGain = 2.2f, FlopLimit = 28f;

    private PhysGrabObject _grab;
    private Transform _frame, _earL, _earR, _tongue, _pawL, _pawR, _footL, _footR;
    private float _excite, _nextCheck;
    private bool _held;
    private Vector3 _lastPos, _lastVel, _accel;
    private Vector2 _flop, _flopVel;                        // x: sideways (deg), y: backward (deg)
    private bool _primed;

    private void Start()
    {
        _grab = GetComponent<PhysGrabObject>();
        var art = GetComponent<ValuableArtInstance>();
        if (art == null || !art.EnsureBindings()) { enabled = false; return; }
        _frame = art.visualRoot;
        Transform P(string group) => art.groupPivots.TryGetValue(group, out var t) ? t : null;
        _earL = P("corgi_ear_l"); _earR = P("corgi_ear_r"); _tongue = P("corgi_tongue");
        _pawL = P("corgi_paw_fl"); _pawR = P("corgi_paw_fr"); _footL = P("corgi_foot_bl"); _footR = P("corgi_foot_br");
        if (_earL == null && _tongue == null && _pawL == null) enabled = false;
    }

    /// <summary>True when any player's grab beam is linked to this toy (known on every client).</summary>
    private bool AnyoneHolding()
    {
        if (_grab == null) return false;
        if (_grab.playerGrabbing.Count > 0) return true;     // host / singleplayer list
        var director = GameDirector.instance;
        if (director == null || director.PlayerList == null) return false;
        foreach (var player in director.PlayerList)
        {
            var grabber = player != null ? player.physGrabber : null;
            if (grabber != null && grabber.grabbed && grabber.grabbedPhysGrabObject == _grab) return true;
        }
        return false;
    }

    private void LateUpdate()
    {
        if (_frame == null) return;
        float dt = Mathf.Clamp(Time.deltaTime, 0f, .05f);
        if (dt <= 0f) return;
        if (Time.time >= _nextCheck) { _nextCheck = Time.time + .1f; _held = AnyoneHolding(); }
        _excite = Mathf.MoveTowards(_excite, _held ? 1f : 0f, (_held ? Rise : Fall) * dt);

        // Toy acceleration in its own frame, smoothed (render-rate deltas are noisy on clients).
        Vector3 pos = _frame.position;
        Vector3 vel = _primed ? (pos - _lastPos) / dt : Vector3.zero;
        Vector3 raw = _primed ? (vel - _lastVel) / dt : Vector3.zero;
        _lastPos = pos; _lastVel = vel; _primed = true;
        _accel = Vector3.Lerp(_accel, Vector3.ClampMagnitude(raw, 40f), 1f - Mathf.Exp(-dt * 12f));
        Vector3 local = _frame.InverseTransformDirection(_accel);
        // Ears lag behind the motion: accelerating right flops them left, forward flops them back.
        var target = new Vector2(-local.x, local.z) * FlopGain;
        _flopVel += (FlopStiffness * (target - _flop) - FlopDamping * _flopVel) * dt;
        _flop += _flopVel * dt;
        _flop = Vector2.ClampMagnitude(_flop, FlopLimit);
        if (!ValuableDynamics.Finite(_flop.x) || !ValuableDynamics.Finite(_flop.y)) { _flop = Vector2.zero; _flopVel = Vector2.zero; }

        float t = Time.time, e = Mathf.SmoothStep(0f, 1f, _excite);
        const float Tau = Mathf.PI * 2f;
        Ear(_earR, 1f, e, t); Ear(_earL, -1f, e, t);
        if (_tongue != null)
            _tongue.localRotation = Quaternion.AngleAxis(e * 17f * Mathf.Sin(Tau * 3.1f * t), Vector3.forward) *
                                    Quaternion.AngleAxis(-e * (6f + 5f * Mathf.Sin(Tau * 1.55f * t)), Vector3.right);
        // Front paws paddle alternately (negative X lifts a forward-pointing paw); back feet kick and wiggle.
        Paw(_pawR, e, Mathf.Sin(Tau * 2.4f * t));
        Paw(_pawL, e, Mathf.Sin(Tau * 2.4f * t + Mathf.PI));
        Foot(_footR, 1f, e, Mathf.Sin(Tau * 2.4f * t + Mathf.PI * .5f));
        Foot(_footL, -1f, e, Mathf.Sin(Tau * 2.4f * t + Mathf.PI * 1.5f));
    }

    private void Ear(Transform ear, float side, float e, float t)
    {
        if (ear == null) return;
        float flap = e * 10f * Mathf.Sin(Mathf.PI * 2f * 2.1f * t + side * .9f);
        ear.localRotation = Quaternion.AngleAxis(side * flap + _flop.x, Vector3.forward) *
                            Quaternion.AngleAxis(e * 4f * Mathf.Sin(Mathf.PI * 2f * 2.1f * t) + _flop.y, Vector3.right);
    }

    private static void Paw(Transform paw, float e, float wave)
    {
        if (paw == null) return;
        // A small paddle about the shoulder join, so the sewn-on leg never pulls out of the body.
        paw.localRotation = Quaternion.AngleAxis(-e * (4f + 9f * Mathf.Max(0f, wave)), Vector3.right);
    }

    private static void Foot(Transform foot, float side, float e, float wave)
    {
        if (foot == null) return;
        foot.localRotation = Quaternion.AngleAxis(side * e * 7f * wave, Vector3.up) *
                             Quaternion.AngleAxis(-e * 3f * Mathf.Max(0f, wave), Vector3.right);
    }
}
