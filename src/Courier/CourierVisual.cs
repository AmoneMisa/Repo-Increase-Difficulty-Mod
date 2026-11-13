using System.Collections.Generic;
using IncreaseHardMod.Visuals;
using UnityEngine;

namespace IncreaseHardMod.Courier;

[System.Flags]
internal enum CourierFlags : byte
{
    None = 0, Happy = 1, Carrying = 2, Flying = 4, Down = 8, Dashing = 16, Pointing = 32, Hit = 64,
    /// <summary>With Carrying: held out in front in both hands (a soul), not overhead.</summary>
    InFront = 128,
}

/// <summary>The face the host wants shown; the pet also blinks and dozes off on its own when idle.</summary>
internal enum CourierMood : byte { Neutral = 0, Happy = 1, Focus = 2, Surprised = 3, Sad = 4, Sleepy = 5 }

/// <summary>
/// One courier pet as seen by every player: the animal model (tools/build_courier.py), its recolour materials and
/// its procedural animations (idle, run, petted, fly, carry). It has no colliders, so nothing in the game treats it as
/// a player or an object: monsters ignore it and it cannot block anyone. The host's state drives where it is.
/// </summary>
internal sealed class CourierVisual : MonoBehaviour
{
    private static Material _template;
    private static Sprite _heart;

    private int _look = -1;
    private Transform _model, _body, _eyesOpen, _eyesHappy, _eyesWide, _eyesSleepy, _browsSad, _browsFocus, _armL, _armR, _footL, _footR, _tail;
    private Vector3 _armLRest, _armRRest, _footLRest, _footRRest, _tailRest;
    private readonly Dictionary<string, Material> _channels = new();
    private readonly List<Mesh> _meshes = new();
    private readonly List<Material> _materials = new();
    private readonly List<(Material material, int slot, Color natural, bool glow)> _tints = new();

    // Smoothed state
    private Vector3 _target, _last;
    private const float SoulArmLift = 105f; // arms reaching forward and up, under the soul
    private float _yawTarget, _yaw, _speed, _phase, _fly, _carry, _front, _point, _happy, _petBounce, _nextStep, _nextHeart;
    private CourierFlags _flags;
    private CourierMood _mood;
    private float _idleFor, _nextBlink, _blinkUntil, _hit;
    private bool _placed;
    private Cosmetics.CosmeticMotion _secondary;
    private float _tailAngle, _tailVelocity;
    private readonly List<(Transform t, float born)> _hearts = new();

    private CourierDanger _danger;
    internal int Look => _look;

    /// <summary>Where a player aims to pet it (the middle of the body, up in the air when flying).</summary>
    internal Vector3 Centre => transform.position + Vector3.up * (.3f + _fly * .45f);

    internal static CourierVisual Create(int look)
    {
        var go = new GameObject("IHM Courier");
        var visual = go.AddComponent<CourierVisual>();
        visual._danger = go.AddComponent<CourierDanger>();
        visual.SetLook(look);
        return visual;
    }

    internal void SetLook(int look)
    {
        CourierRules.UnpackLook(look, out int animal, out int main, out int second, out int accent);
        int previousAnimal = _look < 0 ? -1 : (_look & 15);
        _look = CourierRules.PackLook(animal, main, second, accent);
        if (animal != previousAnimal) Build(animal);
        Recolour(main, second, accent);
    }

    // ------------------------------------------------------------------ model

