using System;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

namespace IncreaseHardMod.Visuals;

/// <summary>Managed JSON loading so model DTOs do not depend on Unity's runtime type serializer.</summary>
internal static class ArtModelReader
{
    internal static ArtModel Parse(string json, string key)
    {
        var model = JsonConvert.DeserializeObject<ArtModel>(json, new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.None,
            MetadataPropertyHandling = MetadataPropertyHandling.Ignore,
            MaxDepth = 64,
        });
        if (model == null || model.version != 1 || model.fps <= 0 || model.frames <= 0 ||
            model.parts == null || model.parts.Length == 0)
            throw new InvalidDataException($"Invalid item art header: {key}");
        foreach (var part in model.parts)
        {
            if (part == null || part.vertices == null || part.vertices.Length == 0 ||
                part.normals == null || part.normals.Length != part.vertices.Length ||
                part.uv == null || part.uv.Length != part.vertices.Length ||
                part.triangles == null || part.triangles.Length % 3 != 0 ||
                part.rest == null || part.samples == null ||
                (part.samples.Length != 0 && part.samples.Length != model.frames))
                throw new InvalidDataException($"Invalid item art geometry: {key}/{part?.name}");
            foreach (int index in part.triangles)
                if (index < 0 || index >= part.vertices.Length)
                    throw new InvalidDataException($"Invalid triangle index: {key}/{part.name}");
        }
        return model;
    }
}

// DTO fields match Blender's checked-in JSON exports.
#pragma warning disable CS0649
[Serializable] internal sealed class ArtModel
{
    public int version, fps, frames;
    public ArtPart[] parts;
}
[Serializable] internal sealed class ArtPart
{
    public string name, texture;
    public Vector3[] vertices, normals;
    public Vector2[] uv;
    public int[] triangles;
    public Color color;
    public ArtPose rest;
    public ArtPose[] samples;
}
[Serializable] internal sealed class ArtPose
{
    public Vector3 position, scale;
    public Quaternion rotation;
}
#pragma warning restore CS0649

