using System.Collections.Generic;
using HarmonyLib;
using IncreaseHardMod.Visuals;
using UnityEngine;
using UnityEngine.Rendering;

namespace IncreaseHardMod.Gear;

/// <summary>
/// Smoke, Cryo and Glue grenades: clones of the stun grenade with the stun stripped. The game invokes
/// <c>onDetonate</c> on every client, so each client draws the burst itself; only the host freezes, glues or hides.
///  - Smoke: a 4.5 m cloud for 12 s. Monsters cannot see a player inside it or through it (except at touching range).
///  - Cryo: freezes every monster within 4 m for 3 s (the Ice Pistol's freeze, so frozen rules and damage apply).
///  - Glue: roots every monster within 4.5 m for 3 s (the Sticky Pistol's root). Players are never affected.
/// </summary>
internal sealed class ModGrenade : MonoBehaviour
{
    public GrenadeKind kind;
    private ItemGrenade _grenade;
    private Sound _arm, _burst;
    private bool _armed;

    private void Start()
    {
        _grenade = GetComponent<ItemGrenade>();
        if (_grenade == null) return;
        try
        {
            _arm = Visuals.ItemAudio.Cue("gear_arm", .35f);
            _burst = Visuals.ItemAudio.Cue(kind.ToString().ToLowerInvariant() + "_burst", .75f);
        }
        catch (System.Exception e) { Plugin.Log.LogWarning($"[Audio] No cue for the {kind} grenade: {e.Message}"); }
        // Only our burst may run off this grenade: the stun parts were stripped, their listeners go too.
        _grenade.onDetonate = new UnityEngine.Events.UnityEvent();
        _grenade.onDetonate.AddListener(() =>
        {
            _burst?.Play(transform.position);
            ModGrenades.Burst(kind, transform.position, ModGrenades.Rank(kind, _grenade.activatorPlayerAvatar));
        });
    }

    // Every client: a click when the pin is pulled (the native countdown tick keeps playing).
    private void Update()
    {
        if (_grenade == null) return;
        if (_grenade.isActive && !_armed) _arm?.Play(transform.position);
        _armed = _grenade.isActive;
    }
}

internal static class ModGrenades
{
    private struct Cloud { internal Vector3 Center; internal float Until, Radius; }

    internal static int Rank(GrenadeKind kind, PlayerAvatar thrower) => Skills.SkillEffects.Level(thrower, kind switch
    {
        GrenadeKind.Smoke => Skills.SkillCatalog.SmokeGrenade,
        GrenadeKind.Cryo => Skills.SkillCatalog.CryoGrenade,
        _ => Skills.SkillCatalog.GlueGrenade,
    });
    private static readonly List<Cloud> Clouds = new();

    internal static void Burst(GrenadeKind kind, Vector3 at, int rank)
    {
        Visual(kind, at, rank);
        if (!SemiFunc.IsMasterClientOrSingleplayer()) return;
        if (kind == GrenadeKind.Smoke)
        {
            Clouds.RemoveAll(c => c.Until <= Time.time);
            Clouds.Add(new Cloud { Center = at + Vector3.up * .8f, Until = Time.time + GrenadeRules.SmokeDuration(rank),
                Radius = GrenadeRules.Radius(kind, rank) });
            return;
        }
        float radius = GrenadeRules.Radius(kind, rank);
        var director = EnemyDirector.instance;
        if (director == null || director.enemiesSpawned == null) return;
        foreach (var parent in director.enemiesSpawned.ToArray())
        {
            var enemy = parent != null && parent.Spawned ? parent.Enemy : null;
            if (enemy == null || !enemy.isActiveAndEnabled) continue;
            var p = enemy.CenterTransform != null ? enemy.CenterTransform.position : enemy.transform.position;
            if (!GrenadeRules.Within(p.x, p.y, p.z, at.x, at.y, at.z, radius)) continue;
            if (kind == GrenadeKind.Cryo) GearEffects.FreezeEnemy(enemy, GrenadeRules.CryoDuration(rank));
            else StickyRoot.Send(null, enemy);
        }
    }

