using System;
using System.Collections;
using System.IO;
using System.Reflection;
using IncreaseHardMod.Cosmetics;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static class CosmeticMotionRegression
{
    private static int checks;
    private static void Check(bool value, string label)
    {
        if (!value) throw new Exception(label);
        checks++;
    }

    public static void Run()
    {
        try
        {
            SkeletonRegression.Check();
            foreach (float dt in new[] { 1f/30, 1f/60, 1f/144 })
            {
                var root = new GameObject("Motion fixture");
                var joint = new GameObject("Tail").transform;
                joint.SetParent(root.transform, false);
                joint.localRotation = Quaternion.Euler(12, 7, 3);
                var rest = joint.localRotation;
                var motion = root.AddComponent<CosmeticMotion>();
                motion.Add(joint, "tail");
                for (int i = 0; i < 4f/dt; i++)
                {
                    root.transform.position = new Vector3(i*dt*4f, 0, 0);
                    root.transform.rotation = Quaternion.Euler(0, i*dt*70f, 0);
                    motion.Step(dt, i*dt);
                    Check(!float.IsNaN(joint.localRotation.w), "Joint remains finite");
                    Check(Quaternion.Angle(rest, joint.localRotation) < 16, "Motion stays bounded and preserves rest pose");
                }
                var paused = joint.localRotation;
                motion.Step(0, 12);
                Check(joint.localRotation == paused, "Pause leaves the pose alone");
                root.transform.position += Vector3.one*100;
                motion.Step(dt, 12);
                Check(Quaternion.Angle(rest, joint.localRotation) < 2, "Teleport discards velocity");
                motion.Step(2f, 14);
                Check(Quaternion.Angle(rest, joint.localRotation) < 9, "Long frames stay bounded");
                Object.DestroyImmediate(root);
            }

            var garment = new GameObject("Cloth fixture");
            var filter = garment.AddComponent<MeshFilter>();
            var source = new Mesh { vertices = new[] { new Vector3(0,.2f,0), new Vector3(1,.2f,0), new Vector3(0,-.5f,0) },
                triangles = new[] { 0,1,2 }, normals = new[] { Vector3.forward,Vector3.forward,Vector3.forward } };
            filter.sharedMesh = source;
            var cloth = garment.AddComponent<CosmeticCloth>();
            cloth.Add(filter, Vector3.zero, .2f);
            var clone = filter.sharedMesh;
            Check(clone != source, "Every cloth instance owns its deformation mesh");
            for (int i=0;i<180;i++)
            {
                garment.transform.position = new Vector3(i*.08f,0,0);
                cloth.Step(1f/60,i/60f);
            }
            Check(clone.vertices[0] == source.vertices[0] && clone.vertices[1] == source.vertices[1], "Attachment seam stays fixed");
            Check(Vector3.Distance(clone.vertices[2],source.vertices[2]) > .001f, "Free hem responds to movement");
            Check(Vector3.Distance(clone.vertices[2],source.vertices[2]) <= .0161f, "Hem has a 16 mm travel limit");
            Check(source.vertices[2] == new Vector3(0,-.5f,0), "Shared source mesh remains untouched");
            foreach (var normal in clone.normals) Check(Mathf.Abs(normal.magnitude-1)<.001f, "Moving normals remain normalized");
            garment.transform.position += Vector3.one*100;
            cloth.Step(1f/60,4);
            Check(Vector3.Distance(clone.vertices[2],source.vertices[2]) < .001f, "Cloth resets on teleport");
            var veilObject = new GameObject("Veil fixture");
            var veilFilter = veilObject.AddComponent<MeshFilter>(); veilFilter.sharedMesh = source;
            var veil = veilObject.AddComponent<CosmeticCloth>(); veil.Add(veilFilter,Vector3.zero,.2f,.045f);
            float travel=0;
            for(int i=0;i<180;i++)
            {
                veil.Step(1f/60,i/60f);
                travel=Mathf.Max(travel,Vector3.Distance(veilFilter.sharedMesh.vertices[2],source.vertices[2]));
                Check(veilFilter.sharedMesh.vertices[0]==source.vertices[0],"Veil crown stays pinned during idle flutter");
            }
            Check(travel>.01f && travel<=.0451f,"Veil flutters visibly even on a stationary menu avatar");
            ((IList)typeof(CosmeticCloth).GetField("_panels",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(veil)).Clear();
            Object.DestroyImmediate(veilFilter.sharedMesh); Object.DestroyImmediate(veilObject);
            // DestroyImmediate in an editor fixture; production uses delayed Destroy during play.
            ((IList)typeof(CosmeticCloth).GetField("_panels",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(cloth)).Clear();
            Object.DestroyImmediate(garment); Object.DestroyImmediate(clone); Object.DestroyImmediate(source);
            string report = "PASS: " + checks + " cosmetic motion checks in Unity " + Application.unityVersion;
            File.WriteAllText(Path.Combine(Application.dataPath,"../cosmetic-motion-result.txt"),report);
            Debug.Log(report); EditorApplication.Exit(0);
        }
        catch(Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
    }
}
