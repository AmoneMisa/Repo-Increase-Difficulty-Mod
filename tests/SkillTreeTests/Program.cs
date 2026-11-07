using IncreaseHardMod.Skills;
using C = IncreaseHardMod.Skills.SkillCatalog;

// Engine-free checks of the skill tree model (src/Skills): catalogue, buy rules, sanitising and save migration.
int checks = 0;
void Check(bool ok, string name) { if (!ok) throw new Exception("FAIL " + name); Console.WriteLine("PASS " + name); checks++; }

// ---------------------------------------------------------------- catalogue
Check(C.NormalPageCost(SkillPage.Recon) == 30 && C.NormalPageCost(SkillPage.Combat) == 50 &&
      C.NormalPageCost(SkillPage.Survival) == 37 && C.NormalPageCost(SkillPage.Utility) == 41 && C.NormalPageCost(SkillPage.Arsenal) == 42, "Normal page costs match the plan");
Check(C.PageCost(SkillPage.Recon) == 36 && C.PageCost(SkillPage.Combat) == 56 && C.PageCost(SkillPage.Survival) == 43 &&
      C.PageCost(SkillPage.Utility) == 47 && C.PageCost(SkillPage.Arsenal) == 48, "Each page adds one trade-off per tier (1+2+3 points)");
Check(C.TotalCost() == 230, "Whole tree costs 230 points (200 normal + 30 trade-off picks)");
Check(C.Find(C.Vampirism) is { Page: SkillPage.Combat, Cost: 1, MaxLevel: 7 } && C.Find(C.Vampirism).Requires.SequenceEqual(new[] { C.MeleeDamage }),
      "Vampirism: Combat, 7 levels of 1 point, under Melee Damage");
Check(Math.Abs(PerkRules.VampireShare(7, 7, .29f) - .29f) < 1e-5f && Math.Abs(PerkRules.VampireShare(1, 7, .29f) - .29f / 7f) < 1e-5f &&
      PerkRules.VampireShare(9, 7, .29f) == PerkRules.VampireShare(7, 7, .29f) && PerkRules.VampireShare(0, 7, .29f) == 0f, "Vampirism reaches 29% at level 7 and never more");
float carry = 0f;
int healed = 0; for (int i = 0; i < 10; i++) healed += PerkRules.VampireHeal(10, .29f, ref carry);
Check(healed == 29 && carry < 1f && PerkRules.VampireHeal(0, .29f, ref carry) == 0, "Vampirism keeps the fractions: ten 10-damage hits heal 29");

// ---------------------------------------------------------------- trade-offs
var tradeOffs = C.All.Where(n => n.Tier > 0).ToList();
var tradePages = new[] { SkillPage.Recon, SkillPage.Combat, SkillPage.Survival, SkillPage.Utility, SkillPage.Arsenal };
Check(tradeOffs.Count == 31, "31 trade-off skills");
Check(tradePages.All(pg => Enumerable.Range(1, 3).All(t => tradeOffs.Count(n => n.Page == pg && n.Tier == t) >= 2)),
      "At least two trade-offs on every tier of every page");
Check(tradeOffs.All(n => n.Page != SkillPage.Contracts && n.Cost == n.Tier && n.MaxLevel == 1 && n.Root && n.Requires.Length == 0 && !n.QuestOnly),
      "Trade-offs: tier N costs N points, one level, no prerequisites");
Check(tradeOffs.All(n => TradeOffRules.Find(n.Id) is { } t && t.Tier == n.Tier && t.Up.Length > 0 && t.Down.Length > 0 && (t.Special || t.Effects.Length >= 2)),
      "Every trade-off has an upside, a downside and effects");
Check(tradeOffs.All(n => TradeOffRules.Find(n.Id).Special || TradeOffRules.Find(n.Id).Effects.Count(e => e.Fx is Fx.MeleeStunSeconds or Fx.LuckyLevels or Fx.StatusCapSeconds || Math.Abs(e.Value - 1f) > 1e-4f) >= 2),
      "Every numeric trade-off changes at least two things");
Check(tradeOffs.All(n => !string.IsNullOrEmpty(TradeOffRules.Find(n.Id).UpRu) && !string.IsNullOrEmpty(TradeOffRules.Find(n.Id).DownRu) && !string.IsNullOrEmpty(n.NameRu)),
      "Every trade-off has a Russian name, upside and downside");
