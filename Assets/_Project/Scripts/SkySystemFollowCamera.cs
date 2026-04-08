using UnityEngine;
using Hecton8.Bootstrap;

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
    private const int RuntimeCameraBufferSize = 8;
    private static readonly Camera[] _runtimeCameraBuffer = new Camera[RuntimeCameraBufferSize];
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
        if (!Application.isPlaying || !followInPlayMode)
            return;

        ApplyFollow();
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

        if (Application.isPlaying &&
            SceneBootstrap.TryGetCurrentPlayerTransform(out Transform playerTransform) &&
            playerTransform != null)
        {
            Camera playerCamera = playerTransform.GetComponentInChildren<Camera>(true);
            if (playerCamera != null)
            {
                _cachedResolvedCamera = playerCamera;
                return _cachedResolvedCamera;
            }
        }

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            SceneView sceneView = SceneView.lastActiveSceneView;
            if (sceneView != null && sceneView.camera != null)
                return sceneView.camera;
        }
#endif

        int cameraCount = Camera.GetAllCameras(_runtimeCameraBuffer);
        for (int i = 0; i < cameraCount; i++)
        {
            Camera candidate = _runtimeCameraBuffer[i];
            if (candidate != null && candidate.enabled && candidate.gameObject.activeInHierarchy)
            {
                _cachedResolvedCamera = candidate;
                return _cachedResolvedCamera;
            }
        }

        return null;
    }
}
