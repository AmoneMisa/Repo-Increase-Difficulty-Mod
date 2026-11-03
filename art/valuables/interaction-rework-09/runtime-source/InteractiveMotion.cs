using System;
using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;
using Object = UnityEngine.Object;

namespace IncreaseHardMod.Valuables;

internal sealed class CannonWheelMotion : MonoBehaviour
{
    private PhysGrabObject _grab;
    private Transform[] _wheels;
    private Vector3[] _previous;
    private float _rollCue;
    private Rigidbody _body;
    private readonly RaycastHit[] _hits = new RaycastHit[24];
    private void Start()
    {
        _grab=GetComponent<PhysGrabObject>();var art=GetComponent<ValuableArtInstance>();
        if(art==null||!art.EnsureBindings()){enabled=false;return;}
        _body=GetComponent<Rigidbody>();
        if(SemiFunc.IsMasterClientOrSingleplayer())
        {
            // Valuable volumes may be rotated for the vase donor. Our carriage always spawns on its wheels.
            var rotation=WheeledPropPhysics.Upright(transform.rotation);transform.rotation=rotation;_body.rotation=rotation;
            Physics.SyncTransforms();
            float floor=float.NegativeInfinity;
            int count=Physics.RaycastNonAlloc(transform.position+Vector3.up*.2f,Vector3.down,_hits,3f,~0,QueryTriggerInteraction.Ignore);
            for(int i=0;i<count;i++)
            {
                var h=_hits[i];if(h.collider==null||h.collider.transform.IsChildOf(transform)||Vector3.Dot(h.normal,Vector3.up)<.6f)continue;
                if(h.collider.GetComponentInParent<PlayerAvatar>()!=null||h.collider.GetComponentInParent<Enemy>()!=null)continue;
                floor=Mathf.Max(floor,h.point.y);
            }
            if(!float.IsNegativeInfinity(floor))
            {
                float bottom=float.PositiveInfinity;
                foreach(var c in GetComponentsInChildren<Collider>())if(c.enabled&&!c.isTrigger)bottom=Mathf.Min(bottom,c.bounds.min.y);
                if(bottom<floor+.02f){var pos=transform.position;pos.y+=floor+.02f-bottom;transform.position=pos;_body.position=pos;}
            }
        }
        _wheels=new[]{art.leftWheel,art.rightWheel};_previous=new[]{_wheels[0].position,_wheels[1].position};
    }
    private void FixedUpdate()
    {
        if(_body==null||!SemiFunc.IsMasterClientOrSingleplayer()||_grab.playerGrabbing.Count==0)return;
        bool floor=false;Vector3 normal=Vector3.up;
        foreach(var wheel in _wheels)
        {
            int count=Physics.SphereCastNonAlloc(wheel.position,.015f,Vector3.down,_hits,.62f,~0,QueryTriggerInteraction.Ignore);
            for(int i=0;i<count;i++)
            {
                var h=_hits[i];if(h.collider==null||h.collider.transform.IsChildOf(transform)||Vector3.Dot(h.normal,Vector3.up)<.6f)continue;
                if(h.collider.GetComponentInParent<PlayerAvatar>()!=null||h.collider.GetComponentInParent<Enemy>()!=null)continue;
                floor=true;normal=h.normal;break;
            }
            if(floor)break;
        }
        Vector3 pull=Vector3.zero;int holders=0;
        foreach(var player in _grab.playerGrabbing)
        {
            if(player==null||player.physGrabPoint==null)continue;
            pull+=Vector3.ClampMagnitude(player.physGrabPointPullerPosition-player.physGrabPoint.position,1);holders++;
        }
        if(holders>0)WheeledPropPhysics.Assist(_body,pull/holders,normal,true,floor);
    }
    private void LateUpdate()
    {
        if(_wheels==null)return;
        _rollCue-=Time.deltaTime;
        bool grabbed=_grab!=null&&(_grab.grabbed||_grab.playerGrabbing.Count>0);
        for(int i=0;i<2;i++)
        {
            var wheel=_wheels[i];Vector3 position=wheel.position;Vector3 delta=position-_previous[i];_previous[i]=position;
            bool grounded=false;float radius=.545f*Mathf.Max(.001f,transform.lossyScale.y);
            if(grabbed&&Mathf.Abs(Vector3.Dot(transform.right,Vector3.up))<.5f)
            {
                int count=Physics.SphereCastNonAlloc(position,.015f,Vector3.down,_hits,radius+.065f,~0,QueryTriggerInteraction.Ignore);
                for(int j=0;j<count;j++)
                {
                    var hit=_hits[j];if(hit.collider==null||hit.collider.transform.IsChildOf(transform))continue;
                    if(hit.collider.GetComponentInParent<PlayerAvatar>()!=null||hit.collider.GetComponentInParent<Enemy>()!=null)continue;
                    if(Vector3.Dot(hit.normal,Vector3.up)<.45f)continue;
                    grounded=true;
                    if(hit.rigidbody!=null)delta-=hit.rigidbody.GetPointVelocity(position)*Time.deltaTime;
                    break;
                }
            }
            float travel=Vector3.Dot(delta,transform.forward);
            float angle=ValuableDynamics.WheelDegrees(travel,radius,grabbed,grounded,delta.sqrMagnitude>1f);
            if(angle!=0f)wheel.localRotation*=Quaternion.AngleAxis(angle,Vector3.right);
            if(Mathf.Abs(angle)>.05f&&_rollCue<=0)
            {_rollCue=.35f;GetComponent<ValuableFoley>()?.Emit(ValuableFoley.Roll,.07f);}
        }
    }
}

