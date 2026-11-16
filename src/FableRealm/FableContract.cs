using System;
using System.Collections.Generic;

namespace IncreaseHardMod.FableRealm;

// The room contract written by tools/fable_realm/export_contract.py (src/FableRealm/fable_rooms.json).
// Plain data, no engine types: the same file is read by the mod and checked by tests/FableTests.
#pragma warning disable CS0649
internal sealed class FableAnchor
{
    public string id, kind, tag;
    /// <summary>Position, rotation (Euler degrees) and size in the room's own space; r and s may be absent.</summary>
    public float[] p, r, s;
    /// <summary>For nav anchors: ids of the nav anchors this point is linked to.</summary>
    public string[] to;
}

internal sealed class FableRoom
{
    public string id, role, special, ambient, fog;
    public int difficulty;
    public float fogDensity;
    /// <summary>Groups of wall plugs, "closed-&lt;portal id&gt;": shown only when that exit leads nowhere.</summary>
    public string[] variants;
    /// <summary>Element ids that are floor for the navmesh; every other collider is an obstacle.</summary>
    public string[] walkable;
    public FableAnchor[] anchors;
}

internal sealed class FableRoomFile
{
    public int schema;
    public string id;
    public FableRoom[] rooms;
}
#pragma warning restore CS0649

/// <summary>
/// Sorts the rooms into the lists of the game's Level asset. A room with a <c>special</c> tag goes to the
/// special list only, never to the list of its role, and the level asks the generator for one special tile:
/// that is what keeps the bean puzzle room to at most one per level.
/// </summary>
internal sealed class FableLevelPlan
{
    internal const int Difficulties = 3;
    internal readonly List<FableRoom> Start = new(), Special = new();
    internal readonly List<FableRoom>[] Normal = Tiers(), Passage = Tiers(), DeadEnd = Tiers(), Extraction = Tiers();
    /// <summary>How many special tiles the generator places (Level.ModulesSpecialAmount).</summary>
    internal int SpecialAmount => Special.Count > 0 ? 1 : 0;

    private static List<FableRoom>[] Tiers()
    {
        var tiers = new List<FableRoom>[Difficulties];
        for (int i = 0; i < Difficulties; i++) tiers[i] = new List<FableRoom>();
        return tiers;
    }

    internal static FableLevelPlan From(IEnumerable<FableRoom> rooms, out string error)
    {
        var plan = new FableLevelPlan();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        error = null;
        if (rooms != null)
            foreach (var room in rooms)
            {
                if (room == null || string.IsNullOrEmpty(room.id) || !ids.Add(room.id)) { error = "Duplicate or unnamed room."; return null; }
                if (!string.IsNullOrEmpty(room.special)) { plan.Special.Add(room); continue; }
                int tier = Math.Min(Difficulties, Math.Max(1, room.difficulty)) - 1;
                switch (room.role)
                {
                    case "Start": plan.Start.Add(room); break;
                    case "Normal": plan.Normal[tier].Add(room); break;
                    case "Passage": plan.Passage[tier].Add(room); break;
                    case "DeadEnd": plan.DeadEnd[tier].Add(room); break;
                    case "Extraction": plan.Extraction[tier].Add(room); break;
                    default: error = "Unknown role '" + room.role + "' of room " + room.id + "."; return null;
                }
            }
        // The generator stops without difficulty-1 normal modules and cannot finish a level without the rest.
        if (plan.Start.Count == 0) error = "No start room.";
        else if (plan.Normal[0].Count == 0) error = "No difficulty-1 normal room.";
        else if (Total(plan.Passage) == 0 || Total(plan.DeadEnd) == 0 || Total(plan.Extraction) == 0) error = "Passage, dead-end and extraction rooms are all required.";
        return error == null ? plan : null;
    }

    private static int Total(List<FableRoom>[] tiers)
    {
        int count = 0;
        foreach (var tier in tiers) count += tier.Count;
        return count;
    }

    /// <summary>The first anchor of a room with this id, or null.</summary>
    internal static FableAnchor Anchor(FableRoom room, string id)
    {
        if (room?.anchors != null)
            foreach (var anchor in room.anchors)
                if (anchor != null && anchor.id == id) return anchor;
        return null;
    }

    /// <summary>Checks that a bean-puzzle room carries every marker the puzzle code looks up.</summary>
    internal static bool PuzzleAnchorsComplete(FableRoom room, out string missing)
    {
        missing = null;
        foreach (string colour in BeanPuzzleRules.Colours)
            if (Anchor(room, BeanPuzzleRules.SocketPrefix + colour) == null) { missing = BeanPuzzleRules.SocketPrefix + colour; return false; }
        if (Anchor(room, BeanPuzzleRules.Landing) == null) { missing = BeanPuzzleRules.Landing; return false; }
        if (Anchor(room, BeanPuzzleRules.EggSpawn) == null) { missing = BeanPuzzleRules.EggSpawn; return false; }
        return true;
    }
}
