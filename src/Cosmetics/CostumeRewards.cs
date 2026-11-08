using System.Collections.Generic;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

namespace IncreaseHardMod.Cosmetics;

/// <summary>
/// The "costume piece" reward (shrines, mystery boxes, special contracts and high-rank quests): every player unlocks
/// one of the mod's own costume parts they don't own yet, straight into their cosmetics menu. Unlocks are per player
/// and saved by the game (MetaManager), so the host only says "everyone gets a piece" (event 164) and each client
/// picks its own. A player who already owns every part gets a Rare cosmetic token instead.
/// </summary>
internal static class CostumeRewards
{
    internal const byte EventCode = 164;
    private const string IdPrefix = "modded:IncreaseDifficultyMod.";

    /// <summary>Host: give every player a costume piece. <paramref name="source"/> is for the log only.</summary>
    internal static void GrantToAll(string source)
    {
        if (!SemiFunc.IsMasterClientOrSingleplayer()) return;
        Plugin.Log.LogInfo($"[Costumes] Costume piece for everyone ({source}).");
        if (SemiFunc.IsMultiplayer() && PhotonNetwork.InRoom)
            PhotonNetwork.RaiseEvent(EventCode, null, new RaiseEventOptions { Receivers = ReceiverGroup.Others }, SendOptions.SendReliable);
        UnlockLocal();
    }

    /// <summary>Client: the host granted a costume piece (ModNetwork checks the sender is the host).</summary>
    internal static void HandleEvent(EventData e) => UnlockLocal();

    /// <summary>The mod's costume parts this player has not unlocked yet.</summary>
    internal static List<CosmeticAsset> Locked()
    {
        var list = new List<CosmeticAsset>();
        var meta = MetaManager.instance;
        if (meta == null || meta.cosmeticAssets == null) return list;
        for (int i = 0; i < meta.cosmeticAssets.Count; i++)
        {
            var asset = meta.cosmeticAssets[i];
            if (asset == null || asset.assetId == null || !asset.assetId.StartsWith(IdPrefix, System.StringComparison.Ordinal)) continue;
            if (!meta.cosmeticUnlocks.Contains(i)) list.Add(asset);
        }
        return list;
    }

    private static void UnlockLocal()
    {
        var meta = MetaManager.instance;
        if (meta == null) return;
        bool ru = Localization.GameLanguage.Russian;
        var locked = Locked();
        if (locked.Count == 0)
        {
            meta.CosmeticTokenAdd(SemiFunc.Rarity.Rare);
            SemiFunc.UIFocusText(ru ? "ВСЕ ЧАСТИ КОСТЮМОВ УЖЕ ЕСТЬ: +1 РЕДКИЙ ЖЕТОН КОСМЕТИКИ" : "EVERY COSTUME PIECE OWNED: +1 RARE COSMETIC TOKEN",
                new Color(.85f, .55f, 1f), Color.white, 4f);
            return;
        }
        // Rarer parts are rarer here too: weight Uncommon 4, Rare 2, Ultra Rare 1.
        float total = 0f;
        foreach (var a in locked) total += Weight(a.rarity);
        float roll = Random.value * total;
        var pick = locked[locked.Count - 1];
        foreach (var a in locked) { roll -= Weight(a.rarity); if (roll <= 0f) { pick = a; break; } }
        if (!meta.CosmeticUnlock(pick)) return;
        Plugin.Log.LogInfo($"[Costumes] Unlocked {pick.assetName} ({pick.rarity}).");
        SemiFunc.UIFocusText((ru ? "ЧАСТЬ КОСТЮМА: " : "COSTUME PIECE: ") + pick.assetName.ToUpperInvariant() +
                             (ru ? " (В МЕНЮ КОСМЕТИКИ)" : " (IN YOUR COSMETICS)"), new Color(.85f, .55f, 1f), Color.white, 4.5f);
    }

    private static float Weight(SemiFunc.Rarity rarity) => rarity switch
    {
        SemiFunc.Rarity.UltraRare => 1f,
        SemiFunc.Rarity.Rare => 2f,
        _ => 4f,
    };
}
