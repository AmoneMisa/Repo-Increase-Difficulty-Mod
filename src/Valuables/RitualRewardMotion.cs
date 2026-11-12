using System.Collections.Generic;
using UnityEngine;

namespace IncreaseHardMod.Valuables;

/// <summary>Local ornament animation only; never moves the valuable or its collision.</summary>
internal sealed class RitualRewardMotion : MonoBehaviour
{
    private ValuableArtInstance _art;
    private Mesh _canvas;
    private Vector2[] _uv, _restUv;
    private readonly List<int> _eyeVertices = new();
    private float[] _eyeWeights;
    private float _nextGaze;

    private void Start()
    {
        _art = GetComponent<ValuableArtInstance>();
        if (_art == null || !_art.EnsureBindings()) { enabled = false; return; }
        foreach (var part in _art.bindings)
        {
            if (part.group != "portrait_canvas" || part.node == null) continue;
            // Clone only this canvas. Other spawns and the registration prefab retain
            // their own neutral gaze; the picture and its collision remain stationary.
            _canvas = part.node.GetComponent<MeshFilter>().mesh;
            _canvas.MarkDynamic();
            _restUv = _canvas.uv;
            _uv = (Vector2[])_restUv.Clone();
            _eyeWeights = new float[_uv.Length];
            for (int i = 0; i < _uv.Length; i++)
            {
                float weight = Mathf.Max(EyeWeight(_uv[i], new Vector2(.450f, .762f)),
                    EyeWeight(_uv[i], new Vector2(.579f, .764f)));
                if (weight <= 0) continue;
                _eyeWeights[i] = weight;
                _eyeVertices.Add(i);
            }
            break;
        }
    }

    private static float EyeWeight(Vector2 uv, Vector2 eye)
    {
        float x = (uv.x - eye.x) / .033f, y = (uv.y - eye.y) / .020f;
        float falloff = Mathf.Clamp01(1f - x * x - y * y);
        return falloff * falloff;
    }

    private void LateUpdate()
    {
        if (_art?.bindings == null) return;
        float t = Time.time;
        if (_canvas != null && t >= _nextGaze)
        {
            _nextGaze = t + .08f;
            float shift = Mathf.Sin(t * .37f) * .004f;
            foreach (int i in _eyeVertices)
                _uv[i] = _restUv[i] + Vector2.right * (shift * _eyeWeights[i]);
            _canvas.uv = _uv;
        }
        foreach (var part in _art.bindings)
        {
            if (part.node == null) continue;
            switch (part.group)
            {
                case "quantum_orbit":
                    part.node.localRotation = Quaternion.Euler(0, t * 22f, 0);
                    break;
                case "sun_core":
                    part.node.localRotation = Quaternion.Euler(0, t * 9f, 0);
                    break;
            }
        }
    }

    private void OnDestroy()
    {
        if (_canvas != null) Destroy(_canvas);
    }
}
