using System.IO;
using UnityEditor;
using UnityEngine;

// Batch build: Unity.exe -batchmode -quit -projectPath tools/IceShaderBundle -executeMethod BuildIceBundle.Build
// Writes the shader-only bundle the mod embeds (src/Art/ihm_ice.bundle).
public static class BuildIceBundle
{
    public static void Build()
    {
        const string shaderPath = "Assets/Shaders/IHMIce.shader";
        AssetDatabase.ImportAsset(shaderPath, ImportAssetOptions.ForceUpdate);
        var shader = AssetDatabase.LoadAssetAtPath<Shader>(shaderPath);
        if (shader == null) throw new FileNotFoundException("Shader did not import: " + shaderPath);
        foreach (var message in ShaderUtil.GetShaderMessages(shader))
            Debug.Log($"[IceBundle] shader {message.severity}: {message.message} (line {message.line})");
        if (ShaderUtil.ShaderHasError(shader)) throw new System.Exception("IHM/Ice has compile errors.");

        string output = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Build"));
        Directory.CreateDirectory(output);
        var build = new AssetBundleBuild { assetBundleName = "ihm_ice.bundle", assetNames = new[] { shaderPath } };
        var manifest = BuildPipeline.BuildAssetBundles(output, new[] { build },
            BuildAssetBundleOptions.ForceRebuildAssetBundle | BuildAssetBundleOptions.StrictMode, BuildTarget.StandaloneWindows64);
        if (manifest == null) throw new System.Exception("Bundle build failed.");

        string target = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "..", "src", "Art", "ihm_ice.bundle"));
        File.Copy(Path.Combine(output, "ihm_ice.bundle"), target, true);
        Debug.Log($"[IceBundle] Built {target} ({new FileInfo(target).Length} bytes) with Unity {Application.unityVersion}.");
    }
}
