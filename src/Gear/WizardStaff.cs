using System.Collections.Generic;
using IncreaseHardMod.Patches.Effects;
using IncreaseHardMod.Visuals;
using Photon.Pun;
using UnityEngine;

namespace IncreaseHardMod.Gear;

/// <summary>
/// Wizard Staff: a carved wand that throws chain lightning (built in <see cref="GearItems"/> from the
/// handgun, priced like the revolver, one battery bar per bolt).
///
/// Firing uses the native gun path end to end (trigger, battery bar, sound, aim; a melee-like strike replaces
/// the recoil kick). Only the bullet
/// is replaced: <see cref="GearEffects"/> skips the native ShootBulletRPC for this kind and calls
/// <see cref="HostFire"/> on the host, which picks the targets, deals the damage and broadcasts the bolt
/// path; every client then draws the same jagged bolt.
///
/// Targets: the monster on the aim line within range, otherwise the closest one inside a small aim-assist
/// cone. The bolt then jumps to the nearest monster not yet struck within the chain range, up to the
/// configured number of targets. Walls block every jump. The damage is split between the struck monsters.
///
/// Players: a bolt aimed straight at a teammate (nothing in between) strikes them and chains to nearby players
/// instead, at <see cref="ModConfig.StaffPlayerDamagePercent"/> of the damage. A bolt that hit a monster first never
/// jumps to players. Everything struck is <see cref="Shock">shocked</see>.
/// </summary>
internal sealed class WizardStaff : MonoBehaviour
{
    internal static Material BoltMaterial;
    private static readonly Color Core = new(.85f, .93f, 1f), Glow = new(.35f, .6f, 1f);
    private const float BoltSeconds = .28f;

    private ItemGun _gun;
    private PhotonView _view;
    private Sound _hit;
    private readonly List<LineRenderer> _lines = new();
    private readonly List<Light> _lights = new();
    private Vector3[] _points;
    private float _timer, _rejag;

    private void Start()
    {
        _gun = GetComponent<ItemGun>();
        _view = GetComponent<PhotonView>();
        try { _hit = ItemAudio.Cue("lightning_hit", .45f); } catch { _hit = null; }
    }

    // ------------------------------------------------------------------ host

    // Cast motion, like a pan swing rather than a gun's recoil: the tip dips forward and down and the
    // whole staff jabs forward a little; the native aim torque then brings it back to the crosshair.
    // Host-side physics, so every client sees the same motion.
    private const float StrikeDip = .55f, StrikeJab = .35f;

    private void Strike()
    {
        var rb = _gun.physGrabObject != null ? _gun.physGrabObject.rb : null;
        if (rb == null || rb.isKinematic) return;
        var muzzle = _gun.gunMuzzle;
        var down = Vector3.ProjectOnPlane(-transform.up, muzzle.forward).normalized;
        rb.AddForceAtPosition(down * StrikeDip, muzzle.position, ForceMode.Impulse);
        rb.AddForce(muzzle.forward * StrikeJab, ForceMode.Impulse);
    }

    // The native empty click also kicks the tip back and up; cancel it exactly for the staff.
    [HarmonyLib.HarmonyPatch(typeof(ItemGun), "DryFireRPC")]
    private static class NoDryFireKick
    {
        [HarmonyLib.HarmonyPostfix]
        private static void Postfix(ItemGun __instance)
        {
            if (!SemiFunc.IsMasterClientOrSingleplayer() || __instance == null || __instance.gunMuzzle == null ||
                __instance.GetComponent<WizardStaff>() == null) return;
            var rb = __instance.physGrabObject != null ? __instance.physGrabObject.rb : null;
            // ItemGun applies -forward * 1 at the muzzle right after this RPC runs locally; this is its opposite.
            if (rb != null && !rb.isKinematic)
                rb.AddForceAtPosition(__instance.gunMuzzle.forward * 1f, __instance.gunMuzzle.position, ForceMode.Impulse);
        }
    }

