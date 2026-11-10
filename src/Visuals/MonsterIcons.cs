using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace IncreaseHardMod.Visuals;

/// <summary>
/// A unique icon for every kind of monster, made from the monster itself: the first time one is seen, its own meshes
/// are drawn from a three-quarter view into a small mask and turned into a white silhouette with a dark outline.
/// The white takes the colour of whatever shows it (red ping callout, purple map dot). Prepared silhouettes are
/// available immediately, with runtime capture for other monsters. One icon per name (EnemyParent.enemyName).
/// </summary>
internal static class MonsterIcons
{
    internal const int Size = 128, Pad = 10, Outline = 3;

    private sealed class Entry { public Texture2D Texture; public readonly Dictionary<int, Sprite> Sprites = new(); public int Tries; public float RetryAt; }
    private static readonly Dictionary<string, Entry> Cache = new();
    private static Material _mask;
    private static RenderTexture _target;

    internal static string Key(EnemyParent enemy) => enemy == null ? null : string.IsNullOrEmpty(enemy.enemyName) ? enemy.name : enemy.enemyName;

    /// <summary>The monster's icon (white silhouette, dark outline, transparent background), or null if it can't be
    /// drawn yet (not spawned, invisible this moment). Captures it the first time; later calls are a lookup.</summary>
    internal static Texture2D Get(EnemyParent enemy)
    {
        string key = Key(enemy);
        if (key == null) return null;
        LoadPrepared();
        if (!Cache.TryGetValue(key, out var entry)) Cache[key] = entry = new Entry();
        if (entry.Texture != null) return entry.Texture;
        // No give-up limit: a monster that is despawned, invisible (Hidden) or culled out of view simply can't be drawn
        // yet. The old cap of 4 tries left such monsters without an icon for the whole session. Back off instead.
        if (Time.unscaledTime < entry.RetryAt) return null;
        entry.Tries++;
        entry.RetryAt = Time.unscaledTime + Mathf.Min(1f + entry.Tries, 10f);
        try { entry.Texture = Capture(enemy); }
        catch (System.Exception e) { Plugin.Log.LogWarning($"[MonsterIcons] {key}: icon capture failed: {e.Message}"); }
        if (entry.Texture != null) { Plugin.Log.LogInfo($"[MonsterIcons] Made the icon for {key}."); Save(key, entry.Texture); }
        else if (entry.Tries == 5) Plugin.Log.LogInfo($"[MonsterIcons] {key}: no drawable shape yet (spawned={enemy.Spawned}); still trying.");
        return entry.Texture;
    }

    private const string ResourcePrefix = "IHM.MonsterIcon.";
    private static bool _prepared;

    internal static Texture2D GetPrepared(string key)
    {
        if (string.IsNullOrEmpty(key)) return null;
        LoadPrepared();
        return Cache.TryGetValue(key, out var entry) ? entry.Texture : null;
    }

    /// <summary>
    /// Ready-made icons, so a monster has its icon from the very first ping even while it is hidden or out of view:
    /// the set shipped inside the mod (src/MonsterIcons) and every icon this PC captured before (BepInEx/IHM_MonsterIcons,
    /// which Save writes). Runtime capture stays as the fallback for monsters in neither, e.g. other mods' monsters.
    /// </summary>
    private static void LoadPrepared()
    {
        if (_prepared) return;
        _prepared = true;
        int shipped = 0, local = 0;
        try
        {
            var asm = typeof(MonsterIcons).Assembly;
            foreach (string resource in asm.GetManifestResourceNames())
            {
                if (!resource.StartsWith(ResourcePrefix) || !resource.EndsWith(".png")) continue;
                using var stream = asm.GetManifestResourceStream(resource);
                if (stream == null) continue;
                using var ms = new System.IO.MemoryStream();
                stream.CopyTo(ms);
                string key = resource.Substring(ResourcePrefix.Length, resource.Length - ResourcePrefix.Length - 4);
                if (Add(key, ms.ToArray())) shipped++;
            }
        }
        catch (System.Exception e) { Plugin.Log.LogWarning($"[MonsterIcons] Couldn't load the shipped icons: {e.Message}"); }
        try
        {
            string folder = System.IO.Path.Combine(BepInEx.Paths.BepInExRootPath, "IHM_MonsterIcons");
            if (System.IO.Directory.Exists(folder))
                foreach (string file in System.IO.Directory.GetFiles(folder, "*.png"))
                {
                    string key = System.IO.Path.GetFileNameWithoutExtension(file);
                    if (Cache.ContainsKey(key)) continue; // shipped set first
                    if (Add(key, System.IO.File.ReadAllBytes(file))) local++;
                }
        }
        catch (System.Exception e) { Plugin.Log.LogWarning($"[MonsterIcons] Couldn't load saved icons: {e.Message}"); }
        Plugin.Log.LogInfo($"[MonsterIcons] Prepared icons: {shipped} shipped, {local} saved on this PC.");
    }

