using System.Collections.Generic;
using HarmonyLib;
using IncreaseHardMod.Networking;
using Photon.Pun;
using UnityEngine;

namespace IncreaseHardMod.Gear;

/// <summary>
/// Host-side effects of the special guns. Bullets are hitscan: <see cref="ItemGun.ShootBulletRPC"/> runs on
/// every client with the impact point, and on the host we look at what is standing there.
///
///  * Poison - refreshes a DoT on the enemy to the full duration (never stacks); damage is dealt in small
///             whole-number ticks through the game's own EnemyHealth.Hurt so it syncs normally.
///  * Ice    - Enemy.Freeze for a few seconds, keeps the enemy un-liftable while frozen. Starting a freeze deals
///             20 damage once (the cloned ice bullet itself deals none); hits on an already frozen enemy only
///             extend it, a shorter repeat never shortens it, and a new freeze after thawing hurts again
///             (GearCombatRules.RefreshFreeze). Every damage type still lands on a frozen enemy, scaled per hit by
///             FrozenDamage: gun bullets IceGunDamagePercent (60%), melee +IceMeleeBonusPercent (+35%),
///             grenade blasts +80%, everything else unchanged. Frozen players keep the damage-free freeze.
///  * Sticky - roots monsters for 3 s and players for 2 s (StickyRoot) and pulls light enough loot toward the shooter; pulled
///             loot loses 80% less value while it travels (not invulnerable).
/// </summary>
internal static class GearEffects
{
    private sealed class Poison
    {
        public EnemyHealth Health;
        public Enemy Enemy;
        public PlayerAvatar Shooter;
        public float Remaining;
        // Damage per second: the full PoisonTotalDamage over this poison's own length (Fast Poison shortens it).
        public float Rate;
        public float Carry;
        public float AggroTimer;
    }

    private static readonly Dictionary<EnemyHealth, Poison> Poisoned = new();
    private static readonly Dictionary<Enemy, float> Frozen = new();
    private sealed class Pull
    {
        public Rigidbody Body;
        public PhysGrabObject Object;
        public Transform Target;
        public float Until;
    }
    private static readonly List<Pull> Pulls = new();
    private static readonly List<Enemy> Scratch = new();
    private static float _visualSync;

    /// <summary>Host: freeze a monster (or extend its freeze); the first freeze deals the ice damage.</summary>
    internal static void FreezeEnemy(Enemy enemy, float seconds)
    {
        if (enemy == null || seconds <= 0f || !SemiFunc.IsMasterClientOrSingleplayer()) return;
        Frozen.TryGetValue(enemy, out float previousEnd);
        int damage = GearCombatRules.RefreshFreeze(Time.time, previousEnd, seconds, out float end);
        Frozen[enemy] = end;
        enemy.Freeze(end - Time.time);
        if (damage > 0 && enemy.HasHealth && enemy.Health != null)
            enemy.Health.Hurt(damage, Vector3.zero);
        ModNetwork.BroadcastEnemyStatus(enemy, 1, end - Time.time);
    }

    public static bool IsFrozen(Enemy e) => e != null && Frozen.TryGetValue(e, out var until) && until > Time.time;

    // Net Launcher: a netted monster is pinned to an anchor on the floor. Unlike ice it is NOT frozen: its
    // AI keeps running, so it can still turn and attack anyone in reach. Each second it may tear free.
    private sealed class Net
    {
        public float Until, NextRoll;
        public Vector3 Anchor;
    }
    private static readonly Dictionary<Enemy, Net> Netted = new();
    private static readonly Dictionary<PlayerAvatar, Net> NettedPlayers = new();
    private static readonly List<PlayerAvatar> PlayerScratch = new();

