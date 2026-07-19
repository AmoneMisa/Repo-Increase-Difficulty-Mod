using IncreaseHardMod.Modifiers;
using IncreaseHardMod.Networking;
using Photon.Pun;
using UnityEngine;

namespace IncreaseHardMod.Patches.Effects;

/// <summary>
/// Enables mid-game joining. REPO deliberately seals the session at run start —
/// <c>MenuPageLobby.ButtonStart</c> sets <c>PhotonNetwork.CurrentRoom.IsOpen = false</c>
/// (+ IsVisible = false) and calls <c>SteamManager.LockLobby()</c>, and the room is
/// only re-opened back in the lobby menu. That's why vanilla players can only join at
/// the lobby, never during a run/shop/truck.
///
/// Design: we re-open the room ONCE right after the game closes it
/// (<see cref="LateJoinButtonStartPatch"/> postfixes <c>MenuPageLobby.ButtonStart</c>),
/// then a cheap THROTTLED safety check (this class, driven from ModNetwork.Update) makes
/// sure it stays open in case some other game code re-closes it. The old version forced
/// room state EVERY frame including during scene loads / before the room existed, which
/// is almost certainly why hosting hung on the loading screen. Now every access is:
///   * throttled (~2s between checks),
///   * gated to a settled in-game state (GameDirector Main + actually in a run/shop/truck),
///   * host-only, and
///   * wrapped in try/catch so a transient null during a load can never break hosting.
///
/// Joining only gets a player into the Photon room; they still need the current mod
/// state. We detect a rise in PhotonNetwork.PlayerList and re-broadcast modifiers, the
/// level timer, and cart-upgrade levels so late joiners converge on the same state.
/// </summary>
internal static class LateJoinController
{
    private static int _lastPlayerCount = -1;
    private static float _nextCheck;
    private const float CheckInterval = 2f;

    // Snapshots for the client-side enemy-registration safety net (see EnsureEnemyRegistrations).
    private static int _lastRegPlayers = -1;
    private static int _lastRegEnemies = -1;

    /// <summary>Re-open the room the same way returning to the lobby would.</summary>
    public static void OpenRoom(string reason)
    {
        if (!ModConfig.LateJoinEnabled.Value)
            return;
        try
        {
            if (!PhotonNetwork.InRoom || !SemiFunc.IsMasterClient())
                return;
            var room = PhotonNetwork.CurrentRoom;
            if (room == null || room.IsOpen)
                return;

            room.IsOpen = true;
            if (GameManager.instance != null && GameManager.instance.connectRandom)
                room.IsVisible = true;
            else if (SteamManager.instance != null)
                SteamManager.instance.UnlockLobby(false);
            Plugin.Log.LogInfo($"[LateJoin] Re-opened the session ({reason}) so players can join mid-game.");
        }
        catch (System.Exception e)
        {
            Plugin.Log.LogWarning($"[LateJoin] OpenRoom failed harmlessly: {e.Message}");
        }
    }

    public static void Tick()
    {
        if (!ModConfig.LateJoinEnabled.Value)
            return;

        // Only ever touch room state when hosting a real Photon session that has settled
        // into an actual run/shop/truck. Bail cheaply (and reset) otherwise — this is what
        // keeps us off the code path during loading screens / offline singleplayer.
        if (!PhotonNetwork.InRoom || !SemiFunc.IsMasterClient())
        {
            _lastPlayerCount = -1;
            return;
        }

        if (Time.realtimeSinceStartup < _nextCheck)
            return;
        _nextCheck = Time.realtimeSinceStartup + CheckInterval;

        try
        {
            if (GameDirector.instance == null ||
                GameDirector.instance.currentState != GameDirector.gameState.Main)
                return;
            if (!SemiFunc.RunIsLevel() && !SemiFunc.RunIsShop() && !SemiFunc.RunIsLobby())
                return;

            OpenRoom("safety check");

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
            }
            _lastPlayerCount = count;
        }
        catch (System.Exception e)
        {
            Plugin.Log.LogWarning($"[LateJoin] Tick failed harmlessly: {e.Message}");
        }
    }

    /// <summary>
    /// Client-side safety net that makes mid-run joins actually playable. R.E.P.O. registers
    /// players into each enemy's on-screen/vision dictionaries ONLY ONCE, when the level's
    /// enemies finish spawning (<c>Enemy.PlayerAdded</c>, run locally on every client). Anyone
    /// who joins after that — or any enemy that spawns after that — is never registered, so the
    /// enemy's Logic/Vision coroutines do a raw <c>dict[player.photonView.ViewID]</c> lookup and
    /// throw KeyNotFoundException every tick (the crash that warps the run to the truck).
    ///
    /// Whenever this client's player count OR spawned-enemy count changes, we re-register every
    /// current player into every spawned enemy. <c>Enemy.PlayerAdded</c> is an idempotent TryAdd,
    /// so re-adding already-known players is harmless. This must run on ALL clients (not just the
    /// host) because the dictionaries live on each client's own enemy instances. Called every
    /// frame from <see cref="Networking.ModNetwork"/>, but only does real work when a count moves.
    /// </summary>
    public static void EnsureEnemyRegistrations()
    {
        if (!ModConfig.LateJoinEnabled.Value)
            return;

        try
        {
            var director = GameDirector.instance;
            var enemyDir = EnemyDirector.instance;
            if (director == null || director.PlayerList == null ||
                enemyDir == null || enemyDir.enemiesSpawned == null ||
                !SemiFunc.RunIsLevel())
            {
                _lastRegPlayers = -1;
                _lastRegEnemies = -1;
                return;
            }

            int players = director.PlayerList.Count;
            int enemies = enemyDir.enemiesSpawned.Count;
            if (players == _lastRegPlayers && enemies == _lastRegEnemies)
                return;

            foreach (var player in director.PlayerList)
            {
                if (player == null || player.photonView == null)
                    continue;
                int viewId = player.photonView.ViewID;
                foreach (var ep in enemyDir.enemiesSpawned)
                {
                    if (ep != null && ep.Enemy != null)
                        ep.Enemy.PlayerAdded(viewId);
                }
            }

            Plugin.Log.LogInfo(
                $"[LateJoin] Registered {players} player(s) into {enemies} enem(y/ies) after a mid-run change.");
            _lastRegPlayers = players;
            _lastRegEnemies = enemies;
        }
        catch (System.Exception e)
        {
            Plugin.Log.LogWarning($"[LateJoin] Enemy registration failed harmlessly: {e.Message}");
        }
    }
}
