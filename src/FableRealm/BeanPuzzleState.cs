using System;
using System.Collections.Generic;

namespace IncreaseHardMod.FableRealm;

/// <summary>Names and numbers shared by the room recipe (tools/fable_realm) and the puzzle code.</summary>
internal static class BeanPuzzleRules
{
    internal const int Count = 5, AllMask = (1 << Count) - 1;
    internal const int EggValue = 250000;
    /// <summary>The giant bean hangs this far above its landing spot (see beanstalk_grove in rooms_extra.py).</summary>
    internal const float LowerDistance = 5.2f;
    internal const float LowerSeconds = 4.5f;
    internal const string Special = "bean-puzzle";
    internal const string SocketPrefix = "bean-socket-", GiantPrefix = "giant-bean-";
    internal const string Landing = "giant-bean-landing", EggSpawn = "golden-egg-spawn";
    internal const string BeanKeyPrefix = "fable_bean_", EggKey = "fable_golden_egg";
    /// <summary>Plate order of the recipe; the index is the colour's bit in every mask.</summary>
    internal static readonly string[] Colours = { "red", "yellow", "blue", "green", "violet" };

    internal static string BeanKey(int colour) => BeanKeyPrefix + Colours[colour];

    internal static int ColourOf(string beanKey)
    {
        if (beanKey == null || !beanKey.StartsWith(BeanKeyPrefix, StringComparison.Ordinal)) return -1;
        return Array.IndexOf(Colours, beanKey.Substring(BeanKeyPrefix.Length));
    }

    /// <summary>How far the giant bean has come down, metres, <paramref name="elapsed"/> seconds after the puzzle was solved.</summary>
    internal static float Drop(double elapsed)
    {
        if (!(elapsed > 0d)) return 0f;
        if (elapsed >= LowerSeconds) return LowerDistance;
        float t = (float)(elapsed / LowerSeconds);
        return LowerDistance * t * t * (3f - 2f * t); // eases out of the tendrils and onto the dais
    }
}

internal enum BeanReward : byte
{
    /// <summary>Not decided yet: the puzzle is unsolved or the bean is still coming down.</summary>
    Pending = 0,
    /// <summary>The Golden Egg was spawned for this level.</summary>
    Given = 1,
    /// <summary>This level already paid its egg (the save was reloaded after it dropped): the bean comes down empty.</summary>
    AlreadyClaimed = 2,
}

/// <summary>What the host tells everyone, and what a late joiner needs to catch up.</summary>
internal readonly struct BeanSnapshot
{
    internal readonly int Matched;
    internal readonly bool Solved;
    internal readonly BeanReward Reward;
    /// <summary>Room clock (PhotonNetwork.Time) at the moment of solving, so every client lowers the bean in step.</summary>
    internal readonly double SolvedAt;
    internal BeanSnapshot(int matched, bool solved, BeanReward reward, double solvedAt)
    {
        Matched = matched; Solved = solved; Reward = reward; SolvedAt = solvedAt;
    }
}

/// <summary>
/// The puzzle itself, free of engine types so it can be tested: five plates, five coloured beans, one reward.
/// The host feeds it what rests on each plate; clients only ever <see cref="Apply"/> the host's snapshots.
/// </summary>
internal sealed class BeanPuzzleState
{
    /// <summary>Bit i is set while the bean of colour i rests on plate i.</summary>
    internal int Matched { get; private set; }
    internal bool Solved { get; private set; }
    internal BeanReward Reward { get; private set; }
    internal double SolvedAt { get; private set; }

    internal BeanSnapshot Snapshot => new(Matched, Solved, Reward, SolvedAt);

    /// <summary>
    /// Host: <paramref name="resting"/>[plate] is the mask of bean colours resting on that plate right now.
    /// A bean on a plate of another colour counts for nothing and spoils nothing. Before the puzzle is solved a
    /// plate goes dark again when its bean is taken away; once all five match, the result is final.
    /// Returns true when the state changed and must be sent to the other players.
    /// </summary>
    internal bool Observe(IReadOnlyList<int> resting, double now)
    {
        if (Solved || resting == null) return false;
        int matched = 0;
        for (int plate = 0; plate < BeanPuzzleRules.Count && plate < resting.Count; plate++)
            if ((resting[plate] & 1 << plate) != 0) matched |= 1 << plate;
        if (matched == Matched) return false;
        Matched = matched;
        if (matched == BeanPuzzleRules.AllMask) { Solved = true; SolvedAt = now; }
        return true;
    }

    /// <summary>Host: true once the bean has finished coming down and the reward has not been decided yet.</summary>
    internal bool RewardDue(double now) =>
        Solved && Reward == BeanReward.Pending && now - SolvedAt >= BeanPuzzleRules.LowerSeconds;