Check(tradePages.All(pg => TradeOffRules.Gate(pg, 1) < TradeOffRules.Gate(pg, 2) && TradeOffRules.Gate(pg, 2) < TradeOffRules.Gate(pg, 3) &&
      TradeOffRules.Gate(pg, 3) <= C.NormalPageCost(pg)), "Gates rise per tier and the last fits inside the page");

// Fill a page's normal perks (catalogue order respects prerequisites) until at least `points` are spent.
SkillLoadout Spend(SkillPage pg, int points)
{
    var lo = new SkillLoadout { Earned = 500 };
    foreach (var n in C.All.Where(n => n.Page == pg && n.Tier == 0 && !n.QuestOnly))
        while (SkillRules.PageSpent(lo, pg) < points && SkillRules.Buy(lo, n.Id) == BuyBlock.None) { }
    return lo;
}
var fresh = new SkillLoadout { Earned = 50 };
Check(SkillRules.CanBuy(fresh, C.GunnersFocus) == BuyBlock.GateNotMet, "Tier I is locked until the page gate");
var combatLo = Spend(SkillPage.Combat, 12);
Check(SkillRules.PageSpent(combatLo, SkillPage.Combat) >= 12 && SkillRules.Buy(combatLo, C.GunnersFocus) == BuyBlock.None, "Tier I opens at 12 Combat points");
Check(SkillRules.CanBuy(combatLo, C.Bruiser) == BuyBlock.TierTaken, "Only one pick per tier");
Check(SkillRules.CanBuy(combatLo, C.GlassCannon) == BuyBlock.GateNotMet, "Tier II needs its own gate");
var combat2 = Spend(SkillPage.Combat, 24);
Check(SkillRules.CanBuy(combat2, C.GlassCannon) == BuyBlock.NeedLowerTier, "Tier II needs a tier I pick");
Check(SkillRules.Buy(combat2, C.Bruiser) == BuyBlock.None && SkillRules.Buy(combat2, C.Overdrive) == BuyBlock.None, "Tier II after tier I");
Check(SkillRules.CanBuy(combat2, C.GunnersFocus) == BuyBlock.TierTaken, "The other tier I pick stays closed");
Check(SkillRules.CanBuy(combat2, C.Sharpshooter) == BuyBlock.GateNotMet, "Tier III needs 36 Combat points");
int normalBefore = combat2.Spent - SkillRules.TradeOffSpent(combat2);
Check(SkillRules.TradeOffSpent(combat2) == 3 && SkillRules.RespecTradeOffs(combat2) == 3 && combat2.Level(C.Bruiser) == 0 &&
      combat2.Spent == normalBefore, "Trade-off reset refunds only trade-offs");
Check(SkillRules.Buy(combat2, C.GunnersFocus) == BuyBlock.None, "After a reset another tier I pick is allowed");
var lost = Spend(SkillPage.Combat, 24);
SkillRules.Buy(lost, C.GunnersFocus); SkillRules.Buy(lost, C.GlassCannon);
lost.Bought.Remove(C.GrenadeDamage); lost.Bought.Remove(C.MeleeDamage); lost.Bought.Remove(C.TumbleDamage);
var lostFixes = SkillRules.Sanitize(lost);
Check(lost.Level(C.GlassCannon) == 0 && lostFixes.Any(f => f.Contains("TO_GlassCannon")), "Sanitize drops a trade-off whose gate is gone");
Check(Math.Abs(TradeOffRules.Factor(id => id == C.GlassCannon || id == C.Juggernaut, Fx.DamageTaken) - 1.2f * .75f) < 1e-4f,
      "Damage-taken factors multiply");
Check(TradeOffRules.Factor(_ => false, Fx.GunDamage) == 1f && TradeOffRules.Sum(id => id == C.Berserker, Fx.MeleeStunSeconds) == 1f, "No trade-offs, no change");
Check(Math.Abs(TradeOffRules.StatusSeconds(6f, 1.25f, 10f) - 7.5f) < 1e-4f && TradeOffRules.StatusSeconds(9f, 1.25f, 10f) == 10f &&
      TradeOffRules.StatusSeconds(12f, 1.25f, 10f) == 12f && TradeOffRules.StatusSeconds(8f, .5f, 8f) == 4f, "Status length: longer up to the cap, shorter by the factor");
