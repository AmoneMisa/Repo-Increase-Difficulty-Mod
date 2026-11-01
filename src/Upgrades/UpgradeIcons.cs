using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace IncreaseHardMod.Upgrades;

/// <summary>
/// Loads custom upgrade icons from PNGs embedded in this DLL (see the EmbeddedResource entries in
/// the .csproj) and turns them into <see cref="Sprite"/>s that get assigned to an upgrade item's
/// <see cref="ItemAttributes.icon"/>.
///
/// Why this works: REPO's ItemAttributes only auto-generates/loads a cached icon when its
/// <c>icon</c> field is still null. By pre-assigning our own Sprite on the cloned upgrade prefab,
/// the game skips generation and shows our art in the shop + inventory. The emoji enum
/// (ItemAttributes.emojiIcon) is separate and only drives the held-item emoji, so it's untouched.
///
/// Resources are matched by filename suffix so MSBuild's resource-name mangling (dashes, folders)
/// doesn't matter. Loaded sprites are cached per file to avoid re-decoding.
/// </summary>
internal static class UpgradeIcons
{
    private static readonly Dictionary<string, Sprite> _spriteCache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, Texture2D> _texCache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Load an embedded PNG (by file name, e.g. "Upgrade_Ping.png") into a Sprite, or null if the
    /// resource is missing / can't be decoded. Cached after first load.
    /// </summary>
    public static Sprite Load(string fileName)
    {
        if (string.IsNullOrEmpty(fileName))
            return null;
        if (_spriteCache.TryGetValue(fileName, out var cached))
            return cached;

        var tex = LoadTexture(fileName);
        Sprite sprite = null;
        if (tex != null)
        {
            sprite = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
            sprite.name = fileName;
        }

        _spriteCache[fileName] = sprite; // cache even null so we don't retry a missing file
        return sprite;
    }

    /// <summary>
    /// Load an embedded PNG into a Texture2D (for IMGUI drawing), or null if missing/undecodable.
    /// Cached after first load.
    /// </summary>
    public static Texture2D LoadTexture(string fileName)
    {
        if (string.IsNullOrEmpty(fileName))
            return null;
        if (_texCache.TryGetValue(fileName, out var cached))
            return cached;

        Texture2D tex = null;
        try
        {
            var asm = Assembly.GetExecutingAssembly();
            string resource = FindResource(asm, fileName);
            if (resource == null)
            {
                Plugin.Log.LogWarning($"[Icons] Embedded texture '{fileName}' not found in assembly.");
            }
            else
            {
                using var stream = asm.GetManifestResourceStream(resource);
                if (stream != null)
                {
                    using var ms = new MemoryStream();
                    stream.CopyTo(ms);
                    var loaded = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    if (loaded.LoadImage(ms.ToArray()))
                    {
                        loaded.name = fileName;
                        tex = loaded;
                    }
                }
            }
        }
        catch (Exception e)
        {
            Plugin.Log.LogWarning($"[Icons] Failed to load embedded texture '{fileName}': {e.Message}");
        }

        _texCache[fileName] = tex; // cache even null so we don't retry a missing file
        return tex;
    }

    private static string FindResource(Assembly asm, string fileName)
    {
        // Match the complete resource filename: Icon_Upgrade_X.png is not Upgrade_X.png.
        foreach (var name in asm.GetManifestResourceNames())
        {
            if (name.EndsWith("." + fileName, StringComparison.OrdinalIgnoreCase))
                return name;
        }
        return null;
    }
}
