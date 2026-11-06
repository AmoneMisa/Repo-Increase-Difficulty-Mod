using UnityEngine;

namespace IncreaseHardMod.Gear;

/// <summary>
/// Shows the Flamethrower C.A.R.T.'s grab state on its hand plate and handle bar, like a vanilla C.A.R.T.:
/// red when nobody holds it, orange while it is dragged by any other grip, green while it is pushed on the
/// handle. Runs on every client: "on the handle" comes from <see cref="FlameCartHandle"/>, whose grabber
/// list the host broadcasts, and "dragged" from each player's grab link, which the game syncs to everyone.
/// </summary>
internal sealed class FlameCartGrabState : MonoBehaviour
{
    private static readonly Color Idle = new(1f, .12f, .08f), Dragged = new(1f, .46f, 0f), Handled = new(.2f, 1f, .15f);

    private PhysGrabObject _pgo;
    private FlameCartHandle _handle;
    private Material _plate, _bar;
    private Color _shown = Idle;
    private float _nextCheck;
    private bool _dragged;

    private void Start()
    {
        _pgo = GetComponent<PhysGrabObject>();
        _handle = GetComponent<FlameCartHandle>();
        foreach (var r in GetComponentsInChildren<MeshRenderer>(true))
        {
            if (r.name == GrabMarker.PlateName) _plate = r.material;   // per-instance copy
            else if (r.name == GrabMarker.BarName) _bar = r.material;
        }
        Apply(Idle);
    }

    private bool AnyoneDragging()
    {
        if (_pgo == null) return false;
        if (_pgo.playerGrabbing.Count > 0) return true;                // host / singleplayer
        var director = GameDirector.instance;
        if (director == null || director.PlayerList == null) return false;
        foreach (var player in director.PlayerList)
        {
            var grabber = player != null ? player.physGrabber : null;
            if (grabber != null && grabber.grabbed && grabber.grabbedPhysGrabObject == _pgo) return true;
        }
        return false;
    }

    private void Update()
    {
        if (_plate == null && _bar == null) return;
        if (Time.time >= _nextCheck) { _nextCheck = Time.time + .1f; _dragged = AnyoneDragging(); }
        var target = _handle != null && _handle.Pushed ? Handled : _dragged ? Dragged : Idle;
        // A soft breathing glow keeps the plate readable in dark rooms; brighter while held.
        float glow = target == Idle ? .8f + .2f * Mathf.Sin(Time.time * 2.4f) : 1f;
        _shown = Color.Lerp(_shown, target, 1f - Mathf.Exp(-Time.deltaTime * 14f));
        Apply(_shown * glow);
    }

    private void Apply(Color c)
    {
        c.a = 1f;
        if (_plate != null) _plate.color = c;
        if (_bar != null) _bar.color = c;
    }

    private void OnDestroy()
    {
        if (_plate != null) Destroy(_plate);
        if (_bar != null) Destroy(_bar);
    }
}
