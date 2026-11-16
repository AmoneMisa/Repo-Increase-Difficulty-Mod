using System;
using System.Collections.Generic;
using IncreaseHardMod.Visuals;
using UnityEngine;

namespace IncreaseHardMod.Valuables;

/// <summary>
/// Valuables found only on Fable Realm: low-poly models built in code, like the ritual loot.
/// The large ones come first (a wide coffin, a big wolf and cauldron, a tall portrait, a very tall broom),
/// then the medium and small ones. Sizes are metres; X is the long side of a wide piece, -Z is its front.
/// The spawn volume of each piece is picked from its measured bounds (InteractiveValuables.VolumeFor).
/// </summary>
internal static class FableLoot
{
    internal const string Prefix = "fable_";
    internal const string Coffin = "fable_glass_coffin", Wolf = "fable_wolf", Portrait = "fable_puss_portrait",
        Broom = "fable_witch_broom", Cauldron = "fable_witch_cauldron", House = "fable_gingerbread_house",
        Basket = "fable_red_basket", Slipper = "fable_glass_slipper", Apple = "fable_poison_apple",
        Mirror = "fable_magic_mirror", Coach = "fable_royal_coach", Frog = "fable_frog_prince",
        Door = "fable_straw_door", Lamp = "fable_floor_lamp", Teapot = "fable_teapot", Teacup = "fable_teacup",
        Ribbon = "fable_red_ribbon", Pumpkin = "fable_pumpkin", Rose = "fable_enchanted_rose", Wardrobe = "fable_wardrobe";
    /// <summary>Part groups of the basket's pies, in the order they fall out.</summary>
    internal const string PieGroup = "pie_";
    internal const int Pies = 5;

    // Weights are not invented: each piece takes the weight preset of the game's own closest valuable
    // (PhysAttribute assets read from the installed game; the game has twelve steps from 0.5 to 8 kg).
    internal static readonly IReadOnlyList<CollectionEntry> Entries = new[]
    {
        // Large: wide, big, tall, very tall.
        new CollectionEntry(Coffin, LootMap.Fable, 5f, 30000, 36000, LootFragility.Fragile),      // Manor Coffin: Heavy+ 5 kg
        new CollectionEntry(Wolf, LootMap.Fable, 4f, 14000, 18000, LootFragility.Sturdy),         // Manor Animal Crate: Heavy 4 kg
        new CollectionEntry(Portrait, LootMap.Fable, 4f, 16000, 20000, LootFragility.Fragile),    // Manor Painting: Heavy 4 kg
        new CollectionEntry(Broom, LootMap.Fable, 3.5f, 8000, 10000),                             // Wizard Broom: Medium++++ 3.5 kg
        new CollectionEntry(Cauldron, LootMap.Fable, 4f, 12000, 15000, LootFragility.Reinforced), // Wizard Cauldron Box: Heavy 4 kg
        // Medium and small.
        new CollectionEntry(House, LootMap.Fable, 3f, 8000, 11000, LootFragility.Fragile),        // Manor Radio: Medium+++ 3 kg
        new CollectionEntry(Coach, LootMap.Fable, 6f, 18000, 24000, LootFragility.Sturdy),        // Desert Golfcart (a vehicle): Heavy++ 6 kg
        new CollectionEntry(Mirror, LootMap.Fable, 2f, 9000, 12000, LootFragility.Fragile),       // Manor Gramophone: Medium++ 2 kg
        new CollectionEntry(Basket, LootMap.Fable, 1f, 4500, 6000),                               // Wizard Gem Box: Medium 1 kg
        new CollectionEntry(Slipper, LootMap.Fable, .5f, 6000, 8000, LootFragility.VeryFragile),  // Manor Goblet: Light 0.5 kg
        new CollectionEntry(Apple, LootMap.Fable, .65f, 2500, 3500),                              // Wizard Red Mushroom: Light+ 0.65 kg
        new CollectionEntry(Frog, LootMap.Fable, .5f, 2200, 3000, LootFragility.Sturdy),          // Manor Frog: Light 0.5 kg
        // Second set: the third little pig's neighbour's door, a floor lamp, a tea set and the charmed ribbon.
        new CollectionEntry(Door, LootMap.Fable, 4f, 5000, 7000, LootFragility.Sturdy),           // Manor Painting (a tall flat panel): Heavy 4 kg
        new CollectionEntry(Lamp, LootMap.Fable, 4f, 6000, 8000, LootFragility.Fragile),          // Wizard Forever Candle: Heavy 4 kg
        new CollectionEntry(Teapot, LootMap.Fable, .65f, 3500, 5000, LootFragility.Fragile),      // Manor Kettle: Light+ 0.65 kg
        new CollectionEntry(Teacup, LootMap.Fable, .65f, 1500, 2200, LootFragility.Fragile),      // Arctic Coffee Cup: Light+ 0.65 kg
        new CollectionEntry(Ribbon, LootMap.Fable, .5f, 2000, 3000, LootFragility.Reinforced),    // Manor Crown: Light 0.5 kg; RibbonCharm
        // Third set: the Beast's castle and Cinderella's pumpkin.
        new CollectionEntry(Rose, LootMap.Fable, 1.5f, 9000, 12000, LootFragility.VeryFragile),   // Wizard Crystal Ball (glass): Medium+ 1.5 kg
        new CollectionEntry(Wardrobe, LootMap.Fable, 5f, 14000, 18000, LootFragility.Sturdy),     // Grandfather Clock (tall furniture): Heavy+ 5 kg
        new CollectionEntry(Pumpkin, LootMap.Fable, 4f, 3000, 4500),                              // Golden Pumpkin Small: Heavy 4 kg
    };

    /// <summary>True for the map loot of this file; the bean puzzle's beans and egg share the prefix but are ritual loot.</summary>
    internal static bool Is(string key)
    {
        if (key == null) return false;
        foreach (var entry in Entries) if (entry.Key == key) return true;
        return false;
    }

    /// <summary>Impact sound family (ValuableFoley constants: 0 metal, 2 glass, 3 bone, 4 paper, 10 porcelain, 11 plush).</summary>
    internal static int Sound(string key) => key switch
    {
        Coffin or Slipper or Mirror or Rose => 2,
        Wolf or Frog or Ribbon or Pumpkin => 11,
        Portrait or Basket or Broom or Door or Wardrobe => 4,
        House or Apple or Teapot or Teacup => 10,
        _ => 0,
    };

    private static readonly Color Gold = new(.95f, .72f, .2f), Wood = new(.36f, .22f, .12f), DarkWood = new(.2f, .12f, .07f),
        Iron = new(.07f, .07f, .08f), Skin = new(.96f, .8f, .7f), White = new(.94f, .93f, .9f), Red = new(.78f, .08f, .08f),
        Black = new(.04f, .04f, .05f), Straw = new(.8f, .64f, .28f), Biscuit = new(.62f, .38f, .18f);

