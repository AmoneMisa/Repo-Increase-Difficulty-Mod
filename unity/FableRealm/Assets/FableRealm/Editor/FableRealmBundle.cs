using System;
using System.Collections.Generic;
using System.IO;
using LevelAuthoring.Editor;
using UnityEditor;
using UnityEngine;

namespace FableRealm.Editor
{
    /// <summary>
    /// Packs the built room prefabs into the bundle the mod loads (fablerealm.bundle, next to the plugin DLL).
    /// The bundle holds geometry, colliders, lights, materials and the anchor markers only. It contains no game
    /// scripts: the mod adds Module, LevelPoint, RoomVolume, ValuableVolume and the rest from the recipe contract
    /// (src/FableRealm/fable_rooms.json), so this project needs neither the game's assemblies nor the REPOLib SDK.
    /// Batch: Unity.exe -batchmode -quit -projectPath &lt;project&gt; -executeMethod FableRealm.Editor.FableRealmBundle.Build
    /// </summary>
    public static class FableRealmBundle
    {
        public const string BundleName="fablerealm.bundle";

        [MenuItem("Fable Realm/Build game bundle")]
        public static void Build()
        {
            var set=JsonUtility.FromJson<RoomSetDefinition>(File.ReadAllText(FableRealmLevel.Set));
            if(set==null || set.rooms==null || set.rooms.Length==0) throw new InvalidDataException("Invalid level room set.");
            var assets=new List<string>(); var names=new List<string>();
            foreach(var entry in set.rooms)
            {
                string id=Path.GetFileName(entry.recipe).Replace(".room.json","");
                string prefab=FableRealmLevel.Output+"/"+id+"/"+id+".prefab";
                if(AssetDatabase.LoadAssetAtPath<GameObject>(prefab)==null)
                    throw new InvalidOperationException("Build the level room set first, missing prefab: "+prefab);
                assets.Add(prefab); names.Add(id);
            }
            int shared=ShareTextures(assets.ToArray());
            int sharedMeshes=ShareMeshes(assets.ToArray());
            Debug.Log("FABLE BUNDLE: "+sharedMeshes+" duplicate mesh references shared.");
            string output=Path.GetFullPath(Path.Combine(Application.dataPath,"..","Build"));
            Directory.CreateDirectory(output);
            var build=new AssetBundleBuild { assetBundleName=BundleName,assetNames=assets.ToArray(),addressableNames=names.ToArray() };
            // LZ4 chunks: the game opens the file directly and loads one room at a time.
            var manifest=BuildPipeline.BuildAssetBundles(output,new[]{build},
                BuildAssetBundleOptions.ForceRebuildAssetBundle|BuildAssetBundleOptions.StrictMode|BuildAssetBundleOptions.ChunkBasedCompression,
                BuildTarget.StandaloneWindows64);
            if(manifest==null) throw new InvalidOperationException("Bundle build failed.");
            string built=Path.Combine(output,BundleName);
            // FABLE_BUNDLE_OUT: where the mod keeps the bundle (default: this repository's src/FableRealm).
            string target=Environment.GetEnvironmentVariable("FABLE_BUNDLE_OUT");
            if(string.IsNullOrEmpty(target)) target=Path.Combine(ModRoot(),"src","FableRealm",BundleName);
            Directory.CreateDirectory(Path.GetDirectoryName(target));
            File.Copy(built,target,true);
            Debug.Log("FABLE BUNDLE: "+names.Count+" rooms, "+shared+" duplicate textures shared, "+new FileInfo(target).Length+" bytes, Unity "+Application.unityVersion+" -> "+target);
            Verify(target,names);
        }

