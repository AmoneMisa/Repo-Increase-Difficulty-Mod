using System;
using System.Collections.Generic;
using UnityEngine;

namespace IncreaseHardMod.Monsters;

/// <summary>
/// Monster cues through the native Sound path (mixer, distance, occlusion).
/// Warnings are procedural; the witch laugh, the gorgon's hoofsteps and bellow, the fairy's wings and the twins' laugh and footsteps use user-supplied embedded recordings.
/// </summary>
internal static class MonsterAudio
{
    internal enum Cue { Oink, Chomp, Charge, Fireball, Zap, Hiss, Rustle, Bite, Roar, Thud, Stare, Swing, SwingWide, Petrify, Slash, Hurt, Death, WitchLaugh, Giggle, Chime, Step, Stomp, Slither, Flap, Whoosh, BladeDraw, Growl, Snort, Shriek, Hum, Sparkle, Rattle, Spit, Squeeze, Chitter, Hoof, HoofRun, FairyFlight, Moo, Gait, TwinsLaugh, TwinsStep }

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
        const int rate = 44100;
        float length = cue switch
        {
            Cue.Oink => .35f, Cue.Chomp => .25f, Cue.Charge => 1.2f, Cue.Fireball => .7f, Cue.Zap => 1f, Cue.Hiss => .9f,
            Cue.Rustle => .6f, Cue.Bite => .25f, Cue.Roar => 1.1f, Cue.Thud => .5f, Cue.Stare => 2f, Cue.Swing => .3f,
            Cue.SwingWide => .45f, Cue.Petrify => 1.5f, Cue.Slash => .4f, Cue.Hurt => .3f, Cue.Giggle => 1.1f, Cue.Chime => .9f,
            Cue.Step => .14f, Cue.Stomp => .35f, Cue.Slither => .7f, Cue.Flap => .22f, Cue.Whoosh => .8f, Cue.BladeDraw => .55f, Cue.Growl => .9f,
            Cue.Snort => .6f, Cue.Shriek => .7f, Cue.Hum => 1f, Cue.Sparkle => .9f, Cue.Rattle => .85f, Cue.Spit => .5f, Cue.Squeeze => .9f, Cue.Chitter => 1f, Cue.Hoof => .32f, _ => .9f,
        };
        int n = (int)(rate * length);
        var data = new float[n];
        var rng = new System.Random((int)cue * 7919 + 13);
        float low = 0f, phase = 0f, svLow = 0f, svBand = 0f, svLow2 = 0f, svBand2 = 0f;
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
                case Cue.Rustle: s = (noise - low) * .35f * (.5f + .5f * Mathf.Sin(u * 40f)); break;
                case Cue.Hiss: case Cue.Chitter:
                {
                    // Snake breath, not TV static: noise through a resonant band (~4-5.5 kHz) instead of flat white
                    // noise, swelling in and tapering off. The hiss flutters lightly; the chitter is the same breath
                    // chopped ~17 times a second - the dry "ts-ts-ts" of a snake tongue.
                    float fc = cue == Cue.Hiss ? 4000f + 1500f * u : 4600f + 600f * Mathf.Sin(u * 9f);
                    float f = 2f * Mathf.Sin(Mathf.PI * fc / rate);
                    svLow += f * svBand; svBand += f * (noise - svLow - .45f * svBand);
                    if (cue == Cue.Hiss)
                        s = (svBand * .4f + (noise - low) * .12f) * Mathf.Clamp01(u / .1f) * (1f - Mathf.SmoothStep(0f, 1f, (u - .55f) / .45f))
                            * (1f - .3f * (.5f + .5f * Mathf.Sin(2 * Mathf.PI * 26f * t)));
                    else
                    {
                        float gate = .5f + .5f * Mathf.Sin(2 * Mathf.PI * (17f + 3f * Mathf.Sin(u * 7f)) * t);
                        s = (svBand * .45f + (noise - low) * .1f) * gate * gate * gate * Mathf.Clamp01(u / .15f)
                            * (1f - Mathf.SmoothStep(0f, 1f, (u - .7f) / .3f));
                    }
                    break;
                }
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
                case Cue.Giggle:
                {
                    // Two children's voices: quick rising "hee" bursts, the second a third above the first.
                    float burst = Mathf.Repeat(u * 7f, 1f), gate = Mathf.Sin(burst * Mathf.PI);
                    phase += 2 * Mathf.PI * (520f + 160f * burst + 60f * Mathf.Sin(u * 5f)) / rate;
                    s = (Mathf.Sin(phase) * .3f + Mathf.Sin(phase * 1.26f) * .22f + Mathf.Sin(phase * 2f) * .08f) * gate * gate; break;
                }
                case Cue.Chime:
                    // A soft, low fairy bell (balance pass: no longer piercing): two warm partials, quick decay.
                    phase += 2 * Mathf.PI * 740f / rate;
                    s = (Mathf.Sin(phase) * .22f + Mathf.Sin(phase * 2f) * .06f) * Mathf.Exp(-u * 6f) * Mathf.Clamp01(t / .03f); break;
                case Cue.BladeDraw:
                    // Steel leaving a scabbard: a bright scrape rising into a short ring - the naga's swing warning.
                    phase += 2 * Mathf.PI * (900f + 900f * u) / rate;
                    s = (noise - low) * .35f * (1f - u) + Mathf.Sin(phase) * .14f * u * Mathf.Exp(-(u - .7f) * (u - .7f) * 12f); break;
                case Cue.Growl:
                    phase += 2 * Mathf.PI * (42f + 10f * Mathf.Sin(u * 11f)) / rate;
                    s = (Mathf.Sin(phase) * .6f + Mathf.Sin(phase * 2.03f) * .25f) * (.6f + .4f * Mathf.Sin(u * 23f)) + low * .5f; break;
                case Cue.Snort:
                    // A bull's blast of breath before the charge: a breathy noise burst over a low nasal grunt.
                    phase += 2 * Mathf.PI * (85f - 30f * u) / rate;
                    s = (noise - low * .5f) * .6f * Mathf.Exp(-u * 4f) + Mathf.Sin(phase) * .35f * Mathf.Exp(-u * 7f); break;
                case Cue.Shriek:
                {
                    // Twins' delighted "got you!": two high voices sliding up, with a breathy edge.
                    float glide = 600f + 500f * Mathf.Sin(u * Mathf.PI * .5f);
                    phase += 2 * Mathf.PI * (glide + 25f * Mathf.Sin(u * 60f)) / rate;
                    s = (Mathf.Sin(phase) * .28f + Mathf.Sin(phase * 1.19f) * .2f + (noise - low) * .06f) * Mathf.Sin(u * Mathf.PI); break;
                }
                case Cue.Sparkle:
                {
                    // The fairy eyeing loot: four soft bell notes running upward (a "ta-da" of mischief), warm not piercing.
                    int note = Mathf.Min(3, (int)(u * 4f));
                    float local = u * 4f - note;
                    phase += 2 * Mathf.PI * (660f * Mathf.Pow(1.26f, note)) / rate;
                    s = (Mathf.Sin(phase) * .22f + Mathf.Sin(phase * 2f) * .05f) * Mathf.Exp(-local * 3f) * Mathf.Clamp01(local / .03f); break;
                }
                case Cue.Rattle:
                {
                    // Rattlesnake tail: rapid dry clicks (~45 per second) swelling in, the basilisk's "back off".
                    float click = Mathf.Repeat(t * 45f, 1f);
                    s = (noise - low) * Mathf.Exp(-click * 14f) * .9f * (.4f + .6f * Mathf.Clamp01(u * 3f)); break;
                }
                case Cue.Spit:
                    // Venom strike: an explosive hiss that cuts off fast, over a wet gurgle.
                    phase += 2 * Mathf.PI * 30f / rate;
                    s = (noise - low * .3f) * 1.1f * Mathf.Exp(-u * 7f) + low * .8f * (.5f + .5f * Mathf.Sin(phase)) * Mathf.Exp(-u * 4f); break;
                case Cue.Squeeze:
                    // Coils tightening: a low strained creak with uneven pressure and scales rubbing.
                    phase += 2 * Mathf.PI * (65f + 12f * Mathf.Sin(u * 7f)) / rate;
                    s = (Mathf.Repeat(phase / (2 * Mathf.PI), 1f) * 2f - 1f) * .3f * (.5f + .5f * Mathf.Abs(Mathf.Sin(u * 17f)))
                        + (noise - low) * .18f * Mathf.Sin(u * Mathf.PI); break;
                case Cue.Hum:
                    // Witch's levitation hold: a buzzing electric hum with sparse crackles; repeats while it lasts.
                    phase += 2 * Mathf.PI * 110f / rate;
                    s = (Mathf.Sin(phase) * .2f + Mathf.Sign(Mathf.Sin(phase * 2f)) * .06f) * (.8f + .2f * Mathf.Sin(u * 30f))
                        + (rng.NextDouble() < .02 ? noise * .5f : 0f); break;
                case Cue.Hoof:
                {
                    // A cloven hoof on a hard floor: "clip-clop" - a hard click of horn that rings two hollow knock
                    // resonances (~520 and ~1350 Hz), a small body thump under it, then the second hoof softer and lower.
                    float x = 0f, thump = 0f, tone = 1f;
                    for (int h = 0; h < 2; h++)
                    {
                        float dt = t - (h == 0 ? 0f : .105f);
                        if (dt < 0f) continue;
                        float a = h == 0 ? 1f : .55f; tone = h == 0 ? 1f : .88f;
                        x += noise * a * Mathf.Exp(-dt / .0025f);
                        thump += a * Mathf.Sin(2 * Mathf.PI * 95f * tone * dt) * Mathf.Exp(-dt / .045f);
                    }
                    float f1 = 2f * Mathf.Sin(Mathf.PI * 520f * tone / rate), f2 = 2f * Mathf.Sin(Mathf.PI * 1350f * tone / rate);
                    svLow += f1 * svBand; svBand += f1 * (x - svLow - .06f * svBand);
                    svLow2 += f2 * svBand2; svBand2 += f2 * (x - svLow2 - .09f * svBand2);
                    s = (svBand * .11f + svBand2 * .06f + x * .18f + thump * .35f) * 2f;
                    env = 1f; // the click must start at full strength; the default fade-in would soften it
                    break;
                }
                case Cue.Step: s = low * 1.4f * Mathf.Exp(-u * 10f); break;
                case Cue.Stomp:
                    // Heavy hoof: a low thump with a gritty tail.
                    phase += 2 * Mathf.PI * (48f - 18f * u) / rate;
                    s = Mathf.Sin(phase) * 1.1f * Mathf.Exp(-u * 6f) + low * .7f * Mathf.Exp(-u * 9f); break;
                case Cue.Slither: s = (noise - low) * .4f * (.55f + .45f * Mathf.Sin(u * Mathf.PI * 3f)) * Mathf.Sin(u * Mathf.PI); break;
                case Cue.Flap: s = low * 1.5f * Mathf.Sin(u * Mathf.PI) * (1f - u); break;
                case Cue.Whoosh:
                    phase += 2 * Mathf.PI * (110f + 40f * Mathf.Sin(u * 6f)) / rate;
                    s = (noise - low) * .35f * Mathf.Sin(u * Mathf.PI) + Mathf.Sin(phase) * .06f; break;
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
