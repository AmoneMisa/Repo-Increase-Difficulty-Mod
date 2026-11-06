using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IncreaseHardMod.Patches.Effects;

/// <summary>Pixel-sized mod panels with the game's font, independent of the world HUD's scale.</summary>
internal sealed class NativeHudPanel : MonoBehaviour
{
    internal RectTransform Panel;
    internal Rect Viewport => _canvasRect.rect;

    /// <summary>A screen y (pixels from the bottom) in this canvas's local coordinates (same space as Viewport).</summary>
    internal float ScreenToLocalY(float screenY)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvasRect, new Vector2(0f, screenY), null, out var point);
        return point.y;
    }
    internal bool HasCurrentOwner => _owner != null && _owner == HUDCanvas.instance;
    private RectTransform _canvasRect;
    private TMP_FontAsset _font;
    private Material _material;
    private FontStyles _fontStyle;
    private FontWeight _fontWeight;
    private float _characterSpacing, _wordSpacing, _lineSpacing;
    private HUDCanvas _owner;

    internal static NativeHudPanel Create(string name, Vector2 size, Vector2 anchor, Vector2 offset, float opacity)
    {
        if (HUDCanvas.instance == null)
            return null;
        var template = StatsUI.instance != null && StatsUI.instance.Text != null ? StatsUI.instance.Text :
            HUDCanvas.instance.GetComponentInChildren<TextMeshProUGUI>(true);
        if (template == null || template.font == null || template.fontSharedMaterial == null)
            return null;

        // HUDCanvas uses the game's low-resolution render coordinates. Putting a 1000-unit
        // panel there makes it several screens wide. A separate scaled canvas has predictable
        // reference pixels and also survives changes to the game's HUD animation/scale.
        var root = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        // A nested canvas ignores CanvasScaler. Keep this a true root canvas.
        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 40;
        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        var result = root.AddComponent<NativeHudPanel>();
        result._owner = HUDCanvas.instance;
        result._canvasRect = (RectTransform)root.transform;
        result._font = template.font;
        result._fontStyle = template.fontStyle;
        result._fontWeight = template.fontWeight;
        result._characterSpacing = template.characterSpacing;
        result._wordSpacing = template.wordSpacing;
        result._lineSpacing = template.lineSpacing;
        // The source HUD animates its material's face/glow colours. Own our copy so labels
        // remain readable and changing our colour cannot recolour the game's text.
        // HUD text can use a masked/animated material (StatsUI is hidden most of the time).
        // Its stencil and face dilation must never leak into our independent root canvas.
        result._material = new Material(template.fontSharedMaterial);
        if (result._material.HasProperty(ShaderUtilities.ID_FaceColor))
            result._material.SetColor(ShaderUtilities.ID_FaceColor, Color.white);
        // Preserve the native glow preset; only reset hidden face colour and stencil state.
        if (result._material.HasProperty("_StencilComp")) result._material.SetFloat("_StencilComp", 8f);
        if (result._material.HasProperty("_Stencil")) result._material.SetFloat("_Stencil", 0f);
        result._material.DisableKeyword("UNITY_UI_CLIP_RECT");
        result._material.DisableKeyword("UNITY_UI_ALPHACLIP");

        var panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(root.transform, false);
        result.Panel = (RectTransform)panel.transform;
        result.Panel.anchorMin = result.Panel.anchorMax = result.Panel.pivot = anchor;
        result.Panel.anchoredPosition = offset;
        result.Panel.sizeDelta = size;
        var bg = panel.GetComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, opacity);
        bg.raycastTarget = false;
        return result;
    }

    internal static NativeHudPanel Fullscreen(string name)
    {
        var result = Create(name, new Vector2(1920f, 1080f), Vector2.one * 0.5f, Vector2.zero, 0.96f);
        if (result == null) return null;
        result.GetComponent<Canvas>().sortingOrder = 80;
        result.Panel.anchorMin = Vector2.zero;
        result.Panel.anchorMax = Vector2.one;
        result.Panel.offsetMin = result.Panel.offsetMax = Vector2.zero;
        return result;
    }

    internal TextMeshProUGUI Label(string name, float fontSize, TextAlignmentOptions alignment)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(Panel, false);
        var text = go.AddComponent<TextMeshProUGUI>();
        text.font = _font;
        text.fontSharedMaterial = _material;
        text.fontSize = fontSize;
        text.enableAutoSizing = true;
        text.fontSizeMax = fontSize;
        text.fontSizeMin = fontSize * 0.75f;
        text.fontWeight = _fontWeight;
        text.fontStyle = _fontStyle;
        text.characterSpacing = _characterSpacing;
        text.wordSpacing = _wordSpacing;
        text.lineSpacing = _lineSpacing;
        text.color = Color.white;
        text.alignment = alignment;
        text.enableWordWrapping = false;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.raycastTarget = false;
        text.richText = true;
        return text;
    }

    internal void Fit()
    {
        if (Panel.anchorMin != Panel.anchorMax)
        {
            Panel.localScale = Vector3.one;
            return;
        }
        var available = _canvasRect.rect.size;
        var size = Panel.sizeDelta;
        // Leave room for the panel's anchor offset as well as a small screen margin.
        var offset = Panel.anchoredPosition;
        float width = Mathf.Max(1f, available.x - Mathf.Abs(offset.x) - 32f);
        float height = Mathf.Max(1f, available.y - Mathf.Abs(offset.y) - 32f);
        float scale = Mathf.Min(1f, width / size.x, height / size.y);
        Panel.localScale = new Vector3(scale, scale, 1f);
    }

    // Project native HUD glyphs through their own canvas camera. Rect sizes alone can include
    // large empty padding, and the native canvas uses different render coordinates.
    internal bool NativeBounds(Component native, out Rect bounds) => NativeBounds(native, out bounds, false);

    internal bool NativeBounds(Component native, out Rect bounds, bool includeChildren)
    {
        bounds = default;
        if (native == null || !native.gameObject.activeInHierarchy) return false;
        bool found = false;
        var primary=native.GetComponent<TextMeshProUGUI>();
        var labels=!includeChildren && primary!=null && !(native is HealthUI) && !(native is EnergyUI)
            ?new[]{primary}:native.GetComponentsInChildren<TextMeshProUGUI>(true);
        foreach (var text in labels)
        {
            if (!text.isActiveAndEnabled || string.IsNullOrEmpty(text.text) || text.color.a <= .01f) continue;
            var canvas = text.canvas;
            if (canvas == null) continue;
            text.ForceMeshUpdate();
            if (text.textInfo.characterCount == 0) continue;
            var camera = HudProjection.CanvasCamera(canvas);
            var box = text.textBounds;
            for (int x=0;x<2;x++) for (int y=0;y<2;y++)
            {
                var world = text.transform.TransformPoint(new Vector3(x==0?box.min.x:box.max.x,y==0?box.min.y:box.max.y,0f));
                var screen = (Vector2)HudProjection.WorldToScreen(camera,world);
                RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvasRect,screen,null,out var point);
                if (!found) { bounds = new Rect(point,Vector2.zero); found=true; }
                else bounds = Rect.MinMaxRect(Mathf.Min(bounds.xMin,point.x),Mathf.Min(bounds.yMin,point.y),Mathf.Max(bounds.xMax,point.x),Mathf.Max(bounds.yMax,point.y));
            }
        }
        return found && bounds.width > 0f && bounds.height > 0f && bounds.Overlaps(_canvasRect.rect);
    }

    internal void BelowNative(float fallbackTop, float gap, bool alignRight, params Component[] anchors)
    {
        float top = fallbackTop;
        float right = _canvasRect.rect.xMax - 24f;
        bool found = false;
        foreach (var anchor in anchors)
            if (NativeBounds(anchor,out var bounds))
            {
                top = Mathf.Max(top,_canvasRect.rect.yMax-bounds.yMin+gap);
                right = found ? Mathf.Max(right,bounds.xMax) : bounds.xMax;
                found=true;
            }
        var position=Panel.anchoredPosition;
        position.y=-top;
        if (alignRight && found) position.x=Mathf.Min(-16f,right-_canvasRect.rect.xMax);
        Panel.anchoredPosition=position;
        Fit();
    }

    internal void AvoidVitals(params Component[] anchors)
    {
        Panel.anchoredPosition=new Vector2(16f,-12f);
        var canvas=_canvasRect.rect;
        foreach(var anchor in anchors)
            if (NativeBounds(anchor,out var bounds))
            {
                var own=new Rect(canvas.xMin+Panel.anchoredPosition.x,canvas.yMax+Panel.anchoredPosition.y-Panel.sizeDelta.y,Panel.sizeDelta.x,Panel.sizeDelta.y);
                bounds.xMin-=12f; bounds.xMax+=12f; bounds.yMin-=12f; bounds.yMax+=12f;
                if(own.Overlaps(bounds))
                {
                    var position=Panel.anchoredPosition;
                    position.x=Mathf.Min(bounds.xMax-canvas.xMin,canvas.width-Panel.sizeDelta.x-16f);
                    Panel.anchoredPosition=position;
                }
            }
        Fit();
    }

    private void LateUpdate()
    {
        if (_owner == null || _owner != HUDCanvas.instance)
            Destroy(gameObject);
    }

    private void OnDestroy()
    {
        if (_material != null)
            Destroy(_material);
    }
}
