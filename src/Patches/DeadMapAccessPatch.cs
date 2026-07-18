using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;

namespace IncreaseHardMod.Patches;

/// <summary>
/// "Dead Map Access" — lets a dead/spectating player still open their map tool, reusing all of
/// the game's own map rendering.
///
/// <see cref="MapToolController.Update"/> only processes the map input when the owning avatar is
/// alive, via the leading <c>!PlayerAvatar.isDisabled</c> term of its open-gate. The transpiler
/// swaps that single <c>isDisabled</c> field read for <see cref="MapDisabled"/>, which reports the
/// real value for everyone EXCEPT the local dead player on a level — for whom it reports "not
/// disabled" so the map can be toggled. Nothing else in the method reads that field, so the change
/// is surgical.
///
/// With the gate open, the controller runs its normal open animation, but it anchors the tool to
/// the local player's hand transform (which is on the dead body, off-screen while spectating). The
/// postfix therefore re-anchors the tool holder to the active view camera each frame so the map is
/// actually visible. All of this is local/client-side (input is polled locally, the tool is the
/// player's own), so it never affects other players and can't desync.
/// </summary>
[HarmonyPatch(typeof(MapToolController), "Update")]
internal static class DeadMapAccessPatch
{
    private static readonly FieldInfo IsDisabledField =
        AccessTools.Field(typeof(PlayerAvatar), nameof(PlayerAvatar.isDisabled));

    private static readonly MethodInfo Replacement =
        AccessTools.Method(typeof(DeadMapAccessPatch), nameof(MapDisabled));

    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        foreach (var ins in instructions)
        {
            // Replace `ldfld PlayerAvatar::isDisabled` with `call MapDisabled(PlayerAvatar)`.
            // Both consume the PlayerAvatar reference on the stack and push a bool, so the stack
            // shape is unchanged.
            if (ins.opcode == OpCodes.Ldfld && ins.operand is FieldInfo f && f == IsDisabledField)
                yield return new CodeInstruction(OpCodes.Call, Replacement);
            else
                yield return ins;
        }
    }

    // The effective "is this avatar disabled?" for the map open-gate.
    private static bool MapDisabled(PlayerAvatar avatar)
    {
        if (avatar == null)
            return true;

        bool real = avatar.isDisabled;
        if (!real)
            return false; // alive — unchanged behaviour

        if (!ModConfig.DeadMapAccessEnabled.Value)
            return true;
        if (avatar != PlayerAvatar.instance)
            return true; // only relax for the LOCAL player's own tool
        if (SafeMenuLevel())
            return true;

        // Local dead player on a level: pretend not disabled so the map can be opened.
        return false;
    }

    [HarmonyPostfix]
    private static void Postfix(MapToolController __instance)
    {
        if (!ModConfig.DeadMapAccessEnabled.Value)
            return;
        if (__instance != MapToolController.instance) // local player's tool only
            return;

        var avatar = __instance.PlayerAvatar;
        if (avatar == null || !avatar.isDisabled) // only while dead
            return;
        if (!__instance.Active)
            return;

        var cam = Camera.main;
        if (cam == null)
            return;

        // The controller just anchored the tool at the (off-screen) dead body's hand transform;
        // move the holder to the current view camera so the map renders in front of the spectator.
        var holder = __instance.transform.parent;
        if (holder == null)
            return;

        holder.position = cam.transform.position;
        holder.rotation = cam.transform.rotation;
    }

    private static bool SafeMenuLevel()
    {
        try { return SemiFunc.MenuLevel(); }
        catch { return false; }
    }
}