    internal void HostFire(Vector3 aimEnd, int shooterId)
    {
        if (!SemiFunc.IsMasterClientOrSingleplayer() || _gun == null || _gun.gunMuzzle == null) return;
        Strike();
        var origin = _gun.gunMuzzle.position;
        var forward = _gun.gunMuzzle.forward;
        var shooter = shooterId >= 0 ? SemiFunc.PlayerAvatarGetFromPhotonID(shooterId) : null;
        float range = ModConfig.StaffRange.Value;
        int max = Mathf.Max(1, ModConfig.StaffMaxTargets.Value);
        // Skill tree: Marksman + Wizard Staff Damage.
        int damage = Mathf.RoundToInt(ModConfig.StaffDamage.Value * Skills.SkillEffects.WeaponMultiplier(shooter, Skills.SkillEffects.Weapon.WizardStaff));

        var struck = new List<Enemy>();
        var points = new List<Vector3> { origin };
        float shock = Mathf.Max(0f, ModConfig.StaffShockSeconds.Value);
        // A teammate right on the aim line takes the bolt (and it chains through players only).
        var firstPlayer = FirstPlayer(origin, aimEnd, range, shooter);
        var first = firstPlayer == null ? FirstTarget(origin, forward, aimEnd, range, shooter) : null;
        if (firstPlayer != null)
        {
            var hitPlayers = new List<PlayerAvatar> { firstPlayer };
            points.Add(PlayerCenter(firstPlayer));
            while (hitPlayers.Count < max)
            {
                var next = NextPlayer(points[points.Count - 1], hitPlayers, shooter, ModConfig.StaffChainRange.Value);
                if (next == null) break;
                hitPlayers.Add(next);
                points.Add(PlayerCenter(next));
            }
            float percent = Mathf.Clamp(ModConfig.StaffPlayerDamagePercent.Value, 0f, 100f) / 100f;
            int share = damage / hitPlayers.Count, extra = damage % hitPlayers.Count;
            for (int i = 0; i < hitPlayers.Count; i++)
            {
                var p = hitPlayers[i];
                int hurt = Mathf.RoundToInt((share + (i < extra ? 1 : 0)) * percent);
                if (hurt > 0 && p.playerHealth != null) p.playerHealth.HurtOther(hurt, points[i + 1], false);
                if (shock > 0f) Networking.ModNetwork.BroadcastPlayerStatus(p, Networking.ModNetwork.StatusShocked, shock);
            }
            Plugin.Log.LogInfo($"[Staff] Bolt struck {hitPlayers.Count} player(s).");
        }
        else if (first != null)
        {
            struck.Add(first);
            points.Add(Center(first));
            while (struck.Count < max)
            {
                var next = NextTarget(points[points.Count - 1], struck, ModConfig.StaffChainRange.Value);
                if (next == null) break;
                struck.Add(next);
                points.Add(Center(next));
            }
            // The bolt's damage is shared: split evenly, with any remainder going to the earliest targets
            // (75 → 75 alone, 38 + 37 for two, 25 each for three). Every target takes at least 1.
            int share = damage / struck.Count, extra = damage % struck.Count;
            for (int i = 0; i < struck.Count; i++)
            {
                var direction = points[i + 1] - points[i];
                QuestEffect.NoteGunHit(struck[i]);
                int hurt = Mathf.Max(1, share + (i < extra ? 1 : 0));
                hurt = Mathf.Max(1, Mathf.RoundToInt(hurt * Skills.TradeOffs.TargetFactor(shooter, struck[i]))); // Executioner
                struck[i].Health.Hurt(hurt, direction.sqrMagnitude > .0001f ? direction.normalized : forward);
                if (shock > 0f) Networking.ModNetwork.BroadcastEnemyStatus(struck[i], Shock.EnemyKind, shock);
            }
        }
        else
        {
            // A miss still shows a short bolt to where the aim line ends (or the wall it hit).
            var end = aimEnd;
            if ((end - origin).sqrMagnitude > range * range || (end - origin).sqrMagnitude < .01f) end = origin + forward * range;
            points.Add(end);
        }

        int targets = Mathf.Max(struck.Count, points.Count - 1 - (struck.Count == 0 && firstPlayer == null ? 1 : 0));
        if (SemiFunc.IsMultiplayer()) _view.RPC(nameof(IHM_StaffBoltRPC), RpcTarget.All, points.ToArray(), targets);
        else Show(points.ToArray(), targets);
    }

    [PunRPC]
    private void IHM_StaffBoltRPC(Vector3[] points, int struck, PhotonMessageInfo info = default)
    {
        if (!SemiFunc.MasterOnlyRPC(info) || points == null || points.Length < 2 || points.Length > 21) return;
        Show(points, struck);
    }

