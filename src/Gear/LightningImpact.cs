using System.Collections.Generic;
using IncreaseHardMod.Visuals;
using UnityEngine;
using UnityEngine.Rendering;

namespace IncreaseHardMod.Gear;

/// <summary>
/// Where a Wizard Staff bolt strikes a monster: a blue-white flash, a burst of sparks, a light flash and
/// small jagged arcs crawling over the struck body for a moment (electrocution). Spawned locally on every
/// client from the host's broadcast bolt points; removes itself when done. Cosmetic only.
/// </summary>
internal sealed class LightningImpact : MonoBehaviour
{
    private const float Life = .9f;
    private static readonly Color Core = new(.85f, .93f, 1f), Glow = new(.35f, .62f, 1f);
    private static Material _flashMaterial, _sparkMaterial;
    private static Mesh _quad;

    private readonly List<LineRenderer> _arcs = new();
    private Transform _flash;
    private Light _light;
    private Bounds _body;
    private float _age, _rejag;

    internal static void Spawn(Vector3 point, Material boltMaterial)
    {
        var fx = new GameObject("IHM Lightning Impact", typeof(Decoration)).AddComponent<LightningImpact>();
        fx.transform.position = point;
        fx.Build(point, boltMaterial);
    }

    private void Build(Vector3 point, Material boltMaterial)
    {
        _body = NearestBody(point);
        float size = Mathf.Clamp(_body.extents.magnitude, .3f, 1.6f);

        // Crawling arcs over the body: thin core + soft glow pairs.
        for (int i = 0; i < 6; i++)
        {
            var line = new GameObject("Arc").AddComponent<LineRenderer>();
            line.transform.SetParent(transform, false);
            line.useWorldSpace = true;
            line.sharedMaterial = boltMaterial;
            line.numCapVertices = 2;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.textureMode = LineTextureMode.Stretch;
            _arcs.Add(line);
        }

        _quad ??= ProcMesh.UnitQuad("IHM Lightning Flash Quad");
        _flashMaterial ??= GlowTextures.Sprite(GlowTextures.Star, "IHM Lightning Flash", 3110);
        _flash = new GameObject("Flash").transform;
        _flash.SetParent(transform, false);
        _flash.gameObject.AddComponent<MeshFilter>().sharedMesh = _quad;
        var fr = _flash.gameObject.AddComponent<MeshRenderer>();
        fr.sharedMaterial = _flashMaterial;
        fr.shadowCastingMode = ShadowCastingMode.Off;
        fr.receiveShadows = false;

        _light = new GameObject("Flash Light").AddComponent<Light>();
        _light.transform.SetParent(transform, false);
        _light.type = LightType.Point;
        _light.color = Glow;
        _light.range = Mathf.Clamp(size * 4f, 2.5f, 7f);
        _light.shadows = LightShadows.None;

        Sparks(size);
        Rejag();
    }

    // The struck monster's visible body near the hit (bolt points are monster centres), else a small sphere.
    private static Bounds NearestBody(Vector3 point)
    {
        var fallback = new Bounds(point, Vector3.one * .7f);
        if (EnemyDirector.instance == null || EnemyDirector.instance.enemiesSpawned == null) return fallback;
        Enemy best = null;
        float bestDistance = 2.5f;
        foreach (var parent in EnemyDirector.instance.enemiesSpawned)
        {
            var enemy = parent != null ? parent.Enemy : null;
            if (enemy == null || !enemy.isActiveAndEnabled) continue;
            var centre = enemy.CenterTransform != null ? enemy.CenterTransform.position : enemy.transform.position;
            float d = Vector3.Distance(centre, point);
            if (d < bestDistance) { bestDistance = d; best = enemy; }
        }
        if (best == null) return fallback;
        var mesh = best.HasHealth && best.Health != null && best.Health.meshParent != null ? best.Health.meshParent.transform : best.transform;
        bool found = false;
        var bounds = fallback;
        foreach (var r in mesh.GetComponentsInChildren<Renderer>())
        {
            if (!(r is MeshRenderer || r is SkinnedMeshRenderer) || !r.enabled || Decoration.Is(r)) continue;
            if (!found) { bounds = r.bounds; found = true; } else bounds.Encapsulate(r.bounds);
        }
        if (found && bounds.extents.magnitude > 3f) bounds = new Bounds(bounds.center, Vector3.Min(bounds.size, Vector3.one * 3f));
        return found ? bounds : fallback;
    }

