using System;

namespace IncreaseHardMod.Challenges;

public enum ChallengeKind : byte { Escort = 0, Targets = 1, Hunt = 2, Cull = 3, Smash = 4, Waves = 5, Vigil = 6, Sparks = 7, Offering = 8, Mushrooms = 9, Flag = 10 }
public enum ChallengePhase : byte { Ready = 0, Active = 1, Won = 2, Lost = 3 }
public enum ChallengeReward : byte { None = 0, SkillPoint = 1, Upgrade = 2, Cosmetic = 3, Costume = 4 }

/// <summary>
/// Engine-free rules for the shrine challenges (no Unity), shared with tests/CollectionTests: which challenge, how
/// long, how many, when it is won or lost, the reward roll and the HUD words (English and Russian).
/// </summary>
public static class ChallengeRules
{
    public const int KindCount = 11;
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
    // Mushroom Path: mushrooms light up in a sequence; jump on them in the same order. Three rounds, each one longer.
    public const int MushroomCount = 7, MushroomMinCount = 5, MushroomRounds = 3, MushroomMistakes = 3;
    public const float MushroomCapHeight = .55f, MushroomCapRadius = .5f, MushroomStep = .9f, MushroomSpacing = 1.7f;
    // Capture the Flag: answer the sum in chat, carry the flag from the shrine to its base, then hold it there.
    public const float FlagPickupRadius = 1.4f, FlagBaseRadius = 2.5f, FlagDefendSeconds = 120f, FlagWaveEvery = 30f;
    public const float FlagMinPath = 22f, FlagMaxPath = 50f;

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
        ChallengeKind.Mushrooms => 150f,
        ChallengeKind.Flag => 150f,   // answer and carry; holding the flag then has its own 2 minutes
        _ => 45f,
    };

    /// <summary>A player death fails the fighting challenges (and Capture the Flag while the flag is held, see the manager).</summary>
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

    /// <summary>Win reward, equally likely: a skill point for everyone, an upgrade, a cosmetic box or (when costume
    /// rewards are on) a costume piece for everyone.</summary>
    public static ChallengeReward Reward(double roll, bool costumes = false)
    {
        int options = costumes ? 4 : 3;
        return (ChallengeReward)(1 + Math.Min(options - 1, (int)(Clamp01(roll) * options)));
    }

    /// <summary>Mushroom Path: sequence length in round <paramref name="round"/> (1-based): 3, 4, 5.</summary>
    public static int MushroomLength(int round) => 2 + Math.Max(1, round);

    /// <summary>Capture the Flag question from three rolls: a three-digit sum, a three-digit difference (never negative)
    /// or a two-digit by one-digit product. op 0 = +, 1 = -, 2 = x.</summary>
    public static (int A, int B, int Op) MathQuestion(double opRoll, double aRoll, double bRoll)
    {
        int op = Math.Min(2, (int)(Clamp01(opRoll) * 3));
        if (op == 2) return (12 + (int)(Clamp01(aRoll) * 87), 3 + (int)(Clamp01(bRoll) * 7), 2);
        int a = 100 + (int)(Clamp01(aRoll) * 900), b = 100 + (int)(Clamp01(bRoll) * 900);
        if (op == 1 && b > a) (a, b) = (b, a);
        return (a, b, op);
    }

    public static int MathAnswer(int a, int b, int op) => op switch { 1 => a - b, 2 => a * b, _ => a + b };

    public static string MathText(int a, int b, int op) => $"{a} {(op == 1 ? "-" : op == 2 ? "x" : "+")} {b}";

    /// <summary>A chat line read as an answer: the first whole number in it ("631", "it's 631!"), or null.</summary>
    public static int? ParseAnswer(string message)
    {
        if (string.IsNullOrEmpty(message) || message.Length > 64) return null;
        int start = -1;
        for (int i = 0; i < message.Length; i++)
        {
            if (char.IsDigit(message[i])) { start = i; break; }
        }
        if (start < 0) return null;
        bool negative = start > 0 && message[start - 1] == '-';
        int end = start;
        while (end < message.Length && end - start < 7 && char.IsDigit(message[end])) end++;
        int value = int.Parse(message.Substring(start, end - start), System.Globalization.CultureInfo.InvariantCulture);
        return negative ? -value : value;
    }

    /// <summary>Capture the Flag HUD line for each stage: 0 the question, 1 carrying, 2 holding.</summary>
    public static string FlagStatus(int stage, string question, int carried, float remaining, bool russian)
    {
        int seconds = (int)Math.Ceiling(Math.Max(0f, remaining));
        string clock = $"{seconds / 60}:{seconds % 60:00}";
        string name = Name(ChallengeKind.Flag, russian).ToUpperInvariant();
        return stage switch
        {
            0 => russian ? $"{name}  {question} = ?  (ответ в чат)  {clock}" : $"{name}  {question} = ?  (answer in chat)  {clock}",
            1 => carried != 0 ? (russian ? $"{name}  НЕСИТЕ ФЛАГ К БАЗЕ  {clock}" : $"{name}  CARRY THE FLAG TO ITS BASE  {clock}")
                              : (russian ? $"{name}  ВОЗЬМИТЕ ФЛАГ  {clock}" : $"{name}  PICK UP THE FLAG  {clock}"),
            _ => russian ? $"{name}  УДЕРЖИВАЙТЕ  {clock}" : $"{name}  HOLD IT  {clock}",
        };
    }

    public static string Name(ChallengeKind kind, bool russian) => kind switch
    {
        ChallengeKind.Mushrooms => russian ? "Грибная тропа" : "Mushroom Path",
        ChallengeKind.Flag => russian ? "Захват флага" : "Capture the Flag",
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
        ChallengeKind.Mushrooms => russian ? $"Запомните, какие грибы светятся, и прыгайте по ним в том же порядке; {total} раунда, {MushroomMistakes} ошибки — провал"
                                           : $"Watch which mushrooms light up, then jump on them in the same order; {total} rounds, {MushroomMistakes} mistakes fail it",
        ChallengeKind.Flag => russian ? "Напишите ответ в чат, отнесите флаг на базу и удержите его 2 минуты; никто не должен умереть"
                                      : "Type the answer in chat, carry the flag to its base and hold it for 2 minutes; nobody may die",
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
            ChallengeKind.Mushrooms => $"  {(russian ? "РАУНД" : "ROUND")} {Math.Min(progress + 1, total)}/{total}",
            _ => $"  {Math.Min(progress, total)}/{total}",
        };
        return $"{Name(kind, russian).ToUpperInvariant()}{count}  {clock}";
    }

    public static string RewardText(ChallengeReward reward, bool russian) => reward switch
    {
        ChallengeReward.SkillPoint => russian ? "+1 очко навыков всем" : "+1 skill point for everyone",
        ChallengeReward.Upgrade => russian ? "улучшение у святилища" : "an upgrade at the shrine",
        ChallengeReward.Cosmetic => russian ? "коробка косметики у святилища" : "a cosmetic box at the shrine",
        ChallengeReward.Costume => russian ? "часть костюма всем" : "a costume piece for everyone",
        _ => "",
    };

    private static double Clamp01(double v) => v < 0 ? 0 : v > .999999 ? .999999 : v;
}
