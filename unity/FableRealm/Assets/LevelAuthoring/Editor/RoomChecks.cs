using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;

namespace LevelAuthoring.Editor
{
    public static class RoomChecks
    {
        static readonly HashSet<string> Kinds=new HashSet<string> { "box","tube","arch","disc","shelf","stairs","rail","light","blocker","lathe" };
        static readonly HashSet<string> AnchorKinds=new HashSet<string> { "portal","nav","loot","volume","hide","pit","surface","extraction","truck","spawn","door","house-door","prop" };
        // Diagnostic footprints for the seven vanilla ValuableVolume types; not extracted game data.
        static readonly Dictionary<string,Vector3> LootSizes=new Dictionary<string,Vector3> {
            { "",new Vector3(.5f,.5f,.5f) },{ "Tiny",new Vector3(.3f,.3f,.3f) },{ "Small",new Vector3(.5f,.5f,.5f) },
            { "Medium",new Vector3(.9f,.9f,.9f) },{ "Big",new Vector3(1.4f,1.4f,1.4f) },{ "Wide",new Vector3(2.2f,1f,1f) },
            { "Tall",new Vector3(.9f,1.9f,.9f) },{ "VeryTall",new Vector3(1.2f,2.9f,1.2f) } };
        public const float ExtractionPad=3.2f;

