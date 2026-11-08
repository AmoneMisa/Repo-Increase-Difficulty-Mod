using IncreaseHardMod.Hud;
using UnityEngine;

namespace IncreaseHardMod.Courier;

/// <summary>
/// The courier gacha, kept per player on their own machine (BepInEx config): gacha coins, half coins from duplicates
/// and the animals unlocked. A player has no courier until their first pull. Coins are found rarely on levels;
/// everyone gets one free starter coin. The look sent to the host is -1 while no animal is unlocked.
/// </summary>
internal static class CourierGacha
{
    internal static int Owned => ModConfig.CourierOwned.Value & ((1 << CourierRules.Animals) - 1);
    internal static bool HasCourier => Owned != 0;
    internal static bool Owns(int animal) => (Owned & (1 << animal)) != 0;
    internal static int Coins => Mathf.Max(0, ModConfig.CourierCoins.Value);
    internal static int Halves => Mathf.Max(0, ModConfig.CourierFragments.Value);

    internal static void GiveStarterCoin()
    {
        if (ModConfig.CourierStarterCoinGiven.Value) return;
        ModConfig.CourierStarterCoinGiven.Value = true;
        ModConfig.CourierCoins.Value = Coins + 1;
    }

    internal static void AddCoin()
    {
        ModConfig.CourierCoins.Value = Coins + 1;
        HudNotify.Toast(CourierManager.L($"GACHA COIN FOUND! ({Coins})  OPEN THE COURIER MENU [{ModConfig.CourierMenuKey.Value}] TO PULL",
            $"НАЙДЕНА МОНЕТА ГАЧИ! ({Coins})  ОТКРОЙТЕ МЕНЮ КУРЬЕРА [{ModConfig.CourierMenuKey.Value}]"), HudStyle.Title, 5f, "courier_coin");
        try { Visuals.ItemAudio.Cue("courier_find", .5f).Play(PlayerAvatar.instance.transform.position); } catch { }
    }

    /// <summary>Spends a coin on one pull. Returns the animal, and whether it was new.</summary>
    internal static bool TryPull(out int animal, out bool isNew)
    {
        animal = -1; isNew = false;
        if (Coins <= 0) return false;
        ModConfig.CourierCoins.Value = Coins - 1;
        animal = CourierRules.Pull(Random.value);
        isNew = !Owns(animal);
        if (isNew)
        {
            ModConfig.CourierOwned.Value = Owned | (1 << animal);
            // A first or new animal becomes the active courier, keeping the chosen colours.
            CourierRules.UnpackLook(CourierLook.Local, out _, out int a, out int b, out int c);
            CourierLook.Local = CourierRules.PackLook(animal, a, b, c);
        }
        else
        {
            int halves = Halves + 1;
            if (halves >= 2) { halves -= 2; ModConfig.CourierCoins.Value = Coins + 1; }
            ModConfig.CourierFragments.Value = halves;
        }
        return true;
    }

    /// <summary>The look to send to the host: -1 with no courier; never an animal that isn't unlocked.</summary>
    internal static int LookToSend()
    {
        if (!HasCourier) return -1;
        int look = CourierLook.Local;
        CourierRules.UnpackLook(look, out int animal, out int a, out int b, out int c);
        if (Owns(animal)) return look;
        for (int i = 0; i < CourierRules.Animals; i++)
            if (Owns(i)) { CourierLook.Local = CourierRules.PackLook(i, a, b, c); return CourierLook.Local; }
        return -1;
    }
}