Check(C.Find(C.CrouchSpeed) is { Page: SkillPage.Survival, Cost: 1, MaxLevel: 3 } && C.Find(C.CrouchSpeed).Requires.SequenceEqual(new[] { C.Survivor }),
      "Crouch Speed: Survival, 1 point a level, 3 levels, under Survivor");
Check(Math.Abs(PerkRules.CrouchSpeedFactor(3, .05f) - 1.15f) < 1e-5f && PerkRules.CrouchSpeedFactor(0, .05f) == 1f &&
      PerkRules.CrouchSpeedFactor(-2, .05f) == 1f && PerkRules.CrouchSpeedFactor(2, -1f) == 1f, "Crouch Speed is +15% at max and never slows you");
Check(C.All.Where(n => n.Page == SkillPage.Arsenal && !n.Root).All(n => n.MaxLevel >= 3 && n.MaxLevel <= 5), "Weapon and status perks have 3-5 levels");

// Status lengths: longer up to 8 s, poison shorter down to 8 s, never past the cap either way.
bool Near(float a, float b) => Math.Abs(a - b) < 0.001f;
Check(Near(C.StatusSeconds(5f, 0, 3), 5f) && Near(C.StatusSeconds(5f, 1, 3), 6f) && Near(C.StatusSeconds(5f, 3, 3), 8f), "Net 5 s grows a step per level to 8 s");
Check(Near(C.StatusSeconds(4.5f, 3, 3), 8f) && Near(C.StatusSeconds(4.5f, 9, 3), 8f), "Freeze never passes 8 s");
Check(Near(C.StatusSeconds(10f, 3, 3), 10f), "A status already longer than 8 s is not lengthened");
Check(Near(C.StatusSeconds(15f, 1, 3, compress: true), 15f - 7f / 3f) && Near(C.StatusSeconds(15f, 3, 3, compress: true), 8f), "Poison 15 s compresses to 8 s");
Check(Near(C.StatusSeconds(15f, 5, 3, compress: true), 8f) && Near(C.StatusSeconds(6f, 3, 3, compress: true), 6f), "Poison never runs shorter than 8 s");
Check(C.All.Select(n => n.Id).Distinct().Count() == C.All.Count, "Node ids are unique");
Check(C.All.All(n => n.Id.StartsWith("IHM_ST_")), "Node ids use the tree prefix");
Check(C.All.All(n => n.MaxLevel >= 1 && (n.QuestOnly ? n.Cost == 0 : n.Cost >= 1)), "Every bought node costs a point; quest-only ones cost none");
Check(C.All.Where(n => n.QuestOnly).All(n => n.Page == SkillPage.Contracts) && C.All.Count(n => n.QuestOnly) == 10, "The ten quest-only perks sit on the Contracts page");
Check(C.QuestPerk(0)?.Id == C.CartHandling && C.QuestPerkIndex(C.SoftSteps) == 3 && C.QuestPerkIndex(C.MendingSurge) == 9 && C.QuestPerk(10) == null,
      "Quest perks are addressed by index (new ones appended)");
Check(new[] { 1, 2, 3 }.Select(PerkRules.BuffSeconds).SequenceEqual(new[] { 3f, 4f, 5f }) && new[] { 1, 2, 3 }.Select(PerkRules.BuffCooldown).SequenceEqual(new[] { 60f, 45f, 30f }) &&
      Math.Abs(PerkRules.SurgeShare(3) - .15f) < 1e-5f && PerkRules.BuffSeconds(9) == 5f, "Instincts: 3/4/5 s, cooldown 60/45/30 s, surge 5/10/15%");
Check(C.All.All(n => n.Requires.All(r => C.Find(r) != null)), "Every prerequisite exists");
Check(C.All.All(n => n.Requires.All(r => C.Find(r).Page == n.Page)), "Prerequisites stay on the node's page");
Check(C.All.All(n => n.Root == (n.Requires.Length == 0)), "Exactly the nodes without prerequisites are roots");
Check(C.All.All(n => !string.IsNullOrEmpty(n.Name) && !string.IsNullOrEmpty(n.NameRu) && !string.IsNullOrEmpty(n.Branch)), "Every node has names and a branch");
Check(C.All.Count(n => n.Vanilla != VanillaUpgrade.None) == 2, "Two nodes drive vanilla upgrades");
bool Acyclic(SkillNode node, HashSet<string> path) =>
    path.Add(node.Id) && node.Requires.All(r => Acyclic(C.Find(r), new HashSet<string>(path)));
