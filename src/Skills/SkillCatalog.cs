using System;
using System.Collections.Generic;

namespace IncreaseHardMod.Skills;

/// <summary>
/// Every node of the skill tree, in display order. Costs and caps are the plan's defaults
/// (docs/skill-tree-plan.md, section 3); ids are stable save keys, so never rename one.
/// </summary>
internal static class SkillCatalog
{
    private const string P = "IHM_ST_";

    // Recon
    public const string MapUplink = P + "MapUplink", ActiveMonsters = P + "ActiveMonsters",
        RespawnTimers = P + "RespawnTimers", MonsterDots = P + "MonsterDots", QuotaHeading = P + "QuotaHeading",
        ValuableTracker = P + "ValuableTracker", MapPings = P + "MapPings", ZoomOne = P + "ZoomOne", ZoomTwo = P + "ZoomTwo",
        CosmeticTracker = P + "CosmeticTracker", BetterMap = P + "BetterMap", MapLandmarks = P + "MapLandmarks";
    public const string Ping = P + "Ping", TargetInfo = P + "TargetInfo", EnemyHp = P + "EnemyHp", LootCondition = P + "LootCondition",
        Weight = P + "Weight", Threat = P + "Threat", PingRange = P + "PingRange", PingPath = P + "PingPath", PingCompass = P + "PingCompass";
    public const string FlashTuning = P + "FlashTuning", BeamWidth = P + "BeamWidth", Brightness = P + "Brightness", BeamColour = P + "BeamColour",
        FlashStun = P + "FlashStun";
    public const string WideView = P + "WideView";
    // Combat
    public const string Brawler = P + "Brawler", GrenadeDamage = P + "GrenadeDamage", MeleeDamage = P + "MeleeDamage", TumbleDamage = P + "TumbleDamage",
        Vampirism = P + "Vampirism";
    public const string MeleeAccuracy = P + "MeleeAccuracy", GunAccuracy = P + "GunAccuracy",
        ExplosiveGrenade = P + "ExplosiveGrenade", ClusterGrenade = P + "ClusterGrenade",
        StunGrenade = P + "StunGrenade", ShockwaveGrenade = P + "ShockwaveGrenade", DecoyGrenade = P + "DecoyGrenade";
    public const string Gunsmith = P + "Gunsmith", BulletShots = P + "BulletShots", LaserShots = P + "LaserShots", TranqShots = P + "TranqShots";
    public const string Technician = P + "Technician", DroneBattery = P + "DroneBattery", Overcharge = P + "Overcharge";
    // Survival
    public const string Survivor = P + "Survivor", StaminaEfficiency = P + "StaminaEfficiency", SecondWind = P + "SecondWind",
        Armor = P + "Armor", HpRegen = P + "HpRegen", Desperation = P + "Desperation", HuntedWarning = P + "HuntedWarning",
        CrouchSpeed = P + "CrouchSpeed", SoulWard = P + "SoulWard", SoulKeeper = P + "SoulKeeper";
    // Utility
    public const string InventorySlot = P + "InventorySlot", Autoscan = P + "Autoscan", HeadBattery = P + "HeadBattery", TumbleWings = P + "TumbleWings",
        HeadRecharge = P + "HeadRecharge", HeadLeap = P + "HeadLeap";
    public const string FieldMedic = P + "FieldMedic", BatterySaver = P + "BatterySaver", LuckyFinder = P + "LuckyFinder";
    // Arsenal: damage per weapon, and longer status effects from your own weapons
    public const string Marksman = P + "Marksman", HandgunDamage = P + "HandgunDamage", RevolverDamage = P + "RevolverDamage",
        ShotgunDamage = P + "ShotgunDamage", CartCannonDamage = P + "CartCannonDamage", CartLaserDamage = P + "CartLaserDamage",
        StaffDamage = P + "StaffDamage", CrossbowDamage = P + "CrossbowDamage";
    public const string StatusLab = P + "StatusLab", TranqTime = P + "TranqTime", NetTime = P + "NetTime", FreezeTime = P + "FreezeTime",
        PoisonRush = P + "PoisonRush", HeavyHitter = P + "HeavyHitter";

    // Trade-offs: one pick per tier per page (see TradeOffRules). Ids are save keys: never rename.
    public const string GunnersFocus = P + "TO_GunnersFocus", Bruiser = P + "TO_Bruiser", GlassCannon = P + "TO_GlassCannon",
        Overdrive = P + "TO_Overdrive", Sharpshooter = P + "TO_Sharpshooter", Berserker = P + "TO_Berserker";
    public const string HandCannon = P + "TO_HandCannon", CartCommander = P + "TO_CartCommander", LingeringToxins = P + "TO_LingeringToxins",
        QuickShock = P + "TO_QuickShock", Executioner = P + "TO_Executioner", Overkill = P + "TO_Overkill";
    public const string ThickSkin = P + "TO_ThickSkin", Sprinter = P + "TO_Sprinter", Juggernaut = P + "TO_Juggernaut",
        Daredevil = P + "TO_Daredevil", SecondHeart = P + "TO_SecondHeart", LastStand = P + "TO_LastStand", GhostRunner = P + "TO_GhostRunner";
    public const string Floodlight = P + "TO_Floodlight", Spotlight = P + "TO_Spotlight", Scout = P + "TO_Scout",
        EchoPing = P + "TO_EchoPing", Oracle = P + "TO_Oracle", HuntersEye = P + "TO_HuntersEye";
    public const string Tinkerer = P + "TO_Tinkerer", DroneJockey = P + "TO_DroneJockey", MedicsOath = P + "TO_MedicsOath",
        FortuneSeeker = P + "TO_FortuneSeeker", Overclock = P + "TO_Overclock", Salvager = P + "TO_Salvager";

