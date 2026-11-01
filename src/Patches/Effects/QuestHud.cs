using System.Collections.Generic;
using System.Text;
using IncreaseHardMod.Quests;
using TMPro;
using UnityEngine;

namespace IncreaseHardMod.Patches.Effects;

/// <summary>
/// The quest tracker, drawn with the game's OWN TextMeshPro pixel font (cloned from the quota
/// label via <see cref="StatsUI"/>) so it matches the native HUD, exactly like
/// <see cref="LevelInfoHud"/>. It lists the active team quests (with live progress + reward)
/// and the local player's personal quest, under a header showing the player's contract rank.
///
/// Visibility: on a run level, it shows whenever the full map tool is open (so it lives inside
/// the opened map like the user asked) OR while the peek hotkey is held (for no-map levels).
/// The label lives under HUDCanvas, which REPO destroys on scene change, so we recreate it
/// lazily whenever the HUD and a font template are available again.
/// </summary>
internal sealed class QuestHud : MonoBehaviour
{
    private static readonly Color HeaderColor = new(1f, 0.85f, 0.2f);
    private const string ActiveHex = "#FFFFFF";
    private const string DoneHex = "#7CFF7C";
    private const string FailHex = "#FF6B6B";
    private const string RewardHex = "#FFD24D";

    private TextMeshProUGUI _label;
    private readonly StringBuilder _sb = new();

    private void Update()
    {
        if (!ModConfig.QuestsEnabled.Value)
        {
            Hide();
            return;
        }

        if (SafeMenuLevel() || !SafeRunIsLevel())
        {
            Hide();
            return;
        }

        if (!Visible())
        {
            Hide();
            return;
        }

        if (!EnsureLabel())
            return;

        BuildText();
        _label.text = _sb.ToString();
        _label.enabled = true;
    }

    // Show while the map tool is open, or while the peek key is held.
    private static bool Visible()
    {
        bool mapOpen = MapToolController.instance != null && MapToolController.instance.Active;
        bool peek = Input.GetKey(ModConfig.QuestPeekKey.Value);
        return mapOpen || peek;
    }

    private void BuildText()
    {
        _sb.Clear();

        var rank = QuestManager.LocalRank();
        int perRank = Mathf.Max(1, QuestManager.LocalRankRequirement());
        _sb.Append($"<color={ColorHex(HeaderColor)}>QUESTS  —  Rank {QuestRankInfo.Label(rank)}  ({QuestManager.LocalRankProgress()}/{perRank})</color>\n");

        var team = QuestManager.TeamQuests;
        if (team.Count == 0)
        {
            _sb.Append($"<color={ActiveHex}>No team quests this level.</color>\n");
        }
        else
        {
            foreach (var q in team)
                AppendQuest(q, prefix: "");
        }

        var personal = QuestManager.PersonalQuest;
        if (personal != null)
        {
            _sb.Append("\n");
            AppendQuest(personal, prefix: "You: ");
        }
    }

    private void AppendQuest(ActiveQuest q, string prefix)
    {
        string hex = q.Completed ? DoneHex : (q.Failed ? FailHex : ActiveHex);
        string status = StatusText(q);
        _sb.Append($"<color={hex}>[{QuestRankInfo.Label(q.Rank)}] {prefix}{q.Title}  {status}</color>");
        if (!q.Failed)
            _sb.Append($"  <color={RewardHex}>{QuestManager.RewardLabel(q)}</color>");
        _sb.Append("\n");
    }

    private static string StatusText(ActiveQuest q)
    {
        if (q.Completed)
            return "\u2713";      // check mark
        if (q.Failed)
            return "\u2717";      // cross mark
        switch (q.Objective)
        {
            case ObjectiveType.NoDeaths:
            case ObjectiveType.NoDamage:
            case ObjectiveType.NoGunKills:
                return "...";
            case ObjectiveType.ReachHaul:
                return $"${Mathf.RoundToInt(q.Progress / 1000f)}K/${Mathf.RoundToInt(q.Target / 1000f)}K";
            default:
                return $"{q.Progress}/{q.Target}";
        }
    }

    private bool EnsureLabel()
    {
        if (HUDCanvas.instance == null)
            return false;
        if (_label != null)
            return true;

        var template = FindTemplate();
        if (template == null)
            return false;

        var go = new GameObject("IHM_QuestHud");
        go.transform.SetParent(HUDCanvas.instance.transform, false);

        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.font = template.font;
        tmp.fontSharedMaterial = template.fontSharedMaterial;
        tmp.fontSize = template.fontSize * 0.8f;
        tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.TopLeft;
        tmp.enableWordWrapping = false;
        tmp.raycastTarget = false;
        tmp.richText = true;

        var rt = tmp.rectTransform;
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.sizeDelta = new Vector2(760f, 400f);
        rt.anchoredPosition = new Vector2(24f, -150f);

        _label = tmp;
        return true;
    }

    private void Hide()
    {
        if (_label != null)
            _label.enabled = false;
    }

    private static TextMeshProUGUI FindTemplate()
    {
        if (StatsUI.instance != null && StatsUI.instance.Text != null)
            return StatsUI.instance.Text;
        return HUDCanvas.instance != null ? HUDCanvas.instance.GetComponentInChildren<TextMeshProUGUI>() : null;
    }

    private static string ColorHex(Color c) => $"#{ColorUtility.ToHtmlStringRGB(c)}";

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
