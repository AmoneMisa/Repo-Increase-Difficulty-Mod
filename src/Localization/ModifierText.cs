using System.Collections.Generic;

namespace IncreaseHardMod.Localization;

/// <summary>
/// Russian names and descriptions for everything the level HUD lists as a modifier: surprise modifiers, stat
/// modifiers, level economy events and shop economy events. Keyed by the English text, so the definitions stay as
/// they are; text without an entry is shown in English.
/// </summary>
internal static class ModifierText
{
    /// <summary>The text in the game's language: Russian when the game is Russian and a translation exists.</summary>
    internal static string T(string english) =>
        english != null && GameLanguage.Russian && Ru.TryGetValue(english, out var russian) ? russian : english;

    // Descriptions that carry the level's rolled number.
    internal static string ReducedHp(int percent) => GameLanguage.Russian
        ? $"В начале уровня останется {percent}% текущего здоровья. Максимум здоровья сохранится."
        : $"Start with {percent}% of current HP. Max HP is unchanged.";
    internal static string ExtraSouls(int count) => GameLanguage.Russian
        ? $"С монстров может выпасть до {count} душ."
        : $"Monsters drop {count} souls.";
    internal static string MoneyLoss(int percent) => GameLanguage.Russian
        ? $"При смерти вы теряете {percent}% собранных денег."
        : $"Dying loses {percent}% of collected money.";

