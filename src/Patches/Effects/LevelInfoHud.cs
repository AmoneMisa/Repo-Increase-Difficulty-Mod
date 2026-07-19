using IncreaseHardMod.Modifiers;
using UnityEngine;

namespace IncreaseHardMod.Patches.Effects;

/// <summary>
/// Persistent on-screen level info. While you're on a run level (or the shop/truck), a small
/// panel at the top-centre shows the current level number + name and, when the run has a special
/// modifier active, that modifier. It reads RunManager.instance every frame so it works the same
/// for the host and joined clients with no host-only patch. Press LevelInfoToggleKey to hide it.
///
/// Drawn with IMGUI (no game HUD dependency) so it always renders once a level is loaded, instead
/// of relying on the game's BigMessageUI which only flashes briefly on a level change.
/// </summary>
internal sealed class LevelInfoHud : MonoBehaviour
{
    private static readonly Color Gold = new(1f, 0.8f, 0.3f);
    private static readonly Color ModColor = new(1f, 0.55f, 0.9f);

    private GUIStyle _title;
    private GUIStyle _sub;
    private bool _suppressed;

    private void Update()
    {
        if (!ModConfig.LevelInfoEnabled.Value)
            return;
        if (Input.GetKeyDown(ModConfig.LevelInfoToggleKey.Value))
            _suppressed = !_suppressed;
    }

    private void OnGUI()
    {
        if (!ModConfig.LevelInfoEnabled.Value || _suppressed)
            return;
        if (SafeMenuLevel())
            return;

        var run = RunManager.instance;
        if (run == null || run.levelCurrent == null)
            return;

        EnsureStyles();

        string title = BuildTitle(run, run.levelCurrent);
        var rect = new Rect(0f, 6f, Screen.width, 22f);
        DrawShadowed(rect, title, _title, Gold);

        // Active level modifiers are listed at the TOP-RIGHT, beneath the game's quota / extraction
        // readout (the "$x / $goal" and "n / m" lines), one modifier per line, right-aligned.
        var mods = ModifierManager.Current;
        if (SafeRunIsLevel() && mods != null && mods.IsSpecial)
        {
            var parts = BuildParts(mods);
            const float w = 620f;
            const float lineH = 30f;
            float x = Screen.width - w - 24f;
            float y = Screen.height * 0.30f; // clears the quota + extraction-point HUD above it
            for (int i = 0; i < parts.Count; i++)
            {
                var lineRect = new Rect(x, y + i * lineH, w, lineH);
                DrawShadowed(lineRect, $"\u2726 {parts[i]}", _sub, ModColor);
            }
        }
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

    private static System.Collections.Generic.List<string> BuildParts(LevelModifiers mods)
    {
        var parts = new System.Collections.Generic.List<string>();
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
        return parts;
    }

    private static string BuildTitle(RunManager run, Level level)
    {
        string name = !string.IsNullOrEmpty(level.NarrativeName) ? level.NarrativeName : Clean(level.name);
        return SafeRunIsLevel() ? $"Lv {run.levelsCompleted + 1}  -  {name}" : name;
    }

    private static void DrawShadowed(Rect rect, string text, GUIStyle style, Color color)
    {
        var prev = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.9f);
        GUI.Label(new Rect(rect.x + 2f, rect.y + 2f, rect.width, rect.height), text, style);
        GUI.color = color;
        GUI.Label(rect, text, style);
        GUI.color = prev;
    }

    private void EnsureStyles()
    {
        _title ??= new GUIStyle
        {
            fontSize = 16,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            richText = false,
        };
        _sub ??= new GUIStyle
        {
            fontSize = 24,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleRight,
            richText = false,
        };
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
