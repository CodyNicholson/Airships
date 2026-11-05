using UnityEngine;

namespace Airships.Game
{
    // Flickering electric bolts: the arcs crawling over a jellyfish, and over a ship it has shocked.
    // A handful of jagged bolt models are shared, and each cluster re-deals them every few frames.
    public class SparkCluster
    {
        const int BoltVariants = 5;
        const int BoltSegments = 6;
        const float BoltJitter = 0.14f;
        const float FlickerMin = 0.04f;
        const float FlickerMax = 0.09f;

        static readonly Mesh[] BoltMeshes = new Mesh[BoltVariants];
        static Material boltMaterial;

        readonly MeshFilter[] bolts;
        float timer;
        bool shown = true;

        public SparkCluster(Transform parent, int count)
        {
            if (boltMaterial == null)
            {
                boltMaterial = Models.NewLitMaterial("Spark Bolts");
                boltMaterial.SetColor("_Emission", new Color(0.55f, 0.9f, 1f) * 1.6f);
            }
            for (var i = 0; i < BoltVariants; i++)
            {
                if (BoltMeshes[i] == null) BoltMeshes[i] = BuildBolt(300 + i);
            }

            var holder = new GameObject("Sparks").transform;
            holder.SetParent(parent, false);
            bolts = new MeshFilter[count];
            for (var i = 0; i < count; i++)
            {
                var bolt = Models.Attach("Bolt", holder, BoltMeshes[i % BoltVariants], boltMaterial, false);
                bolts[i] = bolt.GetComponent<MeshFilter>();
            }
            Hide();
        }

        // A jagged bolt one unit long down +Z, with a short fork off its middle.
        static Mesh BuildBolt(int seed)
        {
            var random = new System.Random(seed);
            float Jitter() => ((float)random.NextDouble() * 2f - 1f) * BoltJitter;
            var kit = new MeshKit();
            var core = Color.Lerp(Palette.Storm, Color.white, 0.75f);
            var from = Vector3.zero;
            var fork = Vector3.zero;
            for (var i = 1; i <= BoltSegments; i++)
            {
                var along = (float)i / BoltSegments;
                var to = i == BoltSegments ? new Vector3(Jitter() * 0.5f, Jitter() * 0.5f, 1f) : new Vector3(Jitter(), Jitter(), along);
                kit.Cylinder(from, to, Mathf.Lerp(0.05f, 0.015f, along - 1f / BoltSegments), Mathf.Lerp(0.05f, 0.015f, along), 4, core);
                if (i == BoltSegments / 2) fork = to;
                from = to;
            }
            var forkEnd = fork + new Vector3(Jitter() * 2.5f, Jitter() * 2.5f, 0.3f);
            var forkMid = Vector3.Lerp(fork, forkEnd, 0.5f) + new Vector3(Jitter(), Jitter(), 0f);
            kit.Cylinder(fork, forkMid, 0.03f, 0.02f, 4, core);
            kit.Cylinder(forkMid, forkEnd, 0.02f, 0.01f, 4, core);
            return kit.Build("Spark Bolt");
        }

        // Re-deals the bolts every few frames: the first few always show and the rest by chance, each
        // starting on an ellipsoid around center (in the parent's space) and reaching outward.
        public void Update(Vector3 center, Vector3 radii, float minLength, float maxLength, float chance, int alwaysShown = 0)
        {
            timer -= Time.deltaTime;
            if (timer > 0f) return;
            timer = Random.Range(FlickerMin, FlickerMax);
            shown = true;
            for (var i = 0; i < bolts.Length; i++)
            {
                var bolt = bolts[i];
                var on = i < alwaysShown || Random.value < chance;
                bolt.gameObject.SetActive(on);
                if (!on) continue;
                var direction = Random.onUnitSphere;
                var transform = bolt.transform;
                transform.localPosition = center + Vector3.Scale(direction, radii);
                transform.localRotation = Quaternion.LookRotation(direction + Random.insideUnitSphere * 0.6f) * Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
                transform.localScale = Vector3.one * Random.Range(minLength, maxLength);
                bolt.sharedMesh = BoltMeshes[Random.Range(0, BoltVariants)];
            }
        }

        public void Hide()
        {
            if (!shown) return;
            shown = false;
            timer = 0f;
            foreach (var bolt in bolts) bolt.gameObject.SetActive(false);
        }
    }
}
