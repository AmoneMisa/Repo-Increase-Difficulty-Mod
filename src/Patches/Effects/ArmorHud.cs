using IncreaseHardMod.Upgrades;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IncreaseHardMod.Patches.Effects;

/// <summary>
/// A native-style vitals row under energy for the Armor upgrade: blue shield icon, current damage reduction
/// in percent and the cap ("10 /20%"). The row is a clone of the game's own energy row, so it uses the same
/// TextMeshPro font, material, glow and size, and it is parented to that row so the native HUD show/hide
/// animation moves it too. The clone's EnergyUI script is removed before it runs; this component writes the
/// numbers instead. Shown only while the local player has at least one Armor level.
/// </summary>
internal sealed class ArmorHud : MonoBehaviour
{
    private const string RowName = "IHM Armor Row";
    private static readonly Color Blue = new(.32f, .56f, 1f), BlueMax = new(.26f, .44f, .95f);
    private static Sprite _shield;

    private GameObject _row;
    private TextMeshProUGUI _value, _max;
    private Vector3 _maxBase;
    private int _shownPercent = -1, _shownCap = -1;

    private void LateUpdate()
    {
        var energy = EnergyUI.instance;
        var local = PlayerController.instance != null ? PlayerController.instance.playerAvatarScript : null;
        float reduction = 0f;
        try { reduction = local != null ? SurvivalUpgrades.ArmorReduction(local) : 0f; } catch { reduction = 0f; }
        bool show = energy != null && reduction > 0f && LevelInfoHud.GameplayReady;
        if (!show) { if (_row != null) _row.SetActive(false); return; }
        if (_row == null || _row.transform.parent != energy.transform) Build(energy);
        if (_row == null) return;
        if (!_row.activeSelf) _row.SetActive(true);

        int percent = Mathf.RoundToInt(reduction * 100f), cap = Mathf.RoundToInt(SurvivalUpgrades.ArmorCap * 100f);
        if (percent == _shownPercent && cap == _shownCap) return;
        _shownPercent = percent; _shownCap = cap;
        _value.text = percent.ToString();
        // Same rule as the native rows: the max label shifts right for numbers longer than three digits.
        float shift = _value.text.Length > 3 ? (_value.text.Length - 3) * 20f : 0f;
        if (_max != null)
        {
            _max.transform.localPosition = _maxBase + new Vector3(shift, 0f, 0f);
            _max.text = "<b><color=#4d7dff>/</color></b>" + cap + "%";
        }
    }

    private void Build(EnergyUI energy)
    {
        if (_row != null) Destroy(_row);
        _row = null;
        var source = energy.gameObject;
        var clone = Instantiate(source, source.transform);
        clone.name = RowName;
        // Never let the copied scripts drive the row (EnergyUI would write the energy numbers).
        foreach (var ui in clone.GetComponentsInChildren<SemiUI>(true)) DestroyImmediate(ui);
        foreach (Transform child in clone.transform)
            if (child.name == RowName) Destroy(child.gameObject); // a previous row cloned along with energy
        var health = HealthUI.instance;
        float step = health != null && health.transform.parent == source.transform.parent
            ? Mathf.Abs(health.transform.localPosition.y - source.transform.localPosition.y) : 0f;
        if (step < 1f) step = source.GetComponent<RectTransform>() is RectTransform rt && rt.rect.height > 1f ? rt.rect.height : 50f;
        // Child of the energy row: its local origin is the energy row, so one row-step straight down.
        clone.transform.localPosition = new Vector3(0f, -step, 0f);
        clone.transform.localRotation = Quaternion.identity;
        clone.transform.localScale = Vector3.one;

        _value = clone.GetComponent<TextMeshProUGUI>();
        var maxTransform = clone.transform.Find("EnergyMax");
        _max = maxTransform != null ? maxTransform.GetComponent<TextMeshProUGUI>() : null;
        if (_value == null) { Destroy(clone); return; }
        _value.color = Blue;
        if (_max != null) { _max.color = BlueMax; _maxBase = _max.transform.localPosition; }

        // Swap the lightning icon for a shield, wherever the row keeps it.
        // Only images that carry an icon (a sprite or texture); plain panels or fills are left alone.
        foreach (var image in clone.GetComponentsInChildren<Image>(true))
            if (image.sprite != null) { image.sprite = Shield(); image.color = Blue; image.preserveAspect = true; }
        foreach (var raw in clone.GetComponentsInChildren<RawImage>(true))
            if (raw.texture != null) { raw.texture = Shield().texture; raw.color = Blue; }
        _row = clone;
        _shownPercent = _shownCap = -1;
    }

    // Heater-shield silhouette, white (tinted at use), anti-aliased; drawn once, nothing shipped.
    private static Sprite Shield()
    {
        if (_shield != null) return _shield;
        const int size = 128;
        var pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            int hits = 0;
            for (int sy = 0; sy < 3; sy++)
            for (int sx = 0; sx < 3; sx++)
                if (Inside((x + (sx + .5f) / 3f) / size * 2f - 1f, 1f - (y + (sy + .5f) / 3f) / size)) hits++;
            pixels[y * size + x] = new Color32(255, 255, 255, (byte)(hits * 255 / 9));
        }
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "IHM Armor Shield", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave,
        };
        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        _shield = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(.5f, .5f), 100f);
        _shield.hideFlags = HideFlags.HideAndDontSave;
        return _shield;
    }

    // u: -1..1 across, v: 0 at the top edge .. 1 at the bottom point.
    private static bool Inside(float u, float v)
    {
        const float top = .08f, bottom = .94f, shoulder = .5f, halfWidth = .78f;
        if (v < top || v > bottom) return false;
        float t = (v - top) / (bottom - top);
        float half = t < shoulder ? halfWidth : halfWidth * Mathf.Sqrt(Mathf.Max(0f, 1f - Mathf.Pow((t - shoulder) / (1f - shoulder), 2f)));
        float dip = .04f * (1f - u * u); // a slightly curved top edge
        return Mathf.Abs(u) <= half && v >= top + dip;
    }
}
