using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using IncreaseHardMod.Networking;
using Photon.Pun;
using UnityEngine;

namespace IncreaseHardMod.Patches.Effects;

/// <summary>
/// "Map Vote" — while everyone is sitting in the truck lobby, the host rolls a small set of random
/// candidate maps and players vote on which one to run next. Whatever candidate is leading at the
/// moment the truck departs (i.e. when the game calls <see cref="RunManager.SetRunLevel"/>) is used
/// instead of the game's own random pick.
///
/// Design notes:
///   • NO timer sync is needed. The vote is simply open for the whole lobby stay; the leading
///     candidate when the host leaves the lobby wins. This makes the network protocol tiny.
///   • HOST-AUTHORITATIVE. Only the host rolls candidates and only the host applies the winner (by
///     overriding <c>levelCurrent</c> in a <see cref="SetRunLevel"/> prefix). The chosen level is
///     then synced to clients through the game's existing <c>UpdateLevelRPC</c> path, exactly like
///     a normal random pick — so clients load the right scene with zero extra work.
///   • HARMLESS WHEN IGNORED. If nobody casts a ballot, the prefix falls through to the vanilla
///     random selection, so enabling this never forces behaviour on a group that doesn't engage.
///
/// The three pieces:
///   • <see cref="MapVoteState"/> — shared session/tally state, driven by network events.
///   • <see cref="MapVoteController"/> — runtime MonoBehaviour: host session lifecycle, local input,
///     and the on-screen IMGUI panel.
///   • <see cref="RunManagerSetRunLevelPatch"/> — host-side prefix that applies the winner.
/// </summary>
internal static class MapVoteState
{
    // -1 means "no active vote". Otherwise identifies the current lobby's vote so stale ballots
    // (from a previous lobby) are ignored.
    internal static int SessionId = -1;

    // Candidate maps for the active session, resolved locally from their asset names.
    internal static string[] Names = Array.Empty<string>();
    internal static Level[] Levels = Array.Empty<Level>();

    // actorNumber -> (choiceIndex, displayName). One ballot per player; re-voting replaces it.
    internal static readonly Dictionary<int, KeyValuePair<int, string>> Ballots = new();

    // Host-authoritative countdown state (mirrored to clients via the candidates broadcast).
    internal static float RemainingSeconds;
    internal static bool Closed;
    internal static int LockedWinner = -1; // frozen winning index once the vote closes

    internal static bool Active => SessionId != -1 && Names.Length > 0;

    internal static void Clear()
    {
        SessionId = -1;
        Names = Array.Empty<string>();
        Levels = Array.Empty<Level>();
        Ballots.Clear();
        RemainingSeconds = 0f;
        Closed = false;
        LockedWinner = -1;
    }

    // HOST: begin a fresh vote for this lobby with the given candidate names, and resolve levels.
    internal static void StartSession(int sessionId, string[] names, float seconds)
    {
        SessionId = sessionId;
        Names = names ?? Array.Empty<string>();
        Levels = ResolveLevels(Names);
        Ballots.Clear();
        RemainingSeconds = seconds;
        Closed = false;
        LockedWinner = -1;
    }

    // CLIENT: a candidate set + countdown arrived from the host. Adopt candidates if it's a new
    // session; always refresh the authoritative countdown/closed/winner (rebroadcast every ~1s).
    internal static void ReceiveCandidates(int sessionId, string[] names, float remaining, bool closed, int lockedWinner)
    {
        if (sessionId != SessionId)
        {
            SessionId = sessionId;
            Names = names ?? Array.Empty<string>();
            Levels = ResolveLevels(Names);
            Ballots.Clear();
        }

        RemainingSeconds = remaining;
        Closed = closed;
        LockedWinner = lockedWinner;
    }

    // A ballot arrived (from another player). Record it against the current session only, and only
    // while the vote is still open.
    internal static void ReceiveBallot(int sessionId, int actorNumber, int choiceIndex, string voterName)
    {
        if (sessionId != SessionId || !Active || Closed)
            return;
        if (choiceIndex < 0 || choiceIndex >= Names.Length)
            return;

        Ballots[actorNumber] = new KeyValuePair<int, string>(choiceIndex, voterName ?? "?");
    }

