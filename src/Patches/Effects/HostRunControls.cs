using System.Collections.Generic;
using ExitGames.Client.Photon;
using HarmonyLib;
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IncreaseHardMod.Patches.Effects;

/// <summary>
/// Host run controls and the failed-level vote.
/// <list type="bullet">
/// <item><b>Esc menu (host only)</b>: RESTART LEVEL (run levels; asks to click again to confirm) reloads the
/// current level with the same run progress; RETURN TO LOBBY takes everyone back to the lobby menu (saving
/// first when in the shop) without anyone having to rejoin, and the run later resumes where it was: the truck,
/// the shop or the level.</item>
/// <item><b>Failed-level vote</b>: when everyone dies on a run level, instead of going straight to the death
/// arena, all players get 15 seconds to vote [1] RESTART LEVEL or [2] RANDOM MAP. The vote closes as soon
/// as everyone has voted; a tie or no votes restarts the level. The host counts the ballots.</item>
/// <item><b>Restart vote (any player)</b>: VOTE TO RESTART in the Esc menu on a run level asks the host to open a
/// 20-second vote for everyone: [1] restart this map, [2] a random map, [3] keep playing. The level is restarted only
/// if more than half of all players vote for one of the restarts (then the one with more votes wins, a tie keeps the
/// map); otherwise play goes on. While the vote is open keys 1-3 vote instead of switching inventory slots. A new
/// vote can be called a minute after the last one ended.</item>
/// </list>
/// </summary>
internal sealed class HostRunControls : MonoBehaviour, IOnEventCallback
{
    internal const byte BallotEventCode = 170, VoteStateEventCode = 171;
    /// <summary>Guest -> host: please open a restart vote. Host -> that guest: why not (int, see Refusal).</summary>
    internal const byte CallVoteEventCode = 152;
    private const float VoteSeconds = 15f, CalledVoteSeconds = 20f, CallCooldown = 60f;
    private const int FailedVote = 0, CalledVote = 1;
    private const int Refused_Off = 1, Refused_NotLevel = 2, Refused_Busy = 3, Refused_Cooldown = 4;
    // Outcomes of a called vote, sent with the closing state.
    private const int Outcome_None = 0, Outcome_Restart = 1, Outcome_Random = 2, Outcome_Stay = 3;

    private static HostRunControls _instance;
    private static bool _bypass;           // our own ChangeLevel calls must not start another vote

    // Host-side tally.
    private bool _voting;
    private double _deadline;
    private readonly Dictionary<int, int> _ballots = new(); // actor -> 1 restart, 2 random, 3 keep playing
    private int _kind;
    private string _caller = "";
    private double _nextCallAt;

    // Every client's view of the vote (from the host's state broadcast).
    private bool _shown;
    private double _shownDeadline;
    private int _restartVotes, _randomVotes, _stayVotes, _voted, _players, _myVote, _shownKind;
    private string _shownCaller = "";

    /// <summary>A player-called restart vote is on screen here: keys 1-3 vote instead of switching inventory slots.</summary>
    internal static bool CalledVoteShown => _instance != null && _instance._shown && _instance._shownKind == CalledVote;
    private NativeHudPanel _panel;
    private TextMeshProUGUI _label;

    private void OnEnable() { _instance = this; PhotonNetwork.AddCallbackTarget(this); }
    private void OnDisable() { PhotonNetwork.RemoveCallbackTarget(this); ClosePanel(); }

    private static double Now => PhotonNetwork.InRoom ? PhotonNetwork.Time : Time.realtimeSinceStartupAsDouble;
    private static bool Host => SemiFunc.IsMasterClientOrSingleplayer();

    // ------------------------------------------------------------------ actions (host)

    internal static void RestartLevel()
    {
        if (!Host || RunManager.instance == null || !SemiFunc.RunIsLevel()) return;
        Plugin.Log.LogInfo("[Run] Host restarts the level.");
        Economy.CurrencyPayout.RollBackAttempt();   // what the failed attempt earned goes with it
        Change(RunManager.ChangeLevelType.Normal); // a run level that was not completed reloads itself
    }

    internal static void RandomMap()
    {
        if (!Host || RunManager.instance == null) return;
        Plugin.Log.LogInfo("[Run] Moving to a random map.");
        if (SemiFunc.RunIsLevel()) Economy.CurrencyPayout.RollBackAttempt();   // the level is replayed on another map
        Change(RunManager.ChangeLevelType.RunLevel);
    }

