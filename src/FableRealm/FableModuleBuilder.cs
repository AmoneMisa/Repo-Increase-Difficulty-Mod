using System;
using System.Collections.Generic;
using Photon.Pun;
using Unity.AI.Navigation;
using UnityEngine;
using Object = UnityEngine.Object;

namespace IncreaseHardMod.FableRealm;

/// <summary>Vanilla objects cloned into the Fable Realm rooms, taken from a level the game already ships.</summary>
internal sealed class FableDonor
{
    /// <summary>The truck of a vanilla start room (shelves, screen, door, spawn points, its own room volume).</summary>
    internal GameObject Truck;
    internal Vector3 TruckPosition;
    internal Quaternion TruckRotation;
    internal GameObject Extraction;
    /// <summary>A vanilla room volume whose reverb, ambience, light and fog presets our volumes reuse.</summary>
    internal RoomVolume Volume;
}

/// <summary>
/// Turns one geometry-only room prefab from the bundle into a game module: the recipe anchors become the
/// components the game looks for. Runs identically on every client, so Photon view ids line up.
///
/// | Recipe anchor                    | Game component                                              |
/// |----------------------------------|-------------------------------------------------------------|
/// | room role / special tag          | Module (+ StartRoom), PhotonView                            |
/// | nav, nav tag connect             | LevelPoint, ModuleConnect with the anchor's outward forward |
/// | volume                           | RoomVolume (reverb, ambience, map tile)                     |
/// | loot tag Tiny..VeryTall          | ValuableVolume of that type                                 |
/// | portal + variant closed-portal   | ModulePropSwitch (plug shown when the exit is unused)       |
/// | extraction, volume tag extraction| clone of the vanilla ExtractionPoint (it has its own volume)|
/// | truck, spawn, volume tag truck   | clone of the vanilla truck behind the room's back wall      |
/// | surface (bean puzzle)            | BeanGrove marker with the plates and the giant bean         |
/// </summary>
internal static class FableModuleBuilder
{
    private const string VariantRoot = "Connection variants - inactive, for ModulePropSwitch";
    private const string MarkerRoot = "Integration anchors - not game components";
    private const int NotWalkable = 1; // built-in navmesh area
    /// <summary>Recipes are centred 15 m squares; the game puts a start room behind its connecting edge.</summary>
    internal const float HalfModule = 7.5f;

    internal static GameObject Build(GameObject source, FableRoom room, FableDonor donor, Transform holder)
    {
        bool start = room.role == "Start";
        var root = new GameObject(room.id);
        root.transform.SetParent(holder, false);
        var view = root.AddComponent<PhotonView>();
        view.ObservedComponents = new List<Component>();
        view.Synchronization = ViewSynchronization.Off;
        var module = root.AddComponent<Module>();
        module.status = Module.Status.Finalized;
        module.type = !string.IsNullOrEmpty(room.special) ? Module.Type.Special : room.role switch
        {
            "Start" => Module.Type.StartRoom,
            "Passage" => Module.Type.Passage,
            "DeadEnd" => Module.Type.DeadEnd,
            "Extraction" => Module.Type.Extraction,
            _ => Module.Type.Normal,
        };
        if (start) root.AddComponent<StartRoom>();

        var body = Object.Instantiate(source, root.transform);
        body.name = "Geometry";
        body.transform.localPosition = start ? new Vector3(0f, 0f, -HalfModule) : Vector3.zero;
        body.transform.localRotation = Quaternion.identity;
        var gameplay = new GameObject("Gameplay").transform;
        gameplay.SetParent(body.transform, false);

        MarkNavigation(body.transform, room);
        var volumes = Volumes(gameplay, room, donor, start && donor.Truck != null);
        LevelPoints(gameplay, room, volumes.Count > 0 ? volumes[0] : null, out var truckPoint);
        Loot(gameplay, room);
        if (module.type == Module.Type.Normal) Exits(body.transform, gameplay, room);
        var extraction = FableLevelPlan.Anchor(room, "extraction");
        if (extraction != null && donor.Extraction != null)
        {
            var clone = Object.Instantiate(donor.Extraction, root.transform);
            clone.name = donor.Extraction.name;
            clone.transform.SetPositionAndRotation(body.transform.TransformPoint(Vec(extraction.p)),
                body.transform.rotation * Quaternion.Euler(Vec(extraction.r)));
        }
        if (start) Truck(root.transform, body.transform, gameplay, room, donor, truckPoint);
        if (room.special == BeanPuzzleRules.Special) Grove(root, body.transform, room);
        view.RefreshRpcMonoBehaviourCache();
        return root;
    }

