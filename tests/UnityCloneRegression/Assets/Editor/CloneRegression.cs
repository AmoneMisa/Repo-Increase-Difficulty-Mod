using System;
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using IncreaseHardMod.Visuals;
using IncreaseHardMod.Valuables;
using Object=UnityEngine.Object;

internal static class CloneRegression
{
    private static int checks;
    private static void Check(bool value,string text){checks++;if(!value)throw new Exception(text);}
    private static Mesh Triangle()
    {
        var mesh=new Mesh{name="Fixture donor/source triangle"};mesh.vertices=new[]{Vector3.zero,Vector3.up,Vector3.right};mesh.triangles=new[]{0,1,2};mesh.RecalculateNormals();return mesh;
    }
    private static GameObject Part(Transform parent,string name,Mesh mesh)
    {
        var go=new GameObject(name);go.transform.SetParent(parent,false);go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>();return go;
    }
    public static void Run()
    {
        try
        {
            var original=Triangle();var template=new GameObject("Valuable IHM Water Watch");template.SetActive(false);
            var donor=Part(template.transform,"Native donor vase",original).GetComponent<MeshRenderer>();donor.forceRenderingOff=true;
            var baseline=Object.Instantiate(template);
            bool flagSurvives=baseline.GetComponentInChildren<MeshRenderer>(true).forceRenderingOff;
            Debug.Log("BASELINE forceRenderingOff survives Instantiate: "+flagSurvives);
            Object.DestroyImmediate(baseline);
            DonorVisuals.RemoveGeometry(new[]{donor});
            Check(original.vertexCount==3,"Must not modify the shared native donor mesh asset");
            var visual=new GameObject("IHM Valuable Visual");visual.transform.SetParent(template.transform,false);
            var pivot=new GameObject("Water watch flip pivot");pivot.transform.SetParent(visual.transform,false);pivot.transform.localPosition=new Vector3(0,.13f,0);
            Part(pivot.transform,"Part000",original);Part(visual.transform,"Part001",original).transform.localPosition=new Vector3(0,.15f,0);
            var art=template.AddComponent<ValuableArtInstance>();art.assetKey="water_timer";Check(art.EnsureBindings(),"Registration template binds");
            var instances=new List<GameObject>();
            for(int i=0;i<4;i++)
            {
                var clone=Object.Instantiate(i==3?instances[0]:template);instances.Add(clone);
                var current=clone.GetComponent<ValuableArtInstance>();
                if(i<3){current.visualRoot=null;current.bindings=null;current.casePivot=null;current.assetKey=null;clone.SetActive(true);}
                Check(current.EnsureBindings(),"Spawn/reclone resolves missing serialized refs");
                Check(current.assetKey=="water_timer"&&current.bindings.Length==2,"Spawn metadata reconstructed");
                Check(current.casePivot.IsChildOf(clone.transform),"Pivot belongs to spawn, never template");
                foreach(var b in current.bindings)Check(b.node.IsChildOf(clone.transform),"Every part belongs to spawn");
                var vase=clone.transform.Find("Native donor vase");var renderer=vase.GetComponent<MeshRenderer>();
                renderer.enabled=true;renderer.forceRenderingOff=false;
                Check(vase.GetComponent<MeshFilter>().sharedMesh.vertexCount==0,"Donor stays empty after clone and renderer re-enable");
                Check(current.bindings[1].node.GetComponent<MeshFilter>().sharedMesh.vertexCount==3,"Replacement model remains intact");
                Check(Mathf.Abs(current.bindings[1].phase-.25f)<.0001f,"Animation metadata restored");
            }
            Object.DestroyImmediate(template);
            foreach(var clone in instances)
            {
                Check(clone.GetComponent<ValuableArtInstance>().bindings[0].node!=null,"Destroying template cannot invalidate spawned bindings");Object.DestroyImmediate(clone);
            }
            CheckRollingPhysics();
            CheckWaterWatch();
            CheckGumballRoster();
            CheckCollectionGeometry();
            CheckLiquidAndSpawnRules();
            string report="PASS: "+checks+" real Unity clone checks; Unity "+Application.unityVersion+"; forceRenderingOff serialized="+flagSurvives;
            File.WriteAllText(Path.Combine(Application.dataPath,"../clone-regression-result.txt"),report);Debug.Log(report);EditorApplication.Exit(0);
        }
        catch(Exception error){Debug.LogException(error);EditorApplication.Exit(1);}
    }
    private static void InvokeWatch(WaterWatch watch,string method)
    {
        typeof(WaterWatch).GetMethod(method,System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(watch,null);
    }
    private static void CheckLiquidAndSpawnRules()
    {
        float up=1;for(int i=0;i<320;i++)up=LiquidFlow.Advance(up,1,.1f,32);
        Check(up<.001f,"Upright liquid drains upper reservoir over 32 seconds");
        float down=up;for(int i=0;i<320;i++)down=LiquidFlow.Advance(down,-1,.1f,32);
        Check(down>.999f,"Physical inversion reverses liquid transfer without a button");
        Check(LiquidFlow.Advance(.4f,0,10,32)==.4f,"Sideways timer pauses instead of dropping through case wall");
        Check(!LiquidFlow.Flowing(0,1)&&LiquidFlow.Flowing(0,-1),"Drops stop when source empties and resume on inversion");
        for(int mask=0;mask<8;mask++)
        {
            bool enabled=(mask&1)!=0,generated=(mask&2)!=0,spawned=(mask&4)!=0;
            Check(IncreaseHardMod.Patches.Effects.LateJoinRules.ShouldSpawn(enabled,generated,spawned)==(mask!=7),"Only existing mid-run players are excluded from respawn: "+mask);
        }
        Check(Mathf.Approximately(ValuableLayout.Scale("bomb"),.8f)&&Mathf.Approximately(ValuableLayout.Scale("cannon"),.85f),"Requested bomb/cannon scale factors");
        Check(Mathf.Approximately(ValuableLayout.Scale("centrifuge"),1.1f)&&Mathf.Approximately(ValuableLayout.Scale("robot_vacuum"),1.1f),"Requested centrifuge/vacuum scale factors");
        Check(Mathf.Approximately(ValuableLayout.Scale("maneki_neko"),2f),"Neko doubles in all three dimensions");
    }
    private static void CheckCollectionGeometry()
    {
        var artDirectory=Path.GetFullPath(Path.Combine(Application.dataPath,"../../../src/Art"));
        foreach(var entry in CollectionCatalog.Entries)
        {
            string path=Path.Combine(artDirectory,"v11_"+entry.Key+".mesh.json");
            if(!File.Exists(path))path=Path.Combine(artDirectory,"v10_"+entry.Key+".mesh.json");
            if(!File.Exists(path))path=Path.Combine(artDirectory,"v09_"+entry.Key+".mesh.json");
            var model=JsonUtility.FromJson<ValuableArtModel>(File.ReadAllText(path));
            if(entry.Key=="maneki_neko")ValuableVisuals.Overrides[entry.Key]=model;
            var go=new GameObject("Fixture "+entry.Key);go.SetActive(false);go.transform.localScale=Vector3.one*ValuableLayout.Scale(entry.Key);
            go.AddComponent<Rigidbody>().mass=entry.Mass;go.AddComponent<PhysGrabObject>();
            var art=go.AddComponent<ValuableArtInstance>();art.assetKey=entry.Key;
            var root=new GameObject("IHM Valuable Visual");root.transform.SetParent(go.transform,false);art.visualRoot=root.transform;
            var nativeCenter=new GameObject("Center of Mass");nativeCenter.transform.SetParent(go.transform,false);nativeCenter.transform.localPosition=Vector3.one*7;
            var meshes=new List<Mesh>();
            var materials=new List<Material>();
            if(entry.Key=="maneki_neko")
            {
                var paw=new GameObject("Beckoning paw pivot");paw.transform.SetParent(root.transform,false);paw.transform.localPosition=model.beckonPivot;
            }
            for(int i=0;i<model.parts.Length;i++)
            {
                var p=model.parts[i];var mesh=new Mesh();mesh.vertices=p.vertices;mesh.triangles=p.triangles;mesh.RecalculateBounds();meshes.Add(mesh);
                var part=Part(root.transform,"Part"+i.ToString("D3"),mesh);part.transform.localPosition=p.rest.position;
                if(entry.Key=="maneki_neko"&&p.group=="beckoning_paw")part.transform.SetParent(root.transform.Find("Beckoning paw pivot"),true);
                var materialData=model.materials[p.material];var material=new Material(Shader.Find("Standard")){name="IHM "+materialData.name};
                material.SetColor("_Color",materialData.color);
                if(materialData.emission>0){material.EnableKeyword("_EMISSION");material.SetColor("_EmissionColor",materialData.color*materialData.emission);}
                part.GetComponent<MeshRenderer>().sharedMaterial=material;materials.Add(material);
                if(entry.Key=="bomb")
                {
                    var collider=new GameObject("Bomb convex fixture "+i);collider.transform.SetParent(go.transform,false);collider.transform.localPosition=p.rest.position;
                    collider.AddComponent<PhysGrabObjectCollider>();var hull=collider.AddComponent<MeshCollider>();hull.sharedMesh=mesh;hull.convex=true;
                    Check(hull.sharedMesh.vertexCount>0,"Bomb convex piece has actual geometry "+i);
                }
            }
            if(entry.Key=="maneki_neko")
            {
                Check(art.EnsureBindings(),"Approved maneki binds before collision setup");
                Check(ManekiPhysics.Attach(go,art),"Approved maneki uses authored collision hulls");
                foreach(var collider in go.GetComponentsInChildren<MeshCollider>(true))meshes.Add(collider.sharedMesh);
            }
            var bounds=ValuableLayout.Apply(go,art);var actual=ValuableLayout.MeshBounds(go.transform,root.transform);
            Check(actual.min.y>=.011f&&actual.min.z>=.011f,entry.Key+" fits positive floor/forward native spawn convention");
            Check(Mathf.Abs(actual.center.x)<.001f,entry.Key+" width is centred on its own mesh");
            var expectedMassCenter=model.physics==null?bounds.center:go.transform.InverseTransformPoint(art.visualRoot.TransformPoint(model.physics.centerOfMass));
            Check((go.GetComponent<Rigidbody>().centerOfMass-expectedMassCenter).sqrMagnitude<.000001f,entry.Key+" no inherited vase centre of mass");
            Check((nativeCenter.transform.localPosition-expectedMassCenter).sqrMagnitude<.000001f,entry.Key+" native centre transform matches authored mass distribution");
            if(CollectionLights.Applies(entry.Key))CheckLighting(go,art,model);
            if(entry.Key=="centrifuge")CheckSamplePhysics(go,art,model);
            if(entry.Key=="skeleton")CheckSkeletonMass(go,art,model);
            if(entry.Key=="maneki_neko")
            {
                ValuableVisuals.Overrides[entry.Key]=model;Check(art.EnsureBindings(),"Neko rebinds a local paw pivot after spawning");
                var motion=go.AddComponent<ManekiMotion>();typeof(ManekiMotion).GetMethod("Start",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(motion,null);
                typeof(ManekiMotion).GetMethod("FixedUpdate",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(motion,null);
                Check(Quaternion.Angle(Quaternion.identity,art.beckonPivot.localRotation)<=24.01f,"Neko paw motion stays within its reserved sweep");
                Check(art.beckonPivot.GetComponentsInChildren<MeshRenderer>(true).Length>=2,"Forearm, paw and claws stay attached to the animated pivot");
                var pawCollider=art.beckonPivot.GetComponentInChildren<MeshCollider>();
                Check(pawCollider!=null&&go.GetComponentsInChildren<MeshCollider>(true).Length==3,"Body/head/paw have three independent convex hulls");
                for(int sample=0;sample<=80;sample++)
                {
                    double time=sample*ManekiMotion.PeriodSeconds/80;
                    float angle=ManekiMotion.AngleAt(time);
                    Check(Mathf.Abs(angle-ManekiMotion.AngleAt(time+ManekiMotion.PeriodSeconds*100000))<.001f,"Late join shares phase "+sample);
                    art.beckonPivot.localRotation=Quaternion.AngleAxis(angle,Vector3.right);
                    foreach(var collider in go.GetComponentsInChildren<MeshCollider>(true))
                    foreach(var vertex in collider.sharedMesh.vertices)
                        Check(bounds.Contains(go.transform.InverseTransformPoint(collider.transform.TransformPoint(vertex))),"Collision sweep fits reserved spawn volume "+sample);
                }
                ValuableVisuals.Overrides.Remove(entry.Key);
            }
            Object.DestroyImmediate(go);foreach(var mesh in meshes)Object.DestroyImmediate(mesh);foreach(var material in materials)Object.DestroyImmediate(material);
        }
    }
    private static void CheckSkeletonMass(GameObject go,ValuableArtInstance art,ValuableArtModel model)
    {
        ValuableVisuals.Overrides["skeleton"]=model;Check(art.EnsureBindings(),"Skeleton resolves all actual articulated parts");
        go.AddComponent<Photon.Pun.PhotonView>();go.AddComponent<ValuableFoley>();
        var motion=go.AddComponent<CollectionPhysics>();
        typeof(CollectionPhysics).GetMethod("Start",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(motion,null);
        var bodies=go.GetComponentsInChildren<Rigidbody>(true);float mass=0;foreach(var body in bodies)mass+=body.mass;
        Check(bodies.Length==16,"Skeleton mass includes root and all fifteen joint bodies");
        Check(Mathf.Abs(mass-3f)<.0001f,"Actual assembled skeleton is 3 kg, not 3 kg plus heavy limbs");
        ValuableVisuals.Overrides.Remove("skeleton");
    }
    private static void CheckLighting(GameObject go,ValuableArtInstance art,ValuableArtModel model)
    {
        ValuableVisuals.Overrides[art.assetKey]=model;Check(art.EnsureBindings(),art.assetKey+" lighting binds its actual delivered model");
        var lights=go.AddComponent<CollectionLights>();
        typeof(CollectionLights).GetMethod("Start",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(lights,null);
        typeof(CollectionLights).GetMethod("Update",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(lights,null);
        var lamps=go.GetComponentsInChildren<Light>(true);Check(lamps.Length>0,art.assetKey+" has an actual local light, not only a coloured texture");
        foreach(var light in lamps)Check(light.range>0&&light.intensity>0&&light.enabled,art.assetKey+" light is enabled with finite range and intensity");
        if(art.assetKey=="robot_vacuum")
            Check(lamps[0].type==LightType.Spot&&lamps[0].cookie!=null&&Vector3.Dot(lamps[0].transform.forward,-go.transform.up)>.99f,"Vacuum projects a rotating ring/sweep cookie downward");
        if(art.assetKey=="monoblock_pc")Check(lamps[0].intensity<=.2f&&Vector3.Dot(lamps[0].transform.forward,-go.transform.forward)>.99f,"PC screen light is weak and faces outward");
        if(art.assetKey=="centrifuge"||art.assetKey=="ram_pair"||art.assetKey=="graphics_card")
        {
            int emissive=0;var block=new MaterialPropertyBlock();
            foreach(var renderer in go.GetComponentsInChildren<MeshRenderer>(true)){renderer.GetPropertyBlock(block);if(block.GetColor("_EmissionColor").maxColorComponent>.5f)emissive++;}
            Check(emissive>=(art.assetKey=="centrifuge"?12:2),art.assetKey+" expected liquid/diffuser surfaces glow independently");
        }
        ValuableVisuals.Overrides.Remove(art.assetKey);
    }
    private static void CheckSamplePhysics(GameObject go,ValuableArtInstance art,ValuableArtModel model)
    {
        ValuableVisuals.Overrides["centrifuge"]=model;
        Check(art.EnsureBindings(),"Actual centrifuge part bindings reconstruct");
        var view=go.AddComponent<Photon.Pun.PhotonView>();go.AddComponent<ValuableFoley>();go.AddComponent<ValuableObject>();
        var impact=go.AddComponent<PhysGrabObjectImpactDetector>();var physics=go.AddComponent<CollectionPhysics>();
        typeof(CollectionPhysics).GetMethod("Start",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(physics,null);
        var samples=art.visualRoot.GetComponentsInChildren<CentrifugeSampleContact>(true);
        Check(samples.Length==12,"Glass, liquid and cap are grouped into exactly twelve samples");
        Check(go.GetComponentsInChildren<Rigidbody>(true).Length==1,"Seated samples use root Rigidbody and cannot kinematically anchor the case");
        var detached=new List<GameObject>();int before=go.GetComponent<PhysGrabObject>().colliders.Count;
        physics.SampleHit(0,2);detached.Add(samples[0].gameObject);
        Check(samples[0].transform.parent==null&&samples[1].transform.IsChildOf(go.transform),"Hitting one sample releases only that sample");
        Check(samples[0].GetComponentsInChildren<Light>(true).Length==1,"Detached sample carries its own liquid light");
        Check(impact.paid>=2000&&impact.paid<=3000,"First sample loss charges 2-3k once through native break event");
        int paid=impact.paid;physics.SampleHit(1,2);detached.Add(samples[1].gameObject);
        Check(impact.paid==paid&&samples[1].transform.parent==null,"Second sample releases independently without a second kit debit");
        Check(go.GetComponent<PhysGrabObject>().colliders.Count==before-2,"Only extracted sample grab markers are removed");
        Check(samples[0].GetComponent<CapsuleCollider>()!=null&&samples[0].GetComponent<Rigidbody>()!=null,"Released samples have round collision and separate physical bodies");
        foreach(var sample in detached)Object.DestroyImmediate(sample);
        ValuableVisuals.Overrides.Remove("centrifuge");
    }
    private static void CheckGumballRoster()
    {
        var root=new GameObject("Gumball roster regression");
        var trap=root.AddComponent<GumballValuable>();trap.HypnosisLinesParenTransform=root.transform;
        var template=new GameObject("Hypnosis line template");template.AddComponent<EyeLines>();template.SetActive(false);trap.hypnosisLine=template;
        var a=new GameObject("Player A").AddComponent<PlayerAvatar>();
        var b=new GameObject("Player B").AddComponent<PlayerAvatar>();
        var c=new GameObject("Player C").AddComponent<PlayerAvatar>();
        int count=0;List<PlayerAvatar> players=null;List<bool> sight=null,seen=null;List<GameObject> lines=null;
        var live=new List<PlayerAvatar>{a};
        void Sync()=>IncreaseHardMod.Patches.GumballRosterPatch.Reconcile(trap,live,ref count,ref players,ref sight,ref seen,ref lines,o=>Object.DestroyImmediate(o));
        Sync();Check(count==1&&lines.Count==1&&sight.Count==1&&seen.Count==1,"Initial Gumball roster builds all parallel lists");
        sight[0]=true;seen[0]=true;var originalLine=lines[0];live.Add(b);Sync();
        Check(count==2&&lines.Count==2&&sight.Count==2&&seen.Count==2,"Late join grows lines and both visibility lists");
        Check(sight[0]&&seen[0]&&lines[0]==originalLine,"Late join preserves existing player state and line");
        Check(lines[1].GetComponent<EyeLines>().target==b&&!lines[1].activeSelf,"New line targets joining player and starts hidden");
        var snapshot=players;Sync();Check(ReferenceEquals(snapshot,players),"Unchanged frame does not rebuild or reset hypnosis");
        live.Reverse();Sync();Check(players[1]==a&&sight[1]&&lines[1]==originalLine,"Roster reorder keeps state attached to player");
        live.Remove(b);Sync();Check(count==1&&players[0]==a&&sight[0],"Departure shrinks every list and preserves remaining player");
        live[0]=c;Sync();Check(count==1&&players[0]==c&&!sight[0]&&lines[0].GetComponent<EyeLines>().target==c,"Same-count replacement cannot inherit another player's hypnosis");
        Object.DestroyImmediate(lines[0]);Sync();Check(lines[0]!=null&&lines[0].GetComponent<EyeLines>().target==c,"Destroyed hypnosis line is recreated");
        live.Clear();Sync();Check(count==0&&lines.Count==0&&sight.Count==0&&seen.Count==0,"Empty room clears every parallel list");
        live=null;Sync();Check(count==0,"Null teardown roster is safe");
        Object.DestroyImmediate(root);Object.DestroyImmediate(template);Object.DestroyImmediate(a.gameObject);Object.DestroyImmediate(b.gameObject);Object.DestroyImmediate(c.gameObject);
    }
    private static void CheckWaterWatch()
    {
        var go=new GameObject("Valuable IHM Water Watch");go.SetActive(false);
        var visual=new GameObject("IHM Valuable Visual");visual.transform.SetParent(go.transform,false);
        var pivot=new GameObject("Water watch flip pivot");pivot.transform.SetParent(visual.transform,false);
        var mesh=Triangle();Part(pivot.transform,"Part000",mesh);
        var liquid=Part(pivot.transform,"Part001",mesh);
        var material=new Material(Shader.Find("Standard"));liquid.GetComponent<MeshRenderer>().sharedMaterial=material;
        var art=go.AddComponent<ValuableArtInstance>();art.assetKey="water_timer";
        go.AddComponent<PhysGrabObject>();var watch=go.AddComponent<WaterWatch>();
        InteractiveValuables.FlowSeconds=null;InteractiveValuables.FlipKey=null;
        InvokeWatch(watch,"Start");InvokeWatch(watch,"Update");
        Check(watch.enabled&&go.GetComponentInChildren<Light>(true)!=null,"WaterWatch starts with missing client config");
        var liquidMaterial=liquid.GetComponent<MeshRenderer>().sharedMaterial;
        Check(liquidMaterial!=material&&liquid.transform.localScale.x>0,"Actual Draw animates liquid with default flow and instanced material");
        Object.DestroyImmediate(liquidMaterial);InvokeWatch(watch,"Draw");
        Check(true,"Destroyed liquid material cannot throw during Draw");
        Object.DestroyImmediate(liquid);InvokeWatch(watch,"Draw");
        Check(true,"Destroyed liquid node cannot throw during Draw");
        Object.DestroyImmediate(pivot);InvokeWatch(watch,"Draw");
        Check(true,"Destroyed flip pivot cannot throw during Draw");
        Object.DestroyImmediate(go);Object.DestroyImmediate(material);Object.DestroyImmediate(mesh);

        go=new GameObject("Valuable IHM Water Watch");go.SetActive(false);
        visual=new GameObject("IHM Valuable Visual");visual.transform.SetParent(go.transform,false);
        pivot=new GameObject("Water watch flip pivot");pivot.transform.SetParent(visual.transform,false);
        mesh=Triangle();Part(pivot.transform,"Part000",mesh);Part(pivot.transform,"Part001",mesh);
        art=go.AddComponent<ValuableArtInstance>();art.assetKey="water_timer";
        go.AddComponent<PhysGrabObject>();watch=go.AddComponent<WaterWatch>();
        InvokeWatch(watch,"Start");InvokeWatch(watch,"Update");
        Check(watch.enabled&&go.GetComponentInChildren<Light>(true)!=null,"Missing liquid material does not abort Start or Update");
        Object.DestroyImmediate(go);Object.DestroyImmediate(mesh);
    }
    private static void CheckRollingPhysics()
    {
        var mode=Physics.simulationMode;var gravity=Physics.gravity;Physics.simulationMode=SimulationMode.Script;Physics.gravity=new Vector3(0,-9.81f,0);
        var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.name="Fixture floor";floor.transform.position=new Vector3(0,-.5f,0);floor.transform.localScale=new Vector3(100,1,100);
        foreach(float mass in new[]{8f,3f,3.5f})
        {
            var prop=GameObject.CreatePrimitive(PrimitiveType.Cube);prop.name="Rolling fixture "+mass;prop.transform.rotation=Quaternion.Euler(0,25,180);
            var body=prop.AddComponent<Rigidbody>();body.mass=mass;body.constraints=RigidbodyConstraints.FreezeAll;
            WheeledPropPhysics.SetRollingContact(prop.GetComponent<Collider>());WheeledPropPhysics.Prepare(body);WheeledPropPhysics.AlignAtSpawn(body);
            Check(Vector3.Dot(prop.transform.up,Vector3.up)>.999f,"Inverted spawn is upright for "+mass);
            Check(body.constraints==RigidbodyConstraints.None,"Inherited frozen constraints removed");
            Check(prop.GetComponent<Collider>().bounds.min.y>=-.001f,"Spawn clears floor instead of embedding geometry");
            for(int i=0;i<40;i++)Physics.Simulate(.02f);
            var start=body.position;
            for(int i=0;i<60;i++){WheeledPropPhysics.Assist(body,Vector3.forward*.65f,Vector3.up,true,true);Physics.Simulate(.02f);}
            Check(body.position.z-start.z>.5f,"One holder can roll "+mass+" kg across the floor");
            Check(Mathf.Abs(body.position.y-start.y)<.08f,"Ground assistance adds no lift");
            Check(body.mass==mass,"Rolling assistance preserves lifting mass");
            body.useGravity=false;body.position=new Vector3(0,3,0);body.velocity=Vector3.zero;body.angularVelocity=Vector3.zero;Physics.SyncTransforms();
            WheeledPropPhysics.Assist(body,Vector3.forward,Vector3.up,true,false);Physics.Simulate(.1f);
            Check(Mathf.Abs(body.position.z)<.001f,"Airborne grab receives no floor assistance");
            WheeledPropPhysics.Assist(body,Vector3.forward,Vector3.up,false,true);Physics.Simulate(.1f);
            Check(Mathf.Abs(body.position.z)<.001f,"Ungrabbed objects cannot drive themselves");
            Object.DestroyImmediate(prop);
        }
        Object.DestroyImmediate(floor);Physics.simulationMode=mode;Physics.gravity=gravity;
    }
}
