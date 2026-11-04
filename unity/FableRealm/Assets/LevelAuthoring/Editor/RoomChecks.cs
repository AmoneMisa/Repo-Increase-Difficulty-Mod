using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;

namespace LevelAuthoring.Editor
{
    public static class RoomChecks
    {
        static readonly HashSet<string> Kinds=new HashSet<string> { "box","tube","arch","disc","shelf","stairs","rail","light" };
        static readonly HashSet<string> AnchorKinds=new HashSet<string> { "portal","nav","loot","volume","hide","pit","surface" };

        public static void ValidateDefinition(RoomDefinition d)
        {
            if(d==null || d.schemaVersion!=1) throw new InvalidDataException("Unsupported room schema.");
            Id(d.id);
            if(d.materials==null || d.elements==null || d.anchors==null || d.views==null) throw new InvalidDataException("Missing definition arrays.");
            Positive(d.bounds,"bounds");
            if(d.navigation!=null)
            {
                var n=d.navigation;
                if(n.radius<=0 || n.height<=0 || n.climb<0 || n.slope<=0 || n.slope>60 || n.voxel<=0 || n.sampleDistance<=0 || n.maxVerticalSnap<0)
                    throw new InvalidDataException("Invalid navigation profile.");
                foreach(var e in d.elements)
                    if(e.navigation!="Walkable" && e.navigation!="Obstacle" && e.navigation!="Ignore")
                        throw new InvalidDataException("Explicit navigation classification required: "+e.id);
            }
            var materials=new HashSet<string>();
            foreach(var m in d.materials)
            {
                Id(m.id);
                if(!materials.Add(m.id) || !ColorUtility.TryParseHtmlString(m.color,out _)) throw new InvalidDataException("Invalid or duplicate material: "+m.id);
            }
            var ids=new HashSet<string>();
            foreach(var e in d.elements)
            {
                Id(e.id);
                if(!ids.Add(e.id) || !Kinds.Contains(e.kind)) throw new InvalidDataException("Invalid or duplicate element: "+e.id);
                if(!materials.Contains(e.material)) throw new InvalidDataException("Missing material: "+e.material);
                Finite(e.position,e.id); Finite(e.rotation,e.id); Positive(e.size,e.id);
                if(e.kind=="tube")
                {
                    if(e.points==null || e.points.Length<2 || e.radius<=0) throw new InvalidDataException("Invalid tube: "+e.id);
                    for(int i=0;i<e.points.Length;i++)
                    {
                        Finite(e.points[i],e.id);
                        if(i>0 && Vector3.Distance(e.points[i-1],e.points[i])<.001f) throw new InvalidDataException("Repeated tube point: "+e.id);
                    }
                }
                if(e.kind=="shelf")
                {
                    if(e.palette==null || e.palette.Length<2 || e.size.x<.6f || e.size.y<.6f) throw new InvalidDataException("Invalid shelf: "+e.id);
                    foreach(var m in e.palette) if(!materials.Contains(m)) throw new InvalidDataException("Missing palette material: "+m);
                }
                if((e.kind=="stairs" || e.kind=="rail" || e.kind=="shelf") && (e.count<1 || e.count>200)) throw new InvalidDataException("Invalid subdivision count: "+e.id);
                if(e.kind=="arch" && e.radius<=0) throw new InvalidDataException("Invalid arch: "+e.id);
            }
            var anchors=new Dictionary<string,AnchorDefinition>();
            foreach(var a in d.anchors)
            {
                Id(a.id); Finite(a.position,a.id); Finite(a.rotation,a.id);
                if(anchors.ContainsKey(a.id) || !AnchorKinds.Contains(a.kind)) throw new InvalidDataException("Invalid anchor: "+a.id);
                anchors.Add(a.id,a);
            }
            var nav=new List<AnchorDefinition>();
            foreach(var a in d.anchors)
            {
                if(a.kind!="nav") continue;
                nav.Add(a);
                foreach(string target in a.connectsTo ?? Array.Empty<string>())
                {
                    if(!anchors.TryGetValue(target,out var b) || b.kind!="nav" || target==a.id) throw new InvalidDataException("Bad navigation target: "+a.id);
                    if(Array.IndexOf(b.connectsTo ?? Array.Empty<string>(),a.id)<0) throw new InvalidDataException("Navigation edge must be reciprocal: "+a.id+" / "+target);
                }
            }
            if(nav.Count==0) throw new InvalidDataException("Room requires a navigation graph.");
            var reached=new HashSet<string>(); var pending=new Stack<string>(); pending.Push(nav[0].id);
            while(pending.Count>0)
            {
                string id=pending.Pop(); if(!reached.Add(id)) continue;
                foreach(var next in anchors[id].connectsTo ?? Array.Empty<string>()) pending.Push(next);
            }
            if(reached.Count!=nav.Count) throw new InvalidDataException("Disconnected navigation graph.");
            var views=new HashSet<string>();
            foreach(var v in d.views)
            {
                Id(v.id); Finite(v.position,v.id); Finite(v.target,v.id);
                if(!views.Add(v.id) || v.fieldOfView<10 || v.fieldOfView>120 || Vector3.Distance(v.position,v.target)<.01f) throw new InvalidDataException("Invalid view: "+v.id);
            }
        }

