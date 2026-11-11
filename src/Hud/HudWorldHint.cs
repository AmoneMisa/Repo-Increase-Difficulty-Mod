using TMPro;
using UnityEngine;

namespace IncreaseHardMod.Hud;

/// <summary>World hints share the native TMP font and material and receive world post-processing.</summary>
internal sealed class HudWorldHint : MonoBehaviour
{
    private TextMeshPro _label;
    private Renderer[] _renderers;
    private Material _material;
    private void LateUpdate()
    {
        var camera = Camera.main;
        if (!SemiFunc.RunIsShop() || camera == null) { if (_label != null) _label.enabled = false; return; }
        if (_label == null)
        {
            var template = StatsUI.instance?.Text;
            if (template == null || template.font == null) return;
            _renderers = GetComponentsInChildren<Renderer>();
            var go = new GameObject("Item information hint", typeof(RectTransform));
            _label = go.AddComponent<TextMeshPro>();
            _label.font = template.font;
            _material = new Material(template.fontSharedMaterial);
            if (_material.HasProperty(ShaderUtilities.ID_FaceColor)) _material.SetColor(ShaderUtilities.ID_FaceColor, Color.white);
            _label.fontSharedMaterial = _material;
            _label.fontWeight = template.fontWeight;
            _label.fontStyle = template.fontStyle;
            _label.fontSize = 2.4f;
            _label.alignment = TextAlignmentOptions.Center;
            _label.color = HudStyle.Info;
            _label.text = "[i]";
            _label.rectTransform.sizeDelta = new Vector2(.6f, .35f);
            _label.raycastTarget = false;
        }
        var bounds = new Bounds(transform.position, Vector3.zero);
        foreach (var renderer in _renderers) if (renderer != null && renderer.enabled) bounds.Encapsulate(renderer.bounds);
        var position = new Vector3(bounds.center.x, bounds.max.y + .18f, bounds.center.z);
        Vector3 delta = position - camera.transform.position;
        _label.enabled = delta.sqrMagnitude < 36f && Vector3.Dot(camera.transform.forward, delta) > 0f &&
            !Physics.Raycast(camera.transform.position, delta.normalized, delta.magnitude, SemiFunc.LayerMaskGetVisionObstruct(), QueryTriggerInteraction.Ignore);
        _label.transform.SetPositionAndRotation(position, camera.transform.rotation);
        _label.transform.localScale = Vector3.one;
    }

    private void OnDisable() { if (_label != null) _label.enabled = false; }
    private void OnDestroy() { if (_label != null) Destroy(_label.gameObject); if (_material != null) Destroy(_material); }
}
