using System;
using System.IO;
using System.Reflection;
using IncreaseHardMod.Valuables;
using UnityEngine;
using Object=UnityEngine.Object;

internal static class SkeletonRegression
{
    internal static void Check()
    {
        var model=JsonUtility.FromJson<ValuableArtModel>(File.ReadAllText(Path.GetFullPath(Path.Combine(Application.dataPath,"../../../src/Art/v09_skeleton.mesh.json"))));
        ValuableVisuals.Overrides["skeleton"]=model;
        var root=new GameObject("Skeleton fixture"); root.SetActive(false);
        root.transform.position=new Vector3(4,2,7);root.transform.rotation=Quaternion.Euler(0,70,0);
        var visual=new GameObject("IHM Valuable Visual").transform;visual.SetParent(root.transform,false);
        visual.localPosition=new Vector3(.1f,.02f,.3f);
        var mat=new Material(Shader.Find("Standard"));
        for(int i=0;i<model.parts.Length;i++)
        {
            var part=model.parts[i];var node=new GameObject("Part"+i.ToString("D3"));node.transform.SetParent(visual,false);node.transform.localPosition=part.rest.position;
            var mesh=new Mesh{vertices=part.vertices,triangles=part.triangles,normals=part.normals};
            node.AddComponent<MeshFilter>().sharedMesh=mesh;node.AddComponent<MeshRenderer>().sharedMaterial=mat;
        }
        var art=root.AddComponent<ValuableArtInstance>();art.assetKey="skeleton";
        var sway=root.AddComponent<SkeletonSway>();
        typeof(SkeletonSway).GetMethod("Start",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(sway,null);
        var torso=visual.Find("Skeleton joint torso");
        if(torso==null || visual.Find("Skeleton suspension bracket")==null)throw new Exception("Skeleton lacks a stand attachment");
        var anchor=torso.localPosition;float movement=0;
        for(int i=0;i<180;i++)
        {
            root.transform.position+=new Vector3(Mathf.Sin(i*.08f)*.02f,0,0);
            root.transform.rotation=Quaternion.Euler(0,70+i*.8f,0);sway.Step(1f/60);
            if(Vector3.Distance(torso.localPosition,anchor)>.0001f)throw new Exception("Suspension drifted from stand");
            foreach(var b in art.bindings)
                if(b.group=="hand_left")movement=Mathf.Max(movement,Quaternion.Angle(b.node.parent.localRotation,Quaternion.identity));
        }
        if(movement<1)throw new Exception("Skeleton hands do not respond to stand movement");
        foreach(var f in root.GetComponentsInChildren<MeshFilter>(true))if(f.name.StartsWith("Part"))Object.DestroyImmediate(f.sharedMesh);
        Object.DestroyImmediate(root);Object.DestroyImmediate(mat);ValuableVisuals.Overrides.Remove("skeleton");
        Debug.Log("PASS skeleton: exported bone hierarchy moves and suspension stays on stand");
    }
}
