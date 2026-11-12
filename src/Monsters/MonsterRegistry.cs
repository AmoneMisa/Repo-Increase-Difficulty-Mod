using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Photon.Pun;
using REPOLib.Modules;
using REPOLib.Objects.Sdk;
using UnityEngine;
using UnityEngine.Events;
using Object = UnityEngine.Object;

namespace IncreaseHardMod.Monsters;

/// <summary>
/// Builds the mod's monsters as real REPO enemies and hands them to REPOLib.
///
/// Each one is a clone of a native ground enemy (preferably "Enemy - Animal"): its generic enemy framework
/// (Enemy, EnemyParent, EnemyHealth, EnemyRigidbody, EnemyNavMeshAgent, EnemyVision, spawn/despawn/stun
/// states, PhysGrabObject, PhotonViews) is kept, so damage of every kind, grabbing, stuns, pings, the map,
/// souls, quests, the room attack limit and the difficulty scaling all treat it as a normal monster. The donor's
/// own AI, animation scripts, attack colliders, renderers, particles and lights are removed, and every
/// persistent UnityEvent on the kept components is replaced with an empty one, so no donor attack can fire
/// through onDeath/onHurt/OnSpawn. Our <see cref="MonsterBrain"/> subscribes at runtime instead.
///
/// The clones are built once, on every client identically, in a prefix of EnemyDirector.Awake (REPOLib adds
/// registered enemies to the director in its postfix of the same method). Whether the director may PICK one is a
/// host decision (<see cref="AllowedInRoster"/>); the Golden Pig is never picked and is spawned by its own roll.
/// </summary>
internal static class MonsterRegistry
{
    internal sealed class Spec
    {
        public MonsterKind Kind;
        public string Name, NameRu;
        public EnemyParent.Difficulty Difficulty;
        public int Health;
        public float Mass, Height, Speed, Hover, GrabForce;
        public string Art;
    }

    internal static readonly Spec[] Specs =
    {
        new() { Kind = MonsterKind.Pig, Name = "Golden Pig", NameRu = "Золотая свинка", Difficulty = EnemyParent.Difficulty.Difficulty1,
            Health = MonsterRules.PigHealth(0), Mass = MonsterRules.PigMass(0), Height = .7f, Speed = 2.2f, Hover = 1.3f, GrabForce = 1.4f, Art = "monster_pig" },
        new() { Kind = MonsterKind.Witch, Name = "Witch", NameRu = "Ведьма", Difficulty = EnemyParent.Difficulty.Difficulty2,
            Health = 150, Mass = 70f, Height = 1.8f, Speed = 2.2f, Hover = MonsterRules.WitchHover, GrabForce = 3f, Art = "monster_witch" },
        new() { Kind = MonsterKind.Basilisk, Name = "Basilisk", NameRu = "Василиск", Difficulty = EnemyParent.Difficulty.Difficulty2,
            Health = 170, Mass = 130f, Height = .45f, Speed = 3.6f, Hover = 0f, GrabForce = 4f, Art = "monster_basilisk" },
        new() { Kind = MonsterKind.Gorgon, Name = "Gorgon", NameRu = "Горгона", Difficulty = EnemyParent.Difficulty.Difficulty3,
            Health = 360, Mass = 450f, Height = 1.5f, Speed = 2.6f, Hover = 0f, GrabForce = 7f, Art = "monster_gorgon" },
        new() { Kind = MonsterKind.Naga, Name = "Naga Queen", NameRu = "Королева Наг", Difficulty = EnemyParent.Difficulty.Difficulty3,
            Health = 260, Mass = 210f, Height = 2.1f, Speed = 3f, Hover = 0f, GrabForce = 5f, Art = "monster_naga" },
        new() { Kind = MonsterKind.Fairy, Name = "Mischief Fairy", NameRu = "Шкодная фея", Difficulty = EnemyParent.Difficulty.Difficulty1,
            Health = MonsterRules.FairyHealth, Mass = 6f, Height = .55f, Speed = 3f, Hover = MonsterRules.FairyHover, GrabForce = .6f, Art = "monster_fairy" },
        new() { Kind = MonsterKind.Twins, Name = "Hansel and Gretel", NameRu = "Гензель и Гретель", Difficulty = EnemyParent.Difficulty.Difficulty2,
            Health = MonsterRules.TwinsHealth, Mass = 55f, Height = 1.2f, Speed = 3.2f, Hover = 0f, GrabForce = 3f, Art = "monster_twins" },
    };

