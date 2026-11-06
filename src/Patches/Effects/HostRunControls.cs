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
/// first when in the shop) without anyone having to rejoin.</item>
/// <item><b>Failed-level vote</b>: when everyone dies on a run level, instead of going straight to the death
/// arena, all players get 15 seconds to vote [1] RESTART LEVEL or [2] RANDOM MAP. The vote closes as soon
/// as everyone has voted; a tie or no votes restarts the level. The host counts the ballots.</item>
/// </list>
/// </summary>
internal sealed class HostRunControls : MonoBehaviour, IOnEventCallback
{
    internal const byte BallotEventCode = 170, VoteStateEventCode = 171;
    private const float VoteSeconds = 15f;

    private static HostRunControls _instance;
    private static bool _bypass;           // our own ChangeLevel calls must not start another vote

    // Host-side tally.
    private bool _voting;
    private double _deadline;
    private readonly Dictionary<int, int> _ballots = new(); // actor -> 1 restart, 2 random

    // Every client's view of the vote (from the host's state broadcast).
    private bool _shown;
    private double _shownDeadline;
    private int _restartVotes, _randomVotes, _voted, _players, _myVote;
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
        Change(RunManager.ChangeLevelType.Normal); // a run level that was not completed reloads itself
    }

    internal static void RandomMap()
    {
        if (!Host || RunManager.instance == null) return;
        Plugin.Log.LogInfo("[Run] Moving to a random map.");
        Change(RunManager.ChangeLevelType.RunLevel);
    }

    internal static void ReturnToLobby()
    {
        if (!Host || RunManager.instance == null) return;
        try { if (SemiFunc.RunIsShop()) SemiFunc.SaveFileSave(); }
        catch (System.Exception e) { Plugin.Log.LogWarning($"[Run] Save before returning to the lobby failed: {e.Message}"); }
        Plugin.Log.LogInfo("[Run] Host returns everyone to the lobby.");
        Change(RunManager.ChangeLevelType.LobbyMenu);
    }

    private static void Change(RunManager.ChangeLevelType type)
    {
        _bypass = true;
        try { RunManager.instance.ChangeLevel(_completedLevel: false, _levelFailed: false, type); }
        finally { _bypass = false; }
        MenuManager.instance?.PageCloseAll();
    }

    // ------------------------------------------------------------------ the failed-level vote

    // Everyone died on a run level: hold the arena transition and vote instead.
    [HarmonyPatch(typeof(RunManager), nameof(RunManager.ChangeLevel))]
    private static class FailedLevel
    {
        [HarmonyPrefix]
        [HarmonyPriority(Priority.High)]
        private static bool Prefix(bool _completedLevel, bool _levelFailed)
        {
            if (_bypass || !_levelFailed || _completedLevel || !ModConfig.FailVoteEnabled.Value || _instance == null || !Host) return true;
            bool runLevel;
            try { runLevel = SemiFunc.RunIsLevel(); } catch { runLevel = false; }
            if (!runLevel) return true; // arena, shop, truck: vanilla behaviour
            if (!_instance._voting) _instance.StartVote();
            return false;
        }
    }

    private void StartVote()
    {
        _voting = true;
        _ballots.Clear();
        _deadline = Now + VoteSeconds;
        Plugin.Log.LogInfo("[Run] Level failed: voting restart vs random map.");
        Broadcast();
    }

    private void Update()
    {
        if (_voting && Host) HostTick();
        ReadInput();
        DrawPanel();
    }

    private void HostTick()
    {
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
        BroadcastClose();
        if (random > restart) RandomMap();
        else RestartLevel(); // tie or nobody voted: replay the same level
    }

    private void Ballot(int actor, int vote)
    {
        if (!_voting || vote < 1 || vote > 2) return;
        _ballots[actor] = vote;
        Broadcast();
    }

    private void ReadInput()
    {
        if (!_shown || !SemiFunc.NoTextInputsActive()) return;
        int vote = Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1) ? 1 :
                   Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2) ? 2 : 0;
        if (vote == 0) return;
        _myVote = vote;
        if (!PhotonNetwork.InRoom || Host) Ballot(PhotonNetwork.InRoom ? PhotonNetwork.LocalPlayer.ActorNumber : 0, vote);
        else
            PhotonNetwork.RaiseEvent(BallotEventCode, vote, new RaiseEventOptions { Receivers = ReceiverGroup.MasterClient },
                SendOptions.SendReliable);
    }

    private void Broadcast()
    {
        int restart = 0, random = 0;
        foreach (int vote in _ballots.Values) { if (vote == 1) restart++; else if (vote == 2) random++; }
        int players = PhotonNetwork.InRoom ? PhotonNetwork.CurrentRoom.PlayerCount : 1;
        Apply(true, _deadline, restart, random, _ballots.Count, players);
        if (PhotonNetwork.InRoom && PhotonNetwork.IsMasterClient)
            PhotonNetwork.RaiseEvent(VoteStateEventCode, new object[] { true, _deadline, restart, random, _ballots.Count, players },
                new RaiseEventOptions { Receivers = ReceiverGroup.Others }, SendOptions.SendReliable);
    }

    private void BroadcastClose()
    {
        Apply(false, 0, 0, 0, 0, 0);
        if (PhotonNetwork.InRoom && PhotonNetwork.IsMasterClient)
            PhotonNetwork.RaiseEvent(VoteStateEventCode, new object[] { false, 0d, 0, 0, 0, 0 },
                new RaiseEventOptions { Receivers = ReceiverGroup.Others }, SendOptions.SendReliable);
    }

    private void Apply(bool open, double deadline, int restart, int random, int voted, int players)
    {
        if (open && !_shown) _myVote = 0;
        _shown = open;
        _shownDeadline = deadline;
        _restartVotes = restart; _randomVotes = random; _voted = voted; _players = players;
        if (!open) ClosePanel();
    }

    public void OnEvent(EventData ev)
    {
        if (ev.Code == BallotEventCode)
        {
            if (PhotonNetwork.IsMasterClient && ev.CustomData is int vote) Ballot(ev.Sender, vote);
            return;
        }
        if (ev.Code != VoteStateEventCode || PhotonNetwork.MasterClient == null || ev.Sender != PhotonNetwork.MasterClient.ActorNumber) return;
        if (ev.CustomData is not object[] d || d.Length != 6 || d[0] is not bool open || d[1] is not double deadline ||
            d[2] is not int restart || d[3] is not int random || d[4] is not int voted || d[5] is not int players) return;
        Apply(open, deadline, Mathf.Clamp(restart, 0, 64), Mathf.Clamp(random, 0, 64), Mathf.Clamp(voted, 0, 64), Mathf.Clamp(players, 0, 64));
    }

    private void DrawPanel()
    {
        if (!_shown) return;
        if (_panel == null)
        {
            _panel = NativeHudPanel.Create("IHM_FailVote", new Vector2(460f, 150f), new Vector2(.5f, .62f), Vector2.zero, .7f);
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
        _label.text = $"<color=#ff5a4d>LEVEL FAILED</color>  VOTE  {left:0}s\n" +
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

    [HarmonyPatch(typeof(MenuPageEsc), "Start")]
    private static class EscButtons
    {
        // Quit is the bottom button of the Esc menu. New buttons go where Quit was, Quit moves below them,
        // and the whole button group shifts up by the same amount, so the menu keeps its bottom edge and
        // nothing is drawn below the panel or over the voice sliders.
        [HarmonyPostfix]
        private static void Postfix(MenuPageEsc __instance)
        {
            if (!ModConfig.HostRunControlsEnabled.Value || !Host || __instance.quitButton == null) return;
            bool runLevel = false, inRun = false;
            try { runLevel = SemiFunc.RunIsLevel(); inRun = runLevel || SemiFunc.RunIsShop() || SemiFunc.RunIsLobby(); } catch { }
            if (!inRun) return;
            var quit = __instance.quitButton;
            var actions = new List<(string text, System.Action action)>();
            if (runLevel) actions.Add(("RESTART LEVEL", RestartLevel));
            actions.Add(("RETURN TO LOBBY", ReturnToLobby));

            float step = Step(quit);
            var parent = quit.transform.parent;
            bool laidOut = parent != null && parent.GetComponent<LayoutGroup>() != null;
            var top = quit.transform.localPosition;
            int added = 0;
            foreach (var (text, action) in actions)
            {
                var clone = Add(quit, text, action);
                if (clone == null) continue;
                if (laidOut) clone.transform.SetSiblingIndex(quit.transform.GetSiblingIndex());
                else clone.transform.localPosition = top - new Vector3(0f, step * added, 0f);
                added++;
            }
            if (added == 0 || laidOut) return;
            quit.transform.localPosition = top - new Vector3(0f, step * added, 0f);
            var group = __instance.mainMenuRectTransform;
            if (group != null && quit.transform.IsChildOf(group))
                group.anchoredPosition += new Vector2(0f, step * added);
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

        private static GameObject Add(MenuButton template, string text, System.Action action)
        {
            var clone = Object.Instantiate(template.gameObject, template.transform.parent);
            clone.name = "IHM " + text;
            // The copied label would keep Quit's localized text: drop the localizer and write ours now
            // (MenuButton.Awake already ran during Instantiate, before buttonTextString could be set).
            foreach (var component in clone.GetComponentsInChildren<MonoBehaviour>(true))
                if (component != null && component.GetType().Name.IndexOf("Localize", System.StringComparison.Ordinal) >= 0)
                    Object.DestroyImmediate(component);
            var menuButton = clone.GetComponent<MenuButton>();
            if (menuButton == null) { Object.Destroy(clone); return null; }
            menuButton.buttonTextString = text;
            if (menuButton.buttonText != null) menuButton.buttonText.text = text;
            try { menuButton.ResizeToText(); } catch { }
            var button = clone.GetComponent<Button>() ?? clone.GetComponentInChildren<Button>(true);
            if (button == null) { Object.Destroy(clone); return null; }
            button.onClick = new Button.ButtonClickedEvent(); // drop the cloned Quit listener
            float armed = -1f;
            button.onClick.AddListener(() =>
            {
                if (Time.unscaledTime - armed > 3f)
                {
                    armed = Time.unscaledTime; // first click arms; a second click within 3 s confirms
                    if (menuButton.buttonText != null) menuButton.buttonText.text = "CLICK AGAIN: " + text;
                    return;
                }
                action();
            });
            return clone;
        }
    }
}
