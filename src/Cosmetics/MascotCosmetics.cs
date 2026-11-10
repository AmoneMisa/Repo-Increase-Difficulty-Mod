using System.Collections.Generic;

namespace IncreaseHardMod.Cosmetics;

/// <summary>Stable asset keys: never reorder or rename existing unlock IDs.</summary>
internal static class MascotCosmetics
{
    internal static IEnumerable<WingCosmetics.Definition> Definitions
    {
        get
        {
            foreach (var set in new[] {
                ("musketeer", "Musketeer Cat"), ("bombbird", "Bomb Bird"),
                ("talkingdog", "Talking Dog"), ("rabbid", "Rabbid Mayhem"),
                ("jackal", "Jackal Warden"), ("panda", "Panda Dojo"),
                ("penguin", "Penguin Captain"), ("kitty", "Hello Kitty"), ("chipmunk", "Chipmunk Hoodie"),
                ("assassin", "Florentine Assassin"), ("pirate", "Jack Sparrow"),
                ("ogre", "Swamp Ogre"), ("sunpriestess", "Chel") })
            {
                yield return Part(set, "hood", "Hood", SemiFunc.CosmeticType.Hat, "Hat");
                yield return Part(set, "suit", "Suit", SemiFunc.CosmeticType.BodyTop, "Body Top");
                yield return Part(set, "belt", "Belt and Tail", SemiFunc.CosmeticType.BodyBottom, "Body Bottom");
                yield return Part(set, "glove_r", "Glove Right", SemiFunc.CosmeticType.ArmRight, "Arm Right");
                yield return Part(set, "glove_l", "Glove Left", SemiFunc.CosmeticType.ArmLeft, "Arm Left");
                yield return Part(set, "boot_r", "Boot Right", SemiFunc.CosmeticType.LegRight, "Leg Right");
                yield return Part(set, "boot_l", "Boot Left", SemiFunc.CosmeticType.LegLeft, "Leg Left");
                // Character faces: fur or skin round the native eyes with the muzzle, beak, teeth or nose below them
                // (the game parents Face Top to the head top), and a matching jaw (Head Bottom).
                if (!FaceSets.Contains(set.Item1)) continue;
                yield return Part(set, "face", "Face", SemiFunc.CosmeticType.FaceTop, "Face Top");
                yield return Part(set, "neck", "Jaw", SemiFunc.CosmeticType.HeadBottom, "Head Bottom");
            }
            yield return new WingCosmetics.Definition("sunpriestess_lips", "Chel Lips", SemiFunc.CosmeticType.FaceBottom, "Face Bottom", SemiFunc.Rarity.Uncommon, false, "mascots");
        }
    }
    private static readonly HashSet<string> FaceSets = new() { "musketeer", "bombbird", "talkingdog", "rabbid", "ogre", "sunpriestess" };

    /// <summary>Parts made only of fixed character colours (skin, hair, jewellery): the slot colour does not apply.</summary>
    private static readonly HashSet<string> Fixed = new()
    {
        "sunpriestess_hood", "sunpriestess_glove_r", "sunpriestess_glove_l", "sunpriestess_boot_r", "sunpriestess_boot_l",
        "sunpriestess_face", "sunpriestess_neck",
    };

    private static WingCosmetics.Definition Part((string Key, string Name) set, string key, string title,
        SemiFunc.CosmeticType type, string slot) => new(set.Key + "_" + key, set.Name + " " + title, type, slot,
            type == SemiFunc.CosmeticType.ArmLeft || type == SemiFunc.CosmeticType.ArmRight || type == SemiFunc.CosmeticType.HeadBottom ? SemiFunc.Rarity.Uncommon : SemiFunc.Rarity.Rare,
            !Fixed.Contains(set.Key + "_" + key), "mascots");
}
