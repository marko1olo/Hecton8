using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class SceneViewSkyboxEnforcer
{
    private const string EnableMenuPath = "Tools/Hecton/Dev/Scene/Enable SceneView Skybox Enforcer";
    private const string DisableMenuPath = "Tools/Hecton/Dev/Scene/Disable SceneView Skybox Enforcer";
    private const double DefaultRefreshIntervalSeconds = 0.5d;
    private static double _nextDefaultsApplyAt;
    private static bool _callbacksRegistered;

    static SceneViewSkyboxEnforcer()
    {
        RegisterCallbacks();
    }

    [MenuItem(EnableMenuPath, priority = 230)]
    private static void Enable()
    {
        RegisterCallbacks();
        ApplySceneViewDefaults();
    }

    [MenuItem(DisableMenuPath, priority = 231)]
    private static void Disable()
    {
        UnregisterCallbacks();
        ResetState();
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
        AssemblyReloadEvents.beforeAssemblyReload -= HandleBeforeAssemblyReload;
        AssemblyReloadEvents.beforeAssemblyReload += HandleBeforeAssemblyReload;
        EditorApplication.quitting -= HandleEditorQuitting;
        EditorApplication.quitting += HandleEditorQuitting;
        _callbacksRegistered = true;
    }

    private static void UnregisterCallbacks()
    {
        if (!_callbacksRegistered)
            return;

        EditorApplication.update -= ApplySceneViewDefaults;
        AssemblyReloadEvents.beforeAssemblyReload -= HandleBeforeAssemblyReload;
        EditorApplication.quitting -= HandleEditorQuitting;
        _callbacksRegistered = false;
    }

    private static void HandleBeforeAssemblyReload()
    {
        UnregisterCallbacks();
        ResetState();
    }

    private static void HandleEditorQuitting()
    {
        UnregisterCallbacks();
        ResetState();
    }

    private static void ResetState()
    {
        _nextDefaultsApplyAt = 0d;
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

            if (!state.showSkybox)
            {
                state.showSkybox = true;
                stateChanged = true;
            }

            if (!state.showClouds)
            {
                state.showClouds = true;
                stateChanged = true;
            }

            if (!state.showImageEffects)
            {
                state.showImageEffects = true;
                stateChanged = true;
            }

            if (!state.showFog)
            {
                state.showFog = true;
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

            if (stateChanged)
                sceneView.Repaint();
        }
    }

}
