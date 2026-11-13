using System.IO;
using UnityEditor;
using UnityEngine;

// Batch build: Unity.exe -batchmode -quit -projectPath tools/IceShaderBundle -executeMethod BuildGlueBundle.Build
// Writes the shader-only bundle the mod embeds for the Sticky Pistol / Glue Grenade (src/Art/ihm_glue.bundle).
public static class BuildGlueBundle
{
    public static void Build()
    {
        const string shaderPath = "Assets/Shaders/IHMGlue.shader";
        AssetDatabase.ImportAsset(shaderPath, ImportAssetOptions.ForceUpdate);
        var shader = AssetDatabase.LoadAssetAtPath<Shader>(shaderPath);
        if (shader == null) throw new FileNotFoundException("Shader did not import: " + shaderPath);
        foreach (var message in ShaderUtil.GetShaderMessages(shader))
            Debug.Log($"[GlueBundle] shader {message.severity}: {message.message} (line {message.line})");
        if (ShaderUtil.ShaderHasError(shader)) throw new System.Exception("IHM/Glue has compile errors.");

        string output = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Build"));
        Directory.CreateDirectory(output);
        var build = new AssetBundleBuild { assetBundleName = "ihm_glue.bundle", assetNames = new[] { shaderPath } };
        var manifest = BuildPipeline.BuildAssetBundles(output, new[] { build },
            BuildAssetBundleOptions.ForceRebuildAssetBundle | BuildAssetBundleOptions.StrictMode, BuildTarget.StandaloneWindows64);
        if (manifest == null) throw new System.Exception("Bundle build failed.");

        string target = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "..", "src", "Art", "ihm_glue.bundle"));
        File.Copy(Path.Combine(output, "ihm_glue.bundle"), target, true);
        Debug.Log($"[GlueBundle] Built {target} ({new FileInfo(target).Length} bytes) with Unity {Application.unityVersion}.");
    }
}
