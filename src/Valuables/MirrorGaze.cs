using UnityEngine;

namespace IncreaseHardMod.Valuables;

/// <summary>
/// The magic mirror of Fable Realm. Its face sleeps until someone picks the mirror up; then it opens its eyes and
/// turns to watch whoever carries it, looking from one carrier to the next when there are several.
///
/// The look is worked out on every client from the game's own list of grabbing players (the game keeps it in step
/// for everyone, late joiners included), so nothing extra is sent. Only the sounds go through the host
/// (<see cref="ValuableFoley"/>): a chime on waking, a whisper now and then while carried, a falling chime on release.
/// </summary>
internal sealed class MirrorGaze : MonoBehaviour
{
    // How far the face can turn (degrees), how much further the pupils lead, and how quickly it follows.
    private const float MaxYaw = 32f, MaxPitch = 22f, PupilLead = .3f, Follow = 7f;
    private const float SwitchSeconds = 2.6f, BlinkSeconds = .13f, DozeSeconds = .6f;
    private PhysGrabObject _grab;
    private ValuableFoley _foley;
    private Transform _face, _pupils, _lids;
    private Vector3 _centre;
    private Quaternion _look = Quaternion.identity;
    private int _watched, _holders;
    private float _switch, _blink, _blinkEnd, _whisper, _released = float.NegativeInfinity;
    private bool _awake;

    private void Start()
    {
        _grab = GetComponent<PhysGrabObject>();
        _foley = GetComponent<ValuableFoley>();
        var art = GetComponent<ValuableArtInstance>();
        if (_grab == null || art == null || !art.EnsureBindings() ||
            !art.groupPivots.TryGetValue(FableLoot.MirrorFace, out _face) ||
            !art.groupPivots.TryGetValue(FableLoot.MirrorPupils, out _pupils) ||
            !art.groupPivots.TryGetValue(FableLoot.MirrorLids, out _lids))
        {
            enabled = false;
            return;
        }
        _centre = transform.InverseTransformPoint(_face.position);
        Show(false);
    }

    private void FixedUpdate()
    {
        if (!SemiFunc.IsMasterClientOrSingleplayer() || _foley == null || _grab == null) return;
        int holders = _grab.playerGrabbing.Count;
        if (holders > 0 && _holders == 0)
        {
            // Passed from hand to hand it does not wake twice.
            if (Time.time - _released > DozeSeconds) _foley.Emit(ValuableFoley.MirrorWake, .3f);
            _whisper = Time.time + Random.Range(7f, 12f);
        }
        else if (holders > 0 && Time.time > _whisper)
        {
            _foley.Emit(ValuableFoley.MirrorWhisper, .26f);
            _whisper = Time.time + Random.Range(11f, 19f);
        }
        if (holders == 0 && _holders > 0) _released = Time.time;
        // The falling chime waits for the doze, so a quick regrab stays quiet.
        if (holders == 0 && !float.IsNegativeInfinity(_released) && Time.time - _released > DozeSeconds)
        {
            _foley.Emit(ValuableFoley.MirrorSleep, .26f);
            _released = float.NegativeInfinity;
        }
        _holders = holders;
    }

    private void LateUpdate()
    {
        var watched = Watched();
        bool awake = watched != null;
        if (awake != _awake)
        {
            _awake = awake;
            _blink = Time.time + Random.Range(1.5f, 3.5f);
            _blinkEnd = 0f;
        }
        var target = Quaternion.identity; var lead = Quaternion.identity;
        if (awake)
        {
            // Where the carrier's head is, seen from the face (which looks along the mirror's -Z).
            var eyes = watched.PlayerVisionTarget != null && watched.PlayerVisionTarget.VisionTransform != null
                ? watched.PlayerVisionTarget.VisionTransform.position : watched.transform.position + Vector3.up * 1.2f;
            var to = transform.InverseTransformPoint(eyes) - _centre;
            float yaw = Mathf.Clamp(Mathf.Atan2(-to.x, -to.z) * Mathf.Rad2Deg, -MaxYaw, MaxYaw);
            float pitch = Mathf.Clamp(Mathf.Atan2(to.y, new Vector2(to.x, to.z).magnitude) * Mathf.Rad2Deg, -MaxPitch, MaxPitch);
            target = Quaternion.Euler(pitch, yaw, 0f);
            lead = Quaternion.Euler(pitch * PupilLead, yaw * PupilLead, 0f);
            if (Time.time > _blink)
            {
                _blinkEnd = Time.time + BlinkSeconds;
                _blink = Time.time + Random.Range(2.5f, 6f);
            }
        }
        _look = Quaternion.Slerp(_look, target, 1f - Mathf.Exp(-Follow * Time.deltaTime));
        _face.localRotation = _look;
        _lids.localRotation = _look;
        _pupils.localRotation = _look * lead;
        Show(awake && Time.time > _blinkEnd);
    }

    // The carrier being watched: the only one, or each in turn.
    private PlayerAvatar Watched()
    {
        var holders = _grab.playerGrabbing;
        if (holders.Count == 0) return null;
        if (Time.time > _switch)
        {
            _switch = Time.time + SwitchSeconds;
            _watched++;
        }
        for (int i = 0; i < holders.Count; i++)
        {
            var grabber = holders[(_watched + i) % holders.Count];
            if (grabber != null && grabber.playerAvatar != null) return grabber.playerAvatar;
        }
        return null;
    }

    private void Show(bool open)
    {
        if (_pupils.gameObject.activeSelf != open) _pupils.gameObject.SetActive(open);
        if (_lids.gameObject.activeSelf == open) _lids.gameObject.SetActive(!open);
    }
}
