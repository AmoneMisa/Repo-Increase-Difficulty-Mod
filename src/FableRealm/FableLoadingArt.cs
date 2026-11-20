using System;
using System.IO;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace IncreaseHardMod.FableRealm;

/// <summary>
/// Fable Realm's own level card: the three loading-screen layers the game slides in before the truck crashes
/// through the front one (a storybook castle, a fairy-tale forest, a sleeping moon). The level keeps the donor's
/// sprites in its data, so a missing resource falls back to them; this postfix swaps the pictures on the screen.
/// </summary>
internal static class FableLoadingArt
{
    private const string Prefix = "DeepRunOverhaul.FableRealm.fable_loading_0";
    private static Sprite[] _sprites;
    private static bool _loaded;

    // A postfix runs right after the game's method, which has just put the donor level's sprites on the three images.
    [HarmonyPatch(typeof(LoadingUI), nameof(LoadingUI.LevelAnimationStart))]
    private static class Swap
    {
        [HarmonyPostfix]
        private static void Postfix(LoadingUI __instance)
        {
            if (FableLevel.Level == null || LevelGenerator.Instance == null || LevelGenerator.Instance.Level != FableLevel.Level) return;
            var sprites = Sprites();
            if (sprites == null) return;
            Set(__instance.loadingGraphic01, sprites[0]);
            Set(__instance.loadingGraphic02, sprites[1]);
            Set(__instance.loadingGraphic03, sprites[2]);
        }
    }

    private static void Set(Image image, Sprite sprite)
    {
        if (image == null || sprite == null) return;
        image.sprite = sprite; // the colour stays the game's own tint
    }

    private static Sprite[] Sprites()
    {
        if (_loaded) return _sprites;
        _loaded = true;
        try
        {
            var sprites = new Sprite[3];
            var assembly = Assembly.GetExecutingAssembly();
            for (int i = 0; i < 3; i++)
            {
                using var stream = assembly.GetManifestResourceStream(Prefix + (i + 1) + ".png");
                if (stream == null) throw new FileNotFoundException("Missing loading layer " + (i + 1));
                using var memory = new MemoryStream();
                stream.CopyTo(memory);
                var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false) { name = "Fable Realm loading " + (i + 1), wrapMode = TextureWrapMode.Clamp };
                if (!texture.LoadImage(memory.ToArray(), true)) throw new InvalidDataException("Unreadable loading layer " + (i + 1));
                // Same size, pivot and scale as the vanilla level cards (2252 x 1128, centre, 100 px per unit).
                sprites[i] = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(.5f, .5f), 100f, 0, SpriteMeshType.FullRect);
                sprites[i].name = texture.name;
                UnityEngine.Object.DontDestroyOnLoad(texture);
            }
            _sprites = sprites;
        }
        catch (Exception error) { Plugin.Log.LogWarning("[Fable] Loading art unavailable, the donor's is shown: " + error.Message); }
        return _sprites;
    }
}
