using System.Collections.Generic;
using System.Text;
using IncreaseHardMod.Quests;
using TMPro;
using UnityEngine;

namespace IncreaseHardMod.Patches.Effects;

/// <summary>Bounded quest sidebar that follows the visible upgrade list without covering the map.</summary>
internal sealed class QuestHud : MonoBehaviour
{
    private TextMeshProUGUI _label;
    private NativeHudPanel _surface;
    private readonly List<string> _pages = new();
    private int _page;
    private bool _questsVisible;
    internal void ToggleQuests() { _questsVisible = !_questsVisible; _page = 0; }
    private bool ShowQuests => ModConfig.QuestsEnabled.Value && _questsVisible;
    private const float Margin = 24f;

    private void LateUpdate()
    {
        if (!LevelInfoHud.InfoReady || SafeMenuLevel() || !SafeRunIsLevel())
        {
            _questsVisible = false;
            Hide();
            return;
        }
        if (ModConfig.QuestsEnabled.Value && SemiFunc.NoTextInputsActive() && Input.GetKeyDown(ModConfig.QuestPeekKey.Value)) ToggleQuests();
        if (!ShowQuests)
        {
            Hide();
            return;
        }
        if (!EnsureLabel()) return;
        _surface.gameObject.SetActive(true);
        Position();
        if (_surface.Panel.sizeDelta.y < 80f) { Hide(); return; }
        BuildText();
        if (_pages.Count > 1)
        {
            if (SemiFunc.NoTextInputsActive() && Input.GetKeyDown(KeyCode.PageDown)) _page = (_page + 1) % _pages.Count;
            if (SemiFunc.NoTextInputsActive() && Input.GetKeyDown(KeyCode.PageUp)) _page = (_page + _pages.Count - 1) % _pages.Count;
        }
        _page = Mathf.Clamp(_page, 0, Mathf.Max(0, _pages.Count - 1));
        _label.text = Header() + _pages[_page] + (_pages.Count > 1 ? $"\n<size=80%>PGUP / PGDN  {_page + 1}/{_pages.Count}</size>" : "");
        _label.enabled = true;
    }

    private bool EnsureLabel()
    {
        if (_surface != null && !_surface.HasCurrentOwner)
        {
            Destroy(_surface.gameObject);
            _surface = null;
            _label = null;
        }
        if (_surface == null)
        {
            _surface = NativeHudPanel.Create("IHM_QuestHud", new Vector2(380f, 300f),
                new Vector2(0f, 1f), new Vector2(Margin, -200f), 0f);
            if (_surface == null) return false;
            _label = _surface.Label("Quests", 22f, TextAlignmentOptions.TopLeft);
            _label.enableWordWrapping = true;
            _label.enableAutoSizing = false;
            _label.overflowMode = TextOverflowModes.Ellipsis;
            var rt = _label.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }
        return _label != null;
    }

    private void Position()
    {
        var canvas = _surface.Viewport;
        float width = Mathf.Min(380f, canvas.width * .28f);
        float left = canvas.xMin + Margin;
        float top = canvas.yMax - 180f;
        float bottom = canvas.yMin + 140f; // Leave the focus hint and inventory band clear.
        foreach (var vital in new Component[] { HealthUI.instance, EnergyUI.instance })
            if (_surface.NativeBounds(vital, out var bounds) && bounds.center.x < canvas.center.x)
                top = Mathf.Min(top, bounds.yMin - 18f);
        if (_surface.NativeBounds(StatsUI.instance, out var upgrades, true) && upgrades.center.x < canvas.center.x)
        {
            // Choose the larger free interval above/below the complete visible upgrade group.
            float belowTop = Mathf.Min(top, upgrades.yMin - 18f);
            float aboveBottom = Mathf.Max(bottom, upgrades.yMax + 18f);
            if (belowTop - bottom >= top - aboveBottom) top = belowTop;
            else bottom = aboveBottom;
            left = Mathf.Clamp(upgrades.xMin, canvas.xMin + Margin, canvas.xMax - width - Margin);
        }
        float height = Mathf.Max(0f, top - bottom);
        // Do not paint over native widgets when no usable sidebar interval exists.
        _surface.Panel.sizeDelta = new Vector2(width, height);
        _surface.Panel.anchoredPosition = new Vector2(left - canvas.xMin, top - canvas.yMax);
        _label.fontSize = height < 180f ? 18f : 22f;
        _surface.Fit();
    }

    private string Header() => "<color=#FFD24D><b>QUESTS</b></color>\n";

    private void BuildText()
    {
        _pages.Clear();
        var body = new StringBuilder();
        var entries = new List<string>();
        if (ShowQuests)
        {
            entries.Add($"<color=#FFD24D><b>RANK {QuestRankInfo.Label(QuestManager.LocalRank())}  {QuestManager.LocalRankProgress()}/{Mathf.Max(1, QuestManager.LocalRankRequirement())}</b></color>\n");
            foreach (var q in QuestManager.TeamQuests) if (q != null) entries.Add(Entry(q, ""));
            if (QuestManager.PersonalQuest != null) entries.Add(Entry(QuestManager.PersonalQuest, "YOU: "));
        }
        if (entries.Count == 0) entries.Add("NO QUESTS THIS LEVEL.\n");
        float width = _surface.Panel.sizeDelta.x;
        float height = _surface.Panel.sizeDelta.y;
        foreach (string entry in entries)
        {
            string candidate = Header() + body + entry + "\n<size=80%>PGUP / PGDN  1/6</size>";
            if (body.Length > 0 && _label.GetPreferredValues(candidate, width, Mathf.Infinity).y > height)
            {
                _pages.Add(body.ToString());
                body.Clear();
            }
            body.Append(entry);
        }
        if (body.Length > 0) _pages.Add(body.ToString());
    }

    private static string Entry(ActiveQuest q, string prefix)
    {
        string colour = q.Completed ? "#7CFF7C" : q.Failed ? "#FF6B6B" : "#FFFFFF";
        string title = Clean(q.Title);
        string line = $"<color={colour}>[{QuestRankInfo.Label(q.Rank)}] {prefix}{title}  {StatusText(q)}</color>\n";
        if (!q.Failed) line += $"<size=90%><color=#FFD24D>{Clean(QuestManager.RewardLabel(q))}</color></size>\n";
        return line;
    }

    private static string Clean(string value) => (value ?? "").Replace("<", "[").Replace(">", "]").Replace("\r", " ").Replace("\n", " ");

    private static string StatusText(ActiveQuest q)
    {
        if (q.Completed) return "DONE";
        if (q.Failed) return "FAILED";
        return q.Objective switch
        {
            ObjectiveType.NoDeaths or ObjectiveType.NoDamage or ObjectiveType.NoGunKills => "...",
            ObjectiveType.ReachHaul => $"${Mathf.RoundToInt(q.Progress / 1000f)}K/${Mathf.RoundToInt(q.Target / 1000f)}K",
            _ => $"{q.Progress}/{q.Target}",
        };
    }

    private void Hide()
    {
        if (_surface != null) _surface.gameObject.SetActive(false);
        if (_label != null) _label.enabled = false;
    }

    private void OnDestroy() { if (_surface != null) Destroy(_surface.gameObject); }
    private static bool SafeMenuLevel() { try { return SemiFunc.MenuLevel(); } catch { return false; } }
    private static bool SafeRunIsLevel() { try { return SemiFunc.RunIsLevel(); } catch { return false; } }
}