    // Contracts: quest-only perks, earned from special quest contracts and never bought with points
    public const string CartHandling = P + "CartHandling", GentleHands = P + "GentleHands", CalmPresence = P + "CalmPresence",
        SoftSteps = P + "SoftSteps";
    // Instincts: quest-only perks that fire a short buff on a trigger (see BuffPerks). Appended: contract indices stay.
    public const string Adrenaline = P + "Adrenaline", IronSkin = P + "IronSkin", HotBarrel = P + "HotBarrel",
        Overheat = P + "Overheat", BattleTrance = P + "BattleTrance", MendingSurge = P + "MendingSurge";

    // Courier: the pet courier's jobs and abilities (see Courier/CourierPerks). Ids are save keys: never rename.
    public const string CourierAlly = P + "CourierAlly", CourierCart = P + "CourierCart", CourierLoad = P + "CourierLoad",
        CourierHeadTrail = P + "CourierHeadTrail", CourierHeadFetch = P + "CourierHeadFetch",
        CourierSpeed = P + "CourierSpeed", CourierDash = P + "CourierDash", CourierFlight = P + "CourierFlight",
        CourierArmor = P + "CourierArmor", CourierRecovery = P + "CourierRecovery", CourierLuck = P + "CourierLuck",
        CourierHealth = P + "CourierHealth", CourierStealth = P + "CourierStealth", CourierPack = P + "CourierPack",
        CourierSouls = P + "CourierSouls";
    // Expensive late perks: a chance that a weapon use costs no charge, thriftier drones, a higher regeneration cap.
    public const string FreeBullet = P + "FreeBullet", FreeLaser = P + "FreeLaser", FreeTranq = P + "FreeTranq", FreeMelee = P + "FreeMelee",
        DroneThrift = P + "DroneThrift", RegenCap = P + "RegenCap", FreeCartShot = P + "FreeCartShot";

    // Monster resistances, mobility and loot perks for the 0.10.0 monsters. Ids are save keys: never rename.
    public const string Grounding = P + "Grounding", ThermalProtection = P + "ThermalProtection", Antidote = P + "Antidote",
        Evasion = P + "Evasion", DodgeRoll = P + "DodgeRoll",
        GoldenNose = P + "GoldenNose", SoulHunter = P + "SoulHunter";
    public const string CriticalStrike = P + "CriticalStrike";
    public const string SmokeGrenade = P + "SmokeGrenade", CryoGrenade = P + "CryoGrenade", GlueGrenade = P + "GlueGrenade",
        SickleThrow = P + "SickleThrow";
    public const string Toughness = P + "Toughness", Hauler = P + "Hauler", Scavenger = P + "Scavenger";
    public const string Hemostasis = P + "Hemostasis", Sapper = P + "Sapper",
        LinkOperator = P + "LinkOperator", LightBasket = P + "LightBasket",
        FlameCartDamage = P + "FlameCartDamage", GhostConvoy = P + "GhostConvoy", LaserGunDamage = P + "LaserGunDamage",
        RapidFire = P + "RapidFire";

    /// <summary>
    /// Perks taken out of the tree. Their save keys stay registered so old saves load without warnings; they are not
    /// in the catalogue, so the points spent on them count as unspent again (SkillLoadout.Spent ignores them).
    /// </summary>
    internal static readonly string[] Retired = { P + "DeadPlayers", P + "StoneResilience" };

    /// <summary>No status from a tree perk lasts longer than this; poison is compressed down to it instead.</summary>
    internal const float StatusCapSeconds = 8f;

    private static readonly List<SkillNode> Nodes = new();
    private static readonly Dictionary<string, SkillNode> ById = new(StringComparer.Ordinal);

    internal static IReadOnlyList<SkillNode> All => Nodes;
    internal static SkillNode Find(string id) => id != null && ById.TryGetValue(id, out var node) ? node : null;

