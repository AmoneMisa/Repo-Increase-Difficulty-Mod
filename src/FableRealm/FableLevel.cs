using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using HarmonyLib;
using Newtonsoft.Json;
using REPOLib.Modules;
using REPOLib.Objects.Sdk;
using Unity.AI.Navigation;
using UnityEngine;
using Object = UnityEngine.Object;

namespace IncreaseHardMod.FableRealm;

/// <summary>
/// Makes the authored Fable Realm rooms a level the game can generate.
///
/// How the pieces meet:
/// - <c>fablerealm.bundle</c> (built by Fable Realm → Build game bundle, shipped next to the plugin DLL) holds one
///   geometry-only prefab per room. It needs no game scripts, so the Unity project needs no game assemblies.
/// - <c>fable_rooms.json</c> (embedded) is the recipe contract: role, difficulty, special tag and anchors per room.
/// - <see cref="FableModuleBuilder"/> adds the game's own components to a copy of each prefab; the truck and the
///   extraction point are cloned from a level the game ships (a "donor").
/// - REPOLib registers the copies as network prefabs and adds the Level to the run's level list. REPOLib knows
///   nothing of the game's special modules, so the bean puzzle room is registered here and listed in
///   Level.ModulesSpecial with ModulesSpecialAmount = 1: the generator then places it on at most one tile.
/// </summary>
internal static class FableLevel
{
    internal const string LevelName = "Level - Fable Realm";
    internal const string BundleFile = "fablerealm.bundle";
    private const string ContractSuffix = ".fable_rooms.json";

    /// <summary>The registered level, or null when it could not be built (missing bundle, no donor, switched off).</summary>
    internal static Level Level { get; private set; }
    private static bool _attempted;
    private static GameObject _holder;

    internal static bool IsCurrent => Level != null && RunManager.instance != null && RunManager.instance.levelCurrent == Level;

    // A Harmony postfix runs right after the game's own method. RunManager.Awake is where the list of levels
    // first exists; High priority puts this before REPOLib's own postfix, which adds queued levels to the game.
    [HarmonyPatch(typeof(RunManager), nameof(RunManager.Awake))]
    private static class Register
    {
        [HarmonyPostfix, HarmonyPriority(Priority.High)]
        private static void Postfix(RunManager __instance)
        {
            if (_attempted || Plugin.Instance == null) return;
            _attempted = true;
            if (!ModConfig.FableRealmEnabled.Value) return;
            try { Build(__instance); }
            catch (Exception error) { Level = null; Plugin.Log.LogError("[Fable] Level registration stopped: " + error); }
        }
    }

    // FableRealmForce: the host's pick of the next run level is always Fable Realm. First priority puts this prefix
    // before the map vote's; returning false skips both the vote's pick and the game's random one. Guests get the
    // level through the game's own level sync, so only the host needs the setting.
    [HarmonyPatch(typeof(RunManager), nameof(RunManager.SetRunLevel))]
    private static class Force
    {
        [HarmonyPrefix, HarmonyPriority(Priority.First)]
        private static bool Prefix(RunManager __instance)
        {
            if (Level == null || !ModConfig.FableRealmForce.Value || !SemiFunc.IsMasterClientOrSingleplayer()) return true;
            __instance.levelCurrent = Level;
            Patches.Effects.MapVoteState.Clear(); // the vote's own prefix is skipped, so close its session here
            Plugin.Log.LogInfo("[Fable] ForceLevel is on: the next level is Fable Realm.");
            return false;
        }
    }

