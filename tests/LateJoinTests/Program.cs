using System.Collections;
using IncreaseHardMod;
using IncreaseHardMod.Patches.Effects;
using Photon.Pun;
using Steamworks;
using UnityEngine;

int checks = 0;
void Check(bool ok, string message)
{
    if (!ok) throw new Exception(message);
    checks++;
    Console.WriteLine("PASS: " + message);
}
void Reset()
{
    ModConfig.LateJoinEnabled.Value = true;
    PhotonNetwork.InRoom = PhotonNetwork.IsMasterClient = SteamClient.IsValid = true;
    PhotonNetwork.CurrentRoom = new();
    PhotonNetwork.OpenCalls = 0;
    PhotonNetwork.CloudRegion = "eu";
    SteamLobbyManager.instance = new();
    GameManager.instance = new();
    GameDirector.instance = new();
    RunManager.instance = new();
    DataDirector.instance = new();
    LevelGenerator.Instance = new();
    SemiFunc.Level = true;
    SemiFunc.Shop = SemiFunc.Truck = false;
    Time.realtimeSinceStartup = 0;
}
IEnumerator NativeConnect() { yield return "native-password-and-version-checks"; }

Reset();
LateJoinSession.OpenRoom("test");
Check(PhotonNetwork.CurrentRoom.IsOpen && PhotonNetwork.OpenCalls == 1, "Closed Photon room opens through native wrapper");
Check(SteamLobbyManager.instance.currentLobby.Joinable && SteamLobbyManager.instance.privateLobby && !PhotonNetwork.CurrentRoom.IsVisible,
    "Friends lobby stays private and becomes joinable");
Check(SteamLobbyManager.instance.currentLobby.GetData("RoomName") == "actual-photon-room" &&
    SteamLobbyManager.instance.currentLobby.GetData("BuildName") == "v0.5.0.net3" &&
    SteamLobbyManager.instance.currentLobby.GetData("Region") == "eu", "Host publishes actual Photon routing and version");
SteamLobbyManager.instance.currentLobby.Joinable = false;
LateJoinSession.OpenRoom("steam locked independently");
Check(SteamLobbyManager.instance.currentLobby.Joinable && PhotonNetwork.OpenCalls == 1,
    "Already-open Photon room does not skip reopening Steam");
Check(SteamLobbyManager.instance.Publishes == 1, "Unchanged metadata is not republished every tick");
SteamLobbyManager.instance.currentLobby.Data["BuildName"] = "";
LateJoinSession.OpenRoom("missing build");
Check(SteamLobbyManager.instance.Publishes == 2, "Host repairs missing BuildName even when Photon is already open");

Reset();
GameManager.instance.connectRandom = true;
LateJoinSession.OpenRoom("public");
Check(PhotonNetwork.CurrentRoom.IsVisible && SteamLobbyManager.instance.currentLobby.Joinable &&
    !SteamLobbyManager.instance.privateLobby, "Public rooms reopen both Steam and Photon");

foreach (string guard in new[] { "disabled", "guest", "disconnected", "loading", "outro", "restarting", "menu", "no room", "no game" })
{
    Reset();
    switch (guard)
    {
        case "disabled": ModConfig.LateJoinEnabled.Value = false; break;
        case "guest": PhotonNetwork.IsMasterClient = false; break;
        case "disconnected": PhotonNetwork.InRoom = false; break;
        case "loading": LevelGenerator.Instance.Generated = false; break;
        case "outro": GameDirector.instance.currentState = GameDirector.gameState.Outro; break;
        case "restarting": RunManager.instance.restarting = true; break;
        case "menu": SemiFunc.Level = false; break;
        case "no room": PhotonNetwork.CurrentRoom = null; break;
        case "no game": GameManager.instance = null; break;
    }
    LateJoinSession.OpenRoom("guard");
    Check(PhotonNetwork.OpenCalls == 0 && !SteamLobbyManager.instance.currentLobby.Joinable &&
        SteamLobbyManager.instance.Publishes == 0, "No admission changes: " + guard);
}
Reset();
SteamLobbyManager.instance.currentLobby.Owner = new(999);
LateJoinSession.OpenRoom("not Steam owner");
Check(!SteamLobbyManager.instance.currentLobby.Joinable && SteamLobbyManager.instance.Publishes == 0,
    "Photon host cannot rewrite a Steam lobby it does not own");
Reset();
GameDirector.instance.currentState = GameDirector.gameState.Start;
LateJoinSession.OpenRoom("generation complete");
Check(PhotonNetwork.CurrentRoom.IsOpen, "Generation completion opens without waiting for the next safety tick");

Reset();
SteamLobbyManager.instance.currentLobby.Owner = new(999);
IEnumerator native = NativeConnect();
var pending = LateJoinSession.WaitForLobbyData(native);
Check(pending.MoveNext() && pending.Current == null && SteamLobbyManager.instance.currentLobby.Refreshes == 1,
    "Guest waits and requests missing Steam metadata before native closed-lobby check");
Time.realtimeSinceStartup = .5f;
pending.MoveNext();
Check(SteamLobbyManager.instance.currentLobby.Refreshes == 1, "Refresh requests are throttled");
// Simulate Steam's data callback, not a host write by the guest.
SteamLobbyManager.instance.currentLobby.Data = new() { ["BuildName"] = "host-version", ["RoomName"] = "host-room", ["Region"] = "us" };
Check(pending.MoveNext() && ReferenceEquals(pending.Current, native), "Ready metadata resumes original connection and version/password validation");
Check(SteamLobbyManager.instance.Publishes == 0 && PhotonNetwork.OpenCalls == 0, "Guest never invents routing or opens a room");

