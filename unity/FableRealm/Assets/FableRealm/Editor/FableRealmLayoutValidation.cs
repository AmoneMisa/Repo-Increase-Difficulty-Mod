using System;
using UnityEngine;

namespace FableRealm.Editor
{
    public static partial class FableRealmBuilder
    {
        static void ValidateLayout(GameObject prefab,string room)
        {
            var instance=UnityEngine.Object.Instantiate(prefab,new Vector3(500,0,500),Quaternion.identity);
            try
            {
                Physics.SyncTransforms();
                foreach(var t in instance.GetComponentsInChildren<Transform>())
                {
                    if(t.name=="CHECK_PIT")
                    {
                        if(!Physics.Raycast(t.position+Vector3.up*.1f,Vector3.down,out var hit,6) || hit.point.y> -3.9f)
                            throw new InvalidOperationException(room+": pit is covered or has no bottom.");
                    }
                    if(t.name=="CHECK_BRIDGE" || t.name=="CHECK_RAMP" || t.name=="CHECK_UPPER")
                    {
                        if(!Physics.Raycast(t.position+Vector3.up*.3f,Vector3.down,out var hit,.7f) || Mathf.Abs(hit.point.y-t.position.y)>.15f)
                            throw new InvalidOperationException(room+": missing walkable surface at "+t.name);
                    }
                    if(t.name=="CHECK_HIDE")
                    {
                        if(!Physics.Raycast(t.position,t.forward,1.2f))
                            throw new InvalidOperationException(room+": hide has no opaque back collider.");
                        if(Physics.Raycast(t.position,-t.forward,1.3f))
                            throw new InvalidOperationException(room+": hide entrance is blocked.");
                    }
                    if(t.name=="CHECK_UNDER_TABLE" && Physics.CheckSphere(t.position,.35f))
                        throw new InvalidOperationException(room+": under-table space is obstructed.");
                    if(t.name.StartsWith("Portal_",StringComparison.Ordinal))
                    {
                        var inside=t.position-t.forward*.6f;
                        if(!Physics.Raycast(inside+Vector3.up*.2f,Vector3.down,.5f))
                            throw new InvalidOperationException(room+": portal has no ground.");
                        if(Physics.CheckCapsule(inside+Vector3.up*.5f,inside+Vector3.up*1.5f,.4f))
                            throw new InvalidOperationException(room+": portal is obstructed.");
                    }
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(instance); }
        }
    }
}
