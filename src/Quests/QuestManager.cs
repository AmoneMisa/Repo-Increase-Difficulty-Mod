using System.Collections.Generic;
using IncreaseHardMod.Networking;
using Photon.Pun;
using UnityEngine;

namespace IncreaseHardMod.Quests;

/// <summary>
/// The quest system's controller. Authority model mirrors the rest of the mod:
///
/// - TEAM quests are HOST-AUTHORITATIVE. The host generates them at level start, tracks
///   progress from host-only events (enemy deaths, pit kills, player deaths, extraction),
///   grants their rewards, and broadcasts the whole set so every client's HUD matches.
///   Each client credits its own local quest rank when it sees a team quest complete, so
///   ranks reflect who was actually present.
///
/// - PERSONAL quests (currently "take no damage") are tracked locally by each player for
///   their own character, completed at level end, and their money reward is applied by the
///   host (the local player asks the host over the network when it isn't the host itself).
///
/// State is plain static data driven by ModNetwork.Update (a persistent object), the quest
/// Harmony patches, and the HUD.
/// </summary>
public static class QuestManager
{
    private static readonly List<ActiveQuest> _teamQuests = new();
    private static ActiveQuest _personal;

    // Signatures completed earlier in THIS run, so we don't re-offer the same quest.
    private static readonly HashSet<string> _usedSignatures = new();

    // ValuableObject instance IDs already credited to intact-extract quests this level, so the
    // per-item and final sweep hooks (which overlap) can't double-count the same valuable.
    private static readonly HashSet<int> _extractedThisLevel = new();

    // A valuable counts as "intact" only if it reaches the tube with (essentially) its full value;
    // any impact damage drops dollarValueCurrent below dollarValueOriginal.
    private const float IntactValueEpsilon = 0.5f;

    private static int _teamDeaths;
    private static bool _levelActive;
    private static float _broadcastCooldown;
    private static bool _dirty;

    public static IReadOnlyList<ActiveQuest> TeamQuests => _teamQuests;
    public static ActiveQuest PersonalQuest => _personal;

    private static string LocalSteamId()
    {
        var me = PlayerAvatar.instance;
        return me != null ? SemiFunc.PlayerGetSteamID(me) : null;
    }

    public static QuestRank LocalRank() => QuestRankProgress.GetRank(LocalSteamId());
    public static int LocalRankProgress() => QuestRankProgress.ProgressIntoRank(LocalSteamId());
    public static int LocalRankRequirement() => QuestRankProgress.RequiredForNextRank(LocalSteamId());

    // ---- Level lifecycle -------------------------------------------------

    /// <summary>Host generates the team set; every client rolls its own personal quest.</summary>
    public static void OnLevelGenerated()
    {
        if (!ModConfig.QuestsEnabled.Value)
            return;

        _teamDeaths = 0;
        _levelActive = true;
        _extractedThisLevel.Clear();

        var ctx = BuildContext();

        // New run -> forget the "already done" list.
        if (ctx.Day <= 0)
            _usedSignatures.Clear();

        // Personal quest is local to every player.
        _personal = QuestCatalog.BuildPersonal(ctx);

        if (SemiFunc.IsMasterClientOrSingleplayer())
        {
            _teamQuests.Clear();
            var generated = QuestCatalog.GenerateTeam(ctx, _usedSignatures);
            _teamQuests.AddRange(generated);
            foreach (var q in _teamQuests)
                Plugin.Log.LogInfo($"[Quests] Team quest [{QuestRankInfo.Label(q.Rank)}] {q.Title} -> {RewardText(q)}");
            Broadcast();
        }
    }