    internal static int VotesFor(int choiceIndex)
    {
        int n = 0;
        foreach (var b in Ballots.Values)
            if (b.Key == choiceIndex)
                n++;
        return n;
    }

    // The winning candidate index, or -1 if there are no ballots. Ties are broken randomly so a
    // deadlocked vote still feels like a roll rather than always favouring the first option.
    internal static int WinningIndex()
    {
        if (Ballots.Count == 0 || Names.Length == 0)
            return -1;

        int best = -1;
        var tied = new List<int>();
        for (int i = 0; i < Names.Length; i++)
        {
            int v = VotesFor(i);
            if (v == 0)
                continue;
            if (v > best)
            {
                best = v;
                tied.Clear();
                tied.Add(i);
            }
            else if (v == best)
            {
                tied.Add(i);
            }
        }

        if (tied.Count == 0)
            return -1;
        return tied[UnityEngine.Random.Range(0, tied.Count)];
    }

    private static Level[] ResolveLevels(string[] names)
    {
        var rm = RunManager.instance;
        var result = new Level[names.Length];
        if (rm == null || rm.levels == null)
            return result;

        for (int i = 0; i < names.Length; i++)
        {
            foreach (var lvl in rm.levels)
            {
                if (lvl != null && lvl.name == names[i])
                {
                    result[i] = lvl;
                    break;
                }
            }
        }
        return result;
    }
}

/// <summary>
/// Runtime MonoBehaviour (lives on the mod's runtime object). Runs the host-side session lifecycle,
/// handles local voting input, and draws the vote panel while in the lobby.
/// </summary>
internal sealed class MapVoteController : MonoBehaviour
{
    private const float RebroadcastInterval = 1f; // host re-sends candidates + countdown to stay in sync
    private float _rebroadcastTimer;
    private float _voteStartTime;
    private int _sessionCounter;

    private GUIStyle _titleStyle;
    private GUIStyle _rowStyle;
    private GUIStyle _hintStyle;

    private static readonly FieldInfo PreviousRunLevelField =
        AccessTools.Field(typeof(RunManager), "previousRunLevel");

    private void Update()
    {
        if (!ModConfig.MapVoteEnabled.Value)
        {
            if (MapVoteState.Active)
                MapVoteState.Clear();
            return;
        }

        bool inLobby = SafeRunIsLobby();

        // Everyone drops the session once we leave the lobby (the vote only exists there).
        if (!inLobby)
        {
            if (MapVoteState.Active)
                MapVoteState.Clear();
            return;
        }

        // Host owns the candidate roll, the countdown, and rebroadcasts for late joiners.
        if (SemiFunc.IsMasterClientOrSingleplayer())
        {
            if (!MapVoteState.Active)
            {
                RollNewSession();
            }
            else
            {
                UpdateHostCountdown(out bool justClosed);

                _rebroadcastTimer -= Time.deltaTime;
                if (justClosed || _rebroadcastTimer <= 0f)
                {
                    _rebroadcastTimer = RebroadcastInterval;
                    ModNetwork.Instance?.BroadcastMapVoteCandidates(
                        MapVoteState.SessionId, MapVoteState.Names,
                        MapVoteState.RemainingSeconds, MapVoteState.Closed, MapVoteState.LockedWinner);
                }
            }
        }

        HandleVoteInput();
    }

    // Host: tick the countdown and close the vote when time runs out OR everyone has voted. On
    // close, freeze the winning index so ties don't re-roll and late ballots can't change it.
    private void UpdateHostCountdown(out bool justClosed)
    {
        justClosed = false;
        if (MapVoteState.Closed)
        {
            MapVoteState.RemainingSeconds = 0f;
            return;
        }

        float remaining = Mathf.Max(0f, ModConfig.MapVoteSeconds.Value - (Time.time - _voteStartTime));
        MapVoteState.RemainingSeconds = remaining;

        int players = PhotonNetwork.InRoom && PhotonNetwork.CurrentRoom != null
            ? PhotonNetwork.CurrentRoom.PlayerCount
            : 1;
        bool everyoneVoted = players > 0 && MapVoteState.Ballots.Count >= players;

        if (remaining <= 0f || everyoneVoted)
        {
            MapVoteState.Closed = true;
            MapVoteState.RemainingSeconds = 0f;
            MapVoteState.LockedWinner = MapVoteState.WinningIndex(); // -1 if nobody voted → random
            justClosed = true;
        }
    }