    // Random point on the body, mostly on the side facing the viewer (arcs behind the body are wasted).
    private Vector3 SurfacePoint()
    {
        var d = Random.onUnitSphere;
        var cam = Camera.main;
        if (cam != null && Random.value < .85f)
        {
            var toCam = (cam.transform.position - _body.center).normalized;
            if (Vector3.Dot(d, toCam) < 0f) d = Vector3.Reflect(d, toCam);
        }
        return _body.center + Vector3.Scale(d, _body.extents * 1.02f);
    }

    // New jagged paths: arcs hop between random points on the body surface, bulging outward.
    private void Rejag()
    {
        for (int i = 0; i < _arcs.Count; i++)
        {
            var line = _arcs[i];
            var a = i == 0 ? transform.position : SurfacePoint();
            var b = SurfacePoint();
            var bulge = ((a + b) * .5f - _body.center).normalized * _body.extents.magnitude * .25f;
            int count = 15;
            line.positionCount = count;
            float jitter = Mathf.Clamp(_body.extents.magnitude * .12f, .03f, .18f);
            for (int k = 0; k < count; k++)
            {
                float t = k / (float)(count - 1);
                var p = Vector3.Lerp(a, b, t) + bulge * Mathf.Sin(t * Mathf.PI);
                if (k > 0 && k < count - 1) p += Random.insideUnitSphere * jitter;
                line.SetPosition(k, p);
            }
        }
    }

    private void Sparks(float size)
    {
        _sparkMaterial ??= GlowTextures.Sprite(GlowTextures.Soft, "IHM Lightning Spark", 3105);
        var ps = new GameObject("Sparks").AddComponent<ParticleSystem>();
        ps.transform.SetParent(transform, false);
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.loop = false; main.playOnAwake = false; main.duration = .2f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(.18f, .5f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(2.5f, 7f);
        main.startSize = new ParticleSystem.MinMaxCurve(.025f, .055f);
        main.startColor = new ParticleSystem.MinMaxGradient(Color.white, new Color(.55f, .8f, 1f));
        main.gravityModifier = 1.4f;
        main.maxParticles = 80;
        var emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 34), new ParticleSystem.Burst(.12f, 14) });
        var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Sphere; shape.radius = Mathf.Min(size * .15f, .2f);
        var colour = ps.colorOverLifetime; colour.enabled = true;
        var fade = new Gradient();
        fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Glow, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        colour.color = fade;
        var r = ps.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = _sparkMaterial;
        r.renderMode = ParticleSystemRenderMode.Stretch; // streaks, like hot sparks
        r.velocityScale = .05f;
        r.lengthScale = 1.5f;
        r.shadowCastingMode = ShadowCastingMode.Off;
        r.receiveShadows = false;
        ps.Play();
    }

    private void LateUpdate()
    {
        _age += Time.deltaTime;
        if (_age >= Life) { Destroy(gameObject); return; }
        float k = 1f - _age / Life;
        _rejag -= Time.deltaTime;
        if (_rejag <= 0f) { _rejag = .05f; Rejag(); }
        for (int i = 0; i < _arcs.Count; i++)
        {
            var line = _arcs[i];
            // Arcs drop out one by one as the charge bleeds away; pairs alternate thin core / soft glow.
            bool on = k > i / (float)_arcs.Count * .8f && Random.value > .15f;
            line.enabled = on;
            if (!on) continue;
            bool glow = i % 2 == 1;
            float w = (glow ? .11f : .022f) * (.4f + .6f * k);
            line.startWidth = w; line.endWidth = w * .5f;
            var c = glow ? Glow : Core; c.a = (glow ? .38f : 1f) * k;
            line.startColor = line.endColor = c;
        }
        var cam = Camera.main;
        float flash = Mathf.Clamp01(1f - _age / .18f);
        _flash.gameObject.SetActive(flash > 0f);
        if (flash > 0f && cam != null)
        {
            _flash.rotation = Quaternion.LookRotation(_flash.position - cam.transform.position, cam.transform.up) * Quaternion.Euler(0, 0, _age * 400f);
            _flash.localScale = Vector3.one * Mathf.Lerp(.5f, 1.3f, 1f - flash) * Mathf.Clamp(_body.extents.magnitude, .5f, 1.5f);
            _flashMaterial.color = new Color(Core.r, Core.g, Core.b, flash);
        }
        _light.intensity = (flash * 6f + k * 1.6f) * (.75f + Random.value * .5f);
    }
}
