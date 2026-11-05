using UnityEngine;

namespace IncreaseHardMod.Valuables;

/// <summary>
/// The plush corgi's toy voice box. Host-side: barks when someone grabs it, yips or whines when it is
/// yanked, pushed or pulled hard (while held, or bumped by a player). Impacts use the soft plush cue preset.
/// Sounds go out through ValuableFoley's host RPC, so everyone hears the same cue.
/// </summary>
internal sealed class CorgiVoice : MonoBehaviour
{
    private const float Cooldown = 1.1f;
    private PhysGrabObject _grab;
    private Rigidbody _body;
    private ValuableFoley _foley;
    private int _holders;
    private Vector3 _lastVelocity;
    private float _next, _jerk;

    private void Start()
    {
        _grab = GetComponent<PhysGrabObject>();
        _body = GetComponent<Rigidbody>();
        _foley = GetComponent<ValuableFoley>();
    }

    private bool Host => SemiFunc.IsMasterClientOrSingleplayer();

    private void FixedUpdate()
    {
        if (!Host || _grab == null || _body == null || _foley == null) return;
        int holders = _grab.playerGrabbing.Count;
        if (holders > _holders) Say(Random.value < .55f ? ValuableFoley.DogBark : ValuableFoley.DogArf, .3f, force: true);
        _holders = holders;

        // Pushed/pulled: a sharp change of velocity (filtered so a steady carry stays quiet).
        var velocity = _body.velocity;
        float accel = (velocity - _lastVelocity).magnitude / Mathf.Max(Time.fixedDeltaTime, .001f);
        _lastVelocity = velocity;
        _jerk = Mathf.Max(accel, _jerk * .85f);
        if (holders > 0 && _jerk > 22f)
        {
            _jerk = 0;
            Say(Random.value < .35f ? ValuableFoley.DogWhine : Random.value < .5f ? ValuableFoley.DogYip : ValuableFoley.DogArf, .26f);
        }
    }

    // The root holds the Rigidbody, so this fires for every child collider.
    private void OnCollisionEnter(Collision collision)
    {
        if (!Host || _foley == null) return;
        float speed = collision.relativeVelocity.magnitude;
        bool byPlayer = collision.collider != null && collision.collider.GetComponentInParent<PlayerAvatar>() != null;
        if (byPlayer && speed > .8f) Say(Random.value < .5f ? ValuableFoley.DogYip : ValuableFoley.DogWhine, .24f);
        else if (speed > 3.5f) Say(ValuableFoley.DogYip, .22f);
    }

    private void Say(int cue, float gain, bool force = false)
    {
        if (!force && Time.time < _next) return;
        _next = Time.time + Cooldown;
        _foley.Emit(cue, gain);
    }
}
