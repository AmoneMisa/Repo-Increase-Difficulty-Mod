using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;

namespace IncreaseHardMod.Upgrades;

/// <summary>
/// Team shop upgrades: bought in the shop like the truck upgrades and shared the same way (the team's highest level
/// counts for everyone, kept in the run stats by the host; see <see cref="TruckUpgrades"/>).
///   • Haggling (3 levels): the shopkeeper robot allows one more discount attempt per shop visit per level (3 -> 6).
///   • Crystal Stock (3 levels): the shop stocks 6 / 7 / 8 power crystals at once, never more than the charging
///     station still needs for a full charge (10 crystals).
///   • Spare Gun / Spare Melee / Spare Drone (1 level each): every limited item of that kind can be bought once more
///     (its shop limit goes up by one). Guns include launchers.
///   • Upgrade Shelf (4 levels): two more upgrades stocked per shop visit per level (up to +8).
/// </summary>
internal static class ShopUpgrades
{
    public const string HaggleId = "IHM_TeamHaggle", CrystalId = "IHM_TeamCrystals", GunId = "IHM_TeamSpareGun",
        MeleeId = "IHM_TeamSpareMelee", DroneId = "IHM_TeamSpareDrone", ShelfId = "IHM_TeamUpgradeShelf";
    internal static readonly string[] Ids = { HaggleId, CrystalId, GunId, MeleeId, DroneId, ShelfId };

    public const int VanillaDiscountAttempts = 3, HaggleMaxLevel = 3, CrystalMaxLevel = 3, ShelfMaxLevel = 4;
    public const int BaseCrystalStock = 5, UpgradesPerShelfLevel = 2, StationCrystals = 10;

    internal static void Register()
    {
        Define(HaggleId, "Haggling",
            $"Team upgrade: the shopkeeper robot will try to lower a price {VanillaDiscountAttempts + 1} / {VanillaDiscountAttempts + 2} / {VanillaDiscountAttempts + 3} times per shop visit instead of {VanillaDiscountAttempts}.",
            $"Улучшение команды: робот-продавец пробует снизить цену {VanillaDiscountAttempts + 1} / {VanillaDiscountAttempts + 2} / {VanillaDiscountAttempts + 3} раз за посещение магазина вместо {VanillaDiscountAttempts}.",
            HaggleMaxLevel, ModConfig.TeamHaggleCost.Value, "Upgrade_Team_Haggling.png");
        Define(CrystalId, "Crystal Stock",
            $"Team upgrade: the shop puts out {BaseCrystalStock + 1} / {BaseCrystalStock + 2} / {BaseCrystalStock + 3} power crystals at once instead of 4-5 - never more than the truck still needs for a full charge.",
            $"Улучшение команды: в магазине одновременно {BaseCrystalStock + 1} / {BaseCrystalStock + 2} / {BaseCrystalStock + 3} кристаллов энергии вместо 4-5, но не больше, чем машине не хватает до полного заряда.",
            CrystalMaxLevel, ModConfig.TeamCrystalCost.Value, "Upgrade_Team_Crystal-Stock.png");
        Define(GunId, "Spare Gun",
            "Team upgrade: every gun and launcher with a purchase limit can be bought one more time.",
            "Улучшение команды: каждое огнестрельное оружие и пусковую установку с ограничением покупок можно купить ещё один раз.",
            1, ModConfig.TeamSpareGunCost.Value, "Upgrade_Team_Spare-Gun.png");
        Define(MeleeId, "Spare Melee",
            "Team upgrade: every melee weapon with a purchase limit can be bought one more time.",
            "Улучшение команды: каждое оружие ближнего боя с ограничением покупок можно купить ещё один раз.",
            1, ModConfig.TeamSpareMeleeCost.Value, "Upgrade_Team_Spare-Melee.png");
        Define(DroneId, "Spare Drone",
            "Team upgrade: every drone with a purchase limit can be bought one more time.",
            "Улучшение команды: каждого дрона с ограничением покупок можно купить ещё один раз.",
            1, ModConfig.TeamSpareDroneCost.Value, "Upgrade_Team_Spare-Drone.png");
        Define(ShelfId, "Upgrade Shelf",
            $"Team upgrade: an extra shelf in the shop with {UpgradesPerShelfLevel} more upgrades per level (up to +{UpgradesPerShelfLevel * ShelfMaxLevel}).",
            $"Улучшение команды: дополнительный стеллаж в магазине, на нём на {UpgradesPerShelfLevel} улучшения больше за уровень (до +{UpgradesPerShelfLevel * ShelfMaxLevel}).",
            ShelfMaxLevel, ModConfig.TeamShelfCost.Value, "Upgrade_Team_Upgrade-Shelf.png");
    }

    private static void Define(string id, string name, string description, string descriptionRu, int maxLevel, int price, string icon) =>
        PlayerUpgradeRegistry.Define(new PlayerUpgradeRegistry.Definition
        {
            Id = id, DisplayName = name, Description = description, DescriptionRu = descriptionRu, MaxLevel = maxLevel,
            BaseShopPrice = price, IconFile = icon,
        });

    private static int Level(string id) => TruckUpgrades.TeamLevel(id);

    /// <summary>Discount attempts the shopkeeper allows per shop visit.</summary>
    public static int DiscountAttempts => VanillaDiscountAttempts + Level(HaggleId);

