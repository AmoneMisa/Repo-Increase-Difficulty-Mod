using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace IncreaseHardMod.Courier;

/// <summary>
/// The pet senses danger: while a monster is within <see cref="CourierRules.DangerRadius"/> of it (through walls
/// too, a little wider than the range at which monsters notice a courier), a glowing red "!" floats over its head and
/// it gives a short alarm chirp when the danger starts (again every few seconds while it lasts). Worked out on every
/// client from the pet's and the monsters' synced positions; only players hear the chirp (no monster investigates
/// it), and the neutral Golden Pig does not count.
/// </summary>
internal sealed class CourierDanger : MonoBehaviour
{
    private static Mesh _mark, _halo;
    private static Material _markMaterial, _haloMaterial;
    private static Sound _alarm;

    private Transform _visual;
    private Light _light;
    private float _nextScan, _shown, _nextChirp, _since;
    private bool _danger;

    /// <summary>Hidden while the pet is knocked out (its model is off).</summary>
    internal bool Suppressed;
    internal float Height = .7f;

    private void Awake()
    {
        EnsureAssets();
        _visual = new GameObject("Danger mark").transform;
        _visual.SetParent(transform, false);
        Part("Halo", _halo, _haloMaterial, new Vector3(0f, 0f, .01f));
        Part("Mark", _mark, _markMaterial, Vector3.zero);
        _light = new GameObject("Danger glow").AddComponent<Light>();
        _light.transform.SetParent(_visual, false);
        _light.type = LightType.Point;
        _light.color = new Color(1f, .12f, .08f);
        _light.range = 2.2f;
        _light.shadows = LightShadows.None;
        _visual.gameObject.SetActive(false);
    }

    private void Part(string name, Mesh mesh, Material material, Vector3 offset)
    {
        var go = new GameObject(name);
        go.transform.SetParent(_visual, false);
        go.transform.localPosition = offset;
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var r = go.AddComponent<MeshRenderer>();
        r.sharedMaterial = material;
        r.shadowCastingMode = ShadowCastingMode.Off;
        r.receiveShadows = false;
    }

    private void Update()
    {
        if (Time.time >= _nextScan) { _nextScan = Time.time + .3f; Scan(); }
        bool show = _danger && !Suppressed;
        _shown = Mathf.MoveTowards(_shown, show ? 1f : 0f, Time.deltaTime * (show ? 6f : 3f));
        bool visible = _shown > .01f;
        if (_visual.gameObject.activeSelf != visible) _visual.gameObject.SetActive(visible);
        if (show && Time.time >= _nextChirp)
        {
            _nextChirp = Time.time + CourierRules.DangerChirpEvery;
            try { _alarm.Play(transform.position); } catch { }
        }
        if (!visible) return;
        float t = Time.time - _since;
        float pop = Mathf.Clamp01(t / .2f);
        _visual.position = transform.position + Vector3.up * (Height + .05f * Mathf.Sin(Time.time * 4f));
        var cam = Camera.main != null ? Camera.main.transform : null;
        if (cam != null) _visual.rotation = Quaternion.LookRotation(_visual.position - cam.position, Vector3.up);
        _visual.localScale = Vector3.one * (.2f * _shown * Mathf.Lerp(1.5f, 1f, pop) * (1f + .08f * Mathf.Sin(Time.time * 9f)));
        _light.intensity = 1.8f * _shown * (.8f + .2f * Mathf.Sin(Time.time * 9f));
    }

    private void Scan()
    {
        bool danger = false;
        var director = EnemyDirector.instance;
        if (director != null && director.enemiesSpawned != null && SemiFunc.RunIsLevel())
        {
            var at = transform.position;
            foreach (var parent in director.enemiesSpawned)
            {
                if (parent == null || !parent.Spawned || parent.Enemy == null) continue;
                if (parent.GetComponentInChildren<Monsters.PigBrain>() != null) continue; // neutral
                var pos = parent.Enemy.CenterTransform != null ? parent.Enemy.CenterTransform.position : parent.Enemy.transform.position;
                if (Mathf.Abs(pos.y - at.y) > 5f) continue; // another floor
                if (new Vector2(pos.x - at.x, pos.z - at.z).sqrMagnitude > CourierRules.DangerRadius * CourierRules.DangerRadius) continue;
                danger = true; break;
            }
        }
        if (danger && !_danger) { _since = Time.time; _nextChirp = Mathf.Min(_nextChirp, Time.time); }
        _danger = danger;
    }

