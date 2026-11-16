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
    /// <summary>The vanilla wall piece around the truck's rear: frame, sill and its place in the start room.</summary>
    internal GameObject Hole;
    internal Vector3 HolePosition, HoleScale = Vector3.one;
    internal Quaternion HoleRotation = Quaternion.identity;
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
/// | surface (bean puzzle)            | BeanGrove marker with the plates and the growing beanstalk  |
/// | kill (one per pit)               | HurtCollider death pit: kills players, kills or maims monsters |
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
        Solidify(body.transform);
        var volumes = Volumes(gameplay, room, donor, start && donor.Truck != null);
        LevelPoints(gameplay, room, volumes.Count > 0 ? volumes[0] : null, out var truckPoint);
        Loot(gameplay, room);
        Pits(gameplay, room);
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
        MapTiles(root, body.transform, room);
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

    // The recipes give collision to floors, walls, furniture and to simple blocker boxes inside big props; the
    // decoration itself (arches, vines, cups, roofs, wall trim) is visual only, so players walked through it.
    // Here every decoration piece gets a collider in its own shape. It runs after MarkNavigation on purpose:
    // those pieces stay out of the navmesh build, so monster paths remain the ones checked in the editor.
    // Skipped: flat floor markings and small loose clutter, which would only snag carts and loot.
    private static void Solidify(Transform body)
    {
        var architecture = body.Find("Architecture");
        if (architecture == null) return;
        foreach (Transform element in architecture)
        {
            if (element.GetComponentInChildren<Collider>(true) != null) continue;
            foreach (var filter in element.GetComponentsInChildren<MeshFilter>(true))
            {
                var mesh = filter.sharedMesh;
                if (mesh == null) continue;
                var bounds = BoundsIn(body, filter.transform, mesh.bounds);
                float largest = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
                if (largest < .3f || (bounds.size.y < .06f && bounds.min.y < .2f)) continue;
                if (mesh.name == "Cube") filter.gameObject.AddComponent<BoxCollider>();
                else if (mesh.isReadable) filter.gameObject.AddComponent<MeshCollider>().sharedMesh = mesh;
                else { var box = filter.gameObject.AddComponent<BoxCollider>(); box.center = mesh.bounds.center; box.size = mesh.bounds.size; }
            }
        }
    }

    /// <summary>The box around a mesh's bounds (given in the mesh object's space) in the axes of another transform.</summary>
    private static Bounds BoundsIn(Transform space, Transform from, Bounds local)
    {
        var result = new Bounds(space.InverseTransformPoint(from.TransformPoint(local.center)), Vector3.zero);
        for (int i = 0; i < 8; i++)
        {
            var corner = local.center + Vector3.Scale(local.extents, new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f));
            result.Encapsulate(space.InverseTransformPoint(from.TransformPoint(corner)));
        }
        return result;
    }

    // The handheld map draws a module from DirtFinderMapFloor / DirtFinderMapWall markers on its floor and wall
    // pieces. The room prefabs have none, so the map showed our rooms as empty space. The markers need the game's
    // map tile prefabs, which exist only inside a level: FableMapTiles places them when the module starts.
    private static void MapTiles(GameObject root, Transform body, FableRoom room)
    {
        var architecture = body.Find("Architecture");
        if (architecture == null) return;
        var walkable = new HashSet<string>(room.walkable ?? Array.Empty<string>(), StringComparer.Ordinal);
        var floors = new List<Transform>(); var walls = new List<Transform>();
        foreach (Transform element in architecture)
        {
            bool wall = element.name.StartsWith("wall-", StringComparison.Ordinal);
            if (!wall && !walkable.Contains(element.name)) continue;
            foreach (var box in element.GetComponentsInChildren<BoxCollider>(true))
                if (box.GetComponent<MeshFilter>() != null) (wall ? walls : floors).Add(box.transform);
        }
        var tiles = root.AddComponent<FableMapTiles>();
        tiles.floors = floors.ToArray(); tiles.walls = walls.ToArray();
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

    // A fall into a pit is fatal, as in the game's own death pits: the same HurtCollider with its deathPit flag.
    // Players die at once. Monsters die on the first moons and take heavy damage with a stun on later ones
    // (the game's own rule for death pits). Loot and carts are left alone, so nothing is destroyed by a slip.
    private static void Pits(Transform parent, FableRoom room)
    {
        foreach (var anchor in room.anchors)
        {
            if (anchor.kind != "kill") continue;
            var size = Vec(anchor.s);
            if (size.x < .5f || size.y < .5f || size.z < .5f) continue;
            var go = new GameObject("Death Pit " + anchor.id) { layer = Layer("Triggers", 2) };
            go.transform.SetParent(parent, false);
            go.transform.localPosition = Vec(anchor.p);
            var box = go.AddComponent<BoxCollider>();
            box.isTrigger = true; box.size = size;
            var hurt = go.AddComponent<HurtCollider>();
            hurt.deathPit = true;
            hurt.playerLogic = true; hurt.playerKill = true;
            hurt.enemyLogic = true; hurt.enemyKill = true;
            hurt.physLogic = false;
            // Unity fills these in for prefabs made in the editor; a component added in code starts with none.
            hurt.onImpactAny = new UnityEngine.Events.UnityEvent(); hurt.onImpactPlayer = new UnityEngine.Events.UnityEvent();
            hurt.onImpactPhysObject = new UnityEngine.Events.UnityEvent(); hurt.onImpactEnemy = new UnityEngine.Events.UnityEvent();
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
    // without its screen, door and shelves. So the real truck is cloned and parked the way vanilla parks it: outside,
    // its rear flush with the wall opposite the exit, with a doorway cut into that wall. The bay becomes its forecourt.
    //
    // Measured in the Manor start rooms: the truck object sits at the room origin and its rear opening is 9.96 m
    // behind it, exactly where the "Truck Hole" wall piece stands. The truck floor is 0.7 m above the room floor;
    // the truck's own ramp comes 3.8 m into the room and the hole piece's sill closes the space under the floor.
    private const float VanillaHoleZ = -9.96f;

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
        // Slide the vanilla arrangement back until its hole plane lies in our back wall, and sideways to the bay.
        var backWall = body.Find("Architecture/wall-south");
        float wallZ = body.localPosition.z + (backWall != null ? backWall.localPosition.z : -HalfModule);
        var shift = new Vector3(Vec(bay.p).x, 0f, wallZ - (donor.Hole != null ? donor.HolePosition.z : VanillaHoleZ));
        var truck = Object.Instantiate(donor.Truck, root);
        truck.name = donor.Truck.name;
        truck.transform.localPosition = donor.TruckPosition + shift;
        truck.transform.localRotation = donor.TruckRotation;
        if (donor.Hole != null)
        {
            var hole = Object.Instantiate(donor.Hole, root);
            hole.name = donor.Hole.name;
            hole.transform.localPosition = donor.HolePosition + shift;
            hole.transform.localRotation = donor.HoleRotation;
            hole.transform.localScale = donor.HoleScale;
        }

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

        // With the vanilla frame the cut ends inside its posts and top bar (outer edge 2.6 m, bar at 3.9 m);
        // without it the cut is the truck's inner width, so the truck's own walls close the sides.
        if (donor.Hole != null) Doorway(body, Vec(bay.p).x, 5.1f, 3.9f);
        else Doorway(body, Vec(bay.p).x, 4.6f, 4.4f);
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

        // Wall trim, hangings and lamps that sat on the removed stretch of wall would now hang in the doorway.
        var opening = new Bounds(new Vector3((doorLeft + doorRight) * .5f, (floor + doorTop) * .5f + .05f, origin.z),
            new Vector3(doorRight - doorLeft - .1f, doorTop - floor - .1f, 1.2f));
        foreach (Transform element in wall.parent)
        {
            if (!element.gameObject.activeSelf || element.name.StartsWith("wall-", StringComparison.Ordinal) ||
                element.name.StartsWith("floor", StringComparison.Ordinal) || element.name.StartsWith("ceiling", StringComparison.Ordinal)) continue;
            foreach (var part in element.GetComponentsInChildren<MeshFilter>(true))
            {
                if (part.sharedMesh == null || !BoundsIn(body, part.transform, part.sharedMesh.bounds).Intersects(opening)) continue;
                element.gameObject.SetActive(false);
                break;
            }
        }
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

/// <summary>Sits on the bean puzzle module, on every client: the plates, the dais and the beanstalk that grows from it.</summary>
internal sealed class BeanGrove : MonoBehaviour
{
    internal static BeanGrove Instance { get; private set; }

    // Public so Unity copies them from the template onto each spawned module. "giant" is every beanstalk piece.
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
        SetGrowth(0f); // the stalk starts under the floor, before the level's navmesh is built
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

    /// <summary>
    /// Raises the beanstalk <paramref name="metres"/> out of the floor: 0 keeps it fully sunk, LowerDistance puts
    /// every piece where the recipe drew it. The leaves and the crown carry their colliders up with them.
    /// </summary>
    internal void SetGrowth(float metres)
    {
        if (_rest == null || Mathf.Approximately(metres, _drop)) return;
        _drop = metres;
        float sunk = Mathf.Max(0f, BeanPuzzleRules.LowerDistance - metres);
        for (int i = 0; i < giant.Length; i++)
            if (giant[i] != null) giant[i].localPosition = _rest[i] + Vector3.down * sunk;
    }
}

/// <summary>
/// Draws a Fable Realm module on the handheld map. Each floor and wall box of the room gets a marker the game's
/// map understands; the marker is scaled and shifted so that the game's own 1x1 map tile covers exactly that box,
/// whatever size and pivot the tile prefab has (both are measured from the prefab here, not assumed).
/// Local to each client, like the map itself.
/// </summary>
internal sealed class FableMapTiles : MonoBehaviour
{
    // Public so Unity copies them from the template onto each spawned module. Each is a unit cube scaled to the box.
    public Transform[] floors, walls;
    private static bool _logged;

    private void Start()
    {
        var map = Map.Instance;
        if (map == null || !SemiFunc.RunIsLevel()) return;
        bool floorOk = Measure(map.FloorObject1x1, out var floorTile), wallOk = Measure(map.Wall1x1Object, out var wallTile);
        if (!_logged)
        {
            _logged = true;
            Plugin.Log.LogInfo($"[Fable] Map tiles: floor {(floorOk ? floorTile.size.ToString("F2") + " at " + floorTile.center.ToString("F2") : "not measurable")}, " +
                $"wall {(wallOk ? wallTile.size.ToString("F2") + " at " + wallTile.center.ToString("F2") : "not measurable")}.");
        }
        var holder = new GameObject("Map Tiles").transform;
        holder.SetParent(transform, false);
        if (floorOk && floors != null)
            foreach (var box in floors)
            {
                // Level floors only: a ramp or a tilted slab has no flat tile.
                if (box == null || !box.gameObject.activeInHierarchy || Vector3.Angle(box.up, Vector3.up) > 5f) continue;
                var size = box.lossyScale;
                var scale = new Vector3(size.x / floorTile.size.x, 1f, size.z / floorTile.size.z);
                var top = box.position + Vector3.up * (size.y * .5f);
                var marker = Marker(holder, "Floor " + box.parent.name, box.rotation, scale, top, floorTile.center);
                marker.AddComponent<DirtFinderMapFloor>().Type = DirtFinderMapFloor.FloorType.Floor_1x1;
            }
        if (wallOk && walls != null)
            foreach (var box in walls)
            {
                if (box == null || !box.gameObject.activeInHierarchy) continue;
                var size = box.lossyScale;
                bool boxAlongX = size.x >= size.z, tileAlongX = wallTile.size.x >= wallTile.size.z;
                float length = Mathf.Max(size.x, size.z), tileLength = Mathf.Max(wallTile.size.x, wallTile.size.z);
                var scale = tileAlongX ? new Vector3(length / tileLength, 1f, 1f) : new Vector3(1f, 1f, length / tileLength);
                var rotation = box.rotation * Quaternion.Euler(0f, boxAlongX == tileAlongX ? 0f : 90f, 0f);
                var foot = box.position - Vector3.up * (size.y * .5f);
                var marker = Marker(holder, "Wall " + box.parent.name, rotation, scale, foot, wallTile.center);
                marker.AddComponent<DirtFinderMapWall>().Type = DirtFinderMapWall.WallType.Wall_1x1;
            }
    }

    // The map copies the marker's position, rotation and scale onto its tile. Put the marker where the tile's
    // pivot must be for the tile's centre (horizontally) to land on the box's centre.
    private static GameObject Marker(Transform holder, string name, Quaternion rotation, Vector3 scale, Vector3 centre, Vector3 tileCentre)
    {
        var marker = new GameObject(name);
        marker.transform.SetParent(holder, true);
        marker.transform.rotation = rotation;
        marker.transform.localScale = scale;
        var offset = Vector3.Scale(new Vector3(tileCentre.x, 0f, tileCentre.z), scale);
        marker.transform.position = centre - rotation * offset;
        return marker;
    }

    /// <summary>The box around everything a map tile prefab draws, in the prefab root's own space.</summary>
    private static bool Measure(GameObject prefab, out Bounds bounds)
    {
        bounds = default;
        if (prefab == null) return false;
        bool any = false;
        var toRoot = prefab.transform.worldToLocalMatrix;
        foreach (var filter in prefab.GetComponentsInChildren<MeshFilter>(true))
        {
            if (filter.sharedMesh == null) continue;
            var matrix = toRoot * filter.transform.localToWorldMatrix;
            var mesh = filter.sharedMesh.bounds;
            for (int i = 0; i < 8; i++)
            {
                var corner = mesh.center + Vector3.Scale(mesh.extents, new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f));
                var point = matrix.MultiplyPoint3x4(corner);
                if (any) bounds.Encapsulate(point); else { bounds = new Bounds(point, Vector3.zero); any = true; }
            }
        }
        return any && bounds.size.x > .01f && bounds.size.z > .01f;
    }
}