    internal static void ReturnToLobby()
    {
        if (!Host || RunManager.instance == null) return;
        try { if (SemiFunc.RunIsShop()) SemiFunc.SaveFileSave(); }
        catch (System.Exception e) { Plugin.Log.LogWarning($"[Run] Save before returning to the lobby failed: {e.Message}"); }
        Plugin.Log.LogInfo("[Run] Host returns everyone to the lobby.");
        // Remember where the run stands (0 run level, 1 shop, 2 truck). The game's lobby-menu Start resumes from
        // loadLevel, and ChangeLevel(LobbyMenu) would save 0 ("run level") since the lobby menu is none of the
        // three - so a crew that left from the truck came back to a level that had already started. The game's own
        // "/level lobby menu" command sets loadLevel the same way.
        var run = RunManager.instance;
        int stage = run.saveLevel;
        run.loadLevel = stage;
        _keepSaveLevel = stage;
        try { Change(RunManager.ChangeLevelType.LobbyMenu); }
        finally { _keepSaveLevel = -1; }
    }

    private static int _keepSaveLevel = -1;

    /// <summary>While returning to the lobby, the saved stage stays where the run was (also in the save file).</summary>
    [HarmonyPatch(typeof(SemiFunc), nameof(SemiFunc.StatSetSaveLevel))]
    private static class KeepSaveLevel
    {
        [HarmonyPrefix]
        private static void Prefix(ref int value)
        {
            if (_keepSaveLevel < 0) return;
            value = _keepSaveLevel;
            if (RunManager.instance != null) RunManager.instance.saveLevel = _keepSaveLevel;
        }
    }

    private static void Change(RunManager.ChangeLevelType type)
    {
        _bypass = true;
        try { RunManager.instance.ChangeLevel(_completedLevel: false, _levelFailed: false, type); }
        finally { _bypass = false; }
        MenuManager.instance?.PageCloseAll();
    }

    // ------------------------------------------------------------------ the failed-level vote

    /// <summary>
    /// True while the current RunManager.ChangeLevel call is being held for the vote. The game calls ChangeLevel
    /// every frame while everyone is dead; Harmony still runs every postfix (and may run later prefixes) for a
    /// held call, so the other ChangeLevel patches check this and do nothing — otherwise each frame re-rolls the
    /// level's modifiers, ends the quests and closes the lobby again.
    /// </summary>
    internal static bool HoldingChange { get; private set; }

    // Everyone died on a run level: hold the arena transition and vote instead.
    [HarmonyPatch(typeof(RunManager), nameof(RunManager.ChangeLevel))]
    private static class FailedLevel
    {
        [HarmonyPrefix]
        [HarmonyPriority(Priority.High)]
        private static bool Prefix(bool _completedLevel, bool _levelFailed)
        {
            HoldingChange = false;
            if (_bypass || !_levelFailed || _completedLevel || !ModConfig.FailVoteEnabled.Value || _instance == null || !Host) return true;
            bool runLevel;
            try { runLevel = SemiFunc.RunIsLevel(); } catch { runLevel = false; }
            if (!runLevel) return true; // arena, shop, truck: vanilla behaviour
            // Everyone died during a player-called vote: it becomes the failed-level vote.
            if (!_instance._voting || _instance._kind != FailedVote) _instance.StartVote(FailedVote, "");
            HoldingChange = true;
            return false;
        }
    }

    private void StartVote(int kind, string caller)
    {
        _voting = true;
        _kind = kind;
        _caller = caller ?? "";
        _ballots.Clear();
        _deadline = Now + (kind == CalledVote ? CalledVoteSeconds : VoteSeconds);
        Plugin.Log.LogInfo(kind == CalledVote ? $"[Run] {_caller} called a restart vote." : "[Run] Level failed: voting restart vs random map.");
        Broadcast();
    }

    // ------------------------------------------------------------------ the player-called restart vote

    /// <summary>Esc menu, any player on a run level: ask the host to open a restart vote.</summary>
    internal static void CallRestartVote()
    {
        MenuManager.instance?.PageCloseAll();
        if (!PhotonNetwork.InRoom || Host)
        {
            int actor = PhotonNetwork.InRoom ? PhotonNetwork.LocalPlayer.ActorNumber : 0;
            int refusal = _instance != null ? _instance.TryCallVote(actor) : Refused_Off;
            if (refusal != 0) ShowRefusal(refusal, _instance != null ? _instance.CooldownLeft : 0);
            return;
        }
        PhotonNetwork.RaiseEvent(CallVoteEventCode, 0, new RaiseEventOptions { Receivers = ReceiverGroup.MasterClient },
            SendOptions.SendReliable);
    }

    private int CooldownLeft => Mathf.Max(1, Mathf.CeilToInt((float)(_nextCallAt - Now)));