    internal static ValuableArtModel Build(string key)
    {
        var parts = new List<ValuableArtPart>();
        var materials = new List<ValuableArtMaterial>();
        switch (key)
        {
            case Coffin: GlassCoffin(); break;
            case Wolf: StuffedWolf(); break;
            case Portrait: PussPortrait(); break;
            case Broom: WitchBroom(); break;
            case Cauldron: WitchCauldron(); break;
            case House: GingerbreadHouse(); break;
            case Basket: RedBasket(); break;
            case Slipper: GlassSlipper(); break;
            case Apple: PoisonApple(); break;
            case Mirror: MagicMirror(); break;
            case Coach: RoyalCoach(); break;
            case Pumpkin: PlainPumpkin(); break;
            case Rose: EnchantedRose(); break;
            case Wardrobe: TalkingWardrobe(); break;
            case Frog: FrogPrince(); break;
            case Door: StrawDoor(); break;
            case Lamp: FloorLamp(); break;
            case Teapot: ChinaTeapot(); break;
            case Teacup: ChinaTeacup(); break;
            case Ribbon: RedRibbon(); break;
            default: throw new ArgumentException("Unknown Fable Realm valuable: " + key);
        }
        return new ValuableArtModel { parts = parts.ToArray(), materials = materials.ToArray() };

        // ---- large ----------------------------------------------------------------------------------

        // Snow White asleep under glass: 2 m long, a gilt frame around a crystal case, on a velvet bed.
        void GlassCoffin()
        {
            var gold = new ProcMesh(); var velvet = new ProcMesh(); var white = new ProcMesh(); var blue = new ProcMesh();
            var yellow = new ProcMesh(); var skin = new ProcMesh(); var hair = new ProcMesh(); var red = new ProcMesh(); var glass = new ProcMesh(); var shoes = new ProcMesh();
            Box(gold, 0f, .06f, 0f, 2f, .12f, .8f);
            foreach (float x in new[] { -.96f, .96f }) foreach (float z in new[] { -.36f, .36f }) Box(gold, x, .42f, z, .06f, .62f, .06f);
            foreach (float z in new[] { -.36f, .36f }) Box(gold, 0f, .73f, z, 1.98f, .05f, .06f);
            foreach (float x in new[] { -.96f, .96f }) Box(gold, x, .73f, 0f, .06f, .05f, .78f);
            Box(gold, 0f, .79f, 0f, .5f, .04f, .16f); gold.Diamond(ProcMesh.TRS(new(0f, .86f, 0f), Vector3.zero, new(.12f, .14f, .12f)));
            Box(velvet, 0f, .16f, 0f, 1.84f, .08f, .64f);
            Box(white, -.68f, .25f, 0f, .32f, .09f, .44f);
            Ball(skin, -.64f, .34f, 0f, .105f);
            Box(hair, -.72f, .31f, 0f, .2f, .15f, .3f); Box(hair, -.6f, .3f, .13f, .2f, .12f, .05f); Box(hair, -.6f, .3f, -.13f, .2f, .12f, .05f);
            Box(red, -.66f, .45f, 0f, .05f, .05f, .16f);
            Box(blue, -.28f, .29f, 0f, .42f, .16f, .3f);
            foreach (float z in new[] { -.2f, .2f }) { Ball(red, -.42f, .3f, z, .075f); Box(skin, -.14f, .33f, z * .45f, .22f, .05f, .07f); }
            Box(white, -.47f, .34f, 0f, .06f, .1f, .22f);
            Box(yellow, .34f, .28f, 0f, .86f, .15f, .36f); Box(yellow, .7f, .26f, 0f, .2f, .12f, .44f);
            foreach (float z in new[] { -.08f, .08f }) Box(shoes, .84f, .27f, z, .12f, .08f, .09f);
            Box(glass, 0f, .43f, 0f, 1.86f, .56f, .68f);
            Part("frame", gold, Gold, metallic: .8f, roughness: .3f); Part("bed", velvet, new Color(.42f, .05f, .12f));
            Part("linen", white, White); Part("skin", skin, Skin); Part("hair", hair, Black); Part("ribbon", red, Red);
            Part("bodice", blue, new Color(.12f, .22f, .62f)); Part("skirt", yellow, new Color(.93f, .78f, .2f));
            Part("shoes", shoes, DarkWood);
            Part("crystal", glass, new Color(.8f, .93f, 1f), transmission: .85f, roughness: .05f);
        }

        // A stuffed wolf on a plinth, 1.3 m long, a red bandana knotted round its neck.
        void StuffedWolf()
        {
            var wood = new ProcMesh(); var fur = new ProcMesh(); var pale = new ProcMesh(); var red = new ProcMesh();
            var black = new ProcMesh(); var eyes = new ProcMesh(); var teeth = new ProcMesh();
            Box(wood, 0f, .04f, 0f, 1.3f, .08f, .5f);
            Box(fur, -.02f, .62f, 0f, .82f, .34f, .32f); Box(pale, .1f, .5f, 0f, .5f, .14f, .26f);
            foreach (float x in new[] { -.32f, .3f }) foreach (float z in new[] { -.1f, .1f }) { Box(fur, x, .3f, z, .11f, .46f, .11f); Box(black, x + .03f, .1f, z, .15f, .05f, .12f); }
            Tilt(fur, .4f, .8f, 0f, .24f, .34f, .26f, -32f);
            Box(fur, .6f, .95f, 0f, .3f, .25f, .25f); Box(pale, .81f, .9f, 0f, .22f, .13f, .15f);
            Box(black, .93f, .93f, 0f, .05f, .06f, .08f); Box(black, .78f, .835f, 0f, .2f, .02f, .12f);
            foreach (float z in new[] { -.09f, .09f })
            {
                fur.Diamond(ProcMesh.TRS(new(.53f, 1.13f, z), Vector3.zero, new(.1f, .2f, .09f)));
                Box(eyes, .745f, .99f, z * 1.15f, .02f, .04f, .05f);
                Box(teeth, .86f, .815f, z * .5f, .03f, .04f, .02f);
            }
            Tilt(fur, -.56f, .58f, 0f, .42f, .13f, .13f, 32f); Box(pale, -.76f, .46f, 0f, .1f, .1f, .1f);
            Box(red, .38f, .74f, 0f, .15f, .1f, .32f); Tilt(red, .5f, .64f, 0f, .05f, .2f, .22f, 24f);
            Box(red, .3f, .82f, .12f, .12f, .06f, .1f);
            Part("plinth", wood, Wood); Part("fur", fur, new Color(.33f, .34f, .37f)); Part("muzzle", pale, new Color(.62f, .62f, .6f));
            Part("bandana", red, Red); Part("nose", black, Black); Part("eyes", eyes, new Color(1f, .78f, .2f), emission: 1.4f); Part("teeth", teeth, White);
        }

        // Puss in Boots, full length, in a gilt frame 1.8 m high. The picture is a shallow relief on the canvas.
        void PussPortrait()
        {
            var gold = new ProcMesh(); var canvas = new ProcMesh(); var cat = new ProcMesh(); var cream = new ProcMesh(); var hat = new ProcMesh();
            var white = new ProcMesh(); var eyes = new ProcMesh(); var boots = new ProcMesh(); var cape = new ProcMesh(); var steel = new ProcMesh(); var feet = new ProcMesh(); var nose = new ProcMesh();
            foreach (float x in new[] { -.385f, .385f }) Box(gold, x, .98f, 0f, .09f, 1.72f, .1f);
            foreach (float y in new[] { .16f, 1.8f }) Box(gold, 0f, y, 0f, .86f, .09f, .1f);
            foreach (float x in new[] { -.385f, .385f }) foreach (float y in new[] { .16f, 1.8f }) gold.Diamond(ProcMesh.TRS(new(x, y, -.04f), Vector3.zero, new(.1f, .1f, .1f)));
            foreach (float x in new[] { -.3f, .3f }) Box(feet, x, .05f, 0f, .12f, .1f, .44f);
            Box(canvas, 0f, .98f, .015f, .7f, 1.58f, .03f);
            const float z = -.03f;
            Box(cape, 0f, .92f, z + .012f, .5f, .46f, .02f);
            Box(cat, 0f, .86f, z, .3f, .5f, .04f); Box(cream, 0f, .84f, z - .012f, .16f, .3f, .03f);
            Box(cat, 0f, 1.26f, z - .005f, .32f, .27f, .05f); Box(cream, 0f, 1.2f, z - .02f, .16f, .1f, .04f);
            foreach (float x in new[] { -.11f, .11f })
            {
                cat.Diamond(ProcMesh.TRS(new(x, 1.42f, z), Vector3.zero, new(.11f, .16f, .04f)));
                Box(eyes, x * .7f, 1.3f, z - .035f, .055f, .05f, .012f);
                Box(white, x * 1.5f, 1.21f, z - .04f, .14f, .008f, .008f); Box(white, x * 1.5f, 1.185f, z - .04f, .13f, .008f, .008f);
                Box(cat, x * 1.6f, .9f, z, .08f, .34f, .04f); Box(white, x * 1.6f, .72f, z - .01f, .1f, .08f, .05f);
                Box(cat, x * .8f, .52f, z, .11f, .22f, .04f);
                Box(boots, x * .8f, .4f, z - .012f, .14f, .26f, .05f); Box(boots, x * 1.15f, .27f, z - .02f, .22f, .08f, .06f);
                Box(gold, x * .8f, .52f, z - .02f, .16f, .04f, .05f);
            }
            Box(nose, 0f, 1.235f, z - .042f, .035f, .025f, .012f);
            Box(hat, 0f, 1.43f, z - .02f, .56f, .04f, .07f); Box(hat, 0f, 1.5f, z - .02f, .26f, .13f, .06f);
            Tilt(white, .2f, 1.6f, z - .03f, .07f, .32f, .03f, -32f);
            Box(gold, 0f, .68f, z - .016f, .32f, .045f, .05f);
            Tilt(cat, -.24f, .6f, z + .004f, .07f, .36f, .035f, 38f);
            Tilt(steel, .31f, .82f, z - .03f, .018f, .66f, .018f, -16f); Box(gold, .225f, .58f, z - .03f, .1f, .03f, .04f);
            Part("frame", gold, Gold, metallic: .8f, roughness: .3f); Part("feet", feet, DarkWood);
            Part("canvas", canvas, new Color(.1f, .2f, .17f)); Part("cape", cape, new Color(.12f, .2f, .5f));
            Part("fur", cat, new Color(.86f, .46f, .12f)); Part("fur light", cream, new Color(.97f, .86f, .66f));
            Part("eyes", eyes, new Color(.35f, 1f, .4f), emission: 1.2f); Part("nose", nose, new Color(.95f, .5f, .56f));
            Part("hat", hat, new Color(.42f, .06f, .08f)); Part("feather", white, White);
            Part("boots", boots, DarkWood); Part("rapier", steel, new Color(.75f, .78f, .82f), metallic: .9f, roughness: .25f);
        }

        // The witch's broom, standing on its bristles: 2.6 m of crooked handle.
        void WitchBroom()
        {
            var wood = new ProcMesh(); var straw = new ProcMesh(); var cord = new ProcMesh(); var charm = new ProcMesh();
            wood.Lathe(ProcMesh.TRS(Vector3.zero, new(0f, 0f, 1.2f), Vector3.one), new[] { (.036f, .55f), (.03f, 1.3f), (.034f, 1.35f), (.028f, 2.2f), (.035f, 2.56f), (0f, 2.6f) }, 7);
            straw.Lathe(ProcMesh.TRS(Vector3.zero, Vector3.zero, new(1f, 1f, .8f)), new[] { (.13f, 0f), (.2f, .06f), (.19f, .3f), (.09f, .58f), (.05f, .66f) }, 10);
            foreach (float y in new[] { .5f, .58f }) cord.Lathe(ProcMesh.TRS(new(0f, y, 0f), Vector3.zero, new(1f, 1f, .8f)), new[] { (.1f, 0f), (.115f, .015f), (.1f, .03f) }, 10);
            Box(cord, .03f, 2.2f, 0f, .012f, .16f, .012f); charm.Diamond(ProcMesh.TRS(new(.03f, 2.08f, 0f), Vector3.zero, new(.07f, .1f, .03f)));
            Part("handle", wood, Wood); Part("bristles", straw, Straw); Part("binding", cord, Red);
            Part("charm", charm, new Color(.6f, .3f, 1f), emission: 1.3f);
        }

        // Her cauldron: a metre of black iron on three legs, the brew still glowing.
        void WitchCauldron()
        {
            var iron = new ProcMesh(); var brew = new ProcMesh(); var bubbles = new ProcMesh(); var wood = new ProcMesh();
            iron.Lathe(ProcMesh.TRS(Vector3.zero, Vector3.zero, Vector3.one), new[] { (0f, .14f), (.26f, .14f), (.43f, .28f), (.5f, .5f), (.47f, .7f), (.41f, .8f), (.47f, .86f), (.43f, .88f), (.37f, .8f) }, 14);
            for (int i = 0; i < 3; i++)
            {
                float a = i * Mathf.PI * 2f / 3f + .5f;
                Tilt(iron, .3f * Mathf.Cos(a), .1f, .3f * Mathf.Sin(a), .09f, .22f, .09f, 0f);
            }
            foreach (float x in new[] { -.5f, .5f }) Box(iron, x, .72f, 0f, .08f, .12f, .16f);
            brew.Lathe(ProcMesh.TRS(Vector3.zero, Vector3.zero, Vector3.one), new[] { (0f, .76f), (.39f, .76f) }, 14);
            Ball(bubbles, .12f, .77f, .1f, .07f); Ball(bubbles, -.16f, .77f, -.05f, .05f); Ball(bubbles, .02f, .77f, -.2f, .04f); Ball(bubbles, -.08f, .78f, .2f, .035f);
            wood.Lathe(ProcMesh.TRS(new(.2f, .7f, -.12f), new(0f, 0f, -28f), Vector3.one), new[] { (.02f, 0f), (.018f, .5f), (0f, .52f) }, 6);
            Part("iron", iron, Iron, metallic: .6f, roughness: .6f); Part("brew", brew, new Color(.2f, .85f, .2f), emission: .8f);
            Part("bubbles", bubbles, new Color(.5f, 1f, .4f), emission: 1.1f); Part("ladle", wood, Wood);
        }

        // ---- medium and small -----------------------------------------------------------------------

        // A gingerbread house small enough to carry: biscuit walls, icing roof, sweets on the tiles.
        void GingerbreadHouse()
        {
            var biscuit = new ProcMesh(); var icing = new ProcMesh(); var pink = new ProcMesh(); var choc = new ProcMesh();
            var glow = new ProcMesh(); var sweets = new ProcMesh(); var mint = new ProcMesh();
            Box(icing, 0f, .02f, 0f, .8f, .04f, .7f);
            Box(biscuit, 0f, .3f, 0f, .66f, .54f, .56f); Box(biscuit, 0f, .62f, 0f, .4f, .16f, .54f); Box(biscuit, 0f, .74f, 0f, .16f, .1f, .52f);
            foreach (float s in new[] { -1f, 1f })
            {
                Tilt(pink, s * .19f, .7f, 0f, .46f, .06f, .68f, -s * 38f);
                Tilt(icing, s * .36f, .57f, 0f, .08f, .07f, .7f, -s * 38f);
                Box(glow, s * .2f, .34f, -.283f, .14f, .14f, .012f); Box(icing, s * .2f, .34f, -.286f, .17f, .025f, .012f); Box(icing, s * .2f, .34f, -.286f, .025f, .17f, .012f);
                for (int i = 0; i < 3; i++) Ball(i % 2 == 0 ? sweets : mint, s * (.12f + i * .1f), .78f - i * .078f, -.22f + i * .2f, .04f);
            }
            Box(icing, 0f, .86f, 0f, .08f, .05f, .7f);
            Box(choc, 0f, .2f, -.283f, .16f, .34f, .014f); Ball(sweets, .05f, .2f, -.295f, .018f);
            Box(biscuit, .17f, .78f, .16f, .11f, .12f, .11f); Box(icing, .17f, .855f, .16f, .14f, .03f, .14f);
            Part("walls", biscuit, Biscuit); Part("icing", icing, White); Part("roof", pink, new Color(.95f, .55f, .68f));
            Part("door", choc, new Color(.25f, .12f, .06f)); Part("windows", glow, new Color(1f, .82f, .4f), emission: 1.5f);
            Part("sweets", sweets, Red); Part("mints", mint, new Color(.3f, .8f, .5f));
        }

        // Cinderella's coach: a pearl-white egg of a carriage on four gilt wheels, a crown on the roof, lanterns lit.
        void RoyalCoach()
        {
            var shell = new ProcMesh(); var gold = new ProcMesh(); var glass = new ProcMesh(); var velvet = new ProcMesh(); var lamp = new ProcMesh();
            var egg = new[] { (0f, -.5f), (.2f, -.46f), (.31f, -.36f), (.38f, -.2f), (.4f, 0f), (.38f, .2f), (.31f, .36f), (.2f, .46f), (0f, .5f) };
            const float cy = .74f, length = 1.15f, widthScale = .9f;
            shell.Lathe(ProcMesh.TRS(new(0f, cy, 0f), new(0f, 0f, 90f), new(1f, length, widthScale)), egg, 28);
            // Gilt bands around the body, where the shell is RadiusAt() wide.
            foreach (float x in new[] { -.3f, 0f, .3f })
            {
                float r = RadiusAt(egg, -x / length) + .004f;
                var band = new Vector3[40];
                for (int i = 0; i < band.Length; i++) { float a = i * Mathf.PI * 2f / band.Length; band[i] = new(x, cy + r * Mathf.Cos(a), r * widthScale * Mathf.Sin(a)); }
                gold.Tube(Id, band, new[] { .013f }, 8, closed: true);
            }
            // A window on each side, framed in gold, a velvet valance across the top.
            foreach (float side in new[] { -1f, 1f })
            {
                float z = side * .345f;
                Blob(glass, new(0f, cy + .04f, z), new(.32f, .32f, .05f));
                var frame = new Vector3[32];
                for (int i = 0; i < frame.Length; i++) { float a = i * Mathf.PI * 2f / frame.Length; frame[i] = new(.165f * Mathf.Cos(a), cy + .04f + .165f * Mathf.Sin(a), z * 1.06f); }
                gold.Tube(Id, frame, new[] { .014f }, 8, closed: true);
                Blob(velvet, new(0f, cy + .16f, z * 1.03f), new(.28f, .08f, .05f));
            }
            // Crown on the roof.
            gold.Lathe(ProcMesh.TRS(new(0f, cy + .38f, 0f), Vector3.zero, Vector3.one), new[] { (0f, 0f), (.12f, 0f), (.13f, .05f), (.11f, .065f), (0f, .06f) }, 20);
            for (int i = 0; i < 5; i++)
            {
                float a = i * Mathf.PI * 2f / 5f;
                gold.Diamond(ProcMesh.TRS(new(.11f * Mathf.Cos(a), cy + .47f, .11f * Mathf.Sin(a)), Vector3.zero, new(.045f, .09f, .045f)));
            }
            Blob(gold, new(0f, cy + .5f, 0f), Vector3.one * .06f);
            // Four wheels with eight spokes each, on gilt axles under the body.
            foreach (float x in new[] { -.36f, .36f })
            {
                gold.Tube(Id, new Vector3[] { new(x, .3f, -.42f), new(x, .3f, .42f) }, new[] { .016f }, 8);
                gold.Tube(Id, new Vector3[] { new(x * .85f, .3f, 0f), new(x * .8f, cy - .3f, 0f) }, new[] { .02f }, 8);
                foreach (float z in new[] { -.42f, .42f })
                {
                    var rim = new Vector3[36];
                    for (int i = 0; i < rim.Length; i++) { float a = i * Mathf.PI * 2f / rim.Length; rim[i] = new(x + .27f * Mathf.Cos(a), .3f + .27f * Mathf.Sin(a), z); }
                    gold.Tube(Id, rim, new[] { .026f }, 8, closed: true);
                    for (int k = 0; k < 8; k++)
                    {
                        float a = k * Mathf.PI / 4f;
                        gold.Tube(Id, new Vector3[] { new(x, .3f, z), new(x + .26f * Mathf.Cos(a), .3f + .26f * Mathf.Sin(a), z) }, new[] { .009f }, 6);
                    }
                    Blob(gold, new(x, .3f, z), new(.08f, .08f, .06f));
                }
            }
            // Two lanterns at the front corners.
            foreach (float z in new[] { -.2f, .2f })
            {
                gold.Tube(Id, new Vector3[] { new(.5f, cy + .1f, z), new(.6f, cy + .18f, z) }, new[] { .01f }, 6);
                Blob(lamp, new(.62f, cy + .2f, z), new(.06f, .09f, .06f));
                Blob(gold, new(.62f, cy + .26f, z), new(.07f, .03f, .07f));
            }
            Part("shell", shell, new Color(.95f, .93f, .88f), roughness: .3f, smooth: true);
            Part("gilding", gold, Gold, metallic: .85f, roughness: .25f, smooth: true);
            Part("windows", glass, new Color(.2f, .32f, .55f), emission: .35f, roughness: .1f, smooth: true);
            Part("valance", velvet, new Color(.85f, .45f, .6f), smooth: true);
            Part("lanterns", lamp, new Color(1f, .8f, .45f), emission: 1.8f, smooth: true);
        }

        // A plain round pumpkin, eight lobes, a twisted stem, a curl of vine and one leaf: before the fairy came.
        void PlainPumpkin()
        {
            var lobes = new ProcMesh(); var deep = new ProcMesh(); var stem = new ProcMesh(); var leaf = new ProcMesh();
            Blob(lobes, new(0f, .21f, 0f), new(.4f, .4f, .4f));
            for (int k = 0; k < 8; k++)
            {
                float a = k * Mathf.PI / 4f;
                Blob(k % 2 == 0 ? lobes : deep, new(.13f * Mathf.Cos(a), .21f, .13f * Mathf.Sin(a)), new(.25f, .42f, .3f), new(0f, 90f - a * Mathf.Rad2Deg, 0f));
            }
            stem.Tube(Id, new Vector3[] { new(0f, .37f, 0f), new(.01f, .43f, .005f), new(.035f, .48f, .015f), new(.06f, .5f, .02f) }, new[] { .032f, .026f, .022f, .02f }, 8, capped: true);
            var curl = new Vector3[14];
            for (int i = 0; i < curl.Length; i++) { float a = i * .55f, r = .05f - i * .0025f; curl[i] = new(-.04f + r * Mathf.Cos(a), .41f + i * .004f, .03f + r * Mathf.Sin(a)); }
            leaf.Tube(Id, curl, new[] { .006f }, 6, capped: true);
            Blob(leaf, new(-.1f, .4f, -.05f), new(.14f, .014f, .09f), new(0f, 35f, -12f));
            Part("pumpkin", lobes, new Color(.95f, .5f, .08f), roughness: .45f, smooth: true);
            Part("pumpkin deep", deep, new Color(.86f, .4f, .05f), roughness: .45f, smooth: true);
            Part("stem", stem, new Color(.38f, .32f, .14f), smooth: true); Part("vine", leaf, new Color(.25f, .55f, .2f), smooth: true);
        }

        // "Mirror, mirror": an oval glass on a stand, with a pale face looking out of it.
        void MagicMirror()
        {
            var gold = new ProcMesh(); var glass = new ProcMesh(); var face = new ProcMesh(); var gem = new ProcMesh();
            var facing = ProcMesh.TRS(new(0f, .47f, 0f), new(90f, 0f, 0f), new(.74f, 1f, 1f));
            gold.Lathe(facing, new[] { (.24f, -.035f), (.3f, -.02f), (.3f, .02f), (.24f, .035f) }, 16);
            glass.Lathe(facing, new[] { (0f, -.012f), (.245f, -.012f), (.245f, .012f), (0f, .012f) }, 16);
            Box(gold, 0f, .1f, 0f, .07f, .18f, .06f); Box(gold, 0f, .025f, 0f, .32f, .05f, .22f);
            gold.Diamond(ProcMesh.TRS(new(0f, .8f, 0f), Vector3.zero, new(.14f, .12f, .06f)));
            foreach (float x in new[] { -.06f, .06f }) Box(face, x, .54f, -.018f, .055f, .035f, .01f);
            Box(face, 0f, .39f, -.018f, .11f, .03f, .01f); Tilt(face, 0f, .465f, -.018f, .025f, .07f, .01f, 0f);
            gem.Diamond(ProcMesh.TRS(new(0f, .8f, -.03f), Vector3.zero, new(.06f, .07f, .04f)));
            Part("frame", gold, Gold, metallic: .8f, roughness: .3f); Part("glass", glass, new Color(.45f, .7f, .95f), emission: .45f);
            Part("face", face, new Color(.1f, .14f, .3f)); Part("gem", gem, new Color(.9f, .2f, .5f), emission: 1.2f);
        }

        // Little Red Riding Hood's basket. The five pies are separate parts: PieBasket tips them out one by one.
        void RedBasket()
        {
            var wicker = new ProcMesh(); var cloth = new ProcMesh(); var check = new ProcMesh();
            var squash = new Vector3(1f, 1f, .76f);
            wicker.Lathe(ProcMesh.TRS(Vector3.zero, Vector3.zero, squash), new[] { (0f, .02f), (.18f, .02f), (.245f, .22f), (.26f, .245f), (.225f, .245f), (.17f, .06f), (0f, .06f) }, 12);
            for (int i = 0; i <= 8; i++)
            {
                float a = Mathf.PI * i / 8f;
                Tilt(wicker, .235f * Mathf.Cos(a), .24f + .2f * Mathf.Sin(a), 0f, .1f, .03f, .045f, a * Mathf.Rad2Deg + 90f);
            }
            cloth.Lathe(ProcMesh.TRS(Vector3.zero, Vector3.zero, squash), new[] { (0f, .19f), (.2f, .19f), (.232f, .235f) }, 12);
            for (int i = -2; i <= 2; i++) { Box(check, i * .08f, .194f, 0f, .02f, .004f, .26f); Box(check, 0f, .194f, i * .06f, .36f, .004f, .016f); }
            Tilt(cloth, .25f, .2f, 0f, .03f, .12f, .2f, 14f);
            Part("wicker", wicker, new Color(.6f, .42f, .2f)); Part("cloth", cloth, Red); Part("checks", check, White);
            var spots = new[] { new Vector2(-.09f, -.05f), new Vector2(.09f, -.05f), new Vector2(0f, .06f), new Vector2(-.12f, .07f), new Vector2(.12f, .07f) };
            for (int i = 0; i < Pies; i++)
            {
                var pie = new ProcMesh();
                pie.Lathe(ProcMesh.TRS(new(spots[i].x, .2f + (i == 2 ? .035f : 0f), spots[i].y), new(0f, i * 40f, 0f), new(1f, 1f, .8f)),
                    new[] { (0f, 0f), (.062f, .004f), (.07f, .03f), (.045f, .056f), (0f, .062f) }, 8);
                Part(PieGroup + i, pie, new Color(.84f, .58f, .24f));
            }
        }

        // Cinderella's glass slipper: a high-heeled pump with a pointed toe, a curved sole and a small bow.
        void GlassSlipper()
        {
            var glass = new ProcMesh(); var bow = new ProcMesh();
            const float flat = .45f; // the sole tube is squashed to this share of its height
            var sole = new[] { new Vector3(-.115f, .088f / flat, 0f), new Vector3(-.075f, .082f / flat, 0f), new Vector3(-.035f, .06f / flat, 0f),
                new Vector3(.005f, .03f / flat, 0f), new Vector3(.05f, .012f / flat, 0f), new Vector3(.095f, .009f / flat, 0f), new Vector3(.128f, .012f / flat, 0f) };
            glass.Tube(ProcMesh.TRS(Vector3.zero, Vector3.zero, new(1f, flat, 1f)), sole, new[] { .03f, .034f, .036f, .038f, .04f, .033f, .016f }, 16, capped: true);
            Blob(glass, new(.075f, .03f, 0f), new(.12f, .07f, .085f));
            Blob(glass, new(-.088f, .1f, 0f), new(.06f, .055f, .078f));
            var rim = new Vector3[28];
            for (int i = 0; i < rim.Length; i++) { float a = i * Mathf.PI * 2f / rim.Length; rim[i] = new(-.03f + .085f * Mathf.Cos(a), .085f - .035f * Mathf.Cos(a), .04f * Mathf.Sin(a)); }
            glass.Tube(Id, rim, new[] { .008f }, 8, closed: true);
            glass.Lathe(ProcMesh.TRS(new(-.1f, 0f, 0f), Vector3.zero, Vector3.one), new[] { (0f, 0f), (.008f, 0f), (.011f, .03f), (.017f, .07f), (.021f, .08f), (0f, .082f) }, 12);
            foreach (float side in new[] { -1f, 1f }) bow.Diamond(ProcMesh.TRS(new(.06f, .068f, side * .025f), new(0f, 0f, side * 10f), new(.035f, .02f, .045f)));
            Blob(bow, new(.06f, .07f, 0f), Vector3.one * .018f);
            Part("glass", glass, new Color(.78f, .92f, 1f), transmission: .8f, roughness: .05f, smooth: true);
            Part("bow", bow, new Color(.6f, .9f, 1f), emission: 1.4f, smooth: true);
        }

        // The Queen's apple: glossy red, with the poison still running down it.
        void PoisonApple()
        {
            var apple = new ProcMesh(); var poison = new ProcMesh(); var stem = new ProcMesh(); var leaf = new ProcMesh();
            apple.Lathe(ProcMesh.TRS(Vector3.zero, Vector3.zero, Vector3.one), new[] { (0f, .012f), (.05f, 0f), (.092f, .04f), (.1f, .1f), (.085f, .16f), (.04f, .19f), (0f, .17f) }, 10);
            poison.Lathe(ProcMesh.TRS(new(0f, .004f, 0f), Vector3.zero, Vector3.one), new[] { (.088f, .16f), (.042f, .197f), (0f, .178f) }, 10);
            for (int i = 0; i < 5; i++)
            {
                float a = i * 1.3f; float drop = .035f + (i % 3) * .02f;
                Box(poison, .093f * Mathf.Cos(a), .162f - drop * .5f, .093f * Mathf.Sin(a), .02f, drop, .02f);
            }
            stem.Lathe(ProcMesh.TRS(new(0f, .17f, 0f), new(0f, 0f, 14f), Vector3.one), new[] { (.008f, 0f), (.006f, .05f) }, 5);
            leaf.Diamond(ProcMesh.TRS(new(.03f, .215f, 0f), new(0f, 0f, -25f), new(.07f, .012f, .04f)));
            Part("apple", apple, new Color(.7f, .04f, .06f), roughness: .2f); Part("poison", poison, new Color(.3f, .9f, .25f), emission: .8f);
            Part("stem", stem, DarkWood); Part("leaf", leaf, new Color(.2f, .55f, .2f));
        }

        // The frog who would be a prince, crown and all.
        void FrogPrince()
        {
            var green = new ProcMesh(); var belly = new ProcMesh(); var white = new ProcMesh(); var black = new ProcMesh(); var gold = new ProcMesh();
            Box(green, 0f, .07f, .02f, .17f, .12f, .19f); Box(belly, 0f, .055f, -.03f, .13f, .08f, .12f);
            Box(green, 0f, .135f, -.06f, .16f, .08f, .13f); Box(black, 0f, .115f, -.127f, .11f, .008f, .01f);
            foreach (float x in new[] { -.055f, .055f })
            {
                Ball(white, x, .185f, -.075f, .03f); Box(black, x, .188f, -.103f, .018f, .022f, .008f);
                Tilt(green, x * 1.7f, .04f, .06f, .09f, .05f, .12f, 0f); Box(green, x * 1.9f, .012f, -.04f, .07f, .02f, .09f);
                Box(green, x * 1.3f, .012f, -.1f, .05f, .02f, .06f);
            }
            gold.Lathe(ProcMesh.TRS(new(0f, .175f, -.03f), Vector3.zero, Vector3.one), new[] { (.045f, 0f), (.05f, .025f), (.04f, .025f) }, 8);
            for (int i = 0; i < 4; i++)
            {
                float a = i * Mathf.PI / 2f + .78f;
                gold.Diamond(ProcMesh.TRS(new(.045f * Mathf.Cos(a), .21f, -.03f + .045f * Mathf.Sin(a)), Vector3.zero, new(.022f, .04f, .022f)));
            }
            Part("frog", green, new Color(.2f, .6f, .16f)); Part("belly", belly, new Color(.75f, .85f, .45f));
            Part("eyes", white, White); Part("pupils", black, Black); Part("crown", gold, Gold, metallic: .8f, roughness: .3f);
        }

        // ---- second set -----------------------------------------------------------------------------

        // The straw door of the first little pig's house: bundles of straw tied to a plank frame, on two feet.
        void StrawDoor()
        {
            var wood = new ProcMesh(); var straw = new ProcMesh(); var pale = new ProcMesh(); var rope = new ProcMesh();
            foreach (float x in new[] { -.4f, .4f }) Box(wood, x, .95f, 0f, .06f, 1.8f, .08f);
            foreach (float y in new[] { .2f, 1.7f }) Box(wood, 0f, y, .02f, .86f, .09f, .06f);
            Tilt(wood, 0f, .95f, .025f, .07f, 1.62f, .05f, -25f);
            foreach (float x in new[] { -.32f, .32f }) Box(wood, x, .04f, 0f, .12f, .08f, .4f);
            for (int i = 0; i < 7; i++)
            {
                float x = -.33f + i * .11f, top = 1.78f - (i % 3) * .035f; // with the stalks it stays under 1.875 m: a tall slot
                Box(i % 2 == 0 ? straw : pale, x, .1f + (top - .1f) * .5f, -.01f, .115f, top - .1f, .07f);
                // Loose stalks poking out of the top of each bundle.
                Tilt(i % 2 == 0 ? pale : straw, x + .02f, top + .04f, -.01f, .02f, .1f, .02f, (i % 2 == 0 ? 1f : -1f) * 14f);
            }
            foreach (float y in new[] { .55f, 1.35f }) Box(rope, 0f, y, -.05f, .84f, .035f, .02f);
            Box(rope, .27f, .95f, -.065f, .03f, .14f, .03f); Box(rope, .27f, 1.02f, -.075f, .08f, .025f, .025f); Box(rope, .27f, .88f, -.075f, .08f, .025f, .025f);
            Part("frame", wood, Wood); Part("straw", straw, Straw); Part("straw light", pale, new Color(.93f, .8f, .45f));
            Part("ties", rope, new Color(.5f, .34f, .18f));
        }

        // A tall floor lamp with a fringed fabric shade, still lit.
        void FloorLamp()
        {
            var brass = new ProcMesh(); var shade = new ProcMesh(); var bulb = new ProcMesh(); var fringe = new ProcMesh();
            brass.Lathe(ProcMesh.TRS(Vector3.zero, Vector3.zero, Vector3.one), new[] { (0f, 0f), (.24f, 0f), (.24f, .03f), (.16f, .06f), (.04f, .08f), (.025f, .12f) }, 14);
            brass.Lathe(ProcMesh.TRS(Vector3.zero, Vector3.zero, Vector3.one), new[] { (.022f, .12f), (.018f, .78f), (.035f, .8f), (.035f, .84f), (.018f, .86f), (.016f, 1.36f), (0f, 1.38f) }, 8);
            Tilt(brass, 0f, 1.36f, 0f, .03f, .22f, .03f, 90f);
            // The shade is a cone open at both ends: an outer and an inner shell, so it reads from below too.
            var shadeProfile = new[] { (.29f, 1.3f), (.27f, 1.34f), (.17f, 1.62f), (.15f, 1.63f) };
            shade.Lathe(ProcMesh.TRS(Vector3.zero, Vector3.zero, Vector3.one), shadeProfile, 16);
            System.Array.Reverse(shadeProfile);
            shade.Lathe(ProcMesh.TRS(Vector3.zero, Vector3.zero, Vector3.one), shadeProfile, 16);
            Ball(bulb, 0f, 1.43f, 0f, .06f);
            for (int i = 0; i < 16; i++)
            {
                float a = i * Mathf.PI * 2f / 16f;
                Box(fringe, .285f * Mathf.Cos(a), 1.27f, .285f * Mathf.Sin(a), .025f, .06f, .025f);
            }
            Box(brass, .12f, 1.26f, 0f, .008f, .2f, .008f); Ball(brass, .12f, 1.15f, 0f, .018f);
            Part("brass", brass, Gold, metallic: .8f, roughness: .3f); Part("shade", shade, new Color(.95f, .76f, .5f), emission: .55f);
            Part("bulb", bulb, new Color(1f, .9f, .62f), emission: 2f); Part("fringe", fringe, new Color(.6f, .14f, .14f));
        }

        // The kindly teapot of the enchanted castle: round ivory porcelain, a face, gold trim and blue flowers.
        void ChinaTeapot()
        {
            var china = new ProcMesh(); var gold = new ProcMesh(); var blue = new ProcMesh();
            var whites = new ProcMesh(); var pupils = new ProcMesh(); var dark = new ProcMesh(); var pink = new ProcMesh();
            var body = new[] { (0f, 0f), (.07f, 0f), (.11f, .01f), (.145f, .035f), (.168f, .075f), (.177f, .12f), (.172f, .16f), (.155f, .195f), (.125f, .222f), (.095f, .235f), (0f, .237f) };
            china.Lathe(Id, body, 32);
            china.Lathe(ProcMesh.TRS(new(0f, .232f, 0f), Vector3.zero, Vector3.one), new[] { (.1f, 0f), (.104f, .008f), (.088f, .03f), (.05f, .045f), (.02f, .05f), (0f, .051f) }, 32);
            Blob(gold, new(0f, .3f, 0f), Vector3.one * .045f);
            // The spout on her right, curving up; the handle on her left.
            china.Tube(Id, new Vector3[] { new(.15f, .07f, 0f), new(.2f, .085f, 0f), new(.235f, .12f, 0f), new(.255f, .17f, 0f), new(.275f, .205f, 0f), new(.3f, .222f, 0f) },
                new[] { .045f, .036f, .028f, .022f, .02f, .023f }, 14);
            var handle = new Vector3[13];
            for (int i = 0; i < handle.Length; i++) { float a = Mathf.Lerp(-75f, 75f, i / 12f) * Mathf.Deg2Rad; handle[i] = new(-.155f - .085f * Mathf.Cos(a), .135f + .08f * Mathf.Sin(a), 0f); }
            china.Tube(Id, handle, new[] { .019f }, 12);
            Ring(gold, .06f, RadiusAt(body, .06f) + .002f, .006f);
            Ring(gold, .2f, RadiusAt(body, .2f) + .002f, .006f);
            Ring(gold, .236f, .101f, .005f);
            foreach (float a in new[] { 40f, 90f, 140f }) Flower(blue, gold, a, .12f, RadiusAt(body, .12f));
            Face(whites, pupils, dark, pink, (x, y) => -Mathf.Sqrt(Mathf.Max(1e-6f, RadiusAt(body, y) * RadiusAt(body, y) - x * x)), .056f, .152f, .052f, .093f, .046f);
            Part("china", china, Ivory, roughness: .2f, smooth: true); Part("gilding", gold, Gold, metallic: .85f, roughness: .25f, smooth: true);
            Part("flowers", blue, new Color(.16f, .3f, .78f), roughness: .25f, smooth: true);
            FaceParts(whites, pupils, dark, pink);
        }

        // Her little son, the cup with a chip in his rim: big eyes, a grin, gold rim and a blue band.
        void ChinaTeacup()
        {
            var china = new ProcMesh(); var gold = new ProcMesh(); var blue = new ProcMesh();
            var whites = new ProcMesh(); var pupils = new ProcMesh(); var dark = new ProcMesh(); var pink = new ProcMesh();
            var outer = new[] { (0f, 0f), (.035f, 0f), (.048f, .012f), (.057f, .045f), (.0625f, .09f), (.066f, .112f) };
            china.Lathe(Id, new[] { (0f, 0f), (.035f, 0f), (.048f, .012f), (.057f, .045f), (.0625f, .09f), (.0565f, .09f), (.051f, .05f), (.042f, .03f), (0f, .028f) }, 32);
            // The top of the rim goes all round but for the chip, front right: a V broken out of it, a sloping wedge on each side.
            const float rimLow = .09f, rimHigh = .112f, chipFrom = 290f, chipTo = 318f, chipSlope = 11f;
            china.LatheArc(Id, new[] { (.0625f, rimLow), (.066f, rimHigh), (.06f, rimHigh), (.0565f, rimLow) }, 32, chipTo - 360f, chipFrom);
            Vector3 Rim(float degrees, float radius, float y) => new(radius * Mathf.Cos(degrees * Mathf.Deg2Rad), y, radius * Mathf.Sin(degrees * Mathf.Deg2Rad));
            foreach (var (edge, foot) in new[] { (chipFrom, chipFrom + chipSlope), (chipTo, chipTo - chipSlope) })
            {
                Vector3 outerLow = Rim(edge, .0625f, rimLow), outerTop = Rim(edge, .066f, rimHigh), innerTop = Rim(edge, .06f, rimHigh), innerLow = Rim(edge, .0565f, rimLow);
                Vector3 outerFoot = Rim(foot, .0625f, rimLow), innerFoot = Rim(foot, .0565f, rimLow);
                var inside = (outerLow + outerTop + innerTop + innerLow + outerFoot + innerFoot) / 6f;
                china.Hull(inside, outerLow, outerTop, outerFoot).Hull(inside, innerLow, innerTop, innerFoot).Hull(inside, outerTop, innerTop, innerFoot, outerFoot);
            }
            var handle = new Vector3[11];
            for (int i = 0; i < handle.Length; i++) { float a = Mathf.Lerp(-78f, 78f, i / 10f) * Mathf.Deg2Rad; handle[i] = new(.056f + .033f * Mathf.Cos(a), .058f + .03f * Mathf.Sin(a), 0f); }
            china.Tube(Id, handle, new[] { .009f }, 10);
            Ring(gold, .086f, RadiusAt(outer, .086f) + .0012f, .0035f);
            Ring(blue, .03f, RadiusAt(outer, .03f) + .001f, .004f);
            Face(whites, pupils, dark, pink, (x, y) => -Mathf.Sqrt(Mathf.Max(1e-6f, RadiusAt(outer, y) * RadiusAt(outer, y) - x * x)), .021f, .066f, .024f, .04f, .017f);
            Part("china", china, Ivory, roughness: .2f, smooth: true); Part("gilding", gold, Gold, metallic: .85f, roughness: .25f, smooth: true);
            Part("band", blue, new Color(.16f, .3f, .78f), roughness: .25f, smooth: true);
            FaceParts(whites, pupils, dark, pink);
        }

        // The enchanted rose under its glass dome, a petal already fallen.
        void EnchantedRose()
        {
            var wood = new ProcMesh(); var gold = new ProcMesh(); var glass = new ProcMesh(); var stem = new ProcMesh();
            var leaf = new ProcMesh(); var bloom = new ProcMesh(); var outerPetals = new ProcMesh();
            wood.Lathe(Id, new[] { (0f, 0f), (.17f, 0f), (.172f, .02f), (.16f, .035f), (.15f, .05f), (.145f, .06f), (0f, .06f) }, 32);
            Ring(gold, .035f, .162f, .006f); Ring(gold, .06f, .137f, .006f);
            glass.Lathe(ProcMesh.TRS(new(0f, .06f, 0f), Vector3.zero, Vector3.one), new[] { (.132f, 0f), (.132f, .24f), (.125f, .29f), (.1f, .33f), (.06f, .355f), (0f, .362f) }, 32);
            Blob(gold, new(0f, .435f, 0f), Vector3.one * .03f);
            stem.Tube(Id, new Vector3[] { new(0f, .06f, 0f), new(.012f, .12f, .005f), new(-.004f, .19f, -.006f), new(.002f, .25f, 0f) }, new[] { .007f, .006f, .006f, .005f }, 8);
            Blob(leaf, new(.032f, .13f, 0f), new(.05f, .008f, .024f), new(0f, 0f, 25f));
            Blob(leaf, new(-.03f, .185f, .01f), new(.045f, .008f, .022f), new(0f, 30f, -25f));
            Blob(bloom, new(0f, .275f, 0f), new(.048f, .055f, .048f));
            for (int k = 0; k < 7; k++)
            {
                float a = k * Mathf.PI * 2f / 7f;
                Blob(bloom, new(.028f * Mathf.Cos(a), .267f + .008f * (k % 2), .028f * Mathf.Sin(a)), new(.042f, .048f, .02f), new(-20f, 90f - a * Mathf.Rad2Deg, 0f));
            }
            for (int k = 0; k < 5; k++)
            {
                float a = k * Mathf.PI * 2f / 5f + .3f;
                Blob(outerPetals, new(.045f * Mathf.Cos(a), .253f, .045f * Mathf.Sin(a)), new(.053f, .042f, .02f), new(-45f, 90f - a * Mathf.Rad2Deg, 0f));
            }
            Blob(outerPetals, new(.07f, .062f, -.05f), new(.026f, .006f, .018f), new(0f, 40f, 0f));
            Part("base", wood, new Color(.28f, .16f, .09f), roughness: .4f, smooth: true); Part("gilding", gold, Gold, metallic: .85f, roughness: .25f, smooth: true);
            Part("stem", stem, new Color(.2f, .45f, .18f), smooth: true); Part("leaves", leaf, new Color(.22f, .55f, .2f), smooth: true);
            Part("rose", bloom, new Color(.88f, .08f, .16f), emission: .55f, roughness: .35f, smooth: true);
            Part("petals", outerPetals, new Color(.66f, .04f, .1f), emission: .35f, roughness: .35f, smooth: true);
            Part("dome", glass, new Color(.85f, .95f, 1f), transmission: .85f, roughness: .05f, smooth: true);
        }

        // The castle's talking wardrobe: a cream cabinet with rose doors, a cornice and an arched gilt pediment, a face across her doors.
        void TalkingWardrobe()
        {
            var paint = new ProcMesh(); var crest = new ProcMesh(); var panel = new ProcMesh(); var gold = new ProcMesh();
            var whites = new ProcMesh(); var pupils = new ProcMesh(); var dark = new ProcMesh(); var pink = new ProcMesh();
            // The cabinet is flat-shaded with a narrow bevel, so it reads as joinery; only the arch on top is rounded.
            paint.ChamferBox(ProcMesh.TRS(new(0f, .86f, 0f), Vector3.zero, new(.8f, 1.4f, .46f)), .03f);
            paint.ChamferBox(ProcMesh.TRS(new(0f, 1.585f, 0f), Vector3.zero, new(.86f, .07f, .5f)), .2f);
            paint.ChamferBox(ProcMesh.TRS(new(0f, .14f, 0f), Vector3.zero, new(.84f, .07f, .5f)), .2f);
            foreach (float x in new[] { -.33f, .33f }) foreach (float z in new[] { -.17f, .17f }) Blob(gold, new(x, .055f, z), new(.1f, .11f, .1f));
            // The pediment: half of a flat drum lying front to back, rising from the cornice.
            const float archY = 1.62f, archWide = .38f, archHigh = .14f, archFront = -.22f;
            crest.Lathe(ProcMesh.TRS(new(0f, archY, 0f), new(90f, 0f, 0f), new(archWide * 2f, -archFront * 2f, archHigh * 2f)),
                new[] { (0f, -.5f), (.47f, -.5f), (.5f, -.46f), (.5f, .46f), (.47f, .5f), (0f, .5f) }, 40);
            var arc = new Vector3[17];
            for (int i = 0; i < arc.Length; i++) { float a = Mathf.PI * i / (arc.Length - 1); arc[i] = new((archWide + .004f) * Mathf.Cos(a), archY + (archHigh + .004f) * Mathf.Sin(a), archFront + .004f); }
            gold.Tube(Id, arc, new[] { .016f }, 8, capped: true);
            Blob(gold, new(0f, archY + .062f, archFront), new(.085f, .085f, .03f));
            Blob(gold, new(0f, archY + archHigh + .02f, 0f), Vector3.one * .075f);
            gold.Diamond(ProcMesh.TRS(new(0f, archY + archHigh + .068f, 0f), Vector3.zero, new(.05f, .075f, .05f)));
            foreach (float x in new[] { -.185f, .185f }) Box(panel, x, .88f, -.233f, .33f, 1.08f, .02f);
            Box(gold, 0f, .88f, -.245f, .014f, 1.1f, .012f);
            foreach (float x in new[] { -.035f, .035f }) Blob(gold, new(x, .74f, -.252f), Vector3.one * .03f);
            Box(panel, 0f, .245f, -.233f, .72f, .1f, .02f); Blob(gold, new(0f, .245f, -.25f), Vector3.one * .03f);
            Face(whites, pupils, dark, pink, (x, y) => -.243f, .14f, 1.2f, .11f, .99f, .13f);
            var cream = new Color(.95f, .92f, .86f);
            Part("paint", paint, cream, roughness: .4f); Part("crest", crest, cream, roughness: .4f, smooth: true);
            Part("panels", panel, new Color(.93f, .7f, .74f), roughness: .4f);
            Part("gilding", gold, Gold, metallic: .85f, roughness: .25f, smooth: true);
            FaceParts(whites, pupils, dark, pink);
        }

        // The red ribbon that keeps its bearer from death once (RibbonCharm). A bow lying flat, faintly glowing:
        // two plump loops, a knot, and two swallow-tailed ends hanging towards the front.
        void RedRibbon()
        {
            var ribbon = new ProcMesh(); var knot = new ProcMesh();
            const float flat = .3f; // the bands are tubes squashed to this share of their width
            var squash = ProcMesh.TRS(Vector3.zero, Vector3.zero, new(1f, flat, 1f));
            Vector3 P(float x, float y, float z) => new(x, y / flat, z);
            foreach (float s in new[] { -1f, 1f })
            {
                // A loop: pinched at the knot, widest near its rounded outer end, with a darker fold along the middle.
                ribbon.Tube(squash, new[] { P(0f, .02f, .02f), P(s * .035f, .024f, .022f), P(s * .08f, .028f, .026f), P(s * .12f, .028f, .03f), P(s * .148f, .025f, .032f), P(s * .16f, .023f, .032f) },
                    new[] { .012f, .034f, .056f, .06f, .044f, .018f }, 14, capped: true);
                Blob(knot, new(s * .1f, .042f, .028f), new(.085f, .014f, .045f));
                // A tail: a narrow band from the knot towards the front, ending in a swallow tail.
                var end = new Vector3(s * .05f, .01f, -.15f);
                ribbon.Tube(squash, new[] { P(s * .006f, .016f, .01f), P(s * .02f, .012f, -.05f), P(s * .036f, .01f, -.1f), P(end.x, end.y, end.z) },
                    new[] { .013f, .017f, .019f, .02f }, 12, capped: true);
                foreach (float tip in new[] { -1f, 1f })
                    ribbon.Tube(squash, new[] { P(end.x + tip * .009f, end.y, end.z + .004f), P(end.x + tip * .013f + s * .008f, end.y, end.z - .032f) }, new[] { .011f, .003f }, 8, capped: true);
            }
            Blob(knot, new(0f, .024f, .018f), new(.048f, .046f, .048f));
            Part("ribbon", ribbon, new Color(.85f, .06f, .1f), emission: .45f, roughness: .35f, smooth: true);
            Part("knot", knot, new Color(.65f, .03f, .07f), emission: .6f, smooth: true);
        }

        // ---- helpers --------------------------------------------------------------------------------

        // The four materials of a face, the same for every character.
        void FaceParts(ProcMesh whites, ProcMesh pupils, ProcMesh dark, ProcMesh pink)
        {
            Part("eye whites", whites, Color.white, emission: .15f, roughness: .2f, smooth: true);
            Part("pupils", pupils, new Color(.1f, .07f, .05f), roughness: .15f, smooth: true);
            Part("brows and mouth", dark, new Color(.32f, .12f, .08f), smooth: true);
            Part("cheeks", pink, new Color(1f, .6f, .66f), smooth: true);
        }

        void Part(string group, ProcMesh shape, Color color, float emission = 0f, float transmission = 0f, float metallic = 0f, float roughness = .55f, bool smooth = false)
        {
            var mesh = smooth ? shape.BuildSmooth(key + " " + group) : shape.Build(key + " " + group);
            if (mesh.vertexCount == 0) { Dispose(mesh); return; }
            parts.Add(new ValuableArtPart
            {
                name = group, group = group, material = materials.Count,
                vertices = mesh.vertices, normals = mesh.normals, uv = mesh.uv, triangles = mesh.triangles,
                rest = new ArtPose { rotation = Quaternion.identity, scale = Vector3.one },
            });
            materials.Add(new ValuableArtMaterial { name = group, color = color, roughness = roughness, emission = emission, transmission = transmission, metallic = metallic });
            Dispose(mesh);
        }
    }

