using UnityEngine;

namespace IncreaseHardMod.Valuables;

/// <summary>Floor-only rolling assistance; keeps the cannon's real mass and airborne grab behavior.</summary>
internal static class WheeledPropPhysics
{
    // Contact tolerance is in world metres, independent of the item's scale.
    internal static bool Contact(Transform root,Vector3 axle,float radius,RaycastHit[] hits,out RaycastHit contact)
    {
        contact=default;float nearest=float.PositiveInfinity;
        int count=Physics.RaycastNonAlloc(axle,Vector3.down,hits,radius+.025f,~0,QueryTriggerInteraction.Ignore);
        for(int i=0;i<count;i++)
        {
            var hit=hits[i];
            if(hit.collider==null||hit.collider.transform.IsChildOf(root)||hit.collider.GetComponentInParent<PlayerAvatar>()!=null||
                hit.collider.GetComponentInParent<Enemy>()!=null||Vector3.Dot(hit.normal,Vector3.up)<.6f||hit.distance>=nearest)continue;
            nearest=hit.distance;contact=hit;
        }
        return !float.IsPositiveInfinity(nearest);
    }
    private static PhysicsMaterial _rolling;
    private static PhysicsMaterial RollingContact
    {
        get
        {
            if(_rolling==null)
                _rolling=new PhysicsMaterial("IHM Wheeled Rolling Contact")
                {
                    staticFriction=.035f,dynamicFriction=.025f,bounciness=0f,
                    frictionCombine=PhysicsMaterialCombine.Minimum,bounceCombine=PhysicsMaterialCombine.Minimum,
                };
            return _rolling;
        }
    }
    internal static void SetRollingContact(Collider collider)=>collider.sharedMaterial=RollingContact;
    internal static Quaternion Upright(Quaternion rotation)
    {
        var forward=Vector3.ProjectOnPlane(rotation*Vector3.forward,Vector3.up);
        if(forward.sqrMagnitude<.001f)forward=Vector3.ProjectOnPlane(rotation*Vector3.up,Vector3.up);
        if(forward.sqrMagnitude<.001f)forward=Vector3.forward;
        return Quaternion.LookRotation(forward.normalized,Vector3.up);
    }
    internal static void Prepare(Rigidbody body)
    {
        body.constraints=RigidbodyConstraints.None;body.linearDamping=.04f;body.angularDamping=.2f;body.useGravity=true;
    }
    internal static void AlignAtSpawn(Rigidbody body)
    {
        var root=body.transform;var rotation=Upright(root.rotation);root.rotation=rotation;body.rotation=rotation;
        Physics.SyncTransforms();var hits=Physics.RaycastAll(root.position+Vector3.up*.2f,Vector3.down,3f,~0,QueryTriggerInteraction.Ignore);
        float floor=float.NegativeInfinity;
        foreach(var hit in hits)
            if(!hit.collider.transform.IsChildOf(root)&&!hit.collider.transform.root.CompareTag("Player")&&Vector3.Dot(hit.normal,Vector3.up)>.6f)floor=Mathf.Max(floor,hit.point.y);
        if(float.IsNegativeInfinity(floor))return;
        float bottom=float.PositiveInfinity;
        foreach(var collider in root.GetComponentsInChildren<Collider>())if(collider.enabled&&!collider.isTrigger)bottom=Mathf.Min(bottom,collider.bounds.min.y);
        if(bottom<floor+.02f){var position=root.position;position.y+=floor+.02f-bottom;root.position=position;body.position=position;}
    }
    internal static void Assist(Rigidbody body,Vector3 pull,Vector3 groundNormal,bool grabbed,bool grounded)
    {
        if(body.isKinematic||!grabbed||!grounded||Vector3.Dot(body.transform.up,Vector3.up)<.65f)return;
        var input=Vector3.ClampMagnitude(Vector3.ProjectOnPlane(pull,groundNormal),1);
        if(input.sqrMagnitude<.0004f)return;
        var horizontal=Vector3.ProjectOnPlane(body.linearVelocity,groundNormal);
        var acceleration=Vector3.ClampMagnitude((input*1.6f-horizontal)*4,4);
        body.AddForce(acceleration,ForceMode.Acceleration);
        // Steer the axle toward the pull, while allowing the carriage to roll backwards.
        float angle=Vector3.SignedAngle(Vector3.ProjectOnPlane(body.transform.forward,groundNormal),input,groundNormal);
        if(Mathf.Abs(angle)>90)angle=Mathf.DeltaAngle(0,angle+180);
        body.AddTorque(groundNormal*Mathf.Clamp(angle*.025f,-2,2),ForceMode.Acceleration);
    }
}

// Native PhysGrabObject/PhotonTransformView initialise in Start. Publish the
// corrected pose afterwards so peers do not restore the upside-down spawn pose.
internal sealed class WheeledSpawnPose:MonoBehaviour
{
    private System.Collections.IEnumerator Start()
    {
        yield return null;
        if(!SemiFunc.IsMasterClientOrSingleplayer())yield break;
        var body=GetComponent<Rigidbody>();var grab=GetComponent<PhysGrabObject>();
        if(body==null||grab==null)yield break;
        WheeledPropPhysics.AlignAtSpawn(body);
        grab.Teleport(body.position,body.rotation);
        // Settling after spawn (a slope, a ledge, a neighbour's collider) can still tip it onto its nose or side.
        // For the first seconds, while nobody holds it, a tipped prop is set back on its wheels once it is still.
        float until=Time.time+5f;int fixes=0;
        while(Time.time<until&&fixes<3&&this!=null&&body!=null)
        {
            yield return new WaitForSeconds(.5f);
            if(body==null||body.isKinematic||grab.grabbed)continue;
            float tilt=Vector3.Angle(body.transform.up,Vector3.up);
            if(tilt<35f||body.linearVelocity.sqrMagnitude>.05f||body.angularVelocity.sqrMagnitude>.2f)continue;
            body.linearVelocity=Vector3.zero;body.angularVelocity=Vector3.zero;
            WheeledPropPhysics.AlignAtSpawn(body);
            grab.Teleport(body.position,body.rotation);
            fixes++;
            Plugin.Log.LogInfo($"[Valuables] {name} tipped {tilt:0} deg after spawning at {body.position}; set upright.");
        }
    }
}
