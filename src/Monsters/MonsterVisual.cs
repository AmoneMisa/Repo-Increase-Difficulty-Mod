using System.Collections.Generic;
using IncreaseHardMod.Visuals;
using UnityEngine;

namespace IncreaseHardMod.Monsters;

/// <summary>
/// The monster's body on every client: the embedded Blender model (src/Art/monster_*.mesh.json), built at runtime,
/// following the synced enemy rigidbody, with procedural joint animation from the brain's state.
///
/// Layers, all visible on every client without a network message of their own: the state animation (windup,
/// attack, hold - from the brain's synced state), the movement by gait (walk / crawl / flight), and three
/// reactions on top - the hurt flinch (native onHurt), the struggle in a player's grip (native grab list) and
/// the stun (native Enemy.Stunned). See <see cref="MonsterAnimRules"/>.
///
/// Parts are merged per animated group and material (one draw call each instead of one per primitive), the
/// renderers are handed to the native EnemyHealth so hurt/heal flashes work, and every mesh/material created
/// here is destroyed with the monster. The model has no colliders: hits use the native enemy body.
/// </summary>
internal sealed class MonsterVisual : MonoBehaviour
{
    [SerializeField] private MonsterKind _kind;
    [SerializeField] private string _art;
    [SerializeField] private float _height = 1f;

    private sealed class Joint
    {
        public Transform T;
        public Quaternion Rest, Target = Quaternion.identity;
        public float K = 16f;
    }

    // tools/build_monsters.py: the basilisk chain hangs on tail6 (neck tail5..0 + head in front, body behind).
    private const int BasiliskRoot = 6;
    private const int SparkPoints = 7;

    private MonsterBrain _brain;
    private Rigidbody _body;
    private Collider[] _colliders;
    private Transform _model;
    private readonly List<Mesh> _meshes = new();
    private readonly List<Material> _materials = new();
    private readonly Dictionary<string, Joint> _joints = new();
    private readonly List<Joint> _all = new();
    private readonly List<(Joint joint, bool left)> _wings = new();
    private readonly List<(Joint joint, bool left, bool front)> _legs = new();
    private Joint[] _tail = new Joint[0];
    private readonly Joint[] _nagaArms = new Joint[4]; // upper left, lower left, upper right, lower right
    private Joint _head, _jaw, _armL, _armR;
    // Twins: Hansel (left) then Gretel - head, inner arm, outer arm, left foot, right foot.
    private readonly Joint[] _twins = new Joint[10];
    private bool _tethered;
    private Vector3 _tetherAt;
    private Transform _handL, _handR, _orbL, _orbR, _lightAt;
    private readonly List<Transform> _eyeOrbs = new();
    private Material _eyeMaterial, _orbMaterial;
    private LineRenderer _spark;
    private Color _eyeColor, _lightColor;
    private float _eyeSize, _eye, _fire, _volt, _fold, _lightPower, _lightRange;
    private Quaternion _tilt = Quaternion.identity;
    private Vector3 _basePos, _shift;
    private float _modelScale = 1f, _pop = 1f, _phase;
    // Lifts the whole model a little while its head would sink under the floor (ground monsters with a low head).
    private float _floorLift;
    // Dizzy stars circling over the head while stunned (every monster).
    private Transform _stars;
    private readonly List<Transform> _starList = new();
    private Vector3 _lastPos;
    private Light _glow, _light;
    private bool _built, _failed;
    // Hurt flash (our own: the native one needs a _ColorOverlay property) and movement / approach sounds.
    private float _flash, _stepClock, _callAt;
    private bool _wasHunting;
    // Reactions every client derives from natively synced state: the hurt flinch (onHurt runs everywhere), the
    // struggle in a player's grip (grab list) and the stun (Enemy.Stunned).
    private float _hitAt = -10f, _hitSide = 1f, _held, _turn, _lastYaw, _move;
    private bool _stunned, _wasStunned;
    // The attack shown now, the one before it and when it changed: a strike's follow-through plays on into the
    // next state instead of being cut off by it.
    private MonsterAnim _anim, _animWas;
    private float _animAt, _tossAt = -10f;
    private readonly List<(Material material, string property, Color color)> _tints = new();

    internal void Configure(MonsterKind kind, string art, float height) { _kind = kind; _art = art; _height = height; }

    private void Start()
    {
        var parent = GetComponentInParent<EnemyParent>();
        _brain = parent != null ? parent.GetComponentInChildren<MonsterBrain>(true) : null;
        var rigid = parent != null ? parent.GetComponentInChildren<EnemyRigidbody>(true) : null;
        _body = rigid != null ? rigid.GetComponent<Rigidbody>() : null;
        _colliders = rigid != null ? rigid.GetComponentsInChildren<Collider>(true) : new Collider[0];
        if (_brain != null) _brain.Visual = this;
        Build(parent);
    }

