using Photon.Pun;
using UnityEngine;
using UnityEngine.Rendering;

namespace IncreaseHardMod.Monsters;

/// <summary>
/// What everyone sees on an afflicted player: licking flames and an ember glow while burning, an electric arc from
/// the witch's hands while levitated, a squeezing green coil while constricted, a stone shell while petrified,
/// red drops while bleeding and green ones while envenomed.
/// Primitive, collider-free, destroyed when the last effect ends or the player is removed.
/// </summary>
internal sealed class AfflictionVisual : MonoBehaviour
{
    private readonly float[] _until = new float[8];
    private int _source;
    private PlayerAvatar _player;
    private Light _ember;
    private LineRenderer _arc;
    private LineRenderer _coil;
    private Transform _stone;
    private Material _stoneMat, _flameMat, _dropMat;
    private readonly Transform[] _flames = new Transform[4];
    private readonly Transform[] _drops = new Transform[4];
    private const int CoilPoints = 48;

    internal void Set(Affliction kind, float seconds, int sourceId)
    {
        _player ??= GetComponent<PlayerAvatar>();
        _until[(int)kind] = seconds > 0f ? Time.time + seconds : 0f;
        if (sourceId != 0) _source = sourceId;
        enabled = true;
    }

    private bool On(Affliction a) => _until[(int)a] > Time.time;

    private void LateUpdate()
    {
        if (_player == null || _player.isDisabled) { Clear(); enabled = false; return; }
        var center = _player.transform.position + Vector3.up * .9f;
        // The local player never sees their own body effects in first person, only the screen frame.
        bool local = _player.photonView != null && _player.photonView.IsMine;
        bool any = false;

        bool burn = On(Affliction.Burning); any |= burn;
        if (burn && _ember == null)
        {
            _ember = new GameObject("IHM burning").AddComponent<Light>();
            _ember.type = LightType.Point; _ember.color = new Color(1f, .45f, .1f); _ember.range = 2.5f; _ember.shadows = LightShadows.None;
        }
        if (_ember != null) { _ember.enabled = burn; _ember.transform.position = center; _ember.intensity = 1.2f + .8f * Mathf.PerlinNoise(Time.time * 8f, 0f); }
        // Flames lick up the body (others only: the burning player has the screen frame).
        if (burn && !local && _flames[0] == null)
        {
            _flameMat = new Material(MonsterFx.Additive()) { name = "IHM burning", color = new Color(1f, .5f, .1f, .85f) };
            for (int i = 0; i < _flames.Length; i++) _flames[i] = Blob("IHM flame", _flameMat);
        }
        for (int i = 0; i < _flames.Length; i++)
        {
            var flame = _flames[i];
            if (flame == null) continue;
            flame.gameObject.SetActive(burn);
            float u = Mathf.Repeat(Time.time * 1.3f + i * .27f, 1f), a = i * 1.7f + Time.time * .8f;
            flame.position = _player.transform.position + new Vector3(Mathf.Cos(a) * .28f, .25f + u * 1.25f, Mathf.Sin(a) * .28f);
            flame.localScale = new Vector3(.16f, .34f, .16f) * (1f - u * .75f);
        }

        bool zap = On(Affliction.Electrified); any |= zap;
        var source = zap && _source != 0 ? PhotonView.Find(_source) : null;
        if (zap && source != null)
        {
            if (_arc == null)
            {
                _arc = new GameObject("IHM electric arc").AddComponent<LineRenderer>();
                _arc.useWorldSpace = true; _arc.positionCount = 12; _arc.sharedMaterial = MonsterFx.Additive();
                _arc.startWidth = .05f; _arc.endWidth = .03f; _arc.shadowCastingMode = ShadowCastingMode.Off;
                _arc.startColor = new Color(.7f, .6f, 1f); _arc.endColor = new Color(.5f, .85f, 1f);
            }
            var brain = source.GetComponent<MonsterBrain>();
            var from = brain == null ? source.transform.position + Vector3.up : brain.Visual != null ? brain.Visual.CastPoint : brain.BodyPosition + Vector3.up * .6f;
            _arc.enabled = true;
            for (int i = 0; i < 12; i++)
            {
                float u = i / 11f;
                var p = Vector3.Lerp(from, center, u);
                if (i > 0 && i < 11) p += Random.insideUnitSphere * .18f;
                _arc.SetPosition(i, p);
            }
        }
        else if (_arc != null) _arc.enabled = false;

        // The snake's body: a helix around the victim that tightens in pulses.
        bool coil = On(Affliction.Constricted); any |= coil;
        if (coil && _coil == null && !local)
        {
            _coil = new GameObject("IHM coils").AddComponent<LineRenderer>();
            _coil.useWorldSpace = true; _coil.positionCount = CoilPoints; _coil.sharedMaterial = MonsterFx.Additive();
            _coil.startWidth = .2f; _coil.endWidth = .07f; _coil.numCapVertices = 4; _coil.shadowCastingMode = ShadowCastingMode.Off;
            _coil.startColor = new Color(.2f, .5f, .14f); _coil.endColor = new Color(.5f, .8f, .3f);
        }
        if (_coil != null)
        {
            _coil.enabled = coil;
            float squeeze = .36f + .05f * Mathf.Sin(Time.time * 6f), spin = Time.time * 1.2f;
            for (int i = 0; coil && i < CoilPoints; i++)
            {
                float u = i / (CoilPoints - 1f), a = spin + u * Mathf.PI * 6f;
                _coil.SetPosition(i, _player.transform.position + new Vector3(Mathf.Cos(a) * squeeze, .25f + u * 1.1f, Mathf.Sin(a) * squeeze));
            }
        }

        bool stone = On(Affliction.Petrified); any |= stone;
        if (stone && _stone == null && !local) _stone = Shell("IHM stone shell", new Color(.62f, .6f, .55f), out _stoneMat, PrimitiveType.Capsule);
        if (_stone != null)
        {
            _stone.gameObject.SetActive(stone);
            _stone.position = center;
            _stone.localScale = new Vector3(.75f, .95f, .75f);
        }
        // Blood (red) or venom (green) drips from the wound.
        bool bleed = On(Affliction.Bleeding), venom = On(Affliction.Venom), drip = bleed || venom;
        if (drip && !local && _drops[0] == null)
        {
            _dropMat = new Material(MonsterFx.Additive()) { name = "IHM drops" };
            for (int i = 0; i < _drops.Length; i++) _drops[i] = Blob("IHM drop", _dropMat);
        }
        if (_dropMat != null && drip) _dropMat.color = bleed ? new Color(.85f, .04f, .04f, .95f) : new Color(.5f, .95f, .12f, .95f);
        for (int i = 0; i < _drops.Length; i++)
        {
            var drop = _drops[i];
            if (drop == null) continue;
            drop.gameObject.SetActive(drip);
            float u = Mathf.Repeat(Time.time * 1.1f + i * .31f, 1f), a = i * 2.4f;
            drop.position = center + new Vector3(Mathf.Cos(a) * .22f, .15f - u * u * 1f, Mathf.Sin(a) * .22f);
            drop.localScale = new Vector3(.05f, .09f, .05f) * (1f - u * .4f);
        }
        any |= drip || On(Affliction.Slowed) || On(Affliction.Dragged);
        if (!any) { Clear(); enabled = false; }
    }

