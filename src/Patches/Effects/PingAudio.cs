using System;
using System.Collections.Generic;
using UnityEngine;
using IncreaseHardMod.Visuals;

namespace IncreaseHardMod.Patches.Effects;

internal static class PingAudio
{
    private static readonly Dictionary<PingType,Sound> Cues=new();
    internal static string Key(PingType type) => type switch
    {
        PingType.Valuable=>"ping_valuable", PingType.Enemy=>"ping_enemy", PingType.Item=>"ping_item",
        PingType.Upgrade=>"ping_upgrade", PingType.Cart=>"ping_cart", PingType.CosmeticBox=>"ping_cosmetic",
        PingType.Player=>"ping_item", PingType.Weapon=>"ping_item", PingType.Scooter=>"ping_vehicle", PingType.Crystal=>"ping_upgrade", PingType.SkillBox=>"ping_upgrade", PingType.MysteryBox=>"ping_cosmetic", _=>"ping_location",
    };

    internal static void Play(PingType type)
    {
        if(ModConfig.PingSoundVolume.Value<=0f) return;
        try
        {
            if(!Cues.TryGetValue(type,out var cue))
            {
                cue=ItemAudio.Cue(Key(type));
                cue.SpatialBlend=0f;
                cue.VolumeRandom=0f;
                Cues[type]=cue;
            }
            cue.Volume=cue.VolumeDefault=Mathf.Clamp01(ModConfig.PingSoundVolume.Value);
            cue.VolumeRandomDefault=0f;
            // Play at the listener through the native mixer, so distant team pings remain audible.
            var camera=PingController.ActiveCamera();
            cue.Play(camera!=null?camera.transform.position:Vector3.zero);
        }
        catch(Exception error) {Plugin.Log.LogWarning("[Ping] Audio cue unavailable: "+error.Message);}
    }
}
