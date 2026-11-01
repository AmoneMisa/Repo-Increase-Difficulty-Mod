using ExitGames.Client.Photon;
using Photon.Pun;
using IncreaseHardMod.Networking;
using IncreaseHardMod.Patches.Effects;
using IncreaseHardMod.Gear;
using UnityEngine;

int checks=0;
void Check(bool ok,string name){if(!ok)throw new Exception(name);checks++;Console.WriteLine("PASS: "+name);}
var network=new ModNetwork();
void Send(byte code,object data,int sender=1)=>network.OnEvent(new EventData{Code=code,CustomData=data,Sender=sender});
if(args.Contains("--baseline-authority"))
{
 Send(ModNetwork.LevelTimerEventCode,new object[]{20f,true},3);Check(LevelTimerController.Applied==1,"Reproduced: peer overwrites timer");
 Send(ModNetwork.MapVoteCandidatesEventCode,new object[]{12,new[]{"A","B"},30f,false,-1},3);Check(MapVoteState.Candidates==1,"Reproduced: peer resets vote candidates");
 Send(ModNetwork.MapVoteBallotEventCode,new object[]{12,77,0,"spoof"},3);Check(MapVoteState.LastActor==77,"Reproduced: ballot impersonates another actor");
 var batteryObject=new GameObject();var batteryView=new PhotonView{ViewID=55};batteryObject.Add(batteryView);var rawBattery=new ItemBattery();batteryObject.Add(rawBattery);PhotonView.All[55]=batteryView;
 PhotonNetwork.IsMasterClient=true;Send(ModNetwork.BatteryDrainEventCode,new object[]{55,-10f},3);Check(rawBattery.batteryLife==110,"Reproduced: negative drain creates charge over 100%");
 var lootObject=new GameObject();var lootView=new PhotonView{ViewID=56};lootObject.Add(lootView);var rawLoot=new ValuableObject();lootObject.Add(rawLoot);PhotonView.All[56]=lootView;
 Send(ModNetwork.RepairEventCode,new object[]{56,100f},3);Check(rawLoot.dollarValueCurrent==100,"Reproduced: peer rewrites valuable value");
 Console.WriteLine("NETWORK_BASELINE_REPRODUCED: "+checks);return;
}
foreach(byte code in new[]{ModNetwork.LevelTimerEventCode,ModNetwork.PingEventCode,ModNetwork.MapVoteCandidatesEventCode,ModNetwork.MapVoteBallotEventCode,ModNetwork.BatteryDrainEventCode,ModNetwork.RepairEventCode})
 foreach(var raw in new object[]{null,42,"wrong",Array.Empty<object>(),new object[]{1},new object[]{"bad",new int[0],null}})
 {Send(code,raw);Check(true,"Malformed payload ignored without exception, event "+code);}
Send(ModNetwork.LevelTimerEventCode,new object[]{20f,true},3);Check(LevelTimerController.Applied==0,"Non-host cannot overwrite timer");
Send(ModNetwork.LevelTimerEventCode,new object[]{float.NaN,true});Check(LevelTimerController.Applied==0,"NaN timer rejected");
Send(ModNetwork.LevelTimerEventCode,new object[]{20f,true});Check(LevelTimerController.Applied==1&&LevelTimerController.Last==20,"Valid host timer is delivered");
object[] candidate={12,new[]{"A","B"},30f,false,-1};
Send(ModNetwork.MapVoteCandidatesEventCode,candidate,3);Check(MapVoteState.Candidates==0,"Non-host cannot reset map vote");
Send(ModNetwork.MapVoteCandidatesEventCode,candidate);Check(MapVoteState.Candidates==1,"Host candidates accepted");
Send(ModNetwork.MapVoteBallotEventCode,new object[]{12,77,0,"spoof"},3);Check(MapVoteState.Ballots==0,"Ballot cannot impersonate another actor");
Send(ModNetwork.MapVoteBallotEventCode,new object[]{12,3,0,"actual"},3);Check(MapVoteState.Ballots==1&&MapVoteState.LastActor==3,"Valid ballot uses Photon sender");
foreach(float invalid in new[]{float.NaN,float.PositiveInfinity,float.NegativeInfinity})
 {Send(ModNetwork.PingEventCode,new object[]{(byte)0,invalid,0f,0f,"place","player",0},3);Check(PingController.Applied==0,"Non-finite ping coordinate rejected");}
