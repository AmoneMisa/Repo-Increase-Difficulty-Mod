using UnityEngine;

namespace IncreaseHardMod.Hud;

/// <summary>Drives the HUD toolkit's shared pieces (vitals rows, notifications, player conditions) once per frame. Added in Plugin.</summary>
internal sealed class HudRuntime : MonoBehaviour
{
    private void LateUpdate()
    {
        HudVitals.Tick();
        HudNotify.Tick();
        HudConditions.Tick();
    }

    private void OnDestroy()
    {
        HudNotify.ClearAll();
        HudConditions.ClearAll();
    }
}
