using System.Collections.Generic;
using IncreaseHardMod.Visuals;
using UnityEngine;

namespace IncreaseHardMod.Monsters;

/// <summary>
/// The monster's body on every client: the embedded Blender model (src/Art/monster_*.mesh.json), built at runtime,
/// following the synced enemy rigidbody, with procedural joint animation from the brain's state.
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
    private Transform _handL, _handR, _orbL, _orbR, _lightAt;
    private readonly List<Transform> _eyeOrbs = new();
    private Material _eyeMaterial, _orbMaterial;
    private LineRenderer _spark;
    private Color _eyeColor, _lightColor;
    private float _eyeSize, _eye, _fire, _volt, _fold, _lightPower, _lightRange;
    private Quaternion _tilt = Quaternion.identity;
    private Vector3 _basePos, _shift;
    private float _modelScale = 1f, _pop = 1f, _phase;
    private Vector3 _lastPos;
    private Light _glow, _light;
    private bool _built, _failed;

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
        if (_kind != MonsterKind.Witch) return;
        _handL = Anchor("hand_l", "arm_l"); _handR = Anchor("hand_r", "arm_r");
        if (_handL == null || _handR == null) return;
        _orbMaterial = new Material(MonsterFx.Additive()) { name = "IHM " + _art + " spell" };
        _materials.Add(_orbMaterial);
        _orbL = Orb(_handL, _orbMaterial, "Spark"); _orbR = Orb(_handR, _orbMaterial, "Spell");
        _spark = new GameObject("Hand arc").AddComponent<LineRenderer>();
        _spark.transform.SetParent(transform, false);
        _spark.useWorldSpace = true; _spark.positionCount = SparkPoints; _spark.sharedMaterial = MonsterFx.Additive();
        _spark.startWidth = .035f; _spark.endWidth = .035f;
        _spark.startColor = new Color(.74f, .62f, 1f); _spark.endColor = new Color(.5f, .85f, 1f);
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

    /// <summary>Where a spell leaves the monster: the witch's hands while she casts, else the chest.</summary>
    internal Vector3 CastPoint
    {
        get
        {
            if (_built && _handL != null && _handR != null)
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
        _phase += Time.deltaTime * Mathf.Lerp(2f, 9f, Mathf.Clamp01(speed / 4f));
        float scale = _kind == MonsterKind.Pig ? MonsterRules.PigScale(Mathf.RoundToInt(_brain.Extra % 100f)) : 1f;
        _pop = Mathf.MoveTowards(_pop, 1f, Time.deltaTime * 4f);
        transform.localScale = Vector3.one * scale * Mathf.Lerp(.85f, 1f, _pop);
        Animate(speed);
        GoldenNose();
    }

    private static Vector3 Flat(Vector3 v) => new(v.x, 0f, v.z);

    // ------------------------------------------------------------------ animation

    private static float Smooth(float u) { u = Mathf.Clamp01(u); return u * u * (3f - 2f * u); }
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
        var anim = stunned || _brain.Dead ? MonsterAnim.None : _brain.Anim;
        float move = Mathf.Clamp01(speed / 3f);
        foreach (var j in _all) { j.Target = Quaternion.identity; j.K = 16f; }
        _tilt = Quaternion.identity; _shift = Vector3.zero; _eye = 0f; _fire = 0f; _volt = 0f; _lightPower = 0f;
        switch (_kind)
        {
            case MonsterKind.Pig: AnimatePig(anim, pose, t, st, move); break;
            case MonsterKind.Witch: AnimateWitch(anim, t, st, move, stunned); break;
            case MonsterKind.Basilisk: AnimateBasilisk(anim, pose, t, st, stunned); break;
            case MonsterKind.Gorgon: AnimateGorgon(anim, t, st, move, stunned); break;
            case MonsterKind.Naga: AnimateNaga(anim, t, st, move, stunned); break;
        }
        if (stunned) _tilt = Quaternion.Euler(Mathf.Sin(t * 9f) * 6f, 0f, Mathf.Sin(t * 7f) * 10f) * _tilt;
        foreach (var j in _all)
            j.T.localRotation = Quaternion.Slerp(j.T.localRotation, j.Rest * j.Target, 1f - Mathf.Exp(-j.K * dt));
        float ease = 1f - Mathf.Exp(-12f * dt);
        _model.localRotation = Quaternion.Slerp(_model.localRotation, _tilt, ease);
        _model.localPosition = Vector3.Lerp(_model.localPosition, _basePos + _shift, ease);
        Effects(t);
    }

    // Golden Pig: beating wings and tucked legs in flight; chomping nods while eating; on landing the wings fold
    // away and it runs on its legs.
    private void AnimatePig(MonsterAnim anim, MonsterPose pose, float t, float st, float move)
    {
        bool grounded = _brain.Extra >= 100f || anim == MonsterAnim.Land;
        _fold = Mathf.MoveTowards(_fold, grounded ? 1f : 0f, Time.deltaTime * (grounded ? 1.4f : 3f));
        float rate = pose == MonsterPose.Flee ? 22f : anim == MonsterAnim.Eat ? 9f : 14f;
        float flap = Mathf.Sin(t * rate) * 35f - 8f; // positive = wing down
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
            if (flying) Rot(leg, (front ? -30f : 30f) + Mathf.Sin(t * 3f + phase) * 8f, 0f, 0f);
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
        else if (flying)
        {
            _tilt = Quaternion.Euler(Mathf.Sin(t * 1.7f) * 3f + 7f * move, 0f, Mathf.Sin(t * 1.3f) * 4f);
            _shift = Vector3.up * (Mathf.Sin(t * 2.4f) * .04f);
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
        float leftX = -38f, leftZ = 0f, rightX = -14f + sway * 4f, rightZ = 0f, head = sway * 2f, k = 16f;
        _tilt = Quaternion.Euler(7f * move, 0f, sway * 2f);
        _shift = Vector3.up * (Mathf.Sin(t * 2.4f) * .04f);
        switch (anim)
        {
            case MonsterAnim.FirePrep:
            {
                float u = Mathf.Clamp01(st / MonsterRules.FireballPrepare), wind = Smooth((u - .82f) / .18f);
                // Rest geometry already has a bent elbow; the old 168-degree rotation
                // folded the casting hand back through the hat rather than winding up.
                rightX = Mathf.Lerp(-65f, -88f, wind) + Mathf.Sin(t * 31f) * 2f * u;
                rightZ = Mathf.Lerp(28f, 12f, wind);
                head = -6f;
                _fire = Mathf.Lerp(.15f, 1f, u);
                _tilt = Quaternion.Euler(-6f * u, 0f, 0f);
                break;
            }
            case MonsterAnim.FireCast:
            {
                float u = Mathf.Clamp01(st / .4f);
                rightX = Mathf.Lerp(-88f, -46f, Smooth(u / .3f)); rightZ = -8f; k = 36f;
                head = 7f;
                _fire = Mathf.Max(0f, .3f - u);
                _tilt = Quaternion.Euler(9f * (1f - u), 0f, 0f);
                break;
            }
            case MonsterAnim.ZapPrep:
            {
                float u = Mathf.Clamp01(st / .8f);
                leftX = rightX = Mathf.Lerp(-60f, -165f, Smooth(u)) + Mathf.Sin(t * 40f) * 3f;
                rightZ = 14f; leftZ = -14f;
                head = -12f * u;
                _volt = Mathf.Lerp(.2f, 1f, u);
                _tilt = Quaternion.Euler(-8f * u, 0f, 0f);
                break;
            }
            case MonsterAnim.Zap:
                leftX = rightX = -84f + Mathf.Sin(t * 37f) * 4f;
                rightZ = -10f; leftZ = 10f;
                head = 8f;
                _volt = .8f + .2f * Mathf.Sin(t * 29f);
                _tilt = Quaternion.Euler(6f, 0f, Mathf.Sin(t * 23f) * 1.5f);
                break;
        }
        if (stunned) { leftX = rightX = 15f; rightZ = 40f; leftZ = -40f; head = 20f; _shift = Vector3.zero; }
        Rot(_armL, leftX, 0f, leftZ, k);
        Rot(_armR, rightX, 0f, rightZ, k);
        Rot(_head, head, 0f, 0f);
        if (_fire > 0f) Glow(_handR, new Color(1f, .5f, .12f), 2.8f * _fire, 4f);
        else if (_volt > 0f) Glow(_handR, new Color(.66f, .55f, 1f), 2.6f * _volt, 4.5f);
    }

    // Raised-neck pitch per neck joint (tail0 next to the head ... tail5), as in the concept render.
    private static readonly float[] NeckRise = { 28f, 26f, 4f, -20f, -26f, -26f };

    // Basilisk: slithers low; before the lunge the neck rears up, the jaw opens in a hiss and the tail tip
    // rattles; the lunge is a straight dart; constricting, the body winds into a tightening ring. The bite is a
    // low sideways S with the jaw wide open.
    private void AnimateBasilisk(MonsterAnim anim, MonsterPose pose, float t, float st, bool stunned)
    {
        float wave = pose == MonsterPose.Flee ? 20f : 13f, rise = 0f, jaw = 0f, coil = 0f, bite = 0f, headYaw = 0f, headPitch = 0f, k = 14f;
        bool rattle = false;
        _eye = .65f;
        switch (anim)
        {
            case MonsterAnim.Rattle:
                rise = Smooth(st / .3f); jaw = 24f + 6f * Mathf.Sin(t * 20f); rattle = true; wave = 3f;
                headYaw = Mathf.Sin(t * 5f) * 12f * rise; headPitch = 6f;
                _eye = .8f + .2f * Mathf.Sin(t * 14f);
                break;
            case MonsterAnim.BitePrep:
                rise = .4f; bite = Smooth(st / .2f); jaw = 44f; wave = 2f; _eye = 1f;
                break;
            case MonsterAnim.Lunge:
                jaw = 48f; wave = 2f; k = 32f; headPitch = -4f; _eye = 1f;
                break;
            case MonsterAnim.Coil:
                coil = Smooth(st / .5f); rise = .5f; jaw = 14f; wave = 0f; _eye = .9f;
                break;
        }
        if (stunned) { wave = 0f; jaw = 10f; _eye = .2f; }
        float turn = 24f + 4f * Mathf.Sin(t * 6f); // the ring squeezes
        int n = _tail.Length;
        for (int i = 0; i < n; i++)
        {
            var j = _tail[i];
            if (j == null || i == BasiliskRoot) continue;
            bool neck = i < BasiliskRoot;
            int d = neck ? BasiliskRoot - i : i - BasiliskRoot;
            float yaw = wave * Mathf.Sin(_phase * 1.5f - d * .8f) * (neck ? -.6f : 1f), pitch = 0f;
            if (neck)
            {
                if (i < NeckRise.Length) pitch = -NeckRise[i] * rise;
                yaw += bite * (i == 5 ? 40f : i == 3 ? -75f : i == 1 ? 35f : 0f);
            }
            // A ring: the neck turns one way, the body behind the root the other (it points backwards).
            yaw = Mathf.Lerp(yaw, neck ? turn : -turn * (1f + d * .07f), coil);
            if (rattle && i >= n - 3) yaw += Mathf.Sin(t * 48f) * 16f;
            if (rattle && (i == n - 4 || i == n - 5)) pitch += 14f;
            Rot(j, pitch, yaw, 0f, k);
        }
        Rot(_head, headPitch, headYaw, 0f, k);
        Rot(_jaw, jaw, 0f, 0f, 26f);
        // Constricting, the brain sits on the victim: slide the model so the ring closes around that spot.
        if (coil > 0f) _shift = new Vector3(-.48f, 0f, .2f * (BasiliskRoot + .5f)) * (_modelScale * coil);
    }

    // Gorgon: lowers its head and paws the floor while it roars, gallops head-down in the charge, lifts its
    // head with burning eyes for the Death Stare, and shakes a dazed head after a wall.
    private void AnimateGorgon(MonsterAnim anim, float t, float st, float move, bool stunned)
    {
        float head = Mathf.Sin(t * 2f) * 3f, headYaw = 0f, jaw = 0f;
        bool gallop = false, paw = false;
        _eye = .2f;
        switch (anim)
        {
            case MonsterAnim.Acquire:
                head = 16f * Mathf.Clamp01(st / .6f); _eye = .45f;
                break;
            case MonsterAnim.Roar:
                head = 30f + Mathf.Sin(t * 26f) * 2f; headYaw = Mathf.Sin(t * 22f) * 5f; jaw = 30f + Mathf.Sin(t * 30f) * 4f;
                paw = true; _eye = .6f;
                _tilt = Quaternion.Euler(4f, 0f, Mathf.Sin(t * 38f) * 1.3f);
                break;
            case MonsterAnim.Charge:
                head = 34f; gallop = true; _eye = .7f;
                _tilt = Quaternion.Euler(6f + Mathf.Sin(_phase * 2f) * 3f, 0f, 0f);
                break;
            case MonsterAnim.Stare:
            {
                float u = Mathf.Clamp01(st / MonsterRules.StareSeconds);
                head = -16f; jaw = 6f;
                _eye = Mathf.Lerp(.5f, 1f, u) * (.85f + .15f * Mathf.Sin(t * (8f + 10f * u)));
                _tilt = Quaternion.Euler(-3f, 0f, Mathf.Sin(t * 30f) * u * .8f);
                Glow(_head != null ? _head.T : null, new Color(1f, .2f, .05f), 3.2f * u, 5f);
                break;
            }
            case MonsterAnim.Daze:
                head = 22f; headYaw = Mathf.Sin(t * 5f) * 20f; jaw = 14f; _eye = .08f;
                break;
        }
        if (stunned) { head = 24f; headYaw = Mathf.Sin(t * 5f) * 20f; jaw = 14f; _eye = .05f; }
        foreach (var (leg, left, front) in _legs)
        {
            float phase = gallop ? (front ? 0f : 2.2f) + (left ? .35f : 0f) : (left ^ front) ? 0f : Mathf.PI;
            float swing = Mathf.Sin(_phase * 2f + phase) * (gallop ? 44f : 30f * move);
            if (paw && front && left) swing = -30f + 28f * Mathf.Sin(t * 15f); // scraping the floor
            Rot(leg, swing, 0f, stunned ? (left ? -9f : 9f) : 0f, gallop || paw ? 30f : 16f);
        }
        Rot(_head, head, headYaw, 0f, 12f);
        Rot(_jaw, jaw, 0f, 0f, 20f);
    }

    // Naga Queen. Combo hit 1: the upper right blade rears back and chops down. Hit 2: all four blades swing
    // level in one sweep from left to right. Bleeding cut: the lower pair crosses low in a red glow, then rips
    // apart. Petrifying gaze: arms open wide, she rises and her eyes flare. After the combo the arms hang.
    private void AnimateNaga(MonsterAnim anim, float t, float st, float move, bool stunned)
    {
        float sway = Mathf.Sin(t * 2f), head = sway * 2f, tail = 5f;
        var ul = Quaternion.Euler(sway * 5f, 0f, 0f); var ur = Quaternion.Euler(-sway * 5f, 0f, 0f);
        var ll = Quaternion.Euler(0f, sway * 6f, 0f); var lr = Quaternion.Euler(0f, -sway * 6f, 0f);
        var droopUl = Quaternion.Euler(60f, 0f, 25f); var droopUr = Quaternion.Euler(60f, 0f, -25f);
        var droopLl = Quaternion.Euler(35f, 0f, 15f); var droopLr = Quaternion.Euler(35f, 0f, -15f);
        float kUpper = 16f, kLower = 16f;
        _eye = .15f;
        _tilt = Quaternion.Euler(4f * move, 0f, 0f);
        switch (anim)
        {
            case MonsterAnim.Combo1:
            {
                bool hit = st >= .17f;
                ur = hit ? Quaternion.Euler(88f, -18f, 0f) : Quaternion.Euler(-50f, 10f, 0f);
                ul = Quaternion.Euler(-20f, 0f, 0f);
                kUpper = hit ? 40f : 24f; head = 6f;
                _tilt = Quaternion.Euler(hit ? 9f : -4f, hit ? -8f : 6f, 0f);
                break;
            }
            case MonsterAnim.Combo2:
            {
                bool hit = st >= .2f;
                ul = Quaternion.Euler(78f, hit ? 115f : -40f, 0f); ll = Quaternion.Euler(10f, hit ? 120f : -45f, 0f);
                ur = Quaternion.Euler(78f, hit ? 40f : -80f, 0f); lr = Quaternion.Euler(10f, hit ? 45f : -75f, 0f);
                kUpper = kLower = hit ? 30f : 26f; head = 4f;
                _tilt = Quaternion.Euler(5f, hit ? 22f : -18f, 0f);
                break;
            }
            case MonsterAnim.BleedCut:
            {
                float u = Mathf.Clamp01(st / .6f);
                bool cut = u >= .78f;
                ll = cut ? Quaternion.Euler(25f, -50f, 0f) : Quaternion.Euler(-18f, 78f, 0f);
                lr = cut ? Quaternion.Euler(25f, 50f, 0f) : Quaternion.Euler(-18f, -78f, 0f);
                ul = Quaternion.Euler(-35f, -15f, 0f); ur = Quaternion.Euler(-35f, 15f, 0f);
                kLower = cut ? 45f : 14f; head = 10f;
                _tilt = Quaternion.Euler(cut ? 10f : -6f * u, 0f, 0f);
                Glow(null, new Color(1f, .08f, .05f), cut ? 4f : 2.6f * u, 4f);
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
                ul = droopUl; ur = droopUr; ll = droopLl; lr = droopLr;
                kUpper = kLower = 6f; head = 20f; _eye = .05f;
                _tilt = Quaternion.Euler(8f, 0f, sway * 3f);
                break;
        }
        if (stunned) { ul = droopUl; ur = droopUr; ll = droopLl; lr = droopLr; head = 22f; _eye = .03f; }
        Rot(_nagaArms[0], ul, kUpper); Rot(_nagaArms[1], ll, kLower);
        Rot(_nagaArms[2], ur, kUpper); Rot(_nagaArms[3], lr, kLower);
        Rot(_head, head, 0f, 0f, 10f);
        for (int i = 0; i < _tail.Length; i++)
            Rot(_tail[i], 0f, (i < 4 ? tail * .35f : tail) * Mathf.Sin(_phase * .9f - i * .55f), 0f, 10f);
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
        // Witch: flame in the right palm, sparks on both hands with an arc between them.
        if (_orbR != null)
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
            _light.transform.position = _lightAt != null ? _lightAt.position : transform.position + Vector3.up * (_height * .55f) + transform.forward * .45f;
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
