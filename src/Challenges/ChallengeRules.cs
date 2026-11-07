using System;

namespace IncreaseHardMod.Challenges;

public enum ChallengeKind : byte { Escort = 0, Targets = 1, Hunt = 2, Cull = 3, Smash = 4 }
public enum ChallengePhase : byte { Ready = 0, Active = 1, Won = 2, Lost = 3 }
public enum ChallengeReward : byte { None = 0, SkillPoint = 1, Upgrade = 2, Cosmetic = 3 }

/// <summary>
/// Engine-free rules for the shrine challenges (no Unity), shared with tests/CollectionTests: which challenge, how
/// long, how many, when it is won or lost, the reward roll and the HUD words (English and Russian).
/// </summary>
public static class ChallengeRules
{
    public const int KindCount = 5;
    public const float StartRadius = 2f, PromptRadius = 3.5f; // press interact within 2 m; the prompt shows from 3.5 m
    public const float EscortFollowRadius = 6f, EscortSpeed = 2.4f, EscortArriveRadius = .7f, EscortMinPath = 25f, EscortMaxPath = 55f;
    public const float TargetHitRadius = .45f;
    public const int MinProps = 6, MaxProps = 10;
    public const float CrateImpactSpeed = 4f;

    public static ChallengeKind Kind(double roll) => (ChallengeKind)Math.Min(KindCount - 1, (int)(Clamp01(roll) * KindCount));

    /// <summary>Seconds to finish once started.</summary>
    public static float Seconds(ChallengeKind kind) => kind switch
    {
        ChallengeKind.Escort => 150f,
        ChallengeKind.Targets => 45f,
        ChallengeKind.Hunt => 180f,
        ChallengeKind.Cull => 150f,
        _ => 60f,
    };

    /// <summary>A player death fails the fighting challenges.</summary>
    public static bool DeathFails(ChallengeKind kind) => kind is ChallengeKind.Hunt or ChallengeKind.Cull;

    /// <summary>Monsters to kill in Cull: three plus one per player, at most eight.</summary>
    public static int CullTarget(int players) => Math.Min(8, 3 + Math.Max(1, players));

    /// <summary>How many targets or crates: between 6 and 10, from a roll.</summary>
    public static int PropCount(double roll) => MinProps + (int)(Clamp01(roll) * (MaxProps - MinProps + 1) * .999999);

    public static bool Expired(float startedAt, float now, ChallengeKind kind) => now - startedAt >= Seconds(kind);

    public static float Remaining(float startedAt, float now, ChallengeKind kind) => Math.Max(0f, Seconds(kind) - (now - startedAt));

    public static bool Done(int progress, int total) => total > 0 && progress >= total;

    /// <summary>Win reward: a skill point for everyone, an upgrade or a cosmetic box, equally likely.</summary>
    public static ChallengeReward Reward(double roll) => (ChallengeReward)(1 + Math.Min(2, (int)(Clamp01(roll) * 3)));

    public static string Name(ChallengeKind kind, bool russian) => kind switch
    {
        ChallengeKind.Escort => russian ? "Проводи огонька" : "Wisp Escort",
        ChallengeKind.Targets => russian ? "Стрельба по мишеням" : "Target Practice",
        ChallengeKind.Hunt => russian ? "Охота" : "Hunt",
        ChallengeKind.Cull => russian ? "Зачистка" : "Cull",
        _ => russian ? "Разгром" : "Smash",
    };

    public static string Goal(ChallengeKind kind, int total, bool russian) => kind switch
    {
        ChallengeKind.Escort => russian ? "Держитесь рядом с огоньком, пока он не доберётся домой" : "Stay near the wisp until it reaches its home",
        ChallengeKind.Targets => russian ? $"Подстрелите все мишени ({total}); оружие не тратит заряд" : $"Shoot all {total} targets; guns use no battery",
        ChallengeKind.Hunt => russian ? "Убейте отмеченного монстра; никто не должен умереть" : "Kill the marked monster; nobody may die",
        ChallengeKind.Cull => russian ? $"Убейте {total} монстров; никто не должен умереть" : $"Kill {total} monsters; nobody may die",
        _ => russian ? $"Разбейте все ящики ({total}): удар, выстрел, кувырок или бросок" : $"Break all {total} crates: hit, shoot, tumble or throw",
    };

    /// <summary>The HUD line while a challenge runs, e.g. "TARGET PRACTICE  4/8  0:31".</summary>
    public static string Status(ChallengeKind kind, int progress, int total, float remaining, bool russian)
    {
        int seconds = (int)Math.Ceiling(Math.Max(0f, remaining));
        string clock = $"{seconds / 60}:{seconds % 60:00}";
        string count = kind == ChallengeKind.Escort ? "" : $"  {Math.Min(progress, total)}/{total}";
        return $"{Name(kind, russian).ToUpperInvariant()}{count}  {clock}";
    }

    public static string RewardText(ChallengeReward reward, bool russian) => reward switch
    {
        ChallengeReward.SkillPoint => russian ? "+1 очко навыков всем" : "+1 skill point for everyone",
        ChallengeReward.Upgrade => russian ? "улучшение у святилища" : "an upgrade at the shrine",
        ChallengeReward.Cosmetic => russian ? "коробка косметики у святилища" : "a cosmetic box at the shrine",
        _ => "",
    };

    private static double Clamp01(double v) => v < 0 ? 0 : v > .999999 ? .999999 : v;
}
