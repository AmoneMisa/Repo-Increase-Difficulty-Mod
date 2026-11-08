using System.Collections.Generic;
using System.Globalization;
using BepInEx.Configuration;

namespace IncreaseHardMod;

/// <summary>
/// All tunable knobs for the mod. BepInEx reads/writes these to
/// BepInEx/config/com.kubai.increasehardmod.cfg, so players can edit values
/// without recompiling. We bind each entry once at startup.
/// </summary>
public static class ModConfig
{
    public static ConfigEntry<bool> VoiceRecoveryEnabled;
    public static ConfigEntry<bool> NetworkDiagnosticsEnabled;
    public static ConfigEntry<UnityEngine.KeyCode> AudioRecoveryKey;
    // When does the difficulty ramp begin?
    // levelsCompleted is how many levels the run has finished so far.
    // With RampStartLevel = 4, the first 4 levels are vanilla and scaling
    // kicks in from the 5th level onward (i.e. once levelsCompleted >= 4).
    public static ConfigEntry<int> RampStartLevel;

    // How many levels of depth make up ONE ramp step. 1 = stats grow every level
    // (the classic behaviour); 2 = stats only step up every 2nd level past the
    // start, etc. Lets you stretch the ramp out over a longer run.
    public static ConfigEntry<int> RampLevelsPerStep;

    // Per-stat switches for the depth ramp. Off = that stat stays vanilla (the other ramps and all
    // surprise modifiers are unaffected). Each client reads its own copy, so keep them identical
    // across players (the host's quota switch is what matters for the quota).
    public static ConfigEntry<bool> ScaleEnemyHealth;
    public static ConfigEntry<bool> ScaleEnemyDamage;
    public static ConfigEntry<bool> ScaleEnemySpeed;
    public static ConfigEntry<bool> ScaleEnemyRespawn;
    public static ConfigEntry<bool> ScaleQuota;

    // Enemy max HP. +PerLevel each level of depth, clamped to Max.
    public static ConfigEntry<float> EnemyHealthPerLevel;
    public static ConfigEntry<float> EnemyHealthMax;

    // Per-enemy HP cap overrides, keyed by the enemy's internal name.
    // Format: "Name:multiplier, Name:multiplier". Names are matched
    // case-insensitively against EnemyParent.enemyName. Any enemy not listed
    // uses the global EnemyHealthMax cap above.
    public static ConfigEntry<string> EnemyHealthMaxPerEnemy;

    // If true, logs each enemy's real internal name the first time it spawns,
    // so you can copy the exact strings into EnemyHealthMaxPerEnemy.
    public static ConfigEntry<bool> LogEnemyNames;

    // Damage players take FROM ENEMIES (not falls/objects). Same ramp shape.
    public static ConfigEntry<float> EnemyDamagePerLevel;
    public static ConfigEntry<float> EnemyDamageMax;

    // Enemy movement speed (and acceleration). Same ramp shape.
    public static ConfigEntry<float> EnemySpeedPerLevel;
    public static ConfigEntry<float> EnemySpeedMax;

    // Enemy respawn wait time. This RAMP REDUCES the time (enemies come back
    // faster). PerLevel is how much faster per level; Min is the floor so they
    // never respawn instantly. e.g. 0.10 = -10% wait per level, floored at 0.3.
    public static ConfigEntry<float> EnemyRespawnFasterPerLevel;
    public static ConfigEntry<float> EnemyRespawnMinFactor;

    // Extraction "quota" (haul goal) required each level.
    public static ConfigEntry<float> QuotaPerLevel;
    public static ConfigEntry<float> QuotaMax;

    // Hard ceiling: quota can never exceed this fraction of the TOTAL loot value
    // present on the level, so a level is always beatable. 0.9 = at most 90%.
    public static ConfigEntry<float> QuotaMaxLootFraction;

    // --- Surprise / special-level system ---
    // Master switch, chance a level becomes special, and how many modifiers it
    // gets. The per-modifier toggles let players disable specific surprises.
    public static ConfigEntry<bool> SurprisesEnabled;
    public static ConfigEntry<int> SpecialLevelChancePercent;
    public static ConfigEntry<int> ModifiersPerLevelMin;
    public static ConfigEntry<int> ModifiersPerLevelMax;
    public static ConfigEntry<int> SurprisesStartLevel;

    // One enable toggle per modifier. Disabled ones are never rolled.
    public static readonly System.Collections.Generic.Dictionary<Modifiers.ModifierType, ConfigEntry<bool>>
        ModifierEnabled = new();

    // Per-modifier effect strengths.
    public static ConfigEntry<float> FragileLootFactor;
    public static ConfigEntry<float> ProtectedLootFactor;
    public static ConfigEntry<float> HeavyLootFactor;
    public static ConfigEntry<float> LightLootFactor;
    public static ConfigEntry<float> LightMonsterMaxMass;
    public static ConfigEntry<float> HeavyMonsterMinMass;
    public static ConfigEntry<float> BiggerSoulDropsFactor;
    public static ConfigEntry<float> MonsterPitDamageFactor;
    public static ConfigEntry<int> ExtraGachaCubesPerSpawn;
    public static ConfigEntry<float> DangerousLootDamageFactor;
    public static ConfigEntry<float> SoulsExplodeHarderFactor;
    public static ConfigEntry<float> ThornyMonstersChance, ThornyMonstersMaxReflect;
    public static ConfigEntry<int> SpreadUpgradesCount;
    public static ConfigEntry<int> MysteryBoxCount, MysteryBoxMoneyMin, MysteryBoxMoneyMax;
    public static ConfigEntry<int> MysteryBoxWeightUpgrade, MysteryBoxWeightMoney, MysteryBoxWeightWeapon, MysteryBoxWeightCostume;
    public static ConfigEntry<bool> CostumeRewardsEnabled;
    public static ConfigEntry<string> SpreadUpgradesAllowed;
    public static ConfigEntry<bool> LogSpreadUpgradeNames;
    public static ConfigEntry<bool> AutoReviveAtCar;
    public static ConfigEntry<float> LevelTimerSeconds;
    public static ConfigEntry<float> LevelTimerKillBonus;
    public static ConfigEntry<float> LevelTimerDeathPenalty;
    public static ConfigEntry<float> LevelTimerQuotaBonus;

    // --- Late Join ---
    // When enabled, the host keeps the Photon room (and Steam lobby) reachable
    // during a run/shop/truck instead of sealing it at run start, and re-syncs
    // mod state (modifiers, timer, truck upgrades) whenever a new player connects.
    public static ConfigEntry<bool> LateJoinEnabled;

    // --- Ping ---
    // Press PingKey to mark whatever you're looking at (valuable/enemy/item/location).
    // The marker is broadcast to all players and shown as a world-anchored label.
    public static ConfigEntry<bool> PingEnabled, PingUnderglowEnabled;
    public static ConfigEntry<UnityEngine.KeyCode> PingKey;
    public static ConfigEntry<float> PingMaxDistance;
    public static ConfigEntry<float> PingDurationSeconds;
    public static ConfigEntry<float> PingMarkerScale, PingSoundVolume;
    // Max live Location pings per player (gated behind the "Ping Anywhere" upgrade).
    public static ConfigEntry<int> MaxLocationPings;
    // Global cap on how many ping markers can be on screen at once (anti-flood).
    public static ConfigEntry<int> MaxActivePings;
    // Shop prices for the two ping-gate upgrades.
    // "Pin": a one-time permanent unlock (like Ping Anywhere) letting you place Location pins.

    // --- Item Unstuck (valuable safety net) ---
    // Host-authoritative safeguards for valuables: relocate ones trapped in geometry
    // and keep them invulnerable until first grabbed.
    public static ConfigEntry<bool> ItemUnstuckEnabled;
    public static ConfigEntry<bool> ValuableRelocateStuck;
    public static ConfigEntry<float> ValuableStuckSeconds;
    public static ConfigEntry<bool> ValuableInvulnerableUntilGrab;

    // --- Loot Safety Net ---
    // Host-side top-up while an extraction point is open: replaces the game's own soft-lock spawn, counting
    // only loot the team can reach and also topping up when the open point stops making progress.
    public static ConfigEntry<bool> LootSafetyNetEnabled;
    public static ConfigEntry<bool> ChallengesEnabled;
    public static ConfigEntry<float> ChallengeChancePercent;
    public static ConfigEntry<float> LootSafetyMinPlayerDistance;
    public static ConfigEntry<int> LootSafetyMaxSpawns;
    public static ConfigEntry<bool> LootSafetyRevealOnMap;
    public static ConfigEntry<bool> LootSafetyLog;

    // --- Spectator Map ---
    public static ConfigEntry<bool> SpectatorMapEnabled;
    public static ConfigEntry<UnityEngine.KeyCode> SpectatorMapHoldKey;
    public static ConfigEntry<float> SpectatorMapScreenFraction;

    // --- Quests ---
    // An extra progression layer: 2-3 team quests per level plus a personal one, scaled by
    // team size / day / your contract rank (F..SSS). Completing quests advances your rank and
    // pays out money or upgrade items. Team quests are host-authoritative and synced.
    public static ConfigEntry<bool> QuestsEnabled;
    public static ConfigEntry<UnityEngine.KeyCode> QuestPeekKey;
    public static ConfigEntry<int> QuestsPerLevelMin;
    public static ConfigEntry<int> QuestsPerLevelMax;
    public static ConfigEntry<int> QuestCompletionsPerRank;
    public static ConfigEntry<int> QuestCompletionsAtoS;
    public static ConfigEntry<int> QuestCompletionsStoSS;
    public static ConfigEntry<int> QuestCompletionsSStoSSS;
    public static ConfigEntry<int> QuestMoneyBase;
    public static ConfigEntry<int> QuestSkillPoints, QuestSkillPointsHighRank;
    public static ConfigEntry<float> QuestSkillPointChance, QuestContractChance;
    public static ConfigEntry<bool> SkillBoxesEnabled, RetryCrystalsEnabled;
    public static ConfigEntry<int> RetryCrystalMin, RetryCrystalMax;
    public static ConfigEntry<float> RetryCrystalCharge;
    public static ConfigEntry<int> SkillBoxMin, SkillBoxMax, SkillBoxWeight0, SkillBoxWeight1, SkillBoxWeight2, SkillBoxWeight3;
    public static ConfigEntry<bool> QuestFunnyEnabled;
    public static ConfigEntry<bool> QuestItemRewardsEnabled;
    public static ConfigEntry<bool> QuestCosmeticRewardsEnabled;
    public static ConfigEntry<bool> QuestExactUpgradeRewardsEnabled;
    public static ConfigEntry<bool> QuestShopItemRewardsEnabled;
    public static ConfigEntry<bool> QuestHealRewardsEnabled;
    public static ConfigEntry<bool> QuestTempStatRewardsEnabled;
    public static ConfigEntry<int> QuestHealAmount;

    // --- Round Results ---
    // End-of-level MVP / OOPS CHAMP screen with a per-player scoreboard. The host records, everyone
    // with the mod sees the same board; players without the mod simply don't get a screen.
    public static ConfigEntry<bool> RoundResultsEnabled;
    public static ConfigEntry<float> RoundResultsSeconds;
    public static ConfigEntry<UnityEngine.KeyCode> RoundResultsKey;
    public static ConfigEntry<UnityEngine.KeyCode> MapVoteHudKey;
    public static ConfigEntry<bool> InventorySlotsEnabled;
    public static ConfigEntry<UnityEngine.KeyCode> InventorySlot4Key, InventorySlot5Key, InventorySlot6Key, InventorySlot7Key;

