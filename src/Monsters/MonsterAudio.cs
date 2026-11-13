using System;
using System.Collections.Generic;
using UnityEngine;

namespace IncreaseHardMod.Monsters;

/// <summary>
/// Monster cues through the native Sound path (mixer, distance, occlusion).
/// Warnings and animal voices are procedural (see MonsterSynth.cs); the witch laugh, the gorgon's hoofsteps and bellow, the fairy's wings and the twins' laugh and footsteps use user-supplied embedded recordings.
/// </summary>
internal static partial class MonsterAudio
{
    private static readonly Dictionary<Cue, Sound> Sounds = new();

    internal static void Play(Cue cue, Vector3 position, float volume = 1f, float pitch = 1f)
    {
        try
        {
            if (!Sounds.TryGetValue(cue, out var sound))
                Sounds[cue] = sound = cue == Cue.WitchLaugh ? Visuals.ItemAudio.Cue("monster_witch_laugh", .32f)
                    // The fairy's wings: a user-supplied 2.4 s recording with soft ends, replayed with overlap while she flies.
                    : cue == Cue.FairyFlight ? Visuals.ItemAudio.Cue("monster_fairy_flight", .5f)
                    // The bull's bellow: a user-supplied recording, pitched down for the roar before the charge.
                    : cue == Cue.Moo ? Visuals.ItemAudio.Cue("monster_gorgon_moo", .5f)
                    // Hansel and Gretel's mischievous laugh as they seize a player: a user-supplied recording.
                    : cue == Cue.TwinsLaugh ? Visuals.ItemAudio.Cue("monster_twins_laugh", .5f)
                    : new Sound
                {
                    // Hoofsteps: four steps cut from a user-supplied recording of a large animal; the game picks one at random.
                    Sounds = cue is Cue.Hoof or Cue.HoofRun ? HoofClips("monster_gorgon_hoof", 4) :
                             cue == Cue.Gait ? HoofClips("monster_gorgon_gait", 2) :
                             cue == Cue.TwinsStep ? HoofClips("monster_twins_step", 5) : new[] { Make(cue) }, Volume = .5f, VolumeRandom = .05f, Pitch = 1f, PitchRandom = .06f,
                    SpatialBlend = 1f, Doppler = 0f, Type = AudioManager.AudioType.Default,
                };
            sound.Volume = (cue == Cue.WitchLaugh ? .32f : .5f) * volume;
            sound.Pitch = pitch;
            sound.Play(position);
        }
        catch (Exception e) { Plugin.Log.LogWarning($"[Monsters] Sound {cue} failed: {e.Message}"); }
    }

    private static AudioClip[] HoofClips(string key, int count)
    {
        try
        {
            var clips = new AudioClip[count];
            for (int i = 0; i < clips.Length; i++) clips[i] = Visuals.ItemAudio.Load(key + (i + 1));
            return clips;
        }
        catch (Exception e)
        {
            Plugin.Log.LogWarning($"[Monsters] Recording {key} missing ({e.Message}); using a synthesized step.");
            return new[] { Make(key.Contains("twins") ? Cue.Step : Cue.Hoof) };
        }
    }

    private static AudioClip Make(Cue cue)
    {
        var data = Render(cue);
        var clip = AudioClip.Create("IHM monster " + cue, data.Length, 1, SynthRate, false);
        clip.SetData(data, 0);
        return clip;
    }
}
