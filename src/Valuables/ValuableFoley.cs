using System;
using IncreaseHardMod.Visuals;
using Photon.Pun;
using UnityEngine;

namespace IncreaseHardMod.Valuables;

internal sealed class ValuableFoley : MonoBehaviour
{
    internal const int Metal=0,Plastic=1,Glass=2,Bone=3,Paper=4,Roll=5,Motor=6,Flip=7,Drip=8,Tube=9,Porcelain=10;
    private static readonly string[] Names={"metal","plastic","glass","bone","paper","roll","motor","flip","drip","tube","porcelain"};
    private PhotonView _view;
    private int _impact;
    private float _last=-10;
    private void Start()
    {
        _view=GetComponent<PhotonView>();string key=GetComponent<ValuableArtInstance>().assetKey;
        _impact=Impact(key);
    }
    internal static int Impact(string key)=>key=="maneki_neko"?Porcelain:key=="crystal_skull"||key=="water_timer"?Glass:key=="skeleton"?Bone:key=="bath_gift_set"?Paper:key=="robot_vacuum"||key=="ram_pair"?Plastic:Metal;
    internal static PhysAudio Preset(string key,PhysAudio donor)
    {
        var preset=UnityEngine.Object.Instantiate(donor);string cue="valuable_"+Names[Impact(key)];
        Sound S(float volume){var sound=ItemAudio.Cue(cue,volume);sound.Type=AudioManager.AudioType.MaterialImpact;return sound;}
        preset.impactLight=S(.10f);preset.impactMedium=S(.20f);preset.impactHeavy=S(.32f);
        if(key=="maneki_neko")cue="valuable_porcelain_break";
        preset.breakLight=S(.13f);preset.breakMedium=S(.23f);preset.breakHeavy=S(.32f);preset.destroy=S(.35f);return preset;
    }
    internal void Contact(float speed)
    {
        if(speed>.24f&&Time.time-_last>.18f)Emit(_impact,Mathf.Clamp(speed*.10f,.045f,.25f));
    }
    internal void Emit(int cue,float gain)
    {
        if(!SemiFunc.IsMasterClientOrSingleplayer()||cue<0||cue>=Names.Length||Time.time-_last<.08f)return;
        _last=Time.time;_view??=GetComponent<PhotonView>();
        if(GameManager.Multiplayer())_view.RPC(nameof(IHM_ValuableSound),RpcTarget.All,cue,gain);
        else Play(cue,gain);
    }
    [PunRPC] private void IHM_ValuableSound(int cue,float gain,PhotonMessageInfo info=default)
    {
        if(!SemiFunc.MasterOnlyRPC(info)||cue<0||cue>=Names.Length||!ValuableDynamics.Finite(gain))return;
        Play(cue,gain);
    }
    private void Play(int cue,float gain)=>ItemAudio.Cue("valuable_"+Names[cue],Mathf.Clamp(gain,0,.35f)).Play(transform.position);
}

internal sealed class ValuableContact : MonoBehaviour
{
    internal ValuableFoley owner;
    private void OnCollisionEnter(Collision collision){if(owner!=null)owner.Contact(collision.relativeVelocity.magnitude);}
}
