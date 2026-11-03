using Photon.Pun;
using UnityEngine;

namespace IncreaseHardMod.Valuables;

internal sealed class ManekiMotion:MonoBehaviour
{
    private Transform _paw;
    private void Start()
    {
        var art=GetComponent<ValuableArtInstance>();
        if(art==null||!art.EnsureBindings()||art.beckonPivot==null){enabled=false;return;}
        _paw=art.beckonPivot;
    }
    private void FixedUpdate()
    {
        if(_paw==null)return;
        double time=GameManager.Multiplayer()?PhotonNetwork.Time:Time.timeAsDouble;
        // A shared clock gives late joiners the same gentle fore/aft beckoning phase.
        float angle=3f-15f*Mathf.Cos((float)((time%(8.0/3.0))*(System.Math.PI*2/(8.0/3.0))));
        _paw.localRotation=Quaternion.AngleAxis(angle,Vector3.right);
    }
}
