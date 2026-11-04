using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace LevelAuthoring.Editor
{
    public static class RoomStudio
    {
        [MenuItem("Level Authoring/Build selected room recipe")]
        public static void BuildSelected()
        {
            string path=AssetDatabase.GetAssetPath(Selection.activeObject);
            if(!path.EndsWith(".room.json",StringComparison.Ordinal)) throw new InvalidOperationException("Select a .room.json asset first.");
            if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Build(path,"Assets/LevelAuthoringOutput/"+RoomCompiler.Read(path).id);
        }

        public static void Build(string recipePath,string output)
        {
            var definition=RoomCompiler.Read(recipePath);
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            RenderSettings.ambientMode=AmbientMode.Flat;
            RenderSettings.ambientLight=new Color(.19f,.22f,.27f);
            RenderSettings.fog=false;
            QualitySettings.pixelLightCount=12;
            var root=new GameObject(definition.id);
            RoomCompiler.BuildInto(root.transform,recipePath,output);
            var prefab=PrefabUtility.SaveAsPrefabAsset(root,output+"/"+definition.id+".prefab");
            string report=RoomChecks.ValidateGeometry(prefab,definition);
            File.WriteAllText(output+"/validation.txt",report+"\n");
            Debug.Log(report);
            string navigationReport=RoomNavigation.BakeAndValidate(root,definition,output);
            File.WriteAllText(output+"/navigation-validation.txt",navigationReport+"\n");
            Debug.Log(navigationReport);
            var camera=new GameObject("Player-height review camera").AddComponent<Camera>();
            camera.clearFlags=CameraClearFlags.SolidColor;
            camera.backgroundColor=new Color(.025f,.035f,.045f);
            camera.nearClipPlane=.05f; camera.farClipPlane=80;
            if(definition.views.Length>0) Position(camera,definition.views[0]);
            EditorSceneManager.SaveScene(scene,output+"/"+definition.id+".unity");
            foreach(var view in definition.views)
            {
                Position(camera,view);
                Render(camera,output+"/"+view.id+".png");
            }
            if(definition.navigation!=null) RenderNavigation(root,camera,output);
            if(definition.views.Length>0) Position(camera,definition.views[0]);
            AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
        }

        static void Position(Camera camera,ViewDefinition view)
        {
            camera.transform.position=view.position; camera.transform.LookAt(view.target);
            camera.fieldOfView=view.fieldOfView;
        }

        static void RenderNavigation(GameObject room,Camera camera,string output)
        {
            var overlay=new GameObject("Navigation review only");
            var surface=new Material(Shader.Find("Unlit/Color")) { color=new Color(.17f,.64f,.72f) };
            var routeMaterial=new Material(Shader.Find("Unlit/Color")) { color=new Color(1,.8f,.3f) };
            bool active=room.activeSelf;
            try
            {
                room.SetActive(false);
                overlay.AddComponent<MeshFilter>().sharedMesh=AssetDatabase.LoadAssetAtPath<Mesh>(output+"/navigation-preview.asset");
                overlay.AddComponent<MeshRenderer>().sharedMaterial=surface;
                var report=JsonUtility.FromJson<RoomNavigation.Report>(File.ReadAllText(output+"/navigation-report.json"));
                foreach(var route in report.routes)
                {
                    var line=new GameObject(route.from+" -> "+route.to).AddComponent<LineRenderer>();
                    line.transform.SetParent(overlay.transform,false); line.sharedMaterial=routeMaterial;
                    line.startWidth=.065f; line.endWidth=.065f; line.positionCount=route.corners.Length;
                    var corners=(Vector3[])route.corners.Clone();
                    for(int i=0;i<corners.Length;i++) corners[i]+=Vector3.up*.08f;
                    line.SetPositions(corners);
                }
                camera.transform.position=new Vector3(17,21,-22); camera.transform.LookAt(new Vector3(0,1,0)); camera.fieldOfView=45;
                Render(camera,output+"/navigation.png");
            }
            finally
            {
                room.SetActive(active); UnityEngine.Object.DestroyImmediate(overlay);
                UnityEngine.Object.DestroyImmediate(surface); UnityEngine.Object.DestroyImmediate(routeMaterial);
            }
        }

        static void Render(Camera camera,string path)
        {
            var target=new RenderTexture(1600,1000,24);
            var old=RenderTexture.active;
            var image=new Texture2D(1600,1000,TextureFormat.RGB24,false);
            try
            {
                camera.targetTexture=target; camera.Render(); RenderTexture.active=target;
                image.ReadPixels(new Rect(0,0,1600,1000),0,0); image.Apply();
                File.WriteAllBytes(path,image.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture=null; RenderTexture.active=old; target.Release();
                UnityEngine.Object.DestroyImmediate(target); UnityEngine.Object.DestroyImmediate(image);
            }
        }
    }
}
