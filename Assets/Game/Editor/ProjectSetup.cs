using System.IO;
using Airships.Game;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Airships.EditorTools
{
    // Repeatable project configuration: graphics modes, Android player settings and the game scene.
    public static class ProjectSetup
    {
        const string ScenePath = "Assets/Game/Scenes/Main.unity";
        const string SettingsFolder = "Assets/Game/Settings";
        const string LowAssetPath = SettingsFolder + "/LowDefinition_RPAsset.asset";
        const string HighAssetPath = SettingsFolder + "/HighDefinition_RPAsset.asset";
        const string TemplateAssetPath = "Assets/Settings/Mobile_RPAsset.asset";
        const string ApkPath = "Builds/Android/Airships.apk";

        [MenuItem("Airships/Apply Project Setup")]
        public static void ApplyAll()
        {
            ConfigureGraphicsModes();
            ConfigureAndroid();
            CreateScene();
            AssetDatabase.SaveAssets();
            Debug.Log("[Setup] Project setup applied");
        }

        static void ConfigureGraphicsModes()
        {
            Directory.CreateDirectory(SettingsFolder);
            var low = LoadOrCopy(LowAssetPath);
            var high = LoadOrCopy(HighAssetPath);

            ConfigureAsset(low, renderScale: 0.6f, msaa: 1, shadows: false, shadowDistance: 0f, hdr: false);

            // Shadow bias scales with shadow map pixel size, so a wide, low-resolution map erases
            // character-sized shadows entirely. This range still covers the dogfight camera's ship.
            ConfigureAsset(high, renderScale: 0.85f, msaa: 2, shadows: true, shadowDistance: 90f, hdr: true);

            var manager = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/QualitySettings.asset")[0];
            var serialized = new SerializedObject(manager);
            var levels = serialized.FindProperty("m_QualitySettings");
            levels.arraySize = 2;
            ConfigureLevel(levels.GetArrayElementAtIndex(0), "Low", low);
            ConfigureLevel(levels.GetArrayElementAtIndex(1), "High", high);

            var defaults = serialized.FindProperty("m_PerPlatformDefaultQuality");
            for (var i = 0; i < defaults.arraySize; i++)
            {
                defaults.GetArrayElementAtIndex(i).FindPropertyRelative("second").intValue = 1;
            }
            serialized.FindProperty("m_CurrentQuality").intValue = 1;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
        }

        static UniversalRenderPipelineAsset LoadOrCopy(string path)
        {
            var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path);
            if (asset != null) return asset;
            if (!AssetDatabase.CopyAsset(TemplateAssetPath, path)) throw new IOException($"Could not copy {TemplateAssetPath} to {path}");
            return AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path);
        }

        static void ConfigureAsset(UniversalRenderPipelineAsset asset, float renderScale, int msaa, bool shadows, float shadowDistance, bool hdr)
        {
            asset.renderScale = renderScale;
            asset.msaaSampleCount = msaa;
            asset.supportsHDR = hdr;
            asset.shadowDistance = shadowDistance;
            var serialized = new SerializedObject(asset);
            serialized.FindProperty("m_MainLightShadowsSupported").boolValue = shadows;
            serialized.FindProperty("m_MainLightShadowmapResolution").intValue = 2048;
            serialized.FindProperty("m_SoftShadowsSupported").boolValue = shadows;
            serialized.FindProperty("m_AdditionalLightShadowsSupported").boolValue = false;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
        }

        static void ConfigureLevel(SerializedProperty level, string name, RenderPipelineAsset asset)
        {
            level.FindPropertyRelative("name").stringValue = name;
            level.FindPropertyRelative("customRenderPipeline").objectReferenceValue = asset;

            // The template limits each level to certain platforms. Both modes must exist on phones.
            level.FindPropertyRelative("excludedTargetPlatforms").ClearArray();
        }

        static void ConfigureAndroid()
        {
            var android = NamedBuildTarget.Android;
            PlayerSettings.companyName = "Cody Nicholson";
            PlayerSettings.productName = "Ransom Skies";
            PlayerSettings.SetApplicationIdentifier(android, "com.codynicholson.airships");
            PlayerSettings.SetScriptingBackend(android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;

            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.Android.renderOutsideSafeArea = true;
        }

        static void CreateScene()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("Game").AddComponent<GameDirector>();
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }

        [MenuItem("Airships/Build Android APK")]
        public static void BuildApk()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ApkPath));
            EditorUserBuildSettings.buildAppBundle = false;
            var options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = ApkPath,
                target = BuildTarget.Android,
                options = BuildOptions.None,
            };
            var report = BuildPipeline.BuildPlayer(options);
            var summary = report.summary;
            Debug.Log($"[Build] {summary.result}: {summary.outputPath}, {summary.totalSize / (1024 * 1024)} MB, {summary.totalErrors} errors, {summary.totalWarnings} warnings, {summary.totalTime}");
            if (summary.result != BuildResult.Succeeded) throw new BuildFailedException($"Android build failed: {summary.result}");
        }
    }
}
