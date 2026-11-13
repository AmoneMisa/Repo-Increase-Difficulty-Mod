using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace IncreaseHardMod.Monsters;

/// <summary>One-off monster effects every client draws (the host decides, <see cref="MonsterBrain.Fx"/> sends).</summary>
internal static class MonsterFx
{
    internal const byte FireballLaunch = 0, FireballBurst = 1, WallImpact = 2, Slash = 3, StoneFlash = 4, Bite = 5, HeadToss = 6;

    private static Material _additive;
    private static readonly List<Fireball> Balls = new();

    internal static Material Additive()
    {
        if (_additive != null) return _additive;
        var shader = Shader.Find("Sprites/Default") ?? Shader.Find("Standard");
        _additive = new Material(shader) { name = "IHM monster fx" };
        return _additive;
    }

    internal static void Play(MonsterBrain brain, byte fx, Vector3 a, Vector3 b)
    {
        switch (fx)
        {
            case FireballLaunch:
                Balls.RemoveAll(x => x == null);
                var go = new GameObject("IHM Fireball");
                go.transform.position = a;
                var ball = go.AddComponent<Fireball>();
                ball.Velocity = b;
                Balls.Add(ball);
                MonsterAudio.Play(MonsterAudio.Cue.Fireball, a);
                break;
            case FireballBurst:
                Fireball nearest = null; float best = 4f;
                foreach (var x in Balls) if (x != null) { float d = Vector3.Distance(x.transform.position, a); if (d < best) { best = d; nearest = x; } }
                if (nearest != null) Object.Destroy(nearest.gameObject);
                Flash(a, new Color(1f, .5f, .1f), 6f, .35f);
                MonsterAudio.Play(MonsterAudio.Cue.Thud, a, .6f);
                break;
            case WallImpact:
                Flash(a, new Color(1f, .9f, .7f), 2f, .15f);
                MonsterAudio.Play(MonsterAudio.Cue.Thud, a, 1.2f);
                GameDirector.instance?.CameraShake?.ShakeDistance(6f, 4f, 12f, a, .4f);
                GameDirector.instance?.CameraImpact?.ShakeDistance(6f, 4f, 12f, a, .2f);
                break;
            case Slash:
                SlashArc(a, b);
                break;
            case Bite:
                MonsterAudio.Play(MonsterAudio.Cue.Bite, a);
                MonsterAudio.Play(MonsterAudio.Cue.Spit, a, .7f, 1.1f); // venom going in
                break;
            case HeadToss:
                if (brain != null && brain.Visual != null) brain.Visual.Toss();
                MonsterAudio.Play(MonsterAudio.Cue.Thud, a);
                MonsterAudio.Play(MonsterAudio.Cue.Snort, a, .8f);
                break;
            case StoneFlash:
                Flash(a, new Color(.85f, .85f, .75f), 4f, .4f);
                MonsterAudio.Play(MonsterAudio.Cue.Petrify, a);
                break;
        }
    }

    private static void Flash(Vector3 at, Color color, float intensity, float seconds)
    {
        var light = new GameObject("IHM monster flash").AddComponent<Light>();
        light.transform.position = at;
        light.type = LightType.Point;
        light.color = color;
        light.range = 5f;
        light.intensity = intensity;
        light.shadows = LightShadows.None;
        light.gameObject.AddComponent<FadeLight>().Seconds = seconds;
    }

    // A short glowing arc showing where a sword sweep landed (a = centre, b = forward * range, arc in degrees in b.y).
    private static void SlashArc(Vector3 center, Vector3 forwardRange)
    {
        var go = new GameObject("IHM slash");
        var line = go.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.sharedMaterial = Additive();
        line.shadowCastingMode = ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.startWidth = .12f; line.endWidth = .02f;
        line.startColor = new Color(1f, .9f, .75f, .9f); line.endColor = new Color(1f, .4f, .3f, 0f);
        float arc = Mathf.Clamp(forwardRange.y, 20f, 340f);
        var forward = new Vector3(forwardRange.x, 0f, forwardRange.z);
        float range = forward.magnitude;
        if (range < .1f) { Object.Destroy(go); return; }
        forward /= range;
        const int n = 14;
        line.positionCount = n;
        for (int i = 0; i < n; i++)
        {
            float ang = -arc * .5f + arc * i / (n - 1);
            line.SetPosition(i, center + Quaternion.Euler(0f, ang, 0f) * forward * range + Vector3.up * 1.1f);
        }
        go.AddComponent<FadeLine>();
        MonsterAudio.Play(arc > 120f ? MonsterAudio.Cue.SwingWide : MonsterAudio.Cue.Swing, center);
    }

    private sealed class FadeLight : MonoBehaviour
    {
        public float Seconds = .3f;
        private float _start, _intensity;
        private Light _light;
        private void Start() { _light = GetComponent<Light>(); _start = Time.time; _intensity = _light.intensity; }
        private void Update()
        {
            float u = (Time.time - _start) / Mathf.Max(.01f, Seconds);
            if (u >= 1f) { Destroy(gameObject); return; }
            _light.intensity = _intensity * (1f - u);
        }
    }

    private sealed class FadeLine : MonoBehaviour
    {
        private float _start;
        private LineRenderer _line;
        private void Start() { _line = GetComponent<LineRenderer>(); _start = Time.time; }
        private void Update()
        {
            float u = (Time.time - _start) / .3f;
            if (u >= 1f) { Destroy(gameObject); return; }
            _line.widthMultiplier = 1f - u;
        }
    }
}

/// <summary>Client-side flight of a witch fireball (the host's copy decides the hit and sends the burst).</summary>
internal sealed class Fireball : MonoBehaviour
{
    public Vector3 Velocity;
    private float _born;
    private Transform _core;
    private Light _light;

    private void Start()
    {
        _born = Time.time;
        var core = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        var collider = core.GetComponent<Collider>(); collider.enabled = false; Destroy(collider);
        core.transform.SetParent(transform, false);
        core.transform.localScale = Vector3.one * .32f;
        var renderer = core.GetComponent<Renderer>();
        renderer.sharedMaterial = MonsterFx.Additive();
        renderer.material.color = new Color(1f, .55f, .12f);
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        _core = core.transform;
        _light = gameObject.AddComponent<Light>();
        _light.type = LightType.Point; _light.color = new Color(1f, .5f, .15f); _light.range = 4f; _light.intensity = 2.5f;
        _light.shadows = LightShadows.None;
        var trail = gameObject.AddComponent<TrailRenderer>();
        trail.sharedMaterial = MonsterFx.Additive();
        trail.time = .25f; trail.startWidth = .28f; trail.endWidth = 0f;
        trail.startColor = new Color(1f, .6f, .1f, .9f); trail.endColor = new Color(.6f, .1f, 0f, 0f);
        trail.shadowCastingMode = ShadowCastingMode.Off;
    }

    private void Update()
    {
        transform.position += Velocity * Time.deltaTime;
        if (_core != null) _core.localScale = Vector3.one * (.3f + .05f * Mathf.Sin(Time.time * 30f));
        if (_light != null) _light.intensity = 2.2f + .6f * Mathf.Sin(Time.time * 25f);
        if (Time.time - _born > 3f) Destroy(gameObject);
    }

    private void OnDestroy()
    {
        if (_core != null) { var r = _core.GetComponent<Renderer>(); if (r != null) Destroy(r.material); }
    }
}
