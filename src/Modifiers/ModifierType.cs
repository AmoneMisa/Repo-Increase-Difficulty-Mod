using System;

namespace IncreaseHardMod.Modifiers;

/// <summary>
/// Every "surprise" that a special level can have. This is a [Flags] enum, so a
/// single int can represent ANY combination of modifiers at once (each value is
/// a distinct bit). That lets us send the whole active set as one integer over
/// the network.
///
/// Reward vs punish is just how it feels in play; mechanically they're all just
/// modifiers. The comment on each notes the intended flavor.
/// </summary>
[Flags]
public enum ModifierType
{
    None = 0,

    // --- Loot property twists ---
    FragileLoot        = 1 << 0,  // punish: loot breaks/loses value more easily
    HeavyLoot          = 1 << 1,  // punish: loot weighs more
    LightLoot          = 1 << 2,  // reward: loot weighs less
    ProtectedLoot      = 1 << 3,  // reward: loot takes less damage
    DangerousLoot      = 1 << 4,  // punish: loot is more hazardous

    // --- Level content ---
    ExtraSmallCart     = 1 << 5,  // reward: an extra small cart spawns
    ExtraBigCart       = 1 << 6,  // reward: an extra big cart spawns
    ExtraSouls         = 1 << 7,  // reward: monsters drop more souls (param: count 4-10)
    SpreadUpgrades     = 1 << 8,  // mixed: upgrades scattered, only usable next level in truck

    // --- Player conditions ---
    NoMap              = 1 << 9,  // punish: no map until you find a map upgrade pickup
    ReducedPlayerHp    = 1 << 10, // punish: everyone starts wounded, current HP cut (param: percent 10-90)
    AutoRevive         = 1 << 11, // reward: one auto-revive per player this level
    MoneyLossOnDeath   = 1 << 12, // punish: lose a % of collected money on death (param: percent)
    LevelTimer         = 1 << 13, // mixed: timed level; kills add time, deaths remove, quota adds
    ChangeVoices       = 1 << 14, // surprise: player voices get swapped/randomized

    // --- Added batch 2 ---
    MoreGachaCubes     = 1 << 15, // reward: more gacha/cube valuables spawn
    SoulsExplodeHarder = 1 << 16, // punish: souls deal more damage when they explode
    BiggerSoulDrops    = 1 << 17, // reward: each soul drop is worth more (value per drop, not count)
    MonstersPitTanky   = 1 << 20, // punish: monsters take less pit/death-pit damage

    // --- Added batch 3: monster weight ---
    LightMonsters      = 1 << 21, // reward: all monsters weigh at most LightMonsterMaxMass (easy to shove)
    HeavyMonsters      = 1 << 22, // punish: all monsters weigh at least HeavyMonsterMinMass (hard to shove)

    // --- Added batch 4 ---
    MysteryBoxes       = 1 << 23, // reward: a few mystery boxes trade one soul each for a random prize
}