    private static Vector3 Vec(float[] v) => v != null && v.Length == 3 ? new Vector3(v[0], v[1], v[2]) : Vector3.zero;

    private static int Layer(string name, int fallback = 0)
    {
        int layer = LayerMask.NameToLayer(name);
        return layer >= 0 ? layer : fallback;
    }

    // The game builds the navmesh after the modules are placed. Depending on its surface settings it either takes
    // everything, or only what sits under a NavMeshModifier: mark the whole room, then mirror the editor bake:
    // recipe "Walkable" elements are floor, other colliders are obstacles, collider-less decoration is ignored.
    private static void MarkNavigation(Transform body, FableRoom room)
    {
        body.gameObject.AddComponent<NavMeshModifier>();
        var walkable = new HashSet<string>(room.walkable ?? Array.Empty<string>(), StringComparer.Ordinal);
        var architecture = body.Find("Architecture");
        if (architecture != null)
            foreach (Transform element in architecture)
            {
                if (walkable.Contains(element.name)) continue;
                var modifier = element.gameObject.AddComponent<NavMeshModifier>();
                if (element.GetComponentInChildren<Collider>(true) == null) modifier.ignoreFromBuild = true;
                else { modifier.overrideArea = true; modifier.area = NotWalkable; }
            }
        var variants = body.Find(VariantRoot);
        if (variants != null)
        {
            var modifier = variants.gameObject.AddComponent<NavMeshModifier>();
            modifier.overrideArea = true; modifier.area = NotWalkable;
        }
        // Authoring markers carry no geometry, but keep them out of every collection pass anyway.
        var markers = body.Find(MarkerRoot);
        if (markers != null) markers.gameObject.AddComponent<NavMeshModifier>().ignoreFromBuild = true;
    }

    private static List<RoomVolume> Volumes(Transform parent, FableRoom room, FableDonor donor, bool truckCloned)
    {
        var list = new List<RoomVolume>();
        foreach (var anchor in room.anchors)
        {
            if (anchor.kind != "volume") continue;
            // The cloned truck brings its own truck volume; the recipe's bay is then just part of the start room.
            if (anchor.tag == "truck" && truckCloned) continue;
            // The vanilla extraction point carries its own volume and switches it on while it is active.
            // A second, always-on one would count everybody standing there as "in the extraction point".
            if (anchor.tag == "extraction" && donor.Extraction != null) continue;
            var go = new GameObject("Room Volume " + anchor.id) { layer = Layer("RoomVolume") };
            go.transform.SetParent(parent, false);
            go.transform.localPosition = Vec(anchor.p);
            go.transform.localRotation = Quaternion.Euler(Vec(anchor.r));
            var box = go.AddComponent<BoxCollider>();
            box.isTrigger = true; box.size = Vec(anchor.s);
            var volume = go.AddComponent<RoomVolume>();
            volume.Truck = anchor.tag == "truck";
            volume.Extraction = anchor.tag == "extraction";
            if (donor.Volume != null)
            {
                volume.ReverbPreset = donor.Volume.ReverbPreset;
                volume.RoomAmbience = donor.Volume.RoomAmbience;
                volume.RoomAmbienceOverride = donor.Volume.RoomAmbienceOverride;
                volume.RoomAmbientLight = donor.Volume.RoomAmbientLight;
                volume.RoomFog = donor.Volume.RoomFog;
            }
            // The whole-room volume first: level points take it as their room.
            if (string.IsNullOrEmpty(anchor.tag)) list.Insert(0, volume); else list.Add(volume);
        }
        return list;
    }

