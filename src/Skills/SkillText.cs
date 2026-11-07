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

    /// <summary>A node's display name: Russian for trade-offs when the game is Russian.</summary>
    internal static string Name(SkillNode node) =>
        node.Tier > 0 && Russian && !string.IsNullOrEmpty(node.NameRu) ? node.NameRu : node.Name;

    // The instinct line: trigger, buff, then the level ladder shared by all of them.
    private static string Instinct(string trigger, string buff) => $"When {trigger.Substring(0, 1).ToLowerInvariant()}{trigger.Substring(1)}: {buff} for 3/4/5 s (by level). Cooldown 60/45/30 s.";

    internal static string Describe(string id)
    {
        float root = SkillEffects.RootBonus, weaponMax = Mathf.Max(0f, ModConfig.SkillWeaponDamageMax.Value);
        string Weapon(string node, string weapon) => $"+{Pct(weaponMax / C.Find(node).MaxLevel)} {weapon} damage per level (+{Pct(weaponMax)} at max).";
        var tradeOff = TradeOffRules.Find(id);
        if (tradeOff != null)
            return Russian && tradeOff.UpRu != null
                ? $"{tradeOff.UpRu}.  <color=#FF8C8C>Минус: {tradeOff.DownRu}.</color>"
                : $"{tradeOff.Up}.  <color=#FF8C8C>Downside: {tradeOff.Down}.</color>";
        switch (id)
        {
            case C.MapUplink: return "The map shows how many players are alive.";
            case C.DeadPlayers: return "The map shows how many players are dead.";
            case C.ActiveMonsters: return "The map shows how many monsters are active.";
            case C.RespawnTimers: return "The map shows how many monsters are respawning.";
            case C.MonsterDots: return "Monsters appear as dots on the map.";
            case C.QuotaHeading: return "The map shows which way the quota is.";
            case C.ValuableTracker: return "The map counts valuables found and your haul.";
            case C.MapPings: return "Team pings appear on the map, including your teammates'.";
            case C.ZoomOne: return "Zoom the map out with the zoom keys.";
            case C.ZoomTwo: return "Zoom the map out even further.";
            case C.CosmeticTracker: return "Cosmetic boxes appear on the map.";

            case C.Ping: return "Ping players, monsters, loot and places; the team sees pings through walls.";
            case C.TargetInfo: return "Pings name what they mark: prices, rarity, monster and item names.";
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

            case C.InventorySlot: return "+1 inventory slot per level (keys 4-7).";
            case C.Autoscan: return "Finds nearby valuables for the whole team; reaches farther per level.";
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
}