Send(ModNetwork.PingEventCode,new object[]{(byte)0,1f,2f,3f,"place","player",0},3);Check(PingController.Applied==1,"Valid peer ping remains supported");
var targetObject=new GameObject();var view=new PhotonView{ViewID=10};targetObject.Add(view);PhotonView.All[10]=view;var valuable=new ValuableObject();targetObject.Add(valuable);
Send(ModNetwork.RepairEventCode,new object[]{10,100f},3);Check(valuable.dollarValueCurrent==50,"Non-host cannot rewrite valuable price");
Send(ModNetwork.RepairEventCode,new object[]{10,-20f});Check(valuable.dollarValueCurrent==50,"Negative repair snapshot rejected");
Send(ModNetwork.RepairEventCode,new object[]{10,500f});Check(valuable.dollarValueCurrent==100,"Host repair clamps to original value");
PhotonNetwork.IsMasterClient=true;
var user=new PlayerAvatar{photonView=new PhotonView{OwnerActorNr=3}};GameDirector.instance.PlayerList.Add(user);
var protectedBattery=new GameObject();var battery=new ItemBattery();var shieldView=new PhotonView{ViewID=20};var shield=new ShieldItem{mode=ShieldMode.DefenceDrone,Target=user};protectedBattery.Add(shieldView);protectedBattery.Add(shield);protectedBattery.Add(battery);PhotonView.All[20]=shieldView;
foreach(float invalid in new[]{-10f,0f,101f,float.NaN,float.PositiveInfinity}){Send(ModNetwork.BatteryDrainEventCode,new object[]{20,invalid},3);Check(battery.batteryLife==100,"Invalid shield battery request rejected");}
Send(ModNetwork.BatteryDrainEventCode,new object[]{20,10f},4);Check(battery.batteryLife==100,"Uncovered player cannot drain another shield");
Send(ModNetwork.BatteryDrainEventCode,new object[]{20,10f},3);Check(battery.batteryLife==90,"Covered player can pay for absorbed damage");
shield.mode=ShieldMode.ProtectionOrb;Send(ModNetwork.BatteryDrainEventCode,new object[]{20,10f},3);Check(battery.batteryLife==90,"Client requests cannot target arbitrary battery categories");
PhotonNetwork.IsMasterClient=false;shield.mode=ShieldMode.DefenceDrone;Send(ModNetwork.BatteryDrainEventCode,new object[]{20,10f},3);Check(battery.batteryLife==90,"Only host applies requested drain");
PhotonNetwork.InRoom=false;Send(ModNetwork.PingEventCode,new object[]{(byte)0,1f,2f,3f,"place","player",0},3);Check(PingController.Applied==1,"Disconnected callback cannot create stale markers");
network.OnEvent(null);Check(true,"Null callback tolerated");
PhotonNetwork.InRoom=true;
var codes=typeof(ModNetwork).GetFields(System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.Static).Where(f=>f.IsLiteral&&f.FieldType==typeof(byte)).Select(f=>(byte)f.GetRawConstantValue()).ToArray();
Check(codes.Distinct().Count()==codes.Length,"Quest event IDs do not collide with weapon or other events");
foreach(byte code in new[]{ModNetwork.QuestStateEventCode,ModNetwork.QuestBuffEventCode,ModNetwork.QuestMoneyRequestEventCode})
 foreach(var raw in new object[]{null,42,"wrong",Array.Empty<object>(),new object[]{-1},new object[]{"bad",null}})
 {Send(code,raw);Check(true,"Malformed quest payload ignored, event "+code);}
