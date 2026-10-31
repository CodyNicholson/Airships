using System;
using UnityEngine;

namespace Airships.Game
{
    public enum Sfx { Click, Confirm, Cannon, HeavyCannon, Hit, HeavyHit, Explosion, Sting, Heal, Slash, Denied }

    // Placeholder audio synthesized at startup, with no asset files. Volumes go through this one gain
    // layer. Unity has no scripting API for creating an AudioMixer, so that upgrade needs the Editor UI.
    public class AudioBank : MonoBehaviour
    {
        const int SampleRate = 22050;
        const int VoiceCount = 10;
        static AudioBank instance;

        AudioClip[] clips;
        AudioSource[] voices;
        AudioSource music;
        AudioSource wind;
        int nextVoice;
        float sfxVolume = 1f;
        float musicVolume = 1f;

        public static void Create(Transform parent)
        {
            var go = new GameObject("Audio");
            go.transform.SetParent(parent, false);
            instance = go.AddComponent<AudioBank>();
            instance.Build();
        }

        void Build()
        {
            var random = new System.Random(4242);
            clips = new AudioClip[Enum.GetValues(typeof(Sfx)).Length];
            clips[(int)Sfx.Click] = Noise("Click", 0.02f, 0.35f, 0.5f, random);
            clips[(int)Sfx.Confirm] = Tones("Confirm", new[] { 659f, 988f }, 0.07f, 0.4f);
            clips[(int)Sfx.Cannon] = Boom("Cannon", 0.32f, 170f, 0.55f, 0.75f, random);
            clips[(int)Sfx.HeavyCannon] = Boom("Heavy Cannon", 0.9f, 80f, 0.8f, 0.95f, random);
            clips[(int)Sfx.Hit] = Noise("Hit", 0.14f, 0.65f, 0.25f, random);
            clips[(int)Sfx.HeavyHit] = Boom("Heavy Hit", 0.42f, 120f, 0.9f, 0.85f, random);
            clips[(int)Sfx.Explosion] = Boom("Explosion", 1.7f, 55f, 0.92f, 1f, random);
            clips[(int)Sfx.Sting] = Tones("Sting", new[] { 523f, 659f, 784f, 1047f }, 0.13f, 0.5f);
            clips[(int)Sfx.Heal] = Tones("Heal", new[] { 784f, 988f, 1175f }, 0.09f, 0.32f);
            clips[(int)Sfx.Slash] = Noise("Slash", 0.13f, 0.5f, 0.75f, random);
            clips[(int)Sfx.Denied] = Tones("Denied", new[] { 196f, 165f }, 0.09f, 0.4f);

            voices = new AudioSource[VoiceCount];
            for (var i = 0; i < VoiceCount; i++)
            {
                voices[i] = gameObject.AddComponent<AudioSource>();
                voices[i].playOnAwake = false;
                voices[i].spatialBlend = 0f;
            }

            wind = gameObject.AddComponent<AudioSource>();
            wind.clip = Wind(random);
            wind.loop = true;
            wind.playOnAwake = false;
            wind.Play();

            music = gameObject.AddComponent<AudioSource>();
            music.clip = Shanty();
            music.loop = true;
            music.playOnAwake = false;
            music.Play();
            ApplyVolumes();
        }

        public static void SetVolumes(float musicLevel, float sfxLevel)
        {
            if (instance == null) return;
            instance.musicVolume = musicLevel;
            instance.sfxVolume = sfxLevel;
            instance.ApplyVolumes();
        }

        void ApplyVolumes()
        {
            music.volume = musicVolume * 0.5f;
            wind.volume = sfxVolume * 0.22f;
        }

        public static void Play(Sfx sfx, float volume = 1f, float pitchVariance = 0.06f)
        {
            if (instance == null) return;
            var voice = instance.voices[instance.nextVoice];
            instance.nextVoice = (instance.nextVoice + 1) % VoiceCount;
            voice.clip = instance.clips[(int)sfx];
            voice.volume = volume * instance.sfxVolume;
            voice.pitch = 1f + UnityEngine.Random.Range(-pitchVariance, pitchVariance);
            voice.Play();
        }

        public static bool IsMusicPlaying => instance != null && instance.music.isPlaying;
        public static float EffectiveMusicVolume => instance != null ? instance.music.volume : 0f;