    private void Build(EnemyParent parent)
    {
        if (_built || _failed) return;
        try
        {
            var model = ItemArt.LoadModel(_art);
            var template = ItemArt.WeaponMaterialDonor(null);
            if (template == null) template = new Material(Shader.Find("Standard"));
            var pivots = new Dictionary<string, Vector3>();
            if (model.pivots != null) foreach (var p in model.pivots) pivots[p.name] = p.position;
            pivots.TryGetValue("body", out var bodyPivot);

            _model = new GameObject("Model").transform;
            _model.SetParent(transform, false);
            var body = new GameObject("body").transform;
            body.SetParent(_model, false);
            body.localPosition = bodyPivot;
            var groups = new Dictionary<string, Transform> { ["body"] = body };
            Transform Group(string name)
            {
                if (groups.TryGetValue(name, out var g)) return g;
                g = new GameObject(name).transform;
                g.SetParent(body, false);
                g.localPosition = (pivots.TryGetValue(name, out var p) ? p : bodyPivot) - bodyPivot;
                groups[name] = g;
                return g;
            }

            // Merge parts by group + material.
            var batches = new Dictionary<string, (string group, Color color, string texture, List<Vector3> v, List<Vector3> n, List<Vector2> uv, List<int> tri)>();
            var min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            var max = -min;
            foreach (var part in model.parts)
            {
                string group = part.name.Split('|')[0];
                Group(group);
                string key = group + "#" + ColorUtility.ToHtmlStringRGBA(part.color) + "#" + part.texture;
                if (!batches.TryGetValue(key, out var b))
                    batches[key] = b = (group, part.color, part.texture, new List<Vector3>(), new List<Vector3>(), new List<Vector2>(), new List<int>());
                var rest = part.rest;
                var scale = rest == null || rest.scale == Vector3.zero ? Vector3.one : rest.scale;
                var rot = rest == null || (rest.rotation.x == 0 && rest.rotation.y == 0 && rest.rotation.z == 0 && rest.rotation.w == 0) ? Quaternion.identity : rest.rotation;
                var pos = rest != null ? rest.position : Vector3.zero;
                var pivot = pivots.TryGetValue(group, out var gp) ? gp : bodyPivot;
                int start = b.v.Count;
                for (int i = 0; i < part.vertices.Length; i++)
                {
                    var world = pos + rot * Vector3.Scale(scale, part.vertices[i]);
                    if (float.IsNaN(world.x) || float.IsInfinity(world.x) || float.IsNaN(world.y) || float.IsInfinity(world.y) ||
                        float.IsNaN(world.z) || float.IsInfinity(world.z)) world = Vector3.zero;
                    min = Vector3.Min(min, world); max = Vector3.Max(max, world);
                    b.v.Add(world - pivot);
                    b.n.Add((rot * part.normals[i]).normalized);
                    b.uv.Add(part.uv[i]);
                }
                foreach (int t in part.triangles) b.tri.Add(start + t);
            }
            var cache = new Dictionary<string, Material>();
            var renderers = new List<MeshRenderer>();
            foreach (var b in batches.Values)
            {
                var mesh = new Mesh
                {
                    name = "IHM " + _art + " " + b.group,
                    indexFormat = b.v.Count > 65535 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16,
                };
                mesh.SetVertices(b.v); mesh.SetNormals(b.n); mesh.SetUVs(0, b.uv); mesh.SetTriangles(b.tri, 0);
                mesh.RecalculateBounds();
                _meshes.Add(mesh);
                string mk = ColorUtility.ToHtmlStringRGBA(b.color) + b.texture;
                if (!cache.TryGetValue(mk, out var material))
                {
                    material = ItemArt.Paint(template, string.IsNullOrEmpty(b.texture) ? null : b.texture, b.color, "IHM " + _art + " " + mk);
                    if (_kind == MonsterKind.Pig && material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", .7f);
                    if (_kind == MonsterKind.Pig && material.HasProperty("_Metallic")) material.SetFloat("_Metallic", .6f);
                    _materials.Add(material);
                    cache[mk] = material;
                }
                var go = new GameObject("Mesh " + b.group);
                go.transform.SetParent(Group(b.group), false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = go.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderers.Add(renderer);
            }
            Rig(groups, pivots);
            // Feet on the floor and the requested height (measured on the rest pose).
            float height = Mathf.Max(.05f, max.y - min.y);
            _modelScale = _height / height;
            _model.localScale = Vector3.one * _modelScale;
            _basePos = new Vector3(0f, -min.y * _modelScale, 0f);
            _model.localPosition = _basePos;
            HandToHealth(parent, renderers);
            _built = true;
        }
        catch (System.Exception e)
        {
            _failed = true;
            Plugin.Log.LogError($"[Monsters] Visual {_art} failed: {e.Message}");
        }
    }

    // Joint hierarchy and effect anchors. Groups are built flat under the body; snake chains, the jaw and the
    // basilisk's head are re-hung here so a turn carries everything further along (see tools/build_monsters.py).
    private void Rig(Dictionary<string, Transform> groups, Dictionary<string, Vector3> pivots)
    {
        Transform Find(string name) => groups.TryGetValue(name, out var g) ? g : null;
        void Hang(string child, string parent)
        {
            var c = Find(child); var p = Find(parent);
            if (c != null && p != null) c.SetParent(p, true);
        }
        int tails = 0;
        while (groups.ContainsKey("tail" + tails)) tails++;
        if (_kind == MonsterKind.Basilisk)
        {
            for (int i = BasiliskRoot + 1; i < tails; i++) Hang("tail" + i, "tail" + (i - 1));
            for (int i = BasiliskRoot - 1; i >= 0; i--) Hang("tail" + i, "tail" + (i + 1));
            Hang("head", "tail0");
        }
        else
            for (int i = 1; i < tails; i++) Hang("tail" + i, "tail" + (i - 1));
        Hang("jaw", "head");

        foreach (var pair in groups)
        {
            if (pair.Key == "body") continue;
            var joint = new Joint { T = pair.Value, Rest = pair.Value.localRotation };
            _joints[pair.Key] = joint;
            _all.Add(joint);
            string name = pair.Key;
            if (name.StartsWith("wing")) _wings.Add((joint, name.EndsWith("_l")));
            else if (name.StartsWith("leg")) _legs.Add((joint, name.Length > 3 && name[3] == '-', !name.Contains("-0.")));
        }
        _tail = new Joint[tails];
        for (int i = 0; i < tails; i++) _tail[i] = J("tail" + i);
        _head = J("head"); _jaw = J("jaw"); _armL = J("arm_l"); _armR = J("arm_r");
        _nagaArms[0] = J("arm-10"); _nagaArms[1] = J("arm-11"); _nagaArms[2] = J("arm10"); _nagaArms[3] = J("arm11");
        string[] twinParts = { "head_", "arm_{0}_in", "arm_{0}_out", "foot_{0}_l", "foot_{0}_r" };
        for (int child = 0; child < 2; child++)
            for (int part = 0; part < twinParts.Length; part++)
                _twins[child * 5 + part] = J(part == 0 ? twinParts[0] + (child == 0 ? "h" : "g") : string.Format(twinParts[part], child == 0 ? "h" : "g"));

        Transform Anchor(string pivot, string group)
        {
            var g = Find(group);
            if (g == null || !pivots.TryGetValue(pivot, out var at) || !pivots.TryGetValue(group, out var origin)) return null;
            var anchor = new GameObject(pivot).transform;
            anchor.SetParent(g, false);
            anchor.localPosition = at - origin;
            return anchor;
        }
        // Glowing eyes: the basilisk's always burn green; the gorgon's flare for the Death Stare, the naga's for
        // the petrifying gaze.
        (_eyeColor, _eyeSize) = _kind switch
        {
            MonsterKind.Basilisk => (new Color(.3f, 1f, .2f), .045f),
            MonsterKind.Gorgon => (new Color(1f, .2f, .05f), .05f),
            MonsterKind.Naga => (new Color(1f, .85f, .2f), .012f),
            _ => (Color.clear, 0f),
        };
        if (_eyeSize > 0f)
        {
            _eyeMaterial = new Material(MonsterFx.Additive()) { name = "IHM " + _art + " eyes", color = _eyeColor };
            _materials.Add(_eyeMaterial);
            foreach (string eye in new[] { "eye_l", "eye_r" })
            {
                var anchor = Anchor(eye, "head");
                if (anchor != null) _eyeOrbs.Add(Orb(anchor, _eyeMaterial, "Eye glow"));
            }
        }
        if (_kind is not (MonsterKind.Witch or MonsterKind.Fairy)) return;
        // Spell hands: the witch's flame and sparks, the fairy's wand glow and the thread to the thing she carries.
        _handL = Anchor("hand_l", "arm_l"); _handR = Anchor("hand_r", "arm_r");
        if (_handL == null || _handR == null) return;
        _orbMaterial = new Material(MonsterFx.Additive()) { name = "IHM " + _art + " spell" };
        _materials.Add(_orbMaterial);
        _orbL = Orb(_handL, _orbMaterial, "Spark"); _orbR = Orb(_handR, _orbMaterial, "Spell");
        _spark = new GameObject("Hand arc").AddComponent<LineRenderer>();
        _spark.transform.SetParent(transform, false);
        _spark.useWorldSpace = true; _spark.positionCount = SparkPoints; _spark.sharedMaterial = MonsterFx.Additive();
        _spark.startWidth = .035f; _spark.endWidth = .035f;
        bool fairy = _kind == MonsterKind.Fairy;
        _spark.startColor = fairy ? new Color(1f, .9f, .45f) : new Color(.74f, .62f, 1f); _spark.endColor = fairy ? new Color(1f, .7f, .9f) : new Color(.5f, .85f, 1f);
        _spark.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _spark.receiveShadows = false;
        _spark.enabled = false;
    }

    // The native hurt flash tints EnemyHealth's instanced materials; register ours (its Awake ran before we built).
    private void HandToHealth(EnemyParent parent, List<MeshRenderer> renderers)
    {
        var health = parent != null ? parent.GetComponentInChildren<EnemyHealth>(true) : null;
        if (health == null) return;
        health.renderers ??= new List<MeshRenderer>();
        foreach (var r in renderers)
        {
            health.renderers.Add(r);
            var instance = r.material; // instanced per monster so the flash is local to it
            _materials.Add(instance);
            if (instance.HasProperty("_ColorOverlay")) instance.SetColor("_ColorOverlay", Color.red);
            health.instancedMaterials.Add(instance);
        }
    }

    internal void Burst() => _pop = 0f;

    /// <summary>The bull's close-range head toss (called on every client with its effect).</summary>
    internal void Toss() => _tossAt = Time.time;

    private LineRenderer _ring;

    // Hansel and Gretel: while they lure, rush or reach, a ring on the floor shows exactly how far their grab
    // reaches (MonsterRules.TwinsReach, measured from their feet like the host's check). Brighter during the reach.
    private void ReachRing()
    {
        if (_kind != MonsterKind.Twins) return;
        var anim = _brain.Anim;
        bool show = !_brain.Dead && anim is MonsterAnim.TwinsLure or MonsterAnim.TwinsRush or MonsterAnim.TwinsReach;
        if (!show) { if (_ring != null) _ring.enabled = false; return; }
        if (_ring == null)
        {
            _ring = new GameObject("Twins grab range").AddComponent<LineRenderer>();
            _ring.transform.SetParent(transform, false);
            _ring.useWorldSpace = true;
            _ring.loop = true;
            _ring.positionCount = 48;
            _ring.sharedMaterial = MonsterFx.Additive();
            _ring.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _ring.receiveShadows = false;
        }
        bool reach = anim == MonsterAnim.TwinsReach;
        var color = reach ? new Color(1f, .25f, .2f, .85f + .15f * Mathf.Sin(Time.time * 18f)) : new Color(1f, .55f, .3f, .4f);
        _ring.startColor = _ring.endColor = color;
        _ring.startWidth = _ring.endWidth = reach ? .06f : .035f;
        _ring.enabled = true;
        var c = transform.position + Vector3.up * .04f;
        for (int i = 0; i < 48; i++)
        {
            float a = i / 48f * Mathf.PI * 2f;
            _ring.SetPosition(i, c + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * MonsterRules.TwinsReach);
        }
    }

    /// <summary>Where the monster's eyes are in the world (the model's eye orbs, else its head, else near the top).</summary>
    internal Vector3 EyePoint
    {
        get
        {
            if (_built && _eyeOrbs.Count > 0)
            {
                var sum = Vector3.zero; int n = 0;
                foreach (var e in _eyeOrbs) if (e != null) { sum += e.position; n++; }
                if (n > 0) return sum / n;
            }
            if (_built && _head != null && _head.T != null) return _head.T.position;
            return transform.position + Vector3.up * (_height * .8f);
        }
    }

    /// <summary>A short red flash on every material of this monster (called on every client when it is hurt).</summary>
    internal void Flash()
    {
        _flash = 1f;
        _hitAt = Time.time;
        _hitSide = Random.value < .5f ? -1f : 1f;
        if (_tints.Count > 0) return;
        foreach (var m in _materials)
        {
            if (m == null) continue;
            if (m.HasProperty("_Color")) _tints.Add((m, "_Color", m.GetColor("_Color")));
            else if (m.HasProperty("_BaseColor")) _tints.Add((m, "_BaseColor", m.GetColor("_BaseColor")));
        }
    }

    private void TickFlash()
    {
        if (_tints.Count == 0) return;
        _flash = Mathf.MoveTowards(_flash, 0f, Time.deltaTime * 4f);
        var hurt = new Color(1f, .15f, .12f);
        foreach (var (m, property, color) in _tints)
            if (m != null) m.SetColor(property, Color.Lerp(color, hurt, _flash * .85f));
        if (_flash <= 0f) _tints.Clear(); // colours restored exactly; recaptured on the next hit
    }

    // Every client: footsteps / slithering / wing beats while it moves, and an occasional call when the local
    // player is close, so players can hear a monster coming. Volumes stay under the attack warnings.
    private void Sounds(float speed)
    {
        if (_brain.Dead) return;
        var at = transform.position + Vector3.up * .3f;
        bool landed = _kind == MonsterKind.Pig && _brain.Extra >= 100f;
        float rate; MonsterAudio.Cue cue; float volume; float pitch = 1f;
        switch (_kind)
        {
            case MonsterKind.Pig when !landed: rate = 3.5f; cue = MonsterAudio.Cue.Flap; volume = .22f; pitch = 1.2f; break;
            case MonsterKind.Witch: rate = speed > .4f ? .7f : 0f; cue = MonsterAudio.Cue.Whoosh; volume = .3f; break;
            case MonsterKind.Basilisk: rate = speed > .3f ? 1.3f : 0f; cue = MonsterAudio.Cue.Slither; volume = .2f; break;
            case MonsterKind.Gorgon:
                // A continuous gait: 1.2 s stretches of recorded hoof clatter, started back to back (slightly overlapping)
                // instead of single chopped steps; faster and a little higher while it charges.
                rate = speed > .3f ? Mathf.Clamp(speed * .33f, .85f, 1.7f) : 0f; cue = MonsterAudio.Cue.Gait; volume = .8f;
                pitch = speed > 4.5f ? 1f : .88f; break; // heavy, unhurried steps; a drumroll only in the charge
            case MonsterKind.Naga: rate = speed > .3f ? 1.5f : 0f; cue = MonsterAudio.Cue.Slither; volume = .35f; pitch = .8f; break;
            case MonsterKind.Fairy: rate = .5f; cue = MonsterAudio.Cue.FairyFlight; volume = .45f; break; // 2.4 s wing recording every 2 s: a seamless flutter
            case MonsterKind.Twins: rate = speed > .3f ? Mathf.Clamp(speed * 1.3f, 1.6f, 4.5f) : 0f; cue = MonsterAudio.Cue.TwinsStep; volume = .55f; break; // recorded children's steps
            default: rate = speed > .3f ? Mathf.Clamp(speed * 1.8f, 2f, 6f) : 0f; cue = MonsterAudio.Cue.Step; volume = .3f; break;
        }
        if (rate > 0f)
        {
            _stepClock += Time.deltaTime * rate;
            if (_stepClock >= 1f) { _stepClock -= 1f; MonsterAudio.Play(cue, at, volume, pitch * Random.Range(.94f, 1.06f)); }
        }
        else _stepClock = 0f;

        // Calls: a soft "neutral" sound while it wanders, and a different, louder and more frequent one while it
        // hunts someone (the host's synced target), so players can tell the two apart by ear.
        var me = PlayerController.instance != null ? PlayerController.instance.playerAvatarScript : null;
        bool hunting = _brain.TargetId != 0;
        // Aggro: the hunting call plays the moment it picks a target, not when the wander timer next runs out.
        if (hunting && !_wasHunting) _callAt = 0f;
        _wasHunting = hunting;
        if (me == null || Time.time < _callAt) return;
        _callAt = Time.time + (hunting ? Random.Range(3f, 5.5f) : Random.Range(6f, 11f));
        if (Vector3.Distance(me.transform.position, transform.position) > (hunting ? 18f : 14f)) return;
        if (!hunting)
            switch (_kind)
            {
                case MonsterKind.Pig: MonsterAudio.Play(MonsterAudio.Cue.Oink, at, .6f); break;
                case MonsterKind.Witch: MonsterAudio.Play(MonsterAudio.Cue.Whoosh, at, .35f, .8f); break; // humming broom
                case MonsterKind.Basilisk: MonsterAudio.Play(MonsterAudio.Cue.Hiss, at, .25f, 1.25f); break;
                case MonsterKind.Gorgon: MonsterAudio.Play(MonsterAudio.Cue.Snort, at, .45f, Random.Range(.9f, 1.05f)); break; // idle snort
                case MonsterKind.Naga: MonsterAudio.Play(MonsterAudio.Cue.Slither, at, .4f, .7f); break;
                case MonsterKind.Fairy: MonsterAudio.Play(MonsterAudio.Cue.Giggle, at, .2f, 1f); break;
                case MonsterKind.Twins: MonsterAudio.Play(MonsterAudio.Cue.Giggle, at, .3f, 1.1f); break;
            }
        else
            switch (_kind)
            {
                case MonsterKind.Pig: MonsterAudio.Play(MonsterAudio.Cue.Oink, at, .8f, 1.2f); break;
                case MonsterKind.Witch:
                    if (_brain is not WitchBrain witch || !witch.TryLaugh(.55f)) MonsterAudio.Play(MonsterAudio.Cue.Whoosh, at, .5f, .7f);
                    break;
                case MonsterKind.Basilisk: MonsterAudio.Play(MonsterAudio.Cue.Hiss, at, .5f, .9f); break;
                case MonsterKind.Gorgon: MonsterAudio.Play(MonsterAudio.Cue.Moo, at, .8f, Random.Range(.85f, .95f)); break;
                case MonsterKind.Naga: MonsterAudio.Play(MonsterAudio.Cue.Chitter, at, .75f, .9f); break;
                case MonsterKind.Fairy: MonsterAudio.Play(MonsterAudio.Cue.Giggle, at, .3f, .9f); break;
                case MonsterKind.Twins: MonsterAudio.Play(MonsterAudio.Cue.Giggle, at, .55f, .8f); break; // lower, creepier
            }
    }

    /// <summary>Where a spell leaves the monster: the witch's hands while she casts, else the chest.</summary>
    internal Vector3 CastPoint
    {
        get
        {
            if (_built && _kind == MonsterKind.Witch && _handL != null && _handR != null)
            {
                if (_volt > .01f) return (_handL.position + _handR.position) * .5f;
                if (_fire > .01f) return _handR.position;
            }
            return transform.position + Vector3.up * (_height * .6f);
        }
    }

    private void LateUpdate()
    {
        if (!_built || _body == null || _brain == null) return;
        // Follow the synced physics body: feet at the bottom of its colliders, facing its yaw.
        float bottom = _body.position.y;
        foreach (var c in _colliders)
            if (c != null && c.enabled && !c.isTrigger) bottom = Mathf.Min(bottom, c.bounds.min.y);
        var pos = new Vector3(_body.position.x, bottom, _body.position.z);
        float yaw = _body.rotation.eulerAngles.y;
        transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
        float speed = Vector3.Distance(Flat(pos), Flat(_lastPos)) / Mathf.Max(Time.deltaTime, .001f);
        _lastPos = pos;
        // Turn rate (degrees a second, smoothed): flyers bank into a turn.
        float turn = Mathf.Clamp(Mathf.DeltaAngle(_lastYaw, yaw) / Mathf.Max(Time.deltaTime, .001f), -240f, 240f);
        _lastYaw = yaw;
        _turn = Mathf.Lerp(_turn, turn, 1f - Mathf.Exp(-6f * Time.deltaTime));
        _phase += Time.deltaTime * Mathf.Lerp(2f, 9f, Mathf.Clamp01(speed / 4f));
        float scale = _kind == MonsterKind.Pig ? MonsterRules.PigScale(Mathf.RoundToInt(_brain.Extra % 100f)) : 1f;
        _pop = Mathf.MoveTowards(_pop, 1f, Time.deltaTime * 4f);
        float flinch = MonsterAnimRules.Flinch(Time.time - _hitAt);
        transform.localScale = Vector3.one * scale * Mathf.Lerp(.85f, 1f, _pop) * (1f - .06f * flinch);
        Animate(speed);
        ChargeDread();
        GoldenNose();
        TickFlash();
        Sounds(speed);
        ReachRing();
    }

    private static Vector3 Flat(Vector3 v) => new(v.x, 0f, v.z);

    /// <summary>
    /// The player a bull or the twins are coming for gets a "here it comes" screen, in the spirit of the Thin Man's
    /// but its own so the two never get mixed up (built from the same native overrides: vignette, camera zoom,
    /// colour, camera shake). Only on that player's own screen, from the wind-up to the hit.
    ///  - Bull (roar and charge): a dark red rim that closes in, a tunnel-vision zoom, harsher contrast and a thud
    ///    of camera shake on each hoofbeat instead of a steady tremble.
    ///  - Twins (lure, rush, reach): a sickly violet rim, washed-out colour and a slow woozy zoom that drifts in
    ///    and out like a carousel; no shake until they actually rush.
    /// </summary>
    private void ChargeDread()
    {
        if (_brain.Dead || _brain.TargetId == 0 || _kind is not (MonsterKind.Gorgon or MonsterKind.Twins)) return;
        var me = PlayerController.instance != null ? PlayerController.instance.playerAvatarScript : null;
        if (me == null || me.photonView == null || me.photonView.ViewID != _brain.TargetId || me.isDisabled) return;
        var anim = _brain.Anim;
        bool bull = _kind == MonsterKind.Gorgon;
        bool coming = bull ? anim is MonsterAnim.Roar or MonsterAnim.Charge
                           : anim is MonsterAnim.TwinsLure or MonsterAnim.TwinsRush or MonsterAnim.TwinsReach;
        if (!coming || _stunned) return;
        try
        {
            var post = PostProcessing.Instance;
            float t = Time.time;
            if (bull)
            {
                bool charging = anim == MonsterAnim.Charge;
                post?.VignetteOverride(new Color(.32f, .03f, .02f), charging ? .55f : .4f, .9f, 4f, 1.2f, .1f, gameObject);
                post?.ContrastOverride(charging ? 18f : 10f, 4f, 1.2f, .1f, gameObject);
                post?.SaturationOverride(-8f, 4f, 1.2f, .1f, gameObject);
                CameraZoom.Instance?.OverrideZoomSet(charging ? 58f : 64f, .1f, 2.5f, 1.2f, gameObject, 140);
                // A thump per hoofbeat (the gallop in AnimateGorgon), not a constant tremble.
                float beat = Mathf.Pow(Mathf.Abs(Mathf.Sin(_phase * 3f)), 8f);
                if (charging && beat > .5f) GameDirector.instance?.CameraImpact?.Shake(4f * Time.deltaTime, .08f);
            }
            else
            {
                bool rushing = anim != MonsterAnim.TwinsLure;
                post?.VignetteOverride(new Color(.24f, .1f, .3f), rushing ? .45f : .33f, .7f, 2f, 1f, .1f, gameObject);
                post?.SaturationOverride(rushing ? -45f : -30f, 2f, 1f, .1f, gameObject);
                CameraZoom.Instance?.OverrideZoomSet(74f + Mathf.Sin(t * 1.7f) * 6f, .1f, 1.5f, 1f, gameObject, 140);
                if (rushing) GameDirector.instance?.CameraShake?.Shake(6f * Time.deltaTime, .5f);
            }
        }
        catch (System.Exception e) { if (!_dreadFailed) { _dreadFailed = true; Plugin.Log.LogWarning($"[Monsters] Charge screen effect failed: {e.Message}"); } }
    }

    private bool _dreadFailed;

    // ------------------------------------------------------------------ animation

    private static float Smooth(float u) { u = Mathf.Clamp01(u); return u * u * (3f - 2f * u); }
    // Unclamped, so a strike's overshoot swings a little past the end pose.
    private static float Mix(float a, float b, float w) => Mathf.LerpUnclamped(a, b, w);
    private static void Rot(Joint j, float x, float y, float z, float k = 16f) { if (j != null) { j.Target = Quaternion.Euler(x, y, z); j.K = k; } }
    private static void Rot(Joint j, Quaternion target, float k) { if (j != null) { j.Target = target; j.K = k; } }
    private Joint J(string name) => _joints.TryGetValue(name, out var j) ? j : null;

    /// <summary>
    /// Procedural joint animation. Every frame each joint gets a target (rest by default) and eases to it, so a
    /// state change blends instead of snapping; strikes use a stiff ease to read as a hit. The brain's
    /// <see cref="MonsterAnim"/> picks the move and its StateTime drives windup and strike inside it.
    /// Angles: +X pitches a forward part down (and swings a hanging limb back), +Y turns clockwise seen from
    /// above, +Z rolls the right side up.
    /// </summary>
    private void Animate(float speed)
    {
        float dt = Time.deltaTime, t = Time.time, st = _brain.StateTime;
        var pose = _brain.Pose;
        bool stunned = pose == MonsterPose.Stunned || (_brain.Enemy != null && _brain.Enemy.IsStunned());
        bool held = _brain.Held;
        var anim = MonsterAnimRules.AttackAnimShown(_brain.Dead, stunned, held) ? _brain.Anim : MonsterAnim.None;
        float move = Mathf.Clamp01(speed / 3f);
        _move = move; _stunned = stunned;
        _held = Mathf.MoveTowards(_held, MonsterAnimRules.Struggle(_brain.Dead, stunned, held), dt * 5f);
        if (stunned && !_wasStunned) { _hitAt = t; _hitSide = Random.value < .5f ? -1f : 1f; } // the stun starts with a jolt
        _wasStunned = stunned;
        if (anim != _anim) { _animWas = _anim; _anim = anim; _animAt = t; }
        if (stunned || held || _brain.Dead) _animWas = MonsterAnim.None; // a reaction ends any follow-through
        foreach (var j in _all) { j.Target = Quaternion.identity; j.K = 16f; }
        _tilt = Quaternion.identity; _shift = Vector3.zero; _eye = 0f; _fire = 0f; _volt = 0f; _lightPower = 0f; _tethered = false;
        switch (_kind)
        {
            case MonsterKind.Pig: AnimatePig(anim, pose, t, st, move); break;
            case MonsterKind.Witch: AnimateWitch(anim, t, st, move, stunned); break;
            case MonsterKind.Basilisk: AnimateBasilisk(anim, pose, t, st, stunned); break;
            case MonsterKind.Gorgon: AnimateGorgon(anim, t, st, move, stunned); break;
            case MonsterKind.Naga: AnimateNaga(anim, t, st, move, stunned); break;
            case MonsterKind.Fairy: AnimateFairy(anim, pose, t, st, move, stunned); break;
            case MonsterKind.Twins: AnimateTwins(anim, t, st, move, stunned); break;
        }
        if (!stunned && !_brain.Dead && MonsterAnimRules.Gait(_kind, _brain.Extra >= 100f) == MonsterGait.Fly)
            _tilt = Quaternion.Euler(0f, 0f, Mathf.Clamp(-_turn * .12f, -18f, 18f)) * _tilt; // bank into the turn
        if (stunned)
        {
            _tilt = Quaternion.Euler(Mathf.Sin(t * 9f) * 6f, 0f, Mathf.Sin(t * 7f) * 10f) * _tilt;
            // Dazed: the head (both heads of the twins) rolls in a slow circle.
            Add(_head, Mathf.Cos(t * 3.1f) * 8f, 0f, Mathf.Sin(t * 3.1f) * 12f);
            Add(_twins[0], Mathf.Cos(t * 3.1f) * 8f, 0f, Mathf.Sin(t * 3.1f) * 12f);
            Add(_twins[5], Mathf.Cos(t * 3.1f + 1.3f) * 8f, 0f, Mathf.Sin(t * 3.1f + 1.3f) * 12f);
        }
        if (_held > .01f) Struggle(_held, t);
        float hit = MonsterAnimRules.Flinch(t - _hitAt);
        if (hit > .01f) Flinch(hit);
        foreach (var j in _all)
            j.T.localRotation = Quaternion.Slerp(j.T.localRotation, j.Rest * j.Target, 1f - Mathf.Exp(-j.K * dt));
        float ease = 1f - Mathf.Exp(-12f * dt);
        _model.localRotation = Quaternion.Slerp(_model.localRotation, _tilt, ease);
        _model.localPosition = Vector3.Lerp(_model.localPosition, _basePos + _shift + Vector3.up * _floorLift, ease);
        GuardFloor(dt);
        Effects(t);
        Stars(t);
    }

    // Snakes keep their head out of the floor: if a pose or a slope would sink it, the model rises a few centimetres
    // until it clears, and settles back once there is room.
    private void GuardFloor(float dt)
    {
        if (_kind is not (MonsterKind.Basilisk or MonsterKind.Naga) || _head == null) { _floorLift = 0f; return; }
        float floor = transform.position.y;
        if (Physics.Raycast(_head.T.position + Vector3.up * .5f, Vector3.down, out var hit, 1.2f, SemiFunc.LayerMaskGetVisionObstruct(), QueryTriggerInteraction.Ignore) &&
            !hit.collider.transform.IsChildOf(_brain.transform.root))
            floor = Mathf.Max(floor, hit.point.y);
        float clearance = _head.T.position.y - floor;
        const float want = .07f;
        if (clearance < want) _floorLift = Mathf.Min(.3f, _floorLift + Mathf.Min(want - clearance, dt * 1.5f));
        else if (clearance > want + .06f) _floorLift = Mathf.MoveTowards(_floorLift, 0f, dt * .3f);
    }

    // Stunned: three little stars spin around above the head (cartoon daze), always turned to the camera.
    private void Stars(float t)
    {
        bool on = _stunned && !_brain.Dead && !_brain.Held; // not while a player holds it (the grab keeps it stunned)
        if (!on) { if (_stars != null && _stars.gameObject.activeSelf) _stars.gameObject.SetActive(false); return; }
        if (_stars == null) BuildStars();
        if (!_stars.gameObject.activeSelf) _stars.gameObject.SetActive(true);
        var headAt = _head != null ? _head.T.position : transform.position + Vector3.up * _height * .9f;
        if (_kind == MonsterKind.Twins && _twins[0] != null && _twins[5] != null) headAt = (_twins[0].T.position + _twins[5].T.position) * .5f;
        float radius = Mathf.Clamp(_height * .18f, .14f, .4f);
        _stars.position = headAt + Vector3.up * (radius * 1.3f + .12f);
        var cam = Camera.main != null ? Camera.main.transform : null;
        for (int i = 0; i < _starList.Count; i++)
        {
            float a = t * 3.2f + i * Mathf.PI * 2f / _starList.Count;
            var star = _starList[i];
            star.position = _stars.position + new Vector3(Mathf.Cos(a), Mathf.Sin(t * 4f + i) * .12f, Mathf.Sin(a)) * radius;
            if (cam != null) star.rotation = Quaternion.LookRotation(star.position - cam.position) * Quaternion.Euler(0f, 0f, t * 140f + i * 40f);
            star.localScale = Vector3.one * (radius * .45f * (.85f + .15f * Mathf.Sin(t * 9f + i * 2f)));
        }
    }

    private void BuildStars()
    {
        _stars = new GameObject("Stun stars").transform;
        _stars.SetParent(transform, false);
        var mesh = new Mesh { name = "IHM stun star" };
        var v = new List<Vector3> { Vector3.zero };
        for (int i = 0; i < 10; i++)
        {
            float a = Mathf.PI / 2f + i * Mathf.PI / 5f, r = i % 2 == 0 ? .5f : .21f;
            v.Add(new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, 0f));
        }
        var tris = new List<int>();
        for (int i = 0; i < 10; i++)
        {
            int a = 1 + i, b = 1 + (i + 1) % 10;
            tris.AddRange(new[] { 0, a, b, 0, b, a }); // both sides
        }
        mesh.SetVertices(v); mesh.SetTriangles(tris, 0); mesh.RecalculateBounds();
        _meshes.Add(mesh);
        var material = new Material(MonsterFx.Additive()) { name = "IHM stun star", color = new Color(1f, .86f, .25f, .95f) };
        _materials.Add(material);
        for (int i = 0; i < 3; i++)
        {
            var go = new GameObject("Star " + i);
            go.transform.SetParent(_stars, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            _starList.Add(go.transform);
        }
        _stars.gameObject.SetActive(false);
    }

    // Adds a rotation on top of whatever the state animation asked for (reactions layer over the pose).
    private static void Add(Joint j, float x, float y, float z, float k = 0f)
    {
        if (j == null) return;
        j.Target = Quaternion.Euler(x, y, z) * j.Target;
        if (k > j.K) j.K = k;
    }

    /// <summary>
    /// In a player's grip: the whole body thrashes and every limb the monster has fights the hold - legs kick,
    /// wings beat out of rhythm, arms flail, snakes whip. <paramref name="w"/> is 1 awake, weaker while stunned.
    /// </summary>
    private void Struggle(float w, float t)
    {
        _tilt = Quaternion.Euler(Mathf.Sin(t * 13f) * 9f * w, Mathf.Sin(t * 8.3f) * 10f * w, Mathf.Sin(t * 16.7f) * 11f * w) * _tilt;
        _shift += new Vector3(Mathf.Sin(t * 21f) * .02f, Mathf.Abs(Mathf.Sin(t * 10f)) * .02f, 0f) * w;
        Add(_head, Mathf.Sin(t * 19f) * 12f * w, Mathf.Sin(t * 11f) * 18f * w, 0f, 30f);
        foreach (var (wing, left) in _wings)
            Add(wing, 0f, 0f, (left ? 1f : -1f) * Mathf.Sin(t * 47f + (left ? 0f : 1.1f)) * 24f * w, 80f);
        foreach (var (leg, left, front) in _legs)
            Add(leg, Mathf.Sin(t * 24f + ((left ^ front) ? 0f : Mathf.PI)) * 40f * w, 0f, 0f, 40f);
        switch (_kind)
        {
            case MonsterKind.Witch:
            case MonsterKind.Fairy:
                Add(_armL, -55f * w + Mathf.Sin(t * 18f) * 45f * w, 0f, -25f * w, 30f);
                Add(_armR, -55f * w - Mathf.Sin(t * 18f) * 45f * w, 0f, 25f * w, 30f);
                break;
            case MonsterKind.Basilisk:
                // Coiled in the hand (AnimateBasilisk): the ring tightens and loosens instead of whipping open.
                for (int i = 0; i < _tail.Length; i++)
                    if (i != BasiliskRoot) Add(_tail[i], Mathf.Sin(t * 7f - i * .5f) * 4f * w, Mathf.Sin(t * 9f) * 3f * w, 0f);
                Add(_jaw, (8f + 8f * Mathf.Sin(t * 23f)) * w, 0f, 0f, 30f);
                break;
            case MonsterKind.Gorgon:
                Add(_jaw, (14f + 8f * Mathf.Sin(t * 17f)) * w, 0f, 0f, 30f);
                break;
            case MonsterKind.Naga:
                for (int i = 0; i < 4; i++)
                {
                    float side = i < 2 ? 1f : -1f; // left pair, right pair
                    Add(_nagaArms[i], -60f * w + Mathf.Sin(t * 15f + i * 1.7f) * 32f * w, 0f, side * 18f * w, 30f);
                }
                for (int i = 0; i < _tail.Length; i++) Add(_tail[i], 0f, Mathf.Sin(t * 11f - i * .6f) * 12f * w, 0f, 20f);
                break;
            case MonsterKind.Twins:
                for (int child = 0; child < 2; child++)
                {
                    int o = child * 5;
                    float kick = Mathf.Sin(t * 20f + child * Mathf.PI) * 38f * w;
                    Add(_twins[o], -10f * w, Mathf.Sin(t * 13f + child * 2f) * 20f * w, 0f, 24f);
                    Add(_twins[o + 2], -100f * w + Mathf.Sin(t * 18f + child * Mathf.PI) * 40f * w, 0f, 0f, 30f);
                    Add(_twins[o + 3], kick, 0f, 0f, 40f);
                    Add(_twins[o + 4], -kick, 0f, 0f, 40f);
                }
                break;
        }
    }

    /// <summary>
    /// Hurt (and the first instant of a stun): the body is knocked back and to one side, the head snaps away
    /// and the limbs jerk - wings flare, arms come up to guard, jaws gape. <paramref name="f"/> runs 1 -> 0
    /// over <see cref="MonsterAnimRules.FlinchSeconds"/>; the red flash is separate (<see cref="Flash"/>).
    /// </summary>
    private void Flinch(float f)
    {
        _tilt = Quaternion.Euler(-13f * f, 0f, _hitSide * 8f * f) * _tilt;
        _shift += Vector3.back * (.07f * f);
        Add(_head, -20f * f, _hitSide * 14f * f, 0f, 45f);
        foreach (var (wing, left) in _wings) Add(wing, 0f, 0f, (left ? -1f : 1f) * 30f * f, 80f); // flare up
        switch (_kind)
        {
            case MonsterKind.Pig:
            case MonsterKind.Gorgon:
                foreach (var (leg, _, front) in _legs) Add(leg, (front ? -16f : 12f) * f, 0f, 0f, 40f); // braces
                Add(_jaw, 16f * f, 0f, 0f, 40f);
                break;
            case MonsterKind.Witch:
            case MonsterKind.Fairy:
                Add(_armL, -35f * f, 0f, -20f * f, 40f);
                Add(_armR, -35f * f, 0f, 20f * f, 40f);
                break;
            case MonsterKind.Basilisk:
                for (int i = 0; i < BasiliskRoot && i < _tail.Length; i++) Add(_tail[i], -7f * f, _hitSide * 5f * f, 0f, 40f); // the neck recoils
                Add(_jaw, 28f * f, 0f, 0f, 40f);
                break;
            case MonsterKind.Naga:
                for (int i = 0; i < 4; i++) Add(_nagaArms[i], -38f * f, (i < 2 ? 1f : -1f) * 24f * f, 0f, 40f); // blades cross in a guard
                for (int i = Mathf.Max(0, _tail.Length - 4); i < _tail.Length; i++) Add(_tail[i], 0f, _hitSide * 12f * f, 0f, 30f);
                break;
            case MonsterKind.Twins:
                for (int child = 0; child < 2; child++)
                {
                    Add(_twins[child * 5], -22f * f, _hitSide * 10f * f, 0f, 40f);
                    Add(_twins[child * 5 + 2], -30f * f, 0f, 0f, 40f);
                }
                break;
        }
    }

    // Golden Pig: beating wings and tucked legs in flight; chomping nods while eating; on landing the wings fold
    // away and it runs on its legs.
    private void AnimatePig(MonsterAnim anim, MonsterPose pose, float t, float st, float move)
    {
        bool grounded = _brain.Extra >= 100f || anim == MonsterAnim.Land;
        _fold = Mathf.MoveTowards(_fold, grounded ? 1f : 0f, Time.deltaTime * (grounded ? 1.4f : 3f));
        float rate = pose == MonsterPose.Flee ? 22f : anim == MonsterAnim.Eat ? 9f : 14f;
        float flap = _stunned ? 48f + Mathf.Sin(t * 5f) * 4f : Mathf.Sin(t * rate) * 35f - 8f; // positive = wing down; limp while stunned
        foreach (var (wing, left) in _wings)
        {
            bool show = _fold < .995f;
            if (wing.T.gameObject.activeSelf != show) wing.T.gameObject.SetActive(show);
            wing.T.localScale = Vector3.one * (1f - .9f * _fold * _fold);
            Rot(wing, 0f, 0f, (left ? 1f : -1f) * Mathf.Lerp(flap, 80f, _fold), 60f);
        }
        bool flying = _fold < .5f;
        foreach (var (leg, left, front) in _legs)
        {
            float phase = (left ^ front) ? 0f : Mathf.PI;
            if (_stunned) Rot(leg, (front ? -18f : 18f) + Mathf.Sin(t * 6f + phase) * 5f, 0f, left ? -12f : 12f); // splayed, twitching
            else if (flying) Rot(leg, (front ? -30f : 30f) + Mathf.Sin(t * 3f + phase) * 8f, 0f, 0f);
            else Rot(leg, Mathf.Sin(_phase * 2f + phase) * 40f * move, 0f, 0f, 30f);
        }
        float nod = Mathf.Sin(t * 2f) * 4f;
        if (anim == MonsterAnim.Eat)
        {
            nod = 24f + 20f * Mathf.Sin(t * 17f); // chomp, chomp
            _tilt = Quaternion.Euler(14f, 0f, 0f);
            _shift = Vector3.down * (.03f * Mathf.Abs(Mathf.Sin(t * 17f)));
        }
        else if (anim == MonsterAnim.Land)
        {
            float u = Mathf.Clamp01(st / 1.2f);
            nod = -12f * (1f - u);
            _tilt = Quaternion.Euler(-14f * (1f - u), 0f, Mathf.Sin(t * 18f) * 7f * (1f - u)); // wobbles down, settles
        }
        else if (_stunned) nod = 22f;
        else if (flying)
        {
            _tilt = Quaternion.Euler(Mathf.Sin(t * 1.7f) * 3f + 7f * move, 0f, Mathf.Sin(t * 1.3f) * 4f);
            _shift = Vector3.up * (Mathf.Sin(t * 2.4f) * .04f);
        }
        else
        {
            // On the ground it waddles: the body rocks over each step and bounces a little.
            _tilt = Quaternion.Euler(0f, 0f, Mathf.Sin(_phase * 2f) * 6f * move);
            _shift = Vector3.up * (Mathf.Abs(Mathf.Sin(_phase * 2f)) * .025f * move);
        }
        Rot(_head, nod, 0f, 0f, 22f);
    }

    // Witch: one hand on the broom. Fireball - the other palm lifts with a growing flame (1.2 s), winds back and
    // throws. Electric hold - both arms rise overhead with sparks between the hands, then thrust at the victim.
    private void AnimateWitch(MonsterAnim anim, float t, float st, float move, bool stunned)
    {
        float sway = Mathf.Sin(t * 1.6f);
        // The mesh uses Blender +Y forward, Unity +Z forward: Blender +X arm pitch
        // becomes Unity -X. Keep the broom hand in the same pose as the art preview.
        // Flying: she leans over the broom the faster she goes, the broom hand pulls in and the free arm trails.
        float leftX = -38f - 10f * move, leftZ = 0f, rightX = -14f + sway * 4f + 22f * move, rightZ = 0f, head = sway * 2f - 9f * move, k = 16f;
        _tilt = Quaternion.Euler(13f * move, 0f, sway * 2f);
        _shift = Vector3.up * (Mathf.Sin(t * 2.4f) * .04f);
        switch (anim)
        {
            case MonsterAnim.FirePrep:
            {
                // The palm lifts, cradles the growing flame in a slow circle, and in the last moments the arm winds
                // back behind the shoulder as she leans away. (Rest geometry already has a bent elbow: a larger
                // rotation folds the casting hand back through the hat.)
                float prep = MonsterRules.FireballPrepare, u = Mathf.Clamp01(st / prep);
                float raise = MonsterAnimRules.Ease(st / .4f), wind = MonsterAnimRules.Ease((st - (prep - .42f)) / .36f);
                rightX = Mathf.Lerp(rightX, -65f + Mathf.Sin(st * 3.2f) * 3f * (1f - wind), raise) - 25f * wind;
                rightZ = 28f * raise - 16f * wind;
                head = Mathf.Lerp(head, -6f, raise) - 4f * wind;
                _fire = Mathf.Lerp(.15f, 1f, u);
                _tilt = Quaternion.Euler(-6f * u * raise - 5f * wind, 0f, sway * 2f * (1f - raise));
                k = 22f;
                break;
            }
            case MonsterAnim.FireCast:
            {
                // The throw: the arm whips forward from the wound-up pose, swings a little past and settles while
                // the body follows it.
                float hit = MonsterAnimRules.Strike(st, .1f, .1f);
                rightX = Mix(-90f, -42f, hit); rightZ = Mix(12f, -8f, hit); k = 30f;
                head = Mix(-10f, 7f, hit);
                _fire = .3f * (1f - Mathf.Clamp01(st / .2f));
                _tilt = Quaternion.Euler(Mix(-11f, 9f, hit), 0f, 0f);
                break;
            }
            case MonsterAnim.ZapPrep:
            {
                // Both arms sweep up overhead (the broom hand lets go a beat later) with sparks gathering, then
                // spread and pull back just before the thrust.
                float prep = MonsterRules.LevitatePrepare, u = Mathf.Clamp01(st / prep);
                float up = MonsterAnimRules.Ease(st / (prep * .6f)), upLeft = MonsterAnimRules.Ease((st - .12f) / (prep * .6f));
                float coil = MonsterAnimRules.Ease((st - (prep - .3f)) / .3f), tremble = Mathf.Sin(t * 13f) * 1.2f * u;
                rightX = Mathf.Lerp(rightX, -165f, up) + tremble; leftX = Mathf.Lerp(leftX, -165f, upLeft) - tremble;
                rightZ = 14f * up + 8f * coil; leftZ = -14f * upLeft - 8f * coil;
                head = Mathf.Lerp(head, -12f, up);
                _volt = Mathf.Lerp(.2f, 1f, u);
                _tilt = Quaternion.Euler(-8f * up - 3f * coil, 0f, sway * 2f * (1f - up));
                k = 22f;
                break;
            }
            case MonsterAnim.Zap:
            {
                // Both arms thrust down at the victim and stay there, straining slowly against the hold.
                float hit = MonsterAnimRules.Strike(st, .14f, .14f), strain = Mathf.Sin(t * 5.5f);
                leftX = rightX = Mix(-165f, -84f, hit) + strain * 2.5f + Mathf.Sin(t * 17f);
                rightZ = Mix(22f, -10f, hit); leftZ = -rightZ;
                head = Mix(-12f, 8f, hit);
                _volt = .8f + .2f * Mathf.Sin(t * 29f);
                _tilt = Quaternion.Euler(Mix(-11f, 6f, hit), 0f, strain * 1.2f);
                k = 28f;
                break;
            }
        }
        if (stunned) { leftX = rightX = 15f; rightZ = 40f; leftZ = -40f; head = 20f; _shift = Vector3.zero; }
        Rot(_armL, leftX, 0f, leftZ, k);
        Rot(_armR, rightX, 0f, rightZ, k);
        Rot(_head, head, 0f, 0f);
        if (_fire > 0f) Glow(_handR, new Color(1f, .5f, .12f), 2.8f * _fire, 4f);
        else if (_volt > 0f) Glow(_handR, new Color(.66f, .55f, 1f), 2.6f * _volt, 4.5f);
    }

    // Raised-neck pitch per neck joint (tail0 next to the head ... tail5), as in the concept render.
    // Rearing: the neck rises from the body (joints 5-4 next to the root pitch up) and the last segments by the head
    // (0-2) level it out again, so the head looks ahead instead of the neck dipping through the floor first.
    private static readonly float[] NeckRise = { -26f, -26f, -20f, 4f, 26f, 28f };

    // Brain timers the body follows: the rattle before the lunge, and the bite with its strike lead-in.
    private const float BasiliskRattle = MonsterRules.BasiliskRattleSeconds, BasiliskBite = MonsterRules.BasiliskBiteWindup, BasiliskBiteLead = .09f;

    // Basilisk: slithers low; before the lunge the neck rears up, the jaw opens in a hiss and the tail tip
    // rattles; the lunge is a straight dart; constricting, the body winds into a tightening ring. The bite is a
    // low sideways S with the jaw wide open.
    private void AnimateBasilisk(MonsterAnim anim, MonsterPose pose, float t, float st, bool stunned)
    {
        // Crawling: the S-wave grows with speed (a slow ripple at rest) and the body slides from side to side.
        float wave = Mathf.Lerp(6f, pose == MonsterPose.Flee ? 20f : 15f, _move), rise = 0f, jaw = 0f, coil = 0f, bite = 0f, headYaw = 0f, headPitch = 0f, k = 14f;
        bool rattle = false;
        // dart: how far the whole snake is thrown forward (a share of its own scale) by a lunge or a bite.
        float dart = 0f, since = t - _animAt;
        _eye = .65f;
        switch (anim)
        {
            case MonsterAnim.Rattle:
            {
                // The neck rears up, the head weaves slowly with the jaw open in a hiss; in the last fifth of a
                // second it pulls back and gapes wider - the lunge starts from there.
                float up = MonsterAnimRules.Ease(st / .3f), pull = MonsterAnimRules.Ease((st - (BasiliskRattle - .22f)) / .22f);
                rise = up + .12f * pull; jaw = (24f + 4f * Mathf.Sin(st * 11f)) * up + 14f * pull; rattle = true; wave = 3f;
                headYaw = Mathf.Sin(st * 5f) * 12f * up * (1f - pull); headPitch = 6f * up - 8f * pull;
                dart = -.05f * pull;
                _eye = .8f + .2f * Mathf.Sin(t * 14f);
                break;
            }
            case MonsterAnim.BitePrep:
            {
                // The neck draws into an S with the jaw wide open, then whips straight: the head snaps forward and
                // the jaw shuts exactly when the host deals the bite.
                float snap = MonsterAnimRules.Strike(st, BasiliskBite, BasiliskBiteLead);
                rise = Mix(.4f, .25f, snap); bite = MonsterAnimRules.Ease(st / .2f) * (1f - Mathf.Clamp01(snap));
                jaw = Mix(44f * MonsterAnimRules.Ease(st / .12f), 8f, snap); headPitch = 8f * snap;
                dart = .14f * snap; wave = 2f; k = 30f; _eye = 1f;
                break;
            }
            case MonsterAnim.None when _animWas == MonsterAnim.BitePrep && since < .45f && !stunned:
            {
                // The bite holds for a moment before it lets go and slides back.
                float carry = MonsterAnimRules.Release(since, .1f, .3f);
                float snap = MonsterAnimRules.Strike(BasiliskBite + since, BasiliskBite, BasiliskBiteLead) * carry;
                rise = .25f * carry; jaw = 8f * carry; headPitch = 8f * snap; dart = .14f * snap;
                wave = Mathf.Lerp(wave, 2f, carry); k = 30f; _eye = Mathf.Lerp(.65f, 1f, carry);
                break;
            }
            case MonsterAnim.Lunge:
            {
                // Out of the reared pose the neck shoots level and the whole body darts after it.
                float go = MonsterAnimRules.Ease(st / .12f);
                rise = _animWas == MonsterAnim.Rattle ? 1.12f * (1f - go) : 0f;
                jaw = Mathf.Lerp(38f, 48f, go); wave = 2f; k = 32f; headPitch = Mathf.Lerp(-2f, -4f, go); _eye = 1f;
                dart = Mathf.Lerp(-.05f, .1f, go);
                break;
            }
            case MonsterAnim.Coil:
                coil = MonsterAnimRules.Ease(st / .5f); rise = .5f; jaw = Mathf.Lerp(40f, 14f, coil); wave = 0f; _eye = .9f;
                break;
        }
        if (anim == MonsterAnim.None && !stunned) _shift = Vector3.right * (Mathf.Sin(_phase * 1.5f) * .03f * _move);
        if (!stunned) _shift += Vector3.forward * (dart * _modelScale);
        if (stunned) { wave = 0f; jaw = 10f; _eye = .2f; }
        // Picked up: a snake in hand curls into a tight ring, head tucked in on top, hissing.
        bool held = _brain.Held && !_brain.Dead;
        int count = Mathf.Max(2, _tail.Length - 1);
        float ringTurn = 340f / count;
        if (held) { coil = 1f; rise = 0f; wave = 0f; bite = 0f; jaw = 22f + 8f * Mathf.Sin(t * 9f); headPitch = 14f; headYaw = 0f; _eye = .9f; k = 10f; }
        float turn = held ? ringTurn * (1f + .04f * Mathf.Sin(t * 5f)) : 24f + 4f * Mathf.Sin(t * 6f); // the ring squeezes
        int n = _tail.Length;
        for (int i = 0; i < n; i++)
        {
            var j = _tail[i];
            if (j == null || i == BasiliskRoot) continue;
            bool neck = i < BasiliskRoot;
            int d = neck ? BasiliskRoot - i : i - BasiliskRoot;
            float yaw = wave * Mathf.Sin(_phase * 1.5f - d * .8f) * (neck ? -.6f : 1f), pitch = 0f;
            if (stunned) yaw = 5f * Mathf.Sin(t * 1.4f - d * .5f); // limp, drifting slowly
            if (neck)
            {
                if (i < NeckRise.Length) pitch = -NeckRise[i] * rise;
                yaw += bite * (i == 5 ? 40f : i == 3 ? -75f : i == 1 ? 35f : 0f);
            }
            // A ring: the neck turns one way, the body behind the root the other (it points backwards).
            yaw = Mathf.Lerp(yaw, neck ? turn : -turn * (held ? 1f : 1f + d * .07f), coil);
            if (rattle && i >= n - 3) yaw += Mathf.Sin(t * 48f) * 16f;
            if (rattle && (i == n - 4 || i == n - 5)) pitch += 14f;
            Rot(j, pitch, yaw, 0f, k);
        }
        Rot(_head, headPitch, headYaw, 0f, k);
        Rot(_jaw, jaw, 0f, 0f, 26f);
        // Constricting, the brain sits on the victim: slide the model so the ring closes around that spot.
        if (coil > 0f && !held) _shift = new Vector3(-.48f, 0f, .2f * (BasiliskRoot + .5f)) * (_modelScale * coil);
    }

    // Gorgon: lowers its head and paws the floor while it roars, gallops head-down in the charge, lifts its
    // head with burning eyes for the Death Stare, and shakes a dazed head after a wall.
    private void AnimateGorgon(MonsterAnim anim, float t, float st, float move, bool stunned)
    {
        float head = Mathf.Sin(t * 2f) * 3f, headYaw = 0f, jaw = 0f;
        // gallop: 0 walking .. 1 full gallop; paw: how much the front left hoof scrapes; scrape: where in a scrape.
        float gallop = 0f, paw = 0f, scrape = 0f, headK = 14f;
        _eye = .2f;
        switch (anim)
        {
            case MonsterAnim.Acquire:
            {
                // Locks on: the head sinks and the weight settles back onto the hind legs.
                float u = MonsterAnimRules.Ease(st / .6f);
                head = Mathf.Lerp(head, 18f, u); _eye = Mathf.Lerp(.2f, .45f, u);
                _tilt = Quaternion.Euler(-2.5f * u, 0f, 0f);
                _shift = Vector3.back * (.03f * u * _modelScale);
                break;
            }
            case MonsterAnim.Roar:
            {
                // Bellows with the head low, swinging it slowly, and scrapes the floor; in the last quarter second
                // it gathers itself - sinks back, jaw closing, hooves planted - and the charge launches from there.
                float open = MonsterAnimRules.Ease(st / .22f), coil = MonsterAnimRules.Ease((st - (MonsterRules.RoarSeconds - .28f)) / .28f);
                float loud = open * (1f - coil);
                head = Mathf.Lerp(18f, 32f, open) + 6f * coil + Mathf.Sin(st * 7f) * 2.5f * loud;
                headYaw = Mathf.Sin(st * 5.2f) * 7f * loud;
                jaw = 30f * open - 14f * coil + Mathf.Sin(st * 9f) * 3f * loud;
                paw = loud; scrape = Mathf.Repeat(st * 1.8f, 1f);
                _eye = Mathf.Lerp(.45f, .75f, open);
                _tilt = Quaternion.Euler(Mathf.Lerp(-2.5f, 4f, open) - 8f * coil, 0f, Mathf.Sin(st * 19f) * .5f * loud);
                _shift = (Vector3.back * (.03f * (1f - open) + .08f * coil) + Vector3.down * (.03f * coil)) * _modelScale;
                break;
            }
            case MonsterAnim.Charge:
            {
                // Springs out of the crouch into the gallop: head down with the horns forward (held steady against
                // the bounding body), body pitched into the run.
                float go = MonsterAnimRules.Ease(st / .3f), bound = Mathf.Sin(_phase * 3f);
                head = Mathf.Lerp(38f, 44f, go) - bound * 3f * go; jaw = 10f; gallop = go; _eye = .9f;
                _tilt = Quaternion.Euler(Mathf.Lerp(-6f, 10f, go) + bound * 5f * go, 0f, Mathf.Sin(_phase * 1.5f) * 2f * go);
                _shift = (Vector3.up * (bound * bound * .09f * go) + Vector3.forward * Mathf.Lerp(-.08f, .05f, go)) * _modelScale;
                Glow(_head != null ? _head.T : null, new Color(1f, .25f, .05f), 1.4f, 3f);
                break;
            }
            case MonsterAnim.Stare:
            {
                float u = Mathf.Clamp01(st / 2f); // unused since the balance pass (no Death Stare); kept for old snapshots
                head = -16f; jaw = 6f;
                _eye = Mathf.Lerp(.5f, 1f, u) * (.85f + .15f * Mathf.Sin(t * (8f + 10f * u)));
                _tilt = Quaternion.Euler(-3f, 0f, Mathf.Sin(t * 30f) * u * .8f);
                Glow(_head != null ? _head.T : null, new Color(1f, .2f, .05f), 3.2f * u, 5f);
                break;
            }
            case MonsterAnim.Daze:
            {
                // Out of a charge the body recoils from the stop; then the hanging head shakes, less and less.
                float jolt = _animWas == MonsterAnim.Charge ? MonsterAnimRules.Impulse(st, .07f, .5f) : 0f;
                float fade = .3f + .7f * Mathf.Exp(-st * .9f);
                head = 22f - 30f * jolt; headYaw = Mathf.Sin(st * 5f) * 20f * fade; jaw = 14f; _eye = .08f;
                _tilt = Quaternion.Euler(-9f * jolt, 0f, Mathf.Sin(st * 3.3f) * 3f * fade);
                _shift = Vector3.back * (.08f * jolt * _modelScale);
                if (jolt > .01f) headK = 30f;
                break;
            }
        }
        if (stunned)
        {
            // Dazed: knees buckle, the body sags and sways, the head hangs and lolls side to side (stars above it).
            head = 38f + Mathf.Sin(t * 1.7f) * 6f; headYaw = Mathf.Sin(t * 2.3f) * 24f; jaw = 18f; _eye = .05f;
            _tilt = Quaternion.Euler(6f + Mathf.Sin(t * 1.3f) * 3f, 0f, Mathf.Sin(t * 1.9f) * 7f);
            _shift = Vector3.down * (.16f * _modelScale);
        }
        else if (anim == MonsterAnim.None)
        {
            // Walking: the heavy body rolls from shoulder to shoulder and the head nods with each step.
            _tilt = Quaternion.Euler(0f, 0f, Mathf.Sin(_phase * 2f) * 2.5f * move);
            _shift = Vector3.up * (Mathf.Abs(Mathf.Sin(_phase * 2f)) * .03f * move);
            head += Mathf.Sin(_phase * 4f) * 4f * move;
        }
        // Head toss (too close to charge): the horns rip upward at once, then the head comes back down.
        float toss = stunned ? 0f : MonsterAnimRules.Impulse(t - _tossAt, .08f, .6f);
        if (toss > .01f)
        {
            head -= 46f * toss; jaw += 14f * toss; headK = 34f;
            _tilt = Quaternion.Euler(-7f * toss, 0f, 0f) * _tilt;
            _shift += Vector3.forward * (.06f * toss * _modelScale);
        }
        // One scrape: the hoof lifts quickly and drags back slowly.
        float lift = scrape < .28f ? MonsterAnimRules.Ease(scrape / .28f) : 1f - MonsterAnimRules.Ease((scrape - .28f) / .72f);
        foreach (var (leg, left, front) in _legs)
        {
            float walk = Mathf.Sin(_phase * 2f + ((left ^ front) ? 0f : Mathf.PI)) * 30f * move;
            float run = Mathf.Sin(_phase * 3f + (front ? 0f : 2.2f) + (left ? .35f : 0f)) * 58f;
            float swing = Mathf.Lerp(walk, run, gallop);
            if (front && left) swing = Mathf.Lerp(swing, -6f - 40f * lift, paw); // scraping the floor
            if (front) swing -= 12f * toss; // braces into the toss
            if (stunned) swing = (front ? -14f : 16f) + Mathf.Sin(t * 2f + ((left ^ front) ? 0f : Mathf.PI)) * 4f; // legs splay and wobble
            Rot(leg, swing, 0f, stunned ? (left ? -18f : 18f) : 0f, gallop > 0f ? 34f : paw > 0f ? 24f : 16f);
        }
        Rot(_head, head, headYaw, 0f, headK);
        Rot(_jaw, jaw, 0f, 0f, 20f);
    }

    // The naga's four arms (upper/lower, left/right), body lean and head pitch at one moment of an attack.
    private struct NagaPose
    {
        public Quaternion Ul, Ur, Ll, Lr, Tilt;
        public float Head;

        public static NagaPose Blend(NagaPose a, NagaPose b, float w) => new()
        {
            Ul = Quaternion.SlerpUnclamped(a.Ul, b.Ul, w), Ur = Quaternion.SlerpUnclamped(a.Ur, b.Ur, w),
            Ll = Quaternion.SlerpUnclamped(a.Ll, b.Ll, w), Lr = Quaternion.SlerpUnclamped(a.Lr, b.Lr, w),
            Tilt = Quaternion.SlerpUnclamped(a.Tilt, b.Tilt, w), Head = Mathf.LerpUnclamped(a.Head, b.Head, w),
        };
    }

    // A strike takes this long from the wound-up pose to the hit; in the sweep the lower blades trail the upper.
    private const float NagaLead = .13f, NagaTrail = .035f;

    // Combo hit 1: the upper right blade rears back (wind), then chops down across the body (hit).
    private static NagaPose NagaChop(NagaPose idle, float wind, float hit)
    {
        var raised = new NagaPose
        {
            Ur = Quaternion.Euler(-65f, 10f, 0f), Ul = Quaternion.Euler(-20f, 0f, 0f), Ll = idle.Ll, Lr = idle.Lr,
            Tilt = Quaternion.Euler(-4f, 6f, 0f), Head = 6f,
        };
        var p = NagaPose.Blend(idle, raised, wind);
        if (hit <= 0f) return p;
        var down = raised;
        down.Ur = Quaternion.Euler(88f, -18f, 0f); down.Ul = Quaternion.Euler(-8f, 0f, 0f);
        down.Tilt = Quaternion.Euler(9f, -8f, 0f);
        return NagaPose.Blend(p, down, hit);
    }

    // Combo hit 2: all four blades drawn back to one side (wind), then swung level across (hit / hitLow).
    private static NagaPose NagaSweep(NagaPose idle, float wind, float hit, float hitLow)
    {
        var swung = new NagaPose
        {
            Ul = Quaternion.Euler(78f, Mix(-40f, 115f, hit), 0f), Ll = Quaternion.Euler(10f, Mix(-45f, 120f, hitLow), 0f),
            Ur = Quaternion.Euler(78f, Mix(-80f, 40f, hit), 0f), Lr = Quaternion.Euler(10f, Mix(-75f, 45f, hitLow), 0f),
            Tilt = Quaternion.Euler(5f, Mix(-18f, 22f, hit), 0f), Head = 4f,
        };
        return hit > 0f || hitLow > 0f ? swung : NagaPose.Blend(idle, swung, wind);
    }

    // Bleeding cut: the lower pair crosses low with the upper pair raised (wind), then rips apart (hit).
    private static NagaPose NagaCut(NagaPose idle, float wind, float hit)
    {
        var cut = new NagaPose
        {
            Ll = Quaternion.Euler(Mix(-18f, 25f, hit), Mix(78f, -50f, hit), 0f), Lr = Quaternion.Euler(Mix(-18f, 25f, hit), Mix(-78f, 50f, hit), 0f),
            Ul = Quaternion.Euler(-35f, -15f, 0f), Ur = Quaternion.Euler(-35f, 15f, 0f),
            Tilt = Quaternion.Euler(Mix(-6f, 10f, hit), 0f, 0f), Head = 10f,
        };
        return hit > 0f ? cut : NagaPose.Blend(idle, cut, wind);
    }

    // Naga Queen. Combo hit 1: the upper right blade rears back and chops down. Hit 2: all four blades swing
    // level in one sweep from left to right. Bleeding cut: the lower pair crosses low in a red glow, then rips
    // apart. Petrifying gaze: arms open wide, she rises and her eyes flare. After the combo the arms hang.
    private void AnimateNaga(MonsterAnim anim, float t, float st, float move, bool stunned)
    {
        // Slithering: the tail's wave grows with speed and the torso counter-sways over it.
        float sway = Mathf.Sin(t * 2f), head = sway * 2f, tail = Mathf.Lerp(5f, 15f, move);
        var ul = Quaternion.Euler(sway * 5f, 0f, 0f); var ur = Quaternion.Euler(-sway * 5f, 0f, 0f);
        var ll = Quaternion.Euler(0f, sway * 6f, 0f); var lr = Quaternion.Euler(0f, -sway * 6f, 0f);
        var droopUl = Quaternion.Euler(60f, 0f, 25f); var droopUr = Quaternion.Euler(60f, 0f, -25f);
        var droopLl = Quaternion.Euler(35f, 0f, 15f); var droopLr = Quaternion.Euler(35f, 0f, -15f);
        float kUpper = 16f, kLower = 16f;
        _eye = .15f;
        _tilt = Quaternion.Euler(4f * move, Mathf.Sin(_phase * .9f) * 5f * move, 0f);
        var idle = new NagaPose { Ul = ul, Ur = ur, Ll = ll, Lr = lr, Tilt = _tilt, Head = head };
        float since = t - _animAt;
        const float w1 = MonsterRules.ComboFirstWindup, w2 = MonsterRules.ComboSecondWindup, wb = MonsterRules.BleedWindup;
        NagaPose? shown = null;
        switch (anim)
        {
            case MonsterAnim.Combo1:
                // The sword comes up early and is slowly drawn back for the rest of the windup so players can read
                // it; the chop accelerates and lands exactly when the host deals the damage.
                shown = NagaChop(idle, MonsterAnimRules.Draw(st, w1 - NagaLead), MonsterAnimRules.Strike(st, w1, NagaLead));
                break;
            case MonsterAnim.Combo2:
            {
                // The chop follows through and settles, then all four blades are drawn back for the sweep. The
                // lower pair trails the upper one by a beat, so the sweep reads as one rolling motion.
                var p = NagaSweep(idle, MonsterAnimRules.Draw(st - .1f, w2 - NagaLead - .1f),
                    MonsterAnimRules.Strike(st, w2, NagaLead), MonsterAnimRules.Strike(st - NagaTrail, w2, NagaLead));
                float carry = _animWas == MonsterAnim.Combo1 ? MonsterAnimRules.Release(st, .1f, .32f) : 0f;
                if (carry > 0f) p = NagaPose.Blend(p, NagaChop(idle, 1f, MonsterAnimRules.Strike(w1 + st, w1, NagaLead)), carry);
                shown = p;
                break;
            }
            case MonsterAnim.BleedCut:
            {
                float wind = MonsterAnimRules.Draw(st, wb - NagaLead), cut = MonsterAnimRules.Strike(st, wb, NagaLead);
                shown = NagaCut(idle, wind, cut);
                Glow(null, new Color(1f, .08f, .05f), 2.6f * wind + 1.4f * Mathf.Clamp01(cut), 4f);
                break;
            }
            case MonsterAnim.None when _animWas == MonsterAnim.BleedCut && since < .6f:
            {
                // The cut rips through and the blades stay out for a moment as she pulls away.
                float carry = MonsterAnimRules.Release(since, .14f, .4f);
                shown = NagaPose.Blend(idle, NagaCut(idle, 1f, MonsterAnimRules.Strike(wb + since, wb, NagaLead)), carry);
                Glow(null, new Color(1f, .08f, .05f), 4f * carry, 4f);
                break;
            }
            case MonsterAnim.StoneGaze:
            {
                float u = Mathf.Clamp01(st / MonsterRules.PetrifyPrepare);
                ul = Quaternion.Euler(0f, -20f, 35f); ur = Quaternion.Euler(0f, 20f, -35f);
                ll = Quaternion.Euler(0f, -40f, 20f); lr = Quaternion.Euler(0f, 40f, -20f);
                kUpper = kLower = 7f; head = 9f; tail = 2f;
                _eye = Mathf.Lerp(.3f, 1f, u) * (.85f + .15f * Mathf.Sin(t * (8f + 12f * u)));
                _tilt = Quaternion.Euler(-7f * u, 0f, 0f);
                _shift = Vector3.up * (.12f * u);
                Glow(_head != null ? _head.T : null, new Color(1f, .85f, .2f), 3f * u, 5f);
                break;
            }
            case MonsterAnim.Exposed:
            {
                // Winded: the sweep carries through, then the arms sink and hang.
                var p = new NagaPose { Ul = droopUl, Ur = droopUr, Ll = droopLl, Lr = droopLr, Tilt = Quaternion.Euler(8f, 0f, sway * 3f), Head = 20f };
                float carry = _animWas == MonsterAnim.Combo2 ? MonsterAnimRules.Release(st, .16f, .55f) : 0f;
                if (carry > 0f)
                    p = NagaPose.Blend(p, NagaSweep(idle, 1f, MonsterAnimRules.Strike(w2 + st, w2, NagaLead), MonsterAnimRules.Strike(w2 + st - NagaTrail, w2, NagaLead)), carry);
                shown = p;
                _eye = .05f;
                break;
            }
        }
        if (shown is { } pose)
        {
            // The curves above are already eased: the joints only follow them closely.
            ul = pose.Ul; ur = pose.Ur; ll = pose.Ll; lr = pose.Lr; head = pose.Head; _tilt = pose.Tilt;
            kUpper = kLower = anim == MonsterAnim.Exposed ? 14f : 28f;
        }
        bool folded = stunned && !_brain.Dead;
        if (folded)
        {
            // Stunned: she folds up - the torso slumps forward and sinks onto the coils, the tail curls around under
            // her, the four arms hug in and the head hangs.
            ul = Quaternion.Euler(70f, 0f, 55f); ur = Quaternion.Euler(70f, 0f, -55f);
            ll = Quaternion.Euler(55f, 0f, 45f); lr = Quaternion.Euler(55f, 0f, -45f);
            kUpper = kLower = 7f; head = 34f + Mathf.Sin(t * 1.6f) * 4f; _eye = .03f;
            _tilt = Quaternion.Euler(30f + Mathf.Sin(t * 1.2f) * 3f, 0f, Mathf.Sin(t * 1.7f) * 4f);
            _shift = Vector3.down * (.38f * _modelScale) + Vector3.back * (.15f * _modelScale);
        }
        Rot(_nagaArms[0], ul, kUpper); Rot(_nagaArms[1], ll, kLower);
        Rot(_nagaArms[2], ur, kUpper); Rot(_nagaArms[3], lr, kLower);
        Rot(_head, head, 0f, 0f, 10f);
        for (int i = 0; i < _tail.Length; i++)
        {
            if (folded) { Rot(_tail[i], 4f, 26f + 3f * Mathf.Sin(t * 1.3f - i * .4f), 0f, 6f); continue; } // coiled up
            Rot(_tail[i], 0f, (i < 4 ? tail * .35f : tail) * Mathf.Sin(_phase * .9f - i * .55f), 0f, 10f);
        }
    }

    // Mischief Fairy: wings a blur, bobbing as she flits. Stealing - both arms up and the wand flaring over the
    // thing (0.8 s), then she tows it below her on a golden thread. Afterwards she covers her mouth and shakes
    // with giggles; startled, she darts off arms flailing.
    private void AnimateFairy(MonsterAnim anim, MonsterPose pose, float t, float st, float move, bool stunned)
    {
        float flutter = stunned ? 0f : Mathf.Sin(t * (pose == MonsterPose.Flee ? 52f : 40f)) * 30f - 6f; // positive = wing down
        foreach (var (wing, left) in _wings) Rot(wing, 0f, 0f, (left ? 1f : -1f) * (stunned ? 55f : flutter), 80f);
        float sway = Mathf.Sin(t * 3f);
        float leftX = 10f + sway * 8f, rightX = -25f + Mathf.Sin(t * 5f) * 14f, leftZ = 0f, rightZ = 0f, head = sway * 4f, roll = -8f, k = 18f;
        _tilt = Quaternion.Euler(10f * move, 0f, sway * 5f);
        _shift = Vector3.up * (Mathf.Sin(t * 4.2f) * .05f);
        float wand = .25f + .1f * Mathf.Sin(t * 9f);
        switch (anim)
        {
            case MonsterAnim.FairyLift:
            {
                float u = Mathf.Clamp01(st / MonsterRules.FairyLiftSeconds);
                leftX = rightX = Mathf.Lerp(-40f, -160f, Smooth(u)) + Mathf.Sin(t * 30f) * 3f; rightZ = 12f; leftZ = -12f;
                head = 14f; roll = 0f;
                wand = Mathf.Lerp(.4f, 1f, u);
                _tilt = Quaternion.Euler(12f, 0f, 0f);
                break;
            }
            case MonsterAnim.FairyCarry:
                leftX = rightX = 38f + Mathf.Sin(t * 12f) * 4f; rightZ = -14f; leftZ = 14f; // straining at the thread below her
                head = 12f; roll = 0f; wand = .8f + .2f * Mathf.Sin(t * 21f);
                _tilt = Quaternion.Euler(14f * move + 4f, 0f, Mathf.Sin(t * 7f) * 6f);
                break;
            case MonsterAnim.FairyGiggle:
                leftX = rightX = -125f; rightZ = -30f; leftZ = 30f; head = -10f + Mathf.Sin(t * 26f) * 6f; roll = Mathf.Sin(t * 13f) * 10f;
                _tilt = Quaternion.Euler(-8f, 0f, Mathf.Sin(t * 26f) * 7f);
                _shift = Vector3.up * (Mathf.Abs(Mathf.Sin(t * 13f)) * .08f);
                break;
            default:
                if (pose == MonsterPose.Flee) { leftX = -70f + Mathf.Sin(t * 24f) * 40f; rightX = -70f - Mathf.Sin(t * 24f) * 40f; leftZ = -30f; rightZ = 30f; head = -8f; k = 30f; }
                break;
        }
        if (stunned) { leftX = rightX = 20f; rightZ = 50f; leftZ = -50f; head = 25f; wand = 0f; _shift = Vector3.zero; }
        Rot(_armL, leftX, 0f, leftZ, k);
        Rot(_armR, rightX, 0f, rightZ, k);
        Rot(_head, head, 0f, roll, 14f);
        _fire = wand; // the wand's glow (gold for the fairy)
        _tethered = _brain.Tether(out _tetherAt);
        if (_tethered) Glow(null, new Color(1f, .85f, .35f), 1.8f, 3f);
        else if (wand > .5f) Glow(_handR, new Color(1f, .85f, .35f), 1.6f * wand, 3f);
    }

    // The snatch and the throw take this long from the wound-up pose to the moment they land.
    private const float TwinsLead = .12f;

    // Hansel and Gretel: skipping hand in hand, heads tipped together. The lure - they stop dead and their heads
    // roll slowly apart. The rush - heads down, free arms out. The reach - both free arms straight at the player.
    // The hold - they rock from side to side, swinging the victim, then fling both arms up for the throw.
    private void AnimateTwins(MonsterAnim anim, float t, float st, float move, bool stunned)
    {
        float skip = Mathf.Sin(_phase * 2f), step = 32f * move, headIn = 16f, headPitch = Mathf.Sin(t * 1.3f) * 3f;
        float outer = skip * 22f * move, k = 16f;
        _tilt = Quaternion.Euler(0f, 0f, skip * 3f * move);
        _shift = Vector3.up * (Mathf.Abs(skip) * .05f * move);
        switch (anim)
        {
            case MonsterAnim.TwinsLure:
            {
                // They stop dead: the heads roll slowly apart (far past upright) and the free arms drift up. In the
                // last quarter second they lean back and sink a little - the rush springs out of that.
                float stop = MonsterAnimRules.Ease(st / .3f), coil = MonsterAnimRules.Ease((st - (MonsterRules.TwinsLureSeconds - .25f)) / .25f);
                headIn = 16f - 46f * MonsterAnimRules.Ease(st / MonsterRules.TwinsLureSeconds);
                headPitch = Mathf.Lerp(headPitch, -6f, stop) + 8f * coil;
                outer = Mathf.Lerp(outer, -20f + Mathf.Sin(st * 3f) * 10f, stop) - 14f * coil; step *= 1f - stop;
                _tilt = Quaternion.Slerp(_tilt, Quaternion.Euler(-5f * coil, 0f, 0f), stop);
                _shift = Vector3.Lerp(_shift, Vector3.down * (.03f * coil), stop);
                k = 20f;
                break;
            }
            case MonsterAnim.TwinsRush:
            {
                // Heads go down and the free arms sweep out as they break into the run.
                float go = MonsterAnimRules.Ease(st / .25f);
                headIn = Mathf.Lerp(-30f, 4f, go); headPitch = Mathf.Lerp(2f, 16f, go); outer = Mathf.Lerp(-34f, -70f, go);
                step = Mathf.Lerp(step, 46f, go); k = 24f;
                _tilt = Quaternion.Euler(Mathf.Lerp(-5f, 12f, go), 0f, skip * 4f * go);
                _shift *= go;
                break;
            }
            case MonsterAnim.TwinsReach:
            {
                // Both free arms come up at the player early and keep stretching, fingers working; the snatch itself
                // lunges forward and closes exactly when the host decides the grab.
                const float reach = MonsterRules.TwinsReachSeconds;
                float up = MonsterAnimRules.Draw(st, reach - TwinsLead), grab = MonsterAnimRules.Strike(st, reach, TwinsLead);
                headIn = Mathf.Lerp(4f, -10f, up); headPitch = Mathf.Lerp(16f, -4f, up);
                outer = Mix(Mathf.Lerp(-70f, -92f, up) + Mathf.Sin(st * 9f) * 1.5f * up, -106f, grab);
                step = 0f; k = 26f;
                _tilt = Quaternion.Euler(Mix(Mathf.Lerp(12f, 5f, up), 14f, grab), 0f, 0f);
                _shift = Vector3.forward * (.05f * grab);
                break;
            }
            case MonsterAnim.TwinsSwing:
            {
                // One, two, three: they rock the victim from side to side, each swing a full smooth arc; before the
                // third ends the arms drop and they crouch, winding up the throw.
                const float hold = MonsterRules.TwinsHoldSeconds;
                float take = MonsterAnimRules.Ease(st / .2f), wind = MonsterAnimRules.Ease((st - (hold - .24f)) / .24f);
                float rock = Mathf.Sin(st * 15.7f) * take * (1f - wind);
                headIn = 16f; headPitch = Mathf.Lerp(-8f, 6f, wind); outer = Mathf.Lerp(Mathf.Lerp(-106f, -88f, take), -45f, wind); step = 0f; k = 26f;
                _tilt = Quaternion.Euler(Mathf.Lerp(Mathf.Lerp(14f, 4f, take), 11f, wind), 0f, rock * 13f);
                _shift = Vector3.right * (rock * .06f) + Vector3.down * (.04f * wind);
                break;
            }
            case MonsterAnim.TwinsThrow:
            {
                // The throw: out of the crouch both arms fling up and over, they rise onto their toes, then settle.
                float fling = MonsterAnimRules.Strike(st, TwinsLead, TwinsLead);
                headIn = Mix(16f, -6f, fling); headPitch = Mix(6f, -18f, fling); outer = Mix(-45f, -165f, fling); step = 0f; k = 32f;
                _tilt = Quaternion.Euler(Mix(11f, -9f, fling), 0f, 0f);
                _shift = Vector3.up * (.05f * MonsterAnimRules.Impulse(st, TwinsLead, .45f)) + Vector3.down * (.04f * (1f - Mathf.Clamp01(fling)));
                break;
            }
        }
        if (stunned) { headIn = 30f; headPitch = 28f; outer = 12f; step = 0f; }
        for (int child = 0; child < 2; child++)
        {
            float side = child == 0 ? 1f : -1f; // Hansel stands on the left: tipping towards his sister rolls his head the other way
            int o = child * 5;
            Rot(_twins[o], headPitch, 0f, -side * headIn, 9f);
            Rot(_twins[o + 2], outer * (anim == MonsterAnim.None ? side : 1f), 0f, anim == MonsterAnim.None ? 0f : side * 6f, k);
            float stride = Mathf.Sin(_phase * 2f + child * Mathf.PI) * step;
            Rot(_twins[o + 3], stride, 0f, 0f, 24f);
            Rot(_twins[o + 4], -stride, 0f, 0f, 24f);
        }
    }

    // ------------------------------------------------------------------ glow effects

    // Ask for the effect light this frame (anchor = null: in front of the chest).
    private void Glow(Transform anchor, Color color, float power, float range)
    {
        _lightAt = anchor; _lightColor = color; _lightPower = power; _lightRange = range;
    }

    private void Effects(float t)
    {
        // Eyes: additive orbs over the modelled eyes.
        bool eyes = _eye > .02f;
        if (eyes && _eyeMaterial != null) _eyeMaterial.color = new Color(_eyeColor.r, _eyeColor.g, _eyeColor.b, Mathf.Lerp(.08f, .32f, _eye));
        foreach (var orb in _eyeOrbs)
        {
            if (orb.gameObject.activeSelf != eyes) orb.gameObject.SetActive(eyes);
            if (eyes) orb.localScale = Vector3.one * (_eyeSize * (.75f + .7f * _eye));
        }
        if (_kind == MonsterKind.Fairy && _orbR != null)
        {
            // Wand glow, and a jittering golden thread down to the thing she carries.
            bool on = _fire > .01f;
            if (_orbR.gameObject.activeSelf != on) _orbR.gameObject.SetActive(on);
            if (_spark.enabled != _tethered) _spark.enabled = _tethered;
            if (on)
            {
                _orbMaterial.color = new Color(1f, .88f, .4f, .95f);
                _orbR.localScale = Vector3.one * ((.05f + .09f * _fire) * (1f + .15f * Mathf.Sin(t * 41f)));
            }
            if (_tethered)
            {
                Vector3 a = _handR.position, b = _tetherAt;
                float jitter = Mathf.Min(.12f, Vector3.Distance(a, b) * .12f);
                for (int i = 0; i < SparkPoints; i++)
                {
                    var p = Vector3.Lerp(a, b, i / (SparkPoints - 1f));
                    if (i > 0 && i < SparkPoints - 1) p += Random.insideUnitSphere * jitter;
                    _spark.SetPosition(i, p);
                }
            }
        }
        // Witch: flame in the right palm, sparks on both hands with an arc between them.
        else if (_orbR != null)
        {
            bool on = Mathf.Max(_fire, _volt) > .01f, both = _volt > .01f;
            if (_orbR.gameObject.activeSelf != on) _orbR.gameObject.SetActive(on);
            if (_orbL.gameObject.activeSelf != both) _orbL.gameObject.SetActive(both);
            if (_spark.enabled != both) _spark.enabled = both;
            if (on)
            {
                float flicker = 1f + .12f * Mathf.Sin(t * 33f) + .08f * Mathf.Sin(t * 57f);
                _orbMaterial.color = both ? new Color(.74f, .62f, 1f, .95f) : new Color(1f, .55f, .12f, .95f);
                _orbR.localScale = Vector3.one * ((both ? .16f + .12f * _volt : .1f + .5f * _fire) * flicker);
                _orbL.localScale = _orbR.localScale;
            }
            if (both)
            {
                Vector3 a = _handL.position, b = _handR.position;
                float jitter = Vector3.Distance(a, b) * .22f;
                for (int i = 0; i < SparkPoints; i++)
                {
                    var p = Vector3.Lerp(a, b, i / (SparkPoints - 1f));
                    if (i > 0 && i < SparkPoints - 1) p += Random.insideUnitSphere * jitter;
                    _spark.SetPosition(i, p);
                }
            }
        }
        if (_lightPower > .02f)
        {
            if (_light == null)
            {
                _light = new GameObject("Spell light").AddComponent<Light>();
                _light.transform.SetParent(transform, false);
                _light.type = LightType.Point;
                _light.shadows = LightShadows.None;
            }
            _light.enabled = true;
            _light.transform.position = _tethered ? _tetherAt : _lightAt != null ? _lightAt.position : transform.position + Vector3.up * (_height * .55f) + transform.forward * .45f;
            _light.color = _lightColor;
            _light.range = _lightRange;
            _light.intensity = _lightPower * (.9f + .1f * Mathf.Sin(t * 27f));
        }
        else if (_light != null && _light.enabled) _light.enabled = false;
    }

    private static Transform Orb(Transform parent, Material material, string name)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = name;
        var collider = go.GetComponent<Collider>(); collider.enabled = false; Destroy(collider);
        go.transform.SetParent(parent, false);
        var renderer = go.GetComponent<Renderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        go.SetActive(false);
        return go.transform;
    }

    // Golden Nose: the local player sees the pig glow within the perk's range.
    private void GoldenNose()
    {
        if (_kind != MonsterKind.Pig) return;
        var me = PlayerController.instance != null ? PlayerController.instance.playerAvatarScript : null;
        float range = MonsterRules.GoldenNoseRange(me != null ? Skills.SkillEffects.Level(me, Skills.SkillCatalog.GoldenNose) : 0);
        bool on = range > 0f && me != null && Vector3.Distance(me.transform.position, transform.position) <= range;
        if (on && _glow == null)
        {
            _glow = new GameObject("Golden Nose glow").AddComponent<Light>();
            _glow.transform.SetParent(transform, false);
            _glow.transform.localPosition = Vector3.up * .5f;
            _glow.type = LightType.Point;
            _glow.color = new Color(1f, .8f, .25f);
            _glow.range = 3.5f;
            _glow.shadows = LightShadows.None;
        }
        if (_glow != null)
        {
            _glow.enabled = on;
            _glow.intensity = 1.6f + .5f * Mathf.Sin(Time.time * 4f);
        }
    }

    private void OnDestroy()
    {
        foreach (var m in _meshes) if (m != null) Destroy(m);
        foreach (var m in _materials) if (m != null) Destroy(m);
    }
}
