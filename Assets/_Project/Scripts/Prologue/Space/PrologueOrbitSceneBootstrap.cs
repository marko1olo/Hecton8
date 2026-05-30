using System.Collections.Generic;
using Hecton8.Core;
using Hecton8.Narrative.Prologue;
using Hecton8.Prologue.VFX;
using UnityEngine;
using UnityEngine.Rendering;
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
        private const string HectonSurfaceName = "Hecton8_Surface";
        private const string AegirName = "GasGiant_Aegir";
        private const string HectonCloudsName = "\u043E\u0431\u043B\u0430\u043A\u0430 \u0433\u0435\u043A\u0442\u043E\u043D8";
        private const string PlasmaOverlayName = "__HECTON_REENTRY_PLASMA_OVERLAY";
        private const int SceneRootScratchCapacity = 16;
        private const float OrbitCameraFarClipMeters = 360000f;
        private const float PlasmaOverlayLocalDistanceMeters = 0.35f;
        private const float PlasmaOverlayNearClipPaddingMeters = 0.03f;

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

        // COLD ALLOC: List<GameObject>[16] - one-shot 01_ORBIT root traversal scratch - owner: PrologueOrbitSceneBootstrap.
        private static readonly List<GameObject> s_sceneRootScratch = new List<GameObject>(SceneRootScratchCapacity);
        private static Mesh s_plasmaOverlayMesh;

        [SerializeField] private Material orbitPlasmaMaterial;

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
            Transform hectonSurface = FindSceneTransform(scene, HectonSurfaceName);
            Transform aegir = FindSceneTransform(scene, AegirName);
            Transform clouds = FindSceneTransform(scene, HectonCloudsName);
            ConfigureCameraForOrbitWindow(camera);

            GameObject runtimeRoot = ResolveOrCreateRuntimeRoot(scene);
            runtimeRoot.SetActive(false);

            OrbitalRelativityDirector orbital = EnsureComponent<OrbitalRelativityDirector>(runtimeRoot);
            AwaitableDropSequenceDirector sequence = EnsureComponent<AwaitableDropSequenceDirector>(runtimeRoot);
            PrologueSequenceRegistryBridge bridge = EnsureComponent<PrologueSequenceRegistryBridge>(runtimeRoot);
            OrbitalDropReentryVfxController reentryVfx = EnsureComponent<OrbitalDropReentryVfxController>(runtimeRoot);
            PrologueWorldHandoffSceneLoader worldHandoff = EnsureComponent<PrologueWorldHandoffSceneLoader>(runtimeRoot);

            Renderer hectonRenderer = ResolveRenderer(hectonSurface);
            Renderer aegirRenderer = ResolveRenderer(aegir);
            Renderer cloudRenderer = ResolveRenderer(clouds);
            Transform plasmaOverlay = null;
            Renderer plasmaOverlayRenderer = null;
            PrologueOrbitSceneBootstrap bootstrap = null;
            if (camera != null)
                camera.TryGetComponent(out bootstrap);
            if (bootstrap != null)
                EnsureCameraLocalPlasmaOverlay(
                    camera,
                    bootstrap.orbitPlasmaMaterial,
                    out plasmaOverlay,
                    out plasmaOverlayRenderer);

            orbital.ConfigureSceneBindings(
                camera,
                hectonSurface,
                null,
                clouds,
                hectonRenderer,
                null,
                cloudRenderer);
            orbital.ConfigureAegirBackdrop(aegir, aegirRenderer);
            reentryVfx.ConfigureSceneBindings(
                camera,
                plasmaOverlay,
                null,
                plasmaOverlayRenderer,
                bootstrap != null ? bootstrap.orbitPlasmaMaterial : null);
            worldHandoff.ConfigureTargetScene(WorldSceneName);

            if (orbital != null && sequence != null && bridge != null && reentryVfx != null && worldHandoff != null)
                runtimeRoot.SetActive(true);
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

        private static void ConfigureCameraForOrbitWindow(Transform cameraTransform)
        {
            if (cameraTransform == null)
                return;

            cameraTransform.SetPositionAndRotation(
                Vector3.zero,
                Quaternion.LookRotation(Vector3.down, Vector3.forward));
            if (cameraTransform.TryGetComponent(out Camera camera))
                camera.farClipPlane = Mathf.Max(camera.farClipPlane, OrbitCameraFarClipMeters);
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
