using System;
using System.Collections.Generic;
using UnityEngine;

namespace IncreaseHardMod.Monsters;

/// <summary>
/// Procedural warning sounds for the new monsters, played through the game's own <see cref="Sound"/> path (game
/// mixer, distance, occlusion). Generated once from code, so nothing proprietary ships in the package.
/// </summary>
internal static class MonsterAudio
{
    internal enum Cue { Oink, Chomp, Charge, Fireball, Zap, Hiss, Rustle, Bite, Roar, Thud, Stare, Swing, SwingWide, Petrify, Slash, Hurt, Death }

    private static readonly Dictionary<Cue, Sound> Sounds = new();

    internal static void Play(Cue cue, Vector3 position, float volume = 1f)
    {
        try
        {
            if (!Sounds.TryGetValue(cue, out var sound))
                Sounds[cue] = sound = new Sound
                {
                    Sounds = new[] { Make(cue) }, Volume = .5f, VolumeRandom = .05f, Pitch = 1f, PitchRandom = .06f,
                    SpatialBlend = 1f, Doppler = 0f, Type = AudioManager.AudioType.Default,
                };
            sound.Volume = .5f * volume;
            sound.Play(position);
        }
        catch (Exception e) { Plugin.Log.LogWarning($"[Monsters] Sound {cue} failed: {e.Message}"); }
    }

    private static AudioClip Make(Cue cue)
    {
        const int rate = 44100;
        float length = cue switch
        {
            Cue.Oink => .35f, Cue.Chomp => .25f, Cue.Charge => 1.2f, Cue.Fireball => .7f, Cue.Zap => 1f, Cue.Hiss => .9f,
            Cue.Rustle => .6f, Cue.Bite => .25f, Cue.Roar => 1.1f, Cue.Thud => .5f, Cue.Stare => 2f, Cue.Swing => .3f,
            Cue.SwingWide => .45f, Cue.Petrify => 1.5f, Cue.Slash => .4f, Cue.Hurt => .3f, _ => .9f,
        };
        int n = (int)(rate * length);
        var data = new float[n];
        var rng = new System.Random((int)cue * 7919 + 13);
        float low = 0f, phase = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)rate, u = t / length;
            float noise = (float)(rng.NextDouble() * 2 - 1);
            low += (noise - low) * .08f;
            float env = Mathf.Clamp01(t / .02f) * Mathf.Clamp01((length - t) / .08f);
            float s;
            switch (cue)
            {
                case Cue.Oink:
                    phase += 2 * Mathf.PI * (180f + 120f * Mathf.Sin(u * 9f)) / rate;
                    s = (Mathf.Sin(phase) + .5f * Mathf.Sin(phase * 2.01f)) * .45f + low * .3f; break;
                case Cue.Chomp: s = low * 1.6f * Mathf.Exp(-u * 6f); break;
                case Cue.Charge: case Cue.Fireball:
                    // Rising crackling fire: filtered noise with a low hum swelling.
                    phase += 2 * Mathf.PI * (70f + 60f * u) / rate;
                    s = low * (cue == Cue.Charge ? .9f + u : 1.4f) + .25f * Mathf.Sin(phase) * u; break;
                case Cue.Zap:
                    phase += 2 * Mathf.PI * 95f / rate;
                    s = Mathf.Sign(Mathf.Sin(phase)) * .25f * (rng.NextDouble() < .3 ? 1f : .2f) + noise * .35f; break;
                case Cue.Hiss: case Cue.Rustle: s = (noise - low) * (cue == Cue.Hiss ? .7f : .35f) * (cue == Cue.Rustle ? (.5f + .5f * Mathf.Sin(u * 40f)) : 1f); break;
                case Cue.Bite: case Cue.Slash: s = (noise - low) * Mathf.Exp(-u * 9f) * 1.2f; break;
                case Cue.Roar:
                    phase += 2 * Mathf.PI * (60f + 25f * Mathf.Sin(u * 3f)) / rate;
                    s = (Mathf.Sin(phase) + .6f * Mathf.Sin(phase * 1.5f) + .4f * Mathf.Sin(phase * 2.7f)) * .35f + low * .6f; break;
                case Cue.Thud:
                    phase += 2 * Mathf.PI * (55f - 25f * u) / rate;
                    s = Mathf.Sin(phase) * Mathf.Exp(-u * 5f) + low * Mathf.Exp(-u * 8f); break;
                case Cue.Stare:
                    phase += 2 * Mathf.PI * (220f + 330f * u) / rate;
                    s = Mathf.Sin(phase) * .25f * u + Mathf.Sin(phase * 1.5f) * .1f; break;
                case Cue.Swing: case Cue.SwingWide: s = (noise - low) * Mathf.Sin(u * Mathf.PI) * .9f; break;
                case Cue.Petrify:
                    phase += 2 * Mathf.PI * 660f / rate;
                    s = Mathf.Sin(phase) * .2f * (1f - u) + Mathf.Sin(phase * .5f) * .15f + low * .2f * u; break;
                case Cue.Hurt:
                    phase += 2 * Mathf.PI * (320f - 150f * u) / rate;
                    s = Mathf.Sin(phase) * .4f + noise * .15f; break;
                default:
                    phase += 2 * Mathf.PI * (240f - 180f * u) / rate;
                    s = (Mathf.Sin(phase) * .5f + low * .5f) * (1f - u); break;
            }
            data[i] = Mathf.Clamp(s * env, -1f, 1f);
        }
        var clip = AudioClip.Create("IHM monster " + cue, n, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }
}
