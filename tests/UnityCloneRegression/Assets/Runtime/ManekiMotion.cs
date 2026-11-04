using Photon.Pun;
using UnityEngine;

namespace IncreaseHardMod.Valuables;

internal sealed class ManekiMotion:MonoBehaviour
{
    private Transform _paw;
    internal const double PeriodSeconds=8.0/3.0;
    internal static float AngleAt(double time)=>3f-15f*Mathf.Cos((float)((time%PeriodSeconds)*(System.Math.PI*2/PeriodSeconds)));
    private void Start()
    {
        var art=GetComponent<ValuableArtInstance>();
        if(art==null||!art.EnsureBindings()||art.beckonPivot==null){enabled=false;return;}
        _paw=art.beckonPivot;
    }
    // Physics samples the same parent transform as rendering. LateUpdate avoids
    // a visible 50 Hz staircase on displays rendering faster than the physics tick.
    private void FixedUpdate()=>ApplyPose();
    private void LateUpdate()=>ApplyPose();
    private void ApplyPose()
    {
        if(_paw==null)return;
        double time=GameManager.Multiplayer()?PhotonNetwork.Time:Time.timeAsDouble;
        // A shared clock gives late joiners the same gentle fore/aft beckoning phase.
        float angle=AngleAt(time);
        _paw.localRotation=Quaternion.AngleAxis(angle,Vector3.right);
    }
}
