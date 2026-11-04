using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace LevelAuthoring.Editor
{
    [Serializable] public sealed class RoomSetDefinition
    {
        public int schemaVersion=1;
        public string id, title;
        public RoomSetEntry[] rooms;
    }
    [Serializable] public sealed class RoomSetEntry
    {
        public string recipe, role;
        public int difficulty=1;
    }

    public static class RoomSetCompiler
    {
        [MenuItem("Level Authoring/Build selected room set")]
        public static void BuildSelected()
        {
            string path=AssetDatabase.GetAssetPath(Selection.activeObject);
            if(!path.EndsWith(".room-set.json",StringComparison.Ordinal)) throw new InvalidOperationException("Select a .room-set.json asset first.");
            if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var spec=Read(path);
            Build(path,"Assets/LevelAuthoringOutput/"+spec.id);
        }

        static RoomSetDefinition Read(string path)
        {
            var set=JsonUtility.FromJson<RoomSetDefinition>(File.ReadAllText(path));
            if(set==null || set.schemaVersion!=1 || string.IsNullOrEmpty(set.id) || !System.Text.RegularExpressions.Regex.IsMatch(set.id,"^[A-Za-z0-9_-]+$") || set.rooms==null || set.rooms.Length==0)
                throw new InvalidDataException("Invalid room set.");
            var ids=new HashSet<string>();
            foreach(var entry in set.rooms)
            {
                if(string.IsNullOrEmpty(entry.recipe) || !entry.recipe.StartsWith("Assets/",StringComparison.Ordinal) || entry.recipe.Contains(".."))
                    throw new InvalidDataException("Recipe must be inside Assets.");
                if(Array.IndexOf(new[]{"Start","Normal","Passage","DeadEnd","Extraction"},entry.role)<0 || entry.difficulty<1 || entry.difficulty>3)
                    throw new InvalidDataException("Invalid role or difficulty: "+entry.recipe);
                if(!ids.Add(RoomCompiler.Read(entry.recipe).id)) throw new InvalidDataException("Duplicate room id.");
            }
            return set;
        }

        public static void Build(string path,string output)
        {
            var set=Read(path); // Validate the complete set before writing any room output.
            foreach(var entry in set.rooms)
            {
                var definition=RoomCompiler.Read(entry.recipe);
                RoomStudio.Build(entry.recipe,output+"/"+definition.id);
            }
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            for(int i=0;i<set.rooms.Length;i++)
            {
                var definition=RoomCompiler.Read(set.rooms[i].recipe);
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(output+"/"+definition.id+"/"+definition.id+".prefab");
                var obj=(GameObject)PrefabUtility.InstantiatePrefab(prefab);
                // Each recipe can use different bounds; never overlap rooms in the review strip.
                float x=0;
                for(int j=0;j<i;j++) x+=RoomCompiler.Read(set.rooms[j].recipe).bounds.x+4;
                obj.transform.position=new Vector3(x+definition.bounds.x/2,0,0);
            }
            EditorSceneManager.SaveScene(scene,output+"/"+set.id+"-review.unity");
            File.WriteAllText(output+"/"+set.id+"-room-set.json",JsonUtility.ToJson(set,true));
            File.WriteAllText(output+"/"+set.id+"-status.txt",
                "AUTHORING ONLY: prefabs, review scenes, renderings and validation reports. This is not a registered playable level or a REPOLib bundle. Roles and difficulty are integration metadata.\n");
            AssetDatabase.Refresh();
        }
    }
}