    private void Build(int animal)
    {
        if (_model != null) { _model.gameObject.SetActive(false); Destroy(_model.gameObject); }
        foreach (var m in _meshes) Destroy(m);
        foreach (var m in _materials) Destroy(m);
        _meshes.Clear(); _materials.Clear(); _channels.Clear(); _tints.Clear();
        _shown = (CourierMood)255;
        _tailAngle = _tailVelocity = 0f;

        _template ??= ItemArt.WeaponMaterialDonor(null);
        var key = "courier_" + CourierLook.Keys[animal];
        var model = ItemArt.LoadModel(key);
        _model = new GameObject("Model").transform;
        _model.SetParent(transform, false);

        var pivots = new Dictionary<string, Vector3>();
        if (model.pivots != null) foreach (var p in model.pivots) pivots[p.name] = p.position;
        Transform Group(string name, Transform parent)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            var world = pivots.TryGetValue(name, out var p) ? p : Vector3.zero;
            // Groups are placed in model space; parts keep model-space vertices, offset by the pivot.
            t.localPosition = world - (parent == _body && pivots.TryGetValue("body", out var bodyPivot) ? bodyPivot : Vector3.zero);
            return t;
        }
        _body = Group("body", _model);
        _eyesOpen = Group("eyes_open", _body);
        _eyesHappy = Group("eyes_happy", _body);
        _eyesWide = Group("eyes_wide", _body); _eyesSleepy = Group("eyes_sleepy", _body);
        _browsSad = Group("brows_sad", _body); _browsFocus = Group("brows_focus", _body);
        _armL = Group("arm_l", _body); _armR = Group("arm_r", _body); _tail = Group("tail", _body);
        _footL = Group("foot_l", _model); _footR = Group("foot_r", _model);
        _armLRest = _armL.localPosition; _armRRest = _armR.localPosition; _tailRest = _tail.localPosition;
        _footLRest = _footL.localPosition; _footRRest = _footR.localPosition;
        var groups = new Dictionary<string, Transform>
        {
            ["body"] = _body, ["eyes_open"] = _eyesOpen, ["eyes_happy"] = _eyesHappy, ["eyes_wide"] = _eyesWide,
            ["eyes_sleepy"] = _eyesSleepy, ["brows_sad"] = _browsSad, ["brows_focus"] = _browsFocus, ["arm_l"] = _armL, ["arm_r"] = _armR,
            ["foot_l"] = _footL, ["foot_r"] = _footR, ["tail"] = _tail,
        };