    private static void Dispose(Mesh mesh)
    {
        if (Application.isPlaying) UnityEngine.Object.Destroy(mesh);
        else UnityEngine.Object.DestroyImmediate(mesh);
    }

    private static void Box(ProcMesh mesh, float x, float y, float z, float w, float h, float d) =>
        mesh.ChamferBox(ProcMesh.TRS(new(x, y, z), Vector3.zero, new(w, h, d)), .11f);

    /// <summary>A rounded box turned about the Z axis (degrees), for necks, tails, roofs and anything that leans.</summary>
    private static void Tilt(ProcMesh mesh, float x, float y, float z, float w, float h, float d, float roll) =>
        mesh.ChamferBox(ProcMesh.TRS(new(x, y, z), new(0f, 0f, roll), new(w, h, d)), .11f);

    private static readonly Matrix4x4 Id = Matrix4x4.identity;
    private static readonly Color Ivory = new(.93f, .89f, .81f);

    /// <summary>A unit sphere (radius 0.5) scaled to <paramref name="size"/>, turned by <paramref name="euler"/>, at <paramref name="centre"/>.</summary>
    private static void Blob(ProcMesh mesh, Vector3 centre, Vector3 size, Vector3 euler = default)
    {
        const int steps = 8;
        var profile = new (float radius, float y)[steps + 1];
        for (int i = 0; i <= steps; i++) profile[i] = (.5f * Mathf.Sin(Mathf.PI * i / steps), -.5f * Mathf.Cos(Mathf.PI * i / steps));
        mesh.Lathe(ProcMesh.TRS(centre, euler, size), profile, 16);
    }

