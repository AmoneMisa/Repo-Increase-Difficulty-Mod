using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace IncreaseHardMod.Quests;

/// <summary>
/// Per-player quest-rank persistence. Each player advances their OWN contract rank by
/// completing quests: every <see cref="ModConfig.QuestCompletionsPerRank"/> completions
/// bumps them one rank (F -> E -> ... -> SSS). We only need to persist a single running
/// count of lifetime completions per Steam ID; the rank is derived from it, so a player's
/// rank naturally reflects how many quests THEY were present for.
///
/// Stored as a tiny hand-rolled text file next to the BepInEx config
/// (quest_ranks.txt, one "steamID=completions" line each) rather than a ConfigEntry, since
/// this is churny runtime state, not a user-tunable knob.
/// </summary>
public static class QuestRankProgress
{
    private static readonly Dictionary<string, int> Completions = new();
    private static bool _loaded;

    private static string FilePath =>
        Path.Combine(BepInEx.Paths.ConfigPath, "com.kubai.increasehardmod.quest_ranks.txt");

    private static void EnsureLoaded()
    {
        if (_loaded)
            return;
        _loaded = true;

        try
        {
            if (!File.Exists(FilePath))
                return;
            foreach (var line in File.ReadAllLines(FilePath))
            {
                int eq = line.LastIndexOf('=');
                if (eq <= 0)
                    continue;
                string id = line.Substring(0, eq).Trim();
                string val = line.Substring(eq + 1).Trim();
                if (id.Length > 0 && int.TryParse(val, NumberStyles.Integer, CultureInfo.InvariantCulture, out int c))
                    Completions[id] = c;
            }
        }
        catch (System.Exception e)
        {
            Plugin.Log.LogWarning($"[Quests] Could not read quest ranks: {e.Message}");
        }
    }

    private static void Save()
    {
        try
        {
            var lines = new List<string>(Completions.Count);
            foreach (var kv in Completions)
                lines.Add($"{kv.Key}={kv.Value.ToString(CultureInfo.InvariantCulture)}");
            File.WriteAllLines(FilePath, lines);
        }
        catch (System.Exception e)
        {
            Plugin.Log.LogWarning($"[Quests] Could not save quest ranks: {e.Message}");
        }
    }

    public static int GetCompletions(string steamId)
    {
        if (string.IsNullOrEmpty(steamId))
            return 0;
        EnsureLoaded();
        return Completions.TryGetValue(steamId, out int c) ? c : 0;
    }

    /// <summary>
    /// How many completions it takes to advance FROM a given rank to the next. Lower ranks share
    /// the base per-rank value; the top three transitions (A->S, S->SS, SS->SSS) have their own,
    /// larger requirements.
    /// </summary>
    private static int StepFrom(QuestRank rank)
    {
        switch (rank)
        {
            case QuestRank.A:  return System.Math.Max(1, ModConfig.QuestCompletionsAtoS.Value);
            case QuestRank.S:  return System.Math.Max(1, ModConfig.QuestCompletionsStoSS.Value);
            case QuestRank.SS: return System.Math.Max(1, ModConfig.QuestCompletionsSStoSSS.Value);
            default:           return System.Math.Max(1, ModConfig.QuestCompletionsPerRank.Value);
        }
    }

    // Walk the rank ladder, spending completions rank by rank; the leftover is progress into the
    // rank we land on.
    private static void Resolve(int completions, out QuestRank rank, out int intoRank)
    {
        int r = 0;
        int c = System.Math.Max(0, completions);
        while (r < (int)QuestRank.SSS && c >= StepFrom((QuestRank)r))
        {
            c -= StepFrom((QuestRank)r);
            r++;
        }
        rank = (QuestRank)r;
        intoRank = c;
    }

    /// <summary>Derived contract rank for a player from their lifetime completion count.</summary>
    public static QuestRank GetRank(string steamId)
    {
        Resolve(GetCompletions(steamId), out var rank, out _);
        return rank;
    }

    /// <summary>Completions accumulated toward the NEXT rank for the HUD (the "x" in x/y).</summary>
    public static int ProgressIntoRank(string steamId)
    {
        Resolve(GetCompletions(steamId), out _, out int into);
        return into;
    }

    /// <summary>Completions needed to reach the next rank for the HUD (the "y" in x/y).</summary>
    public static int RequiredForNextRank(string steamId)
    {
        return StepFrom(GetRank(steamId));
    }

    /// <summary>Credit one completion to a player, persisting immediately. Returns true on rank-up.</summary>
    public static bool AddCompletion(string steamId)
    {
        if (string.IsNullOrEmpty(steamId))
            return false;
        EnsureLoaded();

        int before = GetCompletions(steamId);
        Resolve(before, out var beforeRank, out _);
        int after = before + 1;
        Completions[steamId] = after;
        Save();

        Resolve(after, out var afterRank, out _);
        return afterRank > beforeRank;
    }
}
