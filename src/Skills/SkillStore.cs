using System;
using System.Collections.Generic;
using HarmonyLib;
using IncreaseHardMod.Hud;
using IncreaseHardMod.Networking;
using Photon.Pun;
using UnityEngine;
using RepoUpgrades = REPOLib.Modules.Upgrades;

namespace IncreaseHardMod.Skills;

/// <summary>
/// Game-side storage of the skill tree. Every node's bought levels, the player's earned points and a one-time
/// migration flag are hidden REPOLib player upgrades with no shop item: REPOLib keeps them per Steam id in the
/// save and syncs every change to all players. The host is the only writer; clients ask it through
/// <see cref="RequestBuy"/> / <see cref="RequestRespec"/> and it answers with a result toast.
/// Vanilla nodes (Tumble Wings, Death Head Battery) also move the game's own upgrade through its upgrade RPC,
/// so the vanilla effect, save and sync keep working unchanged. Levels the player owned before the tree
/// (vanilla stat above what the tree bought) count as free levels.
/// </summary>
internal static class SkillStore
{
    internal const string Prefix = "IHM_ST_";
    internal const string PointsId = Prefix + "Points";
    internal const string MigratedId = Prefix + "Migrated";
    internal const byte OpBuy = 0, OpRespec = 1, OpQuestPoints = 2, OpTradeOffReset = 3;
    /// <summary>The shop item that grants a point.</summary>
    internal const string PointItemId = Prefix + "PointItem";

    /// <summary>Raised on every client when any player's tree or points change (argument: Steam id).</summary>
    internal static event Action<string> Changed;

    private static bool _registered;

    /// <summary>Called once from plugin load, before REPOLib binds upgrade dictionaries on RunStartStats.</summary>
    internal static void Register()
    {
        if (_registered || !ModConfig.PlayerUpgradesEnabled.Value) return;
        _registered = true;
        foreach (var node in SkillCatalog.All) RepoUpgrades.RegisterUpgrade(node.Id, null, null, OnChanged);
        RepoUpgrades.RegisterUpgrade(PointsId, null, null, OnChanged);
        RepoUpgrades.RegisterUpgrade(MigratedId, null, null, null);
        RepoUpgrades.RegisterUpgrade(SkillEffects.BeamColourChoiceId, null, null, null);

        // The shop's Skill Point: an upgrade card that gives +1 point to whoever uses it. Repurchasable; the game
        // raises its price with every copy the team buys, like any upgrade.
        Upgrades.PlayerUpgradeRegistry.Define(new Upgrades.PlayerUpgradeRegistry.Definition
        {
            Id = PointItemId,
            DisplayName = "Skill Point",
            Description = "+1 skill point for whoever uses it. Spend points in the skill tree (shop or truck).",
            MaxLevel = 2,
            Consumable = true,
            ShopCopies = 2,
            BaseShopPrice = ModConfig.SkillPointBasePrice.Value,
            IconFile = "Upgrade_Skill-Point.png",
            OnUse = avatar => GrantPoints(avatar.steamID, 1, "shop Skill Point"),
        });
        Plugin.Log.LogInfo($"[Skills] Registered {SkillCatalog.All.Count} tree nodes ({SkillCatalog.TotalCost()} points to max).");
    }

    private static void OnChanged(PlayerAvatar avatar, int level)
    {
        if (avatar == null) return;
        try { Changed?.Invoke(avatar.steamID); }
        catch (Exception error) { Plugin.Log.LogWarning("[Skills] Change listener failed: " + error.Message); }
    }

    /// <summary>True once REPOLib has bound our dictionaries to the loaded save (reads and writes are real).</summary>
    internal static bool Ready
    {
        get
        {
            var stats = StatsManager.instance;
            return _registered && stats != null && RepoUpgrades.TryGetUpgrade(MigratedId, out var upgrade) &&
                   stats.dictionaryOfDictionaries.TryGetValue("playerUpgrade" + MigratedId, out var saved) &&
                   ReferenceEquals(saved, upgrade.PlayerDictionary);
        }
    }

