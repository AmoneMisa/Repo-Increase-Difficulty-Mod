using System;
using System.IO;
using LevelAuthoring.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace FableRealm.Editor
{
    public static class LibraryShowcase
    {
        public const string Recipe="Assets/FableRealm/RoomDefinitions/HollowLibrary.room.json";
        public const string Output="Assets/FableRealm/Generated/Authored/HollowLibrary";

        [MenuItem("Fable Realm/Build detailed library")]
        public static void Build()
        {
            if(!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            RoomStudio.Build(Recipe,Output);
        }

        // End-to-end authoring checks, including a second theme-independent recipe.
        public static void VerifyAll()
        {
            string example="Assets/FableRealm/RoomDefinitions/ExampleRoom.room.json";
            var valid=RoomCompiler.Read(example);
            var missingMaterial=JsonUtility.FromJson<RoomDefinition>(JsonUtility.ToJson(valid));
            missingMaterial.elements[0].material="NotThere";
            ExpectRejected(missingMaterial,"missing material");
            var brokenEdge=JsonUtility.FromJson<RoomDefinition>(JsonUtility.ToJson(valid));
            brokenEdge.anchors[1].connectsTo=Array.Empty<string>();
            ExpectRejected(brokenEdge,"nonreciprocal edge");
            var unknownKind=JsonUtility.FromJson<RoomDefinition>(JsonUtility.ToJson(valid));
            unknownKind.elements[0].kind="typo";
            ExpectRejected(unknownKind,"unknown builder");
            VerifyNavigationRejectsDisconnectedFloor();
            RoomSetCompiler.Build("Assets/FableRealm/RoomDefinitions/LibraryStudy.room-set.json","Assets/FableRealm/Generated/Authored");
            VerifySeam();
            Debug.Log("AUTHORING CHECKS PASSED: example, library, missing materials, invalid edges, unknown builders.");
        }

        static void ExpectRejected(RoomDefinition definition,string name)
        {
            try { RoomChecks.ValidateDefinition(definition); }
            catch(InvalidDataException) { return; }
            throw new InvalidOperationException("Expected validation rejection: "+name);
        }

        static void VerifyNavigationRejectsDisconnectedFloor()
        {
            var root=new GameObject("Navigation negative fixture");
            var architecture=new GameObject("Architecture").transform; architecture.SetParent(root.transform,false);
            var elements=new ElementDefinition[2];
            for(int i=0;i<2;i++)
            {
                var parent=new GameObject("island-"+i).transform; parent.SetParent(architecture,false);
                var floor=GameObject.CreatePrimitive(PrimitiveType.Cube); floor.transform.SetParent(parent,false);
                floor.transform.localPosition=new Vector3(i==0?-3:3,-.2f,0); floor.transform.localScale=new Vector3(2,.4f,4);
                elements[i]=new ElementDefinition { id=parent.name,navigation="Walkable" };
            }
            var definition=new RoomDefinition { id="DisconnectedFixture",navigation=new NavigationDefinition(),elements=elements,
                anchors=new[] {
                    new AnchorDefinition { id="a",kind="nav",position=new Vector3(-3,0,0),connectsTo=new[]{"b"} },
                    new AnchorDefinition { id="b",kind="nav",position=new Vector3(3,0,0),connectsTo=new[]{"a"} }
                }};
            bool rejected=false;
            try
            {
                UnityEngine.Physics.SyncTransforms();
                try { RoomNavigation.BakeAndValidate(root,definition,"Assets/FableRealm/Generated/Authored"); }
                catch(InvalidOperationException ex) when(ex.Message.Contains("incomplete NavMesh path")) { rejected=true; }
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
            if(!rejected) throw new InvalidOperationException("Disconnected NavMesh fixture was incorrectly accepted.");
            Debug.Log("PASS: disconnected NavMesh rejected without auto-moving anchors.");
        }

        static void VerifySeam()
        {
            const string output="Assets/FableRealm/Generated/Authored";
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var library=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(output+"/HollowLibrary/HollowLibrary.prefab"));
            var hall=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(output+"/RootHall/RootHall.prefab"));
            hall.transform.position=new Vector3(0,0,-15);
            var a=RoomCompiler.Read(Recipe);
            var b=RoomCompiler.Read("Assets/FableRealm/RoomDefinitions/RootHall.room.json");
            string report=RoomNavigation.ValidateSeam(library,a,"south",hall,b,"door-north",output+"/JoinedRooms");
            Debug.Log(report);
            EditorSceneManager.SaveScene(scene,output+"/JoinedRooms/LibraryAndRootHall.unity");
        }
    }
}
