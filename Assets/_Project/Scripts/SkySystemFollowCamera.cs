using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
[DisallowMultipleComponent]
[AddComponentMenu("Hecton8/Environment/Sky System Follow Camera")]
public sealed class SkySystemFollowCamera : MonoBehaviour
{
    [SerializeField] private Camera runtimeCamera;
    [SerializeField] private bool followInEditMode = true;
    [SerializeField] private bool followInPlayMode = true;
    [SerializeField] private Vector3 positionOffset = Vector3.zero;
    private Camera _cachedResolvedCamera;

    private void OnEnable()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            EditorApplication.update -= EditorTick;
            EditorApplication.update += EditorTick;
        }
#endif
    }

    private void OnDisable()
    {
#if UNITY_EDITOR
        EditorApplication.update -= EditorTick;
#endif
    }

    private void LateUpdate()
    {
        if (Application.isPlaying)
        {
            if (!followInPlayMode)
                return;

            ApplyFollow();
            return;
        }

#if UNITY_EDITOR
        if (!followInEditMode)
            return;

        ApplyFollow();
#endif
    }

    private void ApplyFollow()
    {
        Camera target = ResolveTargetCamera();
        if (target == null)
            return;

        transform.position = target.transform.position + positionOffset;
    }

#if UNITY_EDITOR
    private void EditorTick()
    {
        if (Application.isPlaying || !followInEditMode || this == null)
            return;

        ApplyFollow();
    }
#endif

    private Camera ResolveTargetCamera()
    {
        if (runtimeCamera != null)
            return runtimeCamera;

        if (_cachedResolvedCamera != null &&
            _cachedResolvedCamera.enabled &&
            _cachedResolvedCamera.gameObject.activeInHierarchy)
        {
            return _cachedResolvedCamera;
        }

        Camera mainCamera = Camera.main;
        if (mainCamera != null)
        {
            _cachedResolvedCamera = mainCamera;
            return _cachedResolvedCamera;
        }

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            SceneView sceneView = SceneView.lastActiveSceneView;
            if (sceneView != null && sceneView.camera != null)
                return sceneView.camera;
        }
#endif

        Camera[] cameras = FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < cameras.Length; i++)
        {
            Camera candidate = cameras[i];
            if (candidate != null && candidate.enabled && candidate.gameObject.activeInHierarchy)
            {
                _cachedResolvedCamera = candidate;
                return _cachedResolvedCamera;
            }
        }

        return null;
    }
}
