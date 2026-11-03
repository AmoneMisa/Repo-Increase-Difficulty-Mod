using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;

namespace IncreaseHardMod.Patches;

// Native CheckForLeftPlayers reuses the original line/visibility arrays when players join.
// Repair before Update (which reads them before CheckForLeftPlayers) and incoming state RPCs.
[HarmonyPatch]
internal static class GumballRosterPatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        foreach(string name in new[]{"Update","CheckForLeftPlayers","PlayerStateChangedRPC"})
            yield return AccessTools.DeclaredMethod(typeof(GumballValuable),name);
    }

    private static bool Prefix(GumballValuable __instance, MethodBase __originalMethod,
        ref int ___playerCount, ref List<PlayerAvatar> ___allPlayers,
        ref List<bool> ___playerInSight, ref List<bool> ___previouslySeenList,
        ref List<GameObject> ___hypnosisLines)
    {
        Reconcile(__instance,SemiFunc.PlayerGetList(),ref ___playerCount,ref ___allPlayers,
            ref ___playerInSight,ref ___previouslySeenList,ref ___hypnosisLines);
        return __originalMethod.Name!="CheckForLeftPlayers";
    }

    internal static void Reconcile(GumballValuable trap,List<PlayerAvatar> roster,
        ref int count,ref List<PlayerAvatar> players,ref List<bool> inSight,
        ref List<bool> seen,ref List<GameObject> lines,System.Action<GameObject> destroy=null)
    {
        int size=roster?.Count??0;
        bool changed=players==null||ReferenceEquals(players,roster)||count!=size||players.Count!=size
            ||inSight==null||inSight.Count!=size||seen==null||seen.Count!=size||lines==null||lines.Count!=size;
        if(!changed)
            for(int i=0;i<size;i++)
                if(roster[i]==null||players[i]!=roster[i]||lines[i]==null){changed=true;break;}
        if(!changed)return;

        var nextPlayers=new List<PlayerAvatar>();var nextLines=new List<GameObject>();
        var nextSight=new List<bool>();var nextSeen=new List<bool>();
        if(roster!=null)foreach(var player in roster)
        {
            if(player==null)continue;
            int old=players?.IndexOf(player)??-1;
            var line=old>=0&&lines!=null&&old<lines.Count?lines[old]:null;
            if(line==null)
            {
                line=Object.Instantiate(trap.hypnosisLine,trap.HypnosisLinesParenTransform);
                line.GetComponent<EyeLines>().InitializeLine(player);
                line.GetComponent<EyeLines>().SetIsActive(false);
                line.SetActive(false);
            }
            bool preserve=old>=0&&!ReferenceEquals(players,roster);
            nextPlayers.Add(player);nextLines.Add(line);
            nextSight.Add(preserve&&inSight!=null&&old<inSight.Count&&inSight[old]);
            nextSeen.Add(preserve&&seen!=null&&old<seen.Count&&seen[old]);
        }
        if(lines!=null)foreach(var line in lines)
            if(line!=null&&!nextLines.Contains(line)){line.SetActive(false);if(destroy!=null)destroy(line);else Object.Destroy(line);}
        // Own a snapshot: the game's shared roster can mutate in place, including same-count swaps.
        players=nextPlayers;lines=nextLines;inSight=nextSight;seen=nextSeen;count=players.Count;
    }
}