    static SkillCatalog()
    {
        var recon = SkillPage.Recon;
        Add(recon, "Map", MapUplink, "Map Uplink", "Отряд на карте", 1, 1, root: true);
        Add(recon, "Map", ActiveMonsters, "Active Monsters", "Активные монстры", 1, 1, MapUplink);
        Add(recon, "Map", RespawnTimers, "Respawning Monsters", "Возрождение монстров", 1, 1, ActiveMonsters);
        Add(recon, "Map", MonsterDots, "Monster Dots", "Монстры на карте", 2, 1, RespawnTimers);
        Add(recon, "Map", QuotaHeading, "Quota Heading", "Курс на квоту", 1, 1, MapUplink);
        Add(recon, "Map", ValuableTracker, "Valuable Tracker", "Учёт ценностей", 1, 1, QuotaHeading);
        Add(recon, "Map", MapPings, "Pings on Map", "Метки на карте", 1, 1, MapUplink);
        Add(recon, "Map", ZoomOne, "Map Zoom I", "Обзор карты I", 1, 1, MapUplink);
        Add(recon, "Map", ZoomTwo, "Map Zoom II", "Обзор карты II", 1, 1, ZoomOne);
        Add(recon, "Map", CosmeticTracker, "Cosmetic Tracker", "Косметика на карте", 2, 1, MapUplink);
        Add(recon, "Map", BetterMap, "Better Map", "Большая карта", 2, 1, ZoomOne);
        Add(recon, "Map", MapLandmarks, "Shrines & Crystals", "Святилища и кристаллы", 1, 1, MapUplink);

        Add(recon, "Ping", Ping, "Ping Anywhere", "Командные метки", 2, 1, root: true);
        Add(recon, "Ping", TargetInfo, "Target Info", "Сведения о цели", 1, 1, Ping);
        Add(recon, "Ping", EnemyHp, "Enemy HP", "Здоровье монстра", 1, 1, Ping);
        Add(recon, "Ping", LootCondition, "Loot Condition", "Состояние добычи", 1, 1, Ping);
        Add(recon, "Ping", Weight, "Target Weight", "Вес цели", 1, 1, Ping);
        Add(recon, "Ping", Threat, "Monster Threat", "Уровень угрозы", 1, 1, Ping);
        Add(recon, "Ping", PingRange, "Range Finder", "Дальномер", 1, 1, Ping);
        Add(recon, "Ping", PingPath, "Pathfinder", "Путь к метке", 2, 1, PingRange);
        Add(recon, "Ping", PingCompass, "Ping Compass", "Указатель меток", 1, 1, Ping);

        Add(recon, "Flashlight", FlashTuning, "Flashlight Tuning", "Настройка фонаря", 1, 1, root: true);
        Add(recon, "Flashlight", BeamWidth, "Beam Width", "Ширина луча", 1, 3, FlashTuning);
        Add(recon, "Flashlight", Brightness, "Brightness", "Яркость луча", 1, 3, FlashTuning);
        Add(recon, "Flashlight", BeamColour, "Beam Colour", "Цвет луча", 1, 1, FlashTuning);
        Add(recon, "Flashlight", FlashStun, "Flash Stun", "Ослепляющая вспышка", 2, 3, Brightness).RequiresMaxed = true;

        var combat = SkillPage.Combat;
        Add(combat, "Damage", Brawler, "Brawler", "Драчун", 1, 1, root: true);
        Add(combat, "Damage", MeleeDamage, "Melee Damage", "Урон в ближнем бою", 1, 5, Brawler);
        Add(combat, "Damage", MeleeAccuracy, "Melee Accuracy", "Точность в ближнем бою", 1, 3, Brawler);
        Add(combat, "Damage", TumbleDamage, "Tumble Damage", "Урон кувырком", 1, 5, Brawler);
        Add(combat, "Damage", Vampirism, "Vampirism", "Вампиризм", 1, 7, MeleeDamage);
        Add(combat, "Damage", FreeMelee, "Unworn Edge", "Без износа", 3, 3, MeleeDamage);

        Add(combat, "Grenades", GrenadeDamage, "Grenade Damage", "Урон гранат", 1, 5, Brawler);
        Add(combat, "Grenades", ExplosiveGrenade, "Explosive Grenade", "Осколочная граната", 1, 3, GrenadeDamage);
        Add(combat, "Grenades", ClusterGrenade, "Duct-Taped Grenade", "Связка гранат", 1, 3, GrenadeDamage);
        Add(combat, "Grenades", StunGrenade, "Stun Grenade", "Оглушающая граната", 1, 3, GrenadeDamage);
        Add(combat, "Grenades", ShockwaveGrenade, "Shockwave Grenade", "Ударная граната", 1, 3, GrenadeDamage);
        Add(combat, "Grenades", DecoyGrenade, "Decoy Grenade", "Граната-приманка", 1, 3, GrenadeDamage);

        Add(combat, "Guns", Gunsmith, "Gunsmith", "Оружейник", 1, 1, root: true);
        Add(combat, "Guns", BulletShots, "Bullet Gun Shots", "Запас патронов", 1, 5, Gunsmith);
        Add(combat, "Guns", LaserShots, "Laser Gun Shots", "Заряды лазера", 1, 5, Gunsmith);
        Add(combat, "Guns", TranqShots, "Tranq Gun Shots", "Заряды транквилизатора", 1, 5, Gunsmith);
        Add(combat, "Guns", FreeBullet, "Free Rounds", "Бесплатный патрон", 3, 3, BulletShots);
        Add(combat, "Guns", FreeLaser, "Free Laser Shot", "Бесплатный разряд", 3, 3, LaserShots);
        Add(combat, "Guns", FreeTranq, "Free Dart", "Бесплатный дротик", 3, 3, TranqShots);

        Add(combat, "Tech", Technician, "Technician", "Техник", 1, 1, root: true);
        Add(combat, "Tech", DroneBattery, "Drone Battery", "Батарея дронов", 1, 5, Technician);
        Add(combat, "Tech", Overcharge, "Overcharge", "Перегрузка", 1, 5, Technician);
        Add(combat, "Tech", DroneThrift, "Frugal Drones", "Экономные дроны", 3, 3, DroneBattery);

        var survival = SkillPage.Survival;
        Add(survival, "Survival", Survivor, "Survivor", "Выживший", 1, 1, root: true);
        Add(survival, "Survival", StaminaEfficiency, "Stamina Efficiency", "Экономия выносливости", 1, 5, Survivor);
        Add(survival, "Survival", SecondWind, "Second Wind", "Второе дыхание", 1, 2, StaminaEfficiency);
        Add(survival, "Survival", Armor, "Armor", "Броня", 2, 5, Survivor);
        Add(survival, "Survival", HpRegen, "HP Regeneration", "Регенерация", 3, 3, Survivor);
        Add(survival, "Survival", RegenCap, "Deep Regeneration", "Глубокая регенерация", 2, 5, HpRegen);
        Add(survival, "Survival", Desperation, "Desperation", "Отчаяние", 2, 3, Survivor);
        Add(survival, "Survival", HuntedWarning, "Hunted Warning", "Чувство погони", 1, 1, Survivor);
        Add(survival, "Survival", CrouchSpeed, "Crouch Speed", "Скорость в приседе", 1, 3, Survivor);
        Add(survival, "Survival", SoulWard, "Soul Ward", "Защита от душ", 1, 3, Survivor);
        Add(survival, "Survival", SoulKeeper, "Soul Keeper", "Хранитель душ", 1, 3, Survivor);
        // Protection against the new monsters' effects and mobility (appended; existing indices stay).
        Add(survival, "Resistances", Grounding, "Grounding", "Заземление", 1, 3, Survivor);
        Add(survival, "Resistances", ThermalProtection, "Thermal Protection", "Термозащита", 1, 3, Survivor);
        Add(survival, "Resistances", Antidote, "Antidote", "Антидот", 1, 3, Survivor);
        Add(survival, "Mobility", Evasion, "Evasion", "Изворотливость", 1, 3, Survivor);
        Add(survival, "Mobility", DodgeRoll, "Dodge Roll", "Рывок уклонения", 2, 3, Evasion);

        var utility = SkillPage.Utility;
        Add(utility, "Gear", InventorySlot, "Inventory Slot", "Слот инвентаря", 3, 4, root: true);
        Add(utility, "Gear", Autoscan, "Autoscan", "Автопоиск", 1, 5, root: true);
        Add(utility, "Gear", HeadBattery, "Death Head Battery", "Батарея головы", 1, 3, root: true).Vanilla = VanillaUpgrade.DeathHeadBattery;
        Add(utility, "Gear", HeadRecharge, "Head Recharge", "Подзарядка головы", 1, 3, HeadBattery);
        Add(utility, "Gear", HeadLeap, "Head Leap", "Прыжок головы", 1, 5, HeadBattery);
        Add(utility, "Gear", TumbleWings, "Tumble Wings", "Крылья кувырка", 1, 3, root: true).Vanilla = VanillaUpgrade.TumbleWings;

        Add(utility, "Support", FieldMedic, "Field Medic", "Полевой медик", 1, 3, root: true);
        Add(utility, "Support", BatterySaver, "Battery Saver", "Экономия батарей", 1, 3, root: true);
        Add(utility, "Support", LuckyFinder, "Lucky Finder", "Везунчик", 2, 2, root: true);
        Add(utility, "Loot", GoldenNose, "Golden Nose", "Золотой нюх", 1, 3, root: true);
        Add(utility, "Loot", SoulHunter, "Soul Hunter", "Охотник за душами", 2, 3, root: true);

        // Cart weapons count for whoever is operating them.
        var arsenal = SkillPage.Arsenal;
        Add(arsenal, "Weapons", Marksman, "Marksman", "Меткий стрелок", 1, 1, root: true);
        Add(arsenal, "Weapons", GunAccuracy, "Gun Accuracy", "Точность стрельбы", 1, 3, Marksman);
        Add(arsenal, "Weapons", HandgunDamage, "Handgun Damage", "Урон пистолета", 1, 5, Marksman);
        Add(arsenal, "Weapons", RevolverDamage, "Revolver Damage", "Урон револьвера", 1, 3, Marksman);
        Add(arsenal, "Weapons", ShotgunDamage, "Shotgun Damage", "Урон дробовика", 1, 4, Marksman);
        Add(arsenal, "Weapons", CartCannonDamage, "Cart Cannon Damage", "Урон пушки тележки", 1, 3, Marksman);
        Add(arsenal, "Weapons", CartLaserDamage, "Cart Laser Damage", "Урон лазера тележки", 1, 3, Marksman);
        Add(arsenal, "Weapons", StaffDamage, "Wizard Staff Damage", "Урон посоха", 1, 4, Marksman);
        Add(arsenal, "Weapons", CrossbowDamage, "Poison Crossbow Damage", "Урон арбалета", 1, 3, Marksman);
        Add(arsenal, "Weapons", FreeCartShot, "Free Cart Shot", "Бесплатный залп тележки", 3, 3, Marksman);

        Add(arsenal, "Status", StatusLab, "Status Lab", "Мастер эффектов", 1, 1, root: true);
        Add(arsenal, "Status", TranqTime, "Longer Tranquilizer", "Долгий транквилизатор", 1, 3, StatusLab);
        Add(arsenal, "Status", NetTime, "Longer Net", "Долгая сеть", 1, 3, StatusLab);
        Add(arsenal, "Status", FreezeTime, "Longer Freeze", "Долгая заморозка", 1, 3, StatusLab);
        Add(arsenal, "Status", PoisonRush, "Fast Poison", "Быстрый яд", 1, 3, StatusLab);
        Add(arsenal, "Status", HeavyHitter, "Heavy Hitter", "Тяжёлый удар", 1, 3, StatusLab);

        AddTradeOffs();

        var contracts = SkillPage.Contracts;
        var courier = SkillPage.Courier;
        Add(courier, "Logistics", CourierAlly, "Ally Delivery", "Доставка союзнику", 2, 1, root: true);
        Add(courier, "Logistics", CourierCart, "Cart Delivery", "Доставка тележки", 2, 1, root: true);
        Add(courier, "Logistics", CourierHeadTrail, "Head Search", "Поиск головы", 2, 1, root: true);
        Add(courier, "Logistics", CourierHeadFetch, "Head Rescue", "Доставка головы", 5, 1, CourierHeadTrail);
        Add(courier, "Logistics", CourierLoad, "Carry Weight", "Грузоподъёмность", 1, 3, root: true);
        Add(courier, "Logistics", CourierPack, "Bigger Backpack", "Большой рюкзак", 2, 2, root: true);
        Add(courier, "Logistics", CourierSouls, "Soul Collector", "Сборщик душ", 2, 1, root: true);
        Add(courier, "Mobility", CourierSpeed, "Courier Speed", "Скорость курьера", 1, 3, root: true);
        Add(courier, "Mobility", CourierDash, "Dash", "Рывок", 2, 1, CourierSpeed);
        Add(courier, "Mobility", CourierFlight, "Flight", "Полёт", 3, 1, CourierDash);
        Add(courier, "Support", CourierHealth, "Courier Health", "Здоровье курьера", 1, 5, root: true);
        Add(courier, "Support", CourierArmor, "Tough Courier", "Крепкий курьер", 1, 4, root: true);
        Add(courier, "Support", CourierStealth, "Low Profile", "Незаметность", 1, 3, root: true);
        Add(courier, "Support", CourierRecovery, "Quick Recovery", "Быстрое восстановление", 1, 3, CourierArmor);
        Add(courier, "Support", CourierLuck, "Lucky Finds", "Удачные находки", 2, 3, root: true);

        Add(contracts, "Contracts", CartHandling, "Cart Handling", "Лёгкая тележка", 0, 3, root: true).QuestOnly = true;
        Add(contracts, "Contracts", GentleHands, "Gentle Hands", "Бережные руки", 0, 3, root: true).QuestOnly = true;
        Add(contracts, "Contracts", CalmPresence, "Calm Presence", "Спокойствие", 0, 3, root: true).QuestOnly = true;
        Add(contracts, "Contracts", SoftSteps, "Soft Steps", "Тихие шаги", 0, 3, root: true).QuestOnly = true;
        Add(contracts, "Instincts", Adrenaline, "Adrenaline", "Адреналин", 0, 3, root: true).QuestOnly = true;
        Add(contracts, "Instincts", IronSkin, "Iron Skin", "Железная кожа", 0, 3, root: true).QuestOnly = true;
        Add(contracts, "Instincts", HotBarrel, "Hot Barrel", "Раскалённый ствол", 0, 3, root: true).QuestOnly = true;
        Add(contracts, "Instincts", Overheat, "Overheat", "Перегрев", 0, 3, root: true).QuestOnly = true;
        Add(contracts, "Instincts", BattleTrance, "Battle Trance", "Боевой транс", 0, 3, root: true).QuestOnly = true;
        Add(contracts, "Instincts", MendingSurge, "Mending Surge", "Прилив сил", 0, 3, root: true).QuestOnly = true;
        // Append to preserve existing catalogue indices and save keys.
        Add(survival, "Resistances", Hemostasis, "Hemostasis", "Гемостаз", 1, 3, Survivor);
        Add(survival, "Resistances", Sapper, "Sapper", "Сапёр", 1, 3, Survivor);
        Add(combat, "Tech", LinkOperator, "Link Operator", "Оператор связки", 1, 3, Technician);
        Add(utility, "Gear", LightBasket, "Light Basket", "Лёгкая корзина", 1, 5, root: true);
        Add(arsenal, "Weapons", FlameCartDamage, "Flamethrower Damage", "Урон огнемёта", 1, 3, Marksman);
        Add(combat, "Tech", GhostConvoy, "Ghost Convoy", "Призрачный обоз", 2, 1, LinkOperator);
        Add(arsenal, "Weapons", LaserGunDamage, "Laser Gun Damage", "Урон лазерного оружия", 1, 3, Marksman);
        Add(arsenal, "Weapons", RapidFire, "Rapid Fire", "Скорострельность", 1, 3, Marksman);
        // Wider camera view, local to each player and switchable in the tree (see WideView.cs).
        Add(survival, "Resistances", Toughness, "Toughness", "Стойкость", 1, 3, Survivor);
        Add(utility, "Loot", Hauler, "Hauler", "Грузчик", 1, 3, root: true);
        Add(utility, "Loot", Scavenger, "Scavenger", "Мародёр", 2, 3, root: true);
        Add(combat, "Damage", CriticalStrike, "Critical Strike", "Критический удар", 1, 5, Brawler);
        Add(combat, "Grenades", SmokeGrenade, "Smoke Grenade", "Дымовая граната", 1, 3, GrenadeDamage);
        Add(combat, "Grenades", CryoGrenade, "Cryo Grenade", "Крио-граната", 1, 3, GrenadeDamage);
        Add(combat, "Grenades", GlueGrenade, "Glue Grenade", "Клеевая граната", 1, 3, GrenadeDamage);
        Add(combat, "Damage", SickleThrow, "Sickle Throw", "Бросок серпа", 1, 3, Brawler);
        Add(recon, "Vision", WideView, "Wide View", "Широкий обзор", 1, 3, root: true);
    }