    // This game build loads module prefabs with Resources.LoadAsync before it spawns them. Prefabs that exist only
    // in memory (anything registered through REPOLib) are not in Resources, so the load fails and the level is
    // abandoned. A prefix runs before the method: hand the game our prefabs through its own cache, which it checks first.
    [HarmonyPatch(typeof(LevelGenerator), nameof(LevelGenerator.PrefabsLoad))]
    private static class SeedPrefabCache
    {
        [HarmonyPrefix]
        private static void Prefix(string[] _paths)
        {
            var run = RunManager.instance;
            if (run == null || _paths == null) return;
            foreach (string path in _paths)
            {
                if (string.IsNullOrEmpty(path) || run.singleplayerPool.ContainsKey(path)) continue;
                if (NetworkPrefabs.Prefabs.TryGetValue(path, out var prefab) && prefab != null) run.singleplayerPool[path] = prefab;
            }
        }
    }

    // The navmesh is built at runtime from the placed modules. Vanilla rooms and ours both stand on the Default
    // layer; should a game update ever narrow the surface to its navmesh-only layer, widen it again for this level
    // (the surface belongs to the level scene, so nothing carries over). The settings are logged once per level.
    [HarmonyPatch(typeof(LevelGenerator), nameof(LevelGenerator.NavMeshSetupRPC))]
    private static class NavMeshForRooms
    {
        [HarmonyPrefix]
        private static void Prefix(LevelGenerator __instance)
        {
            if (!IsCurrent || __instance == null) return;
            var surface = __instance.GetComponent<NavMeshSurface>();
            if (surface == null) return;
            bool widened = (surface.layerMask.value & 1) == 0;
            if (widened) surface.layerMask = surface.layerMask.value | 1;
            Plugin.Log.LogInfo($"[Fable] Navmesh surface: collect {surface.collectObjects}, geometry {surface.useGeometry}, " +
                $"layers 0x{surface.layerMask.value:X}{(widened ? " (Default layer added)" : "")}.");
        }
    }

    // Why the moon tower does or does not wake on a Fable Realm extraction point: one log line per activation.
    [HarmonyPatch(typeof(ExtractionPoint), nameof(ExtractionPoint.HalloweenActivationSet))]
    private static class MoonTowerReport
    {
        [HarmonyPostfix]
        private static void Postfix(ExtractionPoint __instance, bool _active)
        {
            if (!IsCurrent || !_active) return;
            var tower = __instance.GetComponent<ExtractionHalloweenTower>();
            Plugin.Log.LogInfo($"[Fable] Extraction point activated: moon tower {(tower == null ? "missing" : tower.enabled ? "running" : "present but off")}, " +
                $"seasonal event {(SemiFunc.GameEventIsHalloween() ? "on" : "off")}.");
        }
    }

