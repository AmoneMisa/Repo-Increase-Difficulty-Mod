using System.Collections.Generic;
using HarmonyLib;
using IncreaseHardMod.Visuals;
using UnityEngine;

namespace IncreaseHardMod.Gear;

internal sealed class Katana : MonoBehaviour
{
    internal const string Asset = "Item Melee Katana";
    internal const int Damage = 150;
    private const float Recovery = .8f, Guard = -.245f, Tip = .505f;
    private float _nextSwing;

    internal static void Configure(GameObject go)
    {
        var melee = go.GetComponent<ItemMelee>();
        var hurt = go.GetComponentInChildren<HurtCollider>(true);
        var hitbox = hurt.GetComponent<BoxCollider>();
        hurt.enemyDamage = Damage;
        hurt.enemyDamageCooldown = .45f;
        hurt.enemyKill = false;
        hurt.enemyStun = false;
        hurt.enemyRayCast = true;
        melee.durabilityDrainOnEnemiesAndPVP = 8f;
        // Keep the original attack pivot. Only the blade and a small margin can connect.
        hitbox.transform.localPosition = new Vector3(0f, (Guard + Tip) * .5f - hitbox.transform.parent.localPosition.y, .045f);
        hitbox.transform.localRotation = Quaternion.identity;
        hitbox.transform.localScale = Vector3.one;
        hitbox.center = Vector3.zero;
        hitbox.size = new Vector3(.17f, Tip - Guard, .20f);

        var bladeCollider = go.transform.Find("Semi Box Collider");
        if (bladeCollider != null)
        {
            bladeCollider.localPosition = new Vector3(.018f, -.045f, 0f);
            bladeCollider.localScale = new Vector3(.087f, 1.1f, .042f);
        }
        foreach (var name in new[] { "Semi Box Collider (1)", "Semi Box Collider (2)" })
        {
            var guard = go.transform.Find(name);
            if (guard == null) continue;
            guard.localPosition = Vector3.up * Guard;
            guard.localScale = new Vector3(.136f, .024f, .096f);
        }
        go.transform.Find("Swing Point").localPosition = new Vector3(.05f, Tip, 0f);
        go.AddComponent<Katana>();
        BuildModel(go);
    }

    private static void BuildModel(GameObject go)
    {
        var healthy = go.transform.Find("Mesh Healthy");
        var broken = go.transform.Find("Mesh Broken");
        var template = ItemArt.FresnelMaterialDonor(healthy);
        var layer = healthy.gameObject.layer;
        var steel = Metal(template, "Katana satin steel", new Color(.53f, .57f, .62f), .75f);
        var edge = Metal(template, "Katana polished edge", new Color(.83f, .86f, .9f), .86f);
        var brass = Metal(template, "Katana brass", new Color(.58f, .37f, .14f), .54f);
        var wrap = ItemArt.Paint(template, null, new Color(.038f, .033f, .047f), "Katana dark wrap");
        var inlay = ItemArt.Paint(template, null, new Color(.88f, .82f, .64f), "Katana grip diamonds");
        var grip = new ProcMesh().ChamferBox(At(0f, -.424f, 0f, .045f, .335f, .036f), .24f).Build("Katana grip");
        var diamonds = new ProcMesh();
        for (int i = 0; i < 9; i++)
            foreach (float side in new[] { -1f, 1f })
                diamonds.Box(ProcMesh.TRS(new Vector3(0f, -.554f + i * .031f, side * .018f), new Vector3(0f, 0f, 45f),
                    new Vector3(.017f, .017f, .002f)));
        var braid = diamonds.Build("Katana wrap inlays");
        var fittings = new ProcMesh().Crystal(At(0f, Guard, 0f, .142f, .021f, .104f), 12, -.42f, .42f)
            .ChamferBox(At(0f, Guard + .019f, 0f, .057f, .039f, .030f))
            .ChamferBox(At(0f, -.587f, 0f, .047f, .020f, .038f)).Build("Katana fittings");

        foreach (var shell in new[] { healthy, broken })
        {
            foreach (var renderer in shell.GetComponentsInChildren<MeshRenderer>(true)) renderer.enabled = false;
            var root = new GameObject("Katana model").transform;
            root.SetParent(go.transform, false);
            root.SetParent(shell, true);
            Part(root, grip, layer, wrap);
            Part(root, braid, layer, inlay);
            Part(root, fittings, layer, brass);
            Part(root, Blade(shell == broken), layer, steel, edge);
        }
        ItemArt.RetargetItemLights(go, go.transform);
    }

