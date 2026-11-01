using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Airships.Game
{
    // Plays the kit's flipbook presets. Each preset owns one particle system in world space, so emitting
    // costs a draw call per preset in use, however many ships are burning.
    public static class Effects
    {
        const float SmokeRotationDegrees = 12f;
        static readonly int SrcBlend = Shader.PropertyToID("_SrcBlend");
        static readonly int DstBlend = Shader.PropertyToID("_DstBlend");
        static readonly int ZTest = Shader.PropertyToID("_ZTest");

        class Entry
        {
            public EffectPresetDef Preset;
            public FlipbookDef Flipbook;
            public ParticleSystem System;
            public int Cap;
        }

        static readonly Dictionary<string, Entry> Entries = new Dictionary<string, Entry>();
        static ArtData data;
        static Transform root;
        static Shader shader;
        static MonoBehaviour runner;
        static bool highDefinition;

        public static ArtData Data
        {
            get
            {
                if (data == null) data = JsonUtility.FromJson<ArtData>(Resources.Load<TextAsset>("Art/art_data").text);
                return data;
            }
        }

        public static void Create(MonoBehaviour owner)
        {
            runner = owner;
            var go = new GameObject("Flipbook Effects");
            go.transform.SetParent(owner.transform, false);
            root = go.transform;
            Entries.Clear();
        }

        // Sheets and particle caps differ by graphics mode, so a change rebuilds the systems.
        public static void SetDefinition(bool high)
        {
            if (highDefinition == high && Entries.Count > 0) return;
            highDefinition = high;
            foreach (var entry in Entries.Values)
            {
                if (entry.System != null) Object.Destroy(entry.System.gameObject);
            }
            Entries.Clear();
        }

        public static void Clear()
        {
            foreach (var entry in Entries.Values)
            {
                if (entry.System != null) entry.System.Clear();
            }
        }

        static Entry Get(string presetId)
        {
            if (Entries.TryGetValue(presetId, out var entry)) return entry;
            var preset = Data.Preset(presetId);
            if (preset == null) throw new KeyNotFoundException($"No effect preset named '{presetId}'");
            entry = new Entry { Preset = preset, Flipbook = Data.Flipbook(preset.flipbook), Cap = highDefinition ? preset.maxHq : preset.maxLq };
            if (entry.Cap > 0) entry.System = Build(entry);
            Entries[presetId] = entry;
            return entry;
        }

        static ParticleSystem Build(Entry entry)
        {
            var preset = entry.Preset;
            var flipbook = entry.Flipbook;
            var variant = highDefinition ? flipbook.hq : flipbook.lq;

            var go = new GameObject(preset.id);
            go.transform.SetParent(root, false);
            var system = go.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = system.main;
            main.loop = false;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Shape;

            // The manifest's cap is per emitter, and several ships can run one preset at once.
            main.maxParticles = Mathf.Max(4, entry.Cap * 6);
            var emission = system.emission;
            emission.enabled = false;
            var shape = system.shape;
            shape.enabled = false;

            var total = Mathf.Max(1, variant.columns * variant.rows);
            if (total > 1)
            {
                var sheet = system.textureSheetAnimation;
                sheet.enabled = true;
                sheet.mode = ParticleSystemAnimationMode.Grid;
                sheet.numTilesX = variant.columns;
                sheet.numTilesY = variant.rows;
                sheet.animation = ParticleSystemAnimationType.WholeSheet;

                // A sheet can hold fewer frames than its grid has cells.
                var share = variant.frames / (float)total;
                sheet.frameOverTime = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0f, 1f, share * 0.999f));
                sheet.cycleCount = flipbook.loop ? Mathf.Max(1, Mathf.RoundToInt(preset.lifetime * flipbook.fps / variant.frames)) : 1;
            }

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sortMode = ParticleSystemSortMode.None;
            renderer.pivot = new Vector3(0f, 0.5f - flipbook.anchorY, 0f);

            if (shader == null) shader = Resources.Load<Shader>("Shaders/Flipbook");
            var material = new Material(shader) { name = preset.id };
            material.mainTexture = Resources.Load<Texture2D>("Art/Vfx/" + System.IO.Path.GetFileNameWithoutExtension(variant.file));
            material.SetFloat(SrcBlend, (float)UnityEngine.Rendering.BlendMode.One);
            material.SetFloat(DstBlend, (float)(flipbook.blend == "additive" ? UnityEngine.Rendering.BlendMode.One : UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha));
            material.SetFloat(ZTest, (float)(preset.depthTest ? UnityEngine.Rendering.CompareFunction.LessEqual : UnityEngine.Rendering.CompareFunction.Always));
            renderer.sharedMaterial = material;
            return system;
        }

        public static EffectPresetDef Preset(string id) => Get(id).Preset;

        // One burst at a point. Scale multiplies the preset's size, to suit big hulls or scaled models.
        public static void Play(string presetId, Vector3 position, float scale = 1f, Vector3 carry = default)
        {
            var entry = Get(presetId);

            // Low definition swaps the big transparent blasts for opaque models, to save on overdraw.
            EmitOrFallback(entry, position, scale, carry);
            if (runner != null && !string.IsNullOrEmpty(entry.Preset.followUp)) runner.StartCoroutine(FollowUp(entry.Preset, position, scale));
        }

        static IEnumerator FollowUp(EffectPresetDef preset, Vector3 position, float scale)
        {
            yield return new WaitForSeconds(preset.lifetime * 0.5f);
            Play(preset.followUp, position, scale);
        }

        static void Emit(Entry entry, Vector3 position, float scale, Vector3 carry)
        {
            if (entry.System == null) return;
            var preset = entry.Preset;
            var count = Mathf.Max(1, Mathf.Min(preset.burst, entry.Cap));
            var parameters = new ParticleSystem.EmitParams();
            var velocity = new Vector3(preset.velocityX, preset.velocityY, preset.velocityZ) + carry;
            var smoke = preset.flipbook.StartsWith("Smoke") || preset.flipbook == "GasLeak" || preset.flipbook == "ToxicSmog";
            for (var i = 0; i < count; i++)
            {
                parameters.position = position + Random.insideUnitSphere * preset.sizeMeters * scale * (count > 1 ? 0.2f : 0f);
                parameters.velocity = velocity;
                parameters.startSize = preset.sizeMeters * scale * Random.Range(0.9f, 1.1f);
                parameters.startLifetime = preset.lifetime;
                parameters.startColor = Color.white;
                parameters.rotation = smoke ? Random.Range(-SmokeRotationDegrees, SmokeRotationDegrees) : Random.Range(0f, 360f);
                entry.System.Emit(parameters, 1);
            }
        }

        // A preset on every socket it names. The first pattern that matches anything wins, and each match fires.
        public static void PlayOn(string presetId, GameObject kit, float scale = 1f)
        {
            foreach (var socket in KitArt.MatchSockets(kit, Get(presetId).Preset.sockets)) Play(presetId, socket.position, scale);
        }

        // A continuous emitter on one socket, such as a fire or a smoke vent.
        public static EffectEmitter Attach(string presetId, Transform socket, float scale = 1f)
        {
            var emitter = socket.gameObject.AddComponent<EffectEmitter>();
            emitter.Setup(presetId, scale);
            return emitter;
        }

        public static List<EffectEmitter> AttachAll(string presetId, GameObject kit, float scale = 1f)
        {
            var emitters = new List<EffectEmitter>();
            foreach (var socket in KitArt.MatchSockets(kit, Get(presetId).Preset.sockets)) emitters.Add(Attach(presetId, socket, scale));
            return emitters;
        }

        internal static void EmitFromEmitter(string presetId, Vector3 position, float scale, Vector3 carry) => EmitOrFallback(Get(presetId), position, scale, carry);

        internal static int Cap(EffectPresetDef preset) => Mathf.Max(1, highDefinition ? preset.maxHq : preset.maxLq);

        static void EmitOrFallback(Entry entry, Vector3 position, float scale, Vector3 carry)
        {
            if (!highDefinition && !string.IsNullOrEmpty(entry.Preset.fallbackMesh)) FallbackPuff.Spawn(entry.Preset, position, scale, root);
            else Emit(entry, position, scale, carry);
        }
    }

    // The kit's opaque stand-in for a flipbook: it swells and spins about its origin, then disappears.
    public class FallbackPuff : MonoBehaviour
    {
        float life;
        float age;
        float size;
        Vector3 velocity;

        public static void Spawn(EffectPresetDef preset, Vector3 position, float scale, Transform parent)
        {
            var kit = KitArt.Spawn(preset.fallbackMesh, parent);
            KitArt.Paint(kit, KitArt.NewMaterial(preset.fallbackMesh, KitArt.Crimson));
            kit.transform.position = position;
            kit.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            var puff = kit.AddComponent<FallbackPuff>();
            puff.life = Mathf.Max(0.2f, preset.lifetime);
            puff.size = preset.sizeMeters * scale * 0.5f;
            puff.velocity = new Vector3(preset.velocityX, preset.velocityY, preset.velocityZ);
            kit.transform.localScale = Vector3.zero;
        }

        void Update()
        {
            age += Time.deltaTime;
            var t = age / life;
            if (t >= 1f)
            {
                Destroy(gameObject);
                return;
            }
            transform.localScale = Vector3.one * size * Mathf.Sin(Mathf.Min(1f, t * 1.3f) * Mathf.PI * 0.75f);
            transform.position += velocity * Time.deltaTime;
            transform.Rotate(0f, 120f * Time.deltaTime, 0f, Space.Self);
        }
    }

    public class EffectEmitter : MonoBehaviour
    {
        string preset;
        float scale;
        float interval;
        float timer;
        Vector3 lastPosition;

        public bool Active { get; set; }

        public void Setup(string presetId, float effectScale)
        {
            preset = presetId;
            scale = effectScale;
            var def = Effects.Preset(presetId);
            var cap = Effects.Cap(def);

            // The manifest caps particles alive at once, so a long life means slower spawning.
            interval = Mathf.Max(def.rate > 0f ? 1f / def.rate : 0.2f, def.lifetime / cap);
            timer = Random.Range(0f, interval);
            lastPosition = transform.position;
        }

        void Update()
        {
            var moved = (transform.position - lastPosition) / Mathf.Max(0.0001f, Time.deltaTime);
            lastPosition = transform.position;
            if (!Active || Time.deltaTime <= 0f) return;
            timer -= Time.deltaTime;
            if (timer > 0f) return;
            timer += interval;

            // Attached effects travel with the ship. Everything else is left behind in the air.
            var attached = Effects.Preset(preset).attached;
            Effects.EmitFromEmitter(preset, transform.position, scale, attached ? moved : moved * 0.25f);
        }
    }
}
