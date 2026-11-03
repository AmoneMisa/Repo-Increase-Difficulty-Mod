using System;
using System.Collections.Generic;
using System.Linq;
using Photon.Pun;
using UnityEngine;
using Object=UnityEngine.Object;

namespace IncreaseHardMod.Valuables;

/// <summary>Host-simulated jointed teaching skeleton and loose centrifuge samples.</summary>
internal sealed class CollectionPhysics : MonoBehaviour
{
    private sealed class Part
    {
        internal Transform node,parent;
        internal Vector3 rest,receivedPosition;
        internal Quaternion rotation,receivedRotation;
        internal Rigidbody body;
        internal Collider collider;
        internal bool released;
        internal float releaseAge;
        internal bool clearedCase;
    }
    private readonly List<Part> _parts=new();
    private PhysGrabObject _grab;
    private Rigidbody _rootBody;
    private PhotonView _view;
    private ValuableFoley _sound;
    private ValuableObject _valuable;
    private PhysGrabObjectImpactDetector _impact;
    private readonly TubeLossState _tubeLoss=new();
    private bool _tubes,_host,_received;
    private double _receivedAt=-1;
    private float _send,_inverted,_pullTimer;
    private Vector3 _previousRoot;
    private Collider[] _caseColliders=Array.Empty<Collider>();
    private void Start()
    {
        var art=GetComponent<ValuableArtInstance>();
        if(art==null||!art.EnsureBindings()){enabled=false;return;}
        _tubes=art.assetKey=="centrifuge";
        _grab=GetComponent<PhysGrabObject>();_rootBody=GetComponent<Rigidbody>();_view=GetComponent<PhotonView>();_sound=GetComponent<ValuableFoley>();
        _valuable=GetComponent<ValuableObject>();_impact=GetComponent<PhysGrabObjectImpactDetector>();
        _host=SemiFunc.IsMasterClientOrSingleplayer();_previousRoot=transform.position;
        var definitions=new[]{"pelvis","torso","head","upper_arm_left","forearm_left","hand_left","upper_arm_right","forearm_right","hand_right","thigh_left","shin_left","foot_left","thigh_right","shin_right","foot_right"};
        var parents=new[]{"","pelvis","torso","torso","upper_arm_left","forearm_left","torso","upper_arm_right","forearm_right","pelvis","thigh_left","shin_left","pelvis","thigh_right","shin_right"};
        var joints=new[]{new Vector3(0,1.04f,.015f),new Vector3(0,1.09f,.04f),new Vector3(0,1.49f,.02f),new Vector3(-.19f,1.395f,0),new Vector3(-.235f,1.10f,-.01f),new Vector3(-.26f,.85f,-.02f),new Vector3(.19f,1.395f,0),new Vector3(.235f,1.10f,-.01f),new Vector3(.26f,.85f,-.02f),new Vector3(-.095f,1.02f,0),new Vector3(-.095f,.68f,-.009f),new Vector3(-.095f,.26f,-.01f),new Vector3(.095f,1.02f,0),new Vector3(.095f,.68f,-.009f),new Vector3(.095f,.26f,-.01f)};
        var bodies=new Dictionary<string,Rigidbody>();
        var groups=_tubes?art.bindings.Where(b=>b.group.StartsWith("tube_",StringComparison.Ordinal)).OrderBy(b=>b.group,StringComparer.Ordinal).ToArray():definitions.Select(g=>art.bindings.Single(b=>b.group==g)).ToArray();
        for(int i=0;i<groups.Length;i++)
        {
            var b=groups[i];var node=b.node;var bounds=node.GetComponent<MeshFilter>().sharedMesh.bounds;
            var collider=node.gameObject.AddComponent<BoxCollider>();collider.center=bounds.center;collider.size=Vector3.Max(bounds.size,new Vector3(.01f,.01f,.01f));
            var body=node.gameObject.AddComponent<Rigidbody>();body.mass=_tubes?.055f:i<3?.75f:.23f;body.interpolation=RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode=CollisionDetectionMode.ContinuousSpeculative;body.maxAngularVelocity=8;body.drag=.12f;body.angularDrag=.5f;
            body.isKinematic=_tubes||!_host;collider.enabled=true;
            var p=new Part{node=node,parent=node.parent,rest=node.localPosition,rotation=node.localRotation,body=body,collider=collider};_parts.Add(p);
            node.gameObject.AddComponent<ValuableContact>().owner=_sound;
            var marker=node.gameObject.AddComponent<PhysGrabObjectCollider>();marker.colliderID=_grab.colliders.Count;_grab.colliders.Add(node);
            if(_tubes)
            {
                var sample=node.gameObject.AddComponent<CentrifugeSampleContact>();sample.owner=this;sample.index=i;
            }
            if(!_tubes)
            {
                Rigidbody connected=parents[i].Length==0?_rootBody:bodies[parents[i]];bodies[definitions[i]]=body;
                var joint=node.gameObject.AddComponent<ConfigurableJoint>();joint.connectedBody=connected;joint.autoConfigureConnectedAnchor=false;
                Vector3 anchor=transform.TransformPoint(joints[i]);joint.anchor=node.InverseTransformPoint(anchor);joint.connectedAnchor=connected.transform.InverseTransformPoint(anchor);
                joint.xMotion=joint.yMotion=joint.zMotion=ConfigurableJointMotion.Locked;
                joint.angularXMotion=joint.angularYMotion=joint.angularZMotion=ConfigurableJointMotion.Limited;
                float limit=i==0?9:i==1?16:i==2?28:38;
                joint.lowAngularXLimit=new SoftJointLimit{limit=-limit};joint.highAngularXLimit=new SoftJointLimit{limit=limit};
                joint.angularYLimit=new SoftJointLimit{limit=limit*.7f};joint.angularZLimit=new SoftJointLimit{limit=limit};
                joint.rotationDriveMode=RotationDriveMode.Slerp;joint.slerpDrive=new JointDrive{positionSpring=i<3?38:4,positionDamper=i<3?5:.7f,maximumForce=80};
                joint.projectionMode=JointProjectionMode.PositionAndRotation;joint.projectionDistance=.025f;joint.projectionAngle=12;
                joint.enableCollision=false;body.solverIterations=10;body.solverVelocityIterations=6;
            }
        }
        // Bone collisions react to the world and players; self-contact must not explode the stand.
        var own=GetComponentsInChildren<Collider>(true);
        _caseColliders=own.Where(c=>!_parts.Any(p=>p.collider==c)).ToArray();
        foreach(var p in _parts)foreach(var c in own)if(c!=p.collider)Physics.IgnoreCollision(p.collider,c,true);
        if(_tubes&&_impact!=null)_impact.onAllBreaks.AddListener(TubesDamaged);
    }
    private void FixedUpdate()
    {
        if(_parts.Count==0)return;
        bool host=SemiFunc.IsMasterClientOrSingleplayer();
        if(host!=_host)
        {
            _host=host;
            foreach(var p in _parts){p.body.isKinematic=!host||_tubes&&!p.released;p.collider.enabled=!_tubes||!p.released||host;}
        }
        if(host)
        {
            if(!_tubes&&(transform.position-_previousRoot).sqrMagnitude>9)ResetParts();
            _previousRoot=transform.position;
            if(_tubes)
            {
                foreach(var p in _parts)
                {
                    if(!p.released||p.clearedCase)continue;p.releaseAge+=Time.fixedDeltaTime;
                    if(p.releaseAge>.5f)
                    {foreach(var c in _caseColliders)if(c!=null)Physics.IgnoreCollision(p.collider,c,false);p.clearedCase=true;}
                }
                _inverted=ValuableDynamics.ShouldSpill(Vector3.Dot(transform.up,Vector3.up))?_inverted+Time.fixedDeltaTime:0;
                if(_inverted>.22f)
                    LoseTubeKit();
                bool pulling=false;
                if(!_tubeLoss.Committed)
                    foreach(var grabber in _grab.playerGrabbing)
                    {
                        if(grabber==null||grabber.physGrabPoint==null||!_parts.Any(p=>p.collider==grabber.grabbedPhysGrabObjectCollider))continue;
                        if(Vector3.Dot(grabber.physGrabPointPullerPosition-grabber.physGrabPoint.position,transform.up)>.035f){pulling=true;break;}
                    }
                _pullTimer=pulling?_pullTimer+Time.fixedDeltaTime:0;
                if(_pullTimer>.12f)LoseTubeKit();
                ApplyTubePenalty();
            }
            _send-=Time.fixedDeltaTime;
            if(_send<=0&&GameManager.Multiplayer())
            {
                _send=.10f;
                var positions=_parts.Select(p=>_tubes&&p.released?p.node.position:p.node.localPosition).ToArray();
                var rotations=_parts.Select(p=>_tubes&&p.released?p.node.rotation:p.node.localRotation).ToArray();
                int mask=0;for(int i=0;i<_parts.Count;i++)if(_parts[i].released)mask|=1<<i;
                _view.RPC(nameof(IHM_CollectionPose),RpcTarget.Others,mask,positions,rotations,PhotonNetwork.Time,_tubeLoss.Cost,_tubeLoss.Applied);
            }
        }
        else if(_received)
        {
            float t=1-Mathf.Exp(-Time.fixedDeltaTime*18);
            foreach(var p in _parts)
            {
                if(_tubes&&p.released){p.body.MovePosition(Vector3.Lerp(p.node.position,p.receivedPosition,t));p.body.MoveRotation(Quaternion.Slerp(p.node.rotation,p.receivedRotation,t));}
                else{p.body.MovePosition(Vector3.Lerp(p.node.position,p.parent.TransformPoint(p.receivedPosition),t));p.body.MoveRotation(Quaternion.Slerp(p.node.rotation,p.parent.rotation*p.receivedRotation,t));}
            }
        }
    }
    private void TubesDamaged()
    {
        if(SemiFunc.IsMasterClientOrSingleplayer())LoseTubeKit();
    }
    internal void SampleHit(int index,float speed)
    {
        if(_tubes&&SemiFunc.IsMasterClientOrSingleplayer()&&index>=0&&index<_parts.Count&&!_parts[index].released&&speed>1.25f&&ValuableDynamics.Finite(speed))LoseTubeKit();
    }
    private void LoseTubeKit()
    {
        if(!_tubes||!SemiFunc.IsMasterClientOrSingleplayer()||!_tubeLoss.Commit(UnityEngine.Random.Range(2000,3001)))return;
        // A grab aimed into a tube must not remain attached to the case after the sample falls out.
        foreach(var grabber in _grab.playerGrabbing.ToArray())
        {
            if(grabber==null||!_parts.Any(p=>p.collider==grabber.grabbedPhysGrabObjectCollider))continue;
            if(GameManager.Multiplayer())grabber.photonView.RPC("ReleaseObjectRPC",RpcTarget.All,false,.1f,_view.ViewID);
            else grabber.ReleaseObject(_view.ViewID);
        }
        foreach(var p in _parts)
        {
            if(p.released)continue;Release(p);p.body.velocity=_rootBody.GetPointVelocity(p.node.position);p.body.angularVelocity=_rootBody.angularVelocity;
        }
        _sound.Emit(ValuableFoley.Tube,.18f);_send=0;
        ApplyTubePenalty();
    }
    private void ApplyTubePenalty()
    {
        if(_valuable==null||_impact==null||_grab.dead)return;
        int debit=_tubeLoss.TakePayment(SemiFunc.IsMasterClientOrSingleplayer(),_valuable.dollarValueSet,_valuable.dollarValueCurrent);
        if(debit<=0)return;
        // Native BreakRPC updates current value, the native HUD and every peer. It preserves original value.
        // Applied is already set, so the nested onAllBreaks event cannot charge or spill twice.
        _impact.Break(debit,transform.position,_impact.breakLevelLight,true);_send=0;
        Plugin.Log.LogInfo($"[Valuables] Centrifuge sample kit lost: ${debit}; one-time kit cost ${_tubeLoss.Cost}.");
    }
    private void Release(Part p)
    {
        if(_tubes)_grab.colliders.Remove(p.node);
        p.released=true;p.releaseAge=0;p.clearedCase=false;p.node.SetParent(null,true);p.body.isKinematic=!_host;p.collider.enabled=_host;
        // Let seated samples clear the simplified casing collider before allowing return impacts.
        if(_host)foreach(var c in _caseColliders)if(c!=null)Physics.IgnoreCollision(p.collider,c,true);
    }
    [PunRPC] private void IHM_CollectionPose(int mask,Vector3[] positions,Quaternion[] rotations,double stamp,int kitCost,bool kitPaid,PhotonMessageInfo info=default)
    {
        if(!SemiFunc.MasterOnlyRPC(info)||positions==null||rotations==null||positions.Length!=_parts.Count||rotations.Length!=_parts.Count||!ValuableDynamics.Finite(stamp)||stamp<=_receivedAt||stamp>PhotonNetwork.Time+2)return;
        for(int i=0;i<_parts.Count;i++)
        {
            var v=positions[i];var q=rotations[i];
            if(!ValuableDynamics.Finite(v.x)||!ValuableDynamics.Finite(v.y)||!ValuableDynamics.Finite(v.z)||v.sqrMagnitude>1e8||!ValuableDynamics.Finite(q.x)||!ValuableDynamics.Finite(q.y)||!ValuableDynamics.Finite(q.z)||!ValuableDynamics.Finite(q.w))return;
            float norm=q.x*q.x+q.y*q.y+q.z*q.z+q.w*q.w;if(norm<.8f||norm>1.2f)return;
        }
        if(_tubes&&((mask==0)!=(kitCost==0)||!_tubeLoss.Import(kitCost,kitPaid)))return;
        _receivedAt=stamp;_received=true;
        for(int i=0;i<_parts.Count;i++)
        {
            var p=_parts[i];if(_tubes&&(mask&(1<<i))!=0&&!p.released)Release(p);
            p.receivedPosition=positions[i];p.receivedRotation=rotations[i];
        }
    }
    private void ResetParts()
    {
        foreach(var p in _parts)
        {
            p.body.isKinematic=true;p.node.SetParent(p.parent,false);p.node.localPosition=p.rest;p.node.localRotation=p.rotation;p.released=false;p.releaseAge=0;p.clearedCase=false;
            p.body.isKinematic=_tubes||!_host;p.collider.enabled=!_tubes;
            if(!p.body.isKinematic){p.body.velocity=Vector3.zero;p.body.angularVelocity=Vector3.zero;}
        }
        if(!_tubes)
            foreach(var p in _parts)
            {
                foreach(var c in _caseColliders)if(c!=null)Physics.IgnoreCollision(p.collider,c,true);
                foreach(var other in _parts)if(other!=p)Physics.IgnoreCollision(p.collider,other.collider,true);
            }
        _inverted=0;_received=false;_receivedAt=-1;_send=0;
    }
    private void OnEnable()
    {
        if(_parts.Count==0)return;
        if(!_tubes){ResetParts();return;}
        foreach(var p in _parts)if(p.node!=null&&p.released)p.node.gameObject.SetActive(true);
    }
    private void OnDisable()
    {
        if(_parts.Count==0)return;
        if(!_tubes){ResetParts();return;}
        // Preserve missing samples and the paid flag across disable/enable; never refill the kit for free.
        foreach(var p in _parts)if(p.node!=null&&p.released)p.node.gameObject.SetActive(false);
    }
    private void OnDestroy()
    {
        if(_tubes&&_impact!=null)_impact.onAllBreaks.RemoveListener(TubesDamaged);
        if(_tubes)foreach(var p in _parts)if(p.node!=null&&p.released)Object.Destroy(p.node.gameObject);
    }
}

internal sealed class CentrifugeSampleContact : MonoBehaviour
{
    internal CollectionPhysics owner;
    internal int index;
    private void OnCollisionEnter(Collision collision){if(owner!=null)owner.SampleHit(index,collision.relativeVelocity.magnitude);}
    private void OnTriggerEnter(Collider other)
    {
        var hurt=other.GetComponentInParent<HurtCollider>();
        if(owner!=null&&hurt!=null&&hurt.isActiveAndEnabled&&hurt.physLogic&&(hurt.physDestroy||hurt.physImpact!=HurtCollider.BreakImpact.None))owner.SampleHit(index,2f);
    }
}
