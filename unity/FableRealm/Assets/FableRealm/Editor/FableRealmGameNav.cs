using System;
using System.Collections.Generic;
using System.IO;
using LevelAuthoring.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

namespace FableRealm.Editor
{
    /// <summary>
    /// Monster pathing with the game's own agents. The room checks bake with an authoring profile (0.35 m radius,
    /// 1.8 m height, 0.25 m climb); the game bakes its level with the agents in its project settings, read from
    /// REPO_Data/globalgamemanagers: "Humanoid" (0.3 m, 2.0 m, climb 0.75, slope 60) and "Enemy" (0.6 m, 2.0 m,
    /// climb 0.4, slope 60), both with a minimum region of 2 m². Islands under 2 m² are dropped there.
    ///
    /// Every room is joined to a four-exit neighbour through each of its doorways, as the generator joins modules,
    /// and baked once per agent. Every level point must lie on that NavMesh (the game samples 0.5 m around it) and
    /// have a complete path through the doorway to the neighbour's entry point: monsters spawn on level points and
    /// walk between them, so a point that fails here is a spawn that cannot reach the players, or a place they
    /// cannot follow a player to. Doors, plugs and decoration are left out, as at runtime.
    /// </summary>
    public static class FableRealmGameNav
    {
        sealed class Agent
        {
            public string name; public float radius, height, climb, slope;
            public Agent(string name,float radius,float height,float climb,float slope) { this.name=name; this.radius=radius; this.height=height; this.climb=climb; this.slope=slope; }
        }

        static readonly Agent[] Agents={ new Agent("Humanoid",.3f,2f,.75f,60f), new Agent("Enemy",.6f,2f,.4f,60f) };
        const float MinRegionArea=2f, GameSample=.5f, Module=15f;
        // A four-exit room with a plain floor near all of its doorways; it stands in for whatever the generator puts next door.
        const string Neighbour="GiantPantry", NeighbourEntry="south";

        [MenuItem("Fable Realm/Check monster paths (game agents)")]
        public static void Check()
        {
            var set=JsonUtility.FromJson<RoomSetDefinition>(File.ReadAllText(FableRealmLevel.Set));
            var rooms=new List<(RoomDefinition definition,GameObject prefab)>();
            foreach(var entry in set.rooms)
            {
                var definition=RoomCompiler.Read(entry.recipe);
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(FableRealmLevel.Output+"/"+definition.id+"/"+definition.id+".prefab");
                if(prefab==null) throw new InvalidOperationException("Room not built: "+definition.id);
                rooms.Add((definition,prefab));
            }
            var neighbour=rooms.Find(r=>r.definition.id==Neighbour);
            if(neighbour.prefab==null) throw new InvalidOperationException("Missing neighbour room "+Neighbour);

            var lines=new List<string>(); int failures=0;
            foreach(var (definition,prefab) in rooms)
            {
                var problems=new List<string>(); int checkedPoints=0;
                foreach(var portal in definition.anchors)
                {
                    if(portal.kind!="portal") continue;
                    var outward=new Vector3(portal.position.x,0,portal.position.z).normalized;
                    float yaw=Mathf.Round(Mathf.Atan2(outward.x,outward.z)*Mathf.Rad2Deg/90f)*90f;
                    foreach(var agent in Agents)
                    {
                        var room=UnityEngine.Object.Instantiate(prefab,Vector3.zero,Quaternion.identity);
                        // The neighbour's south doorway faces this one across the shared edge.
                        var next=UnityEngine.Object.Instantiate(neighbour.prefab,outward*Module,Quaternion.Euler(0,yaw,0));
                        try
                        {
                            Physics.SyncTransforms();
                            checkedPoints+=CheckPair(room,definition,next,neighbour.definition,agent,portal.id,problems);
                        }
                        finally { UnityEngine.Object.DestroyImmediate(room); UnityEngine.Object.DestroyImmediate(next); }
                    }
                }
                string line=problems.Count==0
                    ? $"GAME NAV PASS: {definition.id}; {checkedPoints} point paths through every doorway for Humanoid and Enemy agents."
                    : $"GAME NAV FAIL: {definition.id}:\n    "+string.Join("\n    ",problems);
                if(problems.Count>0) failures++;
                lines.Add(line); Debug.Log(line);
            }
            File.WriteAllText(FableRealmLevel.Output+"/FableRealm-game-nav-report.txt",string.Join("\n",lines)+
                "\nAgents from the game's NavMesh project settings; one neighbour room per doorway; doors, plugs and decoration excluded as in game.\n");
            AssetDatabase.Refresh();
            if(failures>0) throw new InvalidOperationException(failures+" room(s) have points monsters cannot reach.");
            Debug.Log("GAME NAV SET PASS: "+lines.Count+" rooms.");
        }

        static int CheckPair(GameObject room,RoomDefinition a,GameObject next,RoomDefinition b,Agent agent,string portal,List<string> problems)
        {
            var sources=RoomNavigation.Collect(room.transform,a); sources.AddRange(RoomNavigation.Collect(next.transform,b));
            var bounds=new Bounds(Vector3.zero,Vector3.one);
            foreach(var root in new[]{room,next}) foreach(var c in root.GetComponentsInChildren<Collider>()) bounds.Encapsulate(c.bounds);
            bounds.Expand(2);
            var settings=NavMesh.GetSettingsByID(0);
            settings.agentRadius=agent.radius; settings.agentHeight=agent.height; settings.agentClimb=agent.climb; settings.agentSlope=agent.slope;
            settings.overrideVoxelSize=false; settings.minRegionArea=MinRegionArea;
            var data=NavMeshBuilder.BuildNavMeshData(settings,sources,bounds,Vector3.zero,Quaternion.identity);
            if(data==null) { problems.Add(agent.name+" via "+portal+": bake failed"); return 0; }
            var offset=new Vector3(20000,0,20000);
            var instance=NavMesh.AddNavMeshData(data,offset,Quaternion.identity);
            int count=0;
            try
            {
                var entry=Array.Find(b.anchors,x=>x.kind=="nav" && x.id==NeighbourEntry);
                if(!NavMesh.SamplePosition(next.transform.TransformPoint(entry.position)+offset,out var target,GameSample,NavMesh.AllAreas))
                { problems.Add(agent.name+" via "+portal+": neighbour entry off NavMesh"); return 0; }
                foreach(var anchor in a.anchors)
                {
                    if(anchor.kind!="nav") continue;
                    var p=room.transform.TransformPoint(anchor.position)+offset;
                    if(!NavMesh.SamplePosition(p,out var hit,GameSample,NavMesh.AllAreas))
                    { problems.Add($"{agent.name}: level point {anchor.id} at {anchor.position} is off the NavMesh"); continue; }
                    var path=new NavMeshPath();
                    if(!NavMesh.CalculatePath(hit.position,target.position,NavMesh.AllAreas,path) || path.status!=NavMeshPathStatus.PathComplete)
                        problems.Add($"{agent.name}: no path from {anchor.id} at {anchor.position} out through {portal}");
                    count++;
                }
            }
            finally { if(instance.valid) instance.Remove(); UnityEngine.Object.DestroyImmediate(data); }
            return count;
        }
    }
}
