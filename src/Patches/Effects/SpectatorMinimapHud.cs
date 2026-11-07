using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IncreaseHardMod.Patches.Effects;

/// <summary>
/// A large map panel. Dead players get it as the spectator map, following the current view instead of their body.
/// Living players with the Better Map perk open the same panel with M (BetterMapKey) while their map is up.
/// </summary>
[DefaultExecutionOrder(10000)]
internal sealed class SpectatorMapHud : MonoBehaviour
{
    private Map _map;
    private DirtFinderMapPlayer _marker;
    private Transform _previousSource, _previousMapSource, _focus;
    private Vector3 _previousPosition;
    private Quaternion _previousRotation;
    private int _previousLayer;
    private Camera _nativeCamera, _renderCamera;
    private RenderTexture _texture;
    private NativeHudPanel _surface;
    private RawImage _image;
    private bool _toggle, _show, _alive;
    private static SpectatorMapHud _instance;

    /// <summary>True while a dead player has the spectator map open (their Tab), for HUD that shows "in Tab".</summary>
    internal static bool Showing => _instance != null && _instance.isActiveAndEnabled && _instance._show;
    private void Awake() => _instance = this;
    private float _zoom = 1.5f;
    private Vector2 _pan;

    // Living player with Better Map: the map tool is up on a run level.
    private static bool AliveMapUp()
    {
        if (!Skills.SkillEffects.LocalHas(Skills.SkillCatalog.BetterMap)) return false;
        bool level;
        try { level = SemiFunc.RunIsLevel(); } catch { level = false; }
        var me = SemiFunc.PlayerAvatarLocal();
        return level && me != null && !me.isDisabled && MapToolController.instance != null && MapToolController.instance.Active;
    }

    // What the map centres on: the spectator view when dead, your own camera when alive.
    private Transform Source()
    {
        if (!_alive) return DeadMapAccessPatch.ViewSource();
        var cam = PhysGrabber.instance != null && PhysGrabber.instance.playerCamera != null ? PhysGrabber.instance.playerCamera : Camera.main;
        return cam != null ? cam.transform : PlayerController.instance != null ? PlayerController.instance.transform : null;
    }

    private void Update()
    {
        bool dead = ModConfig.SpectatorMapEnabled.Value && DeadMapAccessPatch.LocalDeadOnLevel();
        bool aliveMap = !dead && AliveMapUp();
        if (!dead && !aliveMap)
        {
            _toggle = false;
            _show = false;
            Release();
            return;
        }
        if (_alive != aliveMap) { _toggle = false; _show = false; Release(); }
        _alive = aliveMap;
        if (_alive)
        {
            // M (BetterMapKey) switches between the hand-held map and the big map while the map is up.
            if (SemiFunc.NoTextInputsActive() && Input.GetKeyDown(ModConfig.BetterMapKey.Value)) _toggle = !_toggle;
            _show = _toggle && SemiFunc.NoTextInputsActive();
        }
        else
        {
            bool nativeToggle = InputManager.instance != null && InputManager.instance.InputToggleGet(InputKey.Map);
            if (nativeToggle && SemiFunc.InputDown(InputKey.Map) && SemiFunc.NoTextInputsActive())
                _toggle = !_toggle;
            if (!nativeToggle) _toggle = false;
            bool hold = Input.GetKey(ModConfig.SpectatorMapHoldKey.Value);
            _show = (_toggle || SemiFunc.InputHold(InputKey.Map) || hold) && SemiFunc.NoTextInputsActive();
        }
        if (!_show)
        {
            Release();
            return;
        }
        if (Input.GetKeyDown(KeyCode.Home)) { _pan = Vector2.zero; _zoom = 1.5f; }
        if (Input.GetKeyDown(ModConfig.MapZoomInKey.Value) || Input.GetKeyDown(KeyCode.KeypadMinus))
            _zoom = Mathf.Max(0.4f, _zoom - 0.2f);
        if (Input.GetKeyDown(ModConfig.MapZoomOutKey.Value) || Input.GetKeyDown(KeyCode.KeypadPlus))
            _zoom = Mathf.Min(4f, _zoom + 0.2f);
        float step = 15f * _zoom * Time.unscaledDeltaTime;
        var movement = Vector3.zero;
        var source = Source();
        if (source != null)
        {
            var yaw = Quaternion.Euler(0f, source.eulerAngles.y, 0f);
            if (Input.GetKey(KeyCode.LeftArrow)) movement -= yaw * Vector3.right * step;
            if (Input.GetKey(KeyCode.RightArrow)) movement += yaw * Vector3.right * step;
            if (Input.GetKey(KeyCode.UpArrow)) movement += yaw * Vector3.forward * step;
            if (Input.GetKey(KeyCode.DownArrow)) movement -= yaw * Vector3.forward * step;
            _pan += new Vector2(movement.x, movement.z);
        }
    }