        public static void ValidateDefinition(RoomDefinition d)
        {
            if(d==null || d.schemaVersion!=1) throw new InvalidDataException("Unsupported room schema.");
            Id(d.id);
            if(d.materials==null || d.elements==null || d.anchors==null || d.views==null) throw new InvalidDataException("Missing definition arrays.");
            Positive(d.bounds,"bounds");
            foreach(string colour in new[]{d.ambient,d.fog})
                if(!string.IsNullOrEmpty(colour) && !ColorUtility.TryParseHtmlString(colour,out _)) throw new InvalidDataException("Invalid lighting colour: "+colour);
            if(d.fogDensity<0 || d.fogDensity>.3f) throw new InvalidDataException("Invalid fog density.");
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
                if(!string.IsNullOrEmpty(m.pattern) && Array.IndexOf(SurfaceTextures.Patterns,m.pattern)<0) throw new InvalidDataException("Unknown surface pattern: "+m.id);
                if(m.scale<=0) throw new InvalidDataException("Invalid surface scale: "+m.id);
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
                if(e.kind=="arch" && e.palette!=null && e.palette.Length>0 && !materials.Contains(e.palette[0])) throw new InvalidDataException("Missing arch infill material: "+e.id);
                if(e.kind=="lathe")
                {
                    if(e.points==null || e.points.Length<2) throw new InvalidDataException("Invalid lathe: "+e.id);
                    foreach(var p in e.points) { Finite(p,e.id); if(p.x<0) throw new InvalidDataException("Negative lathe radius: "+e.id); }
                }
            }
            var anchors=new Dictionary<string,AnchorDefinition>();
            foreach(var a in d.anchors)
            {
                Id(a.id); Finite(a.position,a.id); Finite(a.rotation,a.id);
                if(anchors.ContainsKey(a.id) || !AnchorKinds.Contains(a.kind)) throw new InvalidDataException("Invalid anchor: "+a.id);
                anchors.Add(a.id,a);
                string tag=a.tag ?? "";
                bool known=a.kind=="loot" ? LootSizes.ContainsKey(tag)
                    : a.kind=="nav" ? tag=="" || tag=="connect" || tag=="truck"
                    : a.kind=="volume" ? tag=="" || tag=="truck" || tag=="extraction"
                    : a.kind=="hide" ? tag=="" || tag=="cover"
                    : tag=="";
                if(!known) throw new InvalidDataException("Unknown tag '"+tag+"' on "+a.kind+" anchor "+a.id);
            }
            foreach(var e in d.elements)
            {
                if(string.IsNullOrEmpty(e.variant)) continue;
                if(!e.variant.StartsWith("closed-",StringComparison.Ordinal) || !anchors.TryGetValue(e.variant.Substring(7),out var portal) || portal.kind!="portal")
                    throw new InvalidDataException("Variant must be closed-<portal id>: "+e.id);
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
            foreach(var a in d.anchors)
            {
                // A door leaf is a hinge at a doorway: position = hinge foot, rotation = closed direction, size = leaf.
                if(a.kind!="door") continue;
                if(a.size.x<=0 || a.size.y<=0 || a.size.z<=0) throw new InvalidDataException("Door leaf needs a size: "+a.id);
                if(!Array.Exists(d.anchors,x=>x.kind=="portal" && Vector3.Distance(x.position,a.position)<3f)) throw new InvalidDataException("Door leaf is not at an exit: "+a.id);
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
                    if(a.kind=="loot")
                    {
                        // Position is the base centre of the volume: it needs support below and free space above.
                        var size=LootSizes[a.tag ?? ""];
                        if(!Physics.Raycast(p+Vector3.up*.1f,Vector3.down,.3f))
                            throw new InvalidOperationException(d.id+": unsupported loot volume "+a.id);
                        if(Physics.CheckBox(p+Vector3.up*(size.y/2+.06f),size/2,Quaternion.Euler(a.rotation)))
                            throw new InvalidOperationException(d.id+": obstructed loot volume "+a.id);
                    }
                    if(a.kind=="extraction" || a.kind=="truck")
                    {
                        // Flat, empty pad: extraction uses a fixed square, the truck bay its authored size.
                        var pad=a.kind=="truck"?a.size:new Vector3(ExtractionPad,2.2f,ExtractionPad);
                        var turn=Quaternion.Euler(a.rotation);
                        if(pad.x<=0 || pad.y<=0 || pad.z<=0) throw new InvalidOperationException(d.id+": pad needs a size at "+a.id);
                        if(Physics.CheckBox(p+Vector3.up*(pad.y/2+.08f),pad/2,turn))
                            throw new InvalidOperationException(d.id+": obstructed pad "+a.id);
                        for(int sx=-1;sx<=1;sx++) for(int sz=-1;sz<=1;sz++)
                        {
                            var corner=p+turn*new Vector3(sx*(pad.x/2-.15f),0,sz*(pad.z/2-.15f));
                            if(!Physics.Raycast(corner+Vector3.up*.25f,Vector3.down,out var ground,.55f) || Mathf.Abs(ground.point.y-p.y)>.05f)
                                throw new InvalidOperationException(d.id+": pad is not flat at "+a.id);
                        }
                    }
                    if(a.kind=="nav" || a.kind=="surface" || a.kind=="portal" || a.kind=="spawn")
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
                    if(a.kind=="hide" && a.tag=="cover")
                    {
                        // Open cover (grass, behind a chair, a curtain): floor underneath and room to crouch, no walls required.
                        if(!Physics.Raycast(p,Vector3.down,1.3f) || Physics.CheckSphere(p,.3f))
                            throw new InvalidOperationException(d.id+": cover spot has no floor or is blocked at "+a.id);
                    }
                    else if(a.kind=="hide")
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
                int plugs=ValidateVariants(instance,d);
                return $"PASS: {d.id}; {d.anchors.Length} anchors; {edges} navigation edges; {plugs} closable exits. Clearance probe: radius 0.3 m, height 1.75 m. This is authoring QA, not game AI or cart validation.";
            }
            finally { UnityEngine.Object.DestroyImmediate(instance); }
        }

        // Each closed-exit variant must really seal its doorway when switched on.
        static int ValidateVariants(GameObject instance,RoomDefinition d)
        {
            var root=instance.transform.Find(RoomCompiler.VariantRoot);
            if(root==null) return 0;
            int count=0;
            foreach(Transform group in root)
            {
                var portal=Array.Find(d.anchors,x=>x.kind=="portal" && "closed-"+x.id==group.name);
                if(portal==null) throw new InvalidOperationException(d.id+": variant without portal "+group.name);
                group.gameObject.SetActive(true); Physics.SyncTransforms();
                try
                {
                    Vector3 outward=instance.transform.rotation*Quaternion.Euler(portal.rotation)*Vector3.forward;
                    Vector3 p=instance.transform.TransformPoint(portal.position);
                    float half=Mathf.Max(.6f,portal.size.x/2-.45f);
                    Vector3 side=Vector3.Cross(Vector3.up,outward);
                    foreach(float offset in new[]{-half,0,half})
                        foreach(float height in new[]{.3f,1f,1.7f})
                            if(!Physics.Raycast(p-outward*1.4f+side*offset+Vector3.up*height,outward,1.8f))
                                throw new InvalidOperationException(d.id+": closed variant leaves a gap at "+portal.id);
                }
                finally { group.gameObject.SetActive(false); Physics.SyncTransforms(); }
                count++;
            }
            return count;
        }

