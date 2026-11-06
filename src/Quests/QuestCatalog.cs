using System.Collections.Generic;
using UnityEngine;

namespace IncreaseHardMod.Quests;

/// <summary>
/// A snapshot of the level's context, gathered once at generation time, that the
/// generator uses to pick sensible + eligible quests (only offer a monster-type hunt
/// if that monster is actually present, only offer a pit quest if the level has pits, etc).
/// </summary>
public sealed class QuestContext
{
    public int Players = 1;
    public int Day;                       // levelsCompleted, drives late-run difficulty
    public QuestRank Rank = QuestRank.F;   // the run owner's contract rank
    public bool HasPits;
    public bool Halloween;                 // the Halloween event (moon ritual) is active
    public int Towers;                     // extraction points that can host the moon's judging
    public int HaulGoal;                   // RoundDirector.haulGoal for extraction quests
    public int ValuableCount;              // how many ValuableObjects are on the level (intact-extract gating)
    public string CrownJewel;              // display name of the level's most valuable item (Crown Jewel quest)
    public int CrownJewelValue;            // its original dollar value
    public readonly List<string> EnemyNames = new();
    public readonly List<int> EnemyTiers = new();  // distinct EnemyParent.Difficulty indices present
}

/// <summary>
/// Builds the live quest set for a level from a <see cref="QuestContext"/>, applying the
/// user's scaling rules (kills grow with team size, money grows with team size, targets
/// grow with rank/day) and eligibility gating (no pit/monster quests when they don't apply),
/// while avoiding handing out a signature that was already completed earlier in the run.
/// </summary>
public static class QuestCatalog
{
    // kills target multiplier: 0.8 + 0.4 * players (1p=1.2, 4p=2.4, 6p=3.2).
    private static float KillFactor(int players) => 0.8f + 0.4f * players;

    // money multiplier from team size: +25% per extra player past the first.
    private static float MoneyFactor(int players) => 1f + 0.25f * Mathf.Max(0, players - 1);

    // small extra push on later days so deep runs ask for a bit more.
    private static float DayFactor(int day) => 1f + 0.03f * Mathf.Max(0, day);

    private static int MoneyReward(QuestContext ctx)
    {
        float baseMoney = Mathf.Max(0, ModConfig.QuestMoneyBase.Value);
        float amount = baseMoney * MoneyFactor(ctx.Players) * QuestRankInfo.Scale(ctx.Rank) * DayFactor(ctx.Day);
        // round to a tidy 100.
        return Mathf.Max(100, Mathf.RoundToInt(amount / 100f) * 100);
    }

    // Stats we hand out as temporary (this-level) buffs — mobility/handling boosts that revert cleanly.
    private static readonly StatKind[] TempStatPool =
        { StatKind.Stamina, StatKind.SprintSpeed, StatKind.ExtraJump, StatKind.GrabStrength };

    /// <summary>Skill points a finished quest of this rank pays, on top of its normal reward.</summary>
    public static int SkillPointsFor(QuestRank rank) =>
        Mathf.Max(0, rank >= QuestRank.SS ? ModConfig.QuestSkillPointsHighRank.Value : ModConfig.QuestSkillPoints.Value);

    /// <summary>Chance that a quest of this rank comes with skill points: the base chance, +5% per rank, at most 75%.</summary>
    public static float SkillPointChance(QuestRank rank) =>
        Mathf.Clamp(ModConfig.QuestSkillPointChance.Value + .05f * (int)rank, 0f, .75f);

    // Only some quests give skill points (more often at higher ranks); funny ones and contracts never do.
    private static int RollSkillPoints(ActiveQuest q) =>
        q.Category == QuestCategory.Funny || q.Special || Random.value >= SkillPointChance(q.Rank) ? 0 : SkillPointsFor(q.Rank);

