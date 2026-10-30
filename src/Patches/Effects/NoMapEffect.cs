using HarmonyLib;
using IncreaseHardMod.Modifiers;

namespace IncreaseHardMod.Patches.Effects;

/// <summary>
/// NoMap modifier — the map tool is unavailable on the special level.
///
/// MapToolController.Update only opens the map while the Map input reads as
/// held/toggled: it checks InputManager.InputToggleGet(InputKey.Map) and
/// SemiFunc.InputHold(InputKey.Map) (which forwards to InputManager.KeyHold). If we
/// make both report "not pressed" for the Map key, the controller's open condition
/// is never met and Active stays false — no flicker, no forcing state, the map
/// simply never comes up. Input is polled locally, so this naturally only affects
/// the local player's own map, and since the modifier set is synced every client
/// loses the map together.
///
/// InputKey.Map is used solely for the map, so intercepting just that key is safe.
///
/// NOTE: the design's "until you find a map upgrade pickup" part needs a custom
/// world pickup (a real new asset), so this v1 disables the map for the whole
/// special level. The pickup can be layered on later without changing this gate.
/// </summary>
internal static class NoMapEffect
{
    private static bool Suppressed(InputKey key)
    {
        return key == InputKey.Map && ModifierManager.Current.Has(ModifierType.NoMap);
    }

    [HarmonyPatch(typeof(InputManager), "KeyHold")]
    private static class BlockHold
    {
        [HarmonyPostfix]
        private static void Postfix(InputKey key, ref bool __result)
        {
            if (Suppressed(key))
                __result = false;
        }
    }

    [HarmonyPatch(typeof(InputManager), "InputToggleGet", new[] { typeof(InputKey) })]
    private static class BlockToggle
    {
        [HarmonyPostfix]
        private static void Postfix(InputKey key, ref bool __result)
        {
            if (Suppressed(key))
                __result = false;
        }
    }

    [HarmonyPatch(typeof(InputManager), "InputToggleGet", new[] { typeof(InputKey), typeof(string) })]
    private static class BlockToggleScheme
    {
        [HarmonyPostfix]
        private static void Postfix(InputKey key, ref bool __result)
        {
            if (Suppressed(key))
                __result = false;
        }
    }
}
