using System.Collections.Generic;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using IncreaseHardMod.Modifiers;
using TMPro;
using UnityEngine;

namespace IncreaseHardMod.Patches.Effects;

/// <summary>Native quota-aligned names and a manually paged details region inside the map Tab.</summary>
internal sealed class LevelInfoHud : MonoBehaviour
{
    private static readonly Color TimerColor = Color.white;
    private static readonly Color TimerWarn = new(1f, 0.3f, 0.3f);
    private static readonly Color ModColor = new(1f, 0.62f, 0.08f);

    private TextMeshProUGUI _timerLabel;
    private TextMeshProUGUI _modsLabel;
    private TextMeshProUGUI _descriptionLabel;
    private readonly List<string> _descriptions = new();
    private readonly List<string> _effectDescriptions = new();
    private readonly Vector3[] _corners = new Vector3[4];
    private bool _suppressed;
    private TextMeshProUGUI _detailsPageLabel;
    private bool _tabWasOpen;
    private bool _mapSection;
    private int _detailsPage = 1;
    private bool _layoutReady;
    private LevelGenerator _generation;
    private string _owner;
    private Room _room;
    private string _request;
    private LevelModifiers _snapshotModifiers;
    private int _snapshotEconomy;
    private int _snapshotShop;
    private static LevelInfoHud _instance;
    private readonly Dictionary<int, (string Request, string Level, string Sent)> _readers = new();
    private Room _readersRoom;

    // One reliable request per ready scene/host. The host retains requests while loading and
    // answers after its own generation finishes. A fresh nonce rejects delayed scene/room replies.
    internal const byte StateEventCode = 197;
    internal static bool GameplayReady => RunManager.instance != null &&
        RunManager.instance.levelCurrent != null && !RunManager.instance.restarting && !SafeMenuLevel() &&
        LevelGenerator.Instance != null && LevelGenerator.Instance.Generated &&
        LevelGenerator.Instance.Level == RunManager.instance.levelCurrent &&
        GameDirector.instance != null &&
        (GameDirector.instance.currentState == GameDirector.gameState.Main ||
         GameDirector.instance.currentState == GameDirector.gameState.Death) &&
        (LoadingUI.instance == null || !LoadingUI.instance.gameObject.activeInHierarchy) &&
        HUDCanvas.instance != null && HUDCanvas.instance.gameObject.activeInHierarchy;
    internal static bool TabOpen => Map.Instance != null && Map.Instance.Active;
    internal static bool InfoReady => GameplayReady &&
        (SemiFunc.IsMasterClientOrSingleplayer() || (_instance != null && _instance.SnapshotReady));
    private static string LevelKey => RunManager.instance.levelCurrent.name + "|" +
        RunManager.instance.levelsCompleted;
    private static string OwnerKey => PhotonNetwork.InRoom
        ? PhotonNetwork.CurrentRoom.Name + "|" + PhotonNetwork.MasterClient?.ActorNumber : "local";
    private bool SnapshotReady => _request != null && _snapshotModifiers != null &&
        _generation == LevelGenerator.Instance && _owner == OwnerKey && _room == PhotonNetwork.CurrentRoom;

