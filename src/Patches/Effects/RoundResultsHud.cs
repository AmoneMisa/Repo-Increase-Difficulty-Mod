using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IncreaseHardMod.Patches.Effects;

/// <summary>
/// End-of-level results board: MVP, OOPS CHAMP and a per-player table (hauled / broken / deaths /
/// revives), drawn with the game's own TMP pixel font on a dim, resolution-scaled overlay.
/// The canvas is rebuilt per scene, so we recreate lazily with the current HUD's font.
///
/// The data lives in <see cref="RoundStats.Results"/> (host-recorded, broadcast at level end). It is
/// cleared when the next level generates, so a stale board can never linger into a new run.
/// </summary>
internal sealed class RoundResultsHud : MonoBehaviour
{
    private static readonly Color Gold = new(1f, 0.85f, 0.2f);

    private GameObject _panel;
    private TextMeshProUGUI _label;
    private NativeHudPanel _surface;
    private bool _dismissed;
    private float _lastShownAt = -1f;
    private bool _manual;
    private int _page;
    private const int PageSize = 8;

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
            _manual = false;
            _page = 0;
        }

        // Keep the results data for F7, but never force a board over shopping/gameplay.
        bool shop = RunManager.instance != null && RunManager.instance.levelCurrent != null && SemiFunc.RunIsShop();
        if (shop && !_manual) _dismissed = true;

        if (Input.GetKeyDown(ModConfig.RoundResultsKey.Value))
        {
            if (_dismissed || Time.unscaledTime - RoundStats.ResultsShownAt > ModConfig.RoundResultsSeconds.Value)
            {
                _dismissed = false;
                _manual = true;
                RoundStats.ResultsShownAt = Time.unscaledTime; // bring it back for another full window
                _lastShownAt = RoundStats.ResultsShownAt;
            }
            else
            {
                _dismissed = true;
                _manual = false;
            }
        }
        if (Input.GetKeyDown(KeyCode.Escape)) { _dismissed = true; _manual = false; }
        int pages = Mathf.Max(1, (rows.Count + PageSize - 1) / PageSize);
        if (Input.GetKeyDown(KeyCode.PageDown)) _page = (_page + 1) % pages;
        if (Input.GetKeyDown(KeyCode.PageUp)) _page = (_page + pages - 1) % pages;

        if (_dismissed || Time.unscaledTime - RoundStats.ResultsShownAt > ModConfig.RoundResultsSeconds.Value
            || !Ensure())
        {
            Hide();
            return;
        }

        _label.text = Build(rows, _page);
        _panel.SetActive(true);
        _surface.Fit();
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
        _surface = NativeHudPanel.Fullscreen("IHM_RoundResults");
        if (_surface == null)
            return false;
        _panel = _surface.gameObject;
        _label = _surface.Label("Results", 32f, TextAlignmentOptions.TopLeft);
        _label.enableAutoSizing = true;
        _label.fontSizeMin = 22f;
        _label.fontSizeMax = 32f;
        var rt = _label.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(96f, 64f);
        rt.offsetMax = new Vector2(-96f, -64f);
        return true;
    }

    private static string Hex(Color c) => ColorUtility.ToHtmlStringRGB(c);

    private static string Build(System.Collections.Generic.List<PlayerRoundStat> rows, int page)
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
        bool ru = Localization.GameLanguage.Russian;
        sb.Append($"<line-height=115%><size=54><color=#{Hex(Gold)}>{(ru ? "ИТОГИ УРОВНЯ" : "LEVEL RESULTS")}</color></size>\n\n");
        var summary = RoundStats.ResultsSummary;
        if (summary != null)
        {
            if (ru)
            {
                sb.Append($"ДОБЫЧА НА УРОВНЕ В НАЧАЛЕ: ${summary.InitialValue:N0}  ({summary.InitialCount} шт.)\n");
                sb.Append($"НЕ СОБРАНО: {summary.RemainingCount} шт. / ${summary.RemainingValue:N0}\n");
                sb.Append($"  НЕ НАЙДЕНО (входит в число выше): {summary.UnfoundCount} шт. / ${summary.UnfoundValue:N0}\n");
                sb.Append($"ДОХОД С ДУШ: ${summary.SoulIncome:N0}  |  Сдано душ на: ${summary.SoulHaul:N0}\n\n");
            }
            else
            {
                sb.Append($"LEVEL LOOT AT START: ${summary.InitialValue:N0}  ({summary.InitialCount} items)\n");
                sb.Append($"LEFT UNCOLLECTED: {summary.RemainingCount} items / ${summary.RemainingValue:N0}\n");
                sb.Append($"  UNFOUND (included above): {summary.UnfoundCount} items / ${summary.UnfoundValue:N0}\n");
                sb.Append($"SOUL INCOME: ${summary.SoulIncome:N0}  |  Extracted value: ${summary.SoulHaul:N0}\n\n");
            }
        }
        if (mvp != null)
            sb.Append($"<color=#7CFF7C>MVP: {Clean(mvp.Name)}</color>  {(ru ? "очки" : "score")} {mvp.Score:N0}\n");
        if (oops != null)
            sb.Append(ru ? $"<color=#FF6B6B>ЧЕМПИОН ПО «УПС»: {Clean(oops.Name)}</color>  разбил на ${oops.BrokenValue:N0} ({oops.BrokenCount}x)\n"
                : $"<color=#FF6B6B>OOPS CHAMP: {Clean(oops.Name)}</color>  broke ${oops.BrokenValue:N0} ({oops.BrokenCount}x)\n");
        if (mvp == null && oops == null)
            sb.Append(ru ? "В этот раз никто не выделился.\n" : "Nobody stood out this time.\n");

        sb.Append(ru ? "\n<color=#AAAAAA>ИГРОК<pos=38%>СДАНО<pos=58%>РАЗБИТО<pos=76%>СМЕРТИ<pos=88%>ВОСКРЕШ.</color>\n"
            : "\n<color=#AAAAAA>PLAYER<pos=38%>HAULED<pos=58%>BROKEN<pos=76%>DIED<pos=88%>REVIVED</color>\n");
        int first = Mathf.Clamp(page * PageSize, 0, Mathf.Max(0, sorted.Count - 1));
        for (int i = first; i < Mathf.Min(sorted.Count, first + PageSize); i++)
        {
            var r = sorted[i];
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
        sb.Append($"<color=#{Hex(Gold)}>{(ru ? "КОМАНДА" : "TEAM")}<pos=38%>${hauled:N0}<pos=58%>${broken:N0}<pos=76%>{deaths}<pos=88%>{revives}</color>\n");
        sb.Append($"\n<color=#BBBBBB>{ModConfig.RoundResultsKey.Value} / ESC  {(ru ? "ЗАКРЫТЬ" : "CLOSE")}");
        if (rows.Count > PageSize) sb.Append($"   PGUP / PGDN  {(ru ? "СТРАНИЦА" : "PAGE")} {page + 1}/{(rows.Count + PageSize - 1) / PageSize}");
        sb.Append("</color>");
        return sb.ToString();
    }

    private void OnDestroy()
    {
        if (_surface != null)
            Destroy(_surface.gameObject);
    }

    // Player names go into rich text: neutralise tag openers and keep the column layout short.
    private static string Clean(string name)
    {
        if (string.IsNullOrEmpty(name))
            return "?";
        name = name.Replace("<", "").Replace(">", "").Replace("\n", " ").Replace("\r", " ");
        return name.Length > 16 ? name.Substring(0, 16) : name;
    }
}
