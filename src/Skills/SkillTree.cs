using System;
using System.Collections.Generic;

namespace IncreaseHardMod.Skills;

// Engine-free skill tree model: the node catalogue, point maths, buy rules and validation.
// No UnityEngine here, so tests/SkillTreeTests can run it directly. Storage, networking and
// effects live elsewhere and only ask this model what is allowed. See docs/skill-tree-plan.md.

internal enum SkillPage : byte { Recon = 0, Combat = 1, Survival = 2, Utility = 3, Arsenal = 4, Contracts = 5, Courier = 6 }

/// <summary>Vanilla upgrades a node drives through the game's own PunManager upgrade call.</summary>
internal enum VanillaUpgrade : byte { None = 0, DeathHeadBattery = 1, TumbleWings = 2 }

internal sealed class SkillNode
{
    /// <summary>Stable id; also the storage key of the node's bought levels (a hidden REPOLib upgrade).</summary>
    public string Id;
    public SkillPage Page;
    /// <summary>Panel on the page, e.g. "Map", "Ping", "Flashlight".</summary>
    public string Branch;
    public string Name, NameRu;
    /// <summary>Points per level.</summary>
    public int Cost;
    public int MaxLevel;
    /// <summary>Every listed node needs at least one level (bought or free) before this one can be bought.</summary>
    public string[] Requires = Array.Empty<string>();
    public VanillaUpgrade Vanilla;
    /// <summary>Roots of a branch: the first node on its panel.</summary>
    public bool Root;
    /// <summary>Never bought with points: levels come only from special quest contracts (and a refund keeps them).</summary>
    public bool QuestOnly;
    /// <summary>0 for a normal perk; 1-3 for a trade-off skill (see <see cref="TradeOffRules"/>): one pick per tier
    /// per page, opened by points spent in that page's normal perks, tiers II and III needing the tier below.</summary>
    public int Tier;
    /// <summary>The prerequisite must be at its maximum level, not just learned (Flash Stun needs maxed Brightness).</summary>
    public bool RequiresMaxed;

    public int TotalCost => Cost * MaxLevel;
}

/// <summary>Why a node can't be bought.</summary>
internal enum BuyBlock : byte
{
    None = 0, UnknownNode, Maxed, MissingPrerequisite, NotEnoughPoints, BadCount,
    // Decided by the host's game state rather than the loadout:
    WrongPlace, NotReady, NoMoney, NothingSpent,
    QuestOnly,
    // Trade-off skills:
    GateNotMet, TierTaken, NeedLowerTier,
}

/// <summary>
/// One player's tree. <see cref="Bought"/> are levels paid for with points. <see cref="Free"/> are levels the player
/// already had without spending points (vanilla Tumble Wings / Battery Head bought in the old shop); they count for
/// caps and prerequisites but cost nothing, so <see cref="Spent"/> never charges for them.
/// </summary>
internal sealed class SkillLoadout
{
    public int Earned;
    public readonly Dictionary<string, int> Bought = new(StringComparer.Ordinal);
    public readonly Dictionary<string, int> Free = new(StringComparer.Ordinal);

    public int BoughtLevel(string id) => id != null && Bought.TryGetValue(id, out var l) ? Math.Max(0, l) : 0;
    public int FreeLevel(string id) => id != null && Free.TryGetValue(id, out var l) ? Math.Max(0, l) : 0;

    /// <summary>The level the effect uses: bought plus free, never above the node's cap.</summary>
    public int Level(string id)
    {
        var node = SkillCatalog.Find(id);
        int total = BoughtLevel(id) + FreeLevel(id);
        return node == null ? total : Math.Min(total, node.MaxLevel);
    }

    public int Spent
    {
        get
        {
            int sum = 0;
            foreach (var pair in Bought)
            {
                var node = SkillCatalog.Find(pair.Key);
                if (node != null && pair.Value > 0) sum += pair.Value * node.Cost;
            }
            return sum;
        }
    }

    public int Available => Earned - Spent;

