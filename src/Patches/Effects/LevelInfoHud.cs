using System.Collections.Generic;
using IncreaseHardMod.Modifiers;
using TMPro;
using UnityEngine;

namespace IncreaseHardMod.Patches.Effects;

/// <summary>
/// Persistent on-screen level info. While you're on a run level (or the shop/truck), a small
/// title at the top-centre shows the current level number + name and, when the run has a special
/// modifier active, that modifier is listed at the top-right beneath the game's quota readout.
/// It reads RunManager.instance every frame so it works the same for the host and joined clients
/// with no host-only patch. Press LevelInfoToggleKey to hide it.
///
/// Rendered with the game's OWN TextMeshPro pixel font: two native <see cref="TextMeshProUGUI"/>
/// labels are created under <see cref="HUDCanvas"/>, cloning the font + material from the quota
/// text (<see cref="StatsUI"/>). That way the overlay matches the in-game HUD style exactly instead
/// of the old IMGUI (Arial) look. The labels live under HUDCanvas, so REPO destroys them on scene
/// change; we recreate them lazily whenever the HUD (and a font template) are available again.
/// </summary>
internal sealed class LevelInfoHud : MonoBehaviour
{
    private static readonly Color Gold = new(1f, 0.8f, 0.3f);
    private static readonly Color ModColor = new(1f, 0.55f, 0.9f);

    private TextMeshProUGUI _titleLabel;
    private TextMeshProUGUI _modsLabel;
    private bool _suppressed;

    private readonly List<string> _parts = new();

    private void Update()
    {
        if (!ModConfig.LevelInfoEnabled.Value)
        {
            HideAll();
            return;
        }

        if (Input.GetKeyDown(ModConfig.LevelInfoToggleKey.Value))
            _suppressed = !_suppressed;

        if (_suppressed || SafeMenuLevel())
        {
            HideAll();
            return;
        }

        var run = RunManager.instance;
        if (run == null || run.levelCurrent == null)
        {
            HideAll();
            return;
        }

        if (!EnsureLabels())
            return; // HUD or font template not ready yet

        _titleLabel.text = BuildTitle(run, run.levelCurrent);
        _titleLabel.enabled = true;

        var mods = ModifierManager.Current;
        if (SafeRunIsLevel() && mods != null && mods.IsSpecial)
        {
            BuildParts(mods, _parts);
            _modsLabel.text = string.Join("\n", _parts);
            _modsLabel.enabled = true;
            PositionMods();
        }
        else
        {
            _modsLabel.enabled = false;
        }
    }

    // Create (or recreate) the two native labels once the HUD canvas and a font template exist.
    private bool EnsureLabels()
    {
        if (HUDCanvas.instance == null)
            return false;

        if (_titleLabel != null && _modsLabel != null)
            return true;

        var template = FindTemplate();
        if (template == null)
            return false;

        // Top-centre title.
        if (_titleLabel == null)
            _titleLabel = CreateLabel("IHM_LevelTitle", template, Gold,
                TextAlignmentOptions.Top, new Vector2(0.5f, 1f), new Vector2(0f, -10f), 820f);

        // Top-right modifier list (positioned per-frame under the quota HUD).
        if (_modsLabel == null)
            _modsLabel = CreateLabel("IHM_LevelMods", template, ModColor,
                TextAlignmentOptions.TopRight, new Vector2(1f, 1f), Vector2.zero, 620f);

        return true;
    }

    private static TextMeshProUGUI CreateLabel(string name, TextMeshProUGUI template, Color color,
        TextAlignmentOptions align, Vector2 anchor, Vector2 offset, float width)
    {
        var go = new GameObject(name);
        go.transform.SetParent(HUDCanvas.instance.transform, false);

        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.font = template.font;                       // game's pixel font asset
        tmp.fontSharedMaterial = template.fontSharedMaterial; // same outline/material as the HUD
        tmp.fontSize = template.fontSize;
        tmp.color = color;
        tmp.alignment = align;
        tmp.enableWordWrapping = false;
        tmp.raycastTarget = false;
        tmp.richText = false;

        var rt = tmp.rectTransform;
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = anchor;
        rt.sizeDelta = new Vector2(width, 40f);
        rt.anchoredPosition = offset;
        return tmp;
    }

