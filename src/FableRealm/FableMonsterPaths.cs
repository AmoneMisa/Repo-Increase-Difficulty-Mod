using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using UnityEngine.AI;

namespace IncreaseHardMod.FableRealm;

/// <summary>
/// Monster pathing on Fable Realm. Monsters spawn on level points and walk between them on the level's NavMesh,
/// which the game builds for its "Enemy" agent (0.6 m radius). The rooms are checked for that agent when they are
/// authored (Fable Realm → Check monster paths), but the generator joins rooms in combinations no check sees.
///
/// Right after the game has built and validated the NavMesh, and before it spawns players or monsters, every level
/// point is tested with the agent the NavMesh was built for: it must lie on the NavMesh and have a complete path
/// from the truck. Points that fail are taken out of the level's point list, so no monster spawns where it cannot
/// move or is sent somewhere it cannot reach. The result is logged once per level.
/// </summary>
internal static class FableMonsterPaths
{
    private const float GameSample = .5f; // LevelPoint.NavMeshCheck samples this far around a point

    [HarmonyPatch(typeof(LevelGenerator), nameof(LevelGenerator.NavMeshBuildValidate))]
    private static class AfterNavMesh
    {
        [HarmonyPostfix]
        private static void Postfix(LevelGenerator __instance)
        {
            if (!FableLevel.IsCurrent || __instance.navMeshBuildState != LevelGenerator.NavMeshBuildState.Ready) return;
            try { Audit(__instance); }
            catch (System.Exception error) { Plugin.Log.LogWarning("[Fable] Monster path check skipped: " + error.Message); }
        }
    }

    private static void Audit(LevelGenerator generator)
    {
        var points = generator.LevelPathPoints;
        var surface = generator.NavMeshSurface;
        if (points == null || points.Count == 0 || surface == null) return;
        var filter = new NavMeshQueryFilter { agentTypeID = surface.agentTypeID, areaMask = NavMesh.AllAreas };
        string agent = NavMesh.GetSettingsNameFromID(surface.agentTypeID);

        // The truck point, or failing that any start-room point on the NavMesh, is where the players are.
        Vector3 origin = default; bool found = false;
        foreach (var candidate in Candidates(generator))
            if (candidate != null && NavMesh.SamplePosition(candidate.transform.position, out var hit, GameSample, filter)) { origin = hit.position; found = true; break; }
        if (!found) { Plugin.Log.LogWarning($"[Fable] Monster paths ({agent}): no truck or start-room level point on the NavMesh; nothing checked."); return; }

        var bad = new List<(LevelPoint point, string why)>();
        var path = new NavMeshPath();
        foreach (var point in points)
        {
            if (point == null) continue;
            if (!NavMesh.SamplePosition(point.transform.position, out var hit, GameSample, filter)) { bad.Add((point, "off the NavMesh")); continue; }
            if (!NavMesh.CalculatePath(origin, hit.position, filter, path) || path.status != NavMeshPathStatus.PathComplete) bad.Add((point, "no path from the truck"));
        }
        if (bad.Count == 0) { Plugin.Log.LogInfo($"[Fable] Monster paths ({agent}): all {points.Count} level points are on the NavMesh and reachable from the truck."); return; }

        bool prune = FableMonsterPathRules.ShouldPrune(points.Count, bad.Count);
        var report = new List<string>();
        foreach (var (point, why) in bad)
        {
            var module = point.GetComponentInParent<Module>();
            if (report.Count < 12)
                report.Add($"{point.name} in {(module != null ? module.name : "?")} at {(module != null ? module.transform.InverseTransformPoint(point.transform.position) : point.transform.position):F1}: {why}");
            if (!prune || point == generator.LevelPathTruck) continue;
            points.Remove(point);
            foreach (var other in points) other?.ConnectedPoints?.Remove(point);
            point.ConnectedPoints?.Clear();
        }
        Plugin.Log.LogWarning($"[Fable] Monster paths ({agent}): {bad.Count} of {points.Count + (prune ? bad.Count : 0)} level points " +
            (prune ? "removed, so no monster spawns or walks there" : "fail, too many to be a room problem; kept") + ":\n  " + string.Join("\n  ", report) +
            (bad.Count > report.Count ? $"\n  ... and {bad.Count - report.Count} more." : ""));
    }

    private static IEnumerable<LevelPoint> Candidates(LevelGenerator generator)
    {
        yield return generator.LevelPathTruck;
        foreach (var point in generator.LevelPathPoints)
            if (point != null && point.inStartRoom) yield return point;
    }
}