    public SkillLoadout Clone()
    {
        var copy = new SkillLoadout { Earned = Earned };
        foreach (var pair in Bought) copy.Bought[pair.Key] = pair.Value;
        foreach (var pair in Free) copy.Free[pair.Key] = pair.Value;
        return copy;
    }
}

internal static class SkillRules
{
    internal static BuyBlock CanBuy(SkillLoadout loadout, string id, int count = 1)
    {
        if (count < 1) return BuyBlock.BadCount;
        var node = SkillCatalog.Find(id);
        if (node == null) return BuyBlock.UnknownNode;
        if (loadout.BoughtLevel(id) + loadout.FreeLevel(id) + count > node.MaxLevel) return BuyBlock.Maxed;
        if (node.QuestOnly) return BuyBlock.QuestOnly;
        foreach (var need in node.Requires)
            if (loadout.Level(need) < (node.RequiresMaxed ? SkillCatalog.Find(need)?.MaxLevel ?? 1 : 1)) return BuyBlock.MissingPrerequisite;
        if (node.Tier > 0)
        {
            var tier = TradeOffBlock(loadout, node);
            if (tier != BuyBlock.None) return tier;
        }
        if (loadout.Available < node.Cost * count) return BuyBlock.NotEnoughPoints;
        return BuyBlock.None;
    }

    /// <summary>Points spent in a page's normal perks (trade-offs and quest-only perks don't count).</summary>
    internal static int PageSpent(SkillLoadout loadout, SkillPage page)
    {
        int sum = 0;
        foreach (var pair in loadout.Bought)
        {
            var node = SkillCatalog.Find(pair.Key);
            if (node != null && node.Page == page && node.Tier == 0 && !node.QuestOnly && pair.Value > 0) sum += pair.Value * node.Cost;
        }
        return sum;
    }

    /// <summary>The trade-off this loadout holds on a page's tier, or null.</summary>
    internal static string TierPick(SkillLoadout loadout, SkillPage page, int tier)
    {
        foreach (var pair in loadout.Bought)
        {
            var node = SkillCatalog.Find(pair.Key);
            if (node != null && node.Page == page && node.Tier == tier && pair.Value > 0) return node.Id;
        }
        return null;
    }

    // Why a trade-off can't be taken: the page gate, another pick on its tier, or no pick on the tier below.
    private static BuyBlock TradeOffBlock(SkillLoadout loadout, SkillNode node)
    {
        string held = TierPick(loadout, node.Page, node.Tier);
        if (held != null && held != node.Id) return BuyBlock.TierTaken;
        if (PageSpent(loadout, node.Page) < TradeOffRules.Gate(node.Page, node.Tier)) return BuyBlock.GateNotMet;
        if (node.Tier > 1 && TierPick(loadout, node.Page, node.Tier - 1) == null) return BuyBlock.NeedLowerTier;
        return BuyBlock.None;
    }

    /// <summary>Points spent on trade-off skills.</summary>
    internal static int TradeOffSpent(SkillLoadout loadout)
    {
        int sum = 0;
        foreach (var pair in loadout.Bought)
        {
            var node = SkillCatalog.Find(pair.Key);
            if (node != null && node.Tier > 0 && pair.Value > 0) sum += pair.Value * node.Cost;
        }
        return sum;
    }

    /// <summary>Refunds only the trade-off skills; normal perks stay. Returns the points refunded.</summary>
    internal static int RespecTradeOffs(SkillLoadout loadout)
    {
        int refunded = TradeOffSpent(loadout);
        foreach (var id in new List<string>(loadout.Bought.Keys))
            if (SkillCatalog.Find(id)?.Tier > 0) loadout.Bought.Remove(id);
        return refunded;
    }

    /// <summary>Host side: apply a buy if it is allowed. Returns the reason when it is not.</summary>
    internal static BuyBlock Buy(SkillLoadout loadout, string id, int count = 1)
    {
        var block = CanBuy(loadout, id, count);
        if (block == BuyBlock.None) loadout.Bought[id] = loadout.BoughtLevel(id) + count;
        return block;
    }