Reset();
SteamLobbyManager.instance.currentLobby.Owner = new(999);
native = NativeConnect(); pending = LateJoinSession.WaitForLobbyData(native); pending.MoveNext();
Time.realtimeSinceStartup = 9;
Check(pending.MoveNext() && ReferenceEquals(pending.Current, native), "Truly closed lobby times out to native error instead of hanging");
Reset();
SteamLobbyManager.instance.currentLobby.Owner = new(999);
pending = LateJoinSession.WaitForLobbyData(NativeConnect()); pending.MoveNext();
SteamLobbyManager.instance.currentLobby = new() { Id = new(789) };
Check(!pending.MoveNext(), "Second invite cancels previous pending connection");
Reset();
SteamLobbyManager.instance.currentLobby.Owner = new(999);
pending = LateJoinSession.WaitForLobbyData(NativeConnect()); pending.MoveNext();
SteamLobbyManager.instance.currentLobby = new() { Id = new(0) };
Check(!pending.MoveNext(), "Leaving lobby cancels metadata wait");
foreach (string mode in new[] { "host", "disabled", "local", "public" })
{
    Reset();
    if (mode != "host") SteamLobbyManager.instance.currentLobby.Owner = new(999);
    if (mode == "disabled") ModConfig.LateJoinEnabled.Value = false;
    if (mode == "local") GameManager.instance.localTest = true;
    if (mode == "public") GameManager.instance.connectRandom = true;
    native = NativeConnect(); pending = LateJoinSession.WaitForLobbyData(native);
    Check(pending.MoveNext() && ReferenceEquals(pending.Current, native), "No metadata delay for " + mode);
}
// Max health raised after the host's upgrades arrive (120 -> 180).
Check(IncreaseHardMod.Patches.Effects.LateJoinRules.ReconciledHealth(120, 120, 180, 180) == 180, "Health clamped by a stale max is restored to the saved 180");
Check(IncreaseHardMod.Patches.Effects.LateJoinRules.ReconciledHealth(70, 120, 180, 180) == 70, "A wounded joiner is not healed by the correction");
Check(IncreaseHardMod.Patches.Effects.LateJoinRules.ReconciledHealth(120, 120, 120, 180) == 120, "Saved health is never invented above what the host saved");
Check(IncreaseHardMod.Patches.Effects.LateJoinRules.ReconciledHealth(120, 120, 500, 180) == 180, "Never above the new maximum");
object[] BatteryPacket() => new object[] { (byte)1, "level12", 100d, new[]{12,13}, new[]{8,6}, new[]{3,0}, new[]{43.25f,0f}, new[]{true,false} };
Check(LateJoinBatteryRules.Read(BatteryPacket(), 101d, "level12", out var batteries) && batteries.Length == 2 &&
      batteries[0].Bars == 8 && batteries[0].Segments == 3 && batteries[0].Charge == 43.25f && batteries[0].Charging &&
      batteries[1].Charge == 0f && batteries[1].Segments == 0, "Late join preserves upgraded capacity, fractional charge, charging state and a genuinely empty battery");
Check(!LateJoinBatteryRules.Read(BatteryPacket(), 101d, "another-level", out _), "Battery data from another level is ignored");
Check(!LateJoinBatteryRules.Read(BatteryPacket(), 111d, "level12", out _) &&
      !LateJoinBatteryRules.Read(BatteryPacket(), 99d, "level12", out _), "Stale and future battery snapshots are rejected");
var packet = BatteryPacket(); packet[2] = double.NaN;
Check(!LateJoinBatteryRules.Read(packet, 101d, "level12", out _), "NaN battery timestamps are rejected");
packet = BatteryPacket(); packet[6] = new[]{float.NaN,0f};
Check(!LateJoinBatteryRules.Read(packet, 101d, "level12", out _), "NaN charge is rejected before any battery is changed");
packet = BatteryPacket(); packet[3] = new[]{12,12};
Check(!LateJoinBatteryRules.Read(packet, 101d, "level12", out _), "A batch cannot target the same battery twice");
packet = BatteryPacket(); packet[4] = new[]{8};
Check(!LateJoinBatteryRules.Read(packet, 101d, "level12", out _), "Battery arrays must have matching lengths");
packet = BatteryPacket(); packet[4] = new[]{8,0};
Check(!LateJoinBatteryRules.Read(packet, 101d, "level12", out var rejected) && rejected == null, "A bad later row rejects the complete batch, without partial application");
packet = BatteryPacket(); packet[5] = new[]{9,0};
Check(!LateJoinBatteryRules.Read(packet, 101d, "level12", out _), "Lit segments cannot exceed capacity");
packet = BatteryPacket(); packet[6] = new[]{101f,0f};
Check(!LateJoinBatteryRules.Read(packet, 101d, "level12", out _), "Charge cannot exceed 100 percent");
packet = BatteryPacket(); packet[5] = new[]{3,1};
Check(!LateJoinBatteryRules.Read(packet, 101d, "level12", out _), "A zero-charge battery cannot claim a lit segment");
Check(!LateJoinBatteryRules.Read(new object[]{(byte)2}, 101d, "level12", out _), "A battery request cannot be mistaken for a host snapshot");
Console.WriteLine($"LATE_JOIN_PASS: {checks}");