        static AudioClip MakeClip(string name, float[] data)
        {
            // Short fades at both ends prevent clicks.
            var fade = Mathf.Min(SampleRate / 200, data.Length / 2);
            for (var i = 0; i < fade; i++)
            {
                var gain = i / (float)fade;
                data[i] *= gain;
                data[data.Length - 1 - i] *= gain;
            }
            var clip = AudioClip.Create(name, data.Length, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        static AudioClip Noise(string name, float seconds, float volume, float brightness, System.Random random)
        {
            var data = new float[(int)(seconds * SampleRate)];
            var filtered = 0f;
            for (var i = 0; i < data.Length; i++)
            {
                var t = i / (float)data.Length;
                var white = (float)random.NextDouble() * 2f - 1f;
                filtered += (white - filtered) * brightness;
                data[i] = filtered * volume * Mathf.Pow(1f - t, 2f);
            }
            return MakeClip(name, data);
        }

        static AudioClip Boom(string name, float seconds, float pitch, float volume, float noiseMix, System.Random random)
        {
            var data = new float[(int)(seconds * SampleRate)];
            var filtered = 0f;
            var phase = 0f;
            for (var i = 0; i < data.Length; i++)
            {
                var t = i / (float)data.Length;
                var white = (float)random.NextDouble() * 2f - 1f;
                var cutoff = Mathf.Lerp(0.4f, 0.03f, Mathf.Pow(t, 0.4f));
                filtered += (white - filtered) * cutoff;
                phase += Mathf.Lerp(pitch * 2.2f, pitch * 0.45f, Mathf.Pow(t, 0.3f)) * Mathf.PI * 2f / SampleRate;
                var sample = filtered * noiseMix * 2.4f + Mathf.Sin(phase) * (1f - noiseMix * 0.5f);
                data[i] = Mathf.Clamp(sample * volume * Mathf.Pow(1f - t, 2.4f), -1f, 1f);
            }
            return MakeClip(name, data);
        }

        static AudioClip Tones(string name, float[] frequencies, float noteSeconds, float volume)
        {
            var total = noteSeconds * frequencies.Length + noteSeconds * 2f;
            var data = new float[(int)(total * SampleRate)];
            for (var n = 0; n < frequencies.Length; n++)
            {
                AddNote(data, n * noteSeconds, noteSeconds * 3f, frequencies[n], volume);
            }
            return MakeClip(name, data);
        }

        static void AddNote(float[] data, float start, float seconds, float frequency, float volume)
        {
            var from = (int)(start * SampleRate);
            var count = (int)(seconds * SampleRate);
            for (var i = 0; i < count && from + i < data.Length; i++)
            {
                var t = i / (float)SampleRate;
                var envelope = Mathf.Min(1f, t * 180f) * Mathf.Exp(-3.6f * t / seconds);
                var wave = Mathf.Sin(t * frequency * Mathf.PI * 2f)
                    + 0.35f * Mathf.Sin(t * frequency * Mathf.PI * 4f)
                    + 0.15f * Mathf.Sin(t * frequency * Mathf.PI * 6f);
                data[from + i] += wave * envelope * volume;
            }
        }

        static AudioClip Wind(System.Random random)
        {
            const int seconds = 8;
            var count = seconds * SampleRate;
            var data = new float[count];
            var brown = 0f;
            for (var i = 0; i < count; i++)
            {
                var white = (float)random.NextDouble() * 2f - 1f;
                brown = Mathf.Clamp(brown + white * 0.025f, -1f, 1f);
                var swell = 0.65f + 0.35f * Mathf.Sin(i / (float)count * Mathf.PI * 4f);
                data[i] = brown * swell;
            }

            // Crossfade the ends so the loop has no seam.
            var overlap = SampleRate / 2;
            for (var i = 0; i < overlap; i++)
            {
                var mix = i / (float)overlap;
                data[i] = Mathf.Lerp(data[count - overlap + i], data[i], mix);
            }
            return MakeClip("Wind", data);
        }

        // A short original shanty-style loop in D minor.
        static AudioClip Shanty()
        {
            const float beat = 60f / 112f;
            const int bars = 8;
            const int beatsPerBar = 4;
            var seconds = beat * beatsPerBar * bars;
            var data = new float[(int)(seconds * SampleRate)];

            float[][] chords =
            {
                new[] { 146.83f, 220f, 293.66f }, new[] { 146.83f, 220f, 293.66f },
                new[] { 130.81f, 196f, 261.63f }, new[] { 110f, 220f, 277.18f },
                new[] { 146.83f, 220f, 293.66f }, new[] { 116.54f, 174.61f, 233.08f },
                new[] { 130.81f, 196f, 261.63f }, new[] { 110f, 164.81f, 220f },
            };
            float[,] melody =
            {
                { 0f, 587.33f, 1f }, { 1f, 698.46f, 0.5f }, { 1.5f, 659.25f, 0.5f }, { 2f, 587.33f, 1f }, { 3f, 440f, 1f },
                { 4f, 587.33f, 1f }, { 5f, 698.46f, 0.5f }, { 5.5f, 783.99f, 0.5f }, { 6f, 880f, 2f },
                { 8f, 783.99f, 1f }, { 9f, 659.25f, 0.5f }, { 9.5f, 523.25f, 0.5f }, { 10f, 659.25f, 1f }, { 11f, 783.99f, 1f },
                { 12f, 880f, 1f }, { 13f, 783.99f, 0.5f }, { 13.5f, 698.46f, 0.5f }, { 14f, 659.25f, 2f },
                { 16f, 587.33f, 1f }, { 17f, 698.46f, 0.5f }, { 17.5f, 659.25f, 0.5f }, { 18f, 587.33f, 1f }, { 19f, 440f, 1f },
                { 20f, 466.16f, 1f }, { 21f, 587.33f, 0.5f }, { 21.5f, 698.46f, 0.5f }, { 22f, 932.33f, 2f },
                { 24f, 783.99f, 1f }, { 25f, 659.25f, 0.5f }, { 25.5f, 587.33f, 0.5f }, { 26f, 523.25f, 1f }, { 27f, 659.25f, 1f },
                { 28f, 554.37f, 2f }, { 30f, 440f, 2f },
            };

            for (var bar = 0; bar < bars; bar++)
            {
                var chord = chords[bar];
                var barStart = bar * beatsPerBar * beat;
                AddNote(data, barStart, beat * 2f, chord[0] * 0.5f, 0.16f);
                AddNote(data, barStart + beat * 2f, beat * 2f, chord[0] * 0.5f, 0.13f);
                for (var b = 1; b < beatsPerBar; b += 2)
                {
                    foreach (var note in chord) AddNote(data, barStart + b * beat, beat * 0.9f, note, 0.055f);
                }
            }
            for (var n = 0; n < melody.GetLength(0); n++)
            {
                AddNote(data, melody[n, 0] * beat, Mathf.Max(0.3f, melody[n, 2] * beat * 1.3f), melody[n, 1], 0.1f);
            }
            for (var i = 0; i < data.Length; i++) data[i] = Mathf.Clamp(data[i], -1f, 1f);
            return MakeClip("Shanty", data);
        }
    }
}
