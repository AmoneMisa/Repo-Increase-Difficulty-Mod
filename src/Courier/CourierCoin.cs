using IncreaseHardMod.Visuals;
using UnityEngine;

namespace IncreaseHardMod.Courier;

/// <summary>A gacha coin lying on the level: a spinning, bobbing gold token with a warm glow (no colliders).</summary>
internal sealed class CourierCoin : MonoBehaviour
{
    private static Material _gold, _rim;
    private Vector3 _home;
    private float _collected = -1f, _nextChime;
    private Light _light;

    internal static CourierCoin Create(Vector3 at)
    {
        var go = new GameObject("IHM Gacha Coin");
        go.transform.position = at;
        var coin = go.AddComponent<CourierCoin>();
        coin._home = at;
        var template = ItemArt.WeaponMaterialDonor(null);
        if (template != null)
        {
            _gold ??= ItemArt.Paint(template, null, new Color(1f, .78f, .2f), "IHM Gacha Coin");
            _rim ??= ItemArt.Paint(template, null, new Color(.8f, .45f, .08f), "IHM Gacha Coin Rim");
        }
        Part(go.transform, PrimitiveType.Cylinder, Vector3.zero, new Vector3(.22f, .012f, .22f), _rim);
        Part(go.transform, PrimitiveType.Cylinder, Vector3.zero, new Vector3(.19f, .016f, .19f), _gold);
        // Capsule emblem on both faces
        foreach (float side in new[] { 1f, -1f })
        {
            // The coin faces along Z: a two-tone capsule (top and bottom half) pressed into each face.
            Part(go.transform, PrimitiveType.Sphere, new Vector3(0f, .025f, side * .01f), new Vector3(.08f, .012f, .07f), _rim);
            Part(go.transform, PrimitiveType.Sphere, new Vector3(0f, -.025f, side * .01f), new Vector3(.08f, .012f, .07f), _gold);
        }
        coin._light = new GameObject("Glow").AddComponent<Light>();
        coin._light.transform.SetParent(go.transform, false);
        coin._light.type = LightType.Point; coin._light.color = new Color(1f, .75f, .3f); coin._light.range = 2.5f; coin._light.intensity = 1.6f;
        return coin;
    }

    private static void Part(Transform parent, PrimitiveType type, Vector3 at, Vector3 size, Material material)
    {
        var p = GameObject.CreatePrimitive(type);
        Destroy(p.GetComponent<Collider>());
        p.transform.SetParent(parent, false);
        p.transform.localPosition = at; p.transform.localScale = size;
        p.transform.localRotation = Quaternion.Euler(90f, 0f, 0f); // stand the coin up on its edge
        if (material != null) p.GetComponent<MeshRenderer>().sharedMaterial = material;
    }

    internal void Collect()
    {
        if (_collected >= 0f) return;
        _collected = Time.time;
        try { ItemAudio.Cue("courier_find", .4f).Play(transform.position); } catch { }
    }

    private void Update()
    {
        float t = Time.time;
        if (_collected >= 0f)
        {
            float k = (t - _collected) / .4f;
            transform.position += Vector3.up * Time.deltaTime * 2f;
            transform.localScale = Vector3.one * Mathf.Max(0f, 1f + k * .6f) * (1f - k);
            if (_light != null) _light.intensity = Mathf.Max(0f, 1.6f * (1f - k));
            if (k >= 1f) Destroy(gameObject);
            return;
        }
        transform.position = _home + Vector3.up * Mathf.Sin(t * 2.2f) * .06f;
        transform.rotation = Quaternion.Euler(0f, t * 120f, 0f);
        if (_light != null) _light.intensity = 1.3f + Mathf.Sin(t * 4f) * .3f;
        // A soft chime now and then so a nearby coin can be heard.
        var me = PlayerAvatar.instance;
        if (me != null && t >= _nextChime && Vector3.Distance(me.transform.position, _home) < 8f)
        {
            _nextChime = t + 4f;
            try { ItemAudio.Cue("courier_find", .12f).Play(_home); } catch { }
        }
    }
}