    // Players caught by a net: the host times the hold and rolls the same break-free chance; the netted
    // player's own client stops walking/jumping (PlayerNetEffect).
    private static void ApplyPlayerNet(PlayerAvatar player)
    {
        if (player == null || player.isDisabled || player.deadSet) return;
        float seconds = ModConfig.NetSeconds.Value;
        if (!NettedPlayers.TryGetValue(player, out var net)) NettedPlayers[player] = net = new Net();
        net.Until = Time.time + seconds;
        net.NextRoll = Time.time + 1f;
        ModNetwork.BroadcastPlayerNet(player, seconds);
    }

    public static bool IsNetted(Enemy e) => e != null && Netted.ContainsKey(e);

    private static void ApplyNet(Enemy enemy, PlayerAvatar shooter)
    {
        float seconds = Skills.SkillEffects.StatusSeconds(shooter, Skills.SkillEffects.Status.Net, ModConfig.NetSeconds.Value);
        if (!Netted.TryGetValue(enemy, out var net))
        {
            Netted[enemy] = net = new Net();
            var body = enemy.HasRigidbody && enemy.Rigidbody != null && enemy.Rigidbody.rb != null ? enemy.Rigidbody.rb.position : enemy.transform.position;
            net.Anchor = body;
            // Flying monsters are dragged down to the floor under them.
            if (Physics.Raycast(body + Vector3.up * .1f, Vector3.down, out var floor, 6f, SemiFunc.LayerMaskGetVisionObstruct(), QueryTriggerInteraction.Ignore) &&
                body.y - floor.point.y > 1.3f)
                net.Anchor = floor.point + Vector3.up * .7f;
        }
        net.Until = Time.time + seconds; // a second net refreshes the hold
        net.NextRoll = Time.time + 1f;
        ModNetwork.BroadcastEnemyStatus(enemy, 2, seconds);
    }

    private static void TickNets(float dt)
    {
        if (NettedPlayers.Count > 0)
        {
            PlayerScratch.Clear();
            foreach (var kv in NettedPlayers)
            {
                var player = kv.Key; var net = kv.Value;
                if (player == null || player.isDisabled || player.deadSet) { PlayerScratch.Add(player); continue; }
                if (Time.time >= net.Until) { PlayerScratch.Add(player); ModNetwork.BroadcastPlayerNet(player, NetExpired); continue; }
                if (Time.time >= net.NextRoll)
                {
                    net.NextRoll += 1f;
                    if (Random.value < ModConfig.NetBreakChance.Value) { PlayerScratch.Add(player); ModNetwork.BroadcastPlayerNet(player, NetBroken); }
                }
            }
            foreach (var p in PlayerScratch) NettedPlayers.Remove(p);
        }
        if (Netted.Count == 0) return;
        Scratch.Clear();
        foreach (var kv in Netted)
        {
            var enemy = kv.Key; var net = kv.Value;
            bool dead = enemy == null || !enemy.isActiveAndEnabled || (enemy.HasHealth && enemy.Health != null && enemy.Health.dead);
            if (dead) { Scratch.Add(enemy); continue; }
            if (Time.time >= net.Until)
            {
                Scratch.Add(enemy);
                ModNetwork.BroadcastEnemyStatus(enemy, 2, NetExpired);
                continue;
            }
            if (Time.time >= net.NextRoll)
            {
                net.NextRoll += 1f;
                if (Random.value < ModConfig.NetBreakChance.Value)
                {
                    Scratch.Add(enemy);
                    ModNetwork.BroadcastEnemyStatus(enemy, 2, NetBroken);
                    continue;
                }
            }
            // Hold: cancel walking (the agent drives most monsters) and pull the body back onto the anchor.
            if (enemy.HasNavMeshAgent && enemy.NavMeshAgent != null)
            {
                enemy.NavMeshAgent.Stop(.2f);
                enemy.NavMeshAgent.ResetPath();
            }
            if (enemy.HasRigidbody && enemy.Rigidbody != null && enemy.Rigidbody.rb != null && !enemy.Rigidbody.rb.isKinematic)
            {
                var rb = enemy.Rigidbody.rb;
                var to = net.Anchor - rb.position;
                var v = rb.linearVelocity;
                // Firm horizontally, and never let it rise off the floor; it may still drop.
                rb.linearVelocity = new Vector3(to.x * 9f, Mathf.Min(v.y, to.y * 9f), to.z * 9f);
            }
        }
        foreach (var e in Scratch) Netted.Remove(e);
    }

