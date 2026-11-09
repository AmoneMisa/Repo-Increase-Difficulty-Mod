using System;

namespace IncreaseHardMod.Skills;

/// <summary>
/// Page bonuses: points spent on a page's normal perks also raise one of the game's own main stats, the same as a
/// shop upgrade. Every <see cref="PointsPerLevel"/> points give +1 level, up to <see cref="MaxLevels"/> per page:
/// Combat → Power (grab strength), Survival → Health, Recon → Sprint Speed, Courier → Stamina, Arsenal → Tumble
/// Launch, Utility → Range and Death Head Battery. Trade-offs and contract perks don't count (the same points
/// <see cref="SkillRules.PageSpent"/> uses for the trade-off gates). Stat names are the game's own upgrade names
/// (PunManager.TesterUpgradeCommandRPC).
/// </summary>
internal static class SectionBonusRules
{
    internal const int PointsPerLevel = 10, MaxLevels = 3;

    internal const string Strength = "Strength", Health = "Health", Speed = "Speed", Stamina = "Stamina",
        Launch = "Launch", Range = "Range", DeathHeadBattery = "DeathHeadBattery";

    private static readonly string[] None = Array.Empty<string>();
    private static readonly string[] CombatStats = { Strength }, SurvivalStats = { Health }, ReconStats = { Speed },
        CourierStats = { Stamina }, ArsenalStats = { Launch }, UtilityStats = { Range, DeathHeadBattery };

    /// <summary>Bonus levels a page gives for the points spent on its normal perks.</summary>
    internal static int Levels(int pageSpent) => Math.Min(MaxLevels, Math.Max(0, pageSpent) / PointsPerLevel);

    /// <summary>Points still needed on the page for its next bonus level, or 0 once the page is maxed.</summary>
    internal static int ToNext(int pageSpent)
    {
        int levels = Levels(pageSpent);
        return levels >= MaxLevels ? 0 : (levels + 1) * PointsPerLevel - Math.Max(0, pageSpent);
    }

    /// <summary>The game stats a page raises (empty for Contracts).</summary>
    internal static string[] Stats(SkillPage page) => page switch
    {
        SkillPage.Combat => CombatStats,
        SkillPage.Survival => SurvivalStats,
        SkillPage.Recon => ReconStats,
        SkillPage.Courier => CourierStats,
        SkillPage.Arsenal => ArsenalStats,
        SkillPage.Utility => UtilityStats,
        _ => None,
    };

    /// <summary>Change to make to one stat so the granted levels match what the page earns now.</summary>
    internal static int Delta(int granted, int pageSpent) => Levels(pageSpent) - Math.Max(0, granted);

    internal static string StatName(string stat, bool russian) => stat switch
    {
        Strength => russian ? "Сила" : "Power",
        Health => russian ? "Здоровье" : "Health",
        Speed => russian ? "Скорость бега" : "Sprint Speed",
        Stamina => russian ? "Выносливость" : "Stamina",
        Launch => russian ? "Бросок кувырком" : "Tumble Launch",
        Range => russian ? "Дальность захвата" : "Range",
        DeathHeadBattery => russian ? "Батарея головы" : "Death Head Battery",
        _ => stat,
    };
}
