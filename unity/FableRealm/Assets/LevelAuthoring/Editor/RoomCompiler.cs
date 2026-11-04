using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace LevelAuthoring.Editor
{
    /// <summary>Compiles reusable room recipes to normal Unity objects and persistent assets.</summary>
    public sealed class RoomCompiler
    {
        readonly Dictionary<string,Material> materials = new Dictionary<string,Material>();
        readonly string output;
        readonly System.Random random;
        int meshIndex;
        RoomCompiler(string output,int seed) { this.output=output; random=new System.Random(seed); }

        public static RoomDefinition Read(string path)
        {
            var definition=JsonUtility.FromJson<RoomDefinition>(File.ReadAllText(path));
            RoomChecks.ValidateDefinition(definition);
            return definition;
        }

        public static void BuildInto(Transform root,string path,string output)
        {
            var definition=Read(path);
            if(root.childCount!=0) throw new InvalidOperationException("BuildInto requires an empty root.");
            if(!output.StartsWith("Assets/",StringComparison.Ordinal) || output.Contains(".."))
                throw new ArgumentException("Output must be a project asset folder.");
            Directory.CreateDirectory(output+"/Materials");
            Directory.CreateDirectory(output+"/Meshes");
            AssetDatabase.Refresh();
            var compiler=new RoomCompiler(output,definition.seed);
            compiler.MakeMaterials(definition.materials);
            var architecture=Group(root,"Architecture");
            foreach(var element in definition.elements) compiler.BuildElement(architecture,element);
            var markers=Group(root,"Integration anchors - not game components");
            foreach(var anchor in definition.anchors)
            {
                var t=Group(markers,anchor.kind+"__"+anchor.id);
                t.localPosition=anchor.position; t.localRotation=Quaternion.Euler(anchor.rotation);
            }
            // Keep an exact machine-readable contract beside the prefab for game adapters.
            File.WriteAllText(output+"/room-definition.json",JsonUtility.ToJson(definition,true));
            File.WriteAllText(output+"/integration-status.txt",
                "Authoring only. Editor NavMesh QA may be present; game component binding, runtime navmesh integration, minimap, valuable spawning and REPOLib registration are not implemented.\n");
            AssetDatabase.SaveAssets();
        }

        internal static Transform Group(Transform parent,string name)
        {
            var obj=new GameObject(name); obj.transform.SetParent(parent,false); return obj.transform;
        }

        void MakeMaterials(SurfaceDefinition[] definitions)
        {
            var shader=Shader.Find("Standard");
            if(shader==null) throw new InvalidOperationException("Built-in render pipeline required.");
            foreach(var spec in definitions)
            {
                string path=output+"/Materials/"+spec.id+".mat";
                var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
                if(mat==null) { mat=new Material(shader); AssetDatabase.CreateAsset(mat,path); }
                ColorUtility.TryParseHtmlString(spec.color,out var color);
                mat.color=color; mat.SetFloat("_Glossiness",spec.smoothness); mat.SetFloat("_Metallic",spec.metallic);
                if(spec.emission>0) { mat.EnableKeyword("_EMISSION"); mat.SetColor("_EmissionColor",color*spec.emission); }
                else { mat.DisableKeyword("_EMISSION"); mat.SetColor("_EmissionColor",Color.black); }
                if(spec.grain>0)
                {
                    string texturePath=output+"/Materials/"+spec.id+"-grain.asset";
                    var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
                    if(texture==null)
                    {
                        texture=new Texture2D(128,128,TextureFormat.RGB24,true);
                        AssetDatabase.CreateAsset(texture,texturePath);
                    }
                    for(int y=0;y<128;y++) for(int x=0;x<128;x++)
                    {
                        float wave=Mathf.Sin(x*.75f+Mathf.PerlinNoise(x*.025f,y*.04f)*4);
                        float tone=1-spec.grain*(.5f+.25f*wave+.25f*Mathf.PerlinNoise(x*.3f,y*.12f));
                        texture.SetPixel(x,y,new Color(tone,tone,tone));
                    }
                    texture.wrapMode=TextureWrapMode.Repeat; texture.Apply();
                    EditorUtility.SetDirty(texture); mat.mainTexture=texture;
                }
                else mat.mainTexture=null;
                EditorUtility.SetDirty(mat); materials.Add(spec.id,mat);
            }
        }

        void BuildElement(Transform parent,ElementDefinition e)
        {
            var t=Group(parent,e.id); t.localPosition=e.position; t.localRotation=Quaternion.Euler(e.rotation);
            switch(e.kind)
            {
                case "box": Box(t,"Solid",Vector3.zero,e.size,e.material,e.collider); break;
                case "tube": Tube(t,e.points,e.radius,e.material,e.collider); break;
                case "arch": Arch(t,e.size.x,e.size.y,e.size.z,e.radius,e.material,e.collider); break;
                case "disc": Disc(t,e.size.x,e.size.y,e.material,e.collider); break;
                case "shelf": Shelf(t,e); break;
                case "stairs": Stairs(t,e); break;
                case "rail": Rail(t,e); break;
                case "light":
                    var light=t.gameObject.AddComponent<Light>(); light.type=LightType.Point;
                    light.color=materials[e.material].color; light.intensity=e.intensity; light.range=e.range;
                    light.shadows=e.shadows?LightShadows.Soft:LightShadows.None; break;
                default: throw new InvalidDataException("Unknown element kind: "+e.kind);
            }
        }

        void Box(Transform parent,string name,Vector3 position,Vector3 size,string material,bool collider)
        {
            var obj=GameObject.CreatePrimitive(PrimitiveType.Cube); obj.name=name;
            obj.transform.SetParent(parent,false); obj.transform.localPosition=position; obj.transform.localScale=size;
            obj.GetComponent<MeshRenderer>().sharedMaterial=materials[material];
            if(!collider) UnityEngine.Object.DestroyImmediate(obj.GetComponent<BoxCollider>());
            obj.isStatic=true;
        }

        void Shelf(Transform t,ElementDefinition e)
        {
            float w=e.size.x,h=e.size.y,d=e.size.z;
            Box(t,"Back",new Vector3(0,h/2,d/2-.07f),new Vector3(w,h,.14f),e.material,e.collider);
            for(int side=-1;side<=1;side+=2)
            {
                Box(t,"Carved upright",new Vector3(side*(w/2-.1f),h/2,0),new Vector3(.2f,h,d),e.material,e.collider);
                Box(t,"Gilt edge",new Vector3(side*(w/2-.1f),h/2,-d/2-.025f),new Vector3(.035f,h,.035f),e.palette[0],false);
            }
            for(int row=0;row<=e.count;row++)
            {
                float y=.18f+row*(h-.36f)/e.count;
                Box(t,"Shelf",new Vector3(0,y,0),new Vector3(w,.12f,d),e.material,e.collider);
                if(row==e.count) continue;
                float x=-w/2+.25f;
                while(x<w/2-.35f)
                {
                    float bw=.1f+(float)random.NextDouble()*.16f;
                    float bh=(h-.36f)/e.count*(.55f+(float)random.NextDouble()*.3f);
                    string cover=e.palette[1+random.Next(e.palette.Length-1)];
                    Box(t,"Book cover",new Vector3(x+bw/2,y+.06f+bh/2,-.03f),new Vector3(bw,bh,d*.7f),cover,false);
                    for(int band=0;band<2;band++)
                        Box(t,"Spine band",new Vector3(x+bw/2,y+.12f+band*(bh-.12f),-d*.35f-.035f),new Vector3(bw*.8f,.018f,.012f),e.palette[0],false);
                    x+=bw+.035f;
                }
            }
            Box(t,"Crown",new Vector3(0,h+.08f,0),new Vector3(w+.18f,.18f,d+.12f),e.material,false);
            // Books and trim are decorative: combine them per material to avoid a draw per book.
            CombineDecoration(t);
        }

        void CombineDecoration(Transform root)
        {
            var groups=new Dictionary<Material,List<CombineInstance>>();
            var remove=new List<GameObject>();
            foreach(var filter in root.GetComponentsInChildren<MeshFilter>())
            {
                if(filter.GetComponent<Collider>()!=null) continue;
                var mat=filter.GetComponent<MeshRenderer>().sharedMaterial;
                if(!groups.TryGetValue(mat,out var list)) groups[mat]=list=new List<CombineInstance>();
                list.Add(new CombineInstance { mesh=filter.sharedMesh,transform=root.worldToLocalMatrix*filter.transform.localToWorldMatrix });
                remove.Add(filter.gameObject);
            }
            foreach(var pair in groups)
            {
                var mesh=new Mesh { indexFormat=UnityEngine.Rendering.IndexFormat.UInt32 };
                mesh.CombineMeshes(pair.Value.ToArray(),true,true);
                MeshObject(root,mesh,pair.Key,false);
            }
            foreach(var obj in remove) UnityEngine.Object.DestroyImmediate(obj);
        }

        void Stairs(Transform t,ElementDefinition e)
        {
            float width=e.size.x,rise=e.size.y,run=e.size.z;
            for(int i=0;i<e.count;i++)
            {
                float step=run/e.count,top=rise*(i+1)/e.count;
                Box(t,"Tread",new Vector3(0,top-.07f,-run/2+(i+.5f)*step),new Vector3(width,.14f,step+.015f),e.material,false);
                Box(t,"Riser",new Vector3(0,top-rise/e.count/2,-run/2+i*step),new Vector3(width,rise/e.count,.07f),e.material,false);
            }
            // Smooth physics ramp, while the mesh keeps individual stair treads.
            var ramp=Group(t,"Smooth stair collider");
            float angle=Mathf.Atan2(rise,run);
            ramp.localPosition=new Vector3(0,rise/2-.1f/Mathf.Cos(angle),0);
            ramp.localRotation=Quaternion.Euler(-angle*Mathf.Rad2Deg,0,0);
            var collider=ramp.gameObject.AddComponent<BoxCollider>();
            collider.size=new Vector3(width,.2f,Mathf.Sqrt(run*run+rise*rise));
        }

        void Rail(Transform t,ElementDefinition e)
        {
            float length=e.size.x,height=e.size.y;
            Box(t,"Handrail",new Vector3(0,height,0),new Vector3(length,.12f,.18f),e.material,e.collider);
            for(int i=0;i<=e.count;i++)
            {
                float x=-length/2+length*i/e.count;
                Box(t,"Baluster",new Vector3(x,height/2,0),new Vector3(.085f,height,.085f),e.material,false);
                Box(t,"Baluster ornament",new Vector3(x,height*.65f,0),new Vector3(.14f,.18f,.14f),e.material,false);
            }
            if(e.collider)
            {
                var barrier=t.gameObject.AddComponent<BoxCollider>();
                barrier.center=new Vector3(0,height/2,0); barrier.size=new Vector3(length,height,.16f);
            }
        }

        void Arch(Transform t,float width,float height,float depth,float thickness,string material,bool collider)
        {
            float spring=Mathf.Max(0,height-width/2);
            var points=new List<Vector3> { new Vector3(-width/2,0,0) };
            if(spring>.001f) points.Add(new Vector3(-width/2,spring,0));
            for(int i=1;i<=20;i++)
            {
                float a=Mathf.PI-Mathf.PI*i/20;
                points.Add(new Vector3(Mathf.Cos(a)*width/2,spring+Mathf.Sin(a)*(height-spring),0));
            }
            if(spring>.001f) points.Add(new Vector3(width/2,0,0));
            Tube(t,points.ToArray(),thickness,material,collider);
        }

        void Disc(Transform t,float width,float height,string material,bool collider)
        {
            const int segments=48;
            var vertices=new Vector3[segments+1]; var uv=new Vector2[segments+1];
            var triangles=new int[segments*6]; uv[0]=new Vector2(.5f,.5f);
            for(int i=0;i<segments;i++)
            {
                float a=i*Mathf.PI*2/segments;
                vertices[i+1]=new Vector3(Mathf.Cos(a)*width/2,Mathf.Sin(a)*height/2,0);
                uv[i+1]=new Vector2(.5f+Mathf.Cos(a)*.5f,.5f+Mathf.Sin(a)*.5f);
                triangles[i*6]=0; triangles[i*6+1]=(i+1)%segments+1; triangles[i*6+2]=i+1;
                triangles[i*6+3]=0; triangles[i*6+4]=i+1; triangles[i*6+5]=(i+1)%segments+1;
            }
            var mesh=new Mesh { vertices=vertices,uv=uv,triangles=triangles };
            mesh.RecalculateNormals(); mesh.RecalculateBounds(); MeshObject(t,mesh,materials[material],collider);
        }

        void Tube(Transform parent,Vector3[] points,float radius,string material,bool collider)
        {
            const int sides=10;
            var vertices=new Vector3[points.Length*sides]; var uv=new Vector2[vertices.Length];
            var triangles=new List<int>();
            for(int i=0;i<points.Length;i++)
            {
                Vector3 tangent=(points[Mathf.Min(i+1,points.Length-1)]-points[Mathf.Max(0,i-1)]).normalized;
                Vector3 axis=Mathf.Abs(tangent.y)>.9f?Vector3.right:Vector3.up;
                Vector3 u=Vector3.Cross(tangent,axis).normalized,v=Vector3.Cross(tangent,u);
                for(int j=0;j<sides;j++)
                {
                    float a=j*Mathf.PI*2/sides;
                    vertices[i*sides+j]=points[i]+radius*(u*Mathf.Cos(a)+v*Mathf.Sin(a));
                    uv[i*sides+j]=new Vector2(j/(float)sides,i*.5f);
                    if(i==0) continue;
                    int b=i*sides+j,c=i*sides+(j+1)%sides;
                    triangles.AddRange(new[]{b-sides,c-sides,b,b,c-sides,c});
                }
            }
            var mesh=new Mesh { vertices=vertices,uv=uv,triangles=triangles.ToArray() };
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            MeshObject(parent,mesh,materials[material],collider);
        }

        void MeshObject(Transform parent,Mesh mesh,Material mat,bool collider)
        {
            string path=output+"/Meshes/mesh-"+(meshIndex++).ToString("D4")+".asset";
            var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(existing==null) AssetDatabase.CreateAsset(mesh,path);
            else { EditorUtility.CopySerialized(mesh,existing); UnityEngine.Object.DestroyImmediate(mesh); mesh=existing; EditorUtility.SetDirty(mesh); }
            var t=Group(parent,"Detail "+mat.name);
            t.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;
            t.gameObject.AddComponent<MeshRenderer>().sharedMaterial=mat;
            if(collider) t.gameObject.AddComponent<MeshCollider>().sharedMesh=mesh;
            t.gameObject.isStatic=true;
        }
    }
}
