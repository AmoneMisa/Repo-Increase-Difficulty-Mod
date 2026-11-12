using System.Collections;

namespace HarmonyLib
{
    [AttributeUsage(AttributeTargets.Class)] public class HarmonyPatch(Type type, string name) : Attribute
    {
        public Type TargetType = type;
        public string MethodName = name;
    }
    public class HarmonyPostfix : Attribute { }
}
namespace UnityEngine
{
    public static class Time { public static float realtimeSinceStartup; }
}
namespace Steamworks
{
    public record struct SteamId(ulong Value) { public bool IsValid => Value != 0; }
    public static class SteamClient { public static bool IsValid = true; public static SteamId SteamId = new(123); }
}
public class Lobby
{
    public Steamworks.SteamId Id = new(456);
    public Steamworks.SteamId Owner = Steamworks.SteamClient.SteamId;
    public bool Joinable;
    public int Refreshes;
    public Dictionary<string, string> Data = new();
    public bool IsOwnedBy(Steamworks.SteamId id) => Owner == id;
    public string GetData(string key) => Data.GetValueOrDefault(key, "");
    public bool SetJoinable(bool value) { Joinable = value; return true; }
    public bool Refresh() { Refreshes++; return true; }
}
public class SteamLobbyManager
{
    public static SteamLobbyManager instance;
    public Lobby currentLobby = new();
    public bool privateLobby = true;
    public int Publishes, Unlocks;
    public void UnlockLobby(bool open) { privateLobby = !open; currentLobby.Joinable = true; Unlocks++; }
    public void SetLobbyData(string room)
    {
        currentLobby.Data["RoomName"] = room;
        currentLobby.Data["Region"] = Photon.Pun.PhotonNetwork.CloudRegion;
        currentLobby.Data["BuildName"] = DataDirector.instance.PhotonVersionGet();
        Publishes++;
    }
}
namespace Photon.Pun
{
    public class Room { public bool IsOpen, IsVisible; public string Name = "actual-photon-room"; }
    public static class PhotonNetwork
    {
        public static bool InRoom = true, IsMasterClient = true;
        public static Room CurrentRoom;
        public static string CloudRegion = "eu";
        public static int OpenCalls;
        public static void SetRoomOpen(bool open) { CurrentRoom.IsOpen = open; OpenCalls++; }
    }
}
public class GameManager { public static GameManager instance; public bool connectRandom, localTest; }
public class RunManager { public static RunManager instance; public bool restarting; }
public class GameDirector
{
    public enum gameState { Load, Start, Main, Outro }
    public static GameDirector instance;
    public gameState currentState = gameState.Main;
}
public class LevelGenerator { public static LevelGenerator Instance; public bool Generated = true; public void GenerateDoneApply() { } }
public class DataDirector { public static DataDirector instance; public string PhotonVersionGet() => "v0.5.0.net3"; }
public static class SemiFunc
{
    public static bool Level = true, Shop, Truck;
    public static bool RunIsLevel() => Level;
    public static bool RunIsShop() => Shop;
    public static bool RunIsLobby() => Truck;
}
public class NetworkConnect { public void CreateLobby() { } }
namespace IncreaseHardMod
{
    public class Entry { public bool Value = true; }
    public static class ModConfig { public static Entry LateJoinEnabled = new(); }
    public class Logger { public void LogInfo(string value) { } }
    public static class Plugin { public static Logger Log = new(); }
}
namespace IncreaseHardMod.Patches.Effects
{
    public static class LateJoinController { public static void OpenRoom(string reason) => LateJoinSession.OpenRoom(reason); }
    public static class LateJoinCache { public static void LevelReady() { } }
}