    private static void EnsureAssets()
    {
        if (_mark != null) return;
        var additive = Monsters.MonsterFx.Additive();
        _markMaterial = new Material(additive) { name = "IHM courier danger", color = new Color(1f, .16f, .1f, 1f), hideFlags = HideFlags.HideAndDontSave };
        _haloMaterial = new Material(additive) { name = "IHM courier danger halo", color = new Color(1f, .1f, .05f, .35f), hideFlags = HideFlags.HideAndDontSave };
        // "!": a bar narrowing to the bottom and a round dot, in a unit-tall box around the origin; both sides drawn.
        var v = new List<Vector3>(); var tris = new List<int>();
        v.AddRange(new[] { new Vector3(-.13f, .5f), new Vector3(.13f, .5f), new Vector3(.08f, -.18f), new Vector3(-.08f, -.18f) });
        tris.AddRange(new[] { 0, 1, 2, 0, 2, 3, 0, 2, 1, 0, 3, 2 });
        int c = v.Count; v.Add(new Vector3(0f, -.38f));
        for (int k = 0; k < 16; k++) { float a = k * Mathf.PI / 8f; v.Add(new Vector3(Mathf.Cos(a) * .1f, -.38f + Mathf.Sin(a) * .1f)); }
        for (int k = 0; k < 16; k++) { int a = c + 1 + k, b = c + 1 + (k + 1) % 16; tris.AddRange(new[] { c, a, b, c, b, a }); }
        _mark = new Mesh { name = "IHM courier danger", hideFlags = HideFlags.HideAndDontSave };
        _mark.SetVertices(v); _mark.SetTriangles(tris, 0); _mark.RecalculateBounds();
        // A soft round glow behind it (vertex alpha fades to the rim).
        var hv = new List<Vector3> { Vector3.zero }; var hc = new List<Color> { Color.white }; var ht = new List<int>();
        for (int k = 0; k < 24; k++) { float a = k * Mathf.PI / 12f; hv.Add(new Vector3(Mathf.Cos(a) * .62f, Mathf.Sin(a) * .62f)); hc.Add(new Color(1f, 1f, 1f, 0f)); }
        for (int k = 0; k < 24; k++) { int a = 1 + k, b = 1 + (k + 1) % 24; ht.AddRange(new[] { 0, a, b, 0, b, a }); }
        _halo = new Mesh { name = "IHM courier danger halo", hideFlags = HideFlags.HideAndDontSave };
        _halo.SetVertices(hv); _halo.SetColors(hc); _halo.SetTriangles(ht, 0); _halo.RecalculateBounds();
        _alarm = new Sound
        {
            Sounds = new[] { AlarmClip() }, Volume = .4f, VolumeRandom = .03f, Pitch = 1f, PitchRandom = .02f,
            SpatialBlend = .85f, Doppler = 0f, Type = AudioManager.AudioType.Default,
        };
    }

    // "Danger!": three quick rising two-tone chirps, bright and short, unlike any monster cue.
    private static AudioClip AlarmClip()
    {
        const int rate = 44100; const float length = .62f;
        int n = (int)(rate * length);
        var data = new float[n];
        float phase = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)rate;
            int pulse = Mathf.Min(2, (int)(t / .2f));
            float local = t - pulse * .2f;
            bool on = local < .14f;
            float f = (local < .07f ? 880f : 1320f) * (1f + pulse * .06f);
            phase += 2f * Mathf.PI * f / rate;
            float env = on ? Mathf.Clamp01(local / .005f) * Mathf.Clamp01((.14f - local) / .02f) : 0f;
            data[i] = (Mathf.Sin(phase) * .5f + Mathf.Sin(phase * 2f) * .12f) * env * .8f;
        }
        var clip = AudioClip.Create("IHM courier danger", n, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }
}