    internal static Spec Of(MonsterKind kind) => Specs[(int)kind];

    private static readonly Dictionary<MonsterKind, EnemySetup> Setups = new();
    private static bool _built;
    private static GameObject _holder;

    // Generic enemy framework kept on the clone; any other Enemy* script is the donor's own AI or animation.
    private static readonly HashSet<string> KeepEnemyTypes = new(StringComparer.Ordinal)
    {
        "Enemy", "EnemyParent", "EnemyHealth", "EnemyRigidbody", "EnemyNavMeshAgent", "EnemyVision", "EnemyPlayerDistance",
        "EnemyOnScreen", "EnemyPlayerRoom", "EnemyStateSpawn", "EnemyStateDespawn", "EnemyStateStunned", "EnemyStateInvestigate",
        "EnemyGrounded", "EnemyJump", "EnemyAttackStuckPhysObject",
    };

    [HarmonyPatch(typeof(EnemyDirector), "Awake")]
    private static class BuildOnDirectorAwake
    {
        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        private static void Prefix(EnemyDirector __instance)
        {
            try { Build(__instance); }
            catch (Exception e) { Plugin.Log.LogError($"[Monsters] Build failed, new monsters disabled: {e}"); }
        }
    }

    /// <summary>Host roster filter: our monsters only when enabled; the pig never through the director.</summary>
    internal static bool AllowedInRoster(EnemySetup setup)
    {
        foreach (var kv in Setups)
        {
            if (kv.Value != setup) continue;
            if (!ModConfig.MonstersEnabled.Value) return false;
            return kv.Key switch
            {
                MonsterKind.Witch => ModConfig.WitchEnabled.Value,
                MonsterKind.Basilisk => ModConfig.BasiliskEnabled.Value,
                MonsterKind.Gorgon => ModConfig.GorgonEnabled.Value,
                MonsterKind.Naga => ModConfig.NagaEnabled.Value,
                MonsterKind.Fairy => ModConfig.FairyEnabled.Value,
                MonsterKind.Twins => ModConfig.TwinsEnabled.Value,
                _ => false,
            };
        }
        return true;
    }

    /// <summary>
    /// Host: who may fill this enemy slot. With the configured chance the slot goes to the mod's monsters of the
    /// slot's tier, otherwise to the native ones. Mixed in one list, the game's own draw (the highest random score
    /// wins) gave the few new monsters almost no slots among some ten native candidates per tier.
    /// </summary>
    internal static List<EnemySetup> RollRoster(List<EnemySetup> eligible)
    {
        var ours = eligible.FindAll(setup => Setups.ContainsValue(setup));
        if (ours.Count == 0) return eligible;
        var native = eligible.FindAll(setup => !Setups.ContainsValue(setup));
        return MonsterRules.SlotForNewMonster(UnityEngine.Random.value, ModConfig.NewMonsterChance.Value, ours.Count, native.Count) ? ours : native;
    }

    internal static EnemySetup SetupOf(MonsterKind kind) => Setups.TryGetValue(kind, out var s) ? s : null;

    [HarmonyPatch(typeof(EnemyDirector), "AmountSetup")]
    private static class RosterDiagnostics
    {
        [HarmonyPostfix, HarmonyPriority(Priority.Last)]
        private static void Postfix(EnemyDirector __instance)
        {
            if (!SemiFunc.IsMasterClientOrSingleplayer() || !SemiFunc.RunIsLevel()) return;
            int depth = Difficulty.LevelsCompleted;
            int runs = DataDirector.instance.SettingValueFetch(DataDirector.Setting.RunsPlayed);
            var candidates = new List<string>();
            foreach (var spec in Specs)
            {
                if (spec.Kind == MonsterKind.Pig) continue;
                var setup = SetupOf(spec.Kind);
                string reason;
                if (setup == null) reason = "not registered";
                else if (!AllowedInRoster(setup)) reason = "disabled";
                else if (!__instance.enemiesDifficulty1.Contains(setup) && !__instance.enemiesDifficulty2.Contains(setup) &&
                         !__instance.enemiesDifficulty3.Contains(setup)) reason = "absent from director";
                else if (runs < setup.runsPlayed || (setup.levelsCompletedCondition &&
                         (depth < setup.levelsCompletedMin || (setup.levelsCompletedMax != 0 && depth > setup.levelsCompletedMax))))
                    reason = "progress locked";
                else reason = "eligible";
                candidates.Add(spec.Name + ": " + reason);
            }
            var selected = new List<string>();
            foreach (var setup in __instance.enemyListCurrent)
            {
                if (setup == null) { selected.Add("<missing setup>"); continue; }
                var match = Array.Find(Specs, spec => SetupOf(spec.Kind) == setup);
                selected.Add(match != null ? match.Name : setup.name);
            }
            Plugin.Log.LogInfo($"[Monsters] Slot chance {ModConfig.NewMonsterChance.Value:P0}. Roster candidates: " + string.Join("; ", candidates) +
                               ". Selected for level: " + string.Join(", ", selected) + ". Pig rolls separately.");
        }
    }