Check(C.All.All(n => Acyclic(n, new HashSet<string>())), "Prerequisites have no cycles");

// ---------------------------------------------------------------- buying
var l = new SkillLoadout { Earned = 3 };
Check(SkillRules.CanBuy(l, "nope") == BuyBlock.UnknownNode, "Unknown node is refused");
Check(SkillRules.CanBuy(l, C.MeleeDamage) == BuyBlock.MissingPrerequisite, "A node needs its parent");
Check(SkillRules.Buy(l, C.Brawler) == BuyBlock.None && l.Level(C.Brawler) == 1, "A root can be bought");
Check(SkillRules.CanBuy(l, C.Brawler) == BuyBlock.Maxed, "A maxed node is refused");
Check(SkillRules.Buy(l, C.MeleeDamage, 2) == BuyBlock.None && l.Level(C.MeleeDamage) == 2, "Several levels at once");
Check(l.Spent == 3 && l.Available == 0, "Spent and available points add up");
Check(SkillRules.CanBuy(l, C.MeleeDamage) == BuyBlock.NotEnoughPoints, "No points, no level");
Check(SkillRules.CanBuy(l, C.MeleeDamage, 0) == BuyBlock.BadCount, "Zero levels is refused");
l.Earned = 100;
Check(SkillRules.CanBuy(l, C.MeleeDamage, 4) == BuyBlock.Maxed && SkillRules.CanBuy(l, C.MeleeDamage, 3) == BuyBlock.None, "A multi-level buy never passes the cap");
SkillRules.Buy(l, C.Survivor); SkillRules.Buy(l, C.Armor);
Check(l.Spent == 3 + 1 + 2, "Strong nodes charge their own cost");
Check(SkillRules.Respec(l) == 6 && l.Spent == 0 && l.Available == 100 && l.Level(C.Brawler) == 0, "Respec refunds everything");

// Quest-only perks: never bought, granted by contracts, kept by a refund.
var q = new SkillLoadout { Earned = 10 };
Check(SkillRules.CanBuy(q, C.GentleHands) == BuyBlock.QuestOnly, "Quest-only perks can't be bought with points");
Check(SkillRules.GrantQuestLevel(q, C.GentleHands) && SkillRules.GrantQuestLevel(q, C.GentleHands) && q.Level(C.GentleHands) == 2 && q.Spent == 0, "Contracts grant quest-only levels for free");
q.Bought[C.GentleHands] = 3;
Check(!SkillRules.GrantQuestLevel(q, C.GentleHands) && !SkillRules.GrantQuestLevel(q, C.Armor), "A maxed or normal perk can't be granted by a contract");
SkillRules.Buy(q, C.Survivor);
SkillRules.Respec(q);
Check(q.Level(C.GentleHands) == 3 && q.Level(C.Survivor) == 0 && SkillRules.Sanitize(q).Count == 0, "A refund keeps quest-only perks");

// Free (vanilla) levels: count for caps and prerequisites, never for cost.
var v = new SkillLoadout { Earned = 5 };
v.Free[C.TumbleWings] = 2;
Check(v.Level(C.TumbleWings) == 2 && v.Spent == 0, "Free levels cost nothing");
Check(SkillRules.Buy(v, C.TumbleWings) == BuyBlock.None && v.Spent == 1, "Buying on top of free levels charges only the new level");
Check(SkillRules.CanBuy(v, C.TumbleWings) == BuyBlock.Maxed, "Free levels count toward the cap");
v.Free[C.HeadBattery] = 7;
Check(v.Level(C.HeadBattery) == 3 && SkillRules.CanBuy(v, C.HeadBattery) == BuyBlock.Maxed, "Old vanilla levels above the cap show as maxed and block buying");

