using UnityEngine;

namespace IncreaseHardMod.Monsters;

/// <summary>Short, physically checked low passages omitted by the shared humanoid navmesh.</summary>
internal sealed class BasiliskCrawl : MonoBehaviour
{
    private Enemy _enemy;
    private Transform _original, _waypoint;
    private float _until;

    internal static bool TryMove(Enemy enemy, Vector3 destination, float speed)
    {
        if (!SemiFunc.IsMasterClientOrSingleplayer() || !enemy.HasRigidbody || enemy.Rigidbody.rb == null) return false;
        var crawl = enemy.GetComponent<BasiliskCrawl>() ?? enemy.gameObject.AddComponent<BasiliskCrawl>();
        crawl._enemy = enemy;
        return crawl.Route(destination, speed);
    }

    private bool Route(Vector3 goal, float speed)
    {
        var body = _enemy.Rigidbody;
        var bounds = new Bounds(body.rb.worldCenterOfMass, Vector3.one * .01f);
        foreach (var c in body.GetComponentsInChildren<Collider>())
            if (c.enabled && !c.isTrigger) bounds.Encapsulate(c.bounds);
        var delta = goal - bounds.center; delta.y = 0f;
        float distance = Mathf.Min(delta.magnitude, 4f);
        if (distance < .15f) { Release(); return false; }
        var mask = SemiFunc.LayerMaskGetVisionObstruct();
        var direction = delta.normalized;
        // Sweep the real collider envelope, not the decorative tail. Chair legs and
        // crossbars remain solid, and a snake too large for an opening cannot enter it.
        if (Physics.BoxCast(bounds.center, bounds.extents + Vector3.one * .015f, direction, out _, Quaternion.identity,
                distance, mask, QueryTriggerInteraction.Ignore)) { Release(); return false; }
        bool roof = false;
        float floorY = bounds.min.y;
        for (float step = 0f; step <= distance + .1f; step += .2f)
        {
            var point = bounds.center + direction * Mathf.Min(step, distance);
            var probe = new Vector3(point.x, floorY + .18f, point.z);
            if (!Physics.Raycast(probe, Vector3.down, out var floor, .35f, mask, QueryTriggerInteraction.Ignore) ||
                floor.normal.y < .7f || Mathf.Abs(floor.point.y - floorY) > .15f)
            { Release(); return false; } // no pit crossing or walking on a chair/table top
            floorY = floor.point.y;
            roof |= Physics.Raycast(point + Vector3.up * bounds.extents.y, Vector3.up, 1.4f, mask, QueryTriggerInteraction.Ignore);
        }
        if (!roof) { Release(); return false; }
        if (_waypoint == null)
        {
            _original = body.followTarget;
            _waypoint = new GameObject("Basilisk low passage target").transform;
        }
        _waypoint.position = bounds.center + direction * Mathf.Min(distance, .6f);
        _waypoint.rotation = Quaternion.LookRotation(direction);
        body.followTarget = _waypoint;
        body.OverrideFollowPosition(.15f, speed, 20f);
        _enemy.NavMeshAgent?.Stop(.15f);
        _until = Time.time + .15f;
        return true;
    }

    private void LateUpdate()
    {
        if (Time.time >= _until || _enemy == null || _enemy.IsStunned() || _enemy.FreezeTimer > 0f ||
            (_enemy.HasRigidbody && _enemy.Rigidbody.grabbed)) Release();
    }

    private void Release()
    {
        if (_enemy != null && _waypoint != null && _enemy.Rigidbody.followTarget == _waypoint)
        {
            _enemy.Rigidbody.followTarget = _original;
            // Rejoin the normal route at the physical body's current position where
            // the baked mesh permits it. AgentMove checks membership before warping.
            if (_enemy.HasNavMeshAgent) _enemy.NavMeshAgent.AgentMove(_enemy.Rigidbody.rb.position);
        }
    }

    private void OnDisable() => Release();
    private void OnDestroy() { Release(); if (_waypoint != null) Destroy(_waypoint.gameObject); }
}