    /// <summary>Refunds every bought level. Free levels and quest-only perks stay; earned points are unchanged.</summary>
    internal static int Respec(SkillLoadout loadout)
    {
        int refunded = loadout.Spent;
        foreach (var id in new List<string>(loadout.Bought.Keys))
            if (SkillCatalog.Find(id)?.QuestOnly != true) loadout.Bought.Remove(id);
        return refunded;
    }

    /// <summary>Host: one quest-contract level of a quest-only perk. False when it is already maxed.</summary>
    internal static bool GrantQuestLevel(SkillLoadout loadout, string id)
    {
        var node = SkillCatalog.Find(id);
        if (node == null || !node.QuestOnly || loadout.Level(id) >= node.MaxLevel) return false;
        loadout.Bought[id] = loadout.BoughtLevel(id) + 1;
        return true;
    }

    /// <summary>
    /// Brings a loaded or received loadout back inside the rules: unknown ids and negative levels are dropped,
    /// levels are capped, nodes whose prerequisites are gone are removed (repeated until stable), and while more
    /// points are spent than earned the most expensive leaf levels are refunded. Returns a line per fix.
    /// </summary>
    internal static List<string> Sanitize(SkillLoadout loadout)
    {
        var fixes = new List<string>();
        if (loadout.Earned < 0) { fixes.Add($"earned {loadout.Earned} -> 0"); loadout.Earned = 0; }
        foreach (var id in new List<string>(loadout.Bought.Keys))
        {
            var node = SkillCatalog.Find(id);
            int level = loadout.Bought[id];
            if (node == null || level <= 0) { loadout.Bought.Remove(id); if (level != 0 || node == null) fixes.Add($"{id}: removed"); continue; }
            int cap = Math.Max(0, node.MaxLevel - loadout.FreeLevel(id));
            if (level > cap) { loadout.Bought[id] = cap; fixes.Add($"{id}: {level} -> {cap} (cap)"); }
            if (loadout.Bought[id] == 0) loadout.Bought.Remove(id);
        }
        bool changed = true;
        while (changed)
        {
            changed = false;
            foreach (var id in new List<string>(loadout.Bought.Keys))
            {
                var node = SkillCatalog.Find(id);
                foreach (var need in node.Requires)
                    if (loadout.Level(need) < 1) { loadout.Bought.Remove(id); fixes.Add($"{id}: removed (needs {need})"); changed = true; break; }
            }
        }
        // Trade-offs: drop any whose gate is no longer met, a second pick on one tier, or a tier above a missing one.
        for (int tier = 1; tier <= TradeOffRules.Tiers; tier++)
            foreach (var id in new List<string>(loadout.Bought.Keys))
            {
                var node = SkillCatalog.Find(id);
                if (node == null || node.Tier != tier) continue;
                loadout.Bought.Remove(id); // judged as if it were not held, so its own pick does not block it
                var block = TradeOffBlock(loadout, node);
                if (block == BuyBlock.None) loadout.Bought[id] = 1;
                else fixes.Add($"{id}: removed ({block})");
            }
        while (loadout.Available < 0)
        {
            string worst = null;
            foreach (var id in loadout.Bought.Keys)
            {
                if (IsRequiredByOwned(loadout, id)) continue;
                if (worst == null || SkillCatalog.Find(id).Cost > SkillCatalog.Find(worst).Cost) worst = id;
            }
            if (worst == null) { loadout.Bought.Clear(); fixes.Add("overspent: reset"); break; }
            int left = loadout.Bought[worst] - 1;
            if (left > 0) loadout.Bought[worst] = left; else loadout.Bought.Remove(worst);
            fixes.Add($"{worst}: -1 (overspent)");
        }
        return fixes;
    }

    // A node another owned node depends on can't be refunded without breaking that node, unless it has spare levels.
    private static bool IsRequiredByOwned(SkillLoadout loadout, string id)
    {
        if (loadout.Level(id) > 1) return false;
        foreach (var node in SkillCatalog.All)
            if (loadout.Level(node.Id) > 0 && Array.IndexOf(node.Requires, id) >= 0) return true;
        return false;
    }
}