        /// <summary>Vanilla-style module contract: exits, closable doors, connect points, loot, extraction, truck.</summary>
        public static string ValidateRole(RoomDefinition d,string role)
        {
            var sides=new Dictionary<string,AnchorDefinition>();
            int loot=0,extraction=0,truck=0,spawn=0,truckNav=0,volumes=0;
            foreach(var a in d.anchors)
            {
                if(a.kind=="loot") loot++;
                if(a.kind=="extraction") extraction++;
                if(a.kind=="truck") truck++;
                if(a.kind=="spawn") spawn++;
                if(a.kind=="volume") volumes++;
                if(a.kind=="nav" && a.tag=="truck") truckNav++;
                if(a.kind!="portal") continue;
                // Exits sit on the centre of a 15 m module edge and face outward, so any two rooms can mate.
                Vector3 outward=Quaternion.Euler(a.rotation)*Vector3.forward;
                string side=outward.z>.99f?"north":outward.x>.99f?"east":outward.z<-.99f?"south":outward.x<-.99f?"west":null;
                if(side==null || Vector3.Distance(a.position,outward*7.5f)>.01f || sides.ContainsKey(side))
                    throw new InvalidDataException(d.id+": exit is off the module grid: "+a.id);
                if(Mathf.Abs(a.size.x-4.2f)>.01f || Mathf.Abs(a.size.y-3.8f)>.01f)
                    throw new InvalidDataException(d.id+": exit must be 4.2 x 3.8 m: "+a.id);
                sides.Add(side,a);
                var connect=Array.Find(d.anchors,x=>x.kind=="nav" && x.tag=="connect" && Vector3.Distance(x.position,a.position)<1.6f
                    && Vector3.Dot(Quaternion.Euler(x.rotation)*Vector3.forward,outward)>.99f);
                if(connect==null) throw new InvalidDataException(d.id+": exit needs an outward-facing connect point: "+a.id);
                bool closable=Array.Exists(d.elements,e=>e.variant=="closed-"+a.id);
                if(role=="Normal" && !closable) throw new InvalidDataException(d.id+": Normal room exit needs a closed variant: "+a.id);
            }
            string expected=role=="Normal"?"east,north,south,west":role=="Passage"?"north,south":role=="Start"?"north":"south";
            var found=new List<string>(sides.Keys); found.Sort(StringComparer.Ordinal);
            if(string.Join(",",found)!=expected) throw new InvalidDataException(d.id+": "+role+" needs exits "+expected+", has "+string.Join(",",found));
            if(volumes==0) throw new InvalidDataException(d.id+": missing room volume.");
            int needExtraction=role=="Extraction" || role=="Start"?1:0;
            if(extraction!=needExtraction) throw new InvalidDataException(d.id+": "+role+" needs "+needExtraction+" extraction point(s).");
            if(role=="Start" ? truck!=1 || truckNav!=1 || spawn<6 : truck+truckNav+spawn>0)
                throw new InvalidDataException(d.id+": truck, truck point and at least 6 spawns belong to the Start room only.");
            int needLoot=role=="DeadEnd"?10:role=="Start"?0:role=="Extraction"?5:8;
            if(loot<needLoot) throw new InvalidDataException(d.id+": "+role+" needs at least "+needLoot+" loot volumes, has "+loot);
            return $"ROLE PASS: {d.id} as {role}; exits {expected}; {loot} loot volumes; {extraction} extraction; {spawn} spawns.";
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
