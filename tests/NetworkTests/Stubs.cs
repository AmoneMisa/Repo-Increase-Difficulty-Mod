using System.Collections.Generic;
namespace UnityEngine {
 public class Component {public GameObject gameObject=new();public T GetComponent<T>() where T:class=>gameObject.Get<T>();public T GetComponentInParent<T>() where T:class=>GetComponent<T>();public T GetComponentInChildren<T>(bool includeInactive=false) where T:class=>GetComponent<T>();}
 public class GameObject {readonly Dictionary<Type,object> all=new();public T Get<T>()where T:class=>all.TryGetValue(typeof(T),out var v)?v as T:null;public void Add(Component c){c.gameObject=this;all[c.GetType()]=c;}}
 public class MonoBehaviour:Component {protected void DontDestroyOnLoad(object o){}}
 public struct Vector3 {public float x,y,z;public Vector3(float a,float b,float c){x=a;y=b;z=c;}}
 public static class Time {public static float unscaledTime,deltaTime=.016f,fixedDeltaTime=.02f;}
 public static class Mathf {public static float Max(float a,float b)=>Math.Max(a,b);public static int Max(int a,int b)=>Math.Max(a,b);public static float Min(float a,float b)=>Math.Min(a,b);public static float Clamp(float v,float a,float b)=>Math.Clamp(v,a,b);}
}
namespace ExitGames.Client.Photon {public class EventData {public byte Code;public int Sender;public object CustomData;} public struct SendOptions {public static SendOptions SendReliable,SendUnreliable;}}
namespace Photon.Realtime {public interface IOnEventCallback {void OnEvent(ExitGames.Client.Photon.EventData e);}public enum ReceiverGroup {All,Others,MasterClient}public class RaiseEventOptions {public ReceiverGroup Receivers;public int[] TargetActors;}public class Player {public int ActorNumber;}public class Room {public string Name="room";public int PlayerCount=3;}}
namespace Photon.Pun {
 public class PhotonView:UnityEngine.Component {public int ViewID,OwnerActorNr;public Photon.Realtime.Player Owner;public static Dictionary<int,PhotonView> All=new();public static PhotonView Find(int id)=>All.TryGetValue(id,out var v)?v:null;}
 public static class PhotonNetwork {public static bool InRoom=true,IsMasterClient;public static double Time=10;public static Photon.Realtime.Player MasterClient=new(){ActorNumber=1},LocalPlayer=new(){ActorNumber=2};public static Photon.Realtime.Room CurrentRoom=new();public static void AddCallbackTarget(object o){}public static void RemoveCallbackTarget(object o){}public static List<(byte Code,Photon.Realtime.ReceiverGroup Receivers)> Sent=new();public static List<(byte Code,object Data,int[] Targets)> SentData=new();public static void RaiseEvent(byte code,object data,Photon.Realtime.RaiseEventOptions o,ExitGames.Client.Photon.SendOptions s){Sent.Add((code,o.Receivers));SentData.Add((code,data,o.TargetActors));}}
}
public class GameDirector {public static GameDirector instance=new();public List<PlayerAvatar> PlayerList=new();}
public class PlayerAvatar:UnityEngine.Component {public Photon.Pun.PhotonView photonView;public bool isLocal;public string steamID;}
public class Enemy:UnityEngine.Component {}
public class EnemyParent:UnityEngine.Component {}
public class ItemGun:UnityEngine.Component {}
public class PhysGrabObject:UnityEngine.Component {}
public class ItemBattery:UnityEngine.Component {public float batteryLife=100;}
public class ValuableObject:UnityEngine.Component {public float dollarValueCurrent=50,dollarValueOriginal=100;}
public class Level {public string name="level";}
public class RunManager {public static RunManager instance=new();public Level levelCurrent=new();public int levelsCompleted=1;}
public static class SemiFunc {public static bool IsMasterClientOrSingleplayer()=>Photon.Pun.PhotonNetwork.IsMasterClient;public static List<PlayerAvatar> Players=new();public static List<PlayerAvatar> PlayerGetList()=>Players;}
namespace IncreaseHardMod {
 public class Setting<T> {public T Value;public Setting(T v){Value=v;}}
 public static class ModConfig {public static Setting<string> EnemyHealthMaxPerEnemy=new("");public static Setting<bool> SurprisesEnabled=new(true),RoundResultsEnabled=new(true),QuestsEnabled=new(true);}
 public static class EnemyBalance {public static string Signature()=>"";public static float[] Capture()=>new float[8];public static bool Apply(float[] f,string c,bool s)=>true;}
}
namespace IncreaseHardMod.Modifiers {
 public class LevelModifiers {public object[] Serialize()=>new object[5];public static LevelModifiers Deserialize(object[] d)=>new();}
 public static class ModifierManager {public static int Revision;public static LevelModifiers Current=new();public static void ApplyReceived(LevelModifiers m,long revision){}}
}
namespace IncreaseHardMod.Patches.Effects {
 public static class MysteryBoxEffect {public const byte EventCode=199;public static int Received;public static void Tick(){}public static void HandleEvent(ExitGames.Client.Photon.EventData e){Received++;}}
public static class SkillBoxEffect {public const byte EventCode=175;public static int Received;public static void Tick(){}public static void HandleEvent(ExitGames.Client.Photon.EventData e){Received++;}}
public static class RetryCrystalEffect {public const byte EventCode=176;public static int Received;public static void Tick(){}public static void HandleEvent(ExitGames.Client.Photon.EventData e){Received++;}}
 public static class LevelTimerController {public static int Applied;public static float Last;public static void Tick(float dt){}public static void ApplyNetworkState(float t,bool on){Applied++;Last=t;}}
 public static class RoundStats {public static void Tick(float dt){}public static void ApplyResults(List<PlayerRoundStat> r,RoundLootSummary s){}}
 public class PlayerRoundStat {public string Name;public int BrokenValue,BrokenCount,Delivered,Deaths,Revives;}
 public class RoundLootSummary {public int[] Serialize()=>Array.Empty<int>();public static RoundLootSummary Deserialize(int[] d)=>new();}
 public static class LateJoinCache {public static void Tick(){}}
 public static class LateJoinController {public static void Tick(){}public static void EnsureEnemyRegistrations(){}public static void EnsureLocalHealthInitialized(){}}
 public static class PingController {public static int Applied;public static void AddNetworkPing(byte k,UnityEngine.Vector3 v,string l,string s,int id){Applied++;}}
 public static class MapVoteState {public static int Candidates,Ballots,LastActor;public static void ReceiveCandidates(int session,string[] names,float t,bool closed,int winner){Candidates++;}public static void ReceiveBallot(int session,int actor,int index,string name){Ballots++;LastActor=actor;}}
}
namespace IncreaseHardMod.Economy {
 public static class ShopEconomy {public static void Set(int id){}}
 public static class EconomyEvents {public static void Set(int id){}}
 public static class BlackMarket {public enum Kind {Modifier,Economy,ShopEconomy}public static void TryBuy(Kind kind,string id){}public static void SetFromNetwork(string[] keys){}}
}
namespace IncreaseHardMod.Drones {public static class LinkManager {public static void Tick(float dt){}}}
namespace IncreaseHardMod.Gear {
 public enum ShieldMode {DefenceDrone,ProtectionOrb,DefenceOrb}
 public class ShieldItem:UnityEngine.Component {public ShieldMode mode;public PlayerAvatar Target;public bool Covers(PlayerAvatar p)=>p==Target;}
 public static class GearEffects {public const float NetBroken = -1f, NetExpired = -2f;public static void Tick(float dt){}}
 public static class PlayerFreezeEffect {public static void Apply(PlayerAvatar p,float seconds){}public static bool ReadSnapshot(object data,double time,string key,out int id,out float remaining){id=0;remaining=0;return false;}}
 public static class PlayerNetEffect {public static void Apply(PlayerAvatar p,float seconds){}public static void End(PlayerAvatar p,bool torn){}}
}
namespace IncreaseHardMod.Visuals {
 public static class EnemyStatusVisual {public static void Apply(Enemy e,byte kind,float seconds){}}
 public static class StickyTether {public static void Apply(ItemGun gun,PhysGrabObject p,UnityEngine.Vector3 v,float t){}}
}

