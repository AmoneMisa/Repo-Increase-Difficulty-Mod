using UnityEngine;
using C = IncreaseHardMod.Skills.SkillCatalog;

namespace IncreaseHardMod.Skills;

/// <summary>Player-facing perk descriptions for the tree screen, with the numbers the current config gives.</summary>
internal static class SkillText
{
    private static string Pct(float fraction) => Mathf.RoundToInt(fraction * 100f) + "%";
    private static string Sec(float seconds) => seconds.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) + " s";

    // An old upgrade's per-level step expressed per tree level (old ladder of oldMax levels squeezed into the node).
    private static float PerLevel(float oldPerLevel, int oldMax, string node) =>
        oldPerLevel * Mathf.Max(1, oldMax) / Mathf.Max(1, C.Find(node).MaxLevel);

    /// <summary>The game's language is Russian (shared check in Localization/ModifierText.cs).</summary>
    internal static bool Russian => Localization.GameLanguage.Russian;

    /// <summary>A node's display name: Russian for every perk when the game is Russian.</summary>
    internal static string Name(SkillNode node) =>
        Russian && !string.IsNullOrEmpty(node.NameRu) ? node.NameRu : node.Name;

    private static string SecRu(float seconds) => seconds.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) + " с";

    // The instinct line: trigger, buff, then the level ladder shared by all of them.
    private static string Instinct(string trigger, string buff) => $"When {trigger.Substring(0, 1).ToLowerInvariant()}{trigger.Substring(1)}: {buff} for 3/4/5 s (by level). Cooldown 60/45/30 s.";

    internal static string Describe(string id)
    {
        if (Russian)
        {
            string ru = DescribeRu(id);
            if (!string.IsNullOrEmpty(ru)) return ru;
        }
        float root = SkillEffects.RootBonus, weaponMax = Mathf.Max(0f, ModConfig.SkillWeaponDamageMax.Value);
        string Weapon(string node, string weapon) => $"+{Pct(weaponMax / C.Find(node).MaxLevel)} {weapon} damage per level (+{Pct(weaponMax)} at max).";
        var tradeOff = TradeOffRules.Find(id);
        if (tradeOff != null)
            return Russian && tradeOff.UpRu != null
                ? $"{tradeOff.UpRu}.  <color=#FF8C8C>Минус: {tradeOff.DownRu}.</color>"
                : $"{tradeOff.Up}.  <color=#FF8C8C>Downside: {tradeOff.Down}.</color>";
        switch (id)
        {
            case C.MapUplink: return "Every teammate shows on the map with their nickname, even unseen: a white figure while alive, a red death head when dead. The map also counts the living and the dead.";
            case C.ActiveMonsters: return "The map shows how many monsters are active.";
            case C.MapLandmarks: return "The challenge shrine and every battery crystal nobody has taken show on the map, each with its own icon: a gold shrine and green crystals.";
            case C.BetterMap: return "With the map up, press M to open the big map (the spectator map): a large panel you can pan with the arrows and zoom with +/-. M again or lowering the map closes it.";
            case C.RespawnTimers: return "The map lists every monster waiting to respawn and how long until it comes back.";
            case C.MonsterDots: return "Monsters appear as dots on the map.";
            case C.QuotaHeading: return "The map shows which way the quota is.";
            case C.ValuableTracker: return "The map counts valuables found and your haul.";
            case C.MapPings: return "Team pings appear on the map, including your teammates'.";
            case C.ZoomOne: return "Zoom the map out with the zoom keys.";
            case C.ZoomTwo: return "Zoom the map out even further.";
            case C.CosmeticTracker: return "Cosmetic boxes appear on the map.";

            case C.Ping: return "Ping players, monsters, loot and places; the team sees pings through walls.";
            case C.TargetInfo: return "Pings name what they mark: prices, rarity, monster and item names.";
            case C.PingRange: return "Every ping on screen shows how far it is from you, and how high it sits off the floor when it is on a shelf or ledge.";
            case C.PingPath: return "A glowing path on the floor leads you along the walkable route to each ping (the three newest).";
            case C.PingCompass: return "Pings you can't see (off screen or behind you) get an arrow on the edge of the screen pointing the way.";
            case C.EnemyHp: return "Pinged monsters show their health.";
            case C.LootCondition: return "Pinged loot shows how much of its value is left.";
            case C.Weight: return "Pinged objects show their weight.";
            case C.Threat: return "Pinged monsters show their danger tier.";

            case C.FlashTuning: return $"Your flashlight reaches {Pct(ModConfig.SkillFlashRange.Value)} farther.";
            case C.BeamWidth: return $"+{Pct(ModConfig.SkillFlashWidthPerLevel.Value)} wider beam per level.";
            case C.Brightness: return $"+{Pct(ModConfig.SkillFlashBrightnessPerLevel.Value)} brighter beam per level.";
            case C.BeamColour: return "Choose a beam colour your team sees too. Click again to change it.";

            case C.Brawler: return $"+{Pct(root)} melee, grenade and tumble damage.";
            case C.GrenadeDamage: return $"+{Pct(PerLevel(ModConfig.GrenadeDamagePercentPerLevel.Value, ModConfig.GrenadeDamageMaxLevel.Value, id))} grenade damage per level.";
            case C.Vampirism:
            {
                var node = C.Find(id);
                float max = ModConfig.SkillVampireMax.Value;
                return $"Melee hits heal you for {(max * 100f / node.MaxLevel).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture)}% of the damage dealt per level ({Pct(max)} at max).";
            }
            case C.Adrenaline: return Instinct("A monster hurts you", "+30% speed");
            case C.IronSkin: return Instinct("Your health drops below 35%", "40% less damage taken");
            case C.HotBarrel: return Instinct("You kill a monster with a bullet gun", "+30% bullet gun damage");
            case C.Overheat: return Instinct("You hit a monster with a laser", "+30% laser damage");
            case C.BattleTrance: return Instinct("You land 3 melee hits within 4 s", "+30% melee damage");
            case C.MendingSurge: return "When you kill a monster: heal 5/10/15% of your max health over 5 s (by level). Cooldown 60/45/30 s.";
            case C.MeleeDamage: return $"+{Pct(PerLevel(ModConfig.MeleeDamagePercentPerLevel.Value, ModConfig.MeleeDamageMaxLevel.Value, id))} melee damage per level.";
            case C.TumbleDamage:
            {
                float max = Mathf.Pow(1f + Mathf.Max(0f, ModConfig.TumbleDamagePerLevel.Value), ModConfig.TumbleMaxLevel.Value) - 1f;
                return $"Tumble launches hit harder (+{Pct(max)} at max) and hurt you less.";
            }
            case C.Gunsmith: return "Every gun gets one extra level's worth of shots per charge.";
            case C.BulletShots: case C.LaserShots: case C.TranqShots:
            {
                string kind = id == C.BulletShots ? "bullet" : id == C.LaserShots ? "laser" : "tranquilizer";
                float shots = PerLevel(ModConfig.GunExtraBulletsPerLevel.Value, ModConfig.GunExtraBulletsMaxLevel.Value, id);
                return $"More shots per charge for {kind} guns: the Handgun's share is +{shots:0.#} per level; other guns scale by their own ammo.";
            }
            case C.Technician: return "+10% drone battery life.";
            case C.DroneBattery: return $"+{Pct(PerLevel(ModConfig.DroneBatteryPercentPerLevel.Value, Upgrades.BatteryLifeUpgrade.EffectiveMaxLevel(true), id))} drone battery life per level.";
            case C.Overcharge: return "Hold monsters longer before your grab beam overloads; it recovers faster.";

            case C.Survivor: return $"+{ModConfig.SkillSurvivorStamina.Value:0} max stamina.";
            case C.StaminaEfficiency:
            {
                float keep = Mathf.Pow(1f - Mathf.Clamp(ModConfig.StaminaPercentPerLevel.Value, 0f, .5f), ModConfig.StaminaMaxLevel.Value);
                return $"Sprinting uses less stamina (-{Pct(1f - keep)} at max).";
            }
            case C.SecondWind: return $"Stamina recovers {Pct(ModConfig.SkillSecondWindPerLevel.Value)} faster per level.";
            case C.Armor: return $"-{Pct(PerLevel(ModConfig.ArmorPercentPerLevel.Value, ModConfig.ArmorMaxLevel.Value, id))} damage taken per level (up to -{Pct(Upgrades.SurvivalUpgrades.ArmorCap)}).";
            case C.HpRegen: return $"Slowly regain health, up to {Pct(ModConfig.RegenMaxHpFraction.Value)} of max HP; faster per level.";

            case C.Desperation: return $"-{Pct(ModConfig.SkillDesperationPerLevel.Value)} damage taken per level while your health is under {Pct(ModConfig.SkillDesperationThreshold.Value)}.";
            case C.HuntedWarning: return "Shows HUNTED on your screen while a monster is chasing you.";
            case C.CrouchSpeed: return $"+{Pct(ModConfig.SkillCrouchSpeedPerLevel.Value)} speed while crouched per level (+{Pct(ModConfig.SkillCrouchSpeedPerLevel.Value * C.Find(id).MaxLevel)} at max).";
            case C.SoulWard: return $"Exploding monster souls hurt you {Pct(SoulPerks.WardPerLevel)} less per level (-{Pct(SoulPerks.WardPerLevel * C.Find(id).MaxLevel)} at max).";
            case C.SoulKeeper: return $"A monster soul you are holding loses {Pct(SoulPerks.KeeperPerLevel)} less value when it is hit, per level (-{Pct(SoulPerks.KeeperPerLevel * C.Find(id).MaxLevel)} at max).";

            case C.InventorySlot: return "+1 inventory slot per level (keys 4-7).";
            case C.Autoscan: return "Finds nearby valuables for the whole team; reaches farther per level.";
            case C.HeadRecharge: return "While you watch a teammate, your death head's battery refills 50% faster per level (+150% at level 3).";
            case C.HeadLeap: return "A charged death head jump carries 20% further per level, twice as far at level 5.";
            case C.HeadBattery: return "The game's Death Head Battery upgrade: your death head's battery lasts longer.";
            case C.TumbleWings: return "The game's Tumble Wings upgrade: fly longer after a tumble launch.";
            case C.FieldMedic: return $"Health packs you use heal {Pct(ModConfig.SkillMedicPerLevel.Value)} more per level; teammates within {ModConfig.SkillMedicRadius.Value:0} m get {Pct(ModConfig.SkillMedicPerLevel.Value)} of the pack per level.";
            case C.BatterySaver: return $"Battery items that aren't guns or drones drain {Pct(ModConfig.SkillBatterySaverPerLevel.Value)} slower per level while you use them.";
            case C.LuckyFinder: return $"Skill boxes give points more often, and battery crystals give you +{ModConfig.SkillLuckyCrystalBonus.Value:0}% more charge per level.";

            case C.Marksman: return $"+{Pct(root)} damage with every gun.";
            case C.HandgunDamage: return Weapon(id, "Handgun");
            case C.RevolverDamage: return Weapon(id, "Revolver");
            case C.ShotgunDamage: return Weapon(id, "Shotgun");
            case C.CartCannonDamage: return Weapon(id, "C.A.R.T. Cannon") + " Counts for whoever operates it.";
            case C.CartLaserDamage: return Weapon(id, "C.A.R.T. Laser") + " Counts for whoever operates it.";
            case C.StaffDamage: return Weapon(id, "Wizard Staff");
            case C.CrossbowDamage: return Weapon(id, "Poison Crossbow bolt");

            case C.StatusLab: return $"Statuses from your weapons last {Sec(ModConfig.SkillStatusRootSeconds.Value)} longer (8 s max); poison runs that much faster.";
            case C.TranqTime: return "Tranquilizer stuns last longer per level, up to 8 s.";
            case C.NetTime: return $"Nets hold longer per level: {Sec(ModConfig.NetSeconds.Value)} up to 8 s.";
            case C.FreezeTime: return $"Freezes last longer per level: {Sec(ModConfig.IceFreezeSeconds.Value)} up to 8 s.";
            case C.CartHandling: return $"Push the C.A.R.T. {Pct(ContractPerks.CartPerLevel)} harder per level: full carts climb and turn easier.";
            case C.GentleHands: return $"Loot you carry loses {Pct(ContractPerks.LootPerLevel)} less value per level when it breaks (up to {Pct(ContractPerks.LootPerLevel * 3)}).";
            case C.CalmPresence: return "Monsters chasing you give up sooner once they lose sight of you; more per level.";
            case C.SoftSteps: return $"Your footsteps, landings, voice and the loot you handle alert monsters from {Pct(ContractPerks.NoisePerLevel)} closer per level.";
            case C.HeavyHitter: return $"Your melee hits stun monsters {Sec(ModConfig.SkillHeavyHitterPerLevel.Value)} longer per level (8 s max).";
            case C.PoisonRush: return $"Poison deals the same damage faster per level: {Sec(ModConfig.PoisonSeconds.Value)} down to 8 s.";
        }
        return "";
    }

    private static string InstinctRu(string trigger, string buff) => $"Когда {trigger}: {buff} на 3/4/5 с (по уровню). Перезарядка 60/45/30 с.";

    private static string DescribeRu(string id)
    {
        float root = SkillEffects.RootBonus, weaponMax = Mathf.Max(0f, ModConfig.SkillWeaponDamageMax.Value);
        string Weapon(string node, string weapon) => $"+{Pct(weaponMax / C.Find(node).MaxLevel)} к урону: {weapon}, за уровень (+{Pct(weaponMax)} на максимуме).";
        switch (id)
        {
            case C.MapUplink: return "Все напарники видны на карте вместе с никами, даже если их никто не видит: белая фигурка, пока жив, красная мёртвая голова, когда погиб. Карта также считает живых и мёртвых.";
            case C.ActiveMonsters: return "Карта показывает, сколько монстров сейчас активно.";
            case C.MapLandmarks: return "Святилище испытаний и все ещё не взятые кристаллы батарей видны на карте, каждый своей иконкой: золотое святилище и зелёные кристаллы.";
            case C.BetterMap: return "С поднятой картой нажмите M, чтобы открыть большую карту (как у наблюдателя): стрелки двигают её, +/- меняют масштаб. Повторное M или опущенная карта закрывают её.";
            case C.RespawnTimers: return "Карта показывает список монстров, ждущих возрождения, и сколько осталось до возвращения каждого.";
            case C.MonsterDots: return "Монстры видны на карте, каждый своей иконкой.";
            case C.QuotaHeading: return "Карта показывает, в какой стороне квота и как до неё далеко.";
            case C.ValuableTracker: return "Карта считает найденные ценности и собранную добычу.";
            case C.MapPings: return "Пинги видны на карте, в том числе пинги напарников.";
            case C.ZoomOne: return "Отдаляйте карту клавишами масштаба.";
            case C.ZoomTwo: return "Карта отдаляется ещё сильнее.";
            case C.CosmeticTracker: return "Коробки с косметикой видны на карте.";

            case C.Ping: return "Отмечайте игроков, монстров, добычу и места; команда видит пинги сквозь стены.";
            case C.TargetInfo: return "Пинг называет цель: цену, редкость, имя монстра или предмета.";
            case C.PingRange: return "Под каждым пингом на экране видно расстояние до него и высота над полом, если он на полке или уступе.";
            case C.PingPath: return "Светящаяся дорожка на полу ведёт по проходимому пути к каждому пингу (к трём последним).";
            case C.PingCompass: return "У пингов вне экрана или за спиной на краю экрана появляется стрелка, указывающая направление.";
            case C.EnemyHp: return "У отмеченных монстров видно здоровье.";
            case C.LootCondition: return "У отмеченной добычи видно, сколько стоимости от неё осталось.";
            case C.Weight: return "У отмеченных предметов виден вес.";
            case C.Threat: return "У отмеченных монстров виден уровень опасности.";

            case C.FlashTuning: return $"Фонарь светит на {Pct(ModConfig.SkillFlashRange.Value)} дальше.";
            case C.BeamWidth: return $"Луч на {Pct(ModConfig.SkillFlashWidthPerLevel.Value)} шире за уровень.";
            case C.Brightness: return $"Луч на {Pct(ModConfig.SkillFlashBrightnessPerLevel.Value)} ярче за уровень.";
            case C.BeamColour: return "Выберите цвет луча, его видит и команда. Нажмите ещё раз, чтобы сменить.";

            case C.Brawler: return $"+{Pct(root)} к урону в ближнем бою, гранатами и кувырком.";
            case C.GrenadeDamage: return $"+{Pct(PerLevel(ModConfig.GrenadeDamagePercentPerLevel.Value, ModConfig.GrenadeDamageMaxLevel.Value, id))} к урону гранат за уровень.";
            case C.Vampirism:
            {
                var node = C.Find(id);
                float max = ModConfig.SkillVampireMax.Value;
                return $"Удары в ближнем бою лечат вас на {(max * 100f / node.MaxLevel).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture)}% нанесённого урона за уровень ({Pct(max)} на максимуме).";
            }
            case C.Adrenaline: return InstinctRu("вас ранит монстр", "+30% к скорости");
            case C.IronSkin: return InstinctRu("здоровье падает ниже 35%", "на 40% меньше получаемого урона");
            case C.HotBarrel: return InstinctRu("вы убиваете монстра из огнестрельного оружия", "+30% к урону огнестрельного оружия");
            case C.Overheat: return InstinctRu("вы попадаете по монстру лазером", "+30% к урону лазера");
            case C.BattleTrance: return InstinctRu("вы наносите 3 удара в ближнем бою за 4 с", "+30% к урону в ближнем бою");
            case C.MendingSurge: return "Когда вы убиваете монстра: восстанавливаете 5/10/15% максимального здоровья за 5 с (по уровню). Перезарядка 60/45/30 с.";
            case C.MeleeDamage: return $"+{Pct(PerLevel(ModConfig.MeleeDamagePercentPerLevel.Value, ModConfig.MeleeDamageMaxLevel.Value, id))} к урону в ближнем бою за уровень.";
            case C.TumbleDamage:
            {
                float max = Mathf.Pow(1f + Mathf.Max(0f, ModConfig.TumbleDamagePerLevel.Value), ModConfig.TumbleMaxLevel.Value) - 1f;
                return $"Кувырок бьёт сильнее (+{Pct(max)} на максимуме) и меньше ранит вас самих.";
            }
            case C.Gunsmith: return "Каждое оружие получает заряды ещё одного уровня улучшения.";
            case C.BulletShots: case C.LaserShots: case C.TranqShots:
            {
                string kind = id == C.BulletShots ? "огнестрельного оружия" : id == C.LaserShots ? "лазерного оружия" : "транквилизаторов";
                float shots = PerLevel(ModConfig.GunExtraBulletsPerLevel.Value, ModConfig.GunExtraBulletsMaxLevel.Value, id);
                return $"Больше выстрелов на заряд у {kind}: пистолету +{shots:0.#} за уровень, другому оружию по его боезапасу.";
            }
            case C.Technician: return "+10% к заряду батарей дронов.";
            case C.DroneBattery: return $"+{Pct(PerLevel(ModConfig.DroneBatteryPercentPerLevel.Value, Upgrades.BatteryLifeUpgrade.EffectiveMaxLevel(true), id))} к заряду батарей дронов за уровень.";
            case C.Overcharge: return "Луч захвата дольше держит монстров до перегрузки и быстрее остывает.";

            case C.Survivor: return $"+{ModConfig.SkillSurvivorStamina.Value:0} к максимальной выносливости.";
            case C.StaminaEfficiency:
            {
                float keep = Mathf.Pow(1f - Mathf.Clamp(ModConfig.StaminaPercentPerLevel.Value, 0f, .5f), ModConfig.StaminaMaxLevel.Value);
                return $"Бег тратит меньше выносливости (-{Pct(1f - keep)} на максимуме).";
            }
            case C.SecondWind: return $"Выносливость восстанавливается на {Pct(ModConfig.SkillSecondWindPerLevel.Value)} быстрее за уровень.";
            case C.Armor: return $"-{Pct(PerLevel(ModConfig.ArmorPercentPerLevel.Value, ModConfig.ArmorMaxLevel.Value, id))} получаемого урона за уровень (до -{Pct(Upgrades.SurvivalUpgrades.ArmorCap)}).";
            case C.HpRegen: return $"Здоровье понемногу восстанавливается, до {Pct(ModConfig.RegenMaxHpFraction.Value)} от максимума; с каждым уровнем быстрее.";

            case C.Desperation: return $"-{Pct(ModConfig.SkillDesperationPerLevel.Value)} получаемого урона за уровень, пока здоровья меньше {Pct(ModConfig.SkillDesperationThreshold.Value)}.";
            case C.HuntedWarning: return "Пока за вами гонится монстр, на экране горит надпись ПОГОНЯ.";
            case C.CrouchSpeed: return $"+{Pct(ModConfig.SkillCrouchSpeedPerLevel.Value)} к скорости в приседе за уровень (+{Pct(ModConfig.SkillCrouchSpeedPerLevel.Value * C.Find(id).MaxLevel)} на максимуме).";
            case C.SoulWard: return $"Взрывы душ монстров ранят вас на {Pct(SoulPerks.WardPerLevel)} меньше за уровень (-{Pct(SoulPerks.WardPerLevel * C.Find(id).MaxLevel)} на максимуме).";
            case C.SoulKeeper: return $"Душа монстра в ваших руках теряет при ударах на {Pct(SoulPerks.KeeperPerLevel)} меньше ценности за уровень (-{Pct(SoulPerks.KeeperPerLevel * C.Find(id).MaxLevel)} на максимуме).";

            case C.InventorySlot: return "+1 ячейка инвентаря за уровень (клавиши 4-7).";
            case C.Autoscan: return "Находит ценности рядом для всей команды; с каждым уровнем дальше.";
            case C.HeadRecharge: return "Пока вы смотрите за напарником, батарея мёртвой головы заряжается на 50% быстрее за уровень (+150% на 3 уровне).";
            case C.HeadLeap: return "Заряженный прыжок мёртвой головы улетает на 20% дальше за уровень, вдвое дальше на 5 уровне.";
            case C.HeadBattery: return "Игровое улучшение «Батарея мёртвой головы»: заряда головы хватает дольше.";
            case C.TumbleWings: return "Игровое улучшение «Крылья кувырка»: после броска кувырком вы летите дольше.";
            case C.FieldMedic: return $"Ваши аптечки лечат на {Pct(ModConfig.SkillMedicPerLevel.Value)} больше за уровень; напарники в {ModConfig.SkillMedicRadius.Value:0} м получают {Pct(ModConfig.SkillMedicPerLevel.Value)} лечения аптечки за уровень.";
            case C.BatterySaver: return $"Предметы на батарейках (кроме оружия и дронов) разряжаются на {Pct(ModConfig.SkillBatterySaverPerLevel.Value)} медленнее за уровень, пока вы ими пользуетесь.";
            case C.LuckyFinder: return $"Коробки навыков чаще дают очки, а кристаллы батарей дают на {ModConfig.SkillLuckyCrystalBonus.Value:0}% больше заряда за уровень.";

            case C.Marksman: return $"+{Pct(root)} к урону любого оружия.";
            case C.HandgunDamage: return Weapon(id, "пистолет");
            case C.RevolverDamage: return Weapon(id, "револьвер");
            case C.ShotgunDamage: return Weapon(id, "дробовик");
            case C.CartCannonDamage: return Weapon(id, "пушка тележки") + " Считается для того, кто ей управляет.";
            case C.CartLaserDamage: return Weapon(id, "лазер тележки") + " Считается для того, кто им управляет.";
            case C.StaffDamage: return Weapon(id, "посох волшебника");
            case C.CrossbowDamage: return Weapon(id, "болт ядовитого арбалета");

            case C.StatusLab: return $"Эффекты вашего оружия длятся на {SecRu(ModConfig.SkillStatusRootSeconds.Value)} дольше (до 8 с); яд действует настолько же быстрее.";
            case C.TranqTime: return "Оглушение транквилизатором длится дольше с каждым уровнем, до 8 с.";
            case C.NetTime: return $"Сети держат дольше с каждым уровнем: от {SecRu(ModConfig.NetSeconds.Value)} до 8 с.";
            case C.FreezeTime: return $"Заморозка длится дольше с каждым уровнем: от {SecRu(ModConfig.IceFreezeSeconds.Value)} до 8 с.";
            case C.CartHandling: return $"Вы толкаете тележку на {Pct(ContractPerks.CartPerLevel)} сильнее за уровень: гружёная тележка легче едет в гору и поворачивает.";
            case C.GentleHands: return $"Добыча в ваших руках теряет на {Pct(ContractPerks.LootPerLevel)} меньше стоимости при ударах за уровень (до {Pct(ContractPerks.LootPerLevel * 3)}).";
            case C.CalmPresence: return "Монстры, потерявшие вас из виду, быстрее прекращают погоню; с каждым уровнем сильнее.";
            case C.SoftSteps: return $"Ваши шаги, приземления, голос и добыча в руках слышны монстрам с расстояния на {Pct(ContractPerks.NoisePerLevel)} меньше за уровень.";
            case C.HeavyHitter: return $"Ваши удары в ближнем бою оглушают монстров на {SecRu(ModConfig.SkillHeavyHitterPerLevel.Value)} дольше за уровень (до 8 с).";
            case C.PoisonRush: return $"Яд наносит тот же урон быстрее с каждым уровнем: от {SecRu(ModConfig.PoisonSeconds.Value)} до 8 с.";
        }
        return "";
    }
}
