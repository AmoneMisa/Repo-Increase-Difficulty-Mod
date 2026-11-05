using System;
using System.Collections.Generic;
using IncreaseHardMod.Upgrades;
using UnityEngine;
using UnityEngine.Rendering;

namespace IncreaseHardMod.Visuals;

/// <summary>Cosmetics only. The host's GearEffects owns all damage, targeting and expiry.</summary>
internal sealed class EnemyStatusVisual : MonoBehaviour
{
    private Enemy _enemy;
    private Renderer[] _body;
    private float _poisonUntil, _iceUntil, _netUntil;
    private NetDrape _net;

    private void EndNet(bool torn)
    {
        _netUntil = 0f;
        if (_net != null) { _net.Release(torn); _net = null; }
    }
    private ParticleSystem _cloud;
    private static Material _cloudMaterial;
    private bool _iceApplied;
    private readonly List<FrostSlot> _frost = new();

    private sealed class FrostSlot
    {
        public Renderer Renderer;
        public Material[] Original, Iced;
    }

    internal static void Apply(Enemy enemy, byte kind, float seconds)
    {
        if (enemy == null || !enemy.isActiveAndEnabled || kind > 2 ||
            float.IsNaN(seconds) || float.IsInfinity(seconds)) return;
        var visual = enemy.GetComponent<EnemyStatusVisual>();
        if (kind == 2 && seconds < 0f)
        {
            // Net ended early (torn free) or ran out.
            if (visual != null) visual.EndNet(seconds == Gear.GearEffects.NetBroken);
            return;
        }
        if (seconds <= 0f) return;
        if (visual == null) visual = enemy.gameObject.AddComponent<EnemyStatusVisual>();
        visual.enabled = true;
        if (kind == 0) visual._poisonUntil = Time.time + Mathf.Min(seconds, 600f);
        else if (kind == 1) visual._iceUntil = Time.time + Mathf.Min(seconds, 600f);
        else visual._netUntil = Time.time + Mathf.Min(seconds, 600f);
    }

    private void Awake()
    {
        _enemy = GetComponent<Enemy>();
        var mesh = _enemy.HasHealth && _enemy.Health != null ? _enemy.Health.meshParent : null;
        var candidates = (mesh != null ? mesh : transform).GetComponentsInChildren<Renderer>(true);
        var body = new List<Renderer>();
        foreach (var renderer in candidates)
            if (renderer is MeshRenderer or SkinnedMeshRenderer) body.Add(renderer);
        _body = body.ToArray();
    }

    private bool Alive => _enemy != null && _enemy.isActiveAndEnabled &&
        (!_enemy.HasHealth || _enemy.Health == null || (!_enemy.Health.dead && _enemy.Health.healthCurrent > 0));

    private Bounds BodyBounds()
    {
        var bounds = new Bounds(transform.position + Vector3.up * .5f, Vector3.one * .5f);
        bool found = false;
        foreach (var renderer in _body)
        {
            if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
            if (!found) { bounds = renderer.bounds; found = true; }
            else bounds.Encapsulate(renderer.bounds);
        }
        return bounds;
    }

    private void LateUpdate()
    {
        if (!Alive) { enabled = false; return; }
        bool poisoned = Time.time < _poisonUntil;
        if (poisoned && _cloud == null) CreateCloud();
        if (_cloud != null)
        {
            var bounds = BodyBounds();
            _cloud.transform.position = bounds.center - Vector3.up * bounds.extents.y * .25f;
            var emission = _cloud.emission; emission.enabled = poisoned;
            if (poisoned && !_cloud.isPlaying) _cloud.Play();
            if (!poisoned && !_cloud.IsAlive()) { Destroy(_cloud.gameObject); _cloud = null; }
        }
        if (Time.time < _iceUntil)
        {
            if (!_iceApplied) ApplyFrost();
            // Native hurt flashes can write to an instance material. Reassert the coating in LateUpdate.
            float glow = .15f + Mathf.Sin(Time.time * 2.5f) * .025f;
            foreach (var slot in _frost)
                foreach (var material in slot.Iced)
                {
                    if (material == null) continue;
                    if (material.HasProperty("_Color")) material.SetColor("_Color", new Color(.32f, .67f, 1f));
                    if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", new Color(.32f, .67f, 1f));
                    if (material.HasProperty("_EmissionColor")) material.SetColor("_EmissionColor", new Color(.1f, .38f, .8f) * glow);
                }
        }
        else if (_iceApplied) RestoreFrost();
        if (Time.time < _netUntil)
        {
            if (_net == null && ModConfig.NetLauncherEnabled.Value) _net = NetDrape.Create(transform, BodyBounds());
        }
        else if (_net != null) { _net.Release(false); _net = null; }
        if (!poisoned && _cloud == null && !_iceApplied && _net == null) enabled = false;
    }

