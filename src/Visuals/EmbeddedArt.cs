using System;
using System.IO;
using System.IO.Compression;

namespace IncreaseHardMod.Visuals;

/// <summary>
/// Reads an embedded model file. The build gzips every src/Art/*.mesh.json and embeds it as *.mesh.json.gz (see the
/// CompressArt target in the project), which keeps the DLL a fraction of the size and names the resource for what it
/// is; plain JSON still loads, so a resource embedded without compression keeps working.
/// </summary>
internal static class EmbeddedArt
{
    /// <summary>The resource whose name ends with <paramref name="suffix"/> (e.g. ".v11_veil.mesh.json"), gzipped or plain.</summary>
    internal static string Find(string[] names, string suffix)
    {
        foreach (string name in names)
            if (name.EndsWith(suffix + ".gz", StringComparison.Ordinal) || name.EndsWith(suffix, StringComparison.Ordinal))
                return name;
        return null;
    }

    internal static string ReadText(Stream stream)
    {
        var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        buffer.Position = 0;
        bool gzip = buffer.Length > 2 && buffer.GetBuffer()[0] == 0x1f && buffer.GetBuffer()[1] == 0x8b;
        if (!gzip)
        {
            using var plain = new StreamReader(buffer);
            return plain.ReadToEnd();
        }
        using var inflate = new GZipStream(buffer, CompressionMode.Decompress);
        using var reader = new StreamReader(inflate);
        return reader.ReadToEnd();
    }
}
