using UnityEngine;

namespace IncreaseHardMod.Gear;

/// <summary>The native drone's self-hover, separated from the effect removed from the donor prefab.</summary>
internal sealed class ItemHover : MonoBehaviour
{
    private PhysGrabObject _body;
    private ItemDrone _drone;
    private ItemOrb _orb;
    private ItemEquippable _equippable;

    private void Awake()
    {
        _body=GetComponent<PhysGrabObject>();
        _drone=GetComponent<ItemDrone>();
        _orb=GetComponent<ItemOrb>();
        _equippable=GetComponent<ItemEquippable>();
    }

    private void Update()
    {
        if(!SemiFunc.IsMasterClientOrSingleplayer() || _body==null ||
            (_equippable!=null && _equippable.isEquipped))return;
        if(!((_drone!=null && _drone.itemActivated) || (_orb!=null && _orb.itemActive)))return;
        // Refresh native timed overrides. Switching off lets gravity/drag recover normally.
        _body.OverrideZeroGravity();
        _body.OverrideDrag(1f);
        _body.OverrideAngularDrag(10f);
    }
}
