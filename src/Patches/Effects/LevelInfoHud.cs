using System.Collections.Generic;
using IncreaseHardMod.Modifiers;
using TMPro;
using UnityEngine;

namespace IncreaseHardMod.Patches.Effects;

/// <summary>Bounded native-font modifier labels below the quota and level-number widgets.</summary>
internal sealed class LevelInfoHud : MonoBehaviour
{
    private static readonly Color TimerColor = Color.white;
    private static readonly Color TimerWarn = new(1f, 0.3f, 0.3f);
    private static readonly Color ModColor = new(0.78f, 0.81f, 0.78f);

    private TextMeshProUGUI _timerLabel;
    private TextMeshProUGUI _modsLabel;
    private TextMeshProUGUI _detailsLabel;
    private bool _suppressed;
    private NativeHudPanel _timerPanel, _modsPanel;
    private NativeHudPanel _detailsPanel;

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
            bool details=(Map.Instance != null && Map.Instance.Active) || SemiFunc.InputHold(InputKey.Map);
            if (econ != null)
            {
                _parts.Add(econ.Name);
            }
            if (shopEcon != null)
            {
                _parts.Add(shopEcon.Name);
            }
            if (market)
                _parts.Add($"[{ModConfig.BlackMarketKey.Value}] Black Market");
            _modsLabel.text = string.Join("\n", _parts);
            _modsLabel.enabled = true;
            PositionMods();
            _detailsLabel.enabled=details;
            if(details)
            {
                _parts.Clear();
                if(special)
                {
                    foreach(var (flag,name) in Names)
                        if(mods.Has(flag))_parts.Add(name+": "+ModConfig.ModifierDescription(flag));
                    foreach(int id in mods.Stats)
                    {
                        var stat=StatModifiers.Get(id);
                        if(stat!=null)_parts.Add(stat.Name+": "+stat.Description);
                    }
                }
                if(econ!=null)_parts.Add(econ.Name+": "+econ.Description);
                if(shopEcon!=null)_parts.Add(shopEcon.Name+": "+shopEcon.Description);
                _detailsLabel.text=string.Join("\n",_parts);
                _detailsLabel.enabled=_parts.Count>0;
                PositionDetails();
            }
        }
        else
        {
            _modsLabel.enabled = false;
            _detailsLabel.enabled=false;
        }
    }

    private bool EnsureLabels()
    {
        if (HUDCanvas.instance == null) return false;
        if (_timerPanel == null)
        {
            _timerPanel = NativeHudPanel.Create("IHM_LevelTimer", new Vector2(180f, 46f),
                new Vector2(0.5f, 1f), new Vector2(0f, -20f), 0f);
            if (_timerPanel == null) return false;
            _timerLabel = Fill(_timerPanel, "Countdown", 32f, TimerColor, TextAlignmentOptions.Top);
        }
        if (_modsPanel == null)
        {
            _modsPanel = NativeHudPanel.Create("IHM_LevelMods", new Vector2(440f, 420f),
                new Vector2(1f, 1f), new Vector2(-24f, -300f), 0f);
            if (_modsPanel == null) return false;
            _modsLabel = Fill(_modsPanel, "Modifiers", 30f, ModColor, TextAlignmentOptions.TopRight);
            _modsLabel.enableWordWrapping = true;
        }
        if(_detailsPanel==null)
        {
            _detailsPanel=NativeHudPanel.Create("IHM_LevelModifierDetails",new Vector2(600f,300f),new Vector2(0f,1f),new Vector2(24f,-300f),0f);
            if(_detailsPanel==null)return false;
        }
        if (_detailsLabel == null)
        {
            _detailsLabel=Fill(_detailsPanel,"Description",22f,ModColor,TextAlignmentOptions.TopLeft);
            _detailsLabel.enableWordWrapping=true;
            _detailsLabel.maskable=false;
        }
        _timerPanel.Fit();
        _modsPanel.Fit();
        return true;
    }

    private static TextMeshProUGUI Fill(NativeHudPanel panel, string name, float size, Color color,
        TextAlignmentOptions alignment)
    {
        var text = panel.Label(name, size, alignment);
        text.color = color;
        text.richText = false;
        var rt = text.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        return text;
    }

    private void LateUpdate()
    {
        // Native SemiUI animates during Update; measure its final position for this frame.
        if (_modsLabel != null && _modsLabel.enabled) PositionMods();
        if (_detailsLabel != null && _detailsLabel.enabled) PositionDetails();
    }

    private void PositionMods()
    {
        var canvas = _modsPanel.Viewport;
        float width = Mathf.Min(380f, canvas.width * .28f);
        float height = _modsLabel.GetPreferredValues(_modsLabel.text, width, Mathf.Infinity).y;
        _modsPanel.Panel.sizeDelta = new Vector2(width, Mathf.Max(40f, height + 12f));
        // Conservative fallback remains below both money and extraction counters.
        float top = 320f;
        bool found = false;
        foreach (var anchor in new Component[] { GoalUI.instance, HaulUI.instance })
            if (_modsPanel.NativeBounds(anchor, out var bounds) && bounds.center.x > canvas.center.x)
            {
                float below = canvas.yMax - bounds.yMin + 18f;
                top = found ? Mathf.Max(top, below) : below;
                found = true;
            }
        if (ShoppingListHud.PanelBottom > 0f) top = Mathf.Max(top, ShoppingListHud.PanelBottom + 18f);
        _modsPanel.Panel.anchoredPosition = new Vector2(-24f, -top);
        _modsPanel.Fit();
    }

    private void PositionDetails()
    {
        var heading = LevelUI.instance != null ? LevelUI.instance.textNumber : null;
        if (heading == null || !heading.isActiveAndEnabled || !_detailsPanel.NativeBounds(heading, out var level))
        {
            _detailsLabel.enabled = false;
            return;
        }
        // Follow the level NUMBER in screen coordinates. Its scale must not scale paragraph text.
        var canvas = _detailsPanel.Viewport;
        float width = Mathf.Min(380f, canvas.width * .28f);
        float center = Mathf.Clamp(level.center.x, canvas.xMin + width * .5f + 24f, canvas.xMax - width * .5f - 24f);
        float top = level.yMin - 16f;
        float available = top - (canvas.yMin + 140f);
        if (available < 60f) { _detailsLabel.enabled = false; return; }
        float height = _detailsLabel.GetPreferredValues(_detailsLabel.text, width, Mathf.Infinity).y + 12f;
        var panel = _detailsPanel.Panel;
        panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(.5f, 1f);
        panel.sizeDelta = new Vector2(width, Mathf.Min(height, available));
        panel.anchoredPosition = new Vector2(center - canvas.center.x, top - canvas.yMax);
        _detailsLabel.enableAutoSizing = true;
        _detailsLabel.fontSizeMax = 22f;
        _detailsLabel.fontSizeMin = 18f;
        _detailsLabel.fontSize = 22f;
        _detailsPanel.Fit();
    }

    private void OnDestroy()
    {
        if (_timerPanel != null) Destroy(_timerPanel.gameObject);
        if (_modsPanel != null) Destroy(_modsPanel.gameObject);
        // Labels and style owners are recreated with the native HUD.
        if (_detailsLabel != null) Destroy(_detailsLabel.gameObject);
        if (_detailsPanel != null) Destroy(_detailsPanel.gameObject);
    }

    private void HideAll()
    {
        if (_timerLabel != null)
            _timerLabel.enabled = false;
        if (_modsLabel != null)
            _modsLabel.enabled = false;
        if(_detailsLabel!=null)_detailsLabel.enabled=false;
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
        foreach (int id in mods.Stats)
        {
            var sm = StatModifiers.Get(id);
            if (sm != null)
                parts.Add(sm.Name);
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