    /// <summary>A thin gold or painted ring around the Y axis at height <paramref name="y"/>.</summary>
    private static void Ring(ProcMesh mesh, float y, float radius, float thickness)
    {
        var points = new Vector3[40];
        for (int i = 0; i < points.Length; i++) { float a = i * Mathf.PI * 2f / points.Length; points[i] = new(radius * Mathf.Cos(a), y, radius * Mathf.Sin(a)); }
        mesh.Tube(Id, points, new[] { thickness }, 8, closed: true);
    }

    /// <summary>The radius of a lathe profile at height <paramref name="y"/> (the profile rises from bottom to top).</summary>
    private static float RadiusAt((float radius, float y)[] profile, float y)
    {
        for (int k = 0; k + 1 < profile.Length; k++)
        {
            var (r0, y0) = profile[k]; var (r1, y1) = profile[k + 1];
            if (y1 <= y0 || y < y0 || y > y1) continue;
            return Mathf.Lerp(r0, r1, (y - y0) / (y1 - y0));
        }
        return profile[profile.Length - 1].radius;
    }

    /// <summary>A five-petal flower painted on a round body, <paramref name="angle"/> degrees around Y (0 is +X).</summary>
    private static void Flower(ProcMesh petals, ProcMesh centre, float angle, float y, float radius)
    {
        float a = angle * Mathf.Deg2Rad;
        var outward = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
        var along = new Vector3(-Mathf.Sin(a), 0f, Mathf.Cos(a));
        var at = outward * (radius + .002f) + Vector3.up * y;
        var turn = new Vector3(0f, Mathf.Atan2(-outward.x, -outward.z) * Mathf.Rad2Deg, 0f);
        for (int k = 0; k < 5; k++)
        {
            float b = k * Mathf.PI * 2f / 5f;
            Blob(petals, at + (along * Mathf.Cos(b) + Vector3.up * Mathf.Sin(b)) * .016f, new(.018f, .018f, .006f), turn);
        }
        Blob(centre, at + outward * .002f, new(.01f, .01f, .006f), turn);
    }