    /// <summary>Host: open a restart vote for <paramref name="actor"/>, or say why not (0 = opened).</summary>
    private int TryCallVote(int actor)
    {
        if (!ModConfig.PlayerRestartVoteEnabled.Value) return Refused_Off;
        bool runLevel;
        try { runLevel = SemiFunc.RunIsLevel(); } catch { runLevel = false; }
        if (!runLevel) return Refused_NotLevel;
        if (_voting) return Refused_Busy;
        if (Now < _nextCallAt) return Refused_Cooldown;
        StartVote(CalledVote, PlayerName(actor));
        return 0;
    }

    private static string PlayerName(int actor)
    {
        string name = PhotonNetwork.InRoom ? PhotonNetwork.CurrentRoom.GetPlayer(actor)?.NickName : null;
        if (string.IsNullOrEmpty(name)) name = PlayerAvatar.instance != null ? SemiFunc.PlayerGetName(PlayerAvatar.instance) : "";
        name = (name ?? "").Replace("<", "").Replace(">", "");
        return name.Length > 24 ? name.Substring(0, 24) : name;
    }

    private static void ShowRefusal(int reason, int cooldown)
    {
        bool ru = Localization.GameLanguage.Russian;
        string text = reason switch
        {
            Refused_NotLevel => ru ? "ГОЛОСОВАНИЕ ЗА РЕСТАРТ — ТОЛЬКО НА УРОВНЕ" : "RESTART VOTES ARE FOR LEVELS ONLY",
            Refused_Busy => ru ? "ГОЛОСОВАНИЕ УЖЕ ИДЁТ" : "A VOTE IS ALREADY RUNNING",
            Refused_Cooldown => ru ? $"НОВОЕ ГОЛОСОВАНИЕ ЧЕРЕЗ {cooldown} С" : $"NEXT VOTE IN {cooldown}S",
            _ => ru ? "ХОСТ ОТКЛЮЧИЛ ГОЛОСОВАНИЕ ЗА РЕСТАРТ" : "THE HOST TURNED RESTART VOTES OFF",
        };
        Hud.HudNotify.Toast(text, Hud.HudStyle.Bad, 3f, "restart_vote");
    }

    private static void ShowOutcome(int outcome)
    {
        if (outcome == Outcome_None) return;
        bool ru = Localization.GameLanguage.Russian;
        string text = outcome switch
        {
            Outcome_Restart => ru ? "ГОЛОСОВАНИЕ: ПЕРЕЗАПУСК ЭТОЙ КАРТЫ" : "VOTE: RESTARTING THIS MAP",
            Outcome_Random => ru ? "ГОЛОСОВАНИЕ: СЛУЧАЙНАЯ КАРТА" : "VOTE: A RANDOM MAP",
            _ => ru ? "ГОЛОСОВАНИЕ: ИГРАЕМ ДАЛЬШЕ" : "VOTE: KEEP PLAYING",
        };
        Hud.HudNotify.Toast(text, outcome == Outcome_Stay ? Hud.HudStyle.Info : Hud.HudStyle.Title, 3f, "restart_vote");
    }

    /// <summary>While a player-called vote is on screen, the inventory keys (1-3) cast votes instead.</summary>
    [HarmonyPatch(typeof(SemiFunc), nameof(SemiFunc.InputDown))]
    private static class VoteKeysNotInventory
    {
        [HarmonyPrefix]
        private static bool Prefix(InputKey key, ref bool __result)
        {
            if (key is not (InputKey.Inventory1 or InputKey.Inventory2 or InputKey.Inventory3) || !CalledVoteShown) return true;
            __result = false;
            return false;
        }
    }

    private void Update()
    {
        if (_voting && Host) HostTick();
        ReadInput();
        DrawPanel();
        EscButtons.Watch();
    }

    private void HostTick()
    {
        if (_kind == CalledVote)
        {
            bool runLevel;
            try { runLevel = SemiFunc.RunIsLevel(); } catch { runLevel = false; }
            if (!runLevel) { _voting = false; _nextCallAt = Now + CallCooldown; BroadcastClose(Outcome_None); return; } // the level ended meanwhile
        }
        int players = PhotonNetwork.InRoom ? PhotonNetwork.CurrentRoom.PlayerCount : 1;
        // Forget ballots from players who left.
        if (PhotonNetwork.InRoom)
            foreach (int actor in new List<int>(_ballots.Keys))
                if (PhotonNetwork.CurrentRoom.GetPlayer(actor) == null) _ballots.Remove(actor);
        bool everyone = _ballots.Count >= players;
        if (!everyone && Now < _deadline) return;

        int restart = 0, random = 0;
        foreach (int vote in _ballots.Values) { if (vote == 1) restart++; else if (vote == 2) random++; }
        _voting = false;
        if (_kind == CalledVote)
        {
            _nextCallAt = Now + CallCooldown;
            // A called vote restarts only with more than half of everyone in the room behind a restart.
            int outcome = (restart + random) * 2 > players ? (random > restart ? Outcome_Random : Outcome_Restart) : Outcome_Stay;
            Plugin.Log.LogInfo($"[Run] Restart vote: same map {restart}, random {random}, of {players} players -> {outcome}.");
            BroadcastClose(outcome);
            if (outcome == Outcome_Random) RandomMap();
            else if (outcome == Outcome_Restart) RestartLevel();
            return;
        }
        BroadcastClose(Outcome_None);
        if (random > restart) RandomMap();
        else RestartLevel(); // tie or nobody voted: replay the same level
    }

