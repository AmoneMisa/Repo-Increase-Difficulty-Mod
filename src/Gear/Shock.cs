using System.Collections.Generic;
using UnityEngine;

namespace IncreaseHardMod.Gear;

/// <summary>
/// Shocked: what a Wizard Staff bolt does to whatever it strikes, for <see cref="ModConfig.StaffShockSeconds"/>.
///   * Players (their own client): 50% slower movement through the game's own speed override, stamina recovering half
///     as fast, and the SHOCKED screen frame and card (HudConditions). The host sends it like poison and sedation.
///   * Monsters (every client, sent like poison and ice as enemy status kind <see cref="EnemyKind"/>): the host slows
///     their navigation agent by half, and the damage they deal to players is cut by 25% (EnemyDamagePatch, on the
///     hurt player's client). A flickering blue light shows the shock.
/// </summary>
internal static class Shock
{
    internal const byte EnemyKind = 3;
    internal const float SlowFactor = .5f, StaminaFactor = .5f, MonsterDamageFactor = .75f;

    private static readonly Dictionary<Enemy, float> Enemies = new();
    private static float _localUntil;

    internal static bool IsShocked(Enemy enemy) => enemy != null && Enemies.TryGetValue(enemy, out float until) && until > Time.time;
    internal static bool LocalShocked => Time.time < _localUntil;

    /// <summary>Every client: a monster is shocked for <paramref name="seconds"/> (from EnemyStatusVisual.Apply).</summary>
    internal static void MarkEnemy(Enemy enemy, float seconds)
    {
        if (enemy == null || seconds <= 0f) return;
        Enemies[enemy] = Mathf.Max(Enemies.TryGetValue(enemy, out float until) ? until : 0f, Time.time + Mathf.Min(seconds, 30f));
        var spark = enemy.GetComponent<ShockSpark>() ?? enemy.gameObject.AddComponent<ShockSpark>();
        spark.Until = Enemies[enemy];
    }

    /// <summary>The local player was shocked: slow down now; stamina and the screen effect follow in <see cref="Controller"/>.</summary>
    internal static void ShockLocal(float seconds)
    {
        if (seconds <= 0f) return;
        _localUntil = Mathf.Max(_localUntil, Time.time + Mathf.Min(seconds, 30f));
        if (PlayerController.instance != null) PlayerController.instance.OverrideSpeed(SlowFactor, _localUntil - Time.time);
    }

    /// <summary>Host: slow shocked monsters. Local: halve stamina recovery while shocked.</summary>
    internal sealed class Controller : MonoBehaviour
    {
        private float _lastEnergy;
        private PlayerController _controller;

        private void Update()
        {
            if (Enemies.Count == 0) return;
            bool host = SemiFunc.IsMasterClientOrSingleplayer();
            List<Enemy> ended = null;
            foreach (var pair in Enemies)
            {
                var enemy = pair.Key;
                if (enemy == null || pair.Value <= Time.time) { (ended ??= new()).Add(enemy); continue; }
                if (host && enemy.HasNavMeshAgent && enemy.NavMeshAgent != null)
                    enemy.NavMeshAgent.OverrideAgent(enemy.NavMeshAgent.DefaultSpeed * SlowFactor, enemy.NavMeshAgent.DefaultAcceleration * SlowFactor, .2f);
            }
            if (ended != null) foreach (var e in ended) Enemies.Remove(e);
        }

        private void LateUpdate()
        {
            var pc = PlayerController.instance;
            if (pc != _controller) { _controller = pc; _lastEnergy = pc != null ? pc.EnergyCurrent : 0f; }
            if (pc == null) return;
            float now = pc.EnergyCurrent, gained = now - _lastEnergy;
            // Whatever the game recharged since last frame, take half of it back while shocked.
            if (LocalShocked && gained > 0f && gained < 20f) { now -= gained * (1f - StaminaFactor); pc.EnergyCurrent = now; }
            _lastEnergy = now;
        }
    }

    /// <summary>A flickering blue light on a shocked monster, removed when the shock ends.</summary>
    private sealed class ShockSpark : MonoBehaviour
    {
        internal float Until;
        private Light _light;

        private void Update()
        {
            if (Time.time >= Until) { if (_light != null) Destroy(_light.gameObject); Destroy(this); return; }
            if (_light == null)
            {
                var go = new GameObject("IHM Shock Spark");
                go.transform.SetParent(transform, false);
                go.transform.localPosition = Vector3.up;
                _light = go.AddComponent<Light>();
                _light.type = LightType.Point;
                _light.color = new Color(.55f, .8f, 1f);
                _light.range = 3f;
                _light.shadows = LightShadows.None;
            }
            _light.intensity = Random.value < .25f ? 0f : 1.2f + Random.value * 2f;
        }
    }
}
