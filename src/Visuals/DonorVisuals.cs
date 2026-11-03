using System.Collections.Generic;
using UnityEngine;

namespace IncreaseHardMod.Visuals;

/// <summary>Removes donor geometry in serialized state, so cloned prefabs cannot show it again.</summary>
internal static class DonorVisuals
{
    private static Mesh _empty;
    internal static void RemoveGeometry(IEnumerable<Renderer> donors)
    {
        if(_empty==null)_empty=new Mesh{name="IHM Empty Donor Geometry"};
        foreach(var renderer in donors)
        {
            if(renderer==null)continue;
            var filter=renderer.GetComponent<MeshFilter>();
            if(filter!=null)filter.sharedMesh=_empty;
            if(renderer is SkinnedMeshRenderer skinned)skinned.sharedMesh=_empty;
            // enabled/sharedMesh survive cloning. forceRenderingOff alone does not.
            // Keep components and transforms valid for native culling, grab and destruction scripts.
            renderer.enabled=false;renderer.forceRenderingOff=true;
        }
    }
}
