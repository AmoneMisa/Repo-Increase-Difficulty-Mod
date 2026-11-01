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
    private static readonly Color ModColor = new(0.78f, 0.81f, 0.78f);

    private TextMeshProUGUI _timerLabel;
    private TextMeshProUGUI _modsLabel;
    private TextMeshProUGUI _detailsLabel;
    private bool _suppressed;
    private NativeHudPanel _timerPanel, _modsPanel;
    private NativeHudPanel _detailsPanel;
    private Rect _levelBounds;
    private bool _hasLevelBounds;

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
                _detailsLabel.text=string.Join("\n\n",_parts);
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
            _detailsLabel=Fill(_detailsPanel,"Description",24f,ModColor,TextAlignmentOptions.TopLeft);
            _detailsLabel.enableWordWrapping=true;
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

    private void PositionMods()
    {
        float height = _modsLabel.GetPreferredValues(_modsLabel.text, 440f, Mathf.Infinity).y;
        _modsPanel.Panel.sizeDelta = new Vector2(440f, Mathf.Max(40f, height + 8f));
        float top=250f;
        var canvas=((RectTransform)_modsPanel.transform).rect;
        if(_modsPanel.NativeBounds(GoalUI.instance,out var quota) && quota.center.x>canvas.center.x && quota.center.y>canvas.center.y)
            top=Mathf.Clamp(canvas.yMax-quota.yMin+14f,180f,canvas.height*.65f);
        // The shopping cart sits in the same column; never draw the modifier names over it.
        if(ShoppingListHud.PanelBottom>0f)
            top=Mathf.Max(top,ShoppingListHud.PanelBottom+14f);
        _modsPanel.Panel.anchoredPosition=new Vector2(-24f,-top);
        _modsPanel.Fit();
    }

    private void PositionDetails()
    {
        var canvas=((RectTransform)_detailsPanel.transform).rect;
        float width=Mathf.Min(600f,canvas.width-48f);
        float height=_detailsLabel.GetPreferredValues(_detailsLabel.text,width,Mathf.Infinity).y+12f;
        _detailsPanel.Panel.sizeDelta=new Vector2(width,Mathf.Max(40f,height));
        var position=new Vector2(24f,-300f);
        // LevelUI is hidden most of the time (Tab and the map do not show it), so remember where the
        // level heading last was and keep the description under it instead of the fallback spot.
        if(_detailsPanel.NativeBounds(LevelUI.instance,out var level))
        {
            _levelBounds=level;
            _hasLevelBounds=true;
        }
        if(_hasLevelBounds)
        {
            position.x=Mathf.Clamp(_levelBounds.xMin-canvas.xMin,24f,canvas.width-width-24f);
            position.y=-(canvas.yMax-_levelBounds.yMin+14f);
        }
        position.y=-Mathf.Clamp(-position.y,24f,Mathf.Max(24f,canvas.height-60f));
        _detailsPanel.Panel.anchoredPosition=position;
        _detailsPanel.Fit();
    }

    private void OnDestroy()
    {
        if (_timerPanel != null) Destroy(_timerPanel.gameObject);
        if (_modsPanel != null) Destroy(_modsPanel.gameObject);
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