        var motion = _secondary = _model.gameObject.AddComponent<Cosmetics.CosmeticMotion>();
        foreach (string name in new[] { "ear_l", "ear_r", "wing_l", "wing_r", "scarf" })
            if (pivots.ContainsKey(name))
            {
                var node = Group(name, _body); groups[name] = node; motion.Add(node, name);
            }
        foreach (var part in model.parts)
        {
            var bits = part.name.Split('|');
            string group = bits.Length > 0 ? bits[0] : "body", channel = bits.Length > 1 ? bits[1] : "fixed";
            if (!groups.TryGetValue(group, out var parent)) parent = _body;
            var obj = new GameObject(part.name);
            obj.transform.SetParent(parent, false);
            obj.transform.localPosition = -PivotOf(parent, pivots);
            var mesh = new Mesh { name = "IHM " + key + " " + part.name,
                indexFormat = part.vertices.Length > 65535 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16,
                vertices = part.vertices, normals = part.normals, uv = part.uv, triangles = part.triangles };
            mesh.RecalculateBounds();
            mesh.RecalculateTangents(); // surface-normal maps need the exported UV tangent frame
            _meshes.Add(mesh);
            obj.AddComponent<MeshFilter>().sharedMesh = mesh;
            int slot = channel == "main" ? 0 : channel == "second" ? 1 : channel == "accent" ? 2 : -1;
            bool eyes = group.StartsWith("eyes_", System.StringComparison.Ordinal);
            // "glow" surfaces (an elemental's eyes and runes, a phoenix's flames) are lit from within.
            bool glow = bits.Length > 2 && bits[2].StartsWith("glow", System.StringComparison.Ordinal);
            string materialKey = channel + ":" + ColorUtility.ToHtmlStringRGB(part.color) + ":" + part.texture + (eyes ? ":eye" : "") + (glow ? ":glow" : "");
            if (!_channels.TryGetValue(materialKey, out var material))
            {
                material = ItemArt.Paint(_template, string.IsNullOrEmpty(part.texture) ? null : part.texture, part.color, "IHM Courier " + materialKey);
                if (!string.IsNullOrEmpty(part.texture))
                {
                    var texture = Upgrades.UpgradeIcons.LoadTexture(part.texture);
                    if (texture != null) { texture.filterMode = FilterMode.Bilinear; texture.wrapMode = TextureWrapMode.Clamp; }
                }
                CourierSurface.Apply(material, key, eyes, !string.IsNullOrEmpty(part.texture));
                _materials.Add(material);
                _channels[materialKey] = material;
                if (glow) Glow(material, part.color);
                if (slot >= 0) _tints.Add((material, slot, part.color, glow));
            }
            var renderer = obj.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        }
        ShowFace(CourierMood.Neutral);
    }

    private CourierMood _shown = (CourierMood)255;

    private void ShowFace(CourierMood mood)
    {
        if (mood == _shown || _eyesOpen == null) return;
        _shown = mood;
        _eyesOpen.gameObject.SetActive(mood is CourierMood.Neutral or CourierMood.Focus or CourierMood.Sad);
        _eyesHappy.gameObject.SetActive(mood == CourierMood.Happy);
        _eyesWide.gameObject.SetActive(mood == CourierMood.Surprised);
        _eyesSleepy.gameObject.SetActive(mood == CourierMood.Sleepy);
        _browsSad.gameObject.SetActive(mood == CourierMood.Sad);
        _browsFocus.gameObject.SetActive(mood == CourierMood.Focus);
    }

    // A part's vertices are in model space; under a group placed at its pivot it moves back by the pivot.
    private Vector3 PivotOf(Transform group, Dictionary<string, Vector3> pivots)
    {
        if (group == _body || group.parent == _body && !pivots.ContainsKey(group.name)) return Vector3.zero;
        return pivots.TryGetValue(group.name, out var p) ? p : Vector3.zero;
    }

    private void Recolour(int main, int second, int accent)
    {
        int[] slots = { main, second, accent };
        foreach (var tint in _tints)
            {
                var m = tint.material;
                var c = CourierLook.Resolve(slots[tint.slot], tint.natural);
                if (m.HasProperty("_Color")) m.SetColor("_Color", c);
                if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
                if (m.HasProperty("_AlbedoColor")) m.SetColor("_AlbedoColor", c);
                if (tint.glow) Glow(m, c);
            }
    }

    private static void Glow(Material material, Color color)
    {
        if (!material.HasProperty("_EmissionColor")) return;
        material.EnableKeyword("_EMISSION");
        material.SetColor("_EmissionColor", color * 1.6f);
    }

    // ------------------------------------------------------------------ state from the host

    internal void Drive(Vector3 position, float yaw, CourierFlags flags, CourierMood mood)
    {
        if (mood == CourierMood.Surprised && _mood != CourierMood.Surprised) Cue("call", .35f);
        _mood = mood;
        if (!_placed) { _placed = true; transform.position = _last = position; _yaw = yaw; }
        if ((flags & CourierFlags.Happy) != 0 && (_flags & CourierFlags.Happy) == 0) PetReaction();
        if ((flags & CourierFlags.Down) != 0 && (_flags & CourierFlags.Down) == 0) Poof();
        if ((flags & CourierFlags.Down) == 0 && (_flags & CourierFlags.Down) != 0) { transform.position = position; _last = position; Poof(); }
        _target = position; _yawTarget = yaw; _flags = flags;
    }

    /// <summary>Plays the pet reaction at once on the petting player's screen (the host confirms it for everyone).</summary>
    internal void PetReaction()
    {
        if (_petBounce > .5f) { _petBounce = 1f; return; } // still reacting: just keep it going
        _petBounce = 1f;
        _nextHeart = 0f;
        Cue("happy" + Random.Range(1, 4), .65f); // three petting sounds per animal
        var hearts = Hearts();
        if (hearts != null) { hearts.transform.position = Centre + Vector3.up * .25f; hearts.Emit(30); }
    }

    private void Poof()
    {
        try { ItemAudio.Cue("courier_poof", .45f).Play(transform.position); } catch { }
        for (int i = 0; i < 4; i++) SpawnHeart(new Color(.8f, .8f, .85f), .5f);
    }

    private void Cue(string what, float volume)
    {
        if (_look < 0) return;
        try { ItemAudio.Cue("courier_" + CourierLook.Voice(_look & 15) + "_" + what, volume).Play(transform.position); } catch { }
    }

    internal void Call() => Cue("call", .4f);

    // ------------------------------------------------------------------ animation

    private void Update()
    {
        if (_model == null) return;
        float dt = Mathf.Min(Time.deltaTime, .1f);
        bool down = (_flags & CourierFlags.Down) != 0;
        _model.gameObject.SetActive(!down);
        if (_danger != null) { _danger.Suppressed = down; _danger.Height = .72f + _fly * .45f; }
        if (down) { TickHearts(dt); return; }

        transform.position = Vector3.Lerp(transform.position, _target, 1f - Mathf.Exp(-12f * dt));
        _yaw = Mathf.LerpAngle(_yaw, _yawTarget, 1f - Mathf.Exp(-10f * dt));
        transform.rotation = Quaternion.Euler(0f, _yaw, 0f);
        var moved = transform.position - _last; moved.y = 0f;
        _last = transform.position;
        float speed = dt > 0f ? moved.magnitude / dt : 0f;
        _speed = Mathf.Lerp(_speed, speed, 1f - Mathf.Exp(-8f * dt));

        bool flying = (_flags & CourierFlags.Flying) != 0;
        bool carrying = (_flags & CourierFlags.Carrying) != 0;
        bool happy = (_flags & CourierFlags.Happy) != 0 || _petBounce > 0f;
        _fly = Mathf.MoveTowards(_fly, flying ? 1f : 0f, dt * 2.5f);
        _carry = Mathf.MoveTowards(_carry, carrying ? 1f : 0f, dt * 4f);
        _point = Mathf.MoveTowards(_point, (_flags & CourierFlags.Pointing) != 0 ? 1f : 0f, dt * 5f);
        _front = Mathf.MoveTowards(_front, (_flags & CourierFlags.InFront) != 0 ? 1f : 0f, dt * 4f);
        _happy = Mathf.MoveTowards(_happy, happy ? 1f : 0f, dt * 5f);
        _petBounce = Mathf.Max(0f, _petBounce - dt / 1.6f);

        float run = Mathf.Clamp01(_speed / 3f) * (1f - _fly);
        float cadence = Mathf.Lerp(9f, 15f, Mathf.Clamp01(_speed / 6f));
        _phase += dt * cadence * Mathf.Max(run, .0001f);
        float t = Time.time;

        // Body: waddle and bounce when running, breathing at rest, hover when flying, squash when petted.
        float breathe = Mathf.Sin(t * 2.2f) * .012f;
        float bounce = Mathf.Abs(Mathf.Sin(_phase)) * .035f * run;
        float hover = _fly * (.45f + Mathf.Sin(t * 3f) * .05f);
        float squash = _happy * Mathf.Sin(_petBounce * Mathf.PI * 4f) * .06f;
        // The stone elemental has no legs: it always floats a little off the floor.
        float levitate = (_look & 15) == 14 ? .1f + Mathf.Sin(t * 1.7f) * .025f : 0f;
        _model.localPosition = new Vector3(0f, hover + levitate, 0f);
        _body.localPosition = new Vector3(0f, bounce + _happy * .02f, 0f);
        _body.localScale = new Vector3(1f + squash * .5f, 1f + breathe - squash, 1f + squash * .5f);
        int animal = _look & 15;
        bool bird = animal is 0 or 1 or 3 or 15;
        float roll = Mathf.Sin(_phase) * (bird ? 7f : 3.5f) * run + _happy * Mathf.Sin(t * 9f) * 4f;
        float lean = run * 10f + _fly * 14f - _carry * Mathf.Lerp(4f, -3f, _front) + _point * 6f; // a soul out in front tips it forward a little
        // Knocked back by a monster: a quick tumble.
        _hit = (_flags & CourierFlags.Hit) != 0 ? Mathf.Min(1f, _hit + dt * 6f) : Mathf.Max(0f, _hit - dt * 3f);
        _body.localRotation = Quaternion.Euler(lean - _hit * 35f, _hit * Mathf.Sin(t * 30f) * 25f, roll + _hit * 20f);

        // Arms: swing when running, flap when flying, raised to hold a parcel overhead (or reaching forward to hold a
        // soul out in front), waving when petted; pointing out a monster, the right arm reaches straight out ahead.
        float swing = Mathf.Sin(_phase) * 35f * run;
        float flap = _fly * Mathf.Sin(t * 22f) * 55f;
        float wave = _happy * (25f + Mathf.Sin(t * 14f) * 25f);
        float lift = _carry * Mathf.Lerp(150f, SoulArmLift, _front);
        // The skeleton cat walks on all fours: its "arms" are front legs, so they stride but never lift a parcel or wave.
        if (animal == 13) { lift = 0f; flap *= .3f; wave *= .35f; }
        _armL.localRotation = Quaternion.Euler(-swing - lift, 0f, -(flap + wave) - _fly * 25f);
        float point = _point * (95f + Mathf.Sin(t * 10f) * 6f); // held out, with a little urgent jab
        _armR.localRotation = Quaternion.Euler(swing * (1f - _point) - lift - point, 0f, flap + wave + _fly * 25f);

        // Feet: alternate steps; tucked while flying.
        float stepL = Mathf.Max(0f, Mathf.Sin(_phase)) * .05f * run, stepR = Mathf.Max(0f, -Mathf.Sin(_phase)) * .05f * run;
        _footL.localPosition = _footLRest + new Vector3(0f, stepL + _fly * .05f, Mathf.Cos(_phase) * .045f * run);
        _footR.localPosition = _footRRest + new Vector3(0f, stepR + _fly * .05f, -Mathf.Cos(_phase) * .045f * run);
        _footL.localRotation = Quaternion.Euler(-_fly * 40f + Mathf.Sin(_phase) * 14f * run, 0f, 0f);
        _footR.localRotation = Quaternion.Euler(-_fly * 40f - Mathf.Sin(_phase) * 14f * run, 0f, 0f);

        // Tail: lazy sway, quick wag when happy.
        float tailTarget = Mathf.Sin(t * (2f + _happy * (animal == 7 ? 9f : 5f))) * (bird ? 4f : 9f + _happy * 18f);
        _tailAngle = Mathf.SmoothDampAngle(_tailAngle, tailTarget, ref _tailVelocity, .10f, 180f, dt);
        _tail.localRotation = Quaternion.Euler(run * 4f, _tailAngle, 0f);
        if (_secondary != null) { _secondary.Activity = run; _secondary.Flutter = _fly; }

        // Face: petting wins, then the host's mood; idle pets blink and, after a while, doze off.
        _idleFor = _speed < .15f && _mood == CourierMood.Neutral && !carrying ? _idleFor + dt : 0f;
        if (Time.time >= _nextBlink) { _nextBlink = Time.time + Random.Range(2.5f, 5f); _blinkUntil = Time.time + .12f; }
        var face = _happy >= .5f ? CourierMood.Happy
            : _mood != CourierMood.Neutral ? _mood
            : _idleFor > 14f || Time.time < _blinkUntil ? CourierMood.Sleepy
            : CourierMood.Neutral;
        ShowFace(face);
        if (face == CourierMood.Sleepy && _idleFor > 14f) _body.localRotation *= Quaternion.Euler(6f + Mathf.Sin(t * 1.2f) * 3f, 0f, 0f); // nodding off

        if (!SemiFunc.RunIsLevel() && run > .3f && _fly < .5f && Time.time >= _nextStep)
        {
            _nextStep = Time.time + Mathf.PI / cadence;
            try { ItemAudio.Cue("courier_step", .12f).Play(transform.position); } catch { }
        }
        if (_happy > .5f && Time.time >= _nextHeart && Hearts() == null) // drawn hearts only when Elsa's are unavailable
        {
            _nextHeart = Time.time + .35f;
            SpawnHeart(new Color(1f, .35f, .65f), 1f);
        }
        TickHearts(dt);
    }

    // ------------------------------------------------------------------ hearts

    // The game's own petting hearts: a copy of Elsa's heart particles (EnemyElsaAnim.petHeartParticles).
    private static ParticleSystem _elsaHearts;
    private static bool _searchedElsa;
    private ParticleSystem _elsaCopy;

    private ParticleSystem Hearts()
    {
        if (_elsaCopy != null) return _elsaCopy;
        var source = ElsaHearts();
        if (source == null) return null;
        _elsaCopy = Instantiate(source.gameObject).GetComponent<ParticleSystem>();
        _elsaCopy.gameObject.name = "IHM Courier Hearts";
        _elsaCopy.transform.SetParent(transform, true);
        _elsaCopy.gameObject.SetActive(true);
        var main = _elsaCopy.main; main.simulationSpace = ParticleSystemSimulationSpace.World; main.playOnAwake = false;
        _elsaCopy.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        return _elsaCopy;
    }

    private static ParticleSystem ElsaHearts()
    {
        if (_elsaHearts != null || _searchedElsa) return _elsaHearts;
        var director = EnemyDirector.instance;
        if (director == null) return null;
        _searchedElsa = true;
        foreach (var list in new[] { director.enemiesDifficulty1, director.enemiesDifficulty2, director.enemiesDifficulty3 })
        {
            if (list == null) continue;
            foreach (var setup in list)
            {
                if (setup == null || setup.spawnObjects == null) continue;
                foreach (var prefab in setup.spawnObjects)
                {
                    var go = prefab != null ? prefab.Prefab : null;
                    var anim = go != null ? go.GetComponentInChildren<EnemyElsaAnim>(true) : null;
                    if (anim != null && anim.petHeartParticles != null) return _elsaHearts = anim.petHeartParticles;
                }
            }
        }
        Plugin.Log.LogInfo("[Courier] Elsa's heart particles not found; using drawn hearts.");
        return null;
    }

    private static Sprite Heart => _heart ??= Hud.HudIcons.Silhouette("courier_heart", (u, v) =>
    {
        float x = u * 1.25f, y = (.55f - v) * 2.6f + .1f;
        float a = x * x + y * y - 1f;
        return a * a * a - x * x * y * y * y <= 0f;
    });

    private void SpawnHeart(Color color, float scale)
    {
        var go = new GameObject("IHM Courier Heart");
        go.transform.position = transform.position + Vector3.up * (.62f + _fly * .45f) + Random.insideUnitSphere * .12f;
        go.transform.localScale = Vector3.one * .09f * scale;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = Heart; sr.color = color;
        _hearts.Add((go.transform, Time.time));
    }

    private void TickHearts(float dt)
    {
        var cam = Camera.main;
        for (int i = _hearts.Count - 1; i >= 0; i--)
        {
            var (h, born) = _hearts[i];
            float age = Time.time - born;
            if (h == null || age > 1.2f) { if (h != null) Destroy(h.gameObject); _hearts.RemoveAt(i); continue; }
            h.position += Vector3.up * dt * .45f;
            if (cam != null) h.rotation = cam.transform.rotation;
            var sr = h.GetComponent<SpriteRenderer>();
            var c = sr.color; c.a = 1f - age / 1.2f; sr.color = c;
        }
    }

    private void OnDestroy()
    {
        foreach (var (h, _) in _hearts) if (h != null) Destroy(h.gameObject);
        foreach (var m in _meshes) Destroy(m);
        foreach (var m in _materials) Destroy(m);
    }
}
