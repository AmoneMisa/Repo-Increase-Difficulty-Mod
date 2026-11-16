using Photon.Pun;
using UnityEngine;

namespace IncreaseHardMod.Valuables;

/// <summary>
/// The Fable Realm valuables that shine: each carries one small lamp where its glowing part is, so it lights the
/// floor and walls around it and not only itself. Strength is set against the game's own Wizard Time Glass
/// (<see cref="CollectionLights.ReferenceIntensity"/>, <see cref="CollectionLights.ReferenceRange"/>).
/// Purely local: every client makes the same lamp, and a shared clock keeps the flicker in step.
/// </summary>
internal sealed class FableLights : MonoBehaviour
{
    // Where the lamp sits in the model (metres), its colour, strength, reach, and how much and how fast it wavers.
    private readonly struct Glow
    {
        internal readonly Vector3 Position; internal readonly Color Colour;
        internal readonly float Intensity, Range, Waver, Speed;
        internal Glow(Vector3 position, Color colour, float intensity, float range, float waver, float speed)
        { Position = position; Colour = colour; Intensity = intensity; Range = range; Waver = waver; Speed = speed; }
    }

    private static bool Find(string key, out Glow glow)
    {
        switch (key)
        {
            case FableLoot.Lamp: glow = new(new(0f, 1.43f, 0f), new Color(1f, .86f, .6f), 2f, 5f, .03f, 9f); return true;        // the bulb under the shade
            case FableLoot.Coffin: glow = new(new(0f, .5f, 0f), new Color(.75f, .9f, 1f), 1.5f, 4f, .06f, 1.1f); return true;     // the crystal case
            case FableLoot.Cauldron: glow = new(new(0f, .92f, 0f), new Color(.35f, 1f, .3f), 1.6f, 3.5f, .18f, 6f); return true;  // the brew
            case FableLoot.House: glow = new(new(0f, .34f, -.34f), new Color(1f, .8f, .45f), 1.3f, 3f, .1f, 7f); return true;     // the lit windows
            case FableLoot.Rose: glow = new(new(0f, .28f, 0f), new Color(1f, .3f, .45f), 1.3f, 3f, .15f, 1.4f); return true;      // the bloom under the dome
            case FableLoot.Mirror: glow = new(new(0f, .47f, -.14f), new Color(.6f, .85f, 1f), 1f, 2.5f, .1f, 1.8f); return true;  // the face in the glass
            case FableLoot.Slipper: glow = new(new(.03f, .08f, 0f), new Color(.6f, .9f, 1f), .6f, 1.5f, .12f, 2.2f); return true; // the glass and its bow
            default: glow = default; return false;
        }
    }

    internal static bool Applies(string key) => Find(key, out _);

    private Light _lamp;
    private Glow _glow;
    private float _phase;

    private void Start()
    {
        var art = GetComponent<ValuableArtInstance>();
        if (art == null || !art.EnsureBindings() || !Find(art.assetKey, out _glow)) { enabled = false; return; }
        _lamp = CollectionLights.Lamp(art.visualRoot, "IHM Fable glow", _glow.Position, _glow.Colour, _glow.Intensity, _glow.Range);
        var view = GetComponent<PhotonView>();
        _phase = view != null ? view.ViewID * .37f : 0f; // two of a kind do not pulse in step
    }

    private void Update()
    {
        if (_lamp == null) return;
        double time = GameManager.Multiplayer() ? PhotonNetwork.Time : Time.timeAsDouble;
        float t = (float)(time % 1000.0) * _glow.Speed + _phase;
        // Two sines of unrelated periods read as a living flame or a slow breath, depending on the speed.
        _lamp.intensity = _glow.Intensity * (1f + _glow.Waver * (.6f * Mathf.Sin(t) + .4f * Mathf.Sin(t * 2.3f + 1.7f)));
    }
}
