using System.Collections.Generic;
using IncreaseHardMod.Upgrades;
using UnityEngine;

namespace IncreaseHardMod.Visuals;

/// <summary>Temporary ice materials; restores only slots still owned by this effect.</summary>
internal sealed class FrostCoating
{
    private sealed class Slot
    {
        internal Renderer Renderer;
        internal Material[] Original, Iced;
    }
    private readonly List<Slot> _slots = new();

    internal void Apply(Renderer[] renderers)
    {
        if (_slots.Count > 0) return;
        var texture = UpgradeIcons.LoadTexture("ice_frost.png");
        foreach (var renderer in renderers)
        {
            if (renderer is not MeshRenderer && renderer is not SkinnedMeshRenderer) continue;
            var original = renderer.sharedMaterials;
            var iced = new Material[original.Length];
            for (int i = 0; i < original.Length; i++)
            {
                if (original[i] == null) continue;
                var material = iced[i] = new Material(original[i]) { name = "IHM Player Frost " + original[i].name };
                if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", texture);
                if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", texture);
                if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", .8f);
                if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", .8f);
                if (material.HasProperty("_EmissionMap")) material.SetTexture("_EmissionMap", texture);
                material.EnableKeyword("_EMISSION");
            }
            _slots.Add(new Slot { Renderer = renderer, Original = original, Iced = iced });
            renderer.sharedMaterials = iced;
        }
        Tick();
    }

    internal void Tick()
    {
        foreach (var slot in _slots)
            foreach (var material in slot.Iced)
            {
                if (material == null) continue;
                if (material.HasProperty("_Color")) material.SetColor("_Color", new Color(.32f, .67f, 1f));
                if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", new Color(.32f, .67f, 1f));
                if (material.HasProperty("_EmissionColor")) material.SetColor("_EmissionColor", new Color(.015f, .06f, .12f));
            }
    }

    internal void Clear()
    {
        foreach (var slot in _slots)
        {
            if (slot.Renderer != null)
            {
                var current = slot.Renderer.sharedMaterials;
                for (int i = 0; i < current.Length && i < slot.Iced.Length; i++)
                    if (current[i] == slot.Iced[i]) current[i] = slot.Original[i];
                slot.Renderer.sharedMaterials = current;
            }
            foreach (var material in slot.Iced) if (material != null) Object.Destroy(material);
        }
        _slots.Clear();
    }
}