    /// <summary>EnemyStatus "seconds" values that end a net early: torn free, or simply expired.</summary>
    internal const float NetBroken = -1f, NetExpired = -2f;

    public static void Tick(float dt)
    {
        if (!SemiFunc.IsMasterClientOrSingleplayer())
            return;

        // Poison
        if (Poisoned.Count > 0)
        {
            List<EnemyHealth> done = null;
            float rate = ModConfig.PoisonTotalDamage.Value / Mathf.Max(0.1f, ModConfig.PoisonSeconds.Value);
            foreach (var kv in Poisoned)
            {
                var p = kv.Value;
                if (p.Enemy == null || !p.Enemy.isActiveAndEnabled || p.Health == null || p.Health.dead || p.Health.healthCurrent <= 0 || p.Remaining <= 0f)
                {
                    (done ??= new List<EnemyHealth>()).Add(kv.Key);
                    continue;
                }
                KeepAggressive(p, dt);
                float step = Mathf.Min(dt, p.Remaining);
                p.Remaining -= step;
                p.Carry += (p.Rate > 0f ? p.Rate : rate) * step;
                if (p.Carry >= 4f || (p.Remaining <= 0f && p.Carry > 0f))
                {
                    // Round the final fraction so frame-time accumulation cannot lose the last
                    // point of configured damage (e.g. 124.999 instead of 125).
                    int dmg = p.Remaining <= 0f ? Mathf.RoundToInt(p.Carry) : Mathf.FloorToInt(p.Carry);
                    p.Carry -= dmg;
                    if (dmg > 0) p.Health.Hurt(dmg, Vector3.zero);
                }
            }
            if (done != null)
                foreach (var k in done)
                    Poisoned.Remove(k);
        }

        TickNets(dt);

        // Ice: enemies cannot be picked up while frozen.
        if (Frozen.Count > 0)
        {
            Scratch.Clear();
            foreach (var kv in Frozen)
            {
                if (kv.Key == null || !kv.Key.isActiveAndEnabled || kv.Value <= Time.time ||
                    (kv.Key.HasHealth && kv.Key.Health != null && kv.Key.Health.dead))
                    Scratch.Add(kv.Key);
                else
                {
                    var frozen = kv.Key;
                    // The native FreezeTimer only zeroes the rigidbody velocity; most monsters keep walking
                    // through their NavMeshAgent. Hold the agent too, and keep the native timer topped up.
                    // Freeze() is an RPC to every client: top up in ~1 s chunks, never per frame, and never past the end.
                    float left = kv.Value - Time.time;
                    if (frozen.FreezeTimer < Mathf.Min(0.3f, left - 0.05f)) frozen.Freeze(Mathf.Min(1f, left));
                    if (frozen.HasNavMeshAgent && frozen.NavMeshAgent != null)
                    {
                        frozen.NavMeshAgent.Stop(0.2f);
                        frozen.NavMeshAgent.ResetPath();
                    }
                    if (frozen.HasRigidbody && frozen.Rigidbody.physGrabObject != null)
                        frozen.Rigidbody.physGrabObject.OverrideGrabDisable(0.2f);
                }
            }
            foreach (var e in Scratch)
                Frozen.Remove(e);
        }

        // Periodic snapshots cover late joiners and targets whose PhotonView was not ready yet.
        // No event caching: expired effects cannot attach to a reused view ID on a later level.
        _visualSync -= dt;
        if (_visualSync <= 0f)
        {
            _visualSync = .8f;
            foreach (var p in Poisoned.Values)
                if (p.Remaining > 0f) ModNetwork.BroadcastEnemyStatus(p.Enemy, 0, p.Remaining);
            foreach (var ice in Frozen)
                ModNetwork.BroadcastEnemyStatus(ice.Key, 1, ice.Value - Time.time);
            foreach (var net in Netted)
                if (net.Key != null && net.Value.Until > Time.time)
                    ModNetwork.BroadcastEnemyStatus(net.Key, 2, net.Value.Until - Time.time);
            foreach (var net in NettedPlayers)
                if (net.Key != null && net.Value.Until > Time.time)
                    ModNetwork.BroadcastPlayerNet(net.Key, net.Value.Until - Time.time);
            foreach (var ice in PlayerFreezeEffect.Active)
                if (ice != null && ice.Remaining > 0f)
                    ModNetwork.BroadcastPlayerFreeze(ice.Avatar, ice.Remaining);
        }

        // Sticky
        for (int i = Pulls.Count - 1; i >= 0; i--)
        {
            var p = Pulls[i];
            if (p.Body == null || p.Object == null || !p.Object.isActiveAndEnabled ||
                p.Target == null || Time.time > p.Until || p.Body.isKinematic)
            {
                Pulls.RemoveAt(i);
                continue;
            }
            Vector3 to = p.Target.position + Vector3.up * 0.8f - p.Body.position;
            if (to.magnitude < 1.8f)
            {
                Pulls.RemoveAt(i);
                continue;
            }
            // (linearVelocity doesn't exist in the Unity version we compile against; velocity at the centre of mass is the same.)
            Vector3 current = p.Body.GetPointVelocity(p.Body.worldCenterOfMass);
            // Lift loot off the floor and slow down as it reaches the player. Protect its
            // value while pulled instead of smashing it with a normal gun impulse.
            p.Object.OverrideZeroGravity(0.2f);
            p.Body.WakeUp();
            float speed = Mathf.Min(6f, (to.magnitude - 1.8f) * 5f);
            Vector3 wanted = Vector3.MoveTowards(current, to.normalized * speed, 30f * dt);
            p.Body.AddForce(wanted - current, ForceMode.VelocityChange);
        }
    }

