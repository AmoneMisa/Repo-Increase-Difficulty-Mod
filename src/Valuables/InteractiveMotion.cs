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

