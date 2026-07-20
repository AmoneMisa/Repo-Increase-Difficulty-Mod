using UnityEngine;

namespace IncreaseHardMod.Patches.Effects;

/// <summary>
/// "Spectator Minimap" — while the LOCAL player is dead/spectating on a level, HOLDING the minimap
/// key (default Tab) draws the game's own map render in the CENTRE of the screen so you can watch the
/// level layout without opening the full map tool.
///
/// The map is drawn by a dedicated camera that lives under <see cref="Map"/>.ActiveParent and only
/// renders while the map is <see cref="Map.Active"/> (the game normally toggles that on/off as the
/// map tool opens/closes). So this component keeps the map active the whole time you're dead (so its
/// texture stays warm and appears instantly when you press Tab), grabs that camera's
/// <see cref="Camera.targetTexture"/>, and blits it centre-screen with IMGUI. That reuses ALL of the
/// game's map rendering (walls, valuables, the player dot), so it matches the in-game map exactly; we
/// only place the resulting texture on screen.
///
/// Everything here is local/client-side (it reads the local avatar's disabled state and only toggles
/// the local map render), so it never affects other players and can't desync. It hides itself when
/// the full map tool is open (Dead Map Access) to avoid drawing the map twice.
/// </summary>
internal sealed class SpectatorMinimapHud : MonoBehaviour
{
    private Camera _mapCamera;
    private bool _forcedMapActive;

    private void Update()
    {
        if (!ModConfig.SpectatorMinimapEnabled.Value || !Spectating())
        {
            StopForcing();
            return;
        }

        // Keep the map's render pipeline alive the whole time we're dead so its camera produces a
        // live texture that's ready the instant the key is pressed. ActiveSet just SetActive()s
        // ActiveParent, so this is idempotent.
        var map = Map.Instance;
        if (map != null && !map.Active)
        {
            map.ActiveSet(true);
            _forcedMapActive = true;
        }

        if (_mapCamera == null)
            _mapCamera = FindMapCamera(map);
    }

    private void OnGUI()
    {
        if (!ModConfig.SpectatorMinimapEnabled.Value || !Spectating())
            return;
        if (!Input.GetKey(ModConfig.SpectatorMinimapKey.Value)) // hold-to-show
            return;

        var tex = _mapCamera != null ? _mapCamera.targetTexture : null;
        if (tex == null)
            return;

        float size = Screen.height * ModConfig.SpectatorMinimapScale.Value;
        var rect = new Rect((Screen.width - size) / 2f, (Screen.height - size) / 2f, size, size);

        // Dark backing so the map reads against a bright level, then the map render on top.
        var prev = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.55f);
        GUI.DrawTexture(new Rect(rect.x - 3f, rect.y - 3f, rect.width + 6f, rect.height + 6f),
            Texture2D.whiteTexture);
        GUI.color = prev;
        GUI.DrawTexture(rect, tex, ScaleMode.ScaleToFit, alphaBlend: false);
    }

    // True for the local dead player, on a real level, when the full map tool isn't already open.
    private static bool Spectating()
    {
        if (SafeMenuLevel() || !SafeRunIsLevel())
            return false;

        var me = PlayerAvatar.instance;
        if (me == null || !me.isDisabled)
            return false;

        var tool = MapToolController.instance;
        return tool == null || !tool.Active; // don't double-draw over the full map
    }

    // The map camera renders the DirtFinderMap-layer geometry under ActiveParent to a RenderTexture.
    private static Camera FindMapCamera(Map map)
    {
        if (map == null || map.ActiveParent == null)
            return null;
        return map.ActiveParent.GetComponentInChildren<Camera>(true);
    }

    // Release the forced map-active state we took, unless the real map tool is now holding it open.
    private void StopForcing()
    {
        if (!_forcedMapActive)
            return;
        _forcedMapActive = false;

        var map = Map.Instance;
        if (map == null)
            return;

        var tool = MapToolController.instance;
        if (tool != null && tool.Active)
            return; // the tool wants the map — leave it on

        map.ActiveSet(false);
    }

    private static bool SafeMenuLevel()
    {
        try { return SemiFunc.MenuLevel(); }
        catch { return false; }
    }

    private static bool SafeRunIsLevel()
    {
        try { return SemiFunc.RunIsLevel(); }
        catch { return false; }
    }
}
