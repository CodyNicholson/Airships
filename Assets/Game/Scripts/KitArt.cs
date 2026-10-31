using UnityEngine;

namespace Airships.Game
{
    // Loads Blender kit models baked by the Airships/Import Art Kit menu, in the current graphics mode.
    public static class KitArt
    {
        public const string SocketPrefix = "socket_";
        public const string Crimson = "Crimson";
        public const string Azure = "Azure";
        public const string Emerald = "Emerald";
        public const string Obsidian = "Obsidian";

        static bool HighDefinition => GameDirector.Instance == null || GameDirector.Instance.Stage == null || GameDirector.Instance.Stage.HighDefinition;

        public static GameObject Spawn(string baseName, Transform parent)
        {
            var path = $"Art/{baseName}_{(HighDefinition ? "HQ" : "LQ")}";
            var prefab = Resources.Load<GameObject>(path);
            if (prefab == null) throw new System.IO.FileNotFoundException($"No kit prefab at Resources/{path}. Run Airships/Import Art Kit.");
            var instance = Object.Instantiate(prefab, parent, false);
            instance.name = baseName;
            return instance;
        }

        public static Texture2D Palette(string faction)
        {
            return Resources.Load<Texture2D>($"Art/Textures/T_Palette_{faction}_{(HighDefinition ? "HQ" : "LQ")}");
        }

        public static Material NewMaterial(string name, string faction, float bandEdge = 0.1f, float bandSoftness = 0.08f)
        {
            var material = Models.NewLitMaterial(name, bandEdge, bandSoftness);
            material.SetTexture("_BaseMap", Palette(faction));
            return material;
        }

        public static void Paint(GameObject model, Material material)
        {
            foreach (var renderer in model.GetComponentsInChildren<Renderer>(true))
            {
                renderer.sharedMaterial = material;
                renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
                renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            }
        }

        public static Transform Socket(GameObject model, string name) => model.transform.Find(SocketPrefix + name);

        public static Transform FindDeep(Transform root, string name)
        {
            if (root.name == name) return root;
            foreach (Transform child in root)
            {
                var found = FindDeep(child, name);
                if (found != null) return found;
            }
            return null;
        }
    }
}