    /// <summary>
    /// Host prefix on ChangeLevel: finalize survival quests before the level flips.
    /// <paramref name="completed"/> is the game's own _completedLevel flag.
    /// </summary>
    public static void OnLevelCompleted(bool completed)
    {
        if (!ModConfig.QuestsEnabled.Value || !_levelActive)
            return;
        _levelActive = false;

        // Any temp-stat buffs granted this level expire now (each client reverts its own player).
        QuestTempStats.RevertAll();

        // Personal "no damage": completes locally on a clean, completed level.
        if (_personal != null && _personal.Objective == ObjectiveType.NoDamage)
        {
            if (completed && !_personal.Failed && !_personal.Completed)
            {
                _personal.Completed = true;
                OnPersonalCompleted(_personal);
            }
        }

        if (!SemiFunc.IsMasterClientOrSingleplayer())
            return;

        // Team survival quests resolve now (host-side).
        foreach (var q in _teamQuests)
        {
            if (q.Completed || q.Failed)
                continue;
            if (q.Objective == ObjectiveType.NoDeaths)
            {
                if (completed && _teamDeaths <= q.Target)
                    CompleteTeamQuest(q);
                else
                    q.Failed = true;
            }
            else if (q.Objective == ObjectiveType.NoGunKills)
            {
                // Not failed by a gun kill during the level and the level was completed -> success.
                if (completed && !q.Failed)
                    CompleteTeamQuest(q);
                else
                    q.Failed = true;
            }
        }
        Broadcast();
    }

    /// <summary>Wipe all quest state (returning to menu / new run boundary handling).</summary>
    public static void ResetAll()
    {
        _teamQuests.Clear();
        _personal = null;
        _teamDeaths = 0;
        _levelActive = false;
    }

    // ---- Host-side event feeds ------------------------------------------

    public static void OnEnemyKilled(EnemyParent enemyParent, WeaponSource weapon)
    {
        if (!ModConfig.QuestsEnabled.Value || !SemiFunc.IsMasterClientOrSingleplayer())
            return;

        string name = enemyParent != null ? enemyParent.enemyName : null;
        int tier = enemyParent != null ? (int)enemyParent.difficulty : -1;

        foreach (var q in _teamQuests)
        {
            switch (q.Objective)
            {
                case ObjectiveType.KillAny:
                    Advance(q, 1);
                    break;
                case ObjectiveType.KillType:
                    if (name != null && string.Equals(name, q.Param, System.StringComparison.OrdinalIgnoreCase))
                        Advance(q, 1);
                    break;
                case ObjectiveType.KillTier:
                    if (tier >= 0 && q.Param == tier.ToString())
                        Advance(q, 1);
                    break;
                case ObjectiveType.MeleeKill:
                    if (weapon == WeaponSource.Melee)
                        Advance(q, 1);
                    break;
                case ObjectiveType.NoGunKills:
                    // "Melee only": any gun kill disqualifies this survival quest.
                    if (weapon == WeaponSource.Gun && !q.Completed)
                    {
                        q.Failed = true;
                        _dirty = true;
                    }
                    break;
            }
        }
    }

    public static void OnPitKill()
    {
        if (!ModConfig.QuestsEnabled.Value || !SemiFunc.IsMasterClientOrSingleplayer())
            return;
        foreach (var q in _teamQuests)
            if (q.Objective == ObjectiveType.PitKill)
                Advance(q, 1);
    }

    public static void OnPlayerDeath()
    {
        if (!ModConfig.QuestsEnabled.Value || !SemiFunc.IsMasterClientOrSingleplayer())
            return;
        _teamDeaths++;
        // Eagerly fail any survival quest already over its threshold so the HUD reflects it.
        foreach (var q in _teamQuests)
            if (q.Objective == ObjectiveType.NoDeaths && !q.Completed && _teamDeaths > q.Target)
                q.Failed = true;
        _dirty = true;
    }

    public static void OnExtractionSuccess()
    {
        if (!ModConfig.QuestsEnabled.Value || !SemiFunc.IsMasterClientOrSingleplayer())
            return;

        var rd = RoundDirector.instance;
        if (rd == null)
            return;

        foreach (var q in _teamQuests)
        {
            if (q.Objective == ObjectiveType.ReachHaul && !q.Completed)
            {
                q.Progress = Mathf.Min(q.Target, rd.totalHaul);
                if (q.Progress >= q.Target)
                    CompleteTeamQuest(q);
            }
        }
        _dirty = true;
    }