    /// <summary>
    /// A cartoon face on the front (-Z) of a body: eyes with pupils and a glint, brows, rosy cheeks and a smile.
    /// <paramref name="front"/> gives the surface depth (negative Z) at (x, y); every feature sits on it and faces out.
    /// <paramref name="eye"/> is the eye height; the rest of the face is scaled from it.
    /// </summary>
    private static void Face(ProcMesh whites, ProcMesh pupils, ProcMesh dark, ProcMesh pink, System.Func<float, float, float> front,
        float eyeX, float eyeY, float eye, float mouthY, float mouthWidth)
    {
        Vector3 At(float x, float y, float lift) => new(x, y, front(x, y) - lift);
        Vector3 Turn(float x, float y)
        {
            const float h = .002f;
            float slope = (front(x + h, y) - front(x - h, y)) / (2f * h);
            return new Vector3(0f, -Mathf.Atan(slope) * Mathf.Rad2Deg, 0f);
        }
        foreach (float s in new[] { -1f, 1f })
        {
            float x = s * eyeX;
            Blob(whites, At(x, eyeY, eye * .08f), new(eye * .74f, eye, eye * .42f), Turn(x, eyeY));
            Blob(pupils, At(x * .93f, eyeY - eye * .08f, eye * .26f), new(eye * .38f, eye * .5f, eye * .2f), Turn(x, eyeY));
            Blob(whites, At(x * .93f + eye * .07f, eyeY + eye * .06f, eye * .36f), Vector3.one * eye * .13f, Turn(x, eyeY));
            dark.Tube(Id, new[] { At(x - s * eye * .42f, eyeY + eye * .62f, eye * .06f), At(x, eyeY + eye * .74f, eye * .06f), At(x + s * eye * .42f, eyeY + eye * .66f, eye * .06f) },
                new[] { eye * .055f }, 6, capped: true);
            float cheekX = s * (eyeX + eye * .62f), cheekY = (eyeY + mouthY) * .5f - eye * .1f;
            Blob(pink, At(cheekX, cheekY, eye * .03f), new(eye * .5f, eye * .32f, eye * .18f), Turn(cheekX, cheekY));
        }
        var smile = new Vector3[9];
        for (int i = 0; i < smile.Length; i++) { float t = -1f + 2f * i / (smile.Length - 1); smile[i] = At(mouthWidth * t, mouthY + mouthWidth * .42f * t * t, eye * .035f); }
        dark.Tube(Id, smile, new[] { eye * .07f }, 6, capped: true);
    }

    private static void Ball(ProcMesh mesh, float x, float y, float z, float radius)
    {
        const int steps = 5;
        var profile = new (float radius, float y)[steps + 1];
        for (int i = 0; i <= steps; i++)
            profile[i] = (radius * Mathf.Sin(Mathf.PI * i / steps), -radius * Mathf.Cos(Mathf.PI * i / steps));
        mesh.Lathe(ProcMesh.TRS(new(x, y, z), Vector3.zero, Vector3.one), profile, 8);
    }
}