    private void RollNewSession()
    {
        var rm = RunManager.instance;
        if (rm == null || rm.levels == null || rm.levels.Count == 0)
            return;

        int want = Mathf.Clamp(ModConfig.MapVoteChoices.Value, 2, 6);

        // Candidate pool: all run levels except the one we just played (avoid an immediate repeat).
        var prev = PreviousRunLevelField?.GetValue(rm) as Level;
        var pool = new List<Level>();
        foreach (var lvl in rm.levels)
        {
            if (lvl == null)
                continue;
            if (lvl == prev)
                continue;
            pool.Add(lvl);
        }
        if (pool.Count == 0) // only the previous level exists — fall back to using it
        {
            foreach (var lvl in rm.levels)
                if (lvl != null)
                    pool.Add(lvl);
        }
        if (pool.Count == 0)
            return;

        // Shuffle and take up to `want` distinct candidates.
        for (int i = pool.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            (pool[i], pool[j]) = (pool[j], pool[i]);
        }
        int count = Mathf.Min(want, pool.Count);
        var names = new string[count];
        for (int i = 0; i < count; i++)
            names[i] = pool[i].name;

        int sessionId = unchecked((RunManager.instance.levelsCompleted << 8) ^ (++_sessionCounter) ^ Environment.TickCount);
        float seconds = Mathf.Max(1f, ModConfig.MapVoteSeconds.Value);
        MapVoteState.StartSession(sessionId, names, seconds);
        _voteStartTime = Time.time;
        _rebroadcastTimer = RebroadcastInterval;
        ModNetwork.Instance?.BroadcastMapVoteCandidates(sessionId, names, seconds, false, -1);
    }

    private void HandleVoteInput()
    {
        if (!MapVoteState.Active || MapVoteState.Closed)
            return;
        if (!SafeNoTextInputs())
            return;

        int count = MapVoteState.Names.Length;
        for (int i = 0; i < count; i++)
        {
            if (Input.GetKeyDown(KeyCode.Alpha1 + i) || Input.GetKeyDown(KeyCode.Keypad1 + i))
            {
                CastLocalVote(i);
                return;
            }
        }
    }

    private static void CastLocalVote(int choiceIndex)
    {
        if (!MapVoteState.Active || MapVoteState.Closed || choiceIndex < 0 || choiceIndex >= MapVoteState.Names.Length)
            return;

        int actor = LocalActor();
        string name = LocalName();
        // Apply locally first (RaiseEvent to All does not echo to the sender).
        MapVoteState.ReceiveBallot(MapVoteState.SessionId, actor, choiceIndex, name);
        ModNetwork.Instance?.BroadcastMapVoteBallot(MapVoteState.SessionId, actor, choiceIndex, name);
    }

    private void OnGUI()
    {
        if (!ModConfig.MapVoteEnabled.Value || !MapVoteState.Active)
            return;

        EnsureStyles();

        int count = MapVoteState.Names.Length;
        int myChoice = -1;
        if (MapVoteState.Ballots.TryGetValue(LocalActor(), out var mine))
            myChoice = mine.Key;

        const float width = 340f;
        float rowH = 40f;
        float height = 34f + count * rowH + 24f;
        float x = (Screen.width - width) * 0.5f;
        float y = Screen.height * 0.14f;

        GUI.color = new Color(0f, 0f, 0f, 0.55f);
        GUI.DrawTexture(new Rect(x - 8f, y - 8f, width + 16f, height + 16f), Texture2D.whiteTexture);
        GUI.color = Color.white;

        bool closed = MapVoteState.Closed;
        int lockedWinner = MapVoteState.LockedWinner;

        string title = closed
            ? "Vote closed"
            : $"Vote: Next Map   ({Mathf.CeilToInt(MapVoteState.RemainingSeconds)}s)";
        GUI.Label(new Rect(x, y, width, 28f), title, _titleStyle);
        y += 34f;

        for (int i = 0; i < count; i++)
        {
            int votes = MapVoteState.VotesFor(i);
            bool isMine = i == myChoice;
            bool isWinner = closed && i == lockedWinner;
            var rowRect = new Rect(x, y, width, rowH - 6f);

            var prev = GUI.color;
            GUI.color = isWinner ? new Color(0.4f, 1f, 0.5f, 0.9f)
                : isMine ? new Color(0.35f, 0.75f, 1f, 0.9f)
                : new Color(1f, 1f, 1f, 0.12f);
            GUI.DrawTexture(rowRect, Texture2D.whiteTexture);
            GUI.color = prev;

            string prefix = isWinner ? "\u2714 " : $"[{i + 1}] ";
            string label = $"{prefix}{DisplayName(i)}   —   {votes} vote{(votes == 1 ? "" : "s")}";
            if (GUI.Button(rowRect, label, _rowStyle) && !closed)
                CastLocalVote(i);

            y += rowH;
        }

        string hint = closed
            ? (lockedWinner >= 0 ? "Winner locked in — loads on truck departure." : "No votes — a random map will be used.")
            : "Press 1-" + count + " (or click) to vote. Closes at 0s or once everyone votes.";
        GUI.Label(new Rect(x, y, width, 22f), hint, _hintStyle);
    }