    // --- Utility Drones ---
    public static ConfigEntry<bool> DronesEnabled;
    public static ConfigEntry<bool> ScaleMapValue, ScaleEnemyCap, ScaleLevelSize, ScaleExtractions, ScaleMoon;
    public static ConfigEntry<int> EconomyEventChance, EconomyStartLevel;
    public static ConfigEntry<int> MapValueStartLevel, EnemyCapStartLevel, LevelSizeStartLevel, ExtractionBonusInterval, ExtractionMax, MoonLevelsPerPhase;
    public static ConfigEntry<float> MapValuePerLevel, MapValueMax, EnemyCapPerLevel, EnemyCapMax, LevelSizePerLevel, LevelSizeMax;
    public static ConfigEntry<bool> ShopEconomyEnabled, BlackMarketEnabled;
    public static ConfigEntry<int> ShopEconomyChance, BlackMarketModifierPrice, BlackMarketEconomyPrice, BlackMarketShopEconomyPrice;
    public static ConfigEntry<float> BlackMarketPerLevel;
    public static ConfigEntry<UnityEngine.KeyCode> BlackMarketKey;
    public static readonly Dictionary<int, ConfigEntry<bool>> StatModifierEnabled = new();
    public static ConfigEntry<bool> GearEnabled;
    // Cosmetics (local visuals only; nothing is networked).
    public static ConfigEntry<bool> CircletEnabled, HeroAuraEnabled, IceBlockEnabled, UpgradesListScaleEnabled;
    // Armor / Overcharge upgrades and the Tumble Launch bonus (SurvivalUpgrades).
    public static ConfigEntry<bool> ArmorEnabled, OverchargeEnabled, TumbleBonusEnabled;
    public static ConfigEntry<bool> FailVoteEnabled, HostRunControlsEnabled;
    public static ConfigEntry<float> ArmorPercentPerLevel, ArmorMaxReduction, OverchargeBuildupPerLevel, OverchargeRecoveryPerLevel;
    public static ConfigEntry<float> TumbleDamagePerLevel, TumbleSelfDamageReductionPerLevel, TumbleSelfDamageMaxReduction;
    public static ConfigEntry<int> ArmorMaxLevel, OverchargeMaxLevel, TumbleMaxLevel;
    public static ConfigEntry<float> CircletScale;
    public static ConfigEntry<bool> DefenceOrbEnabled, DefenceDroneEnabled, ProtectionOrbEnabled;
    public static ConfigEntry<bool> PoisonCrossbowEnabled, DecoyGrenadeEnabled, IcePistolEnabled, StickyPistolEnabled;
    // Flamethrower C.A.R.T.: wheeled flamethrower turret with the Hauler's loot storage box.
    public static ConfigEntry<bool> FlameCartEnabled;
    // Extra C.A.R.T.s: Soul C.A.R.T. (souls only, value cap), Wide C.A.R.T., Double-Decker C.A.R.T.
    public static ConfigEntry<bool> CustomCartsEnabled;
    // Courier pet: every player's helper that charges, fetches and delivers items.
    public static ConfigEntry<bool> CourierEnabled;
    public static ConfigEntry<int> CourierLook, CourierOwned, CourierCoins, CourierFragments;
    public static ConfigEntry<bool> CourierStarterCoinGiven;
    public static ConfigEntry<float> CourierCoinChance;
    public static ConfigEntry<UnityEngine.KeyCode> CourierMenuKey;
    public static ConfigEntry<int> SoulCartValueCap;
    public static ConfigEntry<float> WideCartWidth;
    public static ConfigEntry<int> FlameDamagePerSecond, FlameCartValueCap, FlameCartPriceMinK, FlameCartPriceMaxK;
    public static ConfigEntry<float> FlameTickSeconds, FlameFuelSeconds, FlameRange, FlameConeAngle;
    // Wizard Staff: chain-lightning wand (handgun base, revolver price).
    public static ConfigEntry<bool> StaffEnabled, NetLauncherEnabled;
    public static ConfigEntry<bool> WingCosmeticsEnabled, SharkCosmeticsEnabled, StarlightCosmeticsEnabled, LegateCosmeticsEnabled, FairyCosmeticsEnabled, GildedCosmeticsEnabled,
        SeerCosmeticsEnabled, JesterCosmeticsEnabled, BrideCosmeticsEnabled, AngelCosmeticsEnabled;
    public static ConfigEntry<bool> ScreenEffectsEnabled;
    public static ConfigEntry<bool> GuaranteedStrengthEnabled;
    public static ConfigEntry<float> NetSeconds, NetBreakChance;
    public static ConfigEntry<int> NetShots;
    public static ConfigEntry<int> StaffDamage, StaffMaxTargets, StaffCharges;
    public static ConfigEntry<float> StaffRange, StaffChainRange, StaffAimAngle, StaffShockSeconds, StaffPlayerDamagePercent;
    public static ConfigEntry<int> DefenceAbsorb, ProtectionReductionPercent;
    public static ConfigEntry<float> DefenceIdleDrainPerMinute;
    public static ConfigEntry<int> DefenceItemAbsorb;
    public static ConfigEntry<float> DefenceDroneSeconds;
    public static ConfigEntry<int> PoisonHitDamage, PoisonTotalDamage;
    public static ConfigEntry<float> PoisonSeconds, IceFreezeSeconds, DecoySeconds, StickyFreeLiftMass;
    public static ConfigEntry<int> IceGunDamagePercent, IceMeleeBonusPercent;
    public static ConfigEntry<bool> GhostEnabled;
    public static ConfigEntry<bool> SilencerEnabled;
    public static ConfigEntry<bool> LinkEnabled;
    public static ConfigEntry<bool> RepairEnabled;
    public static ConfigEntry<float> LinkMaxDistance;
    public static ConfigEntry<float> SilencerNoiseMultiplier;

    // --- Map Vote ---
    // While in the truck lobby, players vote on which map to run next (a small set of random
    // candidates). Whatever is leading when the truck departs is used instead of the game's
    // random pick. HOST-SIDE application (the host overrides the pick and syncs it normally).
    public static ConfigEntry<bool> MapVoteEnabled;
    public static ConfigEntry<int> MapVoteChoices;
    public static ConfigEntry<float> MapVoteSeconds;

    // --- Photon reachability HUD ---
    // Small main-menu overlay pinging Photon region name servers so you can see which
    // regions are reachable / their latency before hosting or joining.
    public static ConfigEntry<bool> PhotonHudEnabled;
    public static ConfigEntry<string> PhotonHudRegions;

    // --- Auto Backup Saves ---
    // REPO deletes the run save when you wipe / fail a level (DataDirector.SaveDeleteCheck).
    // Automatic deletion is always blocked. This toggle only controls extra snapshots,
    // which are kept indefinitely; legacy KeepSaveLoadable/MaxBackupsPerSave are ignored.
    public static ConfigEntry<bool> AutoBackupEnabled;

    // --- Truck upgrades ---
    // Team-wide truck upgrades sold in the normal shop like any other upgrade (once anyone owns a
    // level, the whole squad gets the effect). Effects apply themselves; there is no separate menu.
    public static ConfigEntry<bool> TruckUpgradesEnabled;
    // Heal power: scales all incoming healing (truck final-heal, medkits, drones).
    public static ConfigEntry<float> TruckHealPercentPerLevel;
    public static ConfigEntry<int> TruckHealMaxLevel;
    public static ConfigEntry<int> TruckHealBaseCost;
    // Charge efficiency: slows the truck charging station's energy-pool drain.
    public static ConfigEntry<float> TruckChargePercentPerLevel;
    public static ConfigEntry<int> TruckChargeMaxLevel;
    public static ConfigEntry<int> TruckChargeBaseCost;
    // Post-quota selling: once bought, leftover valuables are auto-cashed AFTER
    // every extraction quota is closed, for TruckSellFraction of their value.
    public static ConfigEntry<int> TruckSellCost;
    public static ConfigEntry<float> TruckSellFraction, TruckSellMaxQuotaShare;

    // --- Player Upgrades (custom per-player upgrades sold in the DEFAULT shop) ---
    // Registered via REPOLib as runtime clones of vanilla upgrade items, so they roll
    // in the normal shop upgrade booth and are tracked/saved/synced per player.
    public static ConfigEntry<bool> PlayerUpgradesEnabled;
    // Stamina: each level reduces sprint energy drain by this fraction.
    public static ConfigEntry<float> StaminaPercentPerLevel;
    public static ConfigEntry<int> StaminaMaxLevel;
    // HP Regen: very slow passive heal, only up to a fraction of max HP, then stops.
    public static ConfigEntry<float> RegenPercentPerLevel;
    public static ConfigEntry<float> RegenMaxHpFraction;
    public static ConfigEntry<int> RegenMaxLevel;
    // Death Protection: consumable charge; survive a lethal hit at 1 HP, re-bought each use.
    public static ConfigEntry<int> DeathProtectMaxCharges;
    public static ConfigEntry<int> DeathProtectBasePrice;
    // Battery Life: separate upgrades per gun type (Bullet/Laser/Tranq) and for drones.
    // Each level adds effective battery life (refunds a fraction of drain), so the item
    // lasts longer. "+100%" = twice the usage.
    public static ConfigEntry<float> GunExtraBulletsPerLevel;
    public static ConfigEntry<int> GunExtraBulletsMaxLevel;
    public static ConfigEntry<float> DroneBatteryPercentPerLevel;
    public static ConfigEntry<int> DroneBatteryMaxLevel;
    // Melee / Grenade Damage: per-player, each level adds a percentage to monster damage.
    public static ConfigEntry<float> MeleeDamagePercentPerLevel;
    public static ConfigEntry<int> MeleeDamageMaxLevel;
    public static ConfigEntry<float> GrenadeDamagePercentPerLevel;
    public static ConfigEntry<int> GrenadeDamageMaxLevel;
    // Autoscan: passively discovers nearby valuables; each level widens the scan radius.
    public static ConfigEntry<float> AutoscanRangePerLevel;
    public static ConfigEntry<int> AutoscanMaxLevel;
    public static ConfigEntry<float> AutoscanInterval;
    // Map Suite: single 10-level upgrade, each level unlocking a map feature (counts,
    // quota path, zoom, cosmetic-box tracker, monster dots). Level->feature ladder is fixed.
    public static ConfigEntry<float> MapZoomFovLevel7;
    public static ConfigEntry<float> MapZoomFovLevel8;
    // Live zoom hotkeys while the map is open at L7+ (adjust the FOV on top of the level default).
    public static ConfigEntry<UnityEngine.KeyCode> MapZoomInKey, BetterMapKey;
    public static ConfigEntry<UnityEngine.KeyCode> MapZoomOutKey;
    public static ConfigEntry<float> MapZoomStep;
    // When true, the vanilla "Map Player Count" upgrade is removed from the shop (Map Suite replaces it).

    // Skill tree (src/Skills, docs/skill-tree-plan.md).
    public static ConfigEntry<bool> SkillSpendAnywhere;
    public static ConfigEntry<int> SkillRespecCost, SkillTradeOffResetCost;
    public static ConfigEntry<int> SkillPointBasePrice;
    public static ConfigEntry<UnityEngine.KeyCode> SkillTreeKey;
    public static ConfigEntry<float> SkillRootBonus, SkillWeaponDamageMax, SkillStatusRootSeconds, SkillSecondWindPerLevel,
        SkillSurvivorStamina, SkillFlashRange, SkillFlashWidthPerLevel, SkillFlashBrightnessPerLevel,
        SkillDesperationPerLevel, SkillDesperationThreshold, SkillHeavyHitterPerLevel, SkillMedicPerLevel, SkillMedicRadius,
        SkillBatterySaverPerLevel, SkillLuckyCrystalBonus, SkillCrouchSpeedPerLevel, SkillVampireMax;
    public static ConfigEntry<int> SkillLuckyBoxShift;

    // --- Level Info panel ---
    // Right-side overlay logging each level change (number + name + rolled surprises),
    // with a hotkey to hide/show it.
    public static ConfigEntry<bool> LevelInfoEnabled;
    public static ConfigEntry<UnityEngine.KeyCode> LevelInfoToggleKey;

    // --- Server Info panel ---
    // Passive lobby/menu overlay showing current Photon region, ping, room code and
    // player count. Reads PhotonNetwork state only — never opens sockets or callbacks.
    public static ConfigEntry<bool> ServerInfoEnabled;

    // --- Shopping List panel ---
    // In-shop overlay listing everything currently in the cart (ShopManager.shoppingList),
    // aggregated by name with quantity + summed price, colour-coded by item type, plus a
    // total-vs-currency footer. Reads ShopManager state only.
    public static ConfigEntry<bool> ShoppingListEnabled;

    // --- More Players ---
    // Raise the room's player cap above REPO's default of 6 (host-side; the host's
    // value defines the room). The game already supports up to 20 internally.
    public static ConfigEntry<bool> MorePlayersEnabled;
    public static ConfigEntry<int> MorePlayersMax;

    // --- Shop ---
    // Scale shop item prices up with player count (big lobbies earn more, so keep it
    // hard) and optionally let the same item show up multiple times in one shop.
    public static ConfigEntry<bool> ShopPriceScaleEnabled;
    public static ConfigEntry<int> ShopPriceBaselinePlayers;
    public static ConfigEntry<float> ShopPricePerExtraPlayer;
    public static ConfigEntry<int> ShopDuplicatesExtra;
    public static ConfigEntry<int> ShopExtraItems, ShopExtraUpgrades;

    // --- Diagnostics ---
    // Temporary kill-switches to isolate whether the mod interferes with the game's
    // own Photon connection (regions spinner / stuck loading). Both default off.
    public static ConfigEntry<bool> DiagnosticDisablePatches;
    public static ConfigEntry<bool> DiagnosticDisableRuntimeObjects;

    // REPOConfig (REPOModding) reads this exact string tag on a ConfigEntry to exclude it
    // from the in-game menu. Used for free-form strings it can't render as a slider/list.
    private const string HideFromRepoConfig = "HideFromREPOConfig";

    private static ConfigEntry<int> BindInt(ConfigFile cfg, string section, string key,
        int def, int min, int max, string desc) =>
        cfg.Bind(section, key, def, new ConfigDescription(desc, new AcceptableValueRange<int>(min, max)));

    private static ConfigEntry<float> BindFloat(ConfigFile cfg, string section, string key,
        float def, float min, float max, string desc) =>
        cfg.Bind(section, key, def, new ConfigDescription(desc, new AcceptableValueRange<float>(min, max)));

    // Short, player-facing explanation of what each surprise modifier actually does.
    internal static string ModifierDescription(Modifiers.ModifierType m) => m switch
    {
        Modifiers.ModifierType.FragileLoot        => "Loot breaks and loses value more easily.",
        Modifiers.ModifierType.HeavyLoot          => "Loot weighs more, making it harder to haul.",
        Modifiers.ModifierType.LightLoot          => "Loot weighs less, making it easier to haul.",
        Modifiers.ModifierType.ProtectedLoot      => "Loot takes less damage before breaking.",
        Modifiers.ModifierType.DangerousLoot      => "Hazardous valuables deal more damage to players.",
        Modifiers.ModifierType.ExtraSmallCart     => "An extra small cart spawns on the level.",
        Modifiers.ModifierType.ExtraBigCart       => "An extra big cart spawns on the level.",
        Modifiers.ModifierType.ExtraSouls         => "Monsters drop more souls.",
        Modifiers.ModifierType.SpreadUpgrades     => "Upgrade pickups are scattered around; usable only back in the truck.",
        Modifiers.ModifierType.NoMap              => "No map until you find a map upgrade pickup.",
        Modifiers.ModifierType.ReducedPlayerHp    => "Everyone starts the level wounded — current HP is cut to a rolled percentage (max HP is untouched, so you can heal back up).",
        Modifiers.ModifierType.AutoRevive         => "Each player gets one automatic revive this level.",
        Modifiers.ModifierType.MoneyLossOnDeath   => "Dying loses a percentage of your collected money.",
        Modifiers.ModifierType.LevelTimer         => "Timed level: kills add time, deaths remove it, quota adds it; at 0 the layout rerolls.",
        Modifiers.ModifierType.ChangeVoices       => "Player voices get swapped or randomized.",
        Modifiers.ModifierType.MoreGachaCubes     => "More gacha/cube valuables spawn.",
        Modifiers.ModifierType.SoulsExplodeHarder => "Exploding souls deal more damage to players.",
        Modifiers.ModifierType.BiggerSoulDrops    => "Each soul drop is worth more money (value per drop, not count).",
        Modifiers.ModifierType.MonstersPitTanky   => "Monsters take less damage from death pits.",
        Modifiers.ModifierType.LightMonsters      => "Monsters weigh less and yield to a single player's grab without Strength upgrades. Native hold-time, mounted and freeze restrictions still apply.",
        Modifiers.ModifierType.HeavyMonsters      => "All monsters weigh more, so they're hard to shove around.",
        Modifiers.ModifierType.SilentNight        => "Everything players do makes 40% less noise: footsteps, voices and dropped loot alert fewer monsters.",
        Modifiers.ModifierType.LoudFacility       => "The acoustics are terrible: monsters hear every footstep, voice and bump 50% farther away.",
        Modifiers.ModifierType.GreasedWheels      => "Freshly oiled wheels: C.A.R.T.s are much easier to push, even full and uphill.",
        Modifiers.ModifierType.ShortMemory        => "Monsters forget quickly: once they lose sight of you, they give up the chase sooner.",
        Modifiers.ModifierType.ThornyMonsters     => "About half the monsters grew thorns: hitting them with a melee weapon reflects up to 10% of the damage back at you.",
        Modifiers.ModifierType.MysteryBoxes       => "Mystery boxes appear around the map. Drop one monster soul into a box to roll a random upgrade, money, or a weapon/grenade for this level only. Each box takes one soul.",
        _                                         => $"The '{m}' surprise modifier.",
    };