    /// <summary>
    /// Host-side: a single valuable is about to be sucked into the extraction tube. Called from the
    /// extraction-point haul-list hooks BEFORE the object is destroyed, so its dollar value is still
    /// readable. If it arrived undamaged (full value) it credits every intact-extract quest. The
    /// per-item and final-sweep hooks overlap, so we de-dupe by instance ID.
    /// </summary>
    public static void OnValuableExtracted(ValuableObject valuable)
    {
        if (!ModConfig.QuestsEnabled.Value || !SemiFunc.IsMasterClientOrSingleplayer() || valuable == null)
            return;

        // Nothing to do if no intact-extract quest is live.
        bool anyIntact = false;
        foreach (var q in _teamQuests)
            if (q.Objective == ObjectiveType.IntactExtract && !q.Completed && !q.Failed)
            {
                anyIntact = true;
                break;
            }
        if (!anyIntact)
            return;

        if (!_extractedThisLevel.Add(valuable.GetInstanceID()))
            return;   // already counted this valuable

        if (valuable.dollarValueCurrent < valuable.dollarValueOriginal - IntactValueEpsilon)
            return;   // took damage on the way out -> not intact

        foreach (var q in _teamQuests)
            if (q.Objective == ObjectiveType.IntactExtract)
                Advance(q, 1);
    }

    /// <summary>Local client: our character took damage -> our personal "no damage" quest fails.</summary>
    public static void OnLocalPlayerDamaged()
    {
        if (!ModConfig.QuestsEnabled.Value || _personal == null)
            return;
        if (_personal.Objective == ObjectiveType.NoDamage && !_personal.Completed)
            _personal.Failed = true;
    }

    private static void Advance(ActiveQuest q, int amount)
    {
        if (q.Completed || q.Failed)
            return;
        bool wasDone = q.Completed;
        q.Add(amount);
        _dirty = true;
        if (!wasDone && q.Completed)
            CompleteTeamQuest(q);
    }

    private static void CompleteTeamQuest(ActiveQuest q)
    {
        q.Completed = true;
        _usedSignatures.Add(q.Signature());
        GrantReward(q);                 // host-authoritative payout
        CreditLocalCompletion(q);       // host credits its own rank + popup
        Broadcast();
        Plugin.Log.LogInfo($"[Quests] Completed team quest: {q.Title}");
    }

    // ---- Personal completion (local) ------------------------------------

    private static void OnPersonalCompleted(ActiveQuest q)
    {
        CreditLocalCompletion(q);
        // Money reward: host applies directly; a non-host asks the host over the network.
        if (q.RewardType == RewardType.Money)
        {
            if (SemiFunc.IsMasterClientOrSingleplayer())
                QuestRewards.GrantMoney(q.RewardAmount);
            else
                ModNetwork.Instance?.RequestQuestMoney(q.RewardAmount);
        }
    }

    private static void GrantReward(ActiveQuest q)
    {
        switch (q.RewardType)
        {
            case RewardType.Money:
                QuestRewards.GrantMoney(q.RewardAmount);
                break;
            case RewardType.Item:
                q.RewardItem = GrantTeamUpgrade(q, tiered: false) ?? "upgrade";
                break;
            case RewardType.Cosmetic:
                string cube = QuestRewards.SpawnCosmetic(q.Rank);
                q.RewardItem = cube ?? "cosmetic";
                break;
            case RewardType.Heal:
                QuestRewards.HealAll(q.RewardAmount);
                break;
            case RewardType.ShopItem:
                string shopItem = QuestRewards.SpawnGeneralItem();
                q.RewardItem = shopItem ?? "item";
                break;
            case RewardType.ExactUpgrade:
                q.RewardItem = GrantTeamUpgrade(q, tiered: true)
                               ?? $"{UpgradeTierInfo.Label(q.RewardTier)} upgrade";
                break;
            case RewardType.TempStat:
                // Every client buffs its own local player; the host tells the others over the net.
                QuestTempStats.Apply(q.RewardStat, q.RewardAmount);
                if (SemiFunc.IsMultiplayer())
                    ModNetwork.Instance?.BroadcastQuestBuff((byte)q.RewardStat, q.RewardAmount);
                break;
        }
    }

