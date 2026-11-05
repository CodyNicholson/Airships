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
        const string VfxFolder = OutputFolder + "/Vfx";
        const string SkyFolder = OutputFolder + "/Sky";
        const string IconFolder = OutputFolder + "/Icons";
        const string PortraitFolder = OutputFolder + "/Portraits";
        const string ArtDataPath = OutputFolder + "/art_data.json";

        [MenuItem("Airships/Import Art Kit")]
        public static void ImportAll()
        {
            Directory.CreateDirectory(TextureFolder);
            ConfigureTextures();
            ConfigureVfx();
            ConfigureSkies();
            ConfigureSprites(IconFolder, TextureImporterFormat.ASTC_6x6);
            ConfigureSprites(PortraitFolder, TextureImporterFormat.ASTC_6x6);

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
            WriteArtData(manifest);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[Art] Baked {baked} kit prefabs into {OutputFolder}");
        }

        // Sets up icons added since the last full import as sprites, without re-baking the kit's prefabs.
        [MenuItem("Airships/Import Icons")]
        public static void ImportIcons()
        {
            AssetDatabase.Refresh();
            ConfigureSprites(IconFolder, TextureImporterFormat.ASTC_6x6);
            AssetDatabase.SaveAssets();
            Debug.Log("[Art] Icons configured");
        }

        // Flipbook sheets have soft edges, and their frames sit side by side, so no mips or wrapping.
        static void ConfigureVfx()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { VfxFolder }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.mipmapEnabled = false;
                importer.wrapMode = path.Contains("_Rain_") ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
                importer.filterMode = FilterMode.Bilinear;
                importer.sRGBTexture = true;
                importer.alphaSource = TextureImporterAlphaSource.FromInput;
                importer.alphaIsTransparency = false;
                importer.SetPlatformTextureSettings(new TextureImporterPlatformSettings
                {
                    name = "Android",
                    overridden = true,
                    format = TextureImporterFormat.ASTC_6x6,
                    maxTextureSize = 1024,
                });
                importer.SaveAndReimport();
            }
        }

        // Panoramas wrap around the horizon, and stop at the poles.
        static void ConfigureSkies()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { SkyFolder }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.mipmapEnabled = false;
                importer.wrapModeU = TextureWrapMode.Repeat;
                importer.wrapModeV = TextureWrapMode.Clamp;
                importer.filterMode = FilterMode.Bilinear;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.sRGBTexture = true;
                importer.SetPlatformTextureSettings(new TextureImporterPlatformSettings
                {
                    name = "Android",
                    overridden = true,
                    format = TextureImporterFormat.ASTC_8x8,
                    maxTextureSize = 2048,
                });
                importer.SaveAndReimport();
            }
        }

        static void ConfigureSprites(string folder, TextureImporterFormat format)
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { folder }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.filterMode = FilterMode.Bilinear;
                importer.SetPlatformTextureSettings(new TextureImporterPlatformSettings
                {
                    name = "Android",
                    overridden = true,
                    format = format,
                    maxTextureSize = 512,
                });
                importer.SaveAndReimport();
            }
        }

        static float Number(Dictionary<string, object> map, string key) => map.TryGetValue(key, out var value) && value != null ? Convert.ToSingle(value) : 0f;

        static void WriteArtData(Dictionary<string, object> manifest)
        {
            var data = new ArtData();

            var flipbooks = new List<FlipbookDef>();
            foreach (var entry in (Dictionary<string, object>)manifest["effects"])
            {
                var e = (Dictionary<string, object>)entry.Value;
                var anchor = (List<object>)e["anchor"];
                var variants = (Dictionary<string, object>)e["variants"];
                flipbooks.Add(new FlipbookDef
                {
                    id = entry.Key,
                    fps = Number(e, "fps"),
                    loop = (bool)e["loop"],
                    blend = (string)e["blend"],
                    anchorX = Convert.ToSingle(anchor[0]),
                    anchorY = Convert.ToSingle(anchor[1]),
                    hq = Variant((Dictionary<string, object>)variants["HQ"]),
                    lq = Variant((Dictionary<string, object>)variants["LQ"]),
                });
            }
            data.flipbooks = flipbooks.ToArray();

            var presets = new List<EffectPresetDef>();
            foreach (var entry in (Dictionary<string, object>)manifest["effect_presets"])
            {
                var e = (Dictionary<string, object>)entry.Value;
                var velocity = (List<object>)e["velocity_mps"];
                var max = (Dictionary<string, object>)e["max_particles"];
                presets.Add(new EffectPresetDef
                {
                    id = entry.Key,
                    flipbook = (string)e["flipbook"],
                    sockets = ((List<object>)e["sockets"]).Select(o => (string)o).ToArray(),
                    sizeMeters = Number(e, "size_m"),
                    lifetime = Number(e, "lifetime_s"),
                    rate = Number(e, "rate"),
                    burst = (int)Number(e, "burst"),
                    maxHq = (int)Number(max, "HQ"),
                    maxLq = (int)Number(max, "LQ"),
                    velocityX = Convert.ToSingle(velocity[0]),
                    velocityY = Convert.ToSingle(velocity[1]),
                    velocityZ = Convert.ToSingle(velocity[2]),
                    depthTest = !e.TryGetValue("depth_test", out var depth) || (bool)depth,
                    attached = e.TryGetValue("attached", out var attached) && (bool)attached,
                    followUp = e.TryGetValue("follow_up", out var follow) ? (string)follow : "",
                    fallbackMesh = e.TryGetValue("fallback_mesh", out var mesh) ? (string)mesh : "",
                });
            }
            data.presets = presets.ToArray();

            var skies = new List<SkyDef>();
            foreach (var entry in (Dictionary<string, object>)manifest["skies"])
            {
                var e = (Dictionary<string, object>)entry.Value;
                var sun = (List<object>)e["sun_dir"];
                var color = (List<object>)e["sun_color"];
                var ambient = (List<object>)e["ambient_color"];
                var fog = (List<object>)e["fog_color"];
                var variants = (Dictionary<string, object>)e["variants"];
                skies.Add(new SkyDef
                {
                    id = entry.Key,
                    fileHq = (string)((Dictionary<string, object>)variants["HQ"])["file"],
                    fileLq = (string)((Dictionary<string, object>)variants["LQ"])["file"],
                    sunX = Convert.ToSingle(sun[0]),
                    sunY = Convert.ToSingle(sun[1]),
                    sunZ = Convert.ToSingle(sun[2]),
                    sunR = Convert.ToSingle(color[0]),
                    sunG = Convert.ToSingle(color[1]),
                    sunB = Convert.ToSingle(color[2]),
                    sunIntensity = Number(e, "sun_intensity"),
                    ambientR = Convert.ToSingle(ambient[0]),
                    ambientG = Convert.ToSingle(ambient[1]),
                    ambientB = Convert.ToSingle(ambient[2]),
                    fogR = Convert.ToSingle(fog[0]),
                    fogG = Convert.ToSingle(fog[1]),
                    fogB = Convert.ToSingle(fog[2]),
                    fogStart = Number(e, "fog_start_m"),
                    fogEnd = Number(e, "fog_end_m"),
                });
            }
            data.skies = skies.ToArray();
            File.WriteAllText(ArtDataPath, JsonUtility.ToJson(data, true));
        }

        static FlipbookVariant Variant(Dictionary<string, object> v) => new FlipbookVariant
        {
            file = (string)v["file"],
            columns = (int)Number(v, "columns"),
            rows = (int)Number(v, "rows"),
            frames = (int)Number(v, "frames"),
        };

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
