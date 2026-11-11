using IncreaseHardMod.Hud;
using UnityEngine;

namespace IncreaseHardMod.Monsters;

/// <summary>
/// Dodge Roll perk (local player): Left Alt (configurable) with A or D dashes sideways. Cooldown 22/18/14 s by
/// level. C is the courier menu, G emotes and I item info, so Alt does not collide with the mod's keys.
/// No dash while text input is open, while held/levitated/constricted/petrified/dragged, or while tumbling.
/// </summary>
internal static class DodgeRoll
{
    private static float _ready;

    internal static string KeyName => ModConfig.DodgeKey != null ? ModConfig.DodgeKey.Value.ToString() : "LeftAlt";

    internal static void Tick()
    {
        var controller = PlayerController.instance;
        var me = controller != null ? controller.playerAvatarScript : null;
        if (me == null || !SemiFunc.RunIsLevel() || me.isDisabled || me.deadSet || me.isTumbling || !SemiFunc.NoTextInputsActive()) return;
        if (!Input.GetKeyDown(ModConfig.DodgeKey.Value)) return;
        int level = Skills.SkillEffects.Level(me, Skills.SkillCatalog.DodgeRoll);
        if (level <= 0) return;
        bool ru = Localization.GameLanguage.Russian;
        if (MonsterAfflictions.LocalActive(Affliction.Electrified) || MonsterAfflictions.LocalActive(Affliction.Constricted) ||
            MonsterAfflictions.LocalActive(Affliction.Petrified) || MonsterAfflictions.LocalActive(Affliction.Dragged)) return;
        if (Time.time < _ready)
        {
            HudNotify.Toast((ru ? "РЫВОК: ЕЩЁ " : "DODGE: ") + Mathf.CeilToInt(_ready - Time.time) + (ru ? " С" : " S"), HudStyle.Caution, 1.2f, "ihm.dodge");
            return;
        }
        float side = Input.GetKey(KeyCode.A) ? -1f : 1f;
        var camera = Camera.main != null ? Camera.main.transform : controller.transform;
        var right = Vector3.Cross(Vector3.up, Vector3.ProjectOnPlane(camera.forward, Vector3.up).normalized);
        if (right.sqrMagnitude < .01f) right = controller.transform.right;
        controller.ForceImpulse(right.normalized * side * 7.5f + Vector3.up * 1.5f);
        _ready = Time.time + MonsterRules.DodgeCooldown(level);
        HudNotify.Toast(ru ? "РЫВОК" : "DODGE", HudStyle.Info, .8f, "ihm.dodge");
    }
}
