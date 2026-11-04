using System;
using System.Collections.Generic;

namespace IncreaseHardMod.Valuables;

[Flags] internal enum LootMap { None=0,Sunstone=1,Swiftbroom=2,McJannek=4,Manor=8,Museum=16 }
internal enum LootFragility { Normal,Fragile,VeryFragile,Sturdy,Reinforced }
internal readonly struct LootDurability
{
    internal readonly float Fragility,Durability;
    internal LootDurability(float fragility,float durability){Fragility=fragility;Durability=durability;}
}
internal sealed class CollectionEntry
{
    internal readonly string Key;
    internal readonly LootMap Maps;
    internal readonly float Mass,Min,Max;
    internal readonly LootFragility Fragility;
    internal CollectionEntry(string key,LootMap maps,float mass,float min,float max,LootFragility fragility=LootFragility.Normal)
    {Key=key;Maps=maps;Mass=mass;Min=min;Max=max;Fragility=fragility;}
}
internal static class CollectionCatalog
{
    internal static readonly IReadOnlyList<CollectionEntry> Entries=new[]{
        new CollectionEntry("crystal_skull",LootMap.Sunstone|LootMap.Swiftbroom|LootMap.McJannek,.25f,2500,4000,LootFragility.VeryFragile),
        new CollectionEntry("bomb",LootMap.McJannek,9,28000,35000,LootFragility.Reinforced),
        new CollectionEntry("bath_gift_set",LootMap.Sunstone|LootMap.Museum|LootMap.Manor,.8f,2000,3000),
        new CollectionEntry("cannon",LootMap.Manor|LootMap.Sunstone|LootMap.Museum,8,15000,15000,LootFragility.Reinforced),
        new CollectionEntry("water_timer",LootMap.McJannek|LootMap.Swiftbroom|LootMap.Manor|LootMap.Museum,.65f,2200,3200,LootFragility.Fragile),
        new CollectionEntry("centrifuge",LootMap.McJannek,4.5f,7000,9500,LootFragility.Sturdy),
        new CollectionEntry("skeleton",LootMap.McJannek|LootMap.Sunstone|LootMap.Museum,1.8f,12000,15000,LootFragility.Sturdy),
        new CollectionEntry("robot_vacuum",LootMap.Manor|LootMap.Museum,3.5f,6000,8000,LootFragility.Sturdy),
        new CollectionEntry("graphics_card",LootMap.Museum|LootMap.McJannek,.75f,4500,5500,LootFragility.VeryFragile),
        new CollectionEntry("ram_pair",LootMap.Museum|LootMap.McJannek,.15f,4500,5500,LootFragility.VeryFragile),
        new CollectionEntry("monoblock_pc",LootMap.McJannek|LootMap.Museum|LootMap.Manor|LootMap.Sunstone,3,6000,8000),
        new CollectionEntry("gold_ingot",LootMap.Sunstone|LootMap.McJannek,3,3500,4800,LootFragility.Reinforced),
        new CollectionEntry("maneki_neko",LootMap.Museum|LootMap.Manor,6,12000,16000,LootFragility.Fragile)
    };
    internal static float SkeletonPartMass(int index)=>index<3?.16f:.06f;
    internal static float TotalMass(CollectionEntry entry)=>entry.Mass+(entry.Key=="skeleton"?3*SkeletonPartMass(0)+12*SkeletonPartMass(3):0);
    internal static LootDurability DurabilityFor(LootFragility kind)
    {
        switch(kind)
        {
            case LootFragility.VeryFragile:return new LootDurability(100,5);
            case LootFragility.Fragile:return new LootDurability(85,20);
            case LootFragility.Sturdy:return new LootDurability(22,85);
            case LootFragility.Reinforced:return new LootDurability(10,95);
            default:return new LootDurability(35,65);
        }
    }
    internal static LootMap Identify(string assetName,string narrative)
    {
        // Verified against the installed Level assets. Never classify shop/arena/custom levels by substring.
        switch(assetName)
        {
            case "Level - Desert":return LootMap.Sunstone;
            case "Level - Wizard":return LootMap.Swiftbroom;
            case "Level - Arctic":return LootMap.McJannek;
            case "Level - Manor":return LootMap.Manor;
            case "Level - Museum":return LootMap.Museum;
            default:return LootMap.None;
        }
    }
    internal static bool Allows(CollectionEntry entry,LootMap map)=>map!=LootMap.None&&(entry.Maps&map)==map;
}