    internal static void HandleStateEvent(EventData ev)
    {
        if (_instance == null || !PhotonNetwork.InRoom || ev.CustomData is not object[] data ||
            data.Length < 2 || data[0] is not string nonce || nonce.Length != 32 ||
            data[1] is not string key || key.Length > 256) return;
        if (PhotonNetwork.IsMasterClient && data.Length == 2)
        {
            if (_instance._readersRoom != PhotonNetwork.CurrentRoom)
            {
                _instance._readers.Clear();
                _instance._readersRoom = PhotonNetwork.CurrentRoom;
            }
            _instance._readers[ev.Sender] = (nonce, key, null);
            return;
        }
        if (PhotonNetwork.IsMasterClient || PhotonNetwork.MasterClient == null ||
            ev.Sender != PhotonNetwork.MasterClient.ActorNumber || data.Length != 5 ||
            !GameplayReady || nonce != _instance._request || key != LevelKey ||
            _instance._generation != LevelGenerator.Instance || _instance._owner != OwnerKey ||
            _instance._room != PhotonNetwork.CurrentRoom ||
            data[2] is not object[] mods || mods.Length < 4 ||
            mods[0] is not int || mods[1] is not int || mods[2] is not int || mods[3] is not int ||
            data[3] is not int economy || economy < 0 || economy > Economy.EconomyEvents.All.Count ||
            data[4] is not int shop || shop < 0 || shop > Economy.ShopEconomy.All.Count) return;
        // This is a HUD snapshot only: never reset gameplay effects/timers for a display refresh.
        _instance._snapshotModifiers = LevelModifiers.Deserialize(mods);
        _instance._snapshotEconomy = economy;
        _instance._snapshotShop = shop;
    }

    private void RefreshState()
    {
        _instance = this;
        if (_generation != LevelGenerator.Instance || _owner != OwnerKey || _room != PhotonNetwork.CurrentRoom)
        {
            _generation = LevelGenerator.Instance;
            _owner = OwnerKey;
            _room = PhotonNetwork.CurrentRoom;
            _request = null;
            _snapshotModifiers = null;
            _mapSection = false;
            _detailsPage = 1;
        }
        if (!PhotonNetwork.InRoom || _readersRoom != PhotonNetwork.CurrentRoom)
        {
            _readers.Clear();
            _readersRoom = PhotonNetwork.CurrentRoom;
        }
        if (SemiFunc.IsMasterClientOrSingleplayer())
        {
            if (_readers.Count == 0) return;
            string key = LevelKey;
            int economy = SafeRunIsLevel() ? Economy.EconomyEvents.Current?.Id ?? 0 : 0;
            int shop = SemiFunc.RunIsShop() ? Economy.ShopEconomy.Current?.Id ?? 0 : 0;
            string signature = key + "|" + ModifierManager.Revision + "|" + economy + "|" + shop;
            foreach (int actor in new List<int>(_readers.Keys))
            {
                if (PhotonNetwork.CurrentRoom.GetPlayer(actor) == null)
                {
                    _readers.Remove(actor);
                    continue;
                }
                var reader = _readers[actor];
                if (reader.Level != key || reader.Sent == signature) continue;
                PhotonNetwork.RaiseEvent(StateEventCode,
                    new object[] { reader.Request, key, ModifierManager.Current.Serialize(), economy, shop },
                    new RaiseEventOptions { TargetActors = new[] { actor } }, SendOptions.SendReliable);
                _readers[actor] = (reader.Request, key, signature);
            }
        }
        else if (_request == null && PhotonNetwork.MasterClient != null)
        {
            _readers.Clear();
            _request = System.Guid.NewGuid().ToString("N");
            PhotonNetwork.RaiseEvent(StateEventCode, new object[] { _request, LevelKey },
                new RaiseEventOptions { Receivers = ReceiverGroup.MasterClient }, SendOptions.SendReliable);
        }
    }

    private readonly List<string> _parts = new();

