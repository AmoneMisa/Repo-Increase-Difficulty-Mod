using System;
using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;

namespace IncreaseHardMod.Valuables;

internal sealed class CollectionLights : MonoBehaviour
{
    private readonly List<(MeshRenderer renderer,Color emission,int phase)> _bars=new();
    private MaterialPropertyBlock _block;
    private void Start()
    {
        _block=new MaterialPropertyBlock();int phase=0;
        var art=GetComponent<ValuableArtInstance>();if(art==null||!art.EnsureBindings()){enabled=false;return;}
        foreach(var b in art.bindings)
        {
            if(!b.group.StartsWith("rgb_",StringComparison.Ordinal))continue;
            var r=b.node.GetComponent<MeshRenderer>();_bars.Add((r,r.sharedMaterial.GetColor("_EmissionColor"),phase++));
        }
    }
    private void Update()
    {
        double time=GameManager.Multiplayer()?PhotonNetwork.Time:Time.timeAsDouble;
        foreach(var b in _bars)
        {b.renderer.GetPropertyBlock(_block);_block.SetColor("_EmissionColor",b.emission*(.84f+.16f*Mathf.Sin((float)(time*.7)+b.phase*.65f)));b.renderer.SetPropertyBlock(_block);}
    }
}