internal sealed class WaterWatch : MonoBehaviour
{
    private PhysGrabObject _grab;
    private PhotonView _view;
    private ValuableArtInstance _art;
    private readonly List<(ValuablePartBinding binding, Material material, float halfHeight)> _liquids=new();
    private Light _glow;
    private bool _flipped,_wasHost;
    private int _revision;
    private double _epoch,_flipEpoch,_lastFlip=-100;
    private float _broadcast;
    private float _drip;
    private double Now=>GameManager.Multiplayer()?PhotonNetwork.Time:Time.timeAsDouble;
    private void Start()
    {
        _grab=GetComponent<PhysGrabObject>();_view=GetComponent<PhotonView>();_art=GetComponent<ValuableArtInstance>();
        if(_art==null||!_art.EnsureBindings()){enabled=false;return;}
        _epoch=Now;_flipEpoch=Now-2;
        foreach(var part in _art.bindings)
        {
            if(part.group!="drop"&&part.group!="top_pool"&&part.group!="bottom_pool")continue;
            var renderer=part.node.GetComponent<MeshRenderer>();
            _liquids.Add((part,renderer.material,part.node.GetComponent<MeshFilter>().sharedMesh.bounds.extents.y));
        }
        var light=new GameObject("Water watch soft glow");light.transform.SetParent(transform,false);light.transform.localPosition=new Vector3(0,.13f,0);
        _glow=light.AddComponent<Light>();_glow.type=LightType.Point;_glow.range=.7f;_glow.intensity=.16f;_glow.shadows=LightShadows.None;
    }
    private void Update()
    {
        if(_art==null||_glow==null)return;
        bool host=SemiFunc.IsMasterClientOrSingleplayer();
        if(host&&!_wasHost)_broadcast=0;_wasHost=host;
        if(_grab.grabbedLocal&&SemiFunc.NoTextInputsActive()&&Input.GetKeyDown(InteractiveValuables.FlipKey.Value))
        {
            var avatar=PlayerAvatar.instance;
            if(avatar!=null)
            {
                if(GameManager.Multiplayer())_view.RPC(nameof(IHM_WatchFlipRequest),RpcTarget.MasterClient,avatar.photonView.ViewID);
                else TryFlip(avatar);
            }
        }
        if(host)
        {
            _broadcast-=Time.unscaledDeltaTime;
            if(_broadcast<=0f)
            {
                _broadcast=.75f;
                if(GameManager.Multiplayer())_view.RPC(nameof(IHM_WatchState),RpcTarget.Others,_flipped,_epoch,_flipEpoch,_revision);
            }
        }
        Draw();
        _drip-=Time.deltaTime;
        if(host&&Now>=_epoch&&ValuableDynamics.Progress(Now,_epoch,InteractiveValuables.FlowSeconds.Value)<1&&_drip<=0)
        {_drip=1.2f;GetComponent<ValuableFoley>()?.Emit(ValuableFoley.Drip,.035f);}
    }
    private void TryFlip(PlayerAvatar avatar)
    {
        bool holder=avatar!=null&&avatar.physGrabber!=null&&_grab.playerGrabbing.Contains(avatar.physGrabber);
        if(!ValuableDynamics.AcceptFlip(holder,Now,_lastFlip,1.5f))return;
        _lastFlip=Now;_flipped=!_flipped;_flipEpoch=Now;_epoch=Now+1.2;_revision++;_broadcast=0;
        GetComponent<ValuableFoley>()?.Emit(ValuableFoley.Flip,.12f);
    }
    [PunRPC] private void IHM_WatchFlipRequest(int avatarId,PhotonMessageInfo info=default)
    {
        if(!SemiFunc.IsMasterClientOrSingleplayer())return;
        var avatar=SemiFunc.PlayerAvatarGetFromPhotonID(avatarId);
        if(GameManager.Multiplayer()&&(avatar==null||info.Sender==null||avatar.photonView.OwnerActorNr!=info.Sender.ActorNumber))return;
        TryFlip(avatar);
    }
    [PunRPC] private void IHM_WatchState(bool flipped,double epoch,double flipEpoch,int revision,PhotonMessageInfo info=default)
    {
        if(!SemiFunc.MasterOnlyRPC(info)||!ValuableDynamics.Finite(epoch)||!ValuableDynamics.Finite(flipEpoch)||epoch>Now+5||flipEpoch>Now+5||revision<_revision)return;
        _flipped=flipped;_epoch=epoch;_flipEpoch=flipEpoch;_revision=revision;
    }
    private void Draw()
    {
        float seconds=InteractiveValuables.FlowSeconds.Value;
        float progress=ValuableDynamics.Progress(Now,_epoch,seconds);
        float flip=Mathf.SmoothStep(0,1,Mathf.Clamp01((float)(Now-_flipEpoch)/1.2f));
        float end=_flipped?180:0;float start=_flipped?0:180;
        _art.casePivot.localRotation=Quaternion.AngleAxis(Mathf.Lerp(start,end,flip),Vector3.forward);
        float fluidVisibility=Now<_epoch?Mathf.Clamp01(Mathf.Abs(flip-.5f)*2):1;
        Color blue=new(.025f,.13f,.85f),rose=new(.9f,.025f,.28f),cyan=new(.04f,.65f,.65f);
        Color average=Color.Lerp(blue,rose,progress);
        foreach(var item in _liquids)
        {
            var b=item.binding;Vector3 pos=b.rest;float scale=1;
            Color c=average;
            if(b.group=="drop")
            {
                float phase=Mathf.Repeat((float)((Now-_epoch)/6)+b.phase,1);
                scale=Mathf.Max(.001f,Mathf.Min(1,phase/.12f,(1-phase)/.13f))*fluidVisibility*(progress<1?1:0);
                pos.y=Mathf.Lerp(.179f,.047f,phase);
                b.node.localScale=new Vector3(scale,scale*(1+.12f*Mathf.Sin(phase*Mathf.PI)),scale);
                c=Color.Lerp(Color.Lerp(blue,rose,Mathf.Repeat(progress+b.phase,1)),cyan,.16f+.15f*Mathf.Sin(phase*Mathf.PI));
            }
            else
            {
                scale=(b.group=="top_pool"?Mathf.Lerp(1,.12f,progress):Mathf.Lerp(.15f,1.25f,progress))*fluidVisibility;
                pos.y=b.rest.y-item.halfHeight+item.halfHeight*scale;
                b.node.localScale=new Vector3(1,Mathf.Max(.001f,scale),1);
                c=Color.Lerp(b.group=="top_pool"?blue:rose,average,.65f);
            }
            b.node.localPosition=pos;
            foreach(string property in new[]{"_Color","_BaseColor"})if(item.material.HasProperty(property))item.material.SetColor(property,c);
            if(item.material.HasProperty("_EmissionColor"))item.material.SetColor("_EmissionColor",c*1.6f);
        }
        _glow.color=average;_glow.intensity=.13f+.025f*Mathf.Sin((float)Now*.7f);
    }
    private void OnDestroy()
    {
        foreach(var item in _liquids)if(item.material!=null)Object.Destroy(item.material);
    }
}
