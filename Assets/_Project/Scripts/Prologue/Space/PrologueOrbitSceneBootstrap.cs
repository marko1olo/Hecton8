using System.Collections.Generic;
using Hecton8.Audio.Prologue;
using Hecton8.Core;
using Hecton8.Narrative.Prologue;
using Hecton8.Prologue.VFX;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Hecton8.Prologue.Space
{
    /// <summary>
    /// Cold scene composition bridge for the standalone 01_ORBIT prologue scene.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-8700)]
    public sealed class PrologueOrbitSceneBootstrap : MonoBehaviour
    {
        private const string OrbitSceneName = "01_ORBIT";
        private const string RuntimeRootName = "__HECTON_PROLOGUE_ORBIT_RUNTIME";
        private const string WorldSceneName = "02_HECTON_WORLD";
        private const string MainCameraName = "Main Camera";
        private const string DirectionalLightName = "Directional Light";
        private const string HectonSurfaceName = "Hecton8_Surface";
        private const string AegirName = "GasGiant_Aegir";
        private const string HectonCloudsName = "Hecton8_Clouds";
        private const string PlasmaOverlayName = "__HECTON_REENTRY_PLASMA_OVERLAY";
        private const string OrbitVolumeProfileName = "__H8_Orbit_OpticalProfile";
        private const string OrbitReflectionProbeName = "__H8_ORBIT_STATIC_REFLECTION_PROBE";
        private const int SceneRootScratchCapacity = 16;
        private const int SceneTransformScratchCapacity = 64;
        private const float OrbitCameraFarClipMeters = 360000f;
        private const float PlasmaOverlayLocalDistanceMeters = 0.35f;
        private const float PlasmaOverlayNearClipPaddingMeters = 0.03f;
        private const float StandaloneOrbitStartDistanceMeters = 62000f;
        private const float StandaloneOrbitReentryStartMeters = 50000f;
        private const float StandaloneOrbitWhiteoutMeters = 5200f;
        private const float StandaloneOrbitPassiveSpeedMetersPerSecond = 1600f;
        private const float StandaloneOrbitBurnAccelerationMetersPerSecondSq = 720f;
        private const float StandaloneOrbitMaxSpeedMetersPerSecond = 7200f;
        private const float OrbitKeyLightIntensity = 5.5f;
        private const float OrbitBloomMinimumThreshold = 16f;
        private const float OrbitBloomFullThreshold = 0.86f;
        private const float OrbitBloomFullIntensity = 0.46f;
        private const float OrbitBloomMinimumScatter = 0.05f;
        private const float OrbitBloomFullScatter = 0.84f;
        private const float OrbitOpticalQualityFloor = 0.18f;
        private const float OrbitHardShadowStrength = 0.92f;
        private const float OrbitHardShadowBias = 0.015f;
        private const float OrbitHardShadowNormalBias = 0.08f;
        private const float OrbitHardShadowNearPlane = 0.08f;
        private const float OrbitReflectionIntensity = 0.34f;
        private const int OrbitReflectionResolution = 128;

        private static readonly Color OrbitCameraBackground = new Color(0.012f, 0.026f, 0.048f, 1f);
        private static readonly Color OrbitAmbientColor = new Color(0.018f, 0.032f, 0.052f, 1f);
        private static readonly Color OrbitKeyLightColor = new Color(0.74f, 0.9f, 1f, 1f);

        // COLD ALLOC: Vector3[4] - one domain-load mesh vertex seed for the camera-local plasma fake - owner: PrologueOrbitSceneBootstrap.
        private static readonly Vector3[] s_overlayVertices =
        {
            new Vector3(-0.5f, -0.5f, 0f),
            new Vector3(-0.5f, 0.5f, 0f),
            new Vector3(0.5f, 0.5f, 0f),
            new Vector3(0.5f, -0.5f, 0f)
        };

        // COLD ALLOC: Vector2[4] - one domain-load mesh UV seed for the camera-local plasma fake - owner: PrologueOrbitSceneBootstrap.
        private static readonly Vector2[] s_overlayUvs =
        {
            new Vector2(0f, 0f),
            new Vector2(0f, 1f),
            new Vector2(1f, 1f),
            new Vector2(1f, 0f)
        };

        // COLD ALLOC: int[6] - one domain-load mesh triangle seed for the camera-local plasma fake - owner: PrologueOrbitSceneBootstrap.
        private static readonly int[] s_overlayTriangles = { 0, 1, 2, 0, 2, 3 };

        // COLD ALLOC: List<GameObject>[16] - reusable scene-root traversal buffer during scene bootstrap - owner: PrologueOrbitSceneBootstrap.
        private static readonly List<GameObject> s_sceneRootScratch = new List<GameObject>(SceneRootScratchCapacity);
        // COLD ALLOC: List<Transform>[64] - one-shot loaded-scene listener traversal scratch - owner: PrologueOrbitSceneBootstrap.
        private static readonly List<Transform> s_sceneTransformScratch = new List<Transform>(SceneTransformScratchCapacity);
        private static Mesh s_plasmaOverlayMesh;

        [SerializeField] private Material orbitPlasmaMaterial;
        [SerializeField] private Material orbitSkyboxMaterial;
        [SerializeField] private ReflectionProbe orbitStaticReflectionProbe;

        private void Awake()
        {
            if (!Application.isPlaying)
                return;

            Scene scene = gameObject.scene;
            if (!scene.IsValid() || scene.name != OrbitSceneName)
                return;

            EnsurePrologueRuntime(scene);
        }

        private static void EnsurePrologueRuntime(Scene scene)
        {
            Transform camera = FindSceneTransform(scene, MainCameraName);
            Transform keyLight = FindSceneTransform(scene, DirectionalLightName);
            Transform hectonSurface = FindSceneTransform(scene, HectonSurfaceName);
            Transform aegir = FindSceneTransform(scene, AegirName);
            Transform clouds = FindSceneTransform(scene, HectonCloudsName);
            PrologueOrbitSceneBootstrap bootstrap = null;
            if (camera != null)
                camera.TryGetComponent(out bootstrap);
            Material skyboxMaterial = bootstrap != null ? bootstrap.orbitSkyboxMaterial : null;
            ConfigureCameraForOrbitWindow(camera, skyboxMaterial != null);
            ConfigureOrbitLighting(keyLight, skyboxMaterial);
            EnsureSingleOrbitAudioListener(camera);

            GameObject runtimeRoot = ResolveOrCreateRuntimeRoot(scene);
            runtimeRoot.SetActive(false);
            ConfigureOrbitPostProcessing(runtimeRoot, camera);
            ConfigureOrbitStaticReflection(scene, bootstrap, camera);

            OrbitalRelativityDirector orbital = EnsureComponent<OrbitalRelativityDirector>(runtimeRoot);
            AwaitableDropSequenceDirector sequence = EnsureComponent<AwaitableDropSequenceDirector>(runtimeRoot);
            PrologueSequenceRegistryBridge bridge = EnsureComponent<PrologueSequenceRegistryBridge>(runtimeRoot);
            OrbitalDropReentryVfxController reentryVfx = EnsureComponent<OrbitalDropReentryVfxController>(runtimeRoot);
            PrologueWorldHandoffSceneLoader worldHandoff = EnsureComponent<PrologueWorldHandoffSceneLoader>(runtimeRoot);
            PrologueAcousticOrchestrator audioOrchestrator = EnsureComponent<PrologueAcousticOrchestrator>(runtimeRoot);

            Renderer hectonRenderer = ResolveRenderer(hectonSurface);
            Renderer aegirRenderer = ResolveRenderer(aegir);
            Renderer cloudRenderer = ResolveRenderer(clouds);
            Light orbitKeyLight = null;
            if (keyLight != null)
                keyLight.TryGetComponent(out orbitKeyLight);
            Transform plasmaOverlay = null;
            Renderer plasmaOverlayRenderer = null;
            if (bootstrap != null)
                EnsureCameraLocalPlasmaOverlay(
                    camera,
                    bootstrap.orbitPlasmaMaterial,
                    out plasmaOverlay,
                    out plasmaOverlayRenderer);

            orbital.ConfigureStandaloneOrbitPacing(
                StandaloneOrbitStartDistanceMeters,
                StandaloneOrbitReentryStartMeters,
                StandaloneOrbitWhiteoutMeters,
                StandaloneOrbitPassiveSpeedMetersPerSecond,
                StandaloneOrbitBurnAccelerationMetersPerSecondSq,
                StandaloneOrbitMaxSpeedMetersPerSecond);
            orbital.ConfigureSceneBindings(
                camera,
                hectonSurface,
                null,
                clouds,
                hectonRenderer,
                null,
                cloudRenderer);
            orbital.ConfigureAegirBackdrop(aegir, aegirRenderer);
            orbital.ConfigureOrbitKeyLight(orbitKeyLight, OrbitKeyLightIntensity);
            reentryVfx.ConfigureSceneBindings(
                camera,
                plasmaOverlay,
                null,
                plasmaOverlayRenderer,
                bootstrap != null ? bootstrap.orbitPlasmaMaterial : null);
            worldHandoff.ConfigureTargetScene(WorldSceneName);

            if (orbital != null &&
                sequence != null &&
                bridge != null &&
                reentryVfx != null &&
                worldHandoff != null &&
                audioOrchestrator != null)
            {
                runtimeRoot.SetActive(true);
            }
        }

        private static GameObject ResolveOrCreateRuntimeRoot(Scene scene)
        {
            Transform existing = FindSceneTransform(scene, RuntimeRootName);
            if (existing != null)
            {
                existing.gameObject.hideFlags = HideFlags.None;
                return existing.gameObject;
            }

            // COLD ALLOC: GameObject[1] - standalone orbit scene composition root - owner: PrologueOrbitSceneBootstrap.
            GameObject runtimeRoot = new GameObject(RuntimeRootName);
            runtimeRoot.hideFlags = HideFlags.None;
            SceneManager.MoveGameObjectToScene(runtimeRoot, scene);
            return runtimeRoot;
        }

        private static T EnsureComponent<T>(GameObject owner) where T : Component
        {
            if (owner.TryGetComponent(out T component))
                return component;

            return owner.AddComponent<T>();
        }

        private static Renderer ResolveRenderer(Transform root)
        {
            if (root == null)
                return null;

            if (root.TryGetComponent(out Renderer renderer))
                return renderer;

            return ComponentReferenceUtility.ResolveOwnedComponent<Renderer>(root);
        }

        private static void EnsureCameraLocalPlasmaOverlay(
            Transform cameraTransform,
            Material plasmaMaterial,
            out Transform overlayTransform,
            out Renderer overlayRenderer)
        {
            overlayTransform = null;
            overlayRenderer = null;
            if (cameraTransform == null || plasmaMaterial == null)
                return;

            overlayTransform = FindInHierarchy(cameraTransform, PlasmaOverlayName);
            GameObject overlayObject;
            if (overlayTransform != null)
            {
                overlayObject = overlayTransform.gameObject;
            }
            else
            {
                // COLD ALLOC: GameObject[1] - one-shot camera-local plasma surface for 01_ORBIT.
                overlayObject = new GameObject(PlasmaOverlayName);
                overlayTransform = overlayObject.transform;
                overlayTransform.SetParent(cameraTransform, false);
            }

            overlayTransform.localPosition = new Vector3(0f, 0f, ResolveInitialPlasmaOverlayDistance(cameraTransform));
            overlayTransform.localRotation = Quaternion.identity;
            overlayTransform.localScale = Vector3.one;

            if (!overlayObject.TryGetComponent(out MeshFilter meshFilter))
                meshFilter = overlayObject.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = ResolvePlasmaOverlayMesh();

            if (!overlayObject.TryGetComponent(out overlayRenderer))
                overlayRenderer = overlayObject.AddComponent<MeshRenderer>();

            overlayRenderer.sharedMaterial = plasmaMaterial;
            overlayRenderer.enabled = false;
            overlayRenderer.shadowCastingMode = ShadowCastingMode.Off;
            overlayRenderer.receiveShadows = false;
            overlayRenderer.lightProbeUsage = LightProbeUsage.Off;
            overlayRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            overlayRenderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            overlayRenderer.allowOcclusionWhenDynamic = false;
        }

        private static Mesh ResolvePlasmaOverlayMesh()
        {
            if (s_plasmaOverlayMesh != null)
                return s_plasmaOverlayMesh;

            // COLD ALLOC: Mesh[1] - shared unit quad for the camera-local plasma fake.
            Mesh mesh = new Mesh
            {
                name = "__H8_Prologue_PlasmaOverlayQuad",
                hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild,
                vertices = s_overlayVertices,
                uv = s_overlayUvs,
                triangles = s_overlayTriangles,
                bounds = new Bounds(Vector3.zero, new Vector3(1f, 1f, 0.02f))
            };
            mesh.UploadMeshData(true);
            s_plasmaOverlayMesh = mesh;
            return s_plasmaOverlayMesh;
        }

        private static float ResolveInitialPlasmaOverlayDistance(Transform cameraTransform)
        {
            float distance = PlasmaOverlayLocalDistanceMeters;
            if (cameraTransform != null && cameraTransform.TryGetComponent(out Camera camera))
            {
                float nearClip = camera.nearClipPlane;
                if (!float.IsNaN(nearClip) && !float.IsInfinity(nearClip) && nearClip >= 0f)
                    distance = Mathf.Max(distance, nearClip + PlasmaOverlayNearClipPaddingMeters);
            }

            return Mathf.Max(distance, PlasmaOverlayLocalDistanceMeters);
        }

        private static void ConfigureCameraForOrbitWindow(Transform cameraTransform, bool useSkybox)
        {
            if (cameraTransform == null)
                return;

            cameraTransform.SetPositionAndRotation(
                Vector3.zero,
                Quaternion.LookRotation(Vector3.down, Vector3.forward));
            if (cameraTransform.TryGetComponent(out Camera camera))
            {
                camera.farClipPlane = OrbitCameraFarClipMeters;
                camera.clearFlags = useSkybox ? CameraClearFlags.Skybox : CameraClearFlags.SolidColor;
                camera.backgroundColor = OrbitCameraBackground;
                camera.allowHDR = true;
                camera.allowMSAA = false;
                camera.allowDynamicResolution = true;
            }
        }

        private static void ConfigureOrbitLighting(Transform keyLightTransform, Material skyboxMaterial)
        {
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = OrbitAmbientColor;
            RenderSettings.ambientSkyColor = OrbitAmbientColor;
            RenderSettings.ambientEquatorColor = OrbitAmbientColor;
            RenderSettings.ambientGroundColor = OrbitAmbientColor;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;
            RenderSettings.defaultReflectionResolution = OrbitReflectionResolution;
            RenderSettings.reflectionIntensity = OrbitReflectionIntensity;
            if (skyboxMaterial != null)
                RenderSettings.skybox = skyboxMaterial;

            if (keyLightTransform == null)
                return;

            keyLightTransform.rotation = Quaternion.Euler(42f, -28f, 18f);
            if (keyLightTransform.TryGetComponent(out Light light))
            {
                light.type = LightType.Directional;
                light.intensity = OrbitKeyLightIntensity;
                light.color = OrbitKeyLightColor;
                light.shadows = LightShadows.Hard;
                light.shadowStrength = OrbitHardShadowStrength;
                light.shadowBias = OrbitHardShadowBias;
                light.shadowNormalBias = OrbitHardShadowNormalBias;
                light.shadowNearPlane = OrbitHardShadowNearPlane;
                light.renderMode = LightRenderMode.ForcePixel;
                light.bounceIntensity = 0f;
                RenderSettings.sun = light;
            }
        }

        private static void ConfigureOrbitStaticReflection(
            Scene scene,
            PrologueOrbitSceneBootstrap bootstrap,
            Transform cameraTransform)
        {
            ReflectionProbe probe = bootstrap != null ? bootstrap.orbitStaticReflectionProbe : null;
            Transform probeTransform = probe != null ? probe.transform : FindSceneTransform(scene, OrbitReflectionProbeName);
            if (probe == null && probeTransform != null)
                probeTransform.TryGetComponent(out probe);

            if (probe == null)
                return;

            probe.mode = ReflectionProbeMode.Baked;
            probe.refreshMode = ReflectionProbeRefreshMode.ViaScripting;
            probe.timeSlicingMode = ReflectionProbeTimeSlicingMode.NoTimeSlicing;
            probe.resolution = OrbitReflectionResolution;
            probe.intensity = OrbitReflectionIntensity;
            probe.boxProjection = false;
            probe.clearFlags = ReflectionProbeClearFlags.Skybox;
            probe.cullingMask = 0;
            if (cameraTransform != null)
                probeTransform.SetPositionAndRotation(cameraTransform.position, Quaternion.identity);
            probe.size = new Vector3(2048f, 2048f, 2048f);
        }

        private static void EnsureSingleOrbitAudioListener(Transform orbitCamera)
        {
            if (orbitCamera == null)
                return;

            if (!orbitCamera.TryGetComponent(out AudioListener orbitListener))
                orbitListener = orbitCamera.gameObject.AddComponent<AudioListener>();

            if (orbitListener != null)
                orbitListener.enabled = true;

            int sceneCount = SceneManager.sceneCount;
            for (int sceneIndex = 0; sceneIndex < sceneCount; sceneIndex++)
            {
                Scene loadedScene = SceneManager.GetSceneAt(sceneIndex);
                if (!loadedScene.IsValid() || !loadedScene.isLoaded)
                    continue;

                s_sceneRootScratch.Clear();
                s_sceneTransformScratch.Clear();
                loadedScene.GetRootGameObjects(s_sceneRootScratch);

                for (int i = 0; i < s_sceneRootScratch.Count; i++)
                {
                    GameObject root = s_sceneRootScratch[i];
                    if (root != null)
                        s_sceneTransformScratch.Add(root.transform);
                }

                while (s_sceneTransformScratch.Count > 0)
                {
                    int lastIndex = s_sceneTransformScratch.Count - 1;
                    Transform current = s_sceneTransformScratch[lastIndex];
                    s_sceneTransformScratch.RemoveAt(lastIndex);
                    if (current == null)
                        continue;

                    if (!ReferenceEquals(current, orbitCamera) &&
                        current.TryGetComponent(out AudioListener listener) &&
                        listener != null &&
                        listener.enabled)
                    {
                        listener.enabled = false;
                    }

                    int childCount = current.childCount;
                    for (int childIndex = 0; childIndex < childCount; childIndex++)
                        s_sceneTransformScratch.Add(current.GetChild(childIndex));
                }
            }

            s_sceneRootScratch.Clear();
            s_sceneTransformScratch.Clear();
        }

        private static void ConfigureOrbitPostProcessing(GameObject runtimeRoot, Transform cameraTransform)
        {
            if (runtimeRoot == null)
                return;

            UniversalAdditionalCameraData cameraData = null;
            if (cameraTransform != null)
                cameraTransform.TryGetComponent(out cameraData);

            Volume volume = EnsureComponent<Volume>(runtimeRoot);
            volume.isGlobal = true;
            volume.priority = 1601f;
            volume.blendDistance = 0f;
            volume.weight = 0f;

            VolumeProfile profile = volume.sharedProfile;
            if (profile == null)
            {
                // COLD ALLOC: VolumeProfile[1] - scene-local orbit optical stack, not a gameplay hot path object.
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                profile.name = OrbitVolumeProfileName;
                profile.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
                volume.sharedProfile = profile;
            }

            if (!profile.TryGet(out Bloom bloom))
                bloom = profile.Add<Bloom>(true);

            float quality = ResolveQuality01();
            float bloomWeight = ResolveOrbitOpticalWeight01(quality);
            bool postProcessingEnabled = bloomWeight > 0f;
            volume.enabled = postProcessingEnabled;
            volume.weight = bloomWeight;
            bloom.active = postProcessingEnabled;
            bloom.threshold.Override(math.lerp(OrbitBloomMinimumThreshold, OrbitBloomFullThreshold, bloomWeight));
            bloom.intensity.Override(OrbitBloomFullIntensity * bloomWeight);
            bloom.scatter.Override(math.lerp(OrbitBloomMinimumScatter, OrbitBloomFullScatter, bloomWeight));
            bloom.clamp.Override(65472f);
            bloom.highQualityFiltering.Override(false);
            bloom.maxIterations.Override((int)math.round(math.lerp(2f, 7f, bloomWeight)));
            if (cameraData != null)
                cameraData.renderPostProcessing = postProcessingEnabled;
        }

        private static float ResolveOrbitOpticalWeight01(float quality01)
        {
            float quality = math.saturate(quality01);
            float t = math.smoothstep(OrbitOpticalQualityFloor, 1f, quality);
            return t * t;
        }

        private static float ResolveQuality01()
        {
            float quality = HomeostasisBrain.GlobalQualityWeight;
            return math.saturate(math.select(1f, quality, math.isfinite(quality)));
        }

        private static Transform FindSceneTransform(Scene scene, string targetName)
        {
            s_sceneRootScratch.Clear();
            scene.GetRootGameObjects(s_sceneRootScratch);
            for (int i = 0; i < s_sceneRootScratch.Count; i++)
            {
                Transform match = FindInHierarchy(s_sceneRootScratch[i].transform, targetName);
                if (match != null)
                {
                    s_sceneRootScratch.Clear();
                    return match;
                }
            }

            s_sceneRootScratch.Clear();
            return null;
        }

        private static Transform FindInHierarchy(Transform root, string targetName)
        {
            if (root == null)
                return null;

            if (root.name == targetName)
                return root;

            int childCount = root.childCount;
            for (int i = 0; i < childCount; i++)
            {
                Transform match = FindInHierarchy(root.GetChild(i), targetName);
                if (match != null)
                    return match;
            }

            return null;
        }
    }
}
