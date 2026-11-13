using System.Collections.Generic;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

namespace IncreaseHardMod.Upgrades;

/// <summary>
/// Upgrade Shelf's extra shelves: when the team owns Upgrade Shelf, the host copies the shop's own upgrade shelf (the
/// game's model and colliders, none of its scripts) and stands the copies with their backs to a free stretch of
/// wall near the original, as many as the extra upgrades need. Each copy gets upgrade slots where the original has
/// them, and the shop stocking fills those. Every client makes the same copies of the same shelf in its own scene
/// from the host's list (event <see cref="EventCode"/>), sent again when someone joins. The copies belong to the shop
/// scene and go with it. No other mod's shelves are needed or used.
/// </summary>
internal sealed class UpgradeShelfRack : MonoBehaviour, IOnEventCallback
{
    internal const byte EventCode = 151;
    private const int MaxCopies = 4;
    private const float WallGap = .03f, FrontClearance = 1.4f;

    /// <summary>One copy: which shelf of the scene it copies (name and position) and where it stands.</summary>
    private struct Copy { public string Source; public Vector3 SourcePos, Pos; public float Yaw; public GameObject Built; }

    private static readonly List<Copy> Copies = new();
    private static float _receivedAt;
    private static int _sentPlayers = -1;

    private void OnEnable() => PhotonNetwork.AddCallbackTarget(this);
    private void OnDisable() => PhotonNetwork.RemoveCallbackTarget(this);

    // ------------------------------------------------------------------ host

    /// <summary>Host, shop stocking: stand copies of the shop's upgrade shelf and return up to <paramref name="wanted"/> upgrade slots on them.</summary>
    internal static List<ItemVolume> HostBuild(ShopManager shop, int wanted)
    {
        var slots = new List<ItemVolume>();
        Copies.Clear(); _sentPlayers = -1;
        if (wanted <= 0 || shop == null) return slots;

        if (!FindSourceShelf(shop, out var source, out var slotPoses))
        {
            Plugin.Log.LogWarning("[Shop] No upgrade shelf found to copy for Upgrade Shelf; the extra upgrades use free slots only.");
            return slots;
        }
        var bounds = LocalBounds(source);
        var back = BackAxis(source, bounds);
        for (int copy = 0; copy < MaxCopies && slots.Count < wanted; copy++)
        {
            if (!FindWallSpot(source, bounds, back, out var pos, out float yaw)) break;
            var built = Instantiate(source, pos, yaw);
            if (built == null) break;
            Copies.Add(new Copy { Source = source.name, SourcePos = source.position, Pos = pos, Yaw = yaw, Built = built });
            var turn = Quaternion.Euler(0f, yaw, 0f);
            foreach (var pose in slotPoses)
            {
                if (slots.Count >= wanted) break;
                var slot = new GameObject("IHM Upgrade Shelf Slot");
                slot.transform.SetPositionAndRotation(pos + turn * pose.position, turn * pose.rotation);
                slot.transform.SetParent(built.transform, true);
                var volume = slot.AddComponent<ItemVolume>();
                volume.itemVolume = SemiFunc.itemVolume.upgrade;
                volume.itemSecretShopType = SemiFunc.itemSecretShopType.none;
                slots.Add(volume);
            }
        }
        if (Copies.Count == 0)
            Plugin.Log.LogWarning("[Shop] No free wall for an extra upgrade shelf; the extra upgrades use free slots only.");
        else
            Plugin.Log.LogInfo($"[Shop] Upgrade Shelf: {Copies.Count} copy(ies) of '{source.name}' with {slots.Count} slot(s).");
        Broadcast();
        return slots;
    }

