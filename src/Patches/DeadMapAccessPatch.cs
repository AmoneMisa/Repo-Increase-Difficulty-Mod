using UnityEngine;

namespace IncreaseHardMod.Patches;

/// <summary>Dead players use the spectator overlay; leave the native hand tool's death gate intact.</summary>
internal static class DeadMapAccessPatch
{
    internal static bool LocalDeadOnLevel()
    {
        var me = PlayerAvatar.instance;
        return me != null && me.isDisabled && !SemiFunc.MenuLevel() && SemiFunc.RunIsLevel();
    }

    internal static Transform ViewSource()
    {
        // The active view moves both when switching teammates and when flying as a head.
        var camera = GameDirector.instance != null ? GameDirector.instance.MainCamera : Camera.main;
        return camera != null ? camera.transform : null;
    }
}
