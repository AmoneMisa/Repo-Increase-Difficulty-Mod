using System.Collections;
using HarmonyLib;
using Photon.Pun;
using Steamworks;
using UnityEngine;

namespace IncreaseHardMod.Patches.Effects;

/// <summary>Steam admission and Photon admission are independent, including in public rooms.</summary>
internal static class LateJoinSession
{
    internal static void OpenRoom(string reason)
    {
        if (!ModConfig.LateJoinEnabled.Value || !PhotonNetwork.InRoom || !PhotonNetwork.IsMasterClient ||
            GameManager.instance == null || RunManager.instance == null || RunManager.instance.restarting ||
            GameDirector.instance == null || LevelGenerator.Instance == null || !LevelGenerator.Instance.Generated ||
            (GameDirector.instance.currentState != GameDirector.gameState.Start &&
             GameDirector.instance.currentState != GameDirector.gameState.Main) ||
            (!SemiFunc.RunIsLevel() && !SemiFunc.RunIsShop() && !SemiFunc.RunIsLobby()))
            return;

        var room = PhotonNetwork.CurrentRoom;
        if (room == null) return;
        bool wasOpen = room.IsOpen;
        bool openLobby = GameManager.instance.connectRandom;
        var steam = SteamLobbyManager.instance;

        // Never advertise stale/empty routing data. Native NetworkConnect treats an
        // empty BuildName as "Lobby Closed", before even trying to join Photon.
        if (!GameManager.instance.localTest && steam != null && SteamClient.IsValid &&
            steam.currentLobby.Id.IsValid && steam.currentLobby.IsOwnedBy(SteamClient.SteamId))
        {
            string version = DataDirector.instance?.PhotonVersionGet();
            string region = PhotonNetwork.CloudRegion;
            if (string.IsNullOrEmpty(version) || string.IsNullOrEmpty(region) || string.IsNullOrEmpty(room.Name))
                return; // The host is not ready to publish a usable connection yet.

            bool routingChanged = steam.currentLobby.GetData("BuildName") != version ||
                                  steam.currentLobby.GetData("RoomName") != room.Name ||
                                  steam.currentLobby.GetData("Region") != region;
            if (routingChanged)
                steam.SetLobbyData(room.Name);

            // LockLobby also changes visibility. Restore the host's chosen mode;
            // private sessions must not become public just to support late joins.
            if (steam.privateLobby == openLobby)
                steam.UnlockLobby(openLobby);
            else
                steam.currentLobby.SetJoinable(true);

            if (!wasOpen || routingChanged)
                Plugin.Log.LogInfo($"[LateJoin] Steam lobby {steam.currentLobby.Id}: room={room.Name}, region={region}, version={version}, public={openLobby} ({reason}).");
        }

        // Use the game's wrapper: it also clears the persistent-object close handshake.
        // Do not return early for an already-open Photon room: Steam may still be locked.
        if (!wasOpen) PhotonNetwork.SetRoomOpen(true);
        if (openLobby && !room.IsVisible) room.IsVisible = true;
        if (!wasOpen)
            Plugin.Log.LogInfo($"[LateJoin] Re-opened Photon room ({reason}).");
    }

    /// <summary>
    /// Host-only: refuse joins while a scene is being swapped (truck -> level -> shop -> truck). A guest who connects
    /// mid-transition would load the host's old scene state; <see cref="OpenRoom"/> reopens once generation is done,
    /// and the periodic safety check covers a generation that never reports completion.
    /// </summary>
    internal static void CloseForTransition(string reason)
    {
        if (!ModConfig.LateJoinEnabled.Value || !PhotonNetwork.InRoom || !PhotonNetwork.IsMasterClient) return;
        var room = PhotonNetwork.CurrentRoom;
        if (room == null) return;
        var steam = SteamLobbyManager.instance;
        // Only the joinable flag: LockLobby would also flip a public lobby to friends-only.
        if (!GameManager.instance.localTest && steam != null && SteamClient.IsValid &&
            steam.currentLobby.Id.IsValid && steam.currentLobby.IsOwnedBy(SteamClient.SteamId))
            steam.currentLobby.SetJoinable(false);
        if (room.IsOpen) PhotonNetwork.SetRoomOpen(false);
        Plugin.Log.LogInfo($"[LateJoin] Closed admission during scene change ({reason}).");
    }

    internal static IEnumerator WaitForLobbyData(IEnumerator connect)
    {
        var steam = SteamLobbyManager.instance;
        if (ModConfig.LateJoinEnabled.Value && GameManager.instance != null &&
            !GameManager.instance.connectRandom && !GameManager.instance.localTest &&
            steam != null && SteamClient.IsValid && steam.currentLobby.Id.IsValid &&
            !steam.currentLobby.IsOwnedBy(SteamClient.SteamId))
        {
            var lobby = steam.currentLobby;
            float deadline = Time.realtimeSinceStartup + 8f;
            float nextRefresh = 0f;
            bool waited = false;
            while (string.IsNullOrEmpty(lobby.GetData("BuildName")) || string.IsNullOrEmpty(lobby.GetData("RoomName")) ||
                   string.IsNullOrEmpty(lobby.GetData("Region")))
            {
                // Cancellation or a second invite must not connect the old request.
                if (SteamLobbyManager.instance != steam || steam.currentLobby.Id != lobby.Id)
                    yield break;
                if (Time.realtimeSinceStartup >= deadline) break;
                if (Time.realtimeSinceStartup >= nextRefresh)
                {
                    lobby.Refresh();
                    nextRefresh = Time.realtimeSinceStartup + 1f;
                }
                waited = true;
                yield return null;
            }
            if (SteamLobbyManager.instance != steam || steam.currentLobby.Id != lobby.Id)
                yield break;
            if (waited)
                Plugin.Log.LogInfo($"[LateJoin] Refreshed Steam lobby {lobby.Id}: room={lobby.GetData("RoomName")}, region={lobby.GetData("Region")}, version={lobby.GetData("BuildName")}.");
        }

        // Keep native password/version checks and errors for genuinely closed lobbies.
        // In particular, never invent BuildName or join-or-create a room on the guest.
        yield return connect;
    }
}

[HarmonyPatch(typeof(LevelGenerator), "GenerateDoneApply")]
internal static class LateJoinReadyPatch
{
    [HarmonyPostfix]
    private static void Postfix()
    {
        LateJoinCache.LevelReady();
        LateJoinController.OpenRoom("generation complete");
    }
}

// Friends, invites and steam://joinlobby all use this native connection coroutine.
[HarmonyPatch(typeof(NetworkConnect), "CreateLobby")]
internal static class LateJoinLobbyDataPatch
{
    [HarmonyPostfix]
    private static void Postfix(ref IEnumerator __result) => __result = LateJoinSession.WaitForLobbyData(__result);
}
