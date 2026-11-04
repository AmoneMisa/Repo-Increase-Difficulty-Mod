using System;
using UnityEngine;

namespace LevelAuthoring.Editor
{
    // Pure authoring data. No dependency on FableRealm, REPOLib or game assemblies.
    [Serializable] public sealed class RoomDefinition
    {
        public int schemaVersion = 1;
        public string id, title;
        public int seed = 17;
        public Vector3 bounds = new Vector3(15,10,15);
        public SurfaceDefinition[] materials;
        public ElementDefinition[] elements;
        public AnchorDefinition[] anchors;
        public ViewDefinition[] views;
        public NavigationDefinition navigation;
    }

    [Serializable] public sealed class SurfaceDefinition
    {
        public string id, color = "#ffffff";
        public float smoothness = .2f, metallic, emission, grain;
    }

    [Serializable] public sealed class ElementDefinition
    {
        public string id, kind, material;
        public string navigation = "Obstacle";
        public Vector3 position, rotation, size = Vector3.one;
        public bool collider = true;
        public bool shadows;
        public Vector3[] points;
        public float radius = .1f;
        public int count = 12;
        public string[] palette;
        public float intensity = 1, range = 8;
    }

    [Serializable] public sealed class NavigationDefinition
    {
        public float radius=.35f, height=1.8f, climb=.25f, slope=35, voxel=.08f;
        public float sampleDistance=.25f, maxVerticalSnap=.15f;
    }

    [Serializable] public sealed class AnchorDefinition
    {
        public string id, kind;
        public Vector3 position, rotation, size;
        public string[] connectsTo;
    }

    [Serializable] public sealed class ViewDefinition
    {
        public string id;
        public Vector3 position, target;
        public float fieldOfView = 65;
    }
}
