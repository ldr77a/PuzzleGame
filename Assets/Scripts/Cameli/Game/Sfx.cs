using System;
using UnityEngine;

namespace Cameli.Game
{
    public enum SfxKind
    {
        /// <summary>Ate a color cell: short pop, pitch varies a little.</summary>
        Eat,

        /// <summary>Passed a gate: two-note chime.</summary>
        Gate,

        /// <summary>Bumped into a wall, edge, or closed gate: low soft thud.</summary>
        Blocked,

        /// <summary>The camellia blooms: rising notes.</summary>
        Bloom,

        /// <summary>Stage clear: chord.</summary>
        Clear,

        /// <summary>Any button: click.</summary>
        Button,

        /// <summary>Undo: short blip that swells in, like a sound played backwards.</summary>
        Undo,
    }

    /// <summary>
    /// Temporary sound effects synthesized with AudioClip.Create, so the prototype needs no audio assets.
    /// Envelopes start and end at zero so nothing clicks, and peaks stay low. Silent while
    /// <see cref="Settings.Sound"/> is off.
    /// </summary>
    static class Sfx
    {
        const int SampleRate = 44100;
        const int Voices = 4;
        const float Volume = 0.8f;

        static AudioClip[] clips;
        static AudioSource[] sources;
        static int nextVoice;

        public static void Play(SfxKind kind)
        {
            if (!Settings.Sound || !Application.isPlaying) return;
            if (sources == null || sources[0] == null) CreateHost();

            AudioSource source = sources[nextVoice];
            nextVoice = (nextVoice + 1) % Voices;
            source.Stop();
            source.clip = clips[(int)kind];
            source.pitch = kind == SfxKind.Eat ? UnityEngine.Random.Range(0.94f, 1.08f) : 1f;
            source.Play();
        }

        /// <summary>
        /// A hidden object that survives scene loads, with a few voices so sounds can overlap. It brings its own
        /// AudioListener when the scene has none (the code-built scenes add none to their camera).
        /// </summary>
        static void CreateHost()
        {
            var host = new GameObject("Cameli Sfx") { hideFlags = HideFlags.HideInHierarchy };
            UnityEngine.Object.DontDestroyOnLoad(host);
            if (UnityEngine.Object.FindAnyObjectByType<AudioListener>() == null) host.AddComponent<AudioListener>();

            sources = new AudioSource[Voices];
            for (int i = 0; i < Voices; i++)
            {
                sources[i] = host.AddComponent<AudioSource>();
                sources[i].playOnAwake = false;
                sources[i].volume = Volume;
            }
            if (clips == null) clips = CreateClips();
        }

        static AudioClip[] CreateClips()
        {
            var made = new AudioClip[Enum.GetValues(typeof(SfxKind)).Length];
            made[(int)SfxKind.Eat] = Clip("Eat", 0.09f, (t, s) => Tone(s, Sweep(t, 0.09f, 820f, 480f)) * Envelope(t, 0.09f, 0.004f, 30f));
            made[(int)SfxKind.Gate] = Clip("Gate", 0.6f, (t, s) => Bell(t, 0f, 659.3f) + Bell(t, 0.13f, 987.8f));
            made[(int)SfxKind.Blocked] = Clip("Blocked", 0.14f, (t, s) => Tone(s, Sweep(t, 0.14f, 120f, 70f)) * Envelope(t, 0.14f, 0.006f, 22f) * 1.3f);
            made[(int)SfxKind.Bloom] = Clip("Bloom", 0.8f, (t, s) =>
                Bell(t, 0f, 523.3f) + Bell(t, 0.09f, 659.3f) + Bell(t, 0.18f, 784f) + Bell(t, 0.27f, 1046.5f));
            made[(int)SfxKind.Clear] = Clip("Clear", 1.1f, (t, s) =>
                (Bell(t, 0f, 523.3f, 3f) + Bell(t, 0.03f, 659.3f, 3f) + Bell(t, 0.06f, 784f, 3f) + Bell(t, 0.09f, 1046.5f, 3f)) * 0.8f);
            made[(int)SfxKind.Button] = Clip("Button", 0.03f, (t, s) => Tone(s, Sweep(t, 0.03f, 1700f, 1300f)) * Envelope(t, 0.03f, 0.002f, 120f) * 0.7f);
            made[(int)SfxKind.Undo] = Clip("Undo", 0.12f, (t, s) => Tone(s, Sweep(t, 0.12f, 420f, 760f)) * Envelope(0.12f - t, 0.12f, 0.008f, 25f));
            return made;
        }

        /// <summary>
        /// Renders a mono clip. <paramref name="wave"/> gets the time and a running phase that <see cref="Tone"/>
        /// advances; the result is scaled so the loudest sample sits at a soft level.
        /// </summary>
        static AudioClip Clip(string name, float seconds, Func<float, Phase, float> wave)
        {
            int length = Mathf.CeilToInt(seconds * SampleRate);
            var samples = new float[length];
            var phase = new Phase();
            float peak = 0f;
            for (int i = 0; i < length; i++)
            {
                samples[i] = wave(i / (float)SampleRate, phase);
                peak = Mathf.Max(peak, Mathf.Abs(samples[i]));
            }
            // Always end on silence, whatever the envelope left.
            int fade = Mathf.Min(length, SampleRate / 200);
            for (int i = 0; i < fade; i++) samples[length - 1 - i] *= i / (float)fade;

            float gain = peak > 0f ? 0.5f / peak : 0f;
            for (int i = 0; i < length; i++) samples[i] *= gain;

            AudioClip clip = AudioClip.Create("Sfx " + name, length, 1, SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        /// <summary>Running phase for gliding tones; a frequency change then never jumps the waveform.</summary>
        sealed class Phase
        {
            public float Cycles;
        }

        /// <summary>Advances the phase by one sample at <paramref name="frequency"/> and returns it.</summary>
        static Phase Advance(Phase phase, float frequency)
        {
            phase.Cycles += frequency / SampleRate;
            return phase;
        }

        /// <summary>Exponential glide from one frequency to another over the clip.</summary>
        static float Sweep(float t, float seconds, float from, float to) => from * Mathf.Pow(to / from, Mathf.Clamp01(t / seconds));

        /// <summary>Soft sine with a touch of the second harmonic, at a gliding frequency.</summary>
        static float Tone(Phase phase, float frequency)
        {
            float cycles = Advance(phase, frequency).Cycles;
            return Mathf.Sin(2f * Mathf.PI * cycles) + 0.15f * Mathf.Sin(4f * Mathf.PI * cycles);
        }

        /// <summary>Linear attack, then exponential decay that is forced to zero at the end.</summary>
        static float Envelope(float t, float seconds, float attack, float decay)
        {
            if (t < 0f || t > seconds) return 0f;
            float rise = Mathf.Clamp01(t / attack);
            float tailOff = Mathf.Clamp01((seconds - t) / 0.01f);
            return rise * Mathf.Exp(-decay * t) * tailOff;
        }

        /// <summary>A struck note starting at <paramref name="start"/>: fundamental plus a faint octave, ringing out.</summary>
        static float Bell(float t, float start, float frequency, float decay = 6f)
        {
            float local = t - start;
            if (local < 0f) return 0f;
            float w = 2f * Mathf.PI * frequency * local;
            float env = Mathf.Clamp01(local / 0.005f) * Mathf.Exp(-decay * local);
            return (Mathf.Sin(w) + 0.25f * Mathf.Sin(2f * w)) * env;
        }
    }
}
