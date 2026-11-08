namespace IncreaseHardMod.Quests;

/// <summary>Who a quest is scored for.</summary>
public enum QuestScope : byte
{
    // Tracked host-side and shared by the whole squad (everyone completes it together).
    Team = 0,
    // Tracked locally by each player for their own character (e.g. "you take no damage").
    Personal = 1,
}

/// <summary>Flavour bucket, used for generation variety and HUD colouring.</summary>
public enum QuestCategory : byte
{
    Combat = 0,
    Extraction = 1,
    Survival = 2,
    Environment = 3,
    Funny = 4,        // silly side-quests: small reward, at most one per level, never skill points
}

/// <summary>
/// The concrete thing a quest measures. Only objectives with a reliable, host-trackable
/// (or, for Personal scope, cleanly client-trackable) hook are listed here — the wider
/// wishlist (flashlight/no-guns/melee-only/etc.) needs hooks the mod doesn't have yet and
/// is intentionally left out of this phase.
/// </summary>
public enum ObjectiveType : byte
{
    KillAny = 0,        // kill N enemies of any kind (team)
    KillType = 1,       // kill N enemies whose EnemyParent.enemyName matches Param (team)
    KillTier = 2,       // kill N enemies of EnemyParent.Difficulty index Param (team)
    PitKill = 3,        // shove N enemies to their death in a death pit (team)
    ReachHaul = 4,      // bank a total haul value of Target across the level (team)
    NoDeaths = 5,       // finish the level with fewer than Target team deaths (team)
    NoDamage = 6,       // finish the level having taken no damage (personal)
    MeleeKill = 7,      // kill N enemies with a melee weapon (team)
    NoGunKills = 8,     // finish the level having made no kills with a gun (team, "melee only")
    IntactExtract = 9,  // extract N valuables undamaged (full dollar value) (team)
    GunKill = 10,       // kill N enemies with a gun (team)
    ExtractCount = 11,  // extract N valuables of any condition (team)
    HighValueExtract = 12, // extract N valuables whose original value is at least Param dollars (team)
    SoulExtract = 13,   // extract N enemy souls (EnemyValuable orbs) (team)
    MoonRitual = 14,    // Halloween event: earn the moon's approval at an extraction point (team)
    HandKill = 15,      // kill N enemies with bare hands: grabbing, throwing, pushing - no gun or melee (team)
    StunEnemies = 16,   // stun N enemies (team)
    Revive = 17,        // revive N teammates (team)
    ExtractionRush = 18, // close every extraction point within Target seconds; Progress = elapsed seconds (team)
    HatTrick = 19,       // kill Target monsters within Param seconds of each other; Progress = best streak (team)
    CrownJewel = 20,     // extract the level's most valuable item with at least 75% of its value; Param = its name (team)
    FastOpener = 21,     // close the first extraction point within Target seconds; Progress = elapsed seconds (team)
    Oops = 22,           // (funny) break valuables worth Target dollars in total, on purpose or not (team)
    RagdollOlympics = 23, // (funny) tumble Target times in total (team)
    TalkItOut = 24,      // (funny) talk into voice chat for Target seconds in total (team)
    Ballerina = 25,      // (funny) one player spins around 5 times within 6 seconds (team)
    PayRespects = 26,    // (funny) crouch for 3 seconds where a monster died (team)
    MonsterHugger = 27,  // (funny) spend Target seconds right next to living monsters (team)
    LightsOut = 28,      // spend Target seconds in total with flashlights off while moving (team)
}

/// <summary>Which kind of weapon dealt the killing blow, for weapon-restricted quests.</summary>
public enum WeaponSource : byte
{
    Other = 0,   // fall/pit/environment/unknown
    Gun = 1,     // any ItemGun / gun bullet
    Melee = 2,   // any ItemMelee
    Hands = 3,   // grabbed / thrown objects or the grabbed enemy itself, no gun or melee involved
}

/// <summary>Contract rank ladder, F (easiest) up to SSS.</summary>
public enum QuestRank : byte
{
    F = 0, E = 1, D = 2, C = 3, B = 4, A = 5, S = 6, SS = 7, SSS = 8,
}

public enum RewardType : byte
{
    Money = 0,         // adds run currency to the shared pot
    Item = 1,          // spawns a RANDOM upgrade item on the level (hauled out like loot)
    Cosmetic = 2,      // spawns a cosmetic "gacha" box on the level (a rarity cube to roll)
    Heal = 3,          // heals the whole squad
    TempStat = 4,      // grants every player a temporary stat buff for THIS level only
    ShopItem = 5,      // spawns a random general shop item (grenade/health-pack/tool/etc.)
    ExactUpgrade = 6,  // spawns a SPECIFIC upgrade item (chosen by RewardStat)
    QuestPerk = 7,     // special contract: every player gets a level of a quest-only skill tree perk (RewardPerk)
    Costume = 8,       // every player unlocks one of the mod's costume parts they don't own yet
}

