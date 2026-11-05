using System;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace IncreaseHardMod.Visuals;

/// <summary>
/// Loads the mod's own "IHM/Ice" shader from the embedded asset bundle (src/Art/ihm_ice.bundle, built by
/// tools/IceShaderBundle). The bundle holds only this original shader. If it is missing, fails to load or
/// the GPU cannot run it, <see cref="Shader"/> stays null and the ice falls back to the built-in
/// Standard-shader block, so a freeze never breaks.
/// </summary>
internal static class IceShader
{
    private static bool _tried;
    private static Shader _shader;

    internal static Shader Shader
    {
        get
        {
            if (_tried) return _shader;
            _tried = true;
            try
            {
                var asm = Assembly.GetExecutingAssembly();
                string resource = Array.Find(asm.GetManifestResourceNames(), n => n.EndsWith(".ihm_ice.bundle", StringComparison.Ordinal));
                if (resource == null) { Plugin.Log.LogWarning("[Ice] Embedded ice shader bundle missing; using the fallback ice."); return null; }
                byte[] bytes;
                using (var stream = asm.GetManifestResourceStream(resource))
                using (var memory = new MemoryStream())
                {
                    stream.CopyTo(memory);
                    bytes = memory.ToArray();
                }
                var bundle = AssetBundle.LoadFromMemory(bytes);
                if (bundle == null) { Plugin.Log.LogWarning("[Ice] Ice shader bundle did not load; using the fallback ice."); return null; }
                var shader = bundle.LoadAsset<Shader>("Assets/Shaders/IHMIce.shader") ?? bundle.LoadAsset<Shader>("IHMIce");
                bundle.Unload(false); // keep the shader, drop the bundle
                if (shader == null || !shader.isSupported)
                {
                    Plugin.Log.LogWarning($"[Ice] Ice shader {(shader == null ? "not found in bundle" : "not supported by this GPU")}; using the fallback ice.");
                    return null;
                }
                Plugin.Log.LogInfo("[Ice] Loaded the IHM/Ice shader.");
                return _shader = shader;
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"[Ice] Ice shader failed to load ({e.Message}); using the fallback ice.");
                return null;
            }
        }
    }
}
