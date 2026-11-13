using Photon.Pun;
using UnityEngine;
using UnityEngine.AI;

namespace IncreaseHardMod.Courier;

internal sealed class CourierHeadRoute : MonoBehaviour
{
    internal const int MaxRooms = 3, MaxCorners = 512;
    private static readonly Collider[] RoomHits = new Collider[32];
    private Material _material;
    private PlayerDeathHead _head;
    private LineRenderer _line;
    private float _until, _repathAt;

    internal static bool Active(PlayerDeathHead head) => head != null && head.isActiveAndEnabled && head.setup && head.triggered &&
        head.playerAvatar != null && head.playerAvatar.isDisabled && head.physGrabObject != null && !head.physGrabObject.dead;

    internal static Vector3[] Path(Vector3 from, Vector3 to)
    {
        if (!NavMesh.SamplePosition(from, out var start, 1.6f, NavMesh.AllAreas) ||
            !NavMesh.SamplePosition(to, out var end, 1.6f, NavMesh.AllAreas)) return null;
        var path = new NavMeshPath();
        return NavMesh.CalculatePath(start.position, end.position, NavMesh.AllAreas, path) &&
               path.status == NavMeshPathStatus.PathComplete ? path.corners : null;
    }

    internal static float Length(Vector3[] path)
    {
        float length = 0f;
        for (int i = 1; i < path.Length; i++) length += Vector3.Distance(path[i - 1], path[i]);
        return length;
    }

    internal static bool WithinRoomLimit(Vector3[] path)
    {
        if (path == null || path.Length == 0) return false;
        int mask = LayerMask.GetMask("RoomVolume");
        var room = RoomAt(path[0], mask, null);
        if (room == null || RoomAt(path[^1], mask, null) == null) return false;
        int rooms = 0;
        for (int i = 1; i < path.Length; i++)
        {
            int samples = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(path[i - 1], path[i]) / .35f));
            for (int s = 1; s <= samples; s++)
            {
                var next = RoomAt(Vector3.Lerp(path[i - 1], path[i], (float)s / samples), mask, room);
                if (next == null || next == room) continue;
                if (++rooms > MaxRooms) return false;
                room = next;
            }
        }
        return true;
    }

    /// <summary>
    /// Where a path first leaves the room it starts in: the first point in the next room, how far along the path it
    /// is and which way the path heads there. False if the path never leaves its first room or starts in no room.
    /// </summary>
    internal static bool FirstDoorway(Vector3[] path, out Vector3 door, out Vector3 heading, out float along)
    {
        door = heading = Vector3.zero; along = 0f;
        if (path == null || path.Length < 2) return false;
        int mask = LayerMask.GetMask("RoomVolume");
        var room = RoomAt(path[0], mask, null);
        if (room == null) return false;
        for (int i = 1; i < path.Length; i++)
        {
            float segment = Vector3.Distance(path[i - 1], path[i]);
            int samples = Mathf.Max(1, Mathf.CeilToInt(segment / .35f));
            for (int s = 1; s <= samples; s++)
            {
                var point = Vector3.Lerp(path[i - 1], path[i], (float)s / samples);
                var next = RoomAt(point, mask, room);
                if (next == null || next == room) continue;
                door = point; heading = path[i] - path[i - 1]; heading.y = 0f;
                along += segment * s / samples;
                return heading.sqrMagnitude > .0001f;
            }
            along += segment;
        }
        return false;
    }

    private static RoomVolume RoomAt(Vector3 point, int mask, RoomVolume previous)
    {
        int count = Physics.OverlapSphereNonAlloc(point + Vector3.up * .25f, .06f, RoomHits, mask, QueryTriggerInteraction.Collide);
        RoomVolume best = null;
        for (int i = 0; i < count; i++)
        {
            var room = RoomHits[i].GetComponentInParent<RoomVolume>();
            RoomHits[i] = null;
            if (room == null) continue;
            if (best == null || (room.priorityVolume && !best.priorityVolume) ||
                (room.priorityVolume == best.priorityVolume && (room.priority > best.priority ||
                 (room.priority == best.priority && room == previous)))) best = room;
        }
        return best;
    }

    internal void Show(int headView, Vector3[] path)
    {
        var view = PhotonView.Find(headView);
        _head = view != null ? view.GetComponent<PlayerDeathHead>() : null;
        _until = Time.time + 25f;
        _repathAt = Time.time + .75f;
        Draw(path);
    }

    internal void Clear()
    {
        _until = 0f;
        _head = null;
        if (_line != null) _line.enabled = false;
    }

    private void Update()
    {
        if (_until == 0f) return;
        var local = PlayerAvatar.instance;
        if (Time.time >= _until || local == null || local.isDisabled || (_head != null && !Active(_head))) { Clear(); return; }
        if (_head == null || Time.time < _repathAt) return;
        _repathAt = Time.time + .75f;
        // The host's route remains visible on clients whose NavMesh is not available.
        var path = Path(local.transform.position, _head.physGrabObject.midPoint);
        if (path != null) Draw(path);
    }

    private void Draw(Vector3[] path)
    {
        if (_line == null)
        {
            var shader = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color");
            if (shader == null) return;
            _material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            _line = gameObject.AddComponent<LineRenderer>();
            _line.sharedMaterial = _material;
            _line.widthMultiplier = .11f;
            _line.numCornerVertices = 3;
            _line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _line.receiveShadows = false;
            _line.startColor = new Color(.35f, .94f, 1f, .7f);
            _line.endColor = new Color(.35f, .94f, 1f, 1f);
        }
        _line.positionCount = Mathf.Max(2, path.Length);
        for (int i = 0; i < path.Length; i++) _line.SetPosition(i, path[i] + Vector3.up * .12f);
        if (path.Length == 1) _line.SetPosition(1, path[0] + Vector3.up * .55f);
        _line.enabled = path.Length > 0;
    }

    private void OnDestroy() { if (_material != null) Destroy(_material); }
}
