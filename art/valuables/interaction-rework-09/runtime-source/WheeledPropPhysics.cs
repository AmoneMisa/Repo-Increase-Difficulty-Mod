using UnityEngine;
using System;
using System.Reflection;

namespace IncreaseHardMod.Valuables;

/// <summary>Floor-only rolling assistance; keeps the cannon's real mass and airborne grab behavior.</summary>
internal static class WheeledPropPhysics
{
    private static UnityEngine.Object _rolling;
    private static readonly PropertyInfo SharedMaterial=typeof(Collider).GetProperty("sharedMaterial");
    private static UnityEngine.Object RollingContact
    {
        get
        {
            if(_rolling==null)
            {
                // Unity 6 renamed PhysicMaterial to PhysicsMaterial. Use the installed engine's
                // real property type, avoiding the obsolete dummy type in newer runtimes.
                var type=SharedMaterial.PropertyType;
                _rolling=(UnityEngine.Object)Activator.CreateInstance(type,new object[]{"IHM Wheeled Rolling Contact"});
                type.GetProperty("staticFriction").SetValue(_rolling,.035f);
                type.GetProperty("dynamicFriction").SetValue(_rolling,.025f);
                type.GetProperty("bounciness").SetValue(_rolling,0f);
                foreach(string name in new[]{"frictionCombine","bounceCombine"})
                {var property=type.GetProperty(name);property.SetValue(_rolling,Enum.Parse(property.PropertyType,"Minimum"));}
            }
            return _rolling;
        }
    }
    internal static void SetRollingContact(Collider collider)=>SharedMaterial.SetValue(collider,RollingContact);
    internal static Quaternion Upright(Quaternion rotation)
    {
        var forward=Vector3.ProjectOnPlane(rotation*Vector3.forward,Vector3.up);
        if(forward.sqrMagnitude<.001f)forward=Vector3.ProjectOnPlane(rotation*Vector3.up,Vector3.up);
        if(forward.sqrMagnitude<.001f)forward=Vector3.forward;
        return Quaternion.LookRotation(forward.normalized,Vector3.up);
    }
    internal static void Prepare(Rigidbody body)
    {
        body.constraints=RigidbodyConstraints.None;body.drag=.04f;body.angularDrag=.2f;body.useGravity=true;
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
        var horizontal=Vector3.ProjectOnPlane(body.velocity,groundNormal);
        var acceleration=Vector3.ClampMagnitude((input*1.6f-horizontal)*4,4);
        body.AddForce(acceleration,ForceMode.Acceleration);
        // Steer the axle toward the pull, while allowing the carriage to roll backwards.
        float angle=Vector3.SignedAngle(Vector3.ProjectOnPlane(body.transform.forward,groundNormal),input,groundNormal);
        if(Mathf.Abs(angle)>90)angle=Mathf.DeltaAngle(0,angle+180);
        body.AddTorque(groundNormal*Mathf.Clamp(angle*.025f,-2,2),ForceMode.Acceleration);
    }
}