// ---------------------------------------------------------------- sanitising
var s = new SkillLoadout { Earned = 4 };
s.Bought["junk"] = 3;
s.Bought[C.Brawler] = 9;
s.Bought[C.MeleeDamage] = 2;
s.Bought[C.LaserShots] = 1; // no Gunsmith
s.Bought[C.ZoomOne] = -2;
SkillRules.Sanitize(s);
Check(!s.Bought.ContainsKey("junk") && !s.Bought.ContainsKey(C.ZoomOne), "Unknown and negative entries are dropped");
Check(s.Level(C.Brawler) == 1, "Levels are capped");
Check(s.Level(C.LaserShots) == 0, "Nodes without their prerequisite are removed");
Check(s.Available >= 0 && s.Level(C.MeleeDamage) == 2 && s.Spent == 3, "A valid remainder is kept");
var o = new SkillLoadout { Earned = 2 };
o.Bought[C.Brawler] = 1; o.Bought[C.MeleeDamage] = 3;
SkillRules.Sanitize(o);
Check(o.Spent <= 2 && o.Level(C.Brawler) == 1 && o.Level(C.MeleeDamage) == 1, "Overspending refunds leaf levels before their root");
var chain = new SkillLoadout { Earned = 10 };
chain.Bought[C.RespawnTimers] = 1; chain.Bought[C.MonsterDots] = 1;
SkillRules.Sanitize(chain);
Check(chain.Bought.Count == 0, "Removing a missing link removes everything below it");
var neg = new SkillLoadout { Earned = -5 };
SkillRules.Sanitize(neg);
Check(neg.Earned == 0, "Negative earned points are reset");
Check(SkillRules.Sanitize(new SkillLoadout()).Count == 0, "A clean loadout needs no fixes");

// ---------------------------------------------------------------- migration
var oldMax = new Dictionary<string, int> { ["IHM_MeleeDamage"] = 10, ["IHM_DroneBattery"] = 20, ["IHM_Armor"] = 10, ["IHM_Stamina"] = 5 };
var m = SkillMigration.Convert(new Dictionary<string, int> { ["IHM_Stamina"] = 3 }, oldMax);
Check(m.Loadout.Level(C.StaminaEfficiency) == 3 && m.Loadout.Level(C.Survivor) == 1, "Same-length ladder maps 1:1 and grants its root");
Check(m.Refund == 0 && m.Loadout.Available == 0 && m.Loadout.Earned == 4, "Granted levels are paid for with earned points");
m = SkillMigration.Convert(new Dictionary<string, int> { ["IHM_MeleeDamage"] = 3 }, oldMax);
Check(m.Loadout.Level(C.MeleeDamage) == 1 && m.Refund == 1 && m.Loadout.Available == 1, "Compressed ladder keeps the effect and refunds the remainder, rounded up");
m = SkillMigration.Convert(new Dictionary<string, int> { ["IHM_MeleeDamage"] = 10, ["IHM_DroneBattery"] = 20 }, oldMax);
Check(m.Loadout.Level(C.MeleeDamage) == 5 && m.Loadout.Level(C.DroneBattery) == 5 && m.Refund == 0, "Maxed old ladders become maxed nodes");
m = SkillMigration.Convert(new Dictionary<string, int> { ["IHM_Armor"] = 3 }, oldMax);
Check(m.Loadout.Level(C.Armor) == 1 && m.Refund == 1, "Armor 3/10 -> 1/5, half a 2-point level refunded as 1 point");
m = SkillMigration.Convert(new Dictionary<string, int> { ["IHM_MeleeDamage"] = 1 }, oldMax);
Check(m.Loadout.Level(C.MeleeDamage) == 0 && m.Loadout.Level(C.Brawler) == 0 && m.Refund == 1, "A level too small to convert becomes a point, with no root");
m = SkillMigration.Convert(new Dictionary<string, int> { ["IHM_MeleeDamage"] = 14 }, oldMax);
Check(m.Loadout.Level(C.MeleeDamage) == 5 && m.Refund == 2, "Levels above the old cap are refunded");
m = SkillMigration.Convert(new Dictionary<string, int> { ["IHM_PingInfo"] = 1 }, null);
Check(m.Loadout.Level(C.TargetInfo) == 1 && m.Loadout.Level(C.Ping) == 1 && m.Loadout.Earned == 3, "Ping Info alone also grants the Ping root");
m = SkillMigration.Convert(new Dictionary<string, int> { ["IHM_Map"] = 5 }, null);
Check(new[] { C.MapUplink, C.ActiveMonsters, C.RespawnTimers, C.QuotaHeading, C.ValuableTracker }.All(id => m.Loadout.Level(id) == 1)
      && m.Loadout.Level(C.MapPings) == 0, "Map Suite 5 unlocks its first five features");
