using IncreaseHardMod.Valuables;
int checks=0;
void Assert(bool value,string message){checks++;if(!value)throw new Exception(message);}
var expected=new Dictionary<string,(LootMap maps,float min,float max)>{
    ["crystal_skull"]=(LootMap.Sunstone|LootMap.Swiftbroom|LootMap.McJannek,2500,4000),
    ["bomb"]=(LootMap.McJannek,28000,35000),
    ["bath_gift_set"]=(LootMap.Sunstone|LootMap.Museum|LootMap.Manor,2000,3000),
    ["cannon"]=(LootMap.Manor|LootMap.Sunstone|LootMap.Museum,15000,15000),
    ["water_timer"]=(LootMap.McJannek|LootMap.Swiftbroom|LootMap.Manor|LootMap.Museum,2200,3200),
    ["centrifuge"]=(LootMap.McJannek,7000,9500),
    ["skeleton"]=(LootMap.McJannek|LootMap.Sunstone|LootMap.Museum,12000,15000),
    ["robot_vacuum"]=(LootMap.Manor|LootMap.Museum,6000,8000),
    ["graphics_card"]=(LootMap.Museum|LootMap.McJannek,4500,5500),
    ["ram_pair"]=(LootMap.Museum|LootMap.McJannek,4500,5500),
    ["monoblock_pc"]=(LootMap.McJannek|LootMap.Museum|LootMap.Manor|LootMap.Sunstone,6000,8000),
    ["gold_ingot"]=(LootMap.Sunstone|LootMap.McJannek,3500,4800),
    ["maneki_neko"]=(LootMap.Museum|LootMap.Manor,12000,16000)
};
Assert(CollectionCatalog.Entries.Count==13,"All thirteen valuables, excluding the shop-only Spritz");
Assert(CollectionCatalog.Entries.Select(e=>e.Key).Distinct().Count()==13,"No duplicate valuables");
foreach(var entry in CollectionCatalog.Entries)
{
    var row=expected[entry.Key];Assert(entry.Min==row.min&&entry.Max==row.max,entry.Key+" requested price");
    foreach(var map in new[]{LootMap.Sunstone,LootMap.Swiftbroom,LootMap.McJannek,LootMap.Manor,LootMap.Museum})
        Assert(CollectionCatalog.Allows(entry,map)==((row.maps&map)!=0),entry.Key+" map exclusion/inclusion "+map);
    Assert(!CollectionCatalog.Allows(entry,LootMap.None),entry.Key+" unknown maps excluded");
    Assert(entry.Mass>0,entry.Key+" valid mass");
}
foreach(var pair in new[]{("Level - Desert",LootMap.Sunstone),("Level - Wizard",LootMap.Swiftbroom),("Level - Arctic",LootMap.McJannek),("Level - Manor",LootMap.Manor),("Level - Museum",LootMap.Museum)})
    Assert(CollectionCatalog.Identify(pair.Item1,"")==pair.Item2,"Installed native Level identity "+pair.Item1);
foreach(var name in new[]{"Level - Shop Suburb","Level - Arena Fight","Level - Tutorial","Custom Museum","Level - Lobby"})
    Assert(CollectionCatalog.Identify(name,"Sunstone Valley Resort")==LootMap.None,"No shop/arena/custom-map leakage "+name);
CollectionEntry Get(string key)=>CollectionCatalog.Entries.Single(e=>e.Key==key);
Assert(Get("gold_ingot").Mass==3,"Small ingot is 3 kg");
Assert(Get("maneki_neko").Mass==6&&Get("maneki_neko").Fragility==LootFragility.Fragile,"Porcelain neko is 6 kg and fragile");
foreach(var key in new[]{"crystal_skull","ram_pair","graphics_card"})Assert(Get(key).Fragility==LootFragility.VeryFragile,key+" very fragile");
Assert(Get("water_timer").Fragility==LootFragility.Fragile,"Fragile liquid watch");
Assert(Get("bomb").Mass==9&&Get("cannon").Mass==8,"Requested 9 kg bomb and 8 kg cannon");
Assert(Math.Abs(CollectionCatalog.TotalMass(Get("skeleton"))-3)<.0001f,"Skeleton is 3 kg including every joint body");
foreach(var entry in CollectionCatalog.Entries.Where(e=>e.Key!="bomb"&&e.Key!="cannon"&&e.Key!="maneki_neko"))
 Assert(CollectionCatalog.TotalMass(entry)<=6,"Other custom loot has no remaining mass above 6 kg: "+entry.Key);