        public static string ValidateGeometry(GameObject prefab,RoomDefinition d)
        {
            var instance=UnityEngine.Object.Instantiate(prefab,new Vector3(1000,0,1000),Quaternion.identity);
            try
            {
                Physics.SyncTransforms();
                var anchors=new Dictionary<string,AnchorDefinition>();
                foreach(var a in d.anchors) anchors.Add(a.id,a);
                int edges=0;
                foreach(var a in d.anchors)
                {
                    Vector3 p=instance.transform.TransformPoint(a.position);
                    if(a.kind=="nav" || a.kind=="surface" || a.kind=="portal")
                    {
                        if(a.kind=="portal") p-=Quaternion.Euler(a.rotation)*Vector3.forward*.7f;
                        if(!Physics.Raycast(p+Vector3.up*.25f,Vector3.down,out var floor,.55f) || Mathf.Abs(floor.point.y-p.y)>.12f)
                            throw new InvalidOperationException(d.id+": missing floor at "+a.id);
                        if(Physics.CheckCapsule(p+Vector3.up*.35f,p+Vector3.up*1.45f,.3f))
                            throw new InvalidOperationException(d.id+": blocked clearance at "+a.id);
                    }
                    if(a.kind=="pit")
                    {
                        if(!Physics.Raycast(p+Vector3.up*.1f,Vector3.down,out var floor,20) || floor.point.y>p.y-a.size.y+.1f)
                            throw new InvalidOperationException(d.id+": pit is covered at "+a.id);
                    }
                    if(a.kind=="hide")
                    {
                        Vector3 forward=Quaternion.Euler(a.rotation)*Vector3.forward;
                        if(!Physics.Raycast(p,forward,1.3f) || Physics.Raycast(p,-forward,1.3f) || Physics.CheckSphere(p,.3f))
                            throw new InvalidOperationException(d.id+": invalid hide entrance/back at "+a.id);
                    }
                    if(a.kind=="nav") foreach(var target in a.connectsTo ?? Array.Empty<string>())
                    {
                        if(string.CompareOrdinal(a.id,target)>=0) continue;
                        var b=anchors[target]; Vector3 end=instance.transform.TransformPoint(b.position);
                        var delta=end-p;
                        if(Physics.SphereCast(p+Vector3.up*.9f,.25f,delta.normalized,out var hit,delta.magnitude))
                            throw new InvalidOperationException(d.id+": blocked edge "+a.id+" -> "+target+" by "+hit.collider.name);
                        // Sample feet along every segment, so an edge cannot silently cross a hole.
                        int samples=Mathf.CeilToInt(delta.magnitude/.25f);
                        for(int i=1;i<samples;i++)
                        {
                            var foot=Vector3.Lerp(p,end,i/(float)samples);
                            if(!Physics.Raycast(foot+Vector3.up*.25f,Vector3.down,out var floor,.55f) || Mathf.Abs(floor.point.y-foot.y)>.15f)
                                throw new InvalidOperationException(d.id+": unsupported edge "+a.id+" -> "+target);
                        }
                        edges++;
                    }
                }
                return $"PASS: {d.id}; {d.anchors.Length} anchors; {edges} navigation edges. Clearance probe: radius 0.3 m, height 1.75 m. This is authoring QA, not game AI or cart validation.";
            }
            finally { UnityEngine.Object.DestroyImmediate(instance); }
        }

        static void Id(string value)
        {
            if(string.IsNullOrEmpty(value) || !Regex.IsMatch(value,"^[A-Za-z0-9_-]+$")) throw new InvalidDataException("Invalid identifier: "+value);
        }
        static void Positive(Vector3 value,string name)
        {
            Finite(value,name); if(value.x<=0 || value.y<=0 || value.z<=0) throw new InvalidDataException("Nonpositive dimensions: "+name);
        }
        static void Finite(Vector3 value,string name)
        {
            if(float.IsNaN(value.sqrMagnitude) || float.IsInfinity(value.sqrMagnitude)) throw new InvalidDataException("Nonfinite vector: "+name);
        }
    }
}