m = SkillMigration.Convert(new Dictionary<string, int> { ["IHM_Map"] = 10 }, null);
Check(C.All.Where(n => n.Branch == "Map" && n.Id != C.BetterMap && n.Id != C.MapLandmarks).All(n => m.Loadout.Level(n.Id) == 1) && m.Loadout.Level(C.BetterMap) == 0 && m.Loadout.Level(C.MapLandmarks) == 0 && m.Loadout.Spent == 12, "Map Suite 10 unlocks the old Map branch (Better Map is new)");
m = SkillMigration.Convert(new Dictionary<string, int> { ["IHM_DeathProtect"] = 1, ["IHM_TruckHeal"] = 4 }, null);
Check(m.Loadout.Bought.Count == 0 && m.Loadout.Earned == 0, "Death Protection and truck upgrades are left out of the tree");
m = SkillMigration.Convert(new Dictionary<string, int>(), null, headBattery: 2, tumbleWings: 5);
Check(m.Loadout.Level(C.HeadBattery) == 2 && m.Loadout.Level(C.TumbleWings) == 3 && m.Loadout.Earned == 0, "Vanilla levels become free levels");
m = SkillMigration.Convert(new Dictionary<string, int> { ["IHM_Map"] = 10, ["IHM_MeleeDamage"] = 7, ["IHM_Armor"] = 5, ["IHM_InventorySlots"] = 4, ["IHM_PingInfo"] = 1 }, oldMax);
Check(SkillRules.Sanitize(m.Loadout).Count == 0 && m.Loadout.Available == m.Refund, "A migrated loadout is valid and only the refund is left to spend");

// ---------------------------------------------------------------- shop: which old upgrades the tree replaced
var covered = new[] { "IHM_Stamina", "IHM_HpRegen", "IHM_GunBatteryBullet", "IHM_GunBatteryLaser", "IHM_GunBatteryTranq", "IHM_DroneBattery",
    "IHM_MeleeDamage", "IHM_GrenadeDamage", "IHM_TumbleDamage", "IHM_Armor", "IHM_Overcharge", "IHM_Autoscan", "IHM_InventorySlots",
    "IHM_PingAnywhere", "IHM_PingInfo", "IHM_Map" };
Check(covered.All(SkillMigration.IsTreeCovered), "Every tree-covered old upgrade leaves the shop");
Check(new[] { "IHM_DeathProtect", "IHM_TruckHeal", "IHM_TruckCharge", "IHM_TruckSell", "IHM_ST_PointItem" }.All(id => !SkillMigration.IsTreeCovered(id)),
      "Death Protection, truck upgrades and the Skill Point stay in the shop");

// ---------------------------------------------------------------- skill boxes
int[] hits = new int[4];
for (int i = 0; i < 10000; i++) hits[SkillBoxRules.RollPoints((i + 0.5) / 10000, 65, 20, 10, 5)]++;
Check(hits[0] == 6500 && hits[1] == 2000 && hits[2] == 1000 && hits[3] == 500, "Skill boxes give 0/1/2/3 points 65/20/10/5% of the time");
Check(SkillBoxRules.RollPoints(0.99, 0, 0, 0, 0) == 0 && SkillBoxRules.RollPoints(1.5, 1, 1, 1, 1) == 3 &&
      SkillBoxRules.RollPoints(-1, 0, 5, 0, 0) == 1, "Box rolls stay in range with odd input");
var counts = new HashSet<int>();
for (int i = 0; i < 100; i++) counts.Add(SkillBoxRules.Count((i + 0.5) / 100, 1, 5));
Check(counts.SetEquals(new[] { 1, 2, 3, 4, 5 }), "A level gets 1 to 5 skill boxes");
Check(SkillBoxRules.Count(0.9, 3, 9) <= 5 && SkillBoxRules.Count(0.1, 4, 2) == 4 && SkillBoxRules.Count(0.5, 0, 0) == 0, "Box count is clamped to 0-5");

// ---------------------------------------------------------------- battery crystals on retried levels
string step = IncreaseHardMod.Modifiers.RetryCrystalRules.RunStep("save1", 4);
Check(IncreaseHardMod.Modifiers.RetryCrystalRules.IsRetry(step, IncreaseHardMod.Modifiers.RetryCrystalRules.RunStep("save1", 4)),
      "Replaying the same level step of the same save is a retry");
Check(!IncreaseHardMod.Modifiers.RetryCrystalRules.IsRetry(step, IncreaseHardMod.Modifiers.RetryCrystalRules.RunStep("save1", 5)) &&
      !IncreaseHardMod.Modifiers.RetryCrystalRules.IsRetry(step, IncreaseHardMod.Modifiers.RetryCrystalRules.RunStep("save2", 4)) &&
      !IncreaseHardMod.Modifiers.RetryCrystalRules.IsRetry(null, step) && !IncreaseHardMod.Modifiers.RetryCrystalRules.IsRetry(step, null),
      "A new level, another save, or a shop/lobby in between is not a retry");
