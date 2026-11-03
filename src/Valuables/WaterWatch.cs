using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;
using Object=UnityEngine.Object;

namespace IncreaseHardMod.Valuables;

// Liquid is simulated in the actual object's frame. Turning the grabbed object reverses flow.
internal sealed class WaterWatch:MonoBehaviour
{
    private ValuableArtInstance _art;
    private readonly List<(ValuablePartBinding binding,Material material,float halfHeight)> _liquids=new();
    private Light _glow;
    private float _upper=1,_broadcast,_drip;
    private double _phaseEpoch,_lastTick,_received=-1;
    private static float FlowSeconds=>Mathf.Clamp(InteractiveValuables.FlowSeconds?.Value??32f,12f,120f);
    private double Now=>GameManager.Multiplayer()?PhotonNetwork.Time:Time.timeAsDouble;
    private void Start()
    {
        _art=GetComponent<ValuableArtInstance>();
        if(_art==null||!_art.EnsureBindings()){enabled=false;return;}
        _lastTick=_phaseEpoch=Now;
        foreach(var part in _art.bindings)
        {
            if(part.group!="drop"&&part.group!="top_pool"&&part.group!="bottom_pool")continue;
            var renderer=part.node.GetComponent<MeshRenderer>();if(renderer.sharedMaterial==null)continue;
            var material=renderer.material;material.EnableKeyword("_EMISSION");
            _liquids.Add((part,material,part.node.GetComponent<MeshFilter>().sharedMesh.bounds.extents.y));
        }
        var light=new GameObject("Water watch soft glow");light.transform.SetParent(_art.visualRoot,false);light.transform.localPosition=new Vector3(0,.13f,0);
        _glow=light.AddComponent<Light>();_glow.type=LightType.Point;_glow.range=1.1f;_glow.intensity=.5f;_glow.shadows=LightShadows.None;
    }
    private float Upright=>Vector3.Dot(transform.up,Physics.gravity.sqrMagnitude>.01f?-Physics.gravity.normalized:Vector3.up);
    private void Update()
    {
        if(_art==null||_glow==null)return;
        var now=Now;float dt=Mathf.Clamp((float)(now-_lastTick),0,.1f);_lastTick=now;
        _upper=LiquidFlow.Advance(_upper,Upright,dt,FlowSeconds);
        Draw();
        if(!SemiFunc.IsMasterClientOrSingleplayer())return;
        _broadcast-=Time.deltaTime;
        if(_broadcast<=0)
        {
            _broadcast=.5f;var view=GetComponent<PhotonView>();
            if(GameManager.Multiplayer()&&view!=null)view.RPC(nameof(IHM_WatchLiquid),RpcTarget.Others,_upper,_phaseEpoch,now);
        }
        _drip-=Time.deltaTime;
        if(LiquidFlow.Flowing(_upper,Upright)&&_drip<=0)
        {_drip=1.4f;GetComponent<ValuableFoley>()?.Emit(ValuableFoley.Drip,.055f);}
    }
    [PunRPC] private void IHM_WatchLiquid(float upper,double phase,double stamp,PhotonMessageInfo info=default)
    {
        if(!SemiFunc.MasterOnlyRPC(info)||!ValuableDynamics.Finite(upper)||upper<0||upper>1||
            !ValuableDynamics.Finite(phase)||!ValuableDynamics.Finite(stamp)||stamp<=_received||stamp>Now+2)return;
        _upper=upper;_phaseEpoch=phase;_received=stamp;_lastTick=Now;
    }
    private void Draw()
    {
        if(_art==null||_art.casePivot==null||_glow==null)return;
        _art.casePivot.localRotation=Quaternion.identity;
        float up=Upright;bool flowing=LiquidFlow.Flowing(_upper,up);
        Vector3 gravity=transform.InverseTransformDirection(Physics.gravity.normalized);
        Color blue=new(.025f,.13f,.85f),rose=new(.9f,.025f,.28f),cyan=new(.04f,.65f,.65f);
        float mix=.5f+.5f*Mathf.Sin((float)(Now-_phaseEpoch)*.08f);
        Color average=Color.Lerp(blue,rose,mix);
        foreach(var item in _liquids)
        {
            var b=item.binding;if(b==null||b.node==null||item.material==null)continue;
            Vector3 pos=b.rest;Color c=average;
            if(b.group=="drop")
            {
                float phase=Mathf.Repeat((float)((Now-_phaseEpoch)/5.5)+b.phase,1);
                float scale=flowing?Mathf.Min(1,phase/.14f,(1-phase)/.16f):.001f;
                pos.y=Mathf.Lerp(up>=0?.179f:.047f,up>=0?.047f:.179f,phase);
                // Slight drift/slosh stays inside the narrow clear case.
                pos.x=Mathf.Clamp(b.rest.x+gravity.x*.012f*Mathf.Sin(phase*Mathf.PI),-.028f,.028f);
                pos.z=Mathf.Clamp(b.rest.z+gravity.z*.003f,-.007f,.007f);
                b.node.localScale=new Vector3(scale,scale*(1+.18f*Mathf.Sin(phase*Mathf.PI)),scale);
                c=Color.Lerp(b.phase<.5f?blue:rose,cyan,.12f+.15f*mix);
            }
            else
            {
                float fraction=b.group=="top_pool"?_upper:1-_upper;
                float scale=Mathf.Lerp(.06f,1.05f,fraction);
                pos.y=b.rest.y+(up>=0?-1:1)*item.halfHeight*(1-scale);
                b.node.localScale=new Vector3(1,scale,1);
                var surface=Quaternion.FromToRotation(Vector3.up,up>=0?-gravity:gravity);
                b.node.localRotation=Quaternion.RotateTowards(Quaternion.identity,surface,15);
                c=Color.Lerp(b.group=="top_pool"?blue:rose,average,.35f);
            }
            b.node.localPosition=pos;
            foreach(string property in new[]{"_Color","_BaseColor"})if(item.material.HasProperty(property))item.material.SetColor(property,c);
            if(item.material.HasProperty("_EmissionColor"))item.material.SetColor("_EmissionColor",c*3.2f);
        }
        _glow.color=average;_glow.intensity=.5f+.06f*Mathf.Sin((float)Now*.7f);
    }
    private void OnDestroy(){foreach(var item in _liquids)if(item.material!=null)Object.Destroy(item.material);}
}