    // A poisoned monster is actively hostile: it is (re)pointed at a player - the one who shot it, else the
    // nearest living one - for as long as the poison lasts, overriding its own "stop chasing" cooldown.
    //
    // Two kinds of monster AI need two paths. Generic chasers (Enemy.HasStateChase) are driven through
    // SetChaseTarget and their chase timer. Most REPO monsters instead run their own state machine and only
    // react to EnemyVision's "player seen" event, so SetChaseTarget alone did nothing to them (the old
    // HasStateChase early-out skipped them entirely): they get the same vision event the game raises when
    // they spot someone.
    private static void KeepAggressive(Poison p, float dt)
    {
        var e = p.Enemy;
        if (e == null || e.IsStunned() || IsFrozen(e))
            return;

        bool chaser = e.HasStateChase;
        bool chasing = chaser && e.CurrentState is EnemyState.ChaseBegin or EnemyState.Chase or EnemyState.ChaseSlow;
        if (chasing)
            e.SetChaseTimer(); // keeps the current chase from timing out

        p.AggroTimer -= dt;
        if (p.AggroTimer > 0f)
            return;
        p.AggroTimer = 0.5f;

        if (chaser && e.CurrentState is EnemyState.Chase or EnemyState.ChaseBegin)
            return; // already after somebody

        var target = p.Shooter != null && !p.Shooter.isDisabled ? p.Shooter : NearestPlayer(e.transform.position);
        if (target == null || target.photonView == null)
            return;
        if (chaser)
        {
            e.DisableChaseTimer = 0f;
            e.SetChaseTarget(target); // also raises the vision event
            return;
        }
        if (!e.HasVision || e.Vision == null || e.Vision.DisableTimer > 0f)
            return;
        int id = target.photonView.ViewID;
        // Same guard the game's own vision loop relies on: only players the vision tracks.
        if (!e.Vision.VisionTriggered.ContainsKey(id) || !e.Vision.VisionsTriggered.ContainsKey(id))
            return;
        e.Vision.VisionTrigger(id, target, culled: false, playerNear: false);
    }

