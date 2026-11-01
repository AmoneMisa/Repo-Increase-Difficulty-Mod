using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IncreaseHardMod.Patches.Effects;

/// <summary>
/// End-of-level results board: MVP, OOPS CHAMP and a per-player table (hauled / broken / deaths /
/// revives), drawn with the game's own TMP pixel font on a dim panel under <see cref="HUDCanvas"/>
/// (same approach as <see cref="LevelInfoHud"/>; the canvas is rebuilt per scene so we recreate lazily).
///
/// The data lives in <see cref="RoundStats.Results"/> (host-recorded, broadcast at level end). It is
/// cleared when the next level generates, so a stale board can never linger into a new run.
/// </summary>
internal sealed class RoundResultsHud : MonoBehaviour
{
    private static readonly Color Gold = new(1f, 0.85f, 0.2f);

    private GameObject _panel;
    private TextMeshProUGUI _label;
    private bool _dismissed;
    private float _lastShownAt = -1f;

    private void Update()
    {
        var rows = RoundStats.Results;
        if (!ModConfig.RoundResultsEnabled.Value || rows == null || rows.Count == 0)
        {
            Hide();
            return;
        }

        // New results arrived -> un-dismiss.
        if (RoundStats.ResultsShownAt != _lastShownAt)
        {
            _lastShownAt = RoundStats.ResultsShownAt;
            _dismissed = false;
        }

        if (Input.GetKeyDown(ModConfig.RoundResultsKey.Value))
        {
            if (_dismissed || Time.unscaledTime - RoundStats.ResultsShownAt > ModConfig.RoundResultsSeconds.Value)
            {
                _dismissed = false;
                RoundStats.ResultsShownAt = Time.unscaledTime; // bring it back for another full window
                _lastShownAt = RoundStats.ResultsShownAt;
            }
            else
            {
                _dismissed = true;
            }
        }

        if (_dismissed || Time.unscaledTime - RoundStats.ResultsShownAt > ModConfig.RoundResultsSeconds.Value
            || !Ensure())
        {
            Hide();
            return;
        }

        _label.text = Build(rows);
        _panel.SetActive(true);
    }

    private void Hide()
    {
        if (_panel != null)
            _panel.SetActive(false);
    }

    private bool Ensure()
    {
        if (_panel != null && _label != null)
            return true;
        if (HUDCanvas.instance == null)
            return false;

        TextMeshProUGUI template = StatsUI.instance != null && StatsUI.instance.Text != null
            ? StatsUI.instance.Text
            : HUDCanvas.instance.GetComponentInChildren<TextMeshProUGUI>();
        if (template == null)
            return false;

        _panel = new GameObject("IHM_RoundResults", typeof(RectTransform));
        _panel.transform.SetParent(HUDCanvas.instance.transform, false);
        var prt = (RectTransform)_panel.transform;
        prt.anchorMin = prt.anchorMax = prt.pivot = new Vector2(0.5f, 0.5f);
        prt.sizeDelta = new Vector2(980f, 520f);
        prt.anchoredPosition = Vector2.zero;
        var bg = _panel.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.72f);
        bg.raycastTarget = false;

        var go = new GameObject("Text", typeof(RectTransform));
        go.transform.SetParent(_panel.transform, false);
        _label = go.AddComponent<TextMeshProUGUI>();
        _label.font = template.font;
        _label.fontSharedMaterial = template.fontSharedMaterial;
        _label.fontSize = template.fontSize;
        _label.color = Color.white;
        _label.alignment = TextAlignmentOptions.Top;
        _label.enableWordWrapping = false;
        _label.raycastTarget = false;
        _label.richText = true;
        var rt = _label.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(24f, 16f);
        rt.offsetMax = new Vector2(-24f, -16f);
        return true;
    }

    private static string Hex(Color c) => ColorUtility.ToHtmlStringRGB(c);

    private static string Build(System.Collections.Generic.List<PlayerRoundStat> rows)
    {
        var sorted = new System.Collections.Generic.List<PlayerRoundStat>(rows);
        sorted.Sort((a, b) => b.Score.CompareTo(a.Score));

        PlayerRoundStat mvp = null, oops = null;
        foreach (var r in sorted)
        {
            if (mvp == null && r.Score > 0)
                mvp = r;
            if (r.BrokenValue > 0 && (oops == null || r.BrokenValue > oops.BrokenValue))
                oops = r;
        }

        var sb = new StringBuilder();
        sb.Append($"<color=#{Hex(Gold)}>LEVEL RESULTS</color>\n\n");
        if (mvp != null)
            sb.Append($"<color=#7CFF7C>MVP: {Clean(mvp.Name)}</color>  score {mvp.Score:N0}\n");
        if (oops != null)
            sb.Append($"<color=#FF6B6B>OOPS CHAMP: {Clean(oops.Name)}</color>  broke ${oops.BrokenValue:N0} ({oops.BrokenCount}x)\n");
        if (mvp == null && oops == null)
            sb.Append("Nobody stood out this time.\n");

        sb.Append("\n<color=#AAAAAA>PLAYER<pos=38%>HAULED<pos=58%>BROKEN<pos=76%>DIED<pos=88%>REVIVED</color>\n");
        foreach (var r in sorted)
        {
            sb.Append(Clean(r.Name))
              .Append("<pos=38%>$").Append(r.Delivered.ToString("N0"))
              .Append("<pos=58%>$").Append(r.BrokenValue.ToString("N0"))
              .Append("<pos=76%>").Append(r.Deaths)
              .Append("<pos=88%>").Append(r.Revives)
              .Append('\n');
        }
        int hauled = 0, broken = 0, deaths = 0, revives = 0;
        foreach (var r in sorted)
        {
            hauled += r.Delivered;
            broken += r.BrokenValue;
            deaths += r.Deaths;
            revives += r.Revives;
        }
        sb.Append($"<color=#{Hex(Gold)}>TEAM<pos=38%>${hauled:N0}<pos=58%>${broken:N0}<pos=76%>{deaths}<pos=88%>{revives}</color>\n");
        sb.Append($"\n<color=#888888>{ModConfig.RoundResultsKey.Value} to dismiss</color>");
        return sb.ToString();
    }

    // Player names go into rich text: neutralise tag openers and keep the column layout short.
    private static string Clean(string name)
    {
        if (string.IsNullOrEmpty(name))
            return "?";
        name = name.Replace("<", "").Replace(">", "");
        return name.Length > 16 ? name.Substring(0, 16) : name;
    }
}
