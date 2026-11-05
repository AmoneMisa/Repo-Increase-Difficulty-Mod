using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// Preview of the Wizard Staff hit (WizardStaff bolt + forks, LightningImpact arcs/sparks/flash), same shapes,
// widths and colours as the mod, rendered by Unity to art/cosmetics/staff_lightning.png.
public static class RenderLightningPreview
{
    static readonly Color Core = new(.85f, .93f, 1f), Glow = new(.35f, .62f, 1f);
    static Material _bolt;

    public static void Render()
    {
        Random.InitState(7);
        RenderSettings.ambientMode = AmbientMode.Flat; RenderSettings.ambientLight = new Color(.06f, .06f, .08f); RenderSettings.skybox = null;
        var floor = GameObject.CreatePrimitive(PrimitiveType.Plane); floor.transform.localScale = Vector3.one * 2f;
        floor.GetComponent<Renderer>().sharedMaterial = Lit(new Color(.13f, .12f, .11f), .3f);
        var wall = GameObject.CreatePrimitive(PrimitiveType.Cube); wall.transform.position = new Vector3(0, 2, 2.2f); wall.transform.localScale = new Vector3(8, 4, .2f);
        wall.GetComponent<Renderer>().sharedMaterial = Lit(new Color(.22f, .18f, .15f), .15f);
        var skin = Lit(new Color(.38f, .24f, .18f), .3f);
        var body = new Bounds(new Vector3(.6f, .72f, 0), new Vector3(.75f, 1.45f, .6f));
        Part(PrimitiveType.Sphere, new Vector3(.6f, .62f, 0), new Vector3(.72f, 1f, .6f), skin);
        Part(PrimitiveType.Sphere, new Vector3(.6f, 1.22f, -.05f), Vector3.one * .48f, skin);

        _bolt = new Material(Shader.Find("Legacy Shaders/Particles/Additive") ?? Shader.Find("Sprites/Default")) { mainTexture = Beam() };
        var tip = new Vector3(-1.5f, 1.15f, -.6f);
        var hit = new Vector3(.6f, .8f, -.25f);
        // Main bolt: glow + core, then three forks off the core.
        var core = Line(Core, 1f, .03f); var glow = Line(Glow, .45f, .15f);
        Jag(core, tip, hit, .06f); Jag(glow, tip, hit, .075f);
        for (int i = 0; i < 3; i++) { var f = Line(Core, .75f, .02f, fadeEnd: true); Fork(f, core); }
        // Impact: crawling arcs over the body.
        for (int i = 0; i < 6; i++)
        {
            bool isGlow = i % 2 == 1;
            var arc = Line(isGlow ? Glow : Core, isGlow ? .38f : 1f, isGlow ? .11f : .022f);
            var a = i == 0 ? hit : Surface(body); var b = Surface(body);
            var bulge = ((a + b) * .5f - body.center).normalized * body.extents.magnitude * .25f;
            arc.positionCount = 15;
            float jitter = Mathf.Clamp(body.extents.magnitude * .12f, .03f, .18f);
            for (int k = 0; k < 15; k++) { float t = k / 14f; var p = Vector3.Lerp(a, b, t) + bulge * Mathf.Sin(t * Mathf.PI); if (k > 0 && k < 14) p += Random.insideUnitSphere * jitter; arc.SetPosition(k, p); }
        }
        // Flash + sparks + lights.
        var flash = GameObject.CreatePrimitive(PrimitiveType.Quad); Object.DestroyImmediate(flash.GetComponent<Collider>());
        flash.transform.position = hit; flash.transform.localScale = Vector3.one * .8f;
        var fm = new Material(_bolt) { mainTexture = Star() }; fm.SetColor("_TintColor", new Color(.85f, .93f, 1f, .9f)); flash.GetComponent<Renderer>().sharedMaterial = fm;
        var sparks = new GameObject("Sparks").AddComponent<ParticleSystem>(); sparks.transform.position = hit;
        var main = sparks.main; main.startLifetime = new ParticleSystem.MinMaxCurve(.18f, .5f); main.startSpeed = new ParticleSystem.MinMaxCurve(2.5f, 7f);
        main.startSize = new ParticleSystem.MinMaxCurve(.025f, .055f); main.gravityModifier = 1.4f; main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startColor = new ParticleSystem.MinMaxGradient(Color.white, new Color(.55f, .8f, 1f));
        var em = sparks.emission; em.rateOverTime = 0; em.SetBursts(new[] { new ParticleSystem.Burst(0f, 34) });
        var sr = sparks.GetComponent<ParticleSystemRenderer>(); sr.renderMode = ParticleSystemRenderMode.Stretch; sr.velocityScale = .05f; sr.lengthScale = 1.5f;
        var sm = new Material(_bolt) { mainTexture = Soft() }; sm.SetColor("_TintColor", Color.white); sr.sharedMaterial = sm;
        sparks.Simulate(.09f, true, true);
        Light(hit + new Vector3(0, 0, -.3f), Glow, 4f, 4.5f); Light(tip, Glow, 1.6f, 3f); Light(new Vector3(1.8f, 2.4f, -2f), new Color(.8f, .75f, .7f), 1.2f, 7f);

        var cam = new GameObject("Camera").AddComponent<Camera>();
        cam.transform.position = new Vector3(-.4f, 1.35f, -3.1f); cam.transform.LookAt(new Vector3(.05f, .85f, 0));
        cam.fieldOfView = 46; cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(.015f, .015f, .02f);
        var rt = new RenderTexture(1200, 800, 24) { antiAliasing = 4 }; cam.targetTexture = rt; cam.Render();
        RenderTexture.active = rt; var shot = new Texture2D(1200, 800, TextureFormat.RGB24, false); shot.ReadPixels(new Rect(0, 0, 1200, 800), 0, 0); shot.Apply();
        string target = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "..", "art", "cosmetics", "staff_lightning.png"));
        File.WriteAllBytes(target, shot.EncodeToPNG());
        Debug.Log("[LightningPreview] Wrote " + target);
    }

    static LineRenderer Line(Color c, float alpha, float width, bool fadeEnd = false)
    {
        var l = new GameObject("Line").AddComponent<LineRenderer>();
        l.sharedMaterial = _bolt; l.numCapVertices = 2; l.useWorldSpace = true;
        c.a = alpha; l.startColor = c; c.a = fadeEnd ? 0 : alpha; l.endColor = c;
        l.startWidth = width; l.endWidth = width * (fadeEnd ? .25f : .7f);
        return l;
    }

    static void Jag(LineRenderer line, Vector3 a, Vector3 b, float amplitude)
    {
        var delta = b - a; float length = delta.magnitude;
        int count = Mathf.Clamp(Mathf.CeilToInt(length / .11f) + 1, 3, 48); line.positionCount = count;
        var side = Vector3.Cross(delta.normalized, Vector3.up).normalized; var up = Vector3.Cross(side, delta.normalized);
        for (int i = 0; i < count; i++)
        {
            float t = i / (float)(count - 1); var p = Vector3.Lerp(a, b, t);
            if (i > 0 && i < count - 1) p += (side * Random.Range(-1f, 1f) + up * Random.Range(-1f, 1f)) * amplitude * Mathf.Sin(t * Mathf.PI) * Mathf.Min(1f, length);
            line.SetPosition(i, p);
        }
    }

    static void Fork(LineRenderer branch, LineRenderer core)
    {
        int count = core.positionCount; int at = Random.Range(1, count - 1);
        var start = core.GetPosition(at); var along = core.GetPosition(count - 1) - core.GetPosition(0);
        float length = along.magnitude * Random.Range(.12f, .3f);
        var dir = Quaternion.AngleAxis(Random.Range(-55f, 55f), Random.onUnitSphere) * along.normalized;
        branch.positionCount = 9;
        for (int i = 0; i < 9; i++) { float t = i / 8f; var p = start + dir * length * t; if (i > 0) p += Random.insideUnitSphere * length * .14f; branch.SetPosition(i, p); }
    }

    static Vector3 Surface(Bounds b) { var d = Random.onUnitSphere; var toCam = (new Vector3(-.4f, 1.35f, -3.1f) - b.center).normalized; if (Random.value < .85f && Vector3.Dot(d, toCam) < 0f) d = Vector3.Reflect(d, toCam); return b.center + Vector3.Scale(d, b.extents * 1.02f); }
    static Material Lit(Color c, float s) { var m = new Material(Shader.Find("Standard")) { color = c }; m.SetFloat("_Glossiness", s); return m; }
    static void Part(PrimitiveType t, Vector3 p, Vector3 s, Material m) { var g = GameObject.CreatePrimitive(t); g.transform.position = p; g.transform.localScale = s; g.GetComponent<Renderer>().sharedMaterial = m; }
    static void Light(Vector3 p, Color c, float i, float r) { var l = new GameObject("L").AddComponent<Light>(); l.type = LightType.Point; l.transform.position = p; l.color = c; l.intensity = i; l.range = r; }

    static Texture2D Beam() { var t = new Texture2D(4, 64, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp }; for (int y = 0; y < 64; y++) { float v = (y + .5f) / 32f - 1f; float a = Mathf.Exp(-v * v * 9f); for (int x = 0; x < 4; x++) t.SetPixel(x, y, new Color(a, a, a, a)); } t.Apply(); return t; }
    static Texture2D Soft() => Tex(64, (x, y) => Mathf.Pow(1f - Mathf.Clamp01(Mathf.Sqrt(x * x + y * y)), 3f));
    static Texture2D Star() => Tex(64, (x, y) =>
    {
        float cross = Mathf.Max(Mathf.Clamp01(1f - Mathf.Abs(x) * 9f) * (1f - Mathf.Abs(y)), Mathf.Clamp01(1f - Mathf.Abs(y) * 9f) * (1f - Mathf.Abs(x)));
        return Mathf.Clamp01(cross * cross + Mathf.Pow(Mathf.Clamp01(1f - Mathf.Sqrt(x * x + y * y) * 3f), 2f));
    });
    static Texture2D Tex(int n, System.Func<float, float, float> a)
    {
        var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp }; var px = new Color[n * n];
        for (int y = 0; y < n; y++) for (int x = 0; x < n; x++) { float u = (x + .5f) / n * 2 - 1, v = (y + .5f) / n * 2 - 1; float al = a(u, v); px[y * n + x] = new Color(al, al, al, al); }
        t.SetPixels(px); t.Apply(); return t;
    }
}