    private static void LevelPoints(Transform parent, FableRoom room, RoomVolume volume, out LevelPoint truckPoint)
    {
        truckPoint = null;
        var points = new Dictionary<string, LevelPoint>(StringComparer.Ordinal);
        foreach (var anchor in room.anchors)
        {
            if (anchor.kind != "nav" || points.ContainsKey(anchor.id)) continue;
            // Room volumes find their level points by this trigger on the "Other" layer, as in vanilla modules.
            var go = new GameObject("Level Point " + anchor.id) { layer = Layer("Other") };
            go.transform.SetParent(parent, false);
            go.transform.localPosition = Vec(anchor.p);
            go.transform.localRotation = Quaternion.Euler(Vec(anchor.r));
            var box = go.AddComponent<BoxCollider>();
            box.isTrigger = true; box.center = new Vector3(0f, .2f, 0f); box.size = new Vector3(.2f, .5f, .2f);
            var point = go.AddComponent<LevelPoint>();
            point.DebugMeshActive = false;
            point.ConnectedPoints = new List<LevelPoint>();
            point.ModuleConnect = anchor.tag == "connect";
            point.Room = volume;
            points.Add(anchor.id, point);
            if (anchor.tag == "truck") truckPoint = point;
        }
        foreach (var anchor in room.anchors)
        {
            if (anchor.kind != "nav" || anchor.to == null || !points.TryGetValue(anchor.id, out var from)) continue;
            foreach (string id in anchor.to)
                if (points.TryGetValue(id, out var to) && to != from) Link(from, to);
        }
    }

    // The game expects every link in both directions and reports one-way links as errors.
    private static void Link(LevelPoint a, LevelPoint b)
    {
        if (!a.ConnectedPoints.Contains(b)) a.ConnectedPoints.Add(b);
        if (!b.ConnectedPoints.Contains(a)) b.ConnectedPoints.Add(a);
    }

    private static void Loot(Transform parent, FableRoom room)
    {
        foreach (var anchor in room.anchors)
        {
            if (anchor.kind != "loot" || !Enum.TryParse(anchor.tag, out ValuableVolume.Type type)) continue;
            var go = new GameObject("Valuable Volume " + type);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = Vec(anchor.p);
            go.transform.localRotation = Quaternion.Euler(Vec(anchor.r));
            go.AddComponent<ValuableVolume>().VolumeType = type;
        }
    }

    // A normal module can end up with unused sides. Vanilla hides or shows props per side through
    // ModulePropSwitch; here the "not connected" side is the recipe's wall plug.
    private static void Exits(Transform body, Transform parent, FableRoom room)
    {
        var variants = body.Find(VariantRoot);
        if (variants == null) return;
        foreach (var anchor in room.anchors)
        {
            if (anchor.kind != "portal") continue;
            var plug = variants.Find("closed-" + anchor.id);
            if (plug == null) continue;
            var position = Vec(anchor.p);
            var go = new GameObject("Exit " + anchor.id);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            var open = new GameObject("Connected");
            open.transform.SetParent(go.transform, false);
            var prop = go.AddComponent<ModulePropSwitch>();
            prop.ConnectedParent = open;
            prop.NotConnectedParent = plug.gameObject;
            // Grid "top" is +Z, "right" is +X; the game turns the side with the module's rotation itself.
            prop.ConnectionSide = Mathf.Abs(position.z) >= Mathf.Abs(position.x)
                ? position.z > 0f ? ModulePropSwitch.Connection.Top : ModulePropSwitch.Connection.Bot
                : position.x > 0f ? ModulePropSwitch.Connection.Right : ModulePropSwitch.Connection.Left;
        }
    }

