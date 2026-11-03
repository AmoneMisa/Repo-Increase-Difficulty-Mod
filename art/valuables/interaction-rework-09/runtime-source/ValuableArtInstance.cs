using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace IncreaseHardMod.Valuables;

internal sealed class ValuableArtInstance : MonoBehaviour
{
    public string assetKey;
    public Transform visualRoot, leftWheel, rightWheel, casePivot;
    public ValuablePartBinding[] bindings;
    private bool _ready;
    private bool _reported;
    private void Awake(){EnsureBindings();}
    internal bool EnsureBindings()
    {
        if(_ready)return true;
        // Rebind from THIS spawn's hierarchy, not transient references on the registration prefab.
        visualRoot=transform.Find("IHM Valuable Visual");
        if(visualRoot==null)return Fail("visual root is missing");
        if(string.IsNullOrEmpty(assetKey))
        {
            foreach(var entry in CollectionCatalog.Entries)
            {
                string display=entry.Key=="water_timer"?"Water Watch":entry.Key.Replace('_',' ');
                if(gameObject.name.StartsWith("Valuable IHM "+display,StringComparison.Ordinal)){assetKey=entry.Key;break;}
            }
        }
        try
        {
            var model=ValuableVisuals.Load(assetKey);
            var nodes=visualRoot.GetComponentsInChildren<Transform>(true).ToDictionary(t=>t.name,t=>t,StringComparer.Ordinal);
            leftWheel=Find(nodes,"Left wheel pivot");rightWheel=Find(nodes,"Right wheel pivot");casePivot=Find(nodes,"Water watch flip pivot");
            if(assetKey=="cannon"&&(leftWheel==null||rightWheel==null))return Fail("wheel pivots are missing");
            if(assetKey=="water_timer"&&casePivot==null)return Fail("flip pivot is missing");
            var rebuilt=new List<ValuablePartBinding>();
            for(int i=0;i<model.parts.Length;i++)
            {
                var node=Find(nodes,"Part"+i.ToString("D3"));
                if(node==null||node.GetComponent<MeshRenderer>()==null||node.GetComponent<MeshFilter>()?.sharedMesh==null)return Fail("mesh part "+i+" is incomplete");
                var data=model.parts[i];
                rebuilt.Add(new ValuablePartBinding{node=node,group=data.group,phase=data.phase,rest=node.localPosition});
            }
            bindings=rebuilt.ToArray();_ready=true;return true;
        }
        catch(Exception error){return Fail(error.Message);}
    }
    private static Transform Find(Dictionary<string,Transform> nodes,string name)=>nodes.TryGetValue(name,out var node)?node:null;
    private bool Fail(string reason)
    {
        if(!_reported){Plugin.Log?.LogError("[Valuables] Cannot prepare "+gameObject.name+": "+reason);_reported=true;}
        return false;
    }
}
[Serializable] internal sealed class ValuablePartBinding { public Transform node;public string group;public float phase;public Vector3 rest; }