    private static Vector3 PlayerCenter(PlayerAvatar player) =>
        player.PlayerVisionTarget != null && player.PlayerVisionTarget.VisionTransform != null
            ? player.PlayerVisionTarget.VisionTransform.position - Vector3.up * .3f : player.transform.position + Vector3.up * 1f;

    // The first thing on the aim line, when it is another living player (a wall or monster in front: no).
    private PlayerAvatar FirstPlayer(Vector3 origin, Vector3 aimEnd, float range, PlayerAvatar shooter)
    {
        var direction = aimEnd - origin;
        float distance = Mathf.Min(direction.magnitude + .3f, range);
        if (direction.sqrMagnitude < .0001f) return null;
        var hits = Physics.RaycastAll(origin, direction.normalized, distance,
            (int)SemiFunc.LayerMaskGetVisionObstruct() | LayerMask.GetMask("Player", "Enemy"), QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        foreach (var hit in hits)
        {
            var t = hit.collider.transform;
            if (t.IsChildOf(transform) || (shooter != null && t.IsChildOf(shooter.transform))) continue;
            if (hit.collider.GetComponentInParent<PhysGrabObject>() != null && hit.collider.GetComponentInParent<PlayerAvatar>() == null &&
                hit.collider.GetComponentInParent<PlayerTumble>() == null) continue; // held items and loot don't stop the bolt
            var player = hit.collider.GetComponentInParent<PlayerAvatar>() ?? hit.collider.GetComponentInParent<PlayerTumble>()?.playerAvatar;
            return player != null && player != shooter && !player.isDisabled ? player : null;
        }
        return null;
    }

    private PlayerAvatar NextPlayer(Vector3 from, List<PlayerAvatar> struck, PlayerAvatar shooter, float chainRange)
    {
        PlayerAvatar best = null;
        float bestDistance = chainRange;
        var players = SemiFunc.PlayerGetList();
        if (players == null) return null;
        foreach (var p in players)
        {
            if (p == null || p == shooter || p.isDisabled || struck.Contains(p)) continue;
            var to = PlayerCenter(p);
            float distance = Vector3.Distance(from, to);
            if (distance > bestDistance || Physics.Linecast(from, to, SemiFunc.LayerMaskGetVisionObstruct() & ~LayerMask.GetMask("Player"), QueryTriggerInteraction.Ignore)) continue;
            bestDistance = distance;
            best = p;
        }
        return best;
    }

    private static Vector3 Center(Enemy enemy) =>
        enemy.CenterTransform != null ? enemy.CenterTransform.position : enemy.transform.position + Vector3.up * .5f;

    private static bool Alive(Enemy enemy) =>
        enemy != null && enemy.isActiveAndEnabled && enemy.HasHealth && enemy.Health != null && !enemy.Health.dead && enemy.Health.healthCurrent > 0;

    private static IEnumerable<Enemy> Enemies()
    {
        if (EnemyDirector.instance == null || EnemyDirector.instance.enemiesSpawned == null) yield break;
        foreach (var parent in EnemyDirector.instance.enemiesSpawned)
        {
            var enemy = parent != null ? parent.Enemy : null;
            if (Alive(enemy)) yield return enemy;
        }
    }

    private Enemy FirstTarget(Vector3 origin, Vector3 forward, Vector3 aimEnd, float range, PlayerAvatar shooter)
    {
        // 1) Whatever monster the native aim ray actually hit.
        var direction = aimEnd - origin;
        if (direction.sqrMagnitude > .0001f && direction.magnitude <= range + .3f)
        {
            foreach (var hit in Physics.RaycastAll(origin, direction.normalized, direction.magnitude + .2f,
                         (int)SemiFunc.LayerMaskGetVisionObstruct() | LayerMask.GetMask("Enemy"), QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.transform.IsChildOf(transform) || (shooter != null && hit.collider.transform.IsChildOf(shooter.transform))) continue;
                var body = hit.collider.GetComponentInParent<EnemyRigidbody>();
                var enemy = body != null ? body.enemy : hit.collider.GetComponentInParent<Enemy>();
                if (Alive(enemy)) return enemy;
            }
        }
        // 2) Aim assist: the monster closest to the aim line inside a small cone.
        float cone = ModConfig.StaffAimAngle.Value;
        if (cone <= 0f) return null;
        Enemy best = null;
        float bestAngle = cone;
        foreach (var enemy in Enemies())
        {
            var to = Center(enemy) - origin;
            float distance = to.magnitude;
            if (distance > range || distance < .05f) continue;
            float angle = Vector3.Angle(forward, to);
            if (angle > bestAngle || Blocked(origin, Center(enemy), enemy)) continue;
            bestAngle = angle;
            best = enemy;
        }
        return best;
    }

    private Enemy NextTarget(Vector3 from, List<Enemy> struck, float chainRange)
    {
        Enemy best = null;
        float bestDistance = chainRange;
        foreach (var enemy in Enemies())
        {
            if (struck.Contains(enemy)) continue;
            float distance = Vector3.Distance(from, Center(enemy));
            if (distance > bestDistance || Blocked(from, Center(enemy), enemy)) continue;
            bestDistance = distance;
            best = enemy;
        }
        return best;
    }

    // Level geometry stops a jump; monsters, items and players in between do not.
    private bool Blocked(Vector3 from, Vector3 to, Enemy target)
    {
        var delta = to - from;
        float distance = delta.magnitude;
        if (distance < .3f) return false;
        foreach (var hit in Physics.RaycastAll(from, delta / distance, distance, SemiFunc.LayerMaskGetVisionObstruct(), QueryTriggerInteraction.Ignore))
        {
            if (hit.collider == null || hit.collider.transform.IsChildOf(transform)) continue;
            if (hit.collider.GetComponentInParent<Enemy>() != null || hit.collider.GetComponentInParent<EnemyRigidbody>() != null ||
                hit.collider.GetComponentInParent<EnemyParent>() == target.EnemyParent) continue;
            if (hit.collider.GetComponentInParent<PhysGrabObject>() != null || hit.collider.GetComponentInParent<PlayerAvatar>() != null) continue;
            if (hit.distance < distance - .35f) return true;
        }
        return false;
    }

    // ------------------------------------------------------------------ presentation (every client)

    private void Show(Vector3[] points, int struck)
    {
        _points = points;
        _timer = BoltSeconds;
        _rejag = 0f;
        EnsureVisuals(points.Length - 1);
        for (int i = 1; i < points.Length; i++)
        {
            if (i <= struck)
            {
                _hit?.Play(points[i]);
                LightningImpact.Spawn(points[i], Material());
            }
            var light = _lights[i - 1];
            light.transform.position = points[i];
            light.enabled = true;
        }
        if (struck > 0 && EnemyDirector.instance != null) EnemyDirector.instance.SetInvestigate(points[1], 8f);
    }

    private void Update()
    {
        if (_timer <= 0f) return;
        _timer -= Time.deltaTime;
        float fade = Mathf.Clamp01(_timer / BoltSeconds);
        bool on = _timer > 0f;
        _rejag -= Time.deltaTime;
        bool rejag = _rejag <= 0f;
        if (rejag) _rejag = .045f; // lightning flickers into a new shape every few frames
        int segments = _points.Length - 1;
        for (int i = 0; i < _lines.Count; i++)
        {
            var line = _lines[i];
            bool used = on && i < segments * 2;
            line.enabled = used;
            if (!used) continue;
            int s = i / 2;
            bool glow = i % 2 == 1;
            var start = s == 0 && _gun != null && _gun.gunMuzzle != null ? _gun.gunMuzzle.position : _points[s];
            if (rejag) Jag(line, start, _points[s + 1], glow ? .075f : .06f);
            float width = (glow ? .15f : .03f) * (.35f + .65f * fade);
            line.startWidth = width;
            line.endWidth = width * .7f;
            var c = glow ? Glow : Core;
            c.a = (glow ? .45f : 1f) * fade;
            line.startColor = line.endColor = c;
        }
        // Forks: thinner, fainter strands splitting off each segment's core, re-shaped with it.
        for (int i = 0; i < _branches.Count; i++)
        {
            var branch = _branches[i];
            int s = i / BranchesPerSegment;
            bool used = on && s < segments;
            branch.enabled = used && Random.value > .12f;
            if (!used) continue;
            if (rejag) Fork(branch, _lines[s * 2]);
            float width = .02f * (.35f + .65f * fade);
            branch.startWidth = width;
            branch.endWidth = width * .25f;
            var c = Core; c.a = .75f * fade;
            branch.startColor = c;
            c.a = 0f;
            branch.endColor = c;
        }
        for (int i = 0; i < _lights.Count; i++)
        {
            var light = _lights[i];
            light.enabled = on && i < segments;
            if (light.enabled) light.intensity = 3.5f * fade * (.6f + Mathf.PerlinNoise(Time.time * 40f, i) * .8f);
        }
    }

    private static void Jag(LineRenderer line, Vector3 a, Vector3 b, float amplitude)
    {
        var delta = b - a;
        float length = delta.magnitude;
        int count = Mathf.Clamp(Mathf.CeilToInt(length / .11f) + 1, 3, 48); // dense kinks, like real arcs
        line.positionCount = count;
        var side = Vector3.Cross(delta.normalized, Vector3.up);
        if (side.sqrMagnitude < .01f) side = Vector3.right;
        side.Normalize();
        var up = Vector3.Cross(side, delta.normalized);
        for (int i = 0; i < count; i++)
        {
            float t = i / (float)(count - 1);
            var p = Vector3.Lerp(a, b, t);
            if (i > 0 && i < count - 1)
            {
                float envelope = Mathf.Sin(t * Mathf.PI); // pinned at both ends
                p += (side * Random.Range(-1f, 1f) + up * Random.Range(-1f, 1f)) * amplitude * envelope * Mathf.Min(1f, length);
            }
            line.SetPosition(i, p);
        }
    }

    private const int BranchesPerSegment = 3;
    private readonly List<LineRenderer> _branches = new();

    // A fork leaves the core strand at a random kink and wanders off sideways, fading at its tip.
    private static void Fork(LineRenderer branch, LineRenderer core)
    {
        int count = core.positionCount;
        if (count < 3) { branch.positionCount = 0; return; }
        int at = Random.Range(1, count - 1);
        var start = core.GetPosition(at);
        var along = core.GetPosition(count - 1) - core.GetPosition(0);
        float length = along.magnitude * Random.Range(.12f, .3f);
        if (length < .05f) { branch.positionCount = 0; return; }
        var dir = Quaternion.AngleAxis(Random.Range(-55f, 55f), Random.onUnitSphere) * along.normalized;
        int points = 9;
        branch.positionCount = points;
        for (int i = 0; i < points; i++)
        {
            float t = i / (float)(points - 1);
            var p = start + dir * length * t;
            if (i > 0) p += Random.insideUnitSphere * length * .14f;
            branch.SetPosition(i, p);
        }
    }

    private void EnsureVisuals(int segments)
    {
        while (_branches.Count < segments * BranchesPerSegment)
        {
            var obj = new GameObject("IHM Staff Bolt Fork");
            obj.transform.SetParent(transform, false);
            var line = obj.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.sharedMaterial = Material();
            line.numCapVertices = 1;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.enabled = false;
            _branches.Add(line);
        }
        while (_lines.Count < segments * 2)
        {
            var obj = new GameObject("IHM Staff Bolt");
            obj.transform.SetParent(transform, false);
            var line = obj.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.sharedMaterial = Material();
            line.numCapVertices = 2;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.textureMode = LineTextureMode.Stretch;
            line.enabled = false;
            _lines.Add(line);
        }
        while (_lights.Count < segments)
        {
            var obj = new GameObject("IHM Staff Bolt Light");
            obj.transform.SetParent(transform, false);
            var light = obj.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = Glow;
            light.range = 3.5f;
            light.shadows = LightShadows.None;
            light.enabled = false;
            _lights.Add(light);
        }
    }

    private static Material Material()
    {
        if (BoltMaterial != null) return BoltMaterial;
        var shader = Shader.Find("Sprites/Default");
        BoltMaterial = new Material(shader) { name = "IHM Staff Bolt (fallback)", hideFlags = HideFlags.HideAndDontSave };
        return BoltMaterial;
    }

    /// <summary>Copy of a native additive gun-beam material, tinted by the line's vertex colours.</summary>
    internal static void UseNativeBeam(Material source)
    {
        if (source == null || BoltMaterial != null) return;
        var m = new Material(source) { name = "IHM Staff Bolt", hideFlags = HideFlags.HideAndDontSave };
        // Particles/Standard Unlit: plain multiply colour mode, so our white-blue vertex colours apply.
        if (m.HasProperty("_ColorMode")) m.SetFloat("_ColorMode", 0f);
        m.DisableKeyword("_COLOROVERLAY_ON");
        m.DisableKeyword("_COLORCOLOR_ON");
        m.DisableKeyword("_COLORADDSUBDIFF_ON");
        if (m.HasProperty("_Color")) m.SetColor("_Color", Color.white);
        BoltMaterial = m;
    }
}