    private void LateUpdate()
    {
        _instance = this;
        if (!GameplayReady)
        {
            _request = null;
            _snapshotModifiers = null;
            _mapSection = false;
            if (!PhotonNetwork.InRoom) _readers.Clear();
            HideAll();
            return;
        }
        RefreshState();
        if (!InfoReady) { HideAll(); return; }
        if (SemiFunc.NoTextInputsActive() && Input.GetKeyDown(ModConfig.LevelInfoToggleKey.Value))
            _suppressed = !_suppressed;
        bool showModifiers = ModConfig.LevelInfoEnabled.Value && !_suppressed;
        string mapInfo = Upgrades.MapUpgrade.HudInfo;
        if (!EnsureLabels())
            return; // HUD or font template not ready yet

        bool host = SemiFunc.IsMasterClientOrSingleplayer();
        var mods = host ? ModifierManager.Current : _snapshotModifiers;
        // Top-centre countdown for the LevelTimer modifier.
        if (showModifiers && mods.Has(ModifierType.LevelTimer) && LevelTimerController.Running)
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

        bool special = SafeRunIsLevel() && mods != null && mods.IsSpecial;
        var econ = SafeRunIsLevel() ? (host ? Economy.EconomyEvents.Current :
            _snapshotEconomy > 0 ? Economy.EconomyEvents.All[_snapshotEconomy - 1] : null) : null;
        bool inShop = false;
        try { inShop = SemiFunc.RunIsShop(); } catch { }
        var shopEcon = inShop ? (host ? Economy.ShopEconomy.Current :
            _snapshotShop > 0 ? Economy.ShopEconomy.All[_snapshotShop - 1] : null) : null;
        bool market = inShop && ModConfig.BlackMarketEnabled.Value;
        if (showModifiers && (special || econ != null || shopEcon != null || market))
        {
            _parts.Clear();
            _effectDescriptions.Clear();
            if (special)
                BuildParts(mods);
            if (econ != null)
                AddModifier(econ.Name, econ.Description, econ.Tier);
            if (shopEcon != null)
                AddModifier(shopEcon.Name, shopEcon.Description, shopEcon.Tier);
            if (market)
            {
                _parts.Add($"[{ModConfig.BlackMarketKey.Value}] Black Market");
                _effectDescriptions.Add("");
            }
            _modsLabel.text = string.Join("\n", _parts);
            _descriptionLabel.text = "";
            PositionMods();

        }
        else
        {
            _parts.Clear();
            _effectDescriptions.Clear();
            _modsLabel.text = "";
            _descriptionLabel.text = "";
            PositionMods();
        }
        if (_modsLabel.enabled)
        {
            _modsLabel.overflowMode = TextOverflowModes.Page;
            _modsLabel.ForceMeshUpdate();
            int pages = Mathf.Max(1, _modsLabel.textInfo.pageCount);
            if (TabOpen && SemiFunc.NoTextInputsActive() && Input.GetKeyDown(KeyCode.End))
                _modsLabel.pageToDisplay = _modsLabel.pageToDisplay % pages + 1;
        }
        RenderDetails(mapInfo);
    }

    // Create (or recreate) the native labels once the HUD canvas and a font template exist.
    private bool EnsureLabels()
    {
        if (HUDCanvas.instance == null)
            return false;

        if (_timerLabel != null && _modsLabel != null && _descriptionLabel != null && _detailsPageLabel != null &&
            _timerLabel.transform.parent == HUDCanvas.instance.transform)
            return true;

        if (_timerLabel != null && _timerLabel.transform.parent != HUDCanvas.instance.transform)
        {
            foreach (var label in new[] { _timerLabel, _modsLabel, _descriptionLabel, _detailsPageLabel })
                if (label != null) Destroy(label.gameObject);
            _timerLabel = _modsLabel = _descriptionLabel = _detailsPageLabel = null;
        }
        var template = FindTemplate();
        if (template == null)
            return false;

        // Top-centre timer.
        if (_timerLabel == null)
            _timerLabel = CreateLabel("IHM_LevelTimer", template, TimerColor,
                TextAlignmentOptions.Top, new Vector2(0.5f, 1f), new Vector2(0f, -10f), 820f);

        // Top-right modifier list (positioned per-frame under the quota HUD).
        if (_modsLabel == null)
        {
            var heading = StatsUI.instance != null ? StatsUI.instance.upgradesHeader : null;
            _modsLabel = CreateLabel("IHM_LevelMods", heading != null ? heading : template, ModColor,
                TextAlignmentOptions.TopRight, new Vector2(1f, 1f), Vector2.zero, 300f);
            ConfigureModifierLabel(_modsLabel);
        }
        if (_descriptionLabel == null)
        {
            _descriptionLabel = CreateLabel("IHM_LevelModDescriptions", template, ModColor,
                TextAlignmentOptions.TopRight, new Vector2(1f, 1f), Vector2.zero, 300f);
            _descriptionLabel.fontSize *= 0.5f;
            ConfigureModifierLabel(_descriptionLabel);
            _detailsPage = 1;
        }

        if (_detailsPageLabel == null)
        {
            _detailsPageLabel = CreateLabel("IHM_DetailsPage", template, ModColor,
                TextAlignmentOptions.TopRight, new Vector2(1f, 1f), Vector2.zero, 300f);
            _detailsPageLabel.fontSize *= 0.5f;
            _detailsPageLabel.enableWordWrapping = true;
        }
        return true;
    }