    // The vanilla truck is far longer than the recipe's parking bay (about 14 m), and the level cannot be finished
    // without its screen, door and shelves. So the real truck is cloned and parked where vanilla parks it: outside,
    // behind the wall opposite the exit, with a doorway cut into that wall. The bay becomes its forecourt.
    private static void Truck(Transform root, Transform body, Transform gameplay, FableRoom room, FableDonor donor, LevelPoint truckPoint)
    {
        var bay = FableLevelPlan.Anchor(room, "truck");
        if (donor.Truck == null || bay == null)
        {
            // No donor: keep the level loadable with plain spawn points in the bay.
            foreach (var anchor in room.anchors)
            {
                if (anchor.kind != "spawn") continue;
                var go = new GameObject("Spawn Point " + anchor.id);
                go.transform.SetParent(gameplay, false);
                go.transform.localPosition = Vec(anchor.p) + Vector3.up * .1f;
                go.transform.localRotation = Quaternion.Euler(Vec(anchor.r));
                go.AddComponent<SpawnPoint>();
            }
            if (truckPoint != null) truckPoint.Truck = true;
            return;
        }
        var truck = Object.Instantiate(donor.Truck, root);
        truck.name = donor.Truck.name;
        // Same pose a vanilla start room gives it, shifted sideways to the bay. A vanilla start room is half a
        // module deep and ours is a full one, so its back wall (and the truck behind it) is half a module further back.
        truck.transform.localPosition = donor.TruckPosition + new Vector3(Vec(bay.p).x, 0f, -HalfModule);
        truck.transform.localRotation = donor.TruckRotation;

        // Links to the vanilla start room's own level points did not survive the clone: keep the truck's inner
        // links and tie its point nearest to the door to the recipe's truck point.
        var inner = new List<LevelPoint>(truck.GetComponentsInChildren<LevelPoint>(true));
        LevelPoint nearest = null;
        foreach (var point in inner)
        {
            point.ConnectedPoints ??= new List<LevelPoint>();
            point.ConnectedPoints.RemoveAll(other => other == null || !inner.Contains(other));
            if (truckPoint != null && (nearest == null ||
                (point.transform.position - truckPoint.transform.position).sqrMagnitude <
                (nearest.transform.position - truckPoint.transform.position).sqrMagnitude)) nearest = point;
        }
        if (nearest != null) Link(nearest, truckPoint);

        Doorway(body, Vec(bay.p).x, Mathf.Max(4.8f, Vec(bay.s).x), 4.4f);
    }

    // Replaces the solid back wall with three pieces around the truck door. The wall is scaled copies of itself:
    // its material is world-space triplanar, so the surface does not stretch.
    private static void Doorway(Transform body, float centre, float width, float height)
    {
        var wall = body.Find("Architecture/wall-south");
        if (wall == null) { Plugin.Log.LogWarning("[Fable] Start room has no wall-south element; the truck door is not cut."); return; }
        var renderer = wall.GetComponentInChildren<Renderer>(true);
        if (renderer == null) return;
        var filter = renderer.GetComponent<MeshFilter>();
        if (filter == null || filter.sharedMesh == null) return;
        // Size in the wall's own space (the template sits unrotated under an inactive holder).
        var size = Vector3.Scale(filter.sharedMesh.bounds.size, renderer.transform.localScale);
        var origin = wall.localPosition;
        float left = origin.x - size.x * .5f, right = origin.x + size.x * .5f;
        float floor = origin.y - size.y * .5f, top = origin.y + size.y * .5f;
        float doorLeft = Mathf.Max(left, centre - width * .5f), doorRight = Mathf.Min(right, centre + width * .5f);
        float doorTop = Mathf.Min(top, floor + height);
        Piece(wall, "wall-south-left", left, doorLeft, floor, top, origin, size);
        Piece(wall, "wall-south-right", doorRight, right, floor, top, origin, size);
        Piece(wall, "wall-south-lintel", doorLeft, doorRight, doorTop, top, origin, size);
        wall.gameObject.SetActive(false);
    }

    private static void Piece(Transform wall, string name, float x0, float x1, float y0, float y1, Vector3 origin, Vector3 size)
    {
        if (x1 - x0 < .05f || y1 - y0 < .05f) return;
        var piece = Object.Instantiate(wall.gameObject, wall.parent);
        piece.name = name;
        piece.transform.localPosition = new Vector3((x0 + x1) * .5f, (y0 + y1) * .5f, origin.z);
        piece.transform.localScale = Vector3.Scale(wall.localScale, new Vector3((x1 - x0) / size.x, (y1 - y0) / size.y, 1f));
    }

