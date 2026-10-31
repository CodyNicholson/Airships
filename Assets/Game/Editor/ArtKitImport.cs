using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Airships.Game;
using UnityEditor;
using UnityEngine;

namespace Airships.EditorTools
{
    // Bakes the Blender kit's .glb files into prefabs under Resources/Art. Prefabs reference only each
    // file's mesh and clips, so the atlas and material embedded in every .glb stay out of builds. Each
    // manifest socket becomes a child transform, so the game needs no JSON at runtime.
    public static class ArtKitImport
    {
        const string KitFolder = "Assets/Game/Art/Kit";
        const string ModelFolder = KitFolder + "/Models";
        const string ManifestPath = KitFolder + "/asset_manifest.json";
        const string OutputFolder = "Assets/Game/Resources/Art";
        const string TextureFolder = OutputFolder + "/Textures";

        [MenuItem("Airships/Import Art Kit")]
        public static void ImportAll()
        {
            Directory.CreateDirectory(TextureFolder);
            ConfigureTextures();

            var manifest = (Dictionary<string, object>)Json.Parse(File.ReadAllText(ManifestPath));
            var assets = (Dictionary<string, object>)manifest["assets"];
            var baked = 0;
            foreach (var entry in assets)
            {
                var sockets = (Dictionary<string, object>)((Dictionary<string, object>)entry.Value)["sockets"];
                foreach (var quality in new[] { "HQ", "LQ" })
                {
                    Bake($"{entry.Key}_{quality}", (Dictionary<string, object>)sockets[quality]);
                    baked++;
                }
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[Art] Baked {baked} kit prefabs into {OutputFolder}");
        }

        static void ConfigureTextures()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { TextureFolder }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);

                // Swatches sit side by side, so mips and wrapping would bleed neighbouring colours.
                importer.mipmapEnabled = false;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.filterMode = FilterMode.Bilinear;
                importer.sRGBTexture = true;
                importer.SetPlatformTextureSettings(new TextureImporterPlatformSettings
                {
                    name = "Android",
                    overridden = true,
                    format = path.EndsWith("_HQ.png") ? TextureImporterFormat.ASTC_6x6 : TextureImporterFormat.ASTC_4x4,
                    maxTextureSize = 512,
                });
                importer.SaveAndReimport();
            }
        }

        static void Bake(string assetName, Dictionary<string, object> sockets)
        {
            var path = $"{ModelFolder}/{assetName}.glb";
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (source == null) throw new FileNotFoundException($"Kit model missing: {path}");

            var instance = UnityEngine.Object.Instantiate(source);
            try
            {
                instance.name = assetName;
                foreach (var renderer in instance.GetComponentsInChildren<Renderer>(true)) renderer.sharedMaterials = new Material[1];

                // glTF is right-handed. The importer mirrors X to convert, so socket positions match it.
                foreach (var socket in sockets)
                {
                    var values = (List<object>)socket.Value;
                    var child = new GameObject(KitArt.SocketPrefix + socket.Key).transform;
                    child.SetParent(instance.transform, false);
                    child.localPosition = new Vector3(-Convert.ToSingle(values[0]), Convert.ToSingle(values[1]), Convert.ToSingle(values[2]));
                }

                var clips = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().OrderBy(c => c.name).ToArray();
                if (clips.Length > 0) instance.AddComponent<KitCharacter>().Clips = clips;

                PrefabUtility.SaveAsPrefabAsset(instance, $"{OutputFolder}/{assetName}.prefab");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        // A small JSON reader, because JsonUtility can't read objects keyed by asset name.
        static class Json
        {
            public static object Parse(string text)
            {
                var index = 0;
                return Value(text, ref index);
            }

            static object Value(string s, ref int i)
            {
                Skip(s, ref i);
                switch (s[i])
                {
                    case '{':
                        var map = new Dictionary<string, object>();
                        i++;
                        while (true)
                        {
                            Skip(s, ref i);
                            if (s[i] == '}') { i++; return map; }
                            if (s[i] == ',') { i++; continue; }
                            var key = Text(s, ref i);
                            Skip(s, ref i);
                            i++; // The colon.
                            map[key] = Value(s, ref i);
                        }
                    case '[':
                        var list = new List<object>();
                        i++;
                        while (true)
                        {
                            Skip(s, ref i);
                            if (s[i] == ']') { i++; return list; }
                            if (s[i] == ',') { i++; continue; }
                            list.Add(Value(s, ref i));
                        }
                    case '"':
                        return Text(s, ref i);
                    default:
                        var start = i;
                        while (i < s.Length && ",}] \r\n\t".IndexOf(s[i]) < 0) i++;
                        var word = s.Substring(start, i - start);
                        if (word == "true") return true;
                        if (word == "false") return false;
                        if (word == "null") return null;
                        return double.Parse(word, System.Globalization.CultureInfo.InvariantCulture);
                }
            }

            static string Text(string s, ref int i)
            {
                var result = new StringBuilder();
                i++;
                while (s[i] != '"')
                {
                    if (s[i] == '\\') i++;
                    result.Append(s[i++]);
                }
                i++;
                return result.ToString();
            }

            static void Skip(string s, ref int i)
            {
                while (char.IsWhiteSpace(s[i])) i++;
            }
        }
    }
}
