using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using HarmonyLib;
using IncreaseHardMod.Drones;
using Photon.Pun;
using REPOLib.Modules;
using UnityEngine;
using Object = UnityEngine.Object;

namespace IncreaseHardMod.Valuables;

internal static class InteractiveValuables
{
    internal static ConfigEntry<float> FlowSeconds;
    private static readonly HashSet<string> Registered = new();

    [HarmonyPatch(typeof(RunManager), "Awake")]
    private static class Register
    {
        // Queue models before REPOLib's lowest-priority valuable preset initialization.
        [HarmonyPostfix, HarmonyPriority(Priority.High)]
        private static void Postfix(RunManager __instance)
        {
            try
            {
            if (Plugin.Instance == null) return;
            var cfg = Plugin.Instance.Config;
            if (!cfg.Bind("Valuable Collection", "InteractiveValuables", true, "Add the custom valuable collection as collectible loot.").Value) return;
            FlowSeconds ??= cfg.Bind("Valuable Collection", "WaterWatchFlowSeconds", 32f, new ConfigDescription("Seconds for a full liquid transfer.", new AcceptableValueRange<float>(12f, 120f)));
            var donor = FindDonor(__instance);
            if (donor == null) { Plugin.Log.LogWarning("[Valuables] No safe inert donor was found; interactive valuables were not registered.");return; }
            CollectionSpawnPools.Prepare(__instance);
            foreach(var entry in CollectionCatalog.Entries)Make(donor,entry);
            }
            catch (Exception e) { Plugin.Log.LogError("[Valuables] Interactive valuable initialization stopped: " + e); }
        }
    }

