using System;
using UnityEngine;

namespace Airships.Game
{
    public enum Sfx
    {
        Click, Confirm, Cannon, HeavyCannon, Hit, HeavyHit, Explosion, Sting, Heal, Slash, Denied, WhaleCall,

        // From here on, each sound is a clip in Resources/Sfx with the same name, rendered by the music project.
        ShipRoll, BoostIgnite, NearMiss, ShotWhiz, FlakBurst, MortarBurst, TimberBreak, JellyPop, Raked, WarHorn, DangerSting,
        HullCreak, ShipFlyby, GliderLaunch, HookThrow, HookLand, RopeSnap, ChainTaut, MissileLaunch, Overdrive, SignatureFire,
        Pickup, WhaleGulp, ThunderRumble, LightningCrack, ThunderDistant, VolleyWarning, HullAlarm, Pistol, MagicCast, GuardUp,
        BladeClash, BodyFall, Burn, StatusHit, Stunned, BossRoar, Revive, GrappleSwing, Plunder, CoinPurchase, AmmoLoad, LevelUp,
    }

    // The basic sound effects are synthesized at startup, the rest come from Resources/Sfx, and songs come from Resources/Music. Volumes go through this
    // one gain layer. Unity has no scripting API for creating an AudioMixer, so that upgrade needs the Editor UI.
    public class AudioBank : MonoBehaviour
    {
        const int SampleRate = 22050;
        const int VoiceCount = 16;
        const float SameSoundGapSeconds = 0.04f; // Two copies of a clip in the same instant only sound like a louder, phasey one.
        const float EngineLevel = 0.3f;
        const float EngineFadeSeconds = 0.5f;
        const float JingleFadeSeconds = 0.4f;
        public const Sfx FirstClipSound = Sfx.ShipRoll;
        public const string EngineLoopClip = "EngineLoop";
        const float MusicLevel = 0.6f;
        const float SongFadeSeconds = 0.6f;
        const float DangerFadeSeconds = 1.5f;
        const double ScheduleLeadSeconds = 0.15;
        static AudioBank instance;

        AudioClip[] clips;
        AudioSource[] voices;
        AudioSource wind;
        AudioSource engine;
        float[] lastPlayed;
        float engineGain;
        float engineTarget;

        // A song is an intro and a loop, each with an optional danger layer, all sample-locked together.
        AudioSource baseIntro;
        AudioSource baseLoop;
        AudioSource dangerIntro;
        AudioSource dangerLoop;
        AudioSource jingle;
        string wantedSong;
        string currentSong;
        float songGain;
        float danger;
        float dangerTarget;
        float lastJingleTime = float.NegativeInfinity;
        bool jingleGivesWay;
        float jingleGain = 1f;
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
            clips[(int)Sfx.WhaleCall] = WhaleCall();

            foreach (Sfx sound in Enum.GetValues(typeof(Sfx)))
            {
                if (sound < FirstClipSound) continue;
                clips[(int)sound] = Resources.Load<AudioClip>("Sfx/" + sound);
                if (clips[(int)sound] == null) Debug.LogError($"Missing sound effect '{sound}'");
            }
            lastPlayed = new float[clips.Length];
            for (var i = 0; i < lastPlayed.Length; i++) lastPlayed[i] = float.NegativeInfinity;

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

            engine = gameObject.AddComponent<AudioSource>();
            engine.clip = Resources.Load<AudioClip>("Sfx/" + EngineLoopClip);
            engine.loop = true;
            engine.playOnAwake = false;
            engine.spatialBlend = 0f;
            if (engine.clip != null) engine.Play();
            else Debug.LogError($"Missing sound effect '{EngineLoopClip}'");

            baseIntro = MusicSource(false);
            baseLoop = MusicSource(true);
            dangerIntro = MusicSource(false);
            dangerLoop = MusicSource(true);
            jingle = MusicSource(false);
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
            var level = musicVolume * MusicLevel * songGain;
            baseIntro.volume = level;
            baseLoop.volume = level;
            dangerIntro.volume = level * danger;
            dangerLoop.volume = level * danger;
            jingle.volume = musicVolume * MusicLevel * jingleGain;
            wind.volume = sfxVolume * 0.22f;
            engine.volume = sfxVolume * EngineLevel * engineGain;
        }

        public static void Play(Sfx sfx, float volume = 1f, float pitchVariance = 0.06f)
        {
            if (instance == null) return;
            if (Time.unscaledTime - instance.lastPlayed[(int)sfx] < SameSoundGapSeconds) return;
            instance.lastPlayed[(int)sfx] = Time.unscaledTime;
            var voice = instance.voices[instance.nextVoice];
            instance.nextVoice = (instance.nextVoice + 1) % VoiceCount;
            voice.clip = instance.clips[(int)sfx];
            voice.volume = volume * instance.sfxVolume;
            voice.pitch = 1f + UnityEngine.Random.Range(-pitchVariance, pitchVariance);
            voice.Play();
        }

        // How hard the ship's engines are working, from 0 (silent, off the battlefield) to 1 (full throttle).
        public static void SetEngine(float level)
        {
            if (instance != null) instance.engineTarget = level;
        }

        // Starts a song, after fading out the last one and after any jingle ends. Null fades to silence.
        public static void PlayMusic(string song)
        {
            if (instance != null) instance.wantedSong = song;
        }