        // Opens the finished file the way the mod does and loads every room by the name the mod asks for.
        static void Verify(string path,List<string> names)
        {
            var bundle=AssetBundle.LoadFromFile(path);
            if(bundle==null) throw new InvalidOperationException("The built bundle does not open: "+path);
            try
            {
                foreach(string name in names)
                {
                    var room=bundle.LoadAsset<GameObject>(name);
                    if(room==null) throw new InvalidOperationException("Room is not loadable by name: "+name);
                    if(room.transform.Find("Architecture")==null || room.transform.Find("Integration anchors - not game components")==null)
                        throw new InvalidOperationException("Room lacks its architecture or anchor markers: "+name);
                }
                Debug.Log("FABLE BUNDLE VERIFIED: "+names.Count+" rooms load by name ("+bundle.GetAllAssetNames().Length+" assets).");
            }
            finally { bundle.Unload(true); }
        }

        // The mod repository: the nearest folder above this project that holds the plugin's .csproj.
        static string ModRoot()
        {
            for(var dir=new DirectoryInfo(Application.dataPath).Parent;dir!=null;dir=dir.Parent)
                if(File.Exists(Path.Combine(dir.FullName,"IncreaseDifficultyMod.csproj"))) return dir.FullName;
            throw new DirectoryNotFoundException("Mod repository not found above "+Application.dataPath+"; set FABLE_BUNDLE_OUT.");
        }

        // Every room writes its own copy of a surface tile, although a material id always gets the same pixels.
        // Point all materials at one copy per distinct picture, so the bundle stores each tile once.
        static int ShareTextures(string[] prefabs)
        {
            var canonical=new Dictionary<string,Texture2D>(); int shared=0;
            foreach(string path in AssetDatabase.GetDependencies(prefabs,true))
            {
                if(!path.EndsWith(".mat",StringComparison.Ordinal)) continue;
                var material=AssetDatabase.LoadAssetAtPath<Material>(path);
                var texture=material!=null?material.mainTexture as Texture2D:null;
                if(texture==null || !texture.isReadable) continue;
                string key=texture.width+"x"+texture.height+":"+texture.format+":"+Hash128.Compute(texture.GetRawTextureData()).ToString();
                if(!canonical.TryGetValue(key,out var first)) { canonical.Add(key,texture); continue; }
                if(first==texture) continue;
                material.mainTexture=first; EditorUtility.SetDirty(material); shared++;
            }
            if(shared>0) AssetDatabase.SaveAssets();
            return shared;
        }

        // Every room writes its own mesh assets, although many are the same shape: a room's second layout repeats its
        // shell and hero pieces, and the kit's lanterns, plates and trims recur across rooms. Point every renderer and
        // collider at one asset per distinct mesh, so the bundle stores each shape once.
        static int ShareMeshes(string[] prefabs)
        {
            var canonical=new Dictionary<Hash128,Mesh>(); int shared=0;
            Mesh Canonical(Mesh mesh)
            {
                if(mesh==null || !mesh.isReadable || !AssetDatabase.Contains(mesh)) return mesh;
                var hash=new Hash128();
                hash.Append(mesh.vertexCount); hash.Append(mesh.subMeshCount);
                hash.Append(mesh.vertices); hash.Append(mesh.normals); hash.Append(mesh.uv); hash.Append(mesh.colors32);
                for(int i=0;i<mesh.subMeshCount;i++) hash.Append(mesh.GetIndices(i));
                if(canonical.TryGetValue(hash,out var first)) return first;
                canonical.Add(hash,mesh); return mesh;
            }
            foreach(string path in prefabs)
            {
                var root=PrefabUtility.LoadPrefabContents(path); bool changed=false;
                try
                {
                    foreach(var filter in root.GetComponentsInChildren<MeshFilter>(true))
                    {
                        var mesh=Canonical(filter.sharedMesh);
                        if(mesh!=filter.sharedMesh) { filter.sharedMesh=mesh; changed=true; shared++; }
                    }
                    foreach(var collider in root.GetComponentsInChildren<MeshCollider>(true))
                    {
                        var mesh=Canonical(collider.sharedMesh);
                        if(mesh!=collider.sharedMesh) { collider.sharedMesh=mesh; changed=true; shared++; }
                    }
                    if(changed) PrefabUtility.SaveAsPrefabAsset(root,path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            return shared;
        }
    }
}