    // All on/off switches for the mod's modules live together in one section at the top of the config, so
    // you don't have to scroll through every tuning value to find them. Existing values from the old
    // sections are carried over the first time.
    private static void BindModules(ConfigFile cfg)
    {
        SurprisesEnabled = Module(cfg, "Surprises", "Enabled", true, "Surprises", "Master switch for all level surprises, including economic events affecting loot value, quota and extraction pay. Shop price events have their own ShopEconomy switch.");
        ScaleMapValue = Module(cfg, "Level Scaling", "MapValue", true, "ScaleMapValue", "Valuables are worth more the deeper the run goes.");
        ScaleEnemyCap = Module(cfg, "Level Scaling", "EnemyCount", true, "ScaleEnemyCount", "More monsters spawn per level the deeper the run goes.");
        ScaleLevelSize = Module(cfg, "Level Scaling", "LevelSize", true, "ScaleLevelSize", "Levels get more modules (a bigger map) the deeper the run goes.");
        ScaleExtractions = Module(cfg, "Level Scaling", "Extractions", true, "ScaleExtractions", "Deeper levels get extra extraction points.");
        ScaleMoon = Module(cfg, "Level Scaling", "Moon", false, "ScaleMoon", "Moon phases advance every MoonLevelsPerPhase levels instead of every 5 (slows the moon-based difficulty).");
        ShopEconomyEnabled = Module(cfg, "Shop Economy", "Enabled", true, "ShopEconomy", "Shop price events: when the run reaches a shop it may get a sale, a tax or an inflation spike that changes item, upgrade, health pack and crystal prices (including this mod's own items and upgrades). Host rolls it.");
        BlackMarketEnabled = Module(cfg, "Black Market", "Enabled", true, "BlackMarket", "Black Market: in the shop, pay to keep a surprise modifier, level economy or shop economy from ever being rolled again this run. Keyboard driven.");
        PlayerUpgradesEnabled = Module(cfg, "Player Upgrades", "Enabled", true, "PlayerUpgrades", "Master switch for the mod's custom per-player upgrades that appear in the normal shop. Requires REPOLib. Takes effect on restart.");
        TruckUpgradesEnabled = Module(cfg, "Truck Upgrades", "Enabled", true, "TruckUpgrades", "Enable the team-wide truck upgrades (Heal Power, Charge Efficiency, Loot Cash-In). Bought in the normal shop; effects apply for the whole team.");
        ScaleEnemyHealth = Module(cfg, "Scaling", "EnemyHealth", true, "ScaleEnemyHealth", "Enemies gain max HP as the run goes deeper. Off disables the depth ramp; level surprises remain independent. Host settings apply to the lobby.");
        ScaleEnemyDamage = Module(cfg, "Scaling", "EnemyDamage", true, "ScaleEnemyDamage", "Enemies deal more damage to players as the run goes deeper. Off disables the depth ramp; level surprises remain independent. Host settings apply to the lobby.");
        ScaleEnemySpeed = Module(cfg, "Scaling", "EnemySpeed", true, "ScaleEnemySpeed", "Enemies move faster as the run goes deeper. Off disables the depth ramp; level surprises remain independent. Host settings apply to the lobby.");
        ScaleEnemyRespawn = Module(cfg, "Scaling", "EnemyRespawn", true, "ScaleEnemyRespawn", "Enemies respawn faster as the run goes deeper. Off disables the depth ramp; level surprises remain independent. Host settings apply to the lobby.");
        ScaleQuota = Module(cfg, "Scaling", "Quota", true, "ScaleQuota", "The extraction quota grows as the run goes deeper. Off = vanilla quota (host setting).");
        DronesEnabled = Module(cfg, "Drones", "Enabled", true, "Drones", "Add the Ghost, Silencer and Link utility drones to the shop. EVERY player must run this mod with the same setting, otherwise shop items and effects will not match. Takes effect on game restart.");
        GhostEnabled = Module(cfg, "Drones", "GhostEnabled", true, "GhostDrone", "Sell the Ghost Drone. Needs Drones/Enabled. Same on every player; applies on game restart.");
        SilencerEnabled = Module(cfg, "Drones", "SilencerEnabled", true, "SilencerDrone", "Sell the Silencer Drone. Needs Drones/Enabled. Same on every player; applies on game restart.");
        LinkEnabled = Module(cfg, "Drones", "LinkEnabled", true, "LinkDrone", "Sell the Link Drone Set (A + B). Needs Drones/Enabled. Same on every player; applies on game restart.");
        RepairEnabled = Module(cfg, "Drones", "RepairEnabled", true, "RepairDrone", "Sell the Repair Drone. Needs Drones/Enabled. Same on every player; applies on game restart.");
        ShopEconomyChance = BindInt(cfg, "Shop Economy", "EventChancePercent", 50, 0, 100, "Chance (percent) that a shop visit gets a price event.");
        BlackMarketKey = cfg.Bind("Black Market", "Key", UnityEngine.KeyCode.B, "Key that opens the Black Market while you are in the shop.");
        BlackMarketModifierPrice = BindInt(cfg, "Black Market", "ModifierPrice", 35, 1, 5000, "Base price (in the game's K currency) to block a surprise modifier. Scaled by how harmful it is and by depth.");
        BlackMarketEconomyPrice = BindInt(cfg, "Black Market", "EconomyPrice", 50, 1, 5000, "Base price to block a level economy event.");
        BlackMarketShopEconomyPrice = BindInt(cfg, "Black Market", "ShopEconomyPrice", 65, 1, 5000, "Base price to block a shop economy.");
        BlackMarketPerLevel = BindFloat(cfg, "Black Market", "PricePerLevel", 0.07f, 0f, 1f, "Price growth per level completed (0.07 = +7% per level).");
        const string Lvl = "Level Scaling";
        var economicSurprises = EconomicSurpriseConfig.Bind(cfg);
        EconomyEventChance = economicSurprises.Chance;
        EconomyStartLevel = economicSurprises.StartLevel;
        MapValueStartLevel = BindInt(cfg, Lvl, "MapValueStartLevel", 2, 0, 50, "Levels completed before valuables start gaining value.");
        MapValuePerLevel = BindFloat(cfg, Lvl, "MapValuePerLevel", 0.02f, 0f, 0.5f, "Valuable value growth per level (compounding). 0.02 = x1.02 per level.");
        MapValueMax = BindFloat(cfg, Lvl, "MapValueMax", 3f, 1f, 20f, "Cap on the depth multiplier of valuables' value.");
        EnemyCapStartLevel = BindInt(cfg, Lvl, "EnemyCountStartLevel", 8, 0, 100, "Levels completed before extra monsters appear.");
        EnemyCapPerLevel = BindFloat(cfg, Lvl, "EnemyCountPerLevel", 0.03f, 0f, 1f, "Extra monsters per level past the start, as a fraction of the normal count (0.03 = +3%).");
        EnemyCapMax = BindFloat(cfg, Lvl, "EnemyCountMax", 2f, 1f, 5f, "Cap on the monster count multiplier.");
        LevelSizeStartLevel = BindInt(cfg, Lvl, "LevelSizeStartLevel", 8, 0, 100, "Levels completed before levels start growing.");
        LevelSizePerLevel = BindFloat(cfg, Lvl, "LevelSizePerLevel", 0.03f, 0f, 1f, "Level size growth per level past the start (0.03 = +3% modules and grid).");
        LevelSizeMax = BindFloat(cfg, Lvl, "LevelSizeMax", 1.5f, 1f, 3f, "Cap on the level size multiplier.");
        ExtractionBonusInterval = BindInt(cfg, Lvl, "ExtractionBonusInterval", 10, 1, 50, "One extra extraction point every this many levels.");
        ExtractionMax = BindInt(cfg, Lvl, "ExtractionMax", 5, 1, 8, "Most extraction points a level can have.");
        MoonLevelsPerPhase = BindInt(cfg, Lvl, "MoonLevelsPerPhase", 15, 5, 60, "With ScaleMoon on: levels per moon phase (vanilla is 5).");
        GearEnabled = Module(cfg, "Gear", "Enabled", true, "ExtraGear", "Add the extra shop gear (Defence/Protection orbs, Defence drone, poison crossbow, decoy grenade, ice pistol, sticky pistol). EVERY player must run this mod with the same settings. Applies on game restart.");
        DefenceOrbEnabled = Module(cfg, "Gear", "DefenceOrbEnabled", true, "DefenceOrb", "Sell the Defence Orb.");
        DefenceDroneEnabled = Module(cfg, "Gear", "DefenceDroneEnabled", true, "DefenceDrone", "Sell the Defence Drone.");
        ProtectionOrbEnabled = Module(cfg, "Gear", "ProtectionOrbEnabled", true, "ProtectionOrb", "Sell the Protection Orb.");
        PoisonCrossbowEnabled = Module(cfg, "Gear", "PoisonCrossbowEnabled", true, "PoisonCrossbow", "Sell the Poison Crossbow.");
        DecoyGrenadeEnabled = Module(cfg, "Gear", "DecoyGrenadeEnabled", true, "DecoyGrenade", "Sell the Decoy Grenade.");
        IcePistolEnabled = Module(cfg, "Gear", "IcePistolEnabled", true, "IcePistol", "Sell the Pulse Ice Pistol.");
        StickyPistolEnabled = Module(cfg, "Gear", "StickyPistolEnabled", true, "StickyPistol", "Sell the Sticky Pistol.");
        FlameCartEnabled = Module(cfg, "Gear", "FlameCartEnabled", true, "FlameCart", "Sell the Flamethrower C.A.R.T. (a wheeled flamethrower turret with a loot storage box).");
        CourierEnabled = Module(cfg, "Courier", "Enabled", true, "Courier", "Every player gets a courier pet that follows them, charges and fetches items and can be petted by grabbing it, like Elsa. The host's setting decides.");
        CourierLook = cfg.Bind("Courier", "Look", 0, "Your courier's animal and colours (set from the courier menu; saved here).");
        CourierOwned = cfg.Bind("Courier", "OwnedAnimals", 0, "Courier animals you unlocked from the gacha (bit mask: 1 penguin, 2 chicken, 4 cat, 8 owl, 16 beaver).");
        CourierCoins = cfg.Bind("Courier", "GachaCoins", 0, "Your gacha coins (found rarely on levels; one pull each).");
        CourierFragments = cfg.Bind("Courier", "CoinHalves", 0, "Half coins from duplicate pulls; two make a coin.");
        CourierStarterCoinGiven = cfg.Bind("Courier", "StarterCoinGiven", false, "Whether you already got the free starter coin.");
        CourierCoinChance = BindFloat(cfg, "Courier", "CoinChancePerLevel", Courier.CourierRules.CoinChance, 0f, 1f, "HOST: chance that a gacha coin is hidden somewhere on a level.");
        CourierMenuKey = cfg.Bind("Courier", "MenuKey", UnityEngine.KeyCode.G, "Opens the courier command menu. Arrow keys move, Enter/Right picks, Left goes back.");
        CustomCartsEnabled = Module(cfg, "Gear", "CustomCartsEnabled", true, "CustomCarts", "Sell the Soul C.A.R.T. (small, souls only), the Wide C.A.R.T. and the Double-Decker C.A.R.T.");
        SoulCartValueCap = BindInt(cfg, "Carts", "SoulCartValueCap", 55000, 1000, 1000000, "Most soul value the Soul C.A.R.T. carries; a soul that would go over it is pushed back out.");
        WideCartWidth = BindFloat(cfg, "Carts", "WideCartWidth", 1.25f, 1f, 1.5f, "How much wider the Wide C.A.R.T. is than the big C.A.R.T. (1.25 = 25% wider). Above about 1.3 it may not fit through doorways. Applies after a game restart.");
        // New keys (not DamagePerTick/TickSeconds/BatteryDamage): saved values of the old keys would keep the slow flame.
        FlameDamagePerSecond = BindInt(cfg, "Flamethrower", "DamagePerSecond", 24, 1, 500, "Damage per second dealt to every monster inside the flame, applied in small fast ticks.");
        FlameTickSeconds = BindFloat(cfg, "Flamethrower", "DamageTickSeconds", 0.25f, 0.1f, 2f, "Seconds between flame damage ticks. Smaller = smoother, more immediate damage.");
        FlameFuelSeconds = BindFloat(cfg, "Flamethrower", "FuelSeconds", 60f, 5f, 600f, "Seconds of continuous flame one full battery provides (used whether or not anything is hit).");
        FlameRange = BindFloat(cfg, "Flamethrower", "Range", 7f, 2f, 15f, "Flame reach in metres, in the direction the operator looks.");
        FlameConeAngle = BindFloat(cfg, "Flamethrower", "ConeHalfAngle", 30f, 5f, 80f, "Half-angle of the flame cone in degrees.");
        FlameCartValueCap = BindInt(cfg, "Flamethrower", "CargoValueCap", 25000, 0, 1000000, "Most loot value its storage box (the Hauler's storage system) holds; loot that would exceed it is refused. 0 = the Hauler's own limit.");
        FlameCartPriceMinK = BindInt(cfg, "Flamethrower", "ShopPriceMinK", 75, 1, 5000, "Lowest shop price, in K as the shop shows it (75 = $75K). Shop economy modifiers still apply on top.");
        FlameCartPriceMaxK = BindInt(cfg, "Flamethrower", "ShopPriceMaxK", 120, 1, 5000, "Highest shop price, in K as the shop shows it (120 = $120K).");
        StaffEnabled = Module(cfg, "Gear", "WizardStaffEnabled", true, "WizardStaff", "Sell the Wizard Staff (chain-lightning wand).");
        NetLauncherEnabled = Module(cfg, "Gear", "NetLauncherEnabled", true, "NetLauncher", "Sell the Net Launcher (roots a monster to the floor).");
        GuaranteedStrengthEnabled = Module(cfg, "Shop", "GuaranteedStrength", true, "GuaranteedStrength", "When MoreStandsForShops is installed, the shop always offers at least one Strength (grab strength) upgrade, while the team can still buy it. Host setting.");
        ScreenEffectsEnabled = Module(cfg, "Screen Effects", "Enabled", true, "ScreenEffects", "When you are frozen, poisoned, sedated (tranquilized) or netted, show a themed frame around the screen edges and a status card at the left with a time bar. Local only.");
        WingCosmeticsEnabled = Module(cfg, "Cosmetics", "WingsEnabled", true, "WingCosmetics", "Add three Rare back-wing cosmetics (Astral Night, Solar Seraph, Prism Chime) to the Body Top slot. Everyone in the lobby needs this setting to see them. Takes effect on restart.");
        SharkCosmeticsEnabled = Module(cfg, "Cosmetics", "PlushSharkEnabled", true, "SharkCosmetics", "Add the six-part Plush Shark set: hood (Hat), headphones (Ears), suit with dorsal fin (Body Top), tail (Body Bottom) and two fin sleeves (Arm Right, Arm Left). The blue plush takes the colour you pick in the cosmetics menu. Everyone in the lobby needs this setting to see them. Takes effect on restart.");
        SeerCosmeticsEnabled = Module(cfg, "Cosmetics", "SeerEnabled", true, "SeerCosmetics", "Add the Horned Seer set: a hood with crescent horns, chains, crystals and a blindfold (Hat), a black robe with a tattered green shawl and a deer-skull pauldron (Body Top), a long ragged robe with a green veil and crystal chains (Body Bottom) and bell sleeves (Arm Right, Arm Left). The green takes the colour you pick in the cosmetics menu. Takes effect on restart.");
        JesterCosmeticsEnabled = Module(cfg, "Cosmetics", "JesterEnabled", true, "JesterCosmetics", "Add the Card Jester set: a three-pointed hat with bells and playing cards (Hat), a smiling porcelain mask (Face Top), a red-and-black doublet with a ruff and peplum (Body Top), wide split trousers (Body Bottom) and puffed sleeves (Arm Right, Arm Left). The red takes the colour you pick in the cosmetics menu. Takes effect on restart.");
        BrideCosmeticsEnabled = Module(cfg, "Cosmetics", "BrideEnabled", true, "BrideCosmetics", "Add the Ghost Bride set: a blue rose crown with a tattered veil (Hat), a lace corset (Body Top), a long tattered lace skirt (Body Bottom) and torn lace gloves (Arm Right, Arm Left). The lace takes the colour you pick in the cosmetics menu. Takes effect on restart.");
        AngelCosmeticsEnabled = Module(cfg, "Cosmetics", "AngelEnabled", true, "AngelCosmetics", "Add the Mossy Stone Angel set: a stone veil with a moss crown (Hat), a stone robe with a ruffled collar, with or without tall folded stone wings (Body Top), a long draped robe with moss (Body Bottom) and ruffled sleeves (Arm Right, Arm Left). The stone takes the colour you pick in the cosmetics menu. Takes effect on restart.");
        GildedCosmeticsEnabled = Module(cfg, "Cosmetics", "GildedEnabled", true, "GildedCosmetics", "Add the Gilded Shadow set: a gold-trimmed hood (Hat), a corset with an open coat, spiked shoulders and a necklace (Body Top), a long open veil skirt over shorts with chains and moon pendants (Body Bottom), puff sleeves (Arm Right, Arm Left) and stockings with black boots, one dark and one cream (Leg Right, Leg Left). The brown fabric and the stockings take the colour you pick in the cosmetics menu. Everyone in the lobby needs this setting to see them. Takes effect on restart.");
        FairyCosmeticsEnabled = Module(cfg, "Cosmetics", "FairyEnabled", true, "FairyCosmetics", "Add the Petal Fairy set: a pink crop top with choker and flowers (Body Top), the same top with four glowing petal wings (Body Top), a wrap skirt over striped shorts (Body Bottom), off-shoulder sleeves (Arm Right, Arm Left) and striped stockings with slouch boots (Leg Right, Leg Left). The pink fabric, wings and stocking stripes take the colour you pick in the cosmetics menu. Everyone in the lobby needs this setting to see them. Takes effect on restart.");
        LegateCosmeticsEnabled = Module(cfg, "Cosmetics", "LegateEnabled", true, "LegateCosmetics", "Add the Legate set: a silver muscle cuirass with baldric and belt (Body Top), the same cuirass with a red mantle and long cape (Body Top), a war skirt of tooled leather strips (Body Bottom) and red leather bracers (Arm Right, Arm Left). The red cloth and leather take the colour you pick in the cosmetics menu. Everyone in the lobby needs this setting to see them. Takes effect on restart.");
        StarlightCosmeticsEnabled = Module(cfg, "Cosmetics", "StarlightEnabled", true, "StarlightCosmetics", "Add the seven-part Starlight Gown set: galaxy bodice with choker (Body Top), high-low skirt with lace hems and belt (Body Bottom), lace sleeves (Arm Right, Arm Left), thigh-high galaxy boots (Leg Right, Leg Left) and drop earrings (Ears). The galaxy fabric, its glowing stars, the lace and the gems take the colour you pick in the cosmetics menu. Everyone in the lobby needs this setting to see them. Takes effect on restart.");
        NetSeconds = BindFloat(cfg, "Net Launcher", "RootSeconds", 5f, 1f, 30f, "How long a net pins a monster to the floor. It can still turn and attack anyone in reach.");
        NetBreakChance = BindFloat(cfg, "Net Launcher", "BreakChancePerSecond", .12f, 0f, 1f, "Chance each second that the monster tears the net and breaks free early (0.12 = about 45% over 5 seconds).");
        NetShots = BindInt(cfg, "Net Launcher", "Shots", 8, 1, 20, "Nets per full battery (one battery bar per net).");
        StaffDamage = BindInt(cfg, "Wizard Staff", "BoltDamage", 75, 1, 1000, "Total lightning damage per bolt, split evenly between the monsters it strikes (75 = 75 to one, 38 + 37 to two, 25 each to three).");
        StaffRange = BindFloat(cfg, "Wizard Staff", "Range", 5f, 1f, 20f, "How far (metres) the first bolt reaches from the staff tip.");
        StaffShockSeconds = BindFloat(cfg, "Wizard Staff", "ShockSeconds", 4f, 0f, 15f,
            "Everything a bolt strikes is shocked this long: players move 50% slower and recover stamina half as fast; monsters move 50% slower and deal 25% less damage. 0 turns it off.");
        StaffPlayerDamagePercent = BindFloat(cfg, "Wizard Staff", "PlayerDamagePercent", 30f, 0f, 100f,
            "A bolt aimed straight at a teammate strikes them (and chains to nearby players) for this share of its damage. 0 = shock only, no damage.");
        StaffMaxTargets = BindInt(cfg, "Wizard Staff", "MaxTargets", 3, 1, 10, "Most monsters one bolt can strike, including the first (the chain jumps to the nearest unstruck monster).");
        StaffChainRange = BindFloat(cfg, "Wizard Staff", "ChainRange", 5f, 1f, 20f, "Farthest jump (metres) from one struck monster to the next.");
        StaffAimAngle = BindFloat(cfg, "Wizard Staff", "AimAssistAngle", 20f, 0f, 60f, "If the aim line misses, the bolt still finds a monster within this many degrees of it (0 = exact aim only).");
        StaffCharges = BindInt(cfg, "Wizard Staff", "Charges", 8, 1, 20, "Bolts per full battery (one battery bar per bolt).");
        CircletEnabled = Module(cfg, "Cosmetics", "WingedCircletEnabled", true, "WingedCirclet",
            "Player ping marker: when someone pings a player, a floating winged circlet appears above that player's head for the length of the ping, coloured to match the player's own colour (black, cyan, yellow, green, white, purple or red). Not a cosmetic and not always shown. Local visuals only: each player can turn it off for themselves.");
        // Lived in a "Cosmetics" section before 0.7.12; it is a ping marker, so it now sits with the ping settings.
        var orphans = Orphans(cfg);
        var circletScaleDef = new ConfigDefinition("Ping", "WingedCircletScale");
        bool circletScaleExisted = orphans != null && orphans.ContainsKey(circletScaleDef);
        CircletScale = BindFloat(cfg, "Ping", "WingedCircletScale", 1f, .5f, 2f, "Size of the winged circlet shown above a pinged player (1 = default).");
        if (!circletScaleExisted && orphans != null && orphans.TryGetValue(new ConfigDefinition("Cosmetics", "WingedCircletScale"), out var oldCircletScale))
            CircletScale.SetSerializedValue(oldCircletScale);
        IceBlockEnabled = Module(cfg, "Cosmetics", "IceBlockEnabled", true, "IceBlock",
            "Monsters frozen by the Ice Pistol are encased in a see-through block of ice with crystal shards, cold mist and frost sparkles. Local visuals only.");
        HeroAuraEnabled = Module(cfg, "Cosmetics", "HeroAuraEnabled", true, "HeroAura",
            "Give enemy souls a red-purple aura (soft halo, light rays, sparkles and a light) while a ping is on the soul. Local visuals only. Applies on game restart.");
        UpgradesListScaleEnabled = Module(cfg, "HUD", "UpgradesListScaleEnabled", true, "UpgradesListScale",
            "Shrink the native UPGRADES list (names and level numbers together) just enough to fit on screen when a modded run has more upgrades than fit. Short lists stay full size. Local only.");
        FailVoteEnabled = Module(cfg, "Run", "FailVoteEnabled", true, "FailVote",
            "When everyone dies on a level, vote for 15 seconds: [1] restart the level or [2] a random map, instead of going to the death arena. Closes early when everyone has voted; a tie or no votes restarts. HOST-SIDE.");
        HostRunControlsEnabled = Module(cfg, "Run", "HostRunControlsEnabled", true, "HostRunControls",
            "Host-only Esc menu buttons: RESTART LEVEL (on levels) and RETURN TO LOBBY (keeps everyone together; saves first in the shop). Both ask for a second click.");
        Testing.TestShortcuts.Bind(cfg);
        LateJoinEnabled = Module(cfg, "Late Join", "Enabled", true, "LateJoin", "Host keeps the room/lobby joinable during a run/shop/truck (REPO normally seals it at run start) and re-syncs mod state to anyone who connects mid-game.");
        PingEnabled = Module(cfg, "Ping", "Enabled", true, "Ping", "Enable the ping system: press the ping key to mark what you're looking at for the whole team.");
        PingUnderglowEnabled = Module(cfg, "Ping", "UnderglowEnabled", true, "PingUnderglow",
            "Pinged loot, items, carts and other physical objects get a neon underglow in the ping colour: coloured light on the floor beneath them for as long as the ping lasts. Local visuals only.");
        ItemUnstuckEnabled = Module(cfg, "Item Unstuck", "Enabled", true, "ItemUnstuck", "Master switch for the valuable safety-net features (relocate stuck loot, invulnerable-until-grab, undamaged-value HUD). The relocate/invuln parts are HOST-SIDE (computed by the host).");
        ChallengesEnabled = Module(cfg, "Challenges", "Enabled", true, "Challenges",
            "Run levels may get a challenge shrine: crouch on it to start a timed challenge (wisp escort, target practice, hunt, cull or smash). Winning gives a skill point for everyone, an upgrade or a cosmetic box. Host-side.");
        LootSafetyNetEnabled = Module(cfg, "Loot Safety Net", "Enabled", true, "LootSafetyNet",
            "Host-side: while an extraction point is open and the loot the team can reach is not enough to fill it, spawn more (replaces the game's own top-up, which also counts loot nobody can reach). Everyone gets a notice.");
        var spectatorMap = SpectatorMapConfig.Bind(cfg);
        SpectatorMapEnabled = spectatorMap.Enabled;
        SpectatorMapHoldKey = spectatorMap.HoldKey;
        SpectatorMapScreenFraction = spectatorMap.ScreenFraction;
        RoundResultsEnabled = Module(cfg, "Round Results", "Enabled", true, "RoundResults", "After each level show an MVP / OOPS CHAMP screen with per-player stats: loot broken, loot delivered, deaths and revives. Stats are recorded by the host, so the host must run this mod for the board to appear.");
        MapVoteEnabled = Module(cfg, "Map Vote", "Enabled", true, "MapVote", "While in the truck lobby, let players vote on the next map from a few random candidates. Whatever is leading when the truck departs is used instead of the game's random pick. Vote with the number keys (or the on-screen buttons). If nobody votes, the game's normal random selection is used, so this is harmless when ignored. HOST-SIDE: the host applies the winning map and syncs it to everyone normally.");
        AutoBackupEnabled = Module(cfg, "Auto Backup Saves", "Enabled", true, "AutoBackup", "Create extra snapshots when REPO would delete a run. Snapshots are never pruned. The original checkpoint is always protected, even if this module is disabled. Legacy KeepSaveLoadable and MaxBackupsPerSave settings are ignored.");
        VoiceRecoveryEnabled = Module(cfg, "Voice Recovery", "Enabled", true, "VoiceRecovery", "Retry failed local microphone capture up to three times per voice object. Does not override mute or push-to-talk. Restarts voice streams after audio device changes.");
        InventorySlotsEnabled = Module(cfg, "Player Upgrades", "InventorySlotsEnabled", true, "InventorySlots", "Sell Inventory Slot upgrades: +1 slot per level, up to seven total slots (four upgrade levels). Requires PlayerUpgrades and REPOLib.");
        MorePlayersEnabled = Module(cfg, "More Players", "Enabled", false, "MorePlayers", "Raise the room's max player count above REPO's default of 6. HOST-SIDE: the host's value defines the room cap; joiners don't need this on. Everyone in the lobby should still run the mod to avoid desync.");
        ShopPriceScaleEnabled = Module(cfg, "Shop", "PriceScaleEnabled", false, "ShopPriceScale", "Scale shop item prices up with the number of players, so bigger lobbies (which earn more) still feel expensive. HOST-SIDE: prices are computed by the host and synced.");
        ShoppingListEnabled = Module(cfg, "Shopping List", "Enabled", false, "ShoppingList", "While in the shop, show a panel listing everything in the cart (yours and your friends'): quantity, name and summed price per item, colour-coded by item type, with a running total vs. your team's currency.");
        LevelInfoEnabled = Module(cfg, "Level Info", "Enabled", true, "LevelInfo", "Show a panel on the right side of the screen that logs each level change (run number + name, plus any rolled surprise modifiers).");
        ServerInfoEnabled = Module(cfg, "Server Info", "Enabled", true, "ServerInfo", "Show a passive overlay in the lobby/menu with the current Photon region, ping, room code and player count. Reads connection state only — never opens sockets, so it's safe on the main menu.");
        PhotonHudEnabled = Module(cfg, "Photon HUD", "Enabled", false, "PhotonHud", "OPT-IN DIAGNOSTIC (default off). Show a small overlay on the main menu with reachability/latency to Photon region name servers. " + "Leave this OFF for normal play: it TCP-probes the same Photon name servers the game uses, and that connection churn can stall REPO's own 'pick best region' step (regions spin forever / stuck loading online). Only enable it briefly when you're actively diagnosing a connection problem.");
    }