    private static PlayerAvatar NearestPlayer(Vector3 from)
    {
        PlayerAvatar best = null;
        float bestD = float.MaxValue;
        var list = GameDirector.instance != null ? GameDirector.instance.PlayerList : null;
        if (list == null)
            return null;
        foreach (var pl in list)
        {
            if (pl == null || pl.isDisabled)
                continue;
            float d = (pl.transform.position - from).sqrMagnitude;
            if (d < bestD)
            {
                bestD = d;
                best = pl;
            }
        }
        return best;
    }

    private static bool ShotHit(ItemGun gun, Vector3 point, int shooterId, out RaycastHit hit)
    {
        hit = default;
        if (gun.gunMuzzle == null) return false;
        Vector3 origin = gun.gunMuzzle.position;
        Vector3 direction = point - origin;
        if (direction.sqrMagnitude < 0.0001f) return false;
        var shooter = shooterId >= 0 ? SemiFunc.PlayerAvatarGetFromPhotonID(shooterId) : null;
        float nearest = float.PositiveInfinity;
        foreach (var candidate in Physics.RaycastAll(origin, direction.normalized, direction.magnitude + 0.05f,
                     (int)SemiFunc.LayerMaskGetVisionObstruct() | LayerMask.GetMask("Enemy"), QueryTriggerInteraction.UseGlobal))
        {
            if (candidate.transform.IsChildOf(gun.transform) ||
                (shooter != null && candidate.transform.IsChildOf(shooter.transform)) || candidate.distance >= nearest) continue;
            nearest = candidate.distance;
            hit = candidate;
        }
        return !float.IsPositiveInfinity(nearest);
    }

    [HarmonyPatch(typeof(ItemGun), nameof(ItemGun.ShootBulletRPC))]
    private static class OnShot
    {
        [HarmonyPrefix]
        private static bool Prefix(ItemGun __instance, Vector3 _endPosition, bool _hit, int _shooterID,
            bool _surfaceHit, PhotonMessageInfo _info, out RaycastHit? __state)
        {
            __state = null;
            if (!SemiFunc.MasterOnlyRPC(_info)) return false;
            var gear = __instance.GetComponent<GearGun>();
            if (gear != null && gear.kind == GearKind.Lightning)
            {
                // No native bullet: the host resolves the chain and broadcasts the bolt to everyone.
                if (SemiFunc.IsMasterClientOrSingleplayer())
                    __instance.GetComponent<WizardStaff>()?.HostFire(_endPosition, _shooterID);
                return false;
            }
            bool sticky = gear != null && gear.kind == GearKind.Sticky;
            if (sticky && _hit) Visuals.StickyTether.Pulse(__instance, _endPosition);
            // Resolve before the native method activates the bullet's hit colliders.
            // A miss still sets _hit in the game, but must never poison/freeze a nearby enemy.
            if (!_hit || !_surfaceHit || !SemiFunc.IsMasterClientOrSingleplayer() ||
                gear == null) return !sticky;
            if (ShotHit(__instance, _endPosition, _shooterID, out var hit)) __state = hit;
            return !sticky; // No native bullet, tracer, sparks or impact sound for a pulling tool.
        }