    private static void AssignReward(ActiveQuest q, QuestContext ctx)
    {
        q.RewardPoints = RollSkillPoints(q);
        // Funny quests pay a little cash and nothing fancy.
        if (q.Category == QuestCategory.Funny)
        {
            q.RewardType = RewardType.Money;
            q.RewardAmount = Mathf.Max(100, Mathf.RoundToInt(MoneyReward(ctx) * .5f / 100f) * 100);
            return;
        }
        // Easy quests (below rank C) always pay cash. From C upward a "special" reward becomes
        // possible, with tighter gates on the fancier payouts:
        //   - upgrade items (random + tiered): rank C+
        //   - cosmetic gacha boxes:            rank A+
        // The tier of an upgrade is gated further inside QuestUpgradePool (Mid from C, Useful from
        // A, Power from SS).
        if (q.Rank >= QuestRank.C)
        {
            var pool = new List<RewardType>();
            if (ModConfig.QuestItemRewardsEnabled.Value) pool.Add(RewardType.Item);
            if (ModConfig.QuestExactUpgradeRewardsEnabled.Value) pool.Add(RewardType.ExactUpgrade);
            if (ModConfig.QuestShopItemRewardsEnabled.Value) pool.Add(RewardType.ShopItem);
            if (ModConfig.QuestHealRewardsEnabled.Value) pool.Add(RewardType.Heal);
            if (ModConfig.QuestTempStatRewardsEnabled.Value) pool.Add(RewardType.TempStat);
            if (q.Rank >= QuestRank.A && ModConfig.QuestCosmeticRewardsEnabled.Value)
                pool.Add(RewardType.Cosmetic);

            if (pool.Count > 0 && Random.value < 0.5f)
            {
                ApplySpecialReward(q, pool[Random.Range(0, pool.Count)]);
                return;
            }
        }

        q.RewardType = RewardType.Money;
        q.RewardAmount = MoneyReward(ctx);
    }

    private static void ApplySpecialReward(ActiveQuest q, RewardType type)
    {
        q.RewardType = type;
        switch (type)
        {
            case RewardType.Heal:
                q.RewardAmount = Mathf.Max(1, ModConfig.QuestHealAmount.Value);
                break;
            case RewardType.TempStat:
                q.RewardStat = TempStatPool[Random.Range(0, TempStatPool.Length)];
                // A bit stronger on higher ranks (2..4 tiers for the level).
                q.RewardAmount = 2 + Mathf.Clamp((int)q.Rank - (int)QuestRank.C, 0, 2);
                break;
            case RewardType.ExactUpgrade:
                // Roll a quality band weighted by rank (Power is the rare jackpot at high ranks).
                q.RewardTier = QuestUpgradePool.PickTierByRank(q.Rank);
                q.RewardAmount = 1;
                break;
            default:  // Item / Cosmetic / ShopItem
                q.RewardAmount = 1;
                break;
        }
    }

