using System;

namespace IncreaseHardMod.Courier;

/// <summary>Courier numbers and the pure rules behind them (no Unity types, so tests can check them).</summary>
public static class CourierRules
{
    // Movement (metres, seconds)
    public const float WalkSpeed = 3.4f, SpeedPerLevel = .15f, FlightBonus = .2f;
    public const float DashFactor = 1.8f, DashSeconds = 6f, DashCooldown = 30f;
    public const float FollowDistance = 1.4f, CatchUpDistance = 28f;
    // Carrying
    public const float BaseLoad = 3f, LoadPerLevel = 2.5f;
    // Health
    public const float MaxHealth = 100f, HealthPerLevel = .2f, ArmorPerLevel = .1f, RespawnSeconds = 45f, RecoveryPerLevel = 10f;
    // Monsters: they notice a courier nearby, come to look and hit it when close. Low Profile calms them.
    public const float NoticeRadius = 14f, NoticeChance = .35f, NoticeEvery = 18f, HitRange = 1.7f, HitDamage = 30f, HitCooldown = 1.6f,
        AggressionPerLevel = .2f;
    // Backpack slots
    public const int BaseSlots = 1, MaxSlots = 3;
    // Gacha
    public const float CoinChance = .2f;
    // Rare find: chance per level, at most one find per level
    public const float BaseFindChance = .02f, LuckPerLevel = .02f;
    // Charging: give up after this long at the station
    public const float ChargeTimeout = 45f;

    public static float Speed(int speedLevel, bool dashing, bool flying) =>
        WalkSpeed * (1f + SpeedPerLevel * Math.Max(0, speedLevel)) * (dashing ? DashFactor : 1f) * (flying ? 1f + FlightBonus : 1f);

    public static float MaxLoad(int loadLevel) => BaseLoad + LoadPerLevel * Math.Max(0, loadLevel);

    public static float DamageTaken(float damage, int armorLevel) => Math.Max(0f, damage) * Math.Max(.6f, 1f - ArmorPerLevel * Math.Max(0, armorLevel));

    /// <summary>Courier health: +20% per Courier Health level, at most double.</summary>
    public static float Health(int healthLevel) => MaxHealth * Math.Min(2f, 1f + HealthPerLevel * Math.Max(0, healthLevel));

    /// <summary>How readily monsters go for the courier (1 = normal); Low Profile takes 20% off per level.</summary>
    public static float Aggression(int lowProfileLevel) => Math.Max(.2f, 1f - AggressionPerLevel * Math.Max(0, lowProfileLevel));

    public static int Slots(int backpackLevel) => Math.Min(MaxSlots, BaseSlots + Math.Max(0, backpackLevel));

    // Gacha: penguin and chicken common, cat and beaver uncommon, owl rare.
    public static readonly int[] GachaWeights = { 25, 25, 20, 10, 20, 20, 20, 20, 10, 10, 10, 5 };

    /// <summary>Which animal a pull gives for a roll in [0, 1).</summary>
    public static int Pull(double roll)
    {
        int total = 0; foreach (int w in GachaWeights) total += w;
        double r = Math.Max(0, Math.Min(.999999, roll)) * total;
        for (int i = 0; i < GachaWeights.Length; i++) { r -= GachaWeights[i]; if (r < 0) return i; }
        return GachaWeights.Length - 1;
    }

    public static float Respawn(int recoveryLevel) => Math.Max(10f, RespawnSeconds - RecoveryPerLevel * Math.Max(0, recoveryLevel));

    public static float FindChance(int luckLevel) => Math.Min(1f, BaseFindChance + LuckPerLevel * Math.Max(0, luckLevel));

    // ------------------------------------------------------------------ looks

    public const int Animals = 12, Swatches = 12, DefaultSwatch = 31;

    /// <summary>Animal (0-11) and three colour slots (swatch index, or DefaultSwatch for the animal's own colour) in one int.</summary>
    public static int PackLook(int animal, int main, int second, int accent) =>
        (Clamp(animal, 0, Animals - 1)) | (Slot(main) << 4) | (Slot(second) << 9) | (Slot(accent) << 14);

    public static void UnpackLook(int look, out int animal, out int main, out int second, out int accent)
    {
        animal = Clamp(look & 15, 0, Animals - 1);
        main = Slot((look >> 4) & 31); second = Slot((look >> 9) & 31); accent = Slot((look >> 14) & 31);
    }

    /// <summary>Steps a colour slot through the swatches and back to the animal's own colour.</summary>
    public static int NextSwatch(int slot, int step)
    {
        int count = Swatches + 1; // swatches plus "default"
        int index = slot == DefaultSwatch ? Swatches : Clamp(slot, 0, Swatches - 1);
        index = ((index + step) % count + count) % count;
        return index == Swatches ? DefaultSwatch : index;
    }

    private static int Slot(int value) => value >= 0 && value < Swatches ? value : DefaultSwatch;
    private static int Clamp(int v, int lo, int hi) => v < lo ? lo : v > hi ? hi : v;
}