    // ------------------------------------------------------------------ reading (any client)

    private static int Raw(string id, string steamId) =>
        !string.IsNullOrEmpty(steamId) && RepoUpgrades.TryGetUpgrade(id, out var upgrade) ? Math.Max(0, upgrade.GetLevel(steamId)) : 0;

    internal static int VanillaLevel(VanillaUpgrade kind, string steamId)
    {
        var stats = StatsManager.instance;
        if (stats == null || string.IsNullOrEmpty(steamId)) return 0;
        var levels = kind switch
        {
            VanillaUpgrade.DeathHeadBattery => stats.playerUpgradeDeathHeadBattery,
            VanillaUpgrade.TumbleWings => stats.playerUpgradeTumbleWings,
            _ => null,
        };
        return levels != null && levels.TryGetValue(steamId, out var level) ? Math.Max(0, level) : 0;
    }

    internal static int EarnedPoints(string steamId) => Raw(PointsId, steamId);

    /// <summary>The level a perk's effect should use for this player.</summary>
    internal static int Level(string steamId, string nodeId)
    {
        var node = SkillCatalog.Find(nodeId);
        if (node == null) return 0;
        int bought = Raw(nodeId, steamId);
        int free = node.Vanilla != VanillaUpgrade.None ? Math.Max(0, VanillaLevel(node.Vanilla, steamId) - bought) : 0;
        return Math.Min(node.MaxLevel, bought + free);
    }

    internal static int Level(PlayerAvatar avatar, string nodeId) => avatar != null ? Level(avatar.steamID, nodeId) : 0;
    internal static int LocalLevel(string nodeId) => Level(SemiFunc.PlayerAvatarLocal(), nodeId);

    internal static SkillLoadout LoadoutOf(string steamId)
    {
        var loadout = new SkillLoadout { Earned = EarnedPoints(steamId) };
        foreach (var node in SkillCatalog.All)
        {
            int bought = Raw(node.Id, steamId);
            if (bought > 0) loadout.Bought[node.Id] = bought;
            if (node.Vanilla != VanillaUpgrade.None)
            {
                int free = Math.Max(0, VanillaLevel(node.Vanilla, steamId) - bought);
                if (free > 0) loadout.Free[node.Id] = free;
            }
        }
        return loadout;
    }

    internal static SkillLoadout LocalLoadout() => LoadoutOf(SemiFunc.PlayerAvatarLocal()?.steamID);

    internal static bool SpendAllowedHere() => ModConfig.SkillSpendAnywhere.Value || SemiFunc.RunIsShop() || SemiFunc.RunIsLobby();

    // ------------------------------------------------------------------ requests (any client)

    internal static void RequestBuy(string nodeId, int count = 1) => Request(OpBuy, nodeId, count);
    internal static void RequestRespec() => Request(OpRespec, "", 1);
    /// <summary>Refund only the trade-off skills (cheaper than a full refund).</summary>
    internal static void RequestTradeOffReset() => Request(OpTradeOffReset, "", 1);
    /// <summary>A finished personal quest's skill points (tracked on this client; the host pays and caps them).</summary>
    internal static void RequestQuestPoints(int points) => Request(OpQuestPoints, "", Math.Max(1, Math.Min(10, points)));

    private static void Request(byte op, string nodeId, int count)
    {
        var local = SemiFunc.PlayerAvatarLocal();
        if (local == null) return;
        if (SemiFunc.IsMasterClientOrSingleplayer()) Report(op, HostHandle(local.steamID, op, nodeId, count), nodeId);
        else if (ModNetwork.Instance != null) ModNetwork.Instance.SendSkillRequest(op, nodeId ?? "", count);
    }

    /// <summary>Host: one player's request. Returns why it failed, or None.</summary>
    internal static BuyBlock HostHandle(string steamId, byte op, string nodeId, int count) =>
        op == OpRespec ? HostRespec(steamId) : op == OpQuestPoints ? HostQuestPoints(steamId, count) :
        op == OpTradeOffReset ? HostTradeOffReset(steamId) : HostBuy(steamId, nodeId, count);

