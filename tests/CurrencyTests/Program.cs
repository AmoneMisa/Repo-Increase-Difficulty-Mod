using System.Reflection;
using IncreaseHardMod.Economy;
using IncreaseHardMod.Quests;
int checks=0;void Check(bool ok,string label){if(!ok)throw new Exception(label);checks++;Console.WriteLine("PASS: "+label);}
void Scope(string type,string method)=>typeof(CurrencyPayout).GetNestedType(type,BindingFlags.NonPublic).GetMethod(method,BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,null);
foreach(var test in new[]{(0,0),(-1000,0),(1,1),(999,1),(1000,1),(4500,5),(6000,6),(int.MaxValue,2147484)})Check(QuestMoney.WalletUnits(test.Item1)==test.Item2,"Dollar-to-K conversion matches native ceiling: "+test.Item1);
SemiFunc.Currency=29;QuestMoney.Grant(4500);Check(SemiFunc.Currency==34,"Reported +$4,500 reward changes $29K to $34K, not $4,529K");
Check(CurrencyUI.instance.Fetches==1,"Quest payout refreshes the native wallet HUD");
SemiFunc.Currency=0;QuestMoney.Grant(6000);QuestMoney.Grant(4500);Check(SemiFunc.Currency==11,"Team and personal dollar rewards each convert exactly once");
int sets=SemiFunc.Sets;QuestMoney.Grant(0);QuestMoney.Grant(-1000);Check(SemiFunc.Currency==11&&SemiFunc.Sets==sets,"Non-positive rewards cannot mutate currency");
SemiFunc.Host=false;QuestMoney.Grant(6000);Check(SemiFunc.Currency==11,"Only the authoritative host credits quest money");SemiFunc.Host=true;
EconomyEvents.PayMult=.7f;SemiFunc.Currency=100;SemiFunc.StatSetRunCurrency(120);Check(SemiFunc.Currency==120,"Poor Facility does not tax unrelated wallet increases");
Scope("CompleteScope","Prefix");SemiFunc.Currency=100;SemiFunc.StatSetRunCurrency(120);Check(SemiFunc.Currency==114,"Poor Facility still taxes the native extraction payout");
SemiFunc.Currency=100;SemiFunc.StatSetRunCurrency(105);Check(SemiFunc.Currency==104,"Extraction rounding preserves native single-precision 5K x .7 => 4K");
SemiFunc.Currency=100;QuestMoney.Grant(6000);Check(SemiFunc.Currency==106,"Quest reward inside extraction callbacks remains untaxed");
SemiFunc.StatSetRunCurrency(80);Check(SemiFunc.Currency==80,"Purchases/death losses are not scaled as income");Scope("CompleteScope","Finalizer");
EconomyEvents.PayMult=1.3f;Scope("TaxReturnScope","Prefix");SemiFunc.Currency=100;SemiFunc.StatSetRunCurrency(120);Check(SemiFunc.Currency==126,"Native tax-return payout retains the extraction bonus");Scope("TaxReturnScope","Finalizer");
SemiFunc.Currency=100;QuestMoney.Grant(6000);Check(SemiFunc.Currency==106,"Extraction bonuses do not multiply quest rewards");
Scope("CompleteScope","Prefix");SemiFunc.ThrowOnSet=true;try{QuestMoney.Grant(6000);}catch(InvalidOperationException){}finally{SemiFunc.ThrowOnSet=false;}
SemiFunc.Currency=100;SemiFunc.StatSetRunCurrency(120);Check(SemiFunc.Currency==126,"An exception does not leak the untaxed reward scope");Scope("CompleteScope","Finalizer");
SemiFunc.Currency=100;SemiFunc.StatSetRunCurrency(120);Check(SemiFunc.Currency==120,"Extraction scope is released after completion/failure");
SemiFunc.Currency=int.MaxValue-1;QuestMoney.Grant(int.MaxValue);Check(SemiFunc.Currency==int.MaxValue,"Large valid rewards cannot overflow the wallet to negative");
Scope("CompleteScope","Prefix");SemiFunc.Currency=int.MaxValue-10;SemiFunc.StatSetRunCurrency(int.MaxValue);Check(SemiFunc.Currency==int.MaxValue,"Extraction bonus cannot overflow the wallet");Scope("CompleteScope","Finalizer");
SemiFunc.Level=false;Scope("CompleteScope","Prefix");SemiFunc.Currency=100;SemiFunc.StatSetRunCurrency(120);Check(SemiFunc.Currency==120,"Shop/menu payouts do not acquire a level extraction tax");Scope("CompleteScope","Finalizer");SemiFunc.Level=true;
Scope("CompleteScope","Prefix");RunManager.instance=null;SemiFunc.Currency=100;SemiFunc.StatSetRunCurrency(120);Check(SemiFunc.Currency==120,"Startup without RunManager is safe");RunManager.instance=new();Scope("CompleteScope","Finalizer");
Console.WriteLine("CURRENCY_AUDIT_PASS: "+checks);
