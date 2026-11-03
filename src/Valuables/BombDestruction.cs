using System.Linq;
using UnityEngine;

namespace IncreaseHardMod.Valuables;

/// <summary>Native destruction-event explosion, never a Unity OnDestroy/unload side effect.</summary>
internal sealed class BombDestruction : MonoBehaviour
{
    private PhysGrabObjectImpactDetector _impact;
    private ParticleScriptExplosion _explosion;
    private bool _detonated;
    private void Start()
    {
        _impact=GetComponent<PhysGrabObjectImpactDetector>();_explosion=GetComponent<ParticleScriptExplosion>();
        if(StatsManager.instance!=null)
            foreach(var item in StatsManager.instance.itemDictionary.Values.Where(i=>i!=null).OrderBy(i=>i.name,System.StringComparer.Ordinal))
            {
                var prefab=item?.prefab?.Prefab;
                if(prefab==null||prefab.GetComponent<ItemGrenadeExplosive>()==null)continue;
                var donor=prefab.GetComponent<ParticleScriptExplosion>();
                if(donor!=null&&donor.explosionPreset!=null){_explosion.explosionPreset=donor.explosionPreset;break;}
            }
        if(_impact==null||_explosion.explosionPreset==null)
        {Plugin.Log.LogError("[Valuables] Bomb has no native explosive grenade preset/destruction detector; detonation unavailable.");return;}
        _impact.onDestroy.AddListener(Detonate);
    }
    private void Detonate()
    {
        if(_detonated||!GetComponent<PhysGrabObject>().dead)return;
        _detonated=true;
        // The game's validated DestroyObjectRPC invokes this on every peer, just like native grenades.
        // Native HurtCollider supplies its normal player/enemy/valuable damage authority and attribution.
        _explosion.playerHitFullRuckusOverride=true;
        _explosion.Spawn(transform.position,1.2f,75,160,4f);
    }
    private void OnDestroy(){if(_impact!=null)_impact.onDestroy.RemoveListener(Detonate);}
}