    // Personal quests are tracked by each player's own client, so the host can't check them; it caps what it pays
    // per player per level instead (two personal quests' worth at the highest rank).
    private const int PersonalQuestsPerLevel = 2;
    private static readonly Dictionary<string, int> PersonalPaid = new(StringComparer.Ordinal);
    private static string _paidLevel;

    internal static BuyBlock HostQuestPoints(string steamId, int count)
    {
        if (!Host || !Ready || string.IsNullOrEmpty(steamId))
        {
            Plugin.Log.LogWarning($"[Skills] Personal-quest point(s) for {steamId ?? "an unknown player"} not paid: the tree is not ready.");
            return BuyBlock.NotReady;
        }
        var run = RunManager.instance;
        string level = run != null && run.levelCurrent != null ? run.levelsCompleted + ":" + run.levelCurrent.name : "";
        if (level != _paidLevel) { PersonalPaid.Clear(); _paidLevel = level; }
        int cap = PersonalQuestsPerLevel * Math.Max(ModConfig.QuestSkillPoints.Value, ModConfig.QuestSkillPointsHighRank.Value);
        PersonalPaid.TryGetValue(steamId, out int paid);
        int grant = Math.Min(Math.Max(0, count), cap - paid);
        if (grant <= 0)
        {
            Plugin.Log.LogWarning($"[Skills] {steamId} asked for {count} personal-quest point(s) over this level's cap of {cap}; ignored.");
            return BuyBlock.Maxed;
        }
        PersonalPaid[steamId] = paid + grant;
        GrantPoints(steamId, grant, "personal quest");
        return BuyBlock.None;
    }

    /// <summary>Shows the requesting player what happened (their own client).</summary>
    internal static void Report(byte op, BuyBlock block, string nodeId)
    {
        if (op == OpQuestPoints) return; // the quest's own completion toast already said it
        var node = SkillCatalog.Find(nodeId);
        bool ru = SkillText.Russian;
        string name = node != null ? SkillText.Name(node) : ru ? "навык" : "perk";
        string text = ru ? block switch
        {
            BuyBlock.None when op == OpRespec => "Очки навыков возвращены",
            BuyBlock.None when op == OpTradeOffReset => "Очки за компромиссы возвращены",
            BuyBlock.NothingSpent when op == OpTradeOffReset => "Нет компромиссов для сброса",
            BuyBlock.NoMoney when op == OpTradeOffReset => $"Сброс компромиссов стоит ${ModConfig.SkillTradeOffResetCost.Value}K",
            BuyBlock.WrongPlace when op == OpTradeOffReset => "Сбросить компромиссы можно в магазине или грузовике",
            BuyBlock.None => $"Изучено: «{name}» ({LocalLevel(nodeId)}/{node?.MaxLevel})",
            BuyBlock.Maxed => $"«{name}» уже на максимуме",
            BuyBlock.MissingPrerequisite => $"Для «{name}» нужен предыдущий навык",
            BuyBlock.NotEnoughPoints => $"Не хватает очков для «{name}»",
            BuyBlock.WrongPlace => op == OpRespec ? "Вернуть очки можно в магазине или грузовике" : "Тратить очки можно в магазине или грузовике",
            BuyBlock.NoMoney => $"Сброс стоит ${ModConfig.SkillRespecCost.Value}K",
            BuyBlock.NothingSpent => "Очки навыков ещё не потрачены",
            BuyBlock.NotReady => "Дерево навыков ещё загружается",
            BuyBlock.QuestOnly => $"«{name}» даётся только за особые контракты",
            BuyBlock.GateNotMet => $"Для «{name}» нужно больше очков в навыках этой страницы",
            BuyBlock.TierTaken => "На этом уровне уже выбран компромисс (сбросьте компромиссы, чтобы поменять)",
            BuyBlock.NeedLowerTier => $"Для «{name}» сначала нужен компромисс уровнем ниже",
            _ => "Это изучить нельзя",
        } : block switch
        {
            BuyBlock.None when op == OpRespec => "Skill points refunded",
            BuyBlock.None when op == OpTradeOffReset => "Trade-off points refunded",
            BuyBlock.NothingSpent when op == OpTradeOffReset => "No trade-off skills to reset",
            BuyBlock.NoMoney when op == OpTradeOffReset => $"Trade-off reset costs ${ModConfig.SkillTradeOffResetCost.Value}K",
            BuyBlock.WrongPlace when op == OpTradeOffReset => "Reset trade-offs in the shop or truck",
            BuyBlock.None => $"Learned {name} ({LocalLevel(nodeId)}/{node?.MaxLevel})",
            BuyBlock.Maxed => $"{name} is already maxed",
            BuyBlock.MissingPrerequisite => $"{name} needs the perk before it",
            BuyBlock.NotEnoughPoints => $"Not enough skill points for {name}",
            BuyBlock.WrongPlace => op == OpRespec ? "Refund points in the shop or truck" : "Spend points in the shop or truck",
            BuyBlock.NoMoney => $"Refund costs ${ModConfig.SkillRespecCost.Value}K",
            BuyBlock.NothingSpent => "No skill points spent yet",
            BuyBlock.NotReady => "Skill tree is still loading",
            BuyBlock.QuestOnly => $"{name} comes only from special contracts",
            BuyBlock.GateNotMet => $"{name} needs more points in this page's perks",
            BuyBlock.TierTaken => "You already hold a trade-off on this tier (reset trade-offs to swap)",
            BuyBlock.NeedLowerTier => $"{name} needs a trade-off from the tier below first",
            _ => "Can't learn that",
        };
        HudNotify.Toast(text, block == BuyBlock.None ? HudStyle.Good : HudStyle.Bad, 2.5f, "skills");
    }

