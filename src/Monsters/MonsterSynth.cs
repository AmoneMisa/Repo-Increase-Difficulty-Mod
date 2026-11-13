using System;

namespace IncreaseHardMod.Monsters;

/// <summary>
/// The procedural monster cues as plain samples (engine-free, so tests/MonsterTests can render, check and export
/// them). Animal voices are built the way a throat makes them: a glottal pulse train with a wandering, slightly
/// rough pitch, breath noise, and both shaped by moving formant resonances - not bare sine sweeps. Movement and
/// impact sounds are noise shaped by resonant bands (air, scales, a footfall) instead of flat hiss.
/// </summary>
internal static partial class MonsterAudio
{
    internal enum Cue { Oink, Chomp, Charge, Fireball, Zap, Hiss, Rustle, Bite, Roar, Thud, Stare, Swing, SwingWide, Petrify, Slash, Hurt, Death, WitchLaugh, Giggle, Chime, Step, Stomp, Slither, Flap, Whoosh, BladeDraw, Growl, Snort, Shriek, Hum, Sparkle, Rattle, Spit, Squeeze, Chitter, Hoof, HoofRun, FairyFlight, Moo, Gait, TwinsLaugh, TwinsStep }

    internal const int SynthRate = 44100;

    private static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
    private static float Repeat(float v, float length) => v - MathF.Floor(v / length) * length;
    private static float Smooth(float v) { v = Clamp01(v); return v * v * (3f - 2f * v); }

    /// <summary>Seconds of one cue.</summary>
    internal static float SynthLength(Cue cue) => cue switch
    {
        Cue.Oink => .42f, Cue.Chomp => .3f, Cue.Charge => 1.2f, Cue.Fireball => .7f, Cue.Zap => 1f, Cue.Hiss => .9f,
        Cue.Rustle => .6f, Cue.Bite => .25f, Cue.Roar => 1.1f, Cue.Thud => .5f, Cue.Stare => 2f, Cue.Swing => .3f,
        Cue.SwingWide => .45f, Cue.Petrify => 1.5f, Cue.Slash => .4f, Cue.Hurt => .32f, Cue.Giggle => 1.1f, Cue.Chime => .9f,
        Cue.Step => .14f, Cue.Stomp => .35f, Cue.Slither => .7f, Cue.Flap => .22f, Cue.Whoosh => .8f, Cue.BladeDraw => .55f, Cue.Growl => .9f,
        Cue.Snort => .6f, Cue.Shriek => .7f, Cue.Hum => 1f, Cue.Sparkle => .9f, Cue.Rattle => .85f, Cue.Spit => .5f, Cue.Squeeze => .9f, Cue.Chitter => 1f, Cue.Hoof => .32f, _ => .9f,
    };