    private static void Build(RunManager run)
    {
        var file = ReadContract();
        var plan = FableLevelPlan.From(file?.rooms, out string error);
        if (plan == null) { Plugin.Log.LogError("[Fable] Room contract rejected: " + error); return; }

        string path = Path.Combine(Path.GetDirectoryName(typeof(Plugin).Assembly.Location) ?? "", BundleFile);
        if (!File.Exists(path)) { Plugin.Log.LogWarning("[Fable] " + BundleFile + " is not next to the plugin DLL; Fable Realm is not registered."); return; }
        var bundle = AssetBundle.LoadFromFile(path);
        if (bundle == null) { Plugin.Log.LogError("[Fable] The game could not open " + path + "."); return; }

        var donorLevel = DonorLevel(run);
        if (donorLevel == null) { Plugin.Log.LogError("[Fable] No vanilla level to borrow the truck and extraction point from."); return; }
        var donor = Donor(donorLevel);
        bool ownStart = donor.Truck != null && !ModConfig.FableRealmVanillaStartRoom.Value;
        if (donor.Extraction == null) { Plugin.Log.LogError("[Fable] No extraction point found in " + donorLevel.name + "."); return; }

        _holder = new GameObject("IHM_FableRealmPrefabs");
        _holder.SetActive(false); // templates must never run Awake themselves
        Object.DontDestroyOnLoad(_holder);

        var templates = new Dictionary<string, GameObject>(StringComparer.Ordinal);
        foreach (var room in file.rooms)
        {
            if (room.role == "Start" && !ownStart) continue;
            var source = bundle.LoadAsset<GameObject>(room.id);
            if (source == null) { Plugin.Log.LogError("[Fable] Bundle has no prefab for room " + room.id + "."); continue; }
            templates[room.id] = FableModuleBuilder.Build(source, room, donor, _holder.transform);
        }

        var level = ScriptableObject.CreateInstance<Level>();
        level.name = LevelName;
        level.status = Level.Status.Finalized;
        level.NarrativeName = "Fable Realm";
        level.ModuleAmount = donorLevel.ModuleAmount;
        level.PassageMaxAmount = donorLevel.PassageMaxAmount;
        level.HasEnemies = true;
        level.LoadingGraphic01 = donorLevel.LoadingGraphic01;
        level.LoadingGraphic02 = donorLevel.LoadingGraphic02;
        level.LoadingGraphic03 = donorLevel.LoadingGraphic03;
        // Rooms close their own unused exits and join wall to wall: no connect or block props (an empty ref is skipped).
        level.ConnectObject = new PrefabRef();
        level.BlockObject = new PrefabRef();
        level.ModulesMega1 = new List<PrefabRef>(); level.ModulesMega2 = new List<PrefabRef>(); level.ModulesMega3 = new List<PrefabRef>();
        level.ModulesSpecial = new List<PrefabRef>();
        // The donor's vanilla loot fills the level; the mod's Manor-only collection stays on Manor. Fable Realm's
        // own pool is listed three times: the game pools every list it is given, so this triples the odds of a
        // fairy-tale piece against the long vanilla lists.
        level.ValuablePresets = new List<LevelValuables>();
        foreach (var preset in donorLevel.ValuablePresets)
            if (preset != null && !preset.name.StartsWith(Valuables.CollectionSpawnPools.PoolPrefix, StringComparison.Ordinal)) level.ValuablePresets.Add(preset);
        var own = Valuables.CollectionSpawnPools.Ensure(Valuables.LootMap.Fable);
        for (int i = 0; i < 3; i++) level.ValuablePresets.Add(own);
        level.MusicPreset = donorLevel.MusicPreset;
        level.ConstantMusicPreset = donorLevel.ConstantMusicPreset;
        level.AmbiencePresets = new List<LevelAmbience>(donorLevel.AmbiencePresets ?? new List<LevelAmbience>());
        level.DiscordIcon = donorLevel.DiscordIcon;
        Atmosphere(level, donorLevel, file.rooms);

        var content = ScriptableObject.CreateInstance<LevelContent>();
        content.name = LevelName;
        // LevelContent is the asset REPOLib's Unity SDK fills in the editor; its lists are private serialized fields,
        // reached here through the publicized reference so a renamed field fails the build, not the level.
        content._level = level;
        content._startRooms = ownStart ? Templates(plan.Start, templates) : new List<GameObject>();
        content._modulesNormal1 = Templates(plan.Normal[0], templates); content._modulesNormal2 = Templates(plan.Normal[1], templates); content._modulesNormal3 = Templates(plan.Normal[2], templates);
        content._modulesPassage1 = Templates(plan.Passage[0], templates); content._modulesPassage2 = Templates(plan.Passage[1], templates); content._modulesPassage3 = Templates(plan.Passage[2], templates);
        content._modulesDeadEnd1 = Templates(plan.DeadEnd[0], templates); content._modulesDeadEnd2 = Templates(plan.DeadEnd[1], templates); content._modulesDeadEnd3 = Templates(plan.DeadEnd[2], templates);
        content._modulesExtraction1 = Templates(plan.Extraction[0], templates); content._modulesExtraction2 = Templates(plan.Extraction[1], templates); content._modulesExtraction3 = Templates(plan.Extraction[2], templates);
        Levels.RegisterLevel(content);
        if (!ownStart)
        {
            // Fallback: a vanilla start room, complete with its truck, in front of the Fable rooms.
            level.StartRooms = new List<PrefabRef>(donorLevel.StartRooms);
            Plugin.Log.LogWarning("[Fable] Using " + donorLevel.name + " start rooms (" +
                (donor.Truck == null ? "no compatible truck to clone" : "FableRealmVanillaStartRoom is on") + ").");
        }
        if (level.StartRooms == null || level.StartRooms.Count == 0 || level.ModulesNormal1.Count == 0)
        { Plugin.Log.LogError("[Fable] REPOLib did not register the rooms; Fable Realm is not available."); return; }

        foreach (var room in plan.Special)
        {
            if (!templates.TryGetValue(room.id, out var template)) continue;
            var reference = NetworkPrefabs.RegisterNetworkPrefab("Level/" + LevelName + "/Special/" + room.id, template);
            if (reference != null) level.ModulesSpecial.Add(reference);
        }
        // One special tile per level, and each special room is listed once: the bean puzzle cannot appear twice.
        level.ModulesSpecialAmount = level.ModulesSpecial.Count > 0 ? plan.SpecialAmount : 0;

        Level = level;
        Plugin.Log.LogInfo($"[Fable] Registered {LevelName}: {templates.Count} rooms from {BundleFile}, start rooms {level.StartRooms.Count}, " +
            $"special {level.ModulesSpecial.Count} (amount {level.ModulesSpecialAmount}), donor {donorLevel.name}.");
    }

