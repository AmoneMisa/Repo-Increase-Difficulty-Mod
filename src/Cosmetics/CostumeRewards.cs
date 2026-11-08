using System.Collections.Generic;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

namespace IncreaseHardMod.Cosmetics;

/// <summary>
/// The "costume piece" reward (shrines, mystery boxes, special contracts and high-rank quests): every player unlocks
/// one of the mod's own costume parts they don't own yet, straight into their cosmetics menu, and no two players get
/// the same costume: the host sends a random seed and the player order (event 164), every client shuffles the costume
/// sets the same way from the seed, and player N takes the Nth set (moving on past sets they already own completely).
/// Inside its set a player gets a random part they don't own (rarer parts rarer). Unlocks are per player and saved by
/// the game (MetaManager), which is why each client picks its own. A player who owns every part gets a Rare token.
/// </summary>
internal static class CostumeRewards
{
    internal const byte EventCode = 164;
    private const string IdPrefix = "modded:IncreaseDifficultyMod.";

    /// <summary>Host: give every player a costume piece, each from a different costume. <paramref name="source"/> is for the log.</summary>
    internal static void GrantToAll(string source)
    {
        if (!SemiFunc.IsMasterClientOrSingleplayer()) return;
        int seed = Random.Range(1, int.MaxValue);
        var actors = new List<int>();
        if (SemiFunc.IsMultiplayer() && PhotonNetwork.InRoom)
            foreach (var p in PhotonNetwork.PlayerList) if (p != null) actors.Add(p.ActorNumber);
        actors.Sort();
        Plugin.Log.LogInfo($"[Costumes] Costume piece for everyone ({source}), seed {seed}, {actors.Count} player(s).");
        if (SemiFunc.IsMultiplayer() && PhotonNetwork.InRoom)
            PhotonNetwork.RaiseEvent(EventCode, new object[] { seed, actors.ToArray() }, new RaiseEventOptions { Receivers = ReceiverGroup.Others }, SendOptions.SendReliable);
        UnlockLocal(seed, actors.ToArray());
    }

    /// <summary>Client: the host granted a costume piece (ModNetwork checks the sender is the host).</summary>
    internal static void HandleEvent(EventData e)
    {
        if (e.CustomData is not object[] d || d.Length != 2 || d[0] is not int seed || d[1] is not int[] actors || actors.Length > 64) return;
        UnlockLocal(seed, actors);
    }

    private static string SetOf(CosmeticAsset asset)
    {
        string key = asset.assetId.Substring(IdPrefix.Length);
        int cut = key.IndexOf('_');
        return cut > 0 ? key.Substring(0, cut) : key;
    }

    /// <summary>Every mod costume part, grouped by set, in registration order (the same on every client).</summary>
    private static List<(string Set, List<int> Parts)> Sets()
    {
        var sets = new List<(string, List<int>)>();
        var meta = MetaManager.instance;
        if (meta == null || meta.cosmeticAssets == null) return sets;
        for (int i = 0; i < meta.cosmeticAssets.Count; i++)
        {
            var asset = meta.cosmeticAssets[i];
            if (asset == null || asset.assetId == null || !asset.assetId.StartsWith(IdPrefix, System.StringComparison.Ordinal)) continue;
            string set = SetOf(asset);
            int at = sets.FindIndex(s => s.Item1 == set);
            if (at < 0) { sets.Add((set, new List<int>())); at = sets.Count - 1; }
            sets[at].Item2.Add(i);
        }
        return sets;
    }

    private static void UnlockLocal(int seed, int[] actors)
    {
        var meta = MetaManager.instance;
        if (meta == null) return;
        bool ru = Localization.GameLanguage.Russian;
        var sets = Sets();
        // The same shuffle of the sets on every client; each player starts at their own place in it.
        var rng = new System.Random(seed);
        for (int i = sets.Count - 1; i > 0; i--) { int j = rng.Next(i + 1); (sets[i], sets[j]) = (sets[j], sets[i]); }
        int me = PhotonNetwork.LocalPlayer != null ? System.Array.IndexOf(actors, PhotonNetwork.LocalPlayer.ActorNumber) : -1;
        int start = Mathf.Max(0, me);
        var own = new System.Random(seed ^ (start * 7919 + 17));
        for (int step = 0; step < sets.Count; step++)
        {
            var (set, parts) = sets[(start + step) % sets.Count];
            var locked = parts.FindAll(i => !meta.cosmeticUnlocks.Contains(i));
            if (locked.Count == 0) continue;
            // Rarer parts are rarer here too: weight Uncommon 4, Rare 2, Ultra Rare 1.
            float total = 0f;
            foreach (int i in locked) total += Weight(meta.cosmeticAssets[i].rarity);
            float roll = (float)own.NextDouble() * total;
            int pickIndex = locked[locked.Count - 1];
            foreach (int i in locked) { roll -= Weight(meta.cosmeticAssets[i].rarity); if (roll <= 0f) { pickIndex = i; break; } }
            var pick = meta.cosmeticAssets[pickIndex];
            if (!meta.CosmeticUnlock(pick)) return;
            Plugin.Log.LogInfo($"[Costumes] Unlocked {pick.assetName} ({pick.rarity}) from the {set} set.");
            SemiFunc.UIFocusText((ru ? "ЧАСТЬ КОСТЮМА: " : "COSTUME PIECE: ") + pick.assetName.ToUpperInvariant() +
                                 (ru ? " (В МЕНЮ КОСМЕТИКИ)" : " (IN YOUR COSMETICS)"), new Color(.85f, .55f, 1f), Color.white, 4.5f);
            return;
        }
        meta.CosmeticTokenAdd(SemiFunc.Rarity.Rare);
        SemiFunc.UIFocusText(ru ? "ВСЕ ЧАСТИ КОСТЮМОВ УЖЕ ЕСТЬ: +1 РЕДКИЙ ЖЕТОН КОСМЕТИКИ" : "EVERY COSTUME PIECE OWNED: +1 RARE COSMETIC TOKEN",
            new Color(.85f, .55f, 1f), Color.white, 4f);
    }

    private static float Weight(SemiFunc.Rarity rarity) => rarity switch
    {
        SemiFunc.Rarity.UltraRare => 1f,
        SemiFunc.Rarity.Rare => 2f,
        _ => 4f,
    };
}
