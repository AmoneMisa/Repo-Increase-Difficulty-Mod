using System.Reflection;
using IncreaseHardMod.Economy;
namespace HarmonyLib {public class HarmonyPatch:Attribute{public HarmonyPatch(Type t,string name){}}public class HarmonyPrefix:Attribute{}public class HarmonyFinalizer:Attribute{}public class HarmonyPostfix:Attribute{}}
public class LevelGenerator{}
public class ExtractionPoint{}
public class RunManager {public static RunManager instance=new();public object levelCurrent=new();}
public class CurrencyUI {public static CurrencyUI instance=new();public int Fetches;public void FetchCurrency(){Fetches++;}}
public static class SemiFunc {
 public static bool Host=true,Level=true,ThrowOnSet;public static int Currency,Sets;
 public static bool IsMasterClientOrSingleplayer()=>Host;public static bool RunIsLevel()=>Level;
 public static int StatGetRunCurrency()=>Currency;
 public static int StatSetRunCurrency(int value){if(ThrowOnSet)throw new InvalidOperationException("controlled setter failure");var pay=typeof(CurrencyPayout).GetNestedType("Pay",BindingFlags.NonPublic);var args=new object[]{value,-1};pay.GetMethod("Prefix",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,args);Sets++;Currency=(int)args[0];pay.GetMethod("Postfix",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{Currency,args[1]});return Currency;}
}
namespace IncreaseHardMod {public static class Plugin {public class Logger{public void LogInfo(object message){}}public static Logger Log=new();}}
namespace IncreaseHardMod.Economy {internal static class EconomyEvents{internal static float PayMult=1;}}