    private static FableRoomFile ReadContract()
    {
        var assembly = Assembly.GetExecutingAssembly();
        string resource = Array.Find(assembly.GetManifestResourceNames(), n => n.EndsWith(ContractSuffix, StringComparison.Ordinal));
        if (resource == null) throw new FileNotFoundException("Embedded room contract is missing: " + ContractSuffix);
        using var reader = new StreamReader(assembly.GetManifestResourceStream(resource));
        var file = JsonConvert.DeserializeObject<FableRoomFile>(reader.ReadToEnd(),
            new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.None, MaxDepth = 16 });
        if (file?.rooms == null) throw new InvalidDataException("Empty room contract.");
        foreach (var room in file.rooms) room.anchors ??= Array.Empty<FableAnchor>();
        return file;
    }

    // Manor first (indoor rooms, the plainest truck placement), then any other full vanilla level.
    private static Level DonorLevel(RunManager run)
    {
        Level fallback = null;
        foreach (var level in run.levels)
        {
            if (level == null || level.StartRooms == null || level.StartRooms.Count == 0 ||
                level.ModulesExtraction1 == null || level.ModulesExtraction1.Count == 0 || level.ValuablePresets == null) continue;
            if (level.name == "Level - Manor") return level;
            fallback ??= level;
        }
        return fallback;
    }

    private static FableDonor Donor(Level level)
    {
        var donor = new FableDonor();
        foreach (var reference in level.StartRooms)
        {
            var room = reference?.Prefab;
            if (room == null) continue;
            // Any extraction point will do, but one that carries the seasonal moon tower is preferred, so the
            // event works on Fable Realm whenever the game runs it.
            if (donor.Extraction == null || donor.Extraction.GetComponent<ExtractionHalloweenTower>() == null)
            {
                var point = room.GetComponentInChildren<ExtractionPoint>(true);
                if (point != null && (donor.Extraction == null || point.GetComponent<ExtractionHalloweenTower>() != null))
                    donor.Extraction = point.gameObject;
            }
            if (donor.Volume == null)
                foreach (var volume in room.GetComponentsInChildren<RoomVolume>(true))
                    if (!volume.Truck && !volume.Extraction) { donor.Volume = volume; break; }
            if (donor.Truck != null) continue;
            var safety = room.GetComponentInChildren<TruckSafetySpawnPoint>(true);
            var truck = safety != null ? safety.transform : null;
            while (truck != null && truck.parent != room.transform) truck = truck.parent;
            // Only a truck parked straight behind the room, as the doorway cut expects: local yaw 270, centred.
            if (truck == null || Mathf.Abs(truck.localPosition.x) > .1f ||
                Mathf.Abs(Mathf.DeltaAngle(truck.localEulerAngles.y, 270f)) > 1f) continue;
            donor.Truck = truck.gameObject;
            donor.TruckPosition = truck.localPosition;
            donor.TruckRotation = truck.localRotation;
            // The wall piece the truck backs into: a frame with a sill under the truck's raised floor.
            foreach (var piece in room.GetComponentsInChildren<Transform>(true))
            {
                if (!piece.name.StartsWith("Truck Hole", StringComparison.Ordinal)) continue;
                donor.Hole = piece.gameObject;
                donor.HolePosition = room.transform.InverseTransformPoint(piece.position);
                donor.HoleRotation = Quaternion.Inverse(room.transform.rotation) * piece.rotation;
                donor.HoleScale = piece.lossyScale;
                break;
            }
        }
        if (donor.Extraction == null || donor.Extraction.GetComponent<ExtractionHalloweenTower>() == null)
            foreach (var reference in level.ModulesExtraction1)
            {
                var point = reference?.Prefab != null ? reference.Prefab.GetComponentInChildren<ExtractionPoint>(true) : null;
                if (point == null || (donor.Extraction != null && point.GetComponent<ExtractionHalloweenTower>() == null)) continue;
                donor.Extraction = point.gameObject;
                if (point.GetComponent<ExtractionHalloweenTower>() != null) break;
            }
        Plugin.Log.LogInfo("[Fable] Donor extraction point: " + (donor.Extraction == null ? "none" : donor.Extraction.name +
            (donor.Extraction.GetComponent<ExtractionHalloweenTower>() != null ? " (with the moon tower)" : " (no moon tower on it)")) +
            ", truck hole: " + (donor.Hole != null ? donor.Hole.name : "none") + ".");
        return donor;
    }

    // Level-wide fog and ambient light from the recipes (their average), on the donor's distances and grading.
    private static void Atmosphere(Level level, Level donor, FableRoom[] rooms)
    {
        level.FogStartDistance = donor.FogStartDistance; level.FogEndDistance = donor.FogEndDistance;
        level.ColorTemperature = donor.ColorTemperature; level.ColorFilter = donor.ColorFilter;
        level.BloomIntensity = donor.BloomIntensity; level.BloomThreshold = donor.BloomThreshold;
        level.LensDistortionIntensity = donor.LensDistortionIntensity;
        level.VignetteColor = donor.VignetteColor; level.VignetteIntensity = donor.VignetteIntensity; level.VignetteSmoothness = donor.VignetteSmoothness;
        Color fog = Color.clear, ambient = Color.clear; int fogCount = 0, ambientCount = 0;
        foreach (var room in rooms)
        {
            if (ColorUtility.TryParseHtmlString(room.fog ?? "", out var f)) { fog += f; fogCount++; }
            if (ColorUtility.TryParseHtmlString(room.ambient ?? "", out var a)) { ambient += a; ambientCount++; }
        }
        level.FogColor = fogCount > 0 ? Opaque(fog / fogCount) : donor.FogColor;
        level.AmbientColor = ambientCount > 0 ? Opaque(ambient / ambientCount) : donor.AmbientColor;
        level.AmbientColorAdaptation = ambientCount > 0 ? Opaque(ambient / ambientCount * 1.6f) : donor.AmbientColorAdaptation;
    }

    private static Color Opaque(Color color) => new(Mathf.Clamp01(color.r), Mathf.Clamp01(color.g), Mathf.Clamp01(color.b), 1f);

    private static List<GameObject> Templates(List<FableRoom> rooms, Dictionary<string, GameObject> templates)
    {
        var list = new List<GameObject>();
        foreach (var room in rooms)
            if (templates.TryGetValue(room.id, out var template)) list.Add(template);
        return list;
    }
}
