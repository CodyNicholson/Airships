using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Airships.Game
{
    // The sky every scene takes place in, plus the camera, sun and graphics mode switching.
    public class WorldStage : MonoBehaviour
    {
        const int LowQualityLevel = 0;
        const int HighQualityLevel = 1;
        const int ExtraCloudSeed = 9871;

        public Camera MainCamera { get; private set; }
        public CinemachineCamera VirtualCamera { get; private set; }
        public CinemachineImpulseSource Impulse { get; private set; }
        public Light Sun { get; private set; }
        public bool HighDefinition { get; private set; } = true;

        Material skyMaterial;
        Material panoMaterial;
        Material cloudMaterial;
        Transform clouds;
        GameObject extraClouds;
        Transform islands;
        Volume volume;

        public static WorldStage Create()
        {
            var go = new GameObject("World Stage");
            var stage = go.AddComponent<WorldStage>();
            stage.Build();
            return stage;
        }

        void Build()
        {
            BuildCamera();
            BuildSun();
            BuildSky();
            BuildVolume();
            BuildClouds();
        }

        void BuildCamera()
        {
            var cameraObject = new GameObject("Main Camera") { tag = "MainCamera" };
            cameraObject.transform.SetParent(transform, false);
            MainCamera = cameraObject.AddComponent<Camera>();
            MainCamera.clearFlags = CameraClearFlags.Skybox;
            MainCamera.nearClipPlane = 0.5f;
            MainCamera.farClipPlane = 1800f;
            cameraObject.AddComponent<AudioListener>();
            var cameraData = cameraObject.AddComponent<UniversalAdditionalCameraData>();
            cameraData.renderPostProcessing = true;
            cameraData.antialiasing = AntialiasingMode.None;
            var brain = cameraObject.AddComponent<CinemachineBrain>();
            brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.Cut, 0f);

            var rig = new GameObject("Stage Camera");
            rig.transform.SetParent(transform, false);
            VirtualCamera = rig.AddComponent<CinemachineCamera>();
            var lens = VirtualCamera.Lens;
            lens.FieldOfView = 55f;
            lens.NearClipPlane = 0.5f;
            lens.FarClipPlane = 1800f;
            VirtualCamera.Lens = lens;
            var listener = rig.AddComponent<CinemachineImpulseListener>();
            listener.ChannelMask = 1;
            listener.Gain = 1f;
            listener.UseCameraSpace = true;

            Impulse = rig.AddComponent<CinemachineImpulseSource>();
            Impulse.ImpulseDefinition.ImpulseChannel = 1;
            Impulse.ImpulseDefinition.ImpulseType = CinemachineImpulseDefinition.ImpulseTypes.Uniform;
            Impulse.ImpulseDefinition.ImpulseShape = CinemachineImpulseDefinition.ImpulseShapes.Bump;
            Impulse.ImpulseDefinition.ImpulseDuration = 0.28f;
        }

        void BuildSun()
        {
            var sunObject = new GameObject("Sun");
            sunObject.transform.SetParent(transform, false);
            sunObject.transform.rotation = Quaternion.Euler(47f, -38f, 0f);
            Sun = sunObject.AddComponent<Light>();
            Sun.type = LightType.Directional;
            Sun.color = Palette.SunLight;
            Sun.intensity = 1.15f;
            Sun.shadows = LightShadows.Soft;
            Sun.shadowStrength = 0.75f;
            RenderSettings.sun = Sun;
        }

        void BuildSky()
        {
            var sky = new Material(Resources.Load<Shader>("Shaders/Sky")) { name = "Sky" };
            sky.SetColor("_TopColor", Palette.SkyTop);
            sky.SetColor("_HorizonColor", Palette.SkyHorizon);
            sky.SetColor("_BelowColor", Palette.SkyBelow);
            sky.SetColor("_SunColor", Palette.Sun);
            sky.SetVector("_SunDirection", -Sun.transform.forward);
            RenderSettings.skybox = sky;
            skyMaterial = sky;

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = Palette.AmbientSky;
            RenderSettings.ambientEquatorColor = Palette.AmbientSky;
            RenderSettings.ambientGroundColor = Palette.AmbientGround;
            Shader.SetGlobalColor("_AirshipsSkyAmbient", Palette.AmbientSky.linear);
            Shader.SetGlobalColor("_AirshipsGroundAmbient", Palette.AmbientGround.linear);

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = Palette.Fog;
            RenderSettings.fogStartDistance = 320f;
            RenderSettings.fogEndDistance = 1500f;
        }

        void BuildVolume()
        {
            var volumeObject = new GameObject("Global Volume");
            volumeObject.transform.SetParent(transform, false);
            volume = volumeObject.AddComponent<Volume>();
            volume.isGlobal = true;
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();

            // Explicitly off: tonemapping mutes the vivid sky this art style depends on, and URP's
            // default volume profile would otherwise apply its own.
            var tonemapping = profile.Add<Tonemapping>(true);
            tonemapping.mode.Override(TonemappingMode.None);
            var bloom = profile.Add<Bloom>(true);
            bloom.intensity.Override(0.35f);
            bloom.threshold.Override(1.1f);
            var vignette = profile.Add<Vignette>(true);
            vignette.intensity.Override(0.18f);
            var adjustments = profile.Add<ColorAdjustments>(true);
            adjustments.saturation.Override(14f);
            adjustments.contrast.Override(8f);
            volume.sharedProfile = profile;
        }

        void BuildClouds()
        {
            clouds = new GameObject("Clouds").transform;
            clouds.SetParent(transform, false);
            var material = Models.NewLitMaterial("Clouds", -0.25f, 0.45f);
            cloudMaterial = material;
            Models.Attach("Base Clouds", clouds, BuildCloudMesh(38, 1234), material, false);
            extraClouds = Models.Attach("Extra Clouds", clouds, BuildCloudMesh(36, ExtraCloudSeed), material, false);
        }

        static Mesh BuildCloudMesh(int clusters, int seed)
        {
            var random = new System.Random(seed);
            float Range(float min, float max) => min + (float)random.NextDouble() * (max - min);

            var kit = new MeshKit();
            for (var c = 0; c < clusters; c++)
            {
                var angle = Range(0f, Mathf.PI * 2f);
                var distance = Mathf.Sqrt(Range(0.02f, 1f)) * 720f;
                var below = random.NextDouble() < 0.72;
                var center = new Vector3(Mathf.Cos(angle) * distance, below ? Range(-95f, -32f) : Range(55f, 130f), Mathf.Sin(angle) * distance);
                var size = Range(16f, 46f);
                var puffs = random.Next(3, 6);
                for (var p = 0; p < puffs; p++)
                {
                    var offset = new Vector3(Range(-1.3f, 1.3f) * size, Range(-0.18f, 0.28f) * size, Range(-0.75f, 0.75f) * size);
                    var radius = size * Range(0.55f, 1f);
                    kit.Blob(center + offset, new Vector3(radius * 1.25f, radius * 0.6f, radius), Palette.Cloud, 1, 0.1f, random.Next(), true);
                }
            }
            return kit.Build("Clouds");
        }

        // Kit islands come in each graphics mode's detail level, so a mode change rebuilds them.
        void BuildIslands()
        {
            if (islands != null) Destroy(islands.gameObject);
            islands = new GameObject("Islands").transform;
            islands.SetParent(transform, false);

            var random = new System.Random(555);
            float Range(float min, float max) => min + (float)random.NextDouble() * (max - min);

            var material = KitArt.NewMaterial("Islands", KitArt.Crimson);
            for (var i = 0; i < 9; i++)
            {
                var angle = i / 9f * Mathf.PI * 2f + Range(-0.2f, 0.2f);
                var distance = Range(480f, 760f);
                var scale = Range(3.6f, 8.5f);
                var island = KitArt.Spawn("SM_Env_SkyIsland", islands);
                island.transform.localPosition = new Vector3(Mathf.Cos(angle) * distance, Range(-12f, 40f) - 12f * scale, Mathf.Sin(angle) * distance);
                island.transform.localRotation = Quaternion.Euler(0f, Range(0f, 360f), 0f);
                island.transform.localScale = Vector3.one * scale;
                KitArt.Paint(island, material);
                foreach (var renderer in island.GetComponentsInChildren<Renderer>()) renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
        }

        void Update()
        {
            clouds.position += new Vector3(1.2f, 0f, 0.5f) * Time.deltaTime;
            if (clouds.position.x > 180f) clouds.position = new Vector3(-180f, 0f, -75f);
        }

        // Sets the sky, fog and sunlight for one of the campaign's weathers.
        public void ApplyWeather(Airships.Core.WeatherDef weather)
        {
            var pano = string.IsNullOrEmpty(weather.sky) ? null : Effects.Data.Sky(weather.sky);
            if (pano != null)
            {
                ApplySky(pano, weather.lightBoost > 0f ? weather.lightBoost : 1f);
                return;
            }
            RenderSettings.skybox = skyMaterial;
            skyMaterial.SetColor("_TopColor", HexColor(weather.skyTop));
            skyMaterial.SetColor("_HorizonColor", HexColor(weather.skyHorizon));
            skyMaterial.SetColor("_BelowColor", HexColor(weather.skyBelow));
            RenderSettings.fogColor = HexColor(weather.fog);
            Sun.color = HexColor(weather.sun);
            Sun.intensity = weather.sunIntensity;
            var ambient = Mathf.Clamp01(weather.sunIntensity / 1.15f);
            var sky = Palette.AmbientSky * ambient;
            var ground = Palette.AmbientGround * ambient;
            RenderSettings.ambientSkyColor = sky;
            RenderSettings.ambientEquatorColor = sky;
            RenderSettings.ambientGroundColor = ground;
            Shader.SetGlobalColor("_AirshipsSkyAmbient", sky.linear);
            Shader.SetGlobalColor("_AirshipsGroundAmbient", ground.linear);
        }

        // The kit's sky values assume a realistic renderer, and this one is flat-shaded, so light is scaled down.
        const float SunScale = 0.5f;
        const float AmbientScale = 0.95f;
        const float AmbientFloor = 0.16f;
        const float FogDistanceScale = 1.5f;

        void ApplySky(SkyDef def, float boost)
        {
            if (panoMaterial == null) panoMaterial = new Material(Resources.Load<Shader>("Shaders/PanoSky")) { name = "Panorama" };
            var file = System.IO.Path.GetFileNameWithoutExtension(HighDefinition ? def.fileHq : def.fileLq);
            panoMaterial.SetTexture("_MainTex", Resources.Load<Texture2D>("Art/Sky/" + file));
            RenderSettings.skybox = panoMaterial;

            var toSun = new Vector3(def.sunX, def.sunY, def.sunZ).normalized;
            var up = Mathf.Abs(toSun.y) > 0.95f ? Vector3.forward : Vector3.up;
            Sun.transform.rotation = Quaternion.LookRotation(-toSun, up);
            Sun.color = new Color(def.sunR, def.sunG, def.sunB);
            Sun.intensity = def.sunIntensity * SunScale * boost;

            // A floor under the ambient light, so a dark hull still reads against a dark sky.
            var ambient = Color.Lerp(new Color(def.ambientR, def.ambientG, def.ambientB), Color.white, AmbientFloor) * AmbientScale * boost;
            var ground = ambient * 0.6f;
            RenderSettings.ambientSkyColor = ambient;
            RenderSettings.ambientEquatorColor = ambient;
            RenderSettings.ambientGroundColor = ground;
            Shader.SetGlobalColor("_AirshipsSkyAmbient", ambient.linear);
            Shader.SetGlobalColor("_AirshipsGroundAmbient", ground.linear);

            RenderSettings.fogColor = new Color(def.fogR, def.fogG, def.fogB);
            RenderSettings.fogStartDistance = def.fogStart * FogDistanceScale;
            RenderSettings.fogEndDistance = def.fogEnd * FogDistanceScale;
            if (cloudMaterial != null) cloudMaterial.SetColor("_BaseColor", Color.Lerp(new Color(def.fogR, def.fogG, def.fogB), Color.white, 0.55f));
        }

        static Color HexColor(string hex) => ColorUtility.TryParseHtmlString("#" + hex, out var color) ? color : Color.white;

        // Switches every setting that separates the low and high definition modes.
        public void ApplyDefinition(bool high)
        {
            var rebuildIslands = islands == null || HighDefinition != high;
            HighDefinition = high;
            Effects.SetDefinition(high);
            if (rebuildIslands) BuildIslands();
            var level = high ? HighQualityLevel : LowQualityLevel;
            QualitySettings.SetQualityLevel(level, true);
            if (QualitySettings.GetQualityLevel() != level)
            {
                Debug.LogError($"[Graphics] Could not switch to quality level {level}. Check that it isn't excluded for this platform.");
            }
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = high ? 60 : 30;
            MainCamera.GetUniversalAdditionalCameraData().renderPostProcessing = high;
            MainCamera.allowHDR = high;
            Sun.shadows = high ? LightShadows.Soft : LightShadows.None;
            extraClouds.SetActive(high);
            Fx.DensityScale = high ? 1f : 0.45f;
        }

        public void Shake(float force)
        {
            if (force <= 0f) return;
            Impulse.GenerateImpulseWithVelocity(Random.onUnitSphere * force);
        }

        // Hands the camera to a target-following setup for the current screen.
        public void FrameTarget(Transform follow, Transform lookAt, Vector3 offset, float fieldOfView, float positionDamping, float rotationDamping = 0f)
        {
            VirtualCamera.Target.TrackingTarget = follow;
            VirtualCamera.Target.CustomLookAtTarget = true;
            VirtualCamera.Target.LookAtTarget = lookAt;
            var lens = VirtualCamera.Lens;
            lens.FieldOfView = fieldOfView;
            VirtualCamera.Lens = lens;

            var followComponent = VirtualCamera.GetComponent<CinemachineFollow>();
            if (followComponent == null) followComponent = VirtualCamera.gameObject.AddComponent<CinemachineFollow>();
            followComponent.FollowOffset = offset;
            var tracker = followComponent.TrackerSettings;
            tracker.BindingMode = Unity.Cinemachine.TargetTracking.BindingMode.LockToTargetWithWorldUp;
            tracker.PositionDamping = Vector3.one * positionDamping;
            tracker.RotationDamping = Vector3.one * rotationDamping;
            followComponent.TrackerSettings = tracker;

            if (VirtualCamera.GetComponent<CinemachineHardLookAt>() == null) VirtualCamera.gameObject.AddComponent<CinemachineHardLookAt>();
            VirtualCamera.PreviousStateIsValid = false;
        }
    }
}
