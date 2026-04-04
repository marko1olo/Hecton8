using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

[InitializeOnLoad]
public static class SceneViewSkyboxEnforcer
{
    private const string SkyRootName = "Sky_System";
    private const string SkySphereName = "Sphere";
    private const string PreviewName = "__SceneViewSkyPreview";

    private static GameObject _previewObject;
    private static MeshFilter _previewFilter;
    private static MeshRenderer _previewRenderer;

    static SceneViewSkyboxEnforcer()
    {
        RegisterCallbacks();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        EditorApplication.update -= ApplySceneViewDefaults;
        RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
        AssemblyReloadEvents.beforeAssemblyReload -= CleanupPreview;
        EditorApplication.quitting -= CleanupPreview;
        CleanupPreview();
        RegisterCallbacks();
    }

    private static void RegisterCallbacks()
    {
        EditorApplication.update -= ApplySceneViewDefaults;
        RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
        AssemblyReloadEvents.beforeAssemblyReload -= CleanupPreview;
        EditorApplication.quitting -= CleanupPreview;

        EditorApplication.update += ApplySceneViewDefaults;
        RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
        AssemblyReloadEvents.beforeAssemblyReload += CleanupPreview;
        EditorApplication.quitting += CleanupPreview;
    }

    private static void ApplySceneViewDefaults()
    {
        foreach (SceneView sceneView in SceneView.sceneViews)
        {
            if (sceneView == null)
                continue;

            SceneView.SceneViewState state = sceneView.sceneViewState;
            state.showSkybox = true;
            state.showClouds = true;
            state.showImageEffects = true;
            state.showFog = true;
            sceneView.sceneViewState = state;
            sceneView.sceneLighting = true;

            Camera sceneCamera = sceneView.camera;
            if (sceneCamera != null)
            {
                sceneCamera.clearFlags = CameraClearFlags.Skybox;
                UpdatePreview(sceneCamera);
            }
        }
    }

    private static void OnBeginCameraRendering(ScriptableRenderContext _, Camera camera)
    {
        if (_previewRenderer == null)
            return;

        _previewRenderer.enabled = camera != null && camera.cameraType == CameraType.SceneView;
    }

    private static void UpdatePreview(Camera sceneCamera)
    {
        if (sceneCamera == null)
            return;

        if (!TryGetSourceSphere(out MeshFilter sourceFilter, out MeshRenderer sourceRenderer))
        {
            DisablePreview();
            return;
        }

        EnsurePreview();
        if (_previewFilter == null || _previewRenderer == null)
            return;

        _previewObject.transform.position = sceneCamera.transform.position;
        _previewObject.transform.rotation = Quaternion.identity;
        _previewObject.transform.localScale = sourceFilter.transform.lossyScale;

        _previewFilter.sharedMesh = sourceFilter.sharedMesh;
        _previewRenderer.sharedMaterials = sourceRenderer.sharedMaterials;
        _previewRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _previewRenderer.receiveShadows = false;
        _previewRenderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        _previewRenderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        _previewRenderer.enabled = true;
    }

    private static bool TryGetSourceSphere(out MeshFilter sourceFilter, out MeshRenderer sourceRenderer)
    {
        sourceFilter = null;
        sourceRenderer = null;

        GameObject skyRoot = GameObject.Find(SkyRootName);
        if (skyRoot == null)
            return false;

        Transform sphere = skyRoot.transform.Find(SkySphereName);
        if (sphere == null)
            return false;

        sourceFilter = sphere.GetComponent<MeshFilter>();
        sourceRenderer = sphere.GetComponent<MeshRenderer>();
        return sourceFilter != null && sourceRenderer != null && sourceRenderer.sharedMaterial != null;
    }

    private static void EnsurePreview()
    {
        if (_previewObject != null && _previewFilter != null && _previewRenderer != null)
            return;

        _previewObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        _previewObject.name = PreviewName;
        _previewObject.hideFlags = HideFlags.HideAndDontSave;
        Object.DestroyImmediate(_previewObject.GetComponent<Collider>());

        _previewFilter = _previewObject.GetComponent<MeshFilter>();
        _previewRenderer = _previewObject.GetComponent<MeshRenderer>();
        _previewRenderer.hideFlags = HideFlags.HideAndDontSave;
        _previewFilter.hideFlags = HideFlags.HideAndDontSave;
    }

    private static void DisablePreview()
    {
        if (_previewRenderer != null)
            _previewRenderer.enabled = false;
    }

    private static void CleanupPreview()
    {
        if (_previewObject != null)
        {
            Object.DestroyImmediate(_previewObject);
            _previewObject = null;
            _previewFilter = null;
            _previewRenderer = null;
        }
    }
}
