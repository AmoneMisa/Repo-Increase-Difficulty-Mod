using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// Renders the real IHM/Ice shader on a stand-in monster, the way the mod applies it (a twin of each body
// renderer with the ice material), to art/cosmetics/ice_shader.png.
// Unity.exe -batchmode -quit -projectPath tools/IceShaderBundle -executeMethod RenderIcePreview.Render
public static class RenderIcePreview
{
    public static void Render()
    {
        var shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Shaders/IHMIce.shader");
        var root = new GameObject("Preview");
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(.08f, .09f, .12f);
        RenderSettings.skybox = null;

        var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
        floor.transform.SetParent(root.transform); floor.transform.localScale = Vector3.one * 2f;
        floor.GetComponent<Renderer>().sharedMaterial = Lit(new Color(.12f, .11f, .11f), .35f);
        var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.transform.SetParent(root.transform); wall.transform.position = new Vector3(0, 2, 2.2f); wall.transform.localScale = new Vector3(8, 4, .2f);
        wall.GetComponent<Renderer>().sharedMaterial = Lit(new Color(.2f, .17f, .15f), .2f);

        // Stand-in monster, already wearing the mod's frost-blue coat.
        var coat = Lit(new Color(.32f, .55f, .8f), .7f);
        var body = new GameObject("Monster").transform; body.SetParent(root.transform);
        Part(body, PrimitiveType.Sphere, new Vector3(0, .62f, 0), new Vector3(.72f, 1f, .6f), Vector3.zero, coat);
        Part(body, PrimitiveType.Sphere, new Vector3(0, 1.22f, -.05f), Vector3.one * .48f, Vector3.zero, coat);
        Part(body, PrimitiveType.Capsule, new Vector3(-.42f, .78f, 0), new Vector3(.16f, .32f, .16f), new Vector3(0, 0, -25), coat);
        Part(body, PrimitiveType.Capsule, new Vector3(.42f, .78f, 0), new Vector3(.16f, .32f, .16f), new Vector3(0, 0, 25), coat);
        Part(body, PrimitiveType.Capsule, new Vector3(-.16f, .17f, 0), new Vector3(.18f, .17f, .18f), Vector3.zero, coat);
        Part(body, PrimitiveType.Capsule, new Vector3(.16f, .17f, 0), new Vector3(.18f, .17f, .18f), Vector3.zero, coat);

        // The mod's parameters for a monster this size (IceBlock.Wrap / IceBlock.Layers): radii ~ (0.7, 1.0, 0.47).
        float size = new Vector3(.7f, 1f, .47f).magnitude;
        float inflate = Mathf.Clamp(size * .05f, .03f, .12f), lumps = Mathf.Clamp(size * .04f, .025f, .1f);
        float[][] layers =
        {
            new[] { 1f,   1f,   0f,   1f,   .5f,  .7f, .045f, .35f },
            new[] { 1.5f, 1.3f, 1.2f, .22f, .45f, .35f, .03f, .25f },
            new[] { 2f,   1.6f, 2.2f, .14f, .55f, .2f, .02f,  1f   },
        };
        var crackTexture = CrackTexture();
        var sources = body.GetComponentsInChildren<MeshRenderer>();
        for (int layer = 0; layer < layers.Length; layer++)
        {
            var spec = layers[layer];
            var ice = new Material(shader) { renderQueue = 3020 + layer };
            ice.SetTexture("_CrackTex", crackTexture);
            ice.SetFloat("_Inflate", inflate * spec[0]); ice.SetFloat("_Lumps", lumps * spec[1]); ice.SetFloat("_Growth", lumps * spec[2]);
            ice.SetFloat("_Opacity", spec[3]); ice.SetFloat("_Frost", spec[4]); ice.SetFloat("_Bubbles", spec[5]);
            ice.SetFloat("_Refraction", spec[6]); ice.SetFloat("_RimStrength", spec[7]); ice.SetFloat("_UseGrab", layer == 0 ? 1f : 0f);
            ice.SetFloat("_ChipScale", Mathf.Clamp(9f / size, 3f, 14f));
            ice.SetFloat("_Tiling", Mathf.Clamp(2.6f / size, 1f, 5f));
            ice.SetFloat("_BubbleScale", Mathf.Clamp(14f / size, 6f, 24f));
            ice.SetFloat("_Base", 0f); ice.SetFloat("_Height", 1.46f);
            foreach (var r in sources)
            {
                var twin = new GameObject("Ice Twin " + layer);
                twin.transform.SetParent(r.transform, false);
                twin.AddComponent<MeshFilter>().sharedMesh = r.GetComponent<MeshFilter>().sharedMesh;
                twin.AddComponent<MeshRenderer>().sharedMaterial = ice;
            }
        }
        Light(LightType.Directional, new Vector3(50, -30, 0), Vector3.zero, new Color(.75f, .8f, .95f), .7f, 0);
        Light(LightType.Point, Vector3.zero, new Vector3(0, .8f, -.3f), new Color(.5f, .8f, 1f), 1.6f, 3f);   // the cold light
        Light(LightType.Point, Vector3.zero, new Vector3(-1.6f, 1.8f, -1.2f), new Color(1f, .45f, .85f), 2.2f, 5f); // rim
        Light(LightType.Point, Vector3.zero, new Vector3(1.8f, 2.2f, -1.6f), new Color(.85f, .9f, 1f), 2.5f, 6f);

        // A reflection probe so the ice has something to reflect, like a real level.
        var probe = new GameObject("Probe").AddComponent<ReflectionProbe>();
        probe.transform.position = new Vector3(0, 1f, -1f); probe.size = Vector3.one * 10f;
        probe.mode = ReflectionProbeMode.Realtime; probe.refreshMode = ReflectionProbeRefreshMode.ViaScripting;
        probe.RenderProbe();

        var cam = new GameObject("Camera").AddComponent<Camera>();
        cam.transform.position = new Vector3(1.1f, 1.45f, -2.9f);
        cam.transform.LookAt(new Vector3(0, .72f, 0));
        cam.fieldOfView = 40; cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(.02f, .025f, .035f);
        cam.allowHDR = false; cam.allowMSAA = true;
        var rt = new RenderTexture(1200, 900, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
        cam.targetTexture = rt;
        cam.Render();
        RenderTexture.active = rt;
        var shot = new Texture2D(1200, 900, TextureFormat.RGB24, false);
        shot.ReadPixels(new Rect(0, 0, 1200, 900), 0, 0); shot.Apply();
        string target = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "..", "art", "cosmetics", "ice_shader.png"));
        File.WriteAllBytes(target, shot.EncodeToPNG());
        Debug.Log("[IcePreview] Wrote " + target + " on " + SystemInfo.graphicsDeviceType);
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

