using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Object = UnityEngine.Object;

namespace IncreaseHardMod.Gear;

/// <summary>
/// The red "grab here" hand-in-a-diamond plate that vanilla C.A.R.T.s show where players hold them.
/// Drawn at runtime (no shipped texture), unlit and double-sided so it reads in the dark like the native one.
/// </summary>
internal static class GrabMarker
{
    private static Material _material;
    private const int Size = 128;

    /// <summary>Logs donor renderers that look like the native grab plate, so a future build can reuse it.</summary>
    internal static void LogNativeCandidates(GameObject donor)
    {
        var found = new StringBuilder();
        foreach (var r in donor.GetComponentsInChildren<Renderer>(true))
        {
            string text = r.name;
            foreach (var m in r.sharedMaterials)
                if (m != null) text += " | " + m.name + (m.mainTexture != null ? " (" + m.mainTexture.name + ")" : "");
            if (text.IndexOf("grab", StringComparison.OrdinalIgnoreCase) >= 0 ||
                text.IndexOf("hand", StringComparison.OrdinalIgnoreCase) >= 0 ||
                text.IndexOf("arrow", StringComparison.OrdinalIgnoreCase) >= 0)
                found.Append(found.Length > 0 ? "; " : "").Append(text);
        }
        if (found.Length > 0) Plugin.Log.LogInfo("[Flame] Native grab-plate candidates: " + found);
    }

    /// <summary>Puts the plate on the back (-Z) face of <paramref name="box"/>: the push handle, nearest the player.</summary>
    internal static void AttachToBoxBack(Transform box, int layer)
    {
        if (!LocalBounds(box, out var bounds))
        {
            Plugin.Log.LogWarning("[Flame] Storage box has no mesh bounds; grab marker skipped.");
            return;
        }
        float side = Mathf.Min(bounds.size.x, bounds.size.y) * .32f;
        var plate = GameObject.CreatePrimitive(PrimitiveType.Quad);
        plate.name = "IHM Grab Marker";
        plate.layer = layer;
        Object.DestroyImmediate(plate.GetComponent<Collider>()); // visual only; the box colliders take the grab
        plate.transform.SetParent(box, false);
        plate.transform.localPosition = new Vector3(
            bounds.center.x,
            bounds.center.y + bounds.extents.y * .3f,
            bounds.min.z - .004f);
        // Quad faces -Z by default, i.e. out of the back face toward the pushing player.
        plate.transform.localRotation = Quaternion.identity;
        plate.transform.localScale = new Vector3(side, side, 1f);
        var renderer = plate.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = Material();
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
    }

    private static bool LocalBounds(Transform root, out Bounds bounds)
    {
        bounds = default;
        bool any = false;
        var toRoot = root.worldToLocalMatrix;
        foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
        {
            var mesh = filter.sharedMesh;
            if (mesh == null || filter.GetComponent<Renderer>() == null) continue;
            var b = mesh.bounds;
            var m = toRoot * filter.transform.localToWorldMatrix;
            for (int i = 0; i < 8; i++)
            {
                var corner = m.MultiplyPoint3x4(b.center + Vector3.Scale(b.extents,
                    new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1)));
                if (!any) { bounds = new Bounds(corner, Vector3.zero); any = true; }
                else bounds.Encapsulate(corner);
            }
        }
        return any && bounds.size.x > .01f && bounds.size.y > .01f;
    }

    private static Material Material()
    {
        if (_material != null) return _material;
        // Sprites/Default: unlit, alpha-blended, Cull Off, so it glows in the dark and shows from either side.
        _material = new Material(Shader.Find("Sprites/Default"))
        {
            name = "IHM Grab Marker",
            mainTexture = Texture(),
            renderQueue = 3000,
            hideFlags = HideFlags.HideAndDontSave,
        };
        return _material;
    }

    private static Texture2D Texture()
    {
        var red = new Color(1f, .12f, .08f, 1f);
        var fill = new Color(.25f, .02f, .02f, .55f);
        var pixels = new Color[Size * Size];
        const float c = (Size - 1) / 2f, outer = 60f, inner = 53f;
        for (int y = 0; y < Size; y++)
        for (int x = 0; x < Size; x++)
        {
            float d = Mathf.Abs(x - c) + Mathf.Abs(y - c); // diamond distance
            Color p = Color.clear;
            if (d <= outer) p = d >= inner ? red : fill;
            if (d < inner - 2 && Hand(x, y)) p = red;
            pixels[y * Size + x] = p;
        }
        var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
        {
            name = "IHM Grab Marker",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave,
        };
        texture.SetPixels(pixels);
        texture.Apply(false, true);
        return texture;
    }

    // Open hand, palm toward the viewer, fingers up (y grows upward in texture space).
    private static readonly List<(float x, float top)> Fingers = new() { (50f, 88f), (60f, 94f), (70f, 92f), (80f, 84f) };

    private static bool Hand(int x, int y)
    {
        // Palm: rounded block.
        if (Capsule(x, y, new Vector2(64f, 44f), new Vector2(64f, 60f), 17f)) return true;
        foreach (var (fx, top) in Fingers)
            if (Capsule(x, y, new Vector2(fx, 58f), new Vector2(fx, top), 4.2f)) return true;
        // Thumb angled out to the left.
        return Capsule(x, y, new Vector2(50f, 50f), new Vector2(36f, 66f), 4.6f);
    }

    private static bool Capsule(int x, int y, Vector2 a, Vector2 b, float radius)
    {
        var p = new Vector2(x, y);
        var ab = b - a;
        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
        return (p - (a + ab * t)).sqrMagnitude <= radius * radius;
    }
}