    private static Matrix4x4 At(float x, float y, float z, float width, float height, float depth) =>
        ProcMesh.TRS(new Vector3(x, y, z), Vector3.zero, new Vector3(width, height, depth));

    private static Material Metal(Material template, string name, Color color, float smoothness)
    {
        var material = ItemArt.Paint(template, null, color, name);
        if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", .6f);
        if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", smoothness);
        if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
        return material;
    }

    private static void Part(Transform parent, Mesh mesh, int layer, params Material[] materials)
    {
        var obj = new GameObject(mesh.name) { layer = layer };
        obj.transform.SetParent(parent, false);
        obj.AddComponent<MeshFilter>().sharedMesh = mesh;
        obj.AddComponent<MeshRenderer>().sharedMaterials = materials;
    }

    private static Mesh Blade(bool broken)
    {
        var profile = broken ? new[] { (Guard, 0f, .026f), (-.14f, .002f, .025f), (-.11f, .014f, .011f) }
            : new[] { (Guard, 0f, .026f), (-.04f, .004f, .025f), (.18f, .014f, .023f), (.40f, .034f, .019f),
                (.466f, .045f, .012f), (Tip, .054f, .0008f) };
        var vertices = new List<Vector3>();
        var body = new List<int>();
        var bevel = new List<int>();
        Vector3 Point(int section, int side)
        {
            var (y, center, half) = profile[section];
            float thickness = Mathf.Min(.009f, half * .42f);
            return side switch
            {
                0 => new Vector3(center - half, y, 0f),
                1 => new Vector3(center - half * .37f, y, thickness),
                2 => new Vector3(center + half, y, thickness * .7f),
                3 => new Vector3(center + half, y, -thickness * .7f),
                _ => new Vector3(center - half * .37f, y, -thickness),
            };
        }
        void Face(List<int> indices, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            int i = vertices.Count;
            vertices.Add(a); vertices.Add(b); vertices.Add(c); vertices.Add(d);
            indices.AddRange(new[] { i, i + 1, i + 2, i, i + 2, i + 3 });
        }
        for (int i = 1; i < profile.Length; i++)
        for (int side = 0; side < 5; side++)
        {
            int next = (side + 1) % 5;
            Face(side is 0 or 4 ? bevel : body, Point(i - 1, side), Point(i - 1, next), Point(i, next), Point(i, side));
        }
        int end = profile.Length - 1;
        Face(body, Point(end, 0), Point(end, 1), Point(end, 2), Point(end, 3));
        Face(body, Point(end, 0), Point(end, 3), Point(end, 4), Point(end, 4));
        var mesh = new Mesh { name = broken ? "Katana chipped blade" : "Katana curved blade" };
        mesh.SetVertices(vertices);
        mesh.subMeshCount = 2;
        mesh.SetTriangles(body, 0);
        mesh.SetTriangles(bevel, 1);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    [HarmonyPatch(typeof(ItemMelee), nameof(ItemMelee.StateSet))]
    private static class StartCut
    {
        private static bool Prefix(ItemMelee __instance, ItemMelee.State _newState)
        {
            if (_newState != ItemMelee.State.Swinging || !SemiFunc.IsMasterClientOrSingleplayer() ||
                !__instance.TryGetComponent<Katana>(out var katana)) return true;
            if (__instance.currentState == ItemMelee.State.Swinging || __instance.physGrabObject == null ||
                !__instance.physGrabObject.grabbed || Time.time < katana._nextSwing) return false;
            katana._nextSwing = Time.time + Recovery;
            return true;
        }
    }

    [HarmonyPatch(typeof(ItemMelee), "SwingDetectionLogic")]
    private static class PreventContinuousSwing
    {
        private static bool Prefix(ItemMelee __instance) =>
            __instance.currentState != ItemMelee.State.Swinging || !__instance.TryGetComponent<Katana>(out _);
    }
}
