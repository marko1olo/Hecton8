using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public static class SceneViewSkyboxEnforcer
{
    private const string EnableMenuPath = "Tools/Hecton/Dev/Scene/Enable SceneView Skybox Enforcer";
    private const string DisableMenuPath = "Tools/Hecton/Dev/Scene/Disable SceneView Skybox Enforcer";
    private const string SkyRootName = "Sky_System";
    private const string SkySphereName = "Sphere";
    private const string PreviewName = "__SceneViewSkyPreview";
    private const double DefaultRefreshIntervalSeconds = 0.5d;
    private const double SourceRetryIntervalSeconds = 1d;
    private static GameObject _previewObject;
    private static MeshFilter _previewFilter;
    private static MeshRenderer _previewRenderer;
    private static MeshFilter _sourceFilter;
    private static MeshRenderer _sourceRenderer;
    private static int _waterLayer = int.MinValue;
    private static int _terrainLayer = int.MinValue;
    private static bool _sourceDirty = true;
    private static double _nextDefaultsApplyAt;
    private static double _nextSourceResolveAt;
    private static bool _callbacksRegistered;

    [MenuItem(EnableMenuPath, priority = 230)]
    private static void Enable()
    {
        RegisterCallbacks();
        MarkSourceDirty();
        ApplySceneViewDefaults();
    }

    [MenuItem(DisableMenuPath, priority = 231)]
    private static void Disable()
    {
        UnregisterCallbacks();
        ResetState();
        CleanupPreview();
    }

    [MenuItem(EnableMenuPath, true)]
    private static bool EnableValidate()
    {
        return !_callbacksRegistered;
    }

    [MenuItem(DisableMenuPath, true)]
    private static bool DisableValidate()
    {
        return _callbacksRegistered;
    }

    private static void RegisterCallbacks()
    {
        if (_callbacksRegistered)
            return;

        EditorApplication.update += ApplySceneViewDefaults;
        EditorApplication.hierarchyChanged += MarkSourceDirty;
        RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
        AssemblyReloadEvents.beforeAssemblyReload += HandleBeforeAssemblyReload;
        EditorApplication.quitting += HandleEditorQuitting;
        _callbacksRegistered = true;
    }

    private static void UnregisterCallbacks()
    {
        if (!_callbacksRegistered)
            return;

        EditorApplication.update -= ApplySceneViewDefaults;
        EditorApplication.hierarchyChanged -= MarkSourceDirty;
        RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
        AssemblyReloadEvents.beforeAssemblyReload -= HandleBeforeAssemblyReload;
        EditorApplication.quitting -= HandleEditorQuitting;
        _callbacksRegistered = false;
    }

    private static void HandleBeforeAssemblyReload()
    {
        UnregisterCallbacks();
        ResetState();
        CleanupPreview();
    }

    private static void HandleEditorQuitting()
    {
        UnregisterCallbacks();
        ResetState();
        CleanupPreview();
    }

    private static void ResetState()
    {
        _sourceFilter = null;
        _sourceRenderer = null;
        _sourceDirty = true;
        _nextDefaultsApplyAt = 0d;
        _nextSourceResolveAt = 0d;
    }

    private static void ApplySceneViewDefaults()
    {
        double now = EditorApplication.timeSinceStartup;
        if (now < _nextDefaultsApplyAt)
            return;

        _nextDefaultsApplyAt = now + DefaultRefreshIntervalSeconds;

        foreach (SceneView sceneView in SceneView.sceneViews)
        {
            if (sceneView == null)
                continue;

            SceneView.SceneViewState state = sceneView.sceneViewState;
            bool stateChanged = false;

            if (state.showSkybox)
            {
                state.showSkybox = false;
                stateChanged = true;
            }

            if (!state.showClouds)
            {
                state.showClouds = true;
                stateChanged = true;
            }

            if (state.showImageEffects)
            {
                state.showImageEffects = false;
                stateChanged = true;
            }

            if (state.showFog)
            {
                state.showFog = false;
                stateChanged = true;
            }

            if (stateChanged)
                sceneView.sceneViewState = state;

            if (!sceneView.sceneLighting)
            {
                sceneView.sceneLighting = true;
                stateChanged = true;
            }

            Camera sceneCamera = sceneView.camera;
            if (sceneCamera != null && sceneCamera.clearFlags != CameraClearFlags.Skybox)
            {
                sceneCamera.clearFlags = CameraClearFlags.Skybox;
                stateChanged = true;
            }

            if (sceneCamera != null)
            {
                if (ExcludeLayer(sceneCamera, ref _waterLayer, Hecton8.Core.HectonLayerMasks.Water))
                    stateChanged = true;

                if (ExcludeLayer(sceneCamera, ref _terrainLayer, Hecton8.Core.HectonLayerMasks.Terrain))
                    stateChanged = true;
            }

            if (stateChanged)
                sceneView.Repaint();
        }
    }

    private static bool ExcludeLayer(Camera sceneCamera, ref int layer, int layerIndex)
    {
        if (layer == int.MinValue)
            layer = layerIndex;

        if (layer < 0)
            return false;

        int layerMask = 1 << layer;
        if ((sceneCamera.cullingMask & layerMask) == 0)
            return false;

        sceneCamera.cullingMask &= ~layerMask;
        return true;
    }

    private static void OnBeginCameraRendering(ScriptableRenderContext _, Camera camera)
    {
        if (camera == null || camera.cameraType != CameraType.SceneView)
        {
            DisablePreview();
            return;
        }

        ExcludeLayer(camera, ref _waterLayer, Hecton8.Core.HectonLayerMasks.Water);
        ExcludeLayer(camera, ref _terrainLayer, Hecton8.Core.HectonLayerMasks.Terrain);
        UpdatePreview(camera);
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
        _previewRenderer.shadowCastingMode = ShadowCastingMode.Off;
        _previewRenderer.receiveShadows = false;
        _previewRenderer.lightProbeUsage = LightProbeUsage.Off;
        _previewRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        _previewRenderer.enabled = true;
    }

    private static bool TryGetSourceSphere(out MeshFilter sourceFilter, out MeshRenderer sourceRenderer)
    {
        if (!_sourceDirty
            && _sourceFilter != null
            && _sourceRenderer != null
            && _sourceRenderer.sharedMaterial != null)
        {
            sourceFilter = _sourceFilter;
            sourceRenderer = _sourceRenderer;
            return true;
        }

        double now = EditorApplication.timeSinceStartup;
        if (!_sourceDirty && now < _nextSourceResolveAt)
        {
            sourceFilter = null;
            sourceRenderer = null;
            return false;
        }

        GameObject skyRoot = GameObject.Find(SkyRootName);
        if (skyRoot == null)
        {
            CacheResolvedSource(null, null, now);
            sourceFilter = null;
            sourceRenderer = null;
            return false;
        }

        Transform sphere = skyRoot.transform.Find(SkySphereName);
        if (sphere == null)
        {
            CacheResolvedSource(null, null, now);
            sourceFilter = null;
            sourceRenderer = null;
            return false;
        }

        sourceFilter = sphere.GetComponent<MeshFilter>();
        sourceRenderer = sphere.GetComponent<MeshRenderer>();
        bool hasValidSource = sourceFilter != null && sourceRenderer != null && sourceRenderer.sharedMaterial != null;
        CacheResolvedSource(hasValidSource ? sourceFilter : null, hasValidSource ? sourceRenderer : null, now);
        return hasValidSource;
    }

    private static void CacheResolvedSource(MeshFilter sourceFilter, MeshRenderer sourceRenderer, double now)
    {
        _sourceFilter = sourceFilter;
        _sourceRenderer = sourceRenderer;
        _sourceDirty = false;
        _nextSourceResolveAt = now + SourceRetryIntervalSeconds;
    }

    private static void MarkSourceDirty()
    {
        _sourceDirty = true;
        _nextSourceResolveAt = 0d;
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
        if (_previewObject == null)
            return;

        Object.DestroyImmediate(_previewObject);
        _previewObject = null;
        _previewFilter = null;
        _previewRenderer = null;
    }
}