    /// <summary>Generate the team quest set (2..3) plus a personal quest for the local player.</summary>
    public static List<ActiveQuest> GenerateTeam(QuestContext ctx, HashSet<string> usedSignatures)
    {
        var result = new List<ActiveQuest>();
        var builders = new List<System.Func<ActiveQuest>>();

        builders.Add(() => BuildKillAny(ctx));
        builders.Add(() => BuildMeleeKill(ctx));
        builders.Add(() => BuildNoGunKills(ctx));
        builders.Add(() => BuildGunKill(ctx));
        builders.Add(() => BuildHandKill(ctx));
        builders.Add(() => BuildStun(ctx));
        if (ctx.Players > 1)
            builders.Add(() => BuildRevive(ctx));
        if (ctx.Halloween && ctx.Towers > 0)
            builders.Add(() => BuildMoonRitual(ctx));
        if (ctx.EnemyNames.Count > 0)
            builders.Add(() => BuildSoulExtract(ctx));
        if (ctx.HaulGoal > 0)
            builders.Add(() => BuildExtractionRush(ctx));
        builders.Add(() => BuildHatTrick(ctx, special: false));
        builders.Add(() => BuildFastOpener(ctx, special: false));
        builders.Add(() => BuildLightsOut(ctx));
        if (!string.IsNullOrEmpty(ctx.CrownJewel))
            builders.Add(() => BuildCrownJewel(ctx, special: false));
        // Funny ones: at most one per level (see the loop below).
        if (ModConfig.QuestFunnyEnabled.Value)
        {
            builders.Add(() => BuildOops(ctx));
            builders.Add(() => BuildRagdollOlympics(ctx));
            builders.Add(() => BuildTalkItOut(ctx));
            builders.Add(() => BuildBallerina(ctx));
            builders.Add(() => BuildPayRespects(ctx));
            builders.Add(() => BuildMonsterHugger(ctx));
        }

        if (ctx.EnemyNames.Count > 0)
            builders.Add(() => BuildKillType(ctx));
        if (ctx.EnemyTiers.Count > 0)
            builders.Add(() => BuildKillTier(ctx));
        if (ctx.HasPits)
            builders.Add(() => BuildPitKill(ctx));
        if (ctx.HaulGoal > 0)
            builders.Add(() => BuildReachHaul(ctx));
        if (ctx.ValuableCount > 0)
            builders.Add(() => BuildIntactExtract(ctx));
        if (ctx.ValuableCount > 0)
            builders.Add(() => BuildExtractCount(ctx));
        if (ctx.ValuableCount > 0 && ctx.HaulGoal > 0)
            builders.Add(() => BuildHighValueExtract(ctx));
        builders.Add(() => BuildNoDeaths(ctx));

        int want = Random.Range(
            Mathf.Max(1, ModConfig.QuestsPerLevelMin.Value),
            Mathf.Max(1, ModConfig.QuestsPerLevelMax.Value) + 1);

        int guard = 0;
        while (result.Count < want && guard++ < 40 && builders.Count > 0)
        {
            var q = builders[Random.Range(0, builders.Count)].Invoke();
            if (q == null)
                continue;
            string sig = q.Signature();
            if (usedSignatures.Contains(sig))
                continue;
            if (result.Exists(existing => existing.Signature() == sig))
                continue;
            if (q.Category == QuestCategory.Funny && result.Exists(existing => existing.Category == QuestCategory.Funny))
                continue;

            AssignReward(q, ctx);
            q.Id = result.Count + 1;
            result.Add(q);
        }

        // Special contract: a harder extra quest that rewards a quest-only skill tree perk.
        if (ModConfig.PlayerUpgradesEnabled.Value && Random.value < ModConfig.QuestContractChance.Value)
        {
            var contract = BuildContract(ctx, usedSignatures, result);
            if (contract != null)
            {
                contract.Id = result.Count + 1;
                result.Add(contract);
            }
        }

        return result;
    }

    private static ActiveQuest BuildContract(QuestContext ctx, HashSet<string> used, List<ActiveQuest> taken)
    {
        var options = new List<System.Func<ActiveQuest>>
        {
            () => Harder(BuildKillAny(ctx), 1.6f, q => $"Kill {q.Target} enemies"),
            () => BuildHatTrick(ctx, special: true),
            () => BuildFastOpener(ctx, special: true),
        };
        if (ctx.ValuableCount > 0) options.Add(() => Harder(BuildIntactExtract(ctx), 1.5f, q => $"Extract {q.Target} valuables undamaged"));
        if (!string.IsNullOrEmpty(ctx.CrownJewel)) options.Add(() => BuildCrownJewel(ctx, special: true));
        if (ctx.HaulGoal > 0) options.Add(() => BuildExtractionRush(ctx));
        int perks = 0;
        foreach (var node in Skills.SkillCatalog.All) if (node.QuestOnly) perks++;
        while (options.Count > 0 && perks > 0)
        {
            int pick = Random.Range(0, options.Count);
            var q = options[pick]();
            options.RemoveAt(pick);
            if (q == null) continue;
            string sig = q.Signature();
            if (used.Contains(sig) || taken.Exists(t => t.Signature() == sig)) continue;
            q.Special = true;
            q.Title = "CONTRACT: " + q.Title;
            q.RewardType = RewardType.QuestPerk;
            q.RewardAmount = 1;
            q.RewardPoints = 0;
            q.RewardPerk = Random.Range(0, perks);
            return q;
        }
        return null;
    }