    /// <summary>
    /// A team quest grants ONE upgrade for the whole squad (a single item spawned on the level),
    /// with a rare 2% chance to drop a second one. Returns a HUD label for what was granted.
    /// </summary>
    private static string GrantTeamUpgrade(ActiveQuest q, bool tiered)
    {
        int copies = 1 + (Random.value < 0.02f ? 1 : 0);   // 2% chance of a bonus second upgrade
        string last = null;
        for (int c = 0; c < copies; c++)
            last = tiered ? QuestRewards.SpawnTieredUpgrade(q.RewardTier, q.Rank)
                          : QuestRewards.SpawnUpgradeItem();

        if (last == null)
            return null;
        return copies > 1 ? $"2x upgrades ({last})" : last;
    }

    /// <summary>Client hook: the host granted a temp-stat buff -> apply it to our local player.</summary>
    public static void ApplyTempBuff(StatKind stat, int amount)
    {
        if (!ModConfig.QuestsEnabled.Value)
            return;
        QuestTempStats.Apply(stat, amount);
    }

    private static void CreditLocalCompletion(ActiveQuest q)
    {
        string id = LocalSteamId();
        bool rankUp = QuestRankProgress.AddCompletion(id);
        ShowPopup(q, rankUp);
    }

    private static void ShowPopup(ActiveQuest q, bool rankUp)
    {
        var mission = MissionUI.instance;
        if (mission == null)
            return;
        string line = rankUp
            ? $"QUEST COMPLETE — RANK UP TO {QuestRankInfo.Label(QuestRankProgress.GetRank(LocalSteamId()))}!"
            : $"QUEST COMPLETE: {q.Title}";
        try { mission.MissionText(line, Color.white, new Color(1f, 0.85f, 0.2f), 4f); }
        catch { /* MissionUI signature guard */ }
    }

    private static string RewardText(ActiveQuest q) => q.RewardType switch
    {
        RewardType.Money => $"${Mathf.RoundToInt(q.RewardAmount / 1000f)}K",
        RewardType.Cosmetic => "cosmetic box",
        RewardType.Heal => $"heal {q.RewardAmount}",
        RewardType.TempStat => $"+{q.RewardAmount} {StatKindInfo.Label(q.RewardStat)} (level)",
        RewardType.ShopItem => "shop item",
        RewardType.ExactUpgrade => $"{UpgradeTierInfo.Label(q.RewardTier)} upgrade",
        _ => "upgrade item",
    };

    public static string RewardLabel(ActiveQuest q)
    {
        if (q == null)
            return "";
        switch (q.RewardType)
        {
            case RewardType.Item:
                return string.IsNullOrEmpty(q.RewardItem) ? "Reward: upgrade item" : $"Reward: {q.RewardItem}";
            case RewardType.Cosmetic:
                return "Reward: cosmetic box";
            case RewardType.Heal:
                return $"Reward: heal squad {q.RewardAmount} HP";
            case RewardType.TempStat:
                return $"Reward: +{q.RewardAmount} {StatKindInfo.Label(q.RewardStat)} (this level)";
            case RewardType.ShopItem:
                return string.IsNullOrEmpty(q.RewardItem) ? "Reward: shop item" : $"Reward: {q.RewardItem}";
            case RewardType.ExactUpgrade:
                return string.IsNullOrEmpty(q.RewardItem)
                    ? $"Reward: {UpgradeTierInfo.Label(q.RewardTier)} upgrade"
                    : $"Reward: {q.RewardItem}";
            default:
                return $"Reward: ${Mathf.RoundToInt(q.RewardAmount / 1000f)}K";
        }
    }

    // ---- Context gathering ----------------------------------------------