/// <summary>
/// A specific player stat, used by <see cref="RewardType.TempStat"/> (temporary, this level)
/// and <see cref="RewardType.ExactUpgrade"/> (permanent, spawns that exact upgrade item).
/// </summary>
public enum StatKind : byte
{
    Health = 0,
    Stamina = 1,
    SprintSpeed = 2,
    GrabStrength = 3,
    GrabRange = 4,
    ThrowStrength = 5,
    ExtraJump = 6,
    TumbleLaunch = 7,
}

/// <summary>
/// Quality band for an upgrade handed out as a quest reward. Higher-rank quests bias toward the
/// rarer bands (Power is the jackpot), and the band drives which upgrades are eligible.
/// </summary>
public enum UpgradeTier : byte
{
    Useless = 0,  // head battery, tumble wings/climb, crouch rest
    Mid = 1,      // sprint, throw, drone battery, charge/heal team perks
    Useful = 2,   // hp (+regen), stamina (+slower drain), map count, gun battery ("more bullets")
    Power = 3,    // grab range, extra jump, grab strength, autoscan/map-suite trackers, tumble launch
}

public static class UpgradeTierInfo
{
    public static string Label(UpgradeTier t) => t switch
    {
        UpgradeTier.Useless => "Basic",
        UpgradeTier.Mid => "Standard",
        UpgradeTier.Useful => "Rare",
        UpgradeTier.Power => "Power",
        _ => t.ToString(),
    };
}

public static class StatKindInfo
{
    public static string Label(StatKind s) => s switch
    {
        StatKind.Health => "Health",
        StatKind.Stamina => "Stamina",
        StatKind.SprintSpeed => "Sprint Speed",
        StatKind.GrabStrength => "Grab Strength",
        StatKind.GrabRange => "Grab Range",
        StatKind.ThrowStrength => "Throw Strength",
        StatKind.ExtraJump => "Extra Jump",
        StatKind.TumbleLaunch => "Tumble Launch",
        _ => s.ToString(),
    };
}

public static class QuestRankInfo
{
    private static readonly string[] Labels = { "F", "E", "D", "C", "B", "A", "S", "SS", "SSS" };

    public static string Label(QuestRank rank) => Labels[(int)rank];

    public static QuestRank Clamp(int index)
    {
        if (index < 0) index = 0;
        if (index > (int)QuestRank.SSS) index = (int)QuestRank.SSS;
        return (QuestRank)index;
    }

    public static QuestRank Next(QuestRank rank) =>
        rank >= QuestRank.SSS ? QuestRank.SSS : (QuestRank)((int)rank + 1);

    /// <summary>
    /// Difficulty/reward multiplier that grows with rank. F = 1x, each rank up adds 35%,
    /// so SSS (rank 8) is ~3.8x the base targets and rewards.
    /// </summary>
    public static float Scale(QuestRank rank) => 1f + 0.35f * (int)rank;
}

/// <summary>
/// A single live quest instance. Templates in <see cref="QuestCatalog"/> stamp out these
/// with concrete targets/rewards; the manager mutates <see cref="Progress"/> and
/// <see cref="Completed"/> as the level plays out.
/// </summary>
public sealed class ActiveQuest
{
    public int Id;                 // unique per active set, so clients can match on updates
    public QuestScope Scope;
    public QuestCategory Category;
    public ObjectiveType Objective;
    public QuestRank Rank;
    public int Target;             // amount needed
    public int Progress;           // amount so far
    public string Param = "";      // enemy name (KillType) or tier index text (KillTier)
    public RewardType RewardType;
    public int RewardAmount;       // money amount, heal HP, temp-stat tiers, or item count
    public StatKind RewardStat;    // which stat (TempStat reward only)
    public UpgradeTier RewardTier; // upgrade quality band (ExactUpgrade reward only)
    public string RewardItem = ""; // item display name once granted (for the HUD)
    public int RewardPoints;       // skill points on top of the reward (only some quests have them)
    public int RewardPerk = -1;    // QuestPerk reward: index into SkillCatalog.QuestPerks
    public bool Special;           // a special contract: harder, and rewards a quest-only perk
    public string Title = "";      // human-readable objective line
    public bool Completed;
    public bool Failed;            // survival quests flip this on the disqualifying event

    /// <summary>A stable signature used to avoid handing out the same quest twice in a run.</summary>
    public string Signature() => $"{Objective}:{Param}";

    public bool IsDone => Completed;

    public void Add(int amount)
    {
        if (Completed || Failed)
            return;
        Progress += amount;
        if (Progress >= Target)
        {
            Progress = Target;
            Completed = true;
        }
    }
}