        [HarmonyPostfix]
        private static void Postfix(ItemGun __instance, int _shooterID, RaycastHit? __state)
        {
            if (!__state.HasValue || !SemiFunc.IsMasterClientOrSingleplayer())
                return;
            var hit = __state.Value;
            var enemyBody = hit.collider.GetComponentInParent<EnemyRigidbody>();
            var enemy = enemyBody != null ? enemyBody.enemy : hit.collider.GetComponentInParent<Enemy>();
            var gear = __instance.GetComponent<GearGun>();
            if (gear == null)
                return;

            switch (gear.kind)
            {
                case GearKind.Poison:
                {
                    if (enemy != null && enemy.HasHealth)
                    {
                        if (!Poisoned.TryGetValue(enemy.Health, out var p))
                            Poisoned[enemy.Health] = p = new Poison { Health = enemy.Health, Enemy = enemy };
                        p.Shooter = SemiFunc.PlayerAvatarGetFromPhotonID(_shooterID);
                        // Refresh, never stack: a share of the monster's max HP (so it keeps up with the depth ramp),
                        // plus half of what the previous poison had left. Fast Poison deals it in less time (never under 8 s).
                        float left = p.Remaining > 0f ? p.Remaining * p.Rate : 0f;
                        float total = UtilityGearRules.PoisonTotal(ModConfig.PoisonTotalDamage.Value, ModConfig.PoisonMaxHpPercent.Value,
                            enemy.Health.health, left);
                        p.Remaining = Skills.SkillEffects.StatusSeconds(p.Shooter, Skills.SkillEffects.Status.Poison, ModConfig.PoisonSeconds.Value);
                        p.Rate = total / Mathf.Max(0.1f, p.Remaining);
                        ModNetwork.BroadcastEnemyStatus(enemy, 0, p.Remaining);
                    }
                    else
                    {
                        // The bolt's direct damage already landed; show the hit player the toxic screen frame.
                        var player = PlayerAtHit(hit.collider);
                        if (player != null) ModNetwork.BroadcastPlayerStatus(player, ModNetwork.StatusPoisoned, PlayerPoisonScreenSeconds);
                    }
                    break;
                }
                case GearKind.Ice:
                {
                    if (enemy != null)
                    {
                        float t = Skills.SkillEffects.StatusSeconds(SemiFunc.PlayerAvatarGetFromPhotonID(_shooterID),
                            Skills.SkillEffects.Status.Freeze, ModConfig.IceFreezeSeconds.Value);
                        FreezeEnemy(enemy, t);
                    }
                    else
                    {
                        var player = PlayerAtHit(hit.collider);
                        if (player != null)
                            ModNetwork.BroadcastPlayerFreeze(player, ModConfig.IceFreezeSeconds.Value);
                    }
                    break;
                }
                case GearKind.Sticky:
                    TryPull(hit, _shooterID, __instance);
                    break;
                case GearKind.Net:
                    if (enemy != null) ApplyNet(enemy, SemiFunc.PlayerAvatarGetFromPhotonID(_shooterID));
                    else ApplyPlayerNet(PlayerAtHit(hit.collider));
                    break;
            }
        }
    }

    private const float PlayerPoisonScreenSeconds = 4f;

