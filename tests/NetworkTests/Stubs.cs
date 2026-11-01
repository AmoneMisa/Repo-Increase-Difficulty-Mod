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
namespace Photon.Realtime {public interface IOnEventCallback {void OnEvent(ExitGames.Client.Photon.EventData e);}public enum ReceiverGroup {All,Others,MasterClient}public class RaiseEventOptions {public ReceiverGroup Receivers;}public class Player {public int ActorNumber;}public class Room {public string Name="room";public int PlayerCount=3;}}
namespace Photon.Pun {
 public class PhotonView:UnityEngine.Component {public int ViewID,OwnerActorNr;public static Dictionary<int,PhotonView> All=new();public static PhotonView Find(int id)=>All.TryGetValue(id,out var v)?v:null;}
 public static class PhotonNetwork {public static bool InRoom=true,IsMasterClient;public static double Time=10;public static Photon.Realtime.Player MasterClient=new(){ActorNumber=1},LocalPlayer=new(){ActorNumber=2};public static Photon.Realtime.Room CurrentRoom=new();public static void AddCallbackTarget(object o){}public static void RemoveCallbackTarget(object o){}public static List<(byte Code,Photon.Realtime.ReceiverGroup Receivers)> Sent=new();public static void RaiseEvent(byte code,object data,Photon.Realtime.RaiseEventOptions o,ExitGames.Client.Photon.SendOptions s){Sent.Add((code,o.Receivers));}}
}
public class GameDirector {public static GameDirector instance=new();public List<PlayerAvatar> PlayerList=new();}
public class PlayerAvatar:UnityEngine.Component {public Photon.Pun.PhotonView photonView;}
public class Enemy:UnityEngine.Component {}
public class EnemyParent:UnityEngine.Component {}
public class ItemGun:UnityEngine.Component {}
public class PhysGrabObject:UnityEngine.Component {}
public class ItemBattery:UnityEngine.Component {public float batteryLife=100;}
public class ValuableObject:UnityEngine.Component {public float dollarValueCurrent=50,dollarValueOriginal=100;}
public class Level {public string name="level";}
public class RunManager {public static RunManager instance=new();public Level levelCurrent=new();public int levelsCompleted=1;}
public static class SemiFunc {public static bool IsMasterClientOrSingleplayer()=>Photon.Pun.PhotonNetwork.IsMasterClient;}
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
 public static class LevelTimerController {public static int Applied;public static float Last;public static void Tick(float dt){}public static void ApplyNetworkState(float t,bool on){Applied++;Last=t;}}
 public static class RoundStats {public static void Tick(float dt){}public static void ApplyResults(List<PlayerRoundStat> r,RoundLootSummary s){}}
 public class PlayerRoundStat {public string Name;public int BrokenValue,BrokenCount,Delivered,Deaths,Revives;}
 public class RoundLootSummary {public int[] Serialize()=>Array.Empty<int>();public static RoundLootSummary Deserialize(int[] d)=>new();}
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
 public static class GearEffects {public static void Tick(float dt){}}
 public static class PlayerFreezeEffect {public static void Apply(PlayerAvatar p,float seconds){}public static bool ReadSnapshot(object data,double time,string key,out int id,out float remaining){id=0;remaining=0;return false;}}
}
namespace IncreaseHardMod.Visuals {
 public static class EnemyStatusVisual {public static void Apply(Enemy e,byte kind,float seconds){}}
 public static class StickyTether {public static void Apply(ItemGun gun,PhysGrabObject p,UnityEngine.Vector3 v,float t){}}
}

namespace IncreaseHardMod.Quests {
 public enum StatKind:byte {Health,Stamina,SprintSpeed,GrabStrength,GrabRange,ThrowStrength,ExtraJump,TumbleLaunch}
 public static class QuestManager {public static int States,Buffs;public static void Tick(float dt){}public static void ReceiveTeamState(object[] d){States++;}public static void ApplyTempBuff(StatKind stat,int amount){Buffs++;}}
 public static class QuestRewards {public static int Money;public static void GrantMoney(int amount){Money+=amount;}}
}