    private static void Build(EnemyDirector director)
    {
        if (_built) return;
        _built = true;
        var donor = FindDonor(director);
        if (donor == null) { Plugin.Log.LogWarning("[Monsters] No suitable donor enemy found; new monsters disabled."); return; }
        _holder = new GameObject("IHM_MonsterPrefabs");
        _holder.SetActive(false);
        Object.DontDestroyOnLoad(_holder);
        Plugin.Log.LogInfo($"[Monsters] Donor enemy: {donor.name}.");
        foreach (var spec in Specs)
        {
            try { Make(donor, spec); }
            catch (Exception e) { Plugin.Log.LogError($"[Monsters] {spec.Name} not registered: {e}"); }
        }
    }

    // Prefer the Animal (small ground enemy, rigidbody + agent, simple AI); else the first suitable tier-1 enemy
    // by prefab name, so every client picks the same donor.
    private static GameObject FindDonor(EnemyDirector director)
    {
        var candidates = new List<(string name, GameObject prefab)>();
        foreach (var list in new[] { director.enemiesDifficulty1, director.enemiesDifficulty2, director.enemiesDifficulty3 })
        {
            if (list == null) continue;
            foreach (var setup in list)
            {
                if (setup == null || setup.spawnObjects == null || setup.spawnObjects.Count != 1) continue;
                var prefab = setup.spawnObjects[0]?.Prefab;
                if (prefab == null) continue;
                var enemy = prefab.GetComponentInChildren<Enemy>(true);
                if (enemy == null || prefab.GetComponentInChildren<EnemyRigidbody>(true) == null ||
                    prefab.GetComponentInChildren<EnemyNavMeshAgent>(true) == null || prefab.GetComponentInChildren<EnemyVision>(true) == null ||
                    prefab.GetComponentInChildren<EnemyStateStunned>(true) == null || prefab.GetComponentInChildren<EnemyHealth>(true) == null) continue;
                candidates.Add((prefab.name, prefab));
            }
        }
        foreach (var c in candidates) if (c.name == "Enemy - Animal") return c.prefab;
        candidates.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
        return candidates.Count > 0 ? candidates[0].prefab : null;
    }

