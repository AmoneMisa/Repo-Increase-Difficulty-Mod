using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Photon.Pun;
using UnityEngine;

namespace IncreaseHardMod.Patches.Effects;

internal static class LateJoinSceneState
{
    private const string LevelProperty="ihm_level", CountProperty="ihm_level_count";
    internal static void Publish()
    {
        if(!ModConfig.LateJoinEnabled.Value||!PhotonNetwork.InRoom||!PhotonNetwork.IsMasterClient||RunManager.instance==null)return;
        var run=RunManager.instance;if(run.levelCurrent==null)return;
        var room=PhotonNetwork.CurrentRoom;
        if(room.CustomProperties[LateJoinProgress.Property] is bool sync&&sync&&
            room.CustomProperties[LevelProperty] is string current&&current==run.levelCurrent.name&&
            room.CustomProperties[CountProperty] is int count&&count==run.levelsCompleted)return;
        room.SetCustomProperties(new ExitGames.Client.Photon.Hashtable{
            [LevelProperty]=run.levelCurrent.name,[CountProperty]=run.levelsCompleted,[LateJoinProgress.Property]=true});
    }
    internal static void Apply()
    {
        if(!ModConfig.LateJoinEnabled.Value||!PhotonNetwork.InRoom||PhotonNetwork.IsMasterClient||RunManager.instance==null)return;
        var room=PhotonNetwork.CurrentRoom;var run=RunManager.instance;
        if(room.CustomProperties[LevelProperty] is not string name||room.CustomProperties[CountProperty] is not int count||count<0)return;
        var level=run.levels.Concat(run.levelShop).Concat(run.levelArena)
            .Concat(new[]{run.levelLobby,run.levelLobbyMenu}).FirstOrDefault(l=>l!=null&&l.name==name);
        if(level==null)return;
        // Only on joining/local generation startup: never reload the host or other clients.
        run.levelCurrent=level;run.levelsCompleted=count;run.levelIsShop=SemiFunc.IsLevelShop(level);
    }
    internal static bool IsGameplayScene=>LevelGenerator.Instance!=null&&LevelGenerator.Instance.Generated&&
        RunManager.instance!=null&&LevelGenerator.Instance.Level!=null&&
        LevelGenerator.Instance.Level!=RunManager.instance.levelLobbyMenu&&
        LevelGenerator.Instance.Level!=RunManager.instance.levelMainMenu;
}

[HarmonyPatch(typeof(NetworkConnect), nameof(NetworkConnect.OnJoinedRoom))]
internal static class LateJoinRoomScenePatch
{
    [HarmonyPostfix]
    private static void Postfix()
    {
        LateJoinSceneState.Apply();
        LateJoinProgress.JoinedRoom();
    }
}
[HarmonyPatch(typeof(LevelGenerator), nameof(LevelGenerator.Start))]
internal static class LateJoinGenerationScenePatch
{
    [HarmonyPrefix] private static void Prefix(){LateJoinSceneState.Apply();}
}
[HarmonyPatch(typeof(LobbyMenuOpen), nameof(LobbyMenuOpen.Update))]
internal static class LateJoinLobbyOverlayPatch
{
    [HarmonyPrefix] private static bool Prefix()
    {
        if(!ModConfig.LateJoinEnabled.Value||!LateJoinSceneState.IsGameplayScene)return true;
        if(MenuManager.instance!=null&&MenuPageLobby.instance!=null)
            MenuManager.instance.PageClose(MenuPageIndex.Lobby);
        return false;
    }
}

[HarmonyPatch(typeof(LevelGenerator), nameof(LevelGenerator.PlayerSpawn))]
internal static class LateJoinKeepExistingPlayersPatch
{
    [HarmonyTranspiler] private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var spawn=AccessTools.Method(typeof(PlayerAvatar),nameof(PlayerAvatar.Spawn));
        var filtered=AccessTools.Method(typeof(LateJoinKeepExistingPlayersPatch),nameof(SpawnNewPlayer));
        foreach(var code in instructions)
        {
            if(code.Calls(spawn)){code.opcode=OpCodes.Call;code.operand=filtered;}
            yield return code;
        }
    }
    private static void SpawnNewPlayer(PlayerAvatar player,Vector3 position,Quaternion rotation,bool debug)
    {
        if(LateJoinRules.ShouldSpawn(ModConfig.LateJoinEnabled.Value,LevelGenerator.Instance.Generated,player.spawned))
            player.Spawn(position,rotation,debug);
    }
}
