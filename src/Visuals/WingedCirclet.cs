using UnityEngine;
using UnityEngine.Rendering;

namespace IncreaseHardMod.Visuals;

/// <summary>
/// A floating winged circlet above every player's head: a gold bar with a centre gem, two feathered wings
/// and hanging pendants (original procedural model). Its wings, gem and glow take whichever of seven
/// colours is closest to the player's own body colour. Purely local visuals: every client builds it from
/// the colours the game already syncs, so there is nothing to network. Hidden in your own first-person view.
/// </summary>
internal sealed class WingedCirclet : MonoBehaviour
{
    internal struct Variant
    {
        public string Name;
        public Color Wing, Glow;
        public Variant(string name, Color wing, Color glow) { Name = name; Wing = wing; Glow = glow; }
    }

    // The seven colourways: black (violet glow), cyan, yellow, green, white, purple, red.
    internal static readonly Variant Black = new("Black", new Color(.07f, .07f, .09f), new Color(.62f, .48f, .95f));
    internal static readonly Variant White = new("White", new Color(.95f, .95f, 1f), new Color(.9f, .92f, 1f));
    private static readonly (float hue, Variant v)[] Hued =
    {
        (0f, new Variant("Red", new Color(1f, .14f, .12f), new Color(1f, .35f, .25f))),
        (52f, new Variant("Yellow", new Color(1f, .84f, .14f), new Color(1f, .85f, .35f))),
        (125f, new Variant("Green", new Color(.2f, 1f, .35f), new Color(.35f, 1f, .45f))),
        (190f, new Variant("Cyan", new Color(.2f, .85f, 1f), new Color(.35f, .9f, 1f))),
        (280f, new Variant("Purple", new Color(.65f, .25f, 1f), new Color(.72f, .4f, 1f))),
    };

    /// <summary>Nearest colourway: greys go black/white by brightness, everything else by hue.</summary>
    internal static Variant Match(Color c)
    {
        Color.RGBToHSV(c, out float h, out float s, out float v);
        if (v < .22f) return Black;
        if (s < .25f) return v > .55f ? White : Black;
        float hue = h * 360f, best = float.MaxValue;
        var pick = Hued[0].v;
        foreach (var (target, variant) in Hued)
        {
            float d = Mathf.Abs(Mathf.DeltaAngle(hue, target));
            if (d < best) { best = d; pick = variant; }
        }
        return pick;
    }

    private static Mesh _gold, _colour, _quad;
    private static Material _goldMaterial;

    internal PlayerAvatar avatar;
    private Transform _root, _float, _glow;
    private Material _wingMaterial, _glowMaterial;
    private Light _light;
    private int _colourIndex = int.MinValue;
    private float _check;
    private bool _local;

    private void Start()
    {
        var visuals = avatar != null ? avatar.playerAvatarVisuals : null;
        var anchor = visuals != null ? visuals.attachPointTopHeadMiddle : null;
        if (anchor == null) { Destroy(this); return; }
        _local = avatar.isLocal;
        BuildMeshes();

        _root = new GameObject("IHM Winged Circlet").transform;
        _root.SetParent(anchor, false);
        float scale = ModConfig.CircletScale.Value;
        float lossy = Mathf.Max(.0001f, anchor.lossyScale.x);
        _root.localScale = Vector3.one * (scale / lossy);
        _root.localPosition = Vector3.up * (.13f * scale / lossy);
        _float = new GameObject("Float").transform;
        _float.SetParent(_root, false);

        _wingMaterial = new Material(Shader.Find("Standard")) { name = "IHM Circlet Wings" };
        _wingMaterial.SetFloat("_Glossiness", .55f);
        _wingMaterial.EnableKeyword("_EMISSION");
        Part("Gold", _gold, GoldMaterial());
        Part("Wings", _colour, _wingMaterial);

        if (!_local)
        {
            _glow = new GameObject("Glow").transform;
            _glow.SetParent(_float, false);
            _glow.localScale = new Vector3(.85f, .45f, 1f);
            _glowMaterial = GlowTextures.Sprite(GlowTextures.Soft, "IHM Circlet Glow");
            var r = _glow.gameObject.AddComponent<MeshRenderer>();
            _glow.gameObject.AddComponent<MeshFilter>().sharedMesh = _quad;
            r.sharedMaterial = _glowMaterial;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;

            _light = new GameObject("Light").AddComponent<Light>();
            _light.transform.SetParent(_float, false);
            _light.type = LightType.Point;
            _light.range = 1.1f;
            _light.intensity = .55f;
            _light.shadows = LightShadows.None;
        }
        Recolour(true);
    }

