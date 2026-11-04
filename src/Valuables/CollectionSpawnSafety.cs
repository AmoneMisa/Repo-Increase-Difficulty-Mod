using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace IncreaseHardMod.Valuables;

// The native categories describe a slot, not the custom mesh. Never spawn an
// oversized reference model into furniture merely because its category matched.
[HarmonyPatch(typeof(ValuableDirector),"SpawnValuable")]
internal static class CollectionSpawnSafety
{
    // Instantiate receives the volume's rotation, not the prefab rotation. Apply
    // the same upright pose used by the clearance query before native Start runs.
    [HarmonyPostfix]
    private static void Postfix(ValuableObject __result)
    {
        if(__result==null||!SemiFunc.IsMasterClientOrSingleplayer())return;
        var art=__result.GetComponent<ValuableArtInstance>();
        if(art==null||(art.assetKey!="cannon"&&art.assetKey!="skeleton"&&art.assetKey!="robot_vacuum"))return;
        WheeledPropPhysics.AlignAtSpawn(__result.GetComponent<Rigidbody>());
    }
    [HarmonyPrefix]
    private static bool Prefix(ValuableDirector __instance,ref PrefabRef _valuable,ValuableVolume _volume,ref ValuableObject __result)
    {
        var prefab=_valuable?.Prefab;var art=prefab!=null?prefab.GetComponent<ValuableArtInstance>():null;
        if(art==null||_volume==null)return true;
        var bounds=ValuableLayout.MeshBounds(prefab.transform,art.visualRoot);
        var rotation=_volume.transform.rotation;
        if(art.assetKey=="cannon"||art.assetKey=="skeleton"||art.assetKey=="robot_vacuum")rotation=WheeledPropPhysics.Upright(rotation);
        Vector3 center=_volume.transform.position+rotation*Vector3.Scale(bounds.center,prefab.transform.localScale);
        Vector3 half=Vector3.Scale(bounds.extents,prefab.transform.localScale)+Vector3.one*.006f;
        Physics.SyncTransforms();
        var contacts=Physics.OverlapBox(center,half,rotation,LayerMask.GetMask("Default")|SemiFunc.LayerMaskGetPhysGrabObject(),QueryTriggerInteraction.Ignore);
        if(!contacts.Any(c=>c!=null&&!c.transform.IsChildOf(prefab.transform)))return true;
        // Keep native value accounting/loading intact by filling this slot with an
        // ordinary valuable from the same map and size pool instead of wedging ours.
        var pools=new[]{__instance.tinyValuables,__instance.smallValuables,__instance.mediumValuables,__instance.bigValuables,__instance.wideValuables,__instance.tallValuables,__instance.veryTallValuables};
        var fallback=pools[(int)_volume.VolumeType].FirstOrDefault(p=>p?.Prefab!=null&&p.Prefab.GetComponent<ValuableArtInstance>()==null);
        Plugin.Log.LogInfo("[Valuables] Occupied full footprint; skipped custom spawn: "+art.assetKey);
        if(fallback!=null){_valuable=fallback;return true;}
        __result=null;return false;
    }
}
