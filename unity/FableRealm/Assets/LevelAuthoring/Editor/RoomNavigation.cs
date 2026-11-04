using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

namespace LevelAuthoring.Editor
{
    /// <summary>Local collider bake and path checks. Never moves authored anchors.</summary>
    public static class RoomNavigation
    {
        [Serializable] public sealed class Report
        {
            public string room, status;
            public int sources, triangles;
            public NavigationDefinition profile;
            public List<Route> routes=new List<Route>();
        }
        [Serializable] public sealed class Route
        {
            public string from,to,status;
            public Vector3[] corners;
        }

        public static string BakePrefab(GameObject prefab,RoomDefinition definition,string output)
        {
            var instance=UnityEngine.Object.Instantiate(prefab,Vector3.zero,Quaternion.identity);
            try { Physics.SyncTransforms(); return BakeAndValidate(instance,definition,output); }
            finally { UnityEngine.Object.DestroyImmediate(instance); }
        }

        public static string BakeAndValidate(GameObject room,RoomDefinition definition,string output)
        {
            var profile=definition.navigation;
            if(profile==null) return "SKIP: no navigation profile.";
            var sources=Collect(room.transform,definition);
            if(sources.Count==0) throw new InvalidOperationException("No navigation sources.");
            var settings=NavMesh.GetSettingsByID(0);
            settings.agentRadius=profile.radius; settings.agentHeight=profile.height;
            settings.agentClimb=profile.climb; settings.agentSlope=profile.slope;
            settings.overrideVoxelSize=true; settings.voxelSize=profile.voxel;
            settings.minRegionArea=.1f;
            var bounds=new Bounds(Vector3.zero,Vector3.one);
            bool first=true;
            foreach(var c in room.GetComponentsInChildren<Collider>())
            {
                if(first) { bounds=c.bounds; first=false; } else bounds.Encapsulate(c.bounds);
            }
            bounds.Expand(2);
            var data=NavMeshBuilder.BuildNavMeshData(settings,sources,bounds,Vector3.zero,Quaternion.identity);
            if(data==null) throw new InvalidOperationException("NavMesh bake failed: "+definition.id);
            // Isolate queries from any NavMesh present in the currently open scene.
            var offset=new Vector3(10000,0,10000);
            var instance=NavMesh.AddNavMeshData(data,offset,Quaternion.identity);
            bool persisted=false;
            try
            {
                var report=new Report { room=definition.id,profile=profile,sources=sources.Count,status="PASS" };
                var points=new Dictionary<string,Vector3>();
                foreach(var a in definition.anchors)
                {
                    if(a.kind!="nav") continue;
                    Vector3 p=room.transform.TransformPoint(a.position)+offset;
                    if(!NavMesh.SamplePosition(p,out var hit,profile.sampleDistance,NavMesh.AllAreas) || Mathf.Abs(hit.position.y-p.y)>profile.maxVerticalSnap)
                        throw new InvalidOperationException(definition.id+": off NavMesh or wrong storey: "+a.id);
                    points.Add(a.id,hit.position);
                }
                foreach(var a in definition.anchors)
                {
                    if(a.kind!="nav") continue;
                    foreach(var target in a.connectsTo ?? Array.Empty<string>())
                    {
                        if(string.CompareOrdinal(a.id,target)>=0) continue;
                        var path=new NavMeshPath();
                        bool found=NavMesh.CalculatePath(points[a.id],points[target],NavMesh.AllAreas,path);
                        if(!found || path.status!=NavMeshPathStatus.PathComplete)
                            throw new InvalidOperationException(definition.id+": incomplete NavMesh path "+a.id+" -> "+target);
                        var corners=path.corners;
                        for(int i=0;i<corners.Length;i++) corners[i]-=offset;
                        report.routes.Add(new Route { from=a.id,to=target,status=path.status.ToString(),corners=corners });
                    }
                }
                var tri=NavMesh.CalculateTriangulation();
                // Keep only triangles belonging to this isolated bake in the preview asset.
                var vertices=new List<Vector3>(); var indices=new List<int>();
                for(int i=0;i<tri.indices.Length;i+=3)
                {
                    var a=tri.vertices[tri.indices[i]]-offset;
                    if(!bounds.Contains(a)) continue;
                    int start=vertices.Count;
                    vertices.Add(a); vertices.Add(tri.vertices[tri.indices[i+1]]-offset); vertices.Add(tri.vertices[tri.indices[i+2]]-offset);
                    indices.Add(start); indices.Add(start+1); indices.Add(start+2);
                }
                report.triangles=indices.Count/3;
                if(report.triangles==0) throw new InvalidOperationException("Empty navigation bake.");
                var mesh=new Mesh { indexFormat=UnityEngine.Rendering.IndexFormat.UInt32 };
                mesh.SetVertices(vertices); mesh.SetTriangles(indices,0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
                SaveAsset(mesh,output+"/navigation-preview.asset");
                instance.Remove();
                SaveAsset(data,output+"/navigation.asset"); persisted=true;
                File.WriteAllText(output+"/navigation-report.json",JsonUtility.ToJson(report,true));
                return $"NAVMESH PASS: {definition.id}; {points.Count} points; {report.routes.Count} complete paths; {report.triangles} triangles. Authored points unchanged. Profile is for editor QA, not verified game agent settings.";
            }
            finally
            {
                if(instance.valid) instance.Remove();
                if(!persisted) UnityEngine.Object.DestroyImmediate(data);
            }
        }

        static List<NavMeshBuildSource> Collect(Transform root,RoomDefinition definition)
        {
            var sources=new List<NavMeshBuildSource>();
            foreach(var e in definition.elements)
            {
                if(e.navigation=="Ignore") continue;
                var parent=root.Find("Architecture/"+e.id);
                if(parent==null) throw new InvalidOperationException("Missing navigation element: "+e.id);
                foreach(var collider in parent.GetComponentsInChildren<Collider>())
                {
                    if(!collider.enabled || collider.isTrigger) continue;
                    var source=new NavMeshBuildSource { area=e.navigation=="Walkable"?0:1,component=collider };
                    if(collider is BoxCollider box)
                    {
                        source.shape=NavMeshBuildSourceShape.Box; source.size=box.size;
                        source.transform=box.transform.localToWorldMatrix*Matrix4x4.Translate(box.center);
                    }
                    else if(collider is MeshCollider mesh)
                    {
                        source.shape=NavMeshBuildSourceShape.Mesh; source.sourceObject=mesh.sharedMesh;
                        source.transform=mesh.transform.localToWorldMatrix;
                    }
                    else throw new InvalidOperationException("Unsupported navigation collider: "+collider.GetType().Name);
                    sources.Add(source);
                }
            }
            return sources;
        }

        public static string ValidateSeam(GameObject first,RoomDefinition a,string firstPortal,GameObject second,RoomDefinition b,string secondPortal,string output)
        {
            var pa=Array.Find(a.anchors,x=>x.id==firstPortal && x.kind=="portal");
            var pb=Array.Find(b.anchors,x=>x.id==secondPortal && x.kind=="portal");
            if(pa==null || pb==null) throw new InvalidOperationException("Unknown seam portal.");
            var p=first.transform.TransformPoint(pa.position); var q=second.transform.TransformPoint(pb.position);
            var facingA=first.transform.rotation*Quaternion.Euler(pa.rotation)*Vector3.forward;
            var facingB=second.transform.rotation*Quaternion.Euler(pb.rotation)*Vector3.forward;
            if(Vector3.Distance(p,q)>.02f || Vector3.Dot(facingA,facingB)>-.99f)
                throw new InvalidOperationException("Portals do not coincide and face each other.");
            if(a.navigation==null || b.navigation==null || JsonUtility.ToJson(a.navigation)!=JsonUtility.ToJson(b.navigation))
                throw new InvalidOperationException("Seam requires matching navigation profiles.");
            var sources=Collect(first.transform,a); sources.AddRange(Collect(second.transform,b));
            var bounds=new Bounds(p,Vector3.one);
            foreach(var root in new[]{first,second}) foreach(var c in root.GetComponentsInChildren<Collider>()) bounds.Encapsulate(c.bounds);
            bounds.Expand(2);
            var n=a.navigation; var settings=NavMesh.GetSettingsByID(0);
            settings.agentRadius=n.radius; settings.agentHeight=n.height; settings.agentClimb=n.climb; settings.agentSlope=n.slope;
            settings.overrideVoxelSize=true; settings.voxelSize=n.voxel; settings.minRegionArea=.1f;
            var data=NavMeshBuilder.BuildNavMeshData(settings,sources,bounds,Vector3.zero,Quaternion.identity);
            if(data==null) throw new InvalidOperationException("Seam bake failed.");
            var offset=new Vector3(10000,0,10000);
            var instance=NavMesh.AddNavMeshData(data,offset,Quaternion.identity);
            try
            {
                var start=NearestNav(first.transform,a,p)+offset;
                var end=NearestNav(second.transform,b,q)+offset;
                if(!NavMesh.SamplePosition(start,out var startHit,n.sampleDistance,NavMesh.AllAreas) || !NavMesh.SamplePosition(end,out var endHit,n.sampleDistance,NavMesh.AllAreas))
                    throw new InvalidOperationException("Seam endpoints are off NavMesh.");
                if(Mathf.Abs(start.y-startHit.position.y)>n.maxVerticalSnap || Mathf.Abs(end.y-endHit.position.y)>n.maxVerticalSnap)
                    throw new InvalidOperationException("Seam endpoints snapped to another storey.");
                var path=new NavMeshPath();
                if(!NavMesh.CalculatePath(startHit.position,endHit.position,NavMesh.AllAreas,path) || path.status!=NavMeshPathStatus.PathComplete)
                    throw new InvalidOperationException("Broken NavMesh across room seam.");
                var corners=path.corners; for(int i=0;i<corners.Length;i++) corners[i]-=offset;
                Directory.CreateDirectory(output);
                File.WriteAllText(output+"/seam-path.json",JsonUtility.ToJson(new Route { from=a.id+"/"+firstPortal,to=b.id+"/"+secondPortal,status=path.status.ToString(),corners=corners },true));
                string report=$"SEAM PASS: {a.id}/{firstPortal} <-> {b.id}/{secondPortal}; full NavMesh path, coincident portals, matching height. No runtime repair or off-mesh link used.";
                File.WriteAllText(output+"/seam-validation.txt",report+"\n"); return report;
            }
            finally { if(instance.valid) instance.Remove(); UnityEngine.Object.DestroyImmediate(data); }
        }

        static Vector3 NearestNav(Transform root,RoomDefinition definition,Vector3 portal)
        {
            float best=float.PositiveInfinity; Vector3 result=default;
            foreach(var anchor in definition.anchors)
            {
                if(anchor.kind!="nav") continue;
                var point=root.TransformPoint(anchor.position); float distance=Vector3.Distance(point,portal);
                if(distance<best) { result=point; best=distance; }
            }
            if(float.IsInfinity(best)) throw new InvalidOperationException("No navigation point at portal.");
            return result;
        }

        static void SaveAsset(UnityEngine.Object asset,string path)
        {
            var existing=AssetDatabase.LoadMainAssetAtPath(path);
            if(existing==null) AssetDatabase.CreateAsset(asset,path);
            else { EditorUtility.CopySerialized(asset,existing); EditorUtility.SetDirty(existing); UnityEngine.Object.DestroyImmediate(asset); }
        }
    }
}