    private static bool Add(string key, byte[] png)
    {
        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, true)
            { name = "IHM Monster Icon " + key, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.HideAndDontSave };
        if (!texture.LoadImage(png)) { Object.Destroy(texture); return false; }
        Cache[key] = new Entry { Texture = texture };
        return true;
    }

    /// <summary>The icon as a map sprite, sized <paramref name="worldSize"/> units across on the map.</summary>
    internal static Sprite MapSprite(EnemyParent enemy, float worldSize)
    {
        var texture = Get(enemy);
        if (texture == null || worldSize <= 0f) return null;
        var entry = Cache[Key(enemy)];
        int size = Mathf.RoundToInt(worldSize * 10000f); // one sprite per size asked for (map dot, map ping)
        if (!entry.Sprites.TryGetValue(size, out var sprite) || sprite == null)
        {
            sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(.5f, .5f), texture.width / worldSize);
            sprite.name = "IHM Monster " + Key(enemy);
            entry.Sprites[size] = sprite;
        }
        return sprite;
    }

    private static Texture2D Capture(EnemyParent enemy)
    {
        if (enemy == null || !enemy.Spawned || enemy.Enemy == null) return null;
        var renderers = new List<Renderer>();
        bool found = false;
        Bounds bounds = default;
        foreach (var r in enemy.GetComponentsInChildren<Renderer>())
        {
            if (r == null || !r.enabled || !r.gameObject.activeInHierarchy || r.forceRenderingOff) continue;
            if (!(r is MeshRenderer || r is SkinnedMeshRenderer) || Decoration.Is(r)) continue;
            var b = r.bounds;
            if (b.size.magnitude > 12f) continue; // a stray huge helper volume would shrink the monster to a dot
            renderers.Add(r);
            if (!found) { bounds = b; found = true; } else bounds.Encapsulate(b);
        }
        if (!found) return null;

        if (_mask == null)
        {
            var shader = Shader.Find("Unlit/Color") ?? Shader.Find("Standard");
            if (shader == null) return null;
            _mask = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            _mask.SetColor("_Color", Color.white);
            if (_mask.HasProperty("_EmissionColor")) { _mask.SetColor("_EmissionColor", Color.white); _mask.EnableKeyword("_EMISSION"); }
        }
        if (_target == null)
        {
            _target = new RenderTexture(Size, Size, 24, RenderTextureFormat.ARGB32) { name = "IHM Monster Icon", filterMode = FilterMode.Point };
            _target.Create();
        }

        // Three-quarter view from the monster's front right, slightly from above: reads better than straight on.
        var t = enemy.Enemy.HasRigidbody && enemy.Enemy.Rigidbody != null ? enemy.Enemy.Rigidbody.transform : enemy.Enemy.transform;
        var forward = Vector3.ProjectOnPlane(t.forward, Vector3.up);
        if (forward.sqrMagnitude < .001f) forward = Vector3.forward;
        forward.Normalize();
        var right = Vector3.Cross(Vector3.up, forward);
        var direction = (forward + right * .8f + Vector3.up * .35f).normalized;
        float radius = bounds.extents.magnitude;
        var eye = bounds.center + direction * (radius * 3f + 1f);
        var view = Matrix4x4.Scale(new Vector3(1f, 1f, -1f)) * Matrix4x4.TRS(eye, Quaternion.LookRotation(-direction, Vector3.up), Vector3.one).inverse;
        float half = radius * 1.05f;
        var projection = Matrix4x4.Ortho(-half, half, -half, half, .01f, radius * 6f + 2f);

        var commands = new CommandBuffer { name = "IHM monster icon" };
        commands.SetRenderTarget(_target);
        commands.SetViewport(new Rect(0, 0, Size, Size));
        commands.ClearRenderTarget(true, true, Color.clear);
        commands.SetViewProjectionMatrices(view, GL.GetGPUProjectionMatrix(projection, false));
        foreach (var r in renderers)
            for (int sub = 0; sub < r.sharedMaterials.Length; sub++) commands.DrawRenderer(r, _mask, sub, 0);
        Graphics.ExecuteCommandBuffer(commands);
        commands.Release();

        var read = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
        var previous = RenderTexture.active;
        RenderTexture.active = _target;
        read.ReadPixels(new Rect(0, 0, Size, Size), 0, 0);
        read.Apply();
        RenderTexture.active = previous;
        var pixels = read.GetPixels32();
        Object.Destroy(read);
        return Stylise(pixels);
    }

    // Mask -> icon: crop to the silhouette, centre it with padding, white inside, a dark outline around it.
    internal static Texture2D Stylise(Color32[] pixels)
    {
        int minX = Size, minY = Size, maxX = -1, maxY = -1, filled = 0;
        for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
                if (pixels[y * Size + x].r > 127)
                {
                    filled++;
                    if (x < minX) minX = x; if (x > maxX) maxX = x;
                    if (y < minY) minY = y; if (y > maxY) maxY = y;
                }
        if (filled < 40) return null; // nothing visible from here (an invisible or tucked-away monster): try again later

        int w = maxX - minX + 1, h = maxY - minY + 1;
        float scale = (Size - 2f * (Pad + Outline)) / Mathf.Max(w, h);
        var mask = new bool[Size * Size];
        for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                // Sample the source silhouette, scaled to fill the icon and centred.
                float sx = minX + (x - Size * .5f) / scale + w * .5f, sy = minY + (y - Size * .5f) / scale + h * .5f;
                int ix = Mathf.FloorToInt(sx), iy = Mathf.FloorToInt(sy);
                mask[y * Size + x] = ix >= minX && ix <= maxX && iy >= minY && iy <= maxY && pixels[iy * Size + ix].r > 127;
            }

        var output = new Color32[Size * Size];
        for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                int i = y * Size + x;
                if (mask[i])
                {
                    // A faint inner edge so the shape keeps some depth once tinted.
                    bool edge = !At(mask, x - 2, y) || !At(mask, x + 2, y) || !At(mask, x, y - 2) || !At(mask, x, y + 2);
                    byte v = edge ? (byte)255 : (byte)225;
                    output[i] = new Color32(v, v, v, 255);
                    continue;
                }
                float nearest = 99f;
                for (int dy = -Outline; dy <= Outline; dy++)
                    for (int dx = -Outline; dx <= Outline; dx++)
                        if (At(mask, x + dx, y + dy)) nearest = Mathf.Min(nearest, Mathf.Sqrt(dx * dx + dy * dy));
                if (nearest <= Outline)
                    output[i] = new Color32(10, 8, 12, (byte)(230 * Mathf.Clamp01(Outline + .5f - nearest)));
            }

        var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, true)
            { name = "IHM Monster Icon", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.HideAndDontSave };
        texture.SetPixels32(output);
        texture.Apply(true, false);
        return texture;
    }

    // A copy of each icon as a PNG in BepInEx/IHM_MonsterIcons, to look them over outside the game.
    private static void Save(string key, Texture2D texture)
    {
        try
        {
            string folder = System.IO.Path.Combine(BepInEx.Paths.BepInExRootPath, "IHM_MonsterIcons");
            System.IO.Directory.CreateDirectory(folder);
            string file = key;
            foreach (char c in System.IO.Path.GetInvalidFileNameChars()) file = file.Replace(c, '_');
            System.IO.File.WriteAllBytes(System.IO.Path.Combine(folder, file + ".png"), texture.EncodeToPNG());
        }
        catch (System.Exception e) { Plugin.Log.LogWarning($"[MonsterIcons] Couldn't save the {key} icon: {e.Message}"); }
    }

    private static bool At(bool[] mask, int x, int y) => x >= 0 && y >= 0 && x < Size && y < Size && mask[y * Size + x];
}
