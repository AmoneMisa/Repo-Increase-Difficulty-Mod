using IncreaseHardMod.Skills;
using C = IncreaseHardMod.Skills.SkillCatalog;

// Engine-free checks of the skill tree model (src/Skills): catalogue, buy rules, sanitising and save migration.
int checks = 0;
void Check(bool ok, string name) { if (!ok) throw new Exception("FAIL " + name); Console.WriteLine("PASS " + name); checks++; }

// ---------------------------------------------------------------- catalogue
Check(C.PageCost(SkillPage.Recon) == 28 && C.PageCost(SkillPage.Combat) == 43 &&
      C.PageCost(SkillPage.Survival) == 27 && C.PageCost(SkillPage.Utility) == 23 && C.PageCost(SkillPage.Arsenal) == 39, "Page costs match the plan");
Check(C.TotalCost() == 160, "Whole tree costs 160 points");
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
Check(C.All.Where(n => n.QuestOnly).All(n => n.Page == SkillPage.Contracts) && C.All.Count(n => n.QuestOnly) == 4, "The four quest-only perks sit on the Contracts page");
Check(C.QuestPerk(0)?.Id == C.CartHandling && C.QuestPerkIndex(C.SoftSteps) == 3 && C.QuestPerk(9) == null, "Quest perks are addressed by index");
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
Check(new[] { C.MapUplink, C.DeadPlayers, C.ActiveMonsters, C.RespawnTimers, C.QuotaHeading, C.ValuableTracker }.All(id => m.Loadout.Level(id) == 1)
      && m.Loadout.Level(C.MapPings) == 0, "Map Suite 5 unlocks its first five features");
m = SkillMigration.Convert(new Dictionary<string, int> { ["IHM_Map"] = 10 }, null);
Check(C.All.Where(n => n.Branch == "Map").All(n => m.Loadout.Level(n.Id) == 1) && m.Loadout.Spent == 13, "Map Suite 10 unlocks the whole Map branch");
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

Console.WriteLine($"All {checks} skill tree checks passed.");
