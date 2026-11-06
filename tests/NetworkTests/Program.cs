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
object[] quest={1,10,(byte)0,(byte)0,(byte)0,10,1,"",(byte)0,3000,(byte)0,(byte)0,"Quest",(byte)0,(byte)1,(byte)255};
Send(ModNetwork.QuestStateEventCode,quest,3);Check(IncreaseHardMod.Quests.QuestManager.States==0,"Peer cannot replace authoritative quest state");
Send(ModNetwork.QuestStateEventCode,quest);Check(IncreaseHardMod.Quests.QuestManager.States==1,"Valid host quest snapshot remains supported");
Send(ModNetwork.QuestStateEventCode,new object[]{1});Check(IncreaseHardMod.Quests.QuestManager.States==1,"Truncated quest row rejected before deserialization");
{ var contract=(object[])quest.Clone();contract[13]=(byte)4;Check(NetworkPayload.QuestState(contract,out _),"A contract quest flag is accepted");
  var greedy=(object[])quest.Clone();greedy[14]=(byte)50;Check(!NetworkPayload.QuestState(greedy,out _),"An oversized skill point reward is rejected");
  var old=new object[14];System.Array.Copy(quest,old,14);Check(!NetworkPayload.QuestState(old,out _),"The old 13-value quest layout is rejected"); }
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
int hudCalls=LevelInfoHud.Calls; Send(LevelInfoHud.StateEventCode,new object[]{"request"}); Check(LevelInfoHud.Calls==hudCalls+1,"HUD snapshot event routes to its scoped handler");
// Poisoned / sedated screen status: host only, local avatar only, current level only, bounded duration.
{
    bool wasMaster=PhotonNetwork.IsMasterClient;PhotonNetwork.IsMasterClient=false;
    var statusObject=new GameObject();var statusView=new PhotonView{ViewID=70};statusObject.Add(statusView);
    var statusAvatar=new PlayerAvatar{isLocal=true};statusObject.Add(statusAvatar);PhotonView.All[70]=statusView;
    string level=RunManager.instance.levelsCompleted+":"+RunManager.instance.levelCurrent.name;
    int started=IncreaseHardMod.Hud.HudConditions.Started;
    Send(ModNetwork.PlayerStatusEventCode,new object[]{70,(byte)0,4f,PhotonNetwork.Time,level},3);
    Check(IncreaseHardMod.Hud.HudConditions.Started==started,"Peer cannot put a status on a player");
    Send(ModNetwork.PlayerStatusEventCode,new object[]{70,(byte)1,4f,PhotonNetwork.Time,"0:old level"});
    Check(IncreaseHardMod.Hud.HudConditions.Started==started,"Status from another level is ignored");
    Send(ModNetwork.PlayerStatusEventCode,new object[]{70,(byte)1,500f,PhotonNetwork.Time,level});
    Check(IncreaseHardMod.Hud.HudConditions.Started==started,"Oversized status duration is rejected");
    Send(ModNetwork.PlayerStatusEventCode,new object[]{70,(byte)5,4f,PhotonNetwork.Time,level});
    Check(IncreaseHardMod.Hud.HudConditions.Started==started,"Unknown status kind is rejected");
    Send(ModNetwork.PlayerStatusEventCode,new object[]{70,(byte)1,4f,PhotonNetwork.Time,level});
    Check(IncreaseHardMod.Hud.HudConditions.Started==started+1&&IncreaseHardMod.Hud.HudConditions.Last==IncreaseHardMod.Hud.PlayerCondition.Sedated,"Host sedated status reaches the local player");
    statusAvatar.isLocal=false;
    Send(ModNetwork.PlayerStatusEventCode,new object[]{70,(byte)0,4f,PhotonNetwork.Time,level});
    Check(IncreaseHardMod.Hud.HudConditions.Started==started+1,"A status for another player shows nothing here");
    PhotonNetwork.IsMasterClient=wasMaster;
}
// Skill tree requests: only the host handles them, for the sender's own avatar, and answers only that sender.
{
    bool wasMaster=PhotonNetwork.IsMasterClient;PhotonNetwork.IsMasterClient=true;
    var S=IncreaseHardMod.Skills.SkillStore.Handled;S.Clear();PhotonNetwork.SentData.Clear();
    var guest=new PlayerAvatar{steamID="guest",photonView=new PhotonView{Owner=new Photon.Realtime.Player{ActorNumber=5}}};
    SemiFunc.Players.Clear();SemiFunc.Players.Add(guest);
    IncreaseHardMod.Skills.SkillStore.Answer=IncreaseHardMod.Skills.BuyBlock.NotEnoughPoints;
    Send(ModNetwork.SkillRequestEventCode,new object[]{(byte)0,"IHM_ST_Armor",1},5);
    Check(S.Count==1&&S[0].Steam=="guest"&&S[0].Node=="IHM_ST_Armor"&&S[0].Count==1,"Host handles a buy request for the sender's own avatar");
    var reply=PhotonNetwork.SentData.LastOrDefault(e=>e.Code==ModNetwork.SkillResultEventCode);
    Check(reply.Targets!=null&&reply.Targets.Length==1&&reply.Targets[0]==5&&reply.Data is object[] r&&(byte)r[1]==(byte)IncreaseHardMod.Skills.BuyBlock.NotEnoughPoints,"Result goes back to the sender only");
    Send(ModNetwork.SkillRequestEventCode,new object[]{(byte)0,"IHM_ST_Armor",50},5);
    Send(ModNetwork.SkillRequestEventCode,new object[]{(byte)7,"IHM_ST_Armor",1},5);
    Send(ModNetwork.SkillRequestEventCode,new object[]{(byte)0,new string('x',200),1},5);
    Check(S.Count==1,"Malformed skill requests are dropped");
    Send(ModNetwork.SkillRequestEventCode,new object[]{(byte)2,"",1},5);
    Check(S.Count==2&&S[1].Op==2&&S[1].Steam=="guest","A personal-quest point request reaches the host for the sender");
    Send(ModNetwork.SkillRequestEventCode,new object[]{(byte)3,"",1},5);
    Check(S.Count==2,"Unknown skill request kinds are dropped");
    S.RemoveAt(1);
    PhotonNetwork.SentData.Clear();
    Send(ModNetwork.SkillRequestEventCode,new object[]{(byte)1,"",1},9);
    Check(S.Count==1&&PhotonNetwork.SentData.Count==1,"A request from an actor without an avatar is answered but never applied");
    PhotonNetwork.IsMasterClient=false;
    Send(ModNetwork.SkillRequestEventCode,new object[]{(byte)0,"IHM_ST_Armor",1},5);
    Check(S.Count==1,"Only the host handles skill requests");
    var R=IncreaseHardMod.Skills.SkillStore.Reports;R.Clear();
    Send(ModNetwork.SkillResultEventCode,new object[]{(byte)0,(byte)0,"IHM_ST_Armor"},5);
    Check(R.Count==0,"A peer cannot fake a skill result");
    Send(ModNetwork.SkillResultEventCode,new object[]{(byte)0,(byte)0,"IHM_ST_Armor"},1);
    Check(R.Count==1&&R[0].Node=="IHM_ST_Armor","The host's skill result is shown");
    PhotonNetwork.IsMasterClient=wasMaster;SemiFunc.Players.Clear();
}
Console.WriteLine("NETWORK_AUDIT_PASS: "+checks);