    private static void Light(LightType type, Vector3 euler, Vector3 pos, Color c, float intensity, float range)
    {
        var l = new GameObject("Light").AddComponent<Light>();
        l.type = type; l.color = c; l.intensity = intensity; if (range > 0) l.range = range;
        l.transform.position = pos; l.transform.eulerAngles = euler;
    }

    // Copy of IceBlock.CrackTexture (keep in sync).
    private static Texture2D CrackTexture()
    {
        const int size = 256;
        var random = new System.Random(9137);
        var bold = Seeds(random, 11); var fine = Seeds(random, 34);
        var pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            var p = new Vector2((x + .5f) / size, (y + .5f) / size);
            float crack = Mathf.Exp(-Mathf.Pow(Edge(p, bold) / .006f, 2f));
            float hair = Mathf.Exp(-Mathf.Pow(Edge(p, fine) / .003f, 2f)) * .55f;
            float frost = Mathf.SmoothStep(.35f, .8f, Fbm(p * 4f, 4)) * .42f + .16f;
            float a = Mathf.Clamp01(frost + crack * .8f + hair * .6f);
            float bright = Mathf.Clamp01(.75f + crack * .25f);
            pixels[y * size + x] = new Color(bright, bright, 1f, a);
        }
        var t = new Texture2D(size, size, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear };
        t.SetPixels(pixels); t.Apply(true);
        return t;
    }

    private static Vector2[] Seeds(System.Random r, int n) { var s = new Vector2[n]; for (int i = 0; i < n; i++) s[i] = new Vector2((float)r.NextDouble(), (float)r.NextDouble()); return s; }

    private static float Edge(Vector2 p, Vector2[] seeds)
    {
        float d1 = float.MaxValue, d2 = float.MaxValue;
        foreach (var s in seeds)
            for (int ox = -1; ox <= 1; ox++)
            for (int oy = -1; oy <= 1; oy++)
            {
                float d = (p - (s + new Vector2(ox, oy))).magnitude;
                if (d < d1) { d2 = d1; d1 = d; } else if (d < d2) d2 = d;
            }
        return (d2 - d1) * .5f;
    }

    private static float Fbm(Vector2 p, int octaves)
    {
        float sum = 0f, amp = .5f, norm = 0f;
        for (int i = 0; i < octaves; i++) { sum += Mathf.PerlinNoise(p.x * (1 << i) + 17.3f * i, p.y * (1 << i) + 9.1f * i) * amp; norm += amp; amp *= .5f; }
        return sum / norm;
    }
}