    // ------------------------------------------------------------------ host writes

    private static bool Host => SemiFunc.IsMasterClientOrSingleplayer();

    private static void Write(string id, string steamId, int level)
    {
        if (RepoUpgrades.TryGetUpgrade(id, out var upgrade)) upgrade.SetLevel(steamId, Math.Max(0, level));
    }

    /// <summary>Host: give a player skill points (shop item, quest reward).</summary>
    internal static void GrantPoints(string steamId, int amount, string reason)
    {
        if (!Host || !Ready || amount <= 0 || string.IsNullOrEmpty(steamId)) return;
        Write(PointsId, steamId, EarnedPoints(steamId) + amount);
        Plugin.Log.LogInfo($"[Skills] +{amount} point(s) to {steamId} ({reason}); earned {EarnedPoints(steamId)}.");
    }

    /// <summary>Host: a finished special contract gives this player one level of a quest-only perk. False if maxed.</summary>
    internal static bool GrantQuestPerk(string steamId, string nodeId)
    {
        if (!Host || !Ready || string.IsNullOrEmpty(steamId)) return false;
        var loadout = LoadoutOf(steamId);
        if (!SkillRules.GrantQuestLevel(loadout, nodeId)) return false;
        Write(nodeId, steamId, loadout.BoughtLevel(nodeId));
        Plugin.Log.LogInfo($"[Skills] {steamId} earned {nodeId} -> {loadout.Level(nodeId)} from a contract.");
        return true;
    }

    internal static BuyBlock HostBuy(string steamId, string nodeId, int count)
    {
        if (!Host || !Ready || string.IsNullOrEmpty(steamId)) return BuyBlock.NotReady;
        if (!SpendAllowedHere()) return BuyBlock.WrongPlace;
        var loadout = LoadoutOf(steamId);
        var block = SkillRules.Buy(loadout, nodeId, count);
        if (block != BuyBlock.None) return block;
        Write(nodeId, steamId, loadout.BoughtLevel(nodeId));
        var node = SkillCatalog.Find(nodeId);
        if (node.Vanilla != VanillaUpgrade.None) VanillaAdd(node.Vanilla, steamId, count);
        Plugin.Log.LogInfo($"[Skills] {steamId} bought {nodeId} x{count} -> {loadout.Level(nodeId)}/{node.MaxLevel}; {loadout.Available} point(s) left.");
        return BuyBlock.None;
    }