    private static void Grove(GameObject root, Transform body, FableRoom room)
    {
        var markers = body.Find(MarkerRoot);
        var architecture = body.Find("Architecture");
        if (markers == null || architecture == null) return;
        var grove = root.AddComponent<BeanGrove>();
        grove.sockets = new Transform[BeanPuzzleRules.Count];
        for (int i = 0; i < BeanPuzzleRules.Count; i++)
            grove.sockets[i] = markers.Find("surface__" + BeanPuzzleRules.SocketPrefix + BeanPuzzleRules.Colours[i]);
        grove.landing = markers.Find("surface__" + BeanPuzzleRules.Landing);
        grove.eggSpawn = markers.Find("surface__" + BeanPuzzleRules.EggSpawn);
        var giant = new List<Transform>();
        foreach (Transform element in architecture)
            if (element.name.StartsWith(BeanPuzzleRules.GiantPrefix, StringComparison.Ordinal)) giant.Add(element);
        grove.giant = giant.ToArray();
        // Each plate is watched by a standing stone with a lamp in its colour; the lamp flares when the plate is matched.
        grove.lamps = new Light[BeanPuzzleRules.Count];
        for (int i = 0; i < BeanPuzzleRules.Count; i++)
        {
            var lamp = architecture.Find("stone-" + BeanPuzzleRules.Colours[i] + "-light");
            grove.lamps[i] = lamp != null ? lamp.GetComponent<Light>() : null;
        }
        if (!FableLevelPlan.PuzzleAnchorsComplete(room, out string missing) || Array.IndexOf(grove.sockets, null) >= 0 ||
            grove.landing == null || grove.eggSpawn == null || giant.Count == 0)
            Plugin.Log.LogWarning("[Fable] Bean puzzle room is incomplete (" + (missing ?? "prefab markers") + "); the puzzle will not start.");
    }
}

/// <summary>Sits on the bean puzzle module, on every client: the plates, the landing spot and the giant bean.</summary>
internal sealed class BeanGrove : MonoBehaviour
{
    internal static BeanGrove Instance { get; private set; }

    // Public so Unity copies them from the template onto each spawned module.
    public Transform[] sockets;
    public Transform landing, eggSpawn;
    public Transform[] giant;
    public Light[] lamps;

    private Vector3[] _rest;
    private float[] _lampIntensity, _lampRange;
    private float _drop = -1f;
    private int _matched = -1;

    internal bool Complete => sockets != null && sockets.Length == BeanPuzzleRules.Count && Array.IndexOf(sockets, null) < 0 &&
        landing != null && eggSpawn != null && giant != null && giant.Length > 0;

    private void Awake()
    {
        // The generator asks for one special tile; should a second grove ever appear, the first one keeps the puzzle.
        if (Instance == null) Instance = this;
        if (lamps != null)
        {
            _lampIntensity = new float[lamps.Length]; _lampRange = new float[lamps.Length];
            for (int i = 0; i < lamps.Length; i++)
                if (lamps[i] != null) { _lampIntensity[i] = lamps[i].intensity; _lampRange[i] = lamps[i].range; }
        }
        if (giant == null) return;
        _rest = new Vector3[giant.Length];
        for (int i = 0; i < giant.Length; i++) _rest[i] = giant[i] != null ? giant[i].localPosition : Vector3.zero;
    }

    /// <summary>Shows which plates hold the bean of their colour: the stone's lamp burns three times brighter.</summary>
    internal void SetMatched(int mask)
    {
        if (mask == _matched || _lampIntensity == null) return;
        _matched = mask;
        for (int i = 0; i < lamps.Length; i++)
        {
            if (lamps[i] == null) continue;
            bool on = (mask & 1 << i) != 0;
            lamps[i].intensity = _lampIntensity[i] * (on ? 3.2f : 1f);
            lamps[i].range = _lampRange[i] * (on ? 1.6f : 1f);
        }
    }

    private void OnDestroy() { if (Instance == this) Instance = null; }

    /// <summary>Moves every giant-bean element down by <paramref name="metres"/> from where the recipe hangs it.</summary>
    internal void SetDrop(float metres)
    {
        if (_rest == null || Mathf.Approximately(metres, _drop)) return;
        _drop = metres;
        for (int i = 0; i < giant.Length; i++)
            if (giant[i] != null) giant[i].localPosition = _rest[i] + Vector3.down * metres;
    }
}
