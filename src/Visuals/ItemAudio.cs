using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace IncreaseHardMod.Visuals;

internal static class ItemAudio
{
    private static readonly Dictionary<string, AudioClip> Clips = new();

    private static AudioClip Load(string key)
    {
        if (Clips.TryGetValue(key, out var cached)) return cached;
        var asm = Assembly.GetExecutingAssembly();
        foreach (var resource in asm.GetManifestResourceNames())
        {
            if (!resource.EndsWith("." + key + ".wav", StringComparison.Ordinal)) continue;
            using var stream = asm.GetManifestResourceStream(resource);
            using var reader = new BinaryReader(stream);
            // The checked-in generator produces canonical PCM16 mono RIFF files.
            var bytes = reader.ReadBytes((int)stream.Length);
            if (bytes.Length < 44 || System.Text.Encoding.ASCII.GetString(bytes, 0, 4) != "RIFF" ||
                System.Text.Encoding.ASCII.GetString(bytes, 8, 8) != "WAVEfmt " ||
                BitConverter.ToInt32(bytes, 16) != 16 || BitConverter.ToInt16(bytes, 20) != 1 ||
                BitConverter.ToInt16(bytes, 22) != 1 || BitConverter.ToInt16(bytes, 34) != 16 ||
                System.Text.Encoding.ASCII.GetString(bytes, 36, 4) != "data" ||
                BitConverter.ToInt32(bytes, 40) != bytes.Length - 44)
                throw new InvalidDataException("Unsupported audio format: " + key);
            int count = (bytes.Length - 44) / 2;
            var samples = new float[count];
            for (int i = 0; i < count; i++) samples[i] = BitConverter.ToInt16(bytes, 44 + i * 2) / 32768f;
            var clip = AudioClip.Create("IHM " + key, count, 1, BitConverter.ToInt32(bytes, 24), false);
            clip.SetData(samples, 0);
            return Clips[key] = clip;
        }
        throw new FileNotFoundException("Missing audio: " + key);
    }

    internal static Sound Cue(string key, float volume = .45f)
    {
        return new Sound
        {
            Sounds = new[] { Load(key) }, Volume = volume, VolumeRandom = .02f,
            Pitch = 1f, PitchRandom = .035f, SpatialBlend = 1f, Doppler = 0f,
            Type = AudioManager.AudioType.Default
        };
    }

    internal static void Attach(GameObject go, string key)
    {
        if (go.GetComponent<ItemAudioState>() != null) return;
        go.AddComponent<ItemAudioState>().assetKey = key;
    }
}

/// <summary>Uses the game's Sound path: mixer volume, distance, occlusion and Silencer patches all apply.</summary>
internal sealed class ItemAudioState : MonoBehaviour
{
    public string assetKey;
    private ItemDrone _drone;
    private ItemOrb _orb;
    private ItemGrenade _grenade;
    private Sound _on, _off;
    private bool _active;
    private float _nextBeacon;

    private void Start()
    {
        try
        {
            var gun = GetComponent<ItemGun>();
            if (gun != null)
            {
                // ShootRPC already plays locally on each peer. Replace, don't layer another gunshot.
                var shot = ItemAudio.Cue(assetKey + "_shot", assetKey == "sticky" ? .24f : .65f);
                var hit = ItemAudio.Cue(assetKey == "sticky" ? "sticky_hit" : assetKey == "ice" ? "ice_hit" : "poison_hit",
                    assetKey == "sticky" ? .12f : .3f);
                gun.soundShoot = shot;
                gun.soundShootGlobal = new Sound { Sounds = Array.Empty<AudioClip>() };
                gun.soundHit = hit;
                enabled = false;
                return;
            }
            _drone = GetComponent<ItemDrone>();
            _orb = GetComponent<ItemOrb>();
            _grenade = GetComponent<ItemGrenade>();
            _on = ItemAudio.Cue(assetKey + "_activate", assetKey == "silencer" ? .14f : .35f);
            _off = ItemAudio.Cue("power_down", .18f);
            // Decoy's native ticking is replaced with its audible locator chirp.
            if (_grenade != null)
            {
                // PlayLoop needs a clip even when muted; preserve the native initialized source.
                _grenade.soundTick.Volume = 0f;
                _grenade.soundTick.VolumeRandom = 0f;
                _grenade.soundTick.VolumeDefault = 0f;
                _grenade.soundTick.VolumeRandomDefault = 0f;
            }
        }
        catch (Exception e)
        {
            Plugin.Log.LogWarning($"[Audio] Keeping native sounds for {gameObject.name}: {e.Message}");
            enabled = false;
        }
    }

    private void OnEnable() { _active = false; _nextBeacon = 0f; }

    private void Update()
    {
        bool active = (_drone != null && _drone.itemActivated) ||
            (_orb != null && _orb.currentState == ItemOrb.State.Active) ||
            (_grenade != null && _grenade.isActive);
        if (active && !_active) { _on.Play(transform.position); _nextBeacon = Time.time + .8f; }
        if (!active && _active) _off.Play(transform.position);
        if (active && _grenade != null && Time.time >= _nextBeacon)
        { _on.Play(transform.position); _nextBeacon = Time.time + .8f; }
        _active = active;
    }
}
