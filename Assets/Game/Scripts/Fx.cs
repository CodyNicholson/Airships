using UnityEngine;

namespace Airships.Game
{
    // One shared particle system for all bursts. Emitting from a single system costs one draw call
    // no matter how many cannons are firing.
    public class Fx : MonoBehaviour
    {
        public static float DensityScale = 1f;
        static Fx instance;

        ParticleSystem system;
        public static Material ParticleMaterial { get; private set; }

        public static void Create(Transform parent)
        {
            var go = new GameObject("Fx");
            go.transform.SetParent(parent, false);
            instance = go.AddComponent<Fx>();
            ParticleMaterial = new Material(Resources.Load<Shader>("Shaders/Particle")) { name = "Particle" };

            var system = go.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = system.main;
            main.loop = false;
            main.playOnAwake = false;
            main.maxParticles = 800;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startColor = Palette.Spark;
            main.scalingMode = ParticleSystemScalingMode.Shape;
            var emission = system.emission;
            emission.enabled = false;
            var shape = system.shape;
            shape.enabled = false;
            var fade = system.colorOverLifetime;
            fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.75f, 0.5f), new GradientAlphaKey(0f, 1f) });
            fade.color = gradient;
            var size = system.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.6f, 1f, 1.35f));
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = ParticleMaterial;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            instance.system = system;
        }

        public static void Burst(Vector3 position, Color color, int count, float speed, float size, float life, float upward = 0f)
        {
            if (instance == null) return;
            count = Mathf.Max(1, Mathf.RoundToInt(count * DensityScale));
            var parameters = new ParticleSystem.EmitParams();
            for (var i = 0; i < count; i++)
            {
                parameters.position = position + Random.insideUnitSphere * size * 0.4f;
                parameters.velocity = Random.insideUnitSphere * speed + Vector3.up * upward;
                parameters.startSize = size * Random.Range(0.65f, 1.3f);
                parameters.startLifetime = life * Random.Range(0.7f, 1.2f);
                parameters.startColor = color;
                parameters.rotation = Random.Range(0f, 360f);
                instance.system.Emit(parameters, 1);
            }
        }

        public static void Clear()
        {
            if (instance != null) instance.system.Clear();
        }

        public static void MuzzleFlash(Vector3 position)
        {
            Burst(position, Palette.Spark, 4, 5f, 1.8f, 0.14f);
            Burst(position, Palette.Smoke, 4, 3f, 2.4f, 0.8f, 1f);
        }

        public static void Hit(Vector3 position, bool heavy)
        {
            Burst(position, Palette.Spark, heavy ? 16 : 8, heavy ? 22f : 14f, heavy ? 1.2f : 0.8f, 0.4f);
            Burst(position, Palette.DarkSmoke, heavy ? 8 : 4, 6f, heavy ? 4f : 2.4f, 0.9f, 2f);
        }

        // A hit at the boarding battle's closer camera distance.
        public static void DeckHit(Vector3 position, bool heavy)
        {
            Burst(position, Palette.Spark, heavy ? 12 : 7, heavy ? 7f : 5f, heavy ? 0.45f : 0.3f, 0.3f);
            Burst(position, Palette.Smoke, heavy ? 4 : 2, 1.5f, heavy ? 0.8f : 0.55f, 0.5f, 1f);
        }

        public static void Explosion(Vector3 position)
        {
            Burst(position, Palette.Spark, 40, 34f, 2.6f, 0.65f);
            Burst(position, Palette.Danger, 24, 22f, 4f, 0.9f);
            Burst(position, Palette.DarkSmoke, 28, 11f, 8f, 1.9f, 4f);
        }

        public static void Puff(Vector3 position, Color color)
        {
            Burst(position, color, 10, 3.2f, 0.55f, 0.55f, 1.4f);
        }
    }
}
