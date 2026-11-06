using IncreaseHardMod.Hud;
using IncreaseHardMod.Upgrades;
using UnityEngine;

namespace IncreaseHardMod.Patches.Effects;

/// <summary>
/// The Armor upgrade's vitals row under energy: blue shield icon, current damage reduction in percent and the
/// cap ("10 /20%"). Built on the HUD toolkit (<see cref="HudVitals"/>), which clones the native energy row so the
/// font, glow and size match. Shown only while the local player has at least one Armor level.
/// </summary>
internal static class ArmorHud
{
    internal static void Register() => HudVitals.Register(new VitalsRowSpec
    {
        Id = "armor",
        Order = 10,
        Icon = () => HudIcons.Shield,
        ValueColor = HudStyle.Armor,
        MaxColor = HudStyle.ArmorMax,
        Read = Read,
    });

    private static int _percent = -1, _cap = -1;
    private static VitalsReading _reading;

    private static VitalsReading Read()
    {
        var local = PlayerController.instance != null ? PlayerController.instance.playerAvatarScript : null;
        float reduction = local != null ? SurvivalUpgrades.ArmorReduction(local) : 0f;
        if (reduction <= 0f) return VitalsReading.Hidden;
        int percent = Mathf.RoundToInt(reduction * 100f), cap = Mathf.RoundToInt(SurvivalUpgrades.ArmorCap * 100f);
        // Rebuild the strings only when a number changes (Read runs every frame).
        if (percent != _percent || cap != _cap)
        {
            _percent = percent; _cap = cap;
            _reading = VitalsReading.Of(percent.ToString(), cap + "%");
        }
        return _reading;
    }
}