    /// <summary>
    /// The furniture the shop's upgrade slots sit on: from each upgrade slot, the largest furniture-sized ancestor (or,
    /// when the slot is not inside the shelf, the object under it). The shelf holding the most slots wins; its slots'
    /// poses are returned in its own space.
    /// </summary>
    private static bool FindSourceShelf(ShopManager shop, out Transform best, out List<Pose> poses)
    {
        best = null; poses = new List<Pose>();
        var counts = new Dictionary<Transform, List<Pose>>();
        foreach (var volume in shop.itemVolumes)
        {
            if (volume == null || volume.itemVolume != SemiFunc.itemVolume.upgrade) continue;
            var shelf = FurnitureAbove(volume.transform) ?? FurnitureUnder(volume.transform.position);
            if (shelf == null) continue;
            if (!counts.TryGetValue(shelf, out var list)) counts[shelf] = list = new List<Pose>();
            list.Add(new Pose(Flat(shelf, volume.transform.position), Quaternion.Inverse(shelf.rotation) * volume.transform.rotation));
        }
        foreach (var pair in counts)
            if (pair.Value.Count > poses.Count) { best = pair.Key; poses = pair.Value; }
        return best != null;
    }

    // 1 = furniture-sized with a collider, 0 = nothing drawn here (keep looking up), -1 = too big (a room or module).
    private static int Furniture(Transform t)
    {
        var b = WorldBounds(t);
        if (!b.HasValue) return 0;
        var s = b.Value.size;
        if (s.x >= 4.5f || s.z >= 4.5f || s.y >= 3.5f) return -1;
        return t.GetComponentInChildren<Collider>() != null ? 1 : 0;
    }

    private static Transform FurnitureAbove(Transform slot)
    {
        Transform found = null;
        for (var t = slot.parent; t != null; t = t.parent)
        {
            int kind = Furniture(t);
            if (kind < 0) break;
            if (kind > 0) found = t;
        }
        return found;
    }

    private static Transform FurnitureUnder(Vector3 point)
    {
        if (!Physics.Raycast(point + Vector3.up * .1f, Vector3.down, out var hit, 1f, ~0, QueryTriggerInteraction.Ignore)) return null;
        if (hit.collider.GetComponentInParent<PhysGrabObject>() != null) return null; // an item, not furniture
        Transform found = null;
        for (var t = hit.collider.transform; t != null; t = t.parent)
        {
            int kind = Furniture(t);
            if (kind < 0) break;
            if (kind > 0) found = t;
        }
        return found;
    }

    private static Bounds? WorldBounds(Transform t)
    {
        Bounds? total = null;
        foreach (var r in t.GetComponentsInChildren<Renderer>())
        {
            if (!r.enabled || r is ParticleSystemRenderer) continue;
            if (total == null) total = r.bounds; else { var b = total.Value; b.Encapsulate(r.bounds); total = b; }
        }
        return total;
    }

    /// <summary>A world point in the shelf's frame: turned with the shelf, but in metres (its scale included).</summary>
    private static Vector3 Flat(Transform shelf, Vector3 world) => Quaternion.Inverse(shelf.rotation) * (world - shelf.position);

    /// <summary>The shelf's box in its own frame, in metres (from its renderers' corners).</summary>
    private static Bounds LocalBounds(Transform shelf)
    {
        var bounds = new Bounds(Vector3.zero, Vector3.zero); bool any = false;
        foreach (var r in shelf.GetComponentsInChildren<Renderer>())
        {
            if (!r.enabled || r is ParticleSystemRenderer) continue;
            var b = r.bounds;
            for (int i = 0; i < 8; i++)
            {
                var corner = new Vector3((i & 1) == 0 ? b.min.x : b.max.x, (i & 2) == 0 ? b.min.y : b.max.y, (i & 4) == 0 ? b.min.z : b.max.z);
                var local = Flat(shelf, corner);
                if (!any) { bounds = new Bounds(local, Vector3.zero); any = true; } else bounds.Encapsulate(local);
            }
        }
        return bounds;
    }

    /// <summary>Which of the shelf's own horizontal sides is its back: the one nearest a wall in the shop.</summary>
    private static Vector3 BackAxis(Transform shelf, Bounds local)
    {
        var axes = new[] { Vector3.forward, Vector3.back, Vector3.right, Vector3.left };
        var centre = shelf.position + shelf.rotation * local.center;
        int mask = SemiFunc.LayerMaskGetVisionObstruct();
        float best = float.MaxValue; var back = Vector3.back;
        foreach (var axis in axes)
        {
            var dir = shelf.rotation * axis; dir.y = 0f; dir.Normalize();
            float reach = Mathf.Abs(Vector3.Dot(local.extents, new Vector3(Mathf.Abs(axis.x), 0f, Mathf.Abs(axis.z))));
            // Skip the shelf itself: start the ray just outside its side.
            if (Physics.Raycast(centre + dir * (reach + .02f), dir, out var hit, 1.5f, mask, QueryTriggerInteraction.Ignore) && hit.distance < best)
            { best = hit.distance; back = axis; }
        }
        return back;
    }