    private void LateUpdate()
    {
        if (!_show) return;
        var map = Map.Instance;
        if (_map != null && _map != map) Release();
        if (map == null || Source() == null) return;
        if (_map == null)
        {
            _map = map;
            _previousMapSource = map.playerTransformSource;
            _previousLayer = map.PlayerLayer;
            _focus = new GameObject("IHM_SpectatorMapFocus").transform;
        }
        // The hand tool can finish its close animation during Update; enable our pipeline
        // afterwards, and only for frames where the overlay is actually requested.
        if (!map.Active) map.ActiveSet(true);
        if (_marker == null)
        {
            _marker = DirtFinderMapPlayer.Instance;
            if (_marker == null) return;
            _previousSource = _marker.PlayerTransform;
            _previousPosition = _marker.transform.position;
            _previousRotation = _marker.transform.rotation;
        }
        var source = Source();
        _focus.position = source.position + new Vector3(_pan.x, 0f, _pan.y);
        _focus.rotation = Quaternion.Euler(0f, source.eulerAngles.y, 0f);
        map.playerTransformSource = _focus;
        _marker.PlayerTransform = _focus;
        // The native coroutine only ticks at 10 Hz; update before rendering so the displayed
        // area follows the spectator camera on the same frame, including teammate switches.
        _marker.transform.position = _focus.position * map.Scale + map.OverLayerParent.position + _marker.StartOffset;
        var local = _marker.transform.localPosition;
        _marker.transform.localPosition = new Vector3(local.x, 0f, local.z);
        _marker.transform.rotation = _focus.rotation;
        map.PlayerLayer = map.GetLayerParent(_focus.position.y + 0.01f).layer;

        if (_nativeCamera == null)
            foreach (var camera in map.ActiveParent.GetComponentsInChildren<Camera>(true))
                if (camera.targetTexture != null) { _nativeCamera = camera; break; }
        if (_nativeCamera == null) return;
        if (_renderCamera == null)
        {
            _texture = new RenderTexture(1024, 1024, 16, RenderTextureFormat.ARGB32)
                { name = "IHM Spectator Map", filterMode = FilterMode.Point };
            _texture.Create();
            _renderCamera = new GameObject("IHM_SpectatorMapCamera", typeof(Camera)).GetComponent<Camera>();
            _renderCamera.CopyFrom(_nativeCamera);
            _renderCamera.enabled = false;
            _renderCamera.targetTexture = _texture;
            _renderCamera.clearFlags = CameraClearFlags.SolidColor;
            _renderCamera.backgroundColor = new Color(0.025f, 0.03f, 0.035f, 1f);
            _renderCamera.aspect = 1f;
            Plugin.Log.LogInfo($"[SpectatorMap] Opened 1024x1024 map, following {source.name}; native camera: {_nativeCamera.name}.");
        }
        _renderCamera.transform.SetPositionAndRotation(_nativeCamera.transform.position, _nativeCamera.transform.rotation);
        if (_renderCamera.orthographic)
            _renderCamera.orthographicSize = _nativeCamera.orthographicSize * _zoom;
        else
            _renderCamera.fieldOfView = Mathf.Clamp(2f * Mathf.Atan(
                Mathf.Tan(_nativeCamera.fieldOfView * Mathf.Deg2Rad * 0.5f) * _zoom) * Mathf.Rad2Deg, 10f, 140f);
        _renderCamera.Render();
        EnsurePanel();
        if (_surface != null)
        {
            float size = 1080f * ModConfig.SpectatorMapScreenFraction.Value;
            _surface.Panel.sizeDelta = new Vector2(size + 32f, size + 100f);
            _image.rectTransform.sizeDelta = new Vector2(size, size);
            _surface.Fit();
        }
    }