    // Native tranq shots (Tranq Gun, Spritz) knock a hit player out; tell that player so their screen shows it.
    // Host-side, after the native bullet resolved: the player nearest the hit point within a small radius.
    [HarmonyPatch(typeof(ItemGun), nameof(ItemGun.ShootBulletRPC))]
    private static class TranqPlayerHit
    {
        [HarmonyPostfix]
        private static void Postfix(ItemGun __instance, Vector3 _endPosition, bool _hit)
        {
            if (!_hit || __instance == null || !SemiFunc.IsMasterClientOrSingleplayer()) return;
            bool tranq = __instance.GetComponent<ItemGunTranq>() != null || __instance.GetComponent<SpritzDose>() != null;
            if (!tranq || __instance.GetComponent<GearGun>() != null) return;
            PlayerAvatar target = null;
            float best = .6f * .6f;
            foreach (var player in SemiFunc.PlayerGetList())
            {
                if (player == null || player.isDisabled) continue;
                var body = player.PlayerVisionTarget != null ? player.PlayerVisionTarget.VisionTransform.position : player.transform.position + Vector3.up;
                float d = Mathf.Min((player.transform.position + Vector3.up * .5f - _endPosition).sqrMagnitude, (body - _endPosition).sqrMagnitude);
                if (d < best) { best = d; target = player; }
            }
            if (target == null) return;
            // Only when the shot really knocks players out (stun or tumble on its hit collider); the screen
            // lasts as long as that knock-out.
            var hurt = __instance.bulletPrefab != null ? __instance.bulletPrefab.GetComponentInChildren<HurtCollider>(true) : null;
            float knockOut = hurt != null ? Mathf.Max(hurt.playerStunTime, hurt.playerTumbleTime) : 0f;
            if (knockOut <= 0f) return;
            ModNetwork.BroadcastPlayerStatus(target, ModNetwork.StatusSedated, Mathf.Clamp(knockOut, 1.5f, 15f));
        }
    }

    internal static PlayerAvatar PlayerAtHit(Collider collider)
    {
        var avatar = collider.GetComponentInParent<PlayerAvatar>();
        if (avatar != null) return avatar;
        var tumble = collider.GetComponentInParent<PlayerTumble>();
        if (tumble != null) return tumble.playerAvatar;
        var controller = collider.GetComponentInParent<PlayerController>();
        return controller != null ? controller.playerAvatarScript : null;
    }

    private static void TryPull(RaycastHit hit, int shooterId, ItemGun gun)
    {
        var gunObject = gun.GetComponent<PhysGrabObject>();
        var shooter = shooterId >= 0 ? SemiFunc.PlayerAvatarGetFromPhotonID(shooterId) : null;
        if (shooter == null && gunObject != null)
            shooter = gunObject.lastPlayerGrabbing;
        if (shooter == null && !GameManager.Multiplayer())
            shooter = PlayerAvatar.instance;
        if (shooter == null)
            return;

        var enemyBody = hit.collider.GetComponentInParent<EnemyRigidbody>();
        Enemy enemy = enemyBody != null ? enemyBody.enemy : hit.collider.GetComponentInParent<Enemy>();
        var player = PlayerAtHit(hit.collider);
        if (player != null || enemy != null)
        {
            StickyRoot.Send(player, enemy);
            return;
        }
        var pgo = hit.collider.GetComponentInParent<PhysGrabObject>();
        if (pgo == null || pgo == gunObject || pgo.rb == null ||
            hit.collider.GetComponentInParent<PlayerAvatar>() != null)
            return;
        Rigidbody body = pgo.rb;
        if (body.isKinematic)
            return;

        // "Free lift" capacity at one strength level LESS than the shooter has (each level = +0.2 grab strength).
        float strength = shooter.physGrabber != null ? shooter.physGrabber.grabStrength : 1f;
        float capacity = ModConfig.StickyFreeLiftMass.Value * Mathf.Max(0.1f, strength - 0.2f);
        if (body.mass > capacity)
            return;

        Pulls.RemoveAll(x => x.Body == body);
        body.WakeUp();
        Pulls.Add(new Pull { Body = body, Object = pgo, Target = shooter.transform, Until = Time.time + 2f });
        ModNetwork.BroadcastStickyTether(gun, pgo, pgo.transform.InverseTransformPoint(hit.point), 2f);
    }

    [HarmonyPatch(typeof(ItemGun), nameof(ItemGun.MuzzleFlash))]
    private static class StickyMuzzleFlash
    {
        [HarmonyPrefix]
        private static bool Prefix(ItemGun __instance)
        {
            var gear = __instance.GetComponent<GearGun>();
            return gear == null || (gear.kind != GearKind.Sticky && gear.kind != GearKind.Lightning);
        }
    }