object[] quest={1,10,(byte)0,(byte)0,(byte)0,10,1,"",(byte)0,3000,(byte)0,(byte)0,"Quest",(byte)0};
Send(ModNetwork.QuestStateEventCode,quest,3);Check(IncreaseHardMod.Quests.QuestManager.States==0,"Peer cannot replace authoritative quest state");
Send(ModNetwork.QuestStateEventCode,quest);Check(IncreaseHardMod.Quests.QuestManager.States==1,"Valid host quest snapshot remains supported");
Send(ModNetwork.QuestStateEventCode,new object[]{1});Check(IncreaseHardMod.Quests.QuestManager.States==1,"Truncated quest row rejected before deserialization");
Send(ModNetwork.QuestBuffEventCode,new object[]{(byte)1,2},3);Check(IncreaseHardMod.Quests.QuestManager.Buffs==0,"Peer cannot grant team quest buffs");
Send(ModNetwork.QuestBuffEventCode,new object[]{(byte)1,2});Check(IncreaseHardMod.Quests.QuestManager.Buffs==1,"Valid host quest buff accepted");
Send(ModNetwork.QuestBuffEventCode,new object[]{(byte)255,2});Check(IncreaseHardMod.Quests.QuestManager.Buffs==1,"Unknown quest buff kind rejected");
IncreaseHardMod.ModConfig.QuestsEnabled.Value=false;
Send(ModNetwork.QuestStateEventCode,quest);Send(ModNetwork.QuestBuffEventCode,new object[]{(byte)1,2});
Check(IncreaseHardMod.Quests.QuestManager.States==1&&IncreaseHardMod.Quests.QuestManager.Buffs==1,"Disabled quests ignore incoming state and buffs");
PhotonNetwork.IsMasterClient=true;Send(ModNetwork.QuestMoneyRequestEventCode,new object[]{1000},3);
Check(IncreaseHardMod.Quests.QuestRewards.Money==0,"Disabled quests do not accept payout requests");
IncreaseHardMod.ModConfig.QuestsEnabled.Value=true;
Send(ModNetwork.QuestMoneyRequestEventCode,new object[]{-1000},3);Check(IncreaseHardMod.Quests.QuestRewards.Money==0,"Negative quest payout rejected");
Send(ModNetwork.QuestMoneyRequestEventCode,new object[]{1000},3);Check(IncreaseHardMod.Quests.QuestRewards.Money==1000,"Host retains personal quest payout routing");
PhotonNetwork.IsMasterClient=false;Send(ModNetwork.QuestMoneyRequestEventCode,new object[]{1000},3);
Check(IncreaseHardMod.Quests.QuestRewards.Money==1000,"Non-host does not apply personal quest payout");
PhotonNetwork.Sent.Clear();network.BroadcastQuestState(quest);network.BroadcastQuestBuff(1,2);
Check(PhotonNetwork.Sent.Count==0,"Non-host cannot broadcast authoritative quest state or buffs");
PhotonNetwork.IsMasterClient=true;network.BroadcastQuestState(quest);network.BroadcastQuestBuff(1,2);
Check(PhotonNetwork.Sent.Count==2&&PhotonNetwork.Sent.All(e=>e.Receivers==Photon.Realtime.ReceiverGroup.Others),"Locally applied quest broadcasts do not echo back to the host");
Check(NetworkPayload.Ping(new object[]{(byte)8,1f,2f,3f,"Player","Sender",313},out _),"Player ping kind accepts a replicated avatar view ID");
Check(!NetworkPayload.Ping(new object[]{(byte)9,1f,2f,3f,"unknown","Sender",0},out _),"Undefined ping kind remains rejected");
Check(!NetworkPayload.Ping(new object[]{(byte)8,1f,2f,3f,"Player","Sender",-1},out _),"Player ping rejects a negative view ID");
Console.WriteLine("NETWORK_AUDIT_PASS: "+checks);
