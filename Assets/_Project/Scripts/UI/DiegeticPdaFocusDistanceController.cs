using Hecton8.Core;
using Hecton8.Core.Contracts;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Hecton8.UI
{
    /// <summary>
    /// PDA close-focus controller. Performs at most one SDF focus probe per frame while armed.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DiegeticPdaFocusDistanceController : MonoBehaviour, ILateFrameTickable, IGlobalRegistryHotSwapListener
    {
        [SerializeField] private Camera targetCamera;
        [SerializeField] private Volume targetVolume;
        [SerializeField] private LayerMask pdaLayerMask = ~0;
        [SerializeField] private bool focusActiveOnEnable;
        [SerializeField] private bool disableDepthOfFieldWhenNoHit = true;
        [SerializeField, Min(0.05f)] private float maxDistanceMeters = 1.25f;
        [SerializeField, Min(0.01f)] private float minFocusDistanceMeters = 0.08f;
        [SerializeField, Min(0.01f)] private float maxFocusDistanceMeters = 4f;
        [SerializeField, Min(0.01f)] private float blendSharpness = 18f;

        private DepthOfField _depthOfField;
        private Transform _cameraTransform;
        private IPlayerRuntimeContext _cachedPlayerContext;
        private IVoxelSonarSdfReadModel _cachedVoxelSdfReadModel;
        private bool _registered;
        private bool _hotSwapListenerRegistered;
        private bool _focusActive;
        private bool _targetCameraFromPlayerContext;
        private bool _targetVolumeFromCameraContext;
        private int _lastResolveFrame = -1;
        private int _nextResolveFrame;
        private float _lastFocusDistance;

        public bool FocusActive => _focusActive;
        public int LastResolveFrame => _lastResolveFrame;
        public float LastFocusDistance => _lastFocusDistance;

        private void OnEnable()
        {
            CacheRegistryServicesCold();
            TryRegisterHotSwapListener();
            ResolveReferences();
            _focusActive = focusActiveOnEnable;
            TryRegisterTick();
        }

        private void OnDisable()
        {
            TryUnregisterHotSwapListener();
            TryUnregisterTick();
            _focusActive = false;
        }

        private void OnDestroy()
        {
            TryUnregisterHotSwapListener();
            TryUnregisterTick();
        }

        public void SetFocusActive(bool active)
        {
            _focusActive = active;
            if (active)
            {
                ResolveReferences();
                _nextResolveFrame = 0;
                _lastResolveFrame = -1;
            }
            else
            {
                if (disableDepthOfFieldWhenNoHit && _depthOfField != null)
                    _depthOfField.active = false;
            }
        }

        public void LateFrameTick()
        {
            if (!_focusActive)
                return;

            int frame = Hecton8.Core.SystemDispatcher.CurrentFrameIndex;
            if (_cameraTransform == null || _depthOfField == null)
                return;

            if (_lastResolveFrame == frame)
                return;

            _lastResolveFrame = frame;
            if (!TryResolveFocusDistance(out float resolvedDistance))
            {
                if (disableDepthOfFieldWhenNoHit)
                    _depthOfField.active = false;

                return;
            }

            float targetDistance = math.clamp(resolvedDistance, minFocusDistanceMeters, maxFocusDistanceMeters);
            float current = _lastFocusDistance > 0f ? _lastFocusDistance : targetDistance;
            float deltaTime = math.max(0f, SystemDispatcher.CurrentFrameDeltaTime);
            float blend = ResolvePadeApproach01(blendSharpness, math.max(0f, deltaTime));
            _lastFocusDistance = math.lerp(current, targetDistance, blend);
            _depthOfField.focusDistance.value = _lastFocusDistance;
            _depthOfField.active = true;
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.Dispatcher)
            {
                TryUnregisterTick();
                if (isActiveAndEnabled)
                {
                    if (currentService != null)
                        TryRegisterTick();
                }
                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.VoxelEngineRuntime)
            {
                _cachedVoxelSdfReadModel = currentService as IVoxelSonarSdfReadModel;
                return;
            }

            if (serviceSlot != GlobalRegistryServiceSlot.Player)
                return;

            IPlayerRuntimeContext previousContext = previousService as IPlayerRuntimeContext;
            IPlayerRuntimeContext currentContext = currentService as IPlayerRuntimeContext;
            _cachedPlayerContext = currentContext;

            Camera previousCamera = previousContext != null ? previousContext.PlayerCamera : null;
            Camera currentCamera = currentContext != null ? currentContext.PlayerCamera : null;
            if (targetCamera == null || _targetCameraFromPlayerContext || ReferenceEquals(targetCamera, previousCamera))
            {
                targetCamera = currentCamera;
                _targetCameraFromPlayerContext = currentCamera != null;
                _cameraTransform = currentCamera != null ? currentCamera.transform : null;
                if ((targetVolume == null || _targetVolumeFromCameraContext) && currentCamera != null)
                {
                    targetVolume = ResolveCameraVolume(currentCamera.transform);
                    _targetVolumeFromCameraContext = targetVolume != null;
                    _depthOfField = null;
                }
                else if (currentCamera == null && _targetVolumeFromCameraContext)
                {
                    targetVolume = null;
                    _targetVolumeFromCameraContext = false;
                    _depthOfField = null;
                }
                if (_focusActive)
                    _nextResolveFrame = 0;
            }
        }

        private void CacheRegistryServicesCold()
        {
            _cachedPlayerContext = GlobalRegistry.Player;
            _cachedVoxelSdfReadModel = GlobalRegistry.VoxelSonarSdf;
        }

        private void ResolveReferences()
        {
            _nextResolveFrame = Hecton8.Core.SystemDispatcher.CurrentFrameIndex + 30;

            IPlayerRuntimeContext playerContext = _cachedPlayerContext;
            if (targetCamera == null && playerContext != null)
            {
                targetCamera = playerContext.PlayerCamera;
                _targetCameraFromPlayerContext = targetCamera != null;
            }
            if (targetCamera == null)
            {
                targetCamera = ResolveNearestParentCamera(transform);
                _targetCameraFromPlayerContext = false;
            }
            _cameraTransform = targetCamera != null ? targetCamera.transform : null;

            if (targetVolume == null && targetCamera != null)
            {
                targetVolume = ResolveCameraVolume(targetCamera.transform);
                _targetVolumeFromCameraContext = targetVolume != null;
            }
            if (targetVolume == null)
            {
                targetVolume = ResolveNearestParentVolume(transform);
                _targetVolumeFromCameraContext = false;
            }

            if (_depthOfField == null && targetVolume != null && targetVolume.profile != null)
                targetVolume.profile.TryGet(out _depthOfField);
        }

        private void TryRegisterTick()
        {
            if (_registered || !Application.isPlaying)
                return;

            _registered = SystemDispatcher.Register((ILateFrameTickable)this, PriorityLayer.UI);
        }

        private void TryRegisterHotSwapListener()
        {
            if (_hotSwapListenerRegistered || !Application.isPlaying)
                return;

            _hotSwapListenerRegistered = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_hotSwapListenerRegistered)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _hotSwapListenerRegistered = false;
        }

        private void TryUnregisterTick()
        {
            if (!_registered)
                return;

            SystemDispatcher.UnregisterLateFrameTickableDirect(this, PriorityLayer.UI);
            _registered = false;
        }

        private static float ResolvePadeApproach01(float sharpness, float dt)
        {
            float x = math.min(math.max(0f, sharpness) * math.max(0f, dt), 8f);
            float x2 = x * x;
            float expNegApprox = math.rcp(1f + x + (0.48f * x2) + (0.235f * x2 * x));
            return math.saturate(1f - expNegApprox);
        }

        private bool TryResolveFocusDistance(out float distanceMeters)
        {
            distanceMeters = 0f;
            if (_cameraTransform == null ||
                !IsFiniteVector(_cameraTransform.position) ||
                !IsFiniteVector(_cameraTransform.forward) ||
                !math.isfinite(maxDistanceMeters) ||
                maxDistanceMeters <= 0f ||
                !IncludesAnyLayer(pdaLayerMask.value, HectonLayerMasks.VoxelCaveLayerMask | HectonLayerMasks.VoxelProxyLayerMask))
            {
                return false;
            }

            IVoxelSonarSdfReadModel readModel = _cachedVoxelSdfReadModel;
            if (readModel == null)
                return false;

            Vector3 origin = _cameraTransform.position;
            Vector3 forward = _cameraTransform.forward;
            float3 origin3 = new float3(origin.x, origin.y, origin.z);
            float3 direction3 = math.normalizesafe(new float3(forward.x, forward.y, forward.z), new float3(0f, 0f, 1f));
            float stepMeters = ResolveFocusSdfStepMeters(maxDistanceMeters);
            if (!VoxelSonarSdfMath.TryResolveNearestSdfSurface(
                    readModel,
                    origin3,
                    direction3,
                    maxDistanceMeters,
                    stepMeters,
                    out VoxelSonarSdfRaycastHit hit) ||
                (hit.Flags & VoxelSonarSdfRaycastHit.FlagHit) == 0u ||
                !math.isfinite(hit.Distance) ||
                hit.Distance <= 0f)
            {
                return false;
            }

            distanceMeters = hit.Distance;
            return true;
        }

        private static float ResolveFocusSdfStepMeters(float maxDistance)
        {
            float quality = math.saturate(math.isfinite(HomeostasisBrain.GlobalQualityWeight) ? HomeostasisBrain.GlobalQualityWeight : 1f);
            return math.max(0.025f, math.min(maxDistance, math.lerp(0.18f, 0.045f, ResolvePadeApproach01(3f, quality))));
        }

        private static bool IncludesAnyLayer(int queryMask, int requiredMask)
        {
            return queryMask == -1 || (queryMask & requiredMask) != 0;
        }

        private static bool IsFiniteVector(Vector3 value)
        {
            return math.all(math.isfinite(new float3(value.x, value.y, value.z)));
        }

        private static Camera ResolveNearestParentCamera(Transform start)
        {
            for (Transform current = start; current != null; current = current.parent)
            {
                if (current.TryGetComponent(out Camera camera))
                    return camera;
            }

            return null;
        }

        private static Volume ResolveNearestParentVolume(Transform start)
        {
            for (Transform current = start; current != null; current = current.parent)
            {
                if (current.TryGetComponent(out Volume volume))
                    return volume;
            }

            return null;
        }

        private static Volume ResolveCameraVolume(Transform cameraTransform)
        {
            if (cameraTransform == null)
                return null;

            if (cameraTransform.TryGetComponent(out Volume volume))
                return volume;

            return ResolveFirstChildVolume(cameraTransform);
        }

        private static Volume ResolveFirstChildVolume(Transform root)
        {
            return root.GetComponentInChildren<Volume>(false);
        }
    }
}