    /// <summary>Power crystals the shop stocks at once (0 = the game's own 4-5).</summary>
    public static int CrystalStock => Level(CrystalId) > 0 ? BaseCrystalStock + Level(CrystalId) : 0;

    /// <summary>Extra upgrades stocked per shop visit.</summary>
    public static int ExtraUpgradeSlots => UpgradesPerShelfLevel * Level(ShelfId);

    /// <summary>How many more times an item of this kind may be bought.</summary>
    public static int SpareCopies(SemiFunc.itemType type) => type switch
    {
        SemiFunc.itemType.gun or SemiFunc.itemType.launcher => Level(GunId),
        SemiFunc.itemType.melee => Level(MeleeId),
        SemiFunc.itemType.drone => Level(DroneId),
        _ => 0,
    };

    /// <summary>
    /// Host, shop stocking: make sure the crystal pool holds up to <paramref name="target"/> crystals, but no more than
    /// the charging station still lacks for a full charge. Only tops up a crystal the game already offers this visit
    /// (an unaffordable or maxed-out crystal stays out).
    /// </summary>
    internal static void TopUpCrystals(List<Item> pool, int target)
    {
        var crystal = pool.FirstOrDefault(i => i != null && i.itemType == SemiFunc.itemType.power_crystal);
        if (crystal == null) return;
        int needed = Mathf.Max(0, StationCrystals - SemiFunc.StatGetItemsPurchased(crystal.name));
        int want = Mathf.Min(target, needed);
        int have = pool.Count(i => i == crystal);
        for (; have < want; have++) pool.Insert(UnityEngine.Random.Range(0, pool.Count + 1), crystal);
    }

    // ------------------------------------------------------------------ Haggling

    /// <summary>
    /// The shopkeeper's discount limit is the literal 3 in several methods (the attempts check, the deny state and the
    /// network clamp). Each "itemDiscountAttempts vs 3" and the clamp's upper bound read <see cref="DiscountAttempts"/>
    /// instead. Nothing else in these methods is touched (other 3s, such as state values, stay).
    /// </summary>
    [HarmonyPatch]
    private static class HaggleLimit
    {
        private static readonly string[] Methods = { "ItemPresentationTargetValid", "ItemNoticeStart", "TryStartItemDiscount", "ItemDenyPhysics", "UpdateStateRPC" };

        private static IEnumerable<MethodBase> TargetMethods()
        {
            foreach (string name in Methods)
            {
                var method = AccessTools.Method(typeof(ShopKeeper), name);
                if (method != null) yield return method;
                else Plugin.Log.LogWarning($"[Haggling] ShopKeeper.{name} not found; that check keeps the game's limit.");
            }
        }

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase original)
        {
            var attempts = AccessTools.Field(typeof(ShopKeeper), "itemDiscountAttempts");
            var clamp = AccessTools.Method(typeof(Mathf), nameof(Mathf.Clamp), new[] { typeof(int), typeof(int), typeof(int) });
            var limit = AccessTools.PropertyGetter(typeof(ShopUpgrades), nameof(DiscountAttempts));
            var code = instructions.ToList();
            int replaced = 0;
            for (int i = 0; i < code.Count; i++)
            {
                if (code[i].opcode != OpCodes.Ldc_I4_3) continue;
                bool afterAttempts = i > 0 && attempts != null && code[i - 1].LoadsField(attempts);
                bool clampTop = i + 1 < code.Count && clamp != null && code[i + 1].Calls(clamp);
                if (!afterAttempts && !clampTop) continue;
                code[i].opcode = OpCodes.Call; // keeps the instruction's labels
                code[i].operand = limit;
                replaced++;
            }
            if (replaced == 0) Plugin.Log.LogWarning($"[Haggling] No discount limit found in ShopKeeper.{original.Name}.");
            return code;
        }
    }

    // ------------------------------------------------------------------ Spare Gun / Melee / Drone

    /// <summary>
    /// While the shop builds its stock, every limited gun, melee weapon or drone may be stocked once more than its
    /// limit, so it can be bought one more time. The items are shared assets, so the limit is restored right after.
    /// </summary>
    [HarmonyPatch(typeof(ShopManager), nameof(ShopManager.GetAllItemsFromStatsManager))]
    private static class SpareCopiesPatch
    {
        [HarmonyPrefix]
        private static void Prefix(out List<(Item item, int limit)> __state)
        {
            __state = null;
            if (SemiFunc.IsNotMasterClient() || StatsManager.instance == null) return;
            foreach (var item in StatsManager.instance.itemDictionary.Values)
            {
                if (item == null || item.maxAmountInShop <= 0) continue; // 0 hides an item (paired drones): leave it hidden
                int extra = SpareCopies(item.itemType);
                if (extra <= 0) continue;
                __state ??= new List<(Item, int)>();
                __state.Add((item, item.maxAmountInShop));
                item.maxAmountInShop += extra;
            }
        }

        [HarmonyFinalizer]
        private static Exception Finalizer(Exception __exception, List<(Item item, int limit)> __state)
        {
            if (__state != null)
                foreach (var (item, limit) in __state)
                    if (item != null) item.maxAmountInShop = limit;
            return __exception;
        }
    }
}
