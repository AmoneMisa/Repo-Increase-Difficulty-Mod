using IncreaseHardMod.Gear;
using UnityEngine;
using UnityEngine.Rendering;

namespace IncreaseHardMod.Visuals;

/// <summary>Cosmetic cable. The host's GearEffects remains the only owner of pull forces.</summary>
internal sealed class StickyTether : MonoBehaviour
{
    private ItemGun _gun;
    private PhysGrabObject _target;
    private Vector3 _point;
    private float _until, _began;
    private LineRenderer _line;
    private Transform _cup;
    private static Material _material;

    internal static void Pulse(ItemGun gun, Vector3 end)
    {
        if (gun == null) return;
        var visual = Get(gun);
        if (visual._target != null && visual._until > Time.time) return;
        visual._target = null;
        visual._point = end;
        visual._began = Time.time;
        visual._until = Time.time + .55f;
    }

    internal static void Apply(ItemGun gun, PhysGrabObject target, Vector3 localPoint, float seconds)
    {
        var gear = gun != null ? gun.GetComponent<GearGun>() : null;
        if (gear == null || gear.kind != GearKind.Sticky || target == null || seconds <= 0f ||
            float.IsNaN(seconds) || float.IsInfinity(seconds)) return;
        var visual = Get(gun);
        visual._target = target;
        visual._point = localPoint;
        visual._began = Time.time;
        visual._until = Time.time + Mathf.Min(seconds, 2f);
        ItemAudio.Cue("sticky_hit", .12f).Play(target.transform.TransformPoint(localPoint));
    }

    private static StickyTether Get(ItemGun gun)
    {
        var visual = gun.GetComponent<StickyTether>();
        if (visual == null) visual = gun.gameObject.AddComponent<StickyTether>();
        visual._gun = gun;
        return visual;
    }

    private void LateUpdate()
    {
        bool active = _gun != null && _gun.isActiveAndEnabled && _gun.gunMuzzle != null && Time.time < _until;
        if (!active) { if (_line != null) _line.enabled = false; if (_cup != null) _cup.gameObject.SetActive(false); return; }
        var start = _gun.gunMuzzle.position;
        var end = _target != null ? _target.transform.TransformPoint(_point) : _point;
        if (_target != null && (!_target.isActiveAndEnabled || Vector3.Distance(start, end) < 1.2f))
        { _until = 0f; if (_line != null) _line.enabled = false; if (_cup != null) _cup.gameObject.SetActive(false); return; }
        if (_line == null)
        {
            var go = new GameObject("Sticky cable");
            go.transform.SetParent(transform, false);
            _line = go.AddComponent<LineRenderer>();
            _line.useWorldSpace = true;
            _line.positionCount = 24;
            _line.startWidth = _line.endWidth = .022f;
            _line.numCapVertices = 3;
            _line.shadowCastingMode = ShadowCastingMode.Off;
            _line.receiveShadows = false;
            if (_material == null) _material = new Material(Shader.Find("Sprites/Default"));
            _line.sharedMaterial = _material;
            _line.startColor = new Color(.82f,.66f,.4f,.9f);
            _line.endColor = new Color(.95f,.82f,.55f,.9f);
        }
        _line.enabled = true;
        float age = Time.time - _began;
        // Launch, flatten on impact, then reel the cup back into the muzzle.
        float reach = Mathf.Clamp01(age / .12f) * Mathf.Clamp01((_until - Time.time) / .16f);
        end = Vector3.Lerp(start,end,reach);
        var delta = end-start;
        if (_cup == null)
        {
            var cup = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            cup.name = "Sticky suction cup";
            var collider = cup.GetComponent<Collider>(); collider.enabled = false; Destroy(collider);
            cup.GetComponent<Renderer>().sharedMaterial = _material;
            cup.GetComponent<Renderer>().material.color = new Color(.95f, .35f, .65f);
            // World-space and unparented: an item's own scale must not stretch the cup.
            _cup = cup.transform;
        }
        _cup.gameObject.SetActive(true);
        _cup.SetPositionAndRotation(end, Quaternion.LookRotation(delta.sqrMagnitude > .0001f ? delta : transform.forward));
        float impact = Mathf.Clamp01((age - .1f) / .06f);
        _cup.localScale = new Vector3(.13f + impact * .1f, .13f + impact * .1f, .12f - impact * .07f);
        var side = Vector3.Cross(delta.normalized,Vector3.up).normalized;
        if (side.sqrMagnitude < .01f) side = Vector3.right;
        for (int i=0;i<24;i++)
        {
            float t=i/23f;
            float envelope=Mathf.Sin(Mathf.PI*t);
            float coil=Mathf.Sin(t*Mathf.PI*8f-age*24f)*.18f*Mathf.Exp(-age*12f);
            _line.SetPosition(i,Vector3.Lerp(start,end,t)+side*coil*envelope-Vector3.up*(.05f*envelope));
        }
    }

    private void OnDisable() { if (_cup != null) _cup.gameObject.SetActive(false); }

    private void OnDestroy()
    {
        if (_cup == null) return;
        Destroy(_cup.GetComponent<Renderer>().material);
        Destroy(_cup.gameObject);
    }
}