    private static void Make(GameObject donor, Spec spec)
    {
        var go = Object.Instantiate(donor, _holder.transform);
        go.name = "Enemy - IHM " + spec.Kind;
        Strip(go);

        var parent = go.GetComponent<EnemyParent>();
        var enemy = go.GetComponentInChildren<Enemy>(true);
        var health = go.GetComponentInChildren<EnemyHealth>(true);
        var body = go.GetComponentInChildren<EnemyRigidbody>(true);
        if (parent == null || enemy == null || health == null || body == null) throw new InvalidOperationException("donor framework incomplete");
        parent.enemyName = spec.Name;
        parent.enemyNameLocalized = null;
        parent.difficulty = spec.Difficulty;
        health.health = spec.Health;
        health.spawnValuable = true;
        health.impactHurt = true;
        health.objectHurt = true;
        // Thrown or slammed against the floor/walls: hurts while stunned or held (MonsterBrain keeps it on while held).
        health.impactLightDamage = MonsterRules.ImpactDamage(spec.Health, 0);
        health.impactMediumDamage = MonsterRules.ImpactDamage(spec.Health, 1);
        health.impactHeavyDamage = MonsterRules.ImpactDamage(spec.Health, 2);
        health.objectHurtMultiplier = 1f;
        body.grabForceNeeded = ScriptableObject.CreateInstance<GrabForce>();
        body.grabForceNeeded.amount = spec.GrabForce;
        body.gravity = spec.Hover <= 0f;
        var rb = body.GetComponent<Rigidbody>();
        if (rb != null) rb.mass = spec.Mass;
        ScaleBody(body.transform, spec.Height);
        // The naga is a duellist, not a sentry: shorter native sight so players can hide from her.
        var vision = enemy.GetComponent<EnemyVision>();
        if (vision != null && spec.Kind == MonsterKind.Naga)
        {
            vision.VisionDistance = Mathf.Min(vision.VisionDistance, MonsterRules.NagaSight);
            vision.VisionDistanceClose = Mathf.Min(vision.VisionDistanceClose, 2.5f);
            vision.VisionDistanceCloseCrouch = Mathf.Min(vision.VisionDistanceCloseCrouch, 1.2f);
        }
        var agent = enemy.GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (agent != null && spec.Kind == MonsterKind.Basilisk)
        {
            agent.height = spec.Height;
            agent.radius = .18f;
            agent.baseOffset = 0f;
        }

        // Visual root (filled at runtime from the embedded model) and the brain on the Enemy object.
        var visualRoot = new GameObject("IHM Monster Visual").transform;
        // Under EnableObject, so a despawned monster's body disappears with the rest of it.
        visualRoot.SetParent(parent.EnableObject != null ? parent.EnableObject.transform : go.transform, false);
        visualRoot.gameObject.AddComponent<MonsterVisual>().Configure(spec.Kind, spec.Art, spec.Height);
        health.meshParent = visualRoot;
        MonsterBrain brain = spec.Kind switch
        {
            MonsterKind.Pig => enemy.gameObject.AddComponent<PigBrain>(),
            MonsterKind.Witch => enemy.gameObject.AddComponent<WitchBrain>(),
            MonsterKind.Basilisk => enemy.gameObject.AddComponent<BasiliskBrain>(),
            MonsterKind.Gorgon => enemy.gameObject.AddComponent<GorgonBrain>(),
            MonsterKind.Fairy => enemy.gameObject.AddComponent<FairyBrain>(),
            MonsterKind.Twins => enemy.gameObject.AddComponent<TwinsBrain>(),
            _ => enemy.gameObject.AddComponent<NagaBrain>(),
        };
        brain.Kind = spec.Kind;

        var setup = ScriptableObject.CreateInstance<EnemySetup>();
        setup.name = "Enemy - IHM " + spec.Kind;
        setup.status = EnemySetup.Status.Finalized;
        setup.levelsCompletedCondition = spec.Difficulty == EnemyParent.Difficulty.Difficulty3;
        setup.levelsCompletedMin = spec.Difficulty == EnemyParent.Difficulty.Difficulty3 ? 2 : 0;
        setup.levelsCompletedMax = 0;
        setup.rarityPreset = ScriptableObject.CreateInstance<RarityPreset>();
        setup.rarityPreset.chance = ModConfig.MonsterRarity.Value;
        var content = ScriptableObject.CreateInstance<EnemyContent>();
        Set(content, "_setup", setup);
        Set(content, "_spawnObjects", new List<GameObject> { go });
        Enemies.RegisterEnemy(content);
        if (setup.spawnObjects == null || setup.spawnObjects.Count == 0) throw new InvalidOperationException("REPOLib refused the prefab");
        Setups[spec.Kind] = setup;
        Plugin.Log.LogInfo($"[Monsters] Registered {spec.Name} ({spec.Difficulty}, {spec.Health} HP, {spec.Mass} kg).");
    }

