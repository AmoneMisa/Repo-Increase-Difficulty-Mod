using System.Collections.Generic;
using UnityEngine;

namespace IncreaseHardMod.Drones;

/// <summary>Shared, renewable player-collision lease for Ghost and linked cargo. Never ignores world geometry.</summary>
internal sealed class PlayerCargoPassThrough : MonoBehaviour
{
    private float _until, _refresh;
    private readonly List<(Collider Cargo, Collider Player)> _pairs = new();

    internal static void Keep(PhysGrabObject cargo)
    {
        if (cargo == null || cargo.isPlayer || cargo.GetComponentInParent<EnemyRigidbody>() != null) return;
        var lease = cargo.GetComponent<PlayerCargoPassThrough>() ?? cargo.gameObject.AddComponent<PlayerCargoPassThrough>();
        lease._until = Time.time + .65f;
    }

    private void FixedUpdate()
    {
        if (Time.time < _until)
        {
            if (Time.time < _refresh) return;
            _refresh = Time.time + .2f;
            var cargo = GetComponentsInChildren<Collider>();
            foreach (var player in Players())
                foreach (var c in cargo)
                {
                    if (c == null || !c.enabled || c.isTrigger || player == c || Physics.GetIgnoreCollision(c, player)) continue;
                    Physics.IgnoreCollision(c, player, true);
                    _pairs.Add((c, player));
                }
            return;
        }
        Restore(false);
    }

    private static IEnumerable<Collider> Players()
    {
        if (PlayerController.instance != null)
            foreach (var c in PlayerController.instance.GetComponentsInChildren<Collider>(true))
                if (c != null && c.enabled && !c.isTrigger) yield return c;
        if (GameDirector.instance == null) yield break;
        foreach (var p in GameDirector.instance.PlayerList)
        {
            if (p == null) continue;
            foreach (var c in p.GetComponentsInChildren<Collider>(true))
                if (c != null && c.enabled && !c.isTrigger) yield return c;
            if (p.tumble?.colliders == null) continue;
            foreach (var c in p.tumble.colliders)
                if (c != null && c.enabled && !c.isTrigger) yield return c;
        }
    }

    private void Restore(bool force)
    {
        for (int i = _pairs.Count - 1; i >= 0; i--)
        {
            var (cargo, player) = _pairs[i];
            if (cargo != null && player != null)
            {
                if (!force && cargo.enabled && player.enabled && cargo.bounds.Intersects(player.bounds)) continue;
                Physics.IgnoreCollision(cargo, player, false);
            }
            _pairs.RemoveAt(i);
        }
    }
    private void OnDisable() => Restore(true);
}

internal static class LinkConvoy
{
    private static float _next;
    internal static void Tick()
    {
        if (Time.time < _next) return;
        _next = Time.time + .2f;
        var roots = new List<int>();
        var cargo = new Dictionary<int, PhysGrabObject>();
        var graph = new List<(int, int)>();
        var edges = new List<(PhysGrabObject A, PhysGrabObject B, PlayerAvatar PA, PlayerAvatar PB)>(LinkManager.CargoLinks());
        foreach (var edge in edges)
        {
            if (edge.A != null) cargo[edge.A.GetInstanceID()] = edge.A;
            if (edge.B != null) cargo[edge.B.GetInstanceID()] = edge.B;
            if (Skills.SkillEffects.Has(edge.PA, Skills.SkillCatalog.GhostConvoy) && edge.B != null) roots.Add(edge.B.GetInstanceID());
            if (Skills.SkillEffects.Has(edge.PB, Skills.SkillCatalog.GhostConvoy) && edge.A != null) roots.Add(edge.A.GetInstanceID());
            if (edge.A != null && edge.B != null) graph.Add((edge.A.GetInstanceID(), edge.B.GetInstanceID()));
        }
        foreach (int id in LinkRouteRules.ConnectedCargo(graph, roots))
            if (cargo.TryGetValue(id, out var item)) PlayerCargoPassThrough.Keep(item);
    }
}
