using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace IncreaseHardMod.FableRealm;

/// <summary>
/// The Fable Realm's own doorway gate: a plank leaf with banded trim, a hinge post and a brass pull, in the room's
/// biome colours (tools/fable_realm: room_kit.Room.gate and the biome builders). It dresses the vanilla hinge door
/// that does the work: the borrowed leaf keeps its joint, colliders, sounds and map marker, and only its own meshes
/// are hidden. Materials are the room's own (same names as the palette), so the gate is lit and textured like the
/// walls around it.
/// </summary>
internal sealed class FableGateLook
{
    internal Material Wood, Band, Pull;

    // Biome palettes, as in rooms_forest_village.py and rooms_giant_castle.py; castle colours are the kit default.
    private static readonly (string wood, string band) Forest = ("OakDark", "Bark"), Village = ("Chocolate", "Icing"),
        Giant = ("OakLight", "Copper"), Castle = ("OakDark", "Iron");

    private static readonly Dictionary<string, (string wood, string band)> Biomes = new(StringComparer.Ordinal)
    {
        ["BookGate"] = Forest, ["RootHall"] = Forest, ["MushroomChoir"] = Forest, ["FireflyBrook"] = Forest,
        ["LostPicnic"] = Forest, ["HollowLibrary"] = Forest, ["FairyRing"] = Forest,
        ["SugarGate"] = Village, ["GingerbreadSquare"] = Village, ["BakersWorkshop"] = Village, ["CandyMarket"] = Village,
        ["Clockmaker"] = Village, ["MarzipanVault"] = Village, ["CoachYard"] = Village,
        ["BeanstalkCourt"] = Giant, ["GiantTea"] = Giant, ["PorcelainWalk"] = Giant, ["RoseMaze"] = Giant,
        ["ForgottenToy"] = Giant, ["GardenStore"] = Giant, ["TeaTray"] = Giant, ["BeanstalkGrove"] = Giant, ["GiantPantry"] = Giant,
    };

    internal static (string wood, string band) Palette(string roomId)
    {
        string id = FableLayouts.BaseId(roomId);
        return id != null && Biomes.TryGetValue(id, out var palette) ? palette : Castle;
    }

    /// <summary>The gate materials for one room, taken from the room's own renderers; null when it has none usable.</summary>
    internal static FableGateLook For(string roomId, Transform room)
    {
        var byName = new Dictionary<string, Material>(StringComparer.Ordinal);
        foreach (var renderer in room.GetComponentsInChildren<Renderer>(true))
            foreach (var material in renderer.sharedMaterials)
                if (material != null && !byName.ContainsKey(material.name)) byName[material.name] = material;
        var (wood, band) = Palette(roomId);
        var look = new FableGateLook
        {
            Wood = First(byName, wood, "OakDark", "OakLight", "Oak"),
            Band = First(byName, band, "Iron", "Bark", "Brass"),
            Pull = First(byName, "Brass", "Copper", band),
        };
        if (look.Wood == null) return null;
        look.Band ??= look.Wood;
        look.Pull ??= look.Band;
        return look;
    }

    private static Material First(Dictionary<string, Material> byName, params string[] names)
    {
        foreach (string name in names)
            if (name != null && byName.TryGetValue(name, out var material)) return material;
        return null;
    }

    // ---- building -----------------------------------------------------------------------------------------------

    private static Mesh _cube, _cylinder;

    /// <summary>
    /// Hides the borrowed leaf's own meshes and adds the gate under it. <paramref name="pivot"/> is the hinge frame
    /// from FableDoorDonor.BuildPair: +X from the hinge toward the middle of the doorway, +Y up, floor at y = 0.
    /// </summary>
    internal void Dress(GameObject leaf, Transform pivot, float width, float height)
    {
        foreach (var renderer in leaf.GetComponentsInChildren<Renderer>(true))
            if (renderer is MeshRenderer or SkinnedMeshRenderer) renderer.enabled = false;

        var gate = new GameObject("Fable Gate").transform;
        gate.SetParent(leaf.transform, false);
        gate.SetPositionAndRotation(pivot.position, pivot.rotation);
        // The fitted leaf is scaled to the doorway; the gate is modelled in metres, so undo that scale (the leaf
        // only turns in steps of 90 degrees against the pivot, so its scale stays axis-aligned to the gate).
        gate.localScale = Vector3.one;
        var axes = new Vector3(gate.TransformVector(Vector3.right).magnitude, gate.TransformVector(Vector3.up).magnitude,
            gate.TransformVector(Vector3.forward).magnitude);
        gate.localScale = new Vector3(1f / axes.x, 1f / axes.y, 1f / axes.z) * pivot.lossyScale.x;

        // Plank leaf, three bands, a stile on the free edge, the hinge post and a ring pull: room_kit's old _gate.
        Box(gate, "Leaf", new Vector3(width / 2f, height / 2f, 0f), new Vector3(width, height, .1f), Wood);
        for (int i = 0; i < 3; i++)
        {
            float y = height * (.16f + .34f * i);
            Box(gate, "Band " + i, new Vector3(width / 2f, y, 0f), new Vector3(width * .98f, .12f, .15f), Band);
        }
        Box(gate, "Stile", new Vector3(width - .07f, height / 2f, 0f), new Vector3(.14f, height, .16f), Band);
        Cylinder(gate, "Post", new Vector3(0f, (height + .2f) / 2f, 0f), new Vector3(.14f, (height + .2f) / 2f, .14f), Quaternion.identity, Band);
        foreach (float side in new[] { -1f, 1f })
            Cylinder(gate, side < 0f ? "Pull in" : "Pull out", new Vector3(width - .35f, height * .45f, side * .07f),
                new Vector3(.2f, .02f, .2f), Quaternion.Euler(90f, 0f, 0f), Pull);
    }

    private static void Box(Transform parent, string name, Vector3 centre, Vector3 size, Material material) =>
        Part(parent, name, Mesh(PrimitiveType.Cube), centre, size, Quaternion.identity, material);

    private static void Cylinder(Transform parent, string name, Vector3 centre, Vector3 size, Quaternion rotation, Material material) =>
        Part(parent, name, Mesh(PrimitiveType.Cylinder), centre, size, rotation, material);

    private static void Part(Transform parent, string name, Mesh mesh, Vector3 centre, Vector3 size, Quaternion rotation, Material material)
    {
        var part = new GameObject(name);
        part.transform.SetParent(parent, false);
        part.transform.localPosition = centre;
        part.transform.localRotation = rotation;
        part.transform.localScale = size;
        part.AddComponent<MeshFilter>().sharedMesh = mesh;
        part.AddComponent<MeshRenderer>().sharedMaterial = material;
    }

    // The engine's own primitive meshes, without the collider a primitive object would bring: the borrowed leaf's
    // colliders stay the only ones, so the door swings and blocks exactly as the vanilla one does.
    private static Mesh Mesh(PrimitiveType type)
    {
        ref Mesh cached = ref type == PrimitiveType.Cube ? ref _cube : ref _cylinder;
        if (cached != null) return cached;
        var probe = GameObject.CreatePrimitive(type);
        cached = probe.GetComponent<MeshFilter>().sharedMesh;
        Object.DestroyImmediate(probe);
        return cached;
    }
}