    private static void Set(object target, string field, object value) =>
        target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)?.SetValue(target, value);

    private static void Strip(GameObject go)
    {
        // Donor AI and animation scripts first (they may require each other), then attack colliders and views
        // onto removed animators. Two passes cover RequireComponent chains.
        for (int pass = 0; pass < 2; pass++)
            foreach (var mb in go.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb == null) continue;
                string name = mb.GetType().Name;
                bool remove = (name.StartsWith("Enemy", StringComparison.Ordinal) && !KeepEnemyTypes.Contains(name)) ||
                              mb is HurtCollider || name == "PhotonAnimatorView" || name.StartsWith("Animation", StringComparison.Ordinal);
                if (remove) Object.DestroyImmediate(mb);
            }
        foreach (var c in go.GetComponentsInChildren<Animator>(true)) Object.DestroyImmediate(c);
        foreach (var c in go.GetComponentsInChildren<ParticleSystemRenderer>(true)) Object.DestroyImmediate(c);
        foreach (var c in go.GetComponentsInChildren<ParticleSystem>(true)) Object.DestroyImmediate(c);
        foreach (var c in go.GetComponentsInChildren<Light>(true)) Object.DestroyImmediate(c);
        foreach (var c in go.GetComponentsInChildren<SkinnedMeshRenderer>(true)) Object.DestroyImmediate(c);
        foreach (var c in go.GetComponentsInChildren<LineRenderer>(true)) Object.DestroyImmediate(c);
        foreach (var c in go.GetComponentsInChildren<MeshRenderer>(true)) Object.DestroyImmediate(c);
        foreach (var c in go.GetComponentsInChildren<MeshFilter>(true)) Object.DestroyImmediate(c);
        foreach (var a in go.GetComponentsInChildren<AudioSource>(true)) { a.playOnAwake = false; a.loop = false; }
        // No donor listener may survive on the kept components.
        foreach (var mb in go.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (mb == null) continue;
            for (var t = mb.GetType(); t != null && t != typeof(MonoBehaviour); t = t.BaseType)
                foreach (var f in t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                    if (typeof(UnityEventBase).IsAssignableFrom(f.FieldType) && !f.FieldType.IsAbstract &&
                        f.FieldType.GetConstructor(Type.EmptyTypes) != null)
                        f.SetValue(mb, Activator.CreateInstance(f.FieldType));
        }
        // Views must not observe destroyed components.
        foreach (var view in go.GetComponentsInChildren<PhotonView>(true))
            view.ObservedComponents?.RemoveAll(c => c == null);
    }

    // Scale the physical body (its colliders) to the monster's size from the donor's collider height.
    private static void ScaleBody(Transform body, float height)
    {
        float top = float.MinValue, bottom = float.MaxValue;
        foreach (var c in body.GetComponentsInChildren<Collider>(true))
        {
            if (c.isTrigger) continue;
            Vector3 center; float half;
            switch (c)
            {
                case BoxCollider b: center = b.center; half = b.size.y * .5f; break;
                case CapsuleCollider k: center = k.center; half = k.direction == 1 ? k.height * .5f : k.radius; break;
                case SphereCollider s: center = s.center; half = s.radius; break;
                default: continue;
            }
            float scale = Mathf.Abs(c.transform.lossyScale.y / Mathf.Max(.0001f, body.lossyScale.y));
            float y = body.InverseTransformPoint(c.transform.TransformPoint(center)).y;
            top = Mathf.Max(top, y + half * scale);
            bottom = Mathf.Min(bottom, y - half * scale);
        }
        if (top <= bottom) return;
        // A minimum of 0.4 left the snake taller than its requested 45 cm
        // whenever the donor was taller than 1.125 m.
        float factor = Mathf.Clamp(height / (top - bottom), .05f, 4f);
        body.localScale *= factor;
    }

    // ------------------------------------------------------------------ golden pig spawn (host)

    private static LevelGenerator _pigRolled;

    [HarmonyPatch(typeof(LevelGenerator), "GenerateDone")]
    private static class PigRoll
    {
        [HarmonyPostfix]
        private static void Postfix(LevelGenerator __instance)
        {
            if (!SemiFunc.IsMasterClientOrSingleplayer() || !SemiFunc.RunIsLevel() || _pigRolled == __instance) return;
            _pigRolled = __instance;
            PigBrain.ResetLevel();
            if (!ModConfig.MonstersEnabled.Value || !ModConfig.PigEnabled.Value) return;
            if (UnityEngine.Random.value >= ModConfig.PigSpawnChance.Value) return;
            var setup = SetupOf(MonsterKind.Pig);
            var points = SemiFunc.LevelPointsGetAll();
            if (setup == null || points == null || points.Count == 0) return;
            var point = points[UnityEngine.Random.Range(0, points.Count)];
            var spawned = Enemies.SpawnEnemy(setup, point.transform.position, Quaternion.identity);
            Plugin.Log.LogInfo($"[Monsters] Golden Pig rolled for this level: {(spawned != null && spawned.Count > 0 ? "spawned" : "spawn failed")}.");
        }
    }

    // ------------------------------------------------------------------ soul values (host)

    private sealed class PendingSoul { public Vector3 Position; public int Value; public float Time; }
    private static readonly List<PendingSoul> Pending = new();

    /// <summary>Host: the next soul orb spawned at <paramref name="position"/> is worth <paramref name="value"/>.</summary>
    internal static void ExpectSoul(Vector3 position, int value)
    {
        Pending.RemoveAll(p => Time.time - p.Time > 10f);
        Pending.Add(new PendingSoul { Position = position, Value = value, Time = Time.time });
    }

    [HarmonyPatch(typeof(ValuableObject), "DollarValueSetLogic")]
    private static class SoulValue
    {
        [HarmonyPrefix]
        private static void Prefix(ValuableObject __instance, out bool __state) => __state = __instance.dollarValueSet;

        // Before the other soul modifiers (BiggerSoulDrops) so they still multiply the monster's own value.
        [HarmonyPostfix]
        [HarmonyPriority(Priority.First)]
        private static void Postfix(ValuableObject __instance, bool __state)
        {
            if (__state || Pending.Count == 0 || !SemiFunc.IsMasterClientOrSingleplayer() ||
                !Patches.Effects.BiggerSoulDropsEffect.IsEnemyValuable(__instance)) return;
            PendingSoul best = null;
            float bestDistance = 3f * 3f;
            foreach (var p in Pending)
            {
                float d = (p.Position - __instance.transform.position).sqrMagnitude;
                if (d < bestDistance && Time.time - p.Time < 10f) { best = p; bestDistance = d; }
            }
            if (best == null) return;
            Pending.Remove(best);
            __instance.dollarValueOriginal = best.Value;
            __instance.dollarValueCurrent = best.Value;
        }
    }

    /// <summary>The team's highest Soul Hunter level (never a sum of players).</summary>
    internal static int TeamSoulHunter()
    {
        int best = 0;
        foreach (var p in SemiFunc.PlayerGetList())
            if (p != null) best = Mathf.Max(best, Skills.SkillEffects.Level(p, Skills.SkillCatalog.SoulHunter));
        return best;
    }

    /// <summary>Host: spawns a small native valuable (the pig's golden souvenir) at a point.</summary>
    internal static void SpawnSouvenir(Vector3 position)
    {
        var level = LevelGenerator.Instance != null ? LevelGenerator.Instance.Level : null;
        var pool = new List<PrefabRef>();
        if (level != null && level.ValuablePresets != null)
            foreach (var preset in level.ValuablePresets)
                if (preset != null) { if (preset.small != null) pool.AddRange(preset.small); if (preset.tiny != null) pool.AddRange(preset.tiny); }
        pool.RemoveAll(p => p == null || string.IsNullOrEmpty(p.ResourcePath));
        if (pool.Count == 0) return;
        var pick = pool[UnityEngine.Random.Range(0, pool.Count)];
        if (SemiFunc.IsMultiplayer()) PhotonNetwork.InstantiateRoomObject(pick.ResourcePath, position, Quaternion.identity, 0);
        else if (pick.Prefab != null) Object.Instantiate(pick.Prefab, position, Quaternion.identity);
    }

    /// <summary>Host: spawns a random usable upgrade item (the pig's 10% jackpot).</summary>
    internal static void SpawnUpgrade(Vector3 position)
    {
        var stats = StatsManager.instance;
        if (stats == null) return;
        var pool = new List<Item>();
        foreach (var item in stats.itemDictionary.Values)
            if (item != null && item.prefab != null && !string.IsNullOrEmpty(item.prefab.ResourcePath) &&
                (item.itemType == SemiFunc.itemType.item_upgrade || item.itemType == SemiFunc.itemType.player_upgrade) &&
                !(ModConfig.PlayerUpgradesEnabled.Value && Skills.SkillShopPatch.IsTreeOwnedVanilla(item)))
                pool.Add(item);
        if (pool.Count == 0) return;
        var pick = pool[UnityEngine.Random.Range(0, pool.Count)];
        if (SemiFunc.IsMultiplayer()) PhotonNetwork.InstantiateRoomObject(pick.prefab.ResourcePath, position, Quaternion.identity, 0);
        else Object.Instantiate(pick.prefab.Prefab, position, Quaternion.identity);
    }
}