    private static Transform Shell(string name, Color color, out Material material, PrimitiveType shape)
    {
        var go = GameObject.CreatePrimitive(shape);
        go.name = name;
        var collider = go.GetComponent<Collider>(); collider.enabled = false; Destroy(collider);
        var renderer = go.GetComponent<Renderer>();
        material = new Material(Shader.Find("Standard")) { color = color };
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        return go.transform;
    }

    private static Transform Blob(string name, Material material)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = name;
        var collider = go.GetComponent<Collider>(); collider.enabled = false; Destroy(collider);
        var renderer = go.GetComponent<Renderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        return go.transform;
    }

    private void Clear()
    {
        for (int i = 0; i < _flames.Length; i++) { if (_flames[i] != null) Destroy(_flames[i].gameObject); _flames[i] = null; }
        for (int i = 0; i < _drops.Length; i++) { if (_drops[i] != null) Destroy(_drops[i].gameObject); _drops[i] = null; }
        if (_flameMat != null) Destroy(_flameMat);
        if (_dropMat != null) Destroy(_dropMat);
        _flameMat = _dropMat = null;
        if (_ember != null) Destroy(_ember.gameObject);
        if (_arc != null) Destroy(_arc.gameObject);
        if (_coil != null) Destroy(_coil.gameObject);
        if (_stone != null) Destroy(_stone.gameObject);
        if (_stoneMat != null) Destroy(_stoneMat);
        _ember = null; _arc = null; _coil = null; _stone = null; _stoneMat = null;
    }

    private void OnDestroy() => Clear();
}