    /// <summary>Host: refund all of a player's bought levels for the configured fee. Never during a level.</summary>
    internal static BuyBlock HostRespec(string steamId)
    {
        if (!Host || !Ready || string.IsNullOrEmpty(steamId)) return BuyBlock.NotReady;
        if (!SemiFunc.RunIsShop() && !SemiFunc.RunIsLobby()) return BuyBlock.WrongPlace;
        var loadout = LoadoutOf(steamId);
        if (loadout.Spent <= 0) return BuyBlock.NothingSpent;
        int fee = Math.Max(0, ModConfig.SkillRespecCost.Value);
        if (fee > 0)
        {
            int money = SemiFunc.StatGetRunCurrency();
            if (money < fee) return BuyBlock.NoMoney;
            SemiFunc.StatSetRunCurrency(money - fee);
        }
        foreach (var pair in loadout.Bought)
        {
            var node = SkillCatalog.Find(pair.Key);
            if (node != null && node.QuestOnly) continue; // earned from contracts, not bought: kept
            Write(pair.Key, steamId, 0);
            if (node != null && node.Vanilla != VanillaUpgrade.None) VanillaAdd(node.Vanilla, steamId, -pair.Value);
        }
        Plugin.Log.LogInfo($"[Skills] {steamId} refunded {loadout.Spent} point(s) for ${fee}K.");
        return BuyBlock.None;
    }

    /// <summary>Host: refund only a player's trade-off skills, for the (smaller) trade-off reset fee. Never during a level.</summary>
    internal static BuyBlock HostTradeOffReset(string steamId)
    {
        if (!Host || !Ready || string.IsNullOrEmpty(steamId)) return BuyBlock.NotReady;
        if (!SemiFunc.RunIsShop() && !SemiFunc.RunIsLobby()) return BuyBlock.WrongPlace;
        var loadout = LoadoutOf(steamId);
        int spent = SkillRules.TradeOffSpent(loadout);
        if (spent <= 0) return BuyBlock.NothingSpent;
        int fee = Math.Max(0, ModConfig.SkillTradeOffResetCost.Value);
        if (fee > 0)
        {
            int money = SemiFunc.StatGetRunCurrency();
            if (money < fee) return BuyBlock.NoMoney;
            SemiFunc.StatSetRunCurrency(money - fee);
        }
        foreach (var pair in loadout.Bought)
            if (SkillCatalog.Find(pair.Key)?.Tier > 0) Write(pair.Key, steamId, 0);
        Plugin.Log.LogInfo($"[Skills] {steamId} reset trade-offs: refunded {spent} point(s) for ${fee}K.");
        return BuyBlock.None;
    }

    // The game's own upgrade path (the one its tester command uses): changes the stat on every client.
    private static void VanillaAdd(VanillaUpgrade kind, string steamId, int amount)
    {
        var pun = PunManager.instance;
        if (pun == null || amount == 0) return;
        string name = kind == VanillaUpgrade.DeathHeadBattery ? "DeathHeadBattery" : "TumbleWings";
        if (SemiFunc.IsMultiplayer()) pun.photonView.RPC("TesterUpgradeCommandRPC", RpcTarget.All, steamId, name, amount);
        else pun.TesterUpgradeCommandRPC(steamId, name, amount);
    }

