using System;

namespace IncreaseHardMod.Challenges;

public enum ChallengeKind : byte { Escort = 0, Targets = 1, Hunt = 2, Cull = 3, Smash = 4, Waves = 5, Vigil = 6, Sparks = 7, Offering = 8 }
public enum ChallengePhase : byte { Ready = 0, Active = 1, Won = 2, Lost = 3 }
public enum ChallengeReward : byte { None = 0, SkillPoint = 1, Upgrade = 2, Cosmetic = 3 }

/// <summary>
/// Engine-free rules for the shrine challenges (no Unity), shared with tests/CollectionTests: which challenge, how
/// long, how many, when it is won or lost, the reward roll and the HUD words (English and Russian).
/// </summary>
public static class ChallengeRules
{
    public const int KindCount = 9;
    public const float StartRadius = 2f, PromptRadius = 3.5f; // press interact within 2 m; the prompt shows from 3.5 m
    public const float EscortFollowRadius = 5f, EscortSpeed = 2.4f, EscortArriveRadius = .7f, EscortMinPath = 25f, EscortMaxPath = 55f;
    public const float TargetHitRadius = .45f;
    public const float RootRadius = 6f; // Target Practice roots every living player this close to the shrine when it starts
    public const int MinProps = 6, MaxProps = 10;
    public const float CrateImpactSpeed = 4f;
    public const int WaveCount = 3;
    public const float WaveBreak = 5f;                       // seconds between a cleared wave and the next one
    public const float VigilRadius = 4f, VigilSeconds = 40f; // stand in the ring this long (any living player counts)
    public const int SparkCount = 8;
    public const float SparkRadius = 1.4f, SparkMinDistance = 8f, SparkMaxDistance = 45f;
    public const float OfferingRadius = 3f;

    public static ChallengeKind Kind(double roll) => (ChallengeKind)Math.Min(KindCount - 1, (int)(Clamp01(roll) * KindCount));

    /// <summary>Seconds to finish once started.</summary>
    public static float Seconds(ChallengeKind kind) => kind switch
    {
        ChallengeKind.Escort => 135f,
        ChallengeKind.Targets => 45f,
        ChallengeKind.Hunt => 150f,
        ChallengeKind.Cull => 150f,
        ChallengeKind.Waves => 240f,
        ChallengeKind.Vigil => 120f,
        ChallengeKind.Sparks => 120f,
        ChallengeKind.Offering => 180f,
        _ => 45f,
    };

    /// <summary>A player death fails the fighting challenges.</summary>
    public static bool DeathFails(ChallengeKind kind) => kind is ChallengeKind.Hunt or ChallengeKind.Cull or ChallengeKind.Waves;

    /// <summary>Monsters to kill in Cull: four plus one per player, at most nine.</summary>
    public static int CullTarget(int players) => Math.Min(9, 4 + Math.Max(1, players));

    /// <summary>Monsters in wave <paramref name="wave"/> (1-based): 1, 2, 3, one more with three or more players, at most 4.</summary>
    public static int WaveSize(int wave, int players) => Math.Max(1, Math.Min(4, wave + (players >= 3 ? 1 : 0)));

    /// <summary>Offering goal: 15% of the loot on the level, rounded to $500, between $2,000 and $20,000.</summary>
    public static int OfferingTarget(double levelValue)
    {
        double goal = Math.Round(Math.Max(0, levelValue) * .15 / 500.0) * 500.0;
        return (int)Math.Max(2000, Math.Min(20000, goal));
    }

    /// <summary>"$1.5K" style money, like the game's HUD.</summary>
    public static string Money(int dollars) => dollars >= 1000
        ? "$" + (dollars / 1000.0).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) + "K" : "$" + dollars;

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
        ChallengeKind.Waves => russian ? "Волны монстров" : "Monster Waves",
        ChallengeKind.Vigil => russian ? "Бдение" : "Vigil",
        ChallengeKind.Sparks => russian ? "Сбор искр" : "Spark Hunt",
        ChallengeKind.Offering => russian ? "Подношение" : "Offering",
        _ => russian ? "Разгром" : "Smash",
    };

    public static string Goal(ChallengeKind kind, int total, bool russian) => kind switch
    {
        ChallengeKind.Escort => russian ? "Держитесь рядом с огоньком, пока он не доберётся домой" : "Stay near the wisp until it reaches its home",
        ChallengeKind.Targets => russian ? $"Подстрелите все мишени ({total}), не сходя с места; оружие не тратит заряд" : $"Shoot all {total} targets without moving; guns use no battery",
        ChallengeKind.Hunt => russian ? "Убейте отмеченного монстра; никто не должен умереть" : "Kill the marked monster; nobody may die",
        ChallengeKind.Cull => russian ? $"Убейте {total} монстров; никто не должен умереть" : $"Kill {total} monsters; nobody may die",
        ChallengeKind.Waves => russian ? $"Отбейте {total} волны монстров у святилища; никто не должен умереть" : $"Beat {total} monster waves at the shrine; nobody may die",
        ChallengeKind.Vigil => russian ? $"Стойте в кругу у святилища {total} с; монстров тянет на свет" : $"Stand in the shrine's ring for {total} s; the light draws monsters",
        ChallengeKind.Sparks => russian ? $"Соберите все искры ({total}) по уровню" : $"Collect all {total} sparks around the level",
        ChallengeKind.Offering => russian ? $"Принесите добычу на {Money(total)} в круг у святилища" : $"Bring {Money(total)} of loot into the shrine's ring",
        _ => russian ? $"Разбейте все ящики ({total}): удар, выстрел, кувырок или бросок" : $"Break all {total} crates: hit, shoot, tumble or throw",
    };

    /// <summary>The HUD line while a challenge runs, e.g. "TARGET PRACTICE  4/8  0:31".</summary>
    public static string Status(ChallengeKind kind, int progress, int total, float remaining, bool russian)
    {
        int seconds = (int)Math.Ceiling(Math.Max(0f, remaining));
        string clock = $"{seconds / 60}:{seconds % 60:00}";
        string count = kind switch
        {
            ChallengeKind.Escort => "",
            ChallengeKind.Waves => $"  {(russian ? "ВОЛНА" : "WAVE")} {Math.Min(progress + 1, total)}/{total}",
            ChallengeKind.Offering => $"  {Money(Math.Max(0, progress))}/{Money(total)}",
            _ => $"  {Math.Min(progress, total)}/{total}",
        };
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
