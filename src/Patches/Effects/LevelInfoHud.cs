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
    private TextMeshProUGUI _mapLabel;
    private TextMeshProUGUI _descriptionLabel;
    private readonly List<string> _descriptions = new();
    private readonly List<string> _effectDescriptions = new();
    private readonly List<string> _effectColors = new();
    private readonly Vector3[] _corners = new Vector3[4];
    private bool _suppressed;
    private TextMeshProUGUI _detailsPageLabel;
    private bool _tabWasOpen;
    private int _mapPage = 1;
    private int _detailsPage = 1;
    private bool _layoutReady;
    private LevelGenerator _generation;
    private string _owner;
    private Room _room;
    private string _request;
    private LevelModifiers _snapshotModifiers;
    private int _snapshotEconomy;
    private int _snapshotShop;
    // Guest side: when this generation was first seen and when to repeat an unanswered request.
    private float _generationAt, _nextRequestAt;
    private int _requestsSent;
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
    // In gameplay MenuManager has no current page; any open page (Esc, settings, cosmetics...) covers the
    // HUD, so the mod's HUD text hides with it instead of drawing over the menu.
    internal static bool MenuOpen => MenuManager.instance != null && MenuManager.instance.currentMenuPage != null;
    internal static bool TabOpen => (MapToolController.instance != null && MapToolController.instance.Active) ||
        (Map.Instance != null && Map.Instance.Active);
    // Guests wait briefly for the host's HUD snapshot, then fall back to the modifiers, economy and shop
    // economy the mod already synced to them. A missing reply must never hide the whole right-side HUD.
    internal static bool InfoReady => GameplayReady &&
        (SemiFunc.IsMasterClientOrSingleplayer() || (_instance != null && (_instance.SnapshotReady || _instance.SnapshotGraceOver)));
    private bool SnapshotGraceOver => _generation == LevelGenerator.Instance && Time.unscaledTime - _generationAt > 2.5f;
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
            RunManager.instance==null || RunManager.instance.restarting ||
            nonce != _instance._request || key != LevelKey ||
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
            _snapshotEconomy = _snapshotShop = 0;
            _generationAt = Time.unscaledTime;
            _nextRequestAt = 0f;
            _requestsSent = 0;
            _mapPage = 1;
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
        else if (!SnapshotReady && PhotonNetwork.MasterClient != null && Time.unscaledTime >= _nextRequestAt && _requestsSent < 10)
        {
            // Repeat an unanswered request (the host may still be loading, or the event was dropped); same
            // nonce, so a late reply to an earlier attempt is still accepted.
            _readers.Clear();
            _request ??= System.Guid.NewGuid().ToString("N");
            PhotonNetwork.RaiseEvent(StateEventCode, new object[] { _request, LevelKey },
                new RaiseEventOptions { Receivers = ReceiverGroup.MasterClient }, SendOptions.SendReliable);
            _nextRequestAt = Time.unscaledTime + 3f;
            if (++_requestsSent == 10)
                Plugin.Log.LogWarning($"[LevelInfo] Host sent no HUD state after 10 requests ({LevelKey}); showing the modifiers synced at level start.");
        }
    }

    private readonly List<string> _parts = new();
    private string _reportedFor;
    private Rect _layoutArea, _layoutQuota, _layoutHeading, _layoutNumber;
    private bool _layoutQuotaVisible, _layoutHaulVisible, _layoutLevelBelow;
    private float _layoutTop, _layoutBottom, _layoutAvailable;

    // One log line per level when there is text to show but every block ended up hidden.
    private void ReportHiddenOnce(string mapInfo)
    {
        if (_modsLabel == null || _mapLabel == null) return;
        bool wantMods = !TabOpen && !string.IsNullOrEmpty(_modsLabel.text);
        bool wantDetails = TabOpen && _descriptions.Count > 0;
        if (wantDetails && !_descriptionLabel.enabled) wantMods = true; // report it through the same line
        bool wantMap = TabOpen && SafeRunIsLevel() && !string.IsNullOrEmpty(mapInfo);
        if (!(wantMods && !_modsLabel.enabled) && !(wantMap && !_mapLabel.enabled)) return;
        string key = LevelKey;
        if (_reportedFor == key) return;
        _reportedFor = key;
        Plugin.Log.LogWarning($"[LevelInfo] Text hidden by layout (mods={wantMods}/{_modsLabel.enabled}, details={wantDetails}/{_descriptionLabel.enabled}, map={wantMap}/{_mapLabel.enabled}, " +
                              $"font={_modsLabel.fontSize:0}/{_modsLabel.fontSizeMin:0}): canvas={_layoutArea}, quota={_layoutQuota}, " +
                              $"quotaVisible={_layoutQuotaVisible}, haulVisible={_layoutHaulVisible}, level={_layoutHeading}/{_layoutNumber}, " +
                              $"levelBelow={_layoutLevelBelow}, top={_layoutTop:0}, bottom={_layoutBottom:0}, available={_layoutAvailable:0}.");
    }

    private void LateUpdate()
    {
        _instance = this;
        if (!GameplayReady)
        {
            // Hiding/loading the native HUD is not a new scene or a new snapshot request.
            // RefreshState invalidates by generation instance, room and host on return.
            _mapPage = 1;
            if (!PhotonNetwork.InRoom) _readers.Clear();
            HideAll();
            return;
        }
        RefreshState();
        if (!InfoReady || MenuOpen) { HideAll(); return; }
        if (SemiFunc.NoTextInputsActive() && Input.GetKeyDown(ModConfig.LevelInfoToggleKey.Value))
            _suppressed = !_suppressed;
        bool showModifiers = ModConfig.LevelInfoEnabled.Value && !_suppressed;
        string mapInfo = Upgrades.MapUpgrade.HudInfo;
        if (!EnsureLabels())
            return; // HUD or font template not ready yet

        bool host = SemiFunc.IsMasterClientOrSingleplayer();
        bool snapshot = !host && SnapshotReady;
        var mods = host || !snapshot ? ModifierManager.Current : _snapshotModifiers;
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
        var econ = SafeRunIsLevel() ? (host || !snapshot ? Economy.EconomyEvents.Current :
            _snapshotEconomy > 0 ? Economy.EconomyEvents.All[_snapshotEconomy - 1] : null) : null;
        bool inShop = false;
        try { inShop = SemiFunc.RunIsShop(); } catch { }
        var shopEcon = inShop ? (host || !snapshot ? Economy.ShopEconomy.Current :
            _snapshotShop > 0 ? Economy.ShopEconomy.All[_snapshotShop - 1] : null) : null;
        bool market = inShop && ModConfig.BlackMarketEnabled.Value;
        if (showModifiers && (special || econ != null || shopEcon != null || market))
        {
            _parts.Clear();
            _effectDescriptions.Clear();
            _effectColors.Clear();
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
                _effectColors.Add("FFFFFF");
            }
            _modsLabel.text = string.Join("\n", _parts);
            _descriptionLabel.text = "";

        }
        else
        {
            _parts.Clear();
            _effectDescriptions.Clear();
            _effectColors.Clear();
            _modsLabel.text = "";
            _descriptionLabel.text = "";
        }
        PositionMods(mapInfo);
        if (_modsLabel.enabled)
        {
            _modsLabel.overflowMode = TextOverflowModes.Page;
            _modsLabel.ForceMeshUpdate();
            int pages = Mathf.Max(1, _modsLabel.textInfo.pageCount);
            if (TabOpen && SemiFunc.NoTextInputsActive() && Input.GetKeyDown(KeyCode.End))
                _modsLabel.pageToDisplay = _modsLabel.pageToDisplay % pages + 1;
        }
        RenderDetails();
        ReportHiddenOnce(mapInfo);
    }

    // Create (or recreate) the native labels once the HUD canvas and a font template exist.
    private bool EnsureLabels()
    {
        if (HUDCanvas.instance == null)
            return false;

        if (_timerLabel != null && _modsLabel != null && _mapLabel != null && _descriptionLabel != null && _detailsPageLabel != null &&
            _timerLabel.transform.parent == HUDCanvas.instance.transform)
            return true;

        if (_timerLabel != null && _timerLabel.transform.parent != HUDCanvas.instance.transform)
        {
            foreach (var label in new[] { _timerLabel, _modsLabel, _mapLabel, _descriptionLabel, _detailsPageLabel })
                if (label != null) Destroy(label.gameObject);
            _timerLabel = _modsLabel = _mapLabel = _descriptionLabel = _detailsPageLabel = null;
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
            var source = heading != null ? heading : template;
            _modsLabel = CreateLabel("IHM_LevelMods", source, ModColor,
                TextAlignmentOptions.TopRight, new Vector2(1f, 1f), Vector2.zero, 300f);
            // Our label hangs directly off the HUD canvas, while the native heading sits inside a scaled
            // group: copying its raw size drew names larger than the "UPGRADES" heading itself. Match the
            // heading's on-screen size, then step down to list-entry size (native body rows are ~55% of it).
            _modsLabel.fontSize = source.fontSize * OnScreenScale(source) * ModNameScale;
            // Hard bounds from the HUD's own height (native list rows are about 2.6% of it), in case the
            // heading reports an auto-sized or animated size.
            float hudHeight = HUDCanvas.instance.rect.rect.height;
            if (hudHeight > 1f) _modsLabel.fontSize = Mathf.Clamp(_modsLabel.fontSize, hudHeight * .02f, hudHeight * .034f);
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

        if (_mapLabel == null)
        {
            _mapLabel = CreateLabel("IHM_MapSuite", template, Color.white,
                TextAlignmentOptions.TopRight, new Vector2(1f, 1f), Vector2.zero, 300f);
            _mapLabel.fontSize *= 0.5f;
            ConfigureModifierLabel(_mapLabel);
            _mapLabel.fontStyle = template.fontStyle;
            _mapLabel.overflowMode = TextOverflowModes.Page;
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

    private void RenderDetails()
    {
        _descriptionLabel.enabled = _detailsPageLabel.enabled = false;
        bool tab = TabOpen;
        if (!tab || !_tabWasOpen) { _detailsPage = 1; _mapPage = 1; }
        _tabWasOpen = tab;
        if (!tab || !_layoutReady) return;

        int mapPages = _mapLabel.enabled ? Mathf.Max(1, _mapLabel.textInfo.pageCount) : 1;
        if (SemiFunc.NoTextInputsActive())
        {
            if (Input.GetKeyDown(KeyCode.Period)) _mapPage = _mapPage % mapPages + 1;
            if (Input.GetKeyDown(KeyCode.Comma)) _mapPage = (_mapPage + mapPages - 2) % mapPages + 1;
        }
        _mapPage = Mathf.Clamp(_mapPage, 1, mapPages);
        _mapLabel.pageToDisplay = _mapPage;

        // Descriptions only, each in its modifier's colour (good / mixed / bad); the Black Market hint has none.
        _descriptions.Clear();
        for (int i = 0; i < _effectDescriptions.Count; i++)
            if (!string.IsNullOrEmpty(_effectDescriptions[i]))
                _descriptions.Add($"<color=#{_effectColors[i]}>{_effectDescriptions[i]}</color>");
        _descriptionLabel.text = string.Join("\n\n", _descriptions);
        _descriptionLabel.enableAutoSizing = false;
        _descriptionLabel.overflowMode = TextOverflowModes.Page;
        var rt = _descriptionLabel.rectTransform;
        // Reserve a small footer only inside the remaining details region. Neither the
        // map block nor paged descriptions can overlap the native level/inventory band.
        float footer = _detailsPageLabel.fontSize * 3f;
        if (rt.rect.height < footer) return;
        rt.sizeDelta = new Vector2(rt.sizeDelta.x, rt.rect.height - footer);
        int pages = 1;
        if (rt.rect.height >= _descriptionLabel.fontSize && !string.IsNullOrEmpty(_descriptionLabel.text))
        {
            _descriptionLabel.enabled = true;
            _descriptionLabel.ForceMeshUpdate();
            pages = Mathf.Max(1, _descriptionLabel.textInfo.pageCount);
        }
        if (SemiFunc.NoTextInputsActive())
        {
            if (Input.GetKeyDown(KeyCode.RightArrow)) _detailsPage = _detailsPage % pages + 1;
            if (Input.GetKeyDown(KeyCode.LeftArrow)) _detailsPage = (_detailsPage + pages - 2) % pages + 1;
        }
        _detailsPage = Mathf.Clamp(_detailsPage, 1, pages);
        _descriptionLabel.pageToDisplay = _detailsPage;
        _detailsPageLabel.text = (_modsLabel.enabled && _modsLabel.textInfo.pageCount > 1 ? "END: NAMES\n" : "") +
            (mapPages > 1 ? $", / .: MAP SUITE  {_mapPage}/{mapPages}\n" : "") +
            (pages > 1 ? $"LEFT / RIGHT  {_detailsPage}/{pages}" : "");
        _detailsPageLabel.rectTransform.anchoredPosition = rt.anchoredPosition + new Vector2(0f, -rt.rect.height);
        _detailsPageLabel.rectTransform.sizeDelta = new Vector2(rt.rect.width, footer);
        _detailsPageLabel.enabled = !string.IsNullOrEmpty(_detailsPageLabel.text);
    }

    private static TextMeshProUGUI CreateLabel(string name, TextMeshProUGUI template, Color color,
        TextAlignmentOptions align, Vector2 anchor, Vector2 offset, float width)
    {
        var go = new GameObject(name);
        // Parenting does not inherit a Unity layer. REPO's world-space HUD camera
        // renders only UI (layer 5), so Default-layer labels were entirely invisible.
        go.layer = HUDCanvas.instance.gameObject.layer;
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

    private const float ModNameScale = .55f;

    // How much larger or smaller a native label renders than one parented straight to the HUD canvas.
    private static float OnScreenScale(TextMeshProUGUI native)
    {
        var canvas = HUDCanvas.instance != null ? HUDCanvas.instance.transform : null;
        if (native == null || canvas == null) return 1f;
        float own = native.transform.lossyScale.y, root = canvas.lossyScale.y;
        if (own <= 0f || root <= 0f) return 1f;
        return Mathf.Clamp(own / root, .3f, 1.5f);
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

    private void PositionMods(string mapInfo)
    {
        _layoutReady = false;
        var canvas = HUDCanvas.instance.rect;
        var level = LevelUI.instance;
        // Wait for native anchors rather than drawing over the quota during HUD initialization.
        if (canvas == null)
        {
            _modsLabel.enabled = _mapLabel.enabled = _descriptionLabel.enabled = false;
            return;
        }

        _layoutReady = true;
        Rect area = canvas.rect;
        bool quotaVisible=GoalUI.instance!=null&&GoalUI.instance.uiText!=null&&
            GoalUI.instance.uiText.isActiveAndEnabled&&!GoalUI.instance.isHidden;
        Rect heading = level!=null&&level.uiText!=null ? Bounds(level.uiText.rectTransform) : new Rect();
        Rect number = level!=null&&level.textNumber!=null ? Bounds(level.textNumber.rectTransform) : new Rect();
        Rect quota = quotaVisible ? Bounds(GoalUI.instance.uiText.rectTransform) :
            new Rect(area.xMax-_modsLabel.fontSizeMax,area.yMax-_modsLabel.fontSizeMax*3f,0,0);
        bool haulVisible = HaulUI.instance != null && HaulUI.instance.uiText != null &&
            HaulUI.instance.uiText.isActiveAndEnabled && !HaulUI.instance.isHidden;
        if (haulVisible)
        {
            var haul = Bounds(HaulUI.instance.uiText.rectTransform);
            quota = quotaVisible ? Rect.MinMaxRect(quota.xMin, Mathf.Min(quota.yMin, haul.yMin),
                quota.xMax, Mathf.Max(quota.yMax, haul.yMax)) : haul;
        }
        float gap = _modsLabel.fontSizeMax * 0.4f;
        float right = quota.xMax;
        float width = Mathf.Min(area.width * 0.23f, 360f);
        float top = quota.yMin - gap;
        // Toolkit panels stacked at the top right (the shop's cart list) live on their own overlay canvases:
        // start below them, or the Black Market hint and shop economy would be drawn over the list.
        float stackBottom = Hud.HudStack.LowestScreenEdge(Hud.HudCorner.TopRight);
        if (stackBottom >= 0f)
        {
            var display = HudProjection.DisplayRect(HudProjection.CanvasCamera(_modsLabel.canvas));
            if (display.height > 1f)
            {
                float listBottom = area.yMin + (stackBottom - display.y) / display.height * area.height;
                top = Mathf.Min(top, listBottom - gap);
            }
        }
        float floor = area.yMin + area.height * 0.24f;
        // The native level label (number, moon) only limits the block when it sits BELOW the quota area.
        // After the HUD changes it can sit beside or above it, which left no room and hid every label.
        float levelTop = Mathf.Max(heading.yMax, number.yMax);
        bool levelBelow = levelTop > 0f && levelTop + gap < top;
        float bottom = levelBelow ? Mathf.Max(floor, levelTop + gap) : floor;
        // Never squeeze the block below a few lines: fall back to the plain screen-ratio floor.
        if (top - bottom < _modsLabel.fontSizeMax * 3f) bottom = Mathf.Min(bottom, floor);
        float available = Mathf.Max(0f, top - bottom);
        // Raw numbers only; formatted into text only when a hidden-label report is actually logged.
        _layoutArea = area; _layoutQuota = quota; _layoutHeading = heading; _layoutNumber = number;
        _layoutQuotaVisible = quotaVisible; _layoutHaulVisible = haulVisible; _layoutLevelBelow = levelBelow;
        _layoutTop = top; _layoutBottom = bottom; _layoutAvailable = available;
        // Keep the requested order: native haul, native extraction count, Map Suite,
        // modifier names. Map data and descriptions are separate, simultaneous blocks.
        _mapLabel.text = TabOpen && SafeRunIsLevel() ? mapInfo : "";
        float mapHeight = string.IsNullOrEmpty(_mapLabel.text) ? 0f :
            _mapLabel.GetPreferredValues(_mapLabel.text, width, Mathf.Infinity).y;
        float mapBudget = string.IsNullOrEmpty(_modsLabel.text) ? available * 0.8f : available * 0.45f;
        SetBlock(_mapLabel, right, top, width, Mathf.Min(mapHeight, mapBudget));
        if (_mapLabel.enabled)
        {
            _mapLabel.ForceMeshUpdate();
            top -= _mapLabel.rectTransform.rect.height + gap;
        }
        available = Mathf.Max(0f, top - bottom);
        float nameHeight = _modsLabel.GetPreferredValues(_modsLabel.text, width, Mathf.Infinity).y;
        SetBlock(_modsLabel, right, top, width, Mathf.Min(nameHeight, available * 0.45f));
        // Tab shows the descriptions only: no names, no Black Market hint; the descriptions take their place.
        if (TabOpen) _modsLabel.enabled = false;
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
        _mapPage = 1;
        if (_mapLabel != null)
        {
            _mapLabel.enabled = false;
            _mapLabel.pageToDisplay = 1;
        }
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
        foreach (var label in new[] { _timerLabel, _modsLabel, _mapLabel, _descriptionLabel, _detailsPageLabel })
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
        (ModifierType.MysteryBoxes, "Mystery Boxes"),
        (ModifierType.SilentNight, "Silent Night"),
        (ModifierType.LoudFacility, "Loud Facility"),
        (ModifierType.GreasedWheels, "Greased Wheels"),
        (ModifierType.ShortMemory, "Short Memory"),
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
        _effectColors.Add(color);
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

    private static bool SafeRunIsShop()
    {
        try { return SemiFunc.RunIsShop(); }
        catch { return false; }
    }

    private static bool SafeRunIsLevel()
    {
        try { return SemiFunc.RunIsLevel(); }
        catch { return false; }
    }
}
