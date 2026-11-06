using System;
using System.Collections.Generic;
using IncreaseHardMod.Patches.Effects;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IncreaseHardMod.Hud;

/// <summary>What a vitals row shows this frame.</summary>
internal readonly struct VitalsReading
{
    public readonly bool Show;
    public readonly string Value, Max;

    private VitalsReading(bool show, string value, string max) { Show = show; Value = value; Max = max; }

    /// <param name="value">The big number, e.g. "10".</param>
    /// <param name="max">Text after the slash, e.g. "20%"; null for no max.</param>
    internal static VitalsReading Of(string value, string max = null) => new(true, value ?? "", max);
    internal static readonly VitalsReading Hidden = new(false, "", null);
}

/// <summary>One row under the native health / energy rows.</summary>
internal sealed class VitalsRowSpec
{
    /// <summary>Unique id, e.g. "armor".</summary>
    public string Id;
    /// <summary>Rows are listed by order, lowest first, directly under energy.</summary>
    public int Order;
    /// <summary>Icon replacing the energy bolt; white silhouettes from <see cref="HudIcons"/> tint well.</summary>
    public Func<Sprite> Icon;
    public Color ValueColor = Color.white, MaxColor = Color.white;
    /// <summary>Called every frame while the HUD is up; return <see cref="VitalsReading.Hidden"/> to hide the row.</summary>
    public Func<VitalsReading> Read;
}

/// <summary>
/// Native-style vitals rows (icon + value + "/max") under the game's energy row. Each row is a clone of the
/// energy row itself, so it has the native TextMeshPro font, material, glow, size and show/hide animation; the
/// clone's own scripts are removed and the row's <see cref="VitalsRowSpec.Read"/> writes the numbers instead.
/// Visible rows close up in order, so hiding one never leaves a gap.
/// <code>
/// HudVitals.Register(new VitalsRowSpec { Id = "armor", Order = 10, Icon = () => HudIcons.Shield,
///     ValueColor = HudStyle.Armor, MaxColor = HudStyle.ArmorMax,
///     Read = () => level > 0 ? VitalsReading.Of("10", "20%") : VitalsReading.Hidden });
/// </code>
/// </summary>
internal static class HudVitals
{
    private const string RowPrefix = "IHM Vitals ";

    private sealed class Row
    {
        public VitalsRowSpec Spec;
        public GameObject Clone;
        public TextMeshProUGUI Value, Max;
        public Vector3 MaxBase;
        public string ShownValue, ShownMax;
        public int Slot = -1;
    }

    private static readonly List<Row> Rows = new();

    internal static void Register(VitalsRowSpec spec)
    {
        if (spec == null || string.IsNullOrEmpty(spec.Id) || spec.Read == null) throw new ArgumentException("A vitals row needs an Id and Read.");
        Rows.RemoveAll(r => r.Spec.Id == spec.Id);
        Rows.Add(new Row { Spec = spec });
        Rows.Sort((a, b) => a.Spec.Order != b.Spec.Order ? a.Spec.Order.CompareTo(b.Spec.Order) : string.CompareOrdinal(a.Spec.Id, b.Spec.Id));
    }

    /// <summary>Driven by <see cref="HudRuntime"/> once per frame (LateUpdate).</summary>
    internal static void Tick()
    {
        if (Rows.Count == 0) return;
        var energy = EnergyUI.instance;
        bool ready = energy != null && LevelInfoHud.GameplayReady;
        int slot = 0;
        foreach (var row in Rows)
        {
            VitalsReading reading = VitalsReading.Hidden;
            if (ready)
                try { reading = row.Spec.Read(); }
                catch (Exception error) { Report(row, error); }
            if (!reading.Show) { if (row.Clone != null) row.Clone.SetActive(false); continue; }
            slot++;
            if (row.Clone == null || row.Clone.transform.parent != energy.transform) Build(row, energy);
            if (row.Clone == null) continue;
            if (!row.Clone.activeSelf) row.Clone.SetActive(true);
            if (row.Slot != slot) Place(row, energy, slot);
            Write(row, reading);
        }
    }

    private static readonly HashSet<string> Reported = new(StringComparer.Ordinal);

    private static void Report(Row row, Exception error)
    {
        if (Reported.Add(row.Spec.Id)) Plugin.Log?.LogWarning($"[HUD] Vitals row '{row.Spec.Id}' failed to read: {error.Message}");
    }

