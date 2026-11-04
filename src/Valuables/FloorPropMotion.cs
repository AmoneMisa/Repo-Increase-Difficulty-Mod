using System;
using System.Collections.Generic;
using UnityEngine;

namespace IncreaseHardMod.Valuables;

/// <summary>Each axle follows its own displacement, including turns and caster swivel.</summary>
[DefaultExecutionOrder(-200)]
internal sealed class FloorPropMotion:MonoBehaviour
{
    private sealed class Wheel
    {
        internal Transform node;
        internal Vector3 previous;
        internal float radius,angle,yaw;
        internal bool caster;
    }
    private readonly List<Wheel> _wheels=new();
    private readonly RaycastHit[] _hits=new RaycastHit[32];
    private PhysGrabObject _grab;
    private Rigidbody _body;
    private ValuableArtInstance _art;
    private ValuableFoley _sound;
    private float _soundDelay;
    private void Start()
    {
        _grab=GetComponent<PhysGrabObject>();_body=GetComponent<Rigidbody>();_art=GetComponent<ValuableArtInstance>();_sound=GetComponent<ValuableFoley>();
        if(_body==null||_grab==null||_art==null||!_art.EnsureBindings()){enabled=false;return;}
        if(SemiFunc.IsMasterClientOrSingleplayer())WheeledPropPhysics.AlignAtSpawn(_body);
        foreach(var b in _art.bindings)
            if(b.group.StartsWith("drive_",StringComparison.Ordinal)||b.group.StartsWith("caster_",StringComparison.Ordinal))
            {
                var bounds=b.node.GetComponent<MeshFilter>().sharedMesh.bounds;
                _wheels.Add(new Wheel{node=b.node,previous=b.node.position,radius=Mathf.Max(bounds.extents.y,bounds.extents.z)*Mathf.Abs(b.node.lossyScale.y),caster=b.group.StartsWith("caster_",StringComparison.Ordinal)});
            }
    }
    private void FixedUpdate()
    {
        bool grounded=false;Vector3 normal=Vector3.up;
        foreach(var wheel in _wheels)
            if(WheeledPropPhysics.Contact(transform,wheel.node.position,wheel.radius,_hits,out var hit)){grounded=true;normal=hit.normal;break;}
        if(!grounded||!SemiFunc.IsMasterClientOrSingleplayer())return;
        Vector3 pull=Vector3.zero;int holders=0;
        foreach(var holder in _grab.playerGrabbing)
        {
            if(holder==null||holder.physGrabPoint==null)continue;
            pull+=Vector3.ClampMagnitude(holder.physGrabPointPullerPosition-holder.physGrabPoint.position,1);holders++;
        }
        if(holders>0)WheeledPropPhysics.Assist(_body,pull/holders,normal,true,true);
    }
    private void LateUpdate()=>Animate(Time.deltaTime);
    internal void Animate(float dt)
    {
        bool moving=false;
        foreach(var wheel in _wheels)
        {
            var delta=wheel.node.position-wheel.previous;wheel.previous=wheel.node.position;
            bool grounded=WheeledPropPhysics.Contact(transform,wheel.node.position,wheel.radius,_hits,out var hit);
            if(hit.rigidbody!=null)delta-=hit.rigidbody.GetPointVelocity(wheel.node.position)*dt;
            if(!grounded||delta.sqrMagnitude>1||Vector3.Dot(transform.up,Vector3.up)<.65f)continue;
            delta=Vector3.ProjectOnPlane(delta,hit.normal);
            if(wheel.caster&&delta.sqrMagnitude>1e-10f)
            {
                var local=transform.InverseTransformDirection(delta);
                float yaw=Mathf.Atan2(local.x,local.z)*Mathf.Rad2Deg;
                if(Mathf.Abs(Mathf.DeltaAngle(wheel.yaw,yaw))>90)yaw+=180;
                wheel.yaw=Mathf.MoveTowardsAngle(wheel.yaw,yaw,720*dt);
            }
            var forward=transform.TransformDirection(Quaternion.Euler(0,wheel.yaw,0)*Vector3.forward);
            float angle=ValuableDynamics.WheelDegrees(Vector3.Dot(delta,forward),wheel.radius,true,true,false);
            wheel.angle=Mathf.Repeat(wheel.angle+angle,360);
            wheel.node.localRotation=Quaternion.Euler(0,wheel.yaw,0)*Quaternion.AngleAxis(wheel.angle,Vector3.right);
            moving|=Mathf.Abs(angle)>.05f;
        }
        foreach(var b in _art.bindings)
            if(b.group=="brush"||b.group=="lidar")b.node.localRotation*=Quaternion.AngleAxis(moving?dt*180:0,Vector3.up);
        _soundDelay-=dt;
        if(moving&&_soundDelay<=0){_soundDelay=_art.assetKey=="robot_vacuum"?.65f:.34f;_sound?.Emit(_art.assetKey=="robot_vacuum"?ValuableFoley.Motor:ValuableFoley.Roll,.065f);}
    }
}