    private static QuestContext BuildContext()
    {
        var ctx = new QuestContext();

        var players = SemiFunc.PlayerGetList();
        ctx.Players = players != null ? Mathf.Max(1, players.Count) : 1;
        ctx.Day = RunManager.instance != null ? RunManager.instance.levelsCompleted : 0;
        ctx.Rank = QuestRankProgress.GetRank(LocalSteamId());

        var rd = RoundDirector.instance;
        ctx.HaulGoal = rd != null ? rd.haulGoal : 0;

        var valuables = Object.FindObjectsOfType<ValuableObject>();
        ctx.ValuableCount = valuables != null ? valuables.Length : 0;

        // Which monster types and tiers are actually on this level.
        var enemies = Object.FindObjectsOfType<EnemyParent>();
        var seenNames = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
        var seenTiers = new HashSet<int>();
        if (enemies != null)
        {
            foreach (var ep in enemies)
            {
                if (ep == null)
                    continue;
                if (!string.IsNullOrEmpty(ep.enemyName) && seenNames.Add(ep.enemyName))
                    ctx.EnemyNames.Add(ep.enemyName);
                int tier = (int)ep.difficulty;
                if (seenTiers.Add(tier))
                    ctx.EnemyTiers.Add(tier);
            }
        }

        // Does the level have any death pits?
        var hurts = Object.FindObjectsOfType<HurtCollider>();
        if (hurts != null)
        {
            foreach (var h in hurts)
            {
                if (h != null && h.deathPit)
                {
                    ctx.HasPits = true;
                    break;
                }
            }
        }

        return ctx;
    }

    // ---- Networking ------------------------------------------------------

    public static void Tick(float dt)
    {
        if (!SemiFunc.IsMasterClientOrSingleplayer() || !SemiFunc.IsMultiplayer())
            return;
        if (!_dirty)
            return;
        _broadcastCooldown -= dt;
        if (_broadcastCooldown > 0f)
            return;
        Broadcast();
    }

    private static void Broadcast()
    {
        _dirty = false;
        _broadcastCooldown = 0.75f;
        if (SemiFunc.IsMultiplayer())
            ModNetwork.Instance?.BroadcastQuestState(Serialize());
    }

    private static object[] Serialize()
    {
        int n = _teamQuests.Count;
        var data = new object[1 + n * 13];
        data[0] = n;
        int i = 1;
        foreach (var q in _teamQuests)
        {
            data[i++] = q.Id;
            data[i++] = (byte)q.Objective;
            data[i++] = (byte)q.Category;
            data[i++] = (byte)q.Rank;
            data[i++] = q.Target;
            data[i++] = q.Progress;
            data[i++] = q.Param ?? "";
            data[i++] = (byte)q.RewardType;
            data[i++] = q.RewardAmount;
            data[i++] = (byte)q.RewardStat;
            data[i++] = (byte)q.RewardTier;
            data[i++] = q.Title ?? "";
            // Pack completed/failed into one byte. Cast to byte so Photon boxes it AS a byte
            // (a bitwise-or promotes to int, which would then fail to unbox as byte on receipt).
            data[i++] = (byte)((q.Completed ? 1 : 0) | (q.Failed ? 2 : 0));
        }
        return data;
    }

    /// <summary>Clients rebuild the team set from the host snapshot and credit any new completions.</summary>
    public static void ReceiveTeamState(object[] data)
    {
        if (SemiFunc.IsMasterClientOrSingleplayer() || data == null || data.Length == 0)
            return;

        int n = (int)data[0];
        var previous = new Dictionary<int, bool>();
        foreach (var q in _teamQuests)
            previous[q.Id] = q.Completed;

        _teamQuests.Clear();
        int i = 1;
        for (int k = 0; k < n && i + 13 <= data.Length; k++)
        {
            var q = new ActiveQuest
            {
                Id = (int)data[i++],
                Scope = QuestScope.Team,
                Objective = (ObjectiveType)(byte)data[i++],
                Category = (QuestCategory)(byte)data[i++],
                Rank = (QuestRank)(byte)data[i++],
                Target = (int)data[i++],
                Progress = (int)data[i++],
                Param = (string)data[i++],
                RewardType = (RewardType)(byte)data[i++],
                RewardAmount = (int)data[i++],
                RewardStat = (StatKind)(byte)data[i++],
                RewardTier = (UpgradeTier)(byte)data[i++],
                Title = (string)data[i++],
            };
            byte flags = (byte)data[i++];
            q.Completed = (flags & 1) != 0;
            q.Failed = (flags & 2) != 0;
            _teamQuests.Add(q);

            // Newly completed since our last snapshot -> credit our own rank + popup.
            if (q.Completed && (!previous.TryGetValue(q.Id, out bool wasDone) || !wasDone))
                CreditLocalCompletion(q);
        }
    }
}
