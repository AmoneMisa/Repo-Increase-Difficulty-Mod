using HarmonyLib;

namespace IncreaseHardMod.Quests;

/// <summary>
/// Host: a valuable is destroyed by damage (the game calls DestroyObject when it breaks apart). Extraction removes
/// loot through a different path (DestroyPhysGrabObject), so extracted loot never counts here.
/// Feeds the Oops quest and fails the Crown Jewel quest when the jewel breaks.
/// </summary>
[HarmonyPatch(typeof(PhysGrabObjectImpactDetector), nameof(PhysGrabObjectImpactDetector.DestroyObject))]
internal static class QuestValuableBroken
{
    [HarmonyPrefix]
    private static void Prefix(PhysGrabObjectImpactDetector __instance, bool forced)
    {
        if (!__instance.isValuable || (__instance.destroyDisable && !forced) || !SemiFunc.IsMasterClientOrSingleplayer()) return;
        var grab = __instance.physGrabObject;
        if (grab == null || grab.dead) return;
        var valuable = __instance.GetComponent<ValuableObject>();
        if (valuable != null && valuable.GetComponent<EnemyValuable>() == null) QuestManager.OnValuableDestroyed(valuable);
    }
}