    private static void Build(Row row, EnergyUI energy)
    {
        if (row.Clone != null) UnityEngine.Object.Destroy(row.Clone);
        row.Clone = null;
        var source = energy.gameObject;
        var clone = UnityEngine.Object.Instantiate(source, source.transform);
        clone.name = RowPrefix + row.Spec.Id;
        // Never let the copied scripts drive the row (EnergyUI would write the energy numbers).
        foreach (var ui in clone.GetComponentsInChildren<SemiUI>(true)) UnityEngine.Object.DestroyImmediate(ui);
        foreach (Transform child in clone.transform)
            if (child.name.StartsWith(RowPrefix, StringComparison.Ordinal)) UnityEngine.Object.Destroy(child.gameObject); // rows cloned along with energy
        clone.transform.localRotation = Quaternion.identity;
        clone.transform.localScale = Vector3.one;

        row.Value = clone.GetComponent<TextMeshProUGUI>();
        var maxTransform = clone.transform.Find("EnergyMax");
        row.Max = maxTransform != null ? maxTransform.GetComponent<TextMeshProUGUI>() : null;
        if (row.Value == null) { UnityEngine.Object.Destroy(clone); return; }
        row.Value.color = row.Spec.ValueColor;
        if (row.Max != null) { row.Max.color = row.Spec.MaxColor; row.MaxBase = row.Max.transform.localPosition; }

        // Swap the lightning icon, wherever the row keeps it. Only images that carry an icon (a sprite or
        // texture) change; plain panels or fills are left alone.
        // The energy row's only visible graphic is its bolt icon. Other images in the copy (hidden flashes and
        // backings the native SemiUI script normally keeps transparent) are switched off; tinting them made a
        // big shape and a dark square appear. The icon is the smallest visible image with a sprite.
        var icon = row.Spec.Icon?.Invoke();
        Image bolt = null;
        float best = float.MaxValue;
        foreach (var image in clone.GetComponentsInChildren<Image>(true))
        {
            float area = Mathf.Abs(image.rectTransform.rect.width * image.rectTransform.rect.height);
            if (image.sprite != null && image.color.a > .1f && image.enabled && area > 0f && area < best) { best = area; bolt = image; }
        }
        foreach (var image in clone.GetComponentsInChildren<Image>(true))
            if (image != bolt) image.enabled = false;
        foreach (var raw in clone.GetComponentsInChildren<RawImage>(true)) raw.enabled = false;
        if (bolt != null && icon != null)
        {
            var colour = row.Spec.ValueColor;
            colour.a = bolt.color.a;
            bolt.sprite = icon;
            bolt.color = colour;
            bolt.preserveAspect = true;
        }
        LogLayoutOnce(source);
        row.Clone = clone;
        row.Slot = -1;
        row.ShownValue = row.ShownMax = null;
    }

    private static bool _layoutLogged;

    // One line describing the native energy row's graphics, to check the icon choice against a real game.
    private static void LogLayoutOnce(GameObject energy)
    {
        if (_layoutLogged) return;
        _layoutLogged = true;
        var parts = new List<string>();
        foreach (var image in energy.GetComponentsInChildren<Graphic>(true))
            parts.Add($"{image.name}:{image.GetType().Name} sprite={(image is Image i && i.sprite != null ? i.sprite.name : "-")} " +
                      $"size={image.rectTransform.rect.size} alpha={image.color.a:0.##} on={image.enabled && image.gameObject.activeInHierarchy}");
        Plugin.Log?.LogInfo("[HUD] Energy row graphics: " + string.Join(" | ", parts));
    }

    // Child of the energy row: its local origin is the energy row, so slot n is n row-steps straight down.
    private static void Place(Row row, EnergyUI energy, int slot)
    {
        var source = energy.transform;
        var health = HealthUI.instance;
        float step = health != null && health.transform.parent == source.parent
            ? Mathf.Abs(health.transform.localPosition.y - source.localPosition.y) : 0f;
        if (step < 1f) step = source.GetComponent<RectTransform>() is RectTransform rt && rt.rect.height > 1f ? rt.rect.height : 50f;
        row.Clone.transform.localPosition = new Vector3(0f, -step * slot, 0f);
        row.Slot = slot;
    }

    private static void Write(Row row, VitalsReading reading)
    {
        if (reading.Value == row.ShownValue && reading.Max == row.ShownMax) return;
        row.ShownValue = reading.Value;
        row.ShownMax = reading.Max;
        row.Value.text = reading.Value;
        if (row.Max == null) return;
        row.Max.enabled = reading.Max != null;
        // Same rule as the native rows: the max label shifts right for numbers longer than three digits.
        float shift = reading.Value.Length > 3 ? (reading.Value.Length - 3) * 20f : 0f;
        row.Max.transform.localPosition = row.MaxBase + new Vector3(shift, 0f, 0f);
        if (reading.Max != null) row.Max.text = $"<b><color={HudStyle.Hex(row.Spec.MaxColor)}>/</color></b>{reading.Max}";
    }
}