    // Trade-off skills, page by page. The numbers live only here; the tree text is the Up and Down lines.
    private static void AddTradeOffs()
    {
        var combat = SkillPage.Combat;
        AddTradeOff(combat, 1, GunnersFocus, "Gunner's Focus", "Фокус стрелка", "+15% gun damage", "-15% melee and grenade damage",
            "+15% урона оружием", "-15% урона в ближнем бою и гранатами",
            (Fx.GunDamage, 1.15f), (Fx.CartDamage, 1.15f), (Fx.MeleeDamage, .85f), (Fx.GrenadeDamage, .85f));
        AddTradeOff(combat, 1, Bruiser, "Bruiser", "Громила", "+15% melee and tumble damage", "-15% gun damage",
            "+15% урона в ближнем бою и в кувырке", "-15% урона оружием",
            (Fx.MeleeDamage, 1.15f), (Fx.TumbleDamage, 1.15f), (Fx.GunDamage, .85f), (Fx.CartDamage, .85f));
        AddTradeOff(combat, 2, GlassCannon, "Glass Cannon", "Стеклянная пушка", "+25% damage with everything", "+20% damage taken",
            "+25% урона всем", "+20% получаемого урона",
            (Fx.DamageDealt, 1.25f), (Fx.DamageTaken, 1.2f));
        AddTradeOff(combat, 2, Overdrive, "Overdrive", "Форсаж", "Hold monsters in your grab 40% longer; your melee and tranquilizer stuns last 30% longer", "All your battery items drain 25% faster",
            "Монстров можно держать захватом на 40% дольше; оглушения в ближнем бою и транквилизатором на 30% дольше", "Все ваши предметы с батареей разряжаются на 25% быстрее",
            (Fx.GrabHold, 1.4f), (Fx.StunLength, 1.3f), (Fx.BatteryItems, 1.25f), (Fx.BatteryGuns, 1.25f), (Fx.BatteryDrones, 1.25f));
        AddTradeOff(combat, 3, Sharpshooter, "Sharpshooter", "Снайпер", "+35% gun damage", "15% fewer shots per charge",
            "+35% урона оружием", "На 15% меньше выстрелов на заряд",
            (Fx.GunDamage, 1.35f), (Fx.CartDamage, 1.35f), (Fx.BatteryGuns, 1f / .85f));
        AddTradeOff(combat, 3, Berserker, "Berserker", "Берсерк", "+40% melee damage; melee stuns last 1 s longer", "-30% gun and grenade damage; sprinting costs 20% more stamina",
            "+40% урона в ближнем бою; оглушение в ближнем бою на 1 с дольше", "-30% урона оружием и гранатами; бег тратит на 20% больше стамины",
            (Fx.MeleeDamage, 1.4f), (Fx.MeleeStunSeconds, 1f), (Fx.GunDamage, .7f), (Fx.CartDamage, .7f), (Fx.GrenadeDamage, .7f),
            (Fx.SprintStamina, 1.2f));

        var arsenal = SkillPage.Arsenal;
        AddTradeOff(arsenal, 1, HandCannon, "Hand Cannon", "Ручная пушка", "+15% handgun, revolver, shotgun, staff and crossbow damage", "-15% cart cannon and cart laser damage",
            "+15% урона пистолета, револьвера, дробовика, посоха и арбалета", "-15% урона пушки и лазера тележки", (Fx.GunDamage, 1.15f), (Fx.CartDamage, .85f));
        AddTradeOff(arsenal, 1, CartCommander, "Cart Commander", "Командир тележки", "+20% cart cannon and cart laser damage", "-15% handheld gun damage",
            "+20% урона пушки и лазера тележки", "-15% урона ручным оружием", (Fx.CartDamage, 1.2f), (Fx.GunDamage, .85f));
        AddTradeOff(arsenal, 2, LingeringToxins, "Lingering Toxins", "Затяжные токсины", "Your tranquilizer, net, freeze and poison last 25% longer (cap 10 s instead of 8 s)", "-15% direct gun and cart damage",
            "Ваши транквилизатор, сеть, заморозка и яд длятся на 25% дольше (предел 10 с вместо 8 с)", "-15% прямого урона оружием и тележкой",
            (Fx.StatusLength, 1.25f), (Fx.StatusCapSeconds, 10f), (Fx.GunDamage, .85f), (Fx.CartDamage, .85f));
        AddTradeOff(arsenal, 2, QuickShock, "Quick Shock", "Быстрый шок", "+25% gun and cart damage", "Your status effects are 25% shorter",
            "+25% урона оружием и тележкой", "Ваши статусы на 25% короче",
            (Fx.GunDamage, 1.25f), (Fx.CartDamage, 1.25f), (Fx.StatusLength, .75f));
        AddTradeOff(arsenal, 3, Executioner, "Executioner", "Палач", "+50% damage to stunned, frozen, netted or sedated monsters", "-25% damage to every other monster",
            "+50% урона по оглушённым, замороженным, пойманным в сеть или усыплённым монстрам", "-25% урона по остальным монстрам", (Fx.StunnedTargetDamage, 1.5f), (Fx.OtherTargetDamage, .75f));
        AddTradeOff(arsenal, 3, Overkill, "Overkill", "Перебор", "+40% gun and cart damage", "Your status effects last half as long",
            "+40% урона оружием и тележкой", "Ваши статусы длятся вдвое короче",
            (Fx.GunDamage, 1.4f), (Fx.CartDamage, 1.4f), (Fx.StatusLength, .5f));

        var survival = SkillPage.Survival;
        AddTradeOff(survival, 1, ThickSkin, "Thick Skin", "Толстая кожа", "-12% damage taken", "8% slower sprint",
            "-12% получаемого урона", "Бег на 8% медленнее",
            (Fx.DamageTaken, .88f), (Fx.SprintSpeed, .92f));
        AddTradeOff(survival, 1, Sprinter, "Sprinter", "Спринтер", "+10% sprint speed", "Sprinting costs 15% more stamina",
            "+10% скорости бега", "Бег тратит на 15% больше стамины",
            (Fx.SprintSpeed, 1.1f), (Fx.SprintStamina, 1.15f));
        AddTradeOff(survival, 2, Juggernaut, "Juggernaut", "Джаггернаут", "-25% damage taken", "15% slower walking, sprinting and crouching",
            "-25% получаемого урона", "Ходьба, бег и присед на 15% медленнее",
            (Fx.DamageTaken, .75f), (Fx.WalkSpeed, .85f), (Fx.SprintSpeed, .85f), (Fx.CrouchSpeed, .85f));
        AddTradeOff(survival, 2, Daredevil, "Daredevil", "Сорвиголова", "+20% walking, sprinting and crouching speed", "+20% damage taken",
            "+20% скорости ходьбы, бега и приседа", "+20% получаемого урона",
            (Fx.WalkSpeed, 1.2f), (Fx.SprintSpeed, 1.2f), (Fx.CrouchSpeed, 1.2f), (Fx.DamageTaken, 1.2f));
        AddTradeOff(survival, 2, SecondHeart, "Second Heart", "Второе сердце", "HP Regeneration works twice as fast", "Health packs heal you 30% less",
            "Регенерация HP работает вдвое быстрее", "Аптечки лечат вас на 30% меньше", (Fx.RegenRate, 2f), (Fx.SelfHeal, .7f));
        AddTradeOff(survival, 3, LastStand, "Last Stand", "Последний рубеж", "Once per level, a hit that would kill you leaves you at 1 HP", "-30% max stamina",
            "Раз за уровень удар, который убил бы вас, оставляет 1 HP", "-30% максимальной стамины", (Fx.MaxStamina, .7f)).Special = true;
        AddTradeOff(survival, 3, GhostRunner, "Ghost Runner", "Призрак", "Monsters hear you 30% less; +15% crouching speed", "+15% damage taken",
            "Монстры слышат вас на 30% хуже; +15% скорости в присяде", "+15% получаемого урона", (Fx.Noise, .7f), (Fx.CrouchSpeed, 1.15f), (Fx.DamageTaken, 1.15f));

        var recon = SkillPage.Recon;
        AddTradeOff(recon, 1, Floodlight, "Floodlight", "Широкий луч", "Flashlight beam 40% wider", "Beam 25% dimmer and shorter",
            "Луч фонарика на 40% шире", "Луч на 25% тусклее и короче",
            (Fx.FlashWidth, 1.4f), (Fx.FlashRange, .75f), (Fx.FlashBright, .75f));
        AddTradeOff(recon, 1, Spotlight, "Spotlight", "Дальний луч", "Flashlight reaches 40% farther and is 25% brighter", "Beam 30% narrower",
            "Фонарик светит на 40% дальше и на 25% ярче", "Луч на 30% уже",
            (Fx.FlashRange, 1.4f), (Fx.FlashBright, 1.25f), (Fx.FlashWidth, .7f));
        AddTradeOff(recon, 2, Scout, "Scout", "Разведчик", "+50% Autoscan reach", "Monsters hear you 15% more",
            "+50% радиуса автоскана", "Монстры слышат вас на 15% лучше",
            (Fx.AutoscanReach, 1.5f), (Fx.Noise, 1.15f));
        AddTradeOff(recon, 2, EchoPing, "Echo Ping", "Эхо-пинг", "Your pings show monsters within 8 m on your map while the ping lasts", "Monsters near the spot you ping come to look",
            "Пока действует метка, на карте видны монстры в радиусе 8 м от неё", "Монстры поблизости идут проверить отмеченное место").Special = true;
        AddTradeOff(recon, 3, Oracle, "Oracle", "Оракул", "Your map shows every valuable on the level, found or not", "+20% damage taken",
            "Ваша карта показывает все ценности уровня, найденные или нет", "+20% получаемого урона",
            (Fx.DamageTaken, 1.2f)).Special = true;
        AddTradeOff(recon, 3, HuntersEye, "Hunter's Eye", "Глаз охотника", "Monster dots on your map; pings show monster HP, threat tier and distance, as if you had those perks", "Crouching and crawling no longer hide you from monsters' eyes",
            "Карта показывает монстров, а метки — их здоровье, уровень угрозы и расстояние", "Монстры видят вас, даже когда вы крадётесь или ползёте").Special = true;

        var utility = SkillPage.Utility;
        AddTradeOff(utility, 1, Tinkerer, "Tinkerer", "Самоделкин", "Battery items drain 20% slower", "Drone batteries last 20% shorter",
            "Предметы с батареей разряжаются на 20% медленнее", "Батареи дронов на 20% короче",
            (Fx.BatteryItems, .8f), (Fx.BatteryDrones, 1.25f));
        AddTradeOff(utility, 1, DroneJockey, "Drone Jockey", "Дронщик", "Drone batteries last 25% longer", "Battery items drain 15% faster",
            "Батареи дронов на 25% дольше", "Предметы с батареей разряжаются на 15% быстрее",
            (Fx.BatteryDrones, .8f), (Fx.BatteryItems, 1.15f));
        AddTradeOff(utility, 2, MedicsOath, "Medic's Oath", "Клятва медика", "Field Medic gives teammates 50% more", "Health packs heal you 25% less",
            "Полевой медик даёт союзникам на 50% больше", "Аптечки лечат вас на 25% меньше",
            (Fx.MedicShare, 1.5f), (Fx.SelfHeal, .75f));
        AddTradeOff(utility, 2, FortuneSeeker, "Fortune Seeker", "Искатель удачи", "Skill boxes roll as if you had one more Lucky Finder level", "Battery crystals give you half the charge",
            "Ящики навыков дают очки так, словно у «Везунчика» на один уровень больше", "Энергокристаллы дают вам вдвое меньше заряда", (Fx.LuckyLevels, 1f), (Fx.CrystalCharge, .5f));
        AddTradeOff(utility, 3, Overclock, "Overclock", "Разгон", "Battery items and guns drain 30% slower", "-20% grab strength",
            "Предметы с батареей и оружие разряжаются на 30% медленнее", "-20% силы захвата",
            (Fx.BatteryItems, .7f), (Fx.BatteryGuns, .7f), (Fx.GrabStrength, .8f));
        AddTradeOff(utility, 3, Salvager, "Salvager", "Сборщик", "Loot you hold loses 30% less value when it breaks", "You move 10% slower while holding loot",
            "Лут в руках теряет на 30% меньше ценности при поломке", "Пока держите лут, вы двигаетесь на 10% медленнее", (Fx.LootBreakLoss, .7f), (Fx.HoldingLootSpeed, .9f));
    }

