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
///  * Ice    - Enemy.Freeze for a few seconds, keeps the enemy un-liftable while frozen; frozen enemies take
///             reduced firearm and boosted melee damage (HurtCollider patch below).
///  * Sticky - pulls the hit enemy / object toward the shooter if it is light enough.
/// </summary>
internal static class GearEffects
{
    private sealed class Poison
    {
        public EnemyHealth Health;
        public Enemy Enemy;
        public PlayerAvatar Shooter;
        public float Remaining;
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

    public static bool IsFrozen(Enemy e) => e != null && Frozen.TryGetValue(e, out var until) && until > Time.time;

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
                p.Carry += rate * step;
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
            p.Object.OverrideIndestructible(0.3f);
            p.Body.WakeUp();
            float speed = Mathf.Min(6f, (to.magnitude - 1.8f) * 5f);
            Vector3 wanted = Vector3.MoveTowards(current, to.normalized * speed, 30f * dt);
            p.Body.AddForce(wanted - current, ForceMode.VelocityChange);
        }
    }

    // A poisoned monster is actively hostile: it is (re)pointed at a player - the one who shot it, else the
    // nearest living one - and its chase is kept alive for as long as the poison lasts. This deliberately
    // overrides the enemy's own "stop chasing" cooldown.
    private static void KeepAggressive(Poison p, float dt)
    {
        var e = p.Enemy;
        if (e == null || !e.HasStateChase || e.IsStunned() || IsFrozen(e))
            return;

        if (e.CurrentState is EnemyState.ChaseBegin or EnemyState.Chase or EnemyState.ChaseSlow)
            e.SetChaseTimer(); // keeps the current chase from timing out

        p.AggroTimer -= dt;
        if (p.AggroTimer > 0f)
            return;
        p.AggroTimer = 0.5f;

        if (e.CurrentState is EnemyState.Chase or EnemyState.ChaseBegin)
            return; // already after somebody

        var target = p.Shooter != null && !p.Shooter.isDisabled ? p.Shooter : NearestPlayer(e.transform.position);
        if (target == null)
            return;
        e.DisableChaseTimer = 0f;
        e.SetChaseTarget(target);
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

    [HarmonyPatch(typeof(ItemGun), "ShootBulletRPC")]
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
                        p.Remaining = ModConfig.PoisonSeconds.Value; // refresh, never stack
                        p.Shooter = SemiFunc.PlayerAvatarGetFromPhotonID(_shooterID);
                        ModNetwork.BroadcastEnemyStatus(enemy, 0, p.Remaining);
                    }
                    break;
                }
                case GearKind.Ice:
                {
                    if (enemy != null)
                    {
                        float t = ModConfig.IceFreezeSeconds.Value;
                        enemy.Freeze(t);
                        Frozen[enemy] = Time.time + t;
                        ModNetwork.BroadcastEnemyStatus(enemy, 1, t);
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
            }
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
        var pgo = enemy != null && enemy.HasRigidbody ? enemy.Rigidbody.physGrabObject :
            hit.collider.GetComponentInParent<PhysGrabObject>();
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

        if (enemy != null)
            enemy.Freeze(0.9f); // stop its own movement overriding the pull
        Pulls.RemoveAll(x => x.Body == body);
        body.WakeUp();
        Pulls.Add(new Pull { Body = body, Object = pgo, Target = shooter.transform, Until = Time.time + 2f });
        ModNetwork.BroadcastStickyTether(gun, pgo, pgo.transform.InverseTransformPoint(hit.point), 2f);
    }

    [HarmonyPatch(typeof(ItemGun), "MuzzleFlash")]
    private static class StickyMuzzleFlash
    {
        [HarmonyPrefix]
        private static bool Prefix(ItemGun __instance)
        {
            var gear = __instance.GetComponent<GearGun>();
            return gear == null || (gear.kind != GearKind.Sticky && gear.kind != GearKind.Lightning);
        }
    }

    // Frozen enemies: less damage from firearms, more from melee. Scales the collider's enemy damage for
    // the duration of this single hit and restores it afterwards.
    [HarmonyPatch(typeof(HurtCollider), "EnemyHurt")]
    private static class FrozenDamage
    {
        [HarmonyPrefix]
        private static void Prefix(HurtCollider __instance, Enemy _enemy, out int __state)
        {
            __state = __instance.enemyDamage;
            if (!IsFrozen(_enemy))
                return;
            if (__instance.GetComponentInParent<ItemGunBullet>() != null)
                __instance.enemyDamage = Mathf.Max(1, Mathf.RoundToInt(__state * ModConfig.IceGunDamagePercent.Value / 100f));
            else if (__instance.GetComponentInParent<ItemMelee>() != null)
                __instance.enemyDamage = Mathf.RoundToInt(__state * (1f + ModConfig.IceMeleeBonusPercent.Value / 100f));
        }

        [HarmonyPostfix]
        private static void Postfix(HurtCollider __instance, int __state) => __instance.enemyDamage = __state;
    }
}
