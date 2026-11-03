using UnityEngine;

namespace IncreaseHardMod.Valuables;

internal static class ValuableLayout
{
    internal static float Scale(string key)=>key=="bomb"?.8f:key=="cannon"?.85f:
        key=="centrifuge"||key=="robot_vacuum"?1.1f:1f;
    internal static Bounds MeshBounds(Transform root,Transform visual)
    {
        bool first=true;var bounds=new Bounds();
        foreach(var filter in visual.GetComponentsInChildren<MeshFilter>(true))
        {
            var mesh=filter.sharedMesh;if(mesh==null||mesh.vertexCount==0)continue;
            var b=mesh.bounds;
            for(int i=0;i<8;i++)
            {
                var p=b.center+Vector3.Scale(b.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1));
                p=root.InverseTransformPoint(filter.transform.TransformPoint(p));
                if(first){bounds=new Bounds(p,Vector3.zero);first=false;}else bounds.Encapsulate(p);
            }
        }
        // Reserve the complete fore/aft paw sweep, including the moving collision.
        if(root.GetComponent<ValuableArtInstance>()?.assetKey=="maneki_neko")bounds.Expand(new Vector3(0,0,.18f));
        return bounds;
    }
    internal static Bounds Apply(GameObject go,ValuableArtInstance art)
    {
        var bounds=MeshBounds(go.transform,art.visualRoot);
        // Native volume: X is centred; Y starts at the floor; Z extends forward from the spawn.
        var offset=new Vector3(-bounds.center.x,-bounds.min.y+.012f,-bounds.min.z+.012f);
        art.visualRoot.localPosition+=offset;
        foreach(Transform child in go.transform)
            if(child!=art.visualRoot&&child.GetComponent<PhysGrabObjectCollider>()!=null)child.localPosition+=offset;
        bounds.center+=offset;
        var com=go.transform.Find("Center of Mass");
        if(com==null){var node=new GameObject("Center of Mass");node.transform.SetParent(go.transform,false);com=node.transform;}
        com.localPosition=bounds.center;go.GetComponent<Rigidbody>().centerOfMass=bounds.center;
        var force=go.transform.Find("Force Grab Point");if(force!=null)force.localPosition=bounds.center;
        var grab=go.GetComponent<PhysGrabObject>();
        grab.midPointOffset=bounds.center;grab.boundingBox=bounds.size*Scale(art.assetKey);
        return bounds;
    }
}
