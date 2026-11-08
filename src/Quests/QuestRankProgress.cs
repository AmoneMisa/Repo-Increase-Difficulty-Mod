using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace IncreaseHardMod.Quests;

/// <summary>
/// Per-player quest-rank persistence. Each player advances their OWN contract rank by
/// completing quests: every <see cref="ModConfig.QuestCompletionsPerRank"/> completions
/// bumps them one rank (F -> E -> ... -> SSS). We only need to persist a single running
/// count of completions per Steam ID; the rank is derived from it, so a player's
/// rank naturally reflects how many quests THEY were present for.
///
/// Ranks belong to one run: a new save starts at rank F. The run is the host's save file name plus a run
/// generation: when the game deletes a save (a game over), the next run reuses the same name, so the host bumps
/// that name's generation and every count of the lost run stops applying. Guests learn the "name#generation" from
/// a room property the host publishes, since the game clears their own save name.
///
/// Stored as a tiny hand-rolled text file next to the BepInEx config
/// (quest_ranks.txt, one "save|steamID=completions" line each, plus "gen|save=N" for bumped generations) rather
/// than a ConfigEntry, since this is churny runtime state, not a user-tunable knob.
/// </summary>
public static class QuestRankProgress
{
    private static readonly Dictionary<string, int> Completions = new();
    private static bool _loaded;
    private const string SaveProperty = "ihm_save";
    private const string GenPrefix = "gen|";
    private static string _published;

    /// <summary>The run the current quests belong to: the host's save file name, with its run generation.</summary>
    internal static string CurrentSave()
    {
        try
        {
            if (Photon.Pun.PhotonNetwork.InRoom && !Photon.Pun.PhotonNetwork.IsMasterClient)
                return Photon.Pun.PhotonNetwork.CurrentRoom?.CustomProperties[SaveProperty] is string hostSave && hostSave.Length > 0
                    ? hostSave : "unsaved";
            string own = StatsManager.instance != null ? StatsManager.instance.saveFileCurrent : null;
            return string.IsNullOrEmpty(own) ? "unsaved" : WithGeneration(own);
        }
        catch { return "unsaved"; }
    }

    // A save's first run keeps the plain name (so existing counts stay where they are); later runs add "#2", "#3"...
    private static string WithGeneration(string save)
    {
        EnsureLoaded();
        return Completions.TryGetValue(GenPrefix + save, out int gen) && gen > 1 ? save + "#" + gen.ToString(CultureInfo.InvariantCulture) : save;
    }

    /// <summary>Host: publish the run key so guests rank against the same run. Cheap; call every frame.</summary>
    internal static void PublishSave()
    {
        if (!Photon.Pun.PhotonNetwork.InRoom || !Photon.Pun.PhotonNetwork.IsMasterClient || StatsManager.instance == null) return;
        if (string.IsNullOrEmpty(StatsManager.instance.saveFileCurrent)) return;
        string save = CurrentSave();
        if (save == _published) return;
        var room = Photon.Pun.PhotonNetwork.CurrentRoom;
        if (room == null) return;
        if (room.CustomProperties[SaveProperty] is string current && current == save) { _published = save; return; }
        room.SetCustomProperties(new ExitGames.Client.Photon.Hashtable { [SaveProperty] = save });
        _published = save;
    }

    /// <summary>
    /// Host: the game deleted a save (game over, or leaving before the first level was done). The next run will reuse
    /// the same name, so it gets a new generation and starts every player at rank F; the lost run's counts are dropped.
    /// </summary>
    [HarmonyLib.HarmonyPatch(typeof(SemiFunc), nameof(SemiFunc.SaveFileDelete))]
    private static class OnSaveDeleted
    {
        [HarmonyLib.HarmonyPostfix]
        private static void Postfix(string saveFileName)
        {
            if (string.IsNullOrEmpty(saveFileName) || !SemiFunc.IsMasterClientOrSingleplayer()) return;
            EnsureLoaded();
            string run = WithGeneration(saveFileName);
            var gone = new List<string>();
            foreach (var key in Completions.Keys)
                if (key.StartsWith(run + "|", System.StringComparison.Ordinal)) gone.Add(key);
            foreach (var key in gone) Completions.Remove(key);
            Completions.TryGetValue(GenPrefix + saveFileName, out int gen);
            Completions[GenPrefix + saveFileName] = System.Math.Max(1, gen) + 1;
            Save();
            _published = null;
            Plugin.Log.LogInfo($"[Quests] Save {saveFileName} was deleted: quest ranks start over for its next run ({gone.Count} count(s) dropped).");
        }
    }

    private static string Key(string steamId) => CurrentSave() + "|" + steamId;

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
                // Lines from before per-save ranks ("steamID=completions") are a lifetime total across every save:
                // they never belonged to one run, so they are dropped rather than handed to a save.
                if (id.IndexOf('|') < 0) continue;
                if (int.TryParse(val, NumberStyles.Integer, CultureInfo.InvariantCulture, out int c))
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
        return Completions.TryGetValue(Key(steamId), out int c) ? c : 0;
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

    /// <summary>Derived contract rank for a player from their completions in this run.</summary>
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
        Completions[Key(steamId)] = after;
        Save();

        Resolve(after, out var afterRank, out _);
        return afterRank > beforeRank;
    }
}
