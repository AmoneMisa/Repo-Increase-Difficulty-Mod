using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace FableRealm.Editor
{
    /// <summary>Art authoring only. No game components or runtime registration.</summary>
    public static partial class FableRealmBuilder
    {
        const string Root = "Assets/FableRealm";
        const string Output = Root + "/Generated";
        [Serializable] public class Room { public string id, key, title, biome, role, hero, status; public float width, depth, doorClearance; }
        [Serializable] public class Catalog { public Room[] rooms; }
        [Serializable] public class MaterialSpec { public string name, color; public float smoothness; }
        [Serializable] public class Manifest { public MaterialSpec[] materials; }
        static readonly Dictionary<string, Material> Materials = new Dictionary<string, Material>();

        [MenuItem("Fable Realm/Build art review scene")]
        public static void Build()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var catalog = JsonUtility.FromJson<Catalog>(File.ReadAllText(Root + "/room-catalog.json"));
            if (catalog.rooms == null || catalog.rooms.Length != 24) throw new InvalidDataException("Expected 24 room briefs.");
            foreach (var room in catalog.rooms)
                if (AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Models/" + room.hero + ".obj") == null)
                    throw new InvalidDataException("Missing model for " + room.key);
            Directory.CreateDirectory(Output + "/Materials");
            Directory.CreateDirectory(Output + "/Rooms");
            AssetDatabase.Refresh();
            MakeMaterials();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.38f, .42f, .51f);
            RenderSettings.fog = false;
            var sun = new GameObject("Sunset key").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, .79f, .64f);
            sun.intensity = 1.25f;
            sun.transform.rotation = Quaternion.Euler(42,-32,0);
            sun.shadows = LightShadows.Soft;

            for (int i = 0; i < catalog.rooms.Length; i++)
            {
                var room = catalog.rooms[i];
                var root = new GameObject(room.id + "_" + room.key + "_ART_ONLY");
                Compose(root.transform, room, i);
                PrefabUtility.SaveAsPrefabAsset(root, Output + "/Rooms/" + room.id + "_" + room.key + ".prefab");
                root.transform.position = new Vector3((i % 6)*19,0,(i / 6)*19);
            }
            var camera = new GameObject("Art review camera").AddComponent<Camera>();
            camera.transform.position = new Vector3(46,82,-56);
            camera.transform.LookAt(new Vector3(46,0,28));
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.15f,.16f,.23f);
            camera.farClipPlane = 300;
            camera.fieldOfView = 55;
            EditorSceneManager.SaveScene(scene, Output + "/FableRealm_ArtReview.unity");
            AssetDatabase.SaveAssets();
            ValidateGenerated();
            Debug.Log("Fable Realm: created 24 ART review prefabs. Gameplay integration is not implemented.");
        }

        static void MakeMaterials()
        {
            Materials.Clear();
            var manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(Root + "/asset-manifest.json"));
            var shader = Shader.Find("Standard");
            if (shader == null) throw new InvalidOperationException("Built-in Standard shader required.");
            foreach (var spec in manifest.materials)
            {
                string path = Output + "/Materials/" + spec.name + ".mat";
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null) { mat = new Material(shader); AssetDatabase.CreateAsset(mat,path); }
                if (!ColorUtility.TryParseHtmlString(spec.color,out var color)) throw new InvalidDataException(spec.color);
                mat.color=color;
                mat.SetFloat("_Glossiness",spec.smoothness);
                if (spec.name == "Gold") mat.SetFloat("_Metallic",.6f);
                if (spec.name == "Glow" || spec.name == "Glass")
                {
                    mat.EnableKeyword("_EMISSION");
                    mat.SetColor("_EmissionColor",color*.35f);
                }
                EditorUtility.SetDirty(mat);
                Materials.Add(spec.name,mat);
            }
        }

        static void Compose(Transform root, Room room, int index)
        {
            string recipe=Root+"/RoomDefinitions/"+room.key+".room.json";
            if(File.Exists(recipe))
            {
                LevelAuthoring.Editor.RoomCompiler.BuildInto(root,recipe,Output+"/Authored/"+room.key);
                return;
            }
            string ground = room.biome == "Forest" ? "Moss" : room.biome == "Village" ? "Biscuit" : "Stone";
            BuildLayout(root, room, ground);
            // Authored art apertures only; ModulePropSwitch will later close unused connections.
            var portals = new GameObject("Portal guides - no gameplay components").transform;
            portals.SetParent(root,false);
            int[] sides = room.role == "DeadEnd" || room.role == "Extraction" ? new[]{2}
                : room.role == "Passage" ? new[]{0,2} : new[]{0,1,2,3};
            foreach (int side in sides)
            {
                var p = new GameObject("Portal_"+side).transform; p.SetParent(portals,false);
                p.localRotation=Quaternion.Euler(0,side*90,0);
                p.localPosition=p.localRotation*new Vector3(0,0,7.5f);
            }
            for (int side=0;side<4;side++)
            {
                var edge = new GameObject("Boundary_"+side).transform;
                edge.SetParent(root,false); edge.localRotation=Quaternion.Euler(0,side*90,0);
                bool open=Array.IndexOf(sides,side)>=0;
                if (open)
                {
                    Box(edge,"Left",new Vector3(-4.55f,.6f,7.2f),new Vector3(5.9f,1.2f,.4f),ground);
                    Box(edge,"Right",new Vector3(4.55f,.6f,7.2f),new Vector3(5.9f,1.2f,.4f),ground);
                }
                else Box(edge,"Closed",new Vector3(0,.6f,7.2f),new Vector3(15,1.2f,.4f),ground);
            }
            // Landmarks belong to the authored layout; no repeated four-corner dressing.
            Model(root,"Lantern",new Vector3(2.2f,0,-6.7f),.65f,0);
            var notes = new GameObject("BRIEF_"+room.role+"_"+room.title).transform;
            notes.SetParent(root,false);
            if (room.role=="Extraction" || room.role=="Start")
            {
                var reserved = new GameObject("Reserved gameplay area - author extraction or truck here").transform;
                reserved.SetParent(root,false);
                reserved.localPosition = new Vector3(0,0,3.5f);
            }
        }

        static void Box(Transform parent,string name,Vector3 pos,Vector3 size,string material)
        {
            var obj=GameObject.CreatePrimitive(PrimitiveType.Cube);
            obj.name=name; obj.transform.SetParent(parent,false);
            obj.transform.localPosition=pos; obj.transform.localScale=size;
            obj.GetComponent<Renderer>().sharedMaterial=Materials[material];
            obj.isStatic=true;
        }

        static void Model(Transform parent,string name,Vector3 pos,float scale,float yaw)
        {
            var source=AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/Models/"+name+".obj");
            if (source==null) throw new InvalidDataException("Missing OBJ: "+name);
            var obj=(GameObject)PrefabUtility.InstantiatePrefab(source);
            obj.transform.SetParent(parent,false); obj.transform.localPosition=pos;
            obj.transform.localScale=Vector3.one*scale; obj.transform.localRotation=Quaternion.Euler(0,yaw,0);
            foreach(var renderer in obj.GetComponentsInChildren<MeshRenderer>())
            {
                var mats=renderer.sharedMaterials;
                for(int i=0;i<mats.Length;i++)
                    if(mats[i]!=null && Materials.TryGetValue(mats[i].name,out var mat)) mats[i]=mat;
                renderer.sharedMaterials=mats;
                // Static concave mesh collider for art inspection. Simplify before game integration.
                var filter=renderer.GetComponent<MeshFilter>();
                if(filter!=null && filter.sharedMesh!=null)
                    renderer.gameObject.AddComponent<MeshCollider>().sharedMesh=filter.sharedMesh;
                renderer.gameObject.isStatic=true;
            }
        }

        [MenuItem("Fable Realm/Validate generated art")]
        public static void ValidateGenerated()
        {
            var catalog=JsonUtility.FromJson<Catalog>(File.ReadAllText(Root+"/room-catalog.json"));
            int count=0;
            foreach(var room in catalog.rooms)
            {
                var obj=AssetDatabase.LoadAssetAtPath<GameObject>(Output+"/Rooms/"+room.id+"_"+room.key+".prefab");
                if(obj==null) throw new InvalidDataException("Missing room: "+room.key);
                ValidateLayout(obj,room.key);
                string recipe=Root+"/RoomDefinitions/"+room.key+".room.json";
                if(File.Exists(recipe))
                {
                    Debug.Log(LevelAuthoring.Editor.RoomChecks.ValidateGeometry(obj,LevelAuthoring.Editor.RoomCompiler.Read(recipe)));
                    Debug.Log(LevelAuthoring.Editor.RoomNavigation.BakePrefab(obj,LevelAuthoring.Editor.RoomCompiler.Read(recipe),Output+"/Authored/"+room.key));
                }
                foreach(var renderer in obj.GetComponentsInChildren<MeshRenderer>())
                    foreach(var mat in renderer.sharedMaterials)
                        if(mat==null || mat.shader==null || mat.shader.name=="Hidden/InternalErrorShader")
                            throw new InvalidDataException("Missing material in "+room.key);
                count++;
            }
            Debug.Log("Validated "+count+" art prefabs. This does not validate REPO gameplay.");
        }

        [MenuItem("Fable Realm/Render art preview")]
        public static void RenderPreview()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(Output+"/FableRealm_ArtReview.unity");
            var camera=UnityEngine.Object.FindFirstObjectByType<Camera>();
            if(camera==null) throw new InvalidOperationException("Missing review camera.");
            var target=new RenderTexture(1600,1000,24);
            var previous=RenderTexture.active;
            var pixels=new Texture2D(1600,1000,TextureFormat.RGB24,false);
            try
            {
                camera.targetTexture=target;
                camera.Render();
                RenderTexture.active=target;
                pixels.ReadPixels(new Rect(0,0,1600,1000),0,0);
                pixels.Apply();
                File.WriteAllBytes(Output+"/art-review.png",pixels.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture=null; RenderTexture.active=previous;
                target.Release(); UnityEngine.Object.DestroyImmediate(target);
                UnityEngine.Object.DestroyImmediate(pixels);
            }
            AssetDatabase.Refresh();
        }

        public static void BuildAndRender()
        {
            Build();
            RenderPreview();
        }
    }
}
