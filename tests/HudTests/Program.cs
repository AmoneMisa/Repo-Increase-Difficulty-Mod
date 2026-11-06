using System.Reflection;
using IncreaseHardMod.Patches.Effects;
using IncreaseHardMod.Modifiers;
using IncreaseHardMod.Economy;
using Photon.Pun;
using TMPro;
using UnityEngine;
using ExitGames.Client.Photon;
int checks=0;
void Check(bool ok,string name){if(!ok)throw new Exception(name);Console.WriteLine("PASS "+name);checks++;}
var hud=new GameObject().AddComponent<LevelInfoHud>();
void Tick()=>typeof(LevelInfoHud).GetMethod("LateUpdate",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(hud,null);
T Field<T>(string name)=>(T)typeof(LevelInfoHud).GetField(name,BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(hud)!;
TextMeshProUGUI Label()=>new GameObject().AddComponent<TextMeshProUGUI>();
RunManager.instance=new();LevelGenerator.Instance=new();GameDirector.instance=new(){currentState=GameDirector.gameState.Load};
HUDCanvas.instance=new GameObject().AddComponent<HUDCanvas>();HUDCanvas.instance.rect.sizeDelta=new(1920,1080);
HUDCanvas.instance.gameObject.layer=5; // Verified native HUD layer; Camera Overlay mask is 32.
LoadingUI.instance=new GameObject().AddComponent<LoadingUI>();
Map.Instance=new();StatsUI.instance=new(){Text=Label(),upgradesHeader=Label()};
GoalUI.instance=new(){uiText=Label()};GoalUI.instance.uiText.enabled=true;GoalUI.instance.uiText.text="0/3";
HaulUI.instance=new(){uiText=Label()};HaulUI.instance.uiText.enabled=true;HaulUI.instance.uiText.text="$0 / $5000";
HaulUI.instance.uiText.textBounds=new(){min=new(700,470),max=new(900,510)};
LevelUI.instance=new(){uiText=Label(),textNumber=Label()};
GoalUI.instance.uiText.textBounds=new(){min=new(700,400),max=new(900,450)};
LevelUI.instance.uiText.textBounds=LevelUI.instance.textNumber.textBounds=new(){min=new(700,-200),max=new(900,-160)};
Check(!LevelInfoHud.GameplayReady,"Loading is blocked");Tick();Check(Field<TextMeshProUGUI>("_modsLabel")==null,"No labels during generation");
LevelGenerator.Instance.Generated=true;GameDirector.instance.currentState=GameDirector.gameState.Start;
Check(!LevelInfoHud.GameplayReady,"Generated alone does not bypass loading animation");
GameDirector.instance.currentState=GameDirector.gameState.Main;
Check(!LevelInfoHud.GameplayReady,"Active loading screen still blocks HUD");LoadingUI.instance.gameObject.activeInHierarchy=false;
Check(LevelInfoHud.GameplayReady,"Native Main and finished generation unlock HUD");
EconomyEvents.Current=EconomyEvents.All[0];Tick();
foreach (string field in new[]{"_timerLabel","_modsLabel","_mapLabel","_descriptionLabel","_detailsPageLabel"})
    Check((32 & (1 << Field<TextMeshProUGUI>(field).gameObject.layer)) != 0,
        "Native UI-only camera can render " + field);
Check(Field<TextMeshProUGUI>("_modsLabel").text.Contains("POOR FACILITY"),"Economy name retained under quota");
Check(!Field<TextMeshProUGUI>("_descriptionLabel").enabled,"Description hidden with Tab closed");
Check(Field<TextMeshProUGUI>("_descriptionLabel").fontSize==StatsUI.instance.Text.fontSize/2,"Description size is exactly half native body");
Map.Instance.Active=true;Tick();Check(Field<TextMeshProUGUI>("_descriptionLabel").enabled,"Actual map opens details");
Check(Field<TextMeshProUGUI>("_descriptionLabel").text.Contains("30% less"),"Complete economy effect is available in Tab");
IncreaseHardMod.Upgrades.MapUpgrade.HudInfo="Players alive: 4\nMonsters active: 8";Tick();
Check(Field<TextMeshProUGUI>("_mapLabel").enabled&&Field<TextMeshProUGUI>("_mapLabel").text.Contains("Players alive: 4")&&
    Field<TextMeshProUGUI>("_descriptionLabel").enabled&&Field<TextMeshProUGUI>("_descriptionLabel").text.Contains("30% less"),
    "Map Suite and modifier descriptions appear together without switching sections");
Field<TextMeshProUGUI>("_mapLabel").textInfo.pageCount=3;
Input.Pressed=KeyCode.Period;Tick();Input.Pressed=null;
Check(Field<TextMeshProUGUI>("_mapLabel").pageToDisplay==2,"Period pages long Map Suite data in its own block");
Input.Pressed=KeyCode.Comma;Tick();Input.Pressed=null;
Check(Field<TextMeshProUGUI>("_mapLabel").pageToDisplay==1&&Field<TextMeshProUGUI>("_descriptionLabel").text.Contains("30% less"),"Comma pages Map Suite without hiding descriptions");
Input.Pressed=KeyCode.Home;Tick();Input.Pressed=null;
Check(Field<TextMeshProUGUI>("_descriptionLabel").text.Contains("30% less"),"Home no longer switches sections");
SemiFunc.TextInput=true;Input.Pressed=KeyCode.Period;Tick();Input.Pressed=null;SemiFunc.TextInput=false;
Check(Field<TextMeshProUGUI>("_mapLabel").pageToDisplay==1,"Typing period in text input cannot page Map Suite");
Check(Field<TextMeshProUGUI>("_detailsPageLabel").text.Contains(", / .:"),"Tab hint matches comma and period bindings");
Map.Instance.Active=false;Tick();Check(!Field<TextMeshProUGUI>("_mapLabel").enabled&&!Field<TextMeshProUGUI>("_descriptionLabel").enabled&&!Field<TextMeshProUGUI>("_detailsPageLabel").enabled,"Closing map hides statistics and page hints immediately");
RunManager.instance.restarting=true;Tick();Check(!Field<TextMeshProUGUI>("_modsLabel").enabled,"Transition hides names immediately");RunManager.instance.restarting=false;
PhotonNetwork.InRoom=true;PhotonNetwork.IsMasterClient=false;Tick();
Check(!LevelInfoHud.InfoReady,"Client waits for fresh host snapshot");
var request=PhotonNetwork.Sent.Last();string nonce=(string)request[0],key=(string)request[1];
void Reply(string n,string k,int sender=1)=>LevelInfoHud.HandleStateEvent(new(){Sender=sender,CustomData=new object[]{n,k,LevelModifiers.Empty.Serialize(),1,0}});
Reply(nonce,key,2);Check(!LevelInfoHud.InfoReady,"Peer cannot supply HUD state");
Reply(new string('a',32),key);Check(!LevelInfoHud.InfoReady,"Old nonce rejected");
Reply(nonce,"previous level");Check(!LevelInfoHud.InfoReady,"Wrong level rejected");
LoadingUI.instance.gameObject.activeInHierarchy=true;Tick();
Reply(nonce,key);
LoadingUI.instance.gameObject.activeInHierarchy=false;
Check(LevelInfoHud.InfoReady,"Host snapshot received while HUD is temporarily hidden remains valid");
Tick();Check(PhotonNetwork.Sent.Count==1,"No polling or timer-based repeated requests");
GameDirector.instance.currentState=GameDirector.gameState.Outro;Tick();Check(!LevelInfoHud.InfoReady,"Outro invalidates prior snapshot");
GameDirector.instance.currentState=GameDirector.gameState.Main;LevelGenerator.Instance=new(){Generated=true};Tick();
Reply(nonce,key);Check(!LevelInfoHud.InfoReady,"Prior scene reply cannot unlock a new scene even with identical level name");
request=PhotonNetwork.Sent.Last();Reply((string)request[0],(string)request[1]);Check(LevelInfoHud.InfoReady,"Fresh reply restores HUD after scene replacement");
PhotonNetwork.MasterClient=new(){ActorNumber=3};Check(!LevelInfoHud.InfoReady,"Host migration invalidates snapshot before next HUD tick");
Tick();request=PhotonNetwork.Sent.Last();Reply((string)request[0],(string)request[1],3);Check(LevelInfoHud.InfoReady,"New master can provide authoritative HUD snapshot");
// Scope and loading regressions use the production HUD source with deterministic native UI stand-ins.
var currentRequest=PhotonNetwork.Sent.Last();
PhotonNetwork.CurrentRoom=new(){Name=PhotonNetwork.CurrentRoom.Name};
Check(!LevelInfoHud.InfoReady,"Different room object invalidates snapshot even with reused room name and actor IDs");
Tick();request=PhotonNetwork.Sent.Last();Reply((string)request[0],(string)request[1],3);
Check(LevelInfoHud.InfoReady,"Fresh snapshot accepted in rejoined room");
// A host that never answers must not blank the guest HUD: the request repeats (same nonce) and, after a short
// grace period, the HUD falls back to the modifiers already synced at level start.
LevelGenerator.Instance=new(){Generated=true};int sentBefore=PhotonNetwork.Sent.Count;Tick();
string unanswered=(string)PhotonNetwork.Sent.Last()[0];
Check(PhotonNetwork.Sent.Count==sentBefore+1&&!LevelInfoHud.InfoReady,"Guest requests a snapshot for a new scene and waits briefly");
Tick();Check(PhotonNetwork.Sent.Count==sentBefore+1,"Unanswered request is not repeated every frame");
Time.unscaledTime+=3.1f;Tick();
Check(PhotonNetwork.Sent.Count==sentBefore+2&&(string)PhotonNetwork.Sent.Last()[0]==unanswered,"Unanswered request repeats after 3 s with the same nonce");
Check(LevelInfoHud.InfoReady,"Guest HUD falls back to synced modifiers when the host does not answer");
Reply(unanswered,(string)PhotonNetwork.Sent.Last()[1],3);Tick();
Check(LevelInfoHud.InfoReady&&PhotonNetwork.Sent.Count==sentBefore+2,"Late reply to a repeated request is accepted and stops the repeats");
RunManager.instance.levelCurrent=new(){name="next"};
Check(!LevelInfoHud.GameplayReady,"Selected next level cannot reuse the old generated scene");
RunManager.instance.levelCurrent=LevelGenerator.Instance.Level;
GameDirector.instance.currentState=GameDirector.gameState.Start;
PhotonNetwork.IsMasterClient=true;Tick();
string pending=new string('b',32);
LevelInfoHud.HandleStateEvent(new(){Sender=4,CustomData=new object[]{pending,(string)request[1]}});
int before=PhotonNetwork.Sent.Count;
Tick();Check(PhotonNetwork.Sent.Count==before,"Host retains request while its own loading is unfinished");
GameDirector.instance.currentState=GameDirector.gameState.Main;Tick();
Check(PhotonNetwork.Sent.Count==before+1 && (string)PhotonNetwork.Sent.Last()[0]==pending,"Host answers queued request on actual readiness without a timer");
Tick();Check(PhotonNetwork.Sent.Count==before+1,"Unchanged host state is not repeatedly sent");
ModifierManager.Revision++;Tick();
Check(PhotonNetwork.Sent.Count==before+2,"Changed authoritative revision refreshes the existing HUD snapshot");

SemiFunc.IsLevel=false;SemiFunc.Shop=true;Map.Instance.Active=false;MapToolController.instance=new(){Active=true};
ShopEconomy.Current=new(){Id=1,Name="SHOP SALE",Description="Shop items cost less."};GoalUI.instance.isHidden=true;HaulUI.instance.isHidden=true;Tick();
Check(LevelInfoHud.TabOpen&&Field<TextMeshProUGUI>("_descriptionLabel").enabled,"Native shop Tab works without an active map or visible quota");
Check(Field<TextMeshProUGUI>("_descriptionLabel").text.Contains("Shop items cost less"),"Shop modifier description is visible in shop Tab");
MapToolController.instance.Active=false;Tick();
Check(!Field<TextMeshProUGUI>("_descriptionLabel").enabled,"Closing shop Tab hides descriptions");
SemiFunc.Shop=false;SemiFunc.IsLevel=true;GoalUI.instance.isHidden=false;HaulUI.instance.isHidden=false;
EconomyEvents.Current=EconomyEvents.All[0];RunManager.instance.restarting=true;Tick();
RunManager.instance.restarting=false;LevelGenerator.Instance=new(){Generated=true};Tick();
Check(Field<TextMeshProUGUI>("_modsLabel").enabled,"Modifier HUD returns after timer-style scene retry without level count advance");

// The actual HUD is world-space, 712 x 400 at 16:9, rather than a 1920px overlay.
// Measure a comparable right-hand quota/level layout across narrow and wide viewports.
PhotonNetwork.IsMasterClient=true;MapToolController.instance.Active=true;
LevelUI.instance.uiText.text="LEVEL";LevelUI.instance.textNumber.text="1";
foreach(float width in new[]{533f,712f,933f})
{
    HUDCanvas.instance.rect.sizeDelta=new(width,400);
    GoalUI.instance.uiText.textBounds=new(){min=new(width/2-100,160),max=new(width/2-15,185)};
    HaulUI.instance.uiText.textBounds=new(){min=new(width/2-100,188),max=new(width/2-15,199)};
    LevelUI.instance.uiText.textBounds=LevelUI.instance.textNumber.textBounds=
        new(){min=new(width/2-100,-100),max=new(width/2-15,-80)};
    // Existing labels retain their native size, and names/details remain disjoint.
    Tick();
    var names=Field<TextMeshProUGUI>("_modsLabel");
    var mapData=Field<TextMeshProUGUI>("_mapLabel");
    var details=Field<TextMeshProUGUI>("_descriptionLabel");
    Check(names.enabled&&details.enabled,"Names and Tab details fit native HUD width " + width);
    Check(mapData.enabled&&mapData.rectTransform.anchoredPosition.y < 160-200 &&
        names.rectTransform.anchoredPosition.y <= mapData.rectTransform.anchoredPosition.y-mapData.rectTransform.rect.height,
        "Order is haul, extraction count, Map Suite, modifier names at width " + width);
    Check(details.rectTransform.anchoredPosition.y <= names.rectTransform.anchoredPosition.y-names.rectTransform.rect.height,
        "Descriptions do not overlap modifier names at width " + width);
}

IncreaseHardMod.Upgrades.MapUpgrade.HudInfo="Alive: 4\nDead: 1\nActive: 8\nRespawning: 2\nQuota: ahead\nFound: 20/30\nHaul: 1000/5000\nSecured: 2";Tick();
Check(Field<TextMeshProUGUI>("_mapLabel").enabled&&Field<TextMeshProUGUI>("_modsLabel").enabled&&Field<TextMeshProUGUI>("_descriptionLabel").enabled,
    "Full Map Suite statistics leave visible space for names and descriptions");
IncreaseHardMod.Upgrades.MapUpgrade.HudInfo="";Tick();
Check(!Field<TextMeshProUGUI>("_mapLabel").enabled&&Field<TextMeshProUGUI>("_modsLabel").enabled,
    "Without Map Suite there is no empty map block above modifier names");

Console.WriteLine($"HUD_CHECKS_PASSED: {checks}");