    /// <summary>
    /// A free stretch of wall near the original shelf: the copy stands with its back <see cref="WallGap"/> off the
    /// wall, on level floor, in empty space, with room to walk up to its front.
    /// </summary>
    private static bool FindWallSpot(Transform source, Bounds local, Vector3 back, out Vector3 pos, out float yaw)
    {
        pos = default; yaw = 0f;
        int mask = SemiFunc.LayerMaskGetVisionObstruct();
        var origin = source.position;
        var candidates = new List<Vector3>();
        for (float r = 1.5f; r <= 12f; r += .75f)
            for (int a = 0; a < 16; a++)
            {
                float rad = a * Mathf.PI / 8f;
                candidates.Add(origin + new Vector3(Mathf.Cos(rad), 0f, Mathf.Sin(rad)) * r);
            }
        var points = SemiFunc.LevelPointsGetAll();
        if (points != null) foreach (var p in points) if (p != null && !p.Truck) candidates.Add(p.transform.position);
        candidates.Sort((a, b) => (a - origin).sqrMagnitude.CompareTo((b - origin).sqrMagnitude));

        float backReach = Mathf.Abs(Vector3.Dot(local.extents, new Vector3(Mathf.Abs(back.x), 0f, Mathf.Abs(back.z))));
        var half = local.extents - new Vector3(.04f, .04f, .04f);
        foreach (var c in candidates)
        {
            if (!Physics.Raycast(c + Vector3.up * 1.2f, Vector3.down, out var floorHit, 3f, mask, QueryTriggerInteraction.Ignore)) continue;
            if (Vector3.Angle(floorHit.normal, Vector3.up) > 8f) continue;
            var chest = floorHit.point + Vector3.up * .9f;
            for (int k = 0; k < 4; k++)
            {
                var dir = Quaternion.Euler(0f, k * 90f + source.eulerAngles.y, 0f) * Vector3.forward; // the shop's grid
                if (!Physics.Raycast(chest, dir, out var wall, 2.5f, mask, QueryTriggerInteraction.Ignore)) continue;
                if (Vector3.Dot(wall.normal, -dir) < .85f) continue; // not a flat wall facing us
                // Turned so the shelf's back faces the wall (the copy stands upright, like the original).
                float turn = Vector3.SignedAngle(back, dir, Vector3.up);
                var rot = Quaternion.Euler(0f, turn, 0f);
                var centreFlat = wall.point - dir * (backReach + WallGap);
                var p = centreFlat - rot * new Vector3(local.center.x, 0f, local.center.z);
                p.y = floorHit.point.y - local.min.y;
                var worldCentre = p + rot * local.center + Vector3.up * .03f;
                if (Physics.CheckBox(worldCentre, half, rot, ~0, QueryTriggerInteraction.Ignore)) continue;
                // Room in front to walk up and pick from it.
                var front = worldCentre - dir * (backReach + .05f);
                if (Physics.Raycast(new Vector3(front.x, floorHit.point.y + .9f, front.z), -dir, FrontClearance, mask, QueryTriggerInteraction.Ignore)) continue;
                pos = p; yaw = turn;
                return true;
            }
        }
        return false;
    }

    // ------------------------------------------------------------------ copies (host and clients)

    /// <summary>The game's shelf, copied: its meshes and colliders only (scripts, item slots and network views removed).</summary>
    private static GameObject Instantiate(Transform source, Vector3 pos, float yaw)
    {
        var copy = Object.Instantiate(source.gameObject, pos, Quaternion.Euler(0f, yaw, 0f));
        copy.name = "IHM Upgrade Shelf (" + source.name + ")";
        copy.transform.localScale = source.lossyScale;
        var scripts = copy.GetComponentsInChildren<MonoBehaviour>(true);
        for (int i = scripts.Length - 1; i >= 0; i--)
            if (scripts[i] != null) Object.DestroyImmediate(scripts[i]);
        // Anything that would move or be grabbed stays out: the copy is plain furniture.
        foreach (var body in copy.GetComponentsInChildren<Rigidbody>(true)) Object.DestroyImmediate(body);
        Physics.SyncTransforms();
        return copy;
    }