    private static TradeOff AddTradeOff(SkillPage page, int tier, string id, string name, string nameRu, string up, string down,
        string upRu, string downRu, params (Fx, float)[] effects)
    {
        var node = Add(page, TradeOffRules.Branch(tier), id, name, nameRu, tier, 1, root: true);
        node.Tier = tier;
        var tradeOff = TradeOffRules.Define(id, tier, up, down, effects);
        tradeOff.UpRu = upRu;
        tradeOff.DownRu = downRu;
        return tradeOff;
    }

    /// <summary>The quest-only perks, in catalogue order (index = the id a quest contract sends).</summary>
    internal static readonly List<SkillNode> QuestPerks = new();

    /// <summary>
    /// Status length with <paramref name="level"/> of <paramref name="maxLevel"/> bought: each level moves the base a
    /// step toward <see cref="StatusCapSeconds"/>. Longer statuses grow up to the cap and never past it; poison
    /// (<paramref name="compress"/>) shrinks toward the cap instead, keeping its total damage, so it hits harder per
    /// second but never runs shorter than the cap.
    /// </summary>
    internal static float StatusSeconds(float baseSeconds, int level, int maxLevel, bool compress = false)
    {
        if (level <= 0 || maxLevel <= 0) return baseSeconds;
        float t = Math.Min(1f, (float)level / maxLevel);
        if (compress) return baseSeconds <= StatusCapSeconds ? baseSeconds : baseSeconds - (baseSeconds - StatusCapSeconds) * t;
        return baseSeconds >= StatusCapSeconds ? baseSeconds : baseSeconds + (StatusCapSeconds - baseSeconds) * t;
    }