namespace IncreaseHardMod.Quests {
 public enum StatKind:byte {Health,Stamina,SprintSpeed,GrabStrength,GrabRange,ThrowStrength,ExtraJump,TumbleLaunch}
 public static class QuestManager {public static int States,Buffs;public static void Tick(float dt){}public static void ReceiveTeamState(object[] d){States++;}public static void ApplyTempBuff(StatKind stat,int amount){Buffs++;}public static int LevelEnds;public static bool LastCompleted;public static void OnLevelCompleted(bool c){LevelEnds++;LastCompleted=c;}}
 public static class QuestRewards {public static int Money;public static void GrantMoney(int amount){Money+=amount;}public static List<int> Actors=new();public static bool GrantPersonalMoney(int actor,int amount){Actors.Add(actor);Money+=amount;return true;}}
 public static class QuestRankProgress {public static void PublishSave(){}}
}
namespace IncreaseHardMod.Gear { public static class Shock { public const byte EnemyKind=3; public static float Last; public static void ShockLocal(float s){Last=s;} } }
namespace IncreaseHardMod.Challenges { public static class ChallengeManager { public const byte EventCode=168, StartRequestCode=166; public static int Received, Requests; public static void HandleEvent(ExitGames.Client.Photon.EventData e){Received++;} public static void HandleStartRequest(ExitGames.Client.Photon.EventData e){Requests++;} } }
namespace IncreaseHardMod.Skills { public static class BuffPerks { public const byte EventCode=167; public static int Received; public static void HandleEvent(ExitGames.Client.Photon.EventData e){Received++;} } }
namespace IncreaseHardMod.Skills { public static class TradeOffs { public static int Echo; public static void HostPingedByActor(int actor, UnityEngine.Vector3 p){Echo++;} } }
namespace IncreaseHardMod.Valuables { public static class LootSafetyNet { public static int Shown; public static (int Count,int Value,bool Marked) Last; public static void ShowNotice(int c,int v,bool m){Shown++;Last=(c,v,m);} } }
namespace IncreaseHardMod.Patches.Effects { public static class LateJoinNetworkDiagnostics { public static void Tick(){} } }
namespace IncreaseHardMod.Patches.Effects { public static class LevelInfoHud { public const byte StateEventCode=197; public static int Calls; public static void HandleStateEvent(ExitGames.Client.Photon.EventData e){Calls++;} } }