Assert(Get("crystal_skull").Mass<Get("robot_vacuum").Mass&&Get("monoblock_pc").Mass<Get("robot_vacuum").Mass,"Light skull and PC");
foreach(var key in new[]{"robot_vacuum","centrifuge","skeleton"})Assert(Get(key).Fragility==LootFragility.Sturdy,key+" sturdier than normal");
foreach(var key in new[]{"cannon","bomb"})Assert(Get(key).Fragility==LootFragility.Reinforced,key+" reinforced beyond sturdy");
var normal=CollectionCatalog.DurabilityFor(LootFragility.Normal);var sturdy=CollectionCatalog.DurabilityFor(LootFragility.Sturdy);var reinforced=CollectionCatalog.DurabilityFor(LootFragility.Reinforced);
Assert(sturdy.Fragility<normal.Fragility&&sturdy.Durability>normal.Durability,"Sturdy reduces native impact sensitivity AND value loss");
Assert(reinforced.Fragility<sturdy.Fragility&&reinforced.Durability>sturdy.Durability,"Reinforced improves both native axes further");
var kit=new TubeLossState();Assert(kit.TakePayment(true,true,9000)==0,"No fee before losing samples");
Assert(kit.Commit(2500),"First loss event commits");Assert(!kit.Commit(3000)&&kit.Cost==2500,"Repeated inversion/damage/pull cannot reroll or recharge");
Assert(kit.TakePayment(false,true,9000)==0&&!kit.Applied,"Clients cannot debit value");
Assert(kit.TakePayment(true,false,100)==0&&!kit.Applied,"Wait for native initial value assignment");
Assert(kit.TakePayment(true,true,float.NaN)==0&&!kit.Applied,"Invalid value cannot poison the transaction");
Assert(kit.TakePayment(true,true,9000)==2500&&kit.Applied,"Host debits the committed 2–3k kit cost");
Assert(kit.TakePayment(true,true,6500)==0,"Nested native break callbacks cannot charge twice");
var lateJoin=new TubeLossState();Assert(lateJoin.Import(kit.Cost,kit.Applied),"Late join imports paid kit state");
Assert(lateJoin.TakePayment(true,true,6500)==0,"New host cannot repeat a paid debit");
Assert(lateJoin.Import(2500,false)&&lateJoin.Applied,"Older unpaid state cannot undo payment");
Assert(!lateJoin.Import(0,false)&&!lateJoin.Import(2700,true),"Committed loss cannot be cleared or changed");
var pending=new TubeLossState();Assert(pending.Import(2300,false),"Authority transfer can import a pending debit");
Assert(pending.TakePayment(true,true,7000)==2300&&pending.TakePayment(true,true,4700)==0,"New host finishes a pending debit exactly once");
foreach(var amount in new[]{-1,1999,2000,3000,3001,10000})
{
    var bounded=new TubeLossState();bounded.Commit(amount);Assert(bounded.Cost>=2000&&bounded.Cost<=3000,"Kit cost bounds "+amount);
}
var depleted=new TubeLossState();depleted.Commit(3000);Assert(depleted.TakePayment(true,true,1200)==1200,"Cannot subtract more than the remaining item value");
var empty=new TubeLossState();empty.Commit(2000);Assert(empty.TakePayment(true,true,-1)==0&&empty.Applied,"Never creates negative value");
Assert(!new TubeLossState().Import(1500,true)&&!new TubeLossState().Import(3500,false)&&!new TubeLossState().Import(0,true),"Malformed remote fee states rejected");
float liquid=1;
for(int i=0;i<320;i++)liquid=LiquidFlow.Advance(liquid,1,.1f,32);
Assert(liquid<.001f,"Upright watch drains in 32 seconds");
for(int i=0;i<320;i++)liquid=LiquidFlow.Advance(liquid,-1,.1f,32);
Assert(liquid>.999f,"Physical inversion reverses liquid transfer");
Assert(LiquidFlow.Advance(.4f,0,10,32)==.4f,"Sideways watch pauses transfer");
Assert(!LiquidFlow.Flowing(0,1)&&!LiquidFlow.Flowing(1,-1),"Empty source stops droplets");
Assert(LiquidFlow.Advance(.4f,1,-10,32)==.4f,"Negative clock delta cannot reverse flow");
foreach(bool enabled in new[]{false,true})foreach(bool generated in new[]{false,true})foreach(bool spawned in new[]{false,true})
 Assert(IncreaseHardMod.Patches.Effects.LateJoinRules.ShouldSpawn(enabled,generated,spawned)==!(enabled&&generated&&spawned),"Late join only preserves already spawned players during an active run");
Console.WriteLine($"PASS: {checks} collection, sample loss, liquid orientation and late join policy checks.");
