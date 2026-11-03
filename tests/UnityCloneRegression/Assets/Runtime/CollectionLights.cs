using System;
using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;

namespace IncreaseHardMod.Valuables;

// Emissive surfaces remain visible in darkness; small local lamps light nearby geometry.
internal sealed class CollectionLights:MonoBehaviour
{
    private readonly List<(MeshRenderer renderer,Color emission,int phase)> _surfaces=new();
    private readonly List<Light> _rgb=new();
    private MaterialPropertyBlock _block;
    private Light _scanner;
    private Texture2D _cookie;
    internal static bool Applies(string key)=>key=="ram_pair"||key=="graphics_card"||key=="robot_vacuum"||key=="monoblock_pc"||key=="centrifuge";
    private void Start()
    {
        _block=new MaterialPropertyBlock();int phase=0,liquid=0;
        var art=GetComponent<ValuableArtInstance>();if(art==null||!art.EnsureBindings()){enabled=false;return;}
        foreach(var b in art.bindings)
        {
            var renderer=b.node.GetComponent<MeshRenderer>();var material=renderer!=null?renderer.sharedMaterial:null;
            if(material==null)continue;
            if(b.group.StartsWith("rgb_",StringComparison.Ordinal))
            {
                var emission=art.assetKey=="ram_pair"?Color.white*1.8f:new Color(.025f,.4f,.48f)*2.2f;
                _surfaces.Add((renderer,emission,phase++));
                _rgb.Add(Lamp(b.node,b.node.name+" RGB spill",Vector3.zero,new Color(.08f,.65f,.8f),.07f,.24f));
            }
            else if(art.assetKey=="centrifuge"&&material.name.IndexOf("Sample liquid",StringComparison.OrdinalIgnoreCase)>=0)
            {
                var color=material.HasProperty("_Color")?material.GetColor("_Color"):new Color(.03f,.4f,.3f);
                _surfaces.Add((renderer,color*2.4f,phase++));
                // Two tiny fill lights per case; all twelve liquids emit without twelve scene lights.
                if(liquid++%6==0)Lamp(b.node,b.node.name+" liquid glow",Vector3.zero,color,.055f,.16f);
            }
            else if(art.assetKey=="monoblock_pc"&&material.name.IndexOf("display",StringComparison.OrdinalIgnoreCase)>=0)
            {
                var glow=Lamp(b.node,b.node.name+" screen spill",new Vector3(0,0,-.025f),new Color(.30f,.66f,.69f),.14f,.65f);
                glow.type=LightType.Spot;glow.spotAngle=105;glow.transform.localRotation=Quaternion.Euler(0,180,0);
            }
        }
        if(art.assetKey=="robot_vacuum")
        {
            _scanner=Lamp(art.visualRoot,"IHM underside scanner",new Vector3(0,.065f,0),new Color(.03f,.8f,.66f),1.15f,.55f);
            _scanner.type=LightType.Spot;_scanner.spotAngle=150;
            _scanner.transform.localRotation=Quaternion.Euler(90,0,0);
            _cookie=ScannerCookie();_scanner.cookie=_cookie;
        }
    }
    private static Light Lamp(Transform parent,string name,Vector3 position,Color color,float intensity,float range)
    {
        var node=parent.Find(name);
        if(node==null){var go=new GameObject(name);go.transform.SetParent(parent,false);node=go.transform;}
        node.localPosition=position;var lamp=node.GetComponent<Light>();
        if(lamp==null)lamp=node.gameObject.AddComponent<Light>();
        lamp.type=LightType.Point;lamp.color=color;lamp.intensity=intensity;lamp.range=range;lamp.shadows=LightShadows.None;
        lamp.renderMode=LightRenderMode.ForcePixel;return lamp;
    }
    private static Texture2D ScannerCookie()
    {
        const int n=128;var pixels=new Color[n*n];
        for(int y=0;y<n;y++)for(int x=0;x<n;x++)
        {
            float dx=(x+.5f)/n*2-1,dy=(y+.5f)/n*2-1,r=Mathf.Sqrt(dx*dx+dy*dy);
            float ring=Mathf.Max(Mathf.Exp(-Mathf.Pow((r-.56f)/.025f,2)),Mathf.Exp(-Mathf.Pow((r-.86f)/.018f,2)))*.75f;
            float sweep=Mathf.Pow(Mathf.Max(0,Mathf.Cos(Mathf.Atan2(dy,dx))),18)*.7f;
            float alpha=r<.94f&&r>.08f?Mathf.Max(.035f,Mathf.Max(ring,sweep)):0;
            pixels[y*n+x]=new Color(1,1,1,alpha);
        }
        var image=new Texture2D(n,n,TextureFormat.RGBA32,false){name="IHM scanner rings and sweep",wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear};
        image.SetPixels(pixels);image.Apply(false,true);return image;
    }
    private void Update()
    {
        double time=GameManager.Multiplayer()?PhotonNetwork.Time:Time.timeAsDouble;
        foreach(var surface in _surfaces)
        {
            if(surface.renderer==null)continue;
            surface.renderer.GetPropertyBlock(_block);
            _block.SetColor("_EmissionColor",surface.emission*(.94f+.06f*Mathf.Sin((float)(time*.7%(System.Math.PI*2))+surface.phase*.65f)));
            surface.renderer.SetPropertyBlock(_block);
        }
        for(int i=0;i<_rgb.Count;i++)if(_rgb[i]!=null)_rgb[i].color=Color.HSVToRGB(Mathf.Repeat((float)(time*.035%1)+i*.24f,1),.7f,.8f);
        if(_scanner!=null)_scanner.transform.localRotation=Quaternion.Euler(90,0,0)*Quaternion.AngleAxis((float)(time*35%360),Vector3.forward);
    }
    private void OnDestroy(){if(_cookie!=null){if(Application.isPlaying)Destroy(_cookie);else DestroyImmediate(_cookie);}}
}
