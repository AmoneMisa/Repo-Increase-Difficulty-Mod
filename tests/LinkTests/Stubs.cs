using IncreaseHardMod.Drones;
namespace HarmonyLib {public class HarmonyPatch:Attribute{public HarmonyPatch(Type t,string name){}}public class HarmonyPostfix:Attribute{}public class HarmonyPrefix:Attribute{}}
public static class SemiFunc {public static bool Host=true;public static bool IsMasterClientOrSingleplayer()=>Host;}
public class Item {public string name; public bool physicalItem=true;}
public class ItemAttributes {public string instanceName, cachedItemName, itemName;public Item item;}
public class StatsManager {
 public static StatsManager instance=new();public Dictionary<string,int> item=new(),runStats=new(),itemsPurchased=new();public Dictionary<string,Item> itemDictionary=new();public int PartnerPurchases;public bool Shop;
 public void ItemPurchase(string name){var before=LinkSets.BeforePurchase(this,name);itemsPurchased[name]=itemsPurchased.GetValueOrDefault(name)+1;if(itemDictionary[name].physicalItem)ItemAdd(name);if(name==DroneItems.LinkBName)PartnerPurchases++;LinkSets.AfterPurchase(this,name,before);}
 public void ItemAdd(string name){var before=new HashSet<string>(item.Keys);if(!Shop){int id=1;while(item.ContainsKey(name+"/"+id))id++;item[name+"/"+id]=1;}typeof(LinkSets).GetNestedType("Delivery",System.Reflection.BindingFlags.NonPublic).GetMethod("Postfix",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static).Invoke(null,new object[]{this,before});}

}
public class PunManager {public static PunManager instance=new();public int Replications;public void UpdateStat(string dictionary,string key,int value){Replications++;StatsManager.instance.runStats[key]=value;}}
namespace IncreaseHardMod.Drones {internal static class DroneItems {internal const string LinkAName="Item Drone Link A",LinkBName="Item Drone Link B";}}
namespace UnityEngine {public static class Time {public static float unscaledTime;}}

// LinkSets names the set in the game language; tests run in English.
namespace IncreaseHardMod.Localization {internal static class GameLanguage {internal static bool Russian => false;}}
