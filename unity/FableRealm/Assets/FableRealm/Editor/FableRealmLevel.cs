using System;
using System.Collections.Generic;
using System.IO;
using LevelAuthoring.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace FableRealm.Editor
{
    /// <summary>Builds and validates every room of the level set. Authoring only; no game components.</summary>
    public static class FableRealmLevel
    {
        public const string Set="Assets/FableRealm/RoomDefinitions/FableRealm.room-set.json";
        public const string Output="Assets/FableRealm/Generated/Authored";

        // Optional: FABLE_ROOMS=RoseMaze,GiantTea rebuilds only those rooms.
        [MenuItem("Fable Realm/Build level room set")]
        public static void Build()
        {
            if(!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var set=JsonUtility.FromJson<RoomSetDefinition>(File.ReadAllText(Set));
            if(set==null || set.rooms==null || set.rooms.Length==0) throw new InvalidDataException("Invalid level room set.");
            string filter=Environment.GetEnvironmentVariable("FABLE_ROOMS");
            var only=string.IsNullOrEmpty(filter)?null:new HashSet<string>(filter.Split(','));
            var lines=new List<string>(); int failed=0;
            foreach(var entry in set.rooms)
            {
                string id=Path.GetFileName(entry.recipe).Replace(".room.json","");
                if(only!=null && !only.Contains(id)) continue;
                try
                {
                    // Keep going after a failure, so one report lists every room that needs work.
                    var definition=RoomCompiler.Read(entry.recipe);
                    RoomStudio.Build(entry.recipe,Output+"/"+definition.id);
                    string report=RoomChecks.ValidateRole(definition,entry.role);
                    File.WriteAllText(Output+"/"+definition.id+"/role-validation.txt",report+"\n");
                    lines.Add(report);
                }
                catch(Exception ex)
                {
                    failed++; lines.Add("ROOM FAIL: "+id+": "+ex.Message);
                }
                Debug.Log("LEVEL "+lines[lines.Count-1]);
            }
            if(only==null)
                File.WriteAllText(Output+"/FableRealm-level-report.txt",string.Join("\n",lines)+
                    "\nAUTHORING ONLY: geometry, editor NavMesh and module-contract checks. Not a registered playable level.\n");
            AssetDatabase.Refresh();
            if(failed>0) throw new InvalidOperationException(failed+" room(s) failed validation.");
            Debug.Log("LEVEL SET PASS: "+lines.Count+" rooms.");
        }

        sealed class Cell
        {
            public string room, closed; public int x, z, yaw;
            public Cell(string room,int x,int z,int yaw,string closed=null) { this.room=room; this.x=x; this.z=z; this.yaw=yaw; this.closed=closed; }
        }

        // One hand-placed assembly on the vanilla grid: start at the bottom, 15 m cells, leaves turned toward their neighbour.
        static readonly Cell[] SampleLayout={
            new Cell("BookGate",0,0,0),new Cell("RootHall",0,1,0),new Cell("HollowLibrary",-1,1,270),new Cell("FireflyBrook",1,1,90),
            new Cell("MarzipanVault",2,1,90),new Cell("GiantTea",0,2,0),new Cell("ForgottenToy",-1,2,270),
            new Cell("SilentBallroom",1,2,0,"closed-door-south"),new Cell("StoryArchive",2,2,90),new Cell("Clockmaker",1,3,0),
            new Cell("RoyalTreasury",0,3,0) };
        const int SampleConnections=10;

        /// <summary>Assembles a sample level from built prefabs and checks it the way the game uses modules.</summary>
        [MenuItem("Fable Realm/Build sample level assembly")]
        public static void BuildSampleLevel()
        {
            if(!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            string output=Output+"/SampleLevel"; Directory.CreateDirectory(output);
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat; RenderSettings.ambientLight=new Color(.19f,.22f,.27f);
            var rooms=new List<RoomNavigation.PlacedRoom>();
            foreach(var cell in SampleLayout)
            {
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(Output+"/"+cell.room+"/"+cell.room+".prefab");
                if(prefab==null) throw new InvalidOperationException("Build the level room set first: "+cell.room);
                var obj=(GameObject)PrefabUtility.InstantiatePrefab(prefab);
                obj.transform.SetPositionAndRotation(new Vector3(cell.x*15,0,cell.z*15),Quaternion.Euler(0,cell.yaw,0));
                if(cell.closed!=null)
                {
                    var plug=obj.transform.Find(RoomCompiler.VariantRoot+"/"+cell.closed);
                    if(plug==null) throw new InvalidOperationException("Missing closed variant: "+cell.room+"/"+cell.closed);
                    plug.gameObject.SetActive(true);
                }
                rooms.Add(new RoomNavigation.PlacedRoom { room=obj,definition=RoomCompiler.Read("Assets/FableRealm/RoomDefinitions/"+cell.room+".room.json") });
            }
            Physics.SyncTransforms();
            // The game links modules through pairs of connect points: close together and facing each other.
            var positions=new List<Vector3>(); var forwards=new List<Vector3>();
            foreach(var placed in rooms)
                foreach(var a in placed.definition.anchors)
                    if(a.kind=="nav" && a.tag=="connect")
                    {
                        positions.Add(placed.room.transform.TransformPoint(a.position));
                        forwards.Add(placed.room.transform.rotation*Quaternion.Euler(a.rotation)*Vector3.forward);
                    }
            int pairs=0;
            for(int i=0;i<positions.Count;i++)
                for(int j=i+1;j<positions.Count;j++)
                {
                    var delta=positions[j]-positions[i];
                    if(delta.magnitude<3f && Vector3.Dot(forwards[i],forwards[j])<=-.8f && Vector3.Dot(forwards[i],delta.normalized)>=.8f) pairs++;
                }
            if(pairs!=SampleConnections) throw new InvalidOperationException("Expected "+SampleConnections+" connected module pairs, found "+pairs);
            string report=RoomNavigation.ValidateAssembly(rooms,rooms[0],"truck-point",out var preview);
            string connect="CONNECT PASS: "+pairs+" facing connect-point pairs.";
            Debug.Log("LEVEL "+report); Debug.Log("LEVEL "+connect);
            File.WriteAllText(output+"/assembly-validation.txt",report+"\n"+connect+"\nSample layout only: the game generator picks and places modules itself.\n");
            EditorSceneManager.SaveScene(scene,output+"/FableRealm-sample-level.unity");
            // Top-down review image: walkable surface of the whole assembly.
            var overlay=new GameObject("Navigation review only");
            var material=new Material(Shader.Find("Unlit/Color")) { color=new Color(.17f,.64f,.72f) };
            overlay.AddComponent<MeshFilter>().sharedMesh=preview; overlay.AddComponent<MeshRenderer>().sharedMaterial=material;
            foreach(var placed in rooms) placed.room.SetActive(false);
            var camera=new GameObject("Review camera").AddComponent<Camera>();
            camera.orthographic=true; camera.orthographicSize=34; camera.clearFlags=CameraClearFlags.SolidColor;
            camera.backgroundColor=new Color(.025f,.035f,.045f); camera.farClipPlane=200;
            camera.transform.SetPositionAndRotation(new Vector3(7.5f,80,22.5f),Quaternion.Euler(90,0,0));
            var target=new RenderTexture(1600,1600,24); var image=new Texture2D(1600,1600,TextureFormat.RGB24,false);
            try
            {
                camera.targetTexture=target; camera.Render(); RenderTexture.active=target;
                image.ReadPixels(new Rect(0,0,1600,1600),0,0); image.Apply();
                File.WriteAllBytes(output+"/assembly-navigation.png",image.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture=null; RenderTexture.active=null; target.Release();
                UnityEngine.Object.DestroyImmediate(target); UnityEngine.Object.DestroyImmediate(image);
                UnityEngine.Object.DestroyImmediate(overlay); UnityEngine.Object.DestroyImmediate(material);
                UnityEngine.Object.DestroyImmediate(preview);
            }
            AssetDatabase.Refresh();
        }
    }
}