var crystalCounts = new HashSet<int>();
for (int i = 0; i < 100; i++) crystalCounts.Add(IncreaseHardMod.Modifiers.RetryCrystalRules.Count((i + 0.5) / 100, 3, 8));
Check(crystalCounts.SetEquals(new[] { 3, 4, 5, 6, 7, 8 }) && IncreaseHardMod.Modifiers.RetryCrystalRules.Count(0.99, 3, 20) == 8,
      "A retried level gets 3 to 8 crystals, never more than 8");
Check(IncreaseHardMod.Modifiers.RetryCrystalRules.Emptiest(new[] { 80f, 20f, 100f }) == 1 &&
      IncreaseHardMod.Modifiers.RetryCrystalRules.Emptiest(new[] { 100f, 100f }) == -1 &&
      IncreaseHardMod.Modifiers.RetryCrystalRules.Emptiest(new float[0]) == -1, "A crystal picks the emptiest battery, none when all are full");
Check(Math.Abs(IncreaseHardMod.Modifiers.RetryCrystalRules.Charge(10f, 20f) - 30f) < .001f &&
      IncreaseHardMod.Modifiers.RetryCrystalRules.Charge(90f, 20f) == 100f, "A crystal adds 20% and never overfills");

// ---------------------------------------------------------------- newer perks
Check(PerkRules.DesperationReduction(29, 100, 3, .1f, .3f) > .299f && PerkRules.DesperationReduction(30, 100, 3, .1f, .3f) == 0f &&
      PerkRules.DesperationReduction(10, 100, 0, .1f, .3f) == 0f, "Desperation: -10% per level only under 30% health");
Check(PerkRules.DesperationReduction(5, 100, 20, .1f, .3f) <= .9f, "Desperation never blocks more than 90%");
Check(PerkRules.Reduce(10, .3f) == 7 && PerkRules.Reduce(1, .5f) == 1 && PerkRules.Reduce(0, .5f) == 0, "Reduced hits round in the player's favour but still hurt");
Check(PerkRules.MeleeStun(2f, 3, .5f, 8f) == 3.5f && PerkRules.MeleeStun(2f, 0, .5f, 8f) == 2f && PerkRules.MeleeStun(7f, 3, .5f, 8f) == 8f &&
      PerkRules.MeleeStun(10f, 3, .5f, 8f) == 10f, "Heavy Hitter: +0.5 s per level, 8 s cap, never shortens a stun");
Check(Math.Abs(PerkRules.BatteryDrainMultiplier(3, .15f) - .55f) < .001f && PerkRules.BatteryDrainMultiplier(0, .15f) == 1f &&
      PerkRules.BatteryDrainMultiplier(10, .2f) == .25f, "Battery Saver: 15% slower per level, never below a quarter");
Check(PerkRules.MedicSelfHeal(50, 3, .15f) == 73 && PerkRules.MedicShare(50, 3, .15f) == 23 && PerkRules.MedicShare(50, 0, .15f) == 0 &&
      PerkRules.MedicSelfHeal(50, 0, .15f) == 50, "Field Medic: bigger self heal and a share for nearby teammates");
Check(PerkRules.LuckyWeights(65, 20, 10, 5, 1, 10).SequenceEqual(new[] { 55, 25, 13, 7 }) &&
      PerkRules.LuckyWeights(65, 20, 10, 5, 2, 10).SequenceEqual(new[] { 45, 30, 16, 9 }) &&
      PerkRules.LuckyWeights(65, 20, 10, 5, 0, 10).SequenceEqual(new[] { 65, 20, 10, 5 }), "Lucky Finder shifts skill box odds toward points");
Check(PerkRules.LuckyWeights(5, 0, 0, 0, 2, 10).Sum() == 5 && PerkRules.LuckyWeights(5, 0, 0, 0, 2, 10)[0] == 0, "Lucky Finder keeps the total and never goes negative");
Check(PerkRules.LuckyCrystalCharge(20f, 2, 5f) == 30f && PerkRules.LuckyCrystalCharge(20f, 0, 5f) == 20f, "Lucky Finder: +5% crystal charge per level");

Console.WriteLine($"All {checks} skill tree checks passed.");