    /// <summary>Renders one cue: mono samples in -1..1 at <see cref="SynthRate"/>. Deterministic per cue.</summary>
    internal static float[] Render(Cue cue)
    {
        const int rate = SynthRate;
        float length = SynthLength(cue);
        int n = (int)(rate * length);
        var data = new float[n];
        var rng = new Random((int)cue * 7919 + 13);
        float low = 0f, phase = 0f, noise = 0f;
        // Three resonant bands (formants of a throat, or the colour of air and scales).
        float lo1 = 0f, bd1 = 0f, lo2 = 0f, bd2 = 0f, lo3 = 0f, bd3 = 0f;
        // Two throats: cycles run, last pulse value; one slow random drift of the pitch shared by both.
        var cycles = new float[2]; var lastPulse = new float[2];
        float drift = 0f, grain = 0f, grainTo = 0f, crack = 0f, burstPitch = 1f;
        int burstIndex = -1;
        // Cues built from the models above are levelled to this peak afterwards (0 = keep the samples as they are).
        float level = 0f;

        float Band(ref float lo, ref float bd, float x, float fc, float damp)
        {
            float f = 2f * MathF.Sin(MathF.PI * fc / rate);
            lo += f * bd; bd += f * (x - lo - damp * bd);
            return bd * damp; // unity gain at the centre
        }

        // One sample of a glottal pulse train at f0 (its slope, which is what leaves the mouth): a slow opening
        // and a sharp closure each cycle. jitter: how far the pitch wanders (1 is about 1 %).
        float Pulse(int throat, float f0, float jitter)
        {
            cycles[throat] += f0 * (1f + drift * jitter) / rate;
            float p = cycles[throat] - MathF.Floor(cycles[throat]);
            float g = p < .4f ? .5f * (1f - MathF.Cos(MathF.PI * p / .4f)) : p < .56f ? MathF.Cos(MathF.PI * .5f * (p - .4f) / .16f) : 0f;
            float d = (g - lastPulse[throat]) * rate / (f0 * 10f);
            lastPulse[throat] = g;
            return d;
        }

        // Period doubling (every other cycle weaker): the roughness of a grunt, a growl or a strained cry.
        float Rough(int throat, float amount) => 1f + amount * MathF.Sin(MathF.PI * cycles[throat]);

        for (int i = 0; i < n; i++)
        {
            float t = i / (float)rate, u = t / length;
            noise = (float)(rng.NextDouble() * 2 - 1);
            low += (noise - low) * .08f;
            drift += (noise - drift) * .001f;
            float air = noise - low; // breath: noise without its rumble
            float env = Clamp01(t / .02f) * Clamp01((length - t) / .08f);
            float s;
            switch (cue)
            {
                case Cue.Oink:
                {
                    // A pig's grunt: a low, rough throat that pushes up and sags, nasal, with breath in it.
                    float bend = MathF.Sin(MathF.PI * MathF.Min(1f, u / .75f));
                    float f0 = 105f + 85f * bend * bend - 25f * Smooth((u - .75f) / .25f);
                    float src = Pulse(0, f0, 3f) * Rough(0, .55f) + air * .22f;
                    s = Band(ref lo1, ref bd1, src, 520f + 180f * bend, .2f) + Band(ref lo2, ref bd2, src, 1450f + 300f * bend, .18f) * .55f
                        + Band(ref lo3, ref bd3, src, 2700f, .22f) * .2f + low * .25f * (1f - u);
                    env = Smooth(t / .035f) * Smooth((length - t) / .12f); level = .8f;
                    break;
                }
                case Cue.Chomp:
                {
                    // Eating: two wet crunches - a burst of gritty mid noise with small cracks over a soft jaw thump.
                    float g = 0f, thump = 0f;
                    for (int b = 0; b < 2; b++)
                    {
                        float dt = t - b * .13f;
                        if (dt < 0f) continue;
                        float a = b == 0 ? 1f : .8f;
                        g += a * MathF.Exp(-dt / .03f);
                        thump += a * MathF.Sin(2 * MathF.PI * 120f * dt) * MathF.Exp(-dt / .03f);
                    }
                    crack = crack * .72f + (rng.NextDouble() < .006 ? noise * 3f : 0f);
                    s = (Band(ref lo1, ref bd1, noise, 1400f, .5f) + Band(ref lo2, ref bd2, noise, 3000f, .5f) * .6f + crack * .4f + low * 1.5f) * g + thump * .5f;
                    env = Clamp01((length - t) / .03f); level = .8f;
                    break;
                }
                case Cue.Charge: case Cue.Fireball:
                    // Rising crackling fire: filtered noise with a low hum swelling.
                    phase += 2 * MathF.PI * (70f + 60f * u) / rate;
                    s = low * (cue == Cue.Charge ? .9f + u : 1.4f) + .25f * MathF.Sin(phase) * u; break;
                case Cue.Zap:
                    phase += 2 * MathF.PI * 95f / rate;
                    s = MathF.Sign(MathF.Sin(phase)) * .25f * (rng.NextDouble() < .3 ? 1f : .2f) + noise * .35f; break;
                case Cue.Rustle: s = air * .35f * (.5f + .5f * MathF.Sin(u * 40f)); break;
                case Cue.Hiss: case Cue.Chitter:
                {
                    // Snake breath, not TV static: noise through a resonant band (~4-5.5 kHz) instead of flat white
                    // noise, swelling in and tapering off. The hiss flutters lightly; the chitter is the same breath
                    // chopped ~17 times a second - the dry "ts-ts-ts" of a snake tongue.
                    float fc = cue == Cue.Hiss ? 4000f + 1500f * u : 4600f + 600f * MathF.Sin(u * 9f);
                    float f = 2f * MathF.Sin(MathF.PI * fc / rate);
                    lo1 += f * bd1; bd1 += f * (noise - lo1 - .45f * bd1);
                    if (cue == Cue.Hiss)
                        s = (bd1 * .4f + air * .12f) * Clamp01(u / .1f) * (1f - Smooth((u - .55f) / .45f))
                            * (1f - .3f * (.5f + .5f * MathF.Sin(2 * MathF.PI * 26f * t)));
                    else
                    {
                        float gate = .5f + .5f * MathF.Sin(2 * MathF.PI * (17f + 3f * MathF.Sin(u * 7f)) * t);
                        s = (bd1 * .45f + air * .1f) * gate * gate * gate * Clamp01(u / .15f) * (1f - Smooth((u - .7f) / .3f));
                    }
                    break;
                }
                case Cue.Bite:
                {
                    // Jaws snapping shut: a hard click that rings briefly in the mouth, a small thump, a short wet tail.
                    float click = noise * MathF.Exp(-t / .004f);
                    s = click * .5f + Band(ref lo1, ref bd1, click, 2400f, .12f) * 2.2f + Band(ref lo2, ref bd2, click, 900f, .16f) * 1.8f
                        + MathF.Sin(2 * MathF.PI * 150f * t) * MathF.Exp(-t / .03f) * .6f + Band(ref lo3, ref bd3, noise, 3200f, .5f) * MathF.Exp(-t / .06f) * .25f;
                    env = Clamp01((length - t) / .03f); level = .85f;
                    break;
                }
                case Cue.Slash: s = air * MathF.Exp(-u * 9f) * 1.2f; break;
                case Cue.Roar:
                    phase += 2 * MathF.PI * (60f + 25f * MathF.Sin(u * 3f)) / rate;
                    s = (MathF.Sin(phase) + .6f * MathF.Sin(phase * 1.5f) + .4f * MathF.Sin(phase * 2.7f)) * .35f + low * .6f; break;
                case Cue.Thud:
                    phase += 2 * MathF.PI * (55f - 25f * u) / rate;
                    s = MathF.Sin(phase) * MathF.Exp(-u * 5f) + low * MathF.Exp(-u * 8f); break;
                case Cue.Stare:
                    phase += 2 * MathF.PI * (220f + 330f * u) / rate;
                    s = MathF.Sin(phase) * .25f * u + MathF.Sin(phase * 1.5f) * .1f; break;
                case Cue.Swing: case Cue.SwingWide: s = air * MathF.Sin(u * MathF.PI) * .9f; break;
                case Cue.Petrify:
                    phase += 2 * MathF.PI * 660f / rate;
                    s = MathF.Sin(phase) * .2f * (1f - u) + MathF.Sin(phase * .5f) * .15f + low * .2f * u; break;
                case Cue.Giggle:
                {
                    // Children laughing: a run of short "hee"s, each starting on a puff of breath and falling a little
                    // in pitch; the run slows, sinks and fades like real laughter. A second child joins a beat behind.
                    s = 0f;
                    float beats = t * 6.4f * (1f - .18f * u);
                    int index = (int)beats;
                    if (index != burstIndex) { burstIndex = index; burstPitch = .94f + .12f * (float)rng.NextDouble(); }
                    float src = air * .05f;
                    for (int child = 0; child < 2; child++)
                    {
                        float b = beats - child * .45f;
                        if (b < 0f) continue;
                        float frac = b - MathF.Floor(b);
                        float voiced = Smooth((frac - .1f) / .15f) * Smooth((.92f - frac) / .32f);
                        float f0 = (child == 0 ? 560f : 680f) * (1f - .2f * u) * (1f + .12f * (1f - frac)) * burstPitch;
                        src += (Pulse(child, f0, 2.5f) * voiced + air * MathF.Exp(-frac * 9f) * .45f) * (child == 0 ? 1f : .6f);
                    }
                    s = Band(ref lo1, ref bd1, src, 950f, .22f) + Band(ref lo2, ref bd2, src, 2700f, .16f) * .7f + Band(ref lo3, ref bd3, src, 3600f, .2f) * .3f;
                    s *= 1f - .5f * u;
                    env = Smooth(t / .02f) * Smooth((length - t) / .15f); level = .7f;
                    break;
                }
                case Cue.Chime:
                    // A soft, low fairy bell (balance pass: no longer piercing): two warm partials, quick decay.
                    phase += 2 * MathF.PI * 740f / rate;
                    s = (MathF.Sin(phase) * .22f + MathF.Sin(phase * 2f) * .06f) * MathF.Exp(-u * 6f) * Clamp01(t / .03f); break;
                case Cue.BladeDraw:
                    // Steel leaving a scabbard: a bright scrape rising into a short ring - the naga's swing warning.
                    phase += 2 * MathF.PI * (900f + 900f * u) / rate;
                    s = air * .35f * (1f - u) + MathF.Sin(phase) * .14f * u * MathF.Exp(-(u - .7f) * (u - .7f) * 12f); break;
                case Cue.Growl:
                {
                    // A deep throat rattling: slow, very rough pulses through a big chest, swelling unevenly.
                    float src = Pulse(0, 48f + 8f * MathF.Sin(u * 11f), 5f) * Rough(0, .6f) + low * .5f;
                    s = (Band(ref lo1, ref bd1, src, 320f, .25f) + Band(ref lo2, ref bd2, src, 850f, .22f) * .6f + Band(ref lo3, ref bd3, src, 2200f, .3f) * .15f)
                        * (.75f + .25f * MathF.Sin(u * 23f));
                    env = Smooth(t / .08f) * Smooth((length - t) / .2f); level = .8f;
                    break;
                }
                case Cue.Snort:
                {
                    // A bull's blast of breath: air forced through the nose (nasal bands, the nostrils fluttering at
                    // first) over a short low grunt.
                    float blast = MathF.Exp(-u * 4.2f), flutter = 1f - .55f * MathF.Exp(-u * 3f) * (.5f + .5f * MathF.Sin(2 * MathF.PI * 34f * t));
                    float breath = noise - low * .5f;
                    s = (Band(ref lo1, ref bd1, breath, 450f + 350f * blast, .5f) + Band(ref lo2, ref bd2, breath, 1700f, .45f) * .6f
                         + Band(ref lo3, ref bd3, breath, 3200f, .5f) * .3f) * blast * flutter + Pulse(0, 82f - 28f * u, 3f) * Rough(0, .4f) * .3f * MathF.Exp(-u * 8f);
                    env = Clamp01(t / .012f) * Clamp01((length - t) / .08f); level = .8f;
                    break;
                }
                case Cue.Shriek:
                {
                    // Twins' delighted "got you!": two high voices sliding up, with a breathy edge.
                    float glide = 600f + 500f * MathF.Sin(u * MathF.PI * .5f);
                    phase += 2 * MathF.PI * (glide + 25f * MathF.Sin(u * 60f)) / rate;
                    s = (MathF.Sin(phase) * .28f + MathF.Sin(phase * 1.19f) * .2f + air * .06f) * MathF.Sin(u * MathF.PI); break;
                }
                case Cue.Sparkle:
                {
                    // The fairy eyeing loot: four soft bell notes running upward (a "ta-da" of mischief), warm not piercing.
                    int note = Math.Min(3, (int)(u * 4f));
                    float local = u * 4f - note;
                    phase += 2 * MathF.PI * (660f * MathF.Pow(1.26f, note)) / rate;
                    s = (MathF.Sin(phase) * .22f + MathF.Sin(phase * 2f) * .05f) * MathF.Exp(-local * 3f) * Clamp01(local / .03f); break;
                }
                case Cue.Rattle:
                {
                    // Rattlesnake tail: rapid dry clicks (~45 per second) swelling in, the basilisk's "back off".
                    float click = Repeat(t * 45f, 1f);
                    s = air * MathF.Exp(-click * 14f) * .9f * (.4f + .6f * Clamp01(u * 3f)); break;
                }
                case Cue.Spit:
                    // Venom strike: an explosive hiss that cuts off fast, over a wet gurgle.
                    phase += 2 * MathF.PI * 30f / rate;
                    s = (noise - low * .3f) * 1.1f * MathF.Exp(-u * 7f) + low * .8f * (.5f + .5f * MathF.Sin(phase)) * MathF.Exp(-u * 4f); break;
                case Cue.Squeeze:
                    // Coils tightening: a low strained creak with uneven pressure and scales rubbing.
                    phase += 2 * MathF.PI * (65f + 12f * MathF.Sin(u * 7f)) / rate;
                    s = (Repeat(phase / (2 * MathF.PI), 1f) * 2f - 1f) * .3f * (.5f + .5f * MathF.Abs(MathF.Sin(u * 17f)))
                        + air * .18f * MathF.Sin(u * MathF.PI); break;
                case Cue.Hum:
                    // Witch's levitation hold: a buzzing electric hum with sparse crackles; repeats while it lasts.
                    phase += 2 * MathF.PI * 110f / rate;
                    s = (MathF.Sin(phase) * .2f + MathF.Sign(MathF.Sin(phase * 2f)) * .06f) * (.8f + .2f * MathF.Sin(u * 30f))
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
                        x += noise * a * MathF.Exp(-dt / .0025f);
                        thump += a * MathF.Sin(2 * MathF.PI * 95f * tone * dt) * MathF.Exp(-dt / .045f);
                    }
                    float f1 = 2f * MathF.Sin(MathF.PI * 520f * tone / rate), f2 = 2f * MathF.Sin(MathF.PI * 1350f * tone / rate);
                    lo1 += f1 * bd1; bd1 += f1 * (x - lo1 - .06f * bd1);
                    lo2 += f2 * bd2; bd2 += f2 * (x - lo2 - .09f * bd2);
                    s = (bd1 * .11f + bd2 * .06f + x * .18f + thump * .35f) * 2f;
                    env = 1f; // the click must start at full strength; the default fade-in would soften it
                    break;
                }
                case Cue.Step:
                    // A soft footfall: the thump of weight landing, with a short scuff of the sole on the floor.
                    s = MathF.Sin(2 * MathF.PI * (95f - 35f * u) * t) * MathF.Exp(-t / .022f)
                        + Band(ref lo1, ref bd1, noise, 700f, .6f) * MathF.Exp(-t / .03f) * .9f + air * .08f * MathF.Exp(-t / .05f);
                    env = Clamp01(t / .002f) * Clamp01((length - t) / .03f); level = .75f;
                    break;
                case Cue.Stomp:
                    // Heavy hoof: a low thump with a gritty tail.
                    phase += 2 * MathF.PI * (48f - 18f * u) / rate;
                    s = MathF.Sin(phase) * 1.1f * MathF.Exp(-u * 6f) + low * .7f * MathF.Exp(-u * 9f); break;
                case Cue.Slither:
                {
                    // Scales dragging over the floor: bright, dry noise in uneven grains (not a steady hiss), swelling
                    // and fading with the body's push.
                    if (rng.NextDouble() < 90.0 / rate) grainTo = (float)rng.NextDouble();
                    grain += (grainTo - grain) * .01f;
                    float push = MathF.Sin(u * MathF.PI);
                    s = (Band(ref lo1, ref bd1, noise, 3000f + 1200f * MathF.Sin(u * 5f), .5f) + Band(ref lo2, ref bd2, noise, 6000f, .6f) * .5f)
                        * (.2f + .8f * grain) * MathF.Sqrt(push) * (.6f + .4f * MathF.Sin(u * MathF.PI * 3f));
                    level = .45f;
                    break;
                }
                case Cue.Flap:
                {
                    // One wing beat: air pushed down - a dull, rising-then-falling rush that peaks early, over a soft thump.
                    float beat = MathF.Sin(MathF.PI * MathF.Sqrt(u)); beat *= beat;
                    s = Band(ref lo1, ref bd1, noise, 260f + 900f * beat, .7f) * beat + low * 1.4f * beat * (1f - u);
                    level = .7f;
                    break;
                }
                case Cue.Whoosh:
                {
                    // Something passing through the air: wind whose pitch rises as it nears and drops as it goes.
                    float pass = MathF.Sin(u * MathF.PI);
                    s = (Band(ref lo1, ref bd1, noise, 350f + 1100f * pass * pass, .55f) + air * .1f) * pass;
                    level = .5f;
                    break;
                }
                case Cue.Hurt:
                {
                    // A hurt animal's yelp: the voice jumps up, breaks rough and falls away, breathy - not a beep.
                    float f0 = 230f + 90f * Smooth(u / .1f) - 150f * Smooth((u - .1f) / .9f);
                    float src = Pulse(0, f0, 4f) * Rough(0, .4f) + air * .3f;
                    s = Band(ref lo1, ref bd1, src, 850f - 250f * u, .2f) + Band(ref lo2, ref bd2, src, 1900f - 300f * u, .18f) * .6f
                        + Band(ref lo3, ref bd3, src, 3000f, .22f) * .25f;
                    env = Smooth(t / .015f) * Smooth((length - t) / .12f); level = .75f;
                    break;
                }
                default:
                {
                    // Death: a long falling groan - the pitch and the mouth both sink, the voice roughens and what is
                    // left at the end is mostly breath.
                    float f0 = 190f - 120f * Smooth(u);
                    float src = Pulse(0, f0, 4f) * Rough(0, .3f + .5f * u) * (1f - .6f * u) + air * (.15f + .5f * u);
                    s = Band(ref lo1, ref bd1, src, 700f - 280f * u, .2f) + Band(ref lo2, ref bd2, src, 1500f - 500f * u, .18f) * .6f
                        + Band(ref lo3, ref bd3, src, 2600f, .25f) * .2f;
                    env = Smooth(t / .04f) * Smooth((length - t) / .35f); level = .8f;
                    break;
                }
            }
            data[i] = level > 0f ? s * env : Math.Clamp(s * env, -1f, 1f);
        }
        if (level > 0f)
        {
            float peak = 1e-6f;
            foreach (float v in data) peak = MathF.Max(peak, MathF.Abs(v));
            for (int i = 0; i < n; i++) data[i] *= level / peak;
        }
        return data;
    }
}