    private void EnsurePanel()
    {
        if (_surface != null) return;
        _surface = NativeHudPanel.Create("IHM_SpectatorMap", new Vector2(896f, 964f),
            new Vector2(0.5f, 0.5f), Vector2.zero, 0.9f);
        if (_surface == null) return;
        var title = _surface.Label("Title", 24f, TextAlignmentOptions.Top);
        Stretch(title.rectTransform, 12f, 36f, true);
        bool ru = Localization.GameLanguage.Russian;
        title.text = _alive ? (ru ? "КАРТА" : "MAP") : (ru ? "КАРТА НАБЛЮДАТЕЛЯ" : "SPECTATOR MAP");
        title.color = new Color(1f, 0.85f, 0.4f);
        var hint = _surface.Label("Controls", 20f, TextAlignmentOptions.Bottom);
        Stretch(hint.rectTransform, 8f, 40f, false);
        string controls = ru ? "Стрелки: сдвиг   +/-: масштаб   Home: к себе" : "Arrows: pan   +/-: zoom   Home: follow";
        hint.text = _alive ? $"{controls}   {ModConfig.BetterMapKey.Value}: {(ru ? "закрыть" : "close")}" : controls;
        var go = new GameObject("Map", typeof(RectTransform), typeof(RawImage));
        go.transform.SetParent(_surface.Panel, false);
        _image = go.GetComponent<RawImage>();
        _image.texture = _texture;
        _image.raycastTarget = false;
        _image.rectTransform.anchorMin = _image.rectTransform.anchorMax = new Vector2(0.5f, 1f);
        _image.rectTransform.pivot = new Vector2(0.5f, 1f);
        _image.rectTransform.anchoredPosition = new Vector2(0f, -48f);
    }

    private static void Stretch(RectTransform rt, float inset, float height, bool top)
    {
        rt.anchorMin = new Vector2(0f, top ? 1f : 0f);
        rt.anchorMax = new Vector2(1f, top ? 1f : 0f);
        rt.pivot = new Vector2(0.5f, top ? 1f : 0f);
        rt.sizeDelta = new Vector2(-24f, height);
        rt.anchoredPosition = new Vector2(0f, top ? -inset : inset);
    }

    private void Release()
    {
        if (_marker != null)
        {
            _marker.PlayerTransform = _previousSource != null ? _previousSource :
                PlayerController.instance != null ? PlayerController.instance.transform : null;
            _marker.transform.SetPositionAndRotation(_previousPosition, _previousRotation);
        }
        if (_map != null)
        {
            _map.playerTransformSource = _previousMapSource;
            _map.PlayerLayer = _previousLayer;
            if (MapToolController.instance == null || !MapToolController.instance.Active)
                _map.ActiveSet(false);
        }
        if (_surface != null) Destroy(_surface.gameObject);
        if (_renderCamera != null) Destroy(_renderCamera.gameObject);
        if (_texture != null) { _texture.Release(); Destroy(_texture); }
        if (_focus != null) Destroy(_focus.gameObject);
        _surface = null;
        _image = null;
        _renderCamera = null;
        _texture = null;
        _focus = null;
        _marker = null;
        _nativeCamera = null;
        _map = null;
    }

    private void OnDisable() { _show = false; _toggle = false; Release(); }
    private void OnDestroy() => Release();
}