    /// <summary>Host: convert a player's old shop upgrades into tree nodes, once per save (see <see cref="SkillMigration"/>).</summary>
    internal static void HostMigrate(string steamId)
    {
        if (!Host || !Ready || string.IsNullOrEmpty(steamId) || Raw(MigratedId, steamId) > 0) return;
        var old = new Dictionary<string, int>(StringComparer.Ordinal);
        var max = new Dictionary<string, int>(StringComparer.Ordinal);
        void Collect(string id)
        {
            int level = Raw(id, steamId);
            if (level > 0) old[id] = level;
            var definition = Upgrades.PlayerUpgradeRegistry.FindDefinition(id);
            if (definition != null) max[id] = definition.MaxLevel;
        }
        foreach (var (oldId, _) in SkillMigration.Ladders) Collect(oldId);
        Collect(SkillMigration.OldMapId);

        var result = SkillMigration.Convert(old, max);
        foreach (var pair in result.Loadout.Bought) Write(pair.Key, steamId, pair.Value);
        if (result.Loadout.Earned > 0) Write(PointsId, steamId, EarnedPoints(steamId) + result.Loadout.Earned);
        Write(MigratedId, steamId, 1);
        Plugin.Log.LogInfo(old.Count == 0
            ? $"[Skills] {steamId}: no old upgrades to convert."
            : $"[Skills] {steamId}: converted old upgrades, {result.Loadout.Earned} point(s) granted ({result.Refund} free to spend): " +
              string.Join("; ", result.Lines));
    }

    // Photon actor number -> Steam id, kept by the host while players are in the run, so a request that arrives
    // while the level unloads (avatars gone) can still be credited to the right player.
    private static readonly Dictionary<int, string> ActorSteam = new();

    internal static string SteamIdOfActor(int actor) => ActorSteam.TryGetValue(actor, out var id) ? id : null;

    private static void RememberActors()
    {
        var players = SemiFunc.PlayerGetList();
        if (players == null) return;
        foreach (var player in players)
            if (player != null && !string.IsNullOrEmpty(player.steamID) && player.photonView != null && player.photonView.Owner != null)
                ActorSteam[player.photonView.Owner.ActorNumber] = player.steamID;
    }

    /// <summary>Host: migrates every player in the run as soon as the save is bound. Lives on the runtime object.</summary>
    internal sealed class HostController : MonoBehaviour
    {
        private float _next;
        private readonly HashSet<string> _done = new(StringComparer.Ordinal);
        private StatsManager _stats;

        private void Update()
        {
            if (Time.unscaledTime < _next) return;
            _next = Time.unscaledTime + 1f;
            if (Host) RememberActors();
            if (!Host || !Ready) return;
            if (!SemiFunc.RunIsLevel() && !SemiFunc.RunIsShop() && !SemiFunc.RunIsLobby()) return;
            if (_stats != StatsManager.instance) { _stats = StatsManager.instance; _done.Clear(); }
            var players = SemiFunc.PlayerGetList();
            if (players == null) return;
            foreach (var player in players)
            {
                string steamId = player != null ? player.steamID : null;
                if (string.IsNullOrEmpty(steamId) || !_done.Add(steamId)) continue;
                try { HostMigrate(steamId); }
                catch (Exception error) { _done.Remove(steamId); Plugin.Log.LogError($"[Skills] Migration for {steamId} failed: {error}"); }
            }
        }
    }

    // The truck's upgrade screen lists every "playerUpgrade" dictionary; tree nodes and counters are shown in the tree.
    [HarmonyPatch(typeof(StatsManager), nameof(StatsManager.FetchPlayerUpgrades))]
    private static class HideFromUpgradeScreen
    {
        private const string Key = "playerUpgrade" + Prefix;

        // Also hides the old shop upgrades the tree replaced: their saved levels no longer do anything.
        [HarmonyPostfix]
        private static void Postfix(Dictionary<string, int> __result)
        {
            if (__result == null) return;
            List<string> drop = null;
            foreach (var key in __result.Keys)
                if (key.StartsWith(Key, StringComparison.Ordinal) ||
                    key.StartsWith("playerUpgrade", StringComparison.Ordinal) && SkillMigration.IsTreeCovered(key.Substring("playerUpgrade".Length)))
                    (drop ??= new List<string>()).Add(key);
            if (drop != null) foreach (var key in drop) __result.Remove(key);
        }
    }
}
