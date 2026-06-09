using UnityEngine;
using Hecton8.Bootstrap;
using Hecton8.Atmosphere;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Gameplay;
using Unity.Mathematics;

#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
[DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Environment/Sky System Follow Camera")]
public sealed class SkySystemFollowCamera : MonoBehaviour, IUpdatable, ILateFrameTickable, IGlobalRegistryHotSwapListener
{
    private const float DefaultSeaLevelY = OceanSurfaceAtmosphereConstants.DefaultSeaLevel;

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
    [SerializeField] private float fallbackSeaLevelY = DefaultSeaLevelY;
    [Tooltip("Manual Y trim applied after resolving the live sea level. Use this only if the authored sky horizon needs a small scene-specific offset.")]
    [SerializeField] private float horizonVerticalOffset = 0f;
    [Tooltip("Optional position offset applied after resolving the follow target.")]
    [SerializeField] private Vector3 positionOffset = Vector3.zero;
    private Camera _cachedResolvedCamera;
    private float _fixedYPosition;
    private bool _fixedYCaptured;
    private Camera _editorLastTargetCamera;
    private Vector3 _editorLastAppliedPosition;
    private bool _editorPositionCached;
    private bool _registeredForTick;
    private bool _registeredForLateFrame;
    private bool _registeredHotSwapListener;
    private bool _runtimeActive;
    private Vector3 _pendingFollowPosition;
    private bool _hasPendingFollowPosition;
    private IAtmosphereReadModel _cachedAtmosphereReadModel;
    private IPlayerRuntimeContext _cachedPlayerContext;
    private IHectonOceanKinematicsService _cachedOceanKinematicsService;

    private void OnEnable()
    {
#if UNITY_EDITOR
        if (EditorApplication.isCompiling)
            return;
#endif

        _runtimeActive = Application.isPlaying;
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
        _runtimeActive = false;
        _cachedAtmosphereReadModel = null;
        _cachedPlayerContext = null;
        _cachedOceanKinematicsService = null;
        playerMovement = null;
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
        {
            CacheAtmosphereReadModel(currentService as IAtmosphereReadModel);
            return;
        }

        if (serviceSlot == GlobalRegistryServiceSlot.OceanKinematics)
        {
            _cachedOceanKinematicsService = currentService as IHectonOceanKinematicsService;
            return;
        }

        if (serviceSlot == GlobalRegistryServiceSlot.Player)
            CachePlayerContext(currentService as IPlayerRuntimeContext);
    }

    /// <inheritdoc />
    public void Tick(float deltaTime)
    {
        if (!_runtimeActive || !followInPlayMode)
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
        if (_registeredForTick || !_runtimeActive || GlobalRegistry.Dispatcher == null)
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
        if (EditorApplication.isCompiling)
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
        if (IsExplicitFollowCameraUsable(runtimeCamera))
        {
            return runtimeCamera;
        }

        if (Application.isPlaying)
        {
            if (IsRuntimeGameplayCamera(_cachedResolvedCamera))
                return _cachedResolvedCamera;

            Camera cachedPlayerCamera = TryResolveCachedPlayerCamera();
            if (cachedPlayerCamera != null)
            {
                _cachedResolvedCamera = cachedPlayerCamera;
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

    private static bool IsExplicitFollowCameraUsable(Camera camera)
    {
        return camera != null &&
               camera.enabled &&
               camera.gameObject.activeInHierarchy;
    }

    private void ResolveSeaLevelOwners()
    {
        ResolveAtmosphereReadModel();

        if (IsPlayerMovementUsable(playerMovement))
            return;
        playerMovement = null;

        IPlayerRuntimeContext playerContext = ResolvePlayerContext();
        if (playerContext != null && IsPlayerMovementUsable(playerContext.PlayerMovement))
        {
            playerMovement = playerContext.PlayerMovement;
            return;
        }

        if (IsExplicitFollowCameraUsable(runtimeCamera))
        {
            CachePlayerMovementFromCamera(runtimeCamera);
            if (playerMovement != null)
                return;
        }

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
        if (IsAtmosphereReadModelUsable(atmosphereManager))
            return atmosphereManager;

        if (!IsAtmosphereReadModelUsable(_cachedAtmosphereReadModel))
            _cachedAtmosphereReadModel = null;

        return _cachedAtmosphereReadModel;
    }

    private void CacheRegistryServicesCold()
    {
        CacheAtmosphereReadModel(GlobalRegistry.AtmosphereReadModel);
        CachePlayerContext(GlobalRegistry.Player);
        _cachedOceanKinematicsService = GlobalRegistry.OceanKinematics;
    }

    private void CacheAtmosphereReadModel(IAtmosphereReadModel readModel)
    {
        _cachedAtmosphereReadModel = IsAtmosphereReadModelUsable(readModel) ? readModel : null;
    }

    private static bool IsAtmosphereReadModelUsable(IAtmosphereReadModel readModel)
    {
        if (readModel == null)
            return false;

        if (readModel is Behaviour behaviour)
            return behaviour != null && behaviour.isActiveAndEnabled;

        return true;
    }

    private void CachePlayerContext(IPlayerRuntimeContext playerContext)
    {
        _cachedPlayerContext = IsPlayerContextUsable(playerContext) ? playerContext : null;
    }

    private IPlayerRuntimeContext ResolvePlayerContext()
    {
        if (!IsPlayerContextUsable(_cachedPlayerContext))
            _cachedPlayerContext = null;

        return _cachedPlayerContext;
    }

    private static bool IsPlayerContextUsable(IPlayerRuntimeContext playerContext)
    {
        if (playerContext == null)
            return false;

        if (playerContext is Behaviour behaviour)
            return behaviour != null && behaviour.isActiveAndEnabled;

        return true;
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
        if (IsPlayerMovementUsable(playerMovement))
        {
            float movementSurfaceY = playerMovement.CurrentWaterSurfaceY;
            if (TryResolveSeaLevelY(movementSurfaceY, out float movementSeaLevelY))
                return movementSeaLevelY;
        }
        else
        {
            playerMovement = null;
        }

        if (TryResolveOceanSeaLevelY(out float oceanSeaLevelY))
            return oceanSeaLevelY;

        IAtmosphereReadModel resolvedAtmosphere = ResolveAtmosphereReadModel();
        if (resolvedAtmosphere != null)
        {
            float atmosphereSeaLevelY = resolvedAtmosphere.SeaLevelY;
            if (TryResolveSeaLevelY(atmosphereSeaLevelY, out float resolvedSeaLevelY))
                return resolvedSeaLevelY;
        }

        return ResolveFallbackSeaLevelY();
    }

    private bool TryResolveOceanSeaLevelY(out float seaLevelY)
    {
        IHectonOceanKinematicsService oceanKinematicsService = _cachedOceanKinematicsService;
        IHectonOceanKinematics oceanKinematics = oceanKinematicsService != null && oceanKinematicsService.IsInitialized
            ? oceanKinematicsService.ActiveProvider
            : null;
        if (oceanKinematics != null &&
            oceanKinematics.IsAvailable &&
            TryResolveOceanSeaLevelY(oceanKinematics.SeaLevel, out seaLevelY))
        {
            return true;
        }

        seaLevelY = DefaultSeaLevelY;
        return false;
    }

    private float ResolveFallbackSeaLevelY()
    {
        return TryResolveSeaLevelY(fallbackSeaLevelY, out float seaLevelY)
            ? seaLevelY
            : DefaultSeaLevelY;
    }

    private static bool TryResolveSeaLevelY(float candidateSeaLevelY, out float seaLevelY)
    {
        if (math.isfinite(candidateSeaLevelY) &&
            math.abs(candidateSeaLevelY) > 0.0001f &&
            math.abs(candidateSeaLevelY) <= Hecton8.World.WorldWaterLevelCalibrationMath.MaximumAbsoluteWaterLevelY)
        {
            seaLevelY = candidateSeaLevelY;
            return true;
        }

        seaLevelY = DefaultSeaLevelY;
        return false;
    }

    private static bool TryResolveOceanSeaLevelY(float candidateSeaLevelY, out float seaLevelY)
    {
        if (math.isfinite(candidateSeaLevelY) &&
            math.abs(candidateSeaLevelY) <= Hecton8.World.WorldWaterLevelCalibrationMath.MaximumAbsoluteWaterLevelY)
        {
            seaLevelY = candidateSeaLevelY;
            return true;
        }

        seaLevelY = DefaultSeaLevelY;
        return false;
    }

    private void CachePlayerMovementFromCamera(Camera targetCamera)
    {
        if (targetCamera == null)
            return;

        HectonPlayerMovement movement = ResolveComponentInParents<HectonPlayerMovement>(targetCamera.transform);
        if (IsPlayerMovementUsable(movement))
            playerMovement = movement;
    }

    private void CachePlayerMovement(Transform playerTransform)
    {
        if (playerTransform == null)
            return;

        playerTransform.TryGetComponent(out HectonPlayerMovement movement);
        playerMovement = IsPlayerMovementUsable(movement) ? movement : null;
    }

    private Camera TryResolveCachedPlayerCamera()
    {
        IPlayerRuntimeContext playerContext = ResolvePlayerContext();
        if (playerContext == null)
            return null;

        Camera playerCamera = playerContext.PlayerCamera;
        if (!IsRuntimeGameplayCamera(playerCamera))
            return null;

        HectonPlayerMovement movement = playerContext.PlayerMovement;
        if (IsPlayerMovementUsable(movement))
            playerMovement = movement;
        return playerCamera;
    }

    private static bool IsPlayerMovementUsable(HectonPlayerMovement movement)
    {
        return movement != null && movement.isActiveAndEnabled;
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