    private static ActiveQuest Harder(ActiveQuest q, float factor, System.Func<ActiveQuest, string> title)
    {
        if (q == null) return null;
        q.Target = Mathf.Max(q.Target + 1, Mathf.RoundToInt(q.Target * factor));
        q.Title = title(q);
        return q;
    }

    // ---- newer quests -----------------------------------------------------------------------------------

    private static ActiveQuest BuildHatTrick(QuestContext ctx, bool special)
    {
        int kills = special ? 4 : 3;
        int seconds = Mathf.Max(8, (special ? 14 : 20) - (int)ctx.Rank);
        return new ActiveQuest
        {
            Scope = QuestScope.Team, Category = QuestCategory.Combat, Objective = ObjectiveType.HatTrick, Rank = ctx.Rank,
            Target = kills, Param = seconds.ToString(),
            Title = $"Hat trick: kill {kills} monsters within {seconds} seconds",
        };
    }

    private static ActiveQuest BuildCrownJewel(QuestContext ctx, bool special) => new()
    {
        Scope = QuestScope.Team, Category = QuestCategory.Extraction, Objective = ObjectiveType.CrownJewel, Rank = ctx.Rank,
        Target = 1, Param = special ? "100" : "75",
        Title = special ? $"Crown jewel: extract the {ctx.CrownJewel} (${ctx.CrownJewelValue / 1000f:0.#}K) without a scratch"
                        : $"Crown jewel: extract the {ctx.CrownJewel} (${ctx.CrownJewelValue / 1000f:0.#}K) with 75% of its value",
    };

    private static ActiveQuest BuildFastOpener(QuestContext ctx, bool special)
    {
        int seconds = Mathf.Max(special ? 120 : 150, (special ? 210 : 300) - 15 * (int)ctx.Rank);
        return new ActiveQuest
        {
            Scope = QuestScope.Team, Category = QuestCategory.Extraction, Objective = ObjectiveType.FastOpener, Rank = ctx.Rank,
            Target = seconds, Param = seconds.ToString(),
            Title = $"Fast opener: close the first extraction within {seconds / 60}:{seconds % 60:00}",
        };
    }

    private static ActiveQuest BuildLightsOut(QuestContext ctx)
    {
        int seconds = 120 + 30 * Mathf.Max(0, ctx.Players - 1);
        return new ActiveQuest
        {
            Scope = QuestScope.Team, Category = QuestCategory.Environment, Objective = ObjectiveType.LightsOut, Rank = ctx.Rank,
            Target = seconds,
            Title = $"Lights out: explore with flashlights off for {seconds / 60}:{seconds % 60:00} in total",
        };
    }

    private static ActiveQuest BuildOops(QuestContext ctx)
    {
        int dollars = Mathf.Max(1000, Mathf.RoundToInt(ctx.HaulGoal * .08f / 500f) * 500);
        return new ActiveQuest
        {
            Scope = QuestScope.Team, Category = QuestCategory.Funny, Objective = ObjectiveType.Oops, Rank = ctx.Rank,
            Target = dollars, Title = $"Oops! Smash ${dollars / 1000f:0.#}K worth of valuables (we will not tell anyone)",
        };
    }

    private static ActiveQuest BuildRagdollOlympics(QuestContext ctx)
    {
        int tumbles = 8 + 4 * Mathf.Max(0, ctx.Players - 1);
        return new ActiveQuest
        {
            Scope = QuestScope.Team, Category = QuestCategory.Funny, Objective = ObjectiveType.RagdollOlympics, Rank = ctx.Rank,
            Target = tumbles, Title = $"Ragdoll Olympics: tumble {tumbles} times",
        };
    }

    private static ActiveQuest BuildTalkItOut(QuestContext ctx)
    {
        int seconds = ctx.Players <= 1 ? 30 : 45 + 15 * (ctx.Players - 1);
        return new ActiveQuest
        {
            Scope = QuestScope.Team, Category = QuestCategory.Funny, Objective = ObjectiveType.TalkItOut, Rank = ctx.Rank,
            Target = seconds,
            Title = ctx.Players <= 1 ? $"Talk it out: talk to yourself for {seconds} seconds (totally normal)"
                                     : $"Talk it out: chat for {seconds} seconds in total",
        };
    }