    /// <summary>
    /// Host: decides the reward exactly once. Returns true when the caller must spawn the egg now;
    /// <paramref name="firstTimeForThisLevel"/> is false when the run's ledger says this level already paid.
    /// </summary>
    internal bool SettleReward(bool firstTimeForThisLevel)
    {
        if (!Solved || Reward != BeanReward.Pending) return false;
        Reward = firstTimeForThisLevel ? BeanReward.Given : BeanReward.AlreadyClaimed;
        return firstTimeForThisLevel;
    }

    /// <summary>
    /// Client (or a guest promoted to host): take over the host's state. Never goes backwards: a solved puzzle
    /// stays solved and a decided reward stays decided, so a late or repeated message cannot undo or repeat anything.
    /// Returns true when something changed.
    /// </summary>
    internal bool Apply(in BeanSnapshot snapshot)
    {
        if (Solved)
        {
            if (!snapshot.Solved || Reward != BeanReward.Pending || snapshot.Reward == BeanReward.Pending) return false;
            Reward = snapshot.Reward;
            return true;
        }
        bool changed = Matched != snapshot.Matched || snapshot.Solved;
        Matched = snapshot.Matched;
        if (snapshot.Solved)
        {
            Solved = true; SolvedAt = snapshot.SolvedAt; Reward = snapshot.Reward;
        }
        return changed;
    }
}

/// <summary>Shape and range checks for the puzzle's network message, before anything reads it.</summary>
internal static class BeanPuzzleWire
{
    /// <summary>Photon event code of the host's puzzle state (see the list in ModNetwork).</summary>
    internal const byte EventCode = 150;
    private const int Fields = 7;

    internal static object[] Write(string levelKey, in BeanSnapshot state, int[] beanViewIds, float[] beanFloors) => new object[]
    {
        levelKey ?? "", (byte)state.Matched, state.Solved, (byte)state.Reward, state.SolvedAt,
        beanViewIds ?? new int[BeanPuzzleRules.Count], beanFloors ?? new float[BeanPuzzleRules.Count * 3],
    };

    /// <summary>
    /// Accepts a message only for the level this client is in (<paramref name="localLevelKey"/>) and only when it
    /// is self-consistent: no unknown plate bits, solved means all five matched, a reward means solved.
    /// </summary>
    internal static bool TryRead(object raw, string localLevelKey, out BeanSnapshot state, out int[] beanViewIds, out float[] beanFloors)
    {
        state = default; beanViewIds = null; beanFloors = null;
        if (raw is not object[] d || d.Length != Fields || string.IsNullOrEmpty(localLevelKey) ||
            d[0] is not string key || key != localLevelKey ||
            d[1] is not byte matched || matched > BeanPuzzleRules.AllMask || d[2] is not bool solved ||
            d[3] is not byte reward || reward > (byte)BeanReward.AlreadyClaimed ||
            d[4] is not double solvedAt || double.IsNaN(solvedAt) || double.IsInfinity(solvedAt) ||
            d[5] is not int[] ids || ids.Length != BeanPuzzleRules.Count ||
            d[6] is not float[] floors || floors.Length != BeanPuzzleRules.Count * 3) return false;
        if (solved != (matched == BeanPuzzleRules.AllMask) || (!solved && reward != (byte)BeanReward.Pending)) return false;
        foreach (int id in ids) if (id < 0) return false;
        foreach (float f in floors) if (float.IsNaN(f) || float.IsInfinity(f)) return false;
        state = new BeanSnapshot(matched, solved, (BeanReward)reward, solvedAt);
        beanViewIds = ids; beanFloors = floors;
        return true;
    }
}

/// <summary>
/// Remembers which levels of which saves already paid their Golden Egg, so reloading a save cannot farm it.
/// Kept on the host's disk (see BeanPuzzle); this class is only the bookkeeping.
/// </summary>
internal sealed class BeanRewardLedger
{
    private const int Limit = 200;
    private readonly List<string> _claimed = new();

    /// <summary>One entry per level of a run: the save file and how many levels were completed before it.</summary>
    internal static string Key(string saveFile, int levelsCompleted) =>
        (string.IsNullOrEmpty(saveFile) ? "unsaved" : saveFile.Replace('\n', ' ').Replace('\r', ' ').Replace('|', '_')) + "|" + levelsCompleted;

    internal bool Claimed(string key) => _claimed.Contains(key);

    /// <summary>True the first time a key is claimed; false for every repeat.</summary>
    internal bool Claim(string key)
    {
        if (string.IsNullOrEmpty(key) || _claimed.Contains(key)) return false;
        _claimed.Add(key);
        if (_claimed.Count > Limit) _claimed.RemoveRange(0, _claimed.Count - Limit);
        return true;
    }

    internal string[] Lines() => _claimed.ToArray();

    internal void Load(IEnumerable<string> lines)
    {
        _claimed.Clear();
        if (lines == null) return;
        foreach (string line in lines)
        {
            string key = line?.Trim();
            if (!string.IsNullOrEmpty(key) && !_claimed.Contains(key)) _claimed.Add(key);
        }
    }
}