    private void Update()
    {
        if (Copies.Count == 0) return;
        bool host = SemiFunc.IsMasterClientOrSingleplayer();
        if (!SemiFunc.RunIsShop())
        {
            // Left the shop. A client may hear the host before its own shop has loaded, so it waits a while.
            if (host || Time.time - _receivedAt > 30f) Copies.Clear();
            return;
        }
        if (!host && LevelGenerator.Instance != null && LevelGenerator.Instance.Generated)
            for (int i = 0; i < Copies.Count; i++)
            {
                var c = Copies[i];
                if (c.Built != null) continue;
                var source = FindSource(c.Source, c.SourcePos);
                if (source == null) continue;
                c.Built = Instantiate(source, c.Pos, c.Yaw);
                Copies[i] = c;
            }
        if (host && PhotonNetwork.InRoom && PhotonNetwork.CurrentRoom.PlayerCount != _sentPlayers) Broadcast();
    }

    // The same shelf in this client's scene: same name, same place.
    private static Transform FindSource(string name, Vector3 at)
    {
        Transform best = null; float bestD = .5f;
        foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
        {
            if (t.name != name || t.root.name.StartsWith("IHM Upgrade Shelf", System.StringComparison.Ordinal)) continue; // not one of our copies
            float d = Vector3.Distance(t.position, at);
            if (d < bestD) { bestD = d; best = t; }
        }
        return best;
    }

    private static void Broadcast()
    {
        if (!PhotonNetwork.InRoom || !PhotonNetwork.IsMasterClient || Copies.Count == 0) return;
        _sentPlayers = PhotonNetwork.CurrentRoom.PlayerCount;
        var names = new string[Copies.Count];
        var data = new float[Copies.Count * 7];
        for (int i = 0; i < Copies.Count; i++)
        {
            var c = Copies[i];
            names[i] = c.Source;
            data[i * 7] = c.SourcePos.x; data[i * 7 + 1] = c.SourcePos.y; data[i * 7 + 2] = c.SourcePos.z;
            data[i * 7 + 3] = c.Pos.x; data[i * 7 + 4] = c.Pos.y; data[i * 7 + 5] = c.Pos.z; data[i * 7 + 6] = c.Yaw;
        }
        PhotonNetwork.RaiseEvent(EventCode, new object[] { names, data },
            new RaiseEventOptions { Receivers = ReceiverGroup.Others }, SendOptions.SendReliable);
    }

    public void OnEvent(EventData ev)
    {
        if (ev.Code != EventCode || PhotonNetwork.MasterClient == null || ev.Sender != PhotonNetwork.MasterClient.ActorNumber) return;
        if (ev.CustomData is not object[] d || d.Length != 2 || d[0] is not string[] names || d[1] is not float[] f ||
            names.Length > MaxCopies || f.Length != names.Length * 7) return;
        foreach (float v in f) if (float.IsNaN(v) || float.IsInfinity(v) || Mathf.Abs(v) > 100000f) return;
        foreach (string n in names) if (string.IsNullOrEmpty(n) || n.Length > 128) return;
        // Already standing (a resend for a newcomer): keep them.
        if (Copies.Count == names.Length && Copies.TrueForAll(c => c.Built != null)) return;
        foreach (var c in Copies) if (c.Built != null) Destroy(c.Built);
        Copies.Clear();
        for (int i = 0; i < names.Length; i++)
            Copies.Add(new Copy
            {
                Source = names[i], SourcePos = new Vector3(f[i * 7], f[i * 7 + 1], f[i * 7 + 2]),
                Pos = new Vector3(f[i * 7 + 3], f[i * 7 + 4], f[i * 7 + 5]), Yaw = f[i * 7 + 6],
            });
        _receivedAt = Time.time;
    }
}
