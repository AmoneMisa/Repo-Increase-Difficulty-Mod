using System;

namespace IncreaseHardMod.Skills;

/// <summary>
/// Page bonuses: points spent on a page's normal perks also raise the game's own main stats, the same as a shop
/// upgrade. Every <see cref="PointsPerLevel"/> points give +1 level to each of the page's stats, up to that stat's own
/// cap (<see cref="MaxLevel"/>): Combat → Power +5, Survival → Health +5, Recon → Sprint Speed +3, Courier → Stamina
/// +5 and Crouch Rest +3, Arsenal → Tumble Launch +3, Utility → Range +3 and Death Head Battery +5. Trade-offs and
/// contract perks don't count (the same points <see cref="SkillRules.PageSpent"/> uses for the trade-off gates).
/// Stat names are the game's own upgrade names (PunManager.TesterUpgradeCommandRPC).
/// </summary>
internal static class SectionBonusRules
{
    internal const int PointsPerLevel = 5;

    internal const string Strength = "Strength", Health = "Health", Speed = "Speed", Stamina = "Stamina",
        Launch = "Launch", Range = "Range", DeathHeadBattery = "DeathHeadBattery", CrouchRest = "CrouchRest";

    private static readonly string[] None = Array.Empty<string>();
    private static readonly string[] CombatStats = { Strength }, SurvivalStats = { Health }, ReconStats = { Speed },
        CourierStats = { Stamina, CrouchRest }, ArsenalStats = { Launch }, UtilityStats = { Range, DeathHeadBattery };

    /// <summary>The most bonus levels a stat can get from its page.</summary>
    internal static int MaxLevel(string stat) => stat switch
    {
        Strength or Health or Stamina or DeathHeadBattery => 5,
        _ => 3, // Speed, Range, CrouchRest, Launch
    };

    /// <summary>Bonus levels a stat gets for the points spent on its page's normal perks.</summary>
    internal static int Levels(string stat, int pageSpent) => Math.Min(MaxLevel(stat), Math.Max(0, pageSpent) / PointsPerLevel);

    /// <summary>The highest cap among a page's stats: the page stops paying out past this many levels.</summary>
    internal static int PageMax(SkillPage page)
    {
        int max = 0;
        foreach (string stat in Stats(page)) max = Math.Max(max, MaxLevel(stat));
        return max;
    }

    /// <summary>Points still needed on the page for its next bonus level, or 0 once every stat of the page is maxed.</summary>
    internal static int ToNext(SkillPage page, int pageSpent)
    {
        int spent = Math.Max(0, pageSpent);
        int levels = spent / PointsPerLevel;
        return levels >= PageMax(page) ? 0 : (levels + 1) * PointsPerLevel - spent;
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
    internal static int Delta(string stat, int granted, int pageSpent) => Levels(stat, pageSpent) - Math.Max(0, granted);

    internal static string StatName(string stat, bool russian) => stat switch
    {
        Strength => russian ? "Сила" : "Power",
        Health => russian ? "Здоровье" : "Health",
        Speed => russian ? "Скорость бега" : "Sprint Speed",
        Stamina => russian ? "Выносливость" : "Stamina",
        Launch => russian ? "Бросок кувырком" : "Tumble Launch",
        Range => russian ? "Дальность захвата" : "Range",
        DeathHeadBattery => russian ? "Батарея головы" : "Death Head Battery",
        CrouchRest => russian ? "Отдых сидя" : "Crouch Rest",
        _ => stat,
    };
}