    private void RenderDetails(string mapInfo)
    {
        _descriptionLabel.enabled = _detailsPageLabel.enabled = false;
        bool tab = TabOpen;
        if (!tab || !_tabWasOpen) _detailsPage = 1;
        _tabWasOpen = tab;
        if (!tab || !_layoutReady) return;
        bool hasMap = !string.IsNullOrEmpty(mapInfo);
        if (!hasMap) _mapSection = false;
        else if (_parts.Count == 0) _mapSection = true;
        bool previousSection = Input.GetKeyDown(KeyCode.Comma);
        bool nextSection = Input.GetKeyDown(KeyCode.Period);
        if (hasMap && _parts.Count > 0 && SemiFunc.NoTextInputsActive() &&
            previousSection != nextSection)
        {
            _mapSection = !_mapSection;
            _detailsPage = 1;
        }
        // The two sections share the measured RIGHT region. Neither can grow into upgrades,
        // quests, the central map, LEVEL, or the bottom mission/inventory band.
        if (_mapSection) _descriptionLabel.text = "<b>MAP SUITE</b>\n<color=#FFFFFF>" + mapInfo + "</color>";
        else
        {
            _descriptions.Clear();
            for (int i = 0; i < _parts.Count; i++)
                _descriptions.Add(_parts[i] + "\n" + _effectDescriptions[i]);
            _descriptionLabel.text = string.Join("\n\n", _descriptions);
        }
        _descriptionLabel.enableAutoSizing = false;
        _descriptionLabel.overflowMode = TextOverflowModes.Page;
        var rt = _descriptionLabel.rectTransform;
        float footer = _detailsPageLabel.fontSize * 4f;
        if (rt.rect.height < footer + _descriptionLabel.fontSize) return;
        rt.sizeDelta = new Vector2(rt.sizeDelta.x, Mathf.Max(0f, rt.rect.height - footer));
        if (rt.rect.height < _descriptionLabel.fontSize || string.IsNullOrEmpty(_descriptionLabel.text))
        {
            if (!hasMap) return;
        }
        else
        {
            _descriptionLabel.enabled = true;
            _descriptionLabel.ForceMeshUpdate();
        }
        int pages = Mathf.Max(1, _descriptionLabel.textInfo.pageCount);
        if (SemiFunc.NoTextInputsActive())
        {
            if (Input.GetKeyDown(KeyCode.RightArrow)) _detailsPage = _detailsPage % pages + 1;
            if (Input.GetKeyDown(KeyCode.LeftArrow)) _detailsPage = (_detailsPage + pages - 2) % pages + 1;
        }
        _detailsPage = Mathf.Clamp(_detailsPage, 1, pages);
        _descriptionLabel.pageToDisplay = _detailsPage;
        _detailsPageLabel.text = (_modsLabel.textInfo.pageCount > 1 ? "END: NAMES\n" : "") + (hasMap && _parts.Count > 0 ? ", / .: " + (_mapSection ? "MODIFIERS" : "MAP SUITE") : "") +
            (pages > 1 ? $"\nLEFT / RIGHT  {_detailsPage}/{pages}" : "");
        _detailsPageLabel.rectTransform.anchoredPosition = rt.anchoredPosition + new Vector2(0f, -rt.rect.height);
        _detailsPageLabel.rectTransform.sizeDelta = new Vector2(rt.rect.width, footer);
        _detailsPageLabel.enabled = !string.IsNullOrEmpty(_detailsPageLabel.text);
    }

