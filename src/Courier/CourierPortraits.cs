using System.Collections.Generic;
using UnityEngine;

namespace IncreaseHardMod.Courier;

/// <summary>
/// Full-colour portraits of every courier animal for the menu and the gacha list (like the game's cosmetics list):
/// each animal's own model, in its natural colours, is posed far below the level and photographed once by a
/// temporary camera with its own light. The picture is taken twice, on black and on white, so the background can be
/// keyed out exactly (true transparency whatever the shaders write to alpha). One portrait per frame, then cached.
/// </summary>
internal static class CourierPortraits
{
    private const int Size = 192;
    private static readonly Texture2D[] Cache = new Texture2D[CourierRules.Animals];
    private static readonly Dictionary<int, Sprite> Sprites = new();
    private static int _lastFrame = -1;
    private static readonly Vector3 Studio = new(0f, -4000f, 0f);

    /// <summary>The animal's portrait, or null until it has been taken (call again next frame).</summary>
    internal static Sprite Get(int animal)
    {
        if (animal < 0 || animal >= CourierRules.Animals) return null;
        if (Sprites.TryGetValue(animal, out var sprite) && sprite != null) return sprite;
        if (Cache[animal] == null)
        {
            if (Time.frameCount == _lastFrame) return null; // one capture per frame keeps the menu smooth
            _lastFrame = Time.frameCount;
            try { Cache[animal] = Capture(animal); }
            catch (System.Exception e) { Plugin.Log.LogWarning($"[Courier] Portrait of {CourierLook.Voice(animal)} failed: {e.Message}"); Cache[animal] = Texture2D.blackTexture; }
            if (Cache[animal] == null) return null;
        }
        sprite = Sprite.Create(Cache[animal], new Rect(0, 0, Cache[animal].width, Cache[animal].height), new Vector2(.5f, .5f), 100f);
        sprite.name = "IHM courier portrait " + CourierLook.Voice(animal);
        Sprites[animal] = sprite;
        return sprite;
    }

    private static Texture2D Capture(int animal)
    {
        int look = CourierRules.PackLook(animal, CourierRules.DefaultSwatch, CourierRules.DefaultSwatch, CourierRules.DefaultSwatch);
        var visual = CourierVisual.Create(look);
        var pet = visual.gameObject;
        var rig = new GameObject("IHM courier portrait rig");
        RenderTexture target = null;
        try
        {
            pet.transform.SetPositionAndRotation(Studio, Quaternion.Euler(0f, 200f, 0f)); // a three-quarter view
            foreach (var danger in pet.GetComponentsInChildren<CourierDanger>(true)) danger.enabled = false;
            var bounds = new Bounds(Studio + Vector3.up * .35f, Vector3.one * .1f);
            foreach (var r in pet.GetComponentsInChildren<Renderer>())
                if (r.enabled && r.gameObject.activeInHierarchy) bounds.Encapsulate(r.bounds);
            float radius = Mathf.Max(.2f, bounds.extents.magnitude);

            var cam = rig.AddComponent<Camera>();
            cam.enabled = false;
            cam.fieldOfView = 26f;
            cam.nearClipPlane = .05f; cam.farClipPlane = radius * 12f + 5f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            var direction = new Vector3(0f, .28f, 1f).normalized;
            cam.transform.position = bounds.center + direction * (radius / Mathf.Tan(cam.fieldOfView * .5f * Mathf.Deg2Rad) * 1.05f);
            cam.transform.LookAt(bounds.center);
            var key = new GameObject("Key").AddComponent<Light>();
            key.transform.SetParent(rig.transform, false);
            key.transform.position = cam.transform.position + cam.transform.right * .8f + Vector3.up * .8f;
            key.type = LightType.Point; key.range = radius * 10f + 4f; key.intensity = 2.4f; key.shadows = LightShadows.None;
            var fill = new GameObject("Fill").AddComponent<Light>();
            fill.transform.SetParent(rig.transform, false);
            fill.transform.position = cam.transform.position - cam.transform.right * 1f;
            fill.type = LightType.Point; fill.range = radius * 10f + 4f; fill.intensity = 1.1f; fill.color = new Color(.85f, .9f, 1f); fill.shadows = LightShadows.None;

            target = RenderTexture.GetTemporary(Size, Size, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = target;
            bool fog = RenderSettings.fog;
            RenderSettings.fog = false;
            var black = Shot(cam, target, Color.black);
            var white = Shot(cam, target, Color.white);
            RenderSettings.fog = fog;

            var pixels = new Color32[Size * Size];
            for (int i = 0; i < pixels.Length; i++)
            {
                // On black a pixel shows c*a, on white c*a + (1-a): the difference gives alpha, black/alpha the colour.
                float a = 1f - ((white[i].r - black[i].r) + (white[i].g - black[i].g) + (white[i].b - black[i].b)) / (3f * 255f);
                a = Mathf.Clamp01(a);
                if (a < .02f) { pixels[i] = new Color32(0, 0, 0, 0); continue; }
                pixels[i] = new Color32((byte)Mathf.Min(255f, black[i].r / a), (byte)Mathf.Min(255f, black[i].g / a), (byte)Mathf.Min(255f, black[i].b / a), (byte)(a * 255f));
            }
            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
            {
                name = "IHM courier portrait " + CourierLook.Voice(animal), filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
            };
            texture.SetPixels32(pixels);
            texture.Apply();
            return texture;
        }
        finally
        {
            if (target != null) RenderTexture.ReleaseTemporary(target);
            Object.DestroyImmediate(rig);
            Object.DestroyImmediate(pet);
        }
    }

    private static Color32[] Shot(Camera cam, RenderTexture target, Color background)
    {
        cam.backgroundColor = background;
        cam.Render();
        var previous = RenderTexture.active;
        RenderTexture.active = target;
        var read = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
        read.ReadPixels(new Rect(0, 0, Size, Size), 0, 0);
        read.Apply();
        RenderTexture.active = previous;
        var pixels = read.GetPixels32();
        Object.DestroyImmediate(read);
        return pixels;
    }
}