    [HarmonyPatch(typeof(PhysGrabObjectImpactDetector), nameof(PhysGrabObjectImpactDetector.Break))]
    private static class PullDamage
    {
        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        private static void Prefix(PhysGrabObjectImpactDetector __instance, ref float valueLost)
        {
            if (!SemiFunc.IsMasterClientOrSingleplayer() || !__instance.isValuable || valueLost <= 0f) return;
            foreach (var pull in Pulls)
                if (pull.Object == __instance.physGrabObject && pull.Target != null && Time.time < pull.Until &&
                    pull.Body != null && Vector3.Distance(pull.Body.position, pull.Target.position + Vector3.up * .8f) >= 1.8f)
                { valueLost = GearCombatRules.PulledLootLoss(valueLost); return; }
        }
    }

    // Do not mutate HurtCollider.enemyDamage: other weapon perks temporarily change it too,
    // and interleaved restore patches would leak the bonus into later, unfrozen hits.
    private static EnemyHealth _frozenMeleeTarget;
    private static float _frozenHitFactor = 1f;
    [HarmonyPatch(typeof(HurtCollider), nameof(HurtCollider.EnemyHurt))]
    private static class FrozenDamage
    {
        [HarmonyPrefix]
        private static void Prefix(HurtCollider __instance, Enemy _enemy, out (EnemyHealth Target, float Factor) __state)
        {
            __state = (_frozenMeleeTarget, _frozenHitFactor);
            _frozenMeleeTarget = IsFrozen(_enemy) ? _enemy.Health : null;
            _frozenHitFactor = __instance.GetComponentInParent<GrenadeBlast>() != null ? 1.8f :
                __instance.GetComponentInParent<ItemGunBullet>() != null ? ModConfig.IceGunDamagePercent.Value / 100f :
                __instance.GetComponentInParent<ItemMelee>() != null ? 1f + ModConfig.IceMeleeBonusPercent.Value / 100f : 1f;
        }

        [HarmonyFinalizer]
        private static void Restore((EnemyHealth Target, float Factor) __state)
        { _frozenMeleeTarget = __state.Target; _frozenHitFactor = __state.Factor; }
    }

    [HarmonyPatch(typeof(EnemyHealth), nameof(EnemyHealth.Hurt))]
    private static class FrozenMeleeBonus
    {
        [HarmonyPrefix]
        private static void Prefix(EnemyHealth __instance, ref int _damage)
        {
            if (_damage > 0 && _frozenMeleeTarget != null && _frozenMeleeTarget == __instance)
                _damage = Mathf.Max(1, Mathf.RoundToInt(_damage * _frozenHitFactor));
        }
    }

    [HarmonyPatch(typeof(ParticleScriptExplosion), nameof(ParticleScriptExplosion.Spawn))]
    private static class MarkGrenadeBlast
    {
        [HarmonyPostfix]
        private static void Postfix(ParticleScriptExplosion __instance, ParticlePrefabExplosion __result)
        {
            // Grenades only (explosive, duct-taped and its child grenades): mines, carts and monster bombs share
            // ParticleScriptExplosion but are not grenades. Each blast is a fresh instance, so the marker never leaks.
            bool grenade = __instance.GetComponent<ItemGrenade>() != null || __instance.GetComponent<ItemGrenadeHuman>() != null ||
                __instance.GetComponent<ItemGrenadeDuctTaped>() != null || __instance.GetComponent<ItemGrenadeExplosive>() != null;
            if (__result != null && __result.HurtCollider != null && grenade &&
                __result.HurtCollider.GetComponent<GrenadeBlast>() == null)
                __result.HurtCollider.gameObject.AddComponent<GrenadeBlast>().Thrower =
                    __instance.GetComponent<ItemGrenade>()?.activatorPlayerAvatar ?? __instance.playerCausingHurtOverride;
        }
    }
}

internal sealed class GrenadeBlast : MonoBehaviour { internal PlayerAvatar Thrower; }
