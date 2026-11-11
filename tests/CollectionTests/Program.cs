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
    ["maneki_neko"]=(LootMap.Museum|LootMap.Manor,12000,16000),
    ["plush_corgi"]=(LootMap.Manor|LootMap.Museum|LootMap.Sunstone|LootMap.McJannek,3000,4000),
    ["whiskey_bottle"]=(LootMap.Swiftbroom|LootMap.Manor|LootMap.McJannek|LootMap.Museum,5000,6500),
    ["rgb_mouse"]=(LootMap.McJannek,500,700),
    ["rgb_keyboard"]=(LootMap.McJannek,900,1300),
};
Assert(CollectionCatalog.Entries.Count==expected.Count,"All ambient collection valuables, excluding ritual-only objects");
Assert(CollectionCatalog.Entries.Select(e=>e.Key).Distinct().Count()==expected.Count,"No duplicate valuables");
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
// Loot safety net rules
Assert(LootSafetyRules.Shortfall(10000,4000)==6000&&LootSafetyRules.Shortfall(10000,12000)==0&&LootSafetyRules.Shortfall(5000,-50)==5000,"Shortfall is the goal minus reachable loot, never negative");
Assert(LootSafetyRules.Reachable(false,false,false)&&!LootSafetyRules.Reachable(true,false,false)&&!LootSafetyRules.Reachable(false,true,false),"Loot outside rooms or held by a drone does not count");
Assert(LootSafetyRules.Reachable(true,true,true),"Fresh spawns count before their room check runs");
Assert(LootSafetyRules.Decide(10000,4000,out int t1)==LootTopUpReason.Shortfall&&t1==6000,"A shortfall tops up the missing value");
Assert(LootSafetyRules.Decide(10000,20000,out int t2)==LootTopUpReason.None&&t2==0,"Enough collectable loot left: never a top-up, however long it takes");
Assert(LootSafetyRules.Decide(10000,10000,out int t3)==LootTopUpReason.None&&t3==0,"Loot exactly enough for the point: no top-up");
Assert(LootSafetyRules.KeepSpawning(1,24,25)&&!LootSafetyRules.KeepSpawning(1,25,25)&&!LootSafetyRules.KeepSpawning(0,0,25),"Spawning stops at the target or the cap");
Assert(LootSafetyRules.Money(900)=="$900"&&LootSafetyRules.Money(12000)=="$12K"&&LootSafetyRules.Money(12550)=="$12.5K"&&LootSafetyRules.Money(-5)=="$0","Money text matches the game's style");
Assert(LootSafetyRules.Notice(1,4000,false)=="New loot appeared: 1 valuable worth $4K"&&LootSafetyRules.Notice(3,12500,true)=="New loot appeared: 3 valuables worth $12.5K, marked on the map","Notice text");
// Challenge shrine rules
var kinds=Enumerable.Range(0,1000).Select(i=>IncreaseHardMod.Challenges.ChallengeRules.Kind(i/1000.0)).Distinct().Count();
Assert(kinds==11&&IncreaseHardMod.Challenges.ChallengeRules.Kind(1.0)==IncreaseHardMod.Challenges.ChallengeKind.Flag,"Every challenge kind can roll");
{
var CR=typeof(IncreaseHardMod.Challenges.ChallengeRules);
var qs=Enumerable.Range(0,600).Select(i=>IncreaseHardMod.Challenges.ChallengeRules.MathQuestion(i%3/3.0+.01,(i*37%100)/100.0,(i*61%100)/100.0)).ToList();
Assert(qs.All(q=>q.Op==2?q.A is >=12 and <=98&&q.B is >=3 and <=9:q.A is >=100 and <=999&&q.B is >=100 and <=999&&(q.Op==0||q.A>=q.B))&&qs.Select(q=>q.Op).Distinct().Count()==3,"Flag questions: 3-digit sums and differences (never negative), 2-digit times 1-digit");
Assert(IncreaseHardMod.Challenges.ChallengeRules.MathAnswer(387,244,0)==631&&IncreaseHardMod.Challenges.ChallengeRules.MathAnswer(387,244,1)==143&&IncreaseHardMod.Challenges.ChallengeRules.MathAnswer(14,6,2)==84&&IncreaseHardMod.Challenges.ChallengeRules.MathText(387,244,0)=="387 + 244","Flag answers and question text");
Assert(IncreaseHardMod.Challenges.ChallengeRules.ParseAnswer("631")==631&&IncreaseHardMod.Challenges.ChallengeRules.ParseAnswer(" it's 631!")==631&&IncreaseHardMod.Challenges.ChallengeRules.ParseAnswer("hello")==null&&IncreaseHardMod.Challenges.ChallengeRules.ParseAnswer("-5")==-5,"Chat answers: first whole number in the line");
Assert(IncreaseHardMod.Challenges.ChallengeRules.MushroomLength(1)==3&&IncreaseHardMod.Challenges.ChallengeRules.MushroomLength(3)==5,"Mushroom Path rounds are 3, 4, 5 long");
Assert(IncreaseHardMod.Challenges.ChallengeRules.MushroomPress(0, 2, 1, true)==-1,"Playback ignores landings");
Assert(IncreaseHardMod.Challenges.ChallengeRules.MushroomPress(2, 2, 1, false)==-1,"Standing on a mushroom does not repeat its press");
Assert(IncreaseHardMod.Challenges.ChallengeRules.MushroomPress(0, 5, 2, false)==2,"Correct pad wins simultaneous co-op landings");
Assert(IncreaseHardMod.Challenges.ChallengeRules.MushroomPress(0, 4, 1, false)==2,"A lone incorrect landing is still a mistake");
Assert(IncreaseHardMod.Challenges.ChallengeRules.Status(IncreaseHardMod.Challenges.ChallengeKind.Mushrooms,1,3,90f,false)=="MUSHROOM PATH  ROUND 2/3  1:30","Mushroom HUD line");
Assert(IncreaseHardMod.Challenges.ChallengeRules.FlagStatus(0,"387 + 244",0,61f,false)=="CAPTURE THE FLAG  387 + 244 = ?  (answer in chat)  1:01"&&IncreaseHardMod.Challenges.ChallengeRules.FlagStatus(2,"",0,120f,true)=="ЗАХВАТ ФЛАГА  УДЕРЖИВАЙТЕ  2:00","Flag HUD lines");
Assert(!IncreaseHardMod.Challenges.ChallengeRules.DeathFails(IncreaseHardMod.Challenges.ChallengeKind.Mushrooms)&&IncreaseHardMod.Challenges.ChallengeRules.Seconds(IncreaseHardMod.Challenges.ChallengeKind.Flag)==150f,"Mushrooms are not fatal; the flag has 150 s to answer and carry");
}
Assert(IncreaseHardMod.Challenges.ChallengeRules.WaveSize(1,1)==1&&IncreaseHardMod.Challenges.ChallengeRules.WaveSize(3,2)==3&&IncreaseHardMod.Challenges.ChallengeRules.WaveSize(3,4)==4&&IncreaseHardMod.Challenges.ChallengeRules.WaveSize(5,6)==4,"Waves grow 1,2,3 (+1 for 3+ players), at most 4");
Assert(IncreaseHardMod.Challenges.ChallengeRules.DeathFails(IncreaseHardMod.Challenges.ChallengeKind.Waves)&&!IncreaseHardMod.Challenges.ChallengeRules.DeathFails(IncreaseHardMod.Challenges.ChallengeKind.Vigil),"Deaths fail Monster Waves, not Vigil");
Assert(IncreaseHardMod.Challenges.ChallengeRules.OfferingTarget(0)==2000&&IncreaseHardMod.Challenges.ChallengeRules.OfferingTarget(40000)==6000&&IncreaseHardMod.Challenges.ChallengeRules.OfferingTarget(1e7)==20000,"Offering: 15% of level loot, $2K-$20K");
Assert(IncreaseHardMod.Challenges.ChallengeRules.Status(IncreaseHardMod.Challenges.ChallengeKind.Waves,1,3,100f,false)=="MONSTER WAVES  WAVE 2/3  1:40"&&IncreaseHardMod.Challenges.ChallengeRules.Status(IncreaseHardMod.Challenges.ChallengeKind.Offering,1500,6000,60f,false)=="OFFERING  $1.5K/$6K  1:00","New shrine HUD lines");
var rewards=Enumerable.Range(0,300).Select(i=>IncreaseHardMod.Challenges.ChallengeRules.Reward(i/300.0)).Distinct().ToList();
Assert(rewards.Count==3&&!rewards.Contains(IncreaseHardMod.Challenges.ChallengeReward.None),"A win always gives a skill point, an upgrade or a cosmetic box");
var withCostumes=Enumerable.Range(0,400).Select(i=>IncreaseHardMod.Challenges.ChallengeRules.Reward(i/400.0,true)).ToList();
Assert(withCostumes.Distinct().Count()==4&&withCostumes.Count(r=>r==IncreaseHardMod.Challenges.ChallengeReward.Costume)==100,"With costume rewards on, a costume piece is one of four equal shrine rewards");
Assert(IncreaseHardMod.Challenges.ChallengeRules.CullTarget(1)==5&&IncreaseHardMod.Challenges.ChallengeRules.CullTarget(6)==9&&IncreaseHardMod.Challenges.ChallengeRules.CullTarget(0)==5,"Cull: 4 + players, at most 9");
Assert(Enumerable.Range(0,100).All(i=>IncreaseHardMod.Challenges.ChallengeRules.PropCount(i/100.0) is >=6 and <=10)&&IncreaseHardMod.Challenges.ChallengeRules.PropCount(1)==10,"6 to 10 targets or crates");
Assert(IncreaseHardMod.Challenges.ChallengeRules.DeathFails(IncreaseHardMod.Challenges.ChallengeKind.Hunt)&&!IncreaseHardMod.Challenges.ChallengeRules.DeathFails(IncreaseHardMod.Challenges.ChallengeKind.Targets),"Deaths fail only the fighting challenges");
Assert(!IncreaseHardMod.Challenges.ChallengeRules.Expired(0,44.9f,IncreaseHardMod.Challenges.ChallengeKind.Targets)&&IncreaseHardMod.Challenges.ChallengeRules.Expired(0,45f,IncreaseHardMod.Challenges.ChallengeKind.Targets),"Target Practice lasts 45 s");
Assert(IncreaseHardMod.Challenges.ChallengeRules.Status(IncreaseHardMod.Challenges.ChallengeKind.Targets,4,8,30.2f,false)=="TARGET PRACTICE  4/8  0:31"&&IncreaseHardMod.Challenges.ChallengeRules.Status(IncreaseHardMod.Challenges.ChallengeKind.Escort,0,1,90f,false)=="WISP ESCORT  1:30","HUD line text");
Assert(IncreaseHardMod.Challenges.ChallengeRules.Name(IncreaseHardMod.Challenges.ChallengeKind.Smash,true)=="Разгром","Russian names");
Console.WriteLine($"PASS: {checks} collection, sample loss, liquid orientation and late join policy checks.");