    private void Part(string name, Mesh mesh, Material material)
    {
        var obj = new GameObject(name);
        obj.transform.SetParent(_float, false);
        obj.AddComponent<MeshFilter>().sharedMesh = mesh;
        var r = obj.AddComponent<MeshRenderer>();
        r.sharedMaterial = material;
        r.receiveShadows = false;
        if (_local)
        {
            // Same treatment the game gives your own body: invisible to you, still casts its shadow.
            obj.layer = LayerMask.NameToLayer("PlayerVisualsLocal");
            r.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
        }
        else r.shadowCastingMode = ShadowCastingMode.Off;
    }

    private int MainColourIndex()
    {
        var colours = avatar != null && avatar.playerCosmetics != null ? avatar.playerCosmetics.colorsEquipped : null;
        if (colours == null) return -1;
        foreach (var type in new[] { SemiFunc.CosmeticType.BodyTopMesh, SemiFunc.CosmeticType.HeadTopMesh })
        {
            int i = (int)type;
            if (i < colours.Length && colours[i] >= 0) return colours[i];
        }
        foreach (int c in colours) if (c >= 0) return c;
        return -1;
    }

    private void Recolour(bool force)
    {
        int index = MainColourIndex();
        if (!force && index == _colourIndex) return;
        _colourIndex = index;
        var list = MetaManager.instance != null ? MetaManager.instance.colors : null;
        var colour = list != null && index >= 0 && index < list.Count && list[index] != null ? list[index].color : Color.white;
        var v = Match(colour);
        _wingMaterial.color = v.Wing;
        _wingMaterial.SetColor("_EmissionColor", v.Glow * (v.Name == "Black" ? .15f : .9f));
        if (_glowMaterial != null) _glowMaterial.color = new Color(v.Glow.r, v.Glow.g, v.Glow.b, .4f);
        if (_light != null) _light.color = v.Glow;
    }

    private void LateUpdate()
    {
        if (_root == null) return;
        _check -= Time.deltaTime;
        if (_check <= 0f) { _check = 1f; Recolour(false); }
        float t = Time.time + GetInstanceID() * .37f;
        _float.localPosition = Vector3.up * (Mathf.Sin(t * 1.6f) * .012f);
        _float.localRotation = Quaternion.Euler(0f, Mathf.Sin(t * .7f) * 4f, Mathf.Sin(t * 1.1f) * 2f);
        if (_glow != null)
        {
            var cam = Camera.main;
            if (cam != null) _glow.rotation = Quaternion.LookRotation(_glow.position - cam.transform.position, cam.transform.up);
        }
    }

    private void OnDestroy()
    {
        if (_root != null) Destroy(_root.gameObject);
        if (_wingMaterial != null) Destroy(_wingMaterial);
        if (_glowMaterial != null) Destroy(_glowMaterial);
    }

    private static Material GoldMaterial()
    {
        if (_goldMaterial != null) return _goldMaterial;
        _goldMaterial = new Material(Shader.Find("Standard")) { name = "IHM Circlet Gold", color = new Color(1f, .76f, .3f), hideFlags = HideFlags.HideAndDontSave };
        _goldMaterial.SetFloat("_Metallic", .85f);
        _goldMaterial.SetFloat("_Glossiness", .7f);
        _goldMaterial.EnableKeyword("_EMISSION");
        _goldMaterial.SetColor("_EmissionColor", new Color(.35f, .22f, .05f));
        return _goldMaterial;
    }