    private static ActiveQuest BuildBallerina(QuestContext ctx) => new()
    {
        Scope = QuestScope.Team, Category = QuestCategory.Funny, Objective = ObjectiveType.Ballerina, Rank = ctx.Rank,
        Target = 1, Title = "Ballerina: spin around 5 times within 6 seconds",
    };

    private static ActiveQuest BuildPayRespects(QuestContext ctx) => new()
    {
        Scope = QuestScope.Team, Category = QuestCategory.Funny, Objective = ObjectiveType.PayRespects, Rank = ctx.Rank,
        Target = 1, Title = "Pay respects: crouch for 3 seconds where a monster died",
    };

    private static ActiveQuest BuildMonsterHugger(QuestContext ctx) => new()
    {
        Scope = QuestScope.Team, Category = QuestCategory.Funny, Objective = ObjectiveType.MonsterHugger, Rank = ctx.Rank,
        Target = 8, Title = "Monster hugger: stand right next to a living monster for 8 seconds",
    };

    public static ActiveQuest BuildPersonal(QuestContext ctx)
    {
        // The one cleanly client-trackable personal quest we ship this phase: take no damage.
        var q = new ActiveQuest
        {
            Scope = QuestScope.Personal,
            Category = QuestCategory.Survival,
            Objective = ObjectiveType.NoDamage,
            Rank = ctx.Rank,
            Target = 1,               // 1 = "still clean"; flips to Failed on any damage
            Param = "",
            Title = "Take no damage this level",
        };
        q.RewardType = RewardType.Money;
        q.RewardAmount = MoneyReward(ctx);
        q.RewardPoints = RollSkillPoints(q);
        q.Id = 100;
        return q;
    }

    private static int ScaleKills(QuestContext ctx, float baseCount)
    {
        float v = baseCount * KillFactor(ctx.Players) * QuestRankInfo.Scale(ctx.Rank) * DayFactor(ctx.Day);
        return Mathf.Max(1, Mathf.RoundToInt(v));
    }

    private static ActiveQuest BuildKillAny(QuestContext ctx)
    {
        int target = ScaleKills(ctx, 5f);
        return new ActiveQuest
        {
            Scope = QuestScope.Team,
            Category = QuestCategory.Combat,
            Objective = ObjectiveType.KillAny,
            Rank = ctx.Rank,
            Target = target,
            Title = $"Kill {target} enemies",
        };
    }

    private static ActiveQuest BuildMeleeKill(QuestContext ctx)
    {
        int target = ScaleKills(ctx, 3f);
        return new ActiveQuest
        {
            Scope = QuestScope.Team,
            Category = QuestCategory.Combat,
            Objective = ObjectiveType.MeleeKill,
            Rank = ctx.Rank,
            Target = target,
            Title = $"Kill {target} enemies with melee",
        };
    }

    private static ActiveQuest BuildNoGunKills(QuestContext ctx)
    {
        return new ActiveQuest
        {
            Scope = QuestScope.Team,
            Category = QuestCategory.Combat,
            Objective = ObjectiveType.NoGunKills,
            Rank = ctx.Rank,
            Target = 1,   // survival-style: succeeds at level end if never failed
            Title = "Complete the level with no gun kills",
        };
    }

    private static ActiveQuest BuildKillType(QuestContext ctx)
    {
        string name = ctx.EnemyNames[Random.Range(0, ctx.EnemyNames.Count)];
        int target = ScaleKills(ctx, 3f);
        return new ActiveQuest
        {
            Scope = QuestScope.Team,
            Category = QuestCategory.Combat,
            Objective = ObjectiveType.KillType,
            Rank = ctx.Rank,
            Target = target,
            Param = name,
            Title = $"Kill {target}x {name}",
        };
    }

    private static readonly string[] TierNames = { "weak", "medium", "strong" };

