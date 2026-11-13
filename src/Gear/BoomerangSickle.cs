using System.Collections.Generic;
using UnityEngine;

namespace IncreaseHardMod.Gear;

/// <summary>
/// Boomerang Sickle: a normal melee weapon (the sword's swing) that can be thrown. Released at speed it spins, cuts
/// every monster it touches once for <see cref="SickleRules.ThrowDamage"/>, and after half a second flies back to the
/// thrower, dropping next to them. The host moves it (it simulates loose physics objects) and deals the damage.
/// </summary>
internal sealed class BoomerangSickle : MonoBehaviour
{
    private PhysGrabObject _grab;
    private PlayerAvatar _holder;
    private bool _wasHeld;
    private float _flight = -1f;
    private bool _gravity;
    private readonly HashSet<Enemy> _hit = new();

    private void Awake() => _grab = GetComponent<PhysGrabObject>();

    private void FixedUpdate()
    {
        if (_grab == null || _grab.rb == null || !SemiFunc.IsMasterClientOrSingleplayer()) return;
        var rb = _grab.rb;
        bool held = _grab.playerGrabbing.Count > 0;
        if (held)
        {
            var grabber = _grab.playerGrabbing[0];
            if (grabber != null && grabber.playerAvatar != null) _holder = grabber.playerAvatar;
            if (_flight >= 0f) Land(rb);
        }
        else if (SickleRules.Thrown(_wasHeld, held, rb.velocity.magnitude) && _holder != null)
        {
            _flight = 0f;
            _hit.Clear();
            _gravity = rb.useGravity;
            rb.useGravity = false;
        }
        _wasHeld = held;
        if (_flight < 0f) return;

        _flight += Time.fixedDeltaTime;
        if (_holder == null || _holder.isDisabled) { Land(rb); return; }
        var chest = _holder.transform.position + Vector3.up * 1.1f;
        float distance = Vector3.Distance(rb.position, chest);
        if (SickleRules.Ends(_flight, distance)) { Land(rb); return; }
        rb.angularVelocity = transform.up * SickleRules.Spin;
        if (SickleRules.Returning(_flight))
        {
            var desired = (chest - rb.position).normalized * SickleRules.ReturnSpeed;
            rb.velocity = Vector3.Lerp(rb.velocity, desired, Mathf.Clamp01(SickleRules.Steer * Time.fixedDeltaTime));
        }
    }

    private void Land(Rigidbody rb)
    {
        _flight = -1f;
        rb.useGravity = _gravity;
        rb.velocity *= .3f;
        rb.angularVelocity *= .2f;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (_flight < 0f || !SemiFunc.IsMasterClientOrSingleplayer() || collision.collider == null) return;
        var body = collision.collider.GetComponentInParent<EnemyRigidbody>();
        var enemy = body != null ? body.enemy : null;
        if (enemy == null || !enemy.HasHealth || enemy.Health == null || enemy.Health.dead || !_hit.Add(enemy)) return;
        var direction = _grab.rb.velocity.sqrMagnitude > .01f ? _grab.rb.velocity.normalized : transform.forward;
        enemy.Health.Hurt(SickleRules.ThrowDamage, direction);
    }
}