    private static ValuableObject FindDonor(RunManager run)
    {
        var candidates = new Dictionary<string, ValuableObject>(StringComparer.Ordinal);
        foreach (var level in run.levels)
        foreach (var preset in level.ValuablePresets)
        foreach (var reference in preset.tiny.Concat(preset.small).Concat(preset.medium))
        {
            var prefab = reference?.Prefab;
            var valuable = prefab != null ? prefab.GetComponent<ValuableObject>() : null;
            if (valuable == null || valuable.valuePreset == null || valuable.physAttributePreset == null || valuable.durabilityPreset == null || valuable.audioPreset == null) continue;
            string n = prefab.name.ToLowerInvariant();
            if (!n.Contains("vase") && !n.Contains("book") && !n.Contains("bowl") && !n.Contains("plate")) continue;
            if (prefab.GetComponent<ItemAttributes>() != null || prefab.GetComponentsInChildren<HurtCollider>(true).Length != 0) continue;
            if (prefab.GetComponents<MonoBehaviour>().Any(m => m != null && m.GetType().Name.StartsWith("Valuable", StringComparison.Ordinal) && m is not ValuableObject)) continue;
            candidates[prefab.name] = valuable;
        }
        return candidates.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => p.Value).FirstOrDefault();
    }

    private static void Make(ValuableObject donor, CollectionEntry entry)
    {
        string key=entry.Key;float mass=entry.Mass;
        var volume=key=="cannon"||key=="bomb"||key=="skeleton"?ValuableVolume.Type.Big:key=="centrifuge"||key=="monoblock_pc"?ValuableVolume.Type.Medium:ValuableVolume.Type.Small;
        if (Registered.Contains(key)) return;
        GameObject go = null;
        try
        {
            var pools=CollectionSpawnPools.For(entry);
            ValuableVisuals.Load(key); // Missing resources must not register an invisible valuable.
            go = Object.Instantiate(donor.gameObject, DroneItems.EnsureHolder().transform);
            go.name = "Valuable IHM " + (key == "water_timer" ? "Water Watch" : key.Replace('_', ' '));
            go.transform.localPosition = Vector3.zero;go.transform.localRotation = Quaternion.identity;go.transform.localScale = Vector3.one*ValuableLayout.Scale(key);
            Material template = null;
            var donorRenderers=go.GetComponentsInChildren<Renderer>(true);
            foreach (var renderer in donorRenderers)
            {
                template ??= renderer.sharedMaterials.FirstOrDefault(m => m != null && (m.HasProperty("_BaseTexture") || m.HasProperty("_BaseMap") || m.HasProperty("_MainTex")));
            }
            IncreaseHardMod.Visuals.DonorVisuals.RemoveGeometry(donorRenderers);
            foreach (var c in go.GetComponentsInChildren<PhysGrabObjectCollider>(true)) Object.DestroyImmediate(c);
            foreach (var c in go.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
            var value = go.GetComponent<ValuableObject>();value.volumeType = volume;
            value.audioPreset = ValuableFoley.Preset(key,donor.audioPreset);
            value.valuePreset = Object.Instantiate(donor.valuePreset);value.valuePreset.valueMin = entry.Min;value.valuePreset.valueMax = entry.Max;
            value.durabilityPreset=Object.Instantiate(donor.durabilityPreset);
            var durability=CollectionCatalog.DurabilityFor(entry.Fragility);
            value.durabilityPreset.fragility=durability.Fragility;value.durabilityPreset.durability=durability.Durability;
            value.physAttributePreset = Object.Instantiate(donor.physAttributePreset);value.physAttributePreset.mass = mass;
            var body = go.GetComponent<Rigidbody>();body.mass = mass;body.constraints=RigidbodyConstraints.None;
            body.drag=.05f;body.angularDrag=.25f;
            if(key=="cannon"||key=="robot_vacuum"||key=="skeleton")WheeledPropPhysics.Prepare(body);
            var pgo = go.GetComponent<PhysGrabObject>();pgo.massOriginal = mass;pgo.colliders.Clear();
            if(key=="cannon"||key=="robot_vacuum"||key=="skeleton")
            {
                pgo.spawnTorque=Vector3.zero;
                go.AddComponent<WheeledSpawnPose>();
            }
            var art = ValuableVisuals.Attach(go, key, template);
            if (key == "cannon")
            {
                WheelCollider(go, new Vector3(-.64f, .545f, -.1f));WheelCollider(go, new Vector3(.64f, .545f, -.1f));
                BoxCollider(go, "Cannon axle", new Vector3(0, .545f, -.1f), new Vector3(1.35f,.09f,.09f));
                BoxCollider(go, "Cannon barrel", new Vector3(0,1.03f,-.22f), new Vector3(.33f,.32f,1.2f));
                BoxCollider(go, "Cannon trail", new Vector3(0,.43f,.45f), new Vector3(.37f,.14f,1.25f), Quaternion.Euler(34,0,0));
                BoxCollider(go, "Cannon spade", new Vector3(0,.05f,1.17f), new Vector3(.34f,.09f,.23f));
                go.AddComponent<CannonWheelMotion>();
            }
            else if (key == "water_timer")
            {
                BoxCollider(go, "Water watch collider", new Vector3(0,.13f,0),new Vector3(.104f,.26f,.038f));
                go.AddComponent<WaterWatch>();
            }
            else
            {
                if(key=="centrifuge")
                {
                    // Hollow case: a bounding box over the rotor would block selecting the seated tubes.
                    BoxCollider(go,"Centrifuge base",new Vector3(0,.04f,0),new Vector3(.42f,.06f,.42f));
                    BoxCollider(go,"Centrifuge hub",new Vector3(0,.14f,0),new Vector3(.06f,.07f,.06f));
                    BoxCollider(go,"Centrifuge controls",new Vector3(0,.10f,-.223f),new Vector3(.112f,.085f,.024f));
                    foreach(float x in new[]{-.14f,.14f})foreach(float z in new[]{-.12f,.12f})
                        BoxCollider(go,"Centrifuge foot",new Vector3(x,.013f,z),new Vector3(.04f,.025f,.04f));
                    for(int i=0;i<20;i++)
                    {
                        float a=i*Mathf.PI*2/20;
                        BoxCollider(go,"Centrifuge wall "+i,new Vector3(.22f*Mathf.Cos(a),.123f,.22f*Mathf.Sin(a)),new Vector3(.026f,.18f,.08f),Quaternion.Euler(0,-a*Mathf.Rad2Deg,0));
                    }
                }
                bool manekiCollision=key=="maneki_neko"&&ManekiPhysics.Attach(go,art);
                foreach(var binding in art.bindings)
                {
                    if(manekiCollision)continue;
                    if(key=="centrifuge"&&binding.group.StartsWith("tube_",StringComparison.Ordinal))continue;
                    if(key=="centrifuge")
                    {
                        if(binding.group=="lid")
                        {
                            var lid=CollisionChild(go,"Centrifuge lid collision");lid.transform.localPosition=binding.node.localPosition;
                            var collider=lid.AddComponent<MeshCollider>();collider.sharedMesh=binding.node.GetComponent<MeshFilter>().sharedMesh;collider.convex=true;
                        }
                        continue;
                    }
                    if(key=="skeleton"&&binding.group!="static"&&!binding.group.StartsWith("caster_",StringComparison.Ordinal))continue;
                    if(key=="bomb")
                    {
                        var shell=CollisionChild(go,"Bomb collision "+binding.group);shell.transform.localPosition=binding.node.localPosition;
                        var hull=shell.AddComponent<MeshCollider>();hull.sharedMesh=binding.node.GetComponent<MeshFilter>().sharedMesh;hull.convex=true;
                        continue;
                    }
                    var bounds=binding.node.GetComponent<MeshFilter>().sharedMesh.bounds;
                    if(binding.group.StartsWith("drive_",StringComparison.Ordinal)||binding.group.StartsWith("caster_",StringComparison.Ordinal))
                    {
                        WheelCollider(go,binding.node.localPosition+bounds.center,Mathf.Max(bounds.extents.y,bounds.extents.z),bounds.extents.x);
                        continue;
                    }
                    if(key=="maneki_neko"&&binding.group=="beckoning_paw")
                    {
                        var paw=CollisionChild(go,"Beckoning paw collision "+binding.node.name);
                        paw.transform.SetParent(art.beckonPivot,false);
                        paw.transform.localPosition=art.beckonPivot.InverseTransformPoint(binding.node.TransformPoint(bounds.center));
                        paw.AddComponent<UnityEngine.BoxCollider>().size=bounds.size;
                        continue;
                    }
                    BoxCollider(go,"Collection collision "+binding.group,binding.node.localPosition+bounds.center,Vector3.Max(bounds.size,new Vector3(.006f,.006f,.006f)));
                }
                if(key=="robot_vacuum"||key=="skeleton")go.AddComponent<FloorPropMotion>();
                if(key=="skeleton"||key=="centrifuge")go.AddComponent<CollectionPhysics>();
                if(CollectionLights.Applies(key))go.AddComponent<CollectionLights>();
                if(key=="maneki_neko")go.AddComponent<ManekiMotion>();
            }
            go.AddComponent<ValuableFoley>();
            if(key=="bomb"){go.AddComponent<ParticleScriptExplosion>();go.AddComponent<BombDestruction>();}
            var actual=ValuableLayout.Apply(go,art);
            value.volumeType=VolumeFor(value,actual.size*ValuableLayout.Scale(key));
            go.GetComponent<PhotonView>().RefreshRpcMonoBehaviourCache();
            if (REPOLib.Modules.Valuables.RegisterValuable(value,pools) == null) throw new InvalidOperationException("REPOLib rejected the valuable.");
            Registered.Add(key);Plugin.Log.LogInfo($"[Valuables] Registered {key}: ${entry.Min:0}-${entry.Max:0}, total {CollectionCatalog.TotalMass(entry):0.##} kg (root {mass:0.##}), {entry.Fragility}; pools: {string.Join(", ",pools)}");
        }
        catch (Exception e)
        {
            if (go != null) Object.DestroyImmediate(go);
            Plugin.Log.LogError("[Valuables] Failed to register " + key + ": " + e);
        }
    }

    private static ValuableVolume.Type VolumeFor(ValuableObject value,Vector3 size)
    {
        var dimensions=value.debugValuableMeshes;
        if(dimensions==null)throw new InvalidOperationException("Native valuable volume dimensions are unavailable.");
        var choices=new[]{dimensions.tiny,dimensions.small,dimensions.medium,dimensions.big,dimensions.wide,dimensions.tall,dimensions.veryTall};
        int selected=-1;float space=float.MaxValue,overflow=float.MaxValue;
        for(int i=0;i<choices.Length;i++)
        {
            if(choices[i]==null)continue;var capacity=choices[i].bounds.size;
            float excess=Mathf.Max(0,(size.x+.025f-capacity.x)/capacity.x)+Mathf.Max(0,(size.y+.025f-capacity.y)/capacity.y)+Mathf.Max(0,(size.z+.025f-capacity.z)/capacity.z);
            float room=capacity.x*capacity.y*capacity.z;
            if(excess<overflow-.0001f||Mathf.Abs(excess-overflow)<.0001f&&room<space){space=room;overflow=excess;selected=i;}
        }
        if(selected<0)throw new InvalidOperationException("Native spawn meshes are missing.");
        // Some native wide slots are shallower than the reference models. The spawn patch
        // checks their complete footprint against the actual scene before allowing them.
        return (ValuableVolume.Type)selected;
    }

    private static GameObject CollisionChild(GameObject root, string name)
    {
        var child = new GameObject(name);child.transform.SetParent(root.transform,false);int layer = LayerMask.NameToLayer("PhysGrabObject");
        child.layer = layer >= 0 ? layer : root.layer;
        child.AddComponent<PhysGrabObjectCollider>();GrabSurface.Mark(child);return child;
    }

    private static void BoxCollider(GameObject root, string name, Vector3 center, Vector3 size, Quaternion? rotation = null)
    {
        var child = CollisionChild(root,name);child.transform.localPosition = center;
        if (rotation.HasValue) child.transform.localRotation = rotation.Value;
        child.AddComponent<UnityEngine.BoxCollider>().size = size;
    }

    private static void WheelCollider(GameObject root, Vector3 center,float radius=.545f,float halfWidth=.05f)
    {
        var child = CollisionChild(root,"Rolling wheel collider");child.transform.localPosition = center;
        const int n = 20;var vertices = new Vector3[n*2];var triangles = new List<int>();
        for (int side=0;side<2;side++) for (int j=0;j<n;j++)
        {
            float a=j*Mathf.PI*2/n;vertices[side*n+j]=new Vector3(side==0?-halfWidth:halfWidth,radius*Mathf.Cos(a),radius*Mathf.Sin(a));
        }
        for (int j=0;j<n;j++)
        {
            int k=(j+1)%n;triangles.AddRange(new[]{j,k,k+n,j,k+n,j+n});
            if(j>0&&j<n-1)triangles.AddRange(new[]{0,j+1,j,n,n+j,n+j+1});
        }
        var mesh=new Mesh{name="IHM convex wheel"};mesh.vertices=vertices;mesh.triangles=triangles.ToArray();mesh.RecalculateNormals();mesh.RecalculateBounds();
        var collider=child.AddComponent<MeshCollider>();collider.sharedMesh=mesh;collider.convex=true;
        WheeledPropPhysics.SetRollingContact(collider);
    }
}
