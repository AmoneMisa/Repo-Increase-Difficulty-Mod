using System.Collections.Generic;
using IncreaseHardMod.Modifiers;
using TMPro;
using UnityEngine;

namespace IncreaseHardMod.Patches.Effects;

/// <summary>
/// Persistent on-screen level info, rendered with the game's OWN TextMeshPro pixel font so it matches
/// the in-game HUD exactly (two native <see cref="TextMeshProUGUI"/> labels under <see cref="HUDCanvas"/>,
/// cloning the font + material from the quota text via <see cref="StatsUI"/>).
///
/// - Top-centre: the LevelTimer countdown, shown only while a timed level is running (white, turning
///   red in the final seconds). Driven by <see cref="LevelTimerController"/>.
/// - Top-right: when the run has a special modifier active, the list of active modifiers, beneath the
///   game's quota readout.
///
/// It reads <see cref="RunManager"/> / the timer state every frame so it works the same for the host
/// and joined clients with no host-only patch. The labels live under HUDCanvas, so REPO destroys them
/// on scene change; we recreate them lazily whenever the HUD (and a font template) are available again.
/// Press LevelInfoToggleKey to hide it.
/// </summary>
internal sealed class LevelInfoHud : MonoBehaviour
{
    private static readonly Color TimerColor = Color.white;
    private static readonly Color TimerWarn = new(1f, 0.3f, 0.3f);
    private static readonly Color ModColor = new(1f, 0.55f, 0.9f);

    private TextMeshProUGUI _timerLabel;
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

        // Top-centre countdown for the LevelTimer modifier.
        if (LevelTimerController.Running)
        {
            float remaining = Mathf.Max(0f, LevelTimerController.Remaining);
            int minutes = (int)(remaining / 60f);
            int seconds = (int)(remaining % 60f);
            _timerLabel.text = $"{minutes:0}:{seconds:00}";
            _timerLabel.color = remaining <= 30f ? TimerWarn : TimerColor;
            _timerLabel.enabled = true;
        }
        else
        {
            _timerLabel.enabled = false;
        }

        var mods = ModifierManager.Current;
        bool special = SafeRunIsLevel() && mods != null && mods.IsSpecial;
        var econ = SafeRunIsLevel() ? Economy.EconomyEvents.Current : null;
        bool inShop = false;
        try { inShop = SemiFunc.RunIsShop(); } catch { }
        var shopEcon = inShop ? Economy.ShopEconomy.Current : null;
        bool market = inShop && ModConfig.BlackMarketEnabled.Value;
        if (special || econ != null || shopEcon != null || market)
        {
            _parts.Clear();
            if (special)
                BuildParts(mods, _parts);
            if (econ != null)
                _parts.Add($"Economy: {econ.Name}");
            if (shopEcon != null)
                _parts.Add($"Shop: {shopEcon.Name}");
            if (market)
                _parts.Add($"[{ModConfig.BlackMarketKey.Value}] Black Market");
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

        if (_timerLabel != null && _modsLabel != null)
            return true;

        var template = FindTemplate();
        if (template == null)
            return false;

        // Top-centre timer.
        if (_timerLabel == null)
            _timerLabel = CreateLabel("IHM_LevelTimer", template, TimerColor,
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
        if (_timerLabel != null)
            _timerLabel.enabled = false;
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
    internal static readonly (ModifierType Flag, string Name)[] Names =
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

    internal static string NameOf(ModifierType m)
    {
        foreach (var (flag, name) in Names)
            if (flag == m)
                return name;
        return m.ToString();
    }

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