    private void Ballot(int actor, int vote)
    {
        if (!_voting || vote < 1 || vote > (_kind == CalledVote ? 3 : 2)) return;
        _ballots[actor] = vote;
        Broadcast();
    }

    private void ReadInput()
    {
        if (!_shown || !SemiFunc.NoTextInputsActive()) return;
        int vote = Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1) ? 1 :
                   Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2) ? 2 :
                   _shownKind == CalledVote && (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3)) ? 3 : 0;
        if (vote == 0) return;
        _myVote = vote;
        if (!PhotonNetwork.InRoom || Host) Ballot(PhotonNetwork.InRoom ? PhotonNetwork.LocalPlayer.ActorNumber : 0, vote);
        else
            PhotonNetwork.RaiseEvent(BallotEventCode, vote, new RaiseEventOptions { Receivers = ReceiverGroup.MasterClient },
                SendOptions.SendReliable);
    }

    private void Broadcast()
    {
        int restart = 0, random = 0, stay = 0;
        foreach (int vote in _ballots.Values) { if (vote == 1) restart++; else if (vote == 2) random++; else if (vote == 3) stay++; }
        int players = PhotonNetwork.InRoom ? PhotonNetwork.CurrentRoom.PlayerCount : 1;
        Apply(true, _deadline, restart, random, stay, _ballots.Count, players, _kind, _caller, Outcome_None);
        if (PhotonNetwork.InRoom && PhotonNetwork.IsMasterClient)
            PhotonNetwork.RaiseEvent(VoteStateEventCode, new object[] { true, _deadline, restart, random, _ballots.Count, players, _kind, stay, _caller, Outcome_None },
                new RaiseEventOptions { Receivers = ReceiverGroup.Others }, SendOptions.SendReliable);
    }

    private void BroadcastClose(int outcome)
    {
        Apply(false, 0, 0, 0, 0, 0, 0, _kind, "", outcome);
        if (PhotonNetwork.InRoom && PhotonNetwork.IsMasterClient)
            PhotonNetwork.RaiseEvent(VoteStateEventCode, new object[] { false, 0d, 0, 0, 0, 0, _kind, 0, "", outcome },
                new RaiseEventOptions { Receivers = ReceiverGroup.Others }, SendOptions.SendReliable);
    }

    private void Apply(bool open, double deadline, int restart, int random, int stay, int voted, int players, int kind, string caller, int outcome)
    {
        if (open && (!_shown || _shownKind != kind)) _myVote = 0;
        _shown = open;
        _shownDeadline = deadline;
        _shownKind = kind; _shownCaller = caller ?? "";
        _restartVotes = restart; _randomVotes = random; _stayVotes = stay; _voted = voted; _players = players;
        if (!open) { ClosePanel(); ShowOutcome(outcome); }
    }

    public void OnEvent(EventData ev)
    {
        if (ev.Code == BallotEventCode)
        {
            if (PhotonNetwork.IsMasterClient && ev.CustomData is int vote) Ballot(ev.Sender, vote);
            return;
        }
        if (ev.Code == CallVoteEventCode)
        {
            if (PhotonNetwork.IsMasterClient)
            {
                int refusal = TryCallVote(ev.Sender);
                if (refusal != 0)
                    PhotonNetwork.RaiseEvent(CallVoteEventCode, new[] { refusal, CooldownLeft },
                        new RaiseEventOptions { TargetActors = new[] { ev.Sender } }, SendOptions.SendReliable);
            }
            else if (PhotonNetwork.MasterClient != null && ev.Sender == PhotonNetwork.MasterClient.ActorNumber &&
                     ev.CustomData is int[] answer && answer.Length == 2)
                ShowRefusal(answer[0], Mathf.Clamp(answer[1], 1, 600));
            return;
        }
        if (ev.Code != VoteStateEventCode || PhotonNetwork.MasterClient == null || ev.Sender != PhotonNetwork.MasterClient.ActorNumber) return;
        if (ev.CustomData is not object[] d || d.Length != 10 || d[0] is not bool open || d[1] is not double deadline ||
            d[2] is not int restart || d[3] is not int random || d[4] is not int voted || d[5] is not int players ||
            d[6] is not int kind || d[7] is not int stay || d[8] is not string caller || d[9] is not int outcome) return;
        if (kind is not (FailedVote or CalledVote) || caller.Length > 32 || outcome < Outcome_None || outcome > Outcome_Stay) return;
        Apply(open, deadline, Mathf.Clamp(restart, 0, 64), Mathf.Clamp(random, 0, 64), Mathf.Clamp(stay, 0, 64),
            Mathf.Clamp(voted, 0, 64), Mathf.Clamp(players, 0, 64), kind, caller.Replace("<", "").Replace(">", ""), outcome);
    }

    private void DrawPanel()
    {
        if (!_shown) return;
        if (_panel == null)
        {
            _panel = NativeHudPanel.Create("IHM_FailVote", new Vector2(460f, 150f), new Vector2(.5f, .62f), Vector2.zero, 0f); // no backplate (HUD text style rule)
            // Above the level results board (sorting 80): the vote runs while the results are on screen.
            if (_panel != null) _panel.GetComponent<Canvas>().sortingOrder = 90;
            if (_panel == null) return;
            _label = _panel.Label("FailVote", 26f, TextAlignmentOptions.Center);
            _label.richText = true;
            _label.rectTransform.anchorMin = Vector2.zero;
            _label.rectTransform.anchorMax = Vector2.one;
            _label.rectTransform.offsetMin = new Vector2(10f, 6f);
            _label.rectTransform.offsetMax = new Vector2(-10f, -6f);
        }
        float left = Mathf.Max(0f, (float)(_shownDeadline - Now));
        string Pick(int option) => _myVote == option ? "<color=#ffd24d>>" : "<color=#ffffff>";
        bool ru = Localization.GameLanguage.Russian;
        if (_shownKind == CalledVote)
        {
            string who = _shownCaller.Length > 0 ? _shownCaller.ToUpperInvariant() : (ru ? "ИГРОК" : "A PLAYER");
            _label.text = ru
                ? $"<color=#ffd24d>{who}</color> ПРЕДЛАГАЕТ РЕСТАРТ  {left:0} с\n" +
                  $"{Pick(1)}[1] ЭТА ЖЕ КАРТА  ({_restartVotes})</color>\n" +
                  $"{Pick(2)}[2] СЛУЧАЙНАЯ КАРТА  ({_randomVotes})</color>\n" +
                  $"{Pick(3)}[3] ИГРАТЬ ДАЛЬШЕ  ({_stayVotes})</color>\n" +
                  $"<size=70%>ПРОГОЛОСОВАЛИ {_voted}/{_players} · НУЖНО БОЛЬШЕ ПОЛОВИНЫ ЗА РЕСТАРТ</size>"
                : $"<color=#ffd24d>{who}</color> WANTS A RESTART  {left:0}s\n" +
                  $"{Pick(1)}[1] SAME MAP  ({_restartVotes})</color>\n" +
                  $"{Pick(2)}[2] RANDOM MAP  ({_randomVotes})</color>\n" +
                  $"{Pick(3)}[3] KEEP PLAYING  ({_stayVotes})</color>\n" +
                  $"<size=70%>{_voted}/{_players} VOTED · A RESTART NEEDS MORE THAN HALF</size>";
            _panel.Fit();
            return;
        }
        _label.text = ru
            ? $"<color=#ff5a4d>УРОВЕНЬ ПРОВАЛЕН</color>  ГОЛОСОВАНИЕ  {left:0} с\n" +
              $"{Pick(1)}[1] ПЕРЕЗАПУСТИТЬ УРОВЕНЬ  ({_restartVotes})</color>\n" +
              $"{Pick(2)}[2] СЛУЧАЙНАЯ КАРТА  ({_randomVotes})</color>\n" +
              $"<size=70%>ПРОГОЛОСОВАЛИ {_voted}/{_players}</size>"
            : $"<color=#ff5a4d>LEVEL FAILED</color>  VOTE  {left:0}s\n" +
              $"{Pick(1)}[1] RESTART LEVEL  ({_restartVotes})</color>\n" +
              $"{Pick(2)}[2] RANDOM MAP  ({_randomVotes})</color>\n" +
              $"<size=70%>{_voted}/{_players} VOTED</size>";
        _panel.Fit();
    }

    private void ClosePanel()
    {
        if (_panel != null) Destroy(_panel.gameObject);
        _panel = null;
        _label = null;
    }

    // ------------------------------------------------------------------ Esc menu buttons (host)

    // Not a Harmony patch on MenuPageEsc.Start: Menu Lib hooks that same method (MonoMod), and with both
    // present our postfix did not run, so the buttons never appeared. Instead the runtime object watches for
    // a freshly opened Esc page and adds the buttons once per page (a marker component prevents repeats).
    private static class EscButtons
    {
        private static bool _loggedSkip;

        internal static void Watch()
        {
            var page = MenuPageEsc.instance;
            if (page == null || !page.isActiveAndEnabled || page.GetComponent<EscButtonsAdded>() != null) return;
            page.gameObject.AddComponent<EscButtonsAdded>();
            try { Build(page); }
            catch (System.Exception error) { Plugin.Log.LogWarning("[Esc] Host buttons failed: " + error.Message); }
        }

        // Quit is the bottom button of the Esc menu. New buttons go where Quit was, Quit moves below them,
        // and the whole button group shifts up by the same amount, so the menu keeps its bottom edge and
        // nothing is drawn below the panel or over the voice sliders.
        private static void Build(MenuPageEsc __instance)
        {
            bool runLevel = false, inRun = false;
            try { runLevel = SemiFunc.RunIsLevel(); inRun = runLevel || SemiFunc.RunIsShop() || SemiFunc.RunIsLobby(); } catch { }
            bool hostButtons = ModConfig.HostRunControlsEnabled.Value && Host;
            bool skillTree = ModConfig.PlayerUpgradesEnabled.Value; // every player, not only the host
            // Any player on a run level (the host decides whether the vote opens; it says so if not).
            bool restartVote = runLevel && SemiFunc.IsMultiplayer() && ModConfig.PlayerRestartVoteEnabled.Value;
            if ((!hostButtons && !skillTree && !restartVote) || __instance.quitButton == null || !inRun)
            {
                if (!_loggedSkip)
                    Plugin.Log.LogInfo($"[Esc] No mod buttons: enabled={ModConfig.HostRunControlsEnabled.Value}, host={Host}, " +
                                       $"quit={(__instance.quitButton != null ? __instance.quitButton.name : "none")}, inRun={inRun}.");
                _loggedSkip = true;
                return;
            }
            var quit = __instance.quitButton;
            // Top to bottom. Host actions ask for a second click; the skill tree opens at once.
            var actions = new List<(string text, System.Action action, bool confirm)>();
            bool ru = Localization.GameLanguage.Russian;
            if (skillTree) actions.Add((ru ? "ДЕРЕВО НАВЫКОВ" : "SKILL TREE", OpenSkillTree, false));
            if (restartVote) actions.Add((ru ? "ГОЛОСОВАТЬ ЗА РЕСТАРТ" : "VOTE TO RESTART", CallRestartVote, true));
            if (hostButtons && runLevel) actions.Add((ru ? "ПЕРЕЗАПУСК УРОВНЯ" : "RESTART LEVEL", RestartLevel, true));
            if (hostButtons) actions.Add((ru ? "ВЕРНУТЬСЯ В ЛОББИ" : "RETURN TO LOBBY", ReturnToLobby, true));

            float step = Step(quit);
            var parent = quit.transform.parent;
            bool laidOut = parent != null && parent.GetComponent<LayoutGroup>() != null;
            var top = quit.transform.localPosition;
            int added = 0;
            // Prefer free rows between Quit and the button above it (the vanilla menu leaves a gap there; other
            // mods such as REPOConfig put their own button in it at a fixed spot): nothing has to move.
            var free = laidOut ? null : FreeRows(__instance, quit, step, actions.Count);
            // Not enough room: lift the buttons above Quit one row at a time (Quit stays put), which opens rows
            // just below them, until everything fits. Buttons are moved one by one, never through the menu's group:
            // the page's opening animation resets the group's position, which used to undo the lift.
            int lifted = 0;
            List<MenuButton> above = null;
            if (free == null && !laidOut)
            {
                above = ButtonsAbove(__instance, quit, step);
                while (free == null && lifted < actions.Count && above.Count > 0)
                {
                    lifted++;
                    foreach (var button in above) Move(button, button.transform.localPosition + new Vector3(0f, step, 0f));
                    // Rows opened by the lift are right under the lifted buttons: fill from the top down there.
                    free = FreeRows(__instance, quit, step, actions.Count, topDown: true);
                }
                if (free == null && lifted > 0)
                {
                    foreach (var button in above) Move(button, button.transform.localPosition - new Vector3(0f, step * lifted, 0f));
                    lifted = 0; // undo; the plain layout below takes over
                }
            }
            if (free != null)
            {
                for (int i = 0; i < actions.Count; i++)
                {
                    // Bottom row for the last action, so the list reads top-down in order.
                    var (text, action, confirm) = actions[actions.Count - 1 - i];
                    var clone = Add(quit, text, action, confirm);
                    if (clone == null) continue;
                    Move(clone.GetComponent<MenuButton>(), new Vector3(top.x, free[i], top.z));
                    added++;
                }
                __instance.StartCoroutine(Report(__instance, added, false, step));
                return;
            }
            foreach (var (text, action, confirm) in actions)
            {
                var clone = Add(quit, text, action, confirm);
                if (clone == null) continue;
                if (laidOut) clone.transform.SetSiblingIndex(quit.transform.GetSiblingIndex());
                else Move(clone.GetComponent<MenuButton>(), top - new Vector3(0f, step * added, 0f));
                added++;
            }
            if (added > 0 && !laidOut)
            {
                Move(quit, top - new Vector3(0f, step * added, 0f));
                // Keep the bottom edge: everything above moves up by the rows we added (button by button, see Move).
                foreach (var button in above ?? ButtonsAbove(__instance, quit, step))
                    Move(button, button.transform.localPosition + new Vector3(0f, step * added, 0f));
            }
            // One look at the finished menu a moment later (after other mods' builders and the page's own
            // animation): where every button ended up and whether ours still show their text.
            __instance.StartCoroutine(Report(__instance, added, laidOut, step));
        }

        // Places a menu button and makes it stay there: MenuButton snaps back to a remembered position whenever its
        // text is resized (copies inherit Quit's), so that position is updated too.
        private static void Move(MenuButton button, Vector3 local)
        {
            if (button == null) return;
            button.transform.localPosition = local;
            button.originalButtonPosition = local;
        }

        // The game's own Esc buttons are named "Menu Button - ..."; other mods' buttons keep their fixed place.
        private static bool IsNative(MenuButton button) =>
            button.GetComponent<EscButtonLabel>() == null && button.name.StartsWith("Menu Button", System.StringComparison.Ordinal);

        // The game's buttons stacked above Quit in its own column and parent, which a lift moves up.
        private static List<MenuButton> ButtonsAbove(MenuPageEsc page, MenuButton quit, float step)
        {
            var result = new List<MenuButton>();
            var parent = quit.transform.parent;
            if (parent == null) return result;
            foreach (var button in page.GetComponentsInChildren<MenuButton>(true))
                if (button != quit && button.transform.parent == parent && IsNative(button) &&
                    Mathf.Abs(button.transform.localPosition.x - quit.transform.localPosition.x) <= step * 3f &&
                    button.transform.localPosition.y > quit.transform.localPosition.y + step * .5f)
                    result.Add(button);
            return result;
        }

        // Free rows (local y in Quit's parent, bottom-up) between Quit and the nearest button above it in the same
        // column, at the menu's own row spacing; null when there are fewer than needed.
        private static List<float> FreeRows(MenuPageEsc page, MenuButton quit, float step, int needed, bool topDown = false)
        {
            var parent = quit.transform.parent;
            if (parent == null || step <= 1f) return null;
            float quitY = quit.transform.localPosition.y, column = quit.transform.localPosition.x;
            var taken = new List<float>();
            float ceiling = float.PositiveInfinity;
            foreach (var button in page.GetComponentsInChildren<MenuButton>(false))
            {
                if (button == quit || button.GetComponent<EscButtonLabel>() != null) continue;
                var local = parent.InverseTransformPoint(button.transform.position);
                if (Mathf.Abs(local.x - column) > step * 3f) continue; // another column (sliders, side panels)
                taken.Add(local.y);
                // Only the game's own buttons bound the space; other mods' buttons (REPOConfig's Mods) are just taken rows.
                if (button.transform.parent == parent && IsNative(button) && local.y > quitY + step * .5f) ceiling = Mathf.Min(ceiling, local.y);
            }
            if (float.IsPositiveInfinity(ceiling)) return null;
            var rows = new List<float>();
            for (float y = quitY + step; y < ceiling - step * .5f; y += step)
            {
                bool busy = false;
                foreach (float t in taken) if (Mathf.Abs(t - y) < step * .5f) { busy = true; break; }
                if (!busy) rows.Add(y);
            }
            if (rows.Count < needed) return null;
            // Bottom-up by default (next to Quit); top-down after a lift (directly under the lifted buttons).
            // Either way the result is listed bottom-up, as the caller fills it.
            if (topDown) { rows.Reverse(); rows = rows.GetRange(0, needed); rows.Reverse(); }
            else rows = rows.GetRange(0, needed);
            return rows;
        }

        private static bool _reported;

        private static System.Collections.IEnumerator Report(MenuPageEsc page, int added, bool laidOut, float step)
        {
            yield return new WaitForSecondsRealtime(.5f);
            if (_reported || page == null) yield break;
            _reported = true; // once per game session is enough to diagnose the layout
            var rows = new List<string>();
            foreach (var button in page.GetComponentsInChildren<MenuButton>(true))
            {
                var label = button.buttonText;
                rows.Add($"{button.name} '{(label != null ? label.text : "?")}' pos={button.transform.position} " +
                         $"active={button.gameObject.activeInHierarchy} text={(label != null && label.isActiveAndEnabled)} alpha={(label != null ? label.alpha : 0f):0.##}");
            }
            Plugin.Log.LogInfo($"[Esc] Host buttons added={added} layoutGroup={laidOut} step={step:0.#}: " + string.Join(" | ", rows));
        }

        // Vertical spacing between the native buttons (fallback: 32 px, the game's own moons offset).
        private static float Step(MenuButton quit)
        {
            float best = float.MaxValue;
            foreach (var other in quit.transform.parent.GetComponentsInChildren<MenuButton>(true))
            {
                if (other == quit || other.transform.parent != quit.transform.parent) continue;
                float gap = Mathf.Abs(other.transform.localPosition.y - quit.transform.localPosition.y);
                if (gap > 4f && gap < best) best = gap;
            }
            return best < float.MaxValue ? best : 32f;
        }

        private static void OpenSkillTree()
        {
            if (MenuManager.instance != null) MenuManager.instance.PageCloseAll();
            Skills.SkillTreeScreen.Open();
        }

        private static GameObject Add(MenuButton template, string text, System.Action action, bool confirm)
        {
            var clone = Object.Instantiate(template.gameObject, template.transform.parent);
            clone.name = "IHM " + text;
            // The copied label would keep Quit's localized text: drop the localizer and write ours now
            // (MenuButton.Awake already ran during Instantiate, before buttonTextString could be set).
            foreach (var component in clone.GetComponentsInChildren<MonoBehaviour>(true))
                if (component != null && component.GetType().Name.IndexOf("Localize", System.StringComparison.Ordinal) >= 0)
                    Object.DestroyImmediate(component);
            var menuButton = clone.GetComponent<MenuButton>();
            if (menuButton == null) { Plugin.Log.LogWarning("[Esc] Quit button has no MenuButton; host buttons skipped."); Object.Destroy(clone); return null; }
            // Quit carries an "are you sure?" pop-up whose confirm option quits the game: the copy must not.
            foreach (var popUp in clone.GetComponentsInChildren<MenuButtonPopUp>(true)) Object.DestroyImmediate(popUp);
            // Keep our label: anything that rewrites the copied text (localization, other menu mods) is undone.
            clone.AddComponent<EscButtonLabel>().Text = text;
            menuButton.buttonTextString = text;
            if (menuButton.buttonText != null) menuButton.buttonText.text = text;
            try { menuButton.ResizeToText(); } catch { }
            var button = clone.GetComponent<Button>() ?? clone.GetComponentInChildren<Button>(true);
            if (button == null) { Plugin.Log.LogWarning("[Esc] Quit button has no Button; host buttons skipped."); Object.Destroy(clone); return null; }
            button.onClick = new Button.ButtonClickedEvent(); // drop the cloned Quit listener
            var label = clone.GetComponent<EscButtonLabel>();
            button.onClick.AddListener(() =>
            {
                if (confirm && Time.unscaledTime > label.ArmedUntil)
                {
                    label.ArmedUntil = Time.unscaledTime + 3f; // first click arms; a second click within 3 s confirms
                    return;
                }
                action();
            });
            return clone;
        }
    }
}

/// <summary>Holds a host Esc button's label: our text, or "CLICK AGAIN: ..." while armed for 3 s.</summary>
internal sealed class EscButtonLabel : MonoBehaviour
{
    public string Text;
    public float ArmedUntil = -1f;
    private MenuButton _button;

    private void LateUpdate()
    {
        if (_button == null) _button = GetComponent<MenuButton>();
        var label = _button != null ? _button.buttonText : null;
        if (label == null || string.IsNullOrEmpty(Text)) return;
        string wanted = Time.unscaledTime < ArmedUntil ? (Localization.GameLanguage.Russian ? "НАЖМИТЕ ЕЩЁ РАЗ: " : "CLICK AGAIN: ") + Text : Text;
        if (!label.enabled) label.enabled = true;
        if (label.text == wanted) return;
        label.text = wanted;
        _button.buttonTextString = wanted;
        try { _button.ResizeToText(); } catch { }
    }
}

/// <summary>Marks an Esc page that already has the host buttons.</summary>
internal sealed class EscButtonsAdded : MonoBehaviour { }