    // Keep the modifier list just under the quota / extraction readout, scaling with the canvas.
    private void PositionMods()
    {
        var rect = HUDCanvas.instance.rect;
        float h = rect != null ? rect.rect.height : 1080f;
        _modsLabel.rectTransform.anchoredPosition = new Vector2(-24f, -h * 0.28f);
    }

    private void HideAll()
    {
        if (_titleLabel != null)
            _titleLabel.enabled = false;
        if (_modsLabel != null)
            _modsLabel.enabled = false;
    }

    // Prefer the quota text's font (it sits right where our modifier list goes, so it matches best);
    // otherwise borrow any TMP label under the HUD canvas.
    private static TextMeshProUGUI FindTemplate()
    {
        if (StatsUI.instance != null && StatsUI.instance.Text != null)
            return StatsUI.instance.Text;
        return HUDCanvas.instance != null ? HUDCanvas.instance.GetComponentInChildren<TextMeshProUGUI>() : null;
    }

    // Friendly, readable names for each active modifier TYPE (the raw enum renders as code-y
    // PascalCase). Order follows the enum; the three parametrized modifiers append their rolled
    // value so the HUD conveys not just which twist is active but how strong it is.
    private static readonly (ModifierType Flag, string Name)[] Names =
    {
        (ModifierType.FragileLoot, "Fragile Loot"),
        (ModifierType.HeavyLoot, "Heavy Loot"),
        (ModifierType.LightLoot, "Light Loot"),
        (ModifierType.ProtectedLoot, "Protected Loot"),
        (ModifierType.DangerousLoot, "Dangerous Loot"),
        (ModifierType.ExtraSmallCart, "Extra Small Cart"),
        (ModifierType.ExtraBigCart, "Extra Big Cart"),
        (ModifierType.ExtraSouls, "Extra Souls"),
        (ModifierType.SpreadUpgrades, "Spread Upgrades"),
        (ModifierType.NoMap, "No Map"),
        (ModifierType.ReducedPlayerHp, "Reduced HP"),
        (ModifierType.AutoRevive, "Auto-Revive"),
        (ModifierType.MoneyLossOnDeath, "Money Loss on Death"),
        (ModifierType.LevelTimer, "Timed Level"),
        (ModifierType.ChangeVoices, "Changed Voices"),
        (ModifierType.MoreGachaCubes, "More Gacha Cubes"),
        (ModifierType.SoulsExplodeHarder, "Souls Explode Harder"),
        (ModifierType.BiggerSoulDrops, "Bigger Soul Drops"),
        (ModifierType.MonstersPitTanky, "Pit-Tanky Monsters"),
        (ModifierType.LightMonsters, "Light Monsters"),
        (ModifierType.HeavyMonsters, "Heavy Monsters"),
    };

    private static void BuildParts(LevelModifiers mods, List<string> parts)
    {
        parts.Clear();
        foreach (var (flag, name) in Names)
        {
            if (!mods.Has(flag))
                continue;
            if (flag == ModifierType.ReducedPlayerHp)
                parts.Add($"{name} ({mods.PlayerHpPercent}%)");
            else if (flag == ModifierType.ExtraSouls)
                parts.Add($"{name} ({mods.ExtraSouls})");
            else if (flag == ModifierType.MoneyLossOnDeath)
                parts.Add($"{name} ({mods.MoneyLossPercent}%)");
            else
                parts.Add(name);
        }
        if (parts.Count == 0)
            parts.Add(mods.Active.ToString());
    }

    private static string BuildTitle(RunManager run, Level level)
    {
        string name = !string.IsNullOrEmpty(level.NarrativeName) ? level.NarrativeName : Clean(level.name);
        return SafeRunIsLevel() ? $"Lv {run.levelsCompleted + 1}  -  {name}" : name;
    }

    private static string Clean(string raw)
    {
        if (string.IsNullOrEmpty(raw))
            return "Unknown";
        return raw.Replace("Level - ", "").Replace("Level ", "").Trim();
    }

    private static bool SafeMenuLevel()
    {
        try { return SemiFunc.MenuLevel(); }
        catch { return false; }
    }

    private static bool SafeRunIsLevel()
    {
        try { return SemiFunc.RunIsLevel(); }
        catch { return false; }
    }
}
