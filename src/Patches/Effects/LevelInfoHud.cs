using IncreaseHardMod.Modifiers;
using UnityEngine;

namespace IncreaseHardMod.Patches.Effects;

/// <summary>
/// Announces each level change using REPO's own styled HUD (the big centred message,
/// <see cref="BigMessageUI"/> via <c>SemiFunc.UIBigMessage</c>) instead of raw IMGUI,
/// so the text is crisp and matches the game. It polls RunManager.instance.levelCurrent
/// every frame, so it works identically for host and joined clients without any host-only
/// patch. Press LevelInfoToggleKey to suppress the announcement.
///
/// BigMessageUI hides itself ~0.2s after the last call, so we re-issue the message every
/// frame for <see cref="DisplaySeconds"/> to keep it readable.
/// </summary>
internal sealed class LevelInfoHud : MonoBehaviour
{
    private const float DisplaySeconds = 4f;
    private static readonly Color Gold = new(1f, 0.8f, 0.3f);

    private Level _lastLevel;
    private string _message;
    private float _timer;
    private bool _suppressed;

    private void Update()
    {
        if (!ModConfig.LevelInfoEnabled.Value)
            return;

        if (Input.GetKeyDown(ModConfig.LevelInfoToggleKey.Value))
            _suppressed = !_suppressed;

        DetectLevelChange();

        // Keep re-issuing so the game's HUD keeps it on screen for the full duration.
        if (_timer > 0f && !_suppressed)
        {
            _timer -= Time.deltaTime;
            ShowBigMessage(_message);
        }
    }

    private void DetectLevelChange()
    {
        var run = RunManager.instance;
        if (run == null)
            return;

        var current = run.levelCurrent;
        if (current == _lastLevel)
            return;
        _lastLevel = current; // track even for menus, so menu -> level still triggers

        if (current == null || SafeMenuLevel())
            return; // don't announce the main/lobby menus themselves

        _message = BuildMessage(run, current);
        _timer = DisplaySeconds;
    }

    private static string BuildMessage(RunManager run, Level level)
    {
        string name = !string.IsNullOrEmpty(level.NarrativeName) ? level.NarrativeName : Clean(level.name);

        string msg = SemiFunc.RunIsLevel()
            ? $"Lv {run.levelsCompleted + 1}  -  {name}"
            : name;

        var mods = ModifierManager.Current;
        if (SemiFunc.RunIsLevel() && mods != null && mods.IsSpecial)
            msg += $"\n<size=60%>\u2726 {mods.Active}</size>";

        return msg;
    }

    private static void ShowBigMessage(string message)
    {
        try
        {
            if (BigMessageUI.instance != null)
                SemiFunc.UIBigMessage(message, "", 40f, Gold, Color.white);
        }
        catch
        {
            // HUD not ready yet (e.g. mid-transition); ignore this frame.
        }
    }

    private static string Clean(string raw)
    {
        if (string.IsNullOrEmpty(raw))
            return "Unknown";
        return raw.Replace("Level - ", "").Replace("Level ", "").Trim();
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
