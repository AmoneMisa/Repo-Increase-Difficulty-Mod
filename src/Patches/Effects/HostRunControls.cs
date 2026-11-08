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
            if (!_instance._voting) _instance.StartVote();
            HoldingChange = true;
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
        EscButtons.Watch();
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
            if ((!hostButtons && !skillTree) || __instance.quitButton == null || !inRun)
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
