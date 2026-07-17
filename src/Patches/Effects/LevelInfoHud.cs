using System.Collections.Generic;
using IncreaseHardMod.Modifiers;
using UnityEngine;

namespace IncreaseHardMod.Patches.Effects;

/// <summary>
/// Right-side overlay that logs each level change (run number + level name, plus any
/// rolled surprise modifiers). It polls RunManager.instance.levelCurrent every frame,
/// so it works identically for the host and joined clients without relying on any
/// host-only patch. Press LevelInfoToggleKey to hide/show it. Asset-free IMGUI.
/// </summary>
internal sealed class LevelInfoHud : MonoBehaviour
{
    private readonly List<string> _entries = new();
    private Level _lastLevel;
    private bool _hidden;
    private GUIStyle _title;
    private GUIStyle _row;

    private void Update()
    {
        if (!ModConfig.LevelInfoEnabled.Value)
            return;

        if (Input.GetKeyDown(ModConfig.LevelInfoToggleKey.Value))
            _hidden = !_hidden;

        var run = RunManager.instance;
        if (run == null)
            return;

        var current = run.levelCurrent;
        if (current == _lastLevel)
            return;
        _lastLevel = current; // track even for menus, so menu -> level still records

        if (current == null || SafeMenuLevel())
            return; // don't log the main/lobby menus themselves

        _entries.Add(BuildLine(run, current));
        int max = Mathf.Max(1, ModConfig.LevelInfoMaxEntries.Value);
        while (_entries.Count > max)
            _entries.RemoveAt(0);
    }

    private static string BuildLine(RunManager run, Level level)
    {
        string name = !string.IsNullOrEmpty(level.NarrativeName) ? level.NarrativeName : Clean(level.name);

        string line = SemiFunc.RunIsLevel()
            ? $"Lv {run.levelsCompleted + 1}  {name}"
            : name;

        var mods = ModifierManager.Current;
        if (SemiFunc.RunIsLevel() && mods != null && mods.IsSpecial)
            line += $"\n  \u2726 {mods.Active}";

        return line;
    }

    private static string Clean(string raw)
    {
        if (string.IsNullOrEmpty(raw))
            return "Unknown";
        return raw.Replace("Level - ", "").Replace("Level ", "").Trim();
    }

    private void OnGUI()
    {
        if (!ModConfig.LevelInfoEnabled.Value || _hidden || _entries.Count == 0 || SafeMenuLevel())
            return;

        if (_title == null)
        {
            _title = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14, fontStyle = FontStyle.Bold, alignment = TextAnchor.UpperRight,
            };
            _title.normal.textColor = new Color(1f, 0.8f, 0.3f);
            _row = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13, fontStyle = FontStyle.Bold, alignment = TextAnchor.UpperRight, wordWrap = false,
            };
            _row.normal.textColor = Color.white;
        }

        const float w = 320f;
        float x = Screen.width - w - 12f;
        float y = 70f;

        DrawShadowed(new Rect(x, y, w, 20f), "LEVELS", _title);
        y += 24f;

        foreach (string line in _entries)
        {
            int rows = 1 + CountNewlines(line);
            var rect = new Rect(x, y, w, 18f * rows);
            DrawShadowed(rect, line, _row);
            y += 18f * rows + 2f;
        }
    }

    private static void DrawShadowed(Rect rect, string text, GUIStyle style)
    {
        Color prev = style.normal.textColor;
        style.normal.textColor = new Color(0f, 0f, 0f, 0.7f);
        GUI.Label(new Rect(rect.x + 1.5f, rect.y + 1.5f, rect.width, rect.height), text, style);
        style.normal.textColor = prev;
        GUI.Label(rect, text, style);
    }

    private static int CountNewlines(string s)
    {
        int n = 0;
        foreach (char c in s)
            if (c == '\n')
                n++;
        return n;
    }

    private static bool SafeMenuLevel()
    {
        try
        {
            return SemiFunc.MenuLevel();
        }
        catch
        {
            return false;
        }
    }
}