        // Fades the current song's danger layer. Songs without one ignore this.
        public static void SetDanger(float level)
        {
            if (instance != null) instance.dangerTarget = level;
        }

        // Plays a one-shot over silence. The song underneath fades out first and returns afterwards.
        // A jingle that gives way fades out as soon as a new song is asked for, so a quick retry isn't held up.
        public static void PlayJingle(string name, bool givesWay = false)
        {
            if (instance == null) return;
            var clip = Resources.Load<AudioClip>("Music/" + name);
            if (clip == null)
            {
                Debug.LogError($"Missing jingle '{name}'");
                return;
            }
            instance.jingle.clip = clip;
            instance.jingleGivesWay = givesWay;
            instance.jingleGain = 1f;
            instance.jingle.Play();
            if (!givesWay) instance.lastJingleTime = Time.unscaledTime;
        }

        public static float SecondsSinceJingle => instance != null ? Time.unscaledTime - instance.lastJingleTime : float.PositiveInfinity;
        public static string CurrentSong => instance != null ? instance.currentSong : null;
        public static bool IsMusicPlaying => instance != null && (instance.baseIntro.isPlaying || instance.baseLoop.isPlaying);
        public static float EffectiveMusicVolume => instance != null ? instance.baseLoop.volume : 0f;
        public static float DangerLevel => instance != null ? instance.danger : 0f;

        // How far apart a song's two layers are, in samples. They should stay locked together.
        public static int LayerDriftSamples => instance != null && instance.baseLoop.isPlaying && instance.dangerLoop.isPlaying && !instance.baseIntro.isPlaying
            ? Mathf.Abs(instance.baseLoop.timeSamples - instance.dangerLoop.timeSamples)
            : 0;
        public static bool IsJinglePlaying => instance != null && instance.jingle.isPlaying;

        void Update()
        {
            if (jingle.isPlaying && jingleGivesWay && wantedSong != null)
            {
                jingleGain = Mathf.MoveTowards(jingleGain, 0f, Time.unscaledDeltaTime / JingleFadeSeconds);
                if (jingleGain <= 0f) jingle.Stop();
            }
            engineGain = Mathf.MoveTowards(engineGain, engineTarget, Time.unscaledDeltaTime / EngineFadeSeconds);
            var target = jingle.isPlaying ? null : wantedSong;
            if (target != currentSong)
            {
                songGain = Mathf.MoveTowards(songGain, 0f, Time.unscaledDeltaTime / SongFadeSeconds);
                if (songGain <= 0f)
                {
                    StopSong();
                    currentSong = target;
                    if (target != null) StartSong(target);
                }
            }
            else
            {
                songGain = Mathf.MoveTowards(songGain, 1f, Time.unscaledDeltaTime / SongFadeSeconds);
            }
            danger = Mathf.MoveTowards(danger, dangerTarget, Time.unscaledDeltaTime / DangerFadeSeconds);
            ApplyVolumes();
        }

        void StartSong(string song)
        {
            var start = AudioSettings.dspTime + ScheduleLeadSeconds;
            var hasBase = Schedule(baseIntro, baseLoop, $"Music/{song}_base", start);
            Schedule(dangerIntro, dangerLoop, $"Music/{song}_danger", start);
            if (!hasBase) Debug.LogError($"Missing song '{song}'");
        }

        // The loop begins exactly as the intro ends, because both are scheduled on the audio clock.
        static bool Schedule(AudioSource intro, AudioSource loop, string path, double start)
        {
            var introClip = Resources.Load<AudioClip>(path + "_intro");
            var loopClip = Resources.Load<AudioClip>(path + "_loop");
            if (loopClip == null) return false;
            var loopStart = start;
            if (introClip != null)
            {
                intro.clip = introClip;
                intro.PlayScheduled(start);
                loopStart += introClip.samples / (double)introClip.frequency;
            }
            loop.clip = loopClip;
            loop.PlayScheduled(loopStart);
            return true;
        }

        void StopSong()
        {
            foreach (var source in new[] { baseIntro, baseLoop, dangerIntro, dangerLoop })
            {
                source.Stop();
                if (source.clip != null) Resources.UnloadAsset(source.clip);
                source.clip = null;
            }
        }

        AudioSource MusicSource(bool loop)
        {
            var source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = loop;
            source.spatialBlend = 0f;
            source.ignoreListenerPause = true;
            return source;
        }

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

        // A long, low moan that rises and falls, so the warning can be heard before it's seen.
        static AudioClip WhaleCall()
        {
            const float seconds = 2.6f;
            var data = new float[(int)(seconds * SampleRate)];
            var phase = 0f;
            for (var i = 0; i < data.Length; i++)
            {
                var t = i / (float)data.Length;
                phase += (70f + 110f * Mathf.Sin(t * Mathf.PI)) * Mathf.PI * 2f / SampleRate;
                var wave = Mathf.Sin(phase) + 0.4f * Mathf.Sin(phase * 2.01f) + 0.2f * Mathf.Sin(phase * 3.02f);
                data[i] = wave * 0.5f * Mathf.Sin(t * Mathf.PI);
            }
            return MakeClip("Whale Call", data);
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
    }
}