    internal static readonly Dictionary<string, string> Ru = new()
    {
        // ---- surprise modifiers: names
        ["Fragile Loot"] = "Хрупкая добыча",
        ["Heavy Loot"] = "Тяжёлая добыча",
        ["Light Loot"] = "Лёгкая добыча",
        ["Protected Loot"] = "Прочная добыча",
        ["Dangerous Loot"] = "Опасная добыча",
        ["Extra Small Cart"] = "Ещё одна малая тележка",
        ["Extra Big Cart"] = "Ещё одна большая тележка",
        ["Extra Souls"] = "Больше душ",
        ["Spread Upgrades"] = "Разбросанные улучшения",
        ["No Map"] = "Без карты",
        ["Reduced HP"] = "Раненые на старте",
        ["Auto-Revive"] = "Автовоскрешение",
        ["Money Loss on Death"] = "Потеря денег при смерти",
        ["Timed Level"] = "Уровень на время",
        ["Changed Voices"] = "Чужие голоса",
        ["More Gacha Cubes"] = "Больше гача-кубов",
        ["Souls Explode Harder"] = "Души взрываются сильнее",
        ["Bigger Soul Drops"] = "Дорогие души",
        ["Pit-Tanky Monsters"] = "Монстры не боятся ям",
        ["Light Monsters"] = "Лёгкие монстры",
        ["Heavy Monsters"] = "Тяжёлые монстры",
        ["Mystery Boxes"] = "Ящики-сюрпризы",
        ["Silent Night"] = "Тихая ночь",
        ["Loud Facility"] = "Гулкий комплекс",
        ["Greased Wheels"] = "Смазанные колёса",
        ["Short Memory"] = "Короткая память",
        ["Thorny Monsters"] = "Колючие монстры",

        // ---- surprise modifiers: descriptions
        ["Loot breaks and loses value more easily."] = "Ценности легче разбить, и при ударах они быстрее дешевеют.",
        ["Loot weighs more, making it harder to haul."] = "Ценности весят больше, поэтому их труднее переносить.",
        ["Loot weighs less, making it easier to haul."] = "Ценности весят меньше, поэтому их легче переносить.",
        ["Loot takes less damage before breaking."] = "Ценности выдерживают больше ударов.",
        ["Hazardous valuables deal more damage to players."] = "Опасные ценности наносят игрокам больше урона.",
        ["An extra small cart spawns on the level."] = "На уровне появляется ещё одна малая тележка.",
        ["An extra big cart spawns on the level."] = "На уровне появляется ещё одна большая тележка.",
        ["Monsters drop more souls."] = "С монстров выпадает больше душ.",
        ["Up to 6 upgrades on the map. Submit them with a successful quota to receive them in the next truck."] = "До 6 улучшений на всю карту. Сдайте их вместе с успешной квотой — они появятся в следующем грузовике.",
        ["No map until you find a map upgrade pickup."] = "Карта заработает, когда вы найдёте улучшение для неё.",
        ["Everyone starts the level wounded — current HP is cut to a rolled percentage (max HP is untouched, so you can heal back up)."] =
            "Все начинают уровень ранеными. Сколько здоровья останется, решает случай. Лечиться можно до обычного максимума.",
        ["Each player gets one automatic revive this level."] = "Каждый игрок один раз автоматически воскресает на этом уровне.",
        ["Dying loses a percentage of your collected money."] = "При смерти вы теряете часть собранных денег.",
        ["Timed level: kills add time, deaths remove it, quota adds it; at 0 the layout rerolls."] =
            "Убийства монстров и выполнение квоты добавляют время, гибель игроков его отнимает. Когда таймер дойдёт до нуля, уровень перестроится.",
        ["Player voices get swapped or randomized."] = "Голоса игроков меняются местами или становятся случайными.",
        ["More gacha/cube valuables spawn."] = "Появляется больше гача-кубов.",
        ["Exploding souls deal more damage to players."] = "Взрывающиеся души наносят игрокам больше урона.",
        ["Each soul drop is worth more money (value per drop, not count)."] = "Каждая выпавшая душа стоит дороже.",
        ["Monsters take less damage from death pits."] = "При падении в пропасть монстры получают меньше урона.",
        ["Monsters weigh less and yield to a single player's grab without Strength upgrades. Native hold-time, mounted and freeze restrictions still apply."] =
            "Монстров можно схватить в одиночку без улучшения силы. Действуют обычные ограничения захвата, в том числе для замороженных монстров и тех, на ком кто-то сидит.",
        ["All monsters weigh more, so they're hard to shove around."] = "Все монстры тяжелее, их трудно толкать.",
        ["Everything players do makes 40% less noise: footsteps, voices and dropped loot alert fewer monsters."] =
            "Игроки шумят на 40% меньше. Шаги, голоса и падение добычи привлекают меньше монстров.",
        ["The acoustics are terrible: monsters hear every footstep, voice and bump 50% farther away."] =
            "Ужасная акустика: монстры слышат каждый шаг, голос и удар на 50% дальше.",
        ["Freshly oiled wheels: C.A.R.T.s are much easier to push, even full and uphill."] =
            "Тележки легче толкать даже в гору с полным кузовом.",
        ["Monsters forget quickly: once they lose sight of you, they give up the chase sooner."] =
            "Потеряв вас из виду, монстры быстрее прекращают погоню.",
        ["About half the monsters grew thorns: hitting them with a melee weapon reflects up to 10% of the damage back at you."] =
            "Примерно у половины монстров выросли шипы: удар оружием ближнего боя возвращает вам до 10% урона.",
        ["Mystery boxes appear around the map. Drop one monster soul into a box to roll a random upgrade, money, or a weapon/grenade for this level only. Each box takes one soul."] =
            "В каждом ящике можно обменять одну душу монстра на случайный приз. Выпадут деньги, улучшение либо оружие или граната на этот уровень.",

        // ---- stat modifiers
        ["Gilded Loot"] = "Позолоченная добыча",
        ["Valuables are worth 25% more."] = "Ценности стоят на 25% больше.",
        ["Rich Pickings"] = "Богатая добыча",
        ["Valuables are worth 50% more, the quota 25% higher."] = "Ценности стоят на 50% больше, квота на 25% выше.",
        ["Cheap Trinkets"] = "Дешёвые безделушки",
        ["Valuables are worth 20% less."] = "Ценности стоят на 20% меньше.",
        ["Light Load"] = "Лёгкая ноша",
        ["The quota is 20% lower."] = "Квота на 20% ниже.",
        ["Heavy Load"] = "Тяжёлая ноша",
        ["The quota is 25% higher."] = "Квота на 25% выше.",
        ["Frail Monsters"] = "Хилые монстры",
        ["Monsters have 25% less health."] = "У монстров на 25% меньше здоровья.",
        ["Docile Monsters"] = "Смирные монстры",
        ["Monsters hit 25% softer."] = "Монстры бьют на 25% слабее.",
        ["Sluggish Monsters"] = "Вялые монстры",
        ["Monsters move 20% slower."] = "Монстры двигаются на 20% медленнее.",
        ["Slow Respawns"] = "Медленное возрождение",
        ["Monsters take 50% longer to come back."] = "Монстры возвращаются на 50% дольше.",
        ["Thin Patrols"] = "Редкие патрули",
        ["25% fewer monsters."] = "На 25% меньше монстров.",
        ["Tough Monsters"] = "Крепкие монстры",
        ["Monsters have 40% more health."] = "У монстров на 40% больше здоровья.",
        ["Brutal Monsters"] = "Свирепые монстры",
        ["Monsters hit 35% harder."] = "Монстры бьют на 35% сильнее.",
        ["Hasty Monsters"] = "Шустрые монстры",
        ["Monsters move 25% faster."] = "Монстры двигаются на 25% быстрее.",
        ["Relentless Respawns"] = "Неумолимое возрождение",
        ["Monsters come back 40% sooner."] = "Монстры возвращаются на 40% быстрее.",
        ["Infestation"] = "Нашествие",
        ["35% more monsters."] = "На 35% больше монстров.",
        ["Apex Hunters"] = "Высшие хищники",
        ["Monsters are tougher, harder-hitting and faster."] = "Монстры крепче, бьют сильнее и двигаются быстрее.",
        ["Swarm"] = "Рой",
        ["60% more monsters, but each with 15% less health."] = "На 60% больше монстров, но у каждого на 15% меньше здоровья.",
        ["Padded Suits"] = "Мягкие костюмы",
        ["Players take 20% less damage."] = "Игроки получают на 20% меньше урона.",
        ["Stimulants"] = "Стимуляторы",
        ["All healing is 40% stronger."] = "Всё лечение на 40% сильнее.",
        ["Glass Cannons"] = "Стеклянные пушки",
        ["Players take 30% more damage, monsters have 30% less health."] = "Игроки получают на 30% больше урона, у монстров на 30% меньше здоровья.",
        ["Thin Skin"] = "Тонкая кожа",
        ["Players take 30% more damage."] = "Игроки получают на 30% больше урона.",
        ["Poor Medicine"] = "Плохая медицина",
        ["All healing is 40% weaker."] = "Всё лечение на 40% слабее.",
        ["Fragile Everything"] = "Всё хрупкое",
        ["Players take 50% more damage and heal 30% less."] = "Игроки получают на 50% больше урона и лечатся на 30% меньше.",
        ["Sprawling Facility"] = "Обширный комплекс",
        ["A 25% bigger level with 15% more valuable loot."] = "Уровень на 25% больше, а лут на 15% ценнее.",
        ["Compact Facility"] = "Компактный комплекс",
        ["A 20% smaller level with 15% fewer monsters."] = "Уровень на 20% меньше и на 15% меньше монстров.",
        ["Extra Dock"] = "Дополнительная точка сдачи",
        ["One more extraction point."] = "На одну точку сдачи больше.",
        ["Cramped Dock"] = "Меньше точек сдачи",
        ["One fewer extraction point."] = "На одну точку сдачи меньше.",
        ["Gold Rush"] = "Золотая лихорадка",
        ["Valuables are worth 40% more, and 25% more monsters guard them."] = "Ценности стоят на 40% больше, и их охраняет на 25% больше монстров.",
        ["Bargain Bin"] = "Распродажа хлама",
        ["Valuables are worth 30% less, and the quota is 30% lower."] = "Ценности стоят на 30% меньше, и квота на 30% ниже.",
        ["Skeleton Crew"] = "Неполный состав",
        ["40% fewer monsters, but the quota is 15% higher."] = "На 40% меньше монстров, но квота на 15% выше.",
        ["Glass Jaw Monsters"] = "Монстры со стеклянной челюстью",
        ["Monsters have 40% less health but hit 30% harder."] = "У монстров на 40% меньше здоровья, но бьют они на 30% сильнее.",
        ["Blitz"] = "Блиц",
        ["Monsters are 30% faster and come back 30% sooner, with 20% less health."] = "Монстры на 30% быстрее и возвращаются на 30% раньше, но у них на 20% меньше здоровья.",
        ["Marathon"] = "Марафон",
        ["A 40% bigger level with one more extraction point."] = "Уровень на 40% больше и на одну точку сдачи больше.",
        ["Field Hospital"] = "Полевой госпиталь",
        ["Healing is 60% stronger, but players take 10% more damage."] = "Лечение на 60% сильнее, но игроки получают на 10% больше урона.",
        ["Iron Lungs"] = "Железные лёгкие",
        ["Players take 30% less damage, but the quota is 20% higher."] = "Игроки получают на 30% меньше урона, но квота на 20% выше.",

        // ---- level economy events
        ["Loot is worth 35% more."] = "Лут стоит на 35% больше.",
        ["Windfall"] = "Нежданная удача",
        ["Extractions pay 30% more."] = "За сдачу платят на 30% больше.",
        ["Grand Contract"] = "Крупный контракт",
        ["More valuable loot, a lighter quota and better pay."] = "Лут ценнее, квота легче, оплата выше.",
        ["Rich Vein"] = "Богатая жила",
        ["Loot is worth 15% more."] = "Лут стоит на 15% больше.",
        ["Light Quota"] = "Лёгкая квота",
        ["The quota is 25% lower."] = "Квота на 25% ниже.",
        ["Modest Quota"] = "Сниженная квота",
        ["The quota is 15% lower."] = "Квота на 15% ниже.",
        ["Generous Client"] = "Щедрый клиент",
        ["Extractions pay 15% more."] = "За сдачу платят на 15% больше.",
        ["Tax Break"] = "Налоговая льгота",
        ["A small rebate: extractions pay 10% more."] = "За сдачу платят на 10% больше.",
        ["High Stakes"] = "Высокие ставки",
        ["Loot is worth 30% more, but the quota is 30% higher."] = "Лут стоит на 30% больше, но квота на 30% выше.",
        ["Fire Sale"] = "Срочная распродажа",
        ["Loot is worth 20% less, and so is the quota."] = "Лут стоит на 20% меньше, и квота тоже.",
        ["Hazard Pay"] = "Надбавка за риск",
        ["Extractions pay 25% more, the quota is 15% higher."] = "За сдачу платят на 25% больше, а квота на 15% выше.",
        ["Budget Cuts"] = "Урезанный бюджет",
        ["Extractions pay 20% less, the quota is 15% lower."] = "За сдачу платят на 20% меньше, а квота на 15% ниже.",
        ["Inflated Loot"] = "Завышенные цены",
        ["Loot is worth 20% more, extractions pay 20% less."] = "Ценности на 20% дороже, но за сдачу платят на 20% меньше.",
        ["Tax Season"] = "Налоговый сезон",
        ["Loot is worth 15% less."] = "Лут стоит на 15% меньше.",
        ["Heavy Quota"] = "Тяжёлая квота",
        ["Tight Budget"] = "Скромный бюджет",
        ["Extractions pay 15% less."] = "За сдачу платят на 15% меньше.",
        ["Poor Facility"] = "Бедный комплекс",
        ["Extractions pay 30% less."] = "За сдачу платят на 30% меньше.",
        ["Bankruptcy"] = "Банкротство",
        ["Loot is worth 35% less."] = "Лут стоит на 35% меньше.",
        ["Tax Audit"] = "Налоговая проверка",
        ["Extractions pay 25% less and the quota is 10% higher."] = "За сдачу платят на 25% меньше, а квота на 10% выше.",
        ["Crushing Quota"] = "Неподъёмная квота",
        ["The quota is 40% higher."] = "Квота на 40% выше.",

        // ---- shop economy events
        ["Black Friday"] = "Чёрная пятница",
        ["Everything is 45% off."] = "Всё со скидкой 45%.",
        ["Grand Opening"] = "Торжественное открытие",
        ["Opening-week prices: 30-40% off."] = "В честь открытия скидки от 30 до 40%.",
        ["Weekend Sale"] = "Распродажа в выходные",
        ["Items are 20% off."] = "Предметы со скидкой 20%.",
        ["Upgrade Discount"] = "Скидка на улучшения",
        ["Upgrades are 40% off."] = "Улучшения со скидкой 40%.",
        ["Medic Sale"] = "Скидки на аптечки",
        ["Health packs are half price."] = "Аптечки за полцены.",
        ["Crystal Clearance"] = "Распродажа кристаллов",
        ["Power crystals are 40% off."] = "Энергокристаллы со скидкой 40%.",
        ["Loyalty Program"] = "Программа лояльности",
        ["Everything is 15% off."] = "Всё со скидкой 15%.",
        ["Boutique Day"] = "День премиум-товаров",
        ["Items cost 30% more, upgrades are 30% off."] = "Предметы на 30% дороже, улучшения со скидкой 30%.",
        ["Health packs are 40% off, crystals cost 40% more."] = "Аптечки со скидкой 40%, кристаллы на 40% дороже.",
        ["Arms Race"] = "Гонка вооружений",
        ["Items are 25% off, upgrades cost 30% more."] = "Предметы со скидкой 25%, улучшения на 30% дороже.",
        ["Supply Run"] = "Закупка припасов",
        ["Crystals are 30% off, items cost 20% more."] = "Кристаллы со скидкой 30%, предметы на 20% дороже.",
        ["Inflation"] = "Инфляция",
        ["Everything costs 30% more."] = "Всё на 30% дороже.",
        ["Import Tax"] = "Ввозная пошлина",
        ["Items cost 25% more."] = "Предметы на 25% дороже.",
        ["Luxury Tax"] = "Налог на роскошь",
        ["Items cost 40% more."] = "Предметы на 40% дороже.",
        ["Upgrade Surcharge"] = "Наценка на улучшения",
        ["Upgrades cost 40% more."] = "Улучшения на 40% дороже.",
        ["Medical Shortage"] = "Нехватка медикаментов",
        ["Health packs cost 50% more."] = "Аптечки на 50% дороже.",
        ["Crystal Shortage"] = "Нехватка кристаллов",
        ["Power crystals cost 50% more."] = "Энергокристаллы на 50% дороже.",
        ["Market Manipulation"] = "Манипуляция рынком",
        ["Everything costs 60% more."] = "Всё на 60% дороже.",
        ["Hyper Inflation"] = "Гиперинфляция",
        ["Prices are 70-90% higher."] = "Цены выше на 70-90%.",
        ["Security Tax"] = "Налог на охрану",
        ["Items and upgrades cost 50% more."] = "Предметы и улучшения на 50% дороже.",
    };
}

/// <summary>A HUD toolkit status line in the caution colour (challenges and other timed states).</summary>
internal static class HudStatus
{
    internal static void Show(string key, string text) => Hud.HudNotify.Status(key, text, Hud.HudStyle.Caution);
    internal static void Clear(string key) => Hud.HudNotify.Clear(key);
}
