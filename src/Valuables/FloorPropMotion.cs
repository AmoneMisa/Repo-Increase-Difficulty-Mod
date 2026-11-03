using System;
using UnityEngine;

namespace IncreaseHardMod.Valuables;

/// <summary>Grab-guided rolling, driven by physical root velocity and actual floor contact.</summary>
[DefaultExecutionOrder(-200)]
internal sealed class FloorPropMotion : MonoBehaviour
{
    private PhysGrabObject _grab;
    private Rigidbody _body;
    private ValuableArtInstance _art;
    private ValuableFoley _sound;
    private float _soundDelay;
    private readonly RaycastHit[] _hits=new RaycastHit[20];
    private float _radius;
    private Vector3 _previousPosition;
    private void Start()
    {
        _grab=GetComponent<PhysGrabObject>();_body=GetComponent<Rigidbody>();_art=GetComponent<ValuableArtInstance>();_sound=GetComponent<ValuableFoley>();
        if(_art==null||!_art.EnsureBindings()){enabled=false;return;}
        if(SemiFunc.IsMasterClientOrSingleplayer())WheeledPropPhysics.AlignAtSpawn(_body);
        _radius=(_art.assetKey=="robot_vacuum"?.029f:.025f)*transform.lossyScale.x;
        _previousPosition=transform.position;
    }
    private void FixedUpdate()
    {
        bool grabbed=_grab.grabbed||_grab.playerGrabbing.Count>0;
        float height=_art.assetKey=="robot_vacuum"?.055f:.09f;bool floor=false;Vector3 normal=Vector3.up;
        var origin=transform.TransformPoint(_art.visualRoot.localPosition+new Vector3(0,height,0));
        int count=Physics.RaycastNonAlloc(origin,Vector3.down,_hits,height*transform.lossyScale.y+.08f,~0,QueryTriggerInteraction.Ignore);
        for(int i=0;i<count;i++)
        {
            var h=_hits[i];if(h.collider==null||h.collider.transform.IsChildOf(transform)||h.collider.GetComponentInParent<PlayerAvatar>()!=null||h.collider.GetComponentInParent<Enemy>()!=null)continue;
            if(Vector3.Dot(h.normal,Vector3.up)<.6f)continue;floor=true;normal=h.normal;break;
        }
        bool rolling=grabbed&&floor&&Vector3.Dot(transform.up,Vector3.up)>.65f;
        var delta=transform.position-_previousPosition;_previousPosition=transform.position;
        Vector3 velocity=Vector3.ProjectOnPlane(SemiFunc.IsMasterClientOrSingleplayer()?_body.velocity:delta.sqrMagnitude>1?Vector3.zero:delta/Time.fixedDeltaTime,normal);
        if(rolling&&SemiFunc.IsMasterClientOrSingleplayer())
        {
            Vector3 pull=Vector3.zero;int holders=0;
            foreach(var holder in _grab.playerGrabbing)
            {
                if(holder==null||holder.physGrabPoint==null)continue;
                pull+=Vector3.ClampMagnitude(holder.physGrabPointPullerPosition-holder.physGrabPoint.position,1);holders++;
            }
            if(holders>0)WheeledPropPhysics.Assist(_body,pull/holders,normal,true,true);
        }
        float speed=rolling?Vector3.Dot(velocity,transform.forward):0;
        foreach(var b in _art.bindings)
        {
            if(b.group.StartsWith("drive_",StringComparison.Ordinal)||b.group.StartsWith("caster_",StringComparison.Ordinal))
                b.node.localRotation*=Quaternion.AngleAxis(ValuableDynamics.WheelDegrees(speed*Time.fixedDeltaTime,_radius,grabbed,floor,false),Vector3.right);
            else if(b.group=="brush"||b.group=="lidar")b.node.localRotation*=Quaternion.AngleAxis(rolling?Time.fixedDeltaTime*180f:0,Vector3.up);
        }
        _soundDelay-=Time.fixedDeltaTime;
        if(rolling&&velocity.sqrMagnitude>.01f&&_soundDelay<=0)
        {_soundDelay=_art.assetKey=="robot_vacuum"?.65f:.34f;_sound.Emit(_art.assetKey=="robot_vacuum"?ValuableFoley.Motor:ValuableFoley.Roll,.065f);}
    }
}