    // (angle above horizontal, length, width, forward offset). Mirrored in the preview render script.
    private static readonly (float angle, float length, float width, float depth)[] Feathers =
    {
        (14f, .095f, .030f, 0f), (24f, .108f, .030f, .001f), (34f, .120f, .030f, .002f), (44f, .132f, .030f, .003f),
        (54f, .142f, .030f, .004f), (64f, .150f, .028f, .005f), (74f, .152f, .026f, .006f),
        (30f, .060f, .046f, .010f), (52f, .066f, .046f, .011f), (72f, .064f, .042f, .012f), // coverts
    };

    // Model units are metres at scale 1. X = across the head, Y = up, Z = forward.
    private static void BuildMeshes()
    {
        if (_gold != null) return;
        var gold = new ProcMesh();
        var colour = new ProcMesh();
        gold.Box(ProcMesh.TRS(Vector3.zero, Vector3.zero, new Vector3(.40f, .016f, .016f)));             // bar
        gold.Box(ProcMesh.TRS(new Vector3(0f, .004f, 0f), new Vector3(0, 0, 45f), new Vector3(.045f, .045f, .018f))); // gem mount
        gold.Diamond(ProcMesh.TRS(new Vector3(0f, .058f, 0f), Vector3.zero, new Vector3(.022f, .065f, .014f))); // crown spike
        colour.Diamond(ProcMesh.TRS(new Vector3(0f, .004f, .011f), Vector3.zero, new Vector3(.03f, .056f, .02f))); // gem
        foreach (float side in new[] { -1f, 1f })
        {
            gold.Diamond(ProcMesh.TRS(new Vector3(side * .055f, .026f, 0f), Vector3.zero, new Vector3(.012f, .036f, .01f)));
            gold.Diamond(ProcMesh.TRS(new Vector3(side * .205f, 0f, 0f), Vector3.zero, new Vector3(.034f, .034f, .02f))); // end cap
            gold.Diamond(ProcMesh.TRS(new Vector3(side * .205f, .036f, 0f), new Vector3(0, 0, side * -30f), new Vector3(.03f, .055f, .028f))); // wing root
            foreach (float x in new[] { .12f, .2f })
            {
                gold.Box(ProcMesh.TRS(new Vector3(side * x, -.042f, 0f), Vector3.zero, new Vector3(.007f, .06f, .007f)));
                gold.Diamond(ProcMesh.TRS(new Vector3(side * x, -.082f, 0f), Vector3.zero, new Vector3(.016f, .03f, .012f)));
            }
            // A raised wing: long flight feathers fanning out and up (longest at the top, like a wing tip),
            // with a layer of short covert feathers over the root.
            var root = new Vector3(side * .2f, .045f, -.004f);
            foreach (var (angle, length, width, depth) in Feathers)
            {
                var dir = new Vector3(side * Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad), -.1f).normalized;
                var rotation = Quaternion.FromToRotation(Vector3.right, dir);
                colour.Diamond(Matrix4x4.TRS(root + dir * (length * .5f) + Vector3.forward * depth, rotation,
                    new Vector3(length, width, .011f)));
            }
        }
        _gold = gold.Build("IHM Circlet Gold");
        _colour = colour.Build("IHM Circlet Wings");
        _quad = ProcMesh.UnitQuad("IHM Circlet Glow");
        _gold.hideFlags = _colour.hideFlags = _quad.hideFlags = HideFlags.HideAndDontSave;
    }
}

/// <summary>Adds a <see cref="WingedCirclet"/> to every player in the level (host and clients alike).</summary>
internal sealed class WingedCircletDriver : MonoBehaviour
{
    private float _scan;

    private void Update()
    {
        if (!ModConfig.CircletEnabled.Value) return;
        _scan -= Time.deltaTime;
        if (_scan > 0f) return;
        _scan = 1f;
        var players = GameDirector.instance != null ? GameDirector.instance.PlayerList : null;
        if (players == null) return;
        foreach (var avatar in players)
        {
            if (avatar == null || avatar.playerAvatarVisuals == null || avatar.playerAvatarVisuals.isMenuAvatar) continue;
            if (avatar.GetComponent<WingedCirclet>() != null) continue;
            avatar.gameObject.AddComponent<WingedCirclet>().avatar = avatar;
        }
    }
}