    private static ActiveQuest BuildKillTier(QuestContext ctx)
    {
        int tier = ctx.EnemyTiers[Random.Range(0, ctx.EnemyTiers.Count)];
        int target = ScaleKills(ctx, 3f);
        string tierName = tier >= 0 && tier < TierNames.Length ? TierNames[tier] : $"tier {tier + 1}";
        return new ActiveQuest
        {
            Scope = QuestScope.Team,
            Category = QuestCategory.Combat,
            Objective = ObjectiveType.KillTier,
            Rank = ctx.Rank,
            Target = target,
            Param = tier.ToString(),
            Title = $"Kill {target} {tierName} enemies",
        };
    }

    private static ActiveQuest BuildPitKill(QuestContext ctx)
    {
        int target = Mathf.Max(1, ScaleKills(ctx, 1.5f));
        return new ActiveQuest
        {
            Scope = QuestScope.Team,
            Category = QuestCategory.Environment,
            Objective = ObjectiveType.PitKill,
            Rank = ctx.Rank,
            Target = target,
            Title = $"Shove {target} enemies into a pit",
        };
    }

    private static ActiveQuest BuildReachHaul(QuestContext ctx)
    {
        // Bank a bit more than the vanilla quota; grows with rank.
        float over = 1f + 0.15f * (int)ctx.Rank;
        int target = Mathf.Max(1, Mathf.RoundToInt(ctx.HaulGoal * over));
        int display = Mathf.RoundToInt(target / 1000f);
        return new ActiveQuest
        {
            Scope = QuestScope.Team,
            Category = QuestCategory.Extraction,
            Objective = ObjectiveType.ReachHaul,
            Rank = ctx.Rank,
            Target = target,
            Title = $"Extract ${display}K of value",
        };
    }

    private static ActiveQuest BuildIntactExtract(QuestContext ctx)
    {
        // Ask for a modest count that can't exceed the valuables actually on the level (some will
        // inevitably break, so we never demand the whole spawn). Grows with team size, rank and day.
        int scaled = ScaleKills(ctx, 2f);
        int target = Mathf.Clamp(scaled, 1, Mathf.Max(1, ctx.ValuableCount));
        return new ActiveQuest
        {
            Scope = QuestScope.Team,
            Category = QuestCategory.Extraction,
            Objective = ObjectiveType.IntactExtract,
            Rank = ctx.Rank,
            Target = target,
            Title = $"Extract {target} valuables undamaged",
        };
    }

    private static ActiveQuest BuildGunKill(QuestContext ctx)
    {
        int target = ScaleKills(ctx, 3f);
        return new ActiveQuest
        {
            Scope = QuestScope.Team,
            Category = QuestCategory.Combat,
            Objective = ObjectiveType.GunKill,
            Rank = ctx.Rank,
            Target = target,
            Title = $"Kill {target} enemies with guns",
        };
    }

    private static ActiveQuest BuildExtractCount(QuestContext ctx)
    {
        // Damaged loot counts too, so it can ask for more than the intact quest; still capped by what exists.
        int target = Mathf.Clamp(ScaleKills(ctx, 3f), 1, Mathf.Max(1, ctx.ValuableCount));
        return new ActiveQuest
        {
            Scope = QuestScope.Team,
            Category = QuestCategory.Extraction,
            Objective = ObjectiveType.ExtractCount,
            Rank = ctx.Rank,
            Target = target,
            Title = $"Extract {target} valuables",
        };
    }

    private static ActiveQuest BuildHighValueExtract(QuestContext ctx)
    {
        // "Valuable" is relative to the quota: about a twelfth of it, rounded to $500, never below $1,000.
        int minimum = Mathf.Max(1000, Mathf.RoundToInt(ctx.HaulGoal / 12f / 500f) * 500);
        int target = Mathf.Clamp(ScaleKills(ctx, 1f), 1, Mathf.Max(1, ctx.ValuableCount));
        return new ActiveQuest
        {
            Scope = QuestScope.Team,
            Category = QuestCategory.Extraction,
            Objective = ObjectiveType.HighValueExtract,
            Rank = ctx.Rank,
            Target = target,
            Param = minimum.ToString(),
            Title = $"Extract {target} valuables worth ${minimum / 1000f:0.#}K or more each",
        };
    }