    private static string DisplayName(int i)
    {
        var lvl = i >= 0 && i < MapVoteState.Levels.Length ? MapVoteState.Levels[i] : null;
        if (lvl != null && !string.IsNullOrEmpty(lvl.NarrativeName))
            return lvl.NarrativeName;
        string raw = i < MapVoteState.Names.Length ? MapVoteState.Names[i] : "Map";
        return raw.Replace("Level - ", "").Replace("Level ", "").Trim();
    }

    private void EnsureStyles()
    {
        _titleStyle ??= new GUIStyle
        {
            fontSize = 18,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = new Color(1f, 0.9f, 0.5f) },
        };
        _rowStyle ??= new GUIStyle(GUI.skin.button)
        {
            fontSize = 14,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
        };
        _hintStyle ??= new GUIStyle
        {
            fontSize = 12,
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = new Color(0.85f, 0.85f, 0.85f) },
        };
    }

    private static int LocalActor()
    {
        if (PhotonNetwork.InRoom && PhotonNetwork.LocalPlayer != null)
            return PhotonNetwork.LocalPlayer.ActorNumber;
        return 0;
    }

    private static string LocalName()
    {
        if (PhotonNetwork.InRoom && PhotonNetwork.LocalPlayer != null &&
            !string.IsNullOrEmpty(PhotonNetwork.LocalPlayer.NickName))
            return PhotonNetwork.LocalPlayer.NickName;
        return "You";
    }

    private static bool SafeRunIsLobby()
    {
        try { return SemiFunc.RunIsLobby(); }
        catch { return false; }
    }

    private static bool SafeNoTextInputs()
    {
        try { return SemiFunc.NoTextInputsActive(); }
        catch { return true; }
    }
}

/// <summary>
/// Host-side prefix on <see cref="RunManager.SetRunLevel"/>. If a map vote is live and has at least
/// one ballot, override the next run level with the winning candidate and skip the game's random
/// pick. The chosen level then propagates to clients through the normal <c>UpdateLevelRPC</c> path.
/// </summary>
[HarmonyPatch(typeof(RunManager), nameof(RunManager.SetRunLevel))]
internal static class RunManagerSetRunLevelPatch
{
    [HarmonyPrefix]
    private static bool Prefix(RunManager __instance)
    {
        if (!ModConfig.MapVoteEnabled.Value)
            return true;
        if (!SemiFunc.IsMasterClientOrSingleplayer())
            return true;
        if (!MapVoteState.Active)
            return true;

        // Use the frozen winner if the vote already closed; otherwise the current leader (truck may
        // depart before the countdown ends).
        int win = MapVoteState.Closed ? MapVoteState.LockedWinner : MapVoteState.WinningIndex();
        if (win < 0 || win >= MapVoteState.Levels.Length)
            return true; // no ballots — let the vanilla random selection run

        var level = MapVoteState.Levels[win];
        if (level == null)
            return true;

        __instance.levelCurrent = level;
        Plugin.Log?.LogInfo($"[MapVote] Applied voted map: {level.name} ({MapVoteState.VotesFor(win)} vote(s)).");

        // Consume the session so it can't reapply; a fresh one rolls on the next lobby.
        MapVoteState.Clear();
        return false; // skip the original random pick
    }
}