    /// <summary>Host: a smoke cloud hides the player at <paramref name="to"/> from an eye at <paramref name="from"/>.</summary>
    internal static bool Hidden(Vector3 from, Vector3 to)
    {
        if (Clouds.Count == 0) return false;
        float now = Time.time;
        foreach (var c in Clouds)
            if (c.Until > now && GrenadeRules.Hides(from.x, from.y, from.z, to.x, to.y, to.z, c.Center.x, c.Center.y, c.Center.z, c.Radius))
                return true;
        return false;
    }

    internal static void ClearLevel() => Clouds.Clear();

    // ------------------------------------------------------------------ visuals (every client)

    private static void Visual(GrenadeKind kind, Vector3 at, int rank)
    {
        var go = new GameObject("IHM " + kind + " Grenade Burst");
        go.transform.position = at + Vector3.up * .3f;
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.loop = false;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        var emission = ps.emission;
        var fade = ps.colorOverLifetime;
        fade.enabled = true;
        var gradient = new Gradient();
        Color color;
        float life;
        if (kind == GrenadeKind.Smoke)
        {
            color = new Color(.42f, .43f, .45f, .85f);
            main.duration = GrenadeRules.SmokeDuration(rank) - 3f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(3f, 4.5f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(.2f, .8f);
            main.startSize = new ParticleSystem.MinMaxCurve(2.2f, 3.6f);
            main.maxParticles = 220;
            shape.radius = GrenadeRules.Radius(kind, rank) * .55f;
            emission.rateOverTime = 26f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 60) });
            life = GrenadeRules.SmokeDuration(rank) + 5f;
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, .15f), new GradientAlphaKey(.9f, .7f), new GradientAlphaKey(0f, 1f) });
        }
        else
        {
            color = kind == GrenadeKind.Cryo ? new Color(.62f, .88f, 1f, .9f) : new Color(1f, .45f, .85f, .9f);
            main.duration = .2f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(.6f, 1.2f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(2f, GrenadeRules.Radius(kind, rank) * 2.2f);
            main.startSize = new ParticleSystem.MinMaxCurve(.25f, .7f);
            main.maxParticles = 160;
            shape.radius = .3f;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 140) });
            var drag = ps.limitVelocityOverLifetime;
            drag.enabled = true;
            drag.drag = 3f;
            life = 2.5f;
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(.8f, .5f), new GradientAlphaKey(0f, 1f) });
        }
        main.startColor = color;
        fade.color = gradient;
        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = GlowTextures.Sprite(GlowTextures.Soft, "IHM " + kind + " Grenade", kind == GrenadeKind.Smoke ? 3000 : 3100);
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        ps.Play();
        Object.Destroy(go, life);
    }

    // ------------------------------------------------------------------ monster vision (host)

    // Native monsters: a sighting of a player hidden by smoke never triggers.
    [HarmonyPatch(typeof(EnemyVision), nameof(EnemyVision.VisionTrigger))]
    private static class NativeVision
    {
        [HarmonyPrefix]
        private static bool Prefix(EnemyVision __instance, PlayerAvatar player)
        {
            if (Clouds.Count == 0 || player == null || __instance.VisionTransform == null) return true;
            var target = player.PlayerVisionTarget != null && player.PlayerVisionTarget.VisionTransform != null
                ? player.PlayerVisionTarget.VisionTransform.position : player.transform.position + Vector3.up;
            return !Hidden(__instance.VisionTransform.position, target);
        }
    }

    [HarmonyPatch(typeof(RunManager), nameof(RunManager.ChangeLevel))]
    private static class Reset
    {
        [HarmonyPostfix]
        private static void Postfix() => ClearLevel();
    }
}
