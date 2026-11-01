using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using IncreaseHardMod.Modifiers;
using Photon.Pun;

namespace IncreaseHardMod;

/// <summary>Host difficulty settings, separate from the current level's surprise multipliers.</summary>
internal static class EnemyBalance
{
    private static float[] _remote;
    private static Dictionary<string, float> _caps;
    private static Photon.Realtime.Room _room;
    private static int _host;
    private static bool _surprises;
    private static bool Remote => _remote != null && PhotonNetwork.InRoom && !PhotonNetwork.IsMasterClient &&
        ReferenceEquals(_room, PhotonNetwork.CurrentRoom) && PhotonNetwork.MasterClient?.ActorNumber == _host;

    internal static bool Surprises => Remote ? _surprises : ModConfig.SurprisesEnabled.Value;
    internal static float Stat(StatAxis axis) => Surprises ? StatModifiers.Mult(axis) : 1f;
    internal static float Damage => Factor(2) * Stat(StatAxis.EnemyDamage);
    internal static float Speed => Factor(3) * Stat(StatAxis.EnemySpeed);
    internal static float Respawn => Factor(4) * Stat(StatAxis.EnemyRespawn);
    internal static float LightMass => Factor(5);
    internal static float HeavyMass => Factor(6);
    internal static float PitDamage => Factor(7);

    internal static float Health(string name)
    {
        float cap = Remote
            ? (name != null && _caps.TryGetValue(name, out var value) ? value : _remote[1])
            : ModConfig.HealthMaxForEnemy(name);
        return Math.Max(1f, Math.Min(Factor(0), cap)) * Stat(StatAxis.EnemyHealth);
    }

    private static float Factor(int index) => Remote ? _remote[index] : index switch
    {
        0 => ModConfig.ScaleEnemyHealth.Value ? 1f + ModConfig.EnemyHealthPerLevel.Value * Difficulty.Steps : 1f,
        1 => ModConfig.EnemyHealthMax.Value,
        2 => ModConfig.ScaleEnemyDamage.Value ? Difficulty.Multiplier(ModConfig.EnemyDamagePerLevel.Value, ModConfig.EnemyDamageMax.Value) : 1f,
        3 => ModConfig.ScaleEnemySpeed.Value ? Difficulty.Multiplier(ModConfig.EnemySpeedPerLevel.Value, ModConfig.EnemySpeedMax.Value) : 1f,
        4 => ModConfig.ScaleEnemyRespawn.Value ? Difficulty.ReduceMultiplier(ModConfig.EnemyRespawnFasterPerLevel.Value, ModConfig.EnemyRespawnMinFactor.Value) : 1f,
        5 => ModConfig.LightMonsterMaxMass.Value,
        6 => ModConfig.HeavyMonsterMinMass.Value,
        _ => ModConfig.MonsterPitDamageFactor.Value,
    };

    internal static float[] Capture() => Enumerable.Range(0, 8).Select(Factor).ToArray();
    internal static string Signature() => string.Join("|", Capture().Select(v => v.ToString("R", CultureInfo.InvariantCulture))) +
        "|" + ModConfig.EnemyHealthMaxPerEnemy.Value + "|" + ModConfig.SurprisesEnabled.Value;

    internal static bool Apply(float[] factors, string caps, bool surprises)
    {
        if (factors == null || factors.Length != 8 || caps == null || caps.Length > 16384 ||
            factors.Any(v => float.IsNaN(v) || float.IsInfinity(v) || v < 0f || v > 1e9f) ||
            factors[0] < 1f || factors[1] < 1f || factors[2] < 1f || factors[3] < 1f ||
            factors[4] <= 0f || factors[5] <= 0f || factors[6] <= 0f || factors[7] > 5f ||
            PhotonNetwork.MasterClient == null) return false;
        var parsed = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
        foreach (string part in caps.Split(','))
        {
            int colon = part.LastIndexOf(':');
            if (colon <= 0) continue;
            string name = part.Substring(0, colon).Trim();
            if (name.Length > 0 && float.TryParse(part.Substring(colon + 1), NumberStyles.Float, CultureInfo.InvariantCulture, out float cap) &&
                !float.IsNaN(cap) && !float.IsInfinity(cap) && cap >= 1f) parsed[name] = cap;
        }
        _remote = (float[])factors.Clone();
        _caps = parsed;
        _room = PhotonNetwork.CurrentRoom;
        _host = PhotonNetwork.MasterClient.ActorNumber;
        _surprises = surprises;
        return true;
    }
}
