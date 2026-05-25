using UnityEngine;
using Hecton8.Bootstrap;
using Hecton8.Atmosphere;
using Hecton8.Core;
using Hecton8.Gameplay;

#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
[DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Environment/Sky System Follow Camera")]
public sealed class SkySystemFollowCamera : MonoBehaviour, IUpdatable, ILateFrameTickable, IGlobalRegistryHotSwapListener
{
    [Tooltip("Explicit runtime camera override. Falls back to the current player camera when empty.")]
    [SerializeField] private Camera runtimeCamera;
    [Tooltip("Optional explicit atmosphere owner. When empty, the component falls back to the atmosphere read-model route.")]
    [SerializeField] private HectonAtmosphereManager atmosphereManager;
    [Tooltip("Optional explicit player movement owner. When empty, the component resolves the active player and uses its live water surface.")]
    [SerializeField] private HectonPlayerMovement playerMovement;
    [Tooltip("Follow the editor Scene View camera while not playing.")]
    [SerializeField] private bool followInEditMode = true;
    [Tooltip("Follow the resolved gameplay camera while the game is running.")]
    [SerializeField] private bool followInPlayMode = true;
    [Tooltip("Follow camera Y as well as X/Z. Sky rig must move 1:1 with the active observer so celestial bodies never reveal themselves as world geometry.")]
    [SerializeField] private bool followVerticalPosition = true;
    [Tooltip("Legacy sea-level lock. Disabled by default because it exposes celestial bodies inside the world when the observer climbs in height.")]
    [SerializeField] private bool lockToSeaLevel = false;
    [Tooltip("Fallback world Y used only when no live sea-level owner is available.")]
    [SerializeField] private float fallbackSeaLevelY = 0f;
    [Tooltip("Manual Y trim applied after resolving the live sea level. Use this only if the authored sky horizon needs a small scene-specific offset.")]
    [SerializeField] private float horizonVerticalOffset = 0f;
    [Tooltip("Optional position offset applied after resolving the follow target.")]
    [SerializeField] private Vector3 positionOffset = Vector3.zero;
    private const int RuntimeCameraBufferSize = 8;
    private static readonly Camera[] _runtimeCameraBuffer = new Camera[RuntimeCameraBufferSize];
    private Camera _cachedResolvedCamera;
    private float _fixedYPosition;
    private bool _fixedYCaptured;
    private Camera _editorLastTargetCamera;
    private Vector3 _editorLastAppliedPosition;
    private bool _editorPositionCached;
    private bool _registeredForTick;
    private bool _registeredForLateFrame;
    private bool _registeredHotSwapListener;
    private Vector3 _pendingFollowPosition;
    private bool _hasPendingFollowPosition;
    private IAtmosphereReadModel _cachedAtmosphereReadModel;

    private void OnEnable()
    {
#if UNITY_EDITOR
        if (EditorApplication.isCompiling || !Application.isPlaying)
            return;
#endif

        CaptureFixedYPosition();
        CacheRegistryServicesCold();
        TryRegisterHotSwapListener();
        ResolveSeaLevelOwners();
        ApplyFollowImmediately();
        TryRegisterForTick();

#if UNITY_EDITOR
        _editorPositionCached = false;
        _editorLastTargetCamera = null;

        if (!Application.isPlaying && followInEditMode)
        {
            EditorApplication.update -= EditorTick;
            EditorApplication.update += EditorTick;
        }
#endif
    }

    private void OnDisable()
    {
        TryUnregisterFromTick();
        TryUnregisterHotSwapListener();
#if UNITY_EDITOR
        EditorApplication.update -= EditorTick;
#endif
    }

    public void OnGlobalRegistryServiceReplaced(
        GlobalRegistryServiceSlot serviceSlot,
        object previousService,
        object currentService)
    {
        if (serviceSlot == GlobalRegistryServiceSlot.AtmosphereRuntime)
            _cachedAtmosphereReadModel = currentService as IAtmosphereReadModel;
    }

    /// <inheritdoc />
    public void Tick(float deltaTime)
    {
        if (!Application.isPlaying || !followInPlayMode)
            return;

        QueueFollow();
    }

    public void LateFrameTick()
    {
        if (!_hasPendingFollowPosition)
            return;

        _hasPendingFollowPosition = false;
        transform.position = _pendingFollowPosition;
    }

    private void TryRegisterForTick()
    {
        if (_registeredForTick || !Application.isPlaying || GlobalRegistry.Dispatcher == null)
            return;

        _registeredForTick = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.Environment);
        _registeredForLateFrame = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Environment);
    }

    private void TryUnregisterFromTick()
    {
        if (!_registeredForTick)
            return;

        GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Environment);
        _registeredForTick = false;
        if (_registeredForLateFrame)
        {
            GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);
            _registeredForLateFrame = false;
        }

        _hasPendingFollowPosition = false;
    }

    private void ApplyFollow()
    {
        if (!TryResolveFollowPosition(out Vector3 targetPosition))
            return;

        transform.position = targetPosition;
    }

    private void QueueFollow()
    {
        if (!TryResolveFollowPosition(out Vector3 targetPosition))
            return;

        _pendingFollowPosition = targetPosition;
        _hasPendingFollowPosition = true;
    }

    private bool TryResolveFollowPosition(out Vector3 targetPosition)
    {
        targetPosition = default;
        Camera target = ResolveTargetCamera();
        if (target == null)
            return false;

        targetPosition = target.transform.position + positionOffset;
        if (!ShouldFollowVerticalPosition())
        {
            targetPosition.y = ResolveLockedY();
        }

        return true;
    }

    private void ApplyFollowImmediately()
    {
        if (Application.isPlaying)
        {
            if (followInPlayMode)
                ApplyFollow();
            return;
        }

        if (followInEditMode)
            ApplyFollow();
    }

    private float ResolveLockedY()
    {
        if (lockToSeaLevel)
            return ResolveSeaLevelY() + horizonVerticalOffset + positionOffset.y;

        CaptureFixedYPosition();
        return _fixedYPosition + positionOffset.y;
    }

    private bool ShouldFollowVerticalPosition()
    {
        if (followVerticalPosition)
            return true;

        return false;
    }

    private void CaptureFixedYPosition()
    {
        if (_fixedYCaptured)
            return;

        _fixedYPosition = transform.position.y;
        _fixedYCaptured = true;
    }

