using UnityEngine;

namespace IncreaseHardMod.Patches.Effects;

/// <summary>
/// Drives the ping system each frame: reads the ping key, advances ping lifetimes, and clears pings
/// on level change so stale markers never carry over. The markers themselves are now the game's OWN
/// native highlight (<see cref="ValuableDiscoverGraphic"/>, spawned by <see cref="PingController"/>),
/// which renders, tracks its target and fades entirely on its own — so this component no longer draws
/// anything in OnGUI.
/// </summary>
internal sealed class PingHud : MonoBehaviour
{
    private Level _lastLevel;

    private void Update()
    {
        if (!ModConfig.PingEnabled.Value)
            return;

        if (SafeMenuLevel())
            return;

        // Drop all pings when the level changes.
        var run = RunManager.instance;
        if (run != null && run.levelCurrent != _lastLevel)
        {
            _lastLevel = run.levelCurrent;
            PingController.Clear();
        }

        PingController.Tick();

        if (SemiFunc.NoTextInputsActive() && Input.GetKeyDown(ModConfig.PingKey.Value))
            PingController.PingFromAim();
    }

    private static bool SafeMenuLevel()
    {
        try
        {
            return SemiFunc.MenuLevel();
        }
        catch
        {
            return false;
        }
    }
}
