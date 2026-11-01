using System.Collections.Generic;
using System.Text.RegularExpressions;
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

        static readonly Dictionary<string, Sprite> Sprites = new Dictionary<string, Sprite>();
        static readonly Dictionary<string, Regex> Globs = new Dictionary<string, Regex>();

        static bool HighDefinition => GameDirector.Instance == null || GameDirector.Instance.Stage == null || GameDirector.Instance.Stage.HighDefinition;
        public static string Quality => HighDefinition ? "HQ" : "LQ";

        public static GameObject Spawn(string baseName, Transform parent)
        {
            var path = $"Art/{baseName}_{Quality}";
            var prefab = Resources.Load<GameObject>(path);
            if (prefab == null) throw new System.IO.FileNotFoundException($"No kit prefab at Resources/{path}. Run Airships/Import Art Kit.");
            var instance = Object.Instantiate(prefab, parent, false);
            instance.name = baseName;
            return instance;
        }

        public static Texture2D Palette(string faction)
        {
            if (string.IsNullOrEmpty(faction)) faction = Crimson;
            return Resources.Load<Texture2D>($"Art/Textures/T_Palette_{faction}_{Quality}");
        }

        public static Texture2D Glow() => Resources.Load<Texture2D>($"Art/Textures/T_Emissive_{Quality}");

        public static Material NewMaterial(string name, string faction, float bandEdge = 0.1f, float bandSoftness = 0.08f)
        {
            var material = Models.NewLitMaterial(name, bandEdge, bandSoftness);
            material.SetTexture("_BaseMap", Palette(faction));
            material.SetTexture("_GlowMap", Glow());
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

        // Globs are tried in order. The first that matches anything wins, and every match it finds counts.
        public static List<Transform> MatchSockets(GameObject kit, string[] patterns)
        {
            var found = new List<Transform>();
            if (kit == null || patterns == null) return found;
            foreach (var pattern in patterns)
            {
                var regex = Glob(pattern);
                foreach (Transform child in kit.transform)
                {
                    if (child.name.StartsWith(SocketPrefix) && regex.IsMatch(child.name.Substring(SocketPrefix.Length))) found.Add(child);
                }
                if (found.Count > 0) return found;
            }
            return found;
        }

        static Regex Glob(string pattern)
        {
            if (Globs.TryGetValue(pattern, out var regex)) return regex;
            regex = new Regex("^" + Regex.Escape(pattern).Replace("\\*", ".*").Replace("\\?", ".") + "$");
            Globs[pattern] = regex;
            return regex;
        }

        // A sprite from the kit's icon set, such as "Hud_Grapple". Null if the kit doesn't have one.
        public static Sprite Icon(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            var key = $"Icons/T_Icon_{name}_{Quality}";
            if (Sprites.TryGetValue(key, out var sprite)) return sprite;
            sprite = Resources.Load<Sprite>("Art/" + key);
            Sprites[key] = sprite;
            return sprite;
        }

        // The icon for a held weapon, such as "SM_Prop_HookSabre". Null when it has none.
        public static Sprite WeaponIcon(string prop)
        {
            if (string.IsNullOrEmpty(prop)) return null;
            var name = prop.Replace("SM_Prop_", "");
            if (name == "ArmadaHalberd") name = "Halberd";
            if (name == "ArmadaMusket") name = "Musket";
            return Icon("Weapon_" + name);
        }

        // A character's comms bust, such as "Rook". Null if there's no painted portrait.
        public static Sprite Portrait(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            var key = $"Portraits/T_Portrait_{name}_{Quality}";
            if (Sprites.TryGetValue(key, out var sprite)) return sprite;
            sprite = Resources.Load<Sprite>("Art/" + key);
            Sprites[key] = sprite;
            return sprite;
        }
    }
}