#if UNITY_EDITOR
    private void EditorTick()
    {
        if (EditorApplication.isCompiling || !Application.isPlaying)
        {
            EditorApplication.update -= EditorTick;
            return;
        }

        if (Application.isPlaying || !followInEditMode || this == null)
            return;

        if (!UnityEditorInternal.InternalEditorUtility.isApplicationActive)
            return;

        Camera target = ResolveTargetCamera();
        if (target == null)
            return;

        Vector3 targetPosition = target.transform.position + positionOffset;
        if (!ShouldFollowVerticalPosition())
            targetPosition.y = ResolveLockedY();

        Vector3 targetVisualDelta = targetPosition - _editorLastAppliedPosition;
        Vector3 selfVisualDelta = transform.position - targetPosition;
        if (_editorPositionCached &&
            ReferenceEquals(_editorLastTargetCamera, target) &&
            targetVisualDelta.sqrMagnitude <= 0.0001f &&
            selfVisualDelta.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        transform.position = targetPosition;
        _editorLastTargetCamera = target;
        _editorLastAppliedPosition = targetPosition;
        _editorPositionCached = true;
    }
#endif

    private Camera ResolveTargetCamera()
    {
        if (runtimeCamera != null)
        {
            CachePlayerMovementFromCamera(runtimeCamera);
            return runtimeCamera;
        }

        if (Application.isPlaying)
        {
            if (IsRuntimeGameplayCamera(_cachedResolvedCamera))
            {
                CachePlayerMovementFromCamera(_cachedResolvedCamera);
                return _cachedResolvedCamera;
            }

            if (GameBootstrapper.TryGetCurrentPlayerTransform(out Transform playerTransform) &&
                playerTransform != null)
            {
                CachePlayerMovement(playerTransform);

                Camera playerCamera = ResolveTaggedRuntimeMainCamera(playerTransform);
                if (playerCamera != null)
                {
                    _cachedResolvedCamera = playerCamera;
                    return _cachedResolvedCamera;
                }
            }

            Camera runtimeMainCamera = ResolveTaggedRuntimeMainCamera();
            if (runtimeMainCamera != null)
            {
                _cachedResolvedCamera = runtimeMainCamera;
                CachePlayerMovementFromCamera(runtimeMainCamera);
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

        if (!Application.isPlaying &&
            _cachedResolvedCamera != null &&
            _cachedResolvedCamera.enabled &&
            _cachedResolvedCamera.gameObject.activeInHierarchy)
        {
            CachePlayerMovementFromCamera(_cachedResolvedCamera);
            return _cachedResolvedCamera;
        }

        int cameraCount = Camera.GetAllCameras(_runtimeCameraBuffer);
        for (int i = 0; i < cameraCount; i++)
        {
            Camera candidate = _runtimeCameraBuffer[i];
            if (candidate != null && candidate.enabled && candidate.gameObject.activeInHierarchy)
            {
                _cachedResolvedCamera = candidate;
                CachePlayerMovementFromCamera(candidate);
                return _cachedResolvedCamera;
            }
        }

        return null;
    }

    private static bool IsRuntimeGameplayCamera(Camera camera)
    {
        return camera != null &&
               camera.cameraType != CameraType.SceneView &&
               camera.enabled &&
               camera.gameObject.activeInHierarchy &&
               camera.CompareTag("MainCamera");
    }

    private static Camera ResolveTaggedRuntimeMainCamera()
    {
        int cameraCount = Camera.GetAllCameras(_runtimeCameraBuffer);
        for (int i = 0; i < cameraCount; i++)
        {
            Camera candidate = _runtimeCameraBuffer[i];
            if (IsRuntimeGameplayCamera(candidate))
                return candidate;
        }

        return null;
    }

    private static Camera ResolveTaggedRuntimeMainCamera(Transform playerTransform)
    {
        if (playerTransform == null)
            return null;

        int cameraCount = Camera.GetAllCameras(_runtimeCameraBuffer);
        for (int i = 0; i < cameraCount; i++)
        {
            Camera candidate = _runtimeCameraBuffer[i];
            if (!IsRuntimeGameplayCamera(candidate))
                continue;

            Transform candidateTransform = candidate.transform;
            if (ReferenceEquals(candidateTransform, playerTransform) ||
                candidateTransform.IsChildOf(playerTransform))
            {
                return candidate;
            }
        }

        return null;
    }

    private void ResolveSeaLevelOwners()
    {
        ResolveAtmosphereReadModel();

        if (playerMovement != null)
            return;

        if (_cachedResolvedCamera != null)
        {
            CachePlayerMovementFromCamera(_cachedResolvedCamera);
            if (playerMovement != null)
                return;
        }

        if (GameBootstrapper.TryGetCurrentPlayerTransform(out Transform playerTransform) && playerTransform != null)
            CachePlayerMovement(playerTransform);
    }

    private IAtmosphereReadModel ResolveAtmosphereReadModel()
    {
        if (atmosphereManager != null)
            return atmosphereManager;

        return _cachedAtmosphereReadModel;
    }

    private void CacheRegistryServicesCold()
    {
        _cachedAtmosphereReadModel = GlobalRegistry.AtmosphereReadModel;
    }

    private void TryRegisterHotSwapListener()
    {
        if (_registeredHotSwapListener || !Application.isPlaying)
            return;

        _registeredHotSwapListener = GlobalRegistry.TryRegisterHotSwapListener(this);
    }

    private void TryUnregisterHotSwapListener()
    {
        if (!_registeredHotSwapListener)
            return;

        GlobalRegistry.TryUnregisterHotSwapListener(this);
        _registeredHotSwapListener = false;
    }

    private float ResolveSeaLevelY()
    {
        if (playerMovement != null)
            return playerMovement.CurrentWaterSurfaceY;

        IAtmosphereReadModel resolvedAtmosphere = ResolveAtmosphereReadModel();
        if (resolvedAtmosphere != null)
            return resolvedAtmosphere.SeaLevelY;

        return fallbackSeaLevelY;
    }

    private void CachePlayerMovementFromCamera(Camera targetCamera)
    {
        if (targetCamera == null)
            return;

        HectonPlayerMovement movement = ResolveComponentInParents<HectonPlayerMovement>(targetCamera.transform);
        if (movement != null)
            playerMovement = movement;
    }

    private void CachePlayerMovement(Transform playerTransform)
    {
        if (playerTransform == null)
            return;

        playerTransform.TryGetComponent(out playerMovement);
    }

    private static T ResolveComponentInParents<T>(Transform start) where T : Component
    {
        Transform current = start;
        while (current != null)
        {
            if (current.TryGetComponent(out T component))
                return component;

            current = current.parent;
        }

        return null;
    }
}