    private static TextMeshProUGUI CreateLabel(string name, TextMeshProUGUI template, Color color,
        TextAlignmentOptions align, Vector2 anchor, Vector2 offset, float width)
    {
        var go = new GameObject(name);
        go.transform.SetParent(HUDCanvas.instance.transform, false);

        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.font = template.font;                       // native Teko asset
        tmp.fontSharedMaterial = template.fontSharedMaterial; // same outline/material as the HUD
        tmp.fontSize = template.fontSize;
        tmp.fontStyle = template.fontStyle;
        tmp.characterSpacing = template.characterSpacing;
        tmp.lineSpacing = template.lineSpacing;
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

    private static void ConfigureModifierLabel(TextMeshProUGUI label)
    {
        label.enableWordWrapping = true;
        label.richText = true;
        label.enableAutoSizing = false;
        label.fontSizeMax = label.fontSize;
        label.fontSizeMin = label.fontSize;
        label.overflowMode = TextOverflowModes.Ellipsis;
        label.fontStyle |= FontStyles.UpperCase;
    }

    // Measure native HUD rects in canvas coordinates, including their animated offsets.
    private Rect Bounds(RectTransform target)
    {
        var text = target.GetComponent<TextMeshProUGUI>();
        if (text != null && !string.IsNullOrEmpty(text.text))
        {
            text.ForceMeshUpdate();
            var glyphs = text.textBounds;
            _corners[0] = target.TransformPoint(new Vector3(glyphs.min.x, glyphs.min.y));
            _corners[1] = target.TransformPoint(new Vector3(glyphs.min.x, glyphs.max.y));
            _corners[2] = target.TransformPoint(new Vector3(glyphs.max.x, glyphs.max.y));
            _corners[3] = target.TransformPoint(new Vector3(glyphs.max.x, glyphs.min.y));
        }
        else
            target.GetWorldCorners(_corners);
        var canvas = HUDCanvas.instance.rect;
        Vector2 min = canvas.InverseTransformPoint(_corners[0]);
        Vector2 max = min;
        for (int i = 1; i < 4; i++)
        {
            Vector2 point = canvas.InverseTransformPoint(_corners[i]);
            min = Vector2.Min(min, point);
            max = Vector2.Max(max, point);
        }
        return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
    }

    private void PositionMods()
    {
        _layoutReady = false;
        var canvas = HUDCanvas.instance.rect;
        var level = LevelUI.instance;
        // Wait for native anchors rather than drawing over the quota during HUD initialization.
        if (canvas == null || level == null || level.uiText == null || level.textNumber == null ||
            GoalUI.instance == null || GoalUI.instance.uiText == null ||
            !GoalUI.instance.uiText.isActiveAndEnabled || GoalUI.instance.isHidden)
        {
            _modsLabel.enabled = _descriptionLabel.enabled = false;
            return;
        }

        _layoutReady = true;
        Rect area = canvas.rect;
        Rect heading = Bounds(level.uiText.rectTransform);
        Rect number = Bounds(level.textNumber.rectTransform);
        Rect quota = Bounds(GoalUI.instance.uiText.rectTransform);
        float gap = _modsLabel.fontSizeMax * 0.4f;
        float right = quota.xMax;
        float width = Mathf.Min(area.width * 0.23f, 360f);
        float top = quota.yMin - gap;
        float bottom = Mathf.Max(area.yMin + area.height * 0.24f,
            Mathf.Max(heading.yMax, number.yMax) + gap);
        float available = Mathf.Max(0f, top - bottom);
        float nameHeight = _modsLabel.GetPreferredValues(_modsLabel.text, width, Mathf.Infinity).y;
        SetBlock(_modsLabel, right, top, width, Mathf.Min(nameHeight, available * 0.45f));
        // TMP's visual glyph edge (including native font bearings) follows the animated quota
        // edge exactly, rather than a percentage-based screen inset.
        if (_modsLabel.enabled)
        {
            _modsLabel.ForceMeshUpdate();
            float correction = right - Bounds(_modsLabel.rectTransform).xMax;
            _modsLabel.rectTransform.anchoredPosition += new Vector2(correction, 0f);
        }
        top -= _modsLabel.enabled ? _modsLabel.rectTransform.rect.height + gap : 0f;
        SetBlock(_descriptionLabel, right, top, width, top - bottom);
        _descriptionLabel.enabled = false; // Only RenderDetails may enable this, inside real Tab.
    }

    private static void SetBlock(TextMeshProUGUI label, float right, float top, float width, float height)
    {
        var area = HUDCanvas.instance.rect.rect;
        label.rectTransform.anchoredPosition = new Vector2(right - area.xMax, top - area.yMax);
        label.rectTransform.sizeDelta = new Vector2(width, Mathf.Max(0f, height));
        label.enabled = height >= label.fontSizeMin && !string.IsNullOrEmpty(label.text);
    }

    private void HideAll()
    {
        _tabWasOpen = false;
        _detailsPage = 1;
        if (_modsLabel != null) _modsLabel.pageToDisplay = 1;
        if (_timerLabel != null)
            _timerLabel.enabled = false;
        if (_modsLabel != null)
            _modsLabel.enabled = false;
        if (_descriptionLabel != null)
            _descriptionLabel.enabled = false;
        if (_detailsPageLabel != null)
            _detailsPageLabel.enabled = false;
    }

    private void OnDisable()
    {
        _request = null;
        _snapshotModifiers = null;
        HideAll();
    }

    private void OnDestroy()
    {
        foreach (var label in new[] { _timerLabel, _modsLabel, _descriptionLabel, _detailsPageLabel })
            if (label != null) Destroy(label.gameObject);
        if (_instance == this) _instance = null;
    }

    // The upgrades body uses the native lighter Teko face and HUD material.
    private static TextMeshProUGUI FindTemplate()
    {
        if (StatsUI.instance != null && StatsUI.instance.Text != null)
            return StatsUI.instance.Text;
        return null; // Wait for the native body preset; arbitrary HUD labels may use another face.
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

    private void AddModifier(string name, string description, Economy.EconomyTier tier)
    {
        string color = tier switch
        {
            Economy.EconomyTier.Good or Economy.EconomyTier.VeryGood => "70FF45",
            Economy.EconomyTier.Bad or Economy.EconomyTier.VeryBad => "FF4545",
            _ => "FF9E14",
        };
        _parts.Add($"<color=#{color}>{name}</color>");
        _effectDescriptions.Add(description);
    }

    private void BuildParts(LevelModifiers mods)
    {
        foreach (var (flag, name) in Names)
        {
            if (!mods.Has(flag))
                continue;
            string description = flag switch
            {
                ModifierType.ReducedPlayerHp => $"Start with {mods.PlayerHpPercent}% of current HP. Max HP is unchanged.",
                ModifierType.ExtraSouls => $"Monsters drop {mods.ExtraSouls} souls.",
                ModifierType.MoneyLossOnDeath => $"Dying loses {mods.MoneyLossPercent}% of collected money.",
                _ => ModConfig.ModifierDescription(flag),
            };
            AddModifier(name, description, Economy.BlackMarket.TierOfModifier(flag));
        }
        foreach (int id in mods.Stats)
        {
            var sm = StatModifiers.Get(id);
            if (sm != null)
                AddModifier(sm.Name, sm.Description, sm.Tier);
        }
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
