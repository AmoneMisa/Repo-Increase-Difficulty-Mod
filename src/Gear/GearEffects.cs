using System.Collections.Generic;
using HarmonyLib;
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
        public float Remaining;
        public float Carry;
    }

    private static readonly Dictionary<EnemyHealth, Poison> Poisoned = new();
    private static readonly Dictionary<Enemy, float> Frozen = new();
    private sealed class Pull
    {
        public Rigidbody Body;
        public Transform Target;
        public float Until;
    }
    private static readonly List<Pull> Pulls = new();
    private static readonly List<Enemy> Scratch = new();

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
                if (p.Health == null || p.Health.healthCurrent <= 0 || p.Remaining <= 0f)
                {
                    (done ??= new List<EnemyHealth>()).Add(kv.Key);
                    continue;
                }
                float step = Mathf.Min(dt, p.Remaining);
                p.Remaining -= step;
                p.Carry += rate * step;
                if (p.Carry >= 4f || (p.Remaining <= 0f && p.Carry >= 1f))
                {
                    int dmg = Mathf.FloorToInt(p.Carry);
                    p.Carry -= dmg;
                    p.Health.Hurt(dmg, Vector3.zero);
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
                if (kv.Key == null || kv.Value <= Time.time)
                    Scratch.Add(kv.Key);
                else if (kv.Key.HasRigidbody && kv.Key.Rigidbody.physGrabObject != null)
                    kv.Key.Rigidbody.physGrabObject.OverrideGrabDisable(0.2f);
            }
            foreach (var e in Scratch)
                Frozen.Remove(e);
        }

        // Sticky
        for (int i = Pulls.Count - 1; i >= 0; i--)
        {
            var p = Pulls[i];
            if (p.Body == null || p.Target == null || Time.time > p.Until)
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
            Vector3 wanted = Vector3.MoveTowards(current, to.normalized * 10f, 40f * dt);
            p.Body.AddForce(wanted - current, ForceMode.VelocityChange);
        }
    }

    private static Enemy EnemyAt(Vector3 point)
    {
        foreach (var c in Physics.OverlapSphere(point, 0.9f))
        {
            var er = c.GetComponentInParent<EnemyRigidbody>();
            if (er != null && er.enemy != null)
                return er.enemy;
        }
        return null;
    }

    [HarmonyPatch(typeof(ItemGun), "ShootBulletRPC")]
    private static class OnShot
    {
        [HarmonyPostfix]
        private static void Postfix(ItemGun __instance, Vector3 _endPosition, bool _hit, int _shooterID)
        {
            if (!_hit || !SemiFunc.IsMasterClientOrSingleplayer())
                return;
            var gear = __instance.GetComponent<GearGun>();
            if (gear == null)
                return;

            switch (gear.kind)
            {
                case GearKind.Poison:
                {
                    var enemy = EnemyAt(_endPosition);
                    if (enemy != null && enemy.HasHealth)
                    {
                        if (!Poisoned.TryGetValue(enemy.Health, out var p))
                            Poisoned[enemy.Health] = p = new Poison { Health = enemy.Health };
                        p.Remaining = ModConfig.PoisonSeconds.Value; // refresh, never stack
                    }
                    break;
                }
                case GearKind.Ice:
                {
                    var enemy = EnemyAt(_endPosition);
                    if (enemy != null)
                    {
                        float t = ModConfig.IceFreezeSeconds.Value;
                        enemy.Freeze(t);
                        Frozen[enemy] = Time.time + t;
                    }
                    break;
                }
                case GearKind.Sticky:
                    TryPull(_endPosition, _shooterID, __instance.GetComponent<PhysGrabObject>());
                    break;
            }
        }
    }

    private static void TryPull(Vector3 point, int shooterId, PhysGrabObject gun)
    {
        var shooter = SemiFunc.PlayerAvatarGetFromPhotonID(shooterId);
        if (shooter == null)
            return;

        Rigidbody body = null;
        Enemy enemy = EnemyAt(point);
        if (enemy != null && enemy.HasRigidbody)
            body = enemy.Rigidbody.physGrabObject != null ? enemy.Rigidbody.physGrabObject.rb : null;

        if (body == null)
        {
            float best = float.MaxValue;
            foreach (var c in Physics.OverlapSphere(point, 0.8f))
            {
                var pgo = c.GetComponentInParent<PhysGrabObject>();
                if (pgo == null || pgo == gun || pgo.rb == null || c.GetComponentInParent<PlayerAvatar>() != null)
                    continue;
                float d = Vector3.Distance(pgo.rb.position, point);
                if (d < best)
                {
                    best = d;
                    body = pgo.rb;
                }
            }
        }

        if (body == null)
            return;

        // "Free lift" capacity at one strength level LESS than the shooter has (each level = +0.2 grab strength).
        float strength = shooter.physGrabber != null ? shooter.physGrabber.grabStrength : 1f;
        float capacity = ModConfig.StickyFreeLiftMass.Value * Mathf.Max(0.1f, strength - 0.2f);
        if (body.mass > capacity)
            return;

        if (enemy != null)
            enemy.Freeze(0.9f); // stop its own movement overriding the pull
        Pulls.RemoveAll(x => x.Body == body);
        Pulls.Add(new Pull { Body = body, Target = shooter.transform, Until = Time.time + 1.2f });
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
