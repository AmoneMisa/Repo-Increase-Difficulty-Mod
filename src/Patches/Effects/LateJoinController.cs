using IncreaseHardMod.Modifiers;
using IncreaseHardMod.Networking;
using Photon.Pun;
using UnityEngine;

namespace IncreaseHardMod.Patches.Effects;

/// <summary>Maintains late-join admission and synchronizes mod state for arrivals.</summary>
internal static class LateJoinController
{
    private static int _lastPlayerCount = -1;
    private static float _nextCheck;
    private const float CheckInterval = 2f;

    // Snapshots for the client-side enemy-registration safety net (see EnsureEnemyRegistrations).
    private static int _lastRegPlayers = -1;
    private static int _lastRegEnemies = -1;

    // State for the client-side frozen-health safety net (see EnsureLocalHealthInitialized).
    private static float _healthCheckDwell;
    private static bool _localHealthFixed;

    /// <summary>Re-open the room the same way returning to the lobby would.</summary>
    public static void OpenRoom(string reason)
    {
        if (!ModConfig.LateJoinEnabled.Value)
            return;
        if (LateJoinCache.Busy)
            return; // the old level's join cache is still being cleared; the safety check reopens afterwards
        try
        {
            LateJoinSession.OpenRoom(reason);
        }
        catch (System.Exception e)
        {
            Plugin.Log.LogWarning($"[LateJoin] OpenRoom failed harmlessly: {e.Message}");
        }
    }

    public static void Tick()
    {
        LateJoinSceneState.Publish();
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
                ModNetwork.Instance?.BroadcastEconomy(Economy.EconomyEvents.Current?.Id ?? 0);
                ModNetwork.Instance?.BroadcastShopEconomy(Economy.ShopEconomy.Current?.Id ?? 0);
                ModNetwork.Instance?.BroadcastBans(Economy.BlackMarket.All);
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

    /// <summary>
    /// Client-side safety net for a mid-run joiner's FROZEN HEALTH (immune to all damage AND healing).
    ///
    /// <c>PlayerHealth.Fetch()</c> is the ONLY thing that initializes a player's health, and it only
    /// runs for the avatar's owner. Its first line is <c>while (!LevelGenerator.Instance.Generated)</c>
    /// — but a player who joins AFTER the level was generated never sees that flag flip on their client,
    /// so Fetch spins forever and never reaches its body. The avatar is therefore left on its field
    /// defaults: <c>health == maxHealth == 100</c> and <c>healthSet == false</c>. That single state is
    /// exactly the reported freeze: <c>Heal()</c> early-returns on <c>health == maxHealth</c> (heal pack /
    /// ally heal / truck all no-op) and no real value is ever established or synced.
    ///
    /// Fix: on our OWN (owning) client, once we're actually playing a level, if Fetch clearly never
    /// finished we run its body ourselves — read the authoritative health/maxHealth from StatsManager,
    /// clamp, write it back, broadcast <c>UpdateHealthRPC</c> to the others, and set <c>healthSet</c>.
    /// A normal client reaches <c>gameState.Main</c> only AFTER generation+Fetch, so a short dwell guards
    /// the Load->Main frame against preempting a healthy start.
    /// </summary>
    public static void EnsureLocalHealthInitialized()
    {
        if (!ModConfig.LateJoinEnabled.Value)
            return;

        try
        {
            // Only relevant while actually playing a settled run level.
            if (GameDirector.instance == null ||
                GameDirector.instance.currentState != GameDirector.gameState.Main ||
                !SemiFunc.RunIsLevel())
            {
                _healthCheckDwell = 0f;
                _localHealthFixed = false;
                return;
            }

            if (_localHealthFixed)
                return;

            var avatar = SemiFunc.PlayerAvatarLocal();
            if (avatar == null || avatar.playerHealth == null)
                return;

            var ph = avatar.playerHealth;

            // Fetch completed normally -> nothing to do (and never touch it again this level).
            if (ph.healthSet)
            {
                _localHealthFixed = true;
                return;
            }

            // Give the normal Load->Main handoff a moment so we never race a healthy Fetch.
            _healthCheckDwell += Time.deltaTime;
            if (_healthCheckDwell < 3f)
                return;

            // Run exactly what PlayerHealth.Fetch() would have done, on our owning client.
            string steamID = SemiFunc.PlayerGetSteamID(avatar);
            int num = StatsManager.instance.GetPlayerHealth(steamID);
            if (num <= 0)
                num = 1;
            ph.health = num;
            ph.maxHealth = 100 + StatsManager.instance.GetPlayerMaxHealth(steamID);
            ph.health = Mathf.Clamp(ph.health, 0, ph.maxHealth);
            StatsManager.instance.SetPlayerHealth(steamID, ph.health, setInShop: false);
            if (GameManager.Multiplayer() && ph.photonView != null)
                ph.photonView.RPC("UpdateHealthRPC", RpcTarget.Others, ph.health, ph.maxHealth, true, false);
            ph.healthSet = true;
            _localHealthFixed = true;
            Plugin.Log.LogInfo(
                $"[LateJoin] Completed stuck health init for local late-joiner: {ph.health}/{ph.maxHealth}.");
        }
        catch (System.Exception e)
        {
            Plugin.Log.LogWarning($"[LateJoin] Local health init failed harmlessly: {e.Message}");
        }
    }
}