    private static ConfigEntry<bool> Module(ConfigFile cfg, string oldSection, string oldKey, bool def, string key, string desc)
    {
        var newDef = new ConfigDefinition("Modules", key);
        var orphans = Orphans(cfg);
        bool existed = orphans != null && orphans.ContainsKey(newDef);
        var entry = cfg.Bind(newDef, def, new ConfigDescription(desc));
        if (!existed && orphans != null && orphans.TryGetValue(new ConfigDefinition(oldSection, oldKey), out var raw))
            entry.SetSerializedValue(raw);
        return entry;
    }

    // Values present in the .cfg that nothing has bound (yet). BepInEx keeps this non-public.
    private static Dictionary<ConfigDefinition, string> Orphans(ConfigFile cfg)
    {
        var prop = typeof(ConfigFile).GetProperty("OrphanedEntries",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
        return prop?.GetValue(cfg) as Dictionary<ConfigDefinition, string>;
    }

    public static void Init(ConfigFile cfg)
    {
        BindModules(cfg);

        DiagnosticDisablePatches = cfg.Bind(
            "Diagnostics", "DisablePatches", false,
            "DIAGNOSTIC (default off). If true, skip ALL Harmony gameplay patches. Used only to isolate whether the mod's patches interfere with Photon connecting. Leave off for normal play.");
        DiagnosticDisableRuntimeObjects = cfg.Bind(
            "Diagnostics", "DisableRuntimeObjects", false,
            "DIAGNOSTIC (default off). If true, skip creating the mod's runtime menu/HUD objects (networking, timer, ping, Photon HUD). Used only to isolate whether they interfere with Photon connecting. Leave off for normal play.");

        NetworkDiagnosticsEnabled = cfg.Bind("Voice Recovery", "NetworkDiagnostics", false,
            "Log actual Photon room RTT, jitter and cumulative reliable resends once per minute. Does not change transport or region.");
        AudioRecoveryKey = cfg.Bind("Voice Recovery", "RecoveryKey", UnityEngine.KeyCode.F9,
            "Hold Left Ctrl and press this key to reset Unity audio and restart voice streams locally. Briefly interrupts sound. None disables the shortcut.");

        RampStartLevel = BindInt(cfg,
            "General", "RampStartLevel", 4, 0, 30,
            "Completed-level offset for the difficulty ramp. The first boost lands on the first completed level after RampStartLevel, then one more every RampLevelsPerStep levels (start 1, step 3: boosts at 2, 5, 8 completed levels). With start 4 and step 1, the first boost is at 5 completed levels (the sixth playable level).");

        RampLevelsPerStep = BindInt(cfg,
            "General", "RampLevelsPerStep", 1, 1, 10,
            "How many levels of depth make up ONE difficulty step. 1 = stats grow every level; 2 = stats step up only every 2nd level past the start (stretches the ramp).");


        EnemyHealthPerLevel = BindFloat(cfg,
            "Enemies", "HealthPerLevel", 0.15f, 0f, 2f,
            "Extra enemy max HP per level past the ramp start. 0.15 = +15% per level (cumulative, additive).");
        EnemyHealthMax = BindFloat(cfg,
            "Enemies", "HealthMaxMultiplier", 3.0f, 1f, 20f,
            "Global upper cap on the depth HP multiplier, before level surprises. Per-enemy overrides take precedence. 3.0 = at most 3x native HP from the depth ramp.");
        // Free-form string (name:mult list) — REPOConfig can't render this sensibly, so hide it.
        EnemyHealthMaxPerEnemy = cfg.Bind(
            "Enemies", "HealthMaxPerEnemy", "Elsa:1.75, Loom:1.75, Trudge:2.25",
            new ConfigDescription(
                "Per-enemy HP cap overrides. Format: 'Name:multiplier, Name:multiplier'. Names match EnemyParent.enemyName (case-insensitive). A cap of 1 EXCLUDES that enemy from HP scaling. Defaults: Elsa and Loom up to x1.75, Trudge up to x2.25. Unlisted enemies use HealthMaxMultiplier. Enable LogEnemyNames to see exact names.",
                null, HideFromRepoConfig));
        LogEnemyNames = cfg.Bind(
            "Enemies", "LogEnemyNames", true,
            "Log each enemy's internal name the first time it spawns, so you can tune HealthMaxPerEnemy.");

        EnemyDamagePerLevel = BindFloat(cfg,
            "Enemies", "DamagePerLevel", 0.10f, 0f, 2f,
            "Extra enemy attack damage per level past the ramp start. 0.10 = +10% per level.");
        EnemyDamageMax = BindFloat(cfg,
            "Enemies", "DamageMaxMultiplier", 3.0f, 1f, 20f,
            "Upper cap on the enemy damage multiplier.");

        EnemySpeedPerLevel = BindFloat(cfg,
            "Enemies", "SpeedPerLevel", 0.06f, 0f, 1f,
            "Extra enemy movement speed per level past the ramp start. 0.06 = +6% per level.");
        EnemySpeedMax = BindFloat(cfg,
            "Enemies", "SpeedMaxMultiplier", 1.25f, 1f, 4f,
            "Upper cap on the enemy speed multiplier. Keep modest; too high makes enemies uncatchable/janky.");

        EnemyRespawnFasterPerLevel = BindFloat(cfg,
            "Enemies", "RespawnFasterPerLevel", 0.08f, 0f, 1f,
            "How much shorter the enemy respawn wait gets per level. 0.08 = -8% wait per level.");
        EnemyRespawnMinFactor = BindFloat(cfg,
            "Enemies", "RespawnMinFactor", 0.3f, 0.05f, 1f,
            "Floor for the respawn-wait multiplier. 0.3 = respawn wait never drops below 30% of vanilla.");

        QuotaPerLevel = BindFloat(cfg,
            "Quota", "QuotaPerLevel", 0.10f, 0f, 2f,
            "Extra extraction quota per level past the ramp start. 0.10 = +10% per level.");
        QuotaMax = BindFloat(cfg,
            "Quota", "QuotaMaxMultiplier", 5.0f, 1f, 20f,
            "Upper cap on the quota multiplier.");
        QuotaMaxLootFraction = BindFloat(cfg,
            "Quota", "QuotaMaxLootFraction", 0.9f, 0.1f, 1f,
            "Quota can never exceed this fraction of the total loot value on the level (keeps levels beatable). 0.9 = at most 90%.");

        // --- Surprise modifier effect strengths ---
        FragileLootFactor = BindFloat(cfg,
            "Modifier Tuning", "FragileLootFactor", 2.0f, 0.1f, 10f,
            "FragileLoot: multiplies loot fragility. >1 = breaks more easily. 2.0 = twice as fragile.");
        ProtectedLootFactor = BindFloat(cfg,
            "Modifier Tuning", "ProtectedLootFactor", 0.4f, 0.1f, 10f,
            "ProtectedLoot: multiplies loot fragility. <1 = harder to break. 0.4 = 60% less fragile.");
        HeavyLootFactor = BindFloat(cfg,
            "Modifier Tuning", "HeavyLootFactor", 1.75f, 0.1f, 10f,
            "HeavyLoot: multiplies loot mass. >1 = heavier.");
        LightLootFactor = BindFloat(cfg,
            "Modifier Tuning", "LightLootFactor", 0.5f, 0.1f, 10f,
            "LightLoot: multiplies loot mass. <1 = lighter.");
        LightMonsterMaxMass = BindFloat(cfg,
            "Modifier Tuning", "LightMonsterMaxMass", 3.0f, 0.1f, 50f,
            "LightMonsters: cap every monster's mass at this value, including late spawns. A held monster stops fighting the grab so one player without Strength upgrades can lift it. Frozen monsters remain unliftable.");
        HeavyMonsterMinMass = BindFloat(cfg,
            "Modifier Tuning", "HeavyMonsterMinMass", 7.0f, 0.1f, 50f,
            "HeavyMonsters: raise every monster's mass to at least this value (heavier = harder to move).");
        BiggerSoulDropsFactor = BindFloat(cfg,
            "Modifier Tuning", "BiggerSoulDropsFactor", 2.0f, 0.1f, 10f,
            "BiggerSoulDrops: multiplies the money value of each enemy soul drop. 2.0 = each drop worth twice as much. Does not change drop count.");
        MonsterPitDamageFactor = BindFloat(cfg,
            "Modifier Tuning", "MonsterPitDamageFactor", 0.5f, 0f, 5f,
            "MonstersPitTanky: multiplies death-pit chip damage dealt to monsters (moon 2+). 0.5 = monsters take half pit damage. Instant-kill pits on early moons are unaffected.");
        ExtraGachaCubesPerSpawn = BindInt(cfg,
            "Modifier Tuning", "ExtraGachaCubesPerSpawn", 1, 0, 10,
            "MoreGachaCubes: how many additional cosmetic 'gacha' cubes to spawn per cube the game would normally place. Capped by the level's available spawn spots. 1 = roughly double.");
        DangerousLootDamageFactor = BindFloat(cfg,
            "Modifier Tuning", "DangerousLootDamageFactor", 2.0f, 0.1f, 10f,
            "DangerousLoot: multiplies the damage hazardous valuables (cursed dolls, radioactive cups, barrels, saws, etc.) deal to players. 2.0 = double damage.");
        SoulsExplodeHarderFactor = BindFloat(cfg,
            "Modifier Tuning", "SoulsExplodeHarderFactor", 2.0f, 0.1f, 10f,
            "SoulsExplodeHarder: multiplies the player damage of enemy-soul explosions (moon 4+). 2.0 = double explosion damage.");
        ThornyMonstersChance = BindFloat(cfg,
            "Modifier Tuning", "ThornyMonstersChance", 0.5f, 0f, 1f,
            "ThornyMonsters: chance that each monster on the level has thorns. 0.5 = about half of them.");
        ThornyMonstersMaxReflect = BindFloat(cfg,
            "Modifier Tuning", "ThornyMonstersMaxReflect", 0.10f, 0f, 0.5f,
            "ThornyMonsters: the most of a melee hit a thorny monster reflects to the attacker. Each thorny monster rolls between a third of this and this.");
        SpreadUpgradesCount = BindInt(cfg,
            "Modifier Tuning", "SpreadUpgradesCount", 3, 0, 10,
            "SpreadUpgrades: how many upgrade item pickups to scatter across the special level. They can't be used on that level, only once back in the truck.");
        MysteryBoxCount = BindInt(cfg,
            "Modifier Tuning", "MysteryBoxCount", 3, 1, 8,
            "MysteryBoxes: how many mystery boxes are placed across the map. They use navigation points, never valuable spawn spots. Each box accepts one soul.");
        MysteryBoxMoneyMin = BindInt(cfg,
            "Modifier Tuning", "MysteryBoxMoneyMin", 3000, 1000, 100000,
            "MysteryBoxes: smallest money prize in dollars, before depth scaling. Paid to the team wallet in whole $1K.");
        MysteryBoxMoneyMax = BindInt(cfg,
            "Modifier Tuning", "MysteryBoxMoneyMax", 9000, 1000, 200000,
            "MysteryBoxes: largest money prize in dollars, before depth scaling (deeper levels pay a little more).");
        MysteryBoxWeightUpgrade = BindInt(cfg,
            "Modifier Tuning", "MysteryBoxWeightUpgrade", 35, 0, 100,
            "MysteryBoxes: relative chance of a random upgrade pickup (maxed upgrades are skipped).");
        MysteryBoxWeightMoney = BindInt(cfg,
            "Modifier Tuning", "MysteryBoxWeightMoney", 30, 0, 100,
            "MysteryBoxes: relative chance of a money prize.");
        MysteryBoxWeightCostume = BindInt(cfg,
            "Modifier Tuning", "MysteryBoxWeightCostume", 15, 0, 100,
            "MysteryBoxes: relative chance of a costume piece: every player unlocks one of the mod's costume parts they don't own yet.");
        CostumeRewardsEnabled = cfg.Bind(
            "Quests", "CostumeRewardsEnabled", true,
            "Costume pieces as rewards: challenge shrines, mystery boxes, special contracts and rank A+ quests can give every player one of the mod's costume parts they don't own yet (a Rare cosmetic token once they own them all).");
        MysteryBoxWeightWeapon = BindInt(cfg,
            "Modifier Tuning", "MysteryBoxWeightWeapon", 35, 0, 100,
            "MysteryBoxes: relative chance of a weapon or grenade that only exists on this level (it is not added to your saved items).");
        SpreadUpgradesAllowed = cfg.Bind(
            "Modifier Tuning", "SpreadUpgradesAllowed", "",
            new ConfigDescription(
                "SpreadUpgrades: comma-separated list of upgrade item names allowed to be scattered (case-insensitive, substring match against the item's internal name). Empty = any upgrade. Enable LogSpreadUpgradeNames to print the exact names to the log.",
                null, HideFromRepoConfig));
        LogSpreadUpgradeNames = cfg.Bind(
            "Modifier Tuning", "LogSpreadUpgradeNames", false,
            "Log the internal names of all upgrade items the first time a SpreadUpgrades level generates, so you can fill in SpreadUpgradesAllowed.");
        AutoReviveAtCar = cfg.Bind(
            "Modifier Tuning", "AutoReviveAtCar", true,
            "AutoRevive: bring the auto-revived player back at the level's spawn point (the truck/car drop-off) and fully heal them, instead of reviving them on the spot where they died. Still only one auto-revive per player per level.");
        LevelTimerSeconds = BindFloat(cfg,
            // New key names (0.8.2) so existing configs pick up the new defaults (7 min, +1 min per kill, +2 min per quota).
            "Modifier Tuning", "LevelTimerStartSeconds", 420f, 30f, 1800f,
            "LevelTimer: starting countdown in seconds. When it hits 0 the level restarts and rerolls to a fresh layout (progress is not lost, the level is just regenerated).");
        LevelTimerKillBonus = BindFloat(cfg,
            "Modifier Tuning", "LevelTimerKillSeconds", 60f, 0f, 300f,
            "LevelTimer: seconds added to the clock for each enemy killed.");
        LevelTimerDeathPenalty = BindFloat(cfg,
            "Modifier Tuning", "LevelTimerDeathPenalty", 30f, 0f, 300f,
            "LevelTimer: seconds removed from the clock each time a player dies.");
        LevelTimerQuotaBonus = BindFloat(cfg,
            "Modifier Tuning", "LevelTimerQuotaSeconds", 120f, 0f, 600f,
            "LevelTimer: seconds added when an extraction point's quota is completed.");

        // --- Late Join ---

        // --- Ping ---
                PingKey = cfg.Bind(
            "Ping", "PingKey", UnityEngine.KeyCode.Mouse2,
            "Key/button to place a ping on whatever you're aiming at (default: middle mouse).");
        PingMaxDistance = BindFloat(cfg,
            "Ping", "MaxDistance", 60f, 5f, 200f,
            "Maximum distance the ping raycast reaches. Beyond this a ping is placed in mid-air along your aim.");
        PingDurationSeconds = BindFloat(cfg,
            "Ping", "DurationSeconds", 4f, 1f, 30f,
            "How long a ping marker stays visible (seconds).");
        PingMarkerScale = BindFloat(cfg,"Ping","MarkerScale",1f,.5f,2.5f,"Scale of the large cyan location ring and arrow. Object silhouettes retain their actual size.");
        PingSoundVolume = BindFloat(cfg,"Ping","SoundVolume",.28f,0f,1f,"Volume of distinct target-type ping cues through the game's mixer. 0 mutes ping sounds.");
        MaxLocationPings = BindInt(cfg,
            "Ping", "MaxLocationPings", 3, 1, 20,
            "Maximum live Location pings a single player can have at once (needs the 'Ping Anywhere' upgrade). Placing more drops your oldest.");
        MaxActivePings = BindInt(cfg,
            "Ping", "MaxActivePings", 3, 1, 20,
            "Global anti-flood cap: the most ping markers shown on screen at once (across all players). Placing more drops the oldest.");

        // --- Item Unstuck ---
                ValuableRelocateStuck = cfg.Bind(
            "Item Unstuck", "RelocateStuck", true,
            "Recover valuables embedded in geometry or wedged in a frame while a player tries to pull them free. Uses a collision-checked nearby position or a validated previous safe spot. Host-side.");
        ValuableStuckSeconds = BindFloat(cfg,
            "Item Unstuck", "StuckSeconds", 6f, 2f, 60f,
            "Seconds an embedded valuable must remain settled, or a wedged valuable must resist a sustained grab, before a recovery is attempted.");
        ValuableInvulnerableUntilGrab = cfg.Bind(
            "Item Unstuck", "InvulnerableUntilGrab", true,
            "Valuables take no damage / lose no value until a player grabs them for the first time (uses the game's own indestructible flag). Host-side.");

        // --- Challenges ---
        ChallengeChancePercent = BindFloat(cfg,
            "Challenges", "ShrineChancePercent", 100f, 0f, 100f,
            "Chance that a run level gets a challenge shrine.");

        // --- Loot Safety Net ---
        LootSafetyMinPlayerDistance = BindFloat(cfg,
            "Loot Safety Net", "MinPlayerDistance", 20f, 0f, 60f,
            "New loot prefers spawn spots at least this far (metres) from every player, like the game's own top-up. Closer spots are used only when no far spot is free.");
        LootSafetyMaxSpawns = BindInt(cfg,
            "Loot Safety Net", "MaxSpawnsPerTopUp", 25, 1, 100,
            "Most valuables one top-up may spawn.");
        LootSafetyRevealOnMap = cfg.Bind(
            "Loot Safety Net", "RevealOnMap", true,
            "Mark the new loot on the map right away so players can find it.");
        LootSafetyLog = cfg.Bind(
            "Loot Safety Net", "DiagnosticLog", true,
            "While an extraction point is open, log the loot count against its goal about once a minute, plus every top-up and the loot it did not count.");

        // --- Quests ---
        QuestsEnabled = cfg.Bind(
            "Quests", "Enabled", true,
            "Enable the quest progression layer: each run level offers 2-3 team quests plus a personal quest, scaled to team size / day / your contract rank. Completing quests advances your rank (F..SSS) and pays money or upgrade items. Team quests are rolled by the host and synced to everyone.");
        QuestPeekKey = cfg.Bind(
            "Quests", "PeekKey", UnityEngine.KeyCode.J,
            "Press this key to show/hide the quest tracker (default J). Quests are shown by default; your choice is kept between levels.");
        QuestsPerLevelMin = BindInt(cfg,
            "Quests", "QuestsPerLevelMin", 2, 1, 5,
            "Minimum number of TEAM quests offered per level.");
        QuestsPerLevelMax = BindInt(cfg,
            "Quests", "QuestsPerLevelMax", 3, 1, 5,
            "Maximum number of TEAM quests offered per level.");
        QuestCompletionsPerRank = BindInt(cfg,
            "Quests", "CompletionsPerRank", 10, 1, 100,
            "How many quest completions advance your contract rank by one for the LOWER ranks (F->E->D->C->B->A). 10 = ten quests per step. The top ranks use their own larger requirements below.");
        QuestCompletionsAtoS = BindInt(cfg,
            "Quests", "CompletionsAtoS", 40, 1, 500,
            "How many quest completions are required to advance from rank A to rank S.");
        QuestCompletionsStoSS = BindInt(cfg,
            "Quests", "CompletionsStoSS", 42, 1, 500,
            "How many quest completions are required to advance from rank S to rank SS.");
        QuestCompletionsSStoSSS = BindInt(cfg,
            "Quests", "CompletionsSStoSSS", 50, 1, 500,
            "How many quest completions are required to advance from rank SS to rank SSS.");
        QuestMoneyBase = BindInt(cfg,
            "Quests", "MoneyRewardBase", 3000, 0, 1000000,
            "Base money reward for a quest at rank F / one player. It scales up with team size (+25% per extra player), day, and rank.");
        QuestSkillPoints = BindInt(cfg, "Quests", "SkillPointReward", 1, 0, 10,
            "Skill points every finished quest gives on top of its normal reward. Team quests give them to every player, personal quests to you. 0 = none.");
        QuestSkillPointsHighRank = BindInt(cfg, "Quests", "SkillPointRewardSSPlus", 2, 0, 10,
            "Skill points a finished quest gives at contract rank SS and SSS instead.");
        QuestSkillPointChance = BindFloat(cfg, "Quests", "SkillPointChance", .35f, 0f, 1f,
            "Chance that a quest comes with skill points at rank F; +5% per contract rank, at most 75%. Funny quests and contracts never give points.");
        QuestContractChance = BindFloat(cfg, "Quests", "ContractChance", .35f, 0f, 1f,
            "Chance per level of a special CONTRACT quest: harder, and it gives every player a level of a quest-only skill tree perk (Contracts page).");
        SkillBoxesEnabled = cfg.Bind("Skill Boxes", "Enabled", true,
            "Small glowing boxes on every run level. Walk into one to open it: it gives you 0-3 skill points. Host setting.");
        SkillBoxMin = BindInt(cfg, "Skill Boxes", "MinPerLevel", 1, 0, 5, "Fewest skill boxes placed on a level.");
        SkillBoxMax = BindInt(cfg, "Skill Boxes", "MaxPerLevel", 5, 0, 5, "Most skill boxes placed on a level.");
        SkillBoxWeight0 = BindInt(cfg, "Skill Boxes", "Chance0Points", 65, 0, 100, "Weight of an empty box (0 points). Default 65 of 100.");
        SkillBoxWeight1 = BindInt(cfg, "Skill Boxes", "Chance1Point", 20, 0, 100, "Weight of a 1-point box. Default 20 of 100.");
        SkillBoxWeight2 = BindInt(cfg, "Skill Boxes", "Chance2Points", 10, 0, 100, "Weight of a 2-point box. Default 10 of 100.");
        SkillBoxWeight3 = BindInt(cfg, "Skill Boxes", "Chance3Points", 5, 0, 100, "Weight of a 3-point box. Default 5 of 100.");
        RetryCrystalsEnabled = cfg.Bind("Battery Crystals", "Enabled", true,
            "When a level is retried (restart, random map or timer reroll), small battery crystals lie around the map. HP and battery charge carry over between attempts, so walk into a crystal to top up your emptiest battery item. Host setting.");
        RetryCrystalMin = BindInt(cfg, "Battery Crystals", "MinPerLevel", 3, 0, 8, "Fewest battery crystals on a retried level.");
        RetryCrystalMax = BindInt(cfg, "Battery Crystals", "MaxPerLevel", 8, 0, 8, "Most battery crystals on a retried level.");
        RetryCrystalCharge = BindFloat(cfg, "Battery Crystals", "ChargePercent", 20f, 1f, 100f,
            "Battery charge one crystal adds to the emptiest battery item you hold or carry (20 = about one bar of five).");
        QuestFunnyEnabled = cfg.Bind("Quests", "FunnyQuests", true,
            "Allow silly side-quests (talk to yourself, spin around, pay respects...). At most one per level, small cash reward, no skill points.");
        QuestItemRewardsEnabled = cfg.Bind(
            "Quests", "ItemRewardsEnabled", true,
            "Allow higher-rank quests (rank C and above) to sometimes pay out a random upgrade item on the level instead of money.");
        QuestCosmeticRewardsEnabled = cfg.Bind(
            "Quests", "CosmeticRewardsEnabled", true,
            "Allow higher-rank quests (rank C and above) to sometimes pay out a cosmetic 'gacha' box (a rarity cube to roll a skin) on the level instead of money. Higher ranks bias toward rarer boxes.");
        QuestExactUpgradeRewardsEnabled = cfg.Bind(
            "Quests", "ExactUpgradeRewardsEnabled", true,
            "Allow higher-rank quests (rank C and above) to sometimes pay out a SPECIFIC upgrade item (e.g. a Health or Sprint Speed upgrade) on the level instead of money.");
        QuestShopItemRewardsEnabled = cfg.Bind(
            "Quests", "ShopItemRewardsEnabled", true,
            "Allow higher-rank quests (rank C and above) to sometimes spawn a random general shop item (grenade, health pack, gun, tool, etc.) on the level instead of money.");
        QuestHealRewardsEnabled = cfg.Bind(
            "Quests", "HealRewardsEnabled", true,
            "Allow higher-rank quests (rank C and above) to sometimes heal the whole squad instead of paying money.");
        QuestTempStatRewardsEnabled = cfg.Bind(
            "Quests", "TempStatRewardsEnabled", true,
            "Allow higher-rank quests (rank C and above) to sometimes grant every player a TEMPORARY stat buff (stamina/sprint speed/extra jump/grab strength) that lasts only for the current level.");
        QuestHealAmount = BindInt(cfg,
            "Quests", "HealRewardAmount", 100, 1, 100,
            "How much HP a squad-heal quest reward restores to each player (capped at each player's max health).");

        // --- Round Results ---
                RoundResultsSeconds = BindFloat(cfg,
            "Round Results", "ShowSeconds", 20f, 5f, 60f,
            "How long the results screen stays up after a level ends.");
        RoundResultsKey = cfg.Bind(
            "Round Results", "ToggleKey", UnityEngine.KeyCode.F7,
            "Press to dismiss the results screen, or to bring the last results back.");

        // --- Utility Drones ---
                const string Gear = "Gear";
                                                                        DefenceAbsorb = BindInt(cfg, Gear, "DefenceAbsorb", 350, 50, 2000,
            "Total damage a fully charged Defence DRONE can absorb for the player it is attached to.");
        DefenceItemAbsorb = BindInt(cfg, Gear, "DefenceItemAbsorb", 3500, 100, 50000,
            "Defence ORB (protects loot): value, in dollars, a fully charged orb can absorb from valuables breaking inside its field. The Defence DRONE protects a player instead, see DefenceAbsorb.");
        DefenceDroneSeconds = BindFloat(cfg, Gear, "DefenceDroneSeconds", 45f, 5f, 600f,
            "Defence DRONE: seconds from a full battery to empty while it is running, even if the player takes no damage (hits drain it faster).");
        DefenceIdleDrainPerMinute = BindFloat(cfg, Gear, "DefenceIdleDrainPercentPerMinute", 20f, 0f, 60f,
            "Slow passive discharge of an active Defence ORB, in percent of battery per minute, applied whether or not it is taking hits (hits drain it further). 20 = about 5 minutes from full.");
        ProtectionReductionPercent = BindInt(cfg, Gear, "ProtectionReductionPercent", 25, 5, 90,
            "Damage reduction inside an active Protection Orb.");
        PoisonHitDamage = BindInt(cfg, Gear, "PoisonHitDamage", 7, 1, 100, "Direct damage of a poison bolt.");
        PoisonTotalDamage = BindInt(cfg, Gear, "PoisonTotalDamage", 125, 1, 1000, "Total poison damage over the full duration.");
        PoisonSeconds = BindFloat(cfg, Gear, "PoisonSeconds", 15f, 1f, 60f,
            "Poison duration. Each hit refreshes it to this value (it does not stack).");
        IceFreezeSeconds = BindFloat(cfg, Gear, "IceFreezeSeconds", 4.5f, 0.5f, 30f, "How long the Ice Pistol freezes a monster or player. Players receive a visible ice coating and movement pause without shot damage, including in the shop. Repeated hits refresh the timer.");
        IceGunDamagePercent = BindInt(cfg, Gear, "IceGunDamagePercent", 60, 10, 100,
            "Firearm damage frozen enemies take, in percent of normal.");
        IceMeleeBonusPercent = BindInt(cfg, Gear, "IceMeleeBonusPercent", 35, 0, 300,
            "Extra melee damage frozen enemies take, in percent.");
        DecoySeconds = BindFloat(cfg, Gear, "DecoySeconds", 5f, 1f, 30f, "How long the Decoy Grenade attracts monsters once armed.");
        StickyFreeLiftMass = BindFloat(cfg, Gear, "StickyFreeLiftMass", 10f, 1f, 100f,
            "Approximate mass (kg) a player with base grab strength can lift freely. The Sticky Pistol can pull things up to what ONE strength level less could lift.");

                                        LinkMaxDistance = BindFloat(cfg,
            "Drones", "LinkMaxDistance", 6f, 1f, 12f,
            "Link drone: maximum distance (metres) between the two attached objects for the cable to connect. The cable fits the gap when it connects.");
        SilencerNoiseMultiplier = BindFloat(cfg,
            "Drones", "SilencerNoiseMultiplier", 0.08f, 0.01f, 0.35f,
            "Silencer drone: multiplier applied to the attached target's emitted sound, voice and the radius enemies investigate it from. 0.08 = 8% as loud.");

        // --- Map Vote ---
        InventorySlot4Key = cfg.Bind("Inventory Slots", "Slot4Key", UnityEngine.KeyCode.Alpha4, "Store/retrieve from inventory slot 4.");
        InventorySlot5Key = cfg.Bind("Inventory Slots", "Slot5Key", UnityEngine.KeyCode.Alpha5, "Store/retrieve from inventory slot 5.");
        InventorySlot6Key = cfg.Bind("Inventory Slots", "Slot6Key", UnityEngine.KeyCode.Alpha6, "Store/retrieve from inventory slot 6.");
        InventorySlot7Key = cfg.Bind("Inventory Slots", "Slot7Key", UnityEngine.KeyCode.Alpha7, "Store/retrieve from inventory slot 7.");
        MapVoteHudKey = cfg.Bind("Map Vote", "ToggleHudKey", UnityEngine.KeyCode.F6,
            "Hide or show the local map-vote HUD without cancelling the vote. Number-key voting continues while hidden.");
                MapVoteChoices = BindInt(cfg,
            "Map Vote", "Choices", 3, 2, 6,
            "How many random candidate maps to offer for the vote each lobby.");
        MapVoteSeconds = BindFloat(cfg,
            "Map Vote", "Seconds", 60f, 10f, 300f,
            "How long the vote stays open (host-authoritative). It closes early the moment everyone in the lobby has voted. When it closes, the leading map is locked in and used when the truck departs. If nobody votes, the game's normal random selection is used.");

        // --- Photon reachability HUD ---
                PhotonHudRegions = cfg.Bind(
            "Photon HUD", "Regions", "ns=ns.photonengine.io, exitgames=ns.exitgames.com",
            new ConfigDescription(
                "Comma-separated 'label=host' (or 'label=host:port') pairs to TCP-probe for the HUD. " +
                "Default probes the Photon name servers (port 443) — the endpoint the game contacts when connecting with an empty/best region, which is where 'stuck loading online' usually hangs.",
                null, HideFromRepoConfig));

        // --- Level Info panel ---
                LevelInfoToggleKey = cfg.Bind(
            "Level Info", "ToggleKey", UnityEngine.KeyCode.F8,
            "Key to hide/show the level info announcement.");

        // --- Server Info panel ---

        // --- Shopping List panel ---

        // --- More Players ---
                MorePlayersMax = BindInt(cfg,
            "More Players", "MaxPlayers", 6, 2, 20,
            "Maximum players allowed in the room when More Players is enabled. REPO supports up to 20 internally. Large lobbies are unofficial and may be unstable.");

        // --- Shop ---
                ShopPriceBaselinePlayers = BindInt(cfg,
            "Shop", "PriceBaselinePlayers", 4, 1, 20,
            "Player count at which prices are unchanged. Each player above this raises prices by PricePerExtraPlayer; at or below it, prices are vanilla.");
        ShopPricePerExtraPlayer = BindFloat(cfg,
            "Shop", "PricePerExtraPlayer", 0.15f, 0f, 2f,
            "Extra shop price per player above the baseline. 0.15 = +15% per extra player (e.g. 8 players with baseline 4 = +60%).");
        ShopDuplicatesExtra = BindInt(cfg,
            "Shop", "DuplicatesExtra", 0, 0, 10,
            "Allow the same item (e.g. a gun) to appear more than once in a single shop by adding this many extra copies of each item to the shop pool. 0 = vanilla (usually one of each).");
        ShopExtraItems = BindInt(cfg, "Shop", "ExtraItems", 4, 0, 20,
            "Extra different regular items (weapons, tools, drones...) stocked per shop visit on top of the game's 8. Never copies of the same item; they go on free shelf slots, using a bigger free slot when no slot of their own size is left.");
        ShopExtraUpgrades = BindInt(cfg, "Shop", "ExtraUpgrades", 3, 0, 20,
            "Extra different upgrades stocked per shop visit on top of the game's 3, on free shelf slots (a bigger free slot when no slot of their own size is left).");

        // --- Truck upgrades ---
                TruckHealPercentPerLevel = BindFloat(cfg,
            "Truck Upgrades", "HealBonusPerLevel", 0.05f, 0f, 1f,
            "Heal Power: the truck heal is multiplied by (1 + p) per level, compounding. Multiplier = (1 + p)^level. At p=0.05, a 35 HP heal becomes 36.75 / 38.59 / 40.52 at levels 1/2/3 (truck heal only).");
        TruckHealMaxLevel = BindInt(cfg,
            "Truck Upgrades", "HealPowerMaxLevel", 10, 0, 20,
            "Heal Power: maximum purchasable level.");
        TruckHealBaseCost = BindInt(cfg,
            "Truck Upgrades", "HealBaseCost", 10000, 0, 1000000,
            "Heal Power: cost of the first level (grows by CostGrowth each level).");
        TruckChargePercentPerLevel = BindFloat(cfg,
            "Truck Upgrades", "ChargeReductionPerLevel", 0.05f, 0f, 0.95f,
            "Charge Efficiency: multiply remaining energy-pool drain by (1-p) per level, compounding like Slower Stamina Use, with a 5% minimum. At p=0.05, levels 1/2/3 spend 95%/90.25%/85.7% of normal energy.");
        TruckChargeMaxLevel = BindInt(cfg,
            "Truck Upgrades", "ChargePowerMaxLevel", 10, 0, 20,
            "Charge Efficiency: maximum purchasable level.");
        TruckChargeBaseCost = BindInt(cfg,
            "Truck Upgrades", "ChargeBaseCost", 10000, 0, 1000000,
            "Charge Efficiency: cost of the first level (grows by CostGrowth each level).");
        TruckSellCost = BindInt(cfg,
            "Truck Upgrades", "SellUnlockCost", 50000, 0, 1000000,
            "Loot Cash-In: one-time cost to unlock selling leftover valuables after all quotas are closed.");
        TruckSellFraction = BindFloat(cfg,
            "Truck Upgrades", "SellFraction", 0.75f, 0f, 1f,
            "Loot Cash-In: fraction of a leftover valuable's value returned as currency when sold. 0.75 = 75%.");
        TruckSellMaxQuotaShare = BindFloat(cfg,
            "Truck Upgrades", "SellMaxQuotaShare", 0.3f, 0f, 5f,
            "Loot Cash-In: most it can pay per level, as a share of that level's total quota. 0.3 = at most 30% of the quota. Levels hold several times their quota in loot, so without a cap selling every leftover paid more than the extractions themselves.");

        // --- Player Upgrades (custom, sold in the default shop via REPOLib) ---
                StaminaPercentPerLevel = BindFloat(cfg,
            "Player Upgrades", "StaminaPercentPerLevel", 0.05f, 0.01f, 0.5f,
            "Slower Stamina Use: sprint energy drain multiplier per level, compounding: each level multiplies the drain by (1 - value). 0.05 = each level keeps 95% of the drain; it approaches zero but never reaches it.");
        StaminaMaxLevel = BindInt(cfg,
            "Player Upgrades", "StaminaMaxLevel", 5, 1, 20,
            "Slower Stamina Use: maximum purchasable level.");
        RegenPercentPerLevel = BindFloat(cfg,
            "Player Upgrades", "RegenPercentPerLevel", 0.000667f, 0.0005f, 0.05f,
            "HP Regen: fraction of MAX HP healed per SECOND, per level. 0.000667 = about 0.07%/s per level (~1 HP every 15s at level 1 for a 100 HP player).");
        RegenMaxHpFraction = BindFloat(cfg,
            "Player Upgrades", "RegenMaxHpFraction", 0.30f, 0.05f, 1f,
            "HP Regen: stops once current HP reaches this fraction of max HP. 0.30 = regen only fills up to 30% of max HP.");
        RegenMaxLevel = BindInt(cfg,
            "Player Upgrades", "RegenMaxLevel", 3, 1, 20,
            "HP Regen: maximum purchasable level.");
        DeathProtectMaxCharges = BindInt(cfg,
            "Player Upgrades", "DeathProtectMaxCharges", 1, 1, 9,
            "Death Protection: max charges you can stockpile at once. Each charge saves you from one lethal hit (survive at 1 HP) and is consumed on use; re-buy to restore.");
        DeathProtectBasePrice = BindInt(cfg,
            "Player Upgrades", "DeathProtectBasePrice", 50000, 0, 1000000,
            "Death Protection: approximate shop price per charge (very-very expensive).");
        GunExtraBulletsPerLevel = BindFloat(cfg,
            "Player Upgrades", "GunExtraBulletsPerLevel", 1f, 0.25f, 5f,
            "Gun upgrades (Bullet/Laser/Tranq): extra shots per level for the vanilla Handgun. Other guns get the same share of their own battery (fewer shots per charge = fewer extra shots, e.g. the shotgun).");
        GunExtraBulletsMaxLevel = BindInt(cfg,
            "Player Upgrades", "GunExtraBulletsMaxLevel", 5, 1, 100,
            "Gun upgrades (Bullet/Laser/Tranq): maximum purchasable level.");
        DroneBatteryPercentPerLevel = BindFloat(cfg,
            "Player Upgrades", "DroneBatteryPercentPerLevel", 0.05f, 0.01f, 0.5f,
            "Drone Battery Life: extra effective battery life per level. 0.05 = +5%/level; capped at +100% total.");
        DroneBatteryMaxLevel = BindInt(cfg,
            "Player Upgrades", "DroneBatteryMaxLevel", 20, 1, 100,
            "Drone Battery Life: maximum purchasable level (20 x 5% = +100%).");
        ArmorEnabled = Module(cfg, "Player Upgrades", "ArmorEnabled", true, "Armor", "Sell the Armor upgrade: less damage taken per level.");
        ArmorPercentPerLevel = BindFloat(cfg, "Player Upgrades", "ArmorPercentPerLevel", .02f, .005f, .25f,
            "Armor: damage reduction per level. 0.02 = 2% per level; the result is always rounded in the player's favour.");
        ArmorMaxLevel = BindInt(cfg, "Player Upgrades", "ArmorMaxLevel", 10, 1, 20, "Armor: maximum purchasable level (10 x 2% = 20% less damage).");
        ArmorMaxReduction = BindFloat(cfg, "Player Upgrades", "ArmorMaxReduction", .2f, .05f, .9f, "Armor: hard cap on total damage reduction (0.2 = 20%).");
        OverchargeEnabled = Module(cfg, "Player Upgrades", "OverchargeEnabled", true, "Overcharge", "Sell the Overcharge upgrade: hold monsters longer before your grab beam overloads.");
        OverchargeBuildupPerLevel = BindFloat(cfg, "Player Upgrades", "OverchargeBuildupPerLevel", .1f, .02f, .3f,
            "Overcharge: how much slower overcharge builds up per level while holding a monster. 0.1 = 10% slower per level (capped at 80%).");
        OverchargeRecoveryPerLevel = BindFloat(cfg, "Player Upgrades", "OverchargeRecoveryPerLevel", .25f, 0f, 2f,
            "Overcharge: extra drain speed per level once you stop holding a monster, as a share of the vanilla drain. 0.25 = +25% per level.");
        OverchargeMaxLevel = BindInt(cfg, "Player Upgrades", "OverchargeMaxLevel", 5, 1, 10, "Overcharge: maximum purchasable level.");
        TumbleBonusEnabled = Module(cfg, "Player Upgrades", "TumbleDamageEnabled", true, "TumbleDamage",
            "Sell the Tumble Damage upgrade: launching into a monster deals more damage and hurts you less. Host-side.");
        TumbleMaxLevel = BindInt(cfg, "Player Upgrades", "TumbleDamageMaxLevel", 10, 1, 20, "Tumble Damage: maximum purchasable level.");
        TumbleDamagePerLevel = BindFloat(cfg, "Player Upgrades", "TumbleDamagePercentPerLevel", .05f, 0f, 1f,
            "Tumble Damage: extra monster damage per upgrade level, compounding (0.05 = x1.05 per level: x1.16 at 3, x1.63 at 10).");
        TumbleSelfDamageReductionPerLevel = BindFloat(cfg, "Player Upgrades", "TumbleSelfDamageReductionPerLevel", .1f, 0f, 1f,
            "Tumble Damage: less self-damage from launching into a monster, per upgrade level. 0.1 = 10% per level.");
        TumbleSelfDamageMaxReduction = BindFloat(cfg, "Player Upgrades", "TumbleSelfDamageMaxReduction", 1f, 0f, 1f,
            "Tumble Damage: maximum self-damage reduction (1 = none at all at 10 levels with the default 10%).");
        MeleeDamagePercentPerLevel = BindFloat(cfg,
            "Player Upgrades", "MeleeDamagePercentPerLevel", 0.05f, 0.01f, 0.5f,
            "Melee Damage: extra monster damage from your melee weapons per level. 0.05 = +5%/level.");
        MeleeDamageMaxLevel = BindInt(cfg,
            "Player Upgrades", "MeleeDamageMaxLevel", 10, 1, 100,
            "Melee Damage: maximum purchasable level (10 x 5% = +50%).");
        GrenadeDamagePercentPerLevel = BindFloat(cfg,
            "Player Upgrades", "GrenadeDamagePercentPerLevel", 0.05f, 0.01f, 0.5f,
            "Grenade Damage: extra monster damage from grenades you throw per level. 0.05 = +5%/level.");
        GrenadeDamageMaxLevel = BindInt(cfg,
            "Player Upgrades", "GrenadeDamageMaxLevel", 10, 1, 100,
            "Grenade Damage: maximum purchasable level (10 x 5% = +50%).");
        AutoscanRangePerLevel = BindFloat(cfg,
            "Player Upgrades", "AutoscanRangePerLevel", 5f, 1f, 30f,
            "Autoscan: scan radius added per level, in metres. Discovers valuables and native discoverable items for the team. 5 = level 1 scans 5m, level 2 scans 10m.");
        AutoscanMaxLevel = BindInt(cfg,
            "Player Upgrades", "AutoscanMaxLevel", 5, 1, 20,
            "Autoscan: maximum purchasable level.");
        AutoscanInterval = BindFloat(cfg,
            "Player Upgrades", "AutoscanInterval", 1f, 0.1f, 10f,
            "Autoscan: seconds between scans. Lower = discovers valuables faster but costs more CPU.");
        MapZoomFovLevel7 = BindFloat(cfg,
            "Player Upgrades", "MapZoomFovLevel7", 62f, 50f, 120f,
            "Map Suite L7: camera field-of-view used while the map is open (higher = more zoomed out, see more of the level at once). Vanilla map view is 50.");
        MapZoomFovLevel8 = BindFloat(cfg,
            "Player Upgrades", "MapZoomFovLevel8", 74f, 50f, 120f,
            "Map Suite L8: camera field-of-view while the map is open once level 8 is reached (higher = even more zoomed out).");
        MapZoomInKey = cfg.Bind(
            "Player Upgrades", "MapZoomInKey", UnityEngine.KeyCode.Minus,
            "Map Suite L7+: key to zoom the map view IN (lower FOV) while the map is open. Also accepts Keypad Minus and '['.");
        BetterMapKey = cfg.Bind(
            "Player Upgrades", "BetterMapKey", UnityEngine.KeyCode.M,
            "Better Map perk: with the map up, this key opens and closes the big map.");
        MapZoomOutKey = cfg.Bind(
            "Player Upgrades", "MapZoomOutKey", UnityEngine.KeyCode.Equals,
            "Map Suite L7+: key to zoom the map view OUT (higher FOV) while the map is open. Also accepts Keypad Plus and ']'.");
        MapZoomStep = BindFloat(cfg,
            "Player Upgrades", "MapZoomStep", 6f, 1f, 30f,
            "Map Suite L7+: how much field-of-view each zoom-hotkey press adds/removes on top of the level's default FOV.");
        SkillSpendAnywhere = cfg.Bind("Skill Tree", "SpendAnywhere", false,
            "Allow spending skill points during a level. Off: points can be spent only in the shop and the truck between levels (the host decides).");
        SkillRespecCost = BindInt(cfg, "Skill Tree", "RespecCostK", 10, 0, 1000,
            "Run money (in $K, the shop's unit) to refund all of your spent skill points. Paid from the team's money, in the shop or truck only. 0 = free.");
        SkillTradeOffResetCost = BindInt(cfg, "Skill Tree", "TradeOffResetCostK", 5, 0, 1000,
            "Run money (in $K) to refund only your trade-off skills, so you can pick others. Shop or truck only. 0 = free.");
        SkillPointBasePrice = BindInt(cfg, "Skill Tree", "SkillPointBasePrice", 6000, 500, 1000000,
            "Approximate shop price of the first Skill Point; the game raises it with each one the team buys, like any upgrade. Takes effect on restart.");
        SkillTreeKey = cfg.Bind("Skill Tree", "OpenKey", UnityEngine.KeyCode.K,
            "Key that opens and closes the skill tree during a run (the Esc menu also has a SKILL TREE button).");
        SkillRootBonus = BindFloat(cfg, "Skill Tree", "RootDamageBonus", .05f, 0f, .5f,
            "Brawler and Marksman root perks: extra damage on every weapon of their branch (0.05 = +5%).");
        SkillWeaponDamageMax = BindFloat(cfg, "Skill Tree", "WeaponDamageMaxBonus", .40f, 0f, 3f,
            "Arsenal weapon damage perks: bonus at max level for each weapon, spread evenly over its levels (0.40 = +40%).");
        SkillStatusRootSeconds = BindFloat(cfg, "Skill Tree", "StatusLabSeconds", .5f, 0f, 3f,
            "Status Lab root perk: seconds every status from your weapons moves toward the 8-second cap.");
        SkillSecondWindPerLevel = BindFloat(cfg, "Skill Tree", "SecondWindPerLevel", .15f, 0f, 1f,
            "Second Wind: faster stamina recovery per level (0.15 = +15%).");
        SkillSurvivorStamina = BindFloat(cfg, "Skill Tree", "SurvivorStamina", 5f, 0f, 50f,
            "Survivor root perk: extra max stamina (vanilla stamina upgrades give 10 each).");
        SkillFlashRange = BindFloat(cfg, "Skill Tree", "FlashlightTuningRange", .15f, 0f, 1f,
            "Flashlight Tuning root perk: longer beam (0.15 = +15% range).");
        SkillFlashWidthPerLevel = BindFloat(cfg, "Skill Tree", "BeamWidthPerLevel", .10f, 0f, .5f,
            "Beam Width: wider cone per level (0.10 = +10% angle).");
        SkillFlashBrightnessPerLevel = BindFloat(cfg, "Skill Tree", "BrightnessPerLevel", .15f, 0f, 1f,
            "Brightness: brighter and slightly longer beam per level (0.15 = +15%).");
        SkillDesperationPerLevel = BindFloat(cfg, "Skill Tree", "DesperationPerLevel", .10f, 0f, .3f,
            "Desperation: less damage taken per level while your health is low (0.10 = -10%). Combines with Armor by multiplying.");
        SkillDesperationThreshold = BindFloat(cfg, "Skill Tree", "DesperationHealthThreshold", .30f, .05f, .9f,
            "Desperation works while your health is below this share of max health (0.30 = under 30%).");
        SkillHeavyHitterPerLevel = BindFloat(cfg, "Skill Tree", "HeavyHitterSecondsPerLevel", .5f, 0f, 3f,
            "Heavy Hitter: extra seconds of stun per level when your melee weapon stuns a monster (8 s max).");
        SkillMedicPerLevel = BindFloat(cfg, "Skill Tree", "FieldMedicPerLevel", .15f, 0f, 1f,
            "Field Medic: health packs you use heal this much more per level (0.15 = +15%), and teammates near you get this share of the pack per level.");
        SkillMedicRadius = BindFloat(cfg, "Skill Tree", "FieldMedicRadius", 5f, 1f, 20f,
            "Field Medic: teammates within this many metres share your health pack.");
        SkillBatterySaverPerLevel = BindFloat(cfg, "Skill Tree", "BatterySaverPerLevel", .15f, 0f, .25f,
            "Battery Saver: slower drain per level for battery items that are not guns or drones (0.15 = 15% slower).");
        SkillVampireMax = BindFloat(cfg, "Skill Tree", "VampirismMax", .29f, 0f, 1f,
            "Vampirism: share of melee damage dealt that heals you at the last level (0.29 = 29% at 7 of 7, rising evenly per level).");
        SkillCrouchSpeedPerLevel = BindFloat(cfg, "Skill Tree", "CrouchSpeedPerLevel", .05f, 0f, .2f,
            "Crouch Speed: faster walking while crouched, per level (0.05 = +5%, so +15% at the 3-level cap).");
        SkillLuckyBoxShift = BindInt(cfg, "Skill Tree", "LuckyFinderBoxWeightPerLevel", 10, 0, 50,
            "Lucky Finder: weight per level moved from an empty skill box to 1, 2 and 3 points (default odds 65/20/10/5 become 55/25/13/7, then 45/30/16/9).");
        SkillLuckyCrystalBonus = BindFloat(cfg, "Skill Tree", "LuckyFinderCrystalBonusPerLevel", 5f, 0f, 50f,
            "Lucky Finder: extra battery charge per level from each battery crystal you take (5 = +5%).");

        // --- Surprise / special-level system ---
                SpecialLevelChancePercent = BindInt(cfg,
            "Surprises", "SpecialLevelChancePercent", 40, 0, 100,
            "Percent chance (0-100) that any eligible level becomes a special level with modifiers.");
        ModifiersPerLevelMin = BindInt(cfg,
            "Surprises", "ModifiersPerLevelMin", 1, 0, 10,
            "Minimum number of modifiers a special level rolls.");
        ModifiersPerLevelMax = BindInt(cfg,
            "Surprises", "ModifiersPerLevelMax", 2, 0, 10,
            "Maximum number of modifiers a special level rolls.");
        SurprisesStartLevel = BindInt(cfg,
            "Surprises", "StartLevel", 1, 0, 30,
            "Levels completed before special levels can appear. 1 = from the 2nd level onward.");

        foreach (Modifiers.ModifierType m in System.Enum.GetValues(typeof(Modifiers.ModifierType)))
        {
            if (m == Modifiers.ModifierType.None)
                continue;
            ModifierEnabled[m] = cfg.Bind(
                "Surprise Modifiers", m.ToString(), true,
                $"{ModifierDescription(m)} Toggle whether it can be rolled on special levels.");
        }

        foreach (var sm in Modifiers.StatModifiers.All)
            StatModifierEnabled[sm.Id] = cfg.Bind("Surprise Modifiers", sm.Name, true,
                $"{sm.Description} Toggle whether it can be rolled on special levels.");

        RebuildPerEnemyCaps();
        EnemyHealthMaxPerEnemy.SettingChanged += (_, _) => RebuildPerEnemyCaps();
    }

    // Parsed form of EnemyHealthMaxPerEnemy, rebuilt whenever the string changes.
    private static Dictionary<string, float> _perEnemyHealthMax = new();

    private static void RebuildPerEnemyCaps()
    {
        var map = new Dictionary<string, float>(System.StringComparer.OrdinalIgnoreCase);
        foreach (var entry in EnemyHealthMaxPerEnemy.Value.Split(','))
        {
            int colon = entry.LastIndexOf(':');
            if (colon <= 0)
                continue;

            string name = entry.Substring(0, colon).Trim();
            string valueText = entry.Substring(colon + 1).Trim();
            if (name.Length == 0)
                continue;

            if (float.TryParse(valueText, NumberStyles.Float, CultureInfo.InvariantCulture, out float mult) &&
                !float.IsNaN(mult) && !float.IsInfinity(mult) && mult >= 1f)
                map[name] = mult;
        }
        _perEnemyHealthMax = map;
    }

    /// <summary>
    /// The HP cap multiplier for a given enemy name: its per-enemy override if
    /// one exists, otherwise the global cap.
    /// </summary>
    public static float HealthMaxForEnemy(string enemyName)
    {
        if (enemyName != null && _perEnemyHealthMax.TryGetValue(enemyName, out float mult))
            return mult;
        return EnemyHealthMax.Value;
    }
}