    private static ActiveQuest BuildHandKill(QuestContext ctx)
    {
        int target = Mathf.Max(1, ScaleKills(ctx, 1.5f));
        return new ActiveQuest
        {
            Scope = QuestScope.Team,
            Category = QuestCategory.Combat,
            Objective = ObjectiveType.HandKill,
            Rank = ctx.Rank,
            Target = target,
            Title = $"Kill {target} enemies bare-handed (grab, throw, push - no weapons)",
        };
    }

    private static ActiveQuest BuildStun(QuestContext ctx)
    {
        int target = ScaleKills(ctx, 4f);
        return new ActiveQuest
        {
            Scope = QuestScope.Team,
            Category = QuestCategory.Combat,
            Objective = ObjectiveType.StunEnemies,
            Rank = ctx.Rank,
            Target = target,
            Title = $"Stun {target} enemies",
        };
    }

    private static ActiveQuest BuildRevive(QuestContext ctx)
    {
        int target = Mathf.Clamp(Mathf.RoundToInt(QuestRankInfo.Scale(ctx.Rank) / 2f), 1, Mathf.Max(1, ctx.Players));
        return new ActiveQuest
        {
            Scope = QuestScope.Team,
            Category = QuestCategory.Survival,
            Objective = ObjectiveType.Revive,
            Rank = ctx.Rank,
            Target = target,
            Title = target == 1 ? "Revive a fallen teammate" : $"Revive {target} fallen teammates",
        };
    }

    private static ActiveQuest BuildMoonRitual(QuestContext ctx)
    {
        // One approved offering per extraction point; higher ranks ask for more, never more than exist.
        int target = Mathf.Clamp(1 + (int)ctx.Rank / 3, 1, ctx.Towers);
        return new ActiveQuest
        {
            Scope = QuestScope.Team,
            Category = QuestCategory.Extraction,
            Objective = ObjectiveType.MoonRitual,
            Rank = ctx.Rank,
            Target = target,
            Param = "ritual",
            Title = target == 1 ? "A moon is judging: win its approval at an extraction point"
                                : $"A moon is judging: win its approval at {target} extraction points",
        };
    }

    private static ActiveQuest BuildSoulExtract(QuestContext ctx)
    {
        int target = Mathf.Max(1, ScaleKills(ctx, 1.5f));
        return new ActiveQuest
        {
            Scope = QuestScope.Team,
            Category = QuestCategory.Extraction,
            Objective = ObjectiveType.SoulExtract,
            Rank = ctx.Rank,
            Target = target,
            Title = $"Extract {target} enemy souls",
        };
    }

    private static ActiveQuest BuildExtractionRush(QuestContext ctx)
    {
        // Generous at low ranks (10 minutes), tighter by 30 s per rank, never under 5 minutes.
        int seconds = Mathf.Max(300, 600 - 30 * (int)ctx.Rank);
        return new ActiveQuest
        {
            Scope = QuestScope.Team,
            Category = QuestCategory.Extraction,
            Objective = ObjectiveType.ExtractionRush,
            Rank = ctx.Rank,
            Target = seconds,
            Param = seconds.ToString(),
            Title = $"Close every extraction within {seconds / 60}:{seconds % 60:00}",
        };
    }

    private static ActiveQuest BuildNoDeaths(QuestContext ctx)
    {
        // Target is the death threshold; higher rank allows fewer deaths (F..D allow 1, above = 0).
        int allowed = ctx.Rank <= QuestRank.D ? 1 : 0;
        string text = allowed == 0 ? "with no deaths" : $"with at most {allowed} death";
        return new ActiveQuest
        {
            Scope = QuestScope.Team,
            Category = QuestCategory.Survival,
            Objective = ObjectiveType.NoDeaths,
            Rank = ctx.Rank,
            Target = allowed,
            Title = $"Complete the level {text}",
        };
    }
}
