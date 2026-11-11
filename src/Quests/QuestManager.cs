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

    // ---- host state for the newer quests (reset each level) --------------------------------------------
    private static readonly List<float> _killTimes = new();            // Hat Trick
    private static int _crownJewelId;                                  // Crown Jewel: instance id of the target valuable
    private static readonly List<Vector3> _deathSpots = new();         // Pay Respects
    private static readonly Dictionary<PlayerAvatar, PlayerWatch> _watch = new();
    private static float _observeTimer;
    private static bool _firstExtractionDone;

    private sealed class PlayerWatch
    {
        public bool Tumbling;
        public float LastYaw, Spin, SpinClock, Crouch;
    }

    // Signatures completed earlier in THIS run, so we don't re-offer the same quest.
    private static readonly HashSet<string> _usedSignatures = new();

    // ValuableObject instance IDs already credited to intact-extract quests this level, so the
    // per-item and final sweep hooks (which overlap) can't double-count the same valuable.
    private static readonly HashSet<int> _extractedThisLevel = new();

    // A valuable counts as "intact" only if it reaches the tube with (essentially) its full value;
    // any impact damage drops dollarValueCurrent below dollarValueOriginal.
    private const float IntactValueEpsilon = 0.5f;

    private static int _teamDeaths;
    private static float _levelSeconds;
    private static float _rushPulse;
    // Players the mod's own free revive is about to bring back; they must not credit "revive a teammate".
    private static readonly HashSet<PlayerAvatar> _autoRevived = new();
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
        _levelSeconds = 0f;
        _rushPulse = 0f;
        _autoRevived.Clear();
        _levelActive = true;
        _extractedThisLevel.Clear();
        _killTimes.Clear();
        _deathSpots.Clear();
        _watch.Clear();
        _observeTimer = 0f;
        _firstExtractionDone = false;
        _crownJewelId = 0;

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
            // Ids unique across levels, so a guest never mistakes this level's quest 1 for the previous level's.
            _setId++;
            for (int i = 0; i < _teamQuests.Count; i++) _teamQuests[i].Id = _setId * 16 + i + 1;
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

        // The game changes level on the host only: tell the guests, so they finish their own personal quest
        // (and ask the host for its money and points) and drop this level's temp buffs.
        if (SemiFunc.IsMasterClientOrSingleplayer() && SemiFunc.IsMultiplayer())
            ModNetwork.Instance?.BroadcastQuestLevelEnd(completed);

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

        // Pay Respects remembers where monsters fell; Hat Trick keeps the recent kill times.
        if (enemyParent != null && enemyParent.Enemy != null)
        {
            _deathSpots.Add(enemyParent.Enemy.transform.position);
            if (_deathSpots.Count > 12) _deathSpots.RemoveAt(0);
        }
        _killTimes.Add(Time.time);

        foreach (var q in _teamQuests)
        {
            switch (q.Objective)
            {
                case ObjectiveType.KillAny:
                    Advance(q, 1);
                    break;
                case ObjectiveType.HatTrick:
                    if (!q.Completed && int.TryParse(q.Param, out int window))
                    {
                        int streak = 0;
                        foreach (float t in _killTimes) if (Time.time - t <= window) streak++;
                        if (streak > q.Progress) { q.Progress = Mathf.Min(streak, q.Target); _dirty = true; }
                        if (streak >= q.Target) CompleteTeamQuest(q);
                    }
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
                case ObjectiveType.HandKill:
                    if (weapon == WeaponSource.Hands)
                        Advance(q, 1);
                    break;
                case ObjectiveType.GunKill:
                    if (weapon == WeaponSource.Gun)
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

    public static void OnEnemyStunned()
    {
        if (!ModConfig.QuestsEnabled.Value || !SemiFunc.IsMasterClientOrSingleplayer())
            return;
        foreach (var q in _teamQuests)
            if (q.Objective == ObjectiveType.StunEnemies)
                Advance(q, 1);
    }

    internal static void MarkAutoRevive(PlayerAvatar player)
    {
        if (player != null) _autoRevived.Add(player);
    }

    public static void OnPlayerRevived(PlayerAvatar player, bool byTruck)
    {
        if (!ModConfig.QuestsEnabled.Value || !SemiFunc.IsMasterClientOrSingleplayer())
            return;
        if (player != null && _autoRevived.Remove(player))
            return;
        if (byTruck)
            return;
        foreach (var q in _teamQuests)
            if (q.Objective == ObjectiveType.Revive)
                Advance(q, 1);
    }

    /// <summary>Halloween: the moon accepted an extraction point's offering.</summary>
    public static void OnMoonApproved()
    {
        if (!ModConfig.QuestsEnabled.Value || !SemiFunc.IsMasterClientOrSingleplayer())
            return;
        foreach (var q in _teamQuests)
            if (q.Objective == ObjectiveType.MoonRitual)
                Advance(q, 1);
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

        bool first = !_firstExtractionDone;
        _firstExtractionDone = true;
        foreach (var q in _teamQuests)
        {
            if (q.Objective == ObjectiveType.FastOpener && first && !q.Completed && !q.Failed && _levelSeconds < q.Target)
                CompleteTeamQuest(q);
            if (q.Objective == ObjectiveType.ExtractionRush && !q.Completed && !q.Failed && rd.allExtractionPointsCompleted)
                CompleteTeamQuest(q);
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
        if (valuable == null) return;
        OnValuableExtracted(valuable.GetInstanceID(), valuable.dollarValueCurrent, valuable.dollarValueOriginal,
            valuable.GetComponent<EnemyValuable>() != null);
    }

    internal static void OnValuableExtracted(int id, float value, float originalValue, bool soul)
    {
        if (!ModConfig.QuestsEnabled.Value || !SemiFunc.IsMasterClientOrSingleplayer()) return;

        // Nothing to do if no extraction-count quest is live.
        bool anyLive = false;
        foreach (var q in _teamQuests)
            if (q.Objective is ObjectiveType.IntactExtract or ObjectiveType.ExtractCount or ObjectiveType.HighValueExtract
                    or ObjectiveType.SoulExtract or ObjectiveType.CrownJewel &&
                !q.Completed && !q.Failed)
            {
                anyLive = true;
                break;
            }
        if (!anyLive)
            return;

        if (!_extractedThisLevel.Add(id))
            return;   // already counted this valuable

        bool intact = value >= originalValue - IntactValueEpsilon;
        foreach (var q in _teamQuests)
        {
            switch (q.Objective)
            {
                case ObjectiveType.IntactExtract:
                    if (intact) Advance(q, 1);   // took damage on the way out -> not intact
                    break;
                case ObjectiveType.ExtractCount:
                    Advance(q, 1);
                    break;
                case ObjectiveType.SoulExtract:
                    if (soul)
                        Advance(q, 1);
                    break;
                case ObjectiveType.HighValueExtract:
                    if (int.TryParse(q.Param, out int minimum) && originalValue >= minimum)
                        Advance(q, 1);
                    break;
                case ObjectiveType.CrownJewel:
                    if (q.Completed || q.Failed || id != _crownJewelId) break;
                    // Param is the share of its value it must keep (75, or 100 for the contract).
                    float keep = int.TryParse(q.Param, out int percent) ? percent / 100f : .75f;
                    if (value >= originalValue * keep - IntactValueEpsilon) CompleteTeamQuest(q);
                    else { q.Failed = true; _dirty = true; }
                    break;
            }
        }
    }

    /// <summary>Host: a valuable broke completely (Oops counts it; losing the Crown Jewel fails that quest).</summary>
    public static void OnValuableDestroyed(ValuableObject valuable)
    {
        if (!ModConfig.QuestsEnabled.Value || !SemiFunc.IsMasterClientOrSingleplayer() || valuable == null || !_levelActive)
            return;
        foreach (var q in _teamQuests)
        {
            if (q.Completed || q.Failed) continue;
            if (q.Objective == ObjectiveType.Oops)
                Advance(q, Mathf.RoundToInt(valuable.dollarValueOriginal));
            else if (q.Objective == ObjectiveType.CrownJewel && valuable.GetInstanceID() == _crownJewelId)
            {
                q.Failed = true;
                _dirty = true;
            }
        }
    }

    // Host: the quests that watch what players do (about five times a second).
    private static void TickObservers(float dt)
    {
        if (!_levelActive || !ModConfig.QuestsEnabled.Value || !SemiFunc.IsMasterClientOrSingleplayer()) return;
        bool any = false;
        foreach (var q in _teamQuests)
            if (!q.Completed && !q.Failed && q.Objective is ObjectiveType.RagdollOlympics or ObjectiveType.TalkItOut or ObjectiveType.Ballerina
                    or ObjectiveType.PayRespects or ObjectiveType.MonsterHugger or ObjectiveType.LightsOut or ObjectiveType.FastOpener)
            { any = true; break; }
        if (!any) return;
        _observeTimer += dt;
        if (_observeTimer < .2f) return;
        float step = _observeTimer;
        _observeTimer = 0f;

        int tumbles = 0, talking = 0, lightsOff = 0, hugging = 0;
        bool spun = false, respects = false;
        var players = SemiFunc.PlayerGetList();
        var enemies = EnemyDirector.instance != null ? EnemyDirector.instance.enemiesSpawned : null;
        if (players != null)
            foreach (var p in players)
            {
                if (p == null || p.isDisabled) continue;
                if (!_watch.TryGetValue(p, out var w)) _watch[p] = w = new PlayerWatch { LastYaw = p.transform.eulerAngles.y };
                // Tumbles: count each new one.
                if (p.isTumbling && !w.Tumbling) tumbles++;
                w.Tumbling = p.isTumbling;
                if (p.voiceChat != null && p.voiceChat.isTalking) talking++;
                // Ballerina: total turning within a rolling 6-second window.
                float yaw = p.transform.eulerAngles.y;
                w.Spin += Mathf.Abs(Mathf.DeltaAngle(w.LastYaw, yaw));
                w.LastYaw = yaw;
                w.SpinClock += step;
                if (w.Spin >= 5f * 360f) spun = true;
                if (w.SpinClock >= 6f) { w.Spin = 0f; w.SpinClock = 0f; }
                // Lights Out: walking around with the flashlight off.
                var light = p.flashlightController;
                if (light != null && !light.LightActive && p.rbVelocityRaw.sqrMagnitude > .25f) lightsOff++;
                // Pay Respects: crouched near where a monster died.
                bool nearGrave = false;
                foreach (var spot in _deathSpots) if ((spot - p.transform.position).sqrMagnitude < 9f) { nearGrave = true; break; }
                w.Crouch = p.isCrouching && nearGrave ? w.Crouch + step : 0f;
                if (w.Crouch >= 3f) respects = true;
                // Monster Hugger: right next to a living, active monster.
                if (enemies != null)
                    foreach (var ep in enemies)
                        if (ep != null && ep.Spawned && ep.Enemy != null && (!ep.Enemy.HasHealth || !ep.Enemy.Health.dead) &&
                            (ep.Enemy.transform.position - p.transform.position).sqrMagnitude < 2.5f * 2.5f)
                        { hugging++; break; }
            }

        foreach (var q in _teamQuests)
        {
            if (q.Completed || q.Failed) continue;
            switch (q.Objective)
            {
                case ObjectiveType.RagdollOlympics: if (tumbles > 0) Advance(q, tumbles); break;
                case ObjectiveType.Ballerina: if (spun) Advance(q, 1); break;
                case ObjectiveType.PayRespects: if (respects) Advance(q, 1); break;
                case ObjectiveType.TalkItOut: AddSeconds(q, talking * step); break;
                case ObjectiveType.LightsOut: AddSeconds(q, lightsOff * step); break;
                case ObjectiveType.MonsterHugger: AddSeconds(q, hugging * step); break;
                case ObjectiveType.FastOpener:
                    if (_firstExtractionDone) break;
                    if (_levelSeconds >= q.Target) { q.Progress = q.Target; q.Failed = true; _dirty = true; }
                    else if (Mathf.FloorToInt(_levelSeconds) != q.Progress) { q.Progress = Mathf.FloorToInt(_levelSeconds); _dirty = true; }
                    break;
            }
        }
    }

    // Seconds-based quests keep the fraction privately so short ticks still add up.
    private static readonly Dictionary<ActiveQuest, float> _seconds = new();

    private static void AddSeconds(ActiveQuest q, float seconds)
    {
        if (seconds <= 0f) return;
        _seconds.TryGetValue(q, out float total);
        total += seconds;
        _seconds[q] = total;
        int whole = Mathf.FloorToInt(total);
        if (whole > q.Progress) Advance(q, whole - q.Progress);
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
        QuestRewards.GrantSkillPointsToAll(q.RewardPoints, q.Title);
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
        // Skill points: the host pays its own; a guest asks the host (which caps how many it pays per level).
        if (q.RewardPoints > 0) Skills.SkillStore.RequestQuestPoints(q.RewardPoints);
    }

    // A special contract gives every player a level of its quest-only perk; anyone who already maxed it gets
    // two skill points instead.
    private static void GrantContractPerk(ActiveQuest q)
    {
        var perk = Skills.SkillCatalog.QuestPerk(q.RewardPerk);
        if (perk == null || !SemiFunc.IsMasterClientOrSingleplayer()) return;
        q.RewardItem = perk.Name;
        var players = SemiFunc.PlayerGetList();
        if (players == null) return;
        foreach (var player in players)
        {
            if (player == null || string.IsNullOrEmpty(player.steamID)) continue;
            if (!Skills.SkillStore.GrantQuestPerk(player.steamID, perk.Id))
                Skills.SkillStore.GrantPoints(player.steamID, 2, "contract (perk already maxed): " + q.Title);
        }
    }

    private static void GrantReward(ActiveQuest q)
    {
        switch (q.RewardType)
        {
            case RewardType.QuestPerk:
                GrantContractPerk(q);
                break;
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
            case RewardType.Costume:
                Cosmetics.CostumeRewards.GrantToAll("quest " + q.Title);
                q.RewardItem = "costume piece";
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
        // Every player sees what the quest added to their skill tree (the host pays it).
        if (q.Special && ModConfig.PlayerUpgradesEnabled.Value && Skills.SkillCatalog.QuestPerk(q.RewardPerk) is { } perk)
            Hud.HudNotify.Toast(QuestText.Russian ? $"Контракт выполнен: +1 «{Skills.SkillText.Name(perk)}» (дерево навыков, стр. 6)"
                : $"Contract done: +1 {perk.Name} (skill tree, page 6)", new Color(1f, .62f, .85f), 5f, "quest.contract." + q.Id);
        if (q.RewardPoints > 0 && ModConfig.PlayerUpgradesEnabled.Value)
            Hud.HudNotify.Toast(QuestText.Russian
                ? $"+{q.RewardPoints} {QuestText.Plural(q.RewardPoints, "очко", "очка", "очков")} навыков: {QuestText.Title(q)}"
                : $"+{q.RewardPoints} skill point{(q.RewardPoints == 1 ? "" : "s")}: {q.Title}", Hud.HudStyle.Good, 4f, "quest.points." + q.Id);
    }

    private static void ShowPopup(ActiveQuest q, bool rankUp)
    {
        var mission = MissionUI.instance;
        if (mission == null)
            return;
        bool ru = QuestText.Russian;
        string rank = QuestRankInfo.Label(QuestRankProgress.GetRank(LocalSteamId()));
        string line = rankUp
            ? (ru ? $"ЗАДАНИЕ ВЫПОЛНЕНО — НОВЫЙ РАНГ {rank}!" : $"QUEST COMPLETE — RANK UP TO {rank}!")
            : (ru ? $"ЗАДАНИЕ ВЫПОЛНЕНО: {QuestText.Title(q)}" : $"QUEST COMPLETE: {q.Title}");
        try { mission.MissionText(line, Color.white, new Color(1f, 0.85f, 0.2f), 4f); }
        catch { /* MissionUI signature guard */ }
    }

    private static string RewardText(ActiveQuest q) => BaseRewardText(q) + PointsText(q);

    private static string PointsText(ActiveQuest q) =>
        q.RewardPoints <= 0 ? "" : $" + {q.RewardPoints} skill point{(q.RewardPoints == 1 ? "" : "s")}";

    private static string BaseRewardText(ActiveQuest q) => q.RewardType switch
    {
        RewardType.Money => $"${QuestMoney.WalletUnits(q.RewardAmount)}K",
        RewardType.Cosmetic => "cosmetic box",
        RewardType.Costume => "costume piece for everyone",
        RewardType.Heal => $"heal {q.RewardAmount}",
        RewardType.TempStat => $"+{q.RewardAmount} {StatKindInfo.Label(q.RewardStat)} (level)",
        RewardType.ShopItem => "shop item",
        RewardType.ExactUpgrade => $"{UpgradeTierInfo.Label(q.RewardTier)} upgrade",
        RewardType.QuestPerk => $"perk: {Skills.SkillCatalog.QuestPerk(q.RewardPerk)?.Name ?? "contract perk"}",
        _ => "upgrade item",
    };

    public static string RewardLabel(ActiveQuest q) => q == null ? "" : QuestText.Russian ? RewardLabelRu(q) : BaseRewardLabel(q) + PointsText(q);

    private static string RewardLabelRu(ActiveQuest q)
    {
        string item = q.RewardItem;
        string reward = q.RewardType switch
        {
            RewardType.Item => string.IsNullOrEmpty(item) ? "улучшение" : item,
            RewardType.Cosmetic => "коробка с косметикой",
            RewardType.Costume => "деталь костюма для каждого",
            RewardType.Heal => $"+{q.RewardAmount} здоровья каждому",
            RewardType.TempStat => $"+{q.RewardAmount} {QuestText.Stat(q.RewardStat)} (на этот уровень)",
            RewardType.ShopItem => string.IsNullOrEmpty(item) ? "предмет из магазина" : item,
            RewardType.ExactUpgrade => string.IsNullOrEmpty(item) ? $"{QuestText.Tier(q.RewardTier)} улучшение" : item,
            RewardType.QuestPerk => $"+1 «{QuestText.PerkName(q.RewardPerk)}» (дерево навыков, всем)",
            _ => $"${QuestMoney.WalletUnits(q.RewardAmount)}K",
        };
        string points = q.RewardPoints <= 0 ? "" : $" + {q.RewardPoints} {QuestText.Plural(q.RewardPoints, "очко", "очка", "очков")} навыков";
        return "Награда: " + reward + points;
    }

    private static string BaseRewardLabel(ActiveQuest q)
    {
        switch (q.RewardType)
        {
            case RewardType.Item:
                return string.IsNullOrEmpty(q.RewardItem) ? "Reward: upgrade item" : $"Reward: {q.RewardItem}";
            case RewardType.Cosmetic:
                return "Reward: cosmetic box";
            case RewardType.Costume:
                return "Reward: a costume piece for everyone";
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
            case RewardType.QuestPerk:
                return $"Reward: +1 {Skills.SkillCatalog.QuestPerk(q.RewardPerk)?.Name ?? "contract perk"} (skill tree, everyone)";
            default:
                return $"Reward: ${QuestMoney.WalletUnits(q.RewardAmount)}K";
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
        ctx.Halloween = SemiFunc.GameEventIsHalloween();
        if (ctx.Halloween)
            ctx.Towers = Object.FindObjectsOfType<ExtractionHalloweenTower>().Length;

        var valuables = Object.FindObjectsOfType<ValuableObject>();
        ctx.ValuableCount = valuables != null ? valuables.Length : 0;
        // Crown Jewel: the most valuable item on the level (monster souls don't count).
        ValuableObject jewel = null;
        if (valuables != null)
            foreach (var v in valuables)
                if (v != null && v.GetComponent<EnemyValuable>() == null && (jewel == null || v.dollarValueOriginal > jewel.dollarValueOriginal))
                    jewel = v;
        if (jewel != null && jewel.dollarValueOriginal >= 1000f)
        {
            _crownJewelId = jewel.GetInstanceID();
            ctx.CrownJewel = Patches.Effects.PingController.Clean(jewel.name);
            ctx.CrownJewelValue = Mathf.RoundToInt(jewel.dollarValueOriginal);
        }

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
        TickRush(dt);
        TickObservers(dt);
        if (!SemiFunc.IsMasterClientOrSingleplayer() || !SemiFunc.IsMultiplayer())
            return;
        _broadcastCooldown -= dt;
        // Changes go out at most every 0.75 s; the whole set is also re-sent every few seconds, so a player who
        // joined mid-level (or missed a message while loading) catches up.
        _resendTimer -= dt;
        bool resend = _resendTimer <= 0f && _levelActive && _teamQuests.Count > 0;
        if ((!_dirty && !resend) || _broadcastCooldown > 0f)
            return;
        Broadcast();
    }

    private static int _setId;
    private static float _resendTimer;
    private const float ResendSeconds = 8f;

    // Host clock for "close every extraction within M:SS". Progress mirrors the elapsed seconds (about once a
    // second) so clients can show the countdown; the quest fails when the clock reaches the limit.
    private static void TickRush(float dt)
    {
        if (!_levelActive || !ModConfig.QuestsEnabled.Value || !SemiFunc.IsMasterClientOrSingleplayer())
            return;
        _levelSeconds += dt;
        _rushPulse -= dt;
        foreach (var q in _teamQuests)
        {
            if (q.Objective != ObjectiveType.ExtractionRush || q.Completed || q.Failed)
                continue;
            var rd = RoundDirector.instance;
            if (rd != null && rd.allExtractionPointsCompleted)
            {
                CompleteTeamQuest(q);
                continue;
            }
            if (_levelSeconds >= q.Target)
            {
                q.Progress = q.Target;
                q.Failed = true;
                _dirty = true;
            }
            else if (_rushPulse <= 0f)
            {
                q.Progress = Mathf.FloorToInt(_levelSeconds);
                _dirty = true;
            }
        }
        if (_rushPulse <= 0f)
            _rushPulse = 1f;
    }

    private static void Broadcast()
    {
        _dirty = false;
        _broadcastCooldown = 0.75f;
        _resendTimer = ResendSeconds;
        if (SemiFunc.IsMultiplayer())
            ModNetwork.Instance?.BroadcastQuestState(Serialize());
    }

    private static object[] Serialize()
    {
        int n = _teamQuests.Count;
        var data = new object[1 + n * QuestFields];
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
            data[i++] = (byte)((q.Completed ? 1 : 0) | (q.Failed ? 2 : 0) | (q.Special ? 4 : 0));
            data[i++] = (byte)Mathf.Clamp(q.RewardPoints, 0, 10);
            data[i++] = (byte)(q.RewardPerk < 0 ? 255 : Mathf.Min(q.RewardPerk, 254));
        }
        return data;
    }

    /// <summary>Values per quest in the team snapshot (NetworkPayload.QuestState checks the same layout).</summary>
    internal const int QuestFields = 15;

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
        for (int k = 0; k < n && i + QuestFields <= data.Length; k++)
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
            q.Special = (flags & 4) != 0;
            q.RewardPoints = (byte)data[i++];
            byte perk = (byte)data[i++];
            q.RewardPerk = perk == 255 ? -1 : perk;
            _teamQuests.Add(q);

            // Completed since a snapshot in which we saw it still open -> credit our own rank + popup. A quest we never
            // saw open (we joined after it was done) gives no credit; ids are unique per level, so the previous
            // level's quests never match.
            if (q.Completed && previous.TryGetValue(q.Id, out bool wasDone) && !wasDone)
                CreditLocalCompletion(q);
        }
    }
}