    private static SkillNode Add(SkillPage page, string branch, string id, string name, string nameRu, int cost, int maxLevel,
        string requires = null, bool root = false)
    {
        var node = new SkillNode
        {
            Id = id, Page = page, Branch = branch, Name = name, NameRu = nameRu, Cost = cost, MaxLevel = maxLevel,
            Requires = requires == null ? Array.Empty<string>() : new[] { requires }, Root = root,
        };
        Nodes.Add(node);
        ById.Add(id, node);
        return node;
    }

    internal static SkillNode QuestPerk(int index)
    {
        if (QuestPerks.Count == 0) foreach (var node in Nodes) if (node.QuestOnly) QuestPerks.Add(node);
        return index >= 0 && index < QuestPerks.Count ? QuestPerks[index] : null;
    }

    internal static int QuestPerkIndex(string id)
    {
        QuestPerk(0);
        return QuestPerks.FindIndex(n => n.Id == id);
    }

    /// <summary>Points needed to max the tree: every normal node, plus one trade-off per tier on each page.</summary>
    internal static int TotalCost()
    {
        int sum = 0;
        foreach (SkillPage page in Enum.GetValues(typeof(SkillPage))) sum += PageCost(page);
        return sum;
    }

    /// <summary>Points to max a page: its normal nodes plus one pick on each trade-off tier it has.</summary>
    internal static int PageCost(SkillPage page)
    {
        int sum = 0;
        var tiers = new HashSet<int>();
        foreach (var node in Nodes)
        {
            if (node.Page != page) continue;
            if (node.Tier == 0) sum += node.TotalCost;
            else if (tiers.Add(node.Tier)) sum += node.TotalCost;
        }
        return sum;
    }

    /// <summary>Points to max a page's normal nodes only (what the trade-off gates are measured against).</summary>
    internal static int NormalPageCost(SkillPage page)
    {
        int sum = 0;
        foreach (var node in Nodes) if (node.Page == page && node.Tier == 0 && !node.QuestOnly) sum += node.TotalCost;
        return sum;
    }
}
