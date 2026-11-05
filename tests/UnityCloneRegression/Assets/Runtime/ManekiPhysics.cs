using UnityEngine;

namespace IncreaseHardMod.Valuables;

internal static class ManekiPhysics
{
    internal static bool Attach(GameObject root,ValuableArtInstance art)
    {
        var data=ValuableVisuals.Load(art.assetKey).physics;
        if(data?.colliders==null||data.colliders.Length==0)return false;
        foreach(var shape in data.colliders)
        {
            var node=new GameObject("Porcelain collision "+shape.name);
            node.transform.SetParent(shape.moving?art.beckonPivot:root.transform,false);
            int layer=LayerMask.NameToLayer("PhysGrabObject");node.layer=layer>=0?layer:root.layer;
            node.AddComponent<PhysGrabObjectCollider>();GrabSurface.Mark(node);
            var mesh=new Mesh{name="Maneki support hull "+shape.name};
            mesh.vertices=shape.vertices;mesh.triangles=shape.triangles;mesh.RecalculateBounds();
            var collider=node.AddComponent<MeshCollider>();collider.sharedMesh=mesh;collider.convex=true;
        }
        return true;
    }
}
