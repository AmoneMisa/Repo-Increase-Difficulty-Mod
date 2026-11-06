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
    public const string MapUplink = P + "MapUplink", DeadPlayers = P + "DeadPlayers", ActiveMonsters = P + "ActiveMonsters",
        RespawnTimers = P + "RespawnTimers", MonsterDots = P + "MonsterDots", QuotaHeading = P + "QuotaHeading",
        ValuableTracker = P + "ValuableTracker", MapPings = P + "MapPings", ZoomOne = P + "ZoomOne", ZoomTwo = P + "ZoomTwo",
        CosmeticTracker = P + "CosmeticTracker";
    public const string Ping = P + "Ping", TargetInfo = P + "TargetInfo", EnemyHp = P + "EnemyHp", LootCondition = P + "LootCondition",
        Weight = P + "Weight", Threat = P + "Threat";
    public const string FlashTuning = P + "FlashTuning", BeamWidth = P + "BeamWidth", Brightness = P + "Brightness", BeamColour = P + "BeamColour";
    // Combat
    public const string Brawler = P + "Brawler", GrenadeDamage = P + "GrenadeDamage", MeleeDamage = P + "MeleeDamage", TumbleDamage = P + "TumbleDamage";
    public const string Gunsmith = P + "Gunsmith", BulletShots = P + "BulletShots", LaserShots = P + "LaserShots", TranqShots = P + "TranqShots";
    public const string Technician = P + "Technician", DroneBattery = P + "DroneBattery", Overcharge = P + "Overcharge";
    // Survival
    public const string Survivor = P + "Survivor", StaminaEfficiency = P + "StaminaEfficiency", SecondWind = P + "SecondWind",
        Armor = P + "Armor", HpRegen = P + "HpRegen";
    // Utility
    public const string InventorySlot = P + "InventorySlot", Autoscan = P + "Autoscan", HeadBattery = P + "HeadBattery", TumbleWings = P + "TumbleWings";
    // Arsenal: damage per weapon, and longer status effects from your own weapons
    public const string Marksman = P + "Marksman", HandgunDamage = P + "HandgunDamage", RevolverDamage = P + "RevolverDamage",
        ShotgunDamage = P + "ShotgunDamage", CartCannonDamage = P + "CartCannonDamage", CartLaserDamage = P + "CartLaserDamage",
        StaffDamage = P + "StaffDamage", CrossbowDamage = P + "CrossbowDamage";
    public const string StatusLab = P + "StatusLab", TranqTime = P + "TranqTime", NetTime = P + "NetTime", FreezeTime = P + "FreezeTime",
        PoisonRush = P + "PoisonRush";

    // Contracts: quest-only perks, earned from special quest contracts and never bought with points
    public const string CartHandling = P + "CartHandling", GentleHands = P + "GentleHands", CalmPresence = P + "CalmPresence",
        SoftSteps = P + "SoftSteps";

    /// <summary>No status from a tree perk lasts longer than this; poison is compressed down to it instead.</summary>
    internal const float StatusCapSeconds = 8f;

    private static readonly List<SkillNode> Nodes = new();
    private static readonly Dictionary<string, SkillNode> ById = new(StringComparer.Ordinal);

    internal static IReadOnlyList<SkillNode> All => Nodes;
    internal static SkillNode Find(string id) => id != null && ById.TryGetValue(id, out var node) ? node : null;

    static SkillCatalog()
    {
        var recon = SkillPage.Recon;
        Add(recon, "Map", MapUplink, "Map Uplink", "Карта: база", 1, 1, root: true);
        Add(recon, "Map", DeadPlayers, "Dead Players", "Мёртвые игроки", 1, 1, MapUplink);
        Add(recon, "Map", ActiveMonsters, "Active Monsters", "Активные монстры", 1, 1, MapUplink);
        Add(recon, "Map", RespawnTimers, "Respawning Monsters", "Респавн монстров", 1, 1, ActiveMonsters);
        Add(recon, "Map", MonsterDots, "Monster Dots", "Монстры точками", 2, 1, RespawnTimers);
        Add(recon, "Map", QuotaHeading, "Quota Heading", "Направление к квоте", 1, 1, MapUplink);
        Add(recon, "Map", ValuableTracker, "Valuable Tracker", "Трекер ценностей", 1, 1, QuotaHeading);
        Add(recon, "Map", MapPings, "Pings on Map", "Пинги на карте", 1, 1, MapUplink);
        Add(recon, "Map", ZoomOne, "Map Zoom I", "Зум карты I", 1, 1, MapUplink);
        Add(recon, "Map", ZoomTwo, "Map Zoom II", "Зум карты II", 1, 1, ZoomOne);
        Add(recon, "Map", CosmeticTracker, "Cosmetic Tracker", "Трекер косметики", 2, 1, MapUplink);

        Add(recon, "Ping", Ping, "Ping Anywhere", "Пинг", 2, 1, root: true);
        Add(recon, "Ping", TargetInfo, "Target Info", "Инфо цели", 1, 1, Ping);
        Add(recon, "Ping", EnemyHp, "Enemy HP", "HP врага", 1, 1, Ping);
        Add(recon, "Ping", LootCondition, "Loot Condition", "Состояние лута", 1, 1, Ping);
        Add(recon, "Ping", Weight, "Target Weight", "Вес цели", 1, 1, Ping);
        Add(recon, "Ping", Threat, "Monster Threat", "Угроза моба", 1, 1, Ping);

        Add(recon, "Flashlight", FlashTuning, "Flashlight Tuning", "Фонарик", 1, 1, root: true);
        Add(recon, "Flashlight", BeamWidth, "Beam Width", "Радиус света", 1, 3, FlashTuning);
        Add(recon, "Flashlight", Brightness, "Brightness", "Яркость света", 1, 3, FlashTuning);
        Add(recon, "Flashlight", BeamColour, "Beam Colour", "Цвет света", 1, 1, FlashTuning);

        var combat = SkillPage.Combat;
        Add(combat, "Damage", Brawler, "Brawler", "Урон", 1, 1, root: true);
        Add(combat, "Damage", GrenadeDamage, "Grenade Damage", "Урон гранат", 1, 5, Brawler);
        Add(combat, "Damage", MeleeDamage, "Melee Damage", "Урон в ближнем бою", 1, 5, Brawler);
        Add(combat, "Damage", TumbleDamage, "Tumble Damage", "Урон в кувырке", 1, 5, Brawler);

        Add(combat, "Guns", Gunsmith, "Gunsmith", "Заряды оружия", 1, 1, root: true);
        Add(combat, "Guns", BulletShots, "Bullet Gun Shots", "Пули", 1, 5, Gunsmith);
        Add(combat, "Guns", LaserShots, "Laser Gun Shots", "Лазер", 1, 5, Gunsmith);
        Add(combat, "Guns", TranqShots, "Tranq Gun Shots", "Транквилизатор", 1, 5, Gunsmith);

        Add(combat, "Tech", Technician, "Technician", "Техника", 1, 1, root: true);
        Add(combat, "Tech", DroneBattery, "Drone Battery", "Заряд дронов", 1, 5, Technician);
        Add(combat, "Tech", Overcharge, "Overcharge", "Перегрузка", 1, 5, Technician);

        var survival = SkillPage.Survival;
        Add(survival, "Survival", Survivor, "Survivor", "Выживание", 1, 1, root: true);
        Add(survival, "Survival", StaminaEfficiency, "Stamina Efficiency", "Меньше расход стамины", 1, 5, Survivor);
        Add(survival, "Survival", SecondWind, "Second Wind", "Второе дыхание", 1, 2, StaminaEfficiency);
        Add(survival, "Survival", Armor, "Armor", "Броня", 2, 5, Survivor);
        Add(survival, "Survival", HpRegen, "HP Regeneration", "Регенерация HP", 3, 3, Survivor);

        var utility = SkillPage.Utility;
        Add(utility, "Gear", InventorySlot, "Inventory Slot", "Слот инвентаря", 3, 4, root: true);
        Add(utility, "Gear", Autoscan, "Autoscan", "Автоскан", 1, 5, root: true);
        Add(utility, "Gear", HeadBattery, "Death Head Battery", "Заряд мёртвой головы", 1, 3, root: true).Vanilla = VanillaUpgrade.DeathHeadBattery;
        Add(utility, "Gear", TumbleWings, "Tumble Wings", "Tumble Wings", 1, 3, root: true).Vanilla = VanillaUpgrade.TumbleWings;

        // Cart weapons count for whoever is operating them.
        var arsenal = SkillPage.Arsenal;
        Add(arsenal, "Weapons", Marksman, "Marksman", "Стрелок", 1, 1, root: true);
        Add(arsenal, "Weapons", HandgunDamage, "Handgun Damage", "Урон пистолета", 1, 5, Marksman);
        Add(arsenal, "Weapons", RevolverDamage, "Revolver Damage", "Урон револьвера", 1, 3, Marksman);
        Add(arsenal, "Weapons", ShotgunDamage, "Shotgun Damage", "Урон дробовика", 1, 4, Marksman);
        Add(arsenal, "Weapons", CartCannonDamage, "Cart Cannon Damage", "Урон пушки тележки", 1, 3, Marksman);
        Add(arsenal, "Weapons", CartLaserDamage, "Cart Laser Damage", "Урон лазера тележки", 1, 3, Marksman);
        Add(arsenal, "Weapons", StaffDamage, "Wizard Staff Damage", "Урон посоха", 1, 4, Marksman);
        Add(arsenal, "Weapons", CrossbowDamage, "Poison Crossbow Damage", "Урон арбалета", 1, 3, Marksman);

        Add(arsenal, "Status", StatusLab, "Status Lab", "Статусы", 1, 1, root: true);
        Add(arsenal, "Status", TranqTime, "Longer Tranquilizer", "Дольше транквилизатор", 1, 3, StatusLab);
        Add(arsenal, "Status", NetTime, "Longer Net", "Дольше сеть", 1, 3, StatusLab);
        Add(arsenal, "Status", FreezeTime, "Longer Freeze", "Дольше заморозка", 1, 3, StatusLab);
        Add(arsenal, "Status", PoisonRush, "Fast Poison", "Быстрый яд", 1, 3, StatusLab);

        var contracts = SkillPage.Contracts;
        Add(contracts, "Contracts", CartHandling, "Cart Handling", "Лёгкая тележка", 0, 3, root: true).QuestOnly = true;
        Add(contracts, "Contracts", GentleHands, "Gentle Hands", "Бережные руки", 0, 3, root: true).QuestOnly = true;
        Add(contracts, "Contracts", CalmPresence, "Calm Presence", "Спокойствие", 0, 3, root: true).QuestOnly = true;
        Add(contracts, "Contracts", SoftSteps, "Soft Steps", "Тихие шаги", 0, 3, root: true).QuestOnly = true;
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

    /// <summary>Points needed to max every node.</summary>
    internal static int TotalCost()
    {
        int sum = 0;
        foreach (var node in Nodes) sum += node.TotalCost;
        return sum;
    }

    internal static int PageCost(SkillPage page)
    {
        int sum = 0;
        foreach (var node in Nodes) if (node.Page == page) sum += node.TotalCost;
        return sum;
    }
}
