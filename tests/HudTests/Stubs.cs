using System;
using System.Collections.Generic;
namespace UnityEngine {
 public class Object { public static void Destroy(Object o) { if(o is GameObject g)g.activeInHierarchy=false; } }
 public class Component:Object { public GameObject gameObject; public Transform transform=>gameObject.transform; public T GetComponent<T>() where T:class=>gameObject.GetComponent<T>(); }
 public class MonoBehaviour:Component { }
 public class GameObject:Object {public string name;public int layer;public bool activeInHierarchy=true;public Transform transform;Dictionary<Type,object> parts=new();public GameObject(string n=""){name=n;transform=new RectTransform{gameObject=this};}public T AddComponent<T>() where T:Component,new(){var t=new T{gameObject=this};parts[typeof(T)]=t;return t;}public T GetComponent<T>() where T:class=>parts.TryGetValue(typeof(T),out var o)?o as T:null;}
 public class Transform:Component {public Transform parent;public void SetParent(Transform p,bool world){parent=p;}public Vector3 TransformPoint(Vector3 p)=>p;public Vector3 lossyScale=new(1,1,1);public Vector3 InverseTransformPoint(Vector3 p)=>p;}
 public class RectTransform:Transform {public Vector2 anchorMin,anchorMax,pivot,sizeDelta=new(300,80),anchoredPosition;public Rect rect=>new(-sizeDelta.x/2,-sizeDelta.y/2,sizeDelta.x,sizeDelta.y);public void GetWorldCorners(Vector3[] a){a[0]=new(-200,0);a[1]=new(-200,50);a[2]=new(200,50);a[3]=new(200,0);}}
 public struct Vector2 {public float x,y;public Vector2(float a,float b){x=a;y=b;}public static Vector2 zero=>new(0,0);public static Vector2 Min(Vector2 a,Vector2 b)=>new(Math.Min(a.x,b.x),Math.Min(a.y,b.y));public static Vector2 Max(Vector2 a,Vector2 b)=>new(Math.Max(a.x,b.x),Math.Max(a.y,b.y));public static Vector2 operator+(Vector2 a,Vector2 b)=>new(a.x+b.x,a.y+b.y);public static implicit operator Vector2(Vector3 a)=>new(a.x,a.y);}
 public struct Vector3 {public float x,y,z;public Vector3(float a,float b,float c=0){x=a;y=b;z=c;}public static implicit operator Vector3(Vector2 a)=>new(a.x,a.y);}
 public struct Rect {public float xMin,yMin,width,height;public float x=>xMin;public float y=>yMin;public float xMax=>xMin+width;public float yMax=>yMin+height;public Rect(float x,float y,float w,float h){xMin=x;yMin=y;width=w;height=h;}public static Rect MinMaxRect(float x,float y,float a,float b)=>new(x,y,a-x,b-y);}
 public struct Bounds {public Vector3 min,max;}
 public struct Color {public Color(float r,float g,float b,float a=1){}public static Color white=>new(1,1,1);}
 public static class Mathf {public const float Infinity=float.PositiveInfinity;public static float Max(float a,float b)=>Math.Max(a,b);public static int Max(int a,int b)=>Math.Max(a,b);public static float Min(float a,float b)=>Math.Min(a,b);public static int Clamp(int a,int b,int c)=>Math.Clamp(a,b,c);public static float Clamp(float a,float b,float c)=>Math.Clamp(a,b,c);}
 public enum KeyCode {Comma,Period,Home,End,LeftArrow,RightArrow,F8}
 public static class Time {public static float unscaledTime;}
 public class Canvas:Component {}
 public class Camera:Component {}
 public static class Input {public static KeyCode? Pressed;public static bool GetKeyDown(KeyCode k)=>Pressed==k;}
}
namespace TMPro {
 using UnityEngine;
 public enum TextAlignmentOptions {Top,TopRight}
 [Flags] public enum FontStyles {Normal=0,UpperCase=1}
 public enum TextOverflowModes {Ellipsis,Page}
 public class TextInfo {public int pageCount=1;}
 public class TextMeshProUGUI:Component {public object font,fontSharedMaterial;public float fontSize=30,fontSizeMax=30,fontSizeMin=30,characterSpacing,lineSpacing;public FontStyles fontStyle;public Color color;public TextAlignmentOptions alignment;public bool enableWordWrapping,raycastTarget,richText,enableAutoSizing,enabled;public bool isActiveAndEnabled=>enabled&&gameObject.activeInHierarchy;public string text="";public TextOverflowModes overflowMode;public int pageToDisplay=1;public TextInfo textInfo=new();public UnityEngine.Canvas canvas;public RectTransform rectTransform=>(RectTransform)transform;public Bounds textBounds=new(){min=new(0,100),max=new(200,140)};public void ForceMeshUpdate(){}public Vector2 GetPreferredValues(string t,float w,float h)=>new(w,(t.Split('\n').Length)*fontSize);}
}
namespace ExitGames.Client.Photon {public class EventData{public byte Code;public int Sender;public object CustomData;}public struct SendOptions{public static SendOptions SendReliable=>new();}}
namespace Photon.Realtime {public enum ReceiverGroup{MasterClient}public class RaiseEventOptions{public int[] TargetActors;public ReceiverGroup Receivers;}public class Player{public int ActorNumber=1;}public class Room{public string Name="room";public Player GetPlayer(int actor)=>new(){ActorNumber=actor};}}
namespace Photon.Pun {using Photon.Realtime; public static class PhotonNetwork{public static bool InRoom,IsMasterClient;public static Player MasterClient=new();public static Room CurrentRoom=new();public static List<object[]> Sent=new();public static bool RaiseEvent(byte c,object d,RaiseEventOptions o,ExitGames.Client.Photon.SendOptions s){Sent.Add((object[])d);return true;}}}
public class RunManager {public static RunManager instance;public Level levelCurrent=new();public bool restarting;public int levelsCompleted=1;}
public class Level{public string name="level";}
public class LevelGenerator {public static LevelGenerator Instance;public bool Generated;public Level Level=RunManager.instance.levelCurrent;}
public class GameDirector {public static GameDirector instance;public enum gameState{Load,Start,Main,Outro,Result,Death}public gameState currentState;public int Seed;}
public class LoadingUI:UnityEngine.MonoBehaviour {public static LoadingUI instance;}
public class HUDCanvas:UnityEngine.MonoBehaviour {public static HUDCanvas instance;public UnityEngine.RectTransform rect=>(UnityEngine.RectTransform)transform;}
public class Map {public static Map Instance;public bool Active;}
public class MapToolController {public static MapToolController instance;public bool Active;}
public class LevelUI{public static LevelUI instance;public TMPro.TextMeshProUGUI uiText,textNumber;}
public class GoalUI{public static GoalUI instance;public TMPro.TextMeshProUGUI uiText;public bool isHidden;}
public class StatsUI{public static StatsUI instance;public TMPro.TextMeshProUGUI Text,upgradesHeader;}
public static class SemiFunc{public static bool Menu,IsLevel=true,Shop,TextInput;public static bool MenuLevel()=>Menu;public static bool RunIsLevel()=>IsLevel;public static bool RunIsShop()=>Shop;public static bool NoTextInputsActive()=>!TextInput;public static bool IsMasterClientOrSingleplayer()=>!Photon.Pun.PhotonNetwork.InRoom||Photon.Pun.PhotonNetwork.IsMasterClient;}
namespace IncreaseHardMod {
 public class LogStub{public void LogInfo(object o){}public void LogWarning(object o){}}
 public static class Plugin{public static LogStub Log=new();}
 public class Config<T>{public T Value;public Config(T t){Value=t;}}
 public static class ModConfig {public static Config<bool> LevelInfoEnabled=new(true),BlackMarketEnabled=new(false);public static Config<UnityEngine.KeyCode> LevelInfoToggleKey=new(UnityEngine.KeyCode.F8),BlackMarketKey=new(UnityEngine.KeyCode.Home);public static string ModifierDescription(Modifiers.ModifierType t)=>t.ToString();}
}
namespace IncreaseHardMod.Modifiers {
 public static class ModifierManager{public static LevelModifiers Current=LevelModifiers.Empty;public static int Revision;}
 public class StatModifier{public string Name,Description;public IncreaseHardMod.Economy.EconomyTier Tier;}
 public static class StatModifiers{public static StatModifier Get(int i)=>null;}
}
namespace IncreaseHardMod.Economy {
 public enum EconomyTier{Good,VeryGood,Bad,VeryBad,Mixed}
 public class EconomyEvent{public int Id;public string Name="POOR FACILITY",Description="Extractions pay 30% less.";public EconomyTier Tier;}
 public static class EconomyEvents{public static List<EconomyEvent> All=new(){new(){Id=1}};public static EconomyEvent Current;}
 public static class ShopEconomy{public static List<EconomyEvent> All=new(){new(){Id=1}};public static EconomyEvent Current;}
 public static class BlackMarket{public static EconomyTier TierOfModifier(IncreaseHardMod.Modifiers.ModifierType t)=>EconomyTier.Mixed;}
}
namespace IncreaseHardMod.Upgrades{public static class MapUpgrade{public static string HudInfo="";}}
namespace IncreaseHardMod.Patches.Effects{public static class SpectatorMapHud{public static bool Showing;}public static class LevelTimerController{public static bool Running;public static float Remaining;}public static class HudProjection{public static UnityEngine.Camera CanvasCamera(UnityEngine.Canvas c)=>null;public static UnityEngine.Rect DisplayRect(UnityEngine.Camera c)=>new(0,0,1920,1080);}}
public class HaulUI{public static HaulUI instance;public TMPro.TextMeshProUGUI uiText;public bool isHidden;}

namespace IncreaseHardMod.Hud{internal static class HudStack{public static float Edge=-1f;public static float LowestScreenEdge(HudCorner c)=>c==HudCorner.TopRight?Edge:-1f;}}
public class MenuManager{public static MenuManager instance;public object currentMenuPage;}
