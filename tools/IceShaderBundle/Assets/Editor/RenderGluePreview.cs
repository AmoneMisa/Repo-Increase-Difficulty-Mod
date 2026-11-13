using System.IO;
using IncreaseHardMod.Visuals;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// Renders the real IHM/Glue shader the way the mod uses it: a stuck stand-in monster wearing the gel coat (twins of
// its renderers), the puddle and the strands from GlueMesh, under dim room light plus a flashlight-like spot.
// Unity.exe -batchmode -quit -projectPath tools/IceShaderBundle -executeMethod RenderGluePreview.Render
public static class RenderGluePreview
{
    public static void Render()
    {
        var shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Shaders/IHMGlue.shader");
        var root = new GameObject("Preview");
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(.09f, .09f, .11f);
        RenderSettings.skybox = null;

        var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
        floor.transform.localScale = Vector3.one * 2f;
        floor.GetComponent<Renderer>().sharedMaterial = Lit(new Color(.16f, .14f, .13f), .3f);
        var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.transform.position = new Vector3(0, 2, 2.2f); wall.transform.localScale = new Vector3(8, 4, .2f);
        wall.GetComponent<Renderer>().sharedMaterial = Lit(new Color(.22f, .19f, .17f), .2f);

        var coat = Lit(new Color(.42f, .45f, .4f), .35f);
        var body = new GameObject("Monster").transform; body.SetParent(root.transform);
        Part(body, PrimitiveType.Sphere, new Vector3(0, .82f, 0), new Vector3(.72f, 1f, .6f), Vector3.zero, coat);
        Part(body, PrimitiveType.Sphere, new Vector3(0, 1.42f, -.05f), Vector3.one * .48f, Vector3.zero, coat);
        Part(body, PrimitiveType.Capsule, new Vector3(-.42f, .98f, 0), new Vector3(.16f, .32f, .16f), new Vector3(0, 0, -25), coat);
        Part(body, PrimitiveType.Capsule, new Vector3(.42f, .98f, 0), new Vector3(.16f, .32f, .16f), new Vector3(0, 0, 25), coat);
        Part(body, PrimitiveType.Capsule, new Vector3(-.17f, .3f, 0), new Vector3(.2f, .3f, .2f), Vector3.zero, coat);
        Part(body, PrimitiveType.Capsule, new Vector3(.17f, .3f, 0), new Vector3(.2f, .3f, .2f), Vector3.zero, coat);
        var bounds = new Bounds(new Vector3(0, .85f, 0), new Vector3(1.1f, 1.7f, .6f));

        // Coat, as GlueBlob.Coat sets it up.
        float size = bounds.extents.magnitude;
        var gel = new Material(shader) { renderQueue = 3016 };
        gel.SetFloat("_Coat", 1f); gel.SetFloat("_Base", bounds.min.y); gel.SetFloat("_Height", bounds.size.y);
        gel.SetFloat("_Coverage", .34f); gel.SetFloat("_Inflate", Mathf.Clamp(size * .02f, .012f, .045f));
        gel.SetFloat("_Scale", Mathf.Clamp(9f / size, 4f, 16f));
        foreach (var r in body.GetComponentsInChildren<MeshRenderer>())
        {
            var twin = new GameObject("Glue Twin");
            twin.transform.SetParent(r.transform, false);
            twin.AddComponent<MeshFilter>().sharedMesh = r.GetComponent<MeshFilter>().sharedMesh;
            twin.AddComponent<MeshRenderer>().sharedMaterial = gel;
        }

        // Puddle and strands, as GlueBlob.Build makes them.
        var puddleMaterial = new Material(shader);
        puddleMaterial.SetFloat("_Scale", 3.5f); puddleMaterial.SetFloat("_Inflate", .004f); puddleMaterial.SetFloat("_Wobble", .003f);
        var puddle = new GameObject("Puddle");
        puddle.transform.position = new Vector3(0, .006f, 0);
        puddle.AddComponent<MeshFilter>().sharedMesh = GlueMesh.Puddle(4242, 1.0f);
        puddle.AddComponent<MeshRenderer>().sharedMaterial = puddleMaterial;
        var random = new System.Random(7);
        for (int i = 0; i < 5; i++)
        {
            float angle = (i + .2f) * Mathf.PI * 2f / 5;
            var dir = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
            var foot = dir * (.45f + (float)random.NextDouble() * .35f);
            var top = new Vector3(dir.x * .55f * .7f, bounds.min.y + bounds.size.y * (.12f + (float)random.NextDouble() * .2f), dir.z * .3f * .7f);
            var mesh = GlueMesh.NewStrand();
            GlueMesh.Strand(mesh, new Vector3[(GlueMesh.StrandSegments + 1) * GlueMesh.StrandSides], foot, top, .03f, .16f);
            var strand = new GameObject("Strand");
            strand.AddComponent<MeshFilter>().sharedMesh = mesh;
            strand.AddComponent<MeshRenderer>().sharedMaterial = puddleMaterial;
        }

        Light(LightType.Directional, new Vector3(50, -30, 0), Vector3.zero, new Color(.7f, .72f, .8f), .35f, 0, 0);
        Light(LightType.Spot, Vector3.zero, new Vector3(1.2f, 1.9f, -2.4f), new Color(1f, .95f, .85f), 3.2f, 8f, 45f, new Vector3(0, .5f, 0));
        Light(LightType.Point, Vector3.zero, new Vector3(-1.6f, 1.6f, -1f), new Color(.6f, .7f, 1f), 1.4f, 5f, 0);

        var probe = new GameObject("Probe").AddComponent<ReflectionProbe>();
        probe.transform.position = new Vector3(0, 1f, -1f); probe.size = Vector3.one * 10f;
        probe.mode = ReflectionProbeMode.Realtime; probe.refreshMode = ReflectionProbeRefreshMode.ViaScripting;
        probe.RenderProbe();

        var cam = new GameObject("Camera").AddComponent<Camera>();
        cam.transform.position = new Vector3(1.5f, 1.7f, -3.0f);
        cam.transform.LookAt(new Vector3(0, .5f, 0));
        cam.fieldOfView = 40; cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(.02f, .02f, .03f);
        cam.allowHDR = false; cam.allowMSAA = true;
        var rt = new RenderTexture(1200, 900, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
        cam.targetTexture = rt;
        cam.Render();
        RenderTexture.active = rt;
        var shot = new Texture2D(1200, 900, TextureFormat.RGB24, false);
        shot.ReadPixels(new Rect(0, 0, 1200, 900), 0, 0); shot.Apply();
        string target = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "..", "art", "gear15", "glue_shader.png"));
        Directory.CreateDirectory(Path.GetDirectoryName(target));
        File.WriteAllBytes(target, shot.EncodeToPNG());
        Debug.Log("[GluePreview] Wrote " + target + " on " + SystemInfo.graphicsDeviceType);
    }

    private static Material Lit(Color c, float smooth)
    {
        var m = new Material(Shader.Find("Standard")) { color = c };
        m.SetFloat("_Glossiness", smooth);
        return m;
    }

    private static void Part(Transform parent, PrimitiveType type, Vector3 pos, Vector3 scale, Vector3 euler, Material m)
    {
        var go = GameObject.CreatePrimitive(type);
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.transform.SetParent(parent); go.transform.position = pos; go.transform.localScale = scale; go.transform.eulerAngles = euler;
        go.GetComponent<Renderer>().sharedMaterial = m;
    }

    private static void Light(LightType type, Vector3 euler, Vector3 pos, Color c, float intensity, float range, float angle, Vector3? look = null)
    {
        var l = new GameObject("Light").AddComponent<Light>();
        l.type = type; l.color = c; l.intensity = intensity; if (range > 0) l.range = range; if (angle > 0) l.spotAngle = angle;
        l.transform.position = pos; l.transform.eulerAngles = euler;
        if (look.HasValue) l.transform.LookAt(look.Value);
    }
}