    private void CreateCloud()
    {
        if (_cloudMaterial == null)
        {
            var shader = Shader.Find("Particles/Standard Unlit") ?? Shader.Find("Legacy Shaders/Particles/Alpha Blended") ?? Shader.Find("Sprites/Default");
            if (shader == null)
            {
                Plugin.Log.LogWarning("[Art] No supported poison particle shader in this game build.");
                _poisonUntil = 0f;
                return;
            }
            _cloudMaterial = new Material(shader) { name = "IHM Poison Mist", renderQueue = 3000 };
            _cloudMaterial.mainTexture = UpgradeIcons.LoadTexture("poison_puff.png");
            _cloudMaterial.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            _cloudMaterial.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            _cloudMaterial.SetInt("_ZWrite", 0);
            _cloudMaterial.EnableKeyword("_ALPHABLEND_ON");
        }
        var obj = new GameObject("IHM Poison Cloud");
        obj.transform.SetParent(transform, false);
        _cloud = obj.AddComponent<ParticleSystem>();
        _cloud.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var bounds = BodyBounds();
        float size = Mathf.Clamp(bounds.extents.magnitude * .45f, .22f, 1.1f);
        var main = _cloud.main;
        main.loop = true; main.playOnAwake = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(.7f, 1.3f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(.08f, .26f);
        main.startSize = new ParticleSystem.MinMaxCurve(size * .65f, size);
        main.startColor = new Color(.66f, .83f, .33f, .45f);
        main.maxParticles = 36; main.simulationSpace = ParticleSystemSimulationSpace.World;
        var shape = _cloud.shape; shape.shapeType = ParticleSystemShapeType.Sphere; shape.radius = size * .65f;
        var emission = _cloud.emission; emission.rateOverTime = 12f;
        var velocity = _cloud.velocityOverLifetime; velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World; velocity.y = .22f;
        var color = _cloud.colorOverLifetime; color.enabled = true;
        var fade = new Gradient();
        fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, .2f), new GradientAlphaKey(0f, 1f) });
        color.color = fade;
        var renderer = obj.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = _cloudMaterial;
        renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
        _cloud.Play();
    }

    private IceBlock _block;

    private void ApplyFrost()
    {
        _iceApplied = true;
        if (ModConfig.IceBlockEnabled.Value && _block == null)
        {
            // Seeded from the enemy's network ID so everyone sees the same block.
            var view = GetComponentInParent<Photon.Pun.PhotonView>();
            _block = IceBlock.Create(transform, BodyBounds(), view != null ? view.ViewID : GetInstanceID(), _body);
        }
        var texture = UpgradeIcons.LoadTexture("ice_frost.png");
        foreach (var renderer in _body)
        {
            if (renderer == null) continue;
            var original = renderer.sharedMaterials;
            var iced = new Material[original.Length];
            for (int i = 0; i < original.Length; i++)
            {
                if (original[i] == null) continue;
                // Retain the native shader, skinning, culling and transparency behavior. No pink
                // unsupported AssetBundle shader and no duplicate skinned renderer are necessary.
                var material = iced[i] = new Material(original[i]) { name = "IHM Frost " + original[i].name };
                if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", texture);
                if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", texture);
                if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", .8f);
                if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", .8f);
                if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", .2f);
                if (material.HasProperty("_EmissionMap")) material.SetTexture("_EmissionMap", texture);
                material.EnableKeyword("_EMISSION");
            }
            _frost.Add(new FrostSlot { Renderer = renderer, Original = original, Iced = iced });
            renderer.sharedMaterials = iced;
        }
    }

    private void RestoreFrost()
    {
        foreach (var slot in _frost)
        {
            if (slot.Renderer != null)
            {
                var current = slot.Renderer.sharedMaterials;
                for (int i = 0; i < current.Length && i < slot.Iced.Length; i++)
                    if (current[i] == slot.Iced[i]) current[i] = slot.Original[i];
                slot.Renderer.sharedMaterials = current;
            }
            foreach (var material in slot.Iced) if (material != null) Destroy(material);
        }
        _frost.Clear(); _iceApplied = false;
        if (_block != null) { _block.Thaw(); _block = null; }
    }

    private void Clear()
    {
        _poisonUntil = _iceUntil = _netUntil = 0f;
        if (_net != null) { _net.Release(false); _net = null; }
        if (_cloud != null) { Destroy(_cloud.gameObject); _cloud = null; }
        RestoreFrost();
    }
    private void OnDisable() => Clear();
    private void OnDestroy() => Clear();
}
