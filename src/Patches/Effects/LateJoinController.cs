using IncreaseHardMod.Modifiers;
using IncreaseHardMod.Networking;
using Photon.Pun;

namespace IncreaseHardMod.Patches.Effects;

/// <summary>
/// Enables mid-game joining. REPO deliberately seals the session at run start —
/// <c>MenuPageLobby.ButtonStart</c> sets <c>PhotonNetwork.CurrentRoom.IsOpen = false</c>
/// (+ IsVisible = false) and calls <c>SteamManager.LockLobby()</c>, and the room is
/// only re-opened back in the lobby menu. That's why vanilla players can only join at
/// the lobby, never during a run/shop/truck.
///
/// We can't intercept every place REPO might close the room, so instead of patching
/// those call sites we simply RE-ASSERT the open state every frame on the host: if the
/// room is closed while we're in a session, reopen it using the game's own recipe
/// (LobbyMenuOpen.Update): IsOpen = true, and either make it public+visible (random
/// matchmaking) or friends-only-joinable (SteamManager.UnlockLobby(false)).
///
/// Joining only gets a player into the Photon room; they still need the current mod
/// state. We detect a rise in PhotonNetwork.PlayerList and re-broadcast modifiers, the
/// level timer, and cart-upgrade levels so late joiners converge on the same state.
///
/// Host-only: clients have no authority over room state and never roll/own it. Driven
/// from ModNetwork.Update (persistent, survives level loads).
/// </summary>
internal static class LateJoinController
{
    private static int _lastPlayerCount = -1;

    public static void Tick()
    {
        if (!ModConfig.LateJoinEnabled.Value)
            return;
        if (!PhotonNetwork.InRoom || !SemiFunc.IsMasterClient())
        {
            _lastPlayerCount = -1;
            return;
        }

        var room = PhotonNetwork.CurrentRoom;
        if (room == null)
            return;

        // Re-open the session the same way returning to the lobby would.
        if (!room.IsOpen)
        {
            room.IsOpen = true;
            if (GameManager.instance != null && GameManager.instance.connectRandom)
                room.IsVisible = true;
            else if (SteamManager.instance != null)
                SteamManager.instance.UnlockLobby(false);
            Plugin.Log.LogInfo("[LateJoin] Re-opened the session so players can join mid-game.");
        }

        // Re-sync mod state whenever someone new connects.
        int count = PhotonNetwork.PlayerList.Length;
        if (_lastPlayerCount < 0)
        {
            _lastPlayerCount = count;
            return;
        }

        if (count > _lastPlayerCount)
        {
            Plugin.Log.LogInfo($"[LateJoin] Player count {_lastPlayerCount} -> {count}; re-broadcasting mod state to joiners.");
            ModNetwork.Instance?.BroadcastModifiers(ModifierManager.Current);
            ModNetwork.Instance?.BroadcastLevelTimer(LevelTimerController.Remaining, LevelTimerController.Running);
            CartUpgradeState.Broadcast();
        }
        _lastPlayerCount = count;
    }
}