namespace IncreaseHardMod.Hud{public enum PlayerCondition{Frozen,Poisoned,Sedated,Netted,Shocked}public static class HudConditions{public static int Started;public static PlayerCondition Last;public static float Seconds;public static void Start(PlayerCondition c,float s){Started++;Last=c;Seconds=s;}}}

namespace IncreaseHardMod.Skills{public enum BuyBlock:byte{None=0,UnknownNode,Maxed,MissingPrerequisite,NotEnoughPoints,BadCount,WrongPlace,NotReady,NoMoney,NothingSpent}public static class SkillStore{public const byte OpBuy=0,OpRespec=1,OpQuestPoints=2;public static Dictionary<int,string> Actors=new();public static string SteamIdOfActor(int a)=>Actors.TryGetValue(a,out var s)?s:null;public static List<(string Steam,byte Op,string Node,int Count)> Handled=new();public static BuyBlock Answer=BuyBlock.None;public static BuyBlock HostHandle(string steam,byte op,string node,int count){Handled.Add((steam,op,node,count));return Answer;}public static List<(byte Op,BuyBlock Block,string Node)> Reports=new();public static void Report(byte op,BuyBlock b,string node){Reports.Add((op,b,node));}}}
namespace IncreaseHardMod.Upgrades { public static class RespawnList { public const byte EventCode=165; public static int Received; public static void HandleEvent(ExitGames.Client.Photon.EventData e){Received++;} } }
